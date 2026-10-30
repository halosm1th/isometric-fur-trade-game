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
    private Label _title;
    private RichTextLabel _body;
    private VBoxContainer _options;
    private ScrollContainer _scroll, _logScroll;

    // The talk as a log (the old Fallout way): what they said, what you
    // answered, and their answer, scrolling up the window. Kept here on the
    // screen side: the server sends only the current screen, and this
    // remembers the option you pressed and lays its answer under it. Going
    // from menu to menu ("Talk...", "Back") writes nothing; a menu's own
    // prompt is shown until something is said, then gives way.
    private readonly List<string> _history = new();
    private string _current = "";
    private bool _currentIsReply;
    private string _picked;
    private readonly List<string> _labels = new();
    private const float LogMaxHeight = 300f;
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
        // Placed by hand each frame (Place): bottom centre, or beside the book when it is open.
        _panel.ZIndex = 5;
        _panel.AddThemeStyleboxOverride("panel", MenuTheme.PanelBox(34));
        AddChild(_panel);

        var box = new VBoxContainer { CustomMinimumSize = new Vector2(680, 0) };
        box.AddThemeConstantOverride("separation", 6);
        _panel.AddChild(box);
        _title = new Label(); _title.AddThemeFontSizeOverride("font_size", 20); _title.AddThemeColorOverride("font_color", new Color(0.95f, 0.85f, 0.6f)); box.AddChild(_title);
        _logScroll = new ScrollContainer { CustomMinimumSize = new Vector2(680, 0), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        box.AddChild(_logScroll);
        _body = new RichTextLabel { BbcodeEnabled = true, FitContent = true, ScrollActive = false, SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(668, 0) };
        _body.AddThemeFontSizeOverride("normal_font_size", 15);
        _body.AddThemeFontSizeOverride("italics_font_size", 15);
        _body.AddThemeColorOverride("default_color", new Color(0.9f, 0.88f, 0.8f));
        _logScroll.AddChild(_body);
        box.AddChild(new HSeparator());
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(680, 0), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        scroll.SizeFlagsVertical = SizeFlags.ShrinkBegin;
        box.AddChild(scroll);
        _options = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill }; _options.AddThemeConstantOverride("separation", 2); scroll.AddChild(_options);
        _scroll = scroll;
    }

    private void ShowHeld(Godot.Collections.Dictionary state) => ShowState(state);

    public void ShowState(Godot.Collections.Dictionary state)
    {
        if ((bool)state["closed"]) { CloseLocal(); return; }
        int npc = (int)state["npc"];
        string title = (string)state["title"];
        // A new conversation starts a fresh log.
        if (!_panel.Visible || npc != _npcId || (npc < 0 && title != _title.Text && _picked == null)) { _history.Clear(); _current = ""; _currentIsReply = false; _picked = null; }
        _npcId = npc;
        _panel.Visible = true;
        _title.Text = title;
        Log((string)state["text"]);
        foreach (var b in _buttons) b.QueueFree();
        foreach (var c in _options.GetChildren()) if (c is HSeparator) c.QueueFree();
        _buttons.Clear();
        _labels.Clear();
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
            b.AddThemeFontSizeOverride("font_size", 14);
            if (exit) b.Modulate = new Color(0.8f, 0.75f, 0.65f);
            if (submenu) b.AddThemeColorOverride("font_color", new Color(0.95f, 0.88f, 0.7f));
            b.Pressed += () => Pick(idx);
            _options.AddChild(b);
            _buttons.Add(b);
            _labels.Add(label);
        }
        if (_trace) { GD.Print($"[talk] {_title.Text}: {_body.Text}"); for (int k = 0; k < _buttons.Count; k++) GD.Print($"[talk]   {k}. {opts[k]}"); }
        // Long menus scroll instead of climbing off the screen.
        _scroll.CustomMinimumSize = new Vector2(680, Mathf.Min(36f * opts.Count + 12f, 380f));
        _panel.ResetSize();
        Place();
    }

    public override void _Process(double delta) { if (_panel.Visible) Place(); }

    // Bottom centre of the screen; slid right of the journal when the book is
    // open (it used to sit under the book and lose the start of every line).
    private void Place()
    {
        // The options box as tall as its buttons (up to a point), no taller.
        float want = Mathf.Min(_options.GetCombinedMinimumSize().Y + 4f, 380f);
        if (Mathf.Abs(_scroll.CustomMinimumSize.Y - want) > 1f) { _scroll.CustomMinimumSize = new Vector2(680, want); _panel.ResetSize(); }
        var screen = GetGlobalRect();
        var size = _panel.Size;
        float x = screen.Position.X + (screen.Size.X - size.X) / 2f;
        if (JournalPanel.IsOpen) x = Mathf.Max(x, JournalPanel.Area.End.X + 12f);
        x = Mathf.Min(x, screen.End.X - size.X - 12f);
        float y = Mathf.Max(screen.Position.Y + 12f, screen.End.Y - size.Y - 96f);
        _panel.GlobalPosition = new Vector2(Mathf.Max(screen.Position.X + 12f, x), y);
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

    private void Pick(int idx)
    {
        _picked = idx >= 0 && idx < _labels.Count ? _labels[idx] : null;
        NetHub.I?.DialogueRequest(_npcId, idx);
    }

    // Moving between menus is not talk; anything else you pressed is.
    private static bool Navigates(string label) =>
        label.EndsWith("...") || label is "Back" or "Leave" or "Not now" or "Enough" or "Step back" or "Close the chest" || label.StartsWith("Back ");

    private void Log(string text)
    {
        string picked = _picked; _picked = null;
        if (picked != null && !Navigates(picked))
        {
            if (_currentIsReply || _history.Count == 0 && _current != "") _history.Add(Them(_current));
            _history.Add(You(picked));
            _current = text; _currentIsReply = true;
        }
        else
        {
            if (_currentIsReply && _current != text) _history.Add(Them(_current));
            _current = text; _currentIsReply = _currentIsReply && _current == text;
        }
        while (_history.Count > 40) _history.RemoveAt(0);
        var sb = new System.Text.StringBuilder();
        foreach (var h in _history) sb.Append(h).Append("\n\n");
        sb.Append(Escape(_current));
        _body.Text = sb.ToString();
        CallDeferred(nameof(ScrollLogToEnd));
    }

    // What they said, dimmed once it is behind you; what you said, set off.
    private static string Them(string t) => $"[color=#a8a294]{Escape(t)}[/color]";
    private static string You(string label)
    {
        string said = label.Trim();
        bool spoken = said.StartsWith("\"") || said.StartsWith("'");
        return spoken ? $"[color=#e8c77a]You:[/color] [color=#e6dcc4]{Escape(said)}[/color]" : $"[color=#e8c77a]>[/color] [i][color=#cfc5ad]{Escape(said)}[/color][/i]";
    }
    private static string Escape(string t) => (t ?? "").Replace("[", "[lb]");

    private async void ScrollLogToEnd()
    {
        // The label measures itself a frame later; size the box to it (up to a
        // point) and show the newest lines.
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (!IsInstanceValid(_body)) return;
        float h = Mathf.Min(_body.GetContentHeight() + 4f, LogMaxHeight);
        _logScroll.CustomMinimumSize = new Vector2(680, h);
        _panel.ResetSize();
        Place();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        if (IsInstanceValid(_logScroll)) _logScroll.ScrollVertical = (int)_logScroll.GetVScrollBar().MaxValue;
    }

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
