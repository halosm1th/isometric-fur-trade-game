using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Economy;
using IsometricFurTradeGame.Scripts.Player;

namespace IsometricFurTradeGame.Scripts.Agents;

public enum BarkKind { None, Shared, Sour, Mourning, State, Worry, Want, Reply, Story, Song, Toast, Keepsake }

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
    // Whichever player is close enough to hear, if any.
    private static Player.PlayerController Nearby(Npc s)
    {
        foreach (var p in Sim.Players())
            if (p != null && GodotObject.IsInstanceValid(p) && !p.IsDead && p.GlobalPosition.DistanceTo(s.GlobalPosition) < HearRadius * 1.5f) return p;
        return null;
    }

    public static MemoryEntry Recallable(Npc s, Agent listener)
    {
        if (listener == null) return null;
        float now = GameCalendar.Instance?.DayOfYear ?? 0f;
        var open = new List<MemoryEntry>();
        for (int i = s.Memories.Count - 1; i >= 0; i--)
        {
            var m = s.Memories[i];
            if (m.Directness != 1 || m.Weight < 1f || !m.Witnessed(listener)) continue;
            if (Mathf.PosMod(now - m.Day, 365f) < 1f) continue;
            if (s.Recalled.Contains(m.Key + "|" + listener.DisplayName)) continue;
            if (m.LastRaised > -999 && Mathf.PosMod(now - m.LastRaised, 365f) < 7f) continue;   // just talked over, here
            open.Add(m);
            if (open.Count >= 6) break;
        }
        if (open.Count == 0) return null;
        // Of the things the two of them both saw, the one that touches the
        // player's own business comes out first (design doc 2.2).
        var who = listener as Player.PlayerController ?? Nearby(s);
        return who != null ? Core.Director.MostRelevant(open, m => m.Text, who) : open[0];
    }

    public static BarkLine Compose(Npc s, Agent listener)
    {
        // 0. An evening at the fire has its own talk.
        if (s.AtHearth != null && GD.Randf() < 0.7f)
        {
            var fire = HearthLine(s, listener);
            if (!fire.IsEmpty) return fire;
        }

        // 1. Something you went through together.
        var shared = Recallable(s, listener);
        if (shared != null && GD.Randf() < 0.6f) return Recall(s, listener, shared);

        // 2. The hole someone left: his bunk, his things, the man himself.
        var gone = LossLine(s, listener);
        if (!gone.IsEmpty && GD.Randf() < 0.6f) return gone;

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
    public static void Reset() { _houseWorryDay.Clear(); _toldAt.Clear(); }
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
        if (line.Kind == BarkKind.Story) Told(s, line.Memory);
        else if (line.Memory != null) line.Memory.LastRaised = (int)(GameCalendar.Instance?.DayOfYear ?? 0f);
        if (line.Memory != null && listener != null) s.Recalled.Add(line.Memory.Key + "|" + listener.DisplayName);
        if (listener == null) return;
        if (line.Kind == BarkKind.Shared)
        {
            s.ChangeRapport(listener, 1.5f);
            if (listener is Npc ln) ln.ChangeRapport(s, 1.5f);
        }
        if (line.Kind is BarkKind.Story or BarkKind.Song or BarkKind.Toast && s.AtHearth != null)
            foreach (var a in Sim.AgentsWithin(s.AtHearth.GlobalPosition, 260f))
                if (a is Npc n && n != s && n.AtHearth == s.AtHearth) n.ChangeRapport(s, 0.4f);
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
        BarkKind.Mourning => Pick("God rest him.", "Aye.", "He's missed."),
        BarkKind.Toast => line.Text,
        BarkKind.Story => l.Personality.Suspicion > 0.65f ? Pick("I heard it different.", "Who told you that?") : Pick("Is that so?", "God save us.", "I'd not heard that."),
        BarkKind.Song => Pick("Again!", "Ha! That's the one.", "(the others join in)"),
        BarkKind.Keepsake => Pick("He'd want you to have it.", "Keep it well."),
        BarkKind.State => l.Personality.Talkative < 0.3f ? null : Pick("You and me both.", "Aye.", "It'll pass."),
        BarkKind.Worry => l.Role is Role.Labourer or Role.Voyageur ? Pick("Then send us out.", "The beaver don't know about London.") : Pick("We'll make it.", "God help us."),
        _ => null,
    };

    // The event as one of the people in it would say it.
    // "Thomas has been..." to Thomas's face is "you have been...".
    public static string YouForm(string text, string name)
    {
        if (string.IsNullOrEmpty(name)) return text;
        if (!text.Contains(name)) return text;
        return text.Replace(name + " has ", "you have ").Replace(name + " was ", "you were ").Replace(name + " is ", "you are ").Replace(name + "'s", "your").Replace(name, "you").Replace("on his own account", "on your own account");
    }

    public static string Personalise(string text, Agent s, Agent listener)
    {
        if (listener != null && !string.IsNullOrEmpty(listener.DisplayName)) text = text.Replace(listener.DisplayName + " has ", "you have ").Replace(listener.DisplayName + " was ", "you were ");
        if (!string.IsNullOrEmpty(s?.DisplayName)) text = text.Replace(s.DisplayName + " has ", "I have ").Replace(s.DisplayName + " is ", "I am ");
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
    private static bool IsHard(string t) => t.Contains("hungry") || t.Contains("blizzard") || t.Contains(" died");

    // The dead who were close: kin always, housemates by how much they went
    // through together (Losses.Bond). Kin mourn longer.
    private static BarkLine LossLine(Npc s, Agent listener)
    {
        string who = CallName(listener);
        foreach (var l in Losses.All)
        {
            if (l.Home != s.HomeStores || l.Name == s.DisplayName) continue;
            string n = CallName(l.Name);
            // His bunk, empty, or someone else's now.
            if (l.Fresh && l.NewSleeper == null && s.GlobalPosition.DistanceTo(World.IsoGrid.ToWorld(l.Bed)) < 120f && s.Recalled.Add("bunk|" + l.Name))
                return new BarkLine(Pick($"That was {n}'s bunk.", $"Nobody's taken {n}'s bunk yet. Nobody wants to.", $"{n} slept there. Snored like a bull."), BarkKind.Mourning);
            if (l.NewSleeper != null && !l.NewSleeperTold && l.NewSleeper != s.DisplayName)
            {
                l.NewSleeperTold = true;
                return new BarkLine($"{CallName(l.NewSleeper)} has {n}'s bunk now.", BarkKind.Mourning);
            }
            float bond = Losses.Bond(s, l);
            int said = s.Mourned.GetValueOrDefault(l.Name);
            bool kin = bond >= 10f;
            if (!kin && (!l.Fresh || bond < 1.5f) || said >= (kin ? 8 : 4)) continue;
            s.Mourned[l.Name] = said + 1;
            // A thing they went through together, if there is one.
            foreach (var m in s.Memories)
                if (m.Directness == 1 && m.Weight >= 1f && m.Witnesses != null && m.Witnesses.Contains(l.Name) && !m.Text.Contains(l.Name + " died") && !m.Text.Contains(l.Name + " was killed") && GD.Randf() < 0.5f)
                    return new BarkLine($"I keep thinking of {n}. {Personalise(m.Text, s, listener)}", BarkKind.Mourning);
            return new BarkLine(Pick($"{n} should be here.", $"I keep looking for {n} at the fire.", $"{n} would have laughed at that.", kin ? $"I can't get used to it, {who}. {n}'s gone." : $"Poor {n}."), BarkKind.Mourning);
        }
        // Something of his, kept.
        if (s.Keepsakes.Count > 0 && GD.Randf() < 0.3f)
        {
            var (id, from) = s.Keepsakes[(int)(GD.Randi() % (uint)s.Keepsakes.Count)];
            if (s.Inventory.Has(id))
            {
                string item = Items.Items.Of(id).Name.ToLower();
                return new BarkLine(Pick($"This was {CallName(from)}'s {item}.", $"{CallName(from)} carried this {item} every day."), BarkKind.Keepsake);
            }
        }
        return default;
    }

    // ---- the fire --------------------------------------------------------------

    // What's been told at each fire tonight, so a story goes round once.
    private static readonly Dictionary<World.Hearth, (int day, HashSet<string> told)> _toldAt = new();

    private static BarkLine HearthLine(Npc s, Agent listener)
    {
        var fire = s.AtHearth;
        int day = (int)(GameCalendar.Instance?.DayOfYear ?? 0f);
        if (!_toldAt.TryGetValue(fire, out var night) || night.day != day) { night = (day, new HashSet<string>()); _toldAt[fire] = night; }

        // A word for the dead of this house, once a night each.
        foreach (var l in Losses.All)
            if (l.Home == s.HomeStores && l.Fresh && night.told.Add("toast|" + l.Name))
                return new BarkLine(s.IsBandPerson ? $"We remember {CallName(l.Name)}." : $"To {CallName(l.Name)}.", BarkKind.Toast);

        float r = GD.Randf();
        // A story: something he knows that the fire hasn't heard tonight.
        if (r < 0.6f)
        {
            MemoryEntry best = null;
            var tellable = new List<MemoryEntry>();
            foreach (var m in s.Memories)
            {
                if (m.Weight < 0.8f || m.Directness > 3 || night.told.Contains(m.Key)) continue;
                tellable.Add(m);
                if (best == null || m.Weight / m.Directness > best.Weight / best.Directness) best = m;
            }
            // With a player at the fire, the story told is the one he has
            // reason to care about - still only things the teller knows.
            var listening = Nearby(s);
            if (listening != null && tellable.Count > 1 && best != null)
            {
                var worth = new List<MemoryEntry>();
                float top = best.Weight / best.Directness;
                foreach (var m in tellable) if (m.Weight / m.Directness >= top * 0.7f) worth.Add(m);
                best = Core.Director.MostRelevant(worth, m => m.Text, listening) ?? best;
            }
            if (best != null)
            {
                night.told.Add(best.Key);
                string t = Personalise(best.Text, s, null);
                return new BarkLine(best.Directness == 1 ? Pick($"I saw it myself. {t}", $"I was there. {t}") : Pick($"They say {Lower(t)}", $"I had it from a man who was there: {Lower(t)}"), BarkKind.Story, best);
            }
        }
        // A song, or a tale of the people's own.
        if (night.told.Add("song|" + s.DisplayName))
        {
            string song = s.Role switch
            {
                Role.Voyageur => Pick("(sings 'A la claire fontaine', and the others take up the chorus)", "(sings a paddling song, beating time on his knee)"),
                Role.Labourer => Pick("(plays a reel on a borrowed fiddle)", "(sings an Orkney air, slow and low)"),
                Role.CreeHunter or Role.CreeChief => Pick("(tells of Wisahkecahk and how he tricked the ducks)", "(tells of the winter the caribou did not come)"),
                Role.CreeWoman or Role.CreeDaughter => Pick("(tells the children a story of the Thunderbirds)", "(tells how the rabbit got its long ears)", "(sings a lullaby to the smallest child)"),
                Role.Woman or Role.Wife => Pick("(sings low over her sewing)", "(tells of her mother's people up the coast)"),
                Role.Factor or Role.Clerk or Role.Postmaster => "(reads aloud from a London newspaper a year old)",
                _ => null,
            };
            if (song != null) return new BarkLine(song, BarkKind.Song);
        }
        return default;
    }

    // A story told at the fire passes to everyone sitting there, one hop
    // further from the truth (design doc 10.4) - and to a player within
    // earshot, who hears it for their journal like anyone else.
    public static void Told(Npc s, MemoryEntry m)
    {
        if (s.AtHearth == null || m == null) return;
        foreach (var a in Sim.AgentsWithin(s.AtHearth.GlobalPosition, 260f))
        {
            if (a == s) continue;
            if (a is Npc n && !n.IsDead && !n.Sleeping) n.Remember(m.Retell(s));
            else if (a is PlayerController pc && pc.Known.Add(m.Key))
            {
                pc.Journal.Add($"{GameCalendar.Instance?.DateString}: At the fire, {s.DisplayName} told it: {m.Text}");
                if (pc.Journal.Count > 40) pc.Journal.RemoveAt(0);
            }
        }
    }

    public static void ResetFires() => _toldAt.Clear();

    // "They say a blizzard..." but "They say Magnus Spence...": only the
    // little words at the head of a sentence lose their capital.
    private static string Lower(string t)
    {
        foreach (var w in new[] { "A ", "An ", "The ", "There ", "Word " }) if (t.StartsWith(w)) return char.ToLower(t[0]) + t.Substring(1);
        return t;
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
    public static string CallName(Agent a) => CallName(a?.DisplayName ?? "");
    public static string CallName(string n)
    {
        n = (n ?? "").Replace(" (clerk)", "").Replace(" (chief)", "").Replace(" (acting Factor)", "");
        if (n.StartsWith("Mr. ")) { var parts = n.Split(' '); return "Mr. " + parts[^1]; }
        int sp = n.IndexOf(' ');
        return sp > 0 ? n.Substring(0, sp) : n;
    }

    private static string Pick(params string[] options) => options[(int)(GD.Randi() % (uint)options.Length)];
}
