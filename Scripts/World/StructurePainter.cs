using Godot;
using System.Collections.Generic;

namespace IsometricFurTradeGame.Scripts.World;

// Paints the York Factory palisade and the depot building, and owns the
// doors in them. Everything is one continuous world: the building's interior
// is just more cells, furnished in place.
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
    private const int SourceWallN = 6, SourceWallE = 7, SourceWallS = 8, SourceWallW = 9;
    private const int SourceDoorN = 10;
    private const int SourceFenceN = 22, SourceFenceE = 23, SourceFenceS = 24, SourceFenceW = 25;
    private const int SourceFloorPlanks = 26;
    private const int SourceShelf = 27, SourceShelfTall = 28, SourceTableChairs = 30;
    private const int SourceDisplayCase = 32, SourceCandleStand = 33, SourceRug = 34;
    private const int SourceCrate = 35, SourceHayBales = 36;
    private const int SourceDoorOpenN = 39;
    private const int SourceWallCornerBack = 45, SourceWallCornerFront = 46;
    private const int SourceFenceCornerBack = 47, SourceFenceCornerFront = 48;
    private const int SourceHearthLeft = 49, SourceHearthRight = 50;

    // Source id ranges whose tiles sit on a cell edge, laid out N,E,S,W.
    private static readonly int[] EdgeFamilyBases = { 6, 10, 14, 18, 22, 39 };
    private static readonly HashSet<int> OpenDoors = new() { 39, 40, 41, 42 };
    private static readonly HashSet<int> SolidProps = new() { 27, 28, 29, 30, 32, 35, 36, 49, 50 };
    private static readonly HashSet<int> Hearths = new() { 49, 50 };

    [Export] public bool AutoGenerate = true;
    [Export] public NodePath CollisionBodyPath;
    [Export] public NodePath FloorLayerPath;
    [Export] public NodePath FrontLayerPath;
    [Export] public NodePath PlayerPath;

    [Export] public Vector2I YardTopCorner = new(20, 20);
    [Export] public int YardWidth = 16;
    [Export] public int YardDepth = 13;
    [Export] public int GateOffset = 7;

    [Export] public Vector2I BuildingOffset = new(3, 2);
    [Export] public int BuildingWidth = 7;
    [Export] public int BuildingDepth = 5;
    [Export] public int BuildingDoorOffset = 3;

    // How see-through the front wall goes while the player is indoors.
    [Export(PropertyHint.Range, "0,1")] public float IndoorFrontAlpha = 0.2f;

    private readonly HashSet<Vector2I> _buildingCells = new();
    private TileMapLayer _floor;
    private TileMapLayer _front;
    private Node2D _player;
    private StaticBody2D _body;
    private readonly List<Hearth> _hearths = new();

    public Vector2I BuildingTopCorner => IsoGrid.Cell(YardTopCorner, BuildingOffset.X, BuildingOffset.Y);
    public Vector2I DoorCell => IsoGrid.Cell(BuildingTopCorner, BuildingDoorOffset, BuildingDepth - 1);
    public Vector2I GateCell => IsoGrid.Cell(YardTopCorner, GateOffset, YardDepth - 1);
    public IEnumerable<Vector2I> YardCells() => IsoGrid.Rhombus(YardTopCorner, YardWidth, YardDepth);

    public override void _Ready()
    {
        _front = GetNodeOrNull<TileMapLayer>(FrontLayerPath);
        _floor = GetNodeOrNull<TileMapLayer>(FloorLayerPath);
        _body = GetNodeOrNull<StaticBody2D>(CollisionBodyPath);
        _player = GetNodeOrNull<Node2D>(PlayerPath);

        _buildingCells.Clear();
        foreach (var c in IsoGrid.Rhombus(BuildingTopCorner, BuildingWidth, BuildingDepth))
        {
            _buildingCells.Add(c);
        }

        if (AutoGenerate)
        {
            Paint();
        }
        RebuildCollision();

        if (!Engine.IsEditorHint())
        {
            AddToGroup("structures");
            SpawnHearths();
        }
    }

    // ---- fire -----------------------------------------------------------

    // Every hearth tile gets a live Hearth object with fuel and a glow. The
    // tile is the picture and the collision; the object is the fire.
    private void SpawnHearths()
    {
        _hearths.Clear();
        foreach (var cell in GetUsedCells())
        {
            if (!Hearths.Contains(GetCellSourceId(cell))) continue;
            var hearth = new Hearth { Position = MapToLocal(cell), Name = $"Hearth_{cell.X}_{cell.Y}" };
            AddChild(hearth);
            _hearths.Add(hearth);
        }
    }

    public float HeatAt(Vector2 worldPos)
    {
        float best = 0f;
        foreach (var h in _hearths) best = Mathf.Max(best, h.HeatAt(worldPos));
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

    public override void _Process(double delta)
    {
        if (Engine.IsEditorHint() || _front == null || _player == null)
        {
            return;
        }
        float target = IsSheltered(_player.Position) ? IndoorFrontAlpha : 1f;
        var m = _front.Modulate;
        m.A = Mathf.MoveToward(m.A, target, (float)delta * 4f);
        _front.Modulate = m;
    }

    // "Indoors" is a property of the tile you stand on (design doc 18.1): a
    // roofed floor. That's what exposure reads, and it works for rooms built
    // by hand in the editor just as well as generated ones.
    public bool IsSheltered(Vector2 localPos)
    {
        var floor = _floor ?? this;
        return floor.GetCellSourceId(floor.LocalToMap(localPos)) == SourceFloorPlanks;
    }

    // ---- painting -------------------------------------------------------

    private void Paint()
    {
        Clear();
        _front?.Clear();
        var floor = GetNodeOrNull<TileMapLayer>(FloorLayerPath) ?? this;
        floor.Clear();

        PaintPalisade();
        PaintBuilding(floor);
    }

    private void SetBack(Vector2I cell, int source) => SetCell(cell, source, Vector2I.Zero);
    private void SetFront(Vector2I cell, int source) => (_front ?? this).SetCell(cell, source, Vector2I.Zero);

    private void PaintPalisade()
    {
        var gate = GateCell;
        var gateNext = IsoGrid.Cell(YardTopCorner, GateOffset + 1, YardDepth - 1);

        for (int i = 0; i < YardWidth; i++)
        {
            SetBack(IsoGrid.Cell(YardTopCorner, i, 0), SourceFenceS);
            var front = IsoGrid.Cell(YardTopCorner, i, YardDepth - 1);
            if (front != gate && front != gateNext)
            {
                SetFront(front, SourceFenceN);
            }
        }
        for (int j = 0; j < YardDepth; j++)
        {
            SetBack(IsoGrid.Cell(YardTopCorner, 0, j), SourceFenceE);
            SetFront(IsoGrid.Cell(YardTopCorner, YardWidth - 1, j), SourceFenceW);
        }

        // Top and bottom corners need two walls that both belong on the same
        // layer, so they take a composite corner tile.
        SetBack(YardTopCorner, SourceFenceCornerBack);
        SetFront(IsoGrid.Cell(YardTopCorner, YardWidth - 1, YardDepth - 1), SourceFenceCornerFront);
    }

    private void PaintBuilding(TileMapLayer floor)
    {
        var top = BuildingTopCorner;
        foreach (var cell in _buildingCells)
        {
            floor.SetCell(cell, SourceFloorPlanks, Vector2I.Zero);
        }

        var door = DoorCell;
        for (int i = 0; i < BuildingWidth; i++)
        {
            SetBack(IsoGrid.Cell(top, i, 0), SourceWallS);
            var front = IsoGrid.Cell(top, i, BuildingDepth - 1);
            SetFront(front, front == door ? SourceDoorN : SourceWallN);
        }
        for (int j = 0; j < BuildingDepth; j++)
        {
            SetBack(IsoGrid.Cell(top, 0, j), SourceWallE);
            SetFront(IsoGrid.Cell(top, BuildingWidth - 1, j), SourceWallW);
        }
        SetBack(top, SourceWallCornerBack);
        SetFront(IsoGrid.Cell(top, BuildingWidth - 1, BuildingDepth - 1), SourceWallCornerFront);

        // Trading room. Furniture goes on the back layer so it y-sorts with
        // the player like anything else.
        SetBack(IsoGrid.Cell(top, 1, 1), SourceHearthLeft);
        SetBack(IsoGrid.Cell(top, 1, 2), SourceShelf);
        SetBack(IsoGrid.Cell(top, 2, 1), SourceShelfTall);
        SetBack(IsoGrid.Cell(top, 5, 1), SourceCrate);
        SetBack(IsoGrid.Cell(top, 1, 3), SourceDisplayCase);
        SetBack(IsoGrid.Cell(top, 3, 2), SourceRug);
        SetBack(IsoGrid.Cell(top, 4, 2), SourceTableChairs);
        SetBack(IsoGrid.Cell(top, 5, 3), SourceCandleStand);

        // Cargo in the yard.
        SetBack(IsoGrid.Cell(YardTopCorner, 10, 5), SourceCrate);
        SetBack(IsoGrid.Cell(YardTopCorner, 11, 7), SourceHayBales);
    }

    // ---- doors ----------------------------------------------------------

    // Toggles the nearest door within reach of `localPos`. Returns false if
    // there isn't one.
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
        if (bestLayer == null)
        {
            return false;
        }

        int src = bestLayer.GetCellSourceId(bestCell);
        bool isOpen = OpenDoors.Contains(src);
        int dir = src - (isOpen ? SourceDoorOpenN : SourceDoorN);
        bestLayer.SetCell(bestCell, (isOpen ? SourceDoorN : SourceDoorOpenN) + dir, Vector2I.Zero);
        RebuildCollision();
        return true;
    }

    private static bool IsDoor(int s) => (s >= 10 && s <= 13) || OpenDoors.Contains(s);

    // ---- collision ------------------------------------------------------

    private IEnumerable<TileMapLayer> Layers()
    {
        yield return this;
        if (_front != null) yield return _front;
    }

    private void RebuildCollision()
    {
        if (_body == null)
        {
            return;
        }
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

    private static IEnumerable<Shape2D> ShapesFor(int source)
    {
        if (OpenDoors.Contains(source)) yield break;

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
