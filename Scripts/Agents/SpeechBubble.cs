using Godot;

namespace IsometricFurTradeGame.Scripts.Agents;

// A line said out loud, over the speaker's head for a few seconds
// (design doc P.4, P.9). Anyone near enough to see it can read it.
public partial class SpeechBubble : Node2D
{
    private const float Width = 250f;
    private Label _label;
    private float _ttl;

    public override void _Ready()
    {
        var box = new StyleBoxFlat
        {
            BgColor = new Color(0.07f, 0.06f, 0.05f, 0.82f),
            BorderColor = new Color(0.85f, 0.78f, 0.6f, 0.5f),
            ContentMarginLeft = 7, ContentMarginRight = 7, ContentMarginTop = 3, ContentMarginBottom = 4,
        };
        box.SetBorderWidthAll(1);
        box.SetCornerRadiusAll(4);
        _label = new Label
        {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            HorizontalAlignment = HorizontalAlignment.Center,
            Size = new Vector2(Width, 0),
        };
        _label.AddThemeStyleboxOverride("normal", box);
        _label.AddThemeFontSizeOverride("font_size", 13);
        _label.AddThemeColorOverride("font_color", new Color(0.96f, 0.93f, 0.85f));
        AddChild(_label);
        Visible = false;
    }

    public void Say(string text)
    {
        if (_label == null) return;
        _label.Text = text;
        _label.Size = new Vector2(Width, 0);
        float h = _label.GetMinimumSize().Y;
        _label.Position = new Vector2(-Width / 2f, -h);
        _ttl = (3.5f + text.Length * 0.045f) * Core.Settings.BubbleTime;
        Modulate = Colors.White;
        Visible = true;
    }

    public override void _Process(double delta)
    {
        if (!Visible) return;
        _ttl -= (float)delta;
        if (_ttl <= 0f) { Visible = false; return; }
        if (_ttl < 0.6f) Modulate = new Color(1f, 1f, 1f, _ttl / 0.6f);
    }
}
