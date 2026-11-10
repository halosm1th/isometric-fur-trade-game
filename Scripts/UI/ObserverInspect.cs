using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Items;

namespace IsometricFurTradeGame.Scripts.UI;

// The observer's look at one person (Core/Observer.Picked): click someone
// and this tells who they are, what they are doing and why, how they are
// keeping, what they carry, whom they love and hate, and what they remember.
// Read live from the agent a few times a second while the camera follows.
public partial class ObserverInspect : PanelContainer
{
    private RichTextLabel _text;
    private float _timer;
    private Label _status;
    private OptionButton _targets;
    private Label _groupLabel;
    private Control _groupBox, _personBox;
    private Economy.Household _groupFor;
    private readonly List<Economy.Household> _targetList = new();
    private Agent _shownFor;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsPreset(LayoutPreset.TopRight);
        AnchorLeft = 1f; AnchorRight = 1f; AnchorTop = 0f; AnchorBottom = 1f;
        OffsetLeft = -420f; OffsetRight = -12f; OffsetTop = 60f; OffsetBottom = -90f;
        var box = new StyleBoxFlat { BgColor = new Color(0.07f, 0.06f, 0.05f, 0.88f), BorderColor = new Color(0.55f, 0.45f, 0.3f), ContentMarginLeft = 12, ContentMarginRight = 12, ContentMarginTop = 8, ContentMarginBottom = 8 };
        box.SetBorderWidthAll(1); box.SetCornerRadiusAll(4);
        AddThemeStyleboxOverride("panel", box);

        var col = new VBoxContainer();
        AddChild(col);
        var top = new HBoxContainer();
        col.AddChild(top);
        top.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        var close = new Button { Text = "×", Flat = true, TooltipText = "Stop looking (click empty ground)" };
        close.Pressed += () => { Observer.Pick(null); };
        top.AddChild(close);
        var tabs = new TabContainer { SizeFlagsVertical = SizeFlags.ExpandFill };
        col.AddChild(tabs);
        _text = new RichTextLabel { Name = "Who", BbcodeEnabled = true, ScrollActive = true, SizeFlagsVertical = SizeFlags.ExpandFill, SelectionEnabled = false };
        _text.AddThemeFontSizeOverride("normal_font_size", 14);
        _text.AddThemeFontSizeOverride("bold_font_size", 14);
        tabs.AddChild(_text);
        var scroll = new ScrollContainer { Name = "Powers", HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        tabs.AddChild(scroll);
        scroll.AddChild(BuildPowers());
        if (System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--powerstab") >= 0) tabs.CurrentTab = 1;
        _status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(0, 40) };
        _status.AddThemeFontSizeOverride("font_size", 13);
        _status.AddThemeColorOverride("font_color", new Color(0.95f, 0.85f, 0.55f));
        col.AddChild(_status);
        Visible = false;
    }

    // ---- the powers (UI/ObserverPowers) -------------------------------------------------

    private Control BuildPowers()
    {
        var v = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        v.AddThemeConstantOverride("separation", 4);
        Label Head(string t)
        {
            var l = new Label { Text = t };
            l.AddThemeColorOverride("font_color", new Color(0.85f, 0.72f, 0.48f));
            return l;
        }
        HFlowContainer Row(Control into) { var r = new HFlowContainer(); r.AddThemeConstantOverride("h_separation", 4); r.AddThemeConstantOverride("v_separation", 4); into.AddChild(r); return r; }
        void Btn(HFlowContainer row, string text, string tip, System.Func<Agent, string> act)
        {
            var b = new Button { Text = text, TooltipText = tip };
            b.Pressed += () => { if (Observer.Picked is { } a && IsInstanceValid(a)) Say(act(a)); };
            row.AddChild(b);
        }
        void Pair(HFlowContainer row, string text, string ask, System.Func<Agent, Agent, string> act)
        {
            var b = new Button { Text = text + "...", TooltipText = $"Then click {ask}" };
            b.Pressed += () =>
            {
                if (Observer.Picked is not { } a || !IsInstanceValid(a)) return;
                Say($"Click {ask}. Right-click to stop.");
                Observer.Targeting = other => { Observer.Targeting = null; Say(other == null ? "Nobody there." : act(a, other)); };
            };
            row.AddChild(b);
        }

        var person = new VBoxContainer();
        _personBox = person;
        v.AddChild(person);
        person.AddChild(Head("This person"));
        var r1 = Row(person);
        Btn(r1, "Inspire", "Braver, steadier, the winter's weight lifted", ObserverPowers.Inspire);
        Btn(r1, "Dishearten", "Timid, sour, near the end of the rope", ObserverPowers.Dishearten);
        Btn(r1, "Heal", "Mend, feed and warm them", ObserverPowers.Heal);
        Btn(r1, "Wound", "Strike them down sore", ObserverPowers.Wound);
        Btn(r1, "Strike dead", "Kill them where they stand", ObserverPowers.StrikeDead);
        person.AddChild(Head("Send them to"));
        var r2 = Row(person);
        Btn(r2, "Hunt", "Walk out to the best game near", ObserverPowers.Hunt);
        Btn(r2, "Fish", "Fish the nearest water", ObserverPowers.Fish);
        Btn(r2, "Forage", "Gather what the country gives", ObserverPowers.Forage);
        Btn(r2, "Go home", "Walk home to their bed", ObserverPowers.GoHome);
        person.AddChild(Head("Toward someone else (click them next)"));
        var r3 = Row(person);
        Pair(r3, "Turn against", "the one to hate", ObserverPowers.SetAgainst);
        Pair(r3, "Befriend", "the one to love", ObserverPowers.Befriend);
        Pair(r3, "Attack", "the one to fall on", ObserverPowers.Attack);
        Pair(r3, "Follow", "the one to walk with", ObserverPowers.Follow);

        var group = new VBoxContainer();
        _groupBox = group;
        v.AddChild(new HSeparator());
        v.AddChild(group);
        _groupLabel = Head("Their people");
        group.AddChild(_groupLabel);
        var g1 = Row(group);
        void GBtn(HFlowContainer row, string text, string tip, System.Func<Economy.Household, string> act)
        {
            var b = new Button { Text = text, TooltipText = tip };
            b.Pressed += () => { if (_groupFor != null) Say(act(_groupFor)); };
            row.AddChild(b);
        }
        void TBtn(HFlowContainer row, string text, string tip, System.Func<Economy.Household, Economy.Household, string> act)
        {
            var b = new Button { Text = text, TooltipText = tip };
            b.Pressed += () =>
            {
                int i = _targets.Selected;
                if (_groupFor == null || i < 0 || i >= _targetList.Count) { Say("Pick the other people first."); return; }
                Say(act(_groupFor, _targetList[i]));
            };
            row.AddChild(b);
        }
        GBtn(g1, "Hunt", "A buffalo run on the plains; elsewhere the men walk out to the game", ObserverPowers.GroupHunt);
        GBtn(g1, "Go in to trade", "A camp's men carry its furs to the nearest post", ObserverPowers.GroupTrade);
        group.AddChild(new Label { Text = "Against:" });
        _targets = new OptionButton { FitToLongestItem = false, ClipText = true, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        group.AddChild(_targets);
        var g2 = Row(group);
        TBtn(g2, "War party", "Their fighting men ride against them", ObserverPowers.GroupWar);
        TBtn(g2, "Horse raid", "The young men go after their horses", ObserverPowers.GroupRaid);
        TBtn(g2, "Stir a feud", "A bitter score: raids and war parties will come of it", ObserverPowers.GroupFeud);
        TBtn(g2, "Make peace", "A truce for a season, old scores laid down", ObserverPowers.GroupPeace);
        return v;
    }

    private void Say(string text) { _status.Text = text ?? ""; if (!string.IsNullOrEmpty(text)) GD.Print($"[powers] {text}"); }

    // The people of whoever is picked, and the others they could be set against.
    private void RefreshPowers(Agent a)
    {
        if (a != _shownFor) { _shownFor = a; Say(""); Observer.Targeting = null; }
        _personBox.Visible = a is Npc;
        var h = (a as Npc)?.HomeStores;
        if (h != null && !IsInstanceValid(h)) h = null;
        _groupBox.Visible = h != null;
        if (h == _groupFor) return;
        _groupFor = h;
        _targets.Clear(); _targetList.Clear();
        if (h == null) return;
        _groupLabel.Text = $"Their people: {ObserverPowers.GroupName(h)}";
        foreach (var (g, label) in ObserverPowers.Groups(h)) { _targetList.Add(g); _targets.AddItem(label); }
        if (_targetList.Count > 0) _targets.Select(0);
    }

    public override void _Process(double delta)
    {
        var a = Observer.Picked;
        if (a != null && !IsInstanceValid(a)) { Observer.Pick(null); a = null; }
        Visible = a != null;
        if (a == null) return;
        _timer -= (float)delta;
        if (_timer > 0f) return;
        _timer = 0.25f;
        RefreshPowers(a);
        double scroll = _text.GetVScrollBar().Value;
        _text.Text = Describe(a);
        _text.GetVScrollBar().Value = scroll;
    }

    private static string Words(string camel)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < camel.Length; i++)
        {
            if (i > 0 && char.IsUpper(camel[i]) && !char.IsUpper(camel[i - 1])) sb.Append(' ');
            sb.Append(camel[i]);
        }
        return sb.ToString();
    }

    private static string Esc(string s) => (s ?? "").Replace("[", "[lb]");

    private static string Head(string s) => $"\n[color=#d9b77a][b]{s}[/b][/color]\n";

    // One row of the condition table (Bars opens it, the caller closes it).
    private static string Bar(string name, float v)
    {
        v = Mathf.Clamp(v, 0f, 100f);
        string colour = v < 25f ? "#e0584a" : v < 55f ? "#e0b44a" : "#8fc46a";
        int fill = Mathf.RoundToInt(v / 10f);
        return $"[cell]{name}  [/cell][cell][color={colour}]{new string('█', fill)}[/color][color=#3a342c]{new string('█', 10 - fill)}[/color][/cell][cell]  {v:0}[/cell]";
    }

    private static string Trait(float v, string low, string high) => v < 0.25f ? $"very {low}" : v < 0.45f ? low : v <= 0.55f ? $"neither {low} nor {high}" : v <= 0.75f ? high : $"very {high}";

    private static string Describe(Agent a)
    {
        var s = new StringBuilder();
        s.Append($"[font_size=18][b]{Esc(a.Describe())}[/b][/font_size]\n");

        if (a is Animal an)
        {
            s.Append($"{an.Def.Name}, {(an.IsPredator ? "a hunter" : "grazing kind")}{(an.Hungry ? ", hungry" : "")}\n");
            if (an.IsDead) s.Append("[color=#e0584a]Dead.[/color]\n");
            s.Append(Head("Condition"));
            s.Append("[table=3]" + Bar("Health", an.Needs.Health) + Bar("Food", an.Needs.Hunger) + "[/table]\n");
            if (an.Site != null) s.Append($"\nOf the herd at {Esc(an.Site.Name)}.\n");
            return s.ToString();
        }

        var npc = a as Npc;
        string who = npc != null ? Words(npc.Role.ToString()) : "a trader";
        s.Append($"{who} · {Words(a.Faction.ToString())} · {(npc?.IsFemale == true ? "woman" : "man")} of {a.Age}\n");
        if (npc?.HomeStores != null)
        {
            string home = Economy.War.NameOf(npc.HomeStores);
            string lead = npc.HomeStores.Leader != null && npc.HomeStores.Leader != npc ? $", under {Esc(npc.HomeStores.Leader.Describe())}" : npc.HomeStores.Leader == npc ? ", its head" : "";
            s.Append($"Of {Esc(home)}{lead}\n");
        }

        // What they are doing.
        s.Append(Head("Doing"));
        if (a.IsDead) s.Append($"[color=#e0584a]Dead{(a.Killer != null ? $", killed by {Esc(a.Killer.Describe())}" : npc?.DeathCause != null ? $": {Esc(npc.DeathCause)}" : "")}.[/color]\n");
        else if (npc != null)
        {
            var state = new List<string>();
            if (npc.Sleeping) state.Add("asleep");
            if (npc.Conversing) state.Add("talking");
            if (npc.AtHearth != null) state.Add("at the fire");
            if (npc.HeldBy != null) state.Add("held prisoner");
            if (npc.Away) state.Add($"away{(npc.AwayWith != "" ? " with " + Esc(npc.AwayWith) : "")}");
            if (npc.Following != null) state.Add($"with {Esc(npc.Following.Describe())}");
            if (npc.Alerted > 0f) state.Add("on guard");
            if (state.Count > 0) s.Append(string.Join(", ", state) + "\n");
            string goals = npc.GoalDescription;
            s.Append(goals == "idle" ? "Nothing in hand.\n" : $"{Esc(goals)}\n");
            if (!string.IsNullOrEmpty(npc.AssignedTaskName)) s.Append($"[color=#a89c88]Standing orders: {Esc(npc.AssignedTaskName)}[/color]\n");
            if (npc.OrderWaiting != null) s.Append($"[color=#a89c88]An order waits for him from {Esc(npc.OrderWaitingFrom?.Describe() ?? "his master")}[/color]\n");
            if (!string.IsNullOrEmpty(npc.PendingReport)) s.Append($"[color=#a89c88]Has news to report: {Esc(npc.PendingReport)}[/color]\n");
        }
        else s.Append("Another player.\n");

        // How they are keeping.
        s.Append(Head("Condition"));
        s.Append("[table=3]" + Bar("Health", a.Needs.Health) + Bar("Food", a.Needs.Hunger) + Bar("Water", a.Needs.Thirst) + Bar("Warmth", a.Needs.Warmth) + "[/table]\n");
        var ills = new List<string>();
        if (a.IsSick) ills.Add($"fever, {a.SickDaysLeft:0} days to go");
        if (a.HasScurvy) ills.Add("scurvy");
        else if (a.DaysSinceFresh > 28f) ills.Add($"no fresh food in {a.DaysSinceFresh:0} days");
        if (a.IsDrunk) ills.Add("drunk");
        if (npc != null && npc.Strain > 40f) ills.Add(npc.Strain > 75f ? "near breaking with the winter" : "worn by the winter");
        if (ills.Count > 0) s.Append($"[color=#e0b44a]{string.Join(", ", ills)}[/color]\n");
        if (npc != null && (npc.Faction == Faction.HBC || npc.Faction == Faction.NWC) && npc.Role is Role.Labourer or Role.Clerk or Role.Voyageur or Role.Postmaster)
            s.Append($"Standing with his master: {npc.Standing:0}\n");

        // What they can do and who they are.
        s.Append(Head("Skills"));
        for (int i = 0; i < Skills.Count; i++)
            s.Append($"{Skills.Names[i]}: {a.Skills.Describe(a.Skills[i])} [color=#7d7363]({a.Skills[i] * 100f:0})[/color]\n");
        if (npc != null)
        {
            var p = npc.Personality;
            s.Append(Head("Nature"));
            s.Append($"{Trait(p.Loyalty, "fickle", "loyal")}, {Trait(p.Talkative, "close", "talkative")}, {Trait(p.Suspicion, "trusting", "suspicious")}, {Trait(p.Ambition, "content", "ambitious")}, {Trait(p.Courage, "timid", "brave")}\n");
        }

        // Family and feelings.
        if (npc != null)
        {
            var fam = new List<string>();
            if (npc.Spouse != null) fam.Add($"married to {Esc(npc.Spouse.Describe())}");
            if (npc.Father != null) fam.Add($"child of {Esc(npc.Father.Describe())}");
            else if (npc.FatherName != "") fam.Add($"child of {Esc(npc.FatherName)}");
            var kids = npc.Children.Where(c => IsInstanceValid(c)).Select(c => c.Describe() + (c.IsDead ? " (dead)" : "")).ToList();
            if (kids.Count > 0) fam.Add($"children: {Esc(string.Join(", ", kids))}");
            var liked = npc.Rapport.Where(r => IsInstanceValid(r.Key) && r.Value >= 15f).OrderByDescending(r => r.Value).Take(4).ToList();
            var hated = npc.Rapport.Where(r => IsInstanceValid(r.Key) && r.Value <= -15f).OrderBy(r => r.Value).Take(4).ToList();
            if (fam.Count > 0 || liked.Count > 0 || hated.Count > 0)
            {
                s.Append(Head("People"));
                foreach (var f in fam) s.Append(f + "\n");
                if (liked.Count > 0) s.Append($"[color=#8fc46a]Likes[/color] {Esc(string.Join(", ", liked.Select(r => $"{r.Key.Describe()} ({r.Value:0})")))}\n");
                if (hated.Count > 0) s.Append($"[color=#e0584a]Dislikes[/color] {Esc(string.Join(", ", hated.Select(r => $"{r.Key.Describe()} ({r.Value:0})")))}\n");
            }
        }

        // What they carry.
        var lots = new List<string>();
        for (int i = 0; i < a.Inventory.SlotSpan; i++)
            if (a.Inventory.Slot(i) is { } st && st.count > 0) lots.Add(IsometricFurTradeGame.Scripts.Items.Items.Lot(st.id, st.count));
        s.Append(Head("Carrying"));
        s.Append(lots.Count == 0 ? "Nothing.\n" : Esc(string.Join(", ", lots)) + "\n");
        if (a.Mail.Count > 0) s.Append($"{a.Mail.Count} letter{(a.Mail.Count == 1 ? "" : "s")}\n");

        // What they remember, newest first.
        if (npc != null && npc.Memories.Count > 0)
        {
            s.Append(Head("Remembers"));
            for (int i = npc.Memories.Count - 1, n = 0; i >= 0 && n < 6; i--, n++)
            {
                var m = npc.Memories[i];
                string how = m.Directness <= 1 ? "saw" : m.Directness == 2 ? "heard" : "heard said";
                s.Append($"[color=#7d7363]{how}:[/color] {Esc(m.Text)}\n");
            }
        }
        return s.ToString();
    }
}
