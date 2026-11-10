using System;
using Godot;
using IsometricFurTradeGame.Scripts.Core;

namespace IsometricFurTradeGame.Scripts.UI;

// The observer's key (Core/Observer): what each mark on the country means,
// drawn with the same shapes and colours UI/ObserverView draws them, and the
// keys. The help key (H) brings it up and puts it away while watching.
public partial class ObserverKey : PanelContainer
{
    public static readonly Color Hbc = new(0.85f, 0.22f, 0.2f), Nwc = new(0.25f, 0.65f, 0.35f), Amer = new(0.35f, 0.5f, 0.95f);

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        AnchorLeft = 0f; AnchorRight = 0f; AnchorTop = 0f; AnchorBottom = 0f;
        OffsetLeft = 12f; OffsetTop = 160f;
        var box = new StyleBoxFlat { BgColor = new Color(0.07f, 0.06f, 0.05f, 0.88f), BorderColor = new Color(0.55f, 0.45f, 0.3f), ContentMarginLeft = 12, ContentMarginRight = 14, ContentMarginTop = 8, ContentMarginBottom = 10 };
        box.SetBorderWidthAll(1); box.SetCornerRadiusAll(4);
        AddThemeStyleboxOverride("panel", box);

        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 3);
        AddChild(col);
        var title = new Label { Text = $"Key   ({Keybinds.KeyFor("help")} to close)" };
        title.AddThemeColorOverride("font_color", new Color(0.85f, 0.72f, 0.48f));
        col.AddChild(title);

        void Row(string text, Action<Swatch> draw)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 10);
            row.AddChild(new Swatch { Paint = draw, CustomMinimumSize = new Vector2(26, 20) });
            var l = new Label { Text = text };
            l.AddThemeFontSizeOverride("font_size", 14);
            row.AddChild(l);
            col.AddChild(row);
        }
        void Section(string text)
        {
            var l = new Label { Text = text };
            l.AddThemeFontSizeOverride("font_size", 12);
            l.AddThemeColorOverride("font_color", new Color(0.6f, 0.55f, 0.47f));
            col.AddChild(l);
        }

        Section("Zoomed out (these marks appear once you pull back)");
        Row("Hudson's Bay post", s => s.Square(Hbc));
        Row("North West Company post", s => s.Square(Nwc));
        Row("American post", s => s.Square(Amer));
        Row("Camp of a nation", s => s.Camp(5f));
        Row("Family camp", s => s.Camp(3f));
        Row("Brigade on the rivers (company colour)", s => s.Triangle(Hbc));
        Row("Trading party going in to a post", s => s.Party(new Color(1f, 0.85f, 0.3f), 3.5f));
        Row("War party, line to its target", s => s.Party(new Color(0.9f, 0.15f, 0.1f), 5f));
        Row("Horse thieves", s => s.Dot(new Color(1f, 0.5f, 0.1f), 4f));
        Row("Buffalo run", s => s.Ring(new Color(0.9f, 0.55f, 0.2f), 8f, 2.5f));
        Row("Buffalo herd", s => s.Dot(new Color(0.45f, 0.3f, 0.18f, 0.8f), 8f));
        Row("Other herd (caribou, deer...)", s => s.Dot(new Color(0.75f, 0.75f, 0.7f, 0.75f), 8f));
        Section("Any zoom");
        Row("What the camera is following", s => s.Ring(Colors.White, 8f, 2f));
        Row("The person you clicked", s => s.Ring(new Color(1f, 0.9f, 0.5f, 0.9f), 8f, 2f));
        Section("Keys");
        var keys = new Label
        {
            Text = "Click someone: their particulars     Tab: the next thing happening\n"
                + "WASD fly (Shift faster)     wheel or + / -: zoom     Home: York\n"
                + $"[ ]: the clock, Space stops it     M: the map     {Keybinds.KeyFor("help")}: this key",
        };
        keys.AddThemeFontSizeOverride("font_size", 13);
        col.AddChild(keys);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e.IsActionPressed("help")) { Visible = !Visible; GetViewport().SetInputAsHandled(); }
        else if (Visible && e.IsActionPressed("ui_cancel")) { Visible = false; GetViewport().SetInputAsHandled(); }
    }

    public partial class Swatch : Control
    {
        public Action<Swatch> Paint;
        private Vector2 C => Size / 2f;
        public override void _Draw() => Paint?.Invoke(this);
        public void Square(Color c)
        {
            var r = new Rect2(C - Vector2.One * 6f, Vector2.One * 12f);
            DrawRect(r, c); DrawRect(r, new Color(0.1f, 0.08f, 0.06f), false, 1.2f);
        }
        public void Camp(float r)
        {
            DrawCircle(C, r + 1f, new Color(0.95f, 0.85f, 0.55f));
            DrawArc(C, r + 1f, 0f, Mathf.Tau, 12, new Color(0.25f, 0.18f, 0.1f), 1.2f);
        }
        public void Triangle(Color c)
        {
            float r = 7f;
            var tri = new[] { C + new Vector2(0, -r), C + new Vector2(r * 0.9f, r * 0.6f), C + new Vector2(-r * 0.9f, r * 0.6f) };
            DrawColoredPolygon(tri, c);
            DrawPolyline(new[] { tri[0], tri[1], tri[2], tri[0] }, Colors.White, 1.2f);
        }
        public void Party(Color c, float r)
        {
            DrawLine(C, C + new Vector2(12f, -6f), new Color(c, 0.45f), 1.5f);
            DrawCircle(C, r, c);
        }
        public void Dot(Color c, float r) => DrawCircle(C, r, c);
        public void Ring(Color c, float r, float w) => DrawArc(C, r, 0f, Mathf.Tau, 24, c, w);
    }
}
