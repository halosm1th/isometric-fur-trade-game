using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Items;
using IsometricFurTradeGame.Scripts.World;

namespace IsometricFurTradeGame.Scripts.Economy;

// What a post wants (design doc 13.1, 13.2): the other half of the trade.
// The companies brought goods into the country and took furs out, but they
// lived on the country's food - above all the brigades, which ran on
// pemmican from the plains posts; it is why the Pemmican War was fought.
//
//  - Provisions. A post wants a season's food for its people and the rations
//    for every brigade that leaves it, and buys them from the camps that come
//    in, in the goods the camp wants (StandardOfTrade.Trade).
//  - The indent. What a post's customers asked for and did not find is
//    written up, goes down with its returns to the depot (World/Convoys), is
//    added to the depot's own, and so on down the roads. Next season's outfit
//    to a post leans toward what it asked for (Bias); a company's year from
//    London, Montreal, St. Louis or Michilimackinac leans toward the whole
//    country's (YearOrder).
public static class PostWants
{
    private static readonly Dictionary<string, Dictionary<ItemId, float>> _indent = new();   // this season's, by post
    private static readonly Dictionary<string, Dictionary<ItemId, float>> _asked = new();    // the last one sent down
    private static readonly Dictionary<int, Dictionary<ItemId, float>> _inTransit = new();   // riding with a brigade's returns

    public static void Reset() { _indent.Clear(); _asked.Clear(); _inTransit.Clear(); }

    public static string NameOf(FortLedger f) => f == Sim.Fort ? "York Factory" : f == Sim.Outpost ? "Oxford House" : f is TradingPostLedger tp ? tp.Post.Name : f?.Title ?? "";

    // ---- provisions ---------------------------------------------------------------------

    private static readonly ItemId[] Provisions = { ItemId.Pemmican, ItemId.DriedMeat, ItemId.Corn, ItemId.Venison, ItemId.Fish };
    public static bool IsProvision(ItemId id) => System.Array.IndexOf(Provisions, id) >= 0;

    // A season's food for the post's people, and the brigades' rations: what
    // the post would hold, in day-rations (a man eats about 34 points a day).
    public static float FoodTarget(FortLedger post)
    {
        int mouths = 0;
        foreach (var n in Sim.Npcs) if (n.HomeStores == post && !n.IsDead) mouths++;
        float rations = Mathf.Max(4, mouths) * 120f;
        string name = NameOf(post);
        foreach (var run in Convoys.Runs)
            foreach (var leg in run.Legs)
            {
                if (leg.From != name) continue;
                float km = Convoys.PostPos(leg.From).DistanceTo(Convoys.PostPos(leg.To)) / WorldMap.PxPerKm * 1.3f;
                float days = km / Mathf.Max(10f, leg.Cargo == "outfit" ? run.Craft.Up : run.Craft.Down);
                rations += run.Boats * run.Men * days;
            }
        return rations;
    }

    // How many day-rations short the post is.
    public static float FoodShort(FortLedger post) => Mathf.Max(0f, FoodTarget(post) - post.Stores.FoodNutritionTotal / 34f);

    // ---- the indent ---------------------------------------------------------------------

    public static void Ask(FortLedger post, ItemId id, float n)
    {
        string k = NameOf(post);
        if (!_indent.TryGetValue(k, out var d)) _indent[k] = d = new();
        d[id] = d.GetValueOrDefault(id) + n;
    }

    // A brigade takes the returns down: the indent goes with it.
    public static void OnReturnsLeave(int convoyId, string from)
    {
        if (!_indent.TryGetValue(from, out var d) || d.Count == 0) return;
        _asked[from] = new Dictionary<ItemId, float>(d);
        _inTransit[convoyId] = d;
        _indent.Remove(from);
    }

    // ...and is read at the depot, which adds it to its own.
    public static void OnReturnsIn(int convoyId, string to)
    {
        if (!_inTransit.Remove(convoyId, out var d)) return;
        if (!_indent.TryGetValue(to, out var mine)) _indent[to] = mine = new();
        foreach (var (id, n) in d) mine[id] = mine.GetValueOrDefault(id) + n;
    }

    // How much more (or less) of a good an outfit to this post should carry:
    // its share of what the post last asked for, against an even share.
    public static float Bias(string post, ItemId id)
    {
        if (!_asked.TryGetValue(post, out var d) || d.Count == 0) return 1f;
        float total = 0f; foreach (var (k, n) in d) total += n * Items.Items.Of(k).ValueMb;
        if (total < 5f) return 1f;
        float share = d.GetValueOrDefault(id) * Items.Items.Of(id).ValueMb / total;
        return Mathf.Clamp(0.7f + share * 4f, 0.5f, 2.5f);
    }

    // A company's order for the year, from where its goods come onto the map:
    // the standing proportions, leaning toward what the country asked for.
    public static List<(ItemId Id, float Mb)> YearOrder(FortLedger entry)
    {
        var order = new List<(ItemId, float)>();
        string name = NameOf(entry);
        // The indent that reached the entry this year (and was not yet sent on).
        var asked = _indent.TryGetValue(name, out var d) ? d : _asked.GetValueOrDefault(name);
        float askedTotal = 0f;
        if (asked != null) foreach (var (k, n) in asked) askedTotal += n * Items.Items.Of(k).ValueMb;
        float baseTotal = 0f; foreach (var (_, mb) in Outfits.Mix) baseTotal += mb;
        foreach (var (id, mb) in Outfits.Mix)
        {
            float lean = askedTotal > 20f ? asked.GetValueOrDefault(id) * Items.Items.Of(id).ValueMb / askedTotal * baseTotal : mb;
            order.Add((id, mb * 0.6f + lean * 0.4f));
        }
        if (asked != null && askedTotal > 20f) { _asked[name] = new Dictionary<ItemId, float>(asked); _indent.Remove(name); }
        return order;
    }

    // ---- saving -------------------------------------------------------------------------

    private static Godot.Collections.Dictionary Pack(Dictionary<string, Dictionary<ItemId, float>> m)
    {
        var d = new Godot.Collections.Dictionary();
        foreach (var (k, v) in m) { var e = new Godot.Collections.Dictionary(); foreach (var (id, n) in v) e[id.ToString()] = n; d[k] = e; }
        return d;
    }

    private static void Unpack(Dictionary<string, Dictionary<ItemId, float>> into, Godot.Collections.Dictionary d)
    {
        into.Clear();
        if (d == null) return;
        foreach (var kv in d)
        {
            var m = new Dictionary<ItemId, float>();
            foreach (var x in kv.Value.AsGodotDictionary()) if (System.Enum.TryParse(x.Key.AsString(), out ItemId id)) m[id] = x.Value.AsSingle();
            into[kv.Key.AsString()] = m;
        }
    }

    public static Godot.Collections.Dictionary Save() => new() { ["indent"] = Pack(_indent), ["asked"] = Pack(_asked) };

    public static void Load(Godot.Collections.Dictionary d)
    {
        Reset();
        if (d == null) return;
        if (d.ContainsKey("indent")) Unpack(_indent, d["indent"].AsGodotDictionary());
        if (d.ContainsKey("asked")) Unpack(_asked, d["asked"].AsGodotDictionary());
    }
}
