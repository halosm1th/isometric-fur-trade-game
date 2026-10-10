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

        var tabs = new TabContainer { CustomMinimumSize = new Vector2(0, 360) };
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

        var keys = Page(tabs, "Controls");
        var list = new Label { Text = PauseMenu.BindingsText() };
        list.AddThemeFontSizeOverride("font_size", 12);
        keys.AddChild(list);
        Note(keys, "Bindings live in project.godot [input]; a rebinding screen is on the list.");

        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        row.AddThemeConstantOverride("separation", 8);
        AddChild(row);
        var reset = new Button { Text = "Restore defaults", CustomMinimumSize = new Vector2(150, 0) };
        reset.Pressed += () => { Settings.Reset(); RefreshAll(); };
        row.AddChild(reset);
        var back = new Button { Text = "Back", CustomMinimumSize = new Vector2(120, 0) };
        back.Pressed += () => _back?.Invoke();
        row.AddChild(back);
        Refresh();
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
