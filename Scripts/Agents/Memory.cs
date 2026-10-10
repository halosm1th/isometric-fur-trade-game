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
    // Everyone who was there (design doc P.9): a memory two people share
    // firsthand is what "we went through that together" is made of.
    public List<string> Witnesses;
    // The day it was last brought up aloud. Firsthand witnesses share this
    // very entry, so once it's been raised at a post it rests a while for all.
    public int LastRaised = -999;
    public bool Witnessed(Agent a) => a != null && Witnesses != null && Witnesses.Contains(a.DisplayName);

    public MemoryEntry Copy() => new()
    {
        Text = Text, Subject = Subject, Actor = Actor, Day = Day, Directness = Directness + 1, Weight = Weight * 0.7f, Key = Key, Distorted = Distorted, Witnesses = Witnesses,
    };

    public int Distorted;   // how many retellings bent it

    // A retelling (design doc 10.4): one of three things may shift - who
    // did it, how bad it was, or where and when - with the odds set by the
    // teller. A suspicious teller inflates; a careless one garbles names
    // and places. Never all at once.
    public MemoryEntry Retell(Npc teller)
    {
        var c = Copy();
        if (teller == null || Directness <= 1 && GD.Randf() < 0.5f) return c;   // an eyewitness mostly tells it straight
        float garble = 0.1f + (1f - teller.Personality.Suspicion) * 0.1f + teller.Personality.Talkative * 0.15f;
        float inflate = 0.1f + teller.Personality.Suspicion * 0.3f;
        float r = GD.Randf();
        if (r < inflate)
        {
            c.Weight *= 1.6f;
            if (!c.Text.Contains("worse")) c.Text = c.Text.TrimEnd('.') + " - and worse than that, they say.";
            c.Distorted++;
        }
        else if (r < inflate + garble && Actor != null && GodotObject.IsInstanceValid(Actor))
        {
            // The wrong man gets the blame: someone of the same people.
            Agent swap = null;
            foreach (var a in Sim.Agents) if (a != Actor && !a.IsDead && a.Faction == Actor.Faction && a is Npc) { swap = a; if (GD.Randf() < 0.3f) break; }
            if (swap != null && c.Text.Contains(Actor.Describe()))
            {
                c.Text = c.Text.Replace(Actor.Describe(), swap.Describe());
                c.Actor = swap; if (c.Subject == Actor.Describe()) c.Subject = swap.Describe();
                c.Distorted++;
            }
        }
        else if (r < inflate + garble * 2f)
        {
            c.Day += GD.RandRange(-4, 4);
            if (!c.Text.Contains("thereabouts")) c.Text = c.Text.TrimEnd('.') + " (or thereabouts).";
            c.Distorted++;
        }
        return c;
    }
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
            Directness = 1, Weight = weight, Key = $"{_seq++}", Witnesses = new List<string>(),
        };
        foreach (var a in Sim.AgentsWithin(where, WitnessRadius)) if (a is not Animal && !string.IsNullOrEmpty(a.DisplayName)) entry.Witnesses.Add(a.DisplayName);
        foreach (var a in Sim.AgentsWithin(where, WitnessRadius))
        {
            if (a is Npc n && !n.IsDead) n.Remember(entry);
        }
        // Players always know what happens in front of them. The book, though,
        // is not a log of other men's chores (design doc P.4): day work worth
        // nothing to anyone stays out of it unless the player is in it.
        foreach (var pc in Sim.Players())
        {
            if (pc.GlobalPosition.DistanceTo(where) >= WitnessRadius) continue;
            pc.Known.Add(entry.Key);
            bool hisOwn = subject == pc.DisplayName || actor == pc || text.Contains(pc.DisplayName);
            if (weight < 0.5f && !hisOwn) continue;
            pc.Journal.Add($"{cal?.DateString}: {text}");
            if (pc.Journal.Count > 60) pc.Journal.RemoveAt(0);
        }
    }

    // Something a whole household lives through together (design doc P.9):
    // a blizzard, a hungry spell, the ship coming in. Nobody did it; everyone
    // at home remembers it, and it's the stuff "we went through that" is made of.
    public static void Shared(Economy.Household h, string what, float weight = 1.2f)
    {
        if (h == null) return;
        Event($"{what} at {PlaceOf(h)}.", World.IsoGrid.ToWorld(h.AccessCell), subject: PlaceOf(h), weight: weight);
    }

    public static IEnumerable<Economy.Household> Households()
    {
        foreach (var p in Sim.Posts()) yield return p;
        foreach (var b in Sim.Bands) yield return b;
    }

    public static string PlaceOf(Economy.Household h) => h switch
    {
        Economy.OutpostLedger => "Oxford House",
        Economy.NwcLedger => "the Canadians' house",
        Economy.FortLedger => "the Factory",
        Economy.BandLedger b => b.IsCree ? "the Cree camp" : "the Assiniboine camp",
        _ => h.Title,
    };
}
