using System.Collections.Generic;
using Godot;
using NetHub = IsometricFurTradeGame.Scripts.Net.Net;

namespace IsometricFurTradeGame.Scripts.UI;

// The talk window. Shows whatever conversation state the server sends and
// sends back the option picked. Number keys pick options; E or Esc leaves.
public partial class DialogueMenu : Control
{
    public static DialogueMenu Instance { get; private set; }

    private PanelContainer _panel;
    private Label _title, _body;
    private VBoxContainer _options;
    private readonly List<Button> _buttons = new();
    private int _npcId = -1;

    public bool IsOpen => _panel != null && _panel.Visible;

    public override void _Ready()
    {
        Instance = this;
        _panel = new PanelContainer { Visible = false };
        _panel.SetAnchorsPreset(LayoutPreset.CenterBottom);
        _panel.GrowHorizontal = GrowDirection.Both; _panel.GrowVertical = GrowDirection.Begin;
        _panel.Position = new Vector2(0, -110);
        var style = new StyleBoxFlat { BgColor = new Color(0.08f, 0.07f, 0.06f, 0.94f), BorderColor = new Color(0.7f, 0.58f, 0.35f) };
        style.SetBorderWidthAll(2); style.SetCornerRadiusAll(4); style.SetContentMarginAll(12);
        _panel.AddThemeStyleboxOverride("panel", style);
        AddChild(_panel);

        var box = new VBoxContainer { CustomMinimumSize = new Vector2(520, 0) };
        box.AddThemeConstantOverride("separation", 6);
        _panel.AddChild(box);
        _title = new Label(); _title.AddThemeFontSizeOverride("font_size", 17); box.AddChild(_title);
        _body = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(500, 0) };
        _body.AddThemeFontSizeOverride("font_size", 13);
        _body.Modulate = new Color(0.9f, 0.88f, 0.8f);
        box.AddChild(_body);
        _options = new VBoxContainer(); _options.AddThemeConstantOverride("separation", 2); box.AddChild(_options);
    }

    public void ShowState(Godot.Collections.Dictionary state)
    {
        if ((bool)state["closed"]) { Close(); return; }
        _npcId = (int)state["npc"];
        _panel.Visible = true;
        _title.Text = (string)state["title"];
        _body.Text = (string)state["text"];
        foreach (var b in _buttons) b.QueueFree();
        _buttons.Clear();
        int i = 0;
        foreach (var o in (Godot.Collections.Array)state["options"])
        {
            int idx = i++;
            var b = new Button { Text = $"{idx + 1}. {(string)o}", Alignment = HorizontalAlignment.Left };
            b.Pressed += () => Pick(idx);
            _options.AddChild(b);
            _buttons.Add(b);
        }
    }

    private void Pick(int idx) => NetHub.I?.DialogueRequest(_npcId, idx);

    public void Close()
    {
        if (_panel.Visible && _npcId >= 0) NetHub.I?.DialogueRequest(_npcId, -1);
        _panel.Visible = false;
        _npcId = -1;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!IsOpen) return;
        if (@event.IsActionPressed("ui_cancel") || @event.IsActionPressed("interact")) { Close(); GetViewport().SetInputAsHandled(); return; }
        for (int i = 0; i < _buttons.Count && i < 9; i++)
        {
            if (@event.IsActionPressed($"hotbar_{i + 1}")) { Pick(i); GetViewport().SetInputAsHandled(); return; }
        }
    }
}
