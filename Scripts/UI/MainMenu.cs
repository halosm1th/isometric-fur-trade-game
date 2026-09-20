using Godot;
using NetHub = IsometricFurTradeGame.Scripts.Net.Net;

namespace IsometricFurTradeGame.Scripts.UI;

// Solo / host / join. Dev args skip it: --solo, --host, --server (headless
// dedicated), --join=ip[:port], --name=Whoever.
public partial class MainMenu : Control
{
    private const string WorldScene = "res://Scenes/World/YorkFactoryExterior.tscn";
    private LineEdit _name, _address;
    private Label _status;

    public override void _Ready()
    {
        var net = NetHub.I;
        string autoName = null, join = null; bool solo = false, host = false, server = false;
        foreach (string arg in OS.GetCmdlineUserArgs())
        {
            if (arg == "--solo") solo = true;
            else if (arg == "--host") host = true;
            else if (arg == "--server") server = true;
            else if (arg.StartsWith("--join=")) join = arg.Substring(7);
            else if (arg.StartsWith("--name=")) autoName = arg.Substring(7);
            else if (arg.StartsWith("--spawn=") || arg.StartsWith("--hour=") || arg.StartsWith("--date=")) solo = solo || (!host && !server && join == null);
        }
        if (autoName != null) net.LocalName = autoName;
        if (server) { net.StartHost(NetHub.DefaultPort, true); Go(); return; }
        if (host) { net.StartHost(); Go(); return; }
        if (join != null) { Join(join); return; }
        if (solo) { net.StartSolo(); Go(); return; }

        Build();
    }

    private void Build()
    {
        var bg = new ColorRect { Color = new Color(0.07f, 0.08f, 0.1f) };
        bg.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(bg);

        var box = new VBoxContainer { CustomMinimumSize = new Vector2(420, 0) };
        box.SetAnchorsPreset(LayoutPreset.Center);
        box.GrowHorizontal = GrowDirection.Both; box.GrowVertical = GrowDirection.Both;
        box.AddThemeConstantOverride("separation", 10);
        AddChild(box);

        var title = new Label { Text = "York Factory, 1811", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 30);
        box.AddChild(title);
        var sub = new Label { Text = "Hudson's Bay Company - the Pemmican War years", HorizontalAlignment = HorizontalAlignment.Center };
        sub.Modulate = new Color(0.8f, 0.75f, 0.65f);
        box.AddChild(sub);
        box.AddChild(new HSeparator());

        box.AddChild(new Label { Text = "Your name" });
        _name = new LineEdit { Text = NetHub.I.LocalName };
        box.AddChild(_name);

        var solo = new Button { Text = "Play alone" };
        solo.Pressed += () => { Apply(); NetHub.I.StartSolo(); Go(); };
        box.AddChild(solo);

        var host = new Button { Text = "Host a game (friends join you, port 7777)" };
        host.Pressed += () => { Apply(); var e = NetHub.I.StartHost(); if (e == Error.Ok) Go(); else _status.Text = $"Could not host: {e}"; };
        box.AddChild(host);

        box.AddChild(new Label { Text = "Host address" });
        _address = new LineEdit { Text = NetHub.I.JoinAddress, PlaceholderText = "ip or ip:port" };
        box.AddChild(_address);
        var join = new Button { Text = "Join a game" };
        join.Pressed += () => { Apply(); Join(_address.Text); };
        box.AddChild(join);

        var dedicated = new Button { Text = "Run a dedicated server (no player, this window stays open)" };
        dedicated.Pressed += () => { Apply(); var e = NetHub.I.StartHost(NetHub.DefaultPort, true); if (e == Error.Ok) Go(); else _status.Text = $"Could not host: {e}"; };
        box.AddChild(dedicated);

        _status = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _status.Modulate = new Color(1f, 0.8f, 0.6f);
        box.AddChild(_status);
        var help = new Label { Text = "Command line: --solo | --host | --server | --join=ip[:port] | --name=Name", HorizontalAlignment = HorizontalAlignment.Center };
        help.AddThemeFontSizeOverride("font_size", 11);
        help.Modulate = new Color(0.6f, 0.6f, 0.6f);
        box.AddChild(help);
    }

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
        if (e != Error.Ok) { if (_status != null) _status.Text = $"Could not connect: {e}"; return; }
        Go();
    }

    private void Go() => GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, WorldScene);
}
