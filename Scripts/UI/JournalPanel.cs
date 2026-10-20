using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Items;
using IsometricFurTradeGame.Scripts.Player;

namespace IsometricFurTradeGame.Scripts.UI;

// J: your book. Four tabs - the journal (what you saw yourself, dated;
// design doc 10), the log (everything the world has announced), your
// pack (every item with weight and worth, and what you're wearing/holding),
// and yourself (design doc 12.7: body, standing, skills and why they moved).
public partial class JournalPanel : Control
{
    public static bool IsOpen { get; private set; }

    private PanelContainer _panel;
    private TabContainer _tabs;
    private RichTextLabel _journal, _log, _pack, _self;
    private VBoxContainer _packRows;
    private GridContainer _grid;
    private readonly List<InventorySlot> _gridSlots = new();
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
        _self = MakeTab("Yourself (C)");
        _packRows = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _packRows.AddThemeConstantOverride("separation", 2);
        _pack.GetParent().AddChild(_packRows);
        // The pack tab is two stacked parts: the summary text, then the slot grid.
        var packScroll = _pack.GetParent() as ScrollContainer;
        if (packScroll != null)
        {
            var col = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            packScroll.RemoveChild(_pack); packScroll.RemoveChild(_packRows);
            col.AddChild(_pack); col.AddChild(_packRows);
            packScroll.AddChild(col);
        }
        foreach (string arg in OS.GetCmdlineUserArgs()) if (arg.StartsWith("--book=") && int.TryParse(arg.Substring(7), out int t)) GetTree().CreateTimer(4.0).Timeout += () => { Toggle(t); GetTree().CreateTimer(1.0).Timeout += PrintPage; };
    }

    // Dev: `--book=N` opens a page of the book and prints it, as the player sees it.
    private void PrintPage()
    {
        var page = _tabs.CurrentTab switch { 0 => _journal, 1 => _log, 2 => _pack, _ => _self };
        GD.Print("[book] " + page.GetParsedText());
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
            _tabs.SetTabTitle(3, $"Yourself ({Core.Keybinds.KeyFor("character")})");
        }
        IsOpen = _panel.Visible;
        Core.Audio.Ui("ui_page_turn");
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (ChatBox.Instance != null && ChatBox.Instance.IsTyping) return;
        if (@event.IsActionPressed("journal")) { Toggle(0); GetViewport().SetInputAsHandled(); }
        else if (@event.IsActionPressed("log")) { Toggle(1); GetViewport().SetInputAsHandled(); }
        else if (@event.IsActionPressed("inventory")) { Toggle(2); GetViewport().SetInputAsHandled(); }
        else if (@event.IsActionPressed("character")) { Toggle(3); GetViewport().SetInputAsHandled(); }
        else if (_panel.Visible && @event.IsActionPressed("ui_cancel")) { _panel.Visible = false; IsOpen = false; GetViewport().SetInputAsHandled(); }
    }

    private float _refresh;
    public override void _Process(double delta)
    {
        IsOpen = _panel.Visible;
        if (!_panel.Visible) return;
        _refresh -= (float)delta;
        if (_refresh > 0f) return;
        _refresh = _tabs.CurrentTab == 2 ? 0.1f : 0.5f;
        switch (_tabs.CurrentTab)
        {
            case 0: FillJournal(); break;
            case 1: FillLog(); break;
            case 3: FillSelf(); break;
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
        // The cellar and the seat: the two things a man at the post actually
        // watches (design doc P.3, 3.7).
        var post = _player.HomePost ?? Sim.Fort;
        if (post != null)
        {
            string flight = World.GooseHunt.Countdown(post);
            int casks = post.Stores.Count(Items.ItemId.SaltGeese);
            float weeks = World.GooseHunt.WeeksInHand(post);
            sb.AppendLine("[color=#dcb][b]The cellar[/b][/color]");
            sb.AppendLine($"   {casks} casks of salt geese, {weeks:0} weeks' food in the store.");
            if (flight != "") sb.AppendLine("   " + flight);
            sb.AppendLine();
        }
        string race = Agents.Rivals.Describe();
        if (race != "")
        {
            sb.AppendLine("[color=#dcb][b]The seat you want[/b][/color]");
            sb.AppendLine("   " + race);
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
        // The campaign's quests: what is under way (and its next step), then
        // what is finished. The server has them; a joined player is sent them.
        var quests = Net.Net.IsServer ? Core.Quests.Summary(_player) : _player.QuestLines;
        if (quests.Count > 0)
        {
            sb.AppendLine("[color=#dcb][b]Quests[/b][/color]");
            foreach (var q in quests) sb.AppendLine("   " + q);
            sb.AppendLine();
        }
        sb.Append(Core.Campaign.JournalText);
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
        sb.AppendLine($"[color=#998]Skills, body and standing are on the Yourself page ({Core.Keybinds.KeyFor("character")}).[/color]");
        foreach (var g in Grudges.Against(_player.DisplayName)) sb.AppendLine($"[color=#f88]Blood debt:[/color] {g.Holder} holds you to account for {g.Victim} - {g.DebtMb:0} MB would settle it.");
        if (_player.Mail.Count > 0)
        {
            sb.AppendLine("[color=#dcb][b]Papers[/b][/color]");
            foreach (var m in _player.Mail) sb.AppendLine($"   {m.Summary}{(m.Read ? $"\n      [color=#998]{m.Text}[/color]" : "")}");
        }
        sb.AppendLine();
        sb.AppendLine("[color=#998]The first row is the hotbar. Drag items between squares to arrange them (an occupied square swaps). Right-click uses an item, or wears / takes off clothing.[/color]");
        _pack.Text = sb.ToString();

        EnsureGrid();
        for (int i = 0; i < _gridSlots.Count; i++) _gridSlots[i].Refresh(_player, i < Hotbar.Slots ? Core.Keybinds.KeyFor($"hotbar_{i + 1}") : "");
    }

    // ---- yourself (design doc 12.7) ---------------------------------------------------
    // Nothing here is a new mechanic: it is the state the simulation already
    // keeps on you, laid out for the one agent allowed to see it directly.
    private static string Head(string t) => $"[color=#dcb][b]{t}[/b][/color]";
    private static string Bar(float v, int n = 10) { int k = Mathf.Clamp(Mathf.RoundToInt(v * n), 0, n); return $"[color=#cb8]{new string('|', k)}[/color][color=#554]{new string('.', n - k)}[/color]"; }
    private static string Need(float v) => v >= 75f ? "well" : v >= 50f ? "fair" : v >= Needs.Weak ? "[color=#eb8]poor[/color]" : "[color=#f88]failing[/color]";
    private static string Tongue(float f) => f >= 0.7f ? "fluent" : f >= 0.4f ? "getting by" : f >= 0.3f ? "a few words" : "signs only";

    private void FillSelf()
    {
        var p = _player;
        if (p == null) { _self.Text = ""; return; }
        var sb = new System.Text.StringBuilder();
        var cal = GameCalendar.Instance;

        // Who you are.
        sb.AppendLine($"[font_size=15][b]{p.DisplayName}[/b][/font_size], {p.Age} years old");
        if (p.IsBandPerson) sb.AppendLine($"   Born to the {World.Camps.NationName(p.Faction)}{(p.HomeCamp != "" ? $", of {p.HomeCamp}" : "")}");
        else sb.AppendLine($"   {Cap(p.Rank)} of {World.Posts.CompanyOf(p)}{(p.HomePost != null ? $", at {PlaceOf(p.HomePost)}" : "")}");
        if (p.BackgroundTitle != "") sb.AppendLine($"   {p.BackgroundTitle}{(p.TraitTitles.Count > 0 ? "; " + string.Join(", ", p.TraitTitles).ToLower() : "")}");
        if (!p.IsBandPerson)
        {
            float was = p.LastYearStanding > -999f ? p.LastYearStanding : p.Standing;
            float d = p.Standing - was;
            string trend = Mathf.Abs(d) < 0.5f ? "" : d > 0 ? $"  [color=#9c8](+{d:0} this year)[/color]" : $"  [color=#e98]({d:0} this year)[/color]";
            sb.AppendLine($"   Standing {Bar(p.Standing / 100f)} {p.Standing:0} - {StandingWord(p.Standing)}{trend}");
            sb.AppendLine($"   {p.TasksDone} tasks done, {p.TasksRefused} refused; {p.PrivateWorthMb:0.#} MB of your own put by");
        }
        sb.AppendLine();

        // The body.
        sb.AppendLine(Head("Your body"));
        var n = p.Needs;
        sb.AppendLine($"   Health {Bar(n.Health / 100f)} {Need(n.Health)}      Food   {Bar(n.Hunger / 100f)} {Need(n.Hunger)}");
        sb.AppendLine($"   Water  {Bar(n.Thirst / 100f)} {Need(n.Thirst)}      Warmth {Bar(n.Warmth / 100f)} {Need(n.Warmth)}");
        var conds = new List<string>();
        if (p.IsSick) conds.Add($"[color=#f88]fever[/color], {p.SickDaysLeft:0} days more");
        if (p.HasScurvy) conds.Add($"[color=#f88]scurvy[/color] - {p.DaysSinceFresh:0} days since fresh food; berries, fresh meat or spruce beer will mend it");
        else if (p.DaysSinceFresh > 28f) conds.Add($"{p.DaysSinceFresh:0} days on salt and dried food - your gums are starting to bleed");
        if (p.IsDrunk) conds.Add("[color=#eb8]drunk[/color] - slow, and a poor shot");
        else if (p.Intoxication > 5f) conds.Add("a dram or two in you");
        if (p.IsOverloaded) conds.Add($"[color=#eb8]overloaded[/color] ({p.Inventory.TotalWeightLbs:0} of {p.CarryCapacityLbs:0} lb)");
        if (n.Health < 70f && !p.IsSick) conds.Add("hurt - a woman of the post or the camp can see to your wounds");
        if (p.CaptiveOf != null) conds.Add($"[color=#f88]held[/color] by {(p.CaptiveOf.Company == Faction.NWC ? "the Nor'Westers" : "the Company")}");
        if (cal != null && p.ParoleUntilDay > cal.Day) conds.Add($"on parole to the {(p.ParoleTo == Faction.NWC ? "Nor'Westers" : "Company")}: no arms against them");
        foreach (var g in Grudges.Against(p.DisplayName)) conds.Add($"[color=#f88]blood debt[/color] to {g.Holder} for {g.Victim}");
        sb.AppendLine(conds.Count == 0 ? "   Nothing ails you." : "   " + string.Join("\n   ", conds));
        sb.AppendLine();

        // Skills, and why they moved (design doc 12.7: so a player can see
        // why a number moved rather than just that it did).
        sb.AppendLine(Head("What you can do"));
        var sk = p.Skills;
        for (int i = 0; i < 4; i++)
        {
            float v = sk[i];
            var (gain, from) = sk.Recent(i, 30);
            var (next, at) = Skills.NextStep(v);
            string trend = gain >= 0.0005f ? $"  [color=#9c8]+{gain:0.000} this month[/color]" : "";
            sb.AppendLine($"   {Skills.Names[i]} {Bar(v)} {sk.Describe(v)} ({v:0.00}){trend}");
            if (from.Count > 0)
            {
                var parts = new List<string>();
                for (int k = 0; k < from.Count && k < 3; k++) parts.Add($"{from[k].Why} ({from[k].Times}x)");
                sb.AppendLine($"      [color=#998]from {string.Join(", ", parts)}[/color]");
            }
            if (next != "") sb.AppendLine($"      [color=#776]{next} at {at:0.00}; it {HowToLearn(i)}[/color]");
        }
        sb.AppendLine($"   Cree {Bar(p.CreeFluency)} {Tongue(p.CreeFluency)}      Nakoda {Bar(p.AssiniboineFluency)} {Tongue(p.AssiniboineFluency)}");
        sb.AppendLine();

        // The people (design doc P.8): who you count on, and who counts against you.
        sb.AppendLine(Head("Your people"));
        if (p.Spouse != null) sb.AppendLine($"   Married to {p.Spouse.DisplayName}{(p.Spouse.IsDead ? " (dead)" : "")}.");
        if (p.KinNames.Count > 0) sb.AppendLine($"   Kin: {string.Join(", ", p.KinNames)}.");
        var friends = new List<(Npc, float)>(); var enemies = new List<(Npc, float)>();
        foreach (var o in Sim.Npcs)
        {
            if (o.IsDead) continue;
            float r = o.RapportWith(p);
            if (r >= 25f) friends.Add((o, r)); else if (r <= -20f) enemies.Add((o, r));
        }
        friends.Sort((a, b) => b.Item2.CompareTo(a.Item2)); enemies.Sort((a, b) => a.Item2.CompareTo(b.Item2));
        string Names(List<(Npc, float)> l) { var s = new List<string>(); for (int k = 0; k < l.Count && k < 6; k++) s.Add($"{l[k].Item1.DisplayName} ({RapportWord(l[k].Item2)})"); return string.Join(", ", s); }
        sb.AppendLine(friends.Count > 0 ? $"   Well disposed: {Names(friends)}" : "   Nobody yet thinks of you as a friend.");
        if (enemies.Count > 0) sb.AppendLine($"   [color=#e98]Against you: {Names(enemies)}[/color]");
        _self.Text = sb.ToString();
    }

    private static string PlaceOf(Economy.FortLedger post) =>
        post is Economy.TradingPostLedger tp ? tp.Post.Name : post == Sim.Fort ? "York Factory" : post == Sim.Nwc ? "the Nor'Westers' house" : post == Sim.Outpost ? "Oxford House" : post.Title;
    private static string Cap(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s.Substring(1);
    private static string StandingWord(float s) => s >= 70f ? "trusted with a post" : s >= 50f ? "well thought of" : s >= 30f ? "a sound man" : s >= 15f ? "one of the hands" : "watched";
    private static string RapportWord(float r) => r >= 70f ? "devoted" : r >= 45f ? "a friend" : r >= 25f ? "warm" : r <= -50f ? "hates you" : "cold";
    private static string HowToLearn(int skill) => skill switch
    {
        0 => "comes of shooting game at a distance",
        1 => "comes of setting traps at beaver lodges",
        2 => "comes of bargaining at counters and camps",
        _ => "comes of felling and splitting wood",
    };

    // The pack as a grid of squares, nine to a row; the first row is the hotbar.
    // It grows a row when the last one is used.
    private void EnsureGrid()
    {
        int span = _player.Inventory.SlotSpan;
        int want = System.Math.Max(36, (span + Hotbar.Slots) / Hotbar.Slots * Hotbar.Slots);
        if (_grid == null)
        {
            _grid = new GridContainer { Columns = Hotbar.Slots };
            _grid.AddThemeConstantOverride("h_separation", 4); _grid.AddThemeConstantOverride("v_separation", 4);
            _packRows.AddChild(_grid);
        }
        while (_gridSlots.Count < want)
        {
            int idx = _gridSlots.Count;
            var slot = new InventorySlot { Index = idx, Hotbar = idx < Hotbar.Slots, CustomMinimumSize = new Vector2(46, 46) };
            if (idx < Hotbar.Slots) slot.LeftClicked += i => Hotbar.SelectSlot(i);
            _grid.AddChild(slot); _gridSlots.Add(slot);
        }
        for (int i = 0; i < _gridSlots.Count; i++) _gridSlots[i].Selected = i == Hotbar.SelectedSlot;
    }
}
