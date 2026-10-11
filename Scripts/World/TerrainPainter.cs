using Godot;
using System.Collections.Generic;
using IsometricFurTradeGame.Scripts.Core;

namespace IsometricFurTradeGame.Scripts.World;

// Lays down the ground from WorldMap (the whole play area, design doc 6.1)
// and WorldLayout (the posts in it): the sea and lakes, beaches and banks,
// ground by ecozone, trodden dirt in the yards and camps, the roads, the
// wharf.
//
// The world is far too big to paint at once, so it is painted in chunks
// round whoever is looking - the local player in a game, the visible part of
// the canvas in the editor - and forgotten again behind them. Nothing asks
// the painted tiles what the ground is: IsWater and the rest answer from the
// map, so a man walking a hundred miles off still knows a lake is a lake.
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
    public const int SourceRoad = 61;   // trodden dirt with footprints: the river road
    public const int SourceIce = 44;
    public const int SourceWharf = 51;

    // A chunk is 16 columns by 64 rows: 2048 px square.
    public const int ChunkCols = 16, ChunkRows = 64;
    // Painted this far beyond what can be seen, so walking never shows an edge.
    [Export] public float MarginPx = 1400f;
    [Export] public int ChunksPerFrame = 2;
    // A zoomed-out editor view stops filling in past this many.
    [Export] public int MaxChunks = 160;

    private bool _frozen;

    [Export] public bool AutoGenerate = true;
    // Streamed and generated tiles are never written into the scene file.
    public override void _ValidateProperty(Godot.Collections.Dictionary property) { if (AutoGenerate) GeneratedTileLayer.DontStore(property); }
    [Export] public NodePath WaterCollisionPath;
    [Export] public NodePath PlayerSpawnPath;
    [Export] public NodePath StructurePainterPath;

    // Drop the player on a specific cell instead of the gate approach.
    [Export] public Vector2I SpawnOverride = new(-1, -1);

    private readonly HashSet<Vector2I> _loaded = new();
    private readonly List<Vector2I> _queue = new();
    private readonly Dictionary<Vector2I, StaticBody2D> _chunkBodies = new();
    // The landing, and the boardwalk up to it from the Depot (planks, not dirt).
    private static readonly HashSet<Vector2I> Wharf = WharfSet();
    private static HashSet<Vector2I> WharfSet()
    {
        var w = new HashSet<Vector2I>(WorldLayout.WharfCells());
        foreach (var c in WorldLayout.BoardwalkCells()) w.Add(c);
        return w;
    }
    private static readonly HashSet<Vector2I> Yards = YardSet();
    private float _focusTimer;

    private static HashSet<Vector2I> YardSet()
    {
        var yard = new HashSet<Vector2I>(WorldLayout.YardCells());
        foreach (var c in WorldLayout.NwcYardCells()) yard.Add(c);
        foreach (var c in WorldLayout.OutpostYardCells()) yard.Add(c);
        return yard;
    }

    public override void _Ready()
    {
        Clear();
        if (!Engine.IsEditorHint())
        {
            AddToGroup("terrain");
            Sim.Terrain = this;
            Sim.LocalPlayerChanged += PlacePlayer;
            var cal = GameCalendar.Instance;
            if (cal != null)
            {
                cal.SeasonChanged += _ => ApplySeason(cal.IsFrozen);
                _frozen = cal.IsFrozen;
            }
        }
        // The country round the post is there on the first frame (deferred so
        // the structure painter has laid out the posts the forest goes round).
        if (AutoGenerate) CallDeferred(nameof(LoadAround));
    }

    private void LoadAround() { Refocus(); while (_queue.Count > 0) LoadNext(); }

    // Dev aid: `-- --perf` prints where the frame goes every few seconds.
    private float _perfTimer = -1f;
    private void PerfReport(double delta)
    {
        if (_perfTimer < 0f) { _perfTimer = 0f; foreach (string a in OS.GetCmdlineUserArgs()) if (a == "--perf") _perfTimer = 3f; if (_perfTimer == 0f) { _perfTimer = float.MaxValue; return; } }
        _perfTimer -= (float)delta;
        if (_perfTimer > 0f) return;
        _perfTimer = 3f;
        GD.Print($"[perf] fps {Performance.GetMonitor(Performance.Monitor.TimeFps):0}  process {Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000:0.0} ms  physics {Performance.GetMonitor(Performance.Monitor.TimePhysicsProcess) * 1000:0.0} ms  " +
            $"bodies {Performance.GetMonitor(Performance.Monitor.Physics2DActiveObjects):0}  pairs {Performance.GetMonitor(Performance.Monitor.Physics2DCollisionPairs):0}  islands {Performance.GetMonitor(Performance.Monitor.Physics2DIslandCount):0}  " +
            $"nodes {Performance.GetMonitor(Performance.Monitor.ObjectNodeCount):0}  chunks {_loaded.Count}  npcs {Sim.Npcs.Count}  player {(Sim.Player as Node2D)?.GlobalPosition}  skip {GameCalendar.Instance?.SkipRemaining:0.0}h  live {Core.Tiers.LiveCount}  {GameCalendar.Instance?.DateString} {GameCalendar.Instance?.TimeString}");
    }

    // Dev aid: `-- --jumps` reports anyone who moves further in one frame
    // than walking could take him (a teleport), with what he was doing.
    private readonly Dictionary<Agents.Npc, (Vector2 Pos, string Goal)> _lastSeen = new();
    private int _jumpsOn = -1;
    private void JumpReport()
    {
        if (_jumpsOn < 0) { _jumpsOn = 0; foreach (string a in OS.GetCmdlineUserArgs()) if (a == "--jumps") _jumpsOn = 1; }
        if (_jumpsOn == 0) return;
        foreach (var n in Sim.Npcs)
        {
            if (n == null || !IsInstanceValid(n)) continue;
            string goal = n.GoalDescription;
            if (_lastSeen.TryGetValue(n, out var last) && last.Pos.DistanceTo(n.GlobalPosition) > 300f)
                GD.Print($"[jump] {n.DisplayName} {last.Pos.DistanceTo(n.GlobalPosition):0} px, tier {n.Tier}, was '{last.Goal}', now '{goal}' at {GameCalendar.Instance?.TimeString}");
            _lastSeen[n] = (n.GlobalPosition, goal);
        }
    }

    public override void _Process(double delta)
    {
        if (!Engine.IsEditorHint()) { PerfReport(delta); JumpReport(); }
        if (!AutoGenerate) return;
        _focusTimer -= (float)delta;
        if (_focusTimer <= 0f) { _focusTimer = 0.25f; Refocus(); }
        for (int i = 0; i < ChunksPerFrame && _queue.Count > 0; i++) LoadNext();
    }

    // The editor's scene file must not fill up with streamed tiles.
    public override void _Notification(int what)
    {
        if (!Engine.IsEditorHint()) return;
        if (what == NotificationEditorPreSave) { UnloadAll(); Structures?.ClearAll(); }
        else if (what == NotificationEditorPostSave) { Structures?.Repaint(); _focusTimer = 0f; }
    }

    // ---- streaming --------------------------------------------------------

    public static Vector2I ChunkOf(Vector2I cell) => new(Mathf.FloorToInt(cell.X / (float)ChunkCols), Mathf.FloorToInt(cell.Y / (float)ChunkRows));
    private static Rect2 ChunkRect(Vector2I ch) => new(ch.X * ChunkCols * IsoGrid.TileW, ch.Y * ChunkRows * IsoGrid.TileH / 2f, ChunkCols * IsoGrid.TileW, ChunkRows * IsoGrid.TileH / 2f);

    // What has to be painted: a rectangle in world px round each viewer.
    private List<Rect2> Views()
    {
        var views = new List<Rect2>();
#if TOOLS
        if (Engine.IsEditorHint())
        {
            var vp = EditorInterface.Singleton.GetEditorViewport2D();
            if (vp != null) views.Add(vp.GlobalCanvasTransform.AffineInverse() * new Rect2(Vector2.Zero, vp.Size));
            return views;
        }
#endif
        var size = GetViewport()?.GetVisibleRect().Size ?? new Vector2(1280, 720);
        var cam = GetViewport()?.GetCamera2D();
        if (cam != null) size /= cam.Zoom;
        Vector2 centre = Sim.Player is Node2D p && IsInstanceValid(p) ? p.GlobalPosition : cam?.GetScreenCenterPosition() ?? WorldLayout.FortCentre;
        views.Add(new Rect2(centre - size / 2f, size));
        return views;
    }

    private void Refocus()
    {
        var want = new HashSet<Vector2I>();
        var keep = new HashSet<Vector2I>();
        foreach (var v in Views())
        {
            Collect(v.Grow(MarginPx), want);
            Collect(v.Grow(MarginPx + 2400f), keep);
        }
        // Nearest first, so what you are looking at fills in before the edges.
        var centre = Views().Count > 0 ? Views()[0].GetCenter() : Vector2.Zero;
        _queue.Clear();
        foreach (var ch in want) if (!_loaded.Contains(ch)) _queue.Add(ch);
        _queue.Sort((a, b) => ChunkRect(a).GetCenter().DistanceSquaredTo(centre).CompareTo(ChunkRect(b).GetCenter().DistanceSquaredTo(centre)));
        foreach (var ch in new List<Vector2I>(_loaded)) if (!keep.Contains(ch)) Unload(ch);
        int room = Mathf.Max(0, MaxChunks - _loaded.Count);
        if (_queue.Count > room) _queue.RemoveRange(room, _queue.Count - room);
    }

    private static void Collect(Rect2 r, HashSet<Vector2I> into)
    {
        int x0 = Mathf.FloorToInt(r.Position.X / (ChunkCols * IsoGrid.TileW)), x1 = Mathf.FloorToInt(r.End.X / (ChunkCols * IsoGrid.TileW));
        int y0 = Mathf.FloorToInt(r.Position.Y / (ChunkRows * IsoGrid.TileH / 2f)), y1 = Mathf.FloorToInt(r.End.Y / (ChunkRows * IsoGrid.TileH / 2f));
        int maxX = (WorldMap.Columns - 1) / ChunkCols, maxY = (WorldMap.Rows - 1) / ChunkRows;
        for (int x = Mathf.Max(0, x0); x <= Mathf.Min(maxX, x1); x++)
            for (int y = Mathf.Max(0, y0); y <= Mathf.Min(maxY, y1); y++)
                into.Add(new Vector2I(x, y));
    }

    private void LoadNext()
    {
        var ch = _queue[0];
        _queue.RemoveAt(0);
        if (_loaded.Contains(ch)) return;
        _loaded.Add(ch);
        foreach (var cell in CellsOf(ch)) SetCell(cell, SourceFor(cell), Vector2I.Zero);
        Structures?.PaintForest(CellsOf(ch));
        BuildWaterCollision(ch);
    }

    private void Unload(Vector2I ch)
    {
        _loaded.Remove(ch);
        foreach (var cell in CellsOf(ch)) EraseCell(cell);
        Structures?.ClearForest(CellsOf(ch));
        if (_chunkBodies.Remove(ch, out var body) && IsInstanceValid(body)) body.QueueFree();
    }

    private void UnloadAll()
    {
        foreach (var ch in new List<Vector2I>(_loaded)) Unload(ch);
        _queue.Clear();
    }

    public static IEnumerable<Vector2I> CellsOf(Vector2I ch)
    {
        for (int y = ch.Y * ChunkRows; y < (ch.Y + 1) * ChunkRows; y++)
            for (int x = ch.X * ChunkCols; x < (ch.X + 1) * ChunkCols; x++)
            {
                var c = new Vector2I(x, y);
                if (WorldMap.InBounds(c)) yield return c;
            }
    }

    public bool IsLoaded(Vector2I cell) => _loaded.Contains(ChunkOf(cell));

    // ---- what goes where -------------------------------------------------

    // The open-water season's tile for a cell.
    public static int SummerSource(Vector2I cell)
    {
        if (Wharf.Contains(cell)) return SourceWharf;
        var g = WorldMap.At(cell);
        switch (g)
        {
            case Ground.Ocean: return IsIceCell(cell) ? SourceWaterIce : SourceWater;
            case Ground.Lake or Ground.River: return SourceWater;
            case Ground.Ford: return SourceSand;
        }
        if (WorldLayout.IsBeach(cell) || WorldLayout.IsRiverBank(cell)) return SourceSand;
        if (Yards.Contains(cell)) return SourceDirt;
        var p = IsoGrid.ToWorld(cell);
        // The hand-built country round the posts: camps, the road, the trail.
        if (p.DistanceTo(WorldLayout.FortCentre) < 40000f)
        {
            if (p.DistanceTo(IsoGrid.ToWorld(WorldLayout.WinterCamp)) < 300f || p.DistanceTo(IsoGrid.ToWorld(WorldLayout.SummerCamp)) < 300f
                || p.DistanceTo(IsoGrid.ToWorld(WorldLayout.AssiniboineWinterCamp)) < 300f || p.DistanceTo(IsoGrid.ToWorld(WorldLayout.AssiniboineSummerCamp)) < 300f) return SourceDirt;
            if (WorldLayout.RoadDistance(p) < 60f) return SourceRoad;
            if (WorldLayout.TrailDistance(p) < 70f) return SourceDirt;
        }
        if (g == Ground.Cordillera) return IsoGrid.Hash(cell, 7) < 0.7f ? SourceHill : SourceGrass;
        if (g == Ground.Shield) return IsoGrid.Hash(cell, 7) < 0.12f ? SourceHill : SourceGrass;
        return SourceGrass;
    }

    // Winter buries the land in snow and freezes the water solid.
    private static int Winter(int src) => src switch
    {
        SourceGrass or SourceSand or SourceDirt => SourceSnow,
        SourceWater or SourceWaterIce => SourceIce,
        _ => src,
    };

    public int SourceFor(Vector2I cell) => _frozen ? Winter(SummerSource(cell)) : SummerSource(cell);

    // Frozen water has no collision, so the sea route closes but the ice
    // becomes a road. Every painted chunk is repainted for the season.
    private void ApplySeason(bool frozen)
    {
        if (frozen == _frozen) return;
        _frozen = frozen;
        foreach (var ch in _loaded)
        {
            foreach (var cell in CellsOf(ch)) SetCell(cell, SourceFor(cell), Vector2I.Zero);
            BuildWaterCollision(ch);
        }
    }

    public bool IsFrozen => _frozen;

    private StructurePainter Structures =>
        StructurePainterPath == null || StructurePainterPath.IsEmpty
            ? null
            : GetNodeOrNull<StructurePainter>(StructurePainterPath);

    // Sparse drift ice, scattered by a hash of the cell so the bay doesn't
    // read as one repeating tile but stays identical between runs.
    private static bool IsIceCell(Vector2I cell)
    {
        int h = cell.X * 73856093 ^ cell.Y * 19349663;
        return (h & 0x7fffffff) % 11 == 0;
    }

    // How the ground underfoot takes a walker (design doc 12, 17.3): snow
    // drags unless you have snowshoes, ice gives no grip, a beaten trail is
    // quick, the marsh is heavy going till it freezes.
    public float SpeedFactorAt(Vector2 worldPos, bool snowshoes, out bool slippery)
    {
        slippery = false;
        var cell = IsoGrid.FromWorld(worldPos);
        int src = SourceFor(cell);
        // The frozen river is the winter road: fast, and slippery (design doc 14.1).
        if (src == SourceIce || src == SourceWaterIce) { slippery = true; return 1.25f; }
        if (src == SourceSnow) return snowshoes ? 1.0f : 0.7f;
        if (src == SourceDirt) return 1.05f;
        if (src == SourceRoad) return 1.1f;
        if (src == SourceSand) return 0.9f;
        if (src == SourceHill) return 0.8f;
        // The goose marsh is a swamp in summer.
        foreach (var s in WorldLayout.Sites)
            if (s.Species == Ecology.Species.Goose && worldPos.DistanceTo(IsoGrid.ToWorld(s.Cell)) < s.RadiusPx * 0.8f && !_frozen) return 0.6f;
        return 1f;
    }

    // Open water: what blocks a walker (ice doesn't).
    public bool IsWater(Vector2I cell) => !_frozen && !Wharf.Contains(cell) && WorldMap.IsWater(cell);

    // Water or ice: what you can drink from (or melt).
    public bool IsWaterOrIce(Vector2I cell) => !Wharf.Contains(cell) && WorldMap.IsWater(cell);

    // Any water (or ice) within reach of the position - about a cell and a
    // half, so standing on the bank counts.
    public bool IsWaterNear(Vector2 localPos, float reachPx = 120f)
    {
        var c = LocalToMap(localPos);
        if (IsWaterOrIce(c)) return true;
        int rings = Mathf.CeilToInt(reachPx / 64f);
        for (int dx = -rings; dx <= rings; dx++)
        {
            for (int dy = -2 * rings; dy <= 2 * rings; dy++)
            {
                var n = new Vector2I(c.X + dx, c.Y + dy);
                if (!IsWaterOrIce(n)) continue;
                if (IsoGrid.ToWorld(n).DistanceTo(localPos) <= reachPx + 40f) return true;
            }
        }
        return false;
    }

    private void PlacePlayer(Node2D player)
    {
        if (player == null || !Net.Net.IsServer) return;
        if (SpawnOverride.X >= 0 && SpawnOverride.Y >= 0) player.Position = MapToLocal(SpawnOverride);
        // Dev aid: `godot -- --spawn=21,32` or `--spawn=depot` drops the host's player on a cell.
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
        _focusTimer = 0f;
    }

    public static Vector2I NamedSpot(string name)
    {
        // Any post or place on the map by name: `--spawn="Fort William"`.
        if (WorldMap.Place(name) is { } place) return Sim.Paths?.NearestWalkable(place.Cell, 12) ?? place.Cell;
        return name switch
        {
            "depot" => WorldLayout.Depot.Centre,
            "warehouse" => WorldLayout.Warehouse.Centre,
            "menshouse" => WorldLayout.MensHouse.Centre,
            "wharf" => WorldLayout.WharfRoot,
            "nwc" => WorldLayout.NwcHouse.Centre,
            "outpost" => WorldLayout.OutpostHouse.Centre,
            "assiniboine" => IsoGrid.Cell(WorldLayout.AssiniboineWinterCamp, 1, 1),
            "road" => WorldLayout.RoadHeadYork,
            "ford" => FirstCrossing(),
            "berth" => IsoGrid.Cell(WorldLayout.WharfEnd, -2, 0),
            "camp" => IsoGrid.Cell(WorldLayout.CampCentre, 1, 1),
            "caribou" => WorldLayout.SiteOf(Ecology.Species.Caribou).Cell,
            "geese" => WorldLayout.SiteOf(Ecology.Species.Goose).Cell,
            "beaver" => WorldLayout.SiteOf(Ecology.Species.Beaver).Cell,
            "wolves" => IsoGrid.Cell(WorldLayout.SiteOf(Ecology.Species.Wolf).Cell, 4, 0),
            _ => WorldLayout.GateOutside,
        };
    }

    private static Vector2I FirstCrossing()
    {
        var a = WorldMap.LayoutArray("crossings");
        if (a.Count == 0) return WorldLayout.RoadHeadYork;
        var c = a[0].AsGodotArray();
        return new Vector2I(c[0].AsInt32(), c[1].AsInt32());
    }

    // Only water with dry ground beside it needs a wall: you can't reach the
    // middle of the Bay without crossing its edge first.
    private static readonly ConvexPolygonShape2D WaterDiamond = new()
    {
        Points = new[] { new Vector2(0, -32), new Vector2(64, 0), new Vector2(0, 32), new Vector2(-64, 0) },
    };

    private void BuildWaterCollision(Vector2I ch)
    {
        if (Engine.IsEditorHint()) return;
        var host = WaterCollisionPath == null || WaterCollisionPath.IsEmpty ? null : GetNodeOrNull<StaticBody2D>(WaterCollisionPath);
        if (host == null) return;
        if (_chunkBodies.Remove(ch, out var old) && IsInstanceValid(old)) old.QueueFree();
        if (_frozen) return;
        var body = new StaticBody2D { CollisionLayer = host.CollisionLayer, CollisionMask = host.CollisionMask, Name = $"Water_{ch.X}_{ch.Y}" };
        foreach (var cell in CellsOf(ch))
        {
            if (!IsWater(cell)) continue;
            bool shore = false;
            foreach (var n in IsoGrid.EdgeNeighbours(cell)) if (!IsWater(n)) { shore = true; break; }
            if (shore) body.AddChild(new CollisionShape2D { Shape = WaterDiamond, Position = MapToLocal(cell) });
        }
        host.AddChild(body);
        _chunkBodies[ch] = body;
    }
}
