using System.Collections.Generic;
using System.Linq;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Agents.Goals;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Ecology;
using IsometricFurTradeGame.Scripts.Economy;
using IsometricFurTradeGame.Scripts.World;

namespace IsometricFurTradeGame.Scripts.UI;

// The observer's hand on the world (Core/Observer, UI/ObserverInspect): a
// god's nudges, not new rules. Each power works through what the world
// already does - a goal pushed onto a person's stack, a feud heated, a war
// party sent by War.Launch, a buffalo run called the way Plains calls one -
// so whatever follows (the ride there, the fight, the grudges after) is the
// simulation's own. Everyone still walks: nothing here moves anybody.
// Each returns a line to say what came of it.
public static class ObserverPowers
{
    private static bool Host => Net.Net.IsServer;
    private const string NotHost = "Only the host's hand moves the world.";

    // ---- one person -------------------------------------------------------------------

    public static string Inspire(Agent a)
    {
        if (!Host) return NotHost;
        if (a is not Npc n || n.IsDead) return "There is nobody there to stir.";
        var p = n.Personality;
        p.Courage = Mathf.Min(1f, p.Courage + 0.3f); p.Loyalty = Mathf.Min(1f, p.Loyalty + 0.2f); p.Ambition = Mathf.Min(1f, p.Ambition + 0.2f);
        n.Personality = p;
        n.Strain = 0f;
        n.Standing = Mathf.Min(100f, n.Standing + 10f);
        n.Bark("I feel I could do anything today.");
        Sim.NoticeAt(n, $"{n.DisplayName} has a new heart.");
        return $"{n.Describe()} is inspired: braver, steadier, and the winter's weight is lifted.";
    }

    public static string Dishearten(Agent a)
    {
        if (!Host) return NotHost;
        if (a is not Npc n || n.IsDead) return "There is nobody there to dishearten.";
        var p = n.Personality;
        p.Courage = Mathf.Max(0f, p.Courage - 0.3f); p.Loyalty = Mathf.Max(0f, p.Loyalty - 0.2f);
        n.Personality = p;
        n.Strain = Mathf.Min(100f, n.Strain + 45f);
        n.Bark("What is the use of any of it.");
        return $"{n.Describe()} is disheartened: timid, sour, and near the end of their rope.";
    }

    public static string Heal(Agent a)
    {
        if (!Host) return NotHost;
        if (a == null || a.IsDead) return "The dead are past mending.";
        a.Needs.Health = 100f; a.Needs.Hunger = 100f; a.Needs.Thirst = 100f; a.Needs.Warmth = 100f;
        a.SickDaysLeft = 0f; a.DaysSinceFresh = 0f; a.Intoxication = 0f;
        return $"{a.Describe()} is made whole: fed, warm, and well.";
    }

    public static string Wound(Agent a)
    {
        if (!Host) return NotHost;
        if (a == null || a.IsDead) return "Nobody living there.";
        a.Needs.Health = Mathf.Max(1f, a.Needs.Health - 45f);
        return $"{a.Describe()} is struck down sore, but lives.";
    }

    public static string StrikeDead(Agent a)
    {
        if (!Host) return NotHost;
        if (a == null || a.IsDead) return "Already dead.";
        if (a is Npc n) n.Die("struck down where they stood, nobody could say by what");
        else a.Needs.Health = 0f;
        return $"{a.Describe()} falls dead.";
    }

    // Push a goal on top of whatever he is about: when it is done he goes back to his day.
    private static string Push(Agent a, Goal g, string what)
    {
        if (!Host) return NotHost;
        if (a is not Npc n || n.IsDead) return "Only the living can be pushed.";
        if (n.HeldBy != null || n.Away) return $"{n.Describe()} is not free to go anywhere.";
        n.Sleeping = false;
        n.Brain.Push(n, g);
        return $"{n.Describe()} goes to {what}.";
    }

    public static string Hunt(Agent a)
    {
        var site = GameNear(a.GlobalPosition, a is Npc n ? n.HomeStores as BandLedger : null);
        return site == null ? "There is no game within a day's walk." : Push(a, new HuntSiteGoal(site, 6f), $"hunt at {site.Name}");
    }
    public static string Fish(Agent a) => Push(a, new FishGoal(Sim.Terrain?.FrozenAt(a.GlobalPosition) ?? false), "fish");
    public static string Forage(Agent a) => Push(a, new ForageGoal(4f), "forage");
    public static string GoHome(Agent a) => a is Npc n ? Push(a, new GoToGoal(n.BedCell, 60f), "go home") : "Nobody to send.";

    // Two people, the second clicked after the first.
    public static string SetAgainst(Agent a, Agent b)
    {
        if (!Host) return NotHost;
        if (a is not Npc n || b == null || a == b) return "Pick someone else.";
        n.ChangeRapport(b, -70f);
        if (b is Npc m) m.ChangeRapport(a, -40f);
        n.Bark($"I can't abide {b.Describe()}.");
        return $"{n.Describe()} has turned against {b.Describe()}.";
    }

    public static string Befriend(Agent a, Agent b)
    {
        if (!Host) return NotHost;
        if (a is not Npc n || b == null || a == b) return "Pick someone else.";
        n.ChangeRapport(b, 60f);
        if (b is Npc m) m.ChangeRapport(a, 60f);
        return $"{n.Describe()} and {b.Describe()} are fast friends now.";
    }

    public static string Attack(Agent a, Agent b)
    {
        if (a == null || b == null || a == b || b.IsDead) return "Pick someone living, and someone else.";
        if (a is Npc n) n.ChangeRapport(b, -50f);
        return Push(a, new AttackGoal(b), $"fall on {b.Describe()}");
    }

    public static string Follow(Agent a, Agent b) => b == null || a == b ? "Pick someone else." : Push(a, new FollowGoal(b, 12f), $"walk with {b.Describe()}");

    // ---- a people: a camp, a post, a town's garrison -------------------------------------

    public static string GroupName(Household h)
    {
        if (h == null) return "";
        foreach (var t in Towns.All) if (t.Garrison == h) return $"{t.Name} garrison";
        if (Posts.Of(h) is { } p) return p.Name;
        if (h == Sim.Fort) return "York Factory";
        if (h == Sim.Nwc) return "the Nor'Westers at York";
        if (h == Sim.Outpost) return "Oxford House";
        return War.NameOf(h);
    }

    private static string Kind(Household h) => h switch
    {
        BandLedger => "camp",
        TownHousehold => "garrison",
        FortLedger => "post",
        _ => "house",
    };

    // Every people that can be set against another, nearest the given one first.
    public static List<(Household H, string Label)> Groups(Household from, int max = 60)
    {
        var all = new HashSet<Household>();
        void Add(Household h) { if (h != null && GodotObject.IsInstanceValid(h) && h != from) all.Add(h); }
        Add(Sim.Fort); Add(Sim.Nwc); Add(Sim.Outpost); Add(Sim.Band);
        foreach (var c in Camps.All) Add(c.Ledger);
        foreach (var p in Posts.Standing()) Add(p.Ledger);
        foreach (var t in Towns.All) Add(t.Garrison);
        var at = from != null ? IsoGrid.ToWorld(War.CampOf(from)) : Vector2.Zero;
        return all.Select(h => (h, d: IsoGrid.ToWorld(War.CampOf(h)).DistanceTo(at)))
            .OrderBy(x => x.d).Take(max)
            .Select(x => (x.h, $"{GroupName(x.h)} ({Kind(x.h)}, {x.d / WorldMap.PxPerKm:0} km)")).ToList();
    }

    // The men of a people who are about, live, and free to go.
    private static List<Npc> FreeMen(Household h) =>
        Sim.Npcs.Where(n => n.HomeStores == h && !n.IsDead && !n.IsFemale && !n.Away && n.HeldBy == null && n.Tier == Tier.Live && n.Needs.Health >= 40f
            && !n.Brain.Has<BuffaloRunGoal>() && !n.Brain.Has<HorseRaidGoal>() && !n.Brain.Has<WarPartyGoal>()).ToList();

    // The hunt: on the plains, a buffalo run (whatever the season, however
    // lately they ran); elsewhere the hunters walk out to the best game near.
    public static string GroupHunt(Household h)
    {
        if (!Host) return NotHost;
        if (h == null) return "Nobody's people to send.";
        string name = GroupName(h);
        if (h is BandLedger b && BuffaloRun.Of(b) != null) return $"{name} are running the buffalo already.";
        if (h is BandLedger band && band.Herd.Free >= 2 && Plains.HerdNear(band, 14000f) is { } herd)
        {
            var riders = FreeMen(h).Where(n => n.Role is Role.Hunter or Role.Chief).Take(band.Herd.Free).ToList();
            if (riders.Count >= 2)
            {
                var captain = band.Leader != null && riders.Contains(band.Leader) ? band.Leader : riders[0];
                band.Believe(herd.Name, herd.Cell);
                BuffaloRun.Call(band, herd, riders, captain);
                return $"{name} call a buffalo run at {herd.Name}: {riders.Count} riders under {captain.Describe()}.";
            }
        }
        var site = GameNear(IsoGrid.ToWorld(War.CampOf(h)), h as BandLedger);
        if (site == null) return $"There is no game within reach of {name}.";
        var men = FreeMen(h).Where(n => n.Role is Role.Hunter or Role.Chief or Role.Labourer or Role.Voyageur or Role.Farmer or Role.Soldier).ToList();
        if (men.Count == 0) return $"{name} have no men about and fit to hunt (only those near the camera can be sent).";
        foreach (var n in men) { n.Sleeping = false; n.Brain.Push(n, new HuntSiteGoal(site, 6f)); }
        Sim.NoticeAt(h, $"{name} send {men.Count} men out to hunt at {site.Name}.");
        return $"{men.Count} men of {name} go out to hunt at {site.Name}.";
    }

    public static string GroupTrade(Household h)
    {
        if (!Host) return NotHost;
        if (h is not BandLedger b) return "Only a camp goes in to trade.";
        if (TradeTrips.Away(b)) return $"{GroupName(h)}'s men are already away trading.";
        var at = IsoGrid.ToWorld(b.Camp);
        TradingPostLedger post = null; float best = float.MaxValue;
        foreach (var p in Posts.Standing())
            if (p.Ledger is { } led && p.Centre.DistanceTo(at) < best) { best = p.Centre.DistanceTo(at); post = led; }
        if (post == null) return "There is no post standing to trade at.";
        return TradeTrips.Start(b, post, GameCalendar.Instance)
            ? $"{GroupName(h)} set out to trade at {post.Post.Name}."
            : $"{GroupName(h)} have nothing worth the walk to {post.Post.Name}, or no men to carry it.";
    }

    public static string GroupRaid(Household h, Household target)
    {
        if (!Host) return NotHost;
        if (h is not BandLedger b) return "Horse raids are the camps' young men's business.";
        if (target == null) return "Pick whose horses.";
        if (target.Herd.Free < 1) return $"{GroupName(target)} have no horses to take.";
        Plains.HeatFeud(b, target, 1f);
        Plains.SendRaid(b, target);
        return $"The young men of {GroupName(h)} go after {GroupName(target)}'s horses{(Plains.IsLive(target) ? " tonight" : "")}.";
    }

    public static string GroupWar(Household h, Household target)
    {
        if (!Host) return NotHost;
        if (h == null || target == null || h == target) return "Pick another people to ride against.";
        if (War.Fighters(h).Count == 0) return $"{GroupName(h)} have no fighting men.";
        Plains.HeatFeud(h, target, 2f);
        var party = War.Launch(h, target, "with their blood up");
        if (party == null) return "Nobody rode out.";
        return party.Phase == WarPhase.Over
            ? $"{GroupName(h)} rode against {GroupName(target)} far from anyone's sight: {party.Outcome}."
            : $"{GroupName(h)}'s war party rides out against {GroupName(target)}: {party.Men.Count} men.";
    }

    public static string GroupFeud(Household h, Household target)
    {
        if (!Host) return NotHost;
        if (h == null || target == null || h == target) return "Pick another people.";
        Plains.HeatFeud(h, target, 4f);
        foreach (var n in Sim.Npcs)
            if (n.HomeStores == h && !n.IsDead && target.Leader != null) n.ChangeRapport(target.Leader, -30f);
        return $"{GroupName(h)} hold a bitter score against {GroupName(target)} now: raids and war parties will come of it.";
    }

    public static string GroupPeace(Household h, Household target)
    {
        if (!Host) return NotHost;
        if (h == null || target == null || h == target) return "Pick another people.";
        Plains.CoolFeud(h, target); Plains.CoolFeud(target, h);
        War.Truce(h, target, 120);
        return $"{GroupName(h)} and {GroupName(target)} make peace for a season.";
    }

    // The best game near a place: a band's own judgment if it has one, else the nearest living herd or flock.
    private static SpawnSite GameNear(Vector2 at, BandLedger band)
    {
        var eco = Sim.Ecology;
        if (eco == null) return null;
        if (band != null && eco.BestBigGame(band) is { } big) return big;
        SpawnSite best = null; float bd = 7000f;
        foreach (var s in eco.Sites)
        {
            if (s.IsDead || s.Count < 1f || s.Species == Species.Buffalo) continue;
            float d = s.WorldPos.DistanceTo(at);
            if (d < bd) { bd = d; best = s; }
        }
        return best;
    }
}
