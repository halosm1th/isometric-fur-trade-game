using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Items;
using IsometricFurTradeGame.Scripts.World;

namespace IsometricFurTradeGame.Scripts.Economy;

// What one house believes about a Missouri town's corn: when it was seen, how
// much there was, how the crop came in, and how many mouths it has passed
// through since.
public class MarketBelief
{
    public string Town, Crop = "", Source = "";
    public int Day, Corn, CropYear, Hops;
}

// Who knows what about the corn (design doc 2, 11): nobody in the country
// reads a town's lodges from a distance. A camp or a post knows that the
// towns are there and that they grow corn - the country's common knowledge -
// and beyond that only what it has seen or been told. Seeing is a visit
// (Observe); being told is two houses meeting (Meet), and every telling is
// older than the thing told and a little blurred. Trips are decided from
// these beliefs and not from the town's stores, so a camp can walk a fortnight
// to a town that has no corn and come home to say so, and a bad harvest
// reaches the posts when somebody carries the news.
public static class MarketNews
{
    private static readonly Dictionary<string, Dictionary<string, MarketBelief>> Held = new();

    public static void Reset() => Held.Clear();

    public static string KeyOf(Household h) => h is BandLedger b ? b.Name : h.Title;

    private static Vector2 At(Household h) => h is BandLedger b ? IsoGrid.ToWorld(b.Camp) : IsoGrid.ToWorld(h.AccessCell);

    public static MarketBelief Get(Household h, CountryBand town) =>
        Held.TryGetValue(KeyOf(h), out var m) && m.TryGetValue(town.Name, out var b) ? b : null;

    // The towns are common knowledge to those whose country they lie in, and
    // the Assiniboine, who carried the corn north, know every one of them.
    public static bool Knows(Household h, CountryBand town)
    {
        if (Get(h, town) != null || h == Sim.Assiniboine) return true;
        return At(h).DistanceTo(At(town)) <= (h is FortLedger ? 20000f : 26000f);
    }

    // How much corn the house expects to find. A sighting of this year's crop
    // is worth what it was, less what the town has eaten and traded since;
    // short of that, all they have is the habit of the country - the towns
    // are full after the harvest, and lean before the next.
    public static float Expected(Household h, CountryBand town, GameCalendar cal)
    {
        var b = Get(h, town);
        if (b != null && b.CropYear == Gardens.CropYear(cal))
            return b.Corn * Mathf.Clamp(1f - (Plains.Today - b.Day) / 240f, 0f, 1f);
        return cal.Month is >= 10 or <= 3 ? 120f : 40f;
    }

    // The town this house would set out for: the one it expects most from,
    // nearer before farther, among those it knows and can reach. Null when
    // nothing it has heard is worth the walk.
    public static CountryBand Best(Household h, List<CountryBand> towns, float reach, float need, GameCalendar cal)
    {
        CountryBand best = null; float bestScore = 0f;
        var from = At(h);
        foreach (var t in towns)
        {
            if (t == h || !Knows(h, t)) continue;
            float d = from.DistanceTo(At(t));
            if (d > reach) continue;
            float e = Expected(h, t, cal);
            if (e < need) continue;
            float score = e / (1f + d / 8000f);
            if (score > bestScore) { bestScore = score; best = t; }
        }
        return best;
    }

    // Seen with their own eyes: the house now knows the town as it is today.
    public static void Observe(Household who, CountryBand town)
    {
        var cal = GameCalendar.Instance;
        if (cal == null || who == town) return;
        var (crop, year) = Gardens.CropWord(town, cal);
        Hold(who, new MarketBelief { Town = town.Name, Day = Plains.Today, Corn = town.Stores.Count(ItemId.Corn), Crop = crop, CropYear = year, Source = KeyOf(who) }, true);
    }

    // Two houses meet - a camp at a post, a train at a town - and swap what
    // they have heard of the towns. Only what is fresher than the listener's
    // own is taken, and every hop blurs the count.
    public static void Meet(Household a, Household b)
    {
        if (a == null || b == null || a == b) return;
        Share(a, b); Share(b, a);
    }

    private static void Share(Household from, Household to)
    {
        if (!Held.TryGetValue(KeyOf(from), out var m)) return;
        string toKey = KeyOf(to);
        foreach (var s in new List<MarketBelief>(m.Values))
        {
            if (s.Town == toKey || s.Hops >= 4) continue;   // a town needs no telling of itself
            int blurred = Mathf.RoundToInt(s.Corn * (float)GD.RandRange(0.75, 1.25));
            Hold(to, new MarketBelief { Town = s.Town, Day = s.Day, Corn = blurred, Crop = s.Crop, CropYear = s.CropYear, Hops = s.Hops + 1, Source = KeyOf(from) }, false);
        }
    }

    private static void Hold(Household h, MarketBelief nb, bool firsthand)
    {
        string k = KeyOf(h);
        if (!Held.TryGetValue(k, out var m)) Held[k] = m = new Dictionary<string, MarketBelief>();
        m.TryGetValue(nb.Town, out var old);
        if (old != null && old.Day >= nb.Day) return;
        m[nb.Town] = nb;
        // The head of the house hears of a crop that is new to him, and it goes
        // into the gossip like anything else.
        if (nb.Crop != "" && (old == null || old.CropYear != nb.CropYear || old.Crop != nb.Crop)) Tell(h, nb, firsthand);
    }

    private static void Tell(Household h, MarketBelief b, bool firsthand)
    {
        var cal = GameCalendar.Instance;
        var lead = h.Leader;
        if (cal == null || lead == null || !GodotObject.IsInstanceValid(lead) || lead.IsDead) return;
        string what = b.Crop switch
        {
            "failed" => "the corn failed and the lodges are near empty",
            "poor" => "the corn came in poor",
            "good" => "a good crop is in and there is corn to spare",
            _ => "the corn is in, a fair crop",
        };
        lead.Remember(new MemoryEntry
        {
            Text = firsthand ? $"Our men were at {b.Town}: {what}." : $"They say at {b.Town} that {what}.",
            Subject = b.Town, Day = cal.DayOfYear, Directness = firsthand ? 2 : 2 + b.Hops,
            Weight = b.Crop == "failed" ? 1.0f : 0.6f, Key = $"corn{b.Town}{b.CropYear}{b.Crop}",
        });
    }

    public static string Describe(Household h)
    {
        if (!Held.TryGetValue(KeyOf(h), out var m) || m.Count == 0) return "nothing";
        var parts = new List<string>();
        foreach (var b in m.Values) parts.Add($"{b.Town} {b.Corn} ({b.Crop}, d{b.Day}, hop{b.Hops})");
        return string.Join("; ", parts);
    }

    public static Godot.Collections.Dictionary Save()
    {
        var all = new Godot.Collections.Dictionary();
        foreach (var (holder, beliefs) in Held)
        {
            var d = new Godot.Collections.Dictionary();
            foreach (var (town, b) in beliefs)
                d[town] = new Godot.Collections.Dictionary { ["day"] = b.Day, ["corn"] = b.Corn, ["crop"] = b.Crop, ["year"] = b.CropYear, ["hops"] = b.Hops, ["src"] = b.Source };
            all[holder] = d;
        }
        return all;
    }

    public static void Load(Godot.Collections.Dictionary all)
    {
        Held.Clear();
        if (all == null) return;
        foreach (var hk in all)
        {
            var m = new Dictionary<string, MarketBelief>();
            foreach (var tk in hk.Value.AsGodotDictionary())
            {
                var d = tk.Value.AsGodotDictionary();
                m[tk.Key.AsString()] = new MarketBelief
                {
                    Town = tk.Key.AsString(), Day = d["day"].AsInt32(), Corn = d["corn"].AsInt32(), Crop = d["crop"].AsString(),
                    CropYear = d["year"].AsInt32(), Hops = d["hops"].AsInt32(), Source = d["src"].AsString(),
                };
            }
            Held[hk.Key.AsString()] = m;
        }
    }
}
