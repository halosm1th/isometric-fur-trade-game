using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Core;

namespace IsometricFurTradeGame.Scripts.Agents;

// One thing a person knows (design doc 11.2): what, when, who did it, and
// how directly they know it - 1 saw it, 2 heard it from a witness, 3 from
// someone who heard, and so on. Weight fades with hops and time.
public class MemoryEntry
{
    public string Text;
    public string Subject;     // who it's about, for reputation
    public Agent Actor;        // who did it, if anyone
    public float Day;          // calendar day-of-year when it happened
    public int Directness;     // 1 = eyewitness
    public float Weight;       // how much it matters to this person
    public string Key;         // dedupe

    public MemoryEntry Copy() => new()
    {
        Text = Text, Subject = Subject, Actor = Actor, Day = Day, Directness = Directness + 1, Weight = Weight * 0.7f, Key = Key,
    };
}

// The world's event feed. Anyone within earshot when something happens
// remembers it firsthand; everyone else has to be told (design doc 10.2, 10.4).
public static class Chronicle
{
    public const float WitnessRadius = 650f;
    private static int _seq;

    public static void Event(string text, Vector2 where, Agent actor = null, string subject = null, float weight = 1f)
    {
        var cal = GameCalendar.Instance;
        var entry = new MemoryEntry
        {
            Text = text, Actor = actor, Subject = subject ?? actor?.DisplayName ?? "", Day = cal?.DayOfYear ?? 0f,
            Directness = 1, Weight = weight, Key = $"{_seq++}",
        };
        foreach (var a in Sim.AgentsWithin(where, WitnessRadius))
        {
            if (a is Npc n && !n.IsDead) n.Remember(entry);
        }
        // Players always know what happens in front of them.
        foreach (var pc in Sim.Players()) if (pc.GlobalPosition.DistanceTo(where) < WitnessRadius) { pc.Known.Add(entry.Key); pc.Journal.Add($"{cal?.DateString}: {text}"); if (pc.Journal.Count > 40) pc.Journal.RemoveAt(0); }
    }
}
