using System;
using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Agents.Goals;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Items;

namespace IsometricFurTradeGame.Scripts.UI;

// Three things that make talk more than a menu of questions.
//
// Reading people (design doc 3.5): what someone is after is never shown;
// it is learned. Every exchange with a person teaches you a little of them
// (Insight, kept per person), and as it grows the greeting tells you more -
// a vague sense of what presses on him, then what he means to do, then the
// steps he is taking to do it - and you can tell when he is lying to you
// about it.
//
// Passing the time: they raise something - the cold, the master, the other
// house, a thing they heard - with a view of their own, and what you answer
// matters. Agreeing warms most people; pushing back is respected by the bold
// and resented by the timid; a joke lands with a talker and not with a man
// in mourning. They remember what you said, and notice when you say the
// opposite next time. Agreeing that the master is a tyrant can get back to
// the master.
//
// Talking someone round (design doc 3.6's "persuade"): a thing you want of
// them - a word put in with their master, silence about what they saw you
// do, their furs at your house, the other house's plans, a quarrel made up -
// and the way you put it to them: their interest, your friendship, what is
// right, a gift, or fear. Which appeal works is read off who they are; the
// better you know them, the more plainly you see which.
public partial class Conversation
{
    // ---- reading people -------------------------------------------------------------

    // How well you have the measure of this person, 0..1. Kept in the
    // acquaintance store as "read:NNN" so it saves with everything else.
    private float Insight(Npc npc)
    {
        foreach (var t in Acquaintance.Topics(_player, npc))
            if (t.StartsWith("read:") && int.TryParse(t.AsSpan(5), out int n)) return n / 1000f;
        return 0f;
    }

    private void Study(Npc npc, float amount)
    {
        if (npc == null || amount <= 0f) return;
        // A close, suspicious man is slower to read than an open one.
        amount *= Mathf.Lerp(1.3f, 0.6f, npc.Personality.Suspicion);
        int now = Mathf.RoundToInt(Mathf.Min(1f, Insight(npc) + amount) * 1000f);
        Acquaintance.ForgetPrefix(_player, npc, "read:");
        Acquaintance.Add(_player, npc, $"read:{now}");
    }

    private const float SenseAt = 0.15f, GoalAt = 0.45f, StepsAt = 0.75f;

    // The plan under the moment: his goals from the bottom of the stack up,
    // with the body's needs (tier 0) and idle waiting left out.
    private static List<Goal> Plan(Npc npc)
    {
        var plan = new List<Goal>();
        foreach (var g in npc.Brain.Stack)
            if (g.Tier > 0 && !Idle(g.Label)) plan.Add(g);
        return plan;
    }

    private static bool Idle(string label) => label == "" || label == "about" || label.StartsWith("wait") || label == "on their way somewhere" || label == "sitting by the fire";

    // What sort of thing a goal is, for a man who can only sense it.
    private static string Sense(string label)
    {
        string l = label.ToLowerInvariant();
        if (l.Contains("war party") || l.Contains("seize") || l.Contains("fight") || l.Contains("steal") || l.Contains("lie up") || l.Contains("brawl")) return "There is violence in them today; it sits just under the skin.";
        if (l.Contains("hunt") || l.Contains("trap") || l.Contains("snare") || l.Contains("fish") || l.Contains("buffalo") || l.Contains("small game")) return "Their mind is out in the bush; they keep glancing at the treeline.";
        if (l.Contains("trade") || l.Contains("furs") || l.Contains("load") || l.Contains("ship")) return "They are reckoning something - goods, or a price.";
        if (l.Contains("carry") || l.Contains("road") || l.Contains("report") || l.Contains("go to") || l.Contains("look for") || l.Contains("fetch")) return "They have somewhere to be, and you are keeping them from it.";
        if (l.Contains("guard")) return "They are watchful, and not of you.";
        return "They are about their work and would like to get on with it.";
    }

    // Goals a man keeps to himself unless he trusts you.
    private static bool Secret(string label)
    {
        string l = label.ToLowerInvariant();
        return l.Contains("steal") || l.Contains("seize") || l.Contains("lie up") || l.Contains("war party") || l.Contains("with the take");
    }

    // The greeting's read of them, as far as your knowledge of them goes.
    private string ReadLine(Npc npc)
    {
        float k = Insight(npc);
        if (k < SenseAt) return "";
        var plan = Plan(npc);
        if (npc.Brain.Tier0Goal() is Goal need && k >= GoalAt) return $" You can see it in them: they want to {need.Label} before anything else.";
        if (plan.Count == 0) return k >= GoalAt ? " You know them well enough to see there is nothing much on their mind." : "";
        var top = plan[^1]; var bottom = plan[0];
        if (k < GoalAt) return " " + Sense(bottom.Label);
        if (k < StepsAt || plan.Count == 1) return $" You know that look: they mean to {bottom.Label}.";
        var steps = new List<string>();
        for (int i = 1; i < plan.Count; i++) steps.Add(plan[i].Label);
        return $" You can read them like a page: they are set on {bottom.Label}; for that, {string.Join(", then ", steps)}.";
    }

    // "What are you about?" A plain question. A man doing something he would
    // rather you didn't know of lies, unless he trusts you; your knowledge of
    // him is what lets you see through it.
    private string AskAbout(Npc npc)
    {
        var plan = Plan(npc);
        float k = Insight(npc);
        Study(npc, 0.02f);
        if (plan.Count == 0)
        {
            if (npc.Brain.Tier0Goal() is Goal need) return $"\"What does it look like? I mean to {need.Label}.\"";
            return npc.Personality.Talkative > 0.5f ? "\"Nothing much. Waiting on the master's word, same as always.\"" : "\"This and that.\"";
        }
        var goal = plan[0];
        bool hide = Secret(goal.Label) && Trust(npc) < 40f;
        if (hide)
        {
            string cover = npc.IsBandPerson ? "\"Hunting.\" Nothing more." : "\"Work. The master's business.\"";
            if (k >= GoalAt) return cover + $" It is not true, and you know them well enough to see it: they mean to {goal.Label}.";
            if (k >= SenseAt) return cover + " Something about the way they say it does not sit right.";
            return cover;
        }
        string why = npc.AssignedTaskName != "" && goal.Label.Contains(npc.AssignedTaskName) ? " The master set me to it." : npc.HomeStores?.Leader == npc ? " Somebody has to." : "";
        string line = Trust(npc) < 0f ? $"\"To {goal.Label}. Why?\"" : $"\"I'm to {goal.Label}.{why}\"";
        if (plan.Count > 1 && Trust(npc) >= 25f) line += $" \"Just now I'm to {plan[^1].Label} first.\"";
        npc.Warm(_player, 0.5f);
        return line;
    }

    // ---- voice -------------------------------------------------------------------------

    // A word of assent, in the speaker's own manner of English.
    private static string Aye(Npc npc) => npc.IsBandPerson ? "\"Ehe.\"" : npc.Faction switch
    {
        Faction.NWC when npc.Role == Role.Voyageur => "\"Bon.\"",
        Faction.American => "\"I reckon.\"",
        Faction.ShipsCrew => "\"Aye, that's the truth of it.\"",
        _ when npc.Role is Role.Factor or Role.Clerk or Role.Officer or Role.NwcPartner => "\"Quite so.\"",
        _ => "\"Aye.\"",
    };

    // ---- passing the time -------------------------------------------------------------

    // A subject they raise: what it is (a key, so they can remember what you
    // said about it), what they say, and how they lean.
    private enum Lean { Gloomy, Complaint, Praise, Scorn, Wistful, Question }
    private record Subject(string Key, string Line, Lean Lean, Npc About = null);

    private List<Subject> Subjects(Npc npc)
    {
        var list = new List<Subject>();
        var cal = GameCalendar.Instance;
        var sky = cal?.Weather.Current ?? SkyState.Clear;
        var season = cal?.CurrentSeason ?? Season.Winter;
        if (sky is SkyState.Blizzard or SkyState.Storm) list.Add(new("weather", "\"Listen to it. This will kill somebody before the spring, you mark me.\"", Lean.Gloomy));
        else if (season == Season.Winter) list.Add(new("weather", npc.IsBandPerson ? "\"The snow is deep this year. The moose will be easy to run down, and hard to find.\"" : "\"Another month of this. I've forgotten what my feet feel like.\"", Lean.Gloomy));
        else if (season == Season.Breakup) list.Add(new("weather", "\"The ice is going out. The geese will be here any day, and then we'll all eat.\"", Lean.Wistful));
        else if (season == Season.OpenWater) list.Add(new("weather", npc.IsBandPerson ? "\"The flies are bad. Even the dogs hate the summer.\"" : "\"Flies. Flies and the ship. Nothing else in summer.\"", Lean.Gloomy));
        if (npc.Needs.Hunger < 45f || npc.HomeStores is Economy.FortLedger f && f.Stores.Count(ItemId.SaltGeese) < 3 && season == Season.Winter)
            list.Add(new("food", npc.IsBandPerson ? "\"The children are thin. If the hunters come back with nothing again, we will have to move.\"" : "\"We'll be boiling our moccasins by March, the way the store is going.\"", Lean.Gloomy));
        if (npc.HomeStores?.Leader is Npc master && master != npc)
        {
            float r = npc.RapportWith(master);
            if (r < -10f) list.Add(new("master", $"\"{master.DisplayName} drives us like dogs and calls it the Company's business.\"", Lean.Complaint, master));
            else if (r > 30f) list.Add(new("master", $"\"There's no better master in the country than {master.DisplayName}. Hard, but fair.\"", Lean.Praise, master));
        }
        foreach (var other in Sim.Npcs)
        {
            if (other == npc || other.IsDead || other.HomeStores != npc.HomeStores || other == npc.HomeStores?.Leader) continue;
            if (npc.RapportWith(other) < -15f) { list.Add(new("p:" + other.DisplayName, $"\"{other.DisplayName} is a shirker and a sneak. Watch your pack when he's about.\"", Lean.Complaint, other)); break; }
        }
        if (!npc.IsBandPerson && Economy.Rivalry.AreRivals(npc.Faction, npc.Faction == Faction.HBC ? Faction.NWC : Faction.HBC))
            list.Add(new("rivals", npc.Faction == Faction.HBC ? "\"The Nor'Westers would sell their own mothers for a pack of beaver, and buy them back cheaper.\"" : "\"The Bay men sit on their shore and wait for the furs to walk in. Old women.\"", Lean.Scorn));
        foreach (var l in Losses.All) if (l.Home == npc.HomeStores && l.Fresh) { list.Add(new("loss:" + l.Name, $"\"I keep thinking about {l.Name}. He was sitting just there a week ago.\"", Lean.Wistful)); break; }
        foreach (var m in npc.Memories)
            if (m.Directness <= 2 && m.Weight >= 0.6f && m.Actor != _player as Agent) { list.Add(new("heard:" + m.Key, $"\"Did you hear? {m.Text} What do you make of that?\"", Lean.Question)); break; }
        // The old story of the straits (Economy/Lacrosse): the Ojibwe's game
        // outside Fort Michilimackinac on the King's birthday in 1763, told by
        // the old people of the Anishinaabe camps and the old traders of the
        // upper lakes, the way it was told to them or the way they saw it.
        if (OldStoryTeller(npc))
            list.Add(new("tale:1763", npc.IsBandPerson
                ? (npc.Age >= 55 ? "\"I was a boy at the straits when our people played baaga'adowe with the Sauk outside the English fort. The ball went over the pickets by the gate, and the young men ran in after it. The women had the hatchets under their blankets. The fort was ours before the soldiers knew it was not a game.\""
                                 : "\"My grandfather was at the straits when our people played baaga'adowe outside the English fort on their king's birthday. The ball went over by the gate, and the young men ran in after it; the women had the hatchets under their blankets. That is why they still watch the gate when we play.\"")
                : "\"You know how the old fort at Michilimackinac was taken, in the year sixty-three? A ball game outside the gate, on the King's birthday, the soldiers watching with their arms stacked. The ball went in at the gate and the players after it. Henry the trader lay in a garret under the rafters and lived to tell it.\"", Lean.Wistful));
        if (!npc.IsBandPerson && npc.Role is Role.Labourer or Role.Voyageur or Role.Sailor)
            list.Add(new("home", npc.Faction == Faction.NWC ? "\"Some nights I dream of the river at Sorel, the bells on a Sunday. Then I wake up here.\"" : "\"I dreamt of home last night. Peat smoke and my mother's voice. Woke up to the man in the next bunk snoring.\"", Lean.Wistful));
        return list;
    }

    // Who tells the story of 1763: an old Anishinaabe of the upper lakes, or
    // an old hand of the Michilimackinac trade.
    private static bool OldStoryTeller(Npc npc)
    {
        if (npc.Age < 40) return false;
        if (npc.HomeStores is Economy.CountryBand cb)
            return cb.Country.Nation == Faction.Ojibwe && (cb.Country.Name.StartsWith("Odawa") || cb.Country.Name.StartsWith("Ojibwa") || cb.Country.Name.StartsWith("Anishinaabe") || cb.Country.Name.StartsWith("Potawatomi") || cb.Country.Name.StartsWith("Nipissing"));
        string place = (npc.HomeStores as World.TownHousehold)?.Town.Name ?? (npc.HomeStores as Economy.TradingPostLedger)?.Post.Name ?? "";
        return npc.Age >= 50 && place is "Mackinac Island" or "Michilimackinac" or "Sault Ste. Marie" or "St. Joseph Island" or "Detroit";
    }

    private void ShowPassTime(Npc npc)
    {
        var subjects = Subjects(npc);
        int today = GameCalendar.Instance?.Day ?? 0;
        subjects.RemoveAll(s => Acquaintance.Has(_player, npc, $"raised:{s.Key}:{today}"));
        ClearOptions();
        if (subjects.Count == 0)
        {
            _text = $"{npc.DisplayName} has nothing more to say today. Not to you, anyway.";
            AddOption("Back", () => ShowTalk(npc));
            return;
        }
        var s = subjects[(int)(GD.Randi() % (uint)subjects.Count)];
        Acquaintance.Add(_player, npc, $"raised:{s.Key}:{today}");
        _text = s.Line;
        string agree = s.Lean switch
        {
            Lean.Complaint when s.About != null => $"\"You're right about {s.About.DisplayName}.\"",
            Lean.Praise => "\"A fair master. We could do worse.\"",
            Lean.Scorn => "\"Thieves, the lot of them.\"",
            Lean.Question => "\"A bad business.\"",
            Lean.Wistful => "\"I know the feeling.\"",
            _ => "\"It's hard. It is.\"",
        };
        string differ = s.Lean switch
        {
            Lean.Complaint when s.About != null => $"\"{s.About.DisplayName}'s not so bad, if you do your work.\"",
            Lean.Praise => $"\"Fair? I wouldn't go that far.\"",
            Lean.Scorn => "\"They're men like us, and they winter harder.\"",
            Lean.Question => "\"I'd want to hear it from someone who was there.\"",
            Lean.Wistful => "\"No use looking back. This is where we are.\"",
            _ => "\"We'll come through. We always do.\"",
        };
        AddOption(agree, () => AnswerPassTime(npc, s, "agree"));
        AddOption(differ, () => AnswerPassTime(npc, s, "differ"));
        AddOption("Make light of it", () => AnswerPassTime(npc, s, "joke"));
        AddOption("Say nothing", () => AnswerPassTime(npc, s, "quiet"));
    }

    private void AnswerPassTime(Npc npc, Subject s, string how)
    {
        var p = npc.Personality;
        float warm; string said;
        // What you said last time this came up: a man notices a turncoat.
        string before = null;
        foreach (var t in Acquaintance.Topics(_player, npc)) if (t.StartsWith($"said:{s.Key}:")) { before = t.Substring($"said:{s.Key}:".Length); break; }
        bool turned = before != null && before != how && (before is "agree" or "differ") && (how is "agree" or "differ");
        switch (how)
        {
            case "agree":
                warm = 2f + (1f - p.Suspicion) * 2f;
                said = $"{Aye(npc)} {(p.Talkative > 0.5f ? "They go on about it a while, glad of an ear." : "They nod.")}";
                break;
            case "differ":
                bool bold = p.Courage > 0.6f;
                warm = bold ? 1.5f : p.Suspicion > 0.5f ? -3f : -1f;
                if (s.Lean == Lean.Praise && p.Loyalty > 0.6f) warm -= 3f;
                said = bold ? "\"Hm. You say what you think. I like that, even when you're wrong.\"" : warm < -2f ? "Their face closes. \"If you say so.\"" : "\"Maybe.\" They don't sound convinced.";
                break;
            case "joke":
                bool grief = s.Key.StartsWith("loss:");
                warm = grief ? -5f : p.Talkative > 0.5f ? 3f : 0.5f;
                said = grief ? $"{npc.DisplayName} does not laugh. \"He was my friend.\"" : p.Talkative > 0.5f ? "That gets a laugh, and a better one back at you." : "A twitch of the mouth. It will do.";
                break;
            default:
                warm = p.Suspicion > 0.6f ? -1f : 0f;
                said = p.Suspicion > 0.6f ? "They watch you, as if your silence were saying something." : "They let it lie. The quiet is not uncomfortable.";
                break;
        }
        if (turned) { warm -= 3f; said = $"\"That's not what you said before.\" " + said; }
        if (warm > 0f) npc.Warm(_player, warm); else if (warm < 0f) npc.ChangeRapport(_player, warm);
        Acquaintance.ForgetPrefix(_player, npc, $"said:{s.Key}:");
        Acquaintance.Add(_player, npc, $"said:{s.Key}:{how}");
        Study(npc, 0.05f);
        // Damning the master to one of his men: a talker repeats it, and
        // it reaches the master sooner or later.
        if (how == "agree" && s.Lean == Lean.Complaint && s.About != null && s.About == npc.HomeStores?.Leader && GD.Randf() < p.Talkative * 0.5f)
        {
            s.About.ChangeRapport(_player, -4f);
            Chronicle.Event($"{_player.DisplayName} ran down {s.About.DisplayName} to {npc.DisplayName}.", npc.GlobalPosition, _player, weight: 0.5f);
        }
        ShowTalk(npc, s.Line + "\n\n" + said);
    }

    // ---- talking someone round ------------------------------------------------------

    private record Aim(string Key, string Label, float Hard, Func<string> Done);
    private enum Appeal { Interest, Friendship, Right, Gift, Fear }

    private List<Aim> Aims(Npc npc)
    {
        var list = new List<Aim>();
        if (npc.HomeStores?.Leader is Npc master && master != npc && !master.IsDead)
            list.Add(new("word", $"Put in a word for me with {master.DisplayName}", 0.1f, () =>
            {
                float add = 3f + Mathf.Max(0f, npc.RapportWith(master)) / 10f;
                master.ChangeRapport(_player, add);
                if (master == _player.HomePost?.Leader) _player.Standing += 1f;
                return $"\"I'll tell {master.DisplayName} you're a good man. Whether he listens is his business.\"";
            }));
        var seen = new List<MemoryEntry>();
        foreach (var m in npc.Memories) if (m.Actor == _player as Agent && m.Directness == 1 && Lies.IsMisdeed(m.Text) && !npc.Recalled.Contains("hush|" + m.Key)) seen.Add(m);
        if (seen.Count > 0)
            list.Add(new("hush", "Keep what you saw to yourself", 0.25f, () =>
            {
                foreach (var m in seen) npc.Recalled.Add("hush|" + m.Key);
                return "\"I saw nothing. I'll go on seeing nothing - for now.\"";
            }));
        if (npc.IsBandPerson && npc.HomeStores is Economy.BandLedger band && npc.Role is Role.CreeChief or Role.CreeHunter && !_player.IsBandPerson)
            list.Add(new("furs", $"Bring your furs to {(_player.Faction == Faction.NWC ? "our house" : _player.Faction == Faction.American ? "our post" : "the Company")}", 0.3f, () =>
            {
                band.AddStandingWith(_player.Faction, npc.Role == Role.CreeChief ? 6f : 3f);
                return npc.Role == Role.CreeChief ? "\"I will say so at the fire. The young men listen to me, mostly.\"" : "\"I'll bring mine. I can't speak for the others.\"";
            }));
        if (Economy.Rivalry.AreRivals(npc.Faction, _player.Faction) && npc.HomeStores?.Leader is Npc theirs && theirs != npc)
            list.Add(new("plans", $"Tell me what {theirs.DisplayName} is planning", 0.35f, () =>
            {
                var plan = Plan(theirs);
                string aim = plan.Count > 0 ? $"he means to {plan[0].Label}" : "he's sitting tight";
                int beaver = npc.HomeStores.Stores.Count(ItemId.BeaverPelt);
                return $"A look over the shoulder. \"Right now {aim}. We've {beaver} beaver in the press, if you want to know, and he wants more.\"";
            }));
        foreach (var other in Sim.Npcs)
        {
            if (other == npc || other.IsDead || other.HomeStores != npc.HomeStores || npc.RapportWith(other) > -20f) continue;
            var o = other;
            list.Add(new("mend:" + o.DisplayName, $"Make it up with {o.DisplayName}", 0.3f, () =>
            {
                npc.ChangeRapport(o, 15f); o.ChangeRapport(npc, 10f);
                return $"\"All right. I'll share a pipe with {o.DisplayName}. Once.\"";
            }));
            break;
        }
        return list;
    }

    private void ShowPersuade(Npc npc, string said = null)
    {
        _text = said ?? $"What do you want of {npc.DisplayName}?";
        ClearOptions();
        int today = GameCalendar.Instance?.Day ?? 0;
        foreach (var a in Aims(npc))
        {
            var aim = a;
            bool tried = Acquaintance.Has(_player, npc, $"tried:{aim.Key}:{today}");
            if (tried) AddOption(aim.Label + " (asked today)", () => ShowPersuade(npc, $"\"You asked me that already. The answer's the same.\""));
            else AddOption(aim.Label + "...", () => ShowAppeals(npc, aim));
        }
        AddOption("Back", () => ShowTalk(npc));
    }

    // How well an appeal fits this person, 0..1 - read off their traits and
    // what they think of you.
    private float Fit(Npc npc, Appeal how)
    {
        var p = npc.Personality;
        float r = npc.RapportWith(_player);
        return how switch
        {
            Appeal.Interest => p.Ambition,
            Appeal.Friendship => Mathf.Clamp(r / 50f, 0f, 1f),
            Appeal.Right => p.Loyalty,
            Appeal.Gift => 0.5f + p.Ambition * 0.3f,
            _ => 1f - p.Courage,
        };
    }

    private void ShowAppeals(Npc npc, Aim aim)
    {
        float k = Insight(npc);
        _text = $"{aim.Label}. How do you put it to {npc.DisplayName}?" + (k >= GoalAt ? " You know them well enough to guess what will move them." : k >= SenseAt ? " You have some feel for them." : "");
        ClearOptions();
        ItemId? gift = _player.Inventory.Has(ItemId.Tobacco) ? ItemId.Tobacco : _player.Inventory.Has(ItemId.Rum) ? ItemId.Rum : null;
        var appeals = new List<(Appeal, string)>
        {
            (Appeal.Interest, "\"There'll be something in it for you.\""),
            (Appeal.Friendship, "\"Do it for me. I'd do the same for you.\""),
            (Appeal.Right, "\"It's the right thing, for all our sakes.\""),
        };
        if (gift is ItemId g) appeals.Add((Appeal.Gift, $"Press some {Items.Items.Of(g).Name.ToLower()} on them"));
        appeals.Add((Appeal.Fear, "Lean on them"));
        // Knowing a man is knowing which of these he'll listen to.
        Appeal best = Appeal.Interest; float bestFit = -1f;
        foreach (var (a, _) in appeals) { float f = Fit(npc, a); if (f > bestFit) { bestFit = f; best = a; } }
        foreach (var (a, label) in appeals)
        {
            var how = a;
            string hint = k >= GoalAt && how == best ? "  (this will move them)" : k >= SenseAt && Fit(npc, how) < 0.25f ? "  (they won't like this)" : "";
            AddOption(label + hint, () => Persuade(npc, aim, how, gift));
        }
        AddOption("Back", () => ShowPersuade(npc));
    }

    private void Persuade(Npc npc, Aim aim, Appeal how, ItemId? gift)
    {
        int today = GameCalendar.Instance?.Day ?? 0;
        Acquaintance.Add(_player, npc, $"tried:{aim.Key}:{today}");
        if (how == Appeal.Gift && gift is ItemId g) { _player.Inventory.Remove(g); npc.Inventory.Add(g); }
        float chance = 0.2f + Fit(npc, how) * 0.5f + npc.RapportWith(_player) / 200f + Insight(npc) * 0.1f - aim.Hard
            + (how == Appeal.Gift ? 0.15f : 0f);
        bool ok = GD.Randf() < Mathf.Clamp(chance, 0.05f, 0.95f);
        Study(npc, 0.03f);
        string result;
        if (how == Appeal.Fear)
        {
            // Fear works or it doesn't, and it costs either way.
            npc.ChangeRapport(_player, ok ? -4f : -10f);
            if (!ok && npc.Personality.Courage > 0.6f) Chronicle.Event($"{_player.DisplayName} tried to frighten {npc.DisplayName} and was faced down.", npc.GlobalPosition, _player, weight: 0.6f);
            result = ok ? $"{npc.DisplayName} looks away first. " + aim.Done() : npc.Personality.Courage > 0.6f ? "\"Try that again and see what happens.\" They do not step back." : "\"You don't frighten me.\" Their voice says otherwise; their answer is still no.";
        }
        else if (ok)
        {
            npc.ChangeRapport(_player, how == Appeal.Friendship ? -1f : 1f);   // a favour asked on friendship spends a little of it
            result = aim.Done();
        }
        else
        {
            npc.ChangeRapport(_player, -2f);
            result = how switch
            {
                Appeal.Interest => "\"What's in it for me is trouble. No.\"",
                Appeal.Friendship => npc.RapportWith(_player) < 10f ? "\"Friends? We've hardly spoken.\"" : "\"Not even for you. Not this.\"",
                Appeal.Right => "\"Right for you, you mean.\"",
                _ => "They take the gift, and still shake their head.",
            };
        }
        ShowPersuade(npc, result);
    }
}
