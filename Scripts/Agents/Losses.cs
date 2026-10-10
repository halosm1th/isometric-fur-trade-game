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

    // The lot under the hammer: the price it stands at, who holds it, the
    // most the keenest of the men will go to, and when it's gone if nobody
    // speaks up (game hours on the sale's clock).
    public int Price;
    public Agent HighBidder;
    public Npc Keenest; public int KeenestMax;
    public float QuietHours;
    public bool LotCried;
    public int Unsold;              // lots cried with no bid, put by for the end

    public int DaysAgo => (int)Mathf.PosMod((GameCalendar.Instance?.DayOfYear ?? 0f) - Day, 365f);
    public bool Fresh => DaysAgo <= 40;
    public bool HasLot => AuctionOpen && Lots.Count > 0;
}

public static class Losses
{
    public static readonly List<Loss> All = new();
    private const float LotHours = 0.4f;   // a lot goes after this long without a bid

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

    // From the population tick: the evening after a death his effects are
    // gathered; a company post cries them one at a time among the men (the
    // HBC did), a band's family gives them away.
    public static void Tick()
    {
        var cal = GameCalendar.Instance;
        if (cal == null) return;
        foreach (var l in All)
        {
            if (l.NewSleeper == null)
                foreach (var n in Sim.Npcs) if (!n.IsDead && n.HomeStores == l.Home && n.BedCell == l.Bed && n.DisplayName != l.Name) { l.NewSleeper = n.DisplayName; break; }
            if (l.EffectsSettled || l.DaysAgo < 1) continue;
            if (!l.AuctionOpen)
            {
                if (cal.Hour < 18f) continue;
                Gather(l);
                if (l.Lots.Count == 0) { l.EffectsSettled = true; continue; }
                if (l.Home is FortLedger && Auctioneer(l) != null)
                {
                    l.AuctionOpen = true;
                    OpenLot(l);
                    Sim.Notice($"At {Chronicle.PlaceOf(l.Home)} they are selling {l.Name}'s effects among the men, one thing at a time. Talk to {Auctioneer(l).DisplayName} to bid.");
                }
                else Settle(l);
                continue;
            }
            if (cal.Hour >= 21.5f || cal.Hour < 12f || Auctioneer(l) == null) { Settle(l); continue; }
            if (!l.LotCried) CryLot(l);
            l.QuietHours += cal.DeltaGameHours;
            if (l.QuietHours >= LotHours) Knock(l);
        }
    }

    // Whoever runs the post cries the sale.
    public static Npc Auctioneer(Loss l)
    {
        var lead = l.Home.Leader;
        return lead != null && GodotObject.IsInstanceValid(lead) && !lead.IsDead ? lead : null;
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

    // ---- the hammer -----------------------------------------------------------------

    // A lot goes up: each man of the house settles in his head what he'd
    // give - more if he was close to the dead man, more if he's without
    // one himself - and the keenest bids against the rest up to that.
    private static void OpenLot(Loss l)
    {
        var (id, n) = l.Lots[0];
        int worth = PriceOf(id, n);
        l.Price = Mathf.Max(1, worth / 2);
        l.HighBidder = null; l.Keenest = null; l.KeenestMax = 0; l.QuietHours = 0f; l.LotCried = false;
        int second = 0;
        var crier = Auctioneer(l);
        foreach (var m in Sim.Npcs)
        {
            if (m.IsDead || m.HomeStores != l.Home || m == crier || m.DisplayName == l.Name) continue;
            float keen = 0.35f + 0.2f * Mathf.Min(Bond(m, l), 4f) + (m.Inventory.Has(id) ? 0f : 0.35f) + m.Personality.Ambition * 0.1f;
            int max = Mathf.RoundToInt(worth * keen * (float)GD.RandRange(0.8, 1.2));
            if (max > l.KeenestMax) { second = l.KeenestMax; l.KeenestMax = max; l.Keenest = m; }
            else if (max > second) second = max;
        }
        // Among the men alone the price climbs to just past the second man's limit.
        if (l.Keenest != null && l.KeenestMax >= l.Price)
        {
            l.Price = Mathf.Clamp(second + 1, l.Price, l.KeenestMax);
            l.HighBidder = l.Keenest;
        }
    }

    private static void CryLot(Loss l)
    {
        l.LotCried = true;
        var (id, n) = l.Lots[0];
        var crier = Auctioneer(l);
        string item = ItemName(id, n);
        crier?.Bark(Pick($"{Barks.CallName(l.Name)}'s {item}. Who'll give {Mathf.Max(1, PriceOf(id, n) / 2)}?", $"Here's {Barks.CallName(l.Name)}'s {item}, lads. What am I bid?"));
        if (l.HighBidder is Npc b) b.Bark(l.Price == 1 ? "One!" : $"{l.Price}!");
    }

    // Someone bids: the man holding it goes on if he still wants it more.
    public static string Bid(Player.PlayerController p, Loss l)
    {
        if (!l.HasLot) return "The sale is over.";
        if (l.HighBidder == p) return "The lot is yours at that price unless somebody speaks up.";
        int offer = l.HighBidder == null ? l.Price : l.Price + 1;
        if (p.Inventory.Count(ItemId.TradeGoods) < offer) return $"You'd need {offer} pieces of goods; you have {p.Inventory.Count(ItemId.TradeGoods)}.";
        var (id, n) = l.Lots[0];
        l.QuietHours = 0f;
        p.Bark($"{offer}.");
        if (l.Keenest != null && l.KeenestMax > offer)
        {
            l.Price = offer + 1; l.HighBidder = l.Keenest;
            l.Keenest.Bark(Pick($"{l.Price}!", $"{l.Price}, and be damned.", $"{l.Price}. It was my friend's."));
            return $"{l.Keenest.DisplayName} goes to {l.Price} for the {ItemName(id, n)}.";
        }
        l.Price = offer; l.HighBidder = p;
        if (l.Keenest != null && l.Keenest.Personality.Talkative > 0.4f) l.Keenest.Bark(Pick("Take it, then.", "Too rich for me."));
        return $"The {ItemName(id, n)} stands at {offer}, to you.";
    }

    // Going, going: the lot goes to whoever holds it, or, if nobody bid,
    // stays in the pile for the end.
    private static void Knock(Loss l)
    {
        var (id, n) = l.Lots[0];
        var crier = Auctioneer(l);
        l.Lots.RemoveAt(0);
        string item = ItemName(id, n);
        if (l.HighBidder is Player.PlayerController p && GodotObject.IsInstanceValid(p) && p.Inventory.Count(ItemId.TradeGoods) >= l.Price)
        {
            p.Inventory.Remove(ItemId.TradeGoods, l.Price);
            p.Inventory.Add(id, n);
            p.Keepsakes.Add((id, l.Name));
            Proceeds(l, l.Price);
            crier?.Bark($"Gone, to {Barks.CallName(p)}, for {l.Price}.");
            Chronicle.Event($"{p.DisplayName} bought {l.Name}'s {item} at the sale of his effects for {l.Price}.", p.GlobalPosition, p, l.Name, 0.8f);
        }
        else if (l.HighBidder is Npc m && !m.IsDead)
        {
            int paid = m.Inventory.Count(ItemId.TradeGoods) >= l.Price ? l.Price : m.Inventory.Count(ItemId.TradeGoods);
            m.Inventory.Remove(ItemId.TradeGoods, paid);   // the rest stopped from his wages
            m.Inventory.Add(id, n);
            m.Keepsakes.Add((id, l.Name));
            Proceeds(l, l.Price);
            crier?.Bark($"Gone, to {Barks.CallName(m)}, for {l.Price}.");
            Chronicle.Event($"{m.DisplayName} bought {l.Name}'s {item} at the sale for {l.Price}.", m.GlobalPosition, m, l.Name, 0.6f);
        }
        else
        {
            l.Lots.Add((id, n));          // back on the pile, for the end
            l.Unsold++;
            crier?.Bark(Pick("No? Put it by.", "Nobody? It'll keep."));
        }
        if (l.Lots.Count == 0 || l.Unsold >= l.Lots.Count) { Settle(l); return; }
        OpenLot(l);
    }

    private static void Proceeds(Loss l, int goods)
    {
        foreach (var n in Sim.Npcs) if (!n.IsDead && n.Spouse?.DisplayName == l.Name) { n.Inventory.Add(ItemId.TradeGoods, goods); return; }
        l.Home.Stores.Add(ItemId.TradeGoods, goods);
    }

    // What nobody bought goes to whoever was closest to him, as a keepsake.
    private static void Settle(Loss l)
    {
        l.AuctionOpen = false; l.EffectsSettled = true;
        var given = new List<string>();
        foreach (var (id, n) in l.Lots)
        {
            // Closest to him first; failing anyone close, whichever of the house wants it.
            Npc best = null; float bestBond = float.MinValue;
            foreach (var m in Sim.Npcs)
            {
                if (m.IsDead || m.HomeStores != l.Home || m.DisplayName == l.Name) continue;
                float b = Bond(m, l) + GD.Randf() * 0.3f;
                if (b > bestBond) { best = m; bestBond = b; }
            }
            if (best == null) { l.Home.Stores.Add(id, n); continue; }
            best.Inventory.Add(id, n);
            best.Keepsakes.Add((id, l.Name));
            given.Add($"his {ItemName(id, n)} to {best.DisplayName}");
        }
        l.Lots.Clear();
        if (given.Count == 0) { if (l.Home is FortLedger) Sim.Notice($"The sale of {l.Name}'s effects is over."); return; }
        string what = l.Home is FortLedger ? $"what nobody bought of {l.Name}'s went to those closest to him: {string.Join(", ", given)}" : $"{l.Name}'s family gave away his things: {string.Join(", ", given)}";
        Chronicle.Shared(l.Home, what, 0.9f);   // told at the fire, not mooned over
        Sim.Notice(char.ToUpper(what[0]) + what.Substring(1) + ".");
    }

    public static Loss OpenSaleAt(Household h)
    {
        foreach (var l in All) if (l.Home == h && l.HasLot) return l;
        return null;
    }

    public static string ItemName(ItemId id, int n) => Items.Items.Of(id).Name.ToLower() + (n > 1 ? $" (x{n})" : "");
    private static string Pick(params string[] o) => o[(int)(GD.Randi() % (uint)o.Length)];

    // ---- saving --------------------------------------------------------------------

    public static Godot.Collections.Array Save(System.Func<Household, string> key)
    {
        var a = new Godot.Collections.Array();
        foreach (var l in All)
        {
            var lots = new List<string>();
            foreach (var (id, n) in l.Lots) lots.Add($"{(int)id}:{n}");
            a.Add(new Godot.Collections.Dictionary
            {
                ["name"] = l.Name, ["home"] = key(l.Home), ["bed"] = new Godot.Collections.Array { l.Bed.X, l.Bed.Y }, ["day"] = l.Day,
                ["settled"] = l.EffectsSettled || l.AuctionOpen,   // a sale cut off by a save finishes as keepsakes
                ["lots"] = string.Join("|", lots), ["sleeper"] = l.NewSleeper ?? "", ["told"] = l.NewSleeperTold,
            });
        }
        return a;
    }

    public static void Load(Godot.Collections.Array a, System.Func<string, Household> home, System.Func<string, Npc> byName)
    {
        All.Clear();
        foreach (var v in a)
        {
            var d = v.AsGodotDictionary();
            var h = home(d["home"].AsString());
            if (h == null) continue;
            var bed = d["bed"].AsGodotArray();
            var l = new Loss
            {
                Name = d["name"].AsString(), Home = h, Bed = new Vector2I(bed[0].AsInt32(), bed[1].AsInt32()), Day = d["day"].AsInt32(),
                EffectsSettled = d["settled"].AsBool(), NewSleeper = d["sleeper"].AsString() == "" ? null : d["sleeper"].AsString(), NewSleeperTold = d["told"].AsBool(),
            };
            l.Body = byName(l.Name);
            foreach (var s in d["lots"].AsString().Split('|', System.StringSplitOptions.RemoveEmptyEntries))
            {
                var p = s.Split(':');
                l.Lots.Add(((ItemId)int.Parse(p[0]), int.Parse(p[1])));
            }
            if (l.EffectsSettled && l.Lots.Count > 0) { l.EffectsSettled = false; l.AuctionOpen = true; Settle(l); }
            All.Add(l);
        }
    }
}
