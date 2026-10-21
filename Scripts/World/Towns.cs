using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Economy;
using IsometricFurTradeGame.Scripts.Items;

namespace IsometricFurTradeGame.Scripts.World;

// One building of a town and whose home it is.
public record TownBuilding(BuildingDef Def, string Home);

// A town of the Lakes frontier (design doc 6.1): Detroit, Amherstburg,
// York, Niagara and the rest. Not a trading post: streets of family houses,
// a church, the merchants' stores, and usually a garrison in its own
// palisade. Everyone in it is a person placed in the editor (Cast/Towns),
// with a house of their own; the town's layout is in world.json, written by
// tools/world/place_towns.py from tools/world/towns.json.
public class Town
{
    public string Name, Polity, Note;
    public int Year;
    public Vector2I Top;
    public int Width, Depth;
    public readonly List<TownBuilding> Buildings = new();
    // The garrison's palisade, if it has one.
    public Vector2I FortTop;
    public int FortWidth, FortDepth, FortGate;
    public string FortName = "";
    public bool HasFort => FortWidth > 0;
    // Every household of the town by its key ("market", "house 12", "garrison").
    public readonly Dictionary<string, TownHousehold> Houses = new();
    public TownHousehold Market => Houses.GetValueOrDefault("market");
    public bool Standing;
    public Faction Faction => Polity == "AMER" ? Faction.American : Faction.Crown;
    public Vector2 Centre => IsoGrid.ToWorld(IsoGrid.Cell(Top, Width / 2, Depth / 2));
    public string RulerName => Polity == "AMER" ? "the United States" : "the Crown";
}

// A household of a town: a family in its house, the garrison in its
// barracks, the priest in his house by the church, or the town's market -
// the merchants' stores, where the farms' surplus is sold and the rest of
// the town buys its bread (Tiers: the town's day).
public partial class TownHousehold : Household
{
    public Town Town;
    public string Key;
    public BuildingDef Building;
    // Days of work put into this family's fields since the spring, and the
    // year they were last harvested (Tiers.Farm).
    public float FieldDays;
    public int HarvestYear;
    public string FullKey => Town.Name + "|" + Key;
}

public static class Towns
{
    public static readonly List<Town> All = Load();
    private static readonly Dictionary<string, Town> _byName = Index();

    private static List<Town> Load()
    {
        var list = new List<Town>();
        foreach (var v in WorldMap.LayoutArray("towns"))
        {
            var d = v.AsGodotDictionary();
            var top = Cell(d["top"]);
            var t = new Town
            {
                Name = d["name"].AsString(), Polity = d["polity"].AsString(), Year = d["year"].AsInt32(), Top = top,
                Width = d["width"].AsInt32(), Depth = d["depth"].AsInt32(), Note = d.TryGetValue("note", out var nv) ? nv.AsString() : "",
            };
            if (d.TryGetValue("fort", out var fv) && fv.VariantType == Variant.Type.Dictionary)
            {
                var f = fv.AsGodotDictionary();
                t.FortTop = IsoGrid.Cell(top, f["i"].AsInt32(), f["j"].AsInt32());
                t.FortWidth = f["width"].AsInt32(); t.FortDepth = f["depth"].AsInt32(); t.FortGate = f["gate"].AsInt32();
                t.FortName = f["name"].AsString();
            }
            foreach (var bv in d["buildings"].AsGodotArray())
            {
                var b = bv.AsGodotDictionary();
                if (!System.Enum.TryParse(b["kind"].AsString(), out BuildingKind kind)) continue;
                var (w, dd, door) = Posts.SizeOf(kind);
                var def = new BuildingDef(kind, b["name"].AsString(), IsoGrid.Cell(top, b["i"].AsInt32(), b["j"].AsInt32()), w, dd, door);
                t.Buildings.Add(new TownBuilding(def, b.TryGetValue("home", out var hv) ? hv.AsString() : ""));
            }
            list.Add(t);
        }
        return list;
    }

    private static Vector2I Cell(Variant v) { var a = v.AsGodotArray(); return new Vector2I(a[0].AsInt32(), a[1].AsInt32()); }

    private static Dictionary<string, Town> Index()
    {
        var d = new Dictionary<string, Town>();
        foreach (var t in All) d[t.Name] = t;
        return d;
    }

    public static Town Named(string name) => name != null && _byName.TryGetValue(name, out var t) ? t : null;

    // "Detroit|house 12" - how a marker and a save name a town household.
    public static TownHousehold House(string fullKey)
    {
        if (string.IsNullOrEmpty(fullKey)) return null;
        int bar = fullKey.IndexOf('|');
        if (bar < 0) return null;
        var t = Named(fullKey[..bar]);
        return t != null && t.Houses.TryGetValue(fullKey[(bar + 1)..], out var h) ? h : null;
    }

    public static IEnumerable<TownHousehold> Households()
    {
        foreach (var t in All) if (t.Standing) foreach (var h in t.Houses.Values) yield return h;
    }

    // Ground kept clear round the towns (WorldLayout.Cleared): round every
    // building - the town's streets and the farms out in the country with
    // their fields - and round the garrison's palisade. Looked up by area.
    private const float GridPx = 2048f;
    private static Dictionary<Vector2I, List<(Vector2 At, float R)>> _clear;
    public static bool NearAny(Vector2 at, float margin)
    {
        if (_clear == null)
        {
            _clear = new();
            void Add(Vector2 c, float r)
            {
                var k = new Vector2I(Mathf.FloorToInt(c.X / GridPx), Mathf.FloorToInt(c.Y / GridPx));
                if (!_clear.TryGetValue(k, out var l)) _clear[k] = l = new();
                l.Add((c, r));
            }
            foreach (var t in All)
            {
                foreach (var b in t.Buildings) Add(IsoGrid.ToWorld(IsoGrid.Cell(b.Def.Top, b.Def.Width / 2, b.Def.Depth / 2)), b.Def.Kind == BuildingKind.Townhouse && b.Home != "" ? 520f : 420f);
                if (t.HasFort) Add(IsoGrid.ToWorld(IsoGrid.Cell(t.FortTop, t.FortWidth / 2, t.FortDepth / 2)), (t.FortWidth + t.FortDepth) * 34f);
            }
        }
        int kx = Mathf.FloorToInt(at.X / GridPx), ky = Mathf.FloorToInt(at.Y / GridPx);
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
                if (_clear.TryGetValue(new Vector2I(kx + dx, ky + dy), out var l))
                    foreach (var (c, r) in l) if (c.DistanceTo(at) < r + margin) return true;
        return false;
    }

    public static void Reset()
    {
        foreach (var t in All) { t.Standing = false; t.Houses.Clear(); }
    }

    // At world start, on every peer (WorldRoot): the towns of the year are
    // painted everywhere; their households are the server's.
    public static void Begin(Node host)
    {
        int year = GameCalendar.Instance?.Year ?? 1811;
        foreach (var t in All)
        {
            if (t.Standing || t.Year > year) continue;
            t.Standing = true;
            if (Net.Net.IsServer) MakeHouses(host, t);
            Sim.Structures?.PaintTown(t);
        }
        Sim.Structures?.CallDeferred("RebuildCollisionNow");
    }

    private static void MakeHouses(Node host, Town t)
    {
        // Two labouring families may share a house ("house 7;house 8"): each
        // keeps its own stores, and they share the door and the fire.
        foreach (var b in t.Buildings)
            foreach (var key in b.Home.Split(';', System.StringSplitOptions.RemoveEmptyEntries))
            {
                if (t.Houses.ContainsKey(key)) continue;
                var def = b.Def;
                var h = new TownHousehold
                {
                    Town = t, Key = key, Building = def,
                    Name = $"Town_{(t.Name + "_" + key).Replace(' ', '_').Replace('.', '_').Replace('\'', '_')}",
                    Title = key == "market" ? $"the market at {t.Name}" : $"{def.Name}, {t.Name}",
                    AccessCell = def.Threshold,
                    WoodpileCell = IsoGrid.DownLeft(def.DoorCell),
                };
                h.HearthCells.Add(IsoGrid.Cell(def.Top, 1, 1));
                host.AddChild(h);
                t.Houses[key] = h;
            }
    }

    // A new game (Population, once everyone is in his house): the 1810
    // harvest is in. Every family has its bread and corn to the next one and
    // a woodpile for the winter; what the farms grew beyond their own wants
    // is in the market, enough for the rest of the town to the harvest.
    public static void SeedStores()
    {
        var cal = GameCalendar.Instance;
        int dayOfYear = cal != null ? (cal.Month - 1) * 30 + cal.Day : 1;
        int toHarvest = dayOfYear < 250 ? 250 - dayOfYear : 365 - dayOfYear + 250;
        foreach (var t in All)
        {
            if (!t.Standing || t.Market == null) continue;
            int townMouths = 0;
            foreach (var h in t.Houses.Values)
            {
                if (h == t.Market) continue;
                int mouths = h.Alive();
                bool farm = false;
                foreach (var m in h.Members) if (m.Role == Role.Farmer) { farm = true; break; }
                if (!farm) townMouths += mouths;
                // A farm keeps its own to the harvest; everyone else has a fortnight in the house.
                int days = farm ? toHarvest + 20 : 14;
                // Bread and corn, and a third of it potatoes and roots from the cellar.
                h.Stores.Add(ItemId.Roots, mouths * days / 3);
                h.Stores.Add(ItemId.Bread, mouths * days / 3);
                h.Stores.Add(ItemId.Corn, mouths * days - 2 * (mouths * days / 3));
                h.Stores.Add(ItemId.Firewood, 40);
                h.Stores.Add(ItemId.Water, 6);
                if (h.Key == "garrison") h.Stores.Add(ItemId.Musket, 2);
                h.HarvestYear = (cal?.Year ?? 1811) - (dayOfYear >= 250 ? 0 : 1);
            }
            int townDays = toHarvest + 30;
            t.Market.Stores.Add(ItemId.Roots, townMouths * townDays / 3);
            t.Market.Stores.Add(ItemId.Bread, townMouths * townDays / 2);
            t.Market.Stores.Add(ItemId.Corn, townMouths * townDays / 4);
            t.Market.Stores.Add(ItemId.Ale, townMouths / 2);
            t.Market.Stores.Add(ItemId.Rum, townMouths / 4);
        }
    }

    // A house running low buys at the town's market (once a day for the
    // house): a fortnight's bread for everyone in it, as far as the market
    // has it. A market that runs dry is news (Tiers' short commons follows).
    private static readonly ItemId[] MarketFood = { ItemId.Roots, ItemId.Bread, ItemId.Corn, ItemId.Fish, ItemId.Venison, ItemId.DriedMeat, ItemId.Pemmican };
    private static readonly Dictionary<TownHousehold, int> _boughtOn = new();
    public static void Provision(TownHousehold h)
    {
        var market = h.Town?.Market;
        if (market == null || h == market) return;
        int today = GameCalendar.Instance?.Day ?? 0;
        if (_boughtOn.TryGetValue(h, out int on) && on == today) return;
        _boughtOn[h] = today;
        int mouths = h.Alive();
        if (mouths == 0 || h.FoodDays(mouths) >= 7f) return;
        // A third of it roots when the market has them, the rest bread and corn.
        int want = mouths * 14;
        int roots = market.Stores.TakeUpTo(ItemId.Roots, want / 3);
        if (roots > 0) { h.Stores.Add(ItemId.Roots, roots); want -= roots; }
        foreach (var id in MarketFood)
        {
            if (want <= 0) break;
            int got = market.Stores.TakeUpTo(id, want);
            if (got > 0) { h.Stores.Add(id, got); want -= got; }
        }
    }
}
