using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Items;
using IsometricFurTradeGame.Scripts.World;

namespace IsometricFurTradeGame.Scripts.Economy;

// What a camp wants, revealed by how it lives (design doc 2, 13.2). Not a
// list by nation: a camp wants what its own life uses up and breaks, and what
// it has come to rely on.
//
//  - Use. The simulation's own events are the record: a buffalo run spends
//    powder in proportion to the guns that ran and leaves hides for the awls
//    and knives; a winter's trapping wears out traps; ice fishing in the
//    camp's own frozen season wears chisels and nets; a raid or a war party
//    burns powder and loses guns; councils, war talk and trading smoke
//    tobacco; the cold wears out blankets; rum is drunk as fast as the taste
//    for it has grown.
//  - Adoption: how far each good has come into the camp's life. It grows by
//    trading for it, by seeing neighbours with it, and - for guns - by being
//    raided by men who had them; it fades back toward the old ways (the bow,
//    the bark vessel, the deadfall) when the good cannot be got.
//  - Taste: every camp's own lean, fixed when the world begins.
//  - Threat: feuds, raids suffered and war parties against it. A camp in
//    danger wants guns and powder before anything else.
//
// From these come a target stock of each good for the months to the next
// trading trip, looking ahead to the season (nets and chisels before the
// freeze), and the trip buys by need against price: the deficit of each good
// weighed by how much the camp depends on it, its taste, its prosperity for
// the luxuries, and divided by what the post asks. The 1811 slate sets where
// each people stands at the start (Slate); everything after that is the
// simulation's.
public class CampWants
{
    public readonly Dictionary<ItemId, float> Adoption = new();
    public readonly Dictionary<ItemId, float> Taste = new();
    // Use per week, smoothed: what the camp's life has been spending, which
    // it plans by; and this week's, which comes off its stores.
    public readonly Dictionary<ItemId, float> Use = new();
    public readonly Dictionary<ItemId, float> ThisWeek = new();
    public float Threat;
    public float A(ItemId id) => Adoption.GetValueOrDefault(id);
}

public static class Wants
{
    private static readonly Dictionary<BandLedger, CampWants> _all = new();
    private static readonly HashSet<(WarParty, BandLedger)> _fought = new();
    public static void Reset() { _all.Clear(); _fought.Clear(); }

    // Everything a camp might come to want, with its kind of need:
    // 1 a tool its life depends on once adopted, 0.6 a comfort, 0.35 a luxury
    // or a mark of standing (bought when the camp is doing well).
    private static readonly (ItemId Id, float Need)[] Goods =
    {
        (ItemId.Musket, 1f), (ItemId.Powder, 1f), (ItemId.Flint, 0.8f), (ItemId.Kettle, 1f), (ItemId.Axe, 0.9f), (ItemId.Knife, 0.9f),
        (ItemId.Awl, 0.8f), (ItemId.SteelTrap, 1f), (ItemId.IceChisel, 1f), (ItemId.FishingNet, 1f), (ItemId.PointBlanket, 0.7f),
        (ItemId.Stroud, 0.45f), (ItemId.Capote, 0.5f), (ItemId.Tobacco, 0.7f), (ItemId.Beads, 0.35f), (ItemId.Rum, 0.4f),
    };

    public static CampWants Of(BandLedger b)
    {
        if (_all.TryGetValue(b, out var w)) return w;
        w = Slate(b);
        _all[b] = w;
        return w;
    }

    // ---- the 1811 slate --------------------------------------------------------------------
    //
    // Where each people stood with the trade's goods when the game begins.
    // The Cree, the Assiniboine and the Ojibwe had been the middlemen of the
    // trade for a century: guns and kettles were long part of life. The
    // Blackfoot had guns, fewer of them and later; the Plateau peoples very few,
    // because the Blackfoot saw to it that the guns did not cross the
    // mountains. The Dene of the north lived by the net and the ice chisel; the
    // Coast valued blankets as wealth itself. The Métis were the trade's own.
    private static CampWants Slate(BandLedger b)
    {
        var w = new CampWants();
        var rng = new RandomNumberGenerator { Seed = (ulong)Mathf.Abs(b.Name.GetHashCode()) + 1811 };
        float gun = 0.6f, kettle = 0.7f, trap = 0.4f, ice = 0.4f, blanket = 0.5f, rum = 0.3f;
        switch (b.Nation)
        {
            case Faction.Cree: case Faction.Assiniboine: case Faction.Ojibwe: gun = 0.85f; kettle = 0.9f; trap = 0.6f; ice = 0.6f; blanket = 0.7f; rum = 0.5f; break;
            case Faction.Metis: gun = 0.97f; kettle = 0.95f; trap = 0.5f; ice = 0.4f; blanket = 0.8f; rum = 0.5f; break;
            case Faction.Blackfoot: gun = 0.55f; kettle = 0.6f; trap = 0.15f; ice = 0.1f; blanket = 0.5f; rum = 0.4f; break;
            case Faction.Sioux: gun = 0.5f; kettle = 0.6f; trap = 0.2f; ice = 0.15f; blanket = 0.5f; rum = 0.25f; break;
            case Faction.Missouri: gun = 0.4f; kettle = 0.6f; trap = 0.2f; ice = 0.15f; blanket = 0.4f; rum = 0.15f; break;
            case Faction.Plateau: gun = 0.25f; kettle = 0.45f; trap = 0.2f; ice = 0.2f; blanket = 0.4f; rum = 0.05f; break;
            case Faction.Dene: gun = 0.55f; kettle = 0.7f; trap = 0.55f; ice = 0.9f; blanket = 0.6f; rum = 0.1f; break;
            case Faction.Coast: gun = 0.35f; kettle = 0.6f; trap = 0.1f; ice = 0.05f; blanket = 0.9f; rum = 0.1f; break;
        }
        w.Adoption[ItemId.Musket] = gun; w.Adoption[ItemId.Powder] = gun; w.Adoption[ItemId.Flint] = gun;
        w.Adoption[ItemId.Kettle] = kettle; w.Adoption[ItemId.Axe] = kettle * 0.9f; w.Adoption[ItemId.Knife] = 0.9f; w.Adoption[ItemId.Awl] = 0.85f;
        w.Adoption[ItemId.SteelTrap] = trap; w.Adoption[ItemId.IceChisel] = ice; w.Adoption[ItemId.FishingNet] = ice * 0.8f;
        w.Adoption[ItemId.PointBlanket] = blanket; w.Adoption[ItemId.Stroud] = blanket * 0.8f; w.Adoption[ItemId.Capote] = blanket * 0.5f;
        w.Adoption[ItemId.Tobacco] = 0.9f; w.Adoption[ItemId.Beads] = 0.7f; w.Adoption[ItemId.Rum] = rum;
        b.RumTaste = Mathf.Max(b.RumTaste, rum * 40f);
        // Each camp's own lean.
        foreach (var (id, _) in Goods) w.Taste[id] = (float)rng.RandfRange(0.75f, 1.3f);
        // And what it has to hand: about half of what its life would want.
        foreach (var (id, _) in Goods)
        {
            int have = Mathf.RoundToInt(Target(b, w, id, 0f) * 0.5f);
            if (have > 0 && b.Stores.Count(id) < have) b.Stores.Add(id, have - b.Stores.Count(id));
        }
        return w;
    }

    // ---- what the simulation tells it ------------------------------------------------------

    private static void Used(BandLedger b, ItemId id, float n) { var w = Of(b); w.ThisWeek[id] = w.ThisWeek.GetValueOrDefault(id) + n; }

    // A buffalo run or a hunt: powder for the guns that ran, hides for the awls and knives.
    public static void OnHunt(BandLedger b, int kills, int hides)
    {
        var w = Of(b);
        Used(b, ItemId.Powder, kills * 0.6f * w.A(ItemId.Musket));
        Used(b, ItemId.Flint, kills * 0.04f * w.A(ItemId.Musket));
        Used(b, ItemId.Awl, hides * 0.08f);
        Used(b, ItemId.Knife, hides * 0.02f);
    }

    // A winter's trapping wears out traps; furs taken without them are deadfalls and snares.
    public static void OnTrapping(BandLedger b, int pelts) => Used(b, ItemId.SteelTrap, pelts * 0.04f * Of(b).A(ItemId.SteelTrap));

    // A fight: powder burned, guns lost, and a lesson about guns.
    public static void OnFight(BandLedger b, int men, bool enemyArmed, bool lost)
    {
        var w = Of(b);
        Used(b, ItemId.Powder, men * 2.5f * w.A(ItemId.Musket));
        Used(b, ItemId.Musket, lost ? men * 0.1f : men * 0.03f);
        Used(b, ItemId.Tobacco, 3f);   // the council before, and the one after
        w.Threat = Mathf.Min(5f, w.Threat + (lost ? 1.2f : 0.6f));
        if (enemyArmed) Adopt(w, ItemId.Musket, lost ? 0.06f : 0.03f);
    }

    // Raided for horses: the camp knows it is watched.
    public static void OnRaided(BandLedger b, bool byArmed)
    {
        var w = Of(b);
        w.Threat = Mathf.Min(5f, w.Threat + 0.8f);
        if (byArmed) Adopt(w, ItemId.Musket, 0.03f);
    }

    // Goods that came home from a trip: what is had is what is come to rely on.
    public static void OnTraded(BandLedger b, ItemId id, int n)
    {
        var w = Of(b);
        Adopt(w, id, Mathf.Min(0.08f, n * 0.25f / Mathf.Max(1, b.Mouths)));
        if (id == ItemId.Musket) Adopt(w, ItemId.Powder, Mathf.Min(0.08f, n * 0.25f / Mathf.Max(1, b.Mouths)));
        Used(b, ItemId.Tobacco, 1f);   // a trade begins with a pipe
    }

    private static void Adopt(CampWants w, ItemId id, float by) => w.Adoption[id] = Mathf.Clamp(w.A(id) + by, 0f, 1f);

    // ---- a week of life ---------------------------------------------------------------------

    public static void Week(BandLedger b)
    {
        var w = Of(b);
        int mouths = Mathf.Max(1, b.Mouths);
        int lodges = Mathf.Max(1, mouths / 6);
        var at = IsoGrid.ToWorld(b.Camp);
        bool frozen = Sim.Terrain?.FrozenAt(at) ?? GameCalendar.Instance.IsFrozen;
        // The steady part of life: tobacco smoked, the cold on the blankets,
        // the ice fishing in the camp's own winter, rum as the taste has grown.
        Used(b, ItemId.Tobacco, mouths * 0.05f * w.A(ItemId.Tobacco));
        if (frozen)
        {
            Used(b, ItemId.PointBlanket, mouths * 0.004f * w.A(ItemId.PointBlanket));
            Used(b, ItemId.IceChisel, lodges * 0.01f * w.A(ItemId.IceChisel));
            Used(b, ItemId.FishingNet, lodges * 0.012f * w.A(ItemId.FishingNet));
        }
        Used(b, ItemId.Rum, mouths * 0.02f * b.RumTaste / 20f);
        Used(b, ItemId.Beads, mouths * 0.01f);
        Used(b, ItemId.Stroud, mouths * 0.01f * w.A(ItemId.Stroud));
        // Tools break.
        foreach (var id in new[] { ItemId.Kettle, ItemId.Axe, ItemId.Musket, ItemId.Knife })
            Used(b, id, b.Stores.Count(id) * 0.006f);

        // What was used comes off the stores; what the camp did not have, it went without.
        int drunk = 0;
        foreach (var (id, _) in Goods)
        {
            float u = w.ThisWeek.GetValueOrDefault(id);
            int n = Mathf.FloorToInt(u) + (GD.Randf() < u % 1f ? 1 : 0);
            int got = n > 0 ? b.Stores.TakeUpTo(id, n) : 0;
            if (id == ItemId.Rum) drunk = got;
            // The rate it plans by: this week's weighs a fifth.
            w.Use[id] = w.Use.GetValueOrDefault(id) * 0.8f + u * 0.2f;
        }
        w.ThisWeek.Clear();
        // Rum drunk feeds the taste for it; untouched, it fades.
        if (drunk > 0) b.OnRum(Mathf.Min(drunk, 3));
        b.RumTaste = Mathf.Max(0f, b.RumTaste * 0.985f);

        // Neighbours: what the camps around have, this one comes to want.
        // Without it for long, a tool slips back toward the old ways.
        foreach (var (id, _) in Goods)
        {
            float near = Neighbours(b, id);
            if (near > w.A(id)) Adopt(w, id, (near - w.A(id)) * 0.01f);
            if (b.Stores.Count(id) == 0) Adopt(w, id, -0.0015f);
        }
        if (Plains.Log && GameCalendar.Instance.Month % 3 == 0 && GameCalendar.Instance.Day <= 7 && (b.Name.Contains("Sioux") || b.Name.StartsWith("Peigan") || b.Name == "Woodland Cree" || b.Name == "Chipewyan"))
        {
            var top = Deficits(b); var parts = new List<string>();
            for (int i = 0; i < Mathf.Min(4, top.Count); i++) parts.Add($"{Items.Items.Of(top[i].Id).Name.ToLower()} {top[i].Short} ({top[i].Weight:0.00})");
            GD.Print($"[wants] {GameCalendar.Instance.DateString} {b.Name}: guns {w.A(ItemId.Musket):0.00} traps {w.A(ItemId.SteelTrap):0.00} threat {w.Threat:0.0} rum {b.RumTaste:0}; has {b.Stores.Count(ItemId.Musket)} guns {b.Stores.Count(ItemId.Powder)} powder; wants {string.Join(", ", parts)}");
        }
        // Danger fades if nothing renews it; feuds and war parties renew it.
        w.Threat = Mathf.Max(0f, w.Threat * 0.95f);
        foreach (var kv in Plains.Feuds)
            if (kv.Key.StartsWith(b.Name + "|") || kv.Key.EndsWith("|" + b.Name)) w.Threat = Mathf.Min(5f, w.Threat + kv.Value * 0.05f);
        foreach (var p in War.Parties)
        {
            if (p.Against != b && p.From != b) continue;
            w.Threat = Mathf.Min(5f, w.Threat + 0.3f);
            // A fight the camp was in: once for each party, when it is over.
            if (p.Phase is WarPhase.Home or WarPhase.Over && _fought.Add((p, b)))
            {
                var other = p.From == b ? p.Against : p.From;
                OnFight(b, Mathf.Max(3, p.Men.Count), other != null && other.Stores.Count(ItemId.Musket) > 0, p.From == b ? p.Outcome == "lost" : p.Outcome == "won");
            }
        }
    }

    private static float Neighbours(BandLedger b, ItemId id)
    {
        float sum = 0f; int n = 0;
        var at = IsoGrid.ToWorld(b.Camp);
        foreach (var c in Camps.All)
        {
            if (c.Ledger == null || c.Ledger == b || !_all.TryGetValue(c.Ledger, out var o)) continue;
            if (IsoGrid.ToWorld(c.Ledger.Camp).DistanceTo(at) > 9000f) continue;
            sum += o.A(id); n++;
        }
        return n > 0 ? sum / n : 0f;
    }

    // ---- what it wants now ----------------------------------------------------------------

    // How many of a good this camp wants on hand: the months to the next trip
    // at its rate of use, the season ahead, and for its tools, enough for its
    // people - all as far as it has taken the good into its life.
    public static float Target(BandLedger b, CampWants w, ItemId id, float threat)
    {
        int mouths = Mathf.Max(1, b.Mouths);
        int men = Mathf.Max(1, mouths / 3), lodges = Mathf.Max(1, mouths / 6);
        float a = w.A(id);
        if (a <= 0.02f) return 0f;
        float horizon = 10f;   // weeks to the next trip, about
        float use = w.Use.GetValueOrDefault(id);   // smoothed weekly use
        // The season ahead, from the camp's own climate: is the freeze coming?
        bool winterAhead = false;
        var cal = GameCalendar.Instance;
        if (cal != null) winterAhead = Agents.Climate.MeanAt(IsoGrid.ToWorld(b.Camp), cal.DayOfYear + 50f, out _, out _) < -4f;
        float t = id switch
        {
            ItemId.Musket => men * a * (0.6f + 0.25f * threat),
            ItemId.Powder => Mathf.Max(use * horizon, men * a * 2f) * (1f + 0.4f * threat),
            ItemId.Flint => b.Stores.Count(ItemId.Musket) * 1.5f,
            ItemId.Kettle => lodges * 1.5f * a,
            ItemId.Axe => lodges * a,
            ItemId.Knife => men * a + use * horizon,
            ItemId.Awl => mouths * 0.5f * a + use * horizon,
            ItemId.SteelTrap => men * 3f * a,
            ItemId.IceChisel => (winterAhead ? lodges * 1.5f : lodges * 0.5f) * a,
            ItemId.FishingNet => (winterAhead ? lodges * 1.5f : lodges * 0.5f) * a,
            ItemId.PointBlanket => mouths * (winterAhead ? 0.5f : 0.25f) * a,
            ItemId.Capote => men * (winterAhead ? 0.4f : 0.1f) * a,
            ItemId.Stroud => mouths * 0.4f * a,
            ItemId.Tobacco => Mathf.Max(use * horizon, mouths * 0.3f) * a,
            ItemId.Beads => mouths * 0.5f * a,
            ItemId.Rum => mouths * b.RumTaste / 100f,
            _ => 0f,
        };
        return t * w.Taste.GetValueOrDefault(id, 1f);
    }

    // The camp's wants, most pressing first: (good, how many short, how much it matters a unit).
    public static List<(ItemId Id, int Short, float Weight)> Deficits(BandLedger b, Inventory alreadyBought = null)
    {
        var w = Of(b);
        var list = new List<(ItemId, int, float)>();
        // A camp that is eating well and has horses buys its luxuries; a hungry one does not.
        float prosperity = Mathf.Clamp(b.FoodDays(Mathf.Max(1, b.Mouths)) / 30f, 0.2f, 1.2f) * (b.Herd.Horses > b.Mouths ? 1.2f : 1f);
        foreach (var (id, need) in Goods)
        {
            float target = Target(b, w, id, w.Threat);
            int have = b.Stores.Count(id) + (alreadyBought?.Count(id) ?? 0);
            int shortBy = Mathf.CeilToInt(target - have);
            if (shortBy <= 0) continue;
            float urgency = Mathf.Clamp(shortBy / Mathf.Max(1f, target), 0f, 1f);
            float weight = urgency * (need >= 0.9f ? need : need * prosperity) * w.A(id) * w.Taste.GetValueOrDefault(id, 1f);
            if (id is ItemId.Musket or ItemId.Powder) weight *= 1f + 0.3f * w.Threat;
            if (id == ItemId.Rum) weight *= b.RumTaste / 40f;
            list.Add((id, shortBy, weight));
        }
        list.Sort((x, y) => y.Item3.CompareTo(x.Item3));
        return list;
    }

    // ---- saving --------------------------------------------------------------------------

    public static Godot.Collections.Dictionary Save()
    {
        var d = new Godot.Collections.Dictionary();
        foreach (var (b, w) in _all)
        {
            var e = new Godot.Collections.Dictionary { ["threat"] = w.Threat };
            var ad = new Godot.Collections.Dictionary(); foreach (var kv in w.Adoption) ad[kv.Key.ToString()] = kv.Value;
            var ta = new Godot.Collections.Dictionary(); foreach (var kv in w.Taste) ta[kv.Key.ToString()] = kv.Value;
            var us = new Godot.Collections.Dictionary(); foreach (var kv in w.Use) us[kv.Key.ToString()] = kv.Value;
            e["a"] = ad; e["t"] = ta; e["u"] = us;
            d[b.Name] = e;
        }
        return d;
    }

    public static void Load(Godot.Collections.Dictionary d)
    {
        _all.Clear();
        if (d == null) return;
        foreach (var kv in d)
        {
            var b = Camps.Named(kv.Key.AsString())?.Ledger;
            if (b == null) continue;
            var e = kv.Value.AsGodotDictionary();
            var w = new CampWants { Threat = e["threat"].AsSingle() };
            void Fill(Dictionary<ItemId, float> into, string key) { foreach (var x in e[key].AsGodotDictionary()) if (System.Enum.TryParse(x.Key.AsString(), out ItemId id)) into[id] = x.Value.AsSingle(); }
            Fill(w.Adoption, "a"); Fill(w.Taste, "t"); Fill(w.Use, "u");
            _all[b] = w;
        }
    }
}
