using Godot;

namespace IsometricFurTradeGame.Scripts.UI;

// The look shared by the main menu, the Esc menu and the settings screen:
// dark wood and brass, warm text.
public static class MenuTheme
{
    public static readonly Color Ink = new(0.95f, 0.91f, 0.82f);
    public static readonly Color Dim = new(0.72f, 0.68f, 0.6f);
    public static readonly Color Brass = new(0.78f, 0.63f, 0.36f);
    public static readonly Color Panel = new(0.08f, 0.07f, 0.06f, 0.94f);

    private static Theme _theme;
    public static Theme Get()
    {
        if (_theme != null) return _theme;
        var t = new Theme();
        t.DefaultFontSize = 15;

        StyleBoxFlat Box(Color bg, Color border, int bw = 1)
        {
            var b = new StyleBoxFlat { BgColor = bg, BorderColor = border };
            b.SetBorderWidthAll(bw); b.SetCornerRadiusAll(3);
            b.ContentMarginLeft = 12; b.ContentMarginRight = 12; b.ContentMarginTop = 6; b.ContentMarginBottom = 6;
            return b;
        }
        var normal = Box(new Color(0.16f, 0.13f, 0.1f), new Color(0.42f, 0.34f, 0.22f));
        var hover = Box(new Color(0.24f, 0.19f, 0.13f), Brass);
        var pressed = Box(new Color(0.11f, 0.09f, 0.07f), Brass);
        var disabled = Box(new Color(0.12f, 0.11f, 0.1f), new Color(0.25f, 0.22f, 0.18f));
        var focus = Box(new Color(0, 0, 0, 0), new Color(0.95f, 0.8f, 0.5f), 2);
        foreach (var type in new[] { "Button", "OptionButton" })
        {
            t.SetStylebox("normal", type, normal);
            t.SetStylebox("hover", type, hover);
            t.SetStylebox("pressed", type, pressed);
            t.SetStylebox("hover_pressed", type, pressed);
            t.SetStylebox("disabled", type, disabled);
            t.SetStylebox("focus", type, focus);
            t.SetColor("font_color", type, Ink);
            t.SetColor("font_hover_color", type, new Color(1f, 0.92f, 0.7f));
            t.SetColor("font_pressed_color", type, Brass);
            t.SetColor("font_focus_color", type, new Color(1f, 0.92f, 0.7f));
            t.SetColor("font_disabled_color", type, new Color(0.5f, 0.47f, 0.42f));
        }
        t.SetColor("font_color", "Label", Ink);
        foreach (var st in new[] { "normal", "hover", "pressed", "hover_pressed", "focus", "disabled" }) t.SetStylebox(st, "CheckButton", new StyleBoxEmpty());
        t.SetColor("font_color", "CheckButton", Ink);
        t.SetColor("font_hover_color", "CheckButton", new Color(1f, 0.92f, 0.7f));

        var field = Box(new Color(0.05f, 0.045f, 0.04f), new Color(0.42f, 0.34f, 0.22f));
        t.SetStylebox("normal", "LineEdit", field);
        t.SetStylebox("focus", "LineEdit", Box(new Color(0.05f, 0.045f, 0.04f), Brass));
        t.SetColor("font_color", "LineEdit", Ink);

        var tabPanel = new StyleBoxFlat { BgColor = new Color(0.11f, 0.095f, 0.08f), BorderColor = new Color(0.42f, 0.34f, 0.22f) };
        tabPanel.SetBorderWidthAll(1); tabPanel.SetCornerRadiusAll(3);
        t.SetStylebox("panel", "TabContainer", tabPanel);
        var tabOff = Box(new Color(0.09f, 0.08f, 0.065f), new Color(0.3f, 0.25f, 0.18f));
        var tabOn = Box(new Color(0.11f, 0.095f, 0.08f), Brass);
        t.SetStylebox("tab_unselected", "TabContainer", tabOff);
        t.SetStylebox("tab_hovered", "TabContainer", Box(new Color(0.16f, 0.13f, 0.1f), Brass));
        t.SetStylebox("tab_selected", "TabContainer", tabOn);
        t.SetColor("font_selected_color", "TabContainer", new Color(1f, 0.92f, 0.7f));
        t.SetColor("font_unselected_color", "TabContainer", Dim);
        t.SetColor("font_hovered_color", "TabContainer", Ink);

        var grabber = new StyleBoxFlat { BgColor = Brass }; grabber.SetCornerRadiusAll(3);
        grabber.ContentMarginTop = 3; grabber.ContentMarginBottom = 3;
        var track = new StyleBoxFlat { BgColor = new Color(0.05f, 0.045f, 0.04f) }; track.SetCornerRadiusAll(3);
        track.ContentMarginTop = 3; track.ContentMarginBottom = 3;
        t.SetStylebox("slider", "HSlider", track);
        t.SetStylebox("grabber_area", "HSlider", grabber);
        t.SetStylebox("grabber_area_highlight", "HSlider", grabber);

        t.SetConstant("separation", "HSeparator", 12);
        _theme = t;
        return t;
    }

    public static StyleBoxFlat PanelBox(int margin = 18)
    {
        var s = new StyleBoxFlat { BgColor = Panel, BorderColor = Brass };
        s.SetBorderWidthAll(2); s.SetCornerRadiusAll(4); s.SetContentMarginAll(margin);
        s.ShadowColor = new Color(0, 0, 0, 0.5f); s.ShadowSize = 12;
        return s;
    }
}
