using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Player;
using IsometricFurTradeGame.Scripts.World;

namespace IsometricFurTradeGame.Scripts.UI;

// M: the country as you know it (design doc 10.8). The coast, the river
// and the posts are common knowledge; wildlife sites appear only once you
// have found them or been told, and a herd is drawn where you last heard
// it was - which may be nowhere near where it is now.
public partial class MapPanel : Control
{
    public static bool IsOpen { get; private set; }
    private Label _title;
    private const float Scale = 1f / 32f;   // world px -> map px (10240 -> 320)
    private PanelContainer _panel;
    private MapCanvas _canvas;
    private PlayerController _player;

    public override void _Ready()
    {
        _player = Sim.Player as PlayerController;
        Sim.LocalPlayerChanged += p => { _player = p; if (_canvas != null) _canvas.Player = p; };
        _panel = new PanelContainer { Visible = false };
        _panel.SetAnchorsPreset(LayoutPreset.FullRect);
        _panel.OffsetLeft = 40; _panel.OffsetTop = 30; _panel.OffsetRight = -40; _panel.OffsetBottom = -30;
        _panel.AddThemeStyleboxOverride("panel", MenuTheme.PaperBox(18));
        AddChild(_panel);
        var box = new VBoxContainer();
        _panel.AddChild(box);
        var title = _title = new Label { Text = "The country as you know it", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 15);
        title.AddThemeColorOverride("font_color", new Color(0.2f, 0.12f, 0.05f));
        box.AddChild(title);
        _canvas = new MapCanvas { Player = _player, SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        box.AddChild(_canvas);
        var legend = new Label { Text = "you ●   posts ■ (HBC red, NWC blue)   camps ▲   sites ○   waypoint ◆ gold   your task ◆ blue      click: set a waypoint   right-click: clear it   G: set out for it   drag: pan   wheel: zoom", HorizontalAlignment = HorizontalAlignment.Center };
        legend.AddThemeFontSizeOverride("font_size", 11);
        legend.AddThemeColorOverride("font_color", new Color(0.25f, 0.17f, 0.08f));
        box.AddChild(legend);
        foreach (string arg in OS.GetCmdlineUserArgs()) if (arg == "--map") GetTree().CreateTimer(2.5).Timeout += () => { _panel.Visible = true; if (_player != null) _canvas.CentreOn(_player.GlobalPosition); };
        GetParent()?.CallDeferred(Node.MethodName.AddChild, new WayfindingHud { Name = "Wayfinding" });
        GetParent()?.CallDeferred(Node.MethodName.AddChild, new TravelMode { Name = "TravelMode" });
        GetParent()?.CallDeferred(Node.MethodName.AddChild, new RewardPopups { Name = "RewardPopups" });
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("map")) { _panel.Visible = !_panel.Visible; _title.Text = $"The country as you know it ({Core.Keybinds.KeyFor("map")})"; if (_panel.Visible && _player != null) _canvas.CallDeferred(nameof(MapCanvas.CentreOn), _player.GlobalPosition); GetViewport().SetInputAsHandled(); }
        else if (_panel.Visible && @event.IsActionPressed("ui_cancel")) { _panel.Visible = false; GetViewport().SetInputAsHandled(); }
    }

    public override void _Process(double delta)
    {
        IsOpen = _panel.Visible;
        if (_panel.Visible) _canvas.QueueRedraw();
    }
}

// Draws the base map once into a texture and the moving things on top.
public partial class MapCanvas : Control
{
    public PlayerController Player;
    private ImageTexture _base;
    // Pan by dragging, zoom with the wheel about the cursor.
    private float Zoom = 1.3f;
    private Vector2 Pan = Vector2.Zero;
    private bool _dragging;
    private float _dragMoved;
    private const float S = 1f / 32f;
    private ImageTexture _fog;
    private int _fogVersion = -1;

    public override void _Ready()
    {
        BuildBase();
        ClipContents = true;
        MouseFilter = MouseFilterEnum.Stop;
        CustomMinimumSize = new Vector2(600, 400);
        Pan = new Vector2(0, 0);
    }

    // Drag to pan, wheel to zoom about the cursor (design doc 10.8: the map is a thing you pore over).
    public override void _GuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mb)
        {
            if (mb.ButtonIndex == MouseButton.Left)
            {
                // A click that didn't drag marks a waypoint there.
                if (!mb.Pressed && _dragging && _dragMoved < 6f) Waypoint.Set((mb.Position - Pan) / (S * Zoom));
                _dragging = mb.Pressed; _dragMoved = 0f; AcceptEvent();
            }
            else if (mb.ButtonIndex == MouseButton.Right && mb.Pressed) { Waypoint.Clear(); AcceptEvent(); }
            else if (mb.Pressed && (mb.ButtonIndex == MouseButton.WheelUp || mb.ButtonIndex == MouseButton.WheelDown))
            {
                float factor = mb.ButtonIndex == MouseButton.WheelUp ? 1.25f : 0.8f;
                float newZoom = Mathf.Clamp(Zoom * factor, 0.6f, 8f);
                factor = newZoom / Zoom;
                // Keep the point under the cursor where it is.
                Pan = mb.Position - (mb.Position - Pan) * factor;
                Zoom = newZoom;
                ClampPan();
                QueueRedraw();
                AcceptEvent();
            }
        }
        else if (@event is InputEventMouseMotion mm && _dragging)
        {
            Pan += mm.Relative;
            _dragMoved += mm.Relative.Length();
            ClampPan();
            QueueRedraw();
            AcceptEvent();
        }
    }

    private void ClampPan()
    {
        if (_base == null) return;
        var mapSize = _base.GetSize() * Zoom;
        var view = Size;
        // Never let the map drift entirely off the window.
        Pan.X = Mathf.Clamp(Pan.X, Mathf.Min(0f, view.X - mapSize.X), Mathf.Max(0f, view.X - mapSize.X));
        Pan.Y = Mathf.Clamp(Pan.Y, Mathf.Min(0f, view.Y - mapSize.Y), Mathf.Max(0f, view.Y - mapSize.Y));
    }

    // Centre the view on a world point (the player, when the map opens).
    public void CentreOn(Vector2 world)
    {
        Pan = Size / 2f - world * S * Zoom;
        ClampPan();
    }

    private void BuildBase()
    {
        // The whole country from the world map: a cell is 128 x 32 world px,
        // so at 1/32 it is four map pixels across and one down.
        var src = WorldMap.GroundImage;
        if (src == null) return;
        var img = (Image)src.Duplicate();
        img.Convert(Image.Format.Rgba8);
        img.Resize(WorldMap.Columns * 4, WorldMap.Rows, Image.Interpolation.Nearest);
        var yard = new Color(0.55f, 0.42f, 0.28f);
        var fortCells = new HashSet<Vector2I>(WorldLayout.YardCells()); foreach (var c in WorldLayout.NwcYardCells()) fortCells.Add(c); foreach (var c in WorldLayout.OutpostYardCells()) fortCells.Add(c);
        foreach (var cell in fortCells)
        {
            var p = IsoGrid.ToWorld(cell) * S;
            int px = Mathf.FloorToInt(p.X) - 2, py = Mathf.FloorToInt(p.Y);
            for (int dx = 0; dx < 4; dx++) { int xx = px + dx; if (xx >= 0 && xx < img.GetWidth() && py >= 0 && py < img.GetHeight()) img.SetPixel(xx, py, yard); }
        }
        // Dev aid: `-- --dumpmap` saves the whole country to user://map.png.
        foreach (string arg in OS.GetCmdlineUserArgs())
            if (arg == "--dumpmap") { var path = ProjectSettings.GlobalizePath("user://map.png"); img.SavePng(path); GD.Print($"[map] {path}"); }
        _base = ImageTexture.CreateFromImage(img);
    }

    private void BuildFog()
    {
        _fogVersion = Exploration.Version;
        int w = Exploration.Width, h = Exploration.Height;
        var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
        var parchment = new Color(0.8f, 0.72f, 0.55f);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                img.SetPixel(x, y, Exploration.IsSeenCell(x, y) ? new Color(parchment, 0f) : parchment);
        img.Resize(w * 8, h * 8, Image.Interpolation.Bilinear);
        _fog = ImageTexture.CreateFromImage(img);
    }

    private Vector2 M(Vector2 world) => world * S * Zoom + Pan;
    private Vector2 M(Vector2I cell) => M(IsoGrid.ToWorld(cell));

    public override void _Draw()
    {
        if (_base == null) return;
        DrawTextureRect(_base, new Rect2(Pan, _base.GetSize() * Zoom), false);
        var ink = new Color(0.15f, 0.1f, 0.05f);
        var font = ThemeDB.FallbackFont;
        if (_fogVersion != Exploration.Version) BuildFog();
        if (_fog != null) DrawTextureRect(_fog, new Rect2(Pan, new Vector2(Exploration.Width, Exploration.Height) * Exploration.CellPx * S * Zoom), false);

        // The brigade roads and the named country, where you have been (design
        // doc 10.8: geography is knowledge).
        var route = new Color(0.55f, 0.18f, 0.12f, 0.8f);
        foreach (var (_, pts) in WorldMap.BrigadeRoutes())
            for (int i = 0; i + 1 < pts.Length; i++)
                if (Exploration.IsSeen(pts[i]) || Exploration.IsSeen(pts[i + 1])) DrawDashedLine(M(pts[i]), M(pts[i + 1]), route, 2f, 8f);
        foreach (var lm in WorldMap.Landmarks())
        {
            if (!Exploration.IsSeen(lm.At)) continue;
            var c = M(lm.At);
            switch (lm.Kind)
            {
                case "portage":
                    DrawLine(c + new Vector2(-4, -4), c + new Vector2(4, 4), route, 2f); DrawLine(c + new Vector2(-4, 4), c + new Vector2(4, -4), route, 2f);
                    if (Zoom >= 1.6f) DrawString(font, c + new Vector2(7, 4), lm.Name, HorizontalAlignment.Left, -1, 11, route);
                    break;
                case "water": DrawString(font, c + new Vector2(-150, 0), lm.Name, HorizontalAlignment.Center, 300, 16, new Color(0.15f, 0.25f, 0.45f, 0.85f)); break;
                case "mountains": DrawString(font, c + new Vector2(-150, 0), lm.Name, HorizontalAlignment.Center, 300, 15, new Color(0.3f, 0.22f, 0.15f, 0.8f)); break;
                case "lake": if (Zoom >= 1.2f) DrawString(font, c + new Vector2(-100, 0), lm.Name, HorizontalAlignment.Center, 200, 12, new Color(0.15f, 0.25f, 0.45f, 0.85f)); break;
                case "river": if (Zoom >= 1.6f) DrawString(font, c + new Vector2(-100, 0), lm.Name, HorizontalAlignment.Center, 200, 11, new Color(0.15f, 0.3f, 0.5f, 0.8f)); break;
                default: if (Zoom >= 1.2f) DrawString(font, c + new Vector2(-100, 0), lm.Name, HorizontalAlignment.Center, 200, 12, new Color(0.3f, 0.22f, 0.15f, 0.8f)); break;
            }
        }

        // The posts and camps of the wider country, where you have been.
        foreach (var post in Posts.Standing())
        {
            if (!Exploration.IsSeen(post.Centre)) continue;
            var c = M(post.Centre);
            DrawRect(new Rect2(c - new Vector2(3, 3), new Vector2(6, 6)), post.Company switch { "NWC" => new Color(0.15f, 0.25f, 0.6f), "AMER" => new Color(0.2f, 0.45f, 0.25f), _ => new Color(0.6f, 0.15f, 0.1f) });
            if (Zoom >= 1.6f) DrawString(font, c + new Vector2(8, 4), post.Name, HorizontalAlignment.Left, -1, 11, ink);
        }
        foreach (var camp in Camps.All)
        {
            var w = IsoGrid.ToWorld(camp.Centre);
            if (!Exploration.IsSeen(w)) continue;
            var c = M(w);
            DrawColoredPolygon(new[] { c + new Vector2(0, -6), c + new Vector2(5, 3), c + new Vector2(-5, 3) }, new Color(0.62f, 0.42f, 0.25f));
            if (Zoom >= 2.4f && !camp.IsFamily) DrawString(font, c + new Vector2(8, 4), camp.Name, HorizontalAlignment.Left, -1, 10, ink);
        }

        // Common knowledge: the posts, the wharf, the camp where it stands now.
        var fortC = M(IsoGrid.Cell(WorldLayout.FortTop, WorldLayout.FortWidth / 2, WorldLayout.FortDepth / 2));
        DrawRect(new Rect2(fortC - new Vector2(3, 3), new Vector2(6, 6)), new Color(0.5f, 0.25f, 0.1f));
        DrawString(font, fortC + new Vector2(8, 4), "York Factory", HorizontalAlignment.Left, -1, 12, ink);
        var nwcC = M(IsoGrid.Cell(WorldLayout.NwcTop, WorldLayout.NwcWidth / 2, WorldLayout.NwcDepth / 2));
        DrawRect(new Rect2(nwcC - new Vector2(3, 3), new Vector2(6, 6)), new Color(0.6f, 0.15f, 0.1f));
        DrawString(font, nwcC + new Vector2(8, 4), "Nor'Westers", HorizontalAlignment.Left, -1, 12, ink);
        foreach (var band in Sim.Bands)
        {
            var camp = M(band.Camp);
            DrawColoredPolygon(new[] { camp + new Vector2(0, -7), camp + new Vector2(6, 4), camp + new Vector2(-6, 4) }, band.IsCree ? new Color(0.75f, 0.55f, 0.3f) : new Color(0.65f, 0.45f, 0.5f));
            DrawString(font, camp + new Vector2(9, 4), $"{band.Name} ({(band.Camp == band.WinterGround ? "winter camp" : "summer camp")})", HorizontalAlignment.Left, -1, 12, ink);
        }
        DrawLine(M(WorldLayout.WharfRoot), M(WorldLayout.WharfEnd), new Color(0.3f, 0.2f, 0.1f), 2f);
        // The river road and the inland post.
        for (int i = 0; i + 1 < WorldLayout.RiverRoad.Length; i++) DrawDashedLine(M(WorldLayout.RiverRoad[i]), M(WorldLayout.RiverRoad[i + 1]), new Color(0.35f, 0.25f, 0.12f), 1.5f, 6f);
        var outC = M(IsoGrid.Cell(WorldLayout.OutpostTop, WorldLayout.OutpostWidth / 2, WorldLayout.OutpostDepth / 2));
        DrawRect(new Rect2(outC - new Vector2(3, 3), new Vector2(6, 6)), new Color(0.5f, 0.25f, 0.1f));
        DrawString(font, outC + new Vector2(8, 4), "Oxford House", HorizontalAlignment.Left, -1, 12, ink);
        var ford = M(TerrainPainter.NamedSpot("ford"));
        DrawString(font, ford + new Vector2(6, 4), "ford", HorizontalAlignment.Left, -1, 11, ink);

        // What you know of the game (design doc 16.4, 16.5).
        var eco = Sim.Ecology;
        if (Player != null && eco != null)
        {
            foreach (var site in eco.Sites)
            {
                if (!Player.KnownSites.Contains(site.Name)) continue;
                var at = Player.SiteBelief.TryGetValue(site.Name, out var b) ? b : site.HomeCell;
                var p = M(at);
                DrawArc(p, 6f, 0f, Mathf.Tau, 24, ink, 1.5f);
                string label = site.Name + (site.IsMobile ? " (last known)" : "");
                var line = site.LineOf(Player.TrapOwner);
                if (line != null && line.Traps > 0) label += $" - {line.Traps} traps";
                DrawString(font, p + new Vector2(9, 4), label, HorizontalAlignment.Left, -1, 11, ink);
            }
        }

        // Ship, if she's in sight.
        var ship = Sim.Ship;
        if (ship != null && ship.Visible)
        {
            var p = M(ship.GlobalPosition);
            DrawColoredPolygon(new[] { p + new Vector2(0, -6), p + new Vector2(6, 0), p + new Vector2(0, 6), p + new Vector2(-6, 0) }, new Color(0.2f, 0.2f, 0.4f));
        }

        // People: you and the other players.
        foreach (var pc in Sim.Players())
        {
            if (pc == null || !IsInstanceValid(pc)) continue;
            var p = M(pc.GlobalPosition);
            bool me = pc == Player;
            DrawCircle(p, me ? 5f : 4f, me ? Colors.White : new Color(1f, 0.85f, 0.3f));
            DrawArc(p, me ? 5f : 4f, 0f, Mathf.Tau, 16, ink, 1f);
            if (!me) DrawString(font, p + new Vector2(7, 4), pc.DisplayName, HorizontalAlignment.Left, -1, 11, ink);
        }
        // Where you mean to go, and where your task is.
        if (Waypoint.Target is { } wp)
        {
            var p = M(wp);
            DrawColoredPolygon(new[] { p + new Vector2(0, -7), p + new Vector2(6, 0), p + new Vector2(0, 7), p + new Vector2(-6, 0) }, new Color(0.95f, 0.75f, 0.2f));
            if (Player != null) DrawString(font, p + new Vector2(9, 4), $"waypoint, {WorldMap.Distance(Player.GlobalPosition.DistanceTo(wp))}", HorizontalAlignment.Left, -1, 11, ink);
        }
        if (Objective.Of(Player) is { } goal)
        {
            var p = M(goal.At);
            DrawColoredPolygon(new[] { p + new Vector2(0, -6), p + new Vector2(5, 0), p + new Vector2(0, 6), p + new Vector2(-5, 0) }, new Color(0.3f, 0.55f, 0.9f));
            DrawString(font, p + new Vector2(8, -6), goal.Label, HorizontalAlignment.Left, -1, 11, ink);
        }
        // The quest step in hand.
        if (Player?.QuestMark is { } quest)
        {
            var p = M(quest.At);
            DrawColoredPolygon(new[] { p + new Vector2(0, -7), p + new Vector2(6, 0), p + new Vector2(0, 7), p + new Vector2(-6, 0) }, WayfindingHud.QuestColour);
            DrawArc(p, 9f, 0f, Mathf.Tau, 20, WayfindingHud.QuestColour, 1.5f);
            DrawString(font, p + new Vector2(10, 14), quest.Label, HorizontalAlignment.Left, -1, 11, ink);
        }
        if (Player != null && Player.Spouse != null && IsInstanceValid(Player.Spouse) && !Player.Spouse.IsDead)
        {
            var p = M(Player.Spouse.GlobalPosition);
            DrawCircle(p, 3f, new Color(0.9f, 0.5f, 0.6f));
        }
    }
}
