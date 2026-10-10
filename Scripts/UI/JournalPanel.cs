using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Items;
using IsometricFurTradeGame.Scripts.Player;

namespace IsometricFurTradeGame.Scripts.UI;

// J: your book. Three tabs - the journal (what you saw yourself, dated;
// design doc 10), the log (everything the world has announced), and your
// pack (every item with weight and worth, and what you're wearing/holding).
public partial class JournalPanel : Control
{
    public static bool IsOpen { get; private set; }

    private PanelContainer _panel;
    private TabContainer _tabs;
    private RichTextLabel _journal, _log, _pack;
    private VBoxContainer _packRows;
    private string _packKey = "";
    private PlayerController _player;

    public override void _Ready()
    {
        _player = Sim.Player as PlayerController;
        Sim.LocalPlayerChanged += p => _player = p;
        _panel = new PanelContainer { Visible = false };
        _panel.SetAnchorsPreset(LayoutPreset.CenterLeft);
        _panel.GrowVertical = GrowDirection.Both;
        _panel.Position = new Vector2(16, 0);
        var style = new StyleBoxFlat { BgColor = new Color(0.12f, 0.09f, 0.06f, 0.95f), BorderColor = new Color(0.7f, 0.58f, 0.35f) };
        style.SetBorderWidthAll(2); style.SetCornerRadiusAll(4); style.SetContentMarginAll(10);
        _panel.AddThemeStyleboxOverride("panel", style);
        AddChild(_panel);

        _tabs = new TabContainer { CustomMinimumSize = new Vector2(520, 460) };
        _panel.AddChild(_tabs);
        _journal = MakeTab("Journal (J)");
        _log = MakeTab("Log (L)");
        _pack = MakeTab("Pack (I)");
        _packRows = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _packRows.AddThemeConstantOverride("separation", 2);
        _pack.GetParent().AddChild(_packRows);
        // The pack tab is two stacked parts: the summary text, then the rows.
        var packScroll = _pack.GetParent() as ScrollContainer;
        if (packScroll != null)
        {
            var col = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            packScroll.RemoveChild(_pack); packScroll.RemoveChild(_packRows);
            col.AddChild(_pack); col.AddChild(_packRows);
            packScroll.AddChild(col);
        }
        foreach (string arg in OS.GetCmdlineUserArgs()) if (arg.StartsWith("--book=") && int.TryParse(arg.Substring(7), out int t)) GetTree().CreateTimer(4.0).Timeout += () => Toggle(t);
    }

    private RichTextLabel MakeTab(string name)
    {
        var scroll = new ScrollContainer { Name = name, CustomMinimumSize = new Vector2(500, 420) };
        var rtl = new RichTextLabel { BbcodeEnabled = true, FitContent = true, CustomMinimumSize = new Vector2(490, 0), ScrollActive = false, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        rtl.AddThemeFontSizeOverride("normal_font_size", 12);
        scroll.AddChild(rtl);
        _tabs.AddChild(scroll);
        return rtl;
    }

    private void Toggle(int tab)
    {
        if (_panel.Visible && _tabs.CurrentTab == tab) _panel.Visible = false;
        else
        {
            _panel.Visible = true; _tabs.CurrentTab = tab;
            _tabs.SetTabTitle(0, $"Journal ({Core.Keybinds.KeyFor("journal")})");
            _tabs.SetTabTitle(1, $"Log ({Core.Keybinds.KeyFor("log")})");
            _tabs.SetTabTitle(2, $"Pack ({Core.Keybinds.KeyFor("inventory")})");
        }
        IsOpen = _panel.Visible;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (ChatBox.Instance != null && ChatBox.Instance.IsTyping) return;
        if (@event.IsActionPressed("journal")) { Toggle(0); GetViewport().SetInputAsHandled(); }
        else if (@event.IsActionPressed("log")) { Toggle(1); GetViewport().SetInputAsHandled(); }
        else if (@event.IsActionPressed("inventory")) { Toggle(2); GetViewport().SetInputAsHandled(); }
        else if (_panel.Visible && @event.IsActionPressed("ui_cancel")) { _panel.Visible = false; IsOpen = false; GetViewport().SetInputAsHandled(); }
    }

    private float _refresh;
    public override void _Process(double delta)
    {
        IsOpen = _panel.Visible;
        if (!_panel.Visible) return;
        _refresh -= (float)delta;
        if (_refresh > 0f) return;
        _refresh = 0.5f;
        switch (_tabs.CurrentTab)
        {
            case 0: FillJournal(); break;
            case 1: FillLog(); break;
            default: FillPack(); break;
        }
    }

    // What you mean to make of yourself (design doc P.2).
    private void FillAmbitions(System.Text.StringBuilder sb)
    {
        if (_player == null) return;
        if (_player.House.Count > 0)
        {
            sb.AppendLine("[color=#dcb][b]Your house[/b][/color]");
            foreach (var f in _player.House) sb.AppendLine("   " + f.Line);
            sb.AppendLine();
        }
        sb.AppendLine("[color=#dcb][b]What you mean to do[/b][/color]");
        if (_player.Ambitions.Count == 0) sb.AppendLine("   Nothing declared. (Esc - What you mean to do)");
        foreach (var a in _player.Ambitions) sb.AppendLine($"   {a.Line(_player)}");
        sb.AppendLine();
    }

    private void FillJournal()
    {
        if (_player == null) { _journal.Text = ""; return; }
        var lines = _player.Journal;
        var sb = new System.Text.StringBuilder();
        FillAmbitions(sb);
        for (int i = lines.Count - 1; i >= 0; i--) sb.AppendLine(lines[i]);
        _journal.Text = sb.Length == 0 ? "Nothing yet worth the ink. Only what you see with your own eyes goes in here." : sb.ToString();
    }

    private void FillLog()
    {
        var sb = new System.Text.StringBuilder();
        var h = NoticeLog.History;
        for (int i = h.Count - 1; i >= 0; i--) sb.AppendLine($"[color=#bba]{h[i].when}[/color]  {h[i].text}");
        _log.Text = sb.Length == 0 ? "Nothing has happened yet." : sb.ToString();
    }

    private void FillPack()
    {
        if (_player == null) { _pack.Text = ""; return; }
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[b]Load[/b] {_player.Inventory.TotalWeightLbs:0} / {_player.CarryCapacityLbs:0} lb{(_player.IsOverloaded ? "  [color=#f88](overloaded - you are slow and hungry)[/color]" : "")}");
        sb.AppendLine($"[b]In hand[/b] {(_player.Weapon != null ? Items.Items.Of(_player.Weapon.Value).Name : "nothing")}     [b]Worn[/b] warmth {_player.Inventory.ClothingWarmth:0}");
        sb.AppendLine($"[b]Worth[/b] {_player.Inventory.TotalValueMb:0.#} MB in all     [b]Cree[/b] {(_player.CreeFluency >= 0.7f ? "fluent" : _player.CreeFluency >= 0.4f ? "getting by" : _player.CreeFluency >= 0.3f ? "a few words" : "signs only")} ({_player.CreeFluency:0.00})   [b]Nakoda[/b] {(_player.AssiniboineFluency >= 0.7f ? "fluent" : _player.AssiniboineFluency >= 0.4f ? "getting by" : _player.AssiniboineFluency >= 0.3f ? "a few words" : "signs only")} ({_player.AssiniboineFluency:0.00})");
        var sk = _player.Skills;
        sb.AppendLine($"[b]Skills[/b] hunting {sk.Describe(sk.Hunting)} ({sk.Hunting:0.00})   trapping {sk.Describe(sk.Trapping)} ({sk.Trapping:0.00})   trade {sk.Describe(sk.Trade)} ({sk.Trade:0.00})   woodcraft {sk.Describe(sk.Woodcraft)} ({sk.Woodcraft:0.00})");
        foreach (var g in Grudges.Against(_player.DisplayName)) sb.AppendLine($"[color=#f88]Blood debt:[/color] {g.Holder} holds you to account for {g.Victim} - {g.DebtMb:0} MB would settle it.");
        if (_player.Mail.Count > 0)
        {
            sb.AppendLine("[color=#dcb][b]Papers[/b][/color]");
            foreach (var m in _player.Mail) sb.AppendLine($"   {m.Summary}{(m.Read ? $"\n      [color=#998]{m.Text}[/color]" : "")}");
        }
        sb.AppendLine();
        sb.AppendLine("[color=#998]Slots 1-9 are the hotbar. ◄ ► move an item; Wear / Take off changes what's on your body.[/color]");
        _pack.Text = sb.ToString();

        // Rows only rebuild when the pack changes, so the buttons stay clickable.
        var key = new System.Text.StringBuilder();
        foreach (var (id, n) in _player.Inventory.All()) key.Append(id).Append(n).Append(_player.Inventory.IsWorn(id) ? 'w' : '-');
        if (key.ToString() == _packKey) return;
        _packKey = key.ToString();
        foreach (var c in _packRows.GetChildren()) c.QueueFree();
        int slot = 0;
        foreach (var (id, n) in _player.Inventory.All())
        {
            var d = Items.Items.Of(id);
            int idx = slot++;
            var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 6);
            var num = new Label { Text = idx < 9 ? $"{idx + 1}" : "-", CustomMinimumSize = new Vector2(18, 0) }; num.AddThemeFontSizeOverride("font_size", 12); num.Modulate = idx < 9 ? new Color(0.95f, 0.8f, 0.45f) : new Color(0.6f, 0.6f, 0.6f); row.AddChild(num);
            var icon = new TextureRect { Texture = GD.Load<Texture2D>(d.Icon), CustomMinimumSize = new Vector2(22, 22), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered }; row.AddChild(icon);
            string extra = d.Kind switch
            {
                ItemKind.Food => $"feeds {d.Nutrition:0}",
                ItemKind.Drink => $"quenches {d.Thirst:0}" + (d.Intoxication > 0 ? $", {d.Intoxication:0} proof" : ""),
                ItemKind.Clothing => $"warmth {d.Warmth:0}",
                ItemKind.Fuel => $"{d.FuelHours:0} h on the fire",
                ItemKind.Weapon => $"{d.Damage:0} dmg, reach {d.Range:0}",
                ItemKind.Tool => d.Capacity > 0 ? $"+{d.Capacity:0} lb carry" : id == ItemId.Snowshoes ? "snow at full pace" : "use it (click / hotbar)",
                _ => "",
            };
            var name = new Label { Text = $"{d.Name} x{n}   {d.WeightLbs * n:0.#} lb   {d.ValueMb * n:0.#} MB   {extra}", SizeFlagsHorizontal = SizeFlags.ExpandFill }; name.AddThemeFontSizeOverride("font_size", 12); row.AddChild(name);
            if (_player.Inventory.Wearable(id))
            {
                bool worn = _player.Inventory.IsWorn(id);
                var wear = new Button { Text = worn ? "Take off" : "Wear" }; wear.AddThemeFontSizeOverride("font_size", 11);
                int s = idx; wear.Pressed += () => Net.Net.I?.Act("wear", new Godot.Collections.Array { s });
                row.AddChild(wear);
                if (worn) name.Modulate = new Color(0.8f, 1f, 0.8f);
            }
            var left = new Button { Text = "◄" }; left.AddThemeFontSizeOverride("font_size", 11); { int s = idx; left.Pressed += () => Net.Net.I?.Act("move_slot", new Godot.Collections.Array { s, s - 1 }); } left.Disabled = idx == 0; row.AddChild(left);
            var right = new Button { Text = "►" }; right.AddThemeFontSizeOverride("font_size", 11); { int s = idx; right.Pressed += () => Net.Net.I?.Act("move_slot", new Godot.Collections.Array { s, s + 1 }); } right.Disabled = idx >= _player.Inventory.SlotCount - 1; row.AddChild(right);
            _packRows.AddChild(row);
        }
    }
}
