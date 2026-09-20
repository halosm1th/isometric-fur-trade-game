using Godot;
using System.Collections.Generic;
using IsometricFurTradeGame.Scripts.Core;

namespace IsometricFurTradeGame.Scripts.World;

// Lays down the ground: water along the bay, a beach, grass inland, and
// trodden dirt inside the palisade yard. Runs as a [Tool] so the map is
// visible in the editor without pressing Play.
[Tool]
public partial class TerrainPainter : TileMapLayer
{
    private const int SourceGrass = 0;
    private const int SourceDirt = 1;
    private const int SourceHill = 2;
    private const int SourceSand = 3;
    private const int SourceWater = 5;
    private const int SourceWaterIce = 38;
    private const int SourceSnow = 43;
    private const int SourceIce = 44;

    // What each cell looks like in open-water season, so a thaw can restore it.
    private readonly Dictionary<Vector2I, int> _thawed = new();
    private bool _frozen;

    [Export] public bool AutoGenerate = true;
    [Export] public int MapWidth = 44;
    [Export] public int MapHeight = 52;

    // The bay has to be deep enough that the camera never sees past its far
    // edge into empty space, since the player can walk right up to the shore.
    [Export] public int WaterRows = 14;
    [Export] public int BeachRows = 3;
    [Export] public NodePath WaterCollisionPath;
    [Export] public NodePath PlayerSpawnPath;
    [Export] public NodePath StructurePainterPath;

    // Drop the player on a specific cell instead of the gate approach.
    // Useful for testing a particular spot, and for the eventual
    // "start at Fort William / Fort Garry" selection.
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

    private StructurePainter Structures =>
        StructurePainterPath == null || StructurePainterPath.IsEmpty
            ? null
            : GetNodeOrNull<StructurePainter>(StructurePainterPath);

    private void GenerateMap()
    {
        Clear();

        var yard = new HashSet<Vector2I>();
        var structures = Structures;
        if (structures != null)
        {
            foreach (Vector2I cell in structures.YardCells())
            {
                yard.Add(cell);
            }
        }

        for (int x = 0; x < MapWidth; x++)
        {
            for (int y = 0; y < MapHeight; y++)
            {
                var cell = new Vector2I(x, y);
                int source = SourceGrass;

                int shore = ShorelineRow(x);
                if (y < shore) source = IsIceCell(cell) ? SourceWaterIce : SourceWater;
                else if (y < shore + BeachRows) source = SourceSand;
                else if (yard.Contains(cell)) source = SourceDirt;
                else if (x == MapWidth - 3 && y == MapHeight - 3) source = SourceHill;

                SetCell(cell, source, Vector2I.Zero);
            }
        }
    }

    // Where the water gives way to beach, for a given column. A dead-straight
    // boundary renders as a perfect isometric staircase, so the row wanders a
    // little to break it into something that reads as a coast.
    private int ShorelineRow(int x)
    {
        int h = (int)((x * 2654435761u) & 0x7fffffff);
        int jitter = (h % 5) - 2 + ((x / 3) % 3) - 1;
        return Mathf.Max(2, WaterRows + jitter);
    }

    // Sparse drift ice, scattered by a hash of the cell so the bay doesn't
    // read as one repeating tile but stays identical between runs.
    private static bool IsIceCell(Vector2I cell)
    {
        int h = cell.X * 73856093 ^ cell.Y * 19349663;
        return (h & 0x7fffffff) % 11 == 0;
    }

    // Any water (or ice) within a cell of the position.
    public bool IsWaterNear(Vector2 localPos)
    {
        var c = LocalToMap(localPos);
        foreach (var cell in new[] { c, IsoGrid.DownLeft(c), IsoGrid.DownRight(c),
                                     new Vector2I(c.X - 1, c.Y), new Vector2I(c.X + 1, c.Y),
                                     new Vector2I(c.X, c.Y - 1), new Vector2I(c.X - 1, c.Y - 1), new Vector2I(c.X + 1, c.Y - 1) })
        {
            int src = GetCellSourceId(cell);
            if (src == SourceWater || src == SourceWaterIce || src == SourceIce) return true;
        }
        return false;
    }

    private void PlacePlayer()
    {
        if (PlayerSpawnPath == null || PlayerSpawnPath.IsEmpty)
        {
            return;
        }
        var player = GetNodeOrNull<Node2D>(PlayerSpawnPath);
        if (player == null)
        {
            return;
        }

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
        }
    }

    // Arrive on the approach outside the palisade gate.
    private Vector2I DefaultSpawnCell()
    {
        if (SpawnOverride.X >= 0 && SpawnOverride.Y >= 0)
        {
            return SpawnOverride;
        }
        var structures = Structures;
        if (structures == null)
        {
            return new Vector2I(MapWidth / 2, MapHeight / 2);
        }
        return IsoGrid.DownLeft(IsoGrid.DownLeft(structures.GateCell));
    }

    private void BuildWaterCollision()
    {
        if (WaterCollisionPath == null || WaterCollisionPath.IsEmpty)
        {
            return;
        }
        var body = GetNodeOrNull<StaticBody2D>(WaterCollisionPath);
        if (body == null)
        {
            return;
        }

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
            int source = GetCellSourceId(cell);
            if (source != SourceWater && source != SourceWaterIce)
            {
                continue;
            }
            body.AddChild(new CollisionShape2D
            {
                Shape = diamond,
                Position = MapToLocal(cell),
            });
        }
    }
}
