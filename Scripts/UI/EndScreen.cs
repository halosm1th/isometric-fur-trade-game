using Godot;
using NetHub = IsometricFurTradeGame.Scripts.Net.Net;

namespace IsometricFurTradeGame.Scripts.UI;

// The end of the story, on paper (Core/Epilogue): the verdict, the career,
// the people, the last year told, and the years after - then play on in the
// world as it is, or leave it. Built on a layer of its own over everything
// the first time it is needed; alone, the clock stops while it is up.
public partial class EndScreen : Control
{
    private static EndScreen _instance;
    private VBoxContainer _body;
    private Label _title, _subtitle;
    public static bool IsOpen => _instance != null && GodotObject.IsInstanceValid(_instance) && _instance.Visible;

    public static void Open(Godot.Collections.Dictionary d)
    {
        if (_instance == null || !GodotObject.IsInstanceValid(_instance))
        {
            var scene = ((SceneTree)Engine.GetMainLoop()).CurrentScene;
            if (scene == null) return;
            var layer = new CanvasLayer { Layer = 90 };
            scene.AddChild(layer);
            _instance = new EndScreen();
            layer.AddChild(_instance);
        }
        _instance.Fill(d);
    }

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        Visible = false;
        var shade = new ColorRect { Color = new Color(0f, 0f, 0f, 0.72f) };
        shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(shade);

        var paper = new PanelContainer { Theme = MenuTheme.Get() };
        paper.AddThemeStyleboxOverride("panel", MenuTheme.PaperBox(28));
        paper.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        paper.OffsetLeft = 90; paper.OffsetRight = -90; paper.OffsetTop = 50; paper.OffsetBottom = -50;
        AddChild(paper);

        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 10);
        paper.AddChild(col);
        _title = new Label { HorizontalAlignment = HorizontalAlignment.Center, Modulate = MenuTheme.DarkInk };
        _title.AddThemeFontSizeOverride("font_size", 28);
        col.AddChild(_title);
        _subtitle = new Label { HorizontalAlignment = HorizontalAlignment.Center, Modulate = new Color(0.42f, 0.3f, 0.16f) };
        _subtitle.AddThemeFontSizeOverride("font_size", 16);
        col.AddChild(_subtitle);
        col.AddChild(new HSeparator());

        var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        col.AddChild(scroll);
        // The paper's torn edge eats the first letters without a margin of its own.
        var margin = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        foreach (var side in new[] { "margin_left", "margin_right" }) margin.AddThemeConstantOverride(side, 40);
        scroll.AddChild(margin);
        _body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _body.AddThemeConstantOverride("separation", 14);
        margin.AddChild(_body);

        col.AddChild(new HSeparator());
        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        buttons.AddThemeConstantOverride("separation", 24);
        col.AddChild(buttons);
        var on = new Button { Text = "Play on", CustomMinimumSize = new Vector2(180, 0) };
        on.Pressed += () => { Visible = false; Engine.TimeScale = NetHub.HostPaused ? 0.0 : 1.0; };
        buttons.AddChild(on);
        var leave = new Button { Text = "Return to the main menu", CustomMinimumSize = new Vector2(240, 0) };
        leave.Pressed += () =>
        {
            Engine.TimeScale = 1.0;
            NetHub.I?.Leave();
            GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, "res://Scenes/UI/MainMenu.tscn");
        };
        buttons.AddChild(leave);
    }

    private void Fill(Godot.Collections.Dictionary d)
    {
        _title.Text = d["title"].AsString();
        _subtitle.Text = d["subtitle"].AsString();
        foreach (var c in _body.GetChildren()) c.QueueFree();
        foreach (var v in d["sections"].AsGodotArray())
        {
            var s = v.AsGodotDictionary();
            var h = new Label { Text = s["heading"].AsString(), Modulate = new Color(0.42f, 0.3f, 0.16f) };
            h.AddThemeFontSizeOverride("font_size", 18);
            _body.AddChild(h);
            var t = new Label { Text = s["text"].AsString(), AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = SizeFlags.ExpandFill, Modulate = MenuTheme.DarkInk };
            t.AddThemeFontSizeOverride("font_size", 15);
            _body.AddChild(t);
        }
        Visible = true;
        if (NetHub.I == null || NetHub.I.Current == NetHub.Mode.Offline) Engine.TimeScale = 0.0;
    }
}
