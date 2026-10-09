using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Economy;
using IsometricFurTradeGame.Scripts.Player;

namespace IsometricFurTradeGame.Scripts.Agents;

public enum BarkKind { None, Shared, Sour, Mourning, State, Worry, Want, Reply }

public readonly struct BarkLine
{
    public readonly string Text; public readonly BarkKind Kind; public readonly MemoryEntry Memory;
    public BarkLine(string text, BarkKind kind, MemoryEntry memory = null) { Text = text; Kind = kind; Memory = memory; }
    public bool IsEmpty => string.IsNullOrEmpty(Text);
}

// What people say out loud in passing (design doc P.4, P.9). Anyone can
// say it to anyone - a labourer to the player, a hunter to his brother -
// and it's built from what the speaker actually has: memories shared with
// the listener, the dead of his family, his belly, the post's books, what
// he wants. Bringing up a thing you went through together binds you a
// little closer; bringing up a wrong done you does not.
public static class Barks
{
    public const float HearRadius = 170f;
    private const float ShipDay = 232f;   // 20 August

    // Who's beside the speaker to say it to: someone he shares something
    // with first, then a player, then whoever is nearest. Null = muttering.
    public static Agent ListenerFor(Npc s)
    {
        Agent best = null; int bestScore = -1; float bestD = float.MaxValue;
        foreach (var a in Sim.AgentsWithin(s.GlobalPosition, HearRadius))
        {
            if (a == s || a is Animal || s.IsHostileTo(a)) continue;
            if (a is Npc n && (n.Sleeping || n.Tier != Tier.Live)) continue;
            int score = Recallable(s, a) != null ? 2 : a is PlayerController ? 1 : 0;
            float d = s.GlobalPosition.DistanceSquaredTo(a.GlobalPosition);
            if (score > bestScore || score == bestScore && d < bestD) { best = a; bestScore = score; bestD = d; }
        }
        return best;
    }

    // A thing both of them saw firsthand, that mattered, not yet raised
    // between them, and at least a day old.
    public static MemoryEntry Recallable(Npc s, Agent listener)
    {
        if (listener == null) return null;
        float now = GameCalendar.Instance?.DayOfYear ?? 0f;
        for (int i = s.Memories.Count - 1; i >= 0; i--)
        {
            var m = s.Memories[i];
            if (m.Directness != 1 || m.Weight < 1f || !m.Witnessed(listener)) continue;
            if (Mathf.PosMod(now - m.Day, 365f) < 1f) continue;
            if (s.Recalled.Contains(m.Key + "|" + listener.DisplayName)) continue;
            if (m.LastRaised > -999 && Mathf.PosMod(now - m.LastRaised, 365f) < 7f) continue;   // just talked over, here
            return m;
        }
        return null;
    }

    public static BarkLine Compose(Npc s, Agent listener)
    {
        // 1. Something you went through together.
        var shared = Recallable(s, listener);
        if (shared != null && GD.Randf() < 0.6f) return Recall(s, listener, shared);

        // 2. The dead of his own family.
        var lost = LostKin(s);
        if (lost != null && GD.Randf() < 0.5f)
        {
            s.Mourned[lost.DisplayName] = s.Mourned.GetValueOrDefault(lost.DisplayName) + 1;
            string n = CallName(lost);
            return new BarkLine(Pick($"{n} should be here.", $"I keep looking for {n} at the fire.", $"{n} would have laughed at that."), BarkKind.Mourning);
        }

        // 3. The body.
        var state = BodyLine(s);
        if (state != null && GD.Randf() < 0.6f) return new BarkLine(state, BarkKind.State);

        // 4. The post's books, said by the men who keep them.
        var worry = QuotaWorry(s);
        if (worry != null && GD.Randf() < 0.5f && OncePerDay(s, BarkKind.Worry) && HouseWorryFree(s)) return new BarkLine(worry, BarkKind.Worry);

        // 5. What he wants.
        if (GD.Randf() < 0.25f)
        {
            var want = WantLine(s);
            if (want != null && OncePerDay(s, BarkKind.Want)) return new BarkLine(want, BarkKind.Want);
        }
        return default;
    }

    // The post's trouble gets said aloud once a day, by whoever says it first.
    private static readonly Dictionary<Household, int> _houseWorryDay = new();
    public static void Reset() => _houseWorryDay.Clear();
    private static bool HouseWorryFree(Npc s)
    {
        int day = (int)(GameCalendar.Instance?.DayOfYear ?? 0f);
        if (s.HomeStores == null || _houseWorryDay.TryGetValue(s.HomeStores, out int last) && Mathf.PosMod(day - last, 365) < 3) return false;   // every few days, not daily
        _houseWorryDay[s.HomeStores] = day;
        return true;
    }

    // Some things a man says once a day, not every time he opens his mouth.
    private static bool OncePerDay(Npc s, BarkKind kind)
    {
        int day = (int)(GameCalendar.Instance?.DayOfYear ?? 0f);
        if (s.LastBarkDay.GetValueOrDefault(kind, -1) == day) return false;
        s.LastBarkDay[kind] = day;
        return true;
    }

    // What saying it does: shared trouble warms both ways; the listener answers.
    public static void Apply(Npc s, Agent listener, BarkLine line)
    {
        if (line.Memory != null) line.Memory.LastRaised = (int)(GameCalendar.Instance?.DayOfYear ?? 0f);
        if (line.Memory != null && listener != null) s.Recalled.Add(line.Memory.Key + "|" + listener.DisplayName);
        if (listener == null) return;
        if (line.Kind == BarkKind.Shared)
        {
            s.ChangeRapport(listener, 1.5f);
            if (listener is Npc ln) ln.ChangeRapport(s, 1.5f);
        }
        if (listener is Npc l && !l.Sleeping) l.QueueReply(Reply(l, s, line), 2.5f);
    }

    private static BarkLine Recall(Npc s, Agent listener, MemoryEntry m)
    {
        string t = Personalise(m.Text, s, listener);
        string who = CallName(listener);
        bool bad = IsWrong(m.Text);
        if (bad && m.Actor == listener && m.Actor != s)
            return new BarkLine(Pick($"I haven't forgotten, {who}. {t}", $"{t} Don't think I've forgotten.", $"You know what you did. {t}"), BarkKind.Sour, m);
        if (bad)
            return new BarkLine(Pick($"You saw it too, {who}. {t}", $"{t} I still see it.", $"We were both there. {t}"), BarkKind.Shared, m);
        if (IsHard(m.Text))
            return new BarkLine(Pick($"You remember, {who}? {t} Ill times.", $"We came through it, {who}. {t}", $"{t} I'll not forget it."), BarkKind.Shared, m);
        return new BarkLine(Pick($"Do you remember, {who}? {t}", $"{t} You were there.", $"I think of it still, {who}. {t}"), BarkKind.Shared, m);
    }

    private static string Reply(Npc l, Npc s, BarkLine line) => line.Kind switch
    {
        BarkKind.Sour => l.Personality.Courage > 0.6f ? Pick("Then remember it.", "Say it to my face, then.") : Pick("That's done with.", "Leave it be."),
        BarkKind.Shared => l.Personality.Talkative < 0.25f ? "Mm." : IsWrong(line.Memory?.Text ?? "") ? Pick("I'd sooner not think on it.", "Aye. I see it too.") : IsHard(line.Memory?.Text ?? "") ? Pick("Aye. Ill times.", "We came through it.", "Never again, God willing.") : Pick("Aye, I remember.", "Don't I know it.", "How could I forget?"),
        BarkKind.Mourning => Pick("God rest them.", "Aye.", "They're missed."),
        BarkKind.State => l.Personality.Talkative < 0.3f ? null : Pick("You and me both.", "Aye.", "It'll pass."),
        BarkKind.Worry => l.Role is Role.Labourer or Role.Voyageur ? Pick("Then send us out.", "The beaver don't know about London.") : Pick("We'll make it.", "God help us."),
        _ => null,
    };

    // The event as one of the people in it would say it.
    private static string Personalise(string text, Agent s, Agent listener)
    {
        if (listener != null && !string.IsNullOrEmpty(listener.DisplayName))
        {
            text = text.Replace(listener.DisplayName + "'s", "your");
            text = text.StartsWith(listener.DisplayName) ? "You" + text.Substring(listener.DisplayName.Length) : text;
            text = text.Replace(listener.DisplayName, "you");
        }
        if (!string.IsNullOrEmpty(s.DisplayName))
        {
            text = text.Replace($" and {s.DisplayName} came", " and I came");   // still the subject
            text = text.Replace(s.DisplayName + "'s", "my");
            text = text.StartsWith(s.DisplayName) ? "I" + text.Substring(s.DisplayName.Length) : text;
            text = text.Replace(s.DisplayName, "me");
        }
        // Said by someone who was there, the household is "us".
        text = text.Replace("shut everyone in", "shut us all in").Replace("everyone went", "we all went").Replace("everyone ran", "we all ran");
        if (text.Length > 0) text = char.ToUpper(text[0]) + text.Substring(1);
        return text;
    }

    private static bool IsWrong(string t) =>
        t.Contains(" struck ") || t.Contains(" killed ") || t.Contains(" robbed ") || t.Contains("prisoner") || t.Contains(" blows ") || t.Contains(" fell on ") || t.Contains("hard words") || t.Contains("broke into") || t.Contains("set fire");

    // Trouble nobody did to anyone: weather, hunger.
    private static bool IsHard(string t) => t.Contains("hungry") || t.Contains("blizzard");

    private static Agent LostKin(Npc s)
    {
        foreach (var k in Kin(s))
        {
            if (k == null || !GodotObject.IsInstanceValid(k) || !k.IsDead) continue;
            if (s.Mourned.GetValueOrDefault(k.DisplayName) < 6) return k;
        }
        return null;
    }

    private static IEnumerable<Agent> Kin(Npc s)
    {
        if (s.Spouse != null) yield return s.Spouse;
        if (s.Father != null) yield return s.Father;
        foreach (var c in s.Children) yield return c;
    }

    private static string BodyLine(Npc s)
    {
        if (s.HasScurvy) return Pick("My gums are black. Fresh meat, for the love of God.", "Every old cut I have is opening again.");
        if (s.IsSick) return Pick("The fever's in my bones.", "I'm burning and shaking both.");
        if (s.Needs.Hunger < 30f) return Pick("My belly's an empty kettle.", "There's nothing in me but wind.", "I'd eat a moccasin.");
        if (s.Needs.Warmth < 30f) return Pick("Cold enough to split the trees.", "I can't feel my hands.");
        if (s.Needs.Health < 40f) return Pick("I'm not right since that fight.", "Every step hurts.");
        if (s.IsDrunk && GD.Randf() < 0.5f) return Pick("Another dram and I'll sing for you.", "Best rum this side of Stromness!");
        return null;
    }

    private static string QuotaWorry(Npc s)
    {
        if (s.Role is not (Role.Factor or Role.Clerk or Role.Postmaster) || s.HomeStores is not FortLedger f || f.QuotaMb <= 0f) return null;
        float doy = GameCalendar.Instance?.DayOfYear ?? 0f;
        if (doy > ShipDay) return null;
        float furs = f.FursInStoreMb;
        if (furs >= f.QuotaMb * 0.85f) return null;
        int days = (int)(ShipDay - doy);
        string tail = s.Role == Role.Factor ? "God help us." : "The Factor will have our hides.";
        return $"{furs:0} beaver in the warehouse and London wants {f.QuotaMb:0}, with {days} days to the ship. {tail}";
    }

    private static string WantLine(Npc s)
    {
        var p = s.Personality;
        return s.Role switch
        {
            Role.Labourer when p.Ambition > 0.7f => Pick("I'll not carry wood all my life.", "One day I'll keep the books, you'll see."),
            Role.Labourer when p.Loyalty < 0.3f => Pick("Three more winters and I'm home to Orkney.", "The Nor'Westers pay better, they say."),
            Role.Clerk when p.Ambition > 0.6f => Pick("Oxford House wants a proper master.", "A man could make a name inland."),
            Role.CreeHunter when s.Spouse == null => Pick("A man needs a wife to work his furs.", "I'll go courting when the geese come."),
            Role.Voyageur => Pick("Paddle, eat, sleep. A good life.", "Montreal is a long way off."),
            _ => null,
        };
    }

    // "Mr. Auld" for gentlemen, the first name for everyone else.
    public static string CallName(Agent a)
    {
        var n = a?.DisplayName ?? "";
        if (n.StartsWith("Mr. ")) { var parts = n.Split(' '); return "Mr. " + parts[^1]; }
        int sp = n.IndexOf(' ');
        return sp > 0 ? n.Substring(0, sp) : n;
    }

    private static string Pick(params string[] options) => options[(int)(GD.Randi() % (uint)options.Length)];
}
