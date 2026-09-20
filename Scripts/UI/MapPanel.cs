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
    private const float Scale = 1f / 32f;   // world px -> map px (10240 -> 320)
    private PanelContainer _panel;
    private MapCanvas _canvas;
    private PlayerController _player;

    public override void _Ready()
    {
        _player = Sim.Player as PlayerController;
        Sim.LocalPlayerChanged += p => { _player = p; if (_canvas != null) _canvas.Player = p; };
        _panel = new PanelContainer { Visible = false };
        _panel.SetAnchorsPreset(LayoutPreset.Center);
        _panel.GrowHorizontal = GrowDirection.Both; _panel.GrowVertical = GrowDirection.Both;
        var style = new StyleBoxFlat { BgColor = new Color(0.86f, 0.78f, 0.6f, 0.97f), BorderColor = new Color(0.4f, 0.3f, 0.15f) };
        style.SetBorderWidthAll(3); style.SetCornerRadiusAll(3); style.SetContentMarginAll(10);
        _panel.AddThemeStyleboxOverride("panel", style);
        AddChild(_panel);
        var box = new VBoxContainer();
        _panel.AddChild(box);
        var title = new Label { Text = "The country as you know it (M)", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 15);
        title.AddThemeColorOverride("font_color", new Color(0.2f, 0.12f, 0.05f));
        box.AddChild(title);
        _canvas = new MapCanvas { CustomMinimumSize = new Vector2(WorldLayout.MapWidth * IsoGrid.TileW * Scale * 2f, WorldLayout.MapHeight * IsoGrid.TileH / 2f * Scale * 2f), Player = _player };
        box.AddChild(_canvas);
        var legend = new Label { Text = "you ●   others ●   posts ■   camp ▲   sites ○ (where you last knew them)   ship ◆", HorizontalAlignment = HorizontalAlignment.Center };
        legend.AddThemeFontSizeOverride("font_size", 11);
        legend.AddThemeColorOverride("font_color", new Color(0.25f, 0.17f, 0.08f));
        box.AddChild(legend);
        foreach (string arg in OS.GetCmdlineUserArgs()) if (arg == "--map") GetTree().CreateTimer(2.5).Timeout += () => _panel.Visible = true;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("map")) { _panel.Visible = !_panel.Visible; GetViewport().SetInputAsHandled(); }
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
    private const float Zoom = 2f;   // 320 -> 640 px on screen
    private const float S = 1f / 32f;

    public override void _Ready()
    {
        BuildBase();
    }

    private void BuildBase()
    {
        int w = Mathf.CeilToInt(WorldLayout.MapWidth * IsoGrid.TileW * S), h = Mathf.CeilToInt(WorldLayout.MapHeight * IsoGrid.TileH / 2f * S);
        var img = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
        var water = new Color(0.36f, 0.5f, 0.66f); var sand = new Color(0.78f, 0.7f, 0.5f); var grass = new Color(0.6f, 0.64f, 0.42f);
        var bush = new Color(0.36f, 0.45f, 0.3f); var yard = new Color(0.55f, 0.42f, 0.28f);
        var fortCells = new HashSet<Vector2I>(WorldLayout.YardCells()); foreach (var c in WorldLayout.NwcYardCells()) fortCells.Add(c);
        for (int x = 0; x < WorldLayout.MapWidth; x++)
        {
            int shore = TerrainPainter.ShorelineRow(x);
            for (int y = 0; y < WorldLayout.MapHeight; y++)
            {
                var cell = new Vector2I(x, y);
                Color col;
                if (y < shore || WorldLayout.IsRiver(cell) && !WorldLayout.IsFord(cell)) col = water;
                else if (y < shore + WorldLayout.BeachRows || WorldLayout.IsRiverBank(cell) || WorldLayout.IsFord(cell)) col = sand;
                else if (fortCells.Contains(cell)) col = yard;
                else if (WorldLayout.ForestDensity(cell) > 0.2f) col = bush;
                else col = grass;
                var p = IsoGrid.ToWorld(cell) * S;
                int px = Mathf.FloorToInt(p.X) - 2, py = Mathf.FloorToInt(p.Y);
                for (int dx = 0; dx < 4; dx++) { int xx = px + dx; if (xx >= 0 && xx < w && py >= 0 && py < h) img.SetPixel(xx, py, col); }
            }
        }
        _base = ImageTexture.CreateFromImage(img);
    }

    private Vector2 M(Vector2 world) => world * S * Zoom;
    private Vector2 M(Vector2I cell) => M(IsoGrid.ToWorld(cell));

    public override void _Draw()
    {
        if (_base == null) return;
        DrawTextureRect(_base, new Rect2(Vector2.Zero, _base.GetSize() * Zoom), false);
        var ink = new Color(0.15f, 0.1f, 0.05f);
        var font = ThemeDB.FallbackFont;

        // Common knowledge: the posts, the wharf, the camp where it stands now.
        var fortC = M(IsoGrid.Cell(WorldLayout.FortTop, WorldLayout.FortWidth / 2, WorldLayout.FortDepth / 2));
        DrawRect(new Rect2(fortC - new Vector2(3, 3), new Vector2(6, 6)), new Color(0.5f, 0.25f, 0.1f));
        DrawString(font, fortC + new Vector2(8, 4), "York Factory", HorizontalAlignment.Left, -1, 12, ink);
        var nwcC = M(IsoGrid.Cell(WorldLayout.NwcTop, WorldLayout.NwcWidth / 2, WorldLayout.NwcDepth / 2));
        DrawRect(new Rect2(nwcC - new Vector2(3, 3), new Vector2(6, 6)), new Color(0.6f, 0.15f, 0.1f));
        DrawString(font, nwcC + new Vector2(8, 4), "Nor'Westers", HorizontalAlignment.Left, -1, 12, ink);
        var camp = M(WorldLayout.CampCentre);
        DrawColoredPolygon(new[] { camp + new Vector2(0, -7), camp + new Vector2(6, 4), camp + new Vector2(-6, 4) }, new Color(0.75f, 0.55f, 0.3f));
        DrawString(font, camp + new Vector2(9, 4), WorldLayout.IsSummerCamp ? "the band (summer camp)" : "the band (winter camp)", HorizontalAlignment.Left, -1, 12, ink);
        DrawLine(M(WorldLayout.WharfRoot), M(WorldLayout.WharfEnd), new Color(0.3f, 0.2f, 0.1f), 2f);
        var ford = M(new Vector2I(58, 160));
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
        if (Player != null && Player.Spouse != null && IsInstanceValid(Player.Spouse) && !Player.Spouse.IsDead)
        {
            var p = M(Player.Spouse.GlobalPosition);
            DrawCircle(p, 3f, new Color(0.9f, 0.5f, 0.6f));
        }
    }
}
