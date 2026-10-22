using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Items;
using IsometricFurTradeGame.Scripts.World;

namespace IsometricFurTradeGame.Scripts.Economy;

// Money in the towns (design doc 12.5, docs/specs/global-trade.md 3.7).
// The fur country runs on goods and made beaver; the towns of the Lakes ran
// on coin and the store's book. Coin is an item like any other (ItemId.Coin,
// one to the penny of Halifax currency): it is in a house's box, in a
// shopper's pocket, in the market's till, and it moves when somebody carries
// it. Prices are the market's, dearer when it is short and cheaper when it is
// glutted; the merchants buy at a quarter under what they sell for.
//
// Coin comes into a town from outside - the garrison's pay and the priest's
// stipend, sent from Quebec or Washington (the metropole abstraction the
// design doc allows, 10.7) - and from the player selling furs. A house
// without the money buys on the store's book up to a limit, as most did, and
// pays it off the next time it comes with coin, so a poor house goes into
// debt before it goes hungry. The book runs the other way too: what the
// merchants buy and cannot pay for in coin is "store pay", written to the
// seller's credit and drawn on when the house next buys - which is how a
// farm's harvest reached the market in a town short of coin.
public static class Money
{
    // A made beaver at a Lakes merchant's counter: ten shillings.
    public const int PencePerMb = 120;
    // How far the store will trust a house: a pound, and ten shillings more for every mouth.
    public static int CreditLimit(TownHousehold h) => 240 + (h?.Alive() ?? 0) * 120;

    // What a thing costs at a town market in ordinary times, in pence.
    public static int BasePrice(ItemId id) => id switch
    {
        ItemId.Bread => 4,
        ItemId.Flour => 5,
        ItemId.Grain => 4,
        ItemId.Wheat => 3,
        ItemId.Corn => 3,
        ItemId.Roots => 2,
        ItemId.Fish => 3,
        ItemId.Venison => 5,
        ItemId.Goose => 5,
        ItemId.DriedMeat => 8,
        ItemId.Pemmican => 8,
        ItemId.Ale => 3,
        ItemId.Rum => 6,
        ItemId.Firewood => 2,
        ItemId.Water => 0,
        ItemId.Coin => 1,
        _ => Mathf.Max(1, Mathf.RoundToInt(Items.Items.Of(id).ValueMb * PencePerMb)),
    };

    private static bool IsFood(ItemId id) => Items.Items.Of(id).Kind == ItemKind.Food || id is ItemId.Grain or ItemId.Flour or ItemId.Wheat;

    // The market's price for one: food dearer as the market's stock runs
    // below a fortnight for the town, cheaper above it.
    public static int Price(TownHousehold market, ItemId id)
    {
        int p = BasePrice(id);
        if (market == null || !IsFood(id)) return p;
        int want = Mathf.Max(1, TownMouths(market.Town) * 14 / 6);   // a fortnight for the town, spread over the kinds of food
        float f = Mathf.Clamp(Mathf.Sqrt(want / (float)Mathf.Max(1, market.Stores.Count(id))), 0.6f, 2.5f);
        return Mathf.Max(1, Mathf.RoundToInt(p * f));
    }

    // What the merchants pay for one.
    public static int BuyPrice(TownHousehold market, ItemId id) => Mathf.Max(1, Mathf.FloorToInt(Price(market, id) * 0.75f));

    private static readonly Dictionary<Town, (int day, int n)> _mouths = new();
    private static int TownMouths(Town t)
    {
        int day = GameCalendar.Instance?.Day ?? 0;
        if (t == null) return 1;
        if (_mouths.TryGetValue(t, out var m) && m.day == day) return m.n;
        int n = 0;
        foreach (var h in t.Houses.Values) if (h != t.Market) n += h.Alive();
        _mouths[t] = (day, n);
        return n;
    }

    // ---- how it is written -----------------------------------------------------------

    // "2s 6d", "£1 4s", "9d" - or "$0.50" in an American town (five shillings
    // Halifax to the dollar).
    public static string Format(int pence, bool american = false)
    {
        if (american) return $"${pence / 60}.{pence % 60 * 100 / 60:00}";
        if (pence <= 0) return "nothing";
        int l = pence / 240, s = pence % 240 / 12, d = pence % 12;
        var parts = new List<string>();
        if (l > 0) parts.Add($"£{l}");
        if (s > 0) parts.Add($"{s}s");
        if (d > 0) parts.Add($"{d}d");
        return string.Join(" ", parts);
    }
    public static string Format(int pence, Town t) => Format(pence, t?.Faction == Faction.American);

    // ---- the store's book ---------------------------------------------------------------

    // What a house owes the store; less than nothing is store pay owed to it.
    private static readonly Dictionary<string, int> _book = new();
    public static int Owed(TownHousehold h) => h == null ? 0 : _book.GetValueOrDefault(h.FullKey);
    public static int StoreCredit(TownHousehold h) => Mathf.Max(0, -Owed(h));
    private static void SetOwed(TownHousehold h, int v) { if (v == 0) _book.Remove(h.FullKey); else _book[h.FullKey] = v; }

    // Store pay: what the merchants owe a seller they could not pay in coin.
    public static void CreditTo(TownHousehold h, int pence) { if (h != null && pence > 0) SetOwed(h, Owed(h) - pence); }

    // A house pays down its account out of what the buyer carries.
    public static int PayDown(TownHousehold house, Npc payer, TownHousehold market)
    {
        int owed = Owed(house);
        if (owed <= 0) return 0;
        int paid = payer.Inventory.TakeUpTo(ItemId.Coin, owed);
        market.Stores.Add(ItemId.Coin, paid);
        SetOwed(house, owed - paid);
        return paid;
    }

    // Buying one lot of a thing at the market, paid out of the buyer's
    // pocket, or put on the house's account if it has credit left. False if
    // neither.
    public static bool Pay(Npc buyer, TownHousehold house, TownHousehold market, int pence)
    {
        // Store pay owed to the house is spent first, then coin, then the book.
        int credit = Mathf.Min(StoreCredit(house), pence);
        if (credit > 0) { SetOwed(house, Owed(house) + credit); pence -= credit; if (pence == 0) return true; }
        if (buyer.Inventory.Count(ItemId.Coin) >= pence) { buyer.Inventory.Remove(ItemId.Coin, pence); market.Stores.Add(ItemId.Coin, pence); return true; }
        int cash = buyer.Inventory.TakeUpTo(ItemId.Coin, pence);
        int rest = pence - cash;
        if (house == null || Owed(house) + rest > CreditLimit(house)) { buyer.Inventory.Add(ItemId.Coin, cash); return false; }
        market.Stores.Add(ItemId.Coin, cash);
        SetOwed(house, Owed(house) + rest);
        return true;
    }

    // ---- coin from outside --------------------------------------------------------------

    // The first of the month: the paymaster's chest for each garrison (a
    // private's shilling a day less stoppages, an officer's several), and the
    // priest's stipend - into the house's box.
    private static int _paidMonth = -1;
    public static void Day()
    {
        var cal = GameCalendar.Instance;
        if (cal == null || cal.Day != 1 || _paidMonth == cal.Month + cal.Year * 12) return;
        _paidMonth = cal.Month + cal.Year * 12;
        ShipSurplus();
        StoreGoods();
        Commissariat();
        OrderFlour();
        foreach (var h in Towns.Households())
        {
            int pay = 0;
            foreach (var m in h.Members)
            {
                if (m.IsDead) continue;
                pay += m.Role switch { Role.Soldier => 180, Role.Officer => 900, Role.Priest => 600, _ => 0 };
            }
            if (pay > 0) h.Stores.Add(ItemId.Coin, pay);
        }
    }

    // The merchants' trade with the outside (the metropole abstraction, 10.7):
    // once a month, any grain, meal, corn or potatoes in the market beyond what
    // the town will eat until the next harvest is in, and a month over, goes
    // down the lakes - to the commissariat, to Montreal - and the money comes
    // back to the till. Without it a good harvest would fill the market and
    // empty the till for good; with too low a mark it ships the winter's bread.
    private static readonly ItemId[] Exports = { ItemId.Grain, ItemId.Flour, ItemId.Corn, ItemId.Roots, ItemId.Bread };
    private static void ShipSurplus()
    {
        foreach (var t in Towns.All)
        {
            if (!t.Standing || t.Market is not { } m) continue;
            var cal = GameCalendar.Instance;
            int doy = (int)(cal?.DayOfYear ?? 0f);
            int toHarvest = (doy < 250 ? 250 - doy : 365 - doy + 250) + 30;
            int keep = Mathf.Max(1, TownMouths(t) * toHarvest / 3);   // a third of the year's food in each of the main kinds
            foreach (var id in Exports)
            {
                int over = m.Stores.Count(id) - keep;
                if (over <= 0) continue;
                int n = over / 2;
                m.Stores.Remove(id, n);
                m.Stores.Add(ItemId.Coin, n * BuyPrice(m, id));
            }
        }
    }

    // Store goods - cloth, tea, sugar, tools, crockery - come up from Montreal
    // once a month for what the till can spare, as much as the town will want;
    // and the houses' own wear out (a lot for every four mouths a month). This
    // is where farm money goes back into the town.
    public const int StoreGoodsCost = 60;
    private static void StoreGoods()
    {
        foreach (var t in Towns.All)
        {
            if (!t.Standing || t.Market is not { } m) continue;
            int want = TownMouths(t) / 2 - m.Stores.Count(ItemId.TradeGoods);
            int spare = m.Stores.Count(ItemId.Coin) - (400 + t.Houses.Count * 20) / 2;
            int n = Mathf.Min(want, spare / StoreGoodsCost);
            if (n > 0) { m.Stores.Remove(ItemId.Coin, n * StoreGoodsCost); m.Stores.Add(ItemId.TradeGoods, n); }
            foreach (var h in t.Houses.Values) if (h != m) h.Stores.TakeUpTo(ItemId.TradeGoods, Mathf.Max(1, h.Alive() / 4));
        }
    }

    // The King's (or the Republic's) provisions: a garrison was fed from the
    // commissariat's stores, shipped up the lakes, not off its town's market.
    // Once a month, a month's rations for everyone in the barracks - bread,
    // salt meat, and the garrison garden's roots against the scurvy.
    private static void Commissariat()
    {
        foreach (var h in Towns.Households())
        {
            if (h.Key != "garrison") continue;
            int n = h.Alive();
            if (n == 0) continue;
            h.Stores.Add(ItemId.Bread, n * 24);
            h.Stores.Add(ItemId.DriedMeat, n * 6);
            h.Stores.Add(ItemId.Roots, n * 8);
        }
    }

    // A market under a fortnight's food for its town: the merchants order
    // flour and potatoes up the lakes for what the till can pay, at the
    // Montreal price and the freight.
    private const int FlourLanded = 4, RootsLanded = 2;
    private static void OrderFlour()
    {
        foreach (var t in Towns.All)
        {
            if (!t.Standing || t.Market is not { } m) continue;
            int fortnight = TownMouths(t) * 14 * 4 / 3;   // pieces, at about a piece and a third a day
            int have = m.Stores.Count(ItemId.Bread) + m.Stores.Count(ItemId.Corn) + m.Stores.Count(ItemId.Roots) + (m.Stores.Count(ItemId.Flour) + m.Stores.Count(ItemId.Grain)) * 2;
            if (have >= fortnight) continue;
            // Two parts meal (two loaves to the measure) to one of potatoes, so
            // a town living on what comes up the lakes isn't living on bread alone.
            int short_ = fortnight * 2 - have;
            int spare = m.Stores.Count(ItemId.Coin) - 120;
            int roots = Mathf.Min(short_ / 3, spare / 2 / RootsLanded);
            if (roots > 0) { m.Stores.Remove(ItemId.Coin, roots * RootsLanded); m.Stores.Add(ItemId.Roots, roots); spare -= roots * RootsLanded; }
            int n = Mathf.Min((short_ - roots) / 2, spare / FlourLanded);
            if (n <= 0) continue;
            m.Stores.Remove(ItemId.Coin, n * FlourLanded);
            m.Stores.Add(ItemId.Flour, n);
        }
    }

    // A new game: what each house has in its box, by what the house is, and
    // the merchants' float in the market's till.
    public static void Seed(TownHousehold h)
    {
        if (h.Key == "market") { h.Stores.Add(ItemId.Coin, 400 + TownMouths(h.Town) * 6); h.Stores.Add(ItemId.TradeGoods, TownMouths(h.Town) / 2); return; }
        int best = 0;
        foreach (var m in h.Members)
            best = Mathf.Max(best, m.Role switch { Role.Merchant => 2400, Role.Officer => 1200, Role.Priest => 600, Role.Artisan => 360, Role.Farmer => 300, Role.Soldier => 60, _ => 120 });
        h.Stores.Add(ItemId.Coin, best);
    }

    // Dev (Fields' --farmreport): where a town's money is.
    public static string Report(Town t)
    {
        int houses = 0, owed = 0, debtors = 0, broke = 0, farms = 0, merchants = 0, credit = 0;
        foreach (var h in t.Houses.Values)
        {
            if (h == t.Market) continue;
            int c = h.Stores.Count(ItemId.Coin);
            houses += c;
            if (Owed(h) > 0) { owed += Owed(h); debtors++; }
            credit += StoreCredit(h);
            if (c == 0) broke++;
            foreach (var m in h.Members) { if (m.Role == Role.Farmer) { farms += c; break; } if (m.Role == Role.Merchant) { merchants += c; break; } }
        }
        return $"till {Format(t.Market?.Stores.Count(ItemId.Coin) ?? 0)} | houses {Format(houses)} (farms {Format(farms)}, merchants {Format(merchants)}), {broke} with none | on the book {Format(owed)} from {debtors} houses, store pay owed {Format(credit)}";
    }

    // ---- saves ---------------------------------------------------------------------------

    public static void Clear() { _book.Clear(); _paidMonth = -1; }
    public static Godot.Collections.Dictionary Save()
    {
        var d = new Godot.Collections.Dictionary();
        foreach (var (k, v) in _book) d[k] = v;
        d["_paid"] = _paidMonth;
        return d;
    }
    public static void Restore(Godot.Collections.Dictionary d)
    {
        Clear();
        foreach (var (k, v) in d)
        {
            if (k.AsString() == "_paid") _paidMonth = v.AsInt32();
            else _book[k.AsString()] = v.AsInt32();
        }
    }
}
