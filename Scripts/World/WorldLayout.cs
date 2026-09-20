using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Ecology;

namespace IsometricFurTradeGame.Scripts.World;

public enum BuildingKind { Depot, Warehouse, MensHouse, NwcHouse }

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

public readonly record struct SiteDef(string Name, Species Species, Vector2I Cell, int Count, int Capacity, float RadiusPx);

// The York Factory test world, in cells. One place to look up where
// everything is; both painters and the population spawner read from here.
// Rows are 32 px, columns 128 px: the map is 80 x 320 cells = 10240 px square.
public static class WorldLayout
{
    public const int MapWidth = 80;
    public const int MapHeight = 320;
    public const int WaterRows = 40;
    public const int BeachRows = 3;

    // ---- the fort ---------------------------------------------------------
    public static readonly Vector2I FortTop = new(30, 64);
    public const int FortWidth = 22, FortDepth = 18, GateOffset = 10;
    public static Vector2I GateCell => IsoGrid.Cell(FortTop, GateOffset, FortDepth - 1);
    public static Vector2I GateOutside => IsoGrid.DownLeft(IsoGrid.DownLeft(GateCell));
    public static IEnumerable<Vector2I> YardCells() => IsoGrid.Rhombus(FortTop, FortWidth, FortDepth);

    public static readonly BuildingDef Depot = new(BuildingKind.Depot, "Trading room", IsoGrid.Cell(FortTop, 3, 2), 7, 5, 3);
    public static readonly BuildingDef Warehouse = new(BuildingKind.Warehouse, "Warehouse", IsoGrid.Cell(FortTop, 12, 2), 8, 6, 3);
    public static readonly BuildingDef MensHouse = new(BuildingKind.MensHouse, "Men's house", IsoGrid.Cell(FortTop, 3, 10), 7, 5, 3);

    // ---- the Nor'Westers' post, across the river (design doc 5.2, 7.1) ------------
    public static readonly Vector2I NwcTop = new(66, 150);
    public const int NwcWidth = 12, NwcDepth = 10, NwcGateOffset = 5;
    public static Vector2I NwcGateCell => IsoGrid.Cell(NwcTop, NwcGateOffset, NwcDepth - 1);
    public static readonly BuildingDef NwcHouse = new(BuildingKind.NwcHouse, "Nor'Westers' house", IsoGrid.Cell(NwcTop, 2, 2), 7, 5, 3);
    public static IEnumerable<Vector2I> NwcYardCells() => IsoGrid.Rhombus(NwcTop, NwcWidth, NwcDepth);
    public static Vector2I NwcWoodpileCell => IsoGrid.Cell(NwcTop, 9, 7);

    // A shallow crossing: the only way over the river on foot in summer.
    public static bool IsFord(Vector2I c) => c.Y >= 158 && c.Y <= 162 && IsRiver(c);

    public static readonly BuildingDef[] Buildings = { Depot, Warehouse, MensHouse, NwcHouse };

    // Yard props.
    public static Vector2I WoodpileCell => IsoGrid.Cell(FortTop, 13, 11);
    public static Vector2I FlagCell => IsoGrid.Cell(FortTop, 10, 9);

    // ---- wharf and ship ---------------------------------------------------
    public static readonly Vector2I WharfRoot = new(38, 42);
    public const int WharfLength = 14;
    public static IEnumerable<Vector2I> WharfCells()
    {
        var c = WharfRoot;
        for (int i = 0; i < WharfLength; i++) { yield return c; c = IsoGrid.UpRight(c); }
    }
    public static Vector2I WharfEnd
    {
        get { var c = WharfRoot; for (int i = 0; i < WharfLength - 1; i++) c = IsoGrid.UpRight(c); return c; }
    }
    // Where the ship's hull sits when tied up: alongside the last planks.
    public static Vector2 ShipBerth => IsoGrid.ToWorld(WharfEnd) + new Vector2(-40, -120);
    public static Vector2 ShipOffscreen => new(MapWidth * IsoGrid.TileW + 600, 700);

    // ---- the Cree camp ----------------------------------------------------
    public static readonly Vector2I CampCentre = new(14, 140);
    public const float CampClearingPx = 460f;
    public static Vector2I CampFire => CampCentre;
    public static IEnumerable<Vector2I> TipiCells()
    {
        var c = CampCentre;
        yield return IsoGrid.Cell(c, 3, 0);
        yield return IsoGrid.Cell(c, 0, 3);
        yield return IsoGrid.Cell(c, -3, 0);
        yield return IsoGrid.Cell(c, 0, -3);
        yield return IsoGrid.Cell(c, 3, 3);
    }

    // ---- the river --------------------------------------------------------
    // Column of the river's centre at a given row; it meanders a little.
    public static float RiverCentre(int row) => 58f + 4f * Mathf.Sin(row / 25f) + 1.5f * Mathf.Sin(row / 7f);
    public static bool IsRiver(Vector2I c) => c.Y >= WaterRows - 6 && Mathf.Abs(c.X - RiverCentre(c.Y)) <= 1.4f;
    public static bool IsRiverBank(Vector2I c) => !IsRiver(c) && Mathf.Abs(c.X - RiverCentre(c.Y)) <= 2.6f;

    // ---- wildlife sites (design doc 16.4) -------------------------------------
    public static readonly SiteDef[] Sites =
    {
        new("Caribou yard", Species.Caribou, new Vector2I(12, 200), 90, 140, 900f),
        new("Goose marsh", Species.Goose, new Vector2I(66, 46), 180, 300, 700f),
        new("Beaver lodge (upper)", Species.Beaver, new Vector2I(62, 118), 40, 60, 350f),
        new("Beaver lodge (bend)", Species.Beaver, new Vector2I(55, 182), 45, 60, 350f),
        new("Beaver lodge (lower)", Species.Beaver, new Vector2I(60, 246), 36, 60, 350f),
        new("Wolf den", Species.Wolf, new Vector2I(6, 280), 6, 10, 800f),
    };

    // ---- the trail from the gate to the camp ----------------------------------
    public static readonly Vector2[] Trail =
    {
        IsoGrid.ToWorld(GateOutside),
        IsoGrid.ToWorld(new Vector2I(22, 105)),
        IsoGrid.ToWorld(new Vector2I(16, 128)),
        IsoGrid.ToWorld(CampCentre),
    };

    public static float TrailDistance(Vector2 p)
    {
        float best = float.MaxValue;
        for (int i = 0; i + 1 < Trail.Length; i++)
        {
            best = Mathf.Min(best, Geometry2D.GetClosestPointToSegment(p, Trail[i], Trail[i + 1]).DistanceTo(p));
        }
        return best;
    }

    // ---- forest -----------------------------------------------------------
    // Everything west and south of the fort is bush, thinning toward the
    // fort, cleared along the trail, around the camp, and off the river.
    public static float ForestDensity(Vector2I c)
    {
        var p = IsoGrid.ToWorld(c);
        if (c.Y < WaterRows + 8) return 0f;
        if (IsRiver(c) || IsRiverBank(c)) return 0f;
        if (TrailDistance(p) < 130f) return 0f;
        if (p.DistanceTo(IsoGrid.ToWorld(CampCentre)) < CampClearingPx) return 0f;
        // Keep the fort's surroundings open.
        var fortCentre = IsoGrid.ToWorld(IsoGrid.Cell(FortTop, FortWidth / 2, FortDepth / 2));
        float fortDist = p.DistanceTo(fortCentre);
        if (fortDist < 1500f) return 0f;
        var nwcCentre = IsoGrid.ToWorld(IsoGrid.Cell(NwcTop, NwcWidth / 2, NwcDepth / 2));
        if (p.DistanceTo(nwcCentre) < 1100f) return 0f;
        float density = 0.12f;
        if (p.X < 3000f) density += 0.28f * Mathf.Clamp((3000f - p.X) / 1600f, 0f, 1f);
        if (p.Y > 4200f) density += 0.28f * Mathf.Clamp((p.Y - 4200f) / 1800f, 0f, 1f);
        if (fortDist < 2300f) density *= (fortDist - 1500f) / 800f;
        return Mathf.Clamp(density, 0f, 0.5f);
    }
}
