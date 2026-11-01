using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Items;
using IsometricFurTradeGame.Scripts.World;

namespace IsometricFurTradeGame.Scripts.Economy;

// The companies' purses (design doc 1, 3.6, 5): what the furs fetch when they
// are sold - the Bay's at the London sale when the ship is home, the
// Nor'Westers' at Montreal when the returns come down to Lachine, the
// Americans' at New York and St. Louis - against what the year costs: the
// goods bought in London, carrying them (the ship for the Bay; the canoes
// from Lachine, dear beyond anything, for the Nor'Westers), and the wages of
// every man on the books. All of it reckoned in made beaver, the country's
// own money, so it stands beside the trade.
//
// What the purse does: next year's outfit is ordered by this year's returns
// and what the company can pay for; a company in debt hires fewer men; and a
// company that owes a year's costs two years running is exhausted - it can
// neither order nor hire as it should, and the rivalry ends in a union
// (Economy/Union) whatever year it is.
public static class Companies
{
    public sealed class Account
    {
        public Faction Company;
        public string Name;
        public float Cash, SoldMb, SalesThisYear, CostsThisYear, LastSoldMb, LastCosts;
        public int DebtYears;
        public bool Exhausted;
        // The fur market as a weapon (design doc 13.6): furs kept back in the
        // warehouse off a poor sale, the year the company last threw all it
        // had on the market, and the year someone in the country urged it to.
        public float HeldMb;
        public int DumpYear = -9, UrgedYear = -9;
        public readonly List<string> Years = new();
    }

    // Goods bought in London (or New York) cost a part of what they fetch in
    // the country; carrying them is the rest. Per made beaver of goods.
    private static float GoodsCost(Faction f) => 0.4f;
    private static float Freight(Faction f) => f switch { Faction.NWC => 0.35f, Faction.American => 0.3f, _ => 0.12f };
    // A man on the books for a year, in made beaver: the Orkneyman's wage, the
    // voyageur's dearer one, and their keep in goods from the store.
    // (Set against what the country's posts take in a man's year here, which
    // is less than they took in 1811 - the wage follows the trade's scale.)
    private static float Wage(Faction f) => f switch { Faction.NWC => 3.5f, Faction.American => 3.5f, _ => 2.5f };
    // A company's whole year's goods at full strength, across all the places
    // they come in (Economy/Outfits), and what it sold the year before the
    // game begins - so the first order is a real one.
    private static float Nominal(Faction f) => f switch { Faction.NWC => 9500f, Faction.American => 3000f, _ => 8000f };
    private static float SoldBefore(Faction f) => f switch { Faction.NWC => 2500f, Faction.American => 400f, _ => 1800f };

    public static readonly Dictionary<Faction, Account> Books = Fresh();
    private static Dictionary<Faction, Account> Fresh() => new()
    {
        [Faction.HBC] = new Account { Company = Faction.HBC, Name = "the Hudson's Bay Company", Cash = 3000f, LastSoldMb = SoldBefore(Faction.HBC) },
        [Faction.NWC] = new Account { Company = Faction.NWC, Name = "the North West Company", Cash = 5000f, LastSoldMb = SoldBefore(Faction.NWC) },
        [Faction.American] = new Account { Company = Faction.American, Name = "the American companies", Cash = 2500f, LastSoldMb = SoldBefore(Faction.American) },
    };
    private static int _closedYear = -1;
    // Last years' gluts still weighing on the market, in made beaver over what it takes.
    private static float _overhang;
    public const float MarketTakes = 6000f;
    public static bool Log;

    public static void Reset()
    {
        Books.Clear(); foreach (var kv in Fresh()) Books[kv.Key] = kv.Value;
        LondonCommittee.SkimReviewed = 0f; LondonCommittee.Suspicions = 0; LondonCommittee.Recalled = false;
        _closedYear = -1; _overhang = 0f;
        Log = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--plainslog") >= 0;
    }

    public static Account Of(Faction f) => Books.TryGetValue(f, out var a) ? a : Books[Faction.HBC];

    // ---- the fur market --------------------------------------------------------------

    // What a made beaver of furs fetches this year: Napoleon's blockade shuts
    // the Baltic and the market with it until 1814; the peace opens it; and
    // two companies dumping their furs on the same sale beat the price down -
    // and the glut is still in the hatters' warehouses the year after, and
    // the year after that. The Nor'Westers' furs go to market by Montreal,
    // and in the war years the road to the sea is a soldier's road. `extra`
    // is a sale about to be made, which counts against its own price.
    public static float Price(Faction f, float extra = 0f)
    {
        var cal = GameCalendar.Instance;
        float p = 1f;
        if (Campaign.Has("blockade") && !Campaign.Has("blockade-lifted")) p *= 0.75f;
        else if (Campaign.Has("blockade-lifted")) p *= 1.08f;
        if (f == Faction.NWC && Campaign.Has("war-of-1812") && !Campaign.Has("peace_of_ghent")) p *= 0.9f;
        float sold = extra; foreach (var a in Books.Values) sold += a.SoldMb;
        if (sold > MarketTakes) p *= Mathf.Pow(MarketTakes / sold, 0.25f);   // a glutted sale
        if (_overhang > 0f) p *= Mathf.Pow(MarketTakes / (MarketTakes + _overhang), 0.3f);   // last years' gluts
        // The year's own luck at the sale, the same for everyone that year.
        int year = cal?.Year ?? 1811;
        p *= 0.92f + IsoGrid.Hash(new Vector2I(year, 7), 61) * 0.16f;
        return p;
    }

    // ---- what comes in, what goes out ---------------------------------------------------

    // Furs sold: the ship's hold in London, the returns at the edge of the map.
    public static void Sell(Faction f, float mb, string where)
    {
        var a = Of(f);
        if (f != Faction.American) mb = Release(a, mb, where);
        if (mb <= 0f) return;
        float price = Price(f, mb);
        float fetched = mb * price;
        a.SoldMb += mb; a.SalesThisYear += fetched; a.Cash += fetched;
        if (Log) GD.Print($"[companies] {a.Name} sells {mb:0} MB of furs {where} at {price:0.00}: {fetched:0} (purse {a.Cash:0})");
    }

    // What goes on the sale (design doc 13.6). A company with a law to hold
    // furs off a poor sale keeps that share in the warehouse while the price
    // is under par, and puts it all on the first good sale. One in debt
    // sells everything it has, warehouse and all: its creditors will not
    // wait. And one with furs in hand and a rival short of money may throw
    // the lot on the market at once, to beat the price down under him - at
    // the cost of its own sale, and the next years'.
    private static float Release(Account a, float mb, string where)
    {
        int year = GameCalendar.Instance?.Year ?? 1811;
        string co = Capital(a.Name);
        var at = IsoGrid.ToWorld(WorldLayout.GateOutside);
        if (WillDump(a, year))
        {
            float all = mb + a.HeldMb;
            a.HeldMb = 0f; a.DumpYear = year;
            Rivalry.Raise(10f, "furs dumped on the sale");
            Chronicle.Event($"{co} threw every fur it had on the sale {where}, {all:0} made beaver, to beat the price down under its rival.", at, null, a.Name, 1.8f);
            Sim.Headline($"{co} floods the sale {where} with {all:0} made beaver of furs. The price will be ruinous for everyone this year.");
            return all;
        }
        if (a.Cash < 0f && a.HeldMb > 0f)
        {
            float all = mb + a.HeldMb;
            if (Log) GD.Print($"[companies] {a.Name} is in debt: its creditors make it sell the {a.HeldMb:0} MB in the warehouse too");
            a.HeldMb = 0f;
            return all;
        }
        float hold = Laws.FursHeld(a.Company);
        float par = Price(a.Company, mb);
        if (hold > 0f && par < 0.95f)
        {
            float kept = mb * hold;
            a.HeldMb += kept;
            Chronicle.Event($"{co} kept {kept:0} made beaver of furs off a poor sale {where}, to wait for a better price.", at, null, a.Name, 0.8f);
            if (Log) GD.Print($"[companies] {a.Name} holds {kept:0} MB off the sale at {par:0.00} (warehouse {a.HeldMb:0})");
            return mb - kept;
        }
        if (par >= 1f && a.HeldMb > 0f)
        {
            float all = mb + a.HeldMb;
            Chronicle.Event($"{co} brought {a.HeldMb:0} made beaver of warehoused furs to a good sale {where}.", at, null, a.Name, 0.8f);
            if (Log) GD.Print($"[companies] {a.Name} releases {a.HeldMb:0} MB from the warehouse at {par:0.00}");
            a.HeldMb = 0f;
            return all;
        }
        return mb;
    }

    // Would the company flood the market this year? Furs in hand, a purse
    // that can bear a bad sale, a rival short of money, and bad blood - and
    // more readily if a man in the country has urged it.
    private static bool WillDump(Account a, int year)
    {
        if (a.HeldMb < 300f || a.DumpYear >= year - 1 || Rivalry.United) return false;
        var rival = Of(Rivalry.RivalOf(a.Company));
        if (rival == a || rival.Exhausted) return false;
        bool urged = a.UrgedYear == year;
        bool rivalShort = rival.Cash < 0f || rival.DebtYears >= 1 || urged && rival.Cash < rival.LastCosts;
        if (!rivalShort || a.Cash < a.LastCosts * 0.5f || Rivalry.Tension < 40f) return false;
        return GD.Randf() < 0.35f + (urged ? 0.35f : 0f);
    }

    // Could the company be urged to it, as things stand?
    public static bool CouldDump(Faction f) { var a = Of(f); return a.HeldMb >= 300f && a.DumpYear < (GameCalendar.Instance?.Year ?? 1811) - 1 && !Rivalry.United; }

    // The returns have come to where the company's furs leave the country
    // (Fort Coulonge for Lachine, Astoria for New York, Fort Manuel Lisa for
    // St. Louis): they go down to market, and are sold.
    public static void ReturnsHome(Faction f, Household at, string where)
    {
        float mb = 0f;
        foreach (var (id, n) in new List<(ItemId, int)>(at.Stores.All()))
        {
            var d = Items.Items.Of(id);
            if (d.Kind != ItemKind.Fur && id != ItemId.BuffaloRobe) continue;
            at.Stores.Remove(id, n);
            mb += d.ValueMb * n;
        }
        Sell(f, mb, where);
    }

    // The year's outfit, ordered by what the furs fetched last year and what
    // the company can pay for: a company that sold little orders less, one in
    // debt orders less again, an exhausted one hardly anything. What it costs -
    // the goods and the carrying - comes off the purse.
    public static float Order(Faction f, float nominal)
    {
        var a = Of(f);
        // The goods to buy last year's furs again and keep a stock in hand,
        // this entry's share of it.
        float share = nominal / Nominal(f);
        float want = Mathf.Clamp(a.LastSoldMb * 1.2f * share, nominal * 0.15f, nominal * 1.25f);
        if (a.Exhausted) want *= 0.4f;
        else if (a.Cash < 0f) want *= Mathf.Clamp(1f + a.Cash / Mathf.Max(1000f, a.LastCosts * 2f), 0.5f, 1f);
        float cost = want * (GoodsCost(f) + Freight(f));
        a.Cash -= cost; a.CostsThisYear += cost;
        if (Log) GD.Print($"[companies] {a.Name} orders {want:0} MB of goods (nominal {nominal:0}) for {cost:0} (purse {a.Cash:0})");
        return want;
    }

    // How many of the men wanted the company can take on this year.
    public static int Hires(Faction f, int wanted)
    {
        var a = Of(f);
        float n = wanted;
        if (a.Exhausted) n = 0f;
        else if (a.Cash < 0f) n *= 0.5f;
        // The voyageurs went into the Corps of Canadian Voyageurs and the
        // militia in the war years; Montreal had few to spare.
        if (f == Faction.NWC && Campaign.Has("war-of-1812") && !Campaign.Has("peace_of_ghent")) n *= 0.5f;
        return Mathf.Max(0, Mathf.RoundToInt(n));
    }

    // ---- the year's accounts ---------------------------------------------------------------

    // On the first of October, when the ship is gone and the returns are down:
    // the wages of every man on the books, the year's account, and whether a
    // company can go on.
    public static void Daily(GameCalendar cal)
    {
        if (!Net.Net.IsServer || cal.Month != 10 || cal.Day != 1 || _closedYear == cal.Year) return;
        _closedYear = cal.Year;
        var men = new Dictionary<Faction, int>();
        foreach (var n in Sim.Npcs)
        {
            if (n == null || !GodotObject.IsInstanceValid(n) || n.IsDead || n.HomeStores is not FortLedger fl) continue;
            if (n.Faction is not (Faction.HBC or Faction.NWC or Faction.American)) continue;
            men[fl.Company] = men.GetValueOrDefault(fl.Company) + 1;
        }
        // This year's glut weighs on the next sales; half of it is gone in a year.
        float soldAll = 0f; foreach (var a in Books.Values) soldAll += a.SoldMb;
        _overhang = _overhang * 0.5f + Mathf.Max(0f, soldAll - MarketTakes);
        foreach (var a in Books.Values)
        {
            a.HeldMb *= 0.94f;   // the moth, the damp, and the warehouse's rent
            float wages = men.GetValueOrDefault(a.Company) * Wage(a.Company);
            a.Cash -= wages; a.CostsThisYear += wages;
            float net = a.SalesThisYear - a.CostsThisYear;
            a.DebtYears = a.Cash < -a.CostsThisYear ? a.DebtYears + 1 : 0;
            string line = $"{cal.Year}: furs {a.SoldMb:0} MB sold for {a.SalesThisYear:0}, costs {a.CostsThisYear:0} ({men.GetValueOrDefault(a.Company)} men), {(net >= 0 ? "a profit of" : "a loss of")} {Mathf.Abs(net):0}; purse {a.Cash:0}";
            a.Years.Add(line);
            if (Log) GD.Print($"[companies] {a.Name} {line}");
            a.LastSoldMb = a.SoldMb; a.LastCosts = a.CostsThisYear;
            a.SoldMb = 0f; a.SalesThisYear = 0f; a.CostsThisYear = 0f;
            if (!a.Exhausted && a.DebtYears >= 2 && a.Company != Faction.American)
            {
                a.Exhausted = true;
                Campaign.Flag(a.Company == Faction.NWC ? "nwc_exhausted" : "hbc_exhausted");
                Sim.Headline($"{Capital(a.Name)} is exhausted: two years owing more than a year's costs. Its creditors will have a union.");
            }
            else if (net < 0f && a.Company != Faction.American)
                Chronicle.Event($"{Capital(a.Name)}'s year closed at a loss.", IsoGrid.ToWorld(WorldLayout.GateOutside), null, a.Name, 0.6f);
        }
    }

    private static string Capital(string s) => s.Length == 0 ? s : char.ToUpper(s[0]) + s[1..];

    // ---- save ------------------------------------------------------------------------------

    public static Godot.Collections.Dictionary Save()
    {
        var d = new Godot.Collections.Dictionary { ["closed"] = _closedYear, ["overhang"] = _overhang,
            ["audit"] = new Godot.Collections.Array { LondonCommittee.SkimReviewed, LondonCommittee.Suspicions, LondonCommittee.Recalled } };
        foreach (var a in Books.Values)
            d[a.Company.ToString()] = new Godot.Collections.Array { a.Cash, a.SoldMb, a.SalesThisYear, a.CostsThisYear, a.LastSoldMb, a.LastCosts, a.DebtYears, a.Exhausted, string.Join("\n", a.Years), a.HeldMb, a.DumpYear, a.UrgedYear };
        return d;
    }

    public static void Load(Godot.Collections.Dictionary d)
    {
        Reset();
        if (d == null) return;
        if (d.TryGetValue("closed", out var c)) _closedYear = c.AsInt32();
        if (d.TryGetValue("overhang", out var oh)) _overhang = oh.AsSingle();
        if (d.TryGetValue("audit", out var au)) { var x = au.AsGodotArray(); LondonCommittee.SkimReviewed = x[0].AsSingle(); LondonCommittee.Suspicions = x[1].AsInt32(); LondonCommittee.Recalled = x[2].AsBool(); }
        foreach (var a in Books.Values)
        {
            if (!d.TryGetValue(a.Company.ToString(), out var v)) continue;
            var x = v.AsGodotArray();
            a.Cash = x[0].AsSingle(); a.SoldMb = x[1].AsSingle(); a.SalesThisYear = x[2].AsSingle(); a.CostsThisYear = x[3].AsSingle();
            a.LastSoldMb = x[4].AsSingle(); a.LastCosts = x[5].AsSingle(); a.DebtYears = x[6].AsInt32(); a.Exhausted = x[7].AsBool();
            foreach (var l in x[8].AsString().Split('\n', System.StringSplitOptions.RemoveEmptyEntries)) a.Years.Add(l);
            if (x.Count > 11) { a.HeldMb = x[9].AsSingle(); a.DumpYear = x[10].AsInt32(); a.UrgedYear = x[11].AsInt32(); }
        }
    }
}
