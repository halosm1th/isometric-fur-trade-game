using Godot;
using IsometricFurTradeGame.Scripts.Core;
using NetHub = IsometricFurTradeGame.Scripts.Net.Net;

namespace IsometricFurTradeGame.Scripts.UI;

// Esc: resume, key bindings, back to the main menu, quit. Alone, the clock
// stops while it's up; with others in the world it can't (the host's
// simulation is everyone's), so it just floats over the game.
public partial class PauseMenu : Control
{
    public static bool IsOpen { get; private set; }
    private PanelContainer _panel;
    private VBoxContainer _main, _keys;
    private Label _status;

    public override void _Ready()
    {
        _panel = new PanelContainer { Visible = false };
        _panel.SetAnchorsPreset(LayoutPreset.Center);
        _panel.GrowHorizontal = GrowDirection.Both; _panel.GrowVertical = GrowDirection.Both;
        var style = new StyleBoxFlat { BgColor = new Color(0.08f, 0.07f, 0.06f, 0.96f), BorderColor = new Color(0.7f, 0.58f, 0.35f) };
        style.SetBorderWidthAll(2); style.SetCornerRadiusAll(4); style.SetContentMarginAll(16);
        _panel.AddThemeStyleboxOverride("panel", style);
        AddChild(_panel);

        _main = new VBoxContainer { CustomMinimumSize = new Vector2(360, 0) };
        _main.AddThemeConstantOverride("separation", 8);
        _panel.AddChild(_main);
        var title = new Label { Text = "York Factory, 1811", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 22);
        _main.AddChild(title);
        _status = new Label { HorizontalAlignment = HorizontalAlignment.Center, Modulate = new Color(0.8f, 0.75f, 0.65f) };
        _status.AddThemeFontSizeOverride("font_size", 12);
        _main.AddChild(_status);
        _main.AddChild(new HSeparator());
        Button("Resume", Close);
        Button("Key bindings", () => { _main.Visible = false; _keys.Visible = true; });
        if (NetHub.IsServer) Button("Save (F5)", () => { SaveGame.Save(); _status.Text = SaveGame.LastMessage; });
        Button(Settings.Label, () => { Settings.Toggle(); RefreshLabels(); });
        Button("Main menu", ToMainMenu);
        Button("Exit the game", () => GetTree().Quit());

        _keys = new VBoxContainer { Visible = false, CustomMinimumSize = new Vector2(420, 0) };
        _panel.AddChild(_keys);
        var kt = new Label { Text = "Key bindings", HorizontalAlignment = HorizontalAlignment.Center };
        kt.AddThemeFontSizeOverride("font_size", 18);
        _keys.AddChild(kt);
        var list = new Label { Text = BindingsText() };
        list.AddThemeFontSizeOverride("font_size", 12);
        _keys.AddChild(list);
        var note = new Label { Text = "Bindings live in project.godot [input]; a rebinding screen is on the list.", Modulate = new Color(0.6f, 0.6f, 0.6f) };
        note.AddThemeFontSizeOverride("font_size", 11);
        _keys.AddChild(note);
        var back = new Button { Text = "Back" };
        back.Pressed += () => { _keys.Visible = false; _main.Visible = true; };
        _keys.AddChild(back);
        foreach (string arg in OS.GetCmdlineUserArgs()) if (arg == "--pause") GetTree().CreateTimer(4.0).Timeout += Open;
    }

    private void Button(string text, System.Action act)
    {
        var b = new Button { Text = text };
        b.Pressed += act;
        _main.AddChild(b);
    }

    private void RefreshLabels()
    {
        foreach (var c in _main.GetChildren()) if (c is Button b && b.Text.StartsWith("Full screen")) b.Text = Settings.Label;
    }

    // Read straight from the InputMap so it can't go stale.
    private static string BindingsText()
    {
        var sb = new System.Text.StringBuilder();
        var names = new (string action, string what)[]
        {
            ("move_up", "walk up"), ("move_down", "walk down"), ("move_left", "walk left"), ("move_right", "walk right"),
            ("sprint", "run"), ("sneak", "crouch / sneak (and deliberate arson with flint)"), ("interact", "talk / use what's in front of you"),
            ("attack_nearest", "strike the nearest hostile"), ("drink", "drink"), ("wait", "wait an hour / sleep at night"),
            ("time_fast", "hold to fast-forward"), ("pause_clock", "stop / start the clock (host)"), ("clock_slower", "slower clock (host)"), ("clock_faster", "faster clock (host)"), ("hotbar_use", "use the selected hotbar slot"), ("hotbar_1", "hotbar 1 (… to 9)"),
            ("chat", "chat"), ("journal", "journal"), ("log", "the log"), ("inventory", "your pack"), ("map", "map"), ("help", "help"),
            ("debug_menu", "test console (host)"), ("restart", "begin again after death"),
        };
        foreach (var (action, what) in names)
        {
            if (!InputMap.HasAction(action)) continue;
            var keys = new System.Collections.Generic.List<string>();
            foreach (var ev in InputMap.ActionGetEvents(action))
            {
                if (ev is InputEventKey k) keys.Add(OS.GetKeycodeString(k.PhysicalKeycode != Key.None ? DisplayServer.KeyboardGetKeycodeFromPhysical(k.PhysicalKeycode) : k.Keycode));
                else if (ev is InputEventMouseButton m) keys.Add($"mouse {m.ButtonIndex}");
            }
            sb.AppendLine($"{string.Join(" / ", keys),-14} {what}");
        }
        sb.AppendLine($"{"Left click",-14} strike whoever is under the cursor");
        sb.AppendLine($"{"Mouse wheel",-14} pick a hotbar slot");
        sb.AppendLine($"{"F5 / F9",-14} save / load (host)");
        sb.AppendLine($"{"Esc",-14} this menu / close a menu");
        return sb.ToString();
    }

    private static bool AnotherMenuOpen =>
        (DialogueMenu.Instance != null && DialogueMenu.Instance.IsOpen) || (ChatBox.Instance != null && ChatBox.Instance.IsTyping) ||
        JournalPanel.IsOpen || MapPanel.IsOpen || HelpPanel.IsOpen || DebugMenu.IsOpenStatic;

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F11 }) { Settings.Toggle(); RefreshLabels(); GetViewport().SetInputAsHandled(); return; }
        if (!@event.IsActionPressed("ui_cancel")) return;
        if (_panel.Visible) { if (_keys.Visible) { _keys.Visible = false; _main.Visible = true; } else Close(); GetViewport().SetInputAsHandled(); return; }
        if (AnotherMenuOpen) return;   // Esc closes that first
        Open();
        GetViewport().SetInputAsHandled();
    }

    private void Open()
    {
        _panel.Visible = true; _main.Visible = true; _keys.Visible = false; IsOpen = true;
        bool alone = NetHub.I == null || NetHub.I.Current == NetHub.Mode.Offline;
        _status.Text = alone ? "Paused." : NetHub.IsServer ? "You are hosting: the world goes on while this is up." : "The host's world goes on while this is up.";
        // Not SceneTree.Paused: interpolated bodies vanish under it. Time simply stops.
        if (alone) Engine.TimeScale = 0.0;
    }

    private void Close()
    {
        _panel.Visible = false; IsOpen = false;
        Engine.TimeScale = NetHub.HostPaused ? 0.0 : 1.0;
    }

    private void ToMainMenu()
    {
        Engine.TimeScale = 1.0; IsOpen = false;
        NetHub.I?.Leave();
        GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, "res://Scenes/UI/MainMenu.tscn");
    }
}
