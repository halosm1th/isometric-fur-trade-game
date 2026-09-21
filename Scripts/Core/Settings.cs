using Godot;

namespace IsometricFurTradeGame.Scripts.Core;

// The few things a player sets about the program itself, kept in
// user://settings.cfg. The UI is laid out for 1280x720 and stretches
// (canvas_items, expand), so 1920x1080 full screen scales it by 1.5.
public static class Settings
{
    private const string Path = "user://settings.cfg";
    public static bool Fullscreen { get; private set; }

    public static void Load()
    {
        var cfg = new ConfigFile();
        if (cfg.Load(Path) == Error.Ok) Fullscreen = (bool)cfg.GetValue("display", "fullscreen", false);
        Apply();
    }

    public static void SetFullscreen(bool on)
    {
        Fullscreen = on;
        Apply();
        var cfg = new ConfigFile();
        cfg.Load(Path);
        cfg.SetValue("display", "fullscreen", on);
        cfg.Save(Path);
    }

    public static void Toggle() => SetFullscreen(!Fullscreen);

    private static void Apply()
    {
        if (Fullscreen)
        {
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
        }
        else
        {
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
            DisplayServer.WindowSetSize(new Vector2I(1280, 720));
            var screen = DisplayServer.ScreenGetSize();
            DisplayServer.WindowSetPosition((screen - new Vector2I(1280, 720)) / 2);
        }
    }

    public static string Label => Fullscreen ? "Full screen: on (F11) - back to a 1280x720 window" : "Full screen: off (F11) - go full screen (1920x1080 scales the UI)";
}
