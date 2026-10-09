using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Economy;
using IsometricFurTradeGame.Scripts.Items;

namespace IsometricFurTradeGame.Scripts.Agents;

// Someone gone from a household (design doc P.8, lever 7): who, where they
// slept, what they left. The gap shows in the days after - the empty bunk,
// his name at the fire, his things passing to the men who knew him.
public class Loss
{
    public string Name;
    public Household Home;
    public Vector2I Bed;
    public int Day;                 // day of year it happened
    public Npc Body;                // while the body lasts
    public bool EffectsSettled;
    public bool AuctionOpen;        // a company post sells his kit among the men
    public readonly List<(ItemId id, int n)> Lots = new();
    public string NewSleeper;       // who has his bunk now, once someone does
    public bool NewSleeperTold;

    public int DaysAgo => (int)Mathf.PosMod((GameCalendar.Instance?.DayOfYear ?? 0f) - Day, 365f);
    public bool Fresh => DaysAgo <= 40;
}

public static class Losses
{
    public static readonly List<Loss> All = new();

    public static void Record(Npc dead)
    {
        if (dead.HomeStores == null) return;
        All.Add(new Loss { Name = dead.DisplayName, Home = dead.HomeStores, Bed = dead.BedCell, Day = (int)(GameCalendar.Instance?.DayOfYear ?? 0f), Body = dead });
        if (All.Count > 40) All.RemoveAt(0);
    }

    public static void Reset() => All.Clear();

    public static IEnumerable<Loss> Of(Household h)
    {
        foreach (var l in All) if (l.Home == h) yield return l;
    }

    // How deep a living person's tie to a lost one runs: kin outright, else
    // the things they went through together and what they thought of him.
    public static float Bond(Npc s, Loss l)
    {
        if (s == null || l == null) return 0f;
        if (s.Spouse?.DisplayName == l.Name || s.Father?.DisplayName == l.Name) return 10f;
        foreach (var c in s.Children) if (c.DisplayName == l.Name) return 10f;
        float b = 0f;
        foreach (var m in s.Memories) if (m.Directness == 1 && m.Weight >= 1f && m.Witnesses != null && m.Witnesses.Contains(l.Name)) b += 1f;
        if (l.Body != null && GodotObject.IsInstanceValid(l.Body)) b += s.RapportWith(l.Body) / 25f;
        return b;
    }

    // Once an hour or so, from the population tick: the morning after a
    // death his effects are gathered; a company post sells them among the
    // men that evening (the HBC did), a band's family gives them away.
    public static void Tick()
    {
        var cal = GameCalendar.Instance;
        if (cal == null) return;
        foreach (var l in All)
        {
            if (l.NewSleeper == null)
                foreach (var n in Sim.Npcs) if (!n.IsDead && n.HomeStores == l.Home && n.BedCell == l.Bed && n.DisplayName != l.Name) { l.NewSleeper = n.DisplayName; break; }
            if (l.EffectsSettled || l.DaysAgo < 1) continue;
            if (!l.AuctionOpen && cal.Hour >= 17f)
            {
                Gather(l);
                if (l.Lots.Count == 0) { l.EffectsSettled = true; continue; }
                if (l.Home is FortLedger)
                {
                    l.AuctionOpen = true;
                    Sim.Notice($"This evening {Chronicle.PlaceOf(l.Home)} sells {l.Name}'s effects among the men. Ask the master if you want to bid.");
                }
                else Settle(l);
            }
            else if (l.AuctionOpen && (cal.Hour >= 21f || cal.Hour < 12f)) Settle(l);
        }
    }

    // What he left, off the body (whatever nobody has already taken).
    private static void Gather(Loss l)
    {
        if (l.Body == null || !GodotObject.IsInstanceValid(l.Body)) return;
        var inv = l.Body.Inventory;
        foreach (var (id, n) in new List<(ItemId, int)>(inv.All()))
        {
            if (n <= 0) continue;
            inv.Remove(id, n);
            if (Items.Items.Of(id).Kind is ItemKind.Food or ItemKind.Drink || id == ItemId.Water) { l.Home.Stores.Add(id, n); continue; }
            l.Lots.Add((id, n));
        }
    }

    public static int PriceOf(ItemId id, int n)
    {
        float goods = Items.Items.Of(ItemId.TradeGoods).ValueMb;
        return Mathf.Max(1, Mathf.RoundToInt(Items.Items.Of(id).ValueMb * n / Mathf.Max(0.05f, goods)));
    }

    // A player buys a lot at the sale; the price goes to his widow if he
    // had one at the post, else into the stores.
    public static string Buy(Player.PlayerController p, Loss l, int lot)
    {
        if (!l.AuctionOpen || lot < 0 || lot >= l.Lots.Count) return "The sale is over.";
        var (id, n) = l.Lots[lot];
        int price = PriceOf(id, n);
        if (p.Inventory.Count(ItemId.TradeGoods) < price) return $"{Items.Items.Of(id).Name} goes for {price} pieces of goods; you have {p.Inventory.Count(ItemId.TradeGoods)}.";
        p.Inventory.Remove(ItemId.TradeGoods, price);
        Proceeds(l, price);
        p.Inventory.Add(id, n);
        l.Lots.RemoveAt(lot);
        Chronicle.Event($"{p.DisplayName} bought {l.Name}'s {Items.Items.Of(id).Name.ToLower()} at the sale of his effects.", p.GlobalPosition, p, l.Name, 0.8f);
        return $"You pay {price} pieces and take up {l.Name}'s {Items.Items.Of(id).Name.ToLower()}.";
    }

    private static void Proceeds(Loss l, int goods)
    {
        foreach (var n in Sim.Npcs) if (!n.IsDead && n.Spouse?.DisplayName == l.Name) { n.Inventory.Add(ItemId.TradeGoods, goods); return; }
        l.Home.Stores.Add(ItemId.TradeGoods, goods);
    }

    // What's left goes to whoever was closest to him, as a keepsake.
    private static void Settle(Loss l)
    {
        l.AuctionOpen = false; l.EffectsSettled = true;
        var given = new List<string>();
        foreach (var (id, n) in l.Lots)
        {
            Npc best = null; float bestBond = 0.5f;
            foreach (var m in Sim.Npcs)
            {
                if (m.IsDead || m.HomeStores != l.Home || m.DisplayName == l.Name) continue;
                float b = Losses.Bond(m, l) + GD.Randf() * 0.3f;
                if (b > bestBond) { best = m; bestBond = b; }
            }
            if (best == null) { l.Home.Stores.Add(id, n); continue; }
            best.Inventory.Add(id, n);
            best.Keepsakes.Add((id, l.Name));
            given.Add($"his {Items.Items.Of(id).Name.ToLower()} to {best.DisplayName}");
        }
        l.Lots.Clear();
        if (given.Count == 0) return;
        string what = l.Home is FortLedger ? $"the men sold {l.Name}'s effects: {string.Join(", ", given)}" : $"{l.Name}'s family gave away his things: {string.Join(", ", given)}";
        Chronicle.Shared(l.Home, what, 1.1f);
    }

    public static Loss OpenSaleAt(Household h)
    {
        foreach (var l in All) if (l.Home == h && l.AuctionOpen && l.Lots.Count > 0) return l;
        return null;
    }
}
