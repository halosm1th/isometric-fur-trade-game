using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.World;

namespace IsometricFurTradeGame.Scripts.Economy;

// A household's horses (design doc 12.3): on the plains the canoe's
// equivalent and the measure of a man. Not an item: horses breed, starve in a
// hard winter, are run off by raiders and given away at a marriage. A camp's
// standing among its neighbours is counted in them, and they are what a raid
// is *for*.
public class Herd
{
    public int Horses;
    public int Out;                 // under a rider just now
    public int Carts;               // Red River carts (the Métis)
    public float Condition = 70f;   // 0..100: grass in them, or the want of it
    public int Free => Mathf.Max(0, Horses - Out);
    public bool Any => Horses > 0;
    private int _day = -1;

    public string Describe() => Horses == 0 ? "no horses" : $"{Horses} horse{(Horses == 1 ? "" : "s")}{(Carts > 0 ? $" and {Carts} carts" : "")}, {Shape}";
    public string Shape => Condition >= 70f ? "fat on the grass" : Condition >= 45f ? "in fair shape" : Condition >= 25f ? "thin" : "starving";

    // One day of the herd's life: the grass in summer, the snow in winter,
    // the foals in May and the losses of a hard February.
    public void Day(Household owner)
    {
        var cal = GameCalendar.Instance;
        if (cal == null || Horses <= 0 || cal.Day == _day) return;
        _day = cal.Day;
        int m = cal.Month;
        float graze = m is >= 5 and <= 9 ? 0.55f : m is 4 or 10 ? 0.15f : -0.3f;
        // A herd ridden hard is a thin herd.
        float worked = Horses > 0 ? 0.35f * Out / Horses : 0f;
        // Cottonwood bark and the shelter of a river valley see a herd
        // through: the camps winter in the valleys for a reason.
        if (graze < 0f && owner is BandLedger) graze *= 0.8f;
        Condition = Mathf.Clamp(Condition + graze - worked, 0f, 100f);
        bool winter = owner != null && Sim.Terrain != null ? Sim.Terrain.FrozenAt(IsoGrid.ToWorld(owner.AccessCell)) : cal.IsFrozen;
        if (winter && Condition < 30f && GD.Randf() < (30f - Condition) / 30f * 0.2f)
        {
            int lost = Mathf.Max(1, Mathf.RoundToInt(Horses * 0.02f));
            Lose(lost);
            if (owner != null) Chronicle.Event($"{owner.Title}: {lost} horse{(lost == 1 ? "" : "s")} died in the snow, starved.", IsoGrid.ToWorld(owner.AccessCell), subject: owner.Title, weight: 0.4f);
        }
        // Age, a broken leg in a badger hole, a wolf at a foal: a tenth of a
        // herd goes in an ordinary year.
        if (GD.Randf() < Horses * 0.1f / 365f) Lose(1);
        if (m == 5 && cal.Day == 15)
        {
            // About half the herd are mares and most of them foal, if they
            // came through the winter in any shape at all - and if there is
            // grass and a guard for more: a camp keeps what it can herd.
            int foals = Mathf.RoundToInt(Horses * 0.2f * Mathf.Clamp(Condition / 70f, 0.2f, 1.2f));
            int room = owner is BandLedger b ? Mathf.Max(0, b.Mouths * 6 - Horses) : Mathf.Max(0, 60 - Horses);
            Horses += Mathf.Min(foals, room);
        }
    }

    public void Lose(int n)
    {
        Horses = Mathf.Max(0, Horses - n);
        Out = Mathf.Min(Out, Horses);
        if (Carts > Horses) Carts = Horses;
    }

    // What a horse fetched in trade, in Made Beaver: a common horse about a
    // gun, a good buffalo runner a good deal more.
    public const float ValueMb = 12f;

    public Godot.Collections.Array Save() => new() { Horses, Out, Carts, Condition };
    public void Load(Godot.Collections.Array a)
    {
        if (a == null || a.Count < 4) return;
        Horses = a[0].AsInt32(); Out = 0; Carts = a[2].AsInt32(); Condition = a[3].AsSingle();
    }
}

// Who has horses at the start, and the bookkeeping over every herd.
public static class Horses
{
    // Horses to a person, by people. The Peigan, the Crow and the Shoshone were
    // rich in them, the Blackfoot and the Plateau peoples well off, the
    // Assiniboine and the Plains Cree poor by comparison - they were raided for
    // them - and the people of the woods and the Bay had none at all.
    private static readonly (string Name, float PerHead)[] ByCamp =
    {
        ("Peigan", 3.5f), ("Blood", 3f), ("Blackfoot", 3f), ("Gros Ventre", 2.5f), ("Sarcee", 2f),
        ("Crow", 4f), ("Shoshone", 3f), ("Cheyenne", 2.5f), ("Hidatsa", 1.5f), ("Mandan", 1f), ("Arikara", 1f),
        ("Lakota", 2f), ("Dakota", 1f), ("Assiniboine", 1f), ("Nakoda", 1.2f), ("Plains Cree", 1f), ("Saulteaux", 0.6f),
        ("Flathead", 2.5f), ("Kootenay", 2f), ("Okanagan", 2f), ("Shuswap", 1f), ("Nlaka'pamux", 0.6f), ("Tsilhqot'in", 0.3f),
        ("Métis", 2f),
    };
    private static float PerHeadOf(Faction nation) => nation switch
    {
        Faction.Blackfoot => 2f, Faction.Sioux => 1.5f, Faction.Missouri => 1.5f, Faction.Plateau => 1.5f, Faction.Metis => 2f, Faction.Assiniboine => 1f, _ => 0f,
    };

    // The posts of the parkland and the plains kept a horse guard; the
    // canoe country's posts had a horse or two at most.
    private static readonly (string Name, int Horses)[] ByPost =
    {
        ("Edmonton", 40), ("Fort Augustus", 30), ("Rocky Mountain House", 20), ("Acton House", 15), ("Carlton", 25), ("Brandon", 20),
        ("Qu'Appelle", 15), ("Espérance", 15), ("Fort Gibraltar", 20), ("Pembina", 15), ("Chesterfield", 15), ("Buckingham", 15),
        ("Manchester", 10), ("Hudson House", 10), ("South Branch", 10), ("Pine Fort", 10), ("Fort George", 15), ("Fort de l'Isle", 10),
        ("Kootenay House", 6), ("Spokane", 15), ("Flathead", 10), ("Fort William", 6), ("Cumberland", 4), ("Fort Vermilion", 8),
    };

    public static void Seed(CountryBand b)
    {
        float per = -1f;
        foreach (var (name, ph) in ByCamp) if (b.Country.Name.Contains(name)) { per = ph; break; }
        if (per < 0f) per = PerHeadOf(b.Nation);
        if (b.Country.IsFamily) per *= 0.5f;
        b.Herd.Horses = Mathf.RoundToInt(per * Mathf.Max(b.Country.People, 2) * (float)GD.RandRange(0.8, 1.2));
        if (b.Nation == Faction.Metis) b.Herd.Carts = Mathf.Max(1, Mathf.RoundToInt(b.Country.People * 0.6f));
        b.Herd.Condition = 70f;
    }

    public static void Seed(TradingPostLedger p)
    {
        foreach (var (name, n) in ByPost) if (p.Post.Name.Contains(name)) { p.Herd.Horses = n; return; }
    }

    // Every household that can own horses.
    public static IEnumerable<Household> All()
    {
        if (Sim.Fort != null) yield return Sim.Fort;
        if (Sim.Nwc != null) yield return Sim.Nwc;
        if (Sim.Outpost != null) yield return Sim.Outpost;
        foreach (var b in Sim.Bands) yield return b;
        foreach (var c in Camps.All) if (c.Ledger != null) yield return c.Ledger;
        foreach (var p in Posts.All) if (p.Ledger != null) yield return p.Ledger;
    }

    public static Godot.Collections.Dictionary Save(System.Func<Household, string> key)
    {
        var d = new Godot.Collections.Dictionary();
        foreach (var h in All())
        {
            string k = key(h);
            if (k != "" && (h.Herd.Horses > 0 || h.Herd.Carts > 0)) d[k] = h.Herd.Save();
        }
        return d;
    }

    public static void Load(Godot.Collections.Dictionary d, System.Func<string, Household> home)
    {
        if (d == null) return;
        foreach (var h in All()) { h.Herd.Horses = 0; h.Herd.Carts = 0; h.Herd.Out = 0; }
        int n = 0;
        foreach (var kv in d) if (home(kv.Key.AsString()) is { } h) { h.Herd.Load(kv.Value.AsGodotArray()); n++; }
        if (Plains.Log) GD.Print($"[plains] {n} herds back from the save");
    }

    // Coats for the horses a household rides out on, so a camp's horses
    // are told apart from each other at a glance.
    public static int CoatFor(string seed) => Mathf.Abs(seed.GetHashCode()) % Coats.Length;
    public static readonly Color[] Coats =
    {
        new(0.55f, 0.36f, 0.22f), new(0.35f, 0.24f, 0.16f), new(0.82f, 0.68f, 0.45f), new(0.25f, 0.22f, 0.2f),
        new(0.75f, 0.72f, 0.68f), new(0.62f, 0.45f, 0.3f), new(0.9f, 0.85f, 0.78f),
    };
}
