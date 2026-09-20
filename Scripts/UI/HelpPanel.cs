using Godot;

namespace IsometricFurTradeGame.Scripts.UI;

// H: the controls, for whoever just joined and hasn't read the doc.
public partial class HelpPanel : Control
{
    public static bool IsOpen { get; private set; }
    private PanelContainer _panel;
    public override void _Process(double delta) => IsOpen = _panel != null && _panel.Visible;

    private const string Text =
        "W A S D  walk      Shift  run      Ctrl  crouch (sneak; also: set a fire deliberately)\n" +
        "E  talk / bed / pick up / loot / woodpile / warehouse / ration / ship / door / look at a fire\n" +
        "1-9  use a hotbar slot     mouse wheel + Enter  pick and use     Q  drink\n" +
        "Left click  strike whoever is under the cursor      F  strike the nearest hostile\n" +
        "Z  wait an hour (sleep at night)     T  hold to fast-forward     Tab  chat\n" +
        "Space  stop/start the clock (host)     [ / ]  slower / faster clock (host)\n" +
        "J  journal      M  map      F1  test console (host)      F5 / F9  save / load (host)\n" +
        "R  begin again after death      Esc  close a menu      H  this\n\n" +
        "You are the Company's new hand at York Factory. Keep warm (a hearth, a capote), fed and watered;\n" +
        "do what the Factor sets you each morning and your standing rises. Furs are money: trap beaver\n" +
        "(steel traps, come back tomorrow), sell at the counter, trade with the band, or take the\n" +
        "Nor'Westers' better price across the river. Flint and wood make a fire in the bush.\n" +
        "Talk to people: they know things, and they remember what you do.";

    public override void _Ready()
    {
        _panel = new PanelContainer { Visible = false };
        _panel.SetAnchorsPreset(LayoutPreset.Center);
        _panel.GrowHorizontal = GrowDirection.Both; _panel.GrowVertical = GrowDirection.Both;
        var style = new StyleBoxFlat { BgColor = new Color(0.1f, 0.09f, 0.08f, 0.94f), BorderColor = new Color(0.7f, 0.58f, 0.35f) };
        style.SetBorderWidthAll(2); style.SetCornerRadiusAll(4); style.SetContentMarginAll(14);
        _panel.AddThemeStyleboxOverride("panel", style);
        AddChild(_panel);
        var box = new VBoxContainer();
        _panel.AddChild(box);
        var title = new Label { Text = "York Factory, 1811 - how to live here (H)" };
        title.AddThemeFontSizeOverride("font_size", 16);
        box.AddChild(title);
        var body = new Label { Text = Text };
        body.AddThemeFontSizeOverride("font_size", 12);
        box.AddChild(body);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("help")) { _panel.Visible = !_panel.Visible; GetViewport().SetInputAsHandled(); }
        else if (_panel.Visible && @event.IsActionPressed("ui_cancel")) { _panel.Visible = false; GetViewport().SetInputAsHandled(); }
    }
}
