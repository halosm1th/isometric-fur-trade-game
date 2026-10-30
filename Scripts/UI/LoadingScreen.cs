using Godot;
using IsometricFurTradeGame.Scripts.Core;

namespace IsometricFurTradeGame.Scripts.UI;

// A curtain over a change of scene. Building the world (the map, the posts,
// seven thousand people) takes a while, and a frozen menu looked like a
// hang. It hangs on the root, so it outlasts the scene it covers: drawn
// first, then the scene is changed under it, and it lifts once the new
// world has a player in it (or a page of the prologue up) and has drawn
// a few frames. The scene is read on a thread (ResourceLoader's threaded
// load) so the bar and the ship move while it comes in; only building it
// still holds the frame.
public partial class LoadingScreen : CanvasLayer
{
    private static LoadingScreen _on;
    private string _path, _caption;
    private Control _root;
    private Label _line, _tip;
    private LoadingArt _art;
    private ProgressBar _bar;
    private int _frames, _settled;
    private ulong _startMs;
    private bool _requested, _announced, _changed, _lifting;
    private float _target, _shown;
    private PackedScene _scene;

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
        _art = new LoadingArt { Modulate = new Color(0.62f, 0.59f, 0.56f) };
        _art.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(_art);

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
        _bar = new ProgressBar { MinValue = 0, MaxValue = 1, Step = 0, ShowPercentage = false, CustomMinimumSize = new Vector2(0, 8) };
        _bar.AddThemeStyleboxOverride("background", new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0.55f), BorderColor = new Color(MenuTheme.Brass, 0.5f), BorderWidthTop = 1, BorderWidthBottom = 1, BorderWidthLeft = 1, BorderWidthRight = 1 });
        _bar.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = MenuTheme.Brass });
        box.AddChild(_bar);
        _tip = new Label { Text = Tips[GD.RandRange(0, Tips.Length - 1)], AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(600, 0) };
        _tip.AddThemeFontSizeOverride("font_size", 15);
        _tip.AddThemeColorOverride("font_color", MenuTheme.Dim);
        box.AddChild(_tip);
    }

    public override void _Process(double delta)
    {
        _frames++;
        float secs = (Time.GetTicksMsec() - _startMs) / 1000f;
        // Eased toward how far along it is; a frame held while the world is
        // built doesn't jump it.
        _shown = Mathf.MoveToward(_shown, _target, Mathf.Min((float)delta, 0.05f) * 0.45f);
        _bar.Value = _shown;
        _art.Progress = _shown;
        if (_line != null && !_lifting && (!_announced || _changed)) _line.Text = (_changed ? "Building the world" : "Loading") + new string('.', 1 + (int)(secs * 2f) % 3);
        // Two frames on screen first, so the curtain is what freezes, not the menu.
        if (!_changed)
        {
            if (_frames < 3) return;
            if (!_requested)
            {
                _requested = true;
                if (ResourceLoader.LoadThreadedRequest(_path) != Error.Ok) { _changed = true; GetTree().ChangeSceneToFile(_path); }
                return;
            }
            var progress = new Godot.Collections.Array();
            var status = ResourceLoader.LoadThreadedGetStatus(_path, progress);
            // Godot reports a single scene's progress in a few coarse steps,
            // so she also makes way by the clock.
            if (status == ResourceLoader.ThreadLoadStatus.InProgress)
            {
                _target = Mathf.Max(0.05f + 0.55f * (progress.Count > 0 ? (float)progress[0] : 0f), 0.55f * (1f - Mathf.Exp(-secs / 1.6f)));
                return;
            }
            // Building the world holds the frame for many seconds: say so
            // and draw it once before it starts.
            if (_scene == null && status == ResourceLoader.ThreadLoadStatus.Loaded) _scene = ResourceLoader.LoadThreadedGet(_path) as PackedScene;
            if (!_announced) { _announced = true; _line.Text = "Building the world..."; return; }
            _changed = true;
            _target = 0.6f;
            if (_scene != null) GetTree().ChangeSceneToPacked(_scene);
            else GetTree().ChangeSceneToFile(_path);
            return;
        }
        if (_lifting) return;
        bool ready = GodotObject.IsInstanceValid(Sim.Player) || (DialogueMenu.Instance != null && GodotObject.IsInstanceValid(DialogueMenu.Instance) && DialogueMenu.Instance.IsOpen)
            || GetTree().CurrentScene?.SceneFilePath?.Contains("MainMenu") == true;
        if (ready) _settled++;
        _target = 0.6f + 0.4f * Mathf.Min(1f, _settled / 20f);
        // Up when the world is settled and she is at her anchorage.
        if ((_settled < 20 || _shown < 0.99f) && secs < 90f) return;
        _lifting = true;
        var tw = CreateTween();
        tw.SetPauseMode(Tween.TweenPauseMode.Process);
        tw.SetIgnoreTimeScale(true);
        tw.TweenProperty(_root, "modulate:a", 0f, 0.5f);
        tw.TweenCallback(Callable.From(QueueFree));
    }
}
