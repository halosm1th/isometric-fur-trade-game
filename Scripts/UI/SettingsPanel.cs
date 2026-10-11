using System;
using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Core;

namespace IsometricFurTradeGame.Scripts.UI;

// The settings screen, the same one from the main menu and from Esc in the
// game. Every change takes effect and is saved the moment it is made.
public partial class SettingsPanel : VBoxContainer
{
    private static readonly List<SettingsPanel> Live = new();
    private readonly Action _back;
    private readonly List<Action> _refreshers = new();
    private bool _refreshing;

    public SettingsPanel() { }
    public SettingsPanel(Action back) { _back = back; }

    private TabContainer _tabs;
    public void ShowTab(int i) { if (_tabs != null) _tabs.CurrentTab = i; }

    public static void RefreshAll() { foreach (var p in Live) if (IsInstanceValid(p)) p.Refresh(); }

    public override void _EnterTree() => Live.Add(this);
    public override void _ExitTree() => Live.Remove(this);

    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(560, 0);
        AddThemeConstantOverride("separation", 10);

        var title = new Label { Text = "Settings", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 22);
        AddChild(title);

        var tabs = _tabs = new TabContainer { CustomMinimumSize = new Vector2(0, 360) };
        AddChild(tabs);

        var display = Page(tabs, "Display");
        Choice(display, "Window", new[] { Settings.Mode.Windowed, Settings.Mode.Fullscreen, Settings.Mode.Exclusive },
            Settings.ModeName, () => Settings.WindowMode, Settings.SetWindowMode, "F11 switches between windowed and full screen anywhere.");
        var sizes = new List<int>(); for (int i = 0; i < Settings.WindowSizes.Length; i++) sizes.Add(i);
        Choice(display, "Window size", sizes.ToArray(), i => $"{Settings.WindowSizes[i].X} x {Settings.WindowSizes[i].Y}",
            () => Settings.WindowSizeIndex, Settings.SetWindowSize, "Used when windowed. You can also drag the window's edges.");
        Toggle(display, "Vertical sync", () => Settings.VSync, Settings.SetVSync, "Stops tearing; caps the frame rate at the monitor's.");
        Choice(display, "Frame rate limit", Settings.FpsCaps, f => f == 0 ? "Unlimited" : $"{f} fps", () => Settings.MaxFps, Settings.SetMaxFps, null);

        var view = Page(tabs, "Interface & view");
        Slider(view, "Interface size", Settings.MinUi, Settings.MaxUi, 0.05f, () => Settings.UiScale, Settings.SetUiScale,
            "Text, panels and the hotbar. It already grows a little with bigger screens; this scales it from there.");
        Slider(view, "World zoom", Settings.MinZoom, Settings.MaxZoom, 0.05f, () => Settings.WorldZoom, Settings.SetWorldZoom,
            "How close the view sits to you. In the game: Ctrl + mouse wheel, Ctrl + = / -, Ctrl + 0 to reset.");
        Toggle(view, "Smooth camera", () => Settings.CameraSmoothing, on => { Settings.CameraSmoothing = on; Settings.ApplyCamera(); Settings.Save(); },
            "The view eases after you instead of being locked to you.");
        Slider(view, "Speech lingers", 0.5f, 3f, 0.25f, () => Settings.BubbleTime, t => { Settings.BubbleTime = t; Settings.Save(); },
            "How long what people say stays over their heads.");
        Toggle(view, "Show frame rate", () => Settings.ShowFps, on => { Settings.ShowFps = on; Settings.Save(); }, null);

        var game = Page(tabs, "Game");
        Toggle(game, "Pause when the window loses focus", () => Settings.PauseOnFocusLoss, on => { Settings.PauseOnFocusLoss = on; Settings.Save(); },
            "Only when playing alone; a shared world can't stop for one player.");
        Choice(game, "Autosave", Settings.AutosaveMinutes, m => m == 0 ? "Off" : $"Every {m} minutes", () => Settings.Autosave, m => { Settings.Autosave = m; Settings.Save(); },
            "Writes the quicksave (the same file as F5) when you are alone or hosting.");

        var sound = Page(tabs, "Sound");
        Slider(sound, "Master volume", 0f, 1f, 0.05f, () => Settings.MasterVolume, v => Settings.SetVolume("Master", v), null);
        Slider(sound, "Music", 0f, 1f, 0.05f, () => Settings.MusicVolume, v => Settings.SetVolume(Core.Audio.MusicBus, v),
            "Plays now and then, with long quiet between pieces; a fiddle in the men's house of an evening.");
        Slider(sound, "Ambience", 0f, 1f, 0.05f, () => Settings.AmbienceVolume, v => Settings.SetVolume(Core.Audio.AmbienceBus, v),
            "Wind, weather, the river, fires, the flies.");
        Slider(sound, "Effects", 0f, 1f, 0.05f, () => Settings.EffectsVolume, v => Settings.SetVolume(Core.Audio.EffectsBus, v),
            "Footsteps, shots, axes, bells, animals, the interface.");

        var keys = Page(tabs, "Controls");
        BuildKeys(keys);

        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        row.AddThemeConstantOverride("separation", 8);
        AddChild(row);
        _keyStatus.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        row.AddChild(_keyStatus);
        var reset = new Button { Text = "Restore defaults", CustomMinimumSize = new Vector2(150, 0) };
        reset.Pressed += () => { Settings.Reset(); RefreshAll(); };
        row.AddChild(reset);
        var back = new Button { Text = "Back", CustomMinimumSize = new Vector2(120, 0) };
        back.Pressed += () => _back?.Invoke();
        row.AddChild(back);
        Refresh();
    }

    // ---- key rebinding ------------------------------------------------------

    private (string action, int slot, Button button)? _capturing;
    private Label _keyStatus;

    private void BuildKeys(VBoxContainer page)
    {
        Note(page, "Click a key, then press the new one. Esc cancels; right-click clears it. A key taken from another action leaves that one without it.");
        foreach (var (action, what) in Keybinds.Actions)
        {
            if (!InputMap.HasAction(action)) continue;
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 8);
            var name = new Label { Text = what, SizeFlagsHorizontal = SizeFlags.ExpandFill, ClipText = true, SizeFlagsVertical = SizeFlags.ShrinkCenter };
            name.AddThemeFontSizeOverride("font_size", 13);
            row.AddChild(name);
            for (int slot = 0; slot < Keybinds.Slots; slot++)
            {
                int s = slot;
                var b = new Button { CustomMinimumSize = new Vector2(110, 0), FocusMode = FocusModeEnum.None };
                b.AddThemeFontSizeOverride("font_size", 13);
                b.Pressed += () => StartCapture(action, s, b);
                b.GuiInput += e =>
                {
                    if (e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right })
                    {
                        _capturing = null;
                        Keybinds.Bind(action, s, Key.None, out _);
                        _keyStatus.Text = "";
                        RefreshAll();
                    }
                };
                row.AddChild(b);
                _refreshers.Add(() => { if (_capturing?.button != b) b.Text = Keybinds.Name(Keybinds.Current(action)[s]); });
            }
            page.AddChild(row);
        }
        Note(page, "Kept by the game: Esc (menus), F5 / F9 (save / load), F11 (full screen), Ctrl + wheel (zoom), left click (strike), mouse wheel (hotbar).");
        var reset = new Button { Text = "Restore default keys", SizeFlagsHorizontal = SizeFlags.ShrinkBegin };
        reset.Pressed += () => { _capturing = null; Keybinds.ResetAll(); _keyStatus.Text = "Keys put back as they came."; RefreshAll(); };
        page.AddChild(reset);
        _keyStatus = new Label { Modulate = new Color(1f, 0.8f, 0.6f), AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _keyStatus.AddThemeFontSizeOverride("font_size", 12);
    }

    private void StartCapture(string action, int slot, Button b)
    {
        if (_capturing is { } old) old.button.Text = Keybinds.Name(Keybinds.Current(old.action)[old.slot]);
        _capturing = (action, slot, b);
        b.Text = "press a key...";
        _keyStatus.Text = "";
    }

    public override void _Input(InputEvent @event)
    {
        if (_capturing is not { } c || !IsVisibleInTree()) return;
        if (@event is InputEventMouseButton { Pressed: true } && !c.button.GetGlobalRect().HasPoint(c.button.GetGlobalMousePosition()))
        {
            _capturing = null; Refresh(); return;   // clicked elsewhere: never mind
        }
        if (@event is not InputEventKey { Pressed: true, Echo: false } k) return;
        GetViewport().SetInputAsHandled();
        _capturing = null;
        var key = k.PhysicalKeycode != Key.None ? k.PhysicalKeycode : k.Keycode;
        if (key == Key.Escape) { Refresh(); return; }
        string taken = Keybinds.Bind(c.action, c.slot, key, out bool refused);
        _keyStatus.Text = refused ? taken : taken != null ? $"{Keybinds.Name(key)} was \"{taken}\"; that has lost it." : "";
        RefreshAll();
    }

    public void Refresh()
    {
        _refreshing = true;
        foreach (var r in _refreshers) r();
        _refreshing = false;
    }

    // ---- building blocks ----------------------------------------------------

    private static VBoxContainer Page(TabContainer tabs, string name)
    {
        var scroll = new ScrollContainer { Name = name, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        tabs.AddChild(scroll);
        var margin = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        foreach (var side in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride($"margin_{side}", 10);
        scroll.AddChild(margin);
        var box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        box.AddThemeConstantOverride("separation", 6);
        margin.AddChild(box);
        return box;
    }

    private static HBoxContainer Row(VBoxContainer page, string label)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 10);
        row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(200, 0), SizeFlagsVertical = SizeFlags.ShrinkCenter });
        page.AddChild(row);
        return row;
    }

    private static void Note(VBoxContainer page, string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        var n = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart, Modulate = new Color(0.7f, 0.68f, 0.62f) };
        n.AddThemeFontSizeOverride("font_size", 11);
        page.AddChild(n);
        page.AddChild(new Control { CustomMinimumSize = new Vector2(0, 4) });
    }

    private void Choice<T>(VBoxContainer page, string label, T[] values, Func<T, string> name, Func<T> get, Action<T> set, string note)
    {
        var opt = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        foreach (var v in values) opt.AddItem(name(v));
        opt.ItemSelected += i => { if (!_refreshing) { set(values[i]); RefreshAll(); } };
        Row(page, label).AddChild(opt);
        Note(page, note);
        _refreshers.Add(() => { int i = Array.IndexOf(values, get()); opt.Selected = i; });
    }

    private void Toggle(VBoxContainer page, string label, Func<bool> get, Action<bool> set, string note)
    {
        var box = new CheckButton();
        box.Toggled += on => { if (!_refreshing) { set(on); RefreshAll(); } };
        Row(page, label).AddChild(box);
        Note(page, note);
        _refreshers.Add(() => box.ButtonPressed = get());
    }

    private void Slider(VBoxContainer page, string label, float min, float max, float step, Func<float> get, Action<float> set, string note)
    {
        var row = Row(page, label);
        var slider = new HSlider { MinValue = min, MaxValue = max, Step = step, SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter, CustomMinimumSize = new Vector2(220, 0) };
        var value = new Label { CustomMinimumSize = new Vector2(52, 0), HorizontalAlignment = HorizontalAlignment.Right };
        // Applied as it moves would rescale the very slider under the mouse;
        // the interface waits for the drag to end, everything else follows it.
        bool live = label != "Interface size";
        slider.ValueChanged += v => { value.Text = $"{Mathf.RoundToInt((float)v * 100)}%"; if (!_refreshing && live) set((float)v); };
        slider.DragEnded += changed => { if (!_refreshing && !live && changed) { set((float)slider.Value); RefreshAll(); } };
        // Clicks and keys move it without a drag.
        slider.GuiInput += e =>
        {
            if (live || _refreshing) return;
            if ((e is InputEventMouseButton { Pressed: false } || e is InputEventKey { Pressed: true }) && !Mathf.IsEqualApprox((float)slider.Value, get()))
                Callable.From(() => { set((float)slider.Value); RefreshAll(); }).CallDeferred();
        };
        row.AddChild(slider);
        row.AddChild(value);
        var reset = new Button { Text = "100%" };
        reset.Pressed += () => { set(1f); RefreshAll(); };
        row.AddChild(reset);
        Note(page, note);
        _refreshers.Add(() => { slider.Value = get(); value.Text = $"{Mathf.RoundToInt(get() * 100)}%"; });
    }
}
