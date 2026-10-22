using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Agents.Goals;
using IsometricFurTradeGame.Scripts.Ecology;
using IsometricFurTradeGame.Scripts.Economy;
using IsometricFurTradeGame.Scripts.Items;

namespace IsometricFurTradeGame.Scripts.Core;

public enum Tier { Live, Background }

// Concentric simulation fidelity (design doc 18.2). Everyone near a player
// - any player, in a shared world - is simulated as before: a goal stack,
// pathfinding, needs, a body on the screen. Everyone else is a row of
// numbers that the post or the camp resolves once a day: they eat from the
// cache, they do the work their role does, and what they produce goes into
// the stores. They are still people - the same names, the same memories,
// the same letters and debts - they are just not being walked about.
//
// Nothing is despawned: the node stays, so every reference by name, every
// grudge, every piece of mail and the save file all keep working. The body
// simply stops moving, stops drawing, and resolves in batches.
public static class Tiers
{
    public const float LiveRadius = 3200f;       // promote inside this
    public const float BackgroundRadius = 4200f; // demote outside this (hysteresis)
    private static float _timer;
    private static int _lastBatchDay = -1;

    public static int LiveCount, BackgroundCount;
    private static readonly bool Perf = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--perf") >= 0;

    public static void Tick(float delta)
    {
        if (!Net.Net.IsServer) return;
        _timer -= delta;
        if (_timer <= 0f)
        {
            _timer = 1.0f;
            Assign();
        }
        var cal = GameCalendar.Instance;
        if (cal != null) Economy.Lacrosse.Tick(cal);
        if (cal != null && cal.Day != _lastBatchDay)
        {
            _lastBatchDay = cal.Day;
            ulong t0 = Time.GetTicksUsec();
            World.GooseHunt.Tick();
            Reckoning.Spring();
            Economy.Gardens.Tick();
            World.Towns.Day();
            Agents.Rivals.Tick();
            World.Brigades.Season();
            World.Brigades.Tick();
            World.Convoys.Tick();
            ulong t1 = Time.GetTicksUsec();
            DailyBatch();
            if (Perf) GD.Print($"[perf] day {cal.Day}: the day's round {(t1 - t0) / 1000f:0.0} ms, the batch's first slice {(Time.GetTicksUsec() - t1) / 1000f:0.0} ms, {_batch.Count} to go");
        }
        else if (_batch.Count > 0) BatchSlice();
    }

    private static float DistanceToNearestPlayer(Vector2 p)
    {
        float best = float.MaxValue;
        foreach (var pc in Sim.Players())
        {
            // A dead player is still an anchor: his body is there and he will be back.
            if (pc == null || !GodotObject.IsInstanceValid(pc)) continue;
            best = Mathf.Min(best, pc.GlobalPosition.DistanceTo(p));
        }
        return best;
    }

    private static void Assign()
    {
        LiveCount = 0; BackgroundCount = 0;
        foreach (var n in Sim.Npcs)
        {
            if (n == null || !GodotObject.IsInstanceValid(n)) continue;
            // A man away with a brigade is neither at the post nor out of the
            // world: the brigade speaks for him until he lands.
            if (n.Away) { n.Tier = Tier.Background; BackgroundCount++; continue; }
            // Where a background man is reckoned to be: at his own door.
            var where = n.Tier == Tier.Background && n.HomeStores != null ? World.IsoGrid.ToWorld(n.HomeStores.AccessCell) : n.GlobalPosition;
            float d = DistanceToNearestPlayer(where);
            if (n.Tier == Tier.Live && d > BackgroundRadius) Demote(n);
            else if (n.Tier == Tier.Background && d < LiveRadius) Promote(n);
            if (n.Tier == Tier.Live) LiveCount++; else BackgroundCount++;
        }
    }

    // Out of sight: stop the body, put him at his household's door, and let
    // the daily batch speak for him.
    private static void Demote(Npc n)
    {
        if (n.IsDead || n.HomeStores == null) return;           // bodies and strays stay as they are
        if (n.HeldBy != null || n.Following != null) return;     // prisoners and companions keep their own thread
        // A man on a journey is the story; he walks it himself. A hunting trip
        // to a far site is a journey too: demoting him used to park him back at
        // his own door with the day's work undone, which is why nothing ever
        // came back from the goose marsh.
        if (n.Brain.Has<CourierGoal>() || n.Brain.Has<RaidGoal>() || n.Brain.Has<AmbushGoal>() || n.Brain.Has<RoadGoal>() || n.Brain.Has<RescueGoal>() || n.Brain.Has<LookForGoal>()
            || n.Brain.Has<HuntSiteGoal>() || n.Brain.Has<BuffaloRunGoal>() || n.Brain.Has<HorseRaidGoal>() || n.Brain.Has<WarPartyGoal>())
            return;
        n.Tier = Tier.Background;
        n.Brain.Clear(n);
        n.StopMoving();
        n.Velocity = Vector2.Zero;
        n.GlobalPosition = World.IsoGrid.ToWorld(n.StandCellNear(n.HomeStores.AccessCell));
        n.Visible = false;
        n.ProcessMode = Node.ProcessModeEnum.Disabled;
        Net.Net.I?.AnnounceTier(n, true);
    }

    private static void Promote(Npc n)
    {
        n.Tier = Tier.Live;
        n.ProcessMode = Node.ProcessModeEnum.Inherit;
        n.Visible = true;
        n.Brain.Clear(n);
        Net.Net.I?.AnnounceTier(n, false);
    }

    // One day of a background person's life, in numbers (design doc 18.2).
    // The day is worked through a slice at a time (2 ms a frame) so that a
    // big country does not stop the clock once a day; a day still in hand
    // when the next one comes is finished first.
    private static readonly System.Collections.Generic.Queue<Npc> _batch = new();
    private static bool _rushing;
    private const ulong SliceUsec = 2000;

    private static void DailyBatch()
    {
        while (_batch.Count > 0) BatchOne(_batch.Dequeue());
        var cal = GameCalendar.Instance;
        _rushing = cal.MotionScale > GameCalendar.LazyMotion * 0.75f;   // the 6 of the old baseline
        foreach (var n in Sim.Npcs) _batch.Enqueue(n);
        BatchSlice();
    }

    private static void BatchSlice()
    {
        ulong t0 = Time.GetTicksUsec();
        while (_batch.Count > 0 && Time.GetTicksUsec() - t0 < SliceUsec)
        {
            var n = _batch.Dequeue();
            ulong t1 = Time.GetTicksUsec();
            BatchOne(n);
            if (Perf && Time.GetTicksUsec() - t1 > 10000 && GodotObject.IsInstanceValid(n)) GD.Print($"[perf] {n.DisplayName} ({n.Role}, {n.HomeStores?.Title}) took {(Time.GetTicksUsec() - t1) / 1000f:0.0} ms");
        }
    }

    private static void BatchOne(Npc n)
    {
        var cal = GameCalendar.Instance;
        // Foul weather is local: a blizzard at York does not keep the Mandan indoors.
        bool Foul(Economy.Household h) => cal.Weather.FoulAt(World.IsoGrid.ToWorld(h.AccessCell));
        bool rushing = _rushing;
        if (n == null || !GodotObject.IsInstanceValid(n) || n.IsDead) return;
        if (n.Away) return;   // the brigade feeds its own
        // A town house buys its bread at the market when it runs low.
        if (n.HomeStores is World.TownHousehold townHome) World.Towns.Provision(townHome);
        // A man standing in his own yard with food on the shelf does not
        // starve (design doc P.5), whatever the clock is doing - a day can
        // pass in a couple of seconds under fast-forward, too fast for him
        // to walk in and eat.
        // A day that passes in two seconds is a day in which nobody could
        // have walked anywhere (design doc 18.2): under heavy fast-forward
        // everyone's day is resolved in numbers, not on foot, or the whole
        // country starves with food on the shelf.
        if (n.Tier != Tier.Background && !rushing)
        {
            // Even at an ordinary clock, a man in his own yard with food on
            // the shelf does not starve (design doc P.5).
            // A farmer the player is watching has worked his fields today too.
            if (n.Role == Role.Farmer && n.HomeStores is World.TownHousehold farm && !Foul(farm)) Farm(n, farm);
            if (n.Needs.Hunger < 25f)
            {
                if (n.HomeStores != null && n.GlobalPosition.DistanceTo(World.IsoGrid.ToWorld(n.HomeStores.AccessCell)) < 900f)
                    n.HomeStores.TakeRation(n, 1);
                n.Eat();
            }
            return;
        }
        var home = n.HomeStores;
        if (home == null) return;

        // A day's worth of being alive, which a hidden body does not get
        // from the physics loop: the fever runs its course or ends, and
        // the days without fresh meat mount up.
        n.DaysSinceFresh += 1f;
        if (n.IsSick)
        {
            n.SickDaysLeft -= 1f;
            n.Needs.Health = Mathf.Max(0f, n.Needs.Health - 8f);
            if (n.SickDaysLeft <= 0f) { n.Immune = true; Sim.Notice($"{n.Describe()} is over the fever."); }
        }
        else if (n.HasScurvy) n.Needs.Health = Mathf.Max(0f, n.Needs.Health - 3f);

        // A town house's cellar: potatoes and roots with the bread every few
        // days, which is what kept the settlers clear of the scurvy.
        if (home is World.TownHousehold && n.DaysSinceFresh > 5f && home.Stores.Remove(ItemId.Roots)) { n.Inventory.Add(ItemId.Roots); n.Consume(ItemId.Roots); }
        // Eat and drink from the cache, and actually eat it: the scurvy
        // turns on what a man swallowed, not on a ration being issued.
        bool ate = home.TakeRation(n, 1) && n.Eat();
        if (!ate) ate = n.Eat();   // whatever is in his own pack
        if (!ate) n.Needs.Hunger = Mathf.Max(0f, n.Needs.Hunger - 26f);
        else n.Needs.Hunger = Mathf.Max(n.Needs.Hunger, 60f);
        // Snow melted at the fire does for water where there is wood.
        // Winter is where the house is, not at York.
        bool frozen = Sim.Terrain?.FrozenAt(World.IsoGrid.ToWorld(home.AccessCell)) ?? cal.IsFrozen;
        n.Needs.Thirst = home.Stores.Has(ItemId.Water) || !frozen || home.Stores.Has(ItemId.Firewood) ? 100f : Mathf.Max(20f, n.Needs.Thirst - 10f);
        if (home.Stores.Has(ItemId.Water)) home.Stores.Remove(ItemId.Water);
        // A fire in the house keeps him warm; a cold hearth doesn't.
        bool warm = home.Stores.Has(ItemId.Firewood) || !frozen;
        if (warm) { n.Needs.Warmth = 100f; home.Stores.TakeUpTo(ItemId.Firewood, 1); }
        else n.Needs.Warmth = Mathf.Max(0f, n.Needs.Warmth - 30f);
        // Short commons, not a quiet wipe (design doc 17.1): a house with
        // nothing gets thin over weeks, and the country hears of it in time
        // for somebody to do something.
        if (n.Needs.Warmth <= 0f) n.Needs.Health = Mathf.Max(0f, n.Needs.Health - 10f);
        else if (n.Needs.Hunger <= 0f) { n.Needs.Health = Mathf.Max(0f, n.Needs.Health - 3f); ShortCommons(home); }
        if (ate) _hungryDays.Remove(home);   // the spell is over when there is food again
        else if (n.Needs.Health < 100f) n.Needs.Health = Mathf.Min(100f, n.Needs.Health + 6f);
        if (n.Needs.Health <= 0f)
        {
            string took = n.IsSick ? "the fever" : n.Needs.Warmth <= 0f ? "cold" : n.HasScurvy && n.Needs.Hunger > 1f ? "the scurvy" : "want";
            n.DieQuietly();
            Sim.Notice($"{n.DisplayName} has died of {took} at {home.Title}.");
            return;
        }

        if (Foul(home)) return;   // nobody works in a blizzard
        // A camp of the wider country feeds itself in its own weekly sum
        // (Economy/CountryBand): its people are not at York's grounds.
        if (home is Economy.CountryBand) return;

        // A day's work, by what the man is for.
        switch (n.Role)
        {
            case Role.Labourer:
            case Role.Voyageur:
                // A town's labourer is a carter, a boatman, a porter on wages: he
                // brings in the house's wood and buys his bread, he does not hunt.
                if (home is World.TownHousehold) { if (home.Stores.Count(ItemId.Firewood) < 30) home.Stores.Add(ItemId.Firewood, 4); break; }
                Work(n, home);
                break;
            case Role.CreeHunter:
                Hunt(n, home);
                break;
            case Role.Woman:
            case Role.CreeWoman:
            case Role.Wife:
            case Role.CreeDaughter:
                House(n, home);
                break;
            case Role.Farmer:
                if (home is World.TownHousehold farmHome) Farm(n, farmHome);
                break;
            case Role.Soldier:
            case Role.Merchant:
            case Role.Artisan:
            case Role.Officer:
            case Role.Priest:
                // The garrison's fatigue parties and the townsmen bring in their own wood;
                // the rest of the day is the trade, which feeds the house through the market.
                if (home.Stores.Count(ItemId.Firewood) < 30) home.Stores.Add(ItemId.Firewood, 4);
                break;
            case Role.Factor:
            case Role.Postmaster:
            case Role.NwcPartner:
            case Role.CreeChief:
                // The leader's day is his books; his orders wait for a live beat.
                break;
        }
    }

    // How long each house has been on short commons, and a word out when it
    // has gone on long enough to be news.
    private static readonly System.Collections.Generic.Dictionary<Economy.Household, int> _hungryDays = new();
    private static readonly System.Collections.Generic.Dictionary<Economy.Household, int> _hungryCountedOn = new();
    private static void ShortCommons(Economy.Household home)
    {
        // Once a day for the house, not once for every man in it.
        int today = GameCalendar.Instance?.Day ?? 0;
        if (_hungryCountedOn.TryGetValue(home, out int on) && on == today) return;
        _hungryCountedOn[home] = today;
        int days = (_hungryDays.TryGetValue(home, out int had) ? had : 0) + 1;
        _hungryDays[home] = days;
        if (days == 8 || days % 30 == 0)
            Sim.Headline($"Word comes that {home.Title} is on short commons: nothing in the store and the men going thin.");
    }

    private static readonly System.Collections.Generic.HashSet<string> _toldOf = new();

    private static int Mouths(Economy.Household home)
    {
        return Mathf.Max(1, home.Alive());
    }

    private static void Work(Npc n, Economy.Household home)
    {
        // Ship time at the bay comes before everything: the cargo ashore, then
        // the year's furs aboard, two pieces a man a day (Economy/Shipping).
        if (Economy.Shipping.WorkTheShip(n, home)) return;
        // Wood first, then whatever the post is short of.
        if (home.Stores.Count(ItemId.Firewood) < 30) { home.Stores.Add(ItemId.Firewood, n.Skills.Woodcraft >= 0.5f ? 5 : 4); n.Skills.Learn(ref n.Skills.Woodcraft, 0.004f); return; }
        // Traps if there is a line to work; otherwise out after meat. A man
        // whose trapline comes to nothing does not stand about all day.
        if (home is FortLedger f && f.Stores.Has(ItemId.SteelTrap) && GameCalendar.Instance.CurrentSeason == Season.Winter && Trap(n, home)) return;
        Hunt(n, home);
    }

    private static void Hunt(Npc n, Economy.Household home)
    {
        var eco = Sim.Ecology;
        if (eco == null) return;
        // Everyone hunts by what his house knows: a band's ledger has learned
        // every ground in its own country, a post knows what it has been told.
        // When a house is hungry the men go looking further - but only as far
        // as their own country, not to the thickest herd on the continent.
        var known = home;
        bool desperate = home.FoodDays(Mouths(home)) < 8f;
        if (desperate)
            foreach (var near in World.WorldLayout.SitesNear(home.AccessCell, World.WorldLayout.HomeCountryPx))
                home.Learn(near.Name, near.Cell);
        var site = eco.BestBigGame(known) ?? eco.BestSite(Species.Goose, known) ?? eco.BestSite(Species.Beaver, known);
        if (site == null || site.Count < 1f)
        {
            // Snares along the bush: thin fare, and it comes off the hare and
            // ptarmigan grounds like anything else, so a camp that lives on
            // snares all winter thins them (design doc 16.3). Enough, though,
            // that a camp out of the player's sight does not quietly starve.
            int snared = (GD.Randf() < 0.75f + n.Skills.Hunting * 0.2f ? 1 : 0) + (GD.Randf() < 0.4f + n.Skills.Hunting * 0.4f ? 1 : 0);
            if (snared > 0)
            {
                var small = eco.BestSite(Species.Hare, known) ?? eco.BestSite(Species.Ptarmigan, known);
                if (small != null && small.Count >= snared)
                {
                    small.Take(snared);
                    for (int i = 0; i < snared; i++) SpeciesInfo.Yield(small.Species, home.Stores);
                }
                else home.Stores.Add(ItemId.Hare, snared);   // whatever the bush gives where there is no ground mapped
            }
            // A line through the ice is how an inland post got through March
            // (design doc 14.4, 16.1): thin, steady, and it keeps men alive.
            bool line = home.Stores.Has(ItemId.FishingLine) || n.Inventory.Has(ItemId.FishingLine) || home.Stores.Has(ItemId.FishingNet);
            if (line || !GameCalendar.Instance.IsFrozen)
            {
                int fish = (GD.Randf() < 0.65f ? 1 : 0) + (GD.Randf() < 0.35f + n.Skills.Hunting * 0.3f ? 1 : 0);
                if (home.Stores.Has(ItemId.FishingNet)) fish = fish * 2 + 1;
                if (fish > 0) home.Stores.Add(ItemId.Fish, fish);
            }
            return;
        }
        if (desperate && home is not Economy.BandLedger && site.Name != "" && !_toldOf.Contains(home.Title + site.Name))
        {
            _toldOf.Add(home.Title + site.Name);
            Sim.Notice($"{home.Title}: the men went looking and found the game at {site.Name}.");
        }
        float chance = Mathf.Clamp(site.Count / site.Capacity, 0.05f, 1f) * (0.16f + n.Skills.Hunting * 0.24f);
        if (GD.Randf() >= chance) return;
        // A gun kills cleaner and further, while there is powder for it.
        bool gun = n.Weapon == ItemId.Musket && n.UseCharge();
        site.Take(SpeciesInfo.PerKill(site.Species, gun));
        SpeciesInfo.Yield(site.Species, home.Stores, gun);
        n.Skills.Learn(ref n.Skills.Hunting, 0.004f);
        n.DaysSinceFresh = 0f;
    }

    private static bool Trap(Npc n, Economy.Household home)
    {
        var eco = Sim.Ecology;
        var site = eco?.BestSite(Species.Beaver, home);
        if (site == null) return false;
        var line = site.LineOf(home.Title);
        if (line == null || line.Traps == 0)
        {
            int traps = home.Stores.TakeUpTo(ItemId.SteelTrap, 4);
            if (traps > 0) site.SetTraps(home.Title, traps, n.Skills.Trapping);
            return traps > 0;
        }
        // The nightly catch already happens at the site; the man brings it in.
        int got = line.Caught; line.Caught = 0; line.HoursSinceCheck = 0f;
        for (int i = 0; i < got; i++) SpeciesInfo.Yield(Species.Beaver, home.Stores);
        if (got > 0) n.Skills.Learn(ref n.Skills.Trapping, 0.006f);
        return true;
    }

    // A day on a farm of the cote (World/Towns): the winter's wood, the
    // fields from the ploughing to the harvest, and the harvest itself, when
    // the family keeps its year's bread and corn and sells the rest at the
    // market. Off season the men hunt like anyone else.
    private const float FieldYield = 40f;   // pieces of food a day's field work comes to at the harvest
    private static void Farm(Npc n, World.TownHousehold home)
    {
        var cal = GameCalendar.Instance;
        int month = cal.Month;
        bool cold = month >= 10 || month <= 4;
        if (cold && home.Stores.Count(ItemId.Firewood) < 30) { home.Stores.Add(ItemId.Firewood, n.Skills.Woodcraft >= 0.5f ? 5 : 4); return; }
        if (month >= 4 && month <= 8) { home.FieldDays += 1f; return; }
        if (month >= 9 && home.HarvestYear < cal.Year)
        {
            home.HarvestYear = cal.Year;
            int crop = Mathf.RoundToInt(home.FieldDays * FieldYield);
            home.FieldDays = 0f;
            if (crop <= 0) { Sim.Notice($"{home.Title}: nothing was sown, and there is nothing to bring in."); return; }
            // Wheat, corn, and the potatoes and roots of the garden.
            home.Stores.Add(ItemId.Bread, crop / 2);
            home.Stores.Add(ItemId.Corn, crop / 4);
            home.Stores.Add(ItemId.Roots, crop - crop / 2 - crop / 4);
            // The year's food for the house, and the rest to market, in the same shares.
            int keep = home.Alive() * 400;
            int have = home.Stores.Count(ItemId.Bread) + home.Stores.Count(ItemId.Corn) + home.Stores.Count(ItemId.Roots);
            int spare = Mathf.Max(0, have - keep);
            var market = home.Town.Market;
            if (market != null && spare > 0)
                foreach (var (id, share) in new[] { (ItemId.Bread, 0.5f), (ItemId.Corn, 0.25f), (ItemId.Roots, 0.25f) })
                {
                    int n2 = Mathf.Min(Mathf.RoundToInt(spare * share), home.Stores.Count(id));
                    home.Stores.Remove(id, n2); market.Stores.Add(id, n2);
                }
            Sim.Notice($"{home.Title}: the harvest is in, {crop} pieces of wheat and corn, {spare} of them sold at the market.");
            return;
        }
        if (month >= 9) return;   // the fall work: the ploughing for next year, the stock
        Hunt(n, home);
    }

    private static void House(Npc n, Economy.Household home)
    {
        // The flight is salted down before any other work (design doc 14.1).
        if (home.Stores.Count(ItemId.Goose) >= 8)
        {
            int birds = home.Stores.TakeUpTo(ItemId.Goose, 24);
            int casks = birds / 8;
            home.Stores.Add(ItemId.Goose, birds - casks * 8);
            home.Stores.Add(ItemId.SaltGeese, casks);
            World.GooseHunt.Salted(home, casks);
            return;
        }
        // Wood for the fire comes before anything else in a frozen country:
        // a camp with no cordwood in the batch used to freeze to death out of
        // the player's sight.
        if (home.Stores.Count(ItemId.Firewood) < 12) { home.Stores.Add(ItemId.Firewood, 4); n.Skills.Learn(ref n.Skills.Woodcraft, 0.003f); return; }
        // Water, drying, pemmican: the work that keeps a household alive.
        if (home.Stores.Count(ItemId.Water) < 10 && home.Stores.Has(ItemId.Kettle)) { home.Stores.Add(ItemId.Water, 4); return; }
        if (home.Stores.Count(ItemId.Venison) >= 3)
        {
            int fresh = home.Stores.TakeUpTo(ItemId.Venison, 6);
            home.Stores.Add(ItemId.DriedMeat, fresh * 2 / 3);
            return;
        }
        if (home.Stores.Count(ItemId.DriedMeat) >= 8)
        {
            int dried = home.Stores.TakeUpTo(ItemId.DriedMeat, 8);
            home.Stores.Add(ItemId.Pemmican, dried * 3 / 4);
            return;
        }
        if (home.Stores.Count(ItemId.Firewood) < 12) home.Stores.Add(ItemId.Firewood, 3);
        else if (!GameCalendar.Instance.IsFrozen) home.Stores.Add(ItemId.Fish, GD.Randf() < 0.6f ? 2 : 1);
    }

    public static string Report() => $"live {LiveCount}, background {BackgroundCount}";
}
