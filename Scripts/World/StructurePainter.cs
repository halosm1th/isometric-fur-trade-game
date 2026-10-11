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

    // One roof layer per building, drawn above everything; it fades away
    // while the player is under it (design doc 18.1: a roof is a property
    // of the cells, and this is what it looks like from outside).
    private readonly List<(BuildingDef building, TileMapLayer roof)> _roofs = new();

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

    public override void _Process(double delta)
    {
        if (Engine.IsEditorHint() || _front == null || _player == null) return;
        float target = IsSheltered(_player.Position) ? IndoorFrontAlpha : 1f;
        var m = _front.Modulate;
        m.A = Mathf.MoveToward(m.A, target, (float)delta * 4f);
        _front.Modulate = m;

        var playerCell = LocalToMap(_player.Position);
        foreach (var (b, roof) in _roofs)
        {
            bool under = b.Contains(playerCell) || b.Contains(IsoGrid.DownLeft(playerCell)) || b.Contains(IsoGrid.DownRight(playerCell));
            var rm = roof.Modulate;
            rm.A = Mathf.MoveToward(rm.A, under ? 0f : 1f, (float)delta * 4f);
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
            foreach (var cell in IsoGrid.Rhombus(b.Top, b.Width, b.Depth)) roof.SetCell(cell, SourceRoof, Vector2I.Zero);
            host.AddChild(roof);
            _roofs.Add((b, roof));
        }
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

    public Hearth HearthAt(Vector2I cell) => _hearthByCell.TryGetValue(cell, out var h) ? h : null;

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
        PaintNwcPalisade();
        PaintPalisadeAt(WorldLayout.OutpostTop, WorldLayout.OutpostWidth, WorldLayout.OutpostDepth, WorldLayout.OutpostGateOffset);
        SetBack(WorldLayout.OutpostWoodpileCell, SourceWoodpile);
        SetBack(IsoGrid.Cell(WorldLayout.OutpostTop, 10, 3), SourceCrate);
        SetBack(IsoGrid.Cell(WorldLayout.OutpostTop, 1, 7), SourceFurBale);
        foreach (var b in WorldLayout.Buildings) PaintBuilding(b, floor);
        PaintYard();
        PaintCamp();
        // In the editor the posts standing at the usual start are shown; in a
        // game Posts paints each one as it comes to stand.
        if (Engine.IsEditorHint()) foreach (var p in Posts.All) if (p.Year <= 1811) PaintPost(p, false);
        // The forest is painted chunk by chunk as TerrainPainter streams the country in.
    }

    // One of the wider country's posts: palisade, house, woodpile. In a game
    // it gets its hearth and roof too; in the editor just the tiles.
    public void PaintPost(TradingPost p, bool live)
    {
        PaintPalisadeAt(p.Top, TradingPost.Width, TradingPost.Depth, TradingPost.GateOffset);
        PaintBuilding(p.House, _floor ?? this);
        SetBack(p.WoodpileCell, SourceWoodpile);
        SetBack(IsoGrid.Cell(p.Top, 10, 3), SourceCrate);
        if (!live) return;
        EnsureHearth(p.HearthCell, 6f);
        var host = GetParent()?.GetParent();
        if (host != null && !Engine.IsEditorHint())
        {
            var roof = new TileMapLayer { TileSet = TileSet, ZIndex = 5, ZAsRelative = false, Name = $"Roof_{p.Name.Replace(' ', '_').Replace('.', '_').Replace('\'', '_')}", Position = new Vector2(0, -RoofLift) };
            foreach (var cell in IsoGrid.Rhombus(p.House.Top, p.House.Width, p.House.Depth)) roof.SetCell(cell, SourceRoof, Vector2I.Zero);
            host.AddChild(roof);
            _roofs.Add((p.House, roof));
        }
        RebuildCollision();
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

    private static IEnumerable<(Vector2I cell, int source)> CampCells(Vector2I centre)
    {
        yield return (centre, SourceCampfire);
        var c = centre;
        yield return (IsoGrid.Cell(c, 3, 0), SourceTipi);
        yield return (IsoGrid.Cell(c, 0, 3), SourceTipi);
        yield return (IsoGrid.Cell(c, -3, 0), SourceTipi);
        yield return (IsoGrid.Cell(c, 0, -3), SourceTipi);
        yield return (IsoGrid.Cell(c, 3, 3), SourceTipi);
        yield return (IsoGrid.Cell(c, 2, -2), SourceWoodpile);
    }

    private void PaintCampAt(Vector2I centre)
    {
        foreach (var (cell, source) in CampCells(centre)) SetBack(cell, source);
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
            if (_hearthByCell.TryGetValue(cell, out var old)) { _hearths.Remove(old); _hearthByCell.Remove(cell); old.QueueFree(); }
        }
        foreach (var (cell, source) in CampCells(to))
        {
            SetBack(cell, source);
            net?.BroadcastCell(0, cell, source);
            if (source == SourceCampfire)
            {
                var hearth = new Hearth { Position = MapToLocal(cell), Name = $"Hearth_{cell.X}_{cell.Y}", Cell = cell, FuelHours = 4f, Enclosed = false };
                AddChild(hearth);
                _hearths.Add(hearth);
                _hearthByCell[cell] = hearth;
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
            if (!WorldLayout.ForestAt(cell) || _felled.Contains(cell)) continue;
            if (GetCellSourceId(cell) != -1) continue;
            SetBack(cell, SourceSpruce0 + (int)(IsoGrid.Hash(cell, 12) * 3f));
        }
    }

    // And off a chunk that has gone out of view (the trees are still there:
    // IsTree answers from the layout).
    public void ClearForest(IEnumerable<Vector2I> cells)
    {
        foreach (var cell in cells) if (IsTreeSource(GetCellSourceId(cell))) EraseCell(cell);
    }

    // A hearth object for a fire cell that hasn't got one (a campfire built
    // in the bush, or one that arrived from the server).
    private Hearth EnsureHearth(Vector2I cell, float fuel)
    {
        if (_hearthByCell.TryGetValue(cell, out var have)) return have;
        var hearth = new Hearth { Position = MapToLocal(cell), Name = $"Hearth_{cell.X}_{cell.Y}", Cell = cell, FuelHours = fuel, Enclosed = false };
        AddChild(hearth);
        _hearths.Add(hearth);
        _hearthByCell[cell] = hearth;
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
        RebuildCollision();
        Net.Net.I?.BroadcastCell(0, cell, -1);
        return true;
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

    // Ring by ring outward, nearest first.
    public Vector2I? NearestTree(Vector2 worldPos, float maxDist = 4000f)
    {
        var c = LocalToMap(worldPos);
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
        RebuildCollision();
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
        RebuildCollision();
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
        RebuildCollision();
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
