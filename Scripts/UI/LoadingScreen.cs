using Godot;
using IsometricFurTradeGame.Scripts.Core;

namespace IsometricFurTradeGame.Scripts.UI;

// A curtain over a change of scene. Building the world (the map, the posts,
// seven thousand people) takes a while, and a frozen menu looked like a
// hang. It hangs on the root, so it outlasts the scene it covers: drawn
// first, then the scene is changed under it, and it lifts once the new
// world has a player in it (or a page of the prologue up) and has drawn
// a few frames.
public partial class LoadingScreen : CanvasLayer
{
    private static LoadingScreen _on;
    private string _path, _caption;
    private Control _root;
    private Label _line, _tip;
    private int _frames, _settled;
    private ulong _startMs;
    private bool _changed, _lifting;

    private static readonly string[] Tips =
    {
        "The Company's ship comes once a year. Whatever she doesn't bring, you go without till next summer.",
        "A piece is about ninety pounds: one bale of goods, one man's load across a portage.",
        "Made Beaver is the Bay's money. A prime beaver pelt is one, and everything else is reckoned against it.",
        "Men talk. What you do where others can see it is told round the fires for a long while.",
        "Winter kills the careless. Keep a capote on your back and wood on the fire.",
        "The homeguard Cree hunt the geese that feed the post. Their goodwill is worth more than the Factor lets on.",
        "Ask the Factor for work if you have none. Idle hands at York are noticed.",
        "Hold the fast-forward key to let the hours run while you wait.",
    };

    // Draw the curtain, then change to `path` beneath it.
    public static void Go(SceneTree tree, string path, string caption = null)
    {
        if (_on != null && IsInstanceValid(_on)) _on.QueueFree();
        _on = new LoadingScreen { _path = path, _caption = caption, Layer = 128, ProcessMode = ProcessModeEnum.Always };
        // Deferred: a scene's _Ready (the menu starting a dev game) can ask for this while the root is busy.
        tree.Root.CallDeferred(Node.MethodName.AddChild, _on);
    }

    public override void _Ready()
    {
        _startMs = Time.GetTicksMsec();
        _root = new Control { MouseFilter = Control.MouseFilterEnum.Stop };
        _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_root);
        var bg = new ColorRect { Color = new Color(0.04f, 0.035f, 0.03f) };
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(bg);
        var art = new TextureRect
        {
            Texture = GD.Load<Texture2D>("res://assets/ui/menu_background.jpg"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
            Modulate = new Color(0.45f, 0.42f, 0.4f), MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        art.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(art);

        var box = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.End };
        box.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        box.OffsetLeft = 64; box.OffsetRight = -64; box.OffsetBottom = -56;
        box.AddThemeConstantOverride("separation", 10);
        _root.AddChild(box);
        var title = new Label { Text = _caption ?? "York Factory, Hudson's Bay" };
        title.AddThemeFontSizeOverride("font_size", 34);
        title.AddThemeColorOverride("font_color", MenuTheme.Ink);
        title.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.8f));
        box.AddChild(title);
        _line = new Label { Text = "Loading" };
        _line.AddThemeFontSizeOverride("font_size", 16);
        _line.AddThemeColorOverride("font_color", MenuTheme.Brass);
        box.AddChild(_line);
        box.AddChild(new HSeparator());
        _tip = new Label { Text = Tips[GD.RandRange(0, Tips.Length - 1)], AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(600, 0) };
        _tip.AddThemeFontSizeOverride("font_size", 15);
        _tip.AddThemeColorOverride("font_color", MenuTheme.Dim);
        box.AddChild(_tip);
    }

    public override void _Process(double delta)
    {
        _frames++;
        float secs = (Time.GetTicksMsec() - _startMs) / 1000f;
        if (_line != null && !_lifting) _line.Text = (_changed ? "Building the world" : "Loading") + new string('.', 1 + (int)(secs * 2f) % 3);
        // Two frames on screen first, so the curtain is what freezes, not the menu.
        if (!_changed)
        {
            if (_frames < 3) return;
            _changed = true;
            GetTree().ChangeSceneToFile(_path);
            return;
        }
        if (_lifting) return;
        bool ready = GodotObject.IsInstanceValid(Sim.Player) || (DialogueMenu.Instance != null && GodotObject.IsInstanceValid(DialogueMenu.Instance) && DialogueMenu.Instance.IsOpen)
            || GetTree().CurrentScene?.SceneFilePath?.Contains("MainMenu") == true;
        if (ready) _settled++;
        if (_settled < 20 && secs < 90f) return;
        _lifting = true;
        var tw = CreateTween();
        tw.SetPauseMode(Tween.TweenPauseMode.Process);
        tw.SetIgnoreTimeScale(true);
        tw.TweenProperty(_root, "modulate:a", 0f, 0.5f);
        tw.TweenCallback(Callable.From(QueueFree));
    }
}
