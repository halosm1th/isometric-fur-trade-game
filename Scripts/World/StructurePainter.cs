using Godot;
using System.Collections.Generic;
using IsometricFurTradeGame.Scripts.Core;

namespace IsometricFurTradeGame.Scripts.World;

// Paints everything that stands on the ground - the palisade, the buildings,
// the Cree camp, the bush - and owns the doors and the fires in them.
// Everything is one continuous world: a building's interior is just more
// cells, furnished in place.
//
// Wall tiles cover a single cell EDGE, and each of the four sides uses the
// variant drawn on that edge:
//   E = back-left (NW edge)   S = back-right (NE edge)
//   N = front-left (SW edge)  W = front-right (SE edge)
//
// Back-facing walls live on this layer; front-facing walls live on
// FrontLayer. That does two things: a corner cell can hold both of its
// walls (one per layer), and the front layer can fade when the player is
// indoors so the building's own front wall doesn't hide them.
[Tool]
public partial class StructurePainter : TileMapLayer
{
    public const int SourceWallN = 6, SourceWallE = 7, SourceWallS = 8, SourceWallW = 9;
    public const int SourceDoorN = 10;
    public const int SourceWindowN = 14;
    public const int SourceFenceN = 22, SourceFenceE = 23, SourceFenceS = 24, SourceFenceW = 25;
    public const int SourceFloorPlanks = 26;
    public const int SourceShelf = 27, SourceShelfTall = 28, SourceTable = 29, SourceTableChairs = 30, SourceChair = 31;
    public const int SourceDisplayCase = 32, SourceCandleStand = 33, SourceRug = 34;
    public const int SourceCrate = 35, SourceHayBales = 36;
    public const int SourceDoorOpenN = 39;
    public const int SourceWallCornerBack = 45, SourceWallCornerFront = 46;
    public const int SourceFenceCornerBack = 47, SourceFenceCornerFront = 48;
    public const int SourceHearthLeft = 49, SourceHearthRight = 50;
    public const int SourceSpruce0 = 52, SourceTipi = 55, SourceCampfire = 56, SourceWoodpile = 57, SourceFurBale = 58;

    // Source id ranges whose tiles sit on a cell edge, laid out N,E,S,W.
    private static readonly int[] EdgeFamilyBases = { 6, 10, 14, 18, 22, 39 };
    private static readonly HashSet<int> OpenDoors = new() { 39, 40, 41, 42 };
    private static readonly HashSet<int> SolidProps = new() { 27, 28, 29, 30, 32, 35, 36, 49, 50, 52, 53, 54, 56, 57, 58 };
    private static readonly HashSet<int> Hearths = new() { 49, 50, 56 };

    [Export] public bool AutoGenerate = true;
    [Export] public NodePath CollisionBodyPath;
    [Export] public NodePath FloorLayerPath;
    [Export] public NodePath FrontLayerPath;
    [Export] public NodePath PlayerPath;

    // How see-through the front wall goes while the player is indoors.
    [Export(PropertyHint.Range, "0,1")] public float IndoorFrontAlpha = 0.2f;

    private TileMapLayer _floor;
    private TileMapLayer _front;
    private Node2D _player;
    private StaticBody2D _body;
    private readonly List<Hearth> _hearths = new();
    private readonly Dictionary<Vector2I, Hearth> _hearthByCell = new();

    // Kept for older callers; the layout is the source of truth.
    public Vector2I GateCell => WorldLayout.GateCell;
    public Vector2I DoorCell => WorldLayout.Depot.DoorCell;

    public override void _Ready()
    {
        _front = GetNodeOrNull<TileMapLayer>(FrontLayerPath);
        _floor = GetNodeOrNull<TileMapLayer>(FloorLayerPath);
        _body = GetNodeOrNull<StaticBody2D>(CollisionBodyPath);
        _player = GetNodeOrNull<Node2D>(PlayerPath);

        if (AutoGenerate)
        {
            Paint();
        }
        RebuildCollision();

        if (!Engine.IsEditorHint())
        {
            AddToGroup("structures");
            Sim.Structures = this;
            if (Sim.Terrain != null) Sim.Paths = new Pathfinder(Sim.Terrain, this);
            SpawnHearths();
        }
    }

    public override void _Process(double delta)
    {
        if (Engine.IsEditorHint() || _front == null || _player == null) return;
        float target = IsSheltered(_player.Position) ? IndoorFrontAlpha : 1f;
        var m = _front.Modulate;
        m.A = Mathf.MoveToward(m.A, target, (float)delta * 4f);
        _front.Modulate = m;
    }

    // "Indoors" is a property of the tile you stand on (design doc 18.1): a
    // roofed floor, or a tipi's footprint.
    public bool IsSheltered(Vector2 localPos) => IsShelteredCell(LocalToMap(localPos));

    public bool IsShelteredCell(Vector2I cell)
    {
        var floor = _floor ?? this;
        return floor.GetCellSourceId(cell) == SourceFloorPlanks || GetCellSourceId(cell) == SourceTipi;
    }

    // ---- fire -----------------------------------------------------------

    private void SpawnHearths()
    {
        _hearths.Clear();
        _hearthByCell.Clear();
        foreach (var cell in GetUsedCells())
        {
            if (!Hearths.Contains(GetCellSourceId(cell))) continue;
            var hearth = new Hearth { Position = MapToLocal(cell), Name = $"Hearth_{cell.X}_{cell.Y}", Cell = cell };
            if (GetCellSourceId(cell) == SourceCampfire) hearth.FuelHours = 4f;
            AddChild(hearth);
            _hearths.Add(hearth);
            _hearthByCell[cell] = hearth;
        }
    }

    public IReadOnlyList<Hearth> AllHearths => _hearths;

    public float HeatAt(Vector2 worldPos) => HeatAt(worldPos, out _);

    public float HeatAt(Vector2 worldPos, out bool enclosed)
    {
        float best = 0f; enclosed = false;
        foreach (var h in _hearths)
        {
            float v = h.HeatAt(worldPos);
            if (v > best) { best = v; enclosed = h.Enclosed; }
        }
        return best;
    }

    public Hearth HearthNear(Vector2 worldPos, float reach = 140f)
    {
        Hearth best = null;
        foreach (var h in _hearths)
        {
            float d = h.GlobalPosition.DistanceTo(worldPos);
            if (d < reach) { reach = d; best = h; }
        }
        return best;
    }

    public Hearth NearestHearth(Vector2 worldPos) => HearthNear(worldPos, float.MaxValue);

    // ---- painting -------------------------------------------------------

    private void Paint()
    {
        Clear();
        _front?.Clear();
        var floor = _floor ?? this;
        floor.Clear();

        PaintPalisade();
        foreach (var b in WorldLayout.Buildings) PaintBuilding(b, floor);
        PaintYard();
        PaintCamp();
        PaintForest();
    }

    private void SetBack(Vector2I cell, int source) => SetCell(cell, source, Vector2I.Zero);
    private void SetFront(Vector2I cell, int source) => (_front ?? this).SetCell(cell, source, Vector2I.Zero);

    private void PaintPalisade()
    {
        var top = WorldLayout.FortTop;
        int w = WorldLayout.FortWidth, d = WorldLayout.FortDepth;
        var gate = WorldLayout.GateCell;
        var gateNext = IsoGrid.Cell(top, WorldLayout.GateOffset + 1, d - 1);

        for (int i = 0; i < w; i++)
        {
            SetBack(IsoGrid.Cell(top, i, 0), SourceFenceS);
            var front = IsoGrid.Cell(top, i, d - 1);
            if (front != gate && front != gateNext) SetFront(front, SourceFenceN);
        }
        for (int j = 0; j < d; j++)
        {
            SetBack(IsoGrid.Cell(top, 0, j), SourceFenceE);
            SetFront(IsoGrid.Cell(top, w - 1, j), SourceFenceW);
        }
        // Top and bottom corners need two walls that both belong on the same
        // layer, so they take a composite corner tile.
        SetBack(top, SourceFenceCornerBack);
        SetFront(IsoGrid.Cell(top, w - 1, d - 1), SourceFenceCornerFront);
    }

    private void PaintBuilding(BuildingDef b, TileMapLayer floor)
    {
        var top = b.Top;
        foreach (var cell in IsoGrid.Rhombus(top, b.Width, b.Depth))
        {
            floor.SetCell(cell, SourceFloorPlanks, Vector2I.Zero);
        }

        var door = b.DoorCell;
        for (int i = 0; i < b.Width; i++)
        {
            // A window in the middle of the back wall.
            SetBack(IsoGrid.Cell(top, i, 0), i == b.Width / 2 ? SourceWindowN + 2 : SourceWallS);
            var front = IsoGrid.Cell(top, i, b.Depth - 1);
            SetFront(front, front == door ? SourceDoorN : SourceWallN);
        }
        for (int j = 0; j < b.Depth; j++)
        {
            SetBack(IsoGrid.Cell(top, 0, j), SourceWallE);
            SetFront(IsoGrid.Cell(top, b.Width - 1, j), SourceWallW);
        }
        SetBack(top, SourceWallCornerBack);
        SetFront(IsoGrid.Cell(top, b.Width - 1, b.Depth - 1), SourceWallCornerFront);

        switch (b.Kind)
        {
            case BuildingKind.Depot:
                // Trading room: hearth on the back-left wall, counter and
                // shelves of trade goods, a table to eat at.
                SetBack(IsoGrid.Cell(top, 1, 1), SourceHearthLeft);
                SetBack(IsoGrid.Cell(top, 1, 2), SourceShelf);
                SetBack(IsoGrid.Cell(top, 2, 1), SourceShelfTall);
                SetBack(IsoGrid.Cell(top, 5, 1), SourceCrate);
                SetBack(IsoGrid.Cell(top, 1, 3), SourceDisplayCase);
                SetBack(IsoGrid.Cell(top, 3, 2), SourceRug);
                SetBack(IsoGrid.Cell(top, 4, 2), SourceTableChairs);
                SetBack(IsoGrid.Cell(top, 5, 3), SourceCandleStand);
                break;
            case BuildingKind.Warehouse:
                // Bales of furs one side, cargo the other, an aisle between.
                for (int j = 1; j <= 4; j++) SetBack(IsoGrid.Cell(top, 1, j), SourceFurBale);
                for (int j = 1; j <= 4; j++) SetBack(IsoGrid.Cell(top, 2, j), j % 2 == 1 ? SourceFurBale : SourceCrate);
                for (int j = 1; j <= 4; j++) SetBack(IsoGrid.Cell(top, 6, j), SourceCrate);
                SetBack(IsoGrid.Cell(top, 5, 1), SourceCrate);
                SetBack(IsoGrid.Cell(top, 5, 2), SourceHayBales);
                break;
            case BuildingKind.MensHouse:
                // Where the labourers sleep: a big hearth, benches, a table.
                SetBack(IsoGrid.Cell(top, 1, 1), SourceHearthLeft);
                SetBack(IsoGrid.Cell(top, 3, 1), SourceTable);
                SetBack(IsoGrid.Cell(top, 4, 1), SourceChair);
                SetBack(IsoGrid.Cell(top, 5, 1), SourceCrate);
                SetBack(IsoGrid.Cell(top, 1, 3), SourceRug);
                SetBack(IsoGrid.Cell(top, 5, 3), SourceCandleStand);
                break;
        }
    }

    private void PaintYard()
    {
        SetBack(WorldLayout.WoodpileCell, SourceWoodpile);
        SetBack(IsoGrid.Cell(WorldLayout.FortTop, 15, 12), SourceCrate);
        SetBack(IsoGrid.Cell(WorldLayout.FortTop, 16, 13), SourceHayBales);
    }

    private void PaintCamp()
    {
        SetBack(WorldLayout.CampFire, SourceCampfire);
        foreach (var c in WorldLayout.TipiCells()) SetBack(c, SourceTipi);
        SetBack(IsoGrid.Cell(WorldLayout.CampCentre, 2, -2), SourceWoodpile);
    }

    private void PaintForest()
    {
        for (int x = 0; x < WorldLayout.MapWidth; x++)
        {
            for (int y = WorldLayout.WaterRows; y < WorldLayout.MapHeight; y++)
            {
                var cell = new Vector2I(x, y);
                float density = WorldLayout.ForestDensity(cell);
                if (density <= 0f || IsoGrid.Hash(cell, 11) >= density) continue;
                if (GetCellSourceId(cell) != -1) continue;
                SetBack(cell, SourceSpruce0 + (int)(IsoGrid.Hash(cell, 12) * 3f));
            }
        }
    }

    // A tree was felled: the cell clears and the cutter gets wood.
    public bool FellTreeAt(Vector2I cell)
    {
        int s = GetCellSourceId(cell);
        if (s < SourceSpruce0 || s > SourceSpruce0 + 2) return false;
        EraseCell(cell);
        RebuildCollision();
        return true;
    }

    public bool IsTree(Vector2I cell)
    {
        int s = GetCellSourceId(cell);
        return s >= SourceSpruce0 && s <= SourceSpruce0 + 2;
    }

    public Vector2I? NearestTree(Vector2 worldPos, float maxDist = 4000f)
    {
        Vector2I? best = null;
        float bestD = maxDist;
        foreach (var cell in GetUsedCells())
        {
            if (!IsTree(cell)) continue;
            float d = MapToLocal(cell).DistanceTo(worldPos);
            if (d < bestD) { bestD = d; best = cell; }
        }
        return best;
    }

    // ---- doors ----------------------------------------------------------

    public bool TryToggleDoorNear(Vector2 localPos, float reach = 110f)
    {
        TileMapLayer bestLayer = null;
        Vector2I bestCell = default;
        float bestDist = reach;

        foreach (var layer in Layers())
        {
            foreach (var cell in layer.GetUsedCells())
            {
                int s = layer.GetCellSourceId(cell);
                if (!IsDoor(s)) continue;
                float d = localPos.DistanceTo(MapToLocal(cell));
                if (d < bestDist)
                {
                    bestDist = d; bestLayer = layer; bestCell = cell;
                }
            }
        }
        if (bestLayer == null) return false;
        ToggleDoor(bestLayer, bestCell);
        return true;
    }

    private void ToggleDoor(TileMapLayer layer, Vector2I cell)
    {
        int src = layer.GetCellSourceId(cell);
        bool isOpen = OpenDoors.Contains(src);
        int dir = src - (isOpen ? SourceDoorOpenN : SourceDoorN);
        layer.SetCell(cell, (isOpen ? SourceDoorN : SourceDoorOpenN) + dir, Vector2I.Zero);
        RebuildCollision();
    }

    // Make sure the door on this cell is open (NPCs use this on their way in).
    public void OpenDoorAt(Vector2I cell)
    {
        foreach (var layer in Layers())
        {
            int s = layer.GetCellSourceId(cell);
            if (s >= 10 && s <= 13) { ToggleDoor(layer, cell); return; }
        }
    }

    private static bool IsDoor(int s) => (s >= 10 && s <= 13) || OpenDoors.Contains(s);
    private static bool IsClosedDoor(int s) => s >= 10 && s <= 13;

    // ---- walkability (for pathfinding) --------------------------------------

    // Does anything on the structure layers fill this cell? Trees don't
    // count: they only have a trunk's worth of collision and you can push
    // through the bush (slowly - the pathfinder charges for it).
    public bool IsCellSolid(Vector2I cell)
    {
        foreach (var layer in Layers())
        {
            int s = layer.GetCellSourceId(cell);
            if (SolidProps.Contains(s) && !IsTreeSource(s)) return true;
        }
        return false;
    }

    public static bool IsTreeSource(int s) => s >= SourceSpruce0 && s <= SourceSpruce0 + 2;

    // Is there a wall/fence/closed door on the edge between two edge-adjacent
    // cells? Closed doors count as passable-with-effort: the caller opens them.
    public bool EdgeBlocked(Vector2I from, Vector2I to, out bool isDoor)
    {
        isDoor = false;
        // Which edge of `from` faces `to`, and which edge of `to` faces `from`.
        int fromDir = EdgeIndex(from, to);
        int toDir = EdgeIndex(to, from);
        if (fromDir < 0 || toDir < 0) return false;
        return WallOnEdge(from, fromDir, ref isDoor) || WallOnEdge(to, toDir, ref isDoor);
    }

    // 0=N(SW edge) 1=E(NW) 2=S(NE) 3=W(SE), matching the wall variants.
    private static int EdgeIndex(Vector2I cell, Vector2I neighbour)
    {
        if (neighbour == IsoGrid.DownLeft(cell)) return 0;
        if (neighbour == IsoGrid.UpLeft(cell)) return 1;
        if (neighbour == IsoGrid.UpRight(cell)) return 2;
        if (neighbour == IsoGrid.DownRight(cell)) return 3;
        return -1;
    }

    private bool WallOnEdge(Vector2I cell, int edge, ref bool isDoor)
    {
        foreach (var layer in Layers())
        {
            int s = layer.GetCellSourceId(cell);
            if (s < 0) continue;
            if (s == SourceWallCornerBack || s == SourceFenceCornerBack) { if (edge == 1 || edge == 2) return true; continue; }
            if (s == SourceWallCornerFront || s == SourceFenceCornerFront) { if (edge == 0 || edge == 3) return true; continue; }
            if (OpenDoors.Contains(s)) continue;
            foreach (int b in EdgeFamilyBases)
            {
                if (s < b || s > b + 3) continue;
                if (s - b == edge)
                {
                    if (IsClosedDoor(s)) { isDoor = true; return false; }
                    return true;
                }
            }
        }
        return false;
    }

    // ---- collision ------------------------------------------------------

    private IEnumerable<TileMapLayer> Layers()
    {
        yield return this;
        if (_front != null) yield return _front;
    }

    private void RebuildCollision()
    {
        if (_body == null) return;
        foreach (Node child in _body.GetChildren())
        {
            child.Free();
        }
        foreach (var layer in Layers())
        {
            foreach (var cell in layer.GetUsedCells())
            {
                foreach (var shape in ShapesFor(layer.GetCellSourceId(cell)))
                {
                    _body.AddChild(new CollisionShape2D { Shape = shape, Position = MapToLocal(cell) });
                }
            }
        }
    }

    // Edge tiles get a thin strip along their edge, so the rest of the cell
    // stays walkable - otherwise a room loses its whole perimeter ring of
    // floor. Props get the full cell. Open doors get nothing.
    private const float WallHalfThickness = 7f;

    private static readonly Shape2D EdgeN = Strip(-64, 0, 0, 32, WallHalfThickness);   // SW edge
    private static readonly Shape2D EdgeE = Strip(-64, 0, 0, -32, WallHalfThickness);  // NW edge
    private static readonly Shape2D EdgeS = Strip(0, -32, 64, 0, WallHalfThickness);   // NE edge
    private static readonly Shape2D EdgeW = Strip(0, 32, 64, 0, WallHalfThickness);    // SE edge
    private static readonly Shape2D SmallDiamond = Diamond(0, -18, 36, 0, 0, 18, -36, 0);

    private static IEnumerable<Shape2D> ShapesFor(int source)
    {
        if (OpenDoors.Contains(source)) yield break;

        if (IsTreeSource(source))
        {
            // Bush is passable (slowly, for the pathfinder); no collision.
            yield break;
        }
        if (SolidProps.Contains(source))
        {
            yield return Diamond(0, -32, 64, 0, 0, 32, -64, 0);
            yield break;
        }

        switch (source)
        {
            case SourceWallCornerBack or SourceFenceCornerBack:
                yield return EdgeE; yield return EdgeS; yield break;
            case SourceWallCornerFront or SourceFenceCornerFront:
                yield return EdgeN; yield return EdgeW; yield break;
        }

        foreach (int b in EdgeFamilyBases)
        {
            if (source < b || source > b + 3) continue;
            yield return (source - b) switch { 0 => EdgeN, 1 => EdgeE, 2 => EdgeS, _ => EdgeW };
        }
    }

    private static ConvexPolygonShape2D Diamond(params float[] p) =>
        new() { Points = new[] { new Vector2(p[0], p[1]), new Vector2(p[2], p[3]), new Vector2(p[4], p[5]), new Vector2(p[6], p[7]) } };

    private static ConvexPolygonShape2D Strip(float ax, float ay, float bx, float by, float t)
    {
        var a = new Vector2(ax, ay); var b = new Vector2(bx, by);
        var n = (b - a).Orthogonal().Normalized() * t;
        return new ConvexPolygonShape2D { Points = new[] { a + n, b + n, b - n, a - n } };
    }
}
