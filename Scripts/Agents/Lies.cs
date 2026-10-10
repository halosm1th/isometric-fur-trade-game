using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Economy;

namespace IsometricFurTradeGame.Scripts.Agents;

// Telling a story that isn't true (design doc P.10). A lie is an ordinary
// memory with a liar's name on it: it travels by the ordinary rumour rules
// and does what the true thing would have done to the man it blames - until
// it reaches someone who knows better. The man accused knows; whoever saw
// the real deed knows; whoever was with the accused that day knows; a close
// friend of his may simply refuse to believe it. Any of them says so out
// loud, and "X has been telling lies about Y" goes round in its turn.
public static class Lies
{
    private static int _seq;

    // The things a man can be said to have done, worded so the rest of the
    // world reacts to them as it would to the truth.
    public static readonly string[] Deeds = { " robbed ", " struck ", " killed ", "on his own account", "talking with their master", "telling lies about", "broke into", "stole " };

    public static bool IsMisdeed(string text)
    {
        foreach (var d in Deeds) if (text.Contains(d)) return true;
        return false;
    }

    // The story as its teller has it: firsthand, a day old, with only the
    // teller to vouch for it. Listeners get it one hop removed.
    public static MemoryEntry Make(Agent liar, Agent accused, string text, string coversKey = null)
    {
        float now = GameCalendar.Instance?.DayOfYear ?? 0f;
        return new MemoryEntry
        {
            Text = text, Actor = accused, Subject = accused.DisplayName, Day = Mathf.PosMod(now - (float)GD.RandRange(0.4, 2.0), 365f),   // "the other night" Directness = 1, Weight = 1.6f,
            Key = $"lie{_seq++}", Witnesses = new List<string> { liar.DisplayName }, Liar = liar.DisplayName, Accused = accused.DisplayName, CoversKey = coversKey,
        };
    }

    // Does this listener know better? Returns why, in his words, or null.
    // `quiet` means he doesn't believe it but won't make a scene.
    public static string SeesThrough(Npc n, MemoryEntry e, out bool quiet)
    {
        quiet = false;
        if (e.Liar == null || e.Liar == n.DisplayName) return null;
        if (n.DisplayName == e.Accused) return "I never did any such thing.";
        if (e.Text.Contains(n.DisplayName)) return "I'd know - I'm in the story, and it never happened.";
        foreach (var m in n.Memories)
        {
            if (m.Directness != 1) continue;
            if (e.CoversKey != null && m.Key == e.CoversKey) return "I saw who did it, and it wasn't him.";
            // With him at the very hour: about three hours either side.
            if (m.Witnesses != null && m.Witnesses.Contains(e.Accused) && Mathf.Abs(Mathf.PosMod(m.Day - e.Day + 182f, 365f) - 182f) < 0.125f && GD.Randf() < 0.5f)
                return $"{Barks.CallName(e.Accused)} was with me that day.";
        }
        var accused = Find(e.Accused);
        if (accused != null && Friends.IsFriend(n, accused) && GD.Randf() < 0.5f) return $"Not {Barks.CallName(e.Accused)}. I'd stake my life on it.";
        var liar = Find(e.Liar);
        if (liar != null && n.Personality.Suspicion > 0.7f && n.RapportWith(liar) < 0f && GD.Randf() < 0.4f) { quiet = true; return "-"; }
        return null;
    }

    // Caught out: he says so aloud, thinks less of the teller, and the world
    // hears that the liar is a liar.
    public static void Exposed(Npc n, MemoryEntry e, string why)
    {
        var liar = Find(e.Liar);
        n.Bark($"That's a lie. {why}");
        if (liar == null) return;
        n.ChangeRapport(liar, -12f);
        Chronicle.Event($"{e.Liar} has been telling lies about {e.Accused}.", n.GlobalPosition, liar, e.Liar, 1.8f);
        if (liar is Player.PlayerController p)
        {
            string line = $"{n.DisplayName} has called your story about {Barks.CallName(e.Accused)} a lie, and says so to anyone who'll listen.";
            p.Say(line);
            p.Journal.Add($"{GameCalendar.Instance?.DateString}: {line}");
        }
    }

    // What a misdeed - true or invented - does to the people who hear of it:
    // they think less of the man; his master marks him down; a band thinks
    // less of the company whose man it was. Each house reacts once.
    private static readonly HashSet<string> _housesTold = new();
    public static void Consequences(Npc n, MemoryEntry e)
    {
        var actor = e.Actor;
        if (actor == null || actor == n || !GodotObject.IsInstanceValid(actor) || !IsMisdeed(e.Text)) return;
        float w = e.Weight / Mathf.Max(1, e.Directness);
        if (e.Text.Contains("telling lies about " + n.DisplayName)) n.ChangeRapport(actor, -20f);
        var home = n.HomeStores;
        if (home == null || !_housesTold.Add(home.Title + "|" + e.Key)) return;
        // His master hears of it.
        if (home.Leader == n && actor.Faction == n.Faction)
        {
            if (actor is Npc man && man.HomeStores == home) { man.Standing -= 3f * w; GD.Print($"[standing] {man.DisplayName} now {man.Standing:0}: {n.DisplayName} heard \"{e.Text}\""); }
            else if (actor is Player.PlayerController p && !e.Text.Contains("on his own account"))   // the ship's report has its own reckoning for that
            {
                p.Standing -= 3f * w;
                Reveal.Lands(p, $"{n.DisplayName} has heard that {Personal(e.Text, p)} Your standing suffers.", "", 1f);
            }
        }
        // A band hears that a company man wronged them.
        if (home is BandLedger b && !actor.IsBandPerson && (e.Text.Contains(" robbed ") || e.Text.Contains(" struck ") || e.Text.Contains(" killed ")))
        {
            if (actor.Faction == Faction.NWC) b.StandingWithNwc = Mathf.Max(0f, b.StandingWithNwc - 3f * w);
            else if (actor.Faction == Faction.HBC) b.StandingWithCompany = Mathf.Max(0f, b.StandingWithCompany - 3f * w);
        }
    }

    public static void Reset() => _housesTold.Clear();

    private static string Personal(string text, Agent p) => Barks.YouForm(text, p.DisplayName).TrimEnd('.') + ".";

    public static Agent Find(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        foreach (var a in Sim.Agents) if (a != null && GodotObject.IsInstanceValid(a) && a.DisplayName == name) return a;
        return null;
    }
}
