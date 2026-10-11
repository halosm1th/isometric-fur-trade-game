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
        if (cal != null && cal.Day != _lastBatchDay)
        {
            _lastBatchDay = cal.Day;
            World.GooseHunt.Tick();
            Reckoning.Spring();
            Economy.Gardens.Tick();
            Agents.Rivals.Tick();
            World.Brigades.Season();
            World.Brigades.Tick();
            DailyBatch();
        }
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
            || n.Brain.Has<HuntSiteGoal>())
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
    private static void DailyBatch()
    {
        var cal = GameCalendar.Instance;
        bool foul = cal.Weather.Current is SkyState.Blizzard or SkyState.Storm;
        bool rushing = cal.MotionScale > 6f;
        // A copy: someone who dies of want in the batch leaves Sim.Npcs mid-loop.
        foreach (var n in new System.Collections.Generic.List<Npc>(Sim.Npcs))
        {
            if (n == null || !GodotObject.IsInstanceValid(n) || n.IsDead) continue;
            if (n.Away) continue;   // the brigade feeds its own
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
                if (n.Needs.Hunger < 25f)
                {
                    if (n.HomeStores != null && n.GlobalPosition.DistanceTo(World.IsoGrid.ToWorld(n.HomeStores.AccessCell)) < 900f)
                        n.HomeStores.TakeRation(n, 1);
                    n.Eat();
                }
                continue;
            }
            var home = n.HomeStores;
            if (home == null) continue;

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

            // Eat and drink from the cache, and actually eat it: the scurvy
            // turns on what a man swallowed, not on a ration being issued.
            bool ate = home.TakeRation(n, 1) && n.Eat();
            if (!ate) ate = n.Eat();   // whatever is in his own pack
            if (!ate) n.Needs.Hunger = Mathf.Max(0f, n.Needs.Hunger - 26f);
            else n.Needs.Hunger = Mathf.Max(n.Needs.Hunger, 60f);
            // Snow melted at the fire does for water where there is wood.
            n.Needs.Thirst = home.Stores.Has(ItemId.Water) || !cal.IsFrozen || home.Stores.Has(ItemId.Firewood) ? 100f : Mathf.Max(20f, n.Needs.Thirst - 10f);
            if (home.Stores.Has(ItemId.Water)) home.Stores.Remove(ItemId.Water);
            // A fire in the house keeps him warm; a cold hearth doesn't.
            bool warm = home.Stores.Has(ItemId.Firewood) || !cal.IsFrozen;
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
                continue;
            }

            if (foul) continue;   // nobody works in a blizzard
            // A camp of the wider country feeds itself in its own weekly sum
            // (Economy/CountryBand): its people are not at York's grounds.
            if (home is Economy.CountryBand) continue;

            // A day's work, by what the man is for.
            switch (n.Role)
            {
                case Role.Labourer:
                case Role.Voyageur:
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
                case Role.Factor:
                case Role.Postmaster:
                case Role.NwcPartner:
                case Role.CreeChief:
                    // The leader's day is his books; his orders wait for a live beat.
                    break;
            }
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

    private static int Mouths(Economy.Household home)
    {
        int m = 0;
        foreach (var o in Sim.Npcs) if (o.HomeStores == home && !o.IsDead) m++;
        return Mathf.Max(1, m);
    }

    private static void Work(Npc n, Economy.Household home)
    {
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
        // A band hunts its own country, whether or not the camp has been
        // told where the herd is this week; a post only knows what it knows -
        // until it is hungry, when the men go looking further and come back
        // knowing where the game is.
        var known = home is Economy.BandLedger ? null : home;
        bool desperate = home.FoodDays(Mouths(home)) < 8f;
        if (desperate) known = null;
        var site = eco.BestSite(Species.Caribou, known) ?? eco.BestSite(Species.Goose, known) ?? eco.BestSite(Species.Beaver, known);
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
        if (desperate && home is not Economy.BandLedger && home.Learn(site.Name, site.Cell))
            Sim.Notice($"{home.Title}: the men went looking and found the game at {site.Name}.");
        float chance = Mathf.Clamp(site.Count / site.Capacity, 0.05f, 1f) * (0.16f + n.Skills.Hunting * 0.24f);
        if (GD.Randf() >= chance) return;
        site.Take(SpeciesInfo.PerKill(site.Species, n.Weapon == ItemId.Musket));
        SpeciesInfo.Yield(site.Species, home.Stores, n.Weapon == ItemId.Musket);
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
