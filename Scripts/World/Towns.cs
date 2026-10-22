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
    // Gardens, fields and woodpiles round the houses ("garden", "field", "woodpile").
    public readonly List<(string Kind, Vector2I Cell)> Props = new();
    // The streets: beaten earth (World/TerrainPainter).
    public readonly List<Vector2I> Streets = new();
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
    // Its own country, and its board's line under the name: whose it is now.
    public string Home = "";
    public string TownSub => Polity == (Home == "" ? Polity : Home)
        ? (Polity == "AMER" ? (Name == "Detroit" ? "Michigan Territory" : Name.Contains("Mackinac") ? "Michigan Territory" : "New York") : "Upper Canada")
        : Polity == "AMER" ? "held by the United States" : "held for the King";
    public string FortSub => Polity == "AMER" ? "United States garrison" : "His Majesty's garrison";
    public TownHousehold Garrison => Houses.GetValueOrDefault("garrison");
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
    // A town has hundreds of houses: they are not each ticked every frame to
    // see whether the day has turned; the day's round spoils their meat (Towns.Day).
    public override void _Ready() => SetProcess(false);
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
            t.Home = t.Polity;
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
            if (d.TryGetValue("streets", out var sv))
                foreach (var cv in sv.AsGodotArray()) { var c = cv.AsGodotArray(); t.Streets.Add(IsoGrid.Cell(top, c[0].AsInt32(), c[1].AsInt32())); }
            if (d.TryGetValue("props", out var pv))
                foreach (var qv in pv.AsGodotArray())
                {
                    var q = qv.AsGodotDictionary();
                    t.Props.Add((q["kind"].AsString(), IsoGrid.Cell(top, q["i"].AsInt32(), q["j"].AsInt32())));
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

    // Is this cell a street of a standing town? Asked for every cell the
    // ground painter lays, so it is a set lookup.
    private static HashSet<Vector2I> _streets;
    public static bool IsStreet(Vector2I cell)
    {
        if (_streets == null)
        {
            _streets = new();
            foreach (var t in All) if (t.Standing) foreach (var c in t.Streets) _streets.Add(c);
        }
        return _streets.Contains(cell);
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
                foreach (var (_, cell) in t.Props) Add(IsoGrid.ToWorld(cell), 90f);
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
        _streets = null;
    }

    // At world start, on every peer (WorldRoot): the towns of the year are
    // painted everywhere; their households are the server's.
    public static void Begin(Node host)
    {
        int year = GameCalendar.Instance?.Year ?? 1811;
        // Dev aid: `-- --notowns` leaves the towns unbuilt and unpeopled.
        if (System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--notowns") >= 0) return;
        foreach (var t in All)
        {
            if (t.Standing || t.Year > year) continue;
            t.Standing = true;
            _streets = null;
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
                // A farm's seed wheat, kept back from the last harvest for its fields.
                if (farm && Fields.Of(h) is { } fields) foreach (var p in fields.Plots) if (p.Crop == Crop.Wheat) h.Stores.Add(ItemId.Grain, Fields.SeedPer(p.Crop));
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

    // The day's round for the towns' houses (Core/Tiers, once a day).
    public static void Day()
    {
        foreach (var t in All)
            if (t.Standing) foreach (var h in t.Houses.Values) h.SpoilDay();
        Fields.Day();
    }

    // ---- the war (Core/Campaign: the town actions) -------------------------------

    // History being recalled at the start (Campaign.Recall): nothing is said,
    // and the dead of a battle before play began are simply gone.
    public static bool Quiet;
    private static void Tell(string text, bool headline = false) { if (Quiet) return; if (headline) Sim.Headline(text); else Sim.Notice(text); }
    private static void Dies(Npc n, string cause)
    {
        if (!Quiet) { n.Die(cause); return; }
        n.DeathCause = cause;
        n.DieQuietly();
        n.QueueFree();
    }

    // Whose the town is (a save, or a fall).
    public static void SetHolder(Town t, string polity)
    {
        t.Polity = polity == "CROWN" || polity == "Crown" ? "CROWN" : polity == "AMER" || polity == "American" ? "AMER" : t.Polity;
        Sim.Structures?.Resign(t.Name, t.TownSub);
        if (t.HasFort) Sim.Structures?.Resign(t.FortName, t.FortSub);
    }

    // The town changes hands. Its garrison, if it is still there and was the
    // loser's, is taken: the soldiers and their officers go down the country
    // as prisoners, their families with them. The townspeople stay - they
    // live there, whoever holds the fort.
    public static string Falls(string town, string to, bool prisoners)
    {
        var t = Named(town);
        if (t == null) return $"no town called {town}";
        string from = t.Polity;
        SetHolder(t, to);
        int taken = 0;
        if (prisoners && t.Garrison is { } g)
            foreach (var n in new List<Npc>(g.Members))
            {
                if (!GodotObject.IsInstanceValid(n) || n.IsDead || n.Faction != (from == "AMER" ? Faction.American : Faction.Crown)) continue;
                n.SendOffMap(from == "AMER" ? "a prisoner of war at Quebec" : "a prisoner of war in the United States", t.Name);
                taken++;
            }
        Tell($"{t.Name} has fallen to {t.RulerName}{(taken > 0 ? $"; {taken} of its garrison are taken prisoner" : "")}.", true);
        return null;
    }

    // Soldiers moved from one garrison to another (a fort taken and held, a
    // post reinforced): the given number of men and an officer, by water.
    public static string MoveGarrison(string from, string to, int count)
    {
        var a = Named(from); var b = Named(to);
        if (a?.Garrison == null || b?.Garrison == null) return $"no garrison at {(a?.Garrison == null ? from : to)}";
        var barracks = b.Garrison.Building;
        int moved = 0; bool officer = false;
        foreach (var n in new List<Npc>(a.Garrison.Members))
        {
            if (!GodotObject.IsInstanceValid(n) || n.IsDead || n.Away) continue;
            if (n.Role == Role.Officer && officer) continue;
            if (n.Role != Role.Soldier && n.Role != Role.Officer) continue;
            if (moved >= count && (officer || n.Role != Role.Officer)) continue;
            Quarter(n, b, moved);
            if (n.Role == Role.Officer) officer = true; else moved++;
        }
        if (b.Garrison.Leader == null || !GodotObject.IsInstanceValid(b.Garrison.Leader) || b.Garrison.Leader.IsDead || b.Garrison.Leader.Away)
            foreach (var n in b.Garrison.Members) if (n.Role == Role.Officer && !n.IsDead && !n.Away) { b.Garrison.Leader = n; break; }
        Tell($"{moved} men of the garrison at {a.Name} are sent to {b.Name}.");
        return null;
    }

    // A man given a bed in a town's barracks and a place on its parade.
    private static void Quarter(Npc n, Town b, int k)
    {
        var barracks = b.Garrison.Building;
        n.Brain.Clear(n); n.StopMoving();
        n.HomeStores = b.Garrison;
        n.BedCell = IsoGrid.Cell(barracks.Top, 1 + k % Mathf.Max(1, barracks.Width - 2), 1 + k / Mathf.Max(1, barracks.Width - 2) % Mathf.Max(1, barracks.Depth - 2));
        n.WorkCell = b.HasFort ? IsoGrid.Cell(b.FortTop, 2 + k * 7 % Mathf.Max(1, b.FortWidth - 4), 3 + k * 3 % Mathf.Max(1, b.FortDepth - 6)) : barracks.Threshold;
        n.GlobalPosition = IsoGrid.ToWorld(n.StandCellNear(n.BedCell));
        n.ResetPhysicsInterpolation();
    }

    // Men who went out of the country from this town come back to it - the
    // paroled and exchanged prisoners, a garrison back from where it marched -
    // those who serve whoever holds it now. All of them, or so many.
    public static string GarrisonReturns(string town, int count)
    {
        var t = Named(town);
        if (t?.Garrison == null) return $"no garrison at {town}";
        int back = 0;
        foreach (var n in new List<Npc>(Sim.Npcs))
        {
            if (count > 0 && back >= count) break;
            if (!GodotObject.IsInstanceValid(n) || n.IsDead || n.OffMap == "" || n.OffMapFrom != t.Name || n.Faction != t.Faction) continue;
            n.OffMap = ""; n.OffMapFrom = ""; n.Away = false;
            Quarter(n, t, back);
            // Back in the background: the tiers bring him into sight when a player is near.
            n.Visible = false;
            back++;
        }
        if (t.Garrison.Leader == null || !GodotObject.IsInstanceValid(t.Garrison.Leader) || t.Garrison.Leader.IsDead || t.Garrison.Leader.Away)
            foreach (var n in t.Garrison.Members) if (n.Role == Role.Officer && !n.IsDead && !n.Away) { t.Garrison.Leader = n; break; }
        Tell($"{back} of the garrison of {t.Name} are back in its fort.");
        return null;
    }

    // A garrison marches out of the country (to Kingston, to Burlington, down
    // to Montreal): all of it, or the given number.
    public static string GarrisonLeaves(string town, string where, int count)
    {
        var t = Named(town);
        if (t?.Garrison == null) return $"no garrison at {town}";
        int gone = 0;
        foreach (var n in new List<Npc>(t.Garrison.Members))
        {
            if (count > 0 && gone >= count) break;
            if (!GodotObject.IsInstanceValid(n) || n.IsDead || n.Away) continue;
            n.SendOffMap(where, t.Name); gone++;
        }
        Tell($"The garrison of {t.Name}, {gone} souls, has marched {where}.");
        return null;
    }

    // Men killed in a fight fought out of the player's sight: of the
    // garrison, or of the town's men of a role (the militia are its farmers).
    public static string Casualties(string town, int count, string cause, string role)
    {
        var t = Named(town);
        if (t == null) return $"no town called {town}";
        var pool = new List<Npc>();
        bool any = string.IsNullOrEmpty(role);
        Role want = Role.Soldier;
        if (!any && !System.Enum.TryParse(role, true, out want)) return $"no role called {role}";
        foreach (var h in t.Houses.Values)
        {
            if (any && h.Key != "garrison") continue;
            foreach (var n in h.Members)
                if (GodotObject.IsInstanceValid(n) && !n.IsDead && !n.Away && !n.IsFemale && n.Age >= 16 && (any ? n.Role is Role.Soldier or Role.Officer : n.Role == want)) pool.Add(n);
        }
        int killed = 0;
        while (killed < count && pool.Count > 0)
        {
            int i = GD.RandRange(0, pool.Count - 1);
            Dies(pool[i], cause);
            pool.RemoveAt(i); killed++;
        }
        return null;
    }

    // A named person killed (Brock at Queenston, Tecumseh at the Thames).
    public static string Kill(string who, string cause)
    {
        foreach (var n in Sim.Npcs) if (n.DisplayName == who && !n.IsDead) { Dies(n, cause); return null; }
        return $"nobody called {who} is alive";
    }

    // A building of a town set alight (York's Parliament, Newark in December
    // 1813), every one ("*"), or the fort's own ("fort": Fort Malden left
    // burning). The fire takes it from there.
    public static string Burn(string town, string building)
    {
        var t = Named(town);
        if (t == null) return $"no town called {town}";
        int lit = 0;
        foreach (var b in t.Buildings)
        {
            bool fort = building == "fort" && (b.Home == "garrison" || b.Def.Kind == BuildingKind.PowderMagazine);
            if (building != "*" && !fort && b.Def.Name != building) continue;
            var c = IsoGrid.Cell(b.Def.Top, b.Def.Width / 2, b.Def.Depth / 2);
            string verb = b.Def.Name.EndsWith("s") ? "are" : "is";
            if (Fire.I != null && Fire.I.Ignite(c, $"{b.Def.Name} at {t.Name} {verb} burning.")) lit++;
            if (building != "*" && building != "fort") break;
        }
        Tell(building == "*" ? $"{t.Name} is burning." : building == "fort" ? $"{t.FortName} is burning." : $"{building} at {t.Name} {(building.EndsWith("s") ? "are" : "is")} burning.", true);
        return lit > 0 || building is "*" or "fort" ? null : $"no building called {building} at {town}";
    }
}
