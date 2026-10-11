using Godot;

namespace IsometricFurTradeGame.Scripts.Core;

// What a player sets about the program itself, kept in user://settings.cfg.
//
// The UI is laid out for 1280x720 and stretches (canvas_items, expand), so a
// 1920x1080 screen would blow everything - interface and world - up by 1.5.
// Instead the stretch is damped: on a bigger screen things grow by half as
// much as the screen did (1080p -> 1.25x, 1440p -> 1.5x, 4K -> 2x), and the
// player's two sliders scale that again - Interface size through the root
// window's content scale, World zoom through the camera, which is divided
// back by the interface scale so the two never pull on each other.
public static class Settings
{
    private const string Path = "user://settings.cfg";
    public const float BaseCameraZoom = 1.3f;   // Player.tscn's Camera2D
    // World zoom 100% sits this far out from the scene's camera, so you see
    // the country round you rather than the man in front of you.
    public const float DefaultView = 0.75f;
    public static readonly Vector2I Designed = new(1280, 720);

    public enum Mode { Windowed, Fullscreen, Exclusive }
    public static readonly Vector2I[] WindowSizes = { new(1280, 720), new(1600, 900), new(1920, 1080), new(2560, 1440) };
    public static readonly int[] FpsCaps = { 0, 30, 60, 90, 120, 144, 240 };
    public static readonly int[] AutosaveMinutes = { 0, 5, 10, 20, 30 };

    // Display
    public static Mode WindowMode = Mode.Windowed;
    public static int WindowSizeIndex;
    public static bool VSync = true;
    public static int MaxFps;
    // Interface and camera
    public static float UiScale = 1f;
    public static float WorldZoom = 1f;
    public static bool CameraSmoothing = true;
    public static float BubbleTime = 1f;
    public static bool ShowFps;
    // Game
    public static bool PauseOnFocusLoss = true;
    public static int Autosave = 10;   // real minutes, 0 = off
    // Sound, 0..1 on each bus (Core/Audio.cs)
    public static float MasterVolume = 0.8f;
    public static float MusicVolume = 0.6f;
    public static float AmbienceVolume = 0.8f;
    public static float EffectsVolume = 0.9f;

    public static bool Fullscreen => WindowMode != Mode.Windowed;
    public const float MinUi = 0.5f, MaxUi = 1.5f, MinZoom = 0.5f, MaxZoom = 2.5f;

    private static bool Headless => DisplayServer.GetName() == "headless";

    public static void Load()
    {
        var cfg = new ConfigFile();
        if (cfg.Load(Path) == Error.Ok)
        {
            // Older files only had the fullscreen flag.
            bool oldFull = (bool)cfg.GetValue("display", "fullscreen", false);
            WindowMode = (Mode)(int)cfg.GetValue("display", "window_mode", (int)(oldFull ? Mode.Fullscreen : Mode.Windowed));
            WindowSizeIndex = Mathf.Clamp((int)cfg.GetValue("display", "window_size", 0), 0, WindowSizes.Length - 1);
            VSync = (bool)cfg.GetValue("display", "vsync", true);
            MaxFps = (int)cfg.GetValue("display", "max_fps", 0);
            UiScale = Mathf.Clamp((float)cfg.GetValue("interface", "ui_scale", 1f), MinUi, MaxUi);
            WorldZoom = Mathf.Clamp((float)cfg.GetValue("interface", "world_zoom", 1f), MinZoom, MaxZoom);
            CameraSmoothing = (bool)cfg.GetValue("interface", "camera_smoothing", true);
            BubbleTime = Mathf.Clamp((float)cfg.GetValue("interface", "bubble_time", 1f), 0.5f, 3f);
            ShowFps = (bool)cfg.GetValue("interface", "show_fps", false);
            PauseOnFocusLoss = (bool)cfg.GetValue("game", "pause_on_focus_loss", true);
            Autosave = (int)cfg.GetValue("game", "autosave_minutes", 10);
            MasterVolume = Mathf.Clamp((float)cfg.GetValue("sound", "master", 0.8f), 0f, 1f);
            MusicVolume = Mathf.Clamp((float)cfg.GetValue("sound", "music", 0.6f), 0f, 1f);
            AmbienceVolume = Mathf.Clamp((float)cfg.GetValue("sound", "ambience", 0.8f), 0f, 1f);
            EffectsVolume = Mathf.Clamp((float)cfg.GetValue("sound", "effects", 0.9f), 0f, 1f);
            Keybinds.Read(cfg);
        }
        ApplyWindow();
        ApplyRendering();
        ApplyScale();
        Audio.ApplyVolumes();
        if (!Headless) UI.SettingsRuntime.Install();
    }

    public static void Save()
    {
        var cfg = new ConfigFile();
        cfg.SetValue("display", "window_mode", (int)WindowMode);
        cfg.SetValue("display", "window_size", WindowSizeIndex);
        cfg.SetValue("display", "vsync", VSync);
        cfg.SetValue("display", "max_fps", MaxFps);
        cfg.SetValue("interface", "ui_scale", UiScale);
        cfg.SetValue("interface", "world_zoom", WorldZoom);
        cfg.SetValue("interface", "camera_smoothing", CameraSmoothing);
        cfg.SetValue("interface", "bubble_time", BubbleTime);
        cfg.SetValue("interface", "show_fps", ShowFps);
        cfg.SetValue("game", "pause_on_focus_loss", PauseOnFocusLoss);
        cfg.SetValue("game", "autosave_minutes", Autosave);
        cfg.SetValue("sound", "master", MasterVolume);
        cfg.SetValue("sound", "music", MusicVolume);
        cfg.SetValue("sound", "ambience", AmbienceVolume);
        cfg.SetValue("sound", "effects", EffectsVolume);
        Keybinds.Write(cfg);
        cfg.Save(Path);
    }

    public static void Reset()
    {
        WindowMode = Mode.Windowed; WindowSizeIndex = 0; VSync = true; MaxFps = 0;
        UiScale = 1f; WorldZoom = 1f; CameraSmoothing = true; BubbleTime = 1f; ShowFps = false;
        PauseOnFocusLoss = true; Autosave = 10;
        MasterVolume = 0.8f; MusicVolume = 0.6f; AmbienceVolume = 0.8f; EffectsVolume = 0.9f;
        ApplyWindow(); ApplyRendering(); ApplyScale(); Audio.ApplyVolumes(); Save();
    }

    public static void SetVolume(string bus, float v)
    {
        v = Mathf.Clamp(v, 0f, 1f);
        switch (bus)
        {
            case "Master": MasterVolume = v; break;
            case Audio.MusicBus: MusicVolume = v; break;
            case Audio.AmbienceBus: AmbienceVolume = v; break;
            case Audio.EffectsBus: EffectsVolume = v; break;
        }
        Audio.ApplyVolumes();
        Save();
    }

    public static void SetWindowMode(Mode m) { WindowMode = m; ApplyWindow(); Save(); }
    public static void SetWindowSize(int i) { WindowSizeIndex = Mathf.Clamp(i, 0, WindowSizes.Length - 1); ApplyWindow(); Save(); }
    public static void ToggleFullscreen() => SetWindowMode(Fullscreen ? Mode.Windowed : Mode.Fullscreen);
    public static void SetUiScale(float s) { UiScale = Mathf.Clamp(s, MinUi, MaxUi); ApplyScale(); Save(); }
    public static void SetWorldZoom(float z) { WorldZoom = Mathf.Clamp(z, MinZoom, MaxZoom); ApplyCamera(); Save(); }
    public static void SetVSync(bool on) { VSync = on; ApplyRendering(); Save(); }
    public static void SetMaxFps(int fps) { MaxFps = fps; ApplyRendering(); Save(); }

    public static void ApplyWindow()
    {
        if (Headless) return;
        switch (WindowMode)
        {
            case Mode.Fullscreen: DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen); break;
            case Mode.Exclusive: DisplayServer.WindowSetMode(DisplayServer.WindowMode.ExclusiveFullscreen); break;
            default:
                DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
                var screen = DisplayServer.ScreenGetUsableRect().Size;
                var size = WindowSizes[WindowSizeIndex];
                // A size bigger than the desktop would hang off it.
                if (size.X > screen.X || size.Y > screen.Y) size = (Vector2I)((Vector2)size * Mathf.Min((float)screen.X / size.X, (float)screen.Y / size.Y) * 0.95f);
                DisplayServer.WindowSetSize(size);
                var origin = DisplayServer.ScreenGetUsableRect().Position;
                DisplayServer.WindowSetPosition(origin + (screen - size) / 2);
                break;
        }
        ApplyScale();
    }

    public static void ApplyRendering()
    {
        if (Headless) return;
        DisplayServer.WindowSetVsyncMode(VSync ? DisplayServer.VSyncMode.Enabled : DisplayServer.VSyncMode.Disabled);
        Engine.MaxFps = MaxFps;
    }

    // How much bigger than the 1280x720 layout the window is.
    public static float Stretch
    {
        get
        {
            if (Headless) return 1f;
            var w = DisplayServer.WindowGetSize();
            if (w.X <= 0 || w.Y <= 0) return 1f;
            return Mathf.Min((float)w.X / Designed.X, (float)w.Y / Designed.Y);
        }
    }

    // Half the screen's growth, never shrinking below the designed size.
    public static float Damped(float stretch) => stretch <= 1f ? stretch : 1f + (stretch - 1f) * 0.5f;

    // The root's content scale: the stretch already applied is undone down
    // to the damped size, then the player's preference on top.
    // Never so big that the 1280x720 layout's panels stop fitting: the
    // window always holds at least 960x540 of it.
    public static float ContentScale
    {
        get
        {
            float stretch = Stretch;
            float eff = Damped(stretch) * UiScale;
            if (!Headless)
            {
                var w = DisplayServer.WindowGetSize();
                if (w.X > 0 && w.Y > 0) eff = Mathf.Min(eff, Mathf.Min(w.X / 960f, w.Y / 540f));
            }
            return eff / stretch;
        }
    }

    public static void ApplyScale()
    {
        if (Headless) return;
        var root = (Engine.GetMainLoop() as SceneTree)?.Root;
        if (root == null) return;
        float s = ContentScale;
        if (Mathf.Abs(root.ContentScaleFactor - s) > 0.001f) root.ContentScaleFactor = s;
        ApplyCamera();
    }

    // The world on screen is BaseCameraZoom x DefaultView x damped stretch x WorldZoom,
    // whatever the interface is set to.
    public static Vector2 CameraZoom
    {
        get
        {
            float stretch = Stretch;
            return Vector2.One * (BaseCameraZoom * DefaultView * WorldZoom * Damped(stretch) / (stretch * ContentScale));
        }
    }

    public static void ApplyCamera()
    {
        var cam = (Engine.GetMainLoop() as SceneTree)?.Root?.GetCamera2D();
        if (cam == null) return;
        cam.Zoom = CameraZoom;
        cam.PositionSmoothingEnabled = CameraSmoothing;
    }

    public static string ModeName(Mode m) => m switch { Mode.Fullscreen => "Full screen (borderless)", Mode.Exclusive => "Full screen (exclusive)", _ => "Windowed" };
}
