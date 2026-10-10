using Godot;
using NetHub = IsometricFurTradeGame.Scripts.Net.Net;

namespace IsometricFurTradeGame.Scripts.UI;

// Continue / new game / multiplayer / settings / quit. Dev args skip it:
// --solo, --host, --server (headless dedicated), --join=ip[:port],
// --name=Whoever; --menu=settings|controls|multiplayer|newgame opens on that page.
public partial class MainMenu : Control
{
    private const string WorldScene = "res://Scenes/World/YorkFactoryExterior.tscn";
    private LineEdit _name, _address;
    private Label _status;
    private PanelContainer _panel;
    private Control _home, _multi, _newGame;
    private SettingsPanel _settings;

    public override void _Ready()
    {
        var net = NetHub.I;
        string autoName = null, join = null, page = null; bool solo = false, host = false, server = false;
        foreach (string arg in OS.GetCmdlineUserArgs())
        {
            if (arg == "--solo") solo = true;
            else if (arg == "--host") host = true;
            else if (arg == "--server") server = true;
            else if (arg.StartsWith("--join=")) join = arg.Substring(7);
            else if (arg.StartsWith("--name=")) autoName = arg.Substring(7);
            else if (arg.StartsWith("--menu=")) page = arg.Substring(7);
            else if (arg == "--load") { Core.SaveGame.Pending = Core.SaveGame.Read(Core.SaveGame.QuickPath); solo = solo || (!host && !server && join == null); }
            else if (arg.StartsWith("--spawn=") || arg.StartsWith("--hour=") || arg.StartsWith("--date=")) solo = solo || (!host && !server && join == null);
        }
        if (autoName != null) net.LocalName = autoName;
        if (server) { net.StartHost(NetHub.DefaultPort, true); Go(); return; }
        if (host) { net.StartHost(); Go(); return; }
        if (join != null) { Join(join); return; }
        if (solo) { net.StartSolo(); Go(); return; }

        Build();
        if (page == "settings") Show(_settings);
        else if (page == "controls") { Show(_settings); _settings.ShowTab(3); }
        else if (page == "multiplayer") Show(_multi);
        else if (page == "newgame") Show(_newGame);
    }

    private void Build()
    {
        Theme = MenuTheme.Get();

        // Dusk over the Bay: a warm band low down fading up into night.
        var grad = new Gradient();
        grad.SetColor(0, new Color(0.05f, 0.06f, 0.09f));
        grad.SetColor(1, new Color(0.22f, 0.13f, 0.08f));
        grad.AddPoint(0.7f, new Color(0.11f, 0.09f, 0.1f));
        var sky = new TextureRect { Texture = new GradientTexture2D { Gradient = grad, FillFrom = new Vector2(0, 0), FillTo = new Vector2(0, 1), Width = 8, Height = 256 }, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale };
        sky.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(sky);

        // The annual ship riding at anchor, and a new hand just off her.
        var scene = new Control { MouseFilter = MouseFilterEnum.Ignore };
        scene.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(scene);
        var ship = new TextureRect { Texture = GD.Load<Texture2D>("res://assets/sprites/ship.png"), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered, Modulate = new Color(0.75f, 0.68f, 0.62f, 0.9f) };
        ship.AnchorLeft = 0.5f; ship.AnchorRight = 0.98f; ship.AnchorTop = 0.18f; ship.AnchorBottom = 0.85f;
        scene.AddChild(ship);
        var man = new TextureRect { Texture = GD.Load<Texture2D>("res://assets/sprites/player/idle_front.png"), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered };
        man.AnchorLeft = 0.56f; man.AnchorRight = 0.66f; man.AnchorTop = 0.72f; man.AnchorBottom = 0.93f;
        scene.AddChild(man);

        // The menu itself, down the left.
        var margin = new MarginContainer();
        margin.SetAnchorsPreset(LayoutPreset.LeftWide);
        margin.AnchorRight = 0f;
        margin.AddThemeConstantOverride("margin_left", 56);
        margin.AddThemeConstantOverride("margin_top", 40);
        margin.AddThemeConstantOverride("margin_bottom", 40);
        AddChild(margin);
        var center = new CenterContainer();
        margin.AddChild(center);
        _panel = new PanelContainer();
        _panel.AddThemeStyleboxOverride("panel", MenuTheme.PanelBox(22));
        center.AddChild(_panel);

        _home = BuildHome();
        _panel.AddChild(_home);
        _multi = BuildMultiplayer();
        _panel.AddChild(_multi);
        _newGame = BuildNewGame();
        _panel.AddChild(_newGame);
        _settings = new SettingsPanel(() => Show(_home));
        _panel.AddChild(_settings);

        var foot = new Label { Text = "F11 full screen  ·  Command line: --solo | --host | --server | --join=ip[:port] | --name=Name", Modulate = new Color(0.55f, 0.52f, 0.48f) };
        foot.AddThemeFontSizeOverride("font_size", 11);
        foot.SetAnchorsPreset(LayoutPreset.BottomRight);
        foot.GrowHorizontal = GrowDirection.Begin; foot.GrowVertical = GrowDirection.Begin;
        foot.OffsetRight = -14; foot.OffsetBottom = -8;
        AddChild(foot);

        Show(_home);
    }

    private Control BuildHome()
    {
        var box = new VBoxContainer { CustomMinimumSize = new Vector2(380, 0) };
        box.AddThemeConstantOverride("separation", 9);

        var title = new Label { Text = "York Factory, 1811", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 34);
        title.AddThemeColorOverride("font_color", new Color(1f, 0.9f, 0.7f));
        box.AddChild(title);
        var sub = new Label { Text = "Hudson's Bay Company - the Pemmican War years", HorizontalAlignment = HorizontalAlignment.Center, Modulate = MenuTheme.Dim };
        sub.AddThemeFontSizeOverride("font_size", 13);
        box.AddChild(sub);
        box.AddChild(new HSeparator());

        var nameRow = new HBoxContainer();
        nameRow.AddThemeConstantOverride("separation", 10);
        nameRow.AddChild(new Label { Text = "Your name", Modulate = MenuTheme.Dim });
        _name = new LineEdit { Text = NetHub.I.LocalName, SizeFlagsHorizontal = SizeFlags.ExpandFill, MaxLength = 32 };
        nameRow.AddChild(_name);
        box.AddChild(nameRow);
        box.AddChild(new Control { CustomMinimumSize = new Vector2(0, 4) });

        if (Core.SaveGame.Exists())
        {
            var cont = Big(box, "Continue");
            cont.Pressed += () => { Apply(); NetHub.I.StartSolo(); Core.SaveGame.Pending = Core.SaveGame.Read(Core.SaveGame.QuickPath); Go(); };
            Small(box, Core.SaveGame.Summary());
        }
        var fresh = Big(box, "New game");
        fresh.Pressed += () => { Apply(); Show(_newGame); };
        if (Core.SaveGame.Exists()) Small(box, "Your saved game is kept until you save over it (F5, or the autosave).");
        Big(box, "Multiplayer").Pressed += () => Show(_multi);
        Big(box, "Settings").Pressed += () => Show(_settings);
        Big(box, "Quit").Pressed += () => GetTree().Quit();

        _status = new Label { HorizontalAlignment = HorizontalAlignment.Center, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        _status.Modulate = new Color(1f, 0.8f, 0.6f);
        _status.Visible = false;
        box.AddChild(_status);
        return box;
    }

    private Control BuildMultiplayer()
    {
        var box = new VBoxContainer { CustomMinimumSize = new Vector2(420, 0) };
        box.AddThemeConstantOverride("separation", 9);
        var title = new Label { Text = "Multiplayer", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 24);
        box.AddChild(title);
        Small(box, $"Playing as {NetHub.I.LocalName} - change it on the first page.").Name = "PlayingAs";
        box.AddChild(new HSeparator());

        Heading(box, "Host");
        var host = Big(box, $"Host a new game (port {NetHub.DefaultPort})");
        host.Pressed += () => { Apply(); var e = NetHub.I.StartHost(); if (e == Error.Ok) Go(); else Fail($"Could not host: {e}"); };
        if (Core.SaveGame.Exists())
        {
            var contHost = Big(box, "Continue the saved game and host");
            contHost.Pressed += () => { Apply(); var e = NetHub.I.StartHost(); if (e == Error.Ok) { Core.SaveGame.Pending = Core.SaveGame.Read(Core.SaveGame.QuickPath); Go(); } else Fail($"Could not host: {e}"); };
        }
        Small(box, "Friends join you at your IP address. Your world keeps running while you're in a menu.");

        Heading(box, "Join");
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        _address = new LineEdit { Text = NetHub.I.JoinAddress, PlaceholderText = "ip or ip:port", SizeFlagsHorizontal = SizeFlags.ExpandFill };
        row.AddChild(_address);
        var join = new Button { Text = "Join", CustomMinimumSize = new Vector2(90, 0) };
        join.Pressed += () => { Apply(); Join(_address.Text); };
        _address.TextSubmitted += _ => { Apply(); Join(_address.Text); };
        row.AddChild(join);
        box.AddChild(row);

        Heading(box, "Server");
        var dedicated = Big(box, "Run a dedicated server");
        dedicated.Pressed += () => { Apply(); var e = NetHub.I.StartHost(NetHub.DefaultPort, true); if (e == Error.Ok) Go(); else Fail($"Could not host: {e}"); };
        Small(box, "No player of your own; this window stays open as the world.");

        box.AddChild(new HSeparator());
        Big(box, "Back").Pressed += () => Show(_home);
        return box;
    }

    private static Button Big(VBoxContainer box, string text)
    {
        var b = new Button { Text = text, CustomMinimumSize = new Vector2(0, 40) };
        b.AddThemeFontSizeOverride("font_size", 17);
        box.AddChild(b);
        return b;
    }

    private static Label Small(VBoxContainer box, string text)
    {
        var l = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart, Modulate = MenuTheme.Dim, HorizontalAlignment = HorizontalAlignment.Center };
        l.AddThemeFontSizeOverride("font_size", 12);
        box.AddChild(l);
        return l;
    }

    private static void Heading(VBoxContainer box, string text)
    {
        var l = new Label { Text = text.ToUpper() };
        l.AddThemeFontSizeOverride("font_size", 12);
        l.AddThemeColorOverride("font_color", MenuTheme.Brass);
        box.AddChild(l);
    }

    private void Show(Control page)
    {
        Apply();
        foreach (var p in new[] { _home, _multi, _settings }) p.Visible = p == page;
        if (page == _multi && _multi.FindChild("PlayingAs", true, false) is Label who) who.Text = $"Playing as {NetHub.I.LocalName} - change it on the first page.";
        if (page == _settings) _settings.Refresh();
        _panel.ResetSize();
        // Keyboard and pad players start on the first button.
        Callable.From(() => FirstButton(page)?.GrabFocus()).CallDeferred();
    }

    private static Button FirstButton(Node n)
    {
        foreach (var c in n.GetChildren())
        {
            if (c is Button b && b.Visible) return b;
            if (c is Control and not TabContainer && FirstButton(c) is Button inner) return inner;
        }
        return null;
    }

    // Which campaign the world follows and how hard (design doc 2.3).
    private Control BuildNewGame()
    {
        var box = new VBoxContainer { CustomMinimumSize = new Vector2(440, 0) };
        box.AddThemeConstantOverride("separation", 9);
        var title = new Label { Text = "New game", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 24);
        box.AddChild(title);
        box.AddChild(new HSeparator());

        Heading(box, "Campaign");
        var pick = new OptionButton();
        box.AddChild(pick);
        var about = Small(box, "");
        var choices = Core.Campaign.Available();
        void Fill()
        {
            pick.Clear();
            choices = Core.Campaign.Available();
            for (int i = 0; i < choices.Count; i++)
            {
                pick.AddItem(choices[i].Title, i);
                if (choices[i].Id == Core.Campaign.Id) pick.Select(i);
            }
            about.Text = choices[Mathf.Max(0, pick.Selected)].Description;
        }
        Fill();
        pick.ItemSelected += i => { Core.Campaign.Id = choices[(int)i].Id; about.Text = choices[(int)i].Description; };
        Core.Campaign.Id = choices[Mathf.Max(0, pick.Selected)].Id;

        Heading(box, "How closely history is held to");
        var level = new OptionButton();
        string[] names = { "Gentle - the world finds its own way", "Steady - pressure toward the script", "Firm - late events are made to happen", "Strict - events happen on time" };
        string[] notes =
        {
            "Only fixed facts (the blockade, a post's founding) are certain. The director favours news that bears on what history has coming, and nothing more.",
            "As Gentle, and each event's pressures build as its date nears - bad blood between the companies, London's orders - but whether it happens is up to the world.",
            "As Steady, and an event the world hasn't produced by twice its grace period is made to happen. You still only hear of it when the news reaches you.",
            "As Steady, and every event happens once its grace period runs out. History keeps its shape; you play inside it.",
        };
        for (int i = 0; i < names.Length; i++) level.AddItem(names[i], i);
        level.Select((int)Core.Campaign.Level);
        box.AddChild(level);
        var levelNote = Small(box, notes[(int)Core.Campaign.Level]);
        level.ItemSelected += i => { Core.Campaign.Level = (Core.Adherence)(int)i; levelNote.Text = notes[(int)i]; };

        var folder = new Button { Text = "Open the campaigns folder (write your own)" };
        folder.Pressed += () => { OS.ShellOpen(Core.Campaign.EnsureUserFolder()); Fill(); };
        box.AddChild(folder);
        Small(box, "my-campaign.json there starts as a copy of the historical script, and README.txt explains the format. Come back to this page to pick it up.");

        box.AddChild(new HSeparator());
        Big(box, "Begin").Pressed += () => { Apply(); NetHub.I.StartSolo(); Go(); };
        Big(box, "Begin and host").Pressed += () => { Apply(); var e = NetHub.I.StartHost(); if (e == Error.Ok) Go(); else Fail($"Could not host: {e}"); };
        Big(box, "Back").Pressed += () => Show(_home);
        return box;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_home == null || _home.Visible || !@event.IsActionPressed("ui_cancel")) return;
        Show(_home);
        GetViewport().SetInputAsHandled();
    }

    private void Fail(string text) { Show(_home); _status.Text = text; _status.Visible = true; }

    private void Apply()
    {
        if (_name != null && _name.Text.Trim() != "") NetHub.I.LocalName = _name.Text.Trim();
        if (_address != null) NetHub.I.JoinAddress = _address.Text.Trim();
    }

    private void Join(string address)
    {
        string host = address; int port = NetHub.DefaultPort;
        int colon = address.LastIndexOf(':');
        if (colon > 0 && int.TryParse(address.Substring(colon + 1), out int p)) { host = address.Substring(0, colon); port = p; }
        var e = NetHub.I.StartClient(host, port);
        if (e != Error.Ok) { if (_status != null) Fail($"Could not connect: {e}"); return; }
        Go();
    }

    private void Go() => GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, WorldScene);
}
