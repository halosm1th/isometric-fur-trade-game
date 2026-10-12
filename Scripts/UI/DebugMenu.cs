using System;
using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Items;
using IsometricFurTradeGame.Scripts.Player;

namespace IsometricFurTradeGame.Scripts.UI;

// F1 test console. Other systems register their own knobs through
// AddSlider / AddButton so this file doesn't need to know about them.
public partial class DebugMenu : Control
{
    public static DebugMenu Instance { get; private set; }

    [Export] public NodePath PlayerPath;

    private PlayerController _player;
    private PanelContainer _panel;
    private VBoxContainer _body;
    private TabContainer _tabs;
    private readonly Dictionary<string, VBoxContainer> _sections = new();
    private Label _status;
    private readonly List<Action> _refreshers = new();
    private readonly List<(Label, Func<string>)> _readouts = new();

    public bool IsOpen => _panel != null && _panel.Visible;
    public static bool IsOpenStatic;
    public override void _Process(double delta) { IsOpenStatic = IsOpen; ProcessPanel(delta); }

    public override void _Ready()
    {
        Instance = this;
        _player = Sim.Player as PlayerController;
        Sim.LocalPlayerChanged += p => { _player = p; OnPlayerBound(); };
        if (_player != null) CallDeferred(nameof(OnPlayerBound));

        _panel = new PanelContainer { Visible = false };
        _panel.SetAnchorsPreset(LayoutPreset.TopRight);
        _panel.GrowHorizontal = GrowDirection.Begin;
        _panel.Position = new Vector2(-12, 12);
        var style = new StyleBoxFlat { BgColor = new Color(0.06f, 0.06f, 0.08f, 0.92f), BorderColor = new Color(0.6f, 0.5f, 0.3f) };
        style.SetBorderWidthAll(1); style.SetCornerRadiusAll(4); style.SetContentMarginAll(10);
        _panel.AddThemeStyleboxOverride("panel", style);
        _panel.Theme = MenuTheme.Get();   // the same furniture as the menus
        AddChild(_panel);

        // Title and status stay put; everything else lives in its own tab,
        // because the console had grown into one long scroll.
        _body = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _body.AddThemeConstantOverride("separation", 6);
        _panel.AddChild(_body);

        var title = new Label { Text = $"Test console ({Core.Keybinds.KeyFor("debug_menu")})" };
        title.AddThemeFontSizeOverride("font_size", 16);
        _body.AddChild(title);
        _status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(380, 0) };
        _status.AddThemeFontSizeOverride("font_size", 12);
        _body.AddChild(_status);
        _tabs = new TabContainer { CustomMinimumSize = new Vector2(400, 500), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _tabs.AddThemeFontSizeOverride("font_size", 12);
        _body.AddChild(_tabs);

        if (!Net.Net.IsServer)
        {
            var l = new Label { Text = "The host runs the world; the test console is theirs." };
            _body.AddChild(l);
            return;
        }
        BuildCalendarSection();
        CallDeferred(nameof(BuildWorldSections));
    }

    private bool _playerSectionBuilt;
    private void OnPlayerBound()
    {
        if (_playerSectionBuilt || _player == null || !Net.Net.IsServer) return;
        _playerSectionBuilt = true;
        BuildPlayerSection();
    }

    private void BuildWorldSections()
    {
        var fort = Sim.Fort;
        if (fort != null)
        {
            const string S = "London & the post";
            AddSlider(S, "Pelt price now", 0.2f, 3f, 0.1f, () => fort.PeltPrice, v => fort.PeltPrice = v);
            AddSlider(S, "Next ship's price", 0.2f, 3f, 0.1f, () => fort.LondonIncentiveNext, v => fort.LondonIncentiveNext = v);
            AddSlider(S, "Quota (MB)", 10f, 300f, 5f, () => fort.QuotaMb, v => { fort.QuotaMb = v; fort.QuotaNextMb = v; }, "0");
            AddButton(S, "Toggle London deciding prices itself (on)", () => { Economy.LondonCommittee.Automatic = !Economy.LondonCommittee.Automatic; Sim.Notice($"London sets prices automatically: {Economy.LondonCommittee.Automatic}"); });
            AddReadout(S, () =>
            {
                int mouths = 0; foreach (var n in Sim.Npcs) if (n.Faction == Agents.Faction.HBC && !n.IsDead) mouths++;
                string post = ""; for (int i = System.Math.Max(0, Mail.Log.Count - 4); i < Mail.Log.Count; i++) post += "\n  " + Mail.Log[i];
                return $"Stores: {fort.Stores.Summary()}\nFurs in store {fort.FursInStoreMb:0} MB; food for {fort.FoodDays(mouths + 1):0} days ({mouths} men)\nDirective: {fort.DirectiveText}\nIn transit: {Mail.Abroad.Count} packet(s){post}";
            });
            AddButtonRow(S,
                ("+50 trade goods", () => fort.Stores.Add(ItemId.TradeGoods, 50)),
                ("+50 food", () => fort.Stores.Add(ItemId.DriedGoose, 50)),
                ("Empty food", () => { foreach (var id in new[] { ItemId.Pemmican, ItemId.DriedGoose, ItemId.Bread, ItemId.Venison, ItemId.Goose, ItemId.DriedMeat }) fort.Stores.TakeUpTo(id, 9999); }));
        }
        AddReadout("Simulation", () => $"Agents: {Sim.Agents.Count} ({Core.Tiers.Report()}); animals {Sim.Animals.Count}\nPaths {World.Pathfinder.Calls} ({World.Pathfinder.TotalMs:0} ms, {World.Pathfinder.SlowCalls} slow)");
        AddReadout("World log", () =>
        {
            var h = NoticeLog.History; var sb = new System.Text.StringBuilder();
            for (int i = h.Count - 1; i >= System.Math.Max(0, h.Count - 30); i--) sb.AppendLine($"{h[i].when}  {h[i].text}");
            return sb.Length == 0 ? "Nothing yet." : sb.ToString();
        });
        if (Sim.Nwc != null)
        {
            const string R = "The rivalry";
            AddReadout(R, () => $"Tension {Economy.Rivalry.Tension:0} ({Economy.Rivalry.Describe()}); lots lost to raids: Company {Sim.Fort?.LotsLostToRaids ?? 0}, Nor'Westers {Sim.Nwc.LotsLostToRaids}");
            AddSlider(R, "Tension", 0f, 100f, 1f, () => Economy.Rivalry.Tension, v => Economy.Rivalry.Tension = v, "0");
            AddReadout(R, () => "Fort William:\n" + Economy.Council.Report());
            AddButton(R, "Convene the council now (as if the brigade came)", () => { Agents.Npc p = null; foreach (var n in Sim.Npcs) if (n.Role == Agents.Role.NwcPartner && !n.IsDead) p = n; var m = Economy.Council.Convene(Sim.Nwc, p, Sim.Nwc.FursInStoreMb, Sim.Nwc.QuotaMb); if (p != null) Mail.Give(m, p); else Mail.Open(m, null); });
            AddReadout(R, () => { var sb = new System.Text.StringBuilder("Blood debts:\n"); foreach (var g in Agents.Grudges.All) sb.AppendLine("  " + g.Summary); return sb.ToString(); });
            AddButtonRow(R,
                ("Nor'Westers raid the fort", () => { Agents.Npc p = null; foreach (var n in Sim.Npcs) if (n.Role == Agents.Role.NwcPartner && !n.IsDead) p = n; if (p != null) Economy.Rivalry.OrderRaid(p, Sim.Nwc, Sim.Fort, 3); }),
                ("Company raids the Nor'Westers", () => { Agents.Npc f = null; foreach (var n in Sim.Npcs) if (n.Role == Agents.Role.Factor && !n.IsDead) f = n; if (f != null) Economy.Rivalry.OrderRaid(f, Sim.Fort, Sim.Nwc, 3); }));
        }
        if (Sim.Outpost != null)
        {
            const string O = "Oxford House";
            AddReadout(O, () => { int m = 0; foreach (var n in Sim.Npcs) if (n.HomeStores == Sim.Outpost && !n.IsDead) m++; return $"Stores: {Sim.Outpost.Stores.Summary()}\nFurs {Sim.Outpost.FursInStoreMb:0} MB; food {Sim.Outpost.FoodDays(m + 1):0} days ({m} souls); price {Sim.Outpost.PeltPrice:0.00}\nLast report sent day {Sim.Outpost.LastReportSentDay}; York last heard day {Sim.Fort?.LastOutpostReportDay}: {Sim.Fort?.OutpostReportText}"; });
            AddButtonRow(O,
                ("Send the fall outfit now", () => { Agents.Npc f = null; foreach (var n in Sim.Npcs) if (n.Role == Agents.Role.Factor && !n.IsDead) f = n; if (f != null) Agents.Delegation.OrderSupply(f); }),
                ("Teleport me to Oxford House", () => _player.GlobalPosition = World.IsoGrid.ToWorld(World.WorldLayout.OutpostHouse.Centre)),
                ("Teleport me to the road head", () => _player.GlobalPosition = World.IsoGrid.ToWorld(World.WorldLayout.RoadHeadYork)));
        }
        var band = Sim.Band;
        if (band != null)
        {
            AddReadout("Cree band", () =>
            {
                int mouths = band.Mouths;
                string other = Sim.Assiniboine != null ? $"\nAssiniboine cache: {Sim.Assiniboine.Stores.Summary()} (standing {Sim.Assiniboine.StandingWithCompany:0}, camp {(Sim.Assiniboine.Camp == Sim.Assiniboine.WinterGround ? "upland" : "at the Bay")})" : "";
                return $"Cache: {band.Stores.Summary()}\nFood for {band.FoodDays(mouths):0} days ({mouths} people){other}";
            });
            AddSlider("Cree band", "Standing w/ Company", 0f, 100f, 1f, () => band.StandingWithCompany, v => band.StandingWithCompany = v, "0");
            AddButtonRow("Cree band", ("Empty their food", () => { foreach (var id in new[] { ItemId.Venison, ItemId.Goose, ItemId.DriedGoose, ItemId.Pemmican, ItemId.DriedMeat }) band.Stores.TakeUpTo(id, 9999); }));
        }
        var eco = Sim.Ecology;
        if (eco != null)
        {
            const string S = "Wildlife";
            AddReadout(S, () => eco.Report() + $"\nLive animals: {Sim.Animals.Count}");
            AddButtonRow(S,
                ("Sites x2", () => { foreach (var s in eco.Sites) s.Count = Mathf.Min(s.Capacity * 1.5f, s.Count * 2f); }),
                ("Sites /2", () => { foreach (var s in eco.Sites) s.Count *= 0.5f; }),
                ("Force scarcity", () => { foreach (var s in eco.Sites) { s.FoodStock = 0.1f; s.Count = s.Capacity * 1.2f; s.Scarcity = true; } }));
            AddButton(S, "March the herd toward this month's ground", () => { foreach (var s in eco.Sites) s.Migrate(GameCalendar.Instance.Month); });
            AddButton(S, "Wolf pack at the gate", () => eco.SpawnPackNear(Ecology.Species.Wolf, World.IsoGrid.ToWorld(World.WorldLayout.GateOutside) + new Vector2(300, 200), 4, true));
            AddButton(S, "Starving caribou at the gate", () => eco.SpawnPackNear(Ecology.Species.Caribou, World.IsoGrid.ToWorld(World.WorldLayout.GateOutside) + new Vector2(-300, 250), 5, true));
            // Buffalo wear the caribou's frames until their own sheet is drawn.
            AddButton(S, "Buffalo at the gate (caribou frames)", () => eco.SpawnPackNear(Ecology.Species.Buffalo, World.IsoGrid.ToWorld(World.WorldLayout.GateOutside) + new Vector2(-420, 120), 4, true));
        }
        var ship = Sim.Ship;
        if (ship != null)
        {
            AddReadout("Ship", () => $"Ship: {ship.Status()}; crew {ship.Crew.Count}");
            AddButtonRow("Ship", ("Summon ship now", ship.Summon), ("Order departure", ship.Depart));
        }
        AddReadout("People", () =>
        {
            var sb = new System.Text.StringBuilder();
            foreach (var n in Sim.Npcs)
            {
                if (n.IsDead) continue;
                sb.Append($"{n.DisplayName}: {n.GoalDescription} (H{n.Needs.Hunger:0} W{n.Needs.Warmth:0} T{n.Needs.Thirst:0} HP{n.Needs.Health:0})\n");
            }
            return sb.ToString();
        });
        AddButtonRow("Save", ("Save (F5)", () => { SaveGame.Save(); Sim.Notice(SaveGame.LastMessage); }), ("Load (F9)", () => { if (!SaveGame.BeginLoad(GetTree())) Sim.Notice(SaveGame.LastMessage); }));
        AddButton("People", "Set the men's house alight", () => World.Fire.I?.Ignite(World.IsoGrid.Cell(World.WorldLayout.MensHouse.Top, 2, 2), "Fire in the men's house!"));
        AddButton("People", "Teleport me to the camp", () => _player.GlobalPosition = World.IsoGrid.ToWorld(World.IsoGrid.Cell(World.WorldLayout.CampCentre, 2, 1)));
        AddButton("People", "Teleport me to the wharf", () => _player.GlobalPosition = World.IsoGrid.ToWorld(World.WorldLayout.WharfRoot));
        AddButton("People", "Teleport me to the trading room", () => _player.GlobalPosition = World.IsoGrid.ToWorld(World.WorldLayout.Depot.Centre));
        Refresh();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed("debug_menu"))
        {
            _panel.Visible = !_panel.Visible;
            if (_panel.Visible) Refresh();
            GetViewport().SetInputAsHandled();
        }
    }

    public void Refresh()
    {
        foreach (var r in _refreshers) r();
    }

    private void ProcessPanel(double delta)
    {
        if (!_panel.Visible) return;
        var cal = GameCalendar.Instance;
        string skip = cal.IsSkipping ? $"  (skipping {cal.SkipRemaining:0.#} h)" : "";
        _status.Text = $"{cal.DateString} {cal.TimeString}{skip}\n{cal.SeasonString}";
        foreach (var (l, f) in _readouts) l.Text = f();
    }

    // ---- registry -------------------------------------------------------

    public VBoxContainer Section(string name)
    {
        if (_sections.TryGetValue(name, out var box)) return box;
        if (_tabs == null)
        {
            box = new VBoxContainer();
            _body.AddChild(box);
            _sections[name] = box;
            return box;
        }
        var scroll = new ScrollContainer { Name = Tab(name), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        _tabs.AddChild(scroll);
        box = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        box.AddThemeConstantOverride("separation", 3);
        scroll.AddChild(box);
        _sections[name] = box;
        // The player's own tab is the one you want open when you press the key.
        if (name == "Player") _tabs.CurrentTab = _tabs.GetTabIdxFromControl(scroll);
        return box;
    }

    // Tab headings want to be short: "London & the post" becomes "London".
    private static string Tab(string name)
    {
        string t = name.Split('&')[0].Trim();
        return t.Length > 14 ? t.Substring(0, 14) : t;
    }

    public void AddButton(string section, string label, Action onPressed)
    {
        var b = new Button { Text = label };
        b.Pressed += onPressed;
        Section(section).AddChild(b);
    }

    public void AddButtonRow(string section, params (string label, Action action)[] buttons)
    {
        var row = new HBoxContainer();
        foreach (var (label, action) in buttons)
        {
            var b = new Button { Text = label, SizeFlagsHorizontal = SizeFlags.ExpandFill };
            b.Pressed += action;
            row.AddChild(b);
        }
        Section(section).AddChild(row);
    }

    public void AddSlider(string section, string label, float min, float max, float step, Func<float> get, Action<float> set, string format = "0.##")
    {
        var row = new HBoxContainer();
        var name = new Label { Text = label, CustomMinimumSize = new Vector2(130, 0) };
        name.AddThemeFontSizeOverride("font_size", 12);
        var slider = new HSlider { MinValue = min, MaxValue = max, Step = step, SizeFlagsHorizontal = SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(120, 0) };
        var value = new Label { CustomMinimumSize = new Vector2(56, 0), HorizontalAlignment = HorizontalAlignment.Right };
        value.AddThemeFontSizeOverride("font_size", 12);
        slider.ValueChanged += v => { set((float)v); value.Text = ((float)v).ToString(format); };
        row.AddChild(name); row.AddChild(slider); row.AddChild(value);
        Section(section).AddChild(row);
        _refreshers.Add(() => { slider.SetValueNoSignal(get()); value.Text = get().ToString(format); });
    }

    public void AddReadout(string section, Func<string> text)
    {
        var l = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        l.AddThemeFontSizeOverride("font_size", 12);
        Section(section).AddChild(l);
        _readouts.Add((l, text));
    }

    public void AddOptionAction(string section, string label, string[] options, Action<int> onPick)
    {
        var row = new HBoxContainer();
        var opt = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        foreach (var o in options) opt.AddItem(o);
        var b = new Button { Text = label };
        b.Pressed += () => onPick(opt.Selected);
        row.AddChild(opt); row.AddChild(b);
        Section(section).AddChild(row);
    }

    // ---- built-in sections ---------------------------------------------

    private void BuildCalendarSection()
    {
        var cal = GameCalendar.Instance;
        const string S = "Calendar";
        AddSlider(S, "Seconds / hour", 0.5f, 60f, 0.5f, () => cal.SecondsPerGameHour, v => cal.SecondsPerGameHour = v);
        AddSlider(S, $"Fast-forward ({Core.Keybinds.KeyFor("time_fast")})", 2f, 400f, 1f, () => cal.FastForwardMultiplier, v => cal.FastForwardMultiplier = v, "0");
        AddButtonRow(S,
            ("+1 h", () => cal.SkipHours(1)),
            ("+6 h", () => cal.SkipHours(6)),
            ("+1 day", () => cal.SkipHours(24)),
            ("+1 week", () => cal.SkipHours(24 * 7)));
        AddButtonRow(S,
            ("+1 month", () => cal.SkipHours(24 * 30)),
            ("Pause / resume", () => cal.Paused = !cal.Paused));

        var row = new HBoxContainer();
        var year = new SpinBox { MinValue = 1800, MaxValue = 1830, Value = cal.Year, CustomMinimumSize = new Vector2(80, 0) };
        var month = new SpinBox { MinValue = 1, MaxValue = 12, Value = cal.Month };
        var day = new SpinBox { MinValue = 1, MaxValue = 31, Value = cal.Day };
        var hour = new SpinBox { MinValue = 0, MaxValue = 23, Value = Mathf.FloorToInt(cal.Hour) };
        var go = new Button { Text = "Set date" };
        go.Pressed += () => cal.SetDate((int)year.Value, (int)month.Value, (int)day.Value, (float)hour.Value);
        row.AddChild(year); row.AddChild(month); row.AddChild(day); row.AddChild(hour); row.AddChild(go);
        Section(S).AddChild(row);
        _refreshers.Add(() => { year.Value = cal.Year; month.Value = cal.Month; day.Value = cal.Day; hour.Value = Mathf.FloorToInt(cal.Hour); });

        AddButtonRow(S,
            ("Blizzard", () => cal.Weather.Force(SkyState.Blizzard, 12f)),
            ("Clear sky", () => cal.Weather.Force(SkyState.Clear, 24f)),
            ("Gale", () => cal.Weather.Force(SkyState.Storm, 8f)));
        AddButtonRow(S,
            ("Ship eve (19 Aug)", () => cal.SetDate(cal.Year, 8, 19, 8)),
            ("Goose hunt (Sep)", () => cal.SetDate(cal.Year, 9, 20, 8)),
            ("Midwinter", () => cal.SetDate(cal.Year, 1, 15, 8)));
    }

    private void BuildPlayerSection()
    {
        if (_player == null) return;
        const string S = "Player";
        var n = _player.Needs;
        AddButton(S, "GOD MODE: no need ever drains, nothing hurts you (off)", () =>
        {
            n.GodMode = !n.GodMode;
            n.Invulnerable = n.GodMode;
            _player.Invulnerable = n.GodMode;
            if (n.GodMode) { n.Hunger = n.Thirst = n.Warmth = n.Health = 100f; }
            Sim.Notice(n.GodMode ? "God mode on: the bars are held full and nothing can hurt you." : "God mode off: you are a man again.");
        });
        AddReadout(S, () => n.GodMode ? "GOD MODE ON - needs held, damage ignored" : "God mode off");
        AddButtonRow(S,
            ("Fill needs", () => { n.Hunger = n.Thirst = n.Warmth = n.Health = 100f; }),
            ("Starve", () => n.Hunger = 5f),
            ("Freeze", () => n.Warmth = 5f),
            ("Hurt", () => n.Health = 20f));
        AddSlider(S, "Health", 0, 100, 1, () => n.Health, v => n.Health = v, "0");
        AddSlider(S, "Warmth", 0, 100, 1, () => n.Warmth, v => n.Warmth = v, "0");
        AddSlider(S, "Food", 0, 100, 1, () => n.Hunger, v => n.Hunger = v, "0");
        AddSlider(S, "Water", 0, 100, 1, () => n.Thirst, v => n.Thirst = v, "0");
        AddSlider(S, "Walk speed", 60, 400, 10, () => _player.MoveSpeed, v => _player.MoveSpeed = v, "0");
        AddButton(S, "Toggle invulnerable", () => _player.Invulnerable = !_player.Invulnerable);
        AddButtonRow(S, ("Get drunk", () => _player.Intoxication = 80f), ("Catch fever", () => _player.CatchFever()), ("Cure", () => { _player.SickDaysLeft = 0f; _player.Intoxication = 0f; }));

        var names = Enum.GetNames<ItemId>();
        AddOptionAction(S, "Give x1", names, i => _player.Inventory.Add((ItemId)i, 1));
        AddOptionAction(S, "Give x10", names, i => _player.Inventory.Add((ItemId)i, 10));
        AddReadout(S, () => $"Carrying {_player.Inventory.TotalWeightLbs:0} lb, clothing warmth {_player.Inventory.ClothingWarmth:0}");
    }
}
