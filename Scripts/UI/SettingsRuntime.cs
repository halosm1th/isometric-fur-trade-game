using Godot;
using IsometricFurTradeGame.Scripts.Core;
using NetHub = IsometricFurTradeGame.Scripts.Net.Net;

namespace IsometricFurTradeGame.Scripts.UI;

// Lives on the root for the whole run, under every scene: keeps the scale
// right when the window changes size, holds the camera to the chosen zoom
// (a new player's camera starts at the scene's), and owns the keys that work
// anywhere - F11 full screen, Ctrl+wheel / Ctrl+= / Ctrl+- / Ctrl+0 world
// zoom - plus the FPS counter, pausing when the window loses focus, and the
// autosave.
public partial class SettingsRuntime : Node
{
    public static SettingsRuntime Instance { get; private set; }
    private Label _fps;
    private double _autosaveClock;

    public static void Install()
    {
        if (Instance != null) return;
        var root = (Engine.GetMainLoop() as SceneTree)?.Root;
        if (root == null) return;
        Instance = new SettingsRuntime { Name = "SettingsRuntime", ProcessMode = ProcessModeEnum.Always };
        root.CallDeferred(Node.MethodName.AddChild, Instance);
    }

    public override void _Ready()
    {
        GetTree().Root.SizeChanged += Settings.ApplyScale;
        var layer = new CanvasLayer { Layer = 100 };
        AddChild(layer);
        // Top centre: the calendar has the top-left corner, the focus panel the top-right.
        _fps = new Label { Visible = Settings.ShowFps, MouseFilter = Control.MouseFilterEnum.Ignore, HorizontalAlignment = HorizontalAlignment.Center };
        _fps.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
        _fps.GrowHorizontal = Control.GrowDirection.Both;
        _fps.OffsetTop = 2;
        _fps.AddThemeFontSizeOverride("font_size", 12);
        _fps.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.9f));
        layer.AddChild(_fps);
        Settings.ApplyScale();

        // --snap=3,8 saves what is on screen at those seconds to user://snap_N.png, in any scene.
        foreach (string arg in OS.GetCmdlineUserArgs())
        {
            if (!arg.StartsWith("--snap=")) continue;
            int n = 0;
            foreach (var part in arg.Substring(7).Split(','))
            {
                if (!double.TryParse(part, out double secs)) continue;
                int k = ++n;
                GetTree().CreateTimer(secs, true, false, true).Timeout += () =>
                {
                    var path = ProjectSettings.GlobalizePath($"user://snap_{k}.png");
                    GetViewport().GetTexture().GetImage().SavePng(path);
                    GD.Print($"[snap] {path}");
                };
            }
        }
    }

    public override void _Process(double delta)
    {
        var cam = GetViewport().GetCamera2D();
        if (cam != null && (cam.Zoom != Settings.CameraZoom || cam.PositionSmoothingEnabled != Settings.CameraSmoothing)) Settings.ApplyCamera();

        _fps.Visible = Settings.ShowFps;
        if (_fps.Visible) _fps.Text = $"{Engine.GetFramesPerSecond()} fps";

        // Real minutes, counted only while there is a world of ours to save.
        if (Settings.Autosave > 0 && GetTree().CurrentScene is not MainMenu && NetHub.IsServer && Sim.Player != null && NetHub.I != null && !PauseMenu.IsOpen)
        {
            _autosaveClock += delta;
            if (_autosaveClock >= Settings.Autosave * 60.0)
            {
                _autosaveClock = 0;
                if (SaveGame.Save()) Sim.NoticeLocal("Autosaved.");
            }
        }
        else if (Sim.Player == null) _autosaveClock = 0;
    }

    public override void _Notification(int what)
    {
        if (what == NotificationApplicationFocusOut && Settings.PauseOnFocusLoss) PauseMenu.Instance?.OpenFromFocusLoss();
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F11 })
        {
            Settings.ToggleFullscreen();
            SettingsPanel.RefreshAll();
            GetViewport().SetInputAsHandled();
            return;
        }
        // Not while something is being typed into.
        if (GetViewport().GuiGetFocusOwner() is LineEdit or TextEdit) return;
        if (@event is InputEventMouseButton { Pressed: true, CtrlPressed: true } mb && (mb.ButtonIndex == MouseButton.WheelUp || mb.ButtonIndex == MouseButton.WheelDown))
        {
            if (MapPanel.IsOpen) return;   // the map zooms itself
            Zoom(mb.ButtonIndex == MouseButton.WheelUp ? 1.1f : 1f / 1.1f);
            GetViewport().SetInputAsHandled();
        }
        else if (@event is InputEventKey { Pressed: true, CtrlPressed: true } k)
        {
            if (k.Keycode is Key.Equal or Key.Plus or Key.KpAdd) Zoom(1.1f);
            else if (k.Keycode is Key.Minus or Key.KpSubtract) Zoom(1f / 1.1f);
            else if (k.Keycode is Key.Key0 or Key.Kp0) { Settings.SetWorldZoom(1f); SettingsPanel.RefreshAll(); }
            else return;
            GetViewport().SetInputAsHandled();
        }
    }

    private static void Zoom(float factor)
    {
        Settings.SetWorldZoom(Settings.WorldZoom * factor);
        SettingsPanel.RefreshAll();
    }
}
