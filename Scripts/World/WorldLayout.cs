using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Ecology;

namespace IsometricFurTradeGame.Scripts.World;

public enum BuildingKind { Depot, Warehouse, MensHouse, NwcHouse, OutpostHouse, FactorsHouse, ClerksHouse, ProvisionStore, SummerMess, GuestHouse, BoatShed, PowderMagazine, TradingPost, GreatHall, PartnersHouse }

public readonly record struct BuildingDef(BuildingKind Kind, string Name, Vector2I Top, int Width, int Depth, int DoorOffset)
{
    public Vector2I DoorCell => IsoGrid.Cell(Top, DoorOffset, Depth - 1);
    // Just inside the door.
    public Vector2I Threshold => IsoGrid.Cell(Top, DoorOffset, Depth - 2);
    public Vector2I Centre => IsoGrid.Cell(Top, Width / 2, Depth / 2);
    public IEnumerable<Vector2I> Interior()
    {
        for (int i = 1; i < Width - 1; i++)
            for (int j = 1; j < Depth - 1; j++)
                yield return IsoGrid.Cell(Top, i, j);
    }
    public bool Contains(Vector2I c)
    {
        foreach (var x in IsoGrid.Rhombus(Top, Width, Depth)) if (x == c) return true;
        return false;
    }
}

// Route: where the site sits each month (12 entries), or null for a fixed
// site like a lodge or a den (design doc 16.5).
public readonly record struct SiteDef(string Name, Species Species, Vector2I Cell, int Count, int Capacity, float RadiusPx, Vector2I[] Route = null);

// Where everything is, in cells. The country itself is WorldMap (the whole
// play area from real geography, half scale); this is where the hand-built
// places sit in it - each post keeps its shape, and build_world.py found the
// nearest dry spot to its real position (Data/World/world.json, "layout").
// Both painters and the population read from here.
public static class WorldLayout
{
    public static int MapWidth => WorldMap.Columns;
    public static int MapHeight => WorldMap.Rows;

    // ---- the fort ---------------------------------------------------------
    // York Factory after G. Ray's plan and the survey of the post: a long
    // palisade, its length running west-south-west to east-north-east along
    // the north bank of the Hayes, the Depot in the middle, a boardwalk from
    // it through the river gate to the landings, Sloops Creek behind.
    // In cells, i runs toward the river (screen down-right) and j along the
    // length of the post (screen down-left), from the north corner.
    public static readonly Vector2I FortTop = WorldMap.Anchor("fort_top", new(30, 64));
    public const int FortWidth = 22, FortDepth = 34, GateOffset = 21;
    // The river gate, in the south-east palisade.
    public static Vector2I GateCell => IsoGrid.Cell(FortTop, FortWidth - 1, GateOffset);
    public static Vector2I GateOutside => IsoGrid.DownRight(IsoGrid.DownRight(GateCell));
    public static IEnumerable<Vector2I> YardCells() => IsoGrid.Rhombus(FortTop, FortWidth, FortDepth);
    public static Vector2 FortCentre => IsoGrid.ToWorld(IsoGrid.Cell(FortTop, FortWidth / 2, FortDepth / 2));

    // The three the post's work goes through keep their old names: the
    // trading room is the Indian Trading Shop at the west end, the
    // warehouse the great Depot.
    public static readonly BuildingDef Depot = new(BuildingKind.Depot, "Indian trading shop", IsoGrid.Cell(FortTop, 10, 27), 7, 5, 3);
    public static readonly BuildingDef Warehouse = new(BuildingKind.Warehouse, "the Depot", IsoGrid.Cell(FortTop, 8, 15), 8, 6, 3);
    public static readonly BuildingDef MensHouse = new(BuildingKind.MensHouse, "Men's house", IsoGrid.Cell(FortTop, 7, 2), 7, 5, 3);
    public static readonly BuildingDef FactorsHouse = new(BuildingKind.FactorsHouse, "the Chief Factor's house", IsoGrid.Cell(FortTop, 2, 18), 5, 6, 2);
    public static readonly BuildingDef ClerksHouse = new(BuildingKind.ClerksHouse, "Clerks' house", IsoGrid.Cell(FortTop, 2, 10), 5, 6, 2);
    public static readonly BuildingDef SummerMess = new(BuildingKind.SummerMess, "Summer mess", IsoGrid.Cell(FortTop, 8, 8), 5, 5, 2);
    public static readonly BuildingDef ProvisionStore = new(BuildingKind.ProvisionStore, "Provision store", IsoGrid.Cell(FortTop, 12, 22), 5, 4, 2);
    public static readonly BuildingDef GuestHouse = new(BuildingKind.GuestHouse, "Guest house", IsoGrid.Cell(FortTop, 16, 13), 4, 5, 1);
    // Outside the palisade: the boat-building shed by the cargo landing, the
    // powder magazine beyond the creek, away from everything that burns.
    public static readonly BuildingDef BoatShed = new(BuildingKind.BoatShed, "Boat-building shed", IsoGrid.Cell(FortTop, FortWidth + 2, 2), 5, 6, 2);
    public static readonly BuildingDef PowderMagazine = new(BuildingKind.PowderMagazine, "Powder magazine", IsoGrid.Cell(FortTop, -10, 6), 3, 3, 1);

    // The boardwalk from the Depot's door to the river gate.
    public static IEnumerable<Vector2I> BoardwalkCells()
    {
        for (int i = 11; i < FortWidth; i++) yield return IsoGrid.Cell(FortTop, i, GateOffset);
    }

    // Sloops Creek: behind the post, along its length, then down past the
    // north corner to the river - with a ford where the graveyard path crosses.
    public static IEnumerable<Vector2I> SloopsCreek()
    {
        for (int j = FortDepth + 4; j >= -5; j--) { yield return IsoGrid.Cell(FortTop, -5, j); yield return IsoGrid.Cell(FortTop, -6, j); }
        for (int i = -4; i <= FortWidth + 14; i++) { yield return IsoGrid.Cell(FortTop, i, -5); yield return IsoGrid.Cell(FortTop, i, -6); }
    }
    public static IEnumerable<Vector2I> SloopsCreekFord()
    {
        for (int j = 6; j <= 7; j++) { yield return IsoGrid.Cell(FortTop, -5, j); yield return IsoGrid.Cell(FortTop, -6, j); }
    }

    static WorldLayout()
    {
        WorldMap.Carve(SloopsCreek(), Ground.River);
        WorldMap.Carve(SloopsCreekFord(), Ground.Ford);
    }

    // ---- the Nor'Westers' post, up the river (design doc 5.2, 7.1) ------------------
    public static readonly Vector2I NwcTop = WorldMap.Anchor("nwc_top", new(66, 150));
    public const int NwcWidth = 12, NwcDepth = 10, NwcGateOffset = 5;
    public static Vector2I NwcGateCell => IsoGrid.Cell(NwcTop, NwcGateOffset, NwcDepth - 1);
    public static readonly BuildingDef NwcHouse = new(BuildingKind.NwcHouse, "Nor'Westers' house", IsoGrid.Cell(NwcTop, 2, 2), 7, 5, 3);
    public static IEnumerable<Vector2I> NwcYardCells() => IsoGrid.Rhombus(NwcTop, NwcWidth, NwcDepth);
    public static Vector2I NwcWoodpileCell => IsoGrid.Cell(NwcTop, 9, 7);

    // A shallow crossing: where a river can be waded in summer.
    public static bool IsFord(Vector2I c) => WorldMap.At(c) == Ground.Ford;

    // ---- the inland post (design doc 3.9's far outpost, 10.1.2's second leg) --
    // Oxford House, up the Hayes. A postmaster, a few men, a woman, its own
    // books, and the inland bands' furs.
    public static readonly Vector2I OutpostTop = WorldMap.Anchor("outpost_top", new(36, 396));
    public const int OutpostWidth = 12, OutpostDepth = 9, OutpostGateOffset = 5;
    public static Vector2I OutpostGateCell => IsoGrid.Cell(OutpostTop, OutpostGateOffset, OutpostDepth - 1);
    public static Vector2I OutpostGateOutside => IsoGrid.DownLeft(IsoGrid.DownLeft(OutpostGateCell));
    public static readonly BuildingDef OutpostHouse = new(BuildingKind.OutpostHouse, "Oxford House", IsoGrid.Cell(OutpostTop, 2, 2), 7, 5, 3);
    public static IEnumerable<Vector2I> OutpostYardCells() => IsoGrid.Rhombus(OutpostTop, OutpostWidth, OutpostDepth);
    public static Vector2I OutpostWoodpileCell => IsoGrid.Cell(OutpostTop, 9, 6);

    public static readonly BuildingDef[] Buildings = { Depot, Warehouse, MensHouse, FactorsHouse, ClerksHouse, SummerMess, ProvisionStore, GuestHouse, BoatShed, PowderMagazine, NwcHouse, OutpostHouse };

    // ---- the river road: York Factory to Oxford House, up the Hayes -----------
    // Laid by the importer along dry ground beside the river (fording it
    // where it must), from gate to gate.
    public static readonly Vector2[] RiverRoad = Road();
    private static Vector2[] Road()
    {
        var pts = new List<Vector2>(WorldMap.Polyline("road"));
        if (pts.Count < 2) pts = new List<Vector2> { IsoGrid.ToWorld(GateOutside), IsoGrid.ToWorld(OutpostGateOutside) };
        // Callers index into the middle of it (a spot "a few legs up the road").
        while (pts.Count < 6) pts.Insert(pts.Count - 1, pts[^2].Lerp(pts[^1], 0.5f));
        return pts.ToArray();
    }
    // Where you stand to take the road: a little way out of each gate.
    public static Vector2I RoadHeadYork => IsoGrid.FromWorld(RiverRoad[0].Lerp(RiverRoad[1], 0.5f));
    public static Vector2I RoadHeadOutpost => IsoGrid.FromWorld(RiverRoad[^1].Lerp(RiverRoad[^2], 0.5f));
    public static float RoadDistance(Vector2 p)
    {
        float best = float.MaxValue;
        for (int i = 0; i + 1 < RiverRoad.Length; i++) best = Mathf.Min(best, Geometry2D.GetClosestPointToSegment(p, RiverRoad[i], RiverRoad[i + 1]).DistanceTo(p));
        return best;
    }

    // Yard props.
    public static Vector2I WoodpileCell => IsoGrid.Cell(FortTop, 18, 7);
    // The flag staff in the square south-east of the Depot.
    public static Vector2I FlagCell => IsoGrid.Cell(FortTop, 19, 19);

    // ---- the landing and the ship ----------------------------------------------
    // The boardwalk carries on out of the river gate to the passenger landing
    // (the importer only accepts a site where its last planks stand in the river).
    public const int WharfLength = 14;
    public static readonly Vector2I WharfRoot = IsoGrid.DownRight(GateCell);
    public static IEnumerable<Vector2I> WharfCells()
    {
        var c = WharfRoot;
        for (int i = 0; i < WharfLength; i++) { yield return c; c = IsoGrid.DownRight(c); }
    }
    public static Vector2I WharfEnd
    {
        get { var c = WharfRoot; for (int i = 0; i < WharfLength - 1; i++) c = IsoGrid.DownRight(c); return c; }
    }
    // Where the ship's hull sits when tied up: alongside the last planks.
    public static Vector2 ShipBerth => IsoGrid.ToWorld(WharfEnd) + new Vector2(120, -40);
    // Out in the Bay beyond sight of the post, the way she came in.
    // Down the Hayes and out into the Bay, the way she came in.
    public static Vector2 ShipOffscreen => ShipBerth + new Vector2(5600, -2200);

    // ---- the Cree camp ----------------------------------------------------
    // The band follows the seasons (design doc 6, 14.1): the winter camp
    // in the bush, and a summer camp on the trail beside the post for the
    // trading season. CampCentre is wherever they are now.
    public static readonly Vector2I WinterCamp = WorldMap.Anchor("cree_winter", new(14, 140));
    public static readonly Vector2I SummerCamp = WorldMap.Anchor("cree_summer", new(23, 100));
    public static Vector2I CampCentre { get; private set; } = WinterCamp;
    public static void SetCamp(Vector2I centre) => CampCentre = centre;
    // The Assiniboine (design doc 7.2): a plains people wintering upland
    // near Oxford House, coming down to the Bay in high summer to trade.
    public static readonly Vector2I AssiniboineWinterCamp = WorldMap.Anchor("assiniboine_winter", new(16, 424));
    public static readonly Vector2I AssiniboineSummerCamp = WorldMap.Anchor("assiniboine_summer", new(22, 118));
    public static IEnumerable<Vector2I> TipiCellsAt(Vector2I c)
    {
        yield return IsoGrid.Cell(c, 3, 0);
        yield return IsoGrid.Cell(c, 0, 3);
        yield return IsoGrid.Cell(c, -3, 0);
        yield return IsoGrid.Cell(c, 0, -3);
        yield return IsoGrid.Cell(c, 3, 3);
    }
    public static bool IsSummerCamp => CampCentre == SummerCamp;
    public const float CampClearingPx = 460f;
    public static Vector2I CampFire => CampCentre;
    public static IEnumerable<Vector2I> TipiCells() => TipiCellsAt(CampCentre);

    // ---- rivers and shore ---------------------------------------------------
    public static bool IsRiver(Vector2I c) => WorldMap.At(c) == Ground.River;
    public static bool IsRiverBank(Vector2I c) => !WorldMap.IsWater(c) && WorldMap.Near(c, g => g is Ground.River or Ground.Lake);
    // Sand along the sea: two cells of beach.
    public static bool IsBeach(Vector2I c) => !WorldMap.IsWater(c) && WorldMap.Near(c, g => g == Ground.Ocean, 2);

    // ---- wildlife sites (design doc 16.4) -------------------------------------
    // Placed in the editor as WildlifeSite nodes. Read live in the editor
    // (so moving one shows at once); cached once a game has them.
    private static SiteDef[] _sites;
    public static SiteDef[] Sites
    {
        get
        {
            if (Engine.IsEditorHint()) return WildlifeSite.Collect();
            if (_sites == null || _sites.Length == 0) _sites = WildlifeSite.Collect();
            return _sites;
        }
    }
    public static void ForgetSites() => _sites = null;
    // A people's own country: the grounds within about 600 km of home. Nobody
    // at York knows the beaver of the Athabasca.
    public const float HomeCountryPx = 24000f;
    public static IEnumerable<SiteDef> SitesNear(Vector2I home, float px)
    {
        var at = IsoGrid.ToWorld(home);
        foreach (var s in Sites) if (IsoGrid.ToWorld(s.Cell).DistanceTo(at) <= px) yield return s;
    }
    public static SiteDef SiteOf(Species s)
    {
        foreach (var d in Sites) if (d.Species == s) return d;
        return default;
    }

    // ---- the trail from the gate to the camp ----------------------------------
    public static readonly Vector2[] Trail = TrailPoints();
    private static Vector2[] TrailPoints()
    {
        var pts = WorldMap.Polyline("trail");
        return pts.Length >= 2 ? pts : new[] { IsoGrid.ToWorld(GateOutside), IsoGrid.ToWorld(WinterCamp) };
    }

    public static float TrailDistance(Vector2 p)
    {
        float best = float.MaxValue;
        for (int i = 0; i + 1 < Trail.Length; i++)
        {
            best = Mathf.Min(best, Geometry2D.GetClosestPointToSegment(p, Trail[i], Trail[i + 1]).DistanceTo(p));
        }
        return best;
    }

    // ---- what grows where (design doc 6: a region should look like itself) ------
    // Tile sources (terrain_tileset.tres) for the growth and the ground clutter.
    public const int Spruce0 = 52, Tamarack = 80, Willow = 81, Rocks = 82, Boulder = 83, Reeds = 84, MuskegPool = 85,
        DeadTree = 86, Aspen = 97, JackPine = 98, BeaverLodge = 87;
    public static bool IsTreeSource(int s) => s is >= Spruce0 and <= Spruce0 + 2 or Tamarack or DeadTree or Aspen or JackPine;
    public static bool IsFloraSource(int s) => IsTreeSource(s) || s is >= Willow and <= MuskegPool;

    // Ground nobody lets grow: round the posts and camps, along the road and
    // the trail, the beaches.
    public static bool Cleared(Vector2I c)
    {
        if (WorldMap.IsWater(c) || IsBeach(c)) return true;
        var p = IsoGrid.ToWorld(c);
        if (Posts.NearAny(p, 900f)) return true;
        float fortDist = p.DistanceTo(FortCentre);
        if (fortDist > 30000f && p.DistanceTo(IsoGrid.ToWorld(OutpostTop)) > 8000f) return false;
        if (fortDist < 1500f || TrailDistance(p) < 130f || RoadDistance(p) < 150f) return true;
        var outpostCentre = IsoGrid.ToWorld(IsoGrid.Cell(OutpostTop, OutpostWidth / 2, OutpostDepth / 2));
        if (p.DistanceTo(outpostCentre) < 1000f) return true;
        foreach (var camp in new[] { WinterCamp, SummerCamp, AssiniboineWinterCamp, AssiniboineSummerCamp })
            if (p.DistanceTo(IsoGrid.ToWorld(camp)) < CampClearingPx) return true;
        var nwcCentre = IsoGrid.ToWorld(IsoGrid.Cell(NwcTop, NwcWidth / 2, NwcDepth / 2));
        return p.DistanceTo(nwcCentre) < 1100f;
    }

    // How thick the trees stand, by region, in groves and openings.
    public static float ForestDensity(Vector2I c)
    {
        if (Cleared(c) || IsRiverBank(c)) return 0f;
        var g = WorldMap.At(c);
        float density = g switch
        {
            Ground.Boreal => 0.32f, Ground.LakesForest => 0.32f, Ground.Shield => 0.24f, Ground.Lowland => 0.16f,
            Ground.Cordillera => 0.2f, Ground.Parkland => 0.14f, _ => 0f,
        };
        // Groves and openings rather than an even scatter; parkland is the
        // most broken of all - aspen bluffs on open grass.
        float patch = IsoGrid.Hash(new Vector2I(c.X / 6, c.Y / 12), 31);
        density *= g == Ground.Parkland ? (patch > 0.55f ? 2.6f : 0.1f) : 0.55f + 0.9f * patch;
        var p = IsoGrid.ToWorld(c);
        float fortDist = p.DistanceTo(FortCentre);
        if (fortDist < 2300f) density *= (fortDist - 1500f) / 800f;
        return Mathf.Clamp(density, 0f, 0.5f);
    }

    // The tile that grows on a cell as the country was made (before anyone's
    // axe), or -1. Asked a great deal (the pathfinder's bush cost, the
    // woodcutters), so remembered per cell.
    private static short[] _flora;
    public static int Flora(Vector2I c)
    {
        if (!WorldMap.InBounds(c)) return -1;
        _flora ??= NewFloraCache();
        int i = c.Y * WorldMap.Columns + c.X;
        if (_flora[i] == -2) _flora[i] = (short)PickFlora(c);
        return _flora[i];
    }
    private static short[] NewFloraCache() { var a = new short[WorldMap.Columns * WorldMap.Rows]; System.Array.Fill(a, (short)-2); return a; }
    public static bool ForestAt(Vector2I c) => IsTreeSource(Flora(c));

    private static int PickFlora(Vector2I c)
    {
        if (Cleared(c)) return -1;
        var g = WorldMap.At(c);
        float h = IsoGrid.Hash(c, 11), h2 = IsoGrid.Hash(c, 12);
        int spruce = Spruce0 + (int)(IsoGrid.Hash(c, 13) * 3f);
        // River banks: willow and reeds whatever the region.
        if (IsRiverBank(c)) return h < 0.07f ? Willow : h < 0.12f ? Reeds : -1;
        float d = ForestDensity(c);
        if (h < d)
            return g switch
            {
                Ground.LakesForest => h2 < 0.4f ? JackPine : h2 < 0.7f ? spruce : Aspen,
                Ground.Shield => h2 < 0.5f ? JackPine : h2 < 0.92f ? spruce : DeadTree,
                Ground.Lowland => h2 < 0.6f ? Tamarack : h2 < 0.92f ? spruce : DeadTree,
                Ground.Parkland => h2 < 0.85f ? Aspen : Willow,
                Ground.Cordillera => h2 < 0.55f ? JackPine : spruce,
                _ => h2 < 0.72f ? spruce : h2 < 0.86f ? Aspen : h2 < 0.96f ? Tamarack : DeadTree,
            };
        // The clutter between the trees.
        float r = (h - d) / Mathf.Max(0.01f, 1f - d);
        return g switch
        {
            Ground.Shield => r < 0.05f ? Rocks : r < 0.08f ? Boulder : -1,
            Ground.Lowland => r < 0.03f ? MuskegPool : r < 0.06f ? Reeds : -1,
            Ground.Tundra => r < 0.04f ? Willow : r < 0.06f ? Rocks : r < 0.07f ? Boulder : r < 0.075f ? spruce : -1,
            Ground.Cordillera => r < 0.06f ? Boulder : r < 0.1f ? Rocks : -1,
            Ground.Grassland or Ground.TallGrass => r < 0.008f ? Rocks : r < 0.01f ? Boulder : -1,
            Ground.Parkland => r < 0.01f ? Willow : -1,
            _ => r < 0.008f ? Rocks : -1,
        };
    }
}
