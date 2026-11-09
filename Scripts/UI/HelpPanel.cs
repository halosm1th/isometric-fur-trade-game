using Godot;

namespace IsometricFurTradeGame.Scripts.UI;

// H: the controls, for whoever just joined and hasn't read the doc.
public partial class HelpPanel : Control
{
    public static bool IsOpen { get; private set; }
    public static HelpPanel Instance { get; private set; }
    public Control PadRoot => _panel;
    public void Open() { _panel.Visible = true; _title.Text = $"York Factory, 1811 - how to live here ({K("help")})"; _body.Text = Text(); }
    private PanelContainer _panel;
    public override void _Process(double delta) => IsOpen = _panel != null && _panel.Visible;

    private static string K(string action) => Core.Keybinds.KeyFor(action);

    private static string Text() => Core.Pad.Active ? PadText() : KeysText();

    private static string KeysText() =>
        $"{K("move_up")} {K("move_left")} {K("move_down")} {K("move_right")}  walk      {K("sprint")}  run      {K("sneak")}  crouch (sneak; also: set a fire deliberately)\n" +
        $"{K("interact")}  talk / bed / pick up / loot / woodpile / warehouse / ration / ship / door / look at a fire\n" +
        $"{Core.Keybinds.HotbarKeys()}  use a hotbar slot     mouse wheel + {K("hotbar_use")}  pick and use     {K("drink")}  drink\n" +
        $"Left click  strike whoever is under the cursor      {K("attack_nearest")}  strike the nearest hostile\n" +
        $"{K("wait")}  wait an hour (sleep at night)     {K("time_fast")}  hold to fast-forward     {K("chat")}  chat\n" +
        $"{K("pause_clock")}  stop/start the clock (host)     {K("clock_slower")} / {K("clock_faster")}  slower / faster clock (host; or the buttons under the date)\n" +
        $"{K("journal")}  journal      {K("log")}  log      {K("inventory")}  pack (drag items between squares; right-click uses or wears)      {K("character")}  yourself      {K("map")}  map      {K("debug_menu")}  test console (host)      F5 / F9  save / load (host)\n" +
        $"{K("restart")}  begin again after death      Esc  menu (settings, keys)      F11  full screen      Ctrl + wheel  zoom      {K("help")}  this      (an Xbox controller works too: press any button on it)\n\n" + Story;

    // With the controller in hand, its own layout (as bound now: Core/Keybinds).
    private static string PadText() =>
        $"Left stick  walk      {K("sprint")}  run      {K("sneak")}  crouch (sneak; also: set a fire deliberately)\n" +
        $"{K("interact")}  talk / bed / pick up / loot / woodpile / warehouse / ration / ship / door / look at a fire\n" +
        $"{K("hotbar_prev")} / {K("hotbar_next")}  choose a hotbar slot     {K("hotbar_use")}  use it (eat, drink, read...)     {K("use_ahead")}  use what's in hand on the ground ahead, or strike\n" +
        $"Right stick  move the pointer: for a few seconds after, {K("use_ahead")} acts there instead of just ahead     {K("attack_nearest")}  strike the nearest hostile\n" +
        $"{K("wait")}  wait an hour (sleep at night)     {K("time_fast")}  hold to fast-forward     {K("mount")}  mount / get down     {K("drink")}  drink     {K("pause_clock")}  stop/start the clock (host)\n" +
        $"{K("inventory")}  your book (LT / RT turn to the journal, log, pack, yourself)      {K("map")}  map      Menu  menu (settings, controls, save)\n" +
        "In menus: d-pad or left stick to move, A to choose, B to go back, right stick to scroll. In the pack, X uses or wears and Y lifts a stack to move it.\n" +
        $"{K("restart")}  begin again after death.   Chat needs the keyboard ({Core.Keybinds.Describe("chat")}).\n\n" + Story;

    private const string Story =
        "You are the Company's new hand at York Factory. Keep warm (a hearth, a capote), fed and watered;\n" +
        "do what the Factor sets you each morning and your standing rises. Furs are money: trap beaver\n" +
        "(steel traps, come back tomorrow), sell at the counter, trade with the band, or take the\n" +
        "Nor'Westers' better price across the river. Flint and wood make a fire in the bush.\n" +
        "Talk to people: they know things, and they remember what you do.";

    private Label _title, _body;

    public override void _Ready()
    {
        Instance = this;
        _panel = new PanelContainer { Visible = false };
        _panel.SetAnchorsPreset(LayoutPreset.Center);
        _panel.GrowHorizontal = GrowDirection.Both; _panel.GrowVertical = GrowDirection.Both;
        _panel.AddThemeStyleboxOverride("panel", MenuTheme.PanelBox(20));
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
