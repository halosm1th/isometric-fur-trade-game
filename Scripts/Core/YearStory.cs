using System.Collections.Generic;
using Godot;

namespace IsometricFurTradeGame.Scripts.Core;

// The story of your year (design doc P.1): when the year closes - the ship
// sailing, the brigade going down, the band's spring - the year's journal
// and what people hold of you are told back as a short account: what you
// did, what was done to you, who you were with, who is gone. Nothing is
// invented; it is the year's own record, chosen and put in order.
public static class YearStory
{
    public static string Write(Player.PlayerController p)
    {
        if (p == null) return "";
        var cal = GameCalendar.Instance;
        string text = Compose(p);
        p.Journal.Add($"{cal?.DateString}: THE YEAR, AS IT WILL BE TOLD - {text}");
        p.Say("Your year is written up in your book (J).");
        p.LastYearStory = text;
        GD.Print($"[yearstory] {p.DisplayName}: {text}");
        return text;
    }

    // The most telling things of the last year, by the director's measure
    // of what touches the player, kept in the order they happened.
    public static string Compose(Player.PlayerController p)
    {
        var picked = new List<(int i, string line)>();
        var scored = new List<(int i, float score, string line)>();
        for (int i = 0; i < p.Journal.Count; i++)
        {
            string e = p.Journal[i];
            if (e.Contains("THE YEAR, AS IT WILL BE TOLD")) { scored.Clear(); continue; }   // only since the last telling
            int colon = e.IndexOf(": ");
            string line = colon > 0 ? e.Substring(colon + 2) : e;
            float s = Director.Relevance(line, p) + Weight(line);
            if (s > 0.5f) scored.Add((i, s, line));
        }
        scored.Sort((a, b) => b.score.CompareTo(a.score));
        for (int k = 0; k < scored.Count && picked.Count < 7; k++) picked.Add((scored[k].i, scored[k].line));
        picked.Sort((a, b) => a.i.CompareTo(b.i));
        if (picked.Count == 0) return $"{p.DisplayName} kept his head down and his name out of the talk. A quiet year, and nobody's tale.";
        var parts = new List<string>();
        foreach (var (_, line) in picked)
        {
            string t = Agents.Barks.YouForm(line, p.DisplayName);
            // Orders and instructions are not the story; keep what happened.
            int cut = t.IndexOf(" Report to", System.StringComparison.Ordinal);
            if (cut > 0) t = t.Substring(0, cut);
            t = t.Trim().TrimEnd('.');
            if (parts.Count > 0 && (t.StartsWith("You ") || t.StartsWith("A ") || t.StartsWith("The "))) t = char.ToLower(t[0]) + t.Substring(1);
            parts.Add(t);
        }
        var cal = GameCalendar.Instance;
        string standing = p.IsBandPerson ? "among your own people" : $"{p.Rank}, standing {p.Standing:0} with the {(p.Faction == Agents.Faction.NWC ? "partners" : "Company")}";
        return $"{p.DisplayName}'s year to {cal?.DateString}: {string.Join(". Then ", parts)}. You end it {standing}.";
    }

    // What makes a line worth telling, besides the player's own name.
    private static float Weight(string line)
    {
        float w = 0f;
        foreach (var k in new[] { " died", " killed", " struck", "lie", "married", "prisoner", "blizzard", "hungry", "the ship", "sold", "warned you", "shared his food", "bought", "robbed", "brigade" })
            if (line.Contains(k)) w += 1f;
        return w;
    }
}
