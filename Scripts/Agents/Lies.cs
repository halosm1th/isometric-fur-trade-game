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
            Text = text, Actor = accused, Subject = accused.DisplayName, Day = Mathf.PosMod(now - (float)GD.RandRange(0.4, 2.0), 365f) /* "the other night" */, Directness = 1, Weight = 1.6f,
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

    public static void Reset() { _housesTold.Clear(); _lastLieDay.Clear(); _lastAgainst.Clear(); _knownLiars.Clear(); _fellRaiding.Clear(); }

    // ---- NPCs lie too ------------------------------------------------------------------

    // Once a game day at most, a man with little loyalty in him and a reason
    // to lie looks for someone to tell it to - face to face, so a player
    // close enough sees the whisper. Reasons, first that fits wins:
    //   cover    - his own misdeed, laid on whoever he likes least of those who saw it
    //   spite    - someone he can't abide, to anyone who'll listen
    //   career   - an ambitious man pulling down a rival close behind or ahead, to their master
    //   rivalry  - the other company's men, to a band, when the rivalry runs hot
    private static readonly Dictionary<Npc, int> _lastLieDay = new();
    private static readonly Dictionary<(Npc, string), int> _lastAgainst = new();
    public static void Consider(Npc n)
    {
        if (n.Personality.Loyalty >= 0.5f || n.IsBandPerson || n.Conversing || n.Brain.Tier0Goal() != null) return;
        int day = (int)(GameCalendar.Instance?.DayOfYear ?? 0f);
        if (_lastLieDay.TryGetValue(n, out int last) && Mathf.PosMod(day - last, 365) < 1) return;
        var near = new List<Agent>();
        foreach (var a in Sim.AgentsWithin(n.GlobalPosition, 200f)) if (a != n && a is not Animal && !a.IsDead) near.Add(a);
        if (near.Count == 0) return;   // nobody to tell; try again later
        _lastLieDay[n] = day;
        float bent = 1f - n.Personality.Loyalty * 2f;   // 0 at loyalty 0.5, 1 at loyalty 0

        // Cover: his own misdeed, still fresh.
        foreach (var m in n.Memories)
        {
            if (m.Directness != 1 || m.Actor != n || m.Liar != null || !IsMisdeed(m.Text) || m.Text.Contains("telling lies") || Mathf.PosMod(day - m.Day, 365f) > 10f) continue;
            if (GD.Randf() > bent * 0.6f) break;
            Agent scapegoat = null;
            foreach (var w in m.Witnesses ?? new List<string>())
            {
                var a = Find(w);
                if (a != null && a != n && !a.IsDead && a is not Animal && (scapegoat == null || n.RapportWith(a) < n.RapportWith(scapegoat))) scapegoat = a;
            }
            var hearer = Pick(near, a => a is Npc h && h != scapegoat && !m.Witnessed(h));
            if (scapegoat == null || hearer == null) break;
            Tell(n, (Npc)hearer, scapegoat, m.Text.Replace(n.DisplayName, scapegoat.DisplayName), m.Key, "cover");
            return;
        }
        // Spite: someone he cannot abide.
        Agent hated = null;
        foreach (var kv in n.Rapport)
            if (kv.Value <= -25f && GodotObject.IsInstanceValid(kv.Key) && !kv.Key.IsDead && kv.Key is not Animal && (hated == null || kv.Value < n.RapportWith(hated))) hated = kv.Key;
        if (hated != null && GD.Randf() < bent * 0.35f)
        {
            var hearer = Pick(near, a => a is Npc h && h != hated);
            if (hearer is Npc h)
            {
                Agent victim = h.Spouse as Npc ?? h.Father;
                string text = victim != null && victim != hated && !victim.IsDead ? $"{hated.DisplayName} struck {victim.DisplayName}." : $"{hated.DisplayName} robbed the traps at {TrapSiteOf(h)}.";
                Tell(n, h, hated, text, null, "spite");
                return;
            }
        }
        // Career: a rival in his own house standing higher, told to the master.
        var leader = n.HomeStores?.Leader;
        if (leader != null && leader != n && near.Contains(leader) && n.Role is Role.Clerk or Role.Labourer or Role.Voyageur or Role.Postmaster && n.Personality.Ambition > 0.55f && GD.Randf() < bent * n.Personality.Ambition * 0.5f)
        {
            // A rival is anyone of his rank in the house close behind him or ahead.
            Agent rival = null; float rivalStanding = n.Standing - 5f;
            foreach (var m in Sim.Npcs) if (m != n && m != leader && !m.IsDead && m.HomeStores == n.HomeStores && m.Role == n.Role && m.Standing >= rivalStanding) { rival = m; rivalStanding = m.Standing; }
            foreach (var p in Sim.Players()) if (!p.IsDead && p.HomePost == n.HomeStores && p.Standing >= rivalStanding) { rival = p; rivalStanding = p.Standing; }
            if (rival != null)
            {
                string rivalHouse = rival.Faction == Faction.NWC ? "the Company's post" : "the Nor'Westers' house";
                string text = GD.Randf() < 0.5f ? $"{rival.DisplayName} traded on his own account at the camp." : $"{rival.DisplayName} was seen at {rivalHouse} after dark, talking with their master.";
                Tell(n, leader, rival, text, null, "career");
                return;
            }
        }
        // Rivalry: the other company's men, to a band, when it runs hot.
        if (Rivalry.Tension >= 50f && GD.Randf() < bent * 0.4f)
        {
            var hearer = Pick(near, a => a is Npc h && h.IsBandPerson);
            Npc enemy = null;
            foreach (var m in Sim.Npcs) if (!m.IsDead && Rivalry.AreRivals(m.Faction, n.Faction) && !m.IsFemale && (enemy == null || GD.Randf() < 0.4f)) enemy = m;
            if (hearer is Npc h && enemy != null) Tell(n, h, enemy, $"{enemy.DisplayName} robbed the traps at {TrapSiteOf(h)}.", null, "rivalry");
        }
    }

    // He tells it, low, to the one man, and keeps it himself to pass on as
    // anyone passes on what they know. A player near enough overhears.
    public static void Tell(Npc n, Npc hearer, Agent accused, string text, string coversKey, string why)
    {
        // Once it's out, it travels by itself: he starts a story about the same man every few days at most.
        int day = (int)(GameCalendar.Instance?.DayOfYear ?? 0f);
        if (_lastAgainst.TryGetValue((n, accused.DisplayName), out int last) && Mathf.PosMod(day - last, 365) < 5) return;
        _lastAgainst[(n, accused.DisplayName)] = day;
        var lie = Make(n, accused, text, coversKey);
        n.Memories.Add(lie);
        n.FaceToward(hearer.GlobalPosition);
        n.Bark("(low) " + Barks.Personalise(text, n, hearer));
        GD.Print($"[lie] {n.DisplayName} ({why}) to {hearer.DisplayName}: {text}");
        hearer.Remember(lie.Copy());
        foreach (var p in Sim.Players())
        {
            if (p.IsDead || p.GlobalPosition.DistanceTo(n.GlobalPosition) > 260f) continue;
            Learned(p, lie.Key, n.DisplayName);
            p.Journal.Add($"{GameCalendar.Instance?.DateString}: You overheard {n.DisplayName} tell {hearer.DisplayName}: \"{text}\"");
        }
    }

    private static string TrapSiteOf(Npc h)
    {
        if (Sim.Ecology != null && h.HomeStores != null)
            foreach (var s in Sim.Ecology.Sites) if (s.LineOf(h.HomeStores.Title) != null) return s.Name;
        return "the beaver lodge";
    }

    private static Agent Pick(List<Agent> from, System.Func<Agent, bool> ok)
    {
        foreach (var a in from) if (ok(a)) return a;
        return null;
    }

    // ---- what a player knows of who started what ---------------------------------------

    private static readonly Dictionary<string, Dictionary<string, string>> _knownLiars = new();
    public static void Learned(Agent p, string key, string liar)
    {
        if (p == null || key == null || liar == null) return;
        if (!_knownLiars.TryGetValue(p.DisplayName, out var d)) _knownLiars[p.DisplayName] = d = new Dictionary<string, string>();
        d[key] = liar;
    }
    public static string KnownLiar(Agent p, string key) => p != null && key != null && _knownLiars.TryGetValue(p.DisplayName, out var d) && d.TryGetValue(key, out var l) ? l : null;

    // The player says it isn't so. Against a man who saw it, hopeless. For a
    // story that really is a lie, he's believed if he's better liked than the
    // liar - and if he can name the liar, the liar is the one marked down.
    // For a true story, the denial is a lie of his own and a gamble on regard.
    public static string Deny(Npc n, Player.PlayerController p, MemoryEntry m, bool nameLiar)
    {
        string about = Barks.YouForm(m.Text, p.DisplayName).TrimEnd('.');
        if (m.Directness == 1) { n.ChangeRapport(p, -6f); return $"\"I saw it with my own eyes.\" {n.DisplayName} looks at you as if you'd spat."; }
        var liar = Find(m.Liar);
        bool believed = m.Liar != null
            ? n.RapportWith(p) + 10f >= (liar != null ? n.RapportWith(liar) : 0f)
            : GD.Randf() < Mathf.Clamp(0.2f + n.RapportWith(p) / 100f, 0.05f, 0.8f);
        if (!believed) { n.ChangeRapport(p, -2f); return $"\"So you say.\" {n.DisplayName} doesn't believe you."; }
        n.Memories.Remove(m);
        n.Recalled.Add("denied|" + m.Key);
        if (n.HomeStores?.Leader == n && m.Actor == p) p.Standing += 2f;
        if (nameLiar && liar != null)
        {
            n.ChangeRapport(liar, -10f);
            // True this time, and he heard it from a man who knows.
            n.Remember(new MemoryEntry { Text = $"{m.Liar} has been telling lies about {p.DisplayName}.", Actor = liar, Subject = m.Liar, Day = GameCalendar.Instance?.DayOfYear ?? 0f, Directness = 2, Weight = 1.6f, Key = $"named{m.Key}", Witnesses = new List<string> { p.DisplayName } });
            return $"\"{Barks.CallName(m.Liar)}, is it?\" {n.DisplayName} puts the story about how {about} out of his head - and thinks a good deal less of {Barks.CallName(m.Liar)}.";
        }
        return $"{n.DisplayName} hears you out and lets the story about how {about} go.";
    }

    // Men who were killed raiding a house, and whose house it was. Owning
    // such a killing to a man of that house is owning that you stood to.
    private static readonly Dictionary<string, Faction> _fellRaiding = new();
    public static void FellRaiding(string name, Faction house) { if (!string.IsNullOrEmpty(name)) _fellRaiding[name] = house; }
    public static bool WasDefence(Npc n, MemoryEntry m)
    {
        if (m.Liar != null || !(m.Text.Contains(" killed ") || m.Text.Contains(" struck "))) return false;
        foreach (var (name, house) in _fellRaiding) if (house == n.Faction && m.Text.Contains(name)) return true;
        return false;
    }

    // The player says yes, he did it. A blow struck or a man killed who was
    // robbing this listener's own house is to his credit, and the story goes
    // round now as a thing well done. A true misdeed owned is still a
    // misdeed, but he thinks a little better of a man who doesn't lie to him.
    public static string Own(Npc n, Player.PlayerController p, MemoryEntry m)
    {
        n.Recalled.Add("denied|" + m.Key);   // it is said and done with; not raised again
        bool defended = (m.Text.Contains(" killed ") || m.Text.Contains(" struck ")) && m.Liar == null;
        string victim = null;
        if (defended)
            foreach (var (name, house) in _fellRaiding)
                if (house == n.Faction && m.Text.Contains(name)) { victim = name; break; }
        if (victim != null)
        {
            n.Memories.Remove(m);
            n.ChangeRapport(p, 6f * m.Weight);
            if (n.HomeStores?.Leader == n) p.Standing += 3f;
            return $"\"Aye. I did.\" {n.DisplayName} nods slowly. \"{Barks.CallName(victim)} came over our wall for our stores, with a gun in his hand. You did what a man of this house ought to have done - and what precious few of them did.\"";
        }
        if (m.Liar != null) { n.ChangeRapport(p, -4f); return $"\"Aye, I did,\" you say - though you didn't. {n.DisplayName} looks at you strangely and believes you."; }
        n.ChangeRapport(p, 1f);
        return $"\"Aye. I did.\" {n.DisplayName} looks at you a long moment. \"Well. At least you'll not lie to me about it.\"";
    }

    private static string Personal(string text, Agent p) => Barks.YouForm(text, p.DisplayName).TrimEnd('.') + ".";

    public static Agent Find(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        foreach (var a in Sim.Agents) if (a != null && GodotObject.IsInstanceValid(a) && a.DisplayName == name) return a;
        return null;
    }
}
