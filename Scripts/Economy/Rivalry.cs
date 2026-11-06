using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Agents.Goals;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Items;
using IsometricFurTradeGame.Scripts.World;

namespace IsometricFurTradeGame.Scripts.Economy;

// The Pemmican War in miniature (design doc 4.1, 7.1, 15.1): how close the
// two companies are to blows. Furs walking to the other counter, desertions
// and blood raise it; time and a truce bring it down. Past the threshold
// the leaders start sending armed parties for each other's stores.
//
// Each house at York holds its own grievance (design doc 10): a wrong done
// at York is felt at once, one done on a river a month off reaches the house
// only when the news does - a day's travel for every 4,000 px - and the
// London sale's news with the ship. Its master decides on raids from his own
// house's grievance (TensionOf). Tension is what is said at York: the two
// houses' grievances together, for talk, the council and the campaign.
public static class Rivalry
{
    private static readonly Dictionary<Faction, float> _grievance = new() { [Faction.HBC] = 20f, [Faction.NWC] = 20f };
    private sealed class News { public Faction House; public float Amount; public int Due; public string Why; }
    private static readonly List<News> _coming = new();
    private const float NewsPxPerDay = 4000f, AtHomePx = 3000f;

    public static float Tension
    {
        get => (_grievance[Faction.HBC] + _grievance[Faction.NWC]) / 2f;
        set { bool was = OpenHostility; _grievance[Faction.HBC] = _grievance[Faction.NWC] = Mathf.Clamp(value, 0f, 100f); Crossed(was, "set"); }
    }
    // A house's own grievance against the other: what its master decides by.
    public static float TensionOf(Faction house) => _grievance.TryGetValue(house, out float g) ? g : Tension;
    // After the union of 1821 (Economy/Union.cs) there is one company: nobody
    // is anybody's rival, and there is no other post to raid.
    public static bool United;
    public const float HostileAt = 70f;
    public static bool OpenHostility => Tension >= HostileAt;
    public static bool HostileIn(Faction house) => TensionOf(house) >= HostileAt;
    private static int _lastDecayDay = -1;

    // A wrong done at York (the houses are a river apart: both feel it at once).
    public static void Raise(float amount, string why) => Raise(amount, why, WorldLayout.FortCentre);

    // A wrong done somewhere: each house feels it when the news reaches it.
    public static void Raise(float amount, string why, Vector2 where)
    {
        if (!Net.Net.IsServer || amount == 0f || United) return;
        foreach (var house in new[] { Faction.HBC, Faction.NWC })
        {
            var post = PostOf(house);
            float d = post != null ? IsoGrid.ToWorld(post.AccessCell).DistanceTo(where) : 0f;
            int days = d <= AtHomePx ? 0 : Mathf.CeilToInt(d / NewsPxPerDay);
            if (days == 0) Feel(house, amount, why);
            else _coming.Add(new News { House = house, Amount = amount, Due = Plains.Today + days, Why = why });
        }
    }

    // News from the London sale or Montreal: it comes with the ship or the canoes.
    public static void RaiseFromAway(float amount, string why, int days)
    {
        if (!Net.Net.IsServer || amount == 0f || United) return;
        foreach (var house in new[] { Faction.HBC, Faction.NWC })
            _coming.Add(new News { House = house, Amount = amount, Due = Plains.Today + days, Why = why });
    }

    private static void Feel(Faction house, float amount, string why)
    {
        bool was = OpenHostility;
        _grievance[house] = Mathf.Clamp(TensionOf(house) + amount, 0f, 100f);
        Crossed(was, why);
    }

    private static void Crossed(bool was, string why)
    {
        if (!was && OpenHostility) Campaign.Flag("open_hostility");
        if (!was && OpenHostility) Sim.Log($"Bad blood between the companies has become open hostility ({why}). Armed men go about both posts now.");
        if (was && !OpenHostility) Sim.Log($"The companies have drawn back from open hostility ({why}).");
    }

    public static void DailyTick(int day)
    {
        if (day == _lastDecayDay) return;
        _lastDecayDay = day;
        foreach (var n in new List<News>(_coming))
            if (Plains.Today >= n.Due) { _coming.Remove(n); if (!United) Feel(n.House, n.Amount, n.Why); }
        foreach (var house in new[] { Faction.HBC, Faction.NWC }) _grievance[house] = Mathf.Max(0f, _grievance[house] - 1.2f);
    }

    // Saved apart from the York figure, so each house keeps its own.
    public static Godot.Collections.Array Save() => new() { _grievance[Faction.HBC], _grievance[Faction.NWC] };
    public static void Load(Godot.Collections.Array a)
    {
        _coming.Clear();
        if (a == null || a.Count < 2) return;
        _grievance[Faction.HBC] = a[0].AsSingle(); _grievance[Faction.NWC] = a[1].AsSingle();
    }

    public static Faction RivalOf(Faction f) => United && f is Faction.HBC or Faction.NWC ? Faction.Wild : f == Faction.HBC ? Faction.NWC : f == Faction.NWC ? Faction.HBC : f;
    public static FortLedger PostOf(Faction f) => f == Faction.HBC ? Sim.Fort : f == Faction.NWC ? Sim.Nwc : null;
    public static bool AreRivals(Faction a, Faction b) => !United && ((a == Faction.HBC && b == Faction.NWC) || (a == Faction.NWC && b == Faction.HBC));

    public static string Describe() => United ? "one company now" : Tension >= HostileAt ? "open hostility" : Tension >= 50f ? "bad blood" : Tension >= 25f ? "a cold rivalry" : "an uneasy peace";

    // What a raiding party goes for, in order.
    private static readonly ItemId[] Loot = { ItemId.Pemmican, ItemId.DriedGoose, ItemId.DriedMeat, ItemId.Rum, ItemId.TradeGoods, ItemId.BeaverPelt, ItemId.Bread, ItemId.Musket };

    // Break into the other company's stores and carry off what you can.
    // Whoever is in earshot decides whether it's a theft or a fight.
    public static int Plunder(Agent who, FortLedger target, int maxLots)
    {
        if (target == null || who == null) return 0;
        int took = 0;
        foreach (var id in Loot)
        {
            while (took < maxLots && target.Stores.Remove(id)) { who.Inventory.Add(id); took++; }
            if (took >= maxLots) break;
        }
        int day = GameCalendar.Instance?.Day ?? 0;
        target.LastRaidedDay = day;
        target.LotsLostToRaids += took;
        who.Raiding = true; who.RaidTarget = target.Company; who.RaidTimer = 120f;
        string what = took > 0 ? $"carried off {took} lots" : "found little worth taking";
        Chronicle.Event($"{who.Describe()} broke into {target.Title} and {what}.", who.GlobalPosition, who, weight: 2f);
        Sim.NoticeAt(target, $"{who.Describe()} broke into {target.Title} and {what}.");
        Raise(took > 0 ? 12f : 6f, "a raid on the stores");
        Alarm(who, target.Company);
        return took;
    }

    // Does the house have at least as many muskets about as the party at its door?
    public static bool HouseHasOdds(Faction house, Vector2 at)
    {
        int raiders = 0, defenders = 0;
        foreach (var a in Sim.AgentsWithin(at, 1000f))
        {
            if (a.Raiding && a.RaidTarget == house && a.Weapon != null) raiders++;
            else if (a is Npc n && n.Faction == house && n.IsFighter) defenders++;
            // A player of the house with a gun in his hands counts: he is standing to.
            else if (a is Player.PlayerController p && !p.IsDead && p.Faction == house && p.Weapon != null) defenders++;
        }
        return defenders >= System.Math.Max(raiders, 1);
    }

    // The house turns out: armed men with any nerve go for the intruder,
    // the rest run.
    public static void Alarm(Agent intruder, Faction house)
    {
        // Count the odds first: a house outgunned lets the thieves go and
        // remembers it; one that has the numbers turns out (design doc 15.1).
        int raiders = 0, defenders = 0;
        var near = new List<Npc>();
        foreach (var a in Sim.AgentsWithin(intruder.GlobalPosition, 1000f))
        {
            if (a.Raiding && a.RaidTarget == house && a.Weapon != null) raiders++;
            if (a is Npc n && n.Faction == house && !n.IsDead && n != intruder) { near.Add(n); if (n.IsFighter) defenders++; }
        }
        raiders = System.Math.Max(raiders, 1);
        bool odds = defenders >= raiders;
        foreach (var n in near)
        {
            n.ChangeRapport(intruder, -25f);
            var t0 = n.Brain.Tier0Goal();
            if (t0 is AttackGoal || t0 is FleeGoal) continue;
            bool fighter = n.IsFighter;
            if (fighter && (odds ? n.Personality.Courage > 0.2f : n.Personality.Courage > 0.7f)) n.Brain.Push(n, new AttackGoal(intruder));
            else if (!fighter || n.Personality.Courage < 0.5f) n.Brain.Push(n, new FleeGoal(intruder, 6f));
        }
        if (!odds && defenders > 0) Sim.NoticeAt(intruder, $"The {(house == Faction.NWC ? "Nor'Westers" : "Company's men")} are outgunned and stand aside.");
    }

    // A leader deciding to send men across the river (design doc 3.9).
    // Returns the men sent.
    public static List<Npc> OrderRaid(Npc leader, FortLedger from, FortLedger target, int party)
    {
        var sent = new List<Npc>();
        if (leader == null || from == null || target == null) return sent;
        foreach (var n in Sim.Npcs) if (n.Raiding && !n.IsDead) return sent;   // one party out at a time
        // The raid is coming either way; the director keeps it from landing on
        // the same afternoon as everything else (design doc 2.2).
        if (!Core.Director.MayActNow("raid", 2f, target.AccessCell == default ? leader.GlobalPosition : IsoGrid.ToWorld(target.AccessCell))) return sent;
        foreach (var n in Sim.Npcs)
        {
            if (sent.Count >= party) break;
            if (n.Faction != from.Company || n.IsDead || n.IsSick || (n.Role != Role.Labourer && n.Role != Role.Voyageur)) continue;
            if (!n.Inventory.Has(ItemId.Musket) && from.Stores.Remove(ItemId.Musket)) n.Inventory.Add(ItemId.Musket);
            if (n.Weapon == null) continue;
            n.AssignedTask = null;   // one trip, not a day's work
            n.AssignedTaskName = $"seize {target.Title}";
            n.AssignedTaskIsHunt = false;
            // Drop whatever they were doing and go now.
            n.Brain.Clear(n);
            n.Brain.Push(n, new RaidGoal(target));
            sent.Add(n);
        }
        if (sent.Count > 0)
        {
            Accords.Raid(from, target);
            from.LastRaidDay = GameCalendar.Instance?.Day ?? 0;
            Core.Director.Saw("raid", 2f);
            var names = new List<string>(); foreach (var n in sent) names.Add(n.DisplayName);
            Chronicle.Event($"{leader.DisplayName} sent {string.Join(", ", names)} to seize {target.Title}.", leader.GlobalPosition, leader, weight: 1.5f);
            Sim.NoticeAt(leader, $"{leader.DisplayName}: \"{string.Join(", ", names)} - muskets. We are going to relieve the {(target.Company == Faction.NWC ? "Nor'Westers" : "Company")} of their provisions.\"");
            Raise(8f, "a party sent against the other post");
        }
        return sent;
    }
}
