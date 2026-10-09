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
        if (n.Brain.Has<CourierGoal>() || n.Brain.Has<RaidGoal>() || n.Brain.Has<AmbushGoal>() || n.Brain.Has<RoadGoal>() || n.Brain.Has<RescueGoal>() || n.Brain.Has<LookForGoal>())
            return;   // a man on a journey is the story; he walks it himself
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
        // A copy: someone who dies of want in the batch leaves Sim.Npcs mid-loop.
        foreach (var n in new System.Collections.Generic.List<Npc>(Sim.Npcs))
        {
            if (n == null || !GodotObject.IsInstanceValid(n) || n.Tier != Tier.Background || n.IsDead) continue;
            var home = n.HomeStores;
            if (home == null) continue;

            // Eat and drink from the cache. A household with nothing starves,
            // at the same rate a live man would.
            bool ate = home.TakeRation(n, 1) || n.Eat();
            n.Needs.Hunger = ate ? 100f : Mathf.Max(0f, n.Needs.Hunger - 26f);
            n.Needs.Thirst = home.Stores.Has(ItemId.Water) || !cal.IsFrozen ? 100f : Mathf.Max(20f, n.Needs.Thirst - 10f);
            if (home.Stores.Has(ItemId.Water)) home.Stores.Remove(ItemId.Water);
            // A fire in the house keeps him warm; a cold hearth doesn't.
            bool warm = home.Stores.Has(ItemId.Firewood) || !cal.IsFrozen;
            if (warm) { n.Needs.Warmth = 100f; home.Stores.TakeUpTo(ItemId.Firewood, 1); }
            else n.Needs.Warmth = Mathf.Max(0f, n.Needs.Warmth - 30f);
            if (n.Needs.Hunger <= 0f || n.Needs.Warmth <= 0f) n.Needs.Health = Mathf.Max(0f, n.Needs.Health - 10f);
            else if (n.Needs.Health < 100f) n.Needs.Health = Mathf.Min(100f, n.Needs.Health + 6f);
            if (n.Needs.Health <= 0f) { n.DieQuietly(); Sim.Notice($"{n.DisplayName} has died of want at {home.Title}."); continue; }
            if (!ate) n.DaysSinceFresh += 1f;

            if (foul) continue;   // nobody works in a blizzard

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

    private static void Work(Npc n, Economy.Household home)
    {
        // Wood first, then whatever the post is short of.
        if (home.Stores.Count(ItemId.Firewood) < 30) { home.Stores.Add(ItemId.Firewood, n.Skills.Woodcraft >= 0.5f ? 5 : 4); n.Skills.Learn(ref n.Skills.Woodcraft, 0.004f); return; }
        if (home is FortLedger f && f.Stores.Has(ItemId.SteelTrap) && GameCalendar.Instance.CurrentSeason == Season.Winter) { Trap(n, home); return; }
        Hunt(n, home);
    }

    private static void Hunt(Npc n, Economy.Household home)
    {
        var eco = Sim.Ecology;
        if (eco == null) return;
        var site = eco.BestSite(Species.Caribou, home) ?? eco.BestSite(Species.Goose, home) ?? eco.BestSite(Species.Beaver, home);
        if (site == null || site.Count < 1f)
        {
            // Snares along the bush: thin fare, no site drawn down.
            if (GD.Randf() < 0.5f + n.Skills.Hunting * 0.3f) home.Stores.Add(ItemId.Hare, 1 + (GD.Randf() < n.Skills.Hunting ? 1 : 0));
            return;
        }
        float chance = Mathf.Clamp(site.Count / site.Capacity, 0.05f, 1f) * (0.35f + n.Skills.Hunting * 0.45f);
        if (GD.Randf() >= chance) return;
        site.Take(1);
        SpeciesInfo.Yield(site.Species, home.Stores, n.Weapon == ItemId.Musket);
        n.Skills.Learn(ref n.Skills.Hunting, 0.004f);
        n.DaysSinceFresh = 0f;
    }

    private static void Trap(Npc n, Economy.Household home)
    {
        var eco = Sim.Ecology;
        var site = eco?.BestSite(Species.Beaver, home);
        if (site == null) return;
        var line = site.LineOf(home.Title);
        if (line == null || line.Traps == 0)
        {
            int traps = home.Stores.TakeUpTo(ItemId.SteelTrap, 4);
            if (traps > 0) site.SetTraps(home.Title, traps, n.Skills.Trapping);
            return;
        }
        // The nightly catch already happens at the site; the man brings it in.
        int got = line.Caught; line.Caught = 0; line.HoursSinceCheck = 0f;
        for (int i = 0; i < got; i++) SpeciesInfo.Yield(Species.Beaver, home.Stores);
        if (got > 0) n.Skills.Learn(ref n.Skills.Trapping, 0.006f);
    }

    private static void House(Npc n, Economy.Household home)
    {
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
