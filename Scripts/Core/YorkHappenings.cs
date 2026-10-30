using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Items;
using IsometricFurTradeGame.Scripts.Player;
using IsometricFurTradeGame.Scripts.World;
using NetHub = IsometricFurTradeGame.Scripts.Net.Net;

namespace IsometricFurTradeGame.Scripts.Core;

// Life at York while a new hand waits on the boats (design doc P.5: never
// long without something to do). Every few hours something small happens
// near the player: a fight at the men's house, hunters in with geese, a
// fiddle in the evening, an old hand with something to teach, a sailor
// buying furs on the quiet. Each is a page of choices, and each moves the
// player's standing or the men's opinion of him.
//
// And once, a day or two after a Company man first comes ashore, the
// Nor'Westers come down the Hayes in the night and go for the stores.
//
// Server only. Ticked from Population.
public static class YorkHappenings
{
    private const float NearYork = 3200f;
    private static float _nextIn = 2.5f;              // game hours to the next happening
    private static readonly Dictionary<string, float> _last = new();   // when each kind last happened (abs hours)

    // The raid.
    private static float _raidAt = -1f;
    private static bool _raidOn;
    private static float _raidHours;
    private static readonly List<Npc> _raiders = new();
    private static readonly Dictionary<Npc, int> _loot = new();
    private static readonly HashSet<PlayerController> _stoodTo = new();
    private static int _carriedOff, _killed;

    // Private dealings someone saw: the Factor hears in a few hours.
    private sealed class Report { public PlayerController P; public float At; public string Witness, What; }
    private static readonly List<Report> _reports = new();

    public static void Reset()
    {
        _nextIn = 2.5f; _last.Clear();
        _raidAt = -1f; _raidOn = false; _raidHours = 0f; _raiders.Clear(); _loot.Clear(); _stoodTo.Clear(); _carriedOff = 0; _killed = 0;
        _danceOn = false; _dancers.Clear(); _danceTold.Clear(); _atDance.Clear(); Audio.DanceOn = false;
        _reports.Clear();
    }

    private static float Now()
    {
        var cal = GameCalendar.Instance;
        return cal == null ? 0f : cal.Year * 8784f + cal.DayOfYear * 24f;
    }

    public static bool AtYork(Agent a) => a != null && Sim.Fort != null && a.GlobalPosition.DistanceTo(WorldLayout.FortCentre) < NearYork;

    // ---- the tick ---------------------------------------------------------------------

    public static void Tick(float hours)
    {
        if (!NetHub.IsServer || hours <= 0f || Sim.Fort == null) return;
        var cal = GameCalendar.Instance;
        if (cal == null) return;
        float now = Now();

        foreach (var r in new List<Report>(_reports))
            if (now >= r.At) { _reports.Remove(r); Heard(r); }

        // Working out the days till the boats: the mess feeds him and the men's house keeps him warm.
        foreach (var p in Sim.Players())
            if (Convoys.IsWaiting(p) && cal.IsSkipping && !p.IsDead)
            { p.Needs.Hunger = Mathf.Max(p.Needs.Hunger, 75f); p.Needs.Thirst = Mathf.Max(p.Needs.Thirst, 75f); p.Needs.Warmth = Mathf.Max(p.Needs.Warmth, 75f); }

        RaidTick(now, hours);
        if (_raidOn) { if (_danceOn) EndDance("The alarm bell stops the fiddle dead."); return; }
        DanceTick(now);

        if (cal.IsSkipping) return;
        _nextIn -= hours;
        if (_nextIn > 0f) return;
        _nextIn = Dev ? 1.5f : (float)GD.RandRange(4.0, 7.0);
        foreach (var p in Sim.Players())
        {
            if (!Free(p)) continue;
            var page = Pick(p, now);
            if (page == null) { _nextIn = 1f; continue; }   // nothing fits just now: look again soon
            GD.Print($"[york] {page.Title}: for {p.DisplayName}");
            NetHub.I?.PushDialogue(p, p.Conversation.StartScene(page));
            break;
        }
    }

    // At York, on his own feet, not talking or in the prologue or the boats.
    private static bool Free(PlayerController p) =>
        p != null && GodotObject.IsInstanceValid(p) && !p.IsDead && AtYork(p) && !p.Conversation.IsOpen
        && Convoys.BoardedBy(p) == null && (Dev || !(p.Task?.Kind == TaskKind.Report && !p.Task.IsComplete)) && p.CaptiveUntilDay < 0;

    // Dev: `--happen` lets them come before the player has reported, and often.
    private static readonly bool Dev = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--happen") >= 0;
    // Dev: `--dance` (with --happen) makes the evening's happening the fiddle.
    private static readonly bool DevDance = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--dance") >= 0;

    private static bool Rested(string kind, float now, float hours) => !_last.TryGetValue(kind, out var at) || now - at >= hours;

    private static ScenePage Pick(PlayerController p, float now)
    {
        float h = GameCalendar.Instance.Hour;
        var tries = new List<System.Func<PlayerController, ScenePage>>();
        if (h >= 18.5f && h < 23.5f) { tries.Add(Dance); if (!DevDance) tries.Add(Cards); }
        else if (h >= 7f && h < 18.5f)
        {
            tries.Add(Brawl); tries.Add(Geese); tries.Add(Lesson);
            if (Sim.Ship != null && Sim.Ship.IsDocked) tries.Add(SailorBuys);
        }
        // Shuffle, then take the first that fits and has rested.
        for (int i = tries.Count - 1; i > 0; i--) { int j = GD.RandRange(0, i); (tries[i], tries[j]) = (tries[j], tries[i]); }
        foreach (var t in tries)
        {
            string kind = t.Method.Name;
            if (!Rested(kind, now, Dev ? 3f : kind is "Dance" or "Cards" ? 20f : 30f)) continue;
            var page = t(p);
            if (page == null) continue;
            _last[kind] = now;
            return page;
        }
        return null;
    }

    // ---- people to put in a scene -----------------------------------------------------

    private static List<Npc> MenNear(Vector2 at, float radius, System.Predicate<Npc> ok)
    {
        var list = new List<Npc>();
        foreach (var a in Sim.AgentsWithin(at, radius))
            if (a is Npc n && !n.IsDead && !n.Conversing && n.Convoy == null && ok(n)) list.Add(n);
        list.Sort((a, b) => a.GlobalPosition.DistanceSquaredTo(at).CompareTo(b.GlobalPosition.DistanceSquaredTo(at)));
        return list;
    }

    private static bool Hand(Npc n) => !n.IsFemale && n.Faction is Faction.HBC or Faction.ShipsCrew && n.Role is Role.Labourer or Role.Sailor;

    private static ScenePage Page(string title, string text) => new() { Title = title, Text = text, CanLeave = false };
    private static ScenePage Done(string title, string text) => Page(title, text).Add("Go on", () => null);

    private static void Note(PlayerController p, string line) => p.Journal.Add($"{GameCalendar.Instance?.DateString}: {line}");

    // ---- a fight at the men's house ---------------------------------------------------

    private static ScenePage Brawl(PlayerController p)
    {
        var men = MenNear(p.GlobalPosition, 1400f, Hand);
        if (men.Count < 2) return null;
        Npc a = men[0], b = men[1];
        string an = a.DisplayName, bn = b.DisplayName;
        a.Bark($"Say that again, {Barks.CallName(b)}!"); b.Bark("I'll say it twice!");
        Sim.Noise(a.GlobalPosition, 700f, a, "hit");
        var page = Page("A fight", $"Shouting, then the flat smack of a fist. {an} and {bn} are at it in the mud, rolling over and over, and a ring of men is already forming round them. Somebody is laying odds on {bn}.");
        page.Add("Get between them", () =>
        {
            bool hit = GD.Randf() < 0.45f;
            if (hit) { p.Needs.Health = Mathf.Max(5f, p.Needs.Health - 8f); Sim.Noise(p.GlobalPosition, 500f, a, "hit"); }
            a.ChangeRapport(p, 6f); b.ChangeRapport(p, 6f);
            p.Standing += 1f;
            Note(p, $"You pulled {an} and {bn} apart before it came to knives.");
            Chronicle.Event($"{p.DisplayName} broke up a fight between {an} and {bn}.", p.GlobalPosition, p, weight: 0.8f);
            return Done("A fight", (hit ? $"You catch an elbow in the mouth for your trouble and taste blood. " : "") +
                $"But you get a grip on {Barks.CallName(a)}'s collar and haul him off, and the fight goes out of them both. Later {Barks.CallName(b)} nods to you across the yard. Someone will have told the Factor who kept the peace.");
        });
        page.Add($"Put a twist of tobacco on {bn}", () =>
        {
            if (!p.Inventory.Has(ItemId.Tobacco)) return Done("A fight", "You pat your coat. Nothing to bet with. You watch instead.");
            bool won = GD.Randf() < 0.5f;
            if (won) p.Inventory.Add(ItemId.Tobacco); else p.Inventory.Remove(ItemId.Tobacco);
            foreach (var m in MenNear(p.GlobalPosition, 900f, Hand)) m.ChangeRapport(p, 2f);
            p.Standing -= 0.5f;
            return Done("A fight", won
                ? $"{bn} gets {Barks.CallName(a)} down and keeps him there. You collect your winnings, a second twist of tobacco, from a sour-faced sailor. The men grin at you: one of them now."
                : $"{an} gets the better of it, and your tobacco goes into somebody else's pocket. The men laugh and clap you on the back: one of them now, and poorer for it.");
        });
        page.Add("Fetch an officer", () =>
        {
            a.ChangeRapport(p, -8f); b.ChangeRapport(p, -8f);
            foreach (var m in MenNear(p.GlobalPosition, 900f, Hand)) if (m != a && m != b) m.ChangeRapport(p, -2f);
            p.Standing += 1.5f;
            Note(p, $"You fetched an officer to the fight between {an} and {bn}. Both were docked a day's pay.");
            return Done("A fight", $"The clerk comes out with his coat over his nightshirt and both men are docked a day's pay. The officers will remember you did right. {an} and {bn} will remember too.");
        });
        page.Add("Walk on by", () => Done("A fight", "Not your fight. Behind you the ring breaks up in jeers when the bos'n wades in."));
        return page;
    }

    // ---- the homeguard hunters come in with geese ---------------------------------------

    private static ScenePage Geese(PlayerController p)
    {
        var hunters = MenNear(p.GlobalPosition, 2600f, n => n.IsBandPerson && n.Role is Role.Hunter or Role.Chief);
        if (hunters.Count == 0) return null;
        var h = hunters[0];
        h.Bark("Niska! Geese for the house.");
        string hn = h.DisplayName;
        var page = Page("Geese", $"{hn} of the homeguard has come up from the marsh with a canoe-load of geese, forty or more, still warm. He wants them weighed and paid for at the trading shop before the clerk goes to his dinner, and there is no one at the landing to carry.");
        page.Add("Help him carry them up to the provision store", () =>
        {
            Sim.Fort.Stores.Add(ItemId.Goose, 8);
            h.ChangeRapport(p, 8f);
            p.Standing += 1f;
            p.CreeFluency = Mathf.Min(1f, p.CreeFluency + 0.01f);
            Note(p, $"You carried {hn}'s geese up to the provision store.");
            return Done("Geese", $"Four trips up the boardwalk with a brace on each shoulder. {hn} teaches you the word for goose, niska, and laughs at the way you say it. The clerk notes the geese in the book, and that you carried them.");
        });
        if (p.Inventory.Has(ItemId.Tobacco))
            page.Add("Offer him a twist of tobacco for a brace, on the quiet", () =>
            {
                p.Inventory.Remove(ItemId.Tobacco); h.Inventory.Add(ItemId.Tobacco);
                p.Inventory.Add(ItemId.Goose, 2);
                h.ChangeRapport(p, 5f);
                Witnessed(p, h, "bought geese from the homeguard on the side");
                return Done("Geese", $"{hn} takes the tobacco and smells it and hands you two fat geese. That is a better dinner than the mess will give you. It is also trading on your own account, which the Company does not allow.");
            });
        page.Add("Leave it to the clerk", () => Done("Geese", $"You leave {hn} to it. He shrugs and starts carrying them up himself."));
        return page;
    }

    // ---- an old hand with something to teach -------------------------------------------

    private static ScenePage Lesson(PlayerController p)
    {
        var old = MenNear(p.GlobalPosition, 2000f, n => Hand(n) && n.Age >= 30);
        if (old.Count == 0) return null;
        var m = old[0];
        string mn = m.DisplayName;
        int pick = GD.RandRange(0, 2);
        string what = pick switch { 0 => "how to set a snare so the hare doesn't chew out of it", 1 => "how to lay a fire that'll light in the wet", _ => "how a Bay man sights a musket that throws left" };
        var page = Page("An old hand", $"{mn} has been at York eleven winters and looks it. He watches you work for a while, then spits. \"You'll be dead by Christmas doing it like that. I could show you {what}. It'll cost you.\"");
        void Teach(float amount, string why)
        {
            switch (pick)
            {
                case 0: p.Skills.Learn(ref p.Skills.Trapping, amount, why); break;
                case 1: p.Skills.Learn(ref p.Skills.Woodcraft, amount, why); break;
                default: p.Skills.Learn(ref p.Skills.Hunting, amount, why); break;
            }
        }
        string skill = pick switch { 0 => "trapping", 1 => "woodcraft", _ => "hunting" };
        if (p.Inventory.Has(ItemId.Tobacco))
            page.Add("Pay him a twist of tobacco", () =>
            {
                p.Inventory.Remove(ItemId.Tobacco); m.Inventory.Add(ItemId.Tobacco);
                Teach(0.05f, $"{mn} showed you, for tobacco");
                m.ChangeRapport(p, 6f);
                Note(p, $"{mn} showed you {what}.");
                return Done("An old hand", $"An hour behind the boat shed and he shows you properly, cursing all the while. You come away knowing {what}. (Your {skill} improves.)");
            });
        page.Add("Offer to do his turn at the woodpile instead", () =>
        {
            GameCalendar.Instance?.SkipHours(2f);
            p.Needs.Hunger = Mathf.Max(0f, p.Needs.Hunger - 10f);
            Teach(0.03f, $"{mn} showed you, for a turn at the woodpile");
            m.ChangeRapport(p, 4f);
            return Done("An old hand", $"Two hours splitting his share of spruce while he sits on the chopping block and talks. Some of it is lies. Some of it is {what}. (Your {skill} improves a little.)");
        });
        page.Add("\"I'll manage.\"", () => { m.ChangeRapport(p, -2f); return Done("An old hand", $"\"Aye. They all say that.\" {mn} goes back to his pipe."); });
        return page;
    }

    // ---- a sailor buying furs ----------------------------------------------------------

    private static ScenePage SailorBuys(PlayerController p)
    {
        var tars = MenNear(p.GlobalPosition, 2400f, n => n.Faction == Faction.ShipsCrew);
        if (tars.Count == 0) return null;
        var s = tars[0];
        string sn = s.DisplayName;
        int furs = 0;
        foreach (var (id, n) in p.Inventory.All()) if (Items.Items.Of(id).Kind == ItemKind.Fur) furs += n;
        var page = Page("A sailor", $"{sn} from the ship falls into step beside you and speaks without moving his lips. \"A London furrier'll pay three times what the Company gives for a good skin, and the Company need never know. I've room in my sea-chest. Anything for me?\"");
        if (furs > 0)
            page.Add($"Sell him your furs ({furs})", () =>
            {
                float mb = 0f; int sold = 0;
                foreach (var (id, n) in new List<(ItemId, int)>(p.Inventory.All()))
                {
                    if (Items.Items.Of(id).Kind != ItemKind.Fur) continue;
                    mb += Items.Items.Of(id).ValueMb * n * 1.6f; sold += n;
                    p.Inventory.Remove(id, n);
                }
                p.PrivateWorthMb += mb;
                Witnessed(p, s, "sold furs to the ship's crew");
                Note(p, $"You sold {sold} skins to {sn} from the ship, for {mb:0} MB of your own.");
                return Done("A sailor", $"The skins go under his jacket, and a little purse of coin and a promise go into yours: {mb:0} made beaver of your own, more than the Company would give. If anyone saw, the Factor will hear of it.");
            });
        page.Add("\"Nothing yet. Maybe next year.\"", () =>
        {
            Note(p, $"{sn} from the ship would buy furs on the quiet. The homeguard Cree trade furs for tobacco.");
            return Done("A sailor", "\"Next year, then. Get some tobacco into a Cree hunter's hand and see what comes back.\" He winks and goes off toward the wharf.");
        });
        page.Add("\"That's theft from the Company.\"", () => { s.ChangeRapport(p, -6f); p.Standing += 0.5f; return Done("A sailor", "\"Is it now. Well, the Company's a long way from here.\" He goes off without another word."); });
        return page;
    }

    // ---- the evenings ------------------------------------------------------------------

    // ---- the dance ---------------------------------------------------------------------
    // Not a page that drops on you out of nowhere: the fiddle strikes up and
    // the whole post hears it - the reel comes over the yard, the men go
    // in, and you are told and pointed there. The men are in there dancing
    // whether you go or not. Walk in at the door and the evening is yours:
    // a run of scenes, the reel loud, the floor going round behind them.

    private static bool _danceOn;
    private static float _danceUntil;
    private static string _fiddler = "";
    private static readonly List<Npc> _dancers = new();
    private static readonly HashSet<PlayerController> _danceTold = new(), _atDance = new();
    public static bool DanceOn => _danceOn;

    private static Vector2 MensMiddle => IsoGrid.ToWorld(WorldLayout.MensHouse.Centre);

    // The men's house, for the compass, while the fiddle plays and you have not gone in.
    public static (Vector2 At, string Label)? DanceMark(Agent near) =>
        _danceOn && near is PlayerController p && _danceTold.Contains(p) && !_atDance.Contains(p) ? (MensMiddle, "the fiddle in the men's house") : null;

    private static ScenePage Dance(PlayerController p)
    {
        if (_danceOn) return null;
        var door = IsoGrid.ToWorld(WorldLayout.MensHouse.DoorCell);
        if (p.GlobalPosition.DistanceTo(door) > 2600f) return null;
        if (!StrikeUp()) return null;
        _danceTold.Add(p);
        // Already in the house: no need to be told; the evening starts.
        if (p.GlobalPosition.DistanceTo(MensMiddle) < 380f) { _atDance.Add(p); return Evening(p); }
        var page = Page("A fiddle!", $"Over the yard, from the men's house: a fiddle, then the stamp of boots and a whoop. {_fiddler} has got his fiddle out and they are pushing the benches back. Men are drifting in from all over the post, and the homeguard women are coming up from the tents to watch.");
        page.Add("Go over to the men's house", () => { p.Say("Follow the fiddle to the men's house."); return null; });
        page.Add("Not tonight", () => { _danceTold.Remove(p); Note(p, "You let the fiddle go on without you."); return null; });
        return page;
    }

    // The fiddler to his stool and the men onto the floor; the reel over the post.
    private static bool StrikeUp()
    {
        var house = WorldLayout.MensHouse;
        var men = MenNear(IsoGrid.ToWorld(house.DoorCell), 1800f, n => Hand(n) && !n.Sleeping && n.Brain.Tier0Goal() == null);
        if (men.Count < 3) return false;
        var floor = new List<Vector2I>(house.Interior());
        floor.Sort((a, b) => GD.Randi() % 2 == 0 ? -1 : 1);
        var cal = GameCalendar.Instance;
        float hours = Mathf.Clamp(24.5f - (cal?.Hour ?? 20f), 2.5f, 4.5f);
        _danceOn = true; _danceUntil = Now() + hours;
        _fiddler = men[0].DisplayName;
        _dancers.Clear();
        int spot = 0;
        for (int i = 0; i < men.Count && i < 10; i++)
        {
            var n = men[i];
            n.Brain.Push(n, new Agents.Goals.DanceGoal(i == 0 ? IsoGrid.Cell(house.Top, 1, 1) : floor[spot++ % floor.Count], hours, i == 0));
            _dancers.Add(n);
        }
        // A few of the homeguard women come up to the door to watch, and dance if asked.
        foreach (var w in MenNear(IsoGrid.ToWorld(house.DoorCell), 3000f, n => n.IsFemale && n.IsBandPerson && !n.Sleeping && n.Brain.Tier0Goal() == null))
        {
            if (_dancers.Count >= 13) break;
            w.Brain.Push(w, new Agents.Goals.DanceGoal(floor[spot++ % floor.Count], hours, false));
            _dancers.Add(w);
        }
        Audio.DanceOn = true;
        Sim.Headline("A fiddle strikes up in the men's house.");
        Chronicle.Shared(Sim.Fort, $"{_fiddler} played the fiddle in the men's house and everyone danced", 1f);
        GD.Print($"[york] the dance: {_fiddler} plays, {_dancers.Count} on the floor, {hours:0.0} h");
        return true;
    }

    private static void DanceTick(float now)
    {
        if (!_danceOn) return;
        // The evening holds while you are in it: the fiddle does not stop
        // with you halfway through a jig.
        foreach (var p in _atDance) if (GodotObject.IsInstanceValid(p) && !p.IsDead && p.Conversation.IsOpen) { _danceUntil = Mathf.Max(_danceUntil, now + 0.5f); break; }
        if (now >= _danceUntil) { EndDance("The fiddle stops. The candles are guttering and the men go yawning to their bunks."); return; }
        foreach (var p in Sim.Players())
        {
            if (p == null || p.IsDead || _atDance.Contains(p) || p.Conversation.IsOpen || Convoys.BoardedBy(p) != null) continue;
            if (p.GlobalPosition.DistanceTo(MensMiddle) > 380f) continue;
            _atDance.Add(p); _danceTold.Add(p);
            if (p == Sim.Player) GameCalendar.Instance?.CancelSkip();
            NetHub.I?.PushDialogue(p, p.Conversation.StartScene(Evening(p)));
        }
    }

    private static void EndDance(string line)
    {
        _danceOn = false;
        Audio.DanceOn = false;
        foreach (var n in _dancers)
            if (n != null && GodotObject.IsInstanceValid(n) && !n.IsDead && n.Brain.Top is Agents.Goals.DanceGoal) n.Brain.Clear(n);
        _dancers.Clear();
        foreach (var p in _atDance) if (GodotObject.IsInstanceValid(p) && !p.IsDead) p.Say(line);
        _danceTold.Clear(); _atDance.Clear();
    }

    private static Npc Dancer(System.Predicate<Npc> ok)
    {
        foreach (var n in _dancers) if (n != null && GodotObject.IsInstanceValid(n) && !n.IsDead && n.DisplayName != _fiddler && ok(n)) return n;
        return null;
    }

    // The evening itself: three turns of it, each a choice, and how it ends.
    private static ScenePage Evening(PlayerController p)
    {
        const string T = "The men's house";
        var mate = Dancer(n => !n.IsFemale);
        var woman = Dancer(n => n.IsFemale);
        string m = mate != null ? Agents.Barks.CallName(mate) : "Hugh";
        var page = Page(T, $"Heat and noise and tallow smoke. {_fiddler} sits on a keg by the stove with his eyes shut, sawing out a reel, and the floor is going round - boots stamping, men hooking arms and spinning, somebody keeping time on a kettle with a ladle. {m} sees you in the door and roars your name.");
        page.Add("Into the reel with them", () => Reel(p, mate));
        if (woman != null) page.Add($"Ask {woman.DisplayName} to dance", () => WithHer(p, woman));
        page.Add("A corner by the stove, and listen", () =>
        {
            p.Needs.Warmth = Mathf.Min(100f, p.Needs.Warmth + 25f);
            return Turn2(p, "You find a corner by the stove. It is a reel from home, and then a slow air, and for a while the Bay is very far away.");
        });
        return page;
    }

    private static ScenePage Reel(PlayerController p, Npc mate)
    {
        foreach (var n in _dancers) if (GodotObject.IsInstanceValid(n) && !n.IsFemale) n.ChangeRapport(p, 3f);
        p.Needs.Warmth = Mathf.Min(100f, p.Needs.Warmth + 15f);
        p.Needs.Hunger = Mathf.Max(0f, p.Needs.Hunger - 4f);
        string m = mate != null ? Agents.Barks.CallName(mate) : "a big Orkneyman";
        return Turn2(p, $"{m} hooks your arm and you are off, round and round, nobody knowing the steps and everybody doing them. You crash into the woodbox and the whole house cheers.");
    }

    private static ScenePage WithHer(PlayerController p, Npc woman)
    {
        bool welcome = woman.RapportWith(p) > -10f && GD.Randf() < 0.75f;
        if (!welcome) { p.Standing -= 0.5f; return Turn2(p, $"{woman.DisplayName} looks at you, looks at her sister, and they both laugh. Not you, not tonight. The men who saw it laugh louder."); }
        woman.ChangeRapport(p, 6f);
        Acquaint(p, woman);
        return Turn2(p, $"{woman.DisplayName} takes your hand without a word and leads you into it. She knows the steps better than any man in the room; they have been coming up to these dances since before you were born. When the tune ends she lets go and says something in Cree to her sister that makes them both smile.");
    }

    private static void Acquaint(PlayerController p, Npc n)
    {
        var m = new Agents.MemoryEntry { Text = $"{n.DisplayName} danced with {p.DisplayName} in the men's house.", Actor = p, Subject = p.DisplayName, Day = GameCalendar.Instance?.DayOfYear ?? 0f, Directness = 1, Weight = 1.1f, Key = $"dance{n.NetId}_{GameCalendar.Instance?.Day}", Witnesses = new List<string> { p.DisplayName, n.DisplayName } };
        n.Remember(m);
    }

    // The second turn: the rum, and the jig.
    private static ScenePage Turn2(PlayerController p, string said)
    {
        const string T = "The men's house";
        var page = Page(T, said + "\n\nThen the keg comes out from under somebody's bunk and goes round in a tin cup, and a voice shouts for a jig - the old way, one man at a time in the middle, and the rest judging.");
        page.Add("Take the cup when it comes", () =>
        {
            p.Needs.Thirst = Mathf.Min(100f, p.Needs.Thirst + 10f);
            p.Inventory.Add(Items.ItemId.Rum); p.Consume(Items.ItemId.Rum);
            return Turn3(p, "The rum goes down like a hot coal. The second cup goes down easier. The fiddle seems to get faster.");
        });
        page.Add("Get up and jig", () =>
        {
            float knack = 0.35f + p.Skills.Woodcraft * 0.5f + (p.Needs.Health > 70f ? 0.15f : 0f);
            if (GD.Randf() < knack)
            {
                p.Standing += 1.5f;
                foreach (var n in _dancers) if (GodotObject.IsInstanceValid(n)) n.ChangeRapport(p, 2f);
                Note(p, "You won the jig in the men's house.");
                return Turn3(p, "You go out into the middle and your feet remember something your head never knew. The fiddle chases you and you stay ahead of it. When you stop the house is on its feet; somebody puts a twist of tobacco in your hand for the prize.", tobacco: true);
            }
            return Turn3(p, "You go out into the middle, get halfway through, and go down on the boards in a heap. It is the best laugh the house has had all summer, and nobody laughs harder than you.");
        });
        page.Add("Keep to your corner", () => Turn3(p, "You let the cup go by and watch. Two of the Orkneymen jig against each other till one of them falls into the stove-wood."));
        return page;
    }

    // The third turn: the quarrel that comes at the end of every dance.
    private static ScenePage Turn3(PlayerController p, string said, bool tobacco = false)
    {
        const string T = "The men's house";
        if (tobacco) p.Inventory.Add(Items.ItemId.Tobacco);
        var a = Dancer(n => !n.IsFemale);
        var b = Dancer(n => !n.IsFemale && n != a);
        if (a == null || b == null)
            return Done(T, said + "\n\nThe candles burn down. The fiddle stops at last, and you go out into the cold under more stars than there are in Orkney.");
        string an = a.DisplayName, bn = b.DisplayName;
        var page = Page(T, said + $"\n\nLate on, {an} and {bn} are nose to nose by the door over something said about somebody's sister. The fiddle falters. Every man in the house is waiting to see what happens.");
        page.Add("Get between them", () =>
        {
            a.ChangeRapport(p, 2f); b.ChangeRapport(p, 2f); p.Standing += 1f;
            Note(p, $"You kept {an} and {bn} from blows at the dance.");
            return Done(T, $"You get an arm round each of them and walk them apart, talking nonsense. {_fiddler} strikes up again, louder. In the morning neither of them will remember what it was about, but both of them will remember you.");
        });
        page.Add("Egg them on", () =>
        {
            a.ChangeRapport(p, -3f); b.ChangeRapport(p, -3f);
            Chronicle.Event($"{an} and {bn} came to blows at the dance in the men's house.", MensMiddle, a, an, 0.9f);
            return Done(T, "\"Go on, then!\" They go at it, and the dance becomes a brawl, and the brawl goes out into the yard. Somebody's eye will be black at the Factor's muster in the morning, and he will want to know whose idea it was.");
        });
        page.Add("Slip out into the night", () => Done(T, "You slip out. Behind you the fiddle has started again. Overhead, the northern lights are moving over the Bay like a curtain someone has breathed on."));
        return page;
    }

    private static ScenePage Cards(PlayerController p)
    {
        var men = MenNear(p.GlobalPosition, 1800f, Hand);
        if (men.Count < 2 || !p.Inventory.Has(ItemId.Tobacco) && !p.Inventory.Has(ItemId.TradeGoods)) return null;
        var dealer = men[0];
        string dn = dealer.DisplayName;
        var stake = p.Inventory.Has(ItemId.Tobacco) ? ItemId.Tobacco : ItemId.TradeGoods;
        string sname = stake == ItemId.Tobacco ? "a twist of tobacco" : "a piece of goods";
        var page = Page("Cards", $"{dn} has a greasy pack of cards and a candle on an upturned keg behind the men's house. \"Loo, a penny a trick, and since nobody has a penny, {sname} a hand. Sit down.\"");
        page.Add($"Play a hand ({sname})", () =>
        {
            bool won = GD.Randf() < 0.45f + p.Skills.Trade * 0.15f;
            if (won) p.Inventory.Add(stake); else { p.Inventory.Remove(stake); dealer.Inventory.Add(stake); }
            dealer.ChangeRapport(p, 2f);
            if (GameCalendar.Instance?.Hour > 21f) p.Standing -= 0.25f;
            return Done("Cards", won ? $"The cards fall your way. You stand up {sname} richer, and {dn} swears you've done this before." : $"The cards do not fall your way, and {dn} pockets your stake with a wink.");
        });
        page.Add("\"Not tonight.\"", () => Done("Cards", "You leave them to it."));
        return page;
    }

    // ---- private dealings and who saw them ----------------------------------------------

    // Trading on your own account where a Company man can see: he talks, and
    // the Factor hears it in a few hours. Returns the witness, or null.
    public static string Witnessed(PlayerController p, Npc with, string what)
    {
        Chronicle.Event($"{p.DisplayName} {what}.", p.GlobalPosition, p, weight: 1.2f);
        foreach (var a in Sim.AgentsWithin(p.GlobalPosition, 700f))
        {
            if (a is not Npc n || n == with || n.IsDead || n.Faction != p.Faction || n.IsFemale && n.Role != Role.Wife) continue;
            if (n.RapportWith(p) > 40f) continue;   // a friend keeps his mouth shut
            _reports.Add(new Report { P = p, At = Now() + (float)GD.RandRange(3.0, 12.0), Witness = n.DisplayName, What = what });
            return n.DisplayName;
        }
        return null;
    }

    private static void Heard(Report r)
    {
        var p = r.P;
        if (p == null || !GodotObject.IsInstanceValid(p) || p.IsDead) return;
        p.Standing -= 2f;
        string factor = (p.HomePost ?? Sim.Fort)?.Leader?.DisplayName ?? "The Factor";
        Note(p, $"{r.Witness} told {factor} you {r.What}. Your standing with the Company suffers.");
        if (p.IsLocal) Sim.NoticeLocal($"{factor} has heard that you {r.What}: {r.Witness} told him. (standing -2)");
        else Sim.Notice($"{factor} has heard that {p.DisplayName} {r.What}.");
    }

    // ---- the raid ------------------------------------------------------------------------

    public static bool RaidOn => _raidOn;

    // The nearest raider still about, for the compass.
    public static (Vector2 At, string Label)? RaidMark(Agent near)
    {
        if (!_raidOn || near == null) return null;
        Npc best = null; float bd = float.MaxValue;
        foreach (var n in _raiders)
        {
            if (n == null || !GodotObject.IsInstanceValid(n) || n.IsDead) continue;
            float d = n.GlobalPosition.DistanceSquaredTo(near.GlobalPosition);
            if (d < bd) { bd = d; best = n; }
        }
        return best != null ? (best.GlobalPosition, "Nor'Westers in the fort - drive them off") : null;
    }

    private static void RaidTick(float now, float hours)
    {
        if (_raidOn) { RaidRuns(hours); return; }
        if (Campaign.Has("york_raid") || Economy.Rivalry.United || Sim.Nwc == null) return;
        if (_raidAt < 0f)
        {
            // A Company man ashore at York: a day or two from now, before dawn.
            foreach (var p in Sim.Players())
                if (p.Faction == Faction.HBC && AtYork(p) && !p.IsDead && Convoys.BoardedBy(p) == null)
                {
                    float midnight = Mathf.Floor(now / 24f) * 24f;
                    _raidAt = midnight + 24f * GD.RandRange(1, 2) + 4.5f;
                    GD.Print($"[york] the Nor'Westers will come in {(_raidAt - now) / 24f:0.0} days");
                    break;
                }
            foreach (string arg in OS.GetCmdlineUserArgs())
                if (arg == "--yorkraid" && _raidAt > now + 0.1f) { _raidAt = now + 0.05f; GD.Print("[dev] the raid on York comes now"); }
            return;
        }
        if (now >= _raidAt) BeginRaid();
    }

    private static readonly string[] RaiderNames = { "Baptiste Cadotte", "Joseph Falcon", "Pierre Deschamps", "Toussaint Lapointe", "Louis Primeau", "Antoine Trottier", "Michel Paquette" };

    private static void BeginRaid()
    {
        var pop = (Engine.GetMainLoop() as SceneTree)?.GetFirstNodeInGroup("population") as Population;
        if (pop == null) { _raidAt = -1f; return; }
        Campaign.Flag("york_raid");
        _raidOn = true; _raidHours = 0f;
        var gate = IsoGrid.ToWorld(WorldLayout.GateOutside);
        var away = (gate - WorldLayout.FortCentre).Normalized();
        var boatsAt = Convoys.LandNear(gate + away * 900f + away.Orthogonal() * 500f);
        var boats = Sim.Paths?.NearestWalkable(IsoGrid.FromWorld(boatsAt), 6) ?? IsoGrid.FromWorld(boatsAt);
        int party = 5 + (Economy.Rivalry.Tension > 50f ? 1 : 0);
        var frames = GD.Load<SpriteFrames>("res://Scenes/Npc/FactorFrames.tres");
        for (int i = 0; i < party; i++)
        {
            var cell = Sim.Paths?.NearestWalkable(IsoGrid.Cell(boats, i % 3 - 1, i / 3), 4) ?? boats;
            var n = pop.Spawn(RaiderNames[i % RaiderNames.Length], Role.Voyageur, Faction.NWC, cell, cell, Sim.Nwc, frames, new Color(0.85f, 0.75f, 0.75f));
            n.Inventory.Add(ItemId.Capote); n.Inventory.Add(ItemId.Knife);
            if (i < party - 1) { n.Inventory.Add(ItemId.Musket); n.Inventory.Add(ItemId.Powder, 4); }
            n.Personality.Courage = Mathf.Max(n.Personality.Courage, 0.5f);
            n.Brain.Clear(n);
            n.Brain.Push(n, new Agents.Goals.RaidPartyGoal(Sim.Fort, boats));
            _raiders.Add(n);
        }
        Economy.Rivalry.Raise(12f, "the Nor'Westers' raid on York Factory");
        Chronicle.Event($"A party of Nor'Westers came down the Hayes in the night and fell on York Factory.", gate, null, "the raid on York", 2.5f);
        Sim.Headline("Nor'Westers at York! The alarm bell is ringing.");
        Audio.Play("fort_bell_alarm");
        Audio.Play("fort_bell_alarm", delay: 2.5f);

        // The house turns out: the men with any nerve take muskets from the rack.
        int armed = 0;
        foreach (var n in new List<Npc>(Sim.Npcs))
        {
            if (armed >= 8) break;
            if (n.IsDead || n.Faction != Faction.HBC || n.IsFemale || !AtYork(n) || n.Role is not (Role.Labourer or Role.Clerk) || n.Personality.Courage < 0.3f) continue;
            if (!n.Inventory.Has(ItemId.Musket) && Sim.Fort.Stores.Remove(ItemId.Musket)) n.Inventory.Add(ItemId.Musket);
            if (!n.Inventory.Has(ItemId.Musket)) continue;
            n.Brain.Push(n, new Agents.Goals.DefendHouseGoal(Sim.Fort.AccessCell, 6f));
            armed++;
        }

        foreach (var p in Sim.Players())
        {
            if (p.IsDead || !AtYork(p) || p.CaptiveOf != null) continue;   // a prisoner in their store hears it through the door
            if (p == Sim.Player) GameCalendar.Instance?.CancelSkip();
            NetHub.I?.PushDialogue(p, p.Conversation.StartScene(Alarm(p)));
        }
        GD.Print($"[york] the raid: {party} Nor'Westers land at {boats}, {armed} of the house armed");
    }

    private static ScenePage Alarm(PlayerController p)
    {
        float h = GameCalendar.Instance?.Hour ?? 5f;
        string when = h < 7f ? "in the grey before dawn. Men tumble out of the men's house half-dressed" : h >= 20f ? "in the dark. Men come running from the men's house with candles" : "in broad day. Men drop their work and come running";
        var page = Page("The alarm", $"The bell. Somebody is hammering the alarm bell over the gate {when}, shouting: \"Nor'Westers! Boats below the wharf, and they're making for the Depot!\"");
        if (p.Inventory.Has(ItemId.Musket))
            page.Add("Take up your gun and run for the Depot", () => { _stoodTo.Add(p); return null; });
        else
            page.Add("Snatch a musket from the rack and run for the Depot", () =>
            {
                if (Sim.Fort.Stores.Remove(ItemId.Musket)) { p.Inventory.Add(ItemId.Musket); p.Inventory.Add(ItemId.Powder, 6); }
                else p.Inventory.Add(ItemId.Musket);   // a dead man's, from the floor of the guardroom
                _stoodTo.Add(p);
                return null;
            });
        page.Add("Pick up an axe handle and go", () => { _stoodTo.Add(p); return null; });
        page.Add("Keep your head down", () => null);
        return page;
    }

    private static void RaidRuns(float hours)
    {
        _raidHours += hours;
        int about = 0;
        foreach (var n in new List<Npc>(_raiders))
        {
            if (n == null || !GodotObject.IsInstanceValid(n))
            {
                // Back in their boats and away down the river with it.
                _carriedOff += _loot.GetValueOrDefault(n);
                _loot.Remove(n); _raiders.Remove(n);
                continue;
            }
            if (n.IsDead)
            {
                if (n.LastAttacker is PlayerController pk) _stoodTo.Add(pk);
                _killed++; _raiders.Remove(n); _loot.Remove(n);
                continue;
            }
            if (n.LastAttacker is PlayerController ph) _stoodTo.Add(ph);
            int lots = 0;
            foreach (var (id, c) in n.Inventory.All()) { var k = Items.Items.Of(id).Kind; if (k is not (ItemKind.Weapon or ItemKind.Clothing or ItemKind.Tool)) lots += c; }
            _loot[n] = Mathf.Max(0, lots - 4);   // what they brought (powder) is not loot
            about++;
        }
        if (about > 0 && _raidHours < 8f) return;
        EndRaid();
    }

    private static void EndRaid()
    {
        _raidOn = false;
        foreach (var n in _raiders) if (n != null && GodotObject.IsInstanceValid(n) && !n.IsDead) { _carriedOff += _loot.GetValueOrDefault(n); n.Raiding = false; n.QueueFree(); }
        _raiders.Clear(); _loot.Clear();
        string how = _carriedOff == 0
            ? "They got away with nothing."
            : $"They got away with {_carriedOff} pieces from the stores.";
        string dead = _killed == 0 ? "" : _killed == 1 ? " One of them lies dead in the yard." : $" {_killed} of them lie dead in the yard.";
        string line = $"The Nor'Westers are gone from York, back down the river. {how}{dead}";
        Sim.Headline(line);
        Chronicle.Event(line, WorldLayout.FortCentre, null, "the raid on York", 2f);
        foreach (var p in Sim.Players())
        {
            if (p.IsDead) continue;
            if (_stoodTo.Contains(p) && AtYork(p))
            {
                p.Standing += _carriedOff == 0 ? 4f : 2.5f;
                Campaign.Deed("defend");
                Note(p, $"You stood to when the Nor'Westers came for York's stores. {how}{dead} The Factor saw who turned out.");
                if (!p.Conversation.IsOpen)
                    NetHub.I?.PushDialogue(p, p.Conversation.StartScene(Done("After the raid",
                        $"Grey light over the Hayes. {line}\n\n{Sim.Fort?.Leader?.DisplayName ?? "The Factor"} walks the yard with his hands behind his back, looking at who has a musket and who has mud on his knees. He stops in front of you. \"You turned out. Good.\" That is all, and from him it is a great deal.")));
            }
            else if (AtYork(p)) Note(p, $"The Nor'Westers raided York's stores. {how}{dead}");
        }
        _stoodTo.Clear();
        GD.Print($"[york] raid over: {_carriedOff} pieces carried off, {_killed} raiders dead");
    }
}
