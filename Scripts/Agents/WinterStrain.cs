using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents.Goals;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Economy;
using IsometricFurTradeGame.Scripts.Items;
using IsometricFurTradeGame.Scripts.Player;
using IsometricFurTradeGame.Scripts.World;
using NetHub = IsometricFurTradeGame.Scripts.Net.Net;

namespace IsometricFurTradeGame.Scripts.Agents;

// Isolation and winter strain (design doc 17.6). Six months shut in a post
// with the same dozen faces wears on a man, and it builds from what he lives
// through: short days (14.3, by the post's latitude), salt food and no fresh
// meat, nobody new through the gate, hunger and cold. A sociable man feels
// it sooner (Talkative stands for sociability, 11.4).
//
// It degrades before it breaks (doc 19): grumbling at 40, quarrels at 60,
// orders refused more often all the way up (Delegation.Decide), then at 85 a
// man may walk off to the other company or to a band; and when enough of a
// post's men are past 75, they refuse their work together and put their
// demands to whoever is in charge: a mutiny, which the Factor gives in to,
// faces down, or loses men over.
//
// The Factor's levers are real goods spent on morale instead of returns: a
// dram all round out of the stores, the New Year's regale, the fiddle at
// York (YorkHappenings' dance relieves its dancers). A player at his post is
// put the choices (P.5): a story by the stove, his own rum, which side to
// take in a mutiny.
//
// Server only; ticked from Population.
public static class WinterStrain
{
    private static int _lastDay = -1;
    private static readonly Dictionary<FortLedger, int> _lastFaces = new();     // abs day an outsider was last at the gate
    private static readonly Dictionary<FortLedger, int> _lastDram = new();
    private static readonly Dictionary<FortLedger, int> _lastMutiny = new();
    private static readonly Dictionary<FortLedger, int> _mutinyUntil = new();
    private static readonly Dictionary<FortLedger, int> _lastDesert = new();
    private static readonly Dictionary<PlayerController, int> _lastPage = new();
    private static readonly HashSet<string> _done = new();                     // "regale:<post>:<year>"

    public static void Reset()
    {
        _lastDay = -1; _lastFaces.Clear(); _lastDram.Clear(); _lastMutiny.Clear(); _mutinyUntil.Clear(); _lastDesert.Clear(); _lastPage.Clear(); _done.Clear();
    }

    private static readonly string[] Args = OS.GetCmdlineUserArgs();
    // Dev: `--strain=N` sets every post man's strain to N on the first evening;
    // `--straintrace` prints each post's strain every evening.
    private static readonly bool Trace = System.Array.IndexOf(Args, "--straintrace") >= 0;
    private static bool _devSet;

    private static int Today(GameCalendar cal) => cal.Year * 366 + (int)cal.DayOfYear;

    // A man of this post who is at it: not away, not off the map, not in the boats.
    private static List<Npc> MenAt(FortLedger post)
    {
        var list = new List<Npc>();
        foreach (var n in post.Members)
            if (GodotObject.IsInstanceValid(n) && !n.IsDead && !n.IsFemale && !n.Away && n.OffMap == "" && n.Convoy == null && !n.IsBandPerson && n != post.Leader && n.HeldBy == null)
                list.Add(n);
        return list;
    }

    // Shut out of work with the rest: the men of a post in mutiny refuse every order.
    public static bool Mutinous(Npc n) =>
        n?.HomeStores is FortLedger f && _mutinyUntil.TryGetValue(f, out int until) && GameCalendar.Instance is { } cal && Today(cal) <= until;

    // Something that lifts them all: the dance, a regale, good news.
    public static void Relieve(Household h, float amount)
    {
        if (h is not FortLedger f) return;
        foreach (var n in MenAt(f)) n.Strain = Mathf.Max(0f, n.Strain - amount);
    }

    public static float Average(FortLedger post)
    {
        var men = MenAt(post);
        if (men.Count == 0) return 0f;
        float sum = 0f;
        foreach (var n in men) sum += n.Strain;
        return sum / men.Count;
    }

    public static void Tick()
    {
        if (!NetHub.IsServer) return;
        var cal = GameCalendar.Instance;
        if (cal == null || cal.Hour < 20f) return;
        int today = Today(cal);
        if (today == _lastDay) return;
        _lastDay = today;
        foreach (var post in new List<FortLedger>(Sim.Posts())) Evening(post, cal, today);
        _devSet = true;
    }

    private static void Evening(FortLedger post, GameCalendar cal, int today)
    {
        var men = MenAt(post);
        if (men.Count == 0) return;
        var door = IsoGrid.ToWorld(post.AccessCell);

        if (!_devSet)
            foreach (string a in Args)
                if (a.StartsWith("--strain=") && float.TryParse(a.Substring(9), out float s))
                    foreach (var n in men) n.Strain = s;

        // Anyone from outside at the gate today: band hunters in to trade, a brigade, the ship's people.
        bool seen = false;
        foreach (var p in Sim.Players()) if (!p.IsDead && p.GlobalPosition.DistanceSquaredTo(door) < 2500f * 2500f) { seen = true; break; }
        if (!seen)
            foreach (var n in Sim.Npcs)
                if (!n.IsDead && n.HomeStores != post && n.OffMap == "" && n.GlobalPosition.DistanceSquaredTo(door) < 2500f * 2500f) { seen = true; break; }
        if (seen) _lastFaces[post] = today;
        if (!_lastFaces.ContainsKey(post)) _lastFaces[post] = today;
        int sinceFaces = today - _lastFaces[post];

        bool winter = cal.CurrentSeason == Season.Winter;
        float dayLen = cal.DayLengthAt(Climate.LatitudeAt(door));
        foreach (var n in men)
        {
            float build = 0f;
            // A York winter alone brings the ordinary man to grumbling by about
            // February; it takes hunger, a small house far from anyone, or a
            // sociable temper on top of it to bring a post to mutiny.
            if (winter) build += 0.2f + Mathf.Max(0f, 12.6f - dayLen) * 0.12f;   // the dark
            if (n.DaysSinceFresh > 10f) build += 0.3f;                            // salt geese again
            if (winter && sinceFaces > 14) build += 0.4f;                         // the same faces
            if (winter && men.Count <= 8) build += 0.2f;                          // a small, quiet house
            if (n.Needs.Hunger < 35f) build += 1.2f;
            if (n.Needs.Warmth < 35f) build += 0.8f;
            build *= Mathf.Lerp(0.6f, 1.4f, n.Personality.Talkative);
            float relief = winter ? 0.35f : 3f;
            if (n.Intoxication > 10f) relief += 2f;
            if (n.DaysSinceFresh < 1f) relief += 2f;   // fresh meat in the kettle tonight
            n.Strain = Mathf.Clamp(n.Strain + build - relief, 0f, 100f);
        }

        Administer(post, men, cal, today);
        Story(post, men);
        Ladder(post, men, cal, today);
        PlayerPages(post, men, today);
        if (Trace) GD.Print($"[strain] {Economy.Accords.Name(post)} ({post.Title}): {men.Count} men, avg {Average(post):0}, max {Max(men):0}, day {dayLen:0.0} h, {sinceFaces} days since a new face{(Mutinous(men[0]) ? ", IN MUTINY" : "")}");
    }

    private static float Max(List<Npc> men) { float m = 0f; foreach (var n in men) m = Mathf.Max(m, n.Strain); return m; }

    // ---- the Factor's levers ------------------------------------------------------------

    private static void Administer(FortLedger post, List<Npc> men, GameCalendar cal, int today)
    {
        var boss = post.Leader;
        if (boss == null || boss.IsDead) return;
        string place = Economy.Accords.Name(post);

        // New Year's Day: the regale, three drams a man and a holiday, as every post kept it.
        string key = $"regale:{post.Title}:{place}:{cal.Year}";
        if (cal.Month == 1 && cal.Day == 1 && !_done.Contains(key))
        {
            _done.Add(key);
            int drams = Mathf.Min(post.Stores.Count(ItemId.Rum), men.Count * 3);
            post.Stores.TakeUpTo(ItemId.Rum, drams);
            float lift = drams >= men.Count * 2 ? 25f : drams > 0 ? 12f : 5f;
            foreach (var n in men) { n.Strain = Mathf.Max(0f, n.Strain - lift); if (drams > 0) n.Intoxication = Mathf.Min(100f, n.Intoxication + 40f); }
            Chronicle.Shared(post, drams > 0 ? "The New Year's regale was kept" : "New Year's Day passed with no rum for the regale", drams > 0 ? 0.8f : 1.2f);
            Notice(post, drams > 0 ? $"New Year's Day at {place}: {boss.DisplayName} serves out the regale, and the men are singing by noon." : $"New Year's Day at {place}, and no rum in the stores for the regale. The men keep it sullenly.");
            return;
        }

        // A dram all round, when the house is sour and the stores can stand it.
        float avg = Average(post);
        int last = _lastDram.TryGetValue(post, out int d) ? d : -999;
        if (avg >= 35f && today - last >= 7 && post.Stores.Count(ItemId.Rum) >= men.Count)
        {
            _lastDram[post] = today;
            post.Stores.TakeUpTo(ItemId.Rum, men.Count);
            foreach (var n in men) { n.Strain = Mathf.Max(0f, n.Strain - 8f); n.Intoxication = Mathf.Min(100f, n.Intoxication + 20f); n.ChangeRapport(boss, 2f); }
            Notice(post, $"{boss.DisplayName} has a dram served out all round at {place}. It is a long winter.");
        }
    }

    // A story by the stove: the best talker in the house, on a sour evening,
    // and whoever is near enough to hear him. It costs nothing but the
    // evening, and it is a man's own doing, not the Factor's.
    private static readonly string[] Openers =
    {
        "Did I ever tell you about the winter at Severn?", "There was a man at Albany, a Lewisman, and he swore...",
        "My grandfather saw the sea serpent off Stroma. Truly.", "You want to hear about the time the ice took the boat?",
    };

    private static void Story(FortLedger post, List<Npc> men)
    {
        if (GameCalendar.Instance.CurrentSeason != Season.Winter || Average(post) < 30f || GD.Randf() > 0.35f) return;
        Npc teller = null;
        foreach (var n in men) if (!n.Sleeping && n.Personality.Talkative >= 0.6f && n.Strain < 70f && (teller == null || n.Personality.Talkative > teller.Personality.Talkative)) teller = n;
        if (teller == null) return;
        teller.Bark(Openers[GD.RandRange(0, Openers.Length - 1)]);
        int heard = 0;
        foreach (var a in Sim.AgentsWithin(teller.GlobalPosition, 700f))
            if (a is Npc n && n != teller && !n.Sleeping && n.HomeStores == post) { n.Strain = Mathf.Max(0f, n.Strain - 3f); n.ChangeRapport(teller, 1f); heard++; }
        if (heard > 0) Chronicle.Event($"{teller.DisplayName} kept the house laughing with a story by the stove.", teller.GlobalPosition, teller, weight: 0.4f);
        if (Trace) GD.Print($"[strain] {teller.DisplayName} tells a story at {Economy.Accords.Name(post)}: {heard} listening");
    }

    // ---- the ladder ---------------------------------------------------------------------

    private static readonly string[] Grumbles =
    {
        "Salt goose. Again.", "Another month of this.", "If I see your face one more morning...",
        "I'd give a year's wages for a fresh egg.", "Dark at three. Dark at three!", "When's the packet due? Never, that's when.",
    };

    private static void Ladder(FortLedger post, List<Npc> men, GameCalendar cal, int today)
    {
        string place = Economy.Accords.Name(post);
        int sour = 0;
        Npc worst = null;
        foreach (var n in men)
        {
            if (n.Strain >= 75f) sour++;
            if (worst == null || n.Strain > worst.Strain) worst = n;
            if (n.Strain >= 40f && GD.Randf() < 0.3f) n.Bark(Grumbles[GD.RandRange(0, Grumbles.Length - 1)]);
        }

        // Quarrels: two sour men at each other's throats over nothing.
        var touchy = men.FindAll(n => n.Strain >= 60f);
        if (touchy.Count >= 2 && GD.Randf() < 0.5f)
        {
            Npc a = touchy[GD.RandRange(0, touchy.Count - 1)], b = touchy[GD.RandRange(0, touchy.Count - 1)];
            if (a != b)
            {
                a.ChangeRapport(b, -8f); b.ChangeRapport(a, -8f);
                a.Bark($"Keep your hands off my things, {Barks.CallName(b)}."); b.Bark("Your things! Listen to him!");
                Chronicle.Event($"{a.DisplayName} and {b.DisplayName} came to blows over nothing at {place}.", a.GlobalPosition, a, b.DisplayName, 0.7f);
            }
        }

        if (Mutinous(men[0])) return;

        // Mutiny: enough of them past bearing, together.
        int lastM = _lastMutiny.TryGetValue(post, out int lm) ? lm : -999;
        if (cal.CurrentSeason == Season.Winter && sour >= 3 && sour >= men.Count * 0.4f && today - lastM >= 30 && post.Leader is { IsDead: false } boss)
        {
            _lastMutiny[post] = today;
            _mutinyUntil[post] = today + 3;
            Mutiny(post, men, boss, worst);
            return;
        }

        // Desertion: one man past bearing walks out.
        int lastD = _lastDesert.TryGetValue(post, out int ld) ? ld : -999;
        if (cal.CurrentSeason == Season.Winter && worst != null && worst.Strain >= 85f && worst.Personality.Courage > 0.35f && today - lastD >= 20 && GD.Randf() < 0.1f)
        {
            _lastDesert[post] = today;
            Desert(worst, post);
        }
    }

    private static void Desert(Npc n, FortLedger post)
    {
        // The other company's house if one is within a few days, else a band's camp.
        var door = IsoGrid.ToWorld(post.AccessCell);
        Household to = null; float best = 9000f;
        foreach (var p in Sim.Posts())
        {
            if (p == post || p.Leader == null || p.Leader.Faction == n.Faction) continue;
            float dd = IsoGrid.ToWorld(p.AccessCell).DistanceTo(door);
            if (dd < best) { best = dd; to = p; }
        }
        if (to == null)
            foreach (var b in Sim.Bands)
            {
                float dd = IsoGrid.ToWorld(b.AccessCell).DistanceTo(door);
                if (dd < best) { best = dd; to = b; }
            }
        if (to == null) return;
        string place = Economy.Accords.Name(post), dest = Economy.Accords.Name(to);
        var boss = post.Leader;
        n.HomeStores = to;
        if (to is FortLedger f && f.Leader != null) n.Faction = f.Leader.Faction;
        n.BedCell = to.AccessCell; n.WorkCell = to.AccessCell;
        n.Strain = 30f;
        n.ResetBehaviour(); n.Brain.Clear(n);
        n.Brain.Push(n, new RoadGoal(to.AccessCell));
        if (boss != null) boss.ChangeRapport(n, -25f);
        n.Bark("I'm done with this place.");
        Chronicle.Event($"{n.DisplayName} walked out of {place} in the dead of winter and went to {dest}.", n.GlobalPosition, n, weight: 1.4f);
        Notice(post, $"{n.DisplayName} has walked out of {place}, gone to {dest}. The winter was too much for him.");
    }

    private static void Mutiny(FortLedger post, List<Npc> men, Npc boss, Npc ringleader)
    {
        string place = Economy.Accords.Name(post), rn = ringleader.DisplayName;
        ringleader.Bark("We'll not stir a foot till we're used like men!");
        Chronicle.Event($"The men of {place} refused their work, with {rn} speaking for them.", ringleader.GlobalPosition, ringleader, boss.DisplayName, 1.6f);

        // A player of the house there: the choice is his to make, and the outcome waits on him.
        foreach (var p in Sim.Players())
            if (p.HomePost == post && !p.IsDead && !p.Conversation.IsOpen && p.GlobalPosition.DistanceTo(IsoGrid.ToWorld(post.AccessCell)) < 1800f)
            {
                GD.Print($"[strain] mutiny page for {p.DisplayName} at {place}");
                NetHub.I?.PushDialogue(p, p.Conversation.StartScene(MutinyPage(p, post, men, boss, ringleader)));
                return;
            }
        Settle(post, men, boss, ringleader, boss.Personality.Courage > 0.6f ? "face" : post.Stores.Count(ItemId.Rum) >= men.Count * 2 ? "give" : "lose");
    }

    // How it ends: the Factor gives in, faces them down, or loses men over it.
    private static string Settle(FortLedger post, List<Npc> men, Npc boss, Npc ringleader, string how)
    {
        string place = Economy.Accords.Name(post), rn = ringleader.DisplayName, bn = boss.DisplayName;
        switch (how)
        {
            case "give":
            {
                int drams = Mathf.Min(post.Stores.Count(ItemId.Rum), men.Count * 2);
                post.Stores.TakeUpTo(ItemId.Rum, drams);
                foreach (var n in men) { n.Strain = Mathf.Max(0f, n.Strain - 25f); n.ChangeRapport(boss, -2f); }
                boss.Standing -= 2f;
                _mutinyUntil[post] = Today(GameCalendar.Instance);   // back to work tomorrow
                Notice(post, $"{bn} gives the men of {place} their rum and a day's holiday. They go back to work, and every man of them knows they won.");
                return $"{bn} has the rum brought out. The men drink, and in the morning they go back to the woodpile. Nobody at {place} will forget who gave way.";
            }
            case "face":
            {
                foreach (var n in men) n.Strain = Mathf.Clamp(n.Strain + (n == ringleader ? 10f : -12f), 0f, 100f);
                ringleader.ChangeRapport(boss, -30f);
                boss.Standing += 1f;
                ringleader.Standing -= 5f;
                _mutinyUntil[post] = Today(GameCalendar.Instance);
                Chronicle.Event($"{bn} faced down the men of {place} and put {rn} on short rations.", boss.GlobalPosition, boss, rn, 1.2f);
                Notice(post, $"{bn} faces down the men of {place}: {rn} is put on short rations and the rest go back to work.");
                return $"{bn} walks out alone into the yard, names {rn} for what he is, and asks which man will be next. Nobody moves. They go back to work. {rn} goes on short rations and says nothing, but he watches the Factor's door.";
            }
            default:
            {
                Desert(ringleader, post);
                int more = 0;
                foreach (var n in men) if (n != ringleader && n.Strain >= 80f && more < 2 && GD.Randf() < 0.5f) { Desert(n, post); more++; }
                foreach (var n in men) n.Strain = Mathf.Max(0f, n.Strain - 10f);
                boss.Standing -= 3f;
                Notice(post, $"The mutiny at {place} ends with {rn}{(more > 0 ? $" and {more} more" : "")} walking out, and the rest sullen at their work.");
                return $"There is no rum to buy them with and {bn} has not the stomach to face them. In the morning {rn}'s bunk is empty{(more > 0 ? $", and {more} more with it" : "")}. The rest go back to work because there is nothing else to do.";
            }
        }
    }

    private static ScenePage Page(string title, string text) => new() { Title = title, Text = text, CanLeave = false };
    private static ScenePage Done(string title, string text) => Page(title, text).Add("Go on", () => null);
    private static void Note(PlayerController p, string line) => p.Journal.Add($"{GameCalendar.Instance?.DateString}: {line}");

    private static ScenePage MutinyPage(PlayerController p, FortLedger post, List<Npc> men, Npc boss, Npc ringleader)
    {
        string rn = ringleader.DisplayName, bn = boss.DisplayName, rc = Barks.CallName(ringleader);
        var page = Page("The men refuse", $"The men have downed tools. They stand in the yard with their arms folded, and {rn} does the talking: fresh meat, a dram, an end to being treated like dogs, or not a man of them goes out to the woodpile tomorrow. {bn} is at his door. Every face turns to see where you stand.");
        page.Add($"Stand with {bn}", () =>
        {
            p.Standing += 2f;
            ringleader.ChangeRapport(p, -20f);
            foreach (var n in men) n.ChangeRapport(p, -4f);
            boss.ChangeRapport(p, 10f);
            Note(p, $"The men refused their work; you stood with {bn} against {rn}.");
            return Done("The men refuse", "You cross the yard and stand at the Factor's shoulder. " + Settle(post, men, boss, ringleader, "face"));
        });
        page.Add($"Stand with {rc} and the men", () =>
        {
            p.Standing -= 3f;
            boss.ChangeRapport(p, -15f);
            foreach (var n in men) n.ChangeRapport(p, 8f);
            Note(p, $"The men refused their work, and you stood with them.");
            string end = post.Stores.Count(ItemId.Rum) >= men.Count ? Settle(post, men, boss, ringleader, "give") : Settle(post, men, boss, ringleader, "lose");
            return Done("The men refuse", $"You fold your arms with the rest. {bn} looks at you a long moment and writes something in his book. " + end);
        });
        page.Add("Speak for both sides", () =>
        {
            float rapport = (boss.RapportWith(p) + ringleader.RapportWith(p)) / 2f;
            bool heard = GD.Randf() < Mathf.Clamp(0.35f + p.Standing / 60f + rapport / 150f, 0.1f, 0.9f);
            Note(p, heard ? $"You talked {bn} and {rn} round when the men refused their work." : $"You tried to talk {bn} and {rn} round when the men refused their work, and failed.");
            if (!heard)
            {
                ringleader.ChangeRapport(p, -5f); boss.ChangeRapport(p, -5f);
                return Done("The men refuse", $"Neither of them wants to hear it from you. " + Settle(post, men, boss, ringleader, boss.Personality.Courage > 0.5f ? "face" : "lose"));
            }
            p.Standing += 1f;
            ringleader.ChangeRapport(p, 6f); boss.ChangeRapport(p, 6f);
            int drams = Mathf.Min(post.Stores.Count(ItemId.Rum), men.Count);
            post.Stores.TakeUpTo(ItemId.Rum, drams);
            foreach (var n in men) n.Strain = Mathf.Max(0f, n.Strain - 18f);
            _mutinyUntil[post] = Today(GameCalendar.Instance);
            Chronicle.Event($"{p.DisplayName} talked {bn} and the men round at {Economy.Accords.Name(post)}.", p.GlobalPosition, p, weight: 1.2f);
            return Done("The men refuse", $"You get {bn} to promise a hunting party out tomorrow for fresh meat and a dram tonight, and you get {rc} to take it as a win without making the Factor eat his words. By noon the axes are going again.");
        });
        page.Add("Keep out of it", () =>
        {
            Note(p, $"The men refused their work; you kept out of it.");
            return Done("The men refuse", "You find something to do inside. " + Settle(post, men, boss, ringleader, boss.Personality.Courage > 0.6f ? "face" : post.Stores.Count(ItemId.Rum) >= men.Count * 2 ? "give" : "lose"));
        });
        return page;
    }

    // ---- the player at his post -------------------------------------------------------------

    private static void PlayerPages(FortLedger post, List<Npc> men, int today)
    {
        if (Average(post) < 40f) return;
        foreach (var p in Sim.Players())
        {
            if (p.HomePost != post || p.IsDead || p.Conversation.IsOpen || Convoys.BoardedBy(p) != null || p.CaptiveUntilDay >= 0) continue;
            if (p.GlobalPosition.DistanceTo(IsoGrid.ToWorld(post.AccessCell)) > 1800f) continue;
            if (_lastPage.TryGetValue(p, out int last) && today - last < 10) continue;
            _lastPage[p] = today;
            GD.Print($"[strain] long-winter page for {p.DisplayName} at {Economy.Accords.Name(post)}");
            NetHub.I?.PushDialogue(p, p.Conversation.StartScene(StovePage(p, post, men)));
            return;
        }
    }

    private static ScenePage StovePage(PlayerController p, FortLedger post, List<Npc> men)
    {
        var worst = men[0];
        foreach (var n in men) if (n.Strain > worst.Strain) worst = n;
        string wn = Barks.CallName(worst);
        var page = Page("A long winter", $"It has been dark since three. The men sit round the stove in the men's house not talking, and when they do it is to snap. {wn} has not said a civil word in a week. Someone has scratched another line on the wall by his bunk, counting days to the spring.");
        page.Add("Tell them a story", () =>
        {
            float lift = 4f + p.Skills.Woodcraft * 6f;
            foreach (var n in men) { n.Strain = Mathf.Max(0f, n.Strain - lift); n.ChangeRapport(p, 3f); }
            Note(p, "You told a story round the stove on a long winter night.");
            return Done("A long winter", "You tell them about the time the ice took your uncle's boat, and stretch it shamelessly. By the end even " + wn + " is laughing. It is not much. It is something.");
        });
        page.Add("Break out your own rum", () =>
        {
            int have = p.Inventory.Count(ItemId.Rum);
            if (have == 0) return Done("A long winter", "You feel in your pack for the flask and remember you drank it at Christmas. You sit with them instead.");
            int give = Mathf.Min(have, Mathf.Max(1, men.Count / 2));
            p.Inventory.TakeUpTo(ItemId.Rum, give);
            foreach (var n in men) { n.Strain = Mathf.Max(0f, n.Strain - 10f); n.ChangeRapport(p, 6f); n.Intoxication = Mathf.Min(100f, n.Intoxication + 15f); }
            Note(p, $"You shared {give} dram{(give == 1 ? "" : "s")} of your own rum round the stove.");
            return Done("A long winter", "The cup goes round. Somebody starts a song and the rest come in on it badly. The men will remember who had a flask and didn't keep it to himself.");
        });
        page.Add($"Ask the Factor for a dram all round", () =>
        {
            var boss = post.Leader;
            if (boss == null) return Done("A long winter", "There is nobody to ask.");
            bool yes = post.Stores.Count(ItemId.Rum) >= men.Count && GD.Randf() < Mathf.Clamp(0.3f + boss.RapportWith(p) / 100f + p.Standing / 80f, 0.1f, 0.9f);
            if (yes)
            {
                post.Stores.TakeUpTo(ItemId.Rum, men.Count);
                foreach (var n in men) { n.Strain = Mathf.Max(0f, n.Strain - 8f); n.ChangeRapport(p, 4f); }
                Note(p, $"You asked {boss.DisplayName} for a dram all round, and he gave it.");
                return Done("A long winter", $"{boss.DisplayName} grumbles about the returns and London and the price of rum, and has the cask broached anyway. The men know who asked.");
            }
            boss.ChangeRapport(p, -2f);
            Note(p, $"You asked {boss.DisplayName} for a dram all round, and he refused.");
            return Done("A long winter", $"{boss.DisplayName} tells you the rum is for the trade, not for idle hands, and you can tell the men so yourself.");
        });
        page.Add("Leave them to it", () => Done("A long winter", "You turn in early. Through the wall you can hear them at it again."));
        return page;
    }

    // Told to whoever is at the post to see it; the rest hear it as it travels.
    private static void Notice(FortLedger post, string text) => Sim.NoticeAt(post, text);
}
