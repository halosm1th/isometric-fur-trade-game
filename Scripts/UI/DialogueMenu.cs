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
    private ScrollContainer _scroll;
    private readonly List<Button> _buttons = new();
    private int _npcId = -1;

    public bool IsOpen => _panel != null && _panel.Visible;
    public int CurrentNpc => _npcId;
    public int OptionCount => _buttons.Count;

    public override void _Ready()
    {
        Instance = this;
        SetUpDevPicks();
        // A conversation that arrived while the scene was still loading (a joiner's prologue).
        if (NetHub.PendingDialogue is Godot.Collections.Dictionary held) { NetHub.PendingDialogue = null; CallDeferred(nameof(ShowHeld), held); }
        _panel = new PanelContainer { Visible = false };
        _panel.SetAnchorsPreset(LayoutPreset.CenterBottom);
        _panel.GrowHorizontal = GrowDirection.Both; _panel.GrowVertical = GrowDirection.Begin;
        _panel.Position = new Vector2(0, -110);
        var style = new StyleBoxFlat { BgColor = new Color(0.08f, 0.07f, 0.06f, 0.94f), BorderColor = new Color(0.7f, 0.58f, 0.35f) };
        style.SetBorderWidthAll(2); style.SetCornerRadiusAll(4); style.SetContentMarginAll(12);
        _panel.AddThemeStyleboxOverride("panel", style);
        AddChild(_panel);

        var box = new VBoxContainer { CustomMinimumSize = new Vector2(620, 0) };
        box.AddThemeConstantOverride("separation", 6);
        _panel.AddChild(box);
        _title = new Label(); _title.AddThemeFontSizeOverride("font_size", 18); _title.AddThemeColorOverride("font_color", new Color(0.95f, 0.85f, 0.6f)); box.AddChild(_title);
        _body = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(600, 0) };
        _body.AddThemeFontSizeOverride("font_size", 13);
        _body.Modulate = new Color(0.9f, 0.88f, 0.8f);
        box.AddChild(_body);
        box.AddChild(new HSeparator());
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(600, 0), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        scroll.SizeFlagsVertical = SizeFlags.ShrinkBegin;
        box.AddChild(scroll);
        _options = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; _options.AddThemeConstantOverride("separation", 2); scroll.AddChild(_options);
        _scroll = scroll;
    }

    private void ShowHeld(Godot.Collections.Dictionary state) => ShowState(state);

    public void ShowState(Godot.Collections.Dictionary state)
    {
        if ((bool)state["closed"]) { CloseLocal(); return; }
        _npcId = (int)state["npc"];
        _panel.Visible = true;
        _title.Text = (string)state["title"];
        _body.Text = (string)state["text"];
        foreach (var b in _buttons) b.QueueFree();
        foreach (var c in _options.GetChildren()) if (c is HSeparator) c.QueueFree();
        _buttons.Clear();
        var opts = (Godot.Collections.Array)state["options"];
        int i = 0;
        foreach (var o in opts)
        {
            int idx = i++;
            string label = (string)o;
            bool exit = label == "Leave" || label == "Back" || label == "Close the chest";
            bool submenu = label.EndsWith("...");
            if (exit && idx > 0) _options.AddChild(new HSeparator());
            var b = new Button { Text = $"{(idx < 9 ? (idx + 1).ToString() : "0")}.  {label}", Alignment = HorizontalAlignment.Left };
            b.AddThemeFontSizeOverride("font_size", 13);
            if (exit) b.Modulate = new Color(0.8f, 0.75f, 0.65f);
            if (submenu) b.AddThemeColorOverride("font_color", new Color(0.95f, 0.88f, 0.7f));
            b.Pressed += () => Pick(idx);
            _options.AddChild(b);
            _buttons.Add(b);
        }
        if (_trace) { GD.Print($"[talk] {_title.Text}: {_body.Text}"); for (int k = 0; k < _buttons.Count; k++) GD.Print($"[talk]   {k}. {opts[k]}"); }
        // Long menus scroll instead of climbing off the screen.
        _scroll.CustomMinimumSize = new Vector2(600, Mathf.Min(34f * opts.Count + 12f, 380f));
    }

    // Dev aid: `-- --talktrace` prints every screen of a conversation, and
    // `-- --pick=1,3,2` presses those options (0-based) a second apart, so a
    // dialogue tree can be walked in a headless run.
    private bool _trace;
    private void SetUpDevPicks()
    {
        foreach (string arg in OS.GetCmdlineUserArgs())
        {
            if (arg == "--talktrace") _trace = true;
            if (!arg.StartsWith("--pick=")) continue;
            _trace = true;
            double when = 8.0;
            foreach (string part in arg.Substring(7).Split(','))
            {
                if (!int.TryParse(part, out int idx)) continue;
                int pick = idx;
                GetTree().CreateTimer(when).Timeout += () => { if (IsOpen) { GD.Print($"[talk] > option {pick}"); Pick(pick); } else GD.Print("[talk] > nothing open"); };
                when += 1.5;
            }
        }
    }

    private void Pick(int idx) => NetHub.I?.DialogueRequest(_npcId, idx);

    // The player walked away / pressed Esc: tell the server, then hide.
    public void Close()
    {
        int npc = _npcId;
        bool wasOpen = _panel.Visible;
        CloseLocal();
        if (wasOpen && npc >= 0) NetHub.I?.DialogueRequest(npc, -1);
    }

    // The server says the conversation is over: just hide.
    private void CloseLocal()
    {
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
        if (_buttons.Count >= 10 && @event is InputEventKey { Pressed: true, Keycode: Key.Key0 }) { Pick(9); GetViewport().SetInputAsHandled(); }
    }
}
