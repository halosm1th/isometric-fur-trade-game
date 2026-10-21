using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Items;
using IsometricFurTradeGame.Scripts.World;

namespace IsometricFurTradeGame.Scripts.Economy;

// Where the goods come from (design doc 12.1, 12.5, 13.2). Nothing is
// conjured at a post: every bale is somewhere - in a warehouse, in a boat on
// a road, on a man's back - and gets from one to the other by being carried.
//
// The goods enter the map at its edges, from the companies' own off-map
// nodes (design doc 10.1.5): London's ship at York (World/Ship.cs), and the
// North West Company's Montreal canoes at Sault Ste. Marie, where the road
// from Lachine comes in off the map at midsummer. From there everything
// moves by the brigades (World/Convoys.cs, the one logistics system): the
// great roads in Data/World/brigade_runs.json, and - generated here - a
// feeder run for every lesser post from the nearest depot of its company: a
// couple of canoes in the woods, a pack train on the plains, outfit up after
// the brigades are in and returns down after the spring hunt. A feeder can be
// late, robbed, frozen in or lost like any brigade, and a post it does not
// reach winters on what it has.
public static class Outfits
{
    public const string Entry = "Sault Ste. Marie";
    // A year's goods for a company, in made beaver: on the scale of the
    // returns - York alone sent home twenty thousand and more a year.
    public const float YearMb = 8000f;

    // Where each company's goods come onto the map, and when (design doc
    // 10.1.5): the Montreal canoes in at the Sault in June; the Pacific Fur
    // Company's goods up the Columbia from the ship at Astoria in August; the
    // Missouri Fur Company's keelboats up from St. Louis in October; the British
    // goods for the Dakota up the Mississippi from Michilimackinac by way of
    // Prairie du Chien to Dickson's post at Lake Traverse in September. London's
    // ship at York is World/Ship.cs. A campaign can stop one with its flag.
    private static readonly (string Post, int Month, float Mb, string Flag, string Story)[] Entries =
    {
        (Entry, 6, YearMb, "blockade:montreal", "The canoes from Montreal are in at Sault Ste. Marie with the North West Company's goods for the year."),
        ("Fort Okanogan", 8, 1500f, "blockade:pacific", "The Pacific Fur Company's goods are up the Columbia from the ship at Astoria."),
        ("Fort Manuel Lisa", 10, 1500f, "blockade:missouri", "The keelboats are up the Missouri from St. Louis with the year's goods."),
        ("Lake Traverse", 9, 1500f, "blockade:mississippi", "Dickson's canoes are up from Prairie du Chien with the British goods for the Dakota trade."),
    };
    private static readonly System.Collections.Generic.Dictionary<string, int> _entryYears = new();

    private static bool _begun;
    public static void Reset() { _entryYears.Clear(); _begun = false; }
    public static Godot.Collections.Array Save()
    {
        var a = new Godot.Collections.Array();
        foreach (var kv in _entryYears) a.Add(new Godot.Collections.Array { kv.Key, kv.Value });
        return a;
    }
    public static void Load(Godot.Collections.Array a)
    {
        Reset();
        _begun = true;
        if (a == null) return;
        foreach (var v in a) { var e = v.AsGodotArray(); if (e.Count == 2) _entryYears[e[0].AsString()] = e[1].AsInt32(); }
    }

    // What a year's outfit is made of, by the made beaver's worth of each per
    // hundred: London's own proportions, Montreal's near enough.
    public static readonly (ItemId Id, float Mb)[] Mix =
    {
        (ItemId.Musket, 14f), (ItemId.Powder, 16f), (ItemId.Kettle, 6f), (ItemId.PointBlanket, 10f), (ItemId.Stroud, 8f),
        (ItemId.Tobacco, 10f), (ItemId.SteelTrap, 6f), (ItemId.Axe, 4f), (ItemId.Knife, 4f), (ItemId.Awl, 2f), (ItemId.Beads, 4f),
        (ItemId.IceChisel, 2f), (ItemId.FishingNet, 2f), (ItemId.Flint, 2f), (ItemId.Rum, 4f), (ItemId.TradeGoods, 10f),
    };

    public static void Week(GameCalendar cal)
    {
        // A world that begins after this year's goods have come in finds them
        // already in the warehouses: York's from the ship, the others' from the
        // canoes and boats - it is the world as it stood that day.
        if (!_begun)
        {
            _begun = true;
            int today = DayOfYear(cal.Month, cal.Day);
            if (today > ShipDay && Sim.Fort != null && !_entryYears.ContainsKey("York Factory"))
            {
                foreach (var (id, mb) in Mix) Sim.Fort.Stores.Add(id, Mathf.RoundToInt(mb * YearMb / 100f / Items.Items.Of(id).ValueMb));
                _entryYears["York Factory"] = cal.Year;
            }
            foreach (var e in Entries)
                if (today > DayOfYear(e.Month, 5) && !_entryYears.ContainsKey(e.Post) && Posts.Named(e.Post)?.Ledger is { } led)
                {
                    foreach (var (id, mb) in Mix) led.Stores.Add(id, Mathf.RoundToInt(mb * e.Mb / 100f / Items.Items.Of(id).ValueMb));
                    _entryYears[e.Post] = cal.Year;
                }
        }
        foreach (var e in Entries)
        {
            if (cal.Month != e.Month || _entryYears.TryGetValue(e.Post, out int y) && y == cal.Year) continue;
            _entryYears[e.Post] = cal.Year;
            var at = Posts.Named(e.Post)?.Ledger;
            if (at == null) continue;
            if (Campaign.Has(e.Flag)) { Sim.Notice($"No goods came in at {e.Post} this year."); continue; }
            foreach (var (id, mb) in PostWants.YearOrder(at)) at.Stores.Add(id, Mathf.RoundToInt(mb * e.Mb / 100f / Items.Items.Of(id).ValueMb));
            if (Plains.Log) GD.Print($"[trade] {cal.DateString} {e.Story} ({e.Mb:0} MB)");
            Chronicle.Event(e.Story, IsoGrid.ToWorld(at.AccessCell), subject: e.Post, weight: 1f);
        }
    }

    // ---- the feeder runs -----------------------------------------------------------

    // Called by Convoys when it reads the great roads: one run from the
    // nearest supplied depot to every post the roads do not reach, leaving a
    // few days after that depot's own outfit is due in, and the lake run from
    // the Sault to Fort William.
    public static void AddFeederRuns(List<ConvoyRun> runs, Dictionary<string, CraftKind> kinds)
    {
        var canoe = kinds.TryGetValue("canoe", out var c) ? c : new CraftKind();
        // A lesser post's outfit goes up in north canoes, a few pieces each
        // under the crew's own gear and food, or on a pack train on the plains:
        // what they hold is a post's year, not a depot's.
        var north = new CraftKind { Name = "north canoe", Up = canoe.Up * 0.9f, Down = canoe.Down, LotsPerBoat = 8, Sprite = canoe.Sprite, Scale = canoe.Scale * 0.85f };
        var horses = new CraftKind { Name = "pack train", Up = 30f, Down = 32f, LotsPerBoat = 2, Sprite = "", Scale = 0.6f };
        if (Posts.Named(Entry) != null && Posts.Named("Fort William") != null)
        {
            var lake = new ConvoyRun { Name = "the canoes on the lake", Company = Faction.NWC, Craft = canoe, Boats = 8, Men = 8 };
            lake.Stops.Add(Entry); lake.Stops.Add("Fort William");
            lake.Legs.Add(new ConvoyLeg { From = Entry, To = "Fort William", Cargo = "outfit", Month = 6, Day = 8 });
            lake.Legs.Add(new ConvoyLeg { From = "Fort William", To = Entry, Cargo = "returns", Month = 7, Day = 20 });
            runs.Add(lake);
        }

        // When the year's goods come in at each place: where they come onto
        // the map, then along every outfit leg (a day for every day's paddling
        // and a third again for the carrying places). A leg due to leave a
        // depot within a month before the year's goods reach it waits for them
        // - the inland brigades waited at the depots for the outfit - and a
        // depot's feeders leave after the last outfit before the winter.
        var due = new Dictionary<string, int> { ["York Factory"] = ShipDay };
        foreach (var e in Entries) if (Posts.Named(e.Post) != null) due[e.Post] = DayOfYear(e.Month, 5);
        var entryDue = new Dictionary<string, int>(due);
        for (int pass = 0; pass < 8; pass++)
        {
            var arrive = new Dictionary<string, int>(entryDue);
            foreach (var r in runs)
                foreach (var leg in r.Legs)
                {
                    if (leg.Cargo != "outfit") continue;
                    if (arrive.TryGetValue(leg.From, out int inAt) && inAt > DayOfYear(leg.Month, leg.Day) && inAt - DayOfYear(leg.Month, leg.Day) <= 30)
                        (leg.Month, leg.Day) = MonthDay(inAt + 10);   // a week's slack: the river road is longer than the line
                    float km = Convoys.PostPos(leg.From).DistanceTo(Convoys.PostPos(leg.To)) / WorldMap.PxPerKm;
                    int at = DayOfYear(leg.Month, leg.Day) + Mathf.CeilToInt(km * 1.3f / Mathf.Max(10f, r.Craft.Up));
                    if (!arrive.TryGetValue(leg.To, out int had) || at > had) arrive[leg.To] = at;
                }
            due = arrive;
        }

        int added = 0;
        foreach (var p in Posts.All)
        {
            if (due.ContainsKey(p.Name)) continue;
            string depot = DepotFor(p, due.Keys);
            if (depot == null) continue;
            bool plains = WorldMap.At(p.AccessCell) is Ground.Grassland or Ground.TallGrass or Ground.Parkland;
            var run = new ConvoyRun
            {
                Name = $"the {p.Name} {(plains ? "pack train" : "canoes")}", Company = p.Faction,
                Craft = plains ? horses : north, Boats = plains ? 8 : 2, Men = plains ? 1 : 4,
            };
            run.Stops.Add(depot); run.Stops.Add(p.Name);
            // Up a few days after the depot's own outfit is in (and if that is
            // late in the year, it goes anyway and takes its chance with the ice);
            // down with the returns after the spring hunt.
            // Nothing moves on the rivers before the ice goes out in June.
            var (m, d) = MonthDay(Mathf.Clamp(due[depot] + 10, 166, 290));
            run.Legs.Add(new ConvoyLeg { From = depot, To = p.Name, Cargo = "outfit", Month = m, Day = d });
            run.Legs.Add(new ConvoyLeg { From = p.Name, To = depot, Cargo = "returns", Month = 5, Day = 25 });
            runs.Add(run);
            added++;
        }
        // A depot's goods are shared between the posts it supplies and its
        // own country, not taken by whichever canoes leave first. Shares go up
        // as the season's canoes leave and the stock is divided again.
        ShareOut(runs);
        GD.Print($"[convoys] {added} feeder runs from {due.Count} supplied depots to the lesser posts");
    }

    // Every outfit leg takes the share of its depot's stock that matches how
    // much of the country lies beyond it: a depot keeps enough for its own
    // trade and sends the rest on, split between the roads out of it by the
    // posts each one supplies, in the order the boats leave (each takes its
    // share of what the ones before it left). So the Rendezvous sends the
    // year's goods inland instead of keeping half at every stop.
    private static void ShareOut(List<ConvoyRun> runs)
    {
        var outLegs = new Dictionary<string, List<(ConvoyLeg Leg, string To)>>();
        foreach (var r in runs)
            foreach (var leg in r.Legs)
                if (leg.Cargo == "outfit")
                {
                    if (!outLegs.TryGetValue(leg.From, out var l)) outLegs[leg.From] = l = new();
                    l.Add((leg, leg.To));
                }
        var weight = new Dictionary<string, float>();
        float W(string node, int depth)
        {
            if (weight.TryGetValue(node, out float w)) return w;
            w = 1f;   // the place's own trade
            if (depth < 12 && outLegs.TryGetValue(node, out var l))
            {
                var kids = new HashSet<string>();
                foreach (var (_, to) in l) if (to != node) kids.Add(to);
                foreach (var k in kids) w += W(k, depth + 1);
            }
            weight[node] = w;
            return w;
        }
        foreach (var (node, legs) in outLegs)
        {
            // Each road out gets its branch's weight, divided between its legs of the year.
            var perTo = new Dictionary<string, int>();
            foreach (var (_, to) in legs) perTo[to] = perTo.GetValueOrDefault(to) + 1;
            var ordered = new List<(ConvoyLeg Leg, float W)>();
            foreach (var (leg, to) in legs) ordered.Add((leg, W(to, 1) / perTo[to]));
            ordered.Sort((x, y) => DayOfYear(x.Leg.Month, x.Leg.Day).CompareTo(DayOfYear(y.Leg.Month, y.Leg.Day)));
            float remaining = 1f;   // the depot's own trade
            foreach (var (_, w) in ordered) remaining += w;
            foreach (var (leg, w) in ordered)
            {
                leg.Share = Mathf.Clamp(w / remaining, 0.02f, 0.95f);
                remaining -= w;
                if (Plains.Log && w > 1.5f) GD.Print($"[convoys] {leg.From} -> {leg.To} ({leg.Month:00}-{leg.Day:00}) takes {leg.Share:P0} of the stock: {W(leg.To, 1):0} posts beyond");
            }
        }
    }

    private const int ShipDay = 237;   // London's ship is in and landing at York by late August

    private static readonly int[] MonthStart = { 0, 31, 59, 90, 120, 151, 181, 212, 243, 273, 304, 334 };
    private static int DayOfYear(int month, int day) => MonthStart[Mathf.Clamp(month - 1, 0, 11)] + day;
    private static (int, int) MonthDay(int doy)
    {
        int m = 11;
        while (m > 0 && MonthStart[m] >= doy) m--;
        return (m + 1, Mathf.Max(1, doy - MonthStart[m]));
    }

    // The depot a post draws on: the nearest supplied place of its company.
    private static string DepotFor(TradingPost p, IEnumerable<string> depots)
    {
        string best = null; float bd = float.MaxValue;
        var at = IsoGrid.ToWorld(p.AccessCell);
        foreach (var name in depots)
        {
            var faction = name == "York Factory" ? Faction.HBC : Posts.Named(name)?.Faction ?? Faction.HBC;
            if (faction != p.Faction) continue;
            float d = Convoys.PostPos(name).DistanceTo(at);
            if (d < bd) { bd = d; best = name; }
        }
        return best;
    }

    // A place a brigade takes an outfit out of: a depot on the roads.
    public static bool IsDepot(string name)
    {
        foreach (var run in Convoys.Runs) foreach (var leg in run.Legs) if (leg.From == name && leg.Cargo == "outfit") return true;
        return false;
    }

    // A post as the game begins: the outfit of the year before, part traded.
    // A depot on the great roads holds the stock for the posts it supplies.
    public static void Seed(TradingPostLedger p)
    {
        float scale = TrunkStops().Contains(p.Post.Name) ? 25f : 4f;
        foreach (var (id, mb) in Mix) p.Stores.Add(id, Mathf.RoundToInt(mb * 0.35f * scale / Items.Items.Of(id).ValueMb));
    }

    private static HashSet<string> _trunk;
    private static HashSet<string> TrunkStops()
    {
        if (_trunk != null) return _trunk;
        _trunk = new HashSet<string>();
        foreach (var e in Entries) _trunk.Add(e.Post);
        var root = GD.Load<Json>("res://Data/World/brigade_runs.json")?.Data.AsGodotDictionary();
        if (root != null && root.TryGetValue("runs", out var rv))
            foreach (var v in rv.AsGodotArray())
                foreach (var st in v.AsGodotDictionary()["stops"].AsGodotArray()) _trunk.Add(st.AsString());
        return _trunk;
    }
}
