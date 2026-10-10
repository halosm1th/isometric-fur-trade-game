using Godot;
using IsometricFurTradeGame.Scripts.Core;
using NetHub = IsometricFurTradeGame.Scripts.Net.Net;

namespace IsometricFurTradeGame.Scripts.UI;

// Esc: resume, settings, back to the main menu, quit. Alone, the clock
// stops while it's up; with others in the world it can't (the host's
// simulation is everyone's), so it just floats over the game.
public partial class PauseMenu : Control
{
    public static bool IsOpen { get; private set; }
    public static PauseMenu Instance { get; private set; }
    private PanelContainer _panel;
    private VBoxContainer _main;
    private SettingsPanel _settings;
    private Label _status;

    public override void _EnterTree() => Instance = this;
    public override void _ExitTree() { if (Instance == this) Instance = null; }

    public override void _Ready()
    {
        _panel = new PanelContainer { Visible = false };
        _panel.SetAnchorsPreset(LayoutPreset.Center);
        _panel.GrowHorizontal = GrowDirection.Both; _panel.GrowVertical = GrowDirection.Both;
        var style = new StyleBoxFlat { BgColor = new Color(0.08f, 0.07f, 0.06f, 0.96f), BorderColor = new Color(0.7f, 0.58f, 0.35f) };
        style.SetBorderWidthAll(2); style.SetCornerRadiusAll(4); style.SetContentMarginAll(16);
        _panel.AddThemeStyleboxOverride("panel", style);
        _panel.Theme = MenuTheme.Get();
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
        Button("What you mean to do...", () => { _main.Visible = false; BuildAmbitions(); _ambitions.Visible = true; });
        if (NetHub.IsServer) Button("Save (F5)", () => { SaveGame.Save(); _status.Text = SaveGame.LastMessage; });
        Button("Settings", () => { _main.Visible = false; _settings.Visible = true; _settings.Refresh(); });
        _main.AddChild(new HSeparator());
        Button("Main menu", ToMainMenu);
        Button("Exit the game", () => GetTree().Quit());

        _settings = new SettingsPanel(() => { _settings.Visible = false; _main.Visible = true; }) { Visible = false };
        _panel.AddChild(_settings);
        foreach (string arg in OS.GetCmdlineUserArgs())
        {
            if (arg == "--pause") GetTree().CreateTimer(4.0).Timeout += Open;
            if (arg == "--settings") GetTree().CreateTimer(4.0).Timeout += () => { Open(); _main.Visible = false; _settings.Visible = true; };
        }
    }

    // Pages differ in size; keep the panel fitted to whichever is up, centred.
    public override void _Process(double delta)
    {
        if (!_panel.Visible) return;
        var min = _panel.GetCombinedMinimumSize();
        if (_panel.Size != min) _panel.SetAnchorsAndOffsetsPreset(LayoutPreset.Center, LayoutPresetMode.Minsize);
    }

    // The window went to the background: alone, stop the world until you're back.
    public void OpenFromFocusLoss()
    {
        bool alone = NetHub.I == null || NetHub.I.Current == NetHub.Mode.Offline;
        if (!alone || IsOpen || Sim.Player == null) return;
        Open();
    }

    // Declaring an ambition (design doc P.2): two at most, changeable at
    // the turning points - which, for now, means whenever you sit down and
    // think about it.
    private VBoxContainer _ambitions;
    private void BuildAmbitions()
    {
        if (_ambitions != null) { _ambitions.QueueFree(); }
        _ambitions = new VBoxContainer { CustomMinimumSize = new Vector2(560, 0) };
        _ambitions.AddThemeConstantOverride("separation", 6);
        _panel.AddChild(_ambitions);
        var t = new Label { Text = "What do you mean to make of yourself?", HorizontalAlignment = HorizontalAlignment.Center };
        t.AddThemeFontSizeOverride("font_size", 18);
        _ambitions.AddChild(t);
        var p = Sim.Player as Player.PlayerController;
        if (p == null) return;
        var note = new Label { Text = "Two at most. They will pull against your orders; that is the point.", Modulate = new Color(0.75f, 0.72f, 0.65f) };
        note.AddThemeFontSizeOverride("font_size", 12);
        _ambitions.AddChild(note);
        foreach (var kind in Agents.Ambitions.Offered)
        {
            var held = p.Ambitions.Find(a => a.Kind == kind);
            var row = new HBoxContainer();
            var b = new Button { Text = (held != null ? "✓  " : "    ") + new Agents.Ambition { Kind = kind }.Title, CustomMinimumSize = new Vector2(320, 0), Alignment = HorizontalAlignment.Left };
            b.Pressed += () =>
            {
                var cal = GameCalendar.Instance;
                if (held != null) { p.Ambitions.Remove(held); Sim.NoticeLocal($"You put aside the thought of it: {held.Title.ToLower()}."); }
                else if (p.Ambitions.Count >= 2) Sim.NoticeLocal("Two is as many as a man can honestly carry.");
                else
                {
                    var a = new Agents.Ambition { Kind = kind, DeclaredDay = cal?.Day ?? 0, DeclaredYear = cal?.Year ?? 1811 };
                    p.Ambitions.Add(a);
                    p.AmbitionsDeclared = true;
                    p.Journal.Add($"{cal?.DateString}: You set yourself {a.Pledge}.");
                    Sim.NoticeLocal($"You set yourself {a.Pledge}.");
                }
                BuildAmbitions();
                _ambitions.Visible = true;
            };
            row.AddChild(b);
            var d = new Label { Text = Agents.Ambitions.Blurb(kind), AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(230, 0) };
            d.AddThemeFontSizeOverride("font_size", 11);
            d.Modulate = new Color(0.8f, 0.78f, 0.7f);
            row.AddChild(d);
            _ambitions.AddChild(row);
        }
        var back = new Button { Text = "Back" };
        back.Pressed += () => { _ambitions.Visible = false; _main.Visible = true; };
        _ambitions.AddChild(back);
    }

    private void Button(string text, System.Action act)
    {
        var b = new Button { Text = text };
        b.Pressed += act;
        _main.AddChild(b);
    }

    // Read straight from the InputMap so it can't go stale.
    public static string BindingsText()
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
                // A headless run has no keyboard to ask about the layout of.
                if (ev is InputEventKey k) keys.Add(OS.GetKeycodeString(k.PhysicalKeycode != Key.None && !Godot.DisplayServer.GetName().Contains("headless") ? DisplayServer.KeyboardGetKeycodeFromPhysical(k.PhysicalKeycode) : k.PhysicalKeycode != Key.None ? k.PhysicalKeycode : k.Keycode));
                else if (ev is InputEventMouseButton m) keys.Add($"mouse {m.ButtonIndex}");
            }
            sb.AppendLine($"{string.Join(" / ", keys),-14} {what}");
        }
        sb.AppendLine($"{"Left click",-14} strike whoever is under the cursor");
        sb.AppendLine($"{"Mouse wheel",-14} pick a hotbar slot");
        sb.AppendLine($"{"F5 / F9",-14} save / load (host)");
        sb.AppendLine($"{"Esc",-14} this menu / close a menu");
        sb.AppendLine($"{"F11",-14} full screen on / off");
        sb.AppendLine($"{"Ctrl + wheel",-14} zoom the view (Ctrl + = / - / 0 too)");
        return sb.ToString();
    }

    private static bool AnotherMenuOpen =>
        (DialogueMenu.Instance != null && DialogueMenu.Instance.IsOpen) || (ChatBox.Instance != null && ChatBox.Instance.IsTyping) ||
        JournalPanel.IsOpen || MapPanel.IsOpen || HelpPanel.IsOpen || DebugMenu.IsOpenStatic;

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!@event.IsActionPressed("ui_cancel")) return;
        if (_panel.Visible) { if (_settings.Visible) { _settings.Visible = false; _main.Visible = true; } else if (_ambitions != null && _ambitions.Visible) { _ambitions.Visible = false; _main.Visible = true; } else Close(); GetViewport().SetInputAsHandled(); return; }
        if (AnotherMenuOpen) return;   // Esc closes that first
        Open();
        GetViewport().SetInputAsHandled();
    }

    private void Open()
    {
        _panel.Visible = true; _main.Visible = true; _settings.Visible = false; if (_ambitions != null) _ambitions.Visible = false; IsOpen = true;
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
