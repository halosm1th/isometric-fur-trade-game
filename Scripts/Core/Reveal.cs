using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;

namespace IsometricFurTradeGame.Scripts.Core;

// When a consequence finally reaches the player, say so in the journal and
// name the chain *as far as the player could know it* (design doc P.4). If
// the cause is something they did and saw done, it is cited with its date.
// If it is something they could not have known, the entry says nobody will
// say why - which is itself a hook, not a cheat.
public static class Reveal
{
    // A consequence landing on the player. `cause` is a fragment of the
    // journal line that would have recorded the deed ("struck Wapistan",
    // "sold the Company's directive"); leave it empty when there is none.
    public static void Lands(Player.PlayerController p, string what, string cause = "", float importance = 1f)
    {
        if (p == null) return;
        var cal = GameCalendar.Instance;
        string chain = cause == "" ? "" : Trace(p, cause);
        string line = chain == "" ? $"{what} (Nobody will say why.)" : $"{what} ({chain})";
        p.Journal.Add($"{cal?.DateString}: {line}");
        if (p.Journal.Count > 120) p.Journal.RemoveAt(0);
        p.Say(line);
        if (importance >= 1.5f) Sim.Headline(line);
    }

    // Look back through what the player themselves witnessed for the deed,
    // and date it. Their journal is the only record they are entitled to.
    private static string Trace(Player.PlayerController p, string cause)
    {
        for (int i = p.Journal.Count - 1; i >= 0; i--)
        {
            var entry = p.Journal[i];
            if (!entry.Contains(cause)) continue;
            int colon = entry.IndexOf(':');
            string when = colon > 0 ? entry.Substring(0, colon) : "";
            return when == "" ? "you know what you did" : $"you {cause} on {when}";
        }
        return "";
    }

    // How long word would have taken to get there, for the entries that
    // want it: a day's walk is a day, the inland post a week.
    public static string Carried(Vector2 from, Vector2 to)
    {
        float d = from.DistanceTo(to);
        int days = Mathf.Max(1, Mathf.RoundToInt(d / 4000f));
        return days <= 1 ? "word was in the camp by the next day" : $"word took about {days} days to get there";
    }
}
