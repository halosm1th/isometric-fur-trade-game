using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Items;

namespace IsometricFurTradeGame.Scripts.Agents;

// Close friends act without being asked (design doc P.8, lever 4). A
// friend is someone you've been through things with (shared firsthand
// memories) and think well of. He slips you food when you're starving,
// tells you what's being said about you, keeps your misdeeds to himself,
// won't believe a lie about you, and stands when you're attacked. Works
// the same between any two people, the player included.
public static class Friends
{
    public static float Bond(Npc n, Agent a)
    {
        if (n == null || a == null || a == n) return 0f;
        float b = n.RapportWith(a) / 25f;
        foreach (var m in n.Memories) if (m.Directness == 1 && m.Weight >= 1f && m.Witnessed(a)) b += 1f;
        if (n.Spouse == a || n.Father == a || a is Npc k && n.Children.Contains(k)) b += 3f;
        return b;
    }

    public static bool IsFriend(Npc n, Agent a) => a != null && !a.IsDead && n.RapportWith(a) >= 20f && Bond(n, a) >= 3f;

    // A few times a game-hour, from the NPC's own tick.
    public static void Act(Npc n)
    {
        foreach (var a in Sim.AgentsWithin(n.GlobalPosition, 220f))
        {
            if (a == n || a is Animal || n.IsHostileTo(a) || !IsFriend(n, a)) continue;
            if (ShareFood(n, a)) return;
            if (DeliverWarning(n, a)) return;
        }
    }

    // ---- food ----------------------------------------------------------------------

    private static readonly Dictionary<string, int> _fedDay = new();
    private static bool ShareFood(Npc n, Agent a)
    {
        if (a.Needs.Hunger >= 30f || n.Needs.Hunger < 45f) return false;
        var food = n.Inventory.FirstOfKind(ItemKind.Food);
        if (food == null) return false;
        int day = (int)(GameCalendar.Instance?.DayOfYear ?? 0f);
        string key = n.DisplayName + ">" + a.DisplayName;
        if (_fedDay.TryGetValue(key, out int last) && last == day) return false;
        _fedDay[key] = day;
        n.Inventory.Remove(food.Value);
        a.Inventory.Add(food.Value);
        n.FaceToward(a.GlobalPosition);
        n.Bark(Pick($"Here. Eat something, {Barks.CallName(a)}.", $"You look like death, {Barks.CallName(a)}. Take this.", "Go on, I've had mine."));
        Chronicle.Event($"{n.DisplayName} shared his food with {a.DisplayName} when {a.DisplayName} was starving.", n.GlobalPosition, n, a.DisplayName, 1.2f);
        return true;
    }

    // ---- warnings ------------------------------------------------------------------

    // Things a man means to tell a friend when next he sees him.
    private static readonly Dictionary<Npc, Dictionary<string, string>> _toTell = new();
    private static readonly Dictionary<(Npc, string), (string key, string liar)> _whoStarted = new();

    // Called when he hears something: is it bad news for a friend of his?
    public static void Heard(Npc n, MemoryEntry e)
    {
        if (e.Actor == null || e.Actor == n || !Lies.IsMisdeed(e.Text)) return;
        Agent friend = e.Actor;
        if (!IsFriend(n, friend)) return;
        if (!_toTell.TryGetValue(n, out var list)) _toTell[n] = list = new Dictionary<string, string>();
        string said = Barks.YouForm(e.Text, friend.DisplayName).TrimEnd('.');
        GD.Print($"[friend] {n.DisplayName} means to tell {friend.DisplayName}: {e.Text}");
        if (e.Liar != null && e.Directness <= 2) _whoStarted[(n, friend.DisplayName)] = (e.Key, e.Liar);
        list[friend.DisplayName] = e.Liar != null && e.Liar != friend.DisplayName
            ? $"There's a story going round that {Lower(said)}. I don't believe it. {(e.Directness <= 2 ? $"It started with {Barks.CallName(e.Liar)}." : "I couldn't say who started it.")}"
            : $"Word is going round that {Lower(said)}. Your master will hear of it before long.";
    }

    private static bool DeliverWarning(Npc n, Agent a)
    {
        string text = null;
        if (_toTell.TryGetValue(n, out var list) && list.TryGetValue(a.DisplayName, out var t))
        {
            text = t; list.Remove(a.DisplayName);
            // He named who started it: now you know too.
            if (_whoStarted.Remove((n, a.DisplayName), out var started)) Lies.Learned(a, started.key, started.liar);
        }
        else
            foreach (var g in Grudges.All)
                if (!g.Settled && g.Against == a.Describe() && g.Holder != n.DisplayName && n.Recalled.Add("warned|" + g.Holder + "|" + a.DisplayName))
                { text = $"Watch yourself. {Barks.CallName(g.Holder)} has not forgotten {Barks.CallName(g.Victim)}."; break; }
        if (text == null) return false;
        n.FaceToward(a.GlobalPosition);
        n.Bark(text);
        if (a is Player.PlayerController p)
        {
            p.Say($"{n.DisplayName} draws you aside: \"{text}\"");
            p.LastWarning = $"{Barks.CallName(n)}: \"{text}\"";
            p.LastWarningDay = (int)(GameCalendar.Instance?.DayOfYear ?? 0f);
            p.Journal.Add($"{GameCalendar.Instance?.DateString}: {n.DisplayName} warned you: \"{text}\"");
        }
        return true;
    }

    // ---- silence -----------------------------------------------------------------------

    // Whether he'll pass this on: a man keeps a friend's misdeeds to himself.
    public static bool WillRepeat(Npc n, MemoryEntry m, Npc to)
    {
        if (m.Actor == null || m.Actor == to || !Lies.IsMisdeed(m.Text) || !IsFriend(n, m.Actor)) return true;
        return GD.Randf() < 0.1f + n.Personality.Talkative * 0.15f;
    }

    // ---- standing by a friend --------------------------------------------------------------

    // A friend near you has just been set upon by someone who isn't also a
    // friend: he comes in on your side, if he's armed and has the nerve.
    public static Agent FightFor(Npc n)
    {
        if (n.Weapon == null || n.Personality.Courage < 0.25f) return null;
        foreach (var a in Sim.AgentsWithin(n.GlobalPosition, 400f))
        {
            if (a == n || a.LastAttackedAgo > 2f || a.LastAttacker == null || !GodotObject.IsInstanceValid(a.LastAttacker)) continue;
            var foe = a.LastAttacker;
            if (foe == n || foe.IsDead || IsFriend(n, foe) || !IsFriend(n, a)) continue;
            if (foe is Npc fn && fn.Faction == n.Faction && !n.IsHostileTo(foe)) continue;   // a quarrel among his own: he won't draw on them
            n.Bark(Pick($"Leave {Barks.CallName(a)} be!", "Not while I'm standing."));
            return foe;
        }
        return null;
    }

    public static void Reset() { _fedDay.Clear(); _toTell.Clear(); _whoStarted.Clear(); }

    private static string Lower(string t) => t.StartsWith("You") ? "y" + t.Substring(1) : t;
    private static string Pick(params string[] o) => o[(int)(GD.Randi() % (uint)o.Length)];
}
