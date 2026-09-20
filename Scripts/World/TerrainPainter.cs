using Godot;
using System.Collections.Generic;
using IsometricFurTradeGame.Scripts.Core;

namespace IsometricFurTradeGame.Scripts.World;

// Lays down the ground from WorldLayout: the bay, a beach, the river, grass
// and bush inland, trodden dirt in the yard and the camp, and the wharf.
// Runs as a [Tool] so the map is visible in the editor without pressing
// Play; untick AutoGenerate and save to bake it for hand editing.
[Tool]
public partial class TerrainPainter : TileMapLayer
{
    public const int SourceGrass = 0;
    public const int SourceDirt = 1;
    public const int SourceHill = 2;
    public const int SourceSand = 3;
    public const int SourceWater = 5;
    public const int SourceWaterIce = 38;
    public const int SourceSnow = 43;
    public const int SourceIce = 44;
    public const int SourceWharf = 51;

    // What each cell looks like in open-water season, so a thaw can restore it.
    private readonly Dictionary<Vector2I, int> _thawed = new();
    private bool _frozen;

    [Export] public bool AutoGenerate = true;
    [Export] public NodePath WaterCollisionPath;
    [Export] public NodePath PlayerSpawnPath;
    [Export] public NodePath StructurePainterPath;

    // Drop the player on a specific cell instead of the gate approach.
    [Export] public Vector2I SpawnOverride = new(-1, -1);

    public override void _Ready()
    {
        if (AutoGenerate)
        {
            GenerateMap();
        }
        BuildWaterCollision();
        if (!Engine.IsEditorHint())
        {
            AddToGroup("terrain");
            Sim.Terrain = this;
            PlacePlayer();
            var cal = GameCalendar.Instance;
            if (cal != null)
            {
                cal.SeasonChanged += _ => ApplySeason(cal.IsFrozen);
                ApplySeason(cal.IsFrozen);
            }
        }
    }

    // Winter buries the land in snow and freezes the bay solid. Frozen water
    // has no collision, so the sea route closes but the ice becomes a road.
    private void ApplySeason(bool frozen)
    {
        if (frozen == _frozen) return;
        _frozen = frozen;

        if (frozen)
        {
            _thawed.Clear();
            foreach (Vector2I cell in GetUsedCells())
            {
                int src = GetCellSourceId(cell);
                int winter = src switch
                {
                    SourceGrass or SourceSand or SourceDirt => SourceSnow,
                    SourceWater or SourceWaterIce => SourceIce,
                    _ => -1,
                };
                if (winter < 0) continue;
                _thawed[cell] = src;
                SetCell(cell, winter, Vector2I.Zero);
            }
        }
        else
        {
            foreach (var (cell, src) in _thawed)
            {
                SetCell(cell, src, Vector2I.Zero);
            }
            _thawed.Clear();
        }
        BuildWaterCollision();
    }

    public bool IsFrozen => _frozen;

    private StructurePainter Structures =>
        StructurePainterPath == null || StructurePainterPath.IsEmpty
            ? null
            : GetNodeOrNull<StructurePainter>(StructurePainterPath);

    private void GenerateMap()
    {
        Clear();

        var yard = new HashSet<Vector2I>(WorldLayout.YardCells());
        var wharf = new HashSet<Vector2I>(WorldLayout.WharfCells());
        var camp = IsoGrid.ToWorld(WorldLayout.CampCentre);

        for (int x = 0; x < WorldLayout.MapWidth; x++)
        {
            for (int y = 0; y < WorldLayout.MapHeight; y++)
            {
                var cell = new Vector2I(x, y);
                var p = IsoGrid.ToWorld(cell);
                int source = SourceGrass;

                int shore = ShorelineRow(x);
                if (wharf.Contains(cell)) source = SourceWharf;
                else if (y < shore) source = IsIceCell(cell) ? SourceWaterIce : SourceWater;
                else if (WorldLayout.IsRiver(cell)) source = SourceWater;
                else if (y < shore + WorldLayout.BeachRows || WorldLayout.IsRiverBank(cell)) source = SourceSand;
                else if (yard.Contains(cell)) source = SourceDirt;
                else if (p.DistanceTo(camp) < 300f) source = SourceDirt;
                else if (WorldLayout.TrailDistance(p) < 70f) source = SourceDirt;

                SetCell(cell, source, Vector2I.Zero);
            }
        }
    }

    // Where the water gives way to beach, for a given column. A dead-straight
    // boundary renders as a perfect isometric staircase, so the row wanders a
    // little to break it into something that reads as a coast.
    public static int ShorelineRow(int x)
    {
        int h = (int)((x * 2654435761u) & 0x7fffffff);
        int jitter = (h % 5) - 2 + ((x / 3) % 3) - 1;
        return Mathf.Max(2, WorldLayout.WaterRows + jitter);
    }

    // Sparse drift ice, scattered by a hash of the cell so the bay doesn't
    // read as one repeating tile but stays identical between runs.
    private static bool IsIceCell(Vector2I cell)
    {
        int h = cell.X * 73856093 ^ cell.Y * 19349663;
        return (h & 0x7fffffff) % 11 == 0;
    }

    public bool IsWater(Vector2I cell)
    {
        int src = GetCellSourceId(cell);
        return src == SourceWater || src == SourceWaterIce;
    }

    // Water or ice: what you can drink from (or melt).
    public bool IsWaterOrIce(Vector2I cell)
    {
        int src = GetCellSourceId(cell);
        return src == SourceWater || src == SourceWaterIce || src == SourceIce;
    }

    // Any water (or ice) within a cell of the position.
    public bool IsWaterNear(Vector2 localPos)
    {
        var c = LocalToMap(localPos);
        if (IsWaterOrIce(c)) return true;
        foreach (var n in IsoGrid.EdgeNeighbours(c)) if (IsWaterOrIce(n)) return true;
        foreach (var n in IsoGrid.CornerNeighbours(c)) if (IsWaterOrIce(n)) return true;
        return false;
    }

    private void PlacePlayer()
    {
        if (PlayerSpawnPath == null || PlayerSpawnPath.IsEmpty) return;
        var player = GetNodeOrNull<Node2D>(PlayerSpawnPath);
        if (player == null) return;

        player.Position = MapToLocal(DefaultSpawnCell());

        // Dev aid: `godot -- --spawn=21,32` drops the player on a cell.
        foreach (string arg in OS.GetCmdlineUserArgs())
        {
            if (!arg.StartsWith("--spawn=")) continue;
            var parts = arg.Substring(8).Split(',');
            if (parts.Length == 2 && int.TryParse(parts[0], out int sx) && int.TryParse(parts[1], out int sy))
            {
                player.Position = MapToLocal(new Vector2I(sx, sy));
            }
            else if (parts.Length == 1)
            {
                player.Position = MapToLocal(NamedSpot(parts[0]));
            }
        }
    }

    public static Vector2I NamedSpot(string name) => name switch
    {
        "depot" => WorldLayout.Depot.Centre,
        "warehouse" => WorldLayout.Warehouse.Centre,
        "menshouse" => WorldLayout.MensHouse.Centre,
        "wharf" => WorldLayout.WharfRoot,
        "berth" => IsoGrid.Cell(WorldLayout.WharfEnd, -2, 0),
        "camp" => IsoGrid.Cell(WorldLayout.CampCentre, 1, 1),
        "caribou" => WorldLayout.Sites[0].Cell,
        "geese" => WorldLayout.Sites[1].Cell,
        "beaver" => WorldLayout.Sites[2].Cell,
        "wolves" => IsoGrid.Cell(WorldLayout.Sites[5].Cell, 4, 0),
        _ => WorldLayout.GateOutside,
    };

    private Vector2I DefaultSpawnCell()
    {
        if (SpawnOverride.X >= 0 && SpawnOverride.Y >= 0) return SpawnOverride;
        return WorldLayout.GateOutside;
    }

    private void BuildWaterCollision()
    {
        if (WaterCollisionPath == null || WaterCollisionPath.IsEmpty) return;
        var body = GetNodeOrNull<StaticBody2D>(WaterCollisionPath);
        if (body == null) return;

        foreach (Node child in body.GetChildren())
        {
            child.Free();
        }

        var diamond = new ConvexPolygonShape2D
        {
            Points = new[]
            {
                new Vector2(0, -32), new Vector2(64, 0),
                new Vector2(0, 32), new Vector2(-64, 0),
            },
        };

        foreach (Vector2I cell in GetUsedCells())
        {
            if (!IsWater(cell)) continue;
            body.AddChild(new CollisionShape2D { Shape = diamond, Position = MapToLocal(cell) });
        }
    }
}
