using Godot;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Player;

namespace IsometricFurTradeGame.Scripts.UI;

// J: what you have seen with your own eyes, dated. Nothing you weren't
// there for (design doc 10).
public partial class JournalPanel : Control
{
    private PanelContainer _panel;
    private Label _text;
    private PlayerController _player;

    public override void _Ready()
    {
        _player = Sim.Player as PlayerController;
        Sim.LocalPlayerChanged += p => _player = p;
        _panel = new PanelContainer { Visible = false };
        _panel.SetAnchorsPreset(LayoutPreset.CenterLeft);
        _panel.GrowVertical = GrowDirection.Both;
        _panel.Position = new Vector2(16, 0);
        var style = new StyleBoxFlat { BgColor = new Color(0.12f, 0.09f, 0.06f, 0.94f), BorderColor = new Color(0.7f, 0.58f, 0.35f) };
        style.SetBorderWidthAll(2); style.SetCornerRadiusAll(4); style.SetContentMarginAll(12);
        _panel.AddThemeStyleboxOverride("panel", style);
        AddChild(_panel);
        var box = new VBoxContainer { CustomMinimumSize = new Vector2(460, 0) };
        _panel.AddChild(box);
        var title = new Label { Text = "Journal (J)" }; title.AddThemeFontSizeOverride("font_size", 16); box.AddChild(title);
        _text = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(440, 0) };
        _text.AddThemeFontSizeOverride("font_size", 12);
        box.AddChild(_text);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("journal")) { _panel.Visible = !_panel.Visible; GetViewport().SetInputAsHandled(); }
    }

    public override void _Process(double delta)
    {
        if (!_panel.Visible || _player == null) return;
        var lines = _player.Journal;
        var sb = new System.Text.StringBuilder();
        int start = System.Math.Max(0, lines.Count - 18);
        for (int i = lines.Count - 1; i >= start; i--) sb.AppendLine(lines[i]);
        _text.Text = sb.Length == 0 ? "Nothing yet worth the ink." : sb.ToString();
    }
}
