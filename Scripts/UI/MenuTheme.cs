using Godot;

namespace IsometricFurTradeGame.Scripts.UI;

// The look shared by the main menu, the Esc menu, the console and the
// panels: dark boards and iron for the frames, tan leather for the
// buttons, parchment for anything meant to be paper. The art is cut from
// the UI sheet (tools/sprites/panels.py) into nine-patch styleboxes; if a
// piece of it is missing the old painted box is used instead, so the menus
// never come up blank.
public static class MenuTheme
{
    public static readonly Color Ink = new(0.95f, 0.91f, 0.82f);
    public static readonly Color Dim = new(0.72f, 0.68f, 0.6f);
    public static readonly Color Brass = new(0.78f, 0.63f, 0.36f);
    public static readonly Color Panel = new(0.08f, 0.07f, 0.06f, 0.94f);
    // Writing on leather or parchment is dark; writing on boards is light.
    public static readonly Color DarkInk = new(0.16f, 0.1f, 0.05f);

    private const string Art = "res://assets/ui/panels/";

    private static Texture2D Tex(string name)
    {
        string path = Art + name + ".png";
        return ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
    }

    // A nine-patch of one of the drawn pieces: `edge` is how much of it is
    // frame rather than middle, and the content margins are where text sits
    // inside it.
    private static StyleBoxTexture Nine(string name, int edge, int padX, int padY, Color? modulate = null)
    {
        var tex = Tex(name);
        if (tex == null) return null;
        var s = new StyleBoxTexture { Texture = tex };
        s.TextureMarginLeft = s.TextureMarginRight = s.TextureMarginTop = s.TextureMarginBottom = edge;
        s.ContentMarginLeft = s.ContentMarginRight = padX;
        s.ContentMarginTop = s.ContentMarginBottom = padY;
        if (modulate.HasValue) s.ModulateColor = modulate.Value;
        return s;
    }

    private static StyleBoxFlat Flat(Color bg, Color border, int bw = 1, int padX = 12, int padY = 6)
    {
        var b = new StyleBoxFlat { BgColor = bg, BorderColor = border };
        b.SetBorderWidthAll(bw); b.SetCornerRadiusAll(3);
        b.ContentMarginLeft = padX; b.ContentMarginRight = padX;
        b.ContentMarginTop = padY; b.ContentMarginBottom = padY;
        return b;
    }

    private static Theme _theme;
    public static Theme Get()
    {
        if (_theme != null) return _theme;
        var t = new Theme();
        t.DefaultFontSize = 15;

        // ---- buttons: tan leather, dark leather when pressed -------------
        StyleBox normal = Nine("button", 15, 14, 7) ?? (StyleBox)Flat(new Color(0.16f, 0.13f, 0.1f), new Color(0.42f, 0.34f, 0.22f));
        StyleBox hover = Nine("button", 15, 14, 7, new Color(1.14f, 1.1f, 1.02f)) ?? (StyleBox)Flat(new Color(0.24f, 0.19f, 0.13f), Brass);
        StyleBox pressed = Nine("button_pressed", 15, 14, 7) ?? (StyleBox)Flat(new Color(0.11f, 0.09f, 0.07f), Brass);
        StyleBox disabled = Nine("button_disabled", 15, 14, 7) ?? (StyleBox)Flat(new Color(0.12f, 0.11f, 0.1f), new Color(0.25f, 0.22f, 0.18f));
        var focus = Flat(new Color(0, 0, 0, 0), new Color(0.95f, 0.8f, 0.5f), 2);
        bool leather = Tex("button") != null;
        foreach (var type in new[] { "Button", "OptionButton" })
        {
            t.SetStylebox("normal", type, normal);
            t.SetStylebox("hover", type, hover);
            t.SetStylebox("pressed", type, pressed);
            t.SetStylebox("hover_pressed", type, pressed);
            t.SetStylebox("disabled", type, disabled);
            t.SetStylebox("focus", type, focus);
            t.SetColor("font_color", type, leather ? DarkInk : Ink);
            t.SetColor("font_hover_color", type, leather ? new Color(0.1f, 0.06f, 0.03f) : new Color(1f, 0.92f, 0.7f));
            t.SetColor("font_pressed_color", type, leather ? new Color(0.97f, 0.92f, 0.8f) : Brass);
            t.SetColor("font_focus_color", type, leather ? DarkInk : new Color(1f, 0.92f, 0.7f));
            t.SetColor("font_disabled_color", type, leather ? new Color(0.42f, 0.4f, 0.38f) : new Color(0.5f, 0.47f, 0.42f));
        }
        t.SetColor("font_color", "Label", Ink);
        foreach (var st in new[] { "normal", "hover", "pressed", "hover_pressed", "focus", "disabled" }) t.SetStylebox(st, "CheckButton", new StyleBoxEmpty());
        t.SetColor("font_color", "CheckButton", Ink);
        t.SetColor("font_hover_color", "CheckButton", new Color(1f, 0.92f, 0.7f));

        var field = Flat(new Color(0.05f, 0.045f, 0.04f), new Color(0.42f, 0.34f, 0.22f));
        t.SetStylebox("normal", "LineEdit", field);
        t.SetStylebox("focus", "LineEdit", Flat(new Color(0.05f, 0.045f, 0.04f), Brass));
        t.SetColor("font_color", "LineEdit", Ink);

        // ---- tabs: slips of parchment on the boards ----------------------
        StyleBox tabPanel = Nine("panel_wood", 58, 16, 14) ?? (StyleBox)Flat(new Color(0.11f, 0.095f, 0.08f), new Color(0.42f, 0.34f, 0.22f));
        StyleBox tabOff = Nine("tab_off", 15, 14, 6, new Color(0.82f, 0.78f, 0.72f)) ?? (StyleBox)Flat(new Color(0.09f, 0.08f, 0.065f), new Color(0.3f, 0.25f, 0.18f));
        StyleBox tabOn = Nine("tab_on", 15, 14, 6) ?? (StyleBox)Flat(new Color(0.11f, 0.095f, 0.08f), Brass);
        StyleBox tabHover = Nine("tab_off", 15, 14, 6, new Color(0.95f, 0.92f, 0.86f)) ?? (StyleBox)Flat(new Color(0.16f, 0.13f, 0.1f), Brass);
        bool paperTabs = Tex("tab_on") != null;
        t.SetStylebox("panel", "TabContainer", tabPanel);
        t.SetStylebox("tab_unselected", "TabContainer", tabOff);
        t.SetStylebox("tab_hovered", "TabContainer", tabHover);
        t.SetStylebox("tab_selected", "TabContainer", tabOn);
        t.SetColor("font_selected_color", "TabContainer", paperTabs ? DarkInk : new Color(1f, 0.92f, 0.7f));
        t.SetColor("font_unselected_color", "TabContainer", paperTabs ? new Color(0.34f, 0.27f, 0.2f) : Dim);
        t.SetColor("font_hovered_color", "TabContainer", paperTabs ? DarkInk : Ink);

        // ---- sliders and scrollbars --------------------------------------
        var grabber = new StyleBoxFlat { BgColor = Brass }; grabber.SetCornerRadiusAll(3);
        grabber.ContentMarginTop = 3; grabber.ContentMarginBottom = 3;
        var track = new StyleBoxFlat { BgColor = new Color(0.05f, 0.045f, 0.04f) }; track.SetCornerRadiusAll(3);
        track.ContentMarginTop = 3; track.ContentMarginBottom = 3;
        t.SetStylebox("slider", "HSlider", track);
        t.SetStylebox("grabber_area", "HSlider", grabber);
        t.SetStylebox("grabber_area_highlight", "HSlider", grabber);
        var rail = Nine("scrollbar", 24, 0, 0);
        if (rail != null)
        {
            t.SetStylebox("scroll", "VScrollBar", rail);
            t.SetStylebox("grabber", "VScrollBar", grabber);
            t.SetStylebox("grabber_highlight", "VScrollBar", grabber);
            t.SetStylebox("grabber_pressed", "VScrollBar", grabber);
        }

        var rule = Nine("divider", 90, 0, 5);
        if (rule != null) t.SetStylebox("separator", "HSeparator", rule);
        t.SetConstant("separation", "HSeparator", 12);
        _theme = t;
        return t;
    }

    // The frame round a panel: boards and iron, which keeps light text on it
    // readable.
    public static StyleBox PanelBox(int margin = 18)
    {
        var nine = Nine("panel_wood", 58, margin, margin);
        if (nine != null) return nine;
        var s = new StyleBoxFlat { BgColor = Panel, BorderColor = Brass };
        s.SetBorderWidthAll(2); s.SetCornerRadiusAll(4); s.SetContentMarginAll(margin);
        s.ShadowColor = new Color(0, 0, 0, 0.5f); s.ShadowSize = 12;
        return s;
    }

    // And the frame round anything that is meant to be paper - a map, a
    // letter, a page of a book. Write on it in DarkInk.
    public static StyleBox PaperBox(int margin = 18)
    {
        var nine = Nine("panel_paper", 56, margin, margin);
        if (nine != null) return nine;
        var s = new StyleBoxFlat { BgColor = new Color(0.86f, 0.78f, 0.6f, 0.97f), BorderColor = new Color(0.4f, 0.3f, 0.15f) };
        s.SetBorderWidthAll(2); s.SetCornerRadiusAll(4); s.SetContentMarginAll(margin);
        return s;
    }
}
