using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Items;

namespace IsometricFurTradeGame.Scripts.Agents;

// People ask you for things (design doc P.8, lever 4 turned round): a man's
// own trouble - scurvy, fever, a girl he can't approach, a robbed trapline -
// becomes a small request of the player standing beside him. Done, it is a
// shared memory and a debt he remembers, and the debt can be called in:
// stand with me, give me something, speak for me to your master, keep quiet
// about what you saw. Left undone past its day, it is remembered too.
public enum FavourKind { None, TradeForKin, FreshMeat, CoverWork, SpeakForCourting, FindTrapThief }

public class Favour
{
    public Npc Asker;
    public string Player;
    public FavourKind Kind;
    public int Day, DueDay;
    public bool Accepted, Done, Lapsed;
    public string Target;     // the girl's father, the robbed site
    public string Ask, Short;
}

public static class Favours
{
    public static readonly List<Favour> All = new();
    // How many good turns each man owes each player.
    private static readonly Dictionary<(Npc, string), int> _owed = new();
    private static readonly Dictionary<Npc, int> _lastAskDay = new();

    public static int Owed(Npc n, Agent p) => _owed.GetValueOrDefault((n, p.DisplayName));

    // ---- saving --------------------------------------------------------------------
    // Every favour asked (open, done or lapsed - the done ones are history the
    // asker remembers) and every good turn owed, by the names of the people.
    public static Godot.Collections.Dictionary Save()
    {
        var list = new Godot.Collections.Array();
        foreach (var f in All)
        {
            if (f.Asker == null || !GodotObject.IsInstanceValid(f.Asker)) continue;
            list.Add(new Godot.Collections.Dictionary
            {
                ["asker"] = f.Asker.DisplayName, ["player"] = f.Player, ["kind"] = (int)f.Kind, ["day"] = f.Day, ["due"] = f.DueDay,
                ["accepted"] = f.Accepted, ["done"] = f.Done, ["lapsed"] = f.Lapsed, ["target"] = f.Target ?? "", ["ask"] = f.Ask ?? "", ["short"] = f.Short ?? "",
            });
        }
        var owed = new Godot.Collections.Array();
        foreach (var kv in _owed)
            if (kv.Value > 0 && kv.Key.Item1 != null && GodotObject.IsInstanceValid(kv.Key.Item1))
                owed.Add(new Godot.Collections.Dictionary { ["by"] = kv.Key.Item1.DisplayName, ["to"] = kv.Key.Item2, ["n"] = kv.Value });
        return new Godot.Collections.Dictionary { ["favours"] = list, ["owed"] = owed };
    }

    public static void Load(Godot.Collections.Dictionary d, System.Func<string, Npc> byName)
    {
        Reset();
        if (d == null) return;
        if (d.TryGetValue("favours", out var fv))
            foreach (var v in fv.AsGodotArray())
            {
                var e = v.AsGodotDictionary();
                var asker = byName(e["asker"].AsString());
                if (asker == null) continue;
                string target = e["target"].AsString();
                All.Add(new Favour
                {
                    Asker = asker, Player = e["player"].AsString(), Kind = (FavourKind)e["kind"].AsInt32(), Day = e["day"].AsInt32(), DueDay = e["due"].AsInt32(),
                    Accepted = e["accepted"].AsBool(), Done = e["done"].AsBool(), Lapsed = e["lapsed"].AsBool(),
                    Target = target == "" ? null : target, Ask = e["ask"].AsString(), Short = e["short"].AsString(),
                });
            }
        if (d.TryGetValue("owed", out var ov))
            foreach (var v in ov.AsGodotArray())
            {
                var e = v.AsGodotDictionary();
                var by = byName(e["by"].AsString());
                if (by != null) _owed[(by, e["to"].AsString())] = e["n"].AsInt32();
            }
        GD.Print($"[favour] loaded {All.Count} favours ({All.FindAll(f => f.Accepted && !f.Done && !f.Lapsed).Count} open), {_owed.Count} good turns owed");
    }
    public static void Reset() { All.Clear(); _owed.Clear(); _lastAskDay.Clear(); }

    public static Favour Open(Npc n, Agent p)
    {
        foreach (var f in All) if (f.Asker == n && f.Player == p.DisplayName && !f.Done && !f.Lapsed) return f;
        return null;
    }

    public static IEnumerable<Favour> OpenFor(Agent p)
    {
        foreach (var f in All) if (f.Player == p.DisplayName && f.Accepted && !f.Done && !f.Lapsed) yield return f;
    }

    // ---- asking ------------------------------------------------------------------------

    // From the NPC's own tick, at most once a day: is there something he
    // needs that the player beside him could do? He calls out; the asking
    // itself happens when the player talks to him.
    public static void Consider(Npc n)
    {
        if (n.Conversing || n.Sleeping || n.Brain.Tier0Goal() != null) return;
        int day = (int)(GameCalendar.Instance?.DayOfYear ?? 0f);
        if (_lastAskDay.TryGetValue(n, out int last) && Mathf.PosMod(day - last, 365) < 2) return;
        foreach (var a in Sim.AgentsWithin(n.GlobalPosition, 220f))
        {
            if (a is not Player.PlayerController p || p.IsDead || n.IsHostileTo(p) || n.RapportWith(p) < -10f || Open(n, p) != null) continue;
            var kind = Need(n, out string target);
            if (kind == FavourKind.None) return;
            _lastAskDay[n] = day;
            Ask(n, p, kind, 0, target);
            n.FaceToward(p.GlobalPosition);
            n.Bark($"{Barks.CallName(p)}! A word, if you've a moment.");
            return;
        }
    }

    // What he needs that a man in the player's shoes could do.
    private static FavourKind Need(Npc n, out string target)
    {
        target = null;
        if (n.DaysSinceFresh > 28f) return FavourKind.FreshMeat;
        if (n.IsSick && n.HomeStores != null) return FavourKind.CoverWork;
        if (n.Spouse == null && !n.IsFemale && n.Role is Role.CreeHunter or Role.Labourer or Role.Voyageur && n.Personality.Ambition > 0.4f)
            foreach (var d in Sim.Npcs)
                if (!d.IsDead && d.Role == Role.CreeDaughter && d.Spouse == null && d.Father != null && !d.Father.IsDead && d.Father != n) { target = d.Father.DisplayName; return FavourKind.SpeakForCourting; }
        foreach (var m in n.Memories)
            if (m.Text.Contains(" robbed") && m.Text.Contains("traps") && (m.Actor == null || m.Actor == n) && m.Directness == 1) { target = m.Text; return FavourKind.FindTrapThief; }
        return FavourKind.None;
    }

    public static Favour Ask(Npc n, Agent p, FavourKind kind, int dueDays, string target = null)
    {
        int day = (int)(GameCalendar.Instance?.DayOfYear ?? 0f);
        var f = new Favour { Asker = n, Player = p.DisplayName, Kind = kind, Day = day, DueDay = day + (dueDays > 0 ? dueDays : 6), Target = target };
        string who = Barks.CallName(p);
        (f.Ask, f.Short) = kind switch
        {
            FavourKind.TradeForKin => ($"Take the furs {target ?? "down to the Bay house"} and bring back a kettle and some goods. Don't let them cheat you.", $"bring {Barks.CallName(n)} a kettle for the furs"),
            FavourKind.FreshMeat => ($"My gums are going, {who}. Bring me something fresh - a goose, a fish, a bit of venison. I'd not ask if I could go myself.", $"bring {Barks.CallName(n)} fresh meat"),
            FavourKind.CoverWork => ("I've the fever and the woodpile won't fill itself. Bring four armfuls of firewood to our pile for me?", $"stack four firewood on {Barks.CallName(n)}'s woodpile"),
            FavourKind.SpeakForCourting => ($"You've the ear of {Barks.CallName(target)}. Speak for me - tell him I'd be a good husband to his daughter. He won't hear it from me.", $"speak for {Barks.CallName(n)} to {Barks.CallName(target)}"),
            FavourKind.FindTrapThief => ("Somebody's been at my traps. Find out who, and tell me.", $"find out who robbed {Barks.CallName(n)}'s traps"),
            _ => ("", ""),
        };
        // Kin asking a band-born player: no question of saying no.
        if (kind == FavourKind.TradeForKin) f.Accepted = true;
        All.Add(f);
        if (All.Count > 60) All.RemoveAt(0);
        return f;
    }

    // Where an elder means his furs to go (the camp prologue knows; the favour is asked there).
    public static void SetTradeTarget(Npc n, Agent p, string toPost)
    {
        foreach (var f in All) if (f.Asker == n && f.Player == p.DisplayName && f.Kind == FavourKind.TradeForKin && !f.Done) { f.Target = toPost; f.Ask = $"Take the furs {toPost} and bring back a kettle and some goods. Don't let them cheat you."; }
        _tradeTarget[(n, p.DisplayName)] = toPost;
    }
    private static readonly Dictionary<(Npc, string), string> _tradeTarget = new();
    public static string TradeTargetFor(Npc n, Agent p) => _tradeTarget.GetValueOrDefault((n, p.DisplayName));

    // ---- doing it ---------------------------------------------------------------------------

    // Whether the player can hand this over now, talking to the asker.
    public static bool CanDeliver(Favour f, Player.PlayerController p) => f.Kind switch
    {
        FavourKind.TradeForKin => p.Inventory.Has(ItemId.Kettle) && p.Inventory.Count(ItemId.BeaverPelt) == 0,
        FavourKind.FreshMeat => FreshIn(p) != null,
        _ => false,
    };

    public static ItemId? FreshIn(Agent p)
    {
        foreach (var id in new[] { ItemId.Goose, ItemId.Venison, ItemId.Fish, ItemId.Hare }) if (p.Inventory.Has(id)) return id;
        return null;
    }

    public static string Deliver(Favour f, Player.PlayerController p)
    {
        var n = f.Asker;
        switch (f.Kind)
        {
            case FavourKind.TradeForKin: p.Inventory.Remove(ItemId.Kettle); n.Inventory.Add(ItemId.Kettle); break;
            case FavourKind.FreshMeat: var id = FreshIn(p).Value; p.Inventory.Remove(id); n.Inventory.Add(id); n.Eat(); break;
        }
        return Complete(f, p);
    }

    // Done some other way: stacked the wood, spoke to the father, named the thief.
    public static string Complete(Favour f, Player.PlayerController p)
    {
        if (f.Done) return "";
        f.Done = true;
        var n = f.Asker;
        n.ChangeRapport(p, 15f);
        _owed[(n, p.DisplayName)] = Owed(n, p) + 1;
        string text = $"{p.DisplayName} did {n.DisplayName} a good turn: {f.Short.Replace("bring " + Barks.CallName(n), "brought him").Replace(Barks.CallName(n) + "'s", "his")}.";
        n.Memories.Add(new MemoryEntry { Text = text, Subject = p.DisplayName, Day = GameCalendar.Instance?.DayOfYear ?? 0f, Directness = 1, Weight = 1.3f, Key = $"favour{All.IndexOf(f)}{n.DisplayName}", Witnesses = new List<string> { n.DisplayName, p.DisplayName } });
        p.Journal.Add($"{GameCalendar.Instance?.DateString}: You did {n.DisplayName} a good turn - he owes you one.");
        n.Bark(Pick($"I'll not forget this, {Barks.CallName(p)}.", "I owe you one.", "You're a good man. I owe you."));
        GD.Print($"[favour] {n.DisplayName} owes {p.DisplayName}: {f.Short}");
        return $"{n.DisplayName} won't forget it. He owes you a good turn.";
    }

    // The woodpile: a favour done in the yard, not in talk. Called when the
    // player stacks wood on someone else's household pile.
    public static void StackedWood(Player.PlayerController p, Economy.Household pile, int n)
    {
        foreach (var f in All)
            if (f.Player == p.DisplayName && f.Accepted && !f.Done && f.Kind == FavourKind.CoverWork && f.Asker.HomeStores == pile && n >= 4) Complete(f, p);
    }

    // Speaking for a suitor: talking to the girl's father on his behalf.
    public static string SpeakFor(Favour f, Player.PlayerController p, Npc father)
    {
        float said = 6f + p.Skills.Trade * 10f + father.RapportWith(p) / 10f;
        father.ChangeRapport(f.Asker, said);
        Complete(f, p);
        return $"You speak well of {f.Asker.DisplayName}. {father.DisplayName} listens, says little, and thinks the better of him for it.";
    }

    // Naming the thief: true or not, he believes you, and it's a story
    // about the named man from then on - a lie if it wasn't him.
    public static string NameThief(Favour f, Player.PlayerController p, Agent named)
    {
        var n = f.Asker;
        bool truth = false;
        foreach (var m in n.Memories) if (m.Actor == named && m.Text.Contains("traps") && m.Text.Contains("robbed")) truth = true;
        foreach (var x in Sim.Npcs) foreach (var m in x.Memories) if (m.Actor == named && m.Directness == 1 && m.Text.Contains("traps") && (m.Text.Contains(" robbed ") || m.Text.Contains("took beaver"))) truth = true;
        string text = $"{named.DisplayName} robbed the traps.";
        if (truth) n.Remember(new MemoryEntry { Text = text, Actor = named, Subject = named.DisplayName, Day = GameCalendar.Instance?.DayOfYear ?? 0f, Directness = 2, Weight = 1.5f, Key = $"thief{n.DisplayName}{named.DisplayName}", Witnesses = new List<string> { p.DisplayName } });
        else { var lie = Lies.Make(p, named, text); n.Remember(lie.Copy()); }
        Complete(f, p);
        return truth ? $"\"{Barks.CallName(named)}. I might have known.\"" : $"\"{Barks.CallName(named)}?\" He believes you. Whether it was true is between you and your conscience - and anyone who knows better.";
    }

    // Lapsed: the day came and went.
    public static void Tick()
    {
        int day = (int)(GameCalendar.Instance?.DayOfYear ?? 0f);
        foreach (var f in All)
        {
            if (!f.Accepted || f.Done || f.Lapsed || Mathf.PosMod(day - f.DueDay, 365) > 180) continue;   // not yet due
            f.Lapsed = true;
            f.Asker.ChangeRapport(Lies.Find(f.Player), -8f);
            var p = Lies.Find(f.Player) as Player.PlayerController;
            p?.Journal.Add($"{GameCalendar.Instance?.DateString}: You never did what {f.Asker.DisplayName} asked ({f.Short}). He has noticed.");
            p?.Say($"You never did what {f.Asker.DisplayName} asked. He has noticed.");
        }
    }

    // ---- calling it in ------------------------------------------------------------------------

    public static string CallIn(Npc n, Player.PlayerController p, string what)
    {
        if (Owed(n, p) <= 0) return "He owes you nothing.";
        _owed[(n, p.DisplayName)] = Owed(n, p) - 1;
        switch (what)
        {
            case "follow":
                n.Following = p; n.Brain.Clear(n); n.Brain.Push(n, new Goals.FollowGoal(p));
                return $"\"Where you go, I go - this once.\" {n.DisplayName} falls in beside you.";
            case "goods":
                foreach (var id in new[] { ItemId.TradeGoods, ItemId.Pemmican, ItemId.BeaverPelt, ItemId.DriedMeat })
                    if (n.Inventory.Has(id)) { int k = Mathf.Min(3, n.Inventory.Count(id)); n.Inventory.Remove(id, k); p.Inventory.Add(id, k); return $"He gives you {k} {Items.Items.Of(id).Name.ToLower()}. \"We're square now.\""; }
                _owed[(n, p.DisplayName)] = Owed(n, p) + 1;
                return "\"I've nothing to give, or I would.\"";
            case "speak":
                var lead = n.HomeStores?.Leader;
                if (lead == null || lead == n) { _owed[(n, p.DisplayName)] = Owed(n, p) + 1; return "\"There's nobody above me to speak to.\""; }
                lead.ChangeRapport(p, 6f);
                if (lead.Faction == p.Faction) p.Standing += 2f;
                return $"He has a word with {lead.DisplayName} about you. It does you no harm.";
            case "quiet":
                int dropped = n.Memories.RemoveAll(m => m.Actor == p && Lies.IsMisdeed(m.Text));
                return dropped > 0 ? "\"What I saw, I never saw.\" He'll not repeat it." : "\"I've nothing against you to keep quiet about.\"";
        }
        return "";
    }

    private static string Pick(params string[] o) => o[(int)(GD.Randi() % (uint)o.Length)];
}
