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
    public const int SourceRoof = 59;
    public const int SourceBed = 60;
    private const float RoofLift = 118f;

    // Source id ranges whose tiles sit on a cell edge, laid out N,E,S,W.
    private static readonly int[] EdgeFamilyBases = { 6, 10, 14, 18, 22, 39 };
    private static readonly HashSet<int> OpenDoors = new() { 39, 40, 41, 42 };
    private static readonly HashSet<int> SolidProps = new() { 27, 28, 29, 30, 32, 35, 36, 49, 50, 52, 53, 54, 56, 57, 58 };
    private static readonly HashSet<int> Hearths = new() { 49, 50, 56 };

    [Export] public bool AutoGenerate = true;
    // Streamed and generated tiles are never written into the scene file.
    public override void _ValidateProperty(Godot.Collections.Dictionary property) { if (AutoGenerate) GeneratedTileLayer.DontStore(property); }
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

    // The hearths bucketed by where they stand. Every body asks how warm it
    // is every frame, and asking every fire in the country (every post's and
    // every camp's) was the largest single cost in a frame; a fire warms only
    // within its OuterRadius, so only the buckets round you are looked at.
    // Fires don't move: a hearth is put in when it is made and taken out
    // when it goes.
    private const float HearthBucket = 512f;
    private readonly Dictionary<long, List<Hearth>> _hearthGrid = new();
    private static long HearthKey(int bx, int by) => ((long)bx << 32) ^ (uint)by;
    private static long HearthKeyOf(Vector2 p) => HearthKey(Mathf.FloorToInt(p.X / HearthBucket), Mathf.FloorToInt(p.Y / HearthBucket));

    private void AddHearth(Hearth hearth, Vector2I cell)
    {
        AddChild(hearth);
        hearth.WorldPos = hearth.GlobalPosition;
        _hearths.Add(hearth);
        _hearthByCell[cell] = hearth;
        long k = HearthKeyOf(hearth.WorldPos);
        if (!_hearthGrid.TryGetValue(k, out var list)) _hearthGrid[k] = list = new List<Hearth>(4);
        list.Add(hearth);
    }

    private void RemoveHearth(Hearth hearth, Vector2I cell)
    {
        _hearths.Remove(hearth);
        _hearthByCell.Remove(cell);
        if (_hearthGrid.TryGetValue(HearthKeyOf(hearth.WorldPos), out var list)) list.Remove(hearth);
        hearth.QueueFree();
    }

    // One roof layer per building, drawn above everything; it fades away
    // while the player is under it (design doc 18.1: a roof is a property
    // of the cells, and this is what it looks like from outside).
    private readonly List<(BuildingDef building, TileMapLayer roof)> _roofs = new();
    // Beside each roof (same index): the cells it covers, and how opaque it is
    // now. Every frame each roof is asked whether the player is under it; the
    // set answers that at once (the building's own Contains walks the cells),
    // and the alpha kept here means the engine is only told when it changes.
    private readonly List<HashSet<Vector2I>> _roofCells = new();
    private readonly List<float> _roofAlpha = new();

    private void AddRoof(BuildingDef b, TileMapLayer roof)
    {
        var cells = new HashSet<Vector2I>();
        foreach (var cell in IsoGrid.Rhombus(b.Top, b.Width, b.Depth)) { roof.SetCell(cell, SourceRoof, Vector2I.Zero); cells.Add(cell); }
        _roofs.Add((b, roof));
        _roofCells.Add(cells);
        _roofAlpha.Add(roof.Modulate.A);
    }

    // Kept for older callers; the layout is the source of truth.
    public Vector2I GateCell => WorldLayout.GateCell;
    public Vector2I DoorCell => WorldLayout.Depot.DoorCell;

    public override void _Ready()
    {
        _front = GetNodeOrNull<TileMapLayer>(FrontLayerPath);
        _floor = GetNodeOrNull<TileMapLayer>(FloorLayerPath);
        _body = GetNodeOrNull<StaticBody2D>(CollisionBodyPath);
        _player = GetNodeOrNull<Node2D>(PlayerPath);
        Sim.LocalPlayerChanged += p => _player = p;

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
            CallDeferred(nameof(BuildRoofs));
            if (Sim.Player is Node2D lp) _player = lp;
        }
    }

    private const int MaxLights = 24;
    private float _lightTimer;
    private readonly List<Hearth> _lit = new();

    public override void _Process(double delta)
    {
        if (Engine.IsEditorHint()) return;
        var vp = GetViewport();
        if (vp != null) Hearth.View = (vp.GetCanvasTransform().AffineInverse() * vp.GetVisibleRect()).Grow(Hearth.MaxReach);
        // The fires that may throw light: the nearest few to the middle of the view.
        _lightTimer -= (float)delta;
        if (_lightTimer <= 0f)
        {
            _lightTimer = 0.5f;
            var mid = Hearth.View.GetCenter();
            _lit.Clear();
            foreach (var h in _hearths) { h.LightAllowed = false; if (Hearth.IsInView(h.WorldPos)) _lit.Add(h); }
            _lit.Sort((a, b) => a.WorldPos.DistanceSquaredTo(mid).CompareTo(b.WorldPos.DistanceSquaredTo(mid)));
            for (int i = 0; i < _lit.Count && i < MaxLights; i++) _lit[i].LightAllowed = true;
        }
        for (int i = 0; i < _hearths.Count; i++) _hearths[i].Tick(delta);
        if (_front == null || _player == null) return;
        float target = IsSheltered(_player.Position) ? IndoorFrontAlpha : 1f;
        var m = _front.Modulate;
        m.A = Mathf.MoveToward(m.A, target, (float)delta * 4f);
        _front.Modulate = m;

        var playerCell = LocalToMap(_player.Position);
        var dl = IsoGrid.DownLeft(playerCell); var dr = IsoGrid.DownRight(playerCell);
        for (int i = 0; i < _roofs.Count; i++)
        {
            var cells = _roofCells[i];
            bool under = cells.Contains(playerCell) || cells.Contains(dl) || cells.Contains(dr);
            float was = _roofAlpha[i];
            float now = Mathf.MoveToward(was, under ? 0f : 1f, (float)delta * 4f);
            if (now == was) continue;
            _roofAlpha[i] = now;
            var roof = _roofs[i].roof;
            var rm = roof.Modulate;
            rm.A = now;
            roof.Modulate = rm;
        }
    }

    private void BuildRoofs()
    {
        // Sits beside YSort on the scene root so it draws over people and walls.
        var host = GetParent()?.GetParent();
        if (host == null) return;
        foreach (var b in WorldLayout.Buildings)
        {
            // Lifted by the wall height so it sits on top of the walls.
            var roof = new TileMapLayer { TileSet = TileSet, ZIndex = 5, ZAsRelative = false, Name = $"Roof_{b.Kind}", Position = new Vector2(0, -RoofLift) };
            // Two extra courses toward the viewer so the lifted roof still
            // covers the front of the room from above.
            AddRoof(b, roof);
            host.AddChild(roof);
        }
    }

    // "Indoors" is a property of the tile you stand on (design doc 18.1): a
    // roofed floor, or a tipi's footprint.
    public bool IsSheltered(Vector2 localPos) => IsShelteredCell(LocalToMap(localPos));

    // A plank floor underfoot (Core/Audio.cs picks the footstep by it).
    public bool IsPlankFloor(Vector2 localPos) => (_floor ?? this).GetCellSourceId(LocalToMap(localPos)) == SourceFloorPlanks;

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
        _hearthGrid.Clear();
        foreach (var cell in GetUsedCells())
        {
            if (!Hearths.Contains(GetCellSourceId(cell))) continue;
            var hearth = new Hearth { Position = MapToLocal(cell), Name = $"Hearth_{cell.X}_{cell.Y}", Cell = cell };
            if (GetCellSourceId(cell) == SourceCampfire) hearth.FuelHours = 4f;
            AddHearth(hearth, cell);
        }
    }

    public IReadOnlyList<Hearth> AllHearths => _hearths;

    public float HeatAt(Vector2 worldPos) => HeatAt(worldPos, out _);

    public float HeatAt(Vector2 worldPos, out bool enclosed)
    {
        float best = 0f; enclosed = false;
        int x0 = Mathf.FloorToInt((worldPos.X - Hearth.MaxReach) / HearthBucket), x1 = Mathf.FloorToInt((worldPos.X + Hearth.MaxReach) / HearthBucket);
        int y0 = Mathf.FloorToInt((worldPos.Y - Hearth.MaxReach) / HearthBucket), y1 = Mathf.FloorToInt((worldPos.Y + Hearth.MaxReach) / HearthBucket);
        for (int bx = x0; bx <= x1; bx++)
            for (int by = y0; by <= y1; by++)
            {
                if (!_hearthGrid.TryGetValue(HearthKey(bx, by), out var list)) continue;
                for (int i = 0; i < list.Count; i++)
                {
                    var h = list[i];
                    float v = h.HeatAt(worldPos);
                    if (v > best) { best = v; enclosed = h.Enclosed; }
                }
            }
        return best;
    }

    public Hearth HearthAt(Vector2I cell) => _hearthByCell.TryGetValue(cell, out var h) ? h : null;

    public Hearth HearthNear(Vector2 worldPos, float reach = 140f)
    {
        Hearth best = null;
        int x0 = Mathf.FloorToInt((worldPos.X - reach) / HearthBucket), x1 = Mathf.FloorToInt((worldPos.X + reach) / HearthBucket);
        int y0 = Mathf.FloorToInt((worldPos.Y - reach) / HearthBucket), y1 = Mathf.FloorToInt((worldPos.Y + reach) / HearthBucket);
        for (int bx = x0; bx <= x1; bx++)
            for (int by = y0; by <= y1; by++)
            {
                if (!_hearthGrid.TryGetValue(HearthKey(bx, by), out var list)) continue;
                foreach (var h in list)
                {
                    float d = h.WorldPos.DistanceTo(worldPos);
                    if (d < reach) { reach = d; best = h; }
                }
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
        PaintNwcPalisade();
        PaintPalisadeAt(WorldLayout.OutpostTop, WorldLayout.OutpostWidth, WorldLayout.OutpostDepth, WorldLayout.OutpostGateOffset);
        SetBack(WorldLayout.OutpostWoodpileCell, SourceWoodpile);
        SetBack(IsoGrid.Cell(WorldLayout.OutpostTop, 10, 3), SourceCrate);
        SetBack(IsoGrid.Cell(WorldLayout.OutpostTop, 1, 7), SourceFurBale);
        foreach (var b in WorldLayout.Buildings) PaintBuilding(b, floor);
        PaintYard();
        PaintCamp();
        PlaceSign(WorldLayout.GateOutside, "York Factory", "Hudson's Bay Company");
        PlaceSign(IsoGrid.DownLeft(IsoGrid.DownLeft(WorldLayout.NwcGateCell)), "Nor'Westers' House", "North West Company");
        PlaceSign(WorldLayout.OutpostGateOutside, "Oxford House", "Hudson's Bay Company");
        // Each great carrying place: a board, a canoe drawn up and a cache at the landing.
        foreach (var lm in WorldMap.Landmarks())
        {
            if (lm.Kind != "portage") continue;
            var at = IsoGrid.FromWorld(lm.At);
            Vector2I? land = null;
            for (int r = 0; r <= 8 && land == null; r++)
                for (int dx = -r; dx <= r && land == null; dx++)
                    for (int dy = -2 * r; dy <= 2 * r && land == null; dy++)
                    {
                        var n = new Vector2I(at.X + dx, at.Y + dy);
                        if (!WorldMap.IsWater(n) && WorldMap.InBounds(n)) land = n;
                    }
            if (land == null) continue;
            PlaceSign(land.Value, lm.Name, "Portage");
            var canoe = IsoGrid.Cell(land.Value, 2, 1); var cache = IsoGrid.Cell(land.Value, -1, 2);
            if (!WorldMap.IsWater(canoe) && GetCellSourceId(canoe) == -1) SetBack(canoe, WorldLayout.CanoeBeached);
            if (!WorldMap.IsWater(cache) && GetCellSourceId(cache) == -1) SetBack(cache, WorldLayout.Cache);
        }
        // A beaver lodge in the water by each beaver ground, so you can see it.
        foreach (var site in WorldLayout.Sites)
        {
            if (site.Species != Ecology.Species.Beaver) continue;
            var lodge = WaterBeside(site.Cell);
            if (lodge.HasValue && GetCellSourceId(lodge.Value) == -1) SetBack(lodge.Value, WorldLayout.BeaverLodge);
        }
        // The camps of the wider country: a band's ring of tipis, or one
        // family's lodge and fire outside a post.
        foreach (var c in Camps.All)
        {
            if (!c.IsFamily) { PaintCampAt(c.Centre, Economy.Gardens.IsTownNamed(c.Name)); continue; }
            SetBack(c.Centre, SourceCampfire);
            foreach (var t in c.Tipis()) SetBack(t, SourceTipi);
        }
        // In the editor the posts standing at the usual start are shown; in a
        // game Posts paints each one as it comes to stand.
        if (Engine.IsEditorHint()) foreach (var p in Posts.All) if (p.Year <= 1811 && (p.Closed == 0 || p.Closed > 1811)) PaintPost(p, false);
        // The forest is painted chunk by chunk as TerrainPainter streams the country in.
    }

    // One of the wider country's posts: palisade, house, woodpile. In a game
    // it gets its hearth and roof too; in the editor just the tiles.
    public void PaintPost(TradingPost p, bool live)
    {
        PaintPalisadeAt(p.Top, p.Width, p.Depth, p.GateOffset);
        PaintBuilding(p.House, _floor ?? this);
        foreach (var b in p.Extra) PaintBuilding(b, _floor ?? this);
        SetBack(p.WoodpileCell, SourceWoodpile);
        SetBack(p.CrateCell, SourceCrate);
        foreach (var (kind, cell) in p.Props)
        {
            int src = kind switch { "flagstaff" => 93, "crate" => SourceCrate, "fur_bale" => SourceFurBale, "drying_rack" => 90, "canoe" => WorldLayout.CanoeBeached, _ => -1 };
            if (src >= 0 && GetCellSourceId(cell) == -1) SetBack(cell, src);
        }
        PlaceSign(p.GateOutside, p.Name, p.Company switch { "NWC" => "North West Company", "AMER" => Posts.CompanyName(p).Replace("the ", ""), _ => "Hudson's Bay Company" });
        if (!live) return;
        EnsureHearth(p.HearthCell, 6f);
        foreach (var b in p.Extra) EnsureHearth(IsoGrid.Cell(b.Top, 1, 1), 6f);
        var host = GetParent()?.GetParent();
        if (host != null && !Engine.IsEditorHint())
        {
            var roofed = new List<BuildingDef> { p.House };
            roofed.AddRange(p.Extra);
            foreach (var b in roofed)
            {
                var roof = new TileMapLayer { TileSet = TileSet, ZIndex = 5, ZAsRelative = false, Name = $"Roof_{(p.Name + "_" + b.Name).Replace(' ', '_').Replace('.', '_').Replace('\'', '_')}", Position = new Vector2(0, -RoofLift) };
                AddRoof(b, roof);
                host.AddChild(roof);
            }
        }
        RebuildCollision();
    }

    // A town (World/Towns): its garrison's palisade, every building with its
    // roof, a fire laid in every hearth, and its name at the edge. Collision
    // is rebuilt once for all of them (RebuildCollisionNow), not per house.
    public void PaintTown(Town t)
    {
        var floor = _floor ?? this;
        if (t.HasFort) PaintPalisadeAt(t.FortTop, t.FortWidth, t.FortDepth, t.FortGate);
        foreach (var b in t.Buildings) PaintBuilding(b.Def, floor);
        // Kitchen gardens, the farms' fields (stubble from the harvest to the
        // ploughing), and the woodpiles.
        bool fallow = (GameCalendar.Instance?.Month ?? 8) is >= 10 or <= 4;
        foreach (var (kind, cell) in t.Props)
        {
            if (GetCellSourceId(cell) != -1) continue;
            SetBack(cell, kind == "woodpile" ? SourceWoodpile : fallow ? WorldLayout.GardenHarvested : WorldLayout.GardenPlot);
        }
        // The town's name at its market house, its garrison's at the gate.
        var heart = t.Buildings.Find(b => b.Home == "market")?.Def ?? t.Buildings[0].Def;
        PlaceSign(IsoGrid.DownLeft(IsoGrid.DownLeft(heart.DoorCell)), t.Name, t.Polity == "AMER" ? (t.Name == "Detroit" ? "Michigan Territory" : "New York") : "Upper Canada");
        if (t.HasFort) PlaceSign(IsoGrid.DownLeft(IsoGrid.DownLeft(IsoGrid.Cell(t.FortTop, t.FortGate, t.FortDepth - 1))), t.FortName, t.RulerName == "the Crown" ? "His Majesty's garrison" : "United States garrison");
        if (Engine.IsEditorHint()) return;
        var host = GetParent()?.GetParent();
        foreach (var b in t.Buildings)
        {
            EnsureHearth(IsoGrid.Cell(b.Def.Top, 1, 1), 6f);
            if (host == null) continue;
            // Shingles of every age: new and red-brown, weathered silver-grey, old and dark.
            float h = IsoGrid.Hash(b.Def.Top, 53), v = 0.78f + IsoGrid.Hash(b.Def.Top, 59) * 0.3f;
            var weather = h < 0.4f ? new Color(v, v * 0.97f, v * 0.94f) : h < 0.7f ? new Color(v * 0.86f, v * 0.88f, v * 0.9f) : h < 0.88f ? new Color(v * 0.7f, v * 0.66f, v * 0.62f) : new Color(v, v * 0.82f, v * 0.74f);
            var roof = new TileMapLayer { TileSet = TileSet, ZIndex = 5, ZAsRelative = false, Name = $"Roof_{(t.Name + "_" + b.Def.Name).Replace(' ', '_').Replace('.', '_').Replace('\'', '_').Replace(',', '_')}", Position = new Vector2(0, -RoofLift), Modulate = weather };
            AddRoof(b.Def, roof);
            host.AddChild(roof);
        }
    }

    public void RebuildCollisionNow() => RebuildCollision();

    // The nearest open water to a cell (a beaver lodge stands in it).
    private static Vector2I? WaterBeside(Vector2I c)
    {
        for (int r = 0; r <= 4; r++)
            for (int dx = -r; dx <= r; dx++)
                for (int dy = -2 * r; dy <= 2 * r; dy++)
                {
                    var n = new Vector2I(c.X + dx, c.Y + dy);
                    if (WorldMap.At(n) is Ground.River or Ground.Lake) return n;
                }
        return null;
    }

    // A name board outside a gate (never saved with the scene: no owner).
    private readonly HashSet<string> _signs = new();
    public void PlaceSign(Vector2I cell, string text, string sub = "")
    {
        var host = GetParent() ?? this;
        string name = "Sign_" + text.Replace(' ', '_').Replace('.', '_').Replace("'", "_");
        if (!_signs.Add(name)) return;
        host.CallDeferred(Node.MethodName.AddChild, new PostSign { Name = name, Text = text, Sub = sub, Position = MapToLocal(cell) + new Vector2(70, 10) });
    }

    // Around an editor save: the scene file keeps none of the painted tiles.
    public void ClearAll()
    {
        Clear(); _front?.Clear(); _floor?.Clear();
    }
    public void Repaint() { if (AutoGenerate) Paint(); }

    private void SetBack(Vector2I cell, int source) => SetCell(cell, source, Vector2I.Zero);
    private void SetFront(Vector2I cell, int source) => (_front ?? this).SetCell(cell, source, Vector2I.Zero);

    // York Factory's palisade: the gate is in the river side (south-east),
    // where the boardwalk goes out to the landings.
    private void PaintPalisade()
    {
        var top = WorldLayout.FortTop;
        int w = WorldLayout.FortWidth, d = WorldLayout.FortDepth;
        var gate = WorldLayout.GateCell;
        var gateNext = IsoGrid.Cell(top, w - 1, WorldLayout.GateOffset + 1);

        for (int i = 0; i < w; i++)
        {
            SetBack(IsoGrid.Cell(top, i, 0), SourceFenceS);
            SetFront(IsoGrid.Cell(top, i, d - 1), SourceFenceN);
        }
        for (int j = 0; j < d; j++)
        {
            SetBack(IsoGrid.Cell(top, 0, j), SourceFenceE);
            var river = IsoGrid.Cell(top, w - 1, j);
            if (river != gate && river != gateNext) SetFront(river, SourceFenceW);
        }
        // Top and bottom corners need two walls that both belong on the same
        // layer, so they take a composite corner tile.
        SetBack(top, SourceFenceCornerBack);
        SetFront(IsoGrid.Cell(top, w - 1, d - 1), SourceFenceCornerFront);
    }

    // Any square palisade with a two-cell gate in the front wall.
    private void PaintPalisadeAt(Vector2I top, int w, int d, int gateOffset)
    {
        var gate = IsoGrid.Cell(top, gateOffset, d - 1);
        var gateNext = IsoGrid.Cell(top, gateOffset + 1, d - 1);
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
        SetBack(top, SourceFenceCornerBack);
        SetFront(IsoGrid.Cell(top, w - 1, d - 1), SourceFenceCornerFront);
    }

    private void PaintNwcPalisade()
    {
        var top = WorldLayout.NwcTop;
        int w = WorldLayout.NwcWidth, d = WorldLayout.NwcDepth;
        var gate = WorldLayout.NwcGateCell;
        var gateNext = IsoGrid.Cell(top, WorldLayout.NwcGateOffset + 1, d - 1);
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
        SetBack(top, SourceFenceCornerBack);
        SetFront(IsoGrid.Cell(top, w - 1, d - 1), SourceFenceCornerFront);
        SetBack(WorldLayout.NwcWoodpileCell, SourceWoodpile);
        SetBack(IsoGrid.Cell(top, 9, 3), SourceCrate);
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
                // The Factor's and clerks' beds, and one for the new hand.
                SetBack(IsoGrid.Cell(top, 3, 1), SourceBed);
                SetBack(IsoGrid.Cell(top, 4, 1), SourceBed);
                SetBack(IsoGrid.Cell(top, 2, 3), SourceBed);
                SetBack(IsoGrid.Cell(top, 4, 3), SourceBed);
                break;
            case BuildingKind.Townhouse:
                // A family's house: the fire, the bed, the table, and the way
                // in from the door kept clear.
                SetBack(IsoGrid.Cell(top, 1, 1), SourceHearthLeft);
                SetBack(IsoGrid.Cell(top, 2, 1), SourceBed);
                SetBack(IsoGrid.Cell(top, 2, 2), SourceTable);
                break;
            case BuildingKind.Cabin:
                // One room of squared logs: the fire, and the floor for beds.
                SetBack(IsoGrid.Cell(top, 1, 1), SourceHearthLeft);
                break;
            case BuildingKind.House:
                // A frame house of the better sort: the parlour fire, a bed, the table and the books.
                SetBack(IsoGrid.Cell(top, 1, 1), SourceHearthLeft);
                SetBack(IsoGrid.Cell(top, 3, 1), SourceBed);
                SetBack(IsoGrid.Cell(top, 3, 2), SourceTableChairs);
                SetBack(IsoGrid.Cell(top, 1, 2), SourceShelfTall);
                SetBack(IsoGrid.Cell(top, 2, 1), SourceRug);
                break;
            case BuildingKind.Church:
                // Pews either side of the aisle, candles at the altar end, a stove.
                SetBack(IsoGrid.Cell(top, 1, 1), SourceHearthLeft);
                foreach (int i in new[] { 1, 2, 4, 5, 6 }) SetBack(IsoGrid.Cell(top, i, 2), SourceTableChairs);
                SetBack(IsoGrid.Cell(top, 3, 1), SourceCandleStand);
                SetBack(IsoGrid.Cell(top, 6, 1), SourceCandleStand);
                SetBack(IsoGrid.Cell(top, 4, 1), SourceRug);
                break;
            case BuildingKind.Barracks:
                // Two rows of cots, the arms rack, the mess table.
                SetBack(IsoGrid.Cell(top, 1, 1), SourceHearthLeft);
                for (int i = 2; i <= 6; i++) SetBack(IsoGrid.Cell(top, i, 1), SourceBed);
                SetBack(IsoGrid.Cell(top, 5, 3), SourceBed);
                SetBack(IsoGrid.Cell(top, 6, 3), SourceBed);
                SetBack(IsoGrid.Cell(top, 1, 3), SourceShelfTall);
                SetBack(IsoGrid.Cell(top, 1, 2), SourceTable);
                break;
            case BuildingKind.Store:
                // A merchant's store: counter, shelves of goods, bales and casks.
                SetBack(IsoGrid.Cell(top, 1, 1), SourceHearthLeft);
                SetBack(IsoGrid.Cell(top, 2, 1), SourceShelfTall);
                SetBack(IsoGrid.Cell(top, 3, 1), SourceShelf);
                SetBack(IsoGrid.Cell(top, 4, 1), SourceShelfTall);
                SetBack(IsoGrid.Cell(top, 4, 2), SourceDisplayCase);
                SetBack(IsoGrid.Cell(top, 6, 1), SourceCrate);
                SetBack(IsoGrid.Cell(top, 6, 2), SourceFurBale);
                SetBack(IsoGrid.Cell(top, 5, 1), SourceCrate);
                break;
            case BuildingKind.Workshop:
                // A tradesman's shop: the bench, his stock, a forge or a stove.
                SetBack(IsoGrid.Cell(top, 1, 1), SourceHearthLeft);
                SetBack(IsoGrid.Cell(top, 2, 1), SourceTable);
                SetBack(IsoGrid.Cell(top, 2, 2), SourceCrate);
                break;
            case BuildingKind.Hall:
                // A hall of government or council: the long table and the benches.
                SetBack(IsoGrid.Cell(top, 1, 1), SourceHearthLeft);
                foreach (int i in new[] { 2, 4, 5, 6 }) SetBack(IsoGrid.Cell(top, i, 2), SourceTableChairs);
                SetBack(IsoGrid.Cell(top, 6, 1), SourceCandleStand);
                SetBack(IsoGrid.Cell(top, 4, 1), SourceRug);
                break;
            case BuildingKind.Warehouse:
                // Bales of furs one side, cargo the other, an aisle between.
                for (int j = 1; j <= 4; j++) SetBack(IsoGrid.Cell(top, 1, j), SourceFurBale);
                for (int j = 1; j <= 4; j++) SetBack(IsoGrid.Cell(top, 2, j), j % 2 == 1 ? SourceFurBale : SourceCrate);
                for (int j = 1; j <= 4; j++) SetBack(IsoGrid.Cell(top, 6, j), SourceCrate);
                SetBack(IsoGrid.Cell(top, 5, 1), SourceCrate);
                SetBack(IsoGrid.Cell(top, 5, 2), SourceHayBales);
                break;
            case BuildingKind.NwcHouse:
                // The wintering partner's house: hearth, counter, rum, bunks.
                SetBack(IsoGrid.Cell(top, 1, 1), SourceHearthLeft);
                SetBack(IsoGrid.Cell(top, 1, 2), SourceShelf);
                SetBack(IsoGrid.Cell(top, 2, 1), SourceShelfTall);
                SetBack(IsoGrid.Cell(top, 5, 1), SourceCrate);
                SetBack(IsoGrid.Cell(top, 4, 2), SourceTable);
                SetBack(IsoGrid.Cell(top, 5, 3), SourceCandleStand);
                for (int i = 2; i <= 5; i++) SetBack(IsoGrid.Cell(top, i, 3), SourceBed);
                SetBack(IsoGrid.Cell(top, 3, 1), SourceBed);
                break;
            case BuildingKind.OutpostHouse or BuildingKind.TradingPost:
                // One room does for everything inland: hearth, counter, bunks, the bales.
                SetBack(IsoGrid.Cell(top, 1, 1), SourceHearthLeft);
                SetBack(IsoGrid.Cell(top, 1, 2), SourceShelf);
                SetBack(IsoGrid.Cell(top, 2, 1), SourceShelfTall);
                SetBack(IsoGrid.Cell(top, 5, 1), SourceFurBale);
                SetBack(IsoGrid.Cell(top, 4, 2), SourceTable);
                SetBack(IsoGrid.Cell(top, 5, 3), SourceCandleStand);
                for (int i = 2; i <= 5; i++) SetBack(IsoGrid.Cell(top, i, 3), SourceBed);
                SetBack(IsoGrid.Cell(top, 3, 1), SourceBed);
                break;
            case BuildingKind.FactorsHouse:
                // The Chief Factor's house: his own hearth, bed, table and books.
                SetBack(IsoGrid.Cell(top, 1, 1), SourceHearthLeft);
                SetBack(IsoGrid.Cell(top, 2, 2), SourceBed);
                SetBack(IsoGrid.Cell(top, 3, 1), SourceShelfTall);
                SetBack(IsoGrid.Cell(top, 3, 3), SourceTableChairs);
                SetBack(IsoGrid.Cell(top, 1, 4), SourceCandleStand);
                SetBack(IsoGrid.Cell(top, 2, 4), SourceRug);
                break;
            case BuildingKind.ClerksHouse:
                // The clerks share a room: two beds, a desk, the ledgers.
                SetBack(IsoGrid.Cell(top, 1, 1), SourceHearthLeft);
                SetBack(IsoGrid.Cell(top, 1, 2), SourceBed);
                SetBack(IsoGrid.Cell(top, 3, 2), SourceBed);
                SetBack(IsoGrid.Cell(top, 3, 1), SourceShelf);
                SetBack(IsoGrid.Cell(top, 2, 4), SourceTable);
                SetBack(IsoGrid.Cell(top, 3, 4), SourceCandleStand);
                break;
            case BuildingKind.SummerMess:
                // Where the men eat in the open-water season.
                SetBack(IsoGrid.Cell(top, 1, 1), SourceTableChairs);
                SetBack(IsoGrid.Cell(top, 3, 1), SourceTableChairs);
                SetBack(IsoGrid.Cell(top, 1, 3), SourceTableChairs);
                SetBack(IsoGrid.Cell(top, 3, 3), SourceCrate);
                break;
            case BuildingKind.ProvisionStore:
                // Pemmican, flour, salt geese: the post's food under one roof.
                for (int i = 1; i <= 3; i++) SetBack(IsoGrid.Cell(top, i, 1), SourceCrate);
                SetBack(IsoGrid.Cell(top, 1, 2), SourceHayBales);
                SetBack(IsoGrid.Cell(top, 3, 2), SourceShelf);
                break;
            case BuildingKind.GuestHouse:
                // For the officers off the ship and the brigade's guides.
                SetBack(IsoGrid.Cell(top, 1, 1), SourceBed);
                SetBack(IsoGrid.Cell(top, 2, 1), SourceBed);
                SetBack(IsoGrid.Cell(top, 1, 3), SourceTable);
                SetBack(IsoGrid.Cell(top, 2, 3), SourceCandleStand);
                break;
            case BuildingKind.BoatShed:
                // York boats are built and mended here.
                SetBack(IsoGrid.Cell(top, 1, 1), SourceCrate);
                SetBack(IsoGrid.Cell(top, 3, 1), SourceHayBales);
                SetBack(IsoGrid.Cell(top, 1, 4), SourceCrate);
                SetBack(IsoGrid.Cell(top, 3, 4), SourceWoodpile);
                break;
            case BuildingKind.PowderMagazine:
                SetBack(IsoGrid.Cell(top, 1, 1), SourceCrate);
                break;
            case BuildingKind.GreatHall:
                // Where the partners dine and sit in council each July: a
                // long table down the room, the hearth at the end, candles.
                SetBack(IsoGrid.Cell(top, 1, 1), SourceHearthLeft);
                for (int i = 2; i <= 5; i++) SetBack(IsoGrid.Cell(top, i, 2), SourceTableChairs);
                SetBack(IsoGrid.Cell(top, 3, 1), SourceRug);
                SetBack(IsoGrid.Cell(top, 5, 1), SourceShelfTall);
                SetBack(IsoGrid.Cell(top, 1, 2), SourceCandleStand);
                SetBack(IsoGrid.Cell(top, 4, 1), SourceCandleStand);
                break;
            case BuildingKind.PartnersHouse:
                // The wintering partners' quarters while they're down for the rendezvous.
                SetBack(IsoGrid.Cell(top, 1, 1), SourceHearthLeft);
                SetBack(IsoGrid.Cell(top, 2, 1), SourceBed);
                SetBack(IsoGrid.Cell(top, 3, 1), SourceBed);
                break;
            case BuildingKind.MensHouse:
                // Where the labourers sleep: a big hearth, benches, a table.
                SetBack(IsoGrid.Cell(top, 1, 1), SourceHearthLeft);
                SetBack(IsoGrid.Cell(top, 3, 1), SourceTable);
                SetBack(IsoGrid.Cell(top, 4, 1), SourceChair);
                SetBack(IsoGrid.Cell(top, 5, 1), SourceCrate);
                SetBack(IsoGrid.Cell(top, 5, 3), SourceCandleStand);
                // Bunks: a row along the middle, more along the front.
                for (int i = 1; i <= 5; i++) SetBack(IsoGrid.Cell(top, i, 2), SourceBed);
                for (int i = 1; i <= 4; i++) SetBack(IsoGrid.Cell(top, i, 3), SourceBed);
                break;
        }
    }

    private void PaintYard()
    {
        SetBack(WorldLayout.WoodpileCell, SourceWoodpile);
        SetBack(IsoGrid.Cell(WorldLayout.FortTop, 19, 25), SourceCrate);
        SetBack(IsoGrid.Cell(WorldLayout.FortTop, 20, 26), SourceHayBales);
    }

    private void PaintCamp()
    {
        // The map always generates each band's winter camp; MoveCamp shifts
        // them once the systems are up.
        PaintCampAt(WorldLayout.WinterCamp);
        PaintCampAt(WorldLayout.AssiniboineWinterCamp);
        _camps["the band"] = WorldLayout.WinterCamp;
        _camps["the Assiniboine"] = WorldLayout.AssiniboineWinterCamp;
    }

    private static IEnumerable<(Vector2I cell, int source)> CampCells(Vector2I centre, bool town = false)
    {
        yield return (centre, SourceCampfire);
        var c = centre;
        // The Missouri towns are not camps (design doc 13.1): earth lodges
        // twelve metres across, the gardens on the bottomland, corn on the
        // drying scaffolds, a cache pit, a bullboat and the village palisade.
        if (town)
        {
            int lodge = (GameCalendar.Instance?.Month ?? 8) is 12 or 1 or 2 or 3 ? WorldLayout.EarthLodgeWinter : WorldLayout.EarthLodge;
            yield return (IsoGrid.Cell(c, 3, 0), lodge);
            yield return (IsoGrid.Cell(c, 0, 3), lodge);
            yield return (IsoGrid.Cell(c, -3, 0), lodge);
            yield return (IsoGrid.Cell(c, 0, -3), lodge);
            yield return (IsoGrid.Cell(c, 3, 3), lodge);
            yield return (IsoGrid.Cell(c, -3, -3), lodge);
            yield return (IsoGrid.Cell(c, 2, -2), WorldLayout.CornScaffold);
            yield return (IsoGrid.Cell(c, -2, 2), WorldLayout.CachePit);
            yield return (IsoGrid.Cell(c, 1, 1), WorldLayout.CornMortar);
            yield return (IsoGrid.Cell(c, 5, 2), (GameCalendar.Instance?.Month ?? 8) is >= 10 or <= 5 ? WorldLayout.GardenHarvested : WorldLayout.GardenPlot);
            yield return (IsoGrid.Cell(c, 2, 5), (GameCalendar.Instance?.Month ?? 8) is >= 10 or <= 5 ? WorldLayout.GardenHarvested : WorldLayout.GardenPlot);
            yield return (IsoGrid.Cell(c, -5, 0), WorldLayout.VillagePalisade);
            yield return (IsoGrid.Cell(c, 0, -5), WorldLayout.VillagePalisade);
            yield return (IsoGrid.Cell(c, -4, 3), WorldLayout.Bullboat);
            yield break;
        }
        yield return (IsoGrid.Cell(c, 3, 0), SourceTipi);
        yield return (IsoGrid.Cell(c, 0, 3), SourceTipi);
        yield return (IsoGrid.Cell(c, -3, 0), SourceTipi);
        yield return (IsoGrid.Cell(c, 0, -3), SourceTipi);
        yield return (IsoGrid.Cell(c, 3, 3), SourceTipi);
        yield return (IsoGrid.Cell(c, 2, -2), SourceWoodpile);
    }

    private void PaintCampAt(Vector2I centre, bool town = false)
    {
        foreach (var (cell, source) in CampCells(centre, town)) SetBack(cell, source);
    }

    // Where each band's tipis stand now, by band name; the generated map has them at the winter grounds.
    private readonly Dictionary<string, Vector2I> _camps = new();
    private static readonly Dictionary<string, Vector2I> GeneratedCamps = new() { ["the band"] = WorldLayout.WinterCamp, ["the Assiniboine"] = WorldLayout.AssiniboineWinterCamp };

    // Strike the tipis and pitch them elsewhere (design doc 6): the cells,
    // the campfire's hearth, the walkability, and every client's copy.
    public void MoveCamp(string band, Vector2I to)
    {
        if (!_camps.TryGetValue(band, out var from)) from = GeneratedCamps.GetValueOrDefault(band, WorldLayout.WinterCamp);
        if (from == to) return;
        var net = Net.Net.I;
        foreach (var (cell, _) in CampCells(from))
        {
            EraseCell(cell);
            net?.BroadcastCell(0, cell, -1);
            if (_hearthByCell.TryGetValue(cell, out var old)) RemoveHearth(old, cell);
        }
        foreach (var (cell, source) in CampCells(to))
        {
            SetBack(cell, source);
            net?.BroadcastCell(0, cell, source);
            if (source == SourceCampfire)
            {
                var hearth = new Hearth { Position = MapToLocal(cell), Name = $"Hearth_{cell.X}_{cell.Y}", Cell = cell, FuelHours = 4f, Enclosed = false };
                AddHearth(hearth, cell);
            }
        }
        _camps[band] = to;
        RebuildCollision();
        Sim.Paths?.Invalidate();
    }


    // Trees on the cells of a chunk that has come into view.
    public void PaintForest(IEnumerable<Vector2I> cells)
    {
        foreach (var cell in cells)
        {
            int flora = WorldLayout.Flora(cell);
            if (flora < 0 || _felled.Contains(cell)) continue;
            if (GetCellSourceId(cell) != -1) continue;
            SetBack(cell, WorldLayout.IsBerrySource(flora) ? BerryLook(cell, flora) : flora);
        }
    }

    // And off a chunk that has gone out of view (the trees are still there:
    // IsTree answers from the layout).
    public void ClearForest(IEnumerable<Vector2I> cells)
    {
        foreach (var cell in cells) if (WorldLayout.IsFloraSource(GetCellSourceId(cell))) EraseCell(cell);
    }

    // A hearth object for a fire cell that hasn't got one (a campfire built
    // in the bush, or one that arrived from the server).
    private Hearth EnsureHearth(Vector2I cell, float fuel)
    {
        if (_hearthByCell.TryGetValue(cell, out var have)) return have;
        var hearth = new Hearth { Position = MapToLocal(cell), Name = $"Hearth_{cell.X}_{cell.Y}", Cell = cell, FuelHours = fuel, Enclosed = false };
        AddHearth(hearth, cell);
        return hearth;
    }

    // Build a fire in the open (design doc 17.3, 18.4): a ring of stones
    // on bare ground. Returns the hearth, or null if the ground won't do.
    private readonly List<Vector2I> _campfires = new();
    public Hearth BuildCampfire(Vector2I cell)
    {
        if (GetCellSourceId(cell) != -1 || (_front ?? this).GetCellSourceId(cell) != -1) return null;
        if (_floor != null && _floor.GetCellSourceId(cell) != -1) return null;
        if (Sim.Terrain != null && Sim.Terrain.IsWaterOrIce(cell)) return null;
        if (IsSheltered(MapToLocal(cell))) return null;
        SetBack(cell, SourceCampfire);
        _campfires.Add(cell);
        RebuildCollision();
        Net.Net.I?.BroadcastCell(0, cell, SourceCampfire);
        return EnsureHearth(cell, 0f);
    }

    // A tree was felled: the cell clears and the cutter gets wood.
    public bool FellTreeAt(Vector2I cell)
    {
        if (!IsTree(cell)) return false;
        if (IsTreeSource(GetCellSourceId(cell))) EraseCell(cell);
        _felled.Add(cell);
        // No collision to rebuild: bush has none (ShapesFor), and rebuilding
        // every shape in the world for each tree a woodcutter dropped was a
        // hitch every time.
        Net.Net.I?.BroadcastCell(0, cell, -1);
        return true;
    }

    // ---- berries (design doc 13.5) -----------------------------------------
    //
    // A bush is one cell of flora like a tree, but what it shows depends on
    // the month and on whether anybody has been at it: in fruit through its
    // season, picked over for a few weeks after a picker has stripped it,
    // bare in the autumn and a mound of snow in the deep of winter.
    private readonly Dictionary<Vector2I, int> _picked = new();

    private static bool InFruit(int source, int month) => source switch
    {
        WorldLayout.Saskatoon => month is 7 or 8,
        WorldLayout.Cranberry => month is 8 or 9 or 10,
        // The rosehip is the one that hangs on the bush all winter, which is
        // exactly why it mattered to people with the scurvy coming on.
        WorldLayout.Rosehip => month is 7 or 8 or 9 or 10 or 11 or 12 or 1 or 2,
        _ => false,
    };

    public int BerryLook(Vector2I cell, int source)
    {
        var cal = GameCalendar.Instance;
        int month = cal?.Month ?? 8;
        bool stripped = _picked.TryGetValue(cell, out int day) && cal != null && cal.Day - day is >= 0 and < 30;
        if (month is 1 or 2 or 3 && source != WorldLayout.Rosehip) return WorldLayout.BushesSnow;
        if (!stripped && InFruit(source, month)) return source;
        return source switch
        {
            WorldLayout.Saskatoon => WorldLayout.SaskatoonPicked,
            WorldLayout.Cranberry => WorldLayout.CranberryPicked,
            _ => WorldLayout.RosehipBare,
        };
    }

    // The bush a body is standing by, and whether there is anything on it.
    public Vector2I? NearestBerryBush(Vector2 worldPos, float maxDist, bool inFruitOnly)
    {
        var cal = GameCalendar.Instance;
        Vector2I? best = null; float bestD = maxDist;
        var c = LocalToMap(worldPos);
        for (int dx = -3; dx <= 3; dx++)
            for (int dy = -6; dy <= 6; dy++)
            {
                var n = new Vector2I(c.X + dx, c.Y + dy);
                int flora = WorldLayout.Flora(n);
                if (!WorldLayout.IsBerrySource(flora)) continue;
                if (inFruitOnly && BerryLook(n, flora) != flora) continue;
                float d = MapToLocal(n).DistanceTo(worldPos);
                if (d < bestD) { bestD = d; best = n; }
            }
        return best;
    }

    public string BushName(Vector2I cell) => WorldLayout.Flora(cell) switch
    {
        WorldLayout.Saskatoon => "saskatoon bush",
        WorldLayout.Cranberry => "cranberry patch",
        _ => "rosehip bush",
    };

    // Strip it: the picker gets what is on it, and the bush shows picked
    // over until it comes round again.
    public int PickBerriesAt(Vector2I cell)
    {
        int flora = WorldLayout.Flora(cell);
        if (!WorldLayout.IsBerrySource(flora) || BerryLook(cell, flora) != flora) return 0;
        _picked[cell] = GameCalendar.Instance?.Day ?? 0;
        int look = BerryLook(cell, flora);
        if (GetCellSourceId(cell) != -1) SetBack(cell, look);
        Net.Net.I?.BroadcastCell(0, cell, look);
        return flora == WorldLayout.Cranberry ? 5 : flora == WorldLayout.Saskatoon ? 4 : 3;
    }

    public bool IsBed(Vector2I cell) => GetCellSourceId(cell) == SourceBed;

    public Vector2I? NearestBed(Vector2 worldPos, float maxDist)
    {
        Vector2I? best = null; float bestD = maxDist;
        var c = LocalToMap(worldPos);
        for (int dx = -3; dx <= 3; dx++)
            for (int dy = -6; dy <= 6; dy++)
            {
                var n = new Vector2I(c.X + dx, c.Y + dy);
                if (!IsBed(n)) continue;
                float d = MapToLocal(n).DistanceTo(worldPos);
                if (d < bestD) { bestD = d; best = n; }
            }
        return best;
    }

    // A tree whether or not its chunk is painted right now.
    public bool IsTree(Vector2I cell)
    {
        int s = GetCellSourceId(cell);
        if (IsTreeSource(s)) return true;
        if (s != -1 || (Sim.Terrain?.IsLoaded(cell) ?? false)) return false;
        return WorldLayout.ForestAt(cell) && !_felled.Contains(cell);
    }

    // Ring by ring outward, nearest first. The answer for a starting cell is
    // remembered until that tree is felled: woodcutters ask from their post's
    // door again and again, and under fast-forward that was every few frames.
    private readonly Dictionary<Vector2I, Vector2I> _nearestTree = new();
    public Vector2I? NearestTree(Vector2 worldPos, float maxDist = 4000f)
    {
        var c = LocalToMap(worldPos);
        if (_nearestTree.TryGetValue(c, out var known) && IsTree(known) && MapToLocal(known).DistanceTo(worldPos) <= maxDist) return known;
        var found = SearchNearestTree(worldPos, c, maxDist);
        if (found.HasValue) { if (_nearestTree.Count > 2000) _nearestTree.Clear(); _nearestTree[c] = found.Value; }
        return found;
    }

    private Vector2I? SearchNearestTree(Vector2 worldPos, Vector2I c, float maxDist)
    {
        int rings = Mathf.CeilToInt(maxDist / IsoGrid.TileW);
        Vector2I? best = null;
        float bestD = maxDist;
        for (int r = 0; r <= rings; r++)
        {
            if (best.HasValue && r * IsoGrid.TileH / 2f > bestD) break;
            for (int dx = -r; dx <= r; dx++)
                for (int dy = -2 * r; dy <= 2 * r; dy++)
                {
                    if (Mathf.Abs(dx) != r && Mathf.Abs(dy) < 2 * r - 1) continue;
                    var n = new Vector2I(c.X + dx, c.Y + dy);
                    if (!IsTree(n)) continue;
                    float d = MapToLocal(n).DistanceTo(worldPos);
                    if (d < bestD) { bestD = d; best = n; }
                }
        }
        return best;
    }

    // ---- doors ----------------------------------------------------------

    public bool HasDoorNear(Vector2 localPos, float reach = 110f)
    {
        foreach (var layer in Layers())
            foreach (var cell in layer.GetUsedCells())
                if (IsDoor(layer.GetCellSourceId(cell)) && localPos.DistanceTo(MapToLocal(cell)) < reach) return true;
        return false;
    }

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
        int now = (isOpen ? SourceDoorN : SourceDoorOpenN) + dir;
        layer.SetCell(cell, now, Vector2I.Zero);
        RebuildCollisionAt(cell);
        Net.Net.I?.BroadcastCell(layer == this ? 0 : 1, cell, now);
    }

    // A cell change that arrived from the server.
    public void ApplyRemoteCell(int layer, Vector2I cell, int source)
    {
        if (layer == 3)
        {
            foreach (var (_, roof) in _roofs) if (roof.GetCellSourceId(cell) >= 0) roof.EraseCell(cell);
            return;
        }
        var l = layer == 0 ? this : layer == 1 ? (_front ?? this) : (_floor ?? this);
        // A felled tree stays felled when its chunk is painted again.
        if (layer == 0 && source < 0 && IsTree(cell)) _felled.Add(cell);
        if (source < 0) l.EraseCell(cell); else l.SetCell(cell, source, Vector2I.Zero);
        if (layer == 0 && source == SourceCampfire) { EnsureHearth(cell, 0f); if (!_campfires.Contains(cell)) _campfires.Add(cell); }
        RebuildCollisionAt(cell);
    }

    // A late joiner needs every cell that differs from the generated map:
    // open doors and felled trees.
    public void SendCellStateTo(int peer)
    {
        var net = Net.Net.I;
        if (net == null) return;
        foreach (var layer in Layers())
        {
            int idx = layer == this ? 0 : 1;
            foreach (var cell in layer.GetUsedCells())
            {
                int s = layer.GetCellSourceId(cell);
                if (OpenDoors.Contains(s)) net.RpcId(peer, nameof(Net.Net.SetCell), idx, cell.X, cell.Y, s);
            }
        }
        foreach (var (band, gen) in GeneratedCamps)
        {
            if (!_camps.TryGetValue(band, out var now) || now == gen) continue;
            foreach (var (cell, _) in CampCells(gen)) net.RpcId(peer, nameof(Net.Net.SetCell), 0, cell.X, cell.Y, -1);
            foreach (var (cell, source) in CampCells(now)) net.RpcId(peer, nameof(Net.Net.SetCell), 0, cell.X, cell.Y, source);
        }
        foreach (var cell in _campfires) net.RpcId(peer, nameof(Net.Net.SetCell), 0, cell.X, cell.Y, SourceCampfire);
        foreach (var cell in _felled)
        {
            net.RpcId(peer, nameof(Net.Net.SetCell), 0, cell.X, cell.Y, -1);
            if ((_floor ?? this).GetCellSourceId(cell) < 0) { net.RpcId(peer, nameof(Net.Net.SetCell), 1, cell.X, cell.Y, -1); net.RpcId(peer, nameof(Net.Net.SetCell), 2, cell.X, cell.Y, -1); net.RpcId(peer, nameof(Net.Net.SetCell), 3, cell.X, cell.Y, -1); }
        }
    }
    private readonly HashSet<Vector2I> _felled = new();

    // Everything that differs from the generated map, for a save: open
    // doors and cells cleared by axe or fire.
    public Godot.Collections.Array ChangedCells()
    {
        var a = new Godot.Collections.Array();
        foreach (var layer in Layers())
        {
            int idx = layer == this ? 0 : 1;
            foreach (var cell in layer.GetUsedCells())
            {
                int s = layer.GetCellSourceId(cell);
                if (OpenDoors.Contains(s)) a.Add(new Godot.Collections.Array { idx, cell.X, cell.Y, s });
            }
        }
        foreach (var cell in _campfires) a.Add(new Godot.Collections.Array { 0, cell.X, cell.Y, SourceCampfire });
        foreach (var cell in _felled)
        {
            a.Add(new Godot.Collections.Array { 0, cell.X, cell.Y, -1 });
            if ((_floor ?? this).GetCellSourceId(cell) < 0)
            {
                a.Add(new Godot.Collections.Array { 1, cell.X, cell.Y, -1 });
                a.Add(new Godot.Collections.Array { 2, cell.X, cell.Y, -1 });
                a.Add(new Godot.Collections.Array { 3, cell.X, cell.Y, -1 });
            }
        }
        return a;
    }

    public void RestoreCells(Godot.Collections.Array cells)
    {
        foreach (var v in cells)
        {
            var c = v.AsGodotArray();
            int layer = c[0].AsInt32(); var cell = new Vector2I(c[1].AsInt32(), c[2].AsInt32()); int source = c[3].AsInt32();
            ApplyRemoteCell(layer, cell, source);
            if (layer == 0 && source < 0 && !_felled.Contains(cell)) _felled.Add(cell);
        }
    }

    public void SetHearthFuel(Vector2I cell, float fuel)
    {
        if (_hearthByCell.TryGetValue(cell, out var h)) { h.Lit = fuel >= 0f; h.FuelHours = Mathf.Abs(fuel); }
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

    // ---- fire (design doc 18.4) -----------------------------------------------

    // Wood burns: floors, walls, doors, fences, furniture, trees. Stone hearths don't.
    public bool IsFlammable(Vector2I cell)
    {
        var floor = _floor ?? this;
        if (floor.GetCellSourceId(cell) == SourceFloorPlanks) return true;
        foreach (var layer in Layers())
        {
            int s = layer.GetCellSourceId(cell);
            if (s < 0) continue;
            if (Hearths.Contains(s) || s == SourceHearthLeft || s == SourceHearthRight) continue;
            return true;   // walls, doors, fences, beds, tables, trees, tipis
        }
        return false;
    }

    // Everything on the cell is gone: no wall, no floor, no roof, no bed.
    public void BurnOut(Vector2I cell)
    {
        foreach (var layer in Layers())
        {
            if (layer.GetCellSourceId(cell) >= 0) { layer.EraseCell(cell); Net.Net.I?.BroadcastCell(layer == this ? 0 : 1, cell, -1); }
        }
        var floor = _floor ?? this;
        if (floor.GetCellSourceId(cell) == SourceFloorPlanks) { floor.EraseCell(cell); Net.Net.I?.BroadcastCell(2, cell, -1); }
        foreach (var (b, roof) in _roofs)
        {
            if (roof.GetCellSourceId(cell) >= 0) { roof.EraseCell(cell); Net.Net.I?.BroadcastCell(3, cell, -1); }
        }
        _felled.Add(cell);
        RebuildCollisionAt(cell);
    }

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

    public static bool IsTreeSource(int s) => WorldLayout.IsTreeSource(s);

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

    // An array, not an iterator: the pathfinder asks about walls thousands of
    // times a search, and each iterator was garbage for the collector.
    private TileMapLayer[] _layers;
    private TileMapLayer[] Layers()
    {
        int want = _front != null ? 2 : 1;
        if (_layers == null || _layers.Length != want || (want == 2 && _layers[1] != _front))
            _layers = want == 2 ? new TileMapLayer[] { this, _front } : new TileMapLayer[] { this };
        return _layers;
    }

    // Each cell's collision shapes, so one cell that changes (a door opened,
    // a wall burnt) is redone on its own. Rebuilding every shape in the world
    // each time anybody walked through a door was the worst hitch in a frame.
    private readonly Dictionary<Vector2I, List<CollisionShape2D>> _shapesAt = new();

    private void RebuildCollision()
    {
        if (_body == null) return;
        foreach (Node child in _body.GetChildren())
        {
            child.Free();
        }
        _shapesAt.Clear();
        foreach (var layer in Layers())
        {
            foreach (var cell in layer.GetUsedCells()) AddShapes(layer, cell);
        }
    }

    private void AddShapes(TileMapLayer layer, Vector2I cell)
    {
        foreach (var shape in ShapesFor(layer.GetCellSourceId(cell)))
        {
            var node = new CollisionShape2D { Shape = shape, Position = MapToLocal(cell) };
            _body.AddChild(node);
            if (!_shapesAt.TryGetValue(cell, out var list)) _shapesAt[cell] = list = new List<CollisionShape2D>(2);
            list.Add(node);
        }
    }

    // Just this cell's shapes, from whatever is on it now in every layer.
    private void RebuildCollisionAt(Vector2I cell)
    {
        if (_body == null) return;
        if (_shapesAt.Remove(cell, out var old)) foreach (var node in old) if (IsInstanceValid(node)) node.Free();
        foreach (var layer in Layers()) if (layer.GetCellSourceId(cell) >= 0) AddShapes(layer, cell);
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
