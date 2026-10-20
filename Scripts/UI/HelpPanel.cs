using Godot;

namespace IsometricFurTradeGame.Scripts.UI;

// H: the controls, for whoever just joined and hasn't read the doc.
public partial class HelpPanel : Control
{
    public static bool IsOpen { get; private set; }
    private PanelContainer _panel;
    public override void _Process(double delta) => IsOpen = _panel != null && _panel.Visible;

    private static string K(string action) => Core.Keybinds.KeyFor(action);

    private static string Text() =>
        $"{K("move_up")} {K("move_left")} {K("move_down")} {K("move_right")}  walk      {K("sprint")}  run      {K("sneak")}  crouch (sneak; also: set a fire deliberately)\n" +
        $"{K("interact")}  talk / bed / pick up / loot / woodpile / warehouse / ration / ship / door / look at a fire\n" +
        $"{Core.Keybinds.HotbarKeys()}  use a hotbar slot     mouse wheel + {K("hotbar_use")}  pick and use     {K("drink")}  drink\n" +
        $"Left click  strike whoever is under the cursor      {K("attack_nearest")}  strike the nearest hostile\n" +
        $"{K("wait")}  wait an hour (sleep at night)     {K("time_fast")}  hold to fast-forward     {K("chat")}  chat\n" +
        $"{K("pause_clock")}  stop/start the clock (host)     {K("clock_slower")} / {K("clock_faster")}  slower / faster clock (host; or the buttons under the date)\n" +
        $"{K("journal")}  journal      {K("log")}  log      {K("inventory")}  pack (drag items between squares; right-click uses or wears)      {K("character")}  yourself      {K("map")}  map      {K("debug_menu")}  test console (host)      F5 / F9  save / load (host)\n" +
        $"{K("restart")}  begin again after death      Esc  menu (settings, keys)      F11  full screen      Ctrl + wheel  zoom      {K("help")}  this\n\n" +
        "You are the Company's new hand at York Factory. Keep warm (a hearth, a capote), fed and watered;\n" +
        "do what the Factor sets you each morning and your standing rises. Furs are money: trap beaver\n" +
        "(steel traps, come back tomorrow), sell at the counter, trade with the band, or take the\n" +
        "Nor'Westers' better price across the river. Flint and wood make a fire in the bush.\n" +
        "Talk to people: they know things, and they remember what you do.";

    private Label _title, _body;

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
        var title = _title = new Label();
        title.AddThemeFontSizeOverride("font_size", 16);
        box.AddChild(title);
        var body = _body = new Label();
        body.AddThemeFontSizeOverride("font_size", 12);
        box.AddChild(body);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("help"))
        {
            _panel.Visible = !_panel.Visible;
            if (_panel.Visible) { _title.Text = $"York Factory, 1811 - how to live here ({K("help")})"; _body.Text = Text(); }
            GetViewport().SetInputAsHandled();
        }
        else if (_panel.Visible && @event.IsActionPressed("ui_cancel")) { _panel.Visible = false; GetViewport().SetInputAsHandled(); }
    }
}
