using System;
using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Agents.Goals;
using IsometricFurTradeGame.Scripts.Economy;
using IsometricFurTradeGame.Scripts.Items;
using IsometricFurTradeGame.Scripts.World;
using static IsometricFurTradeGame.Scripts.Agents.Goals.ChoreGoal;

namespace IsometricFurTradeGame.Scripts.Core;

// Rules a people make for themselves, and unmake (design doc 10.7, 12.5).
// The town keeps its bread back from the lakes trade because its meeting
// voted to; a camp turns away the traders' rum because its council agreed
// to; the posts of the Bay give no spirits because London has written so.
// None of it is fixed: anyone with an interest and a tongue can carry a
// motion round the houses, get the meeting called, speak to it, and see it
// carried or thrown out - a merchant who can talk the farmers into shipping
// the town's winter down the lakes, say, and a hungry spring after.
//
// A body (a town meeting, a band's council, the Governor and Committee) has
// laws and voters. A motion is opened by a sponsor, canvassed house to house
// (by the sponsor for it and a champion against it), put to a meeting the
// sponsor calls, debated there by whoever rises to speak, and voted by
// whoever came. How each voter leans is his own interest (what the law does
// to his house), his temper, and everything said to him since.
public enum LawKind { KeepBread, ExportDuty, ImportDuty, NoSpirits }   // saved as ints: append only
public enum BodyKind { Town, Band, Company }
public enum Motion { Enact, Repeal }
public enum MotionStage { Canvassing, Called, Debating, Decided }

public class Law
{
    public LawKind Kind;
    public float Value;      // KeepBread: the share of a year's food the market keeps; duties: the rate
    public int Since;        // year
    public string By = "";
}

public class Voter
{
    public string Name;
    public Npc Npc;          // null for a body that sits off the map (the Committee)
    public float Weight = 1f;
}

public class Body
{
    public string Key, Name;
    public BodyKind Kind;
    public Town Town;
    public BandLedger Band;
    public float Majority = 0.5f;     // the share of the votes cast a motion needs
    public int Chest;                 // duties collected, in pence: the poor relief
    public readonly List<Law> Laws = new();
    public Law Get(LawKind k) => Laws.Find(l => l.Kind == k);
    public string Meeting => Kind switch { BodyKind.Town => "town meeting", BodyKind.Band => "council", _ => "Committee" };

    // Who may vote, worked out once a day.
    private List<Voter> _voters;
    private int _votersDay = -1;
    public List<Voter> Voters()
    {
        if (_voters != null && _votersDay == Core.Laws.Today) return _voters;
        _votersDay = Core.Laws.Today;
        var list = _voters = new List<Voter>();
        switch (Kind)
        {
            case BodyKind.Town:
                // One vote to a house, cast by its master: the householders of
                // the town. The garrison, the market and the boarders have none.
                foreach (var h in Town.Houses.Values)
                {
                    if (h == Town.Market || h.Key == "garrison") continue;
                    Npc head = null;
                    foreach (var m in h.Members)
                        if (GodotObject.IsInstanceValid(m) && !m.IsDead && !m.IsFemale && m.Age >= 21 && m.OffMap == "" && m.Role != Role.Child && (head == null || m.Age > head.Age)) head = m;
                    if (head != null) list.Add(new Voter { Name = head.DisplayName, Npc = head });
                }
                break;
            case BodyKind.Band:
                // The chief and the heads of lodges sit at the council; the
                // chief's word carries the most, and it wants most of them.
                foreach (var n in Sim.Npcs)
                    if (n.HomeStores == Band && !n.IsDead && n.OffMap == "" && n.Role is Role.Chief or Role.Hunter)
                        list.Add(new Voter { Name = n.DisplayName, Npc = n, Weight = n.Role == Role.Chief ? 3f : 1f });
                break;
            case BodyKind.Company:
                foreach (var (name, w) in Committee) list.Add(new Voter { Name = name, Weight = w });
                break;
        }
        return list;
    }

    // The Governor, the Deputy and the Committee in London.
    private static readonly (string, float)[] Committee = { ("the Governor", 2f), ("the Deputy Governor", 1.5f), ("Mr. Colvile", 1f), ("Mr. Berens", 1f), ("Mr. Pelly", 1f), ("Mr. Wedderburn", 1f) };

    // Where it sits: the church, or the market if there is none; the camp's centre.
    public Vector2I Hall()
    {
        if (Kind == BodyKind.Town)
        {
            var ch = Town.Buildings.Find(b => b.Def.Kind == BuildingKind.Church);
            return ch != null ? ch.Def.Threshold : Town.Market?.AccessCell ?? IsoGrid.FromWorld(Town.Centre);
        }
        return Band?.Camp ?? Vector2I.Zero;
    }
    public bool Sits => Kind != BodyKind.Company;
}

public class Proposal
{
    public int Id;
    public Body Body;
    public LawKind Kind;
    public Motion Motion;
    public float Value;
    public string Sponsor = "", Opponent = "";
    public int Opened, MeetingDay = -1;
    public float MeetingHour = 14f;
    public MotionStage Stage;
    public string Result = "";
    // What has been said to each voter since it was opened: + for, - against.
    public readonly Dictionary<string, float> Swayed = new();
    public readonly Dictionary<string, int> Visits = new();
    public readonly HashSet<string> Present = new();
    public readonly HashSet<string> Coming = new();   // set out for the meeting: counted when it sits
    public bool Summoned;
    public readonly Queue<(string who, int side)> Speakers = new();
    public float NextSpeech;
    public int PlayerSign;   // the player's side: +1 for, -1 against, 0 none

    private Agent _sponsor;
    public Agent SponsorAgent()
    {
        if (_sponsor != null && GodotObject.IsInstanceValid(_sponsor) && !_sponsor.IsDead && _sponsor.DisplayName == Sponsor) return _sponsor;
        return _sponsor = Laws.Speaker(Sponsor);
    }

    public string Title => Motion == Motion.Repeal ? $"to repeal the law {Laws.Describe(Kind, Body.Get(Kind)?.Value ?? Value, Body)}" : Body.Get(Kind) != null ? $"to amend the law, {Laws.Describe(Kind, Value, Body)}" : Laws.Describe(Kind, Value, Body);
}

public static class Laws
{
    public static readonly List<Body> Bodies = new();
    public static readonly List<Proposal> Open = new();
    public static readonly List<string> Minutes = new();
    private static readonly Dictionary<string, int> _lastTried = new();   // body|kind -> day
    private static int _nextId = 1, _lastDay = -1;
    private static float _lastHour = -1f;
    private static bool _seeded;

    private static GameCalendar Cal => GameCalendar.Instance;
    public static int Today => Cal == null ? 0 : Cal.Year * 366 + (int)Cal.DayOfYear;

    // ---- what the laws say, for the rest of the game ---------------------------

    public static Body Of(Town t)
    {
        Ensure();
        if (t == null) return null;
        return Bodies.Find(b => b.Town == t) ?? (t.Standing ? AddTown(t) : null);
    }
    public static Body Of(Household h)
    {
        Ensure();
        if (h is TownHousehold th) return Of(th.Town);
        if (h is BandLedger bl) return Bodies.Find(b => b.Band == bl) ?? AddBand(bl);
        return null;
    }
    public static Body Company => Bodies.Find(b => b.Kind == BodyKind.Company);

    // The share of a year's food the town's market must hold before any goes
    // down the lakes (Economy/Money.ShipSurplus). No law, no mark.
    public static float BreadKept(Town t) => Of(t)?.Get(LawKind.KeepBread)?.Value ?? 0f;

    // The duty on a sum of trade, paid into the town's chest; what is left.
    public static int Levy(Town t, LawKind k, int pence)
    {
        var b = Of(t);
        var law = b?.Get(k);
        if (law == null || pence <= 0) return 0;
        int duty = Mathf.RoundToInt(pence * law.Value);
        b.Chest += duty;
        return duty;
    }

    // No spirits sold in the town, drunk in the camp, or given across a
    // company's counter.
    public static bool Dry(Household h) => Of(h)?.Get(LawKind.NoSpirits) != null;
    public static bool CompanyDry(Faction company) => company == Faction.HBC && Company?.Get(LawKind.NoSpirits) != null;

    public static string Short(LawKind k) => k switch
    {
        LawKind.KeepBread => "the bread kept back",
        LawKind.ExportDuty => "the duty on grain shipped",
        LawKind.ImportDuty => "the duty on store goods",
        _ => "the ban on spirits",
    };
    // The law as it is written: "that the market keep ...".
    public static string Describe(LawKind k, float v, Body b) => k switch
    {
        LawKind.KeepBread => $"that the market keep {Bread(v)} before any is shipped down the lakes",
        LawKind.ExportDuty => $"that a duty of {Mathf.RoundToInt(v * 100)} in the hundred be paid on grain shipped down the lakes",
        LawKind.ImportDuty => $"that a duty of {Mathf.RoundToInt(v * 100)} in the hundred be paid on store goods brought up",
        _ => b?.Kind == BodyKind.Company ? "that no spirits be given in trade at the Company's posts" : b?.Kind == BodyKind.Band ? "that no rum be taken into the camp" : "that no spirits be sold in the town",
    };
    // A third of the year's food in each of the three main kinds is the town's food until the harvest.
    private static string Bread(float v)
    {
        float f = Mathf.Round(v * 3f * 4f) / 4f;
        return f switch { 1f => "the town's bread until the harvest", 0.5f => "half the town's bread until the harvest", 0.25f => "a quarter of the town's bread until the harvest", 0.75f => "three parts of the town's bread until the harvest", 1.5f => "the town's bread until the harvest and half as much again", 2f => "twice the town's bread until the harvest", _ => $"{f:0.##} times the town's bread until the harvest" };
    }
    public static string Text(Law l, Body b) => $"{char.ToUpper(Describe(l.Kind, l.Value, b)[0])}{Describe(l.Kind, l.Value, b)[1..]} (since {l.Since}, {l.By})";

    // What a body may take up at all.
    public static IEnumerable<LawKind> Competent(Body b) => b.Kind switch
    {
        BodyKind.Town => new[] { LawKind.KeepBread, LawKind.ExportDuty, LawKind.ImportDuty, LawKind.NoSpirits },
        _ => new[] { LawKind.NoSpirits },
    };
    public static float DefaultValue(LawKind k) => k switch { LawKind.KeepBread => 1f / 3f, LawKind.ExportDuty => 0.1f, LawKind.ImportDuty => 0.1f, _ => 1f };

    // ---- the bodies and their standing laws ------------------------------------

    private static void Ensure()
    {
        if (_seeded) return;
        _seeded = true;
        foreach (var t in Towns.All) if (t.Standing) AddTown(t);
        foreach (var bl in Sim.Bands) if (bl != null) AddBand(bl);
        Bodies.Add(new Body { Key = "company:HBC", Name = "the Governor and Committee", Kind = BodyKind.Company, Majority = 0.5f });
    }
    private static Body AddTown(Town t)
    {
        var b = new Body { Key = "town:" + t.Name, Name = $"the town meeting of {t.Name}", Kind = BodyKind.Town, Town = t };
        // The assize: what the magistrates laid down when the town was
        // young, that the merchants ship only what it can spare.
        b.Laws.Add(new Law { Kind = LawKind.KeepBread, Value = 1f / 3f, Since = t.Year > 0 ? t.Year : 1800, By = "the magistrates in Quarter Sessions" });
        Bodies.Add(b);
        return b;
    }
    private static Body AddBand(BandLedger bl)
    {
        if (bl == null) return null;
        var b = new Body { Key = "band:" + bl.Name, Name = $"the council of {bl.Name}", Kind = BodyKind.Band, Band = bl, Majority = 2f / 3f };
        Bodies.Add(b);
        return b;
    }

    // ---- who stands where -------------------------------------------------------

    // How well someone carries a room: a talker, used to bargaining, of some
    // standing. 0.2 .. ~1.2.
    public static float Persuasion(Agent a)
    {
        if (a is Npc n) return 0.2f + n.Personality.Talkative * 0.4f + n.Personality.Ambition * 0.15f + n.Skills.Trade * 0.3f + Mathf.Clamp(n.Standing, 0f, 100f) / 250f;
        return a == null ? 0f : 0.3f + a.Skills.Trade * 0.5f;
    }

    // How far a law tightens (+) or loosens (-) things if the motion carries:
    // more bread kept, a higher duty, a ban where there was none.
    private static float Tightening(Proposal p)
    {
        float now = p.Body.Get(p.Kind)?.Value ?? 0f;
        float then = p.Motion == Motion.Repeal ? 0f : p.Value;
        float d = then - now;
        return p.Kind switch { LawKind.KeepBread => d * 3f, LawKind.NoSpirits => d, _ => d * 5f };
    }

    // What one person's house gains from the law being tighter (+) or looser (-), per unit.
    private static float Stance(Npc n, Body b, LawKind k)
    {
        float hunger = 0f;
        if (n.HomeStores is TownHousehold h && h.Alive() > 0)
        {
            int food = h.Stores.Count(ItemId.Bread) + h.Stores.Count(ItemId.Flour) * Fields.LoavesPerMeal + h.Stores.Count(ItemId.Grain) + h.Stores.Count(ItemId.Corn) + h.Stores.Count(ItemId.Roots);
            hunger = Mathf.Clamp(1f - food / (h.Alive() * 60f), 0f, 1f);   // under a month's food in the house
        }
        // A stable lean of his own, the same every time he is asked.
        float own = (Hash(n.DisplayName + (int)k) % 1000) / 1000f - 0.5f;
        switch (k)
        {
            case LawKind.KeepBread:
                return n.Role switch { Role.Farmer => -0.15f, Role.Merchant => -0.6f, Role.Labourer => 0.45f, Role.Artisan => 0.3f, Role.Priest => 0.4f, Role.Officer => 0.2f, _ => 0.1f }
                    + hunger * 0.5f - n.Personality.Ambition * 0.3f + own * 0.3f;
            case LawKind.ExportDuty:
                return n.Role switch { Role.Farmer => -0.35f, Role.Merchant => -0.6f, Role.Labourer => 0.3f, Role.Priest => 0.2f, _ => 0f } + hunger * 0.3f + own * 0.3f;
            case LawKind.ImportDuty:
                return n.Role switch { Role.Merchant => -0.5f, Role.Artisan => 0.5f, Role.Farmer => -0.1f, Role.Labourer => -0.2f, _ => 0f } + own * 0.3f;
            default:
                if (b.Kind == BodyKind.Band)
                {
                    float taste = (b.Band?.RumTaste ?? 0f) / 100f;
                    // The chief sees what it does to the camp; the young men want it.
                    return n.Role == Role.Chief ? 0.2f + taste * 0.6f : 0.2f - taste * 0.7f + own * 0.4f;
                }
                return n.Role switch { Role.Priest => 0.8f, Role.Merchant => -0.4f, _ => 0f } - Mathf.Clamp(n.Intoxication / 50f, 0f, 0.4f) + own * 0.8f - n.Personality.Talkative * 0.2f;
        }
    }
    private static float CommitteeStance(string name, LawKind k)
    {
        // London's view of the rum: dear, dangerous, and the Canadians give it freely.
        float own = (Hash(name + (int)k) % 1000) / 1000f - 0.5f;
        return 0.1f + own * 0.6f;
    }

    // Where a voter stands, -1 (against) .. +1 (for): his interest, a
    // suspicious man's dislike of change, what he thinks of the sponsor, and
    // whatever he has been told.
    public static float Lean(Proposal p, Voter v)
    {
        float interest;
        if (v.Npc == null) interest = CommitteeStance(v.Name, p.Kind) * Tightening(p);
        else
        {
            interest = Stance(v.Npc, p.Body, p.Kind) * Tightening(p) - v.Npc.Personality.Suspicion * 0.12f;
            if (p.SponsorAgent() is { } sp) interest += v.Npc.RapportWith(sp) / 250f;
        }
        return Mathf.Clamp(interest + p.Swayed.GetValueOrDefault(v.Name), -1f, 1f);
    }
    private static bool VotesFor(Proposal p, Voter v)
    {
        float l = Lean(p, v);
        if (Mathf.Abs(l) < 0.04f) return Hash(v.Name + p.Id) % 2 == 0;
        return l > 0f;
    }

    // The share of the body's weight that would vote for it today.
    public static float Support(Proposal p, bool presentOnly = false)
    {
        float yes = 0f, all = 0f;
        foreach (var v in p.Body.Voters())
        {
            if (presentOnly && !p.Present.Contains(v.Name)) continue;
            all += v.Weight;
            if (VotesFor(p, v)) yes += v.Weight;
        }
        return all <= 0f ? 0f : yes / all;
    }

    // One person talked to by another: for (+1) or against (-1). Each visit
    // tells less than the last; a suspicious man is harder to move.
    public static float Persuade(Proposal p, Agent speaker, Voter v, int side, float strength = 0.25f)
    {
        if (speaker == null || v.Name == speaker.DisplayName) return 0f;
        string key = v.Name + (side > 0 ? "+" : "-") + speaker.DisplayName;
        int visits = p.Visits.GetValueOrDefault(key);
        p.Visits[key] = visits + 1;
        float give = Persuasion(speaker) * strength / (1f + visits) * (1f - (v.Npc?.Personality.Suspicion ?? 0.5f) * 0.5f);
        if (v.Npc != null) give *= 1f + Mathf.Clamp(v.Npc.RapportWith(speaker), -50f, 100f) / 100f;
        float now = p.Swayed.GetValueOrDefault(v.Name);
        p.Swayed[v.Name] = Mathf.Clamp(now + side * give, -0.9f, 0.9f);
        return give;
    }

    // ---- opening a motion -------------------------------------------------------

    public static Proposal OpenFor(Body b) => Open.Find(p => p.Body == b && p.Stage != MotionStage.Decided);

    public static Proposal Propose(Body b, LawKind k, Motion m, float value, Agent sponsor, bool quiet = false)
    {
        if (b == null || OpenFor(b) != null) return null;
        if (m == Motion.Repeal && b.Get(k) == null) return null;
        var p = new Proposal { Id = _nextId++, Body = b, Kind = k, Motion = m, Value = value, Sponsor = sponsor?.DisplayName ?? "", Opened = Today };
        // A champion against it: whoever stands to lose most and can talk.
        Npc best = null; float worst = -0.25f;
        foreach (var v in b.Voters())
        {
            if (v.Npc == null || v.Name == p.Sponsor || Persuasion(v.Npc) < 0.45f) continue;
            float l = Lean(p, v);
            if (l < worst) { worst = l; best = v.Npc; }
        }
        p.Opponent = best?.DisplayName ?? "";
        Open.Add(p);
        _lastTried[b.Key + "|" + (int)k] = Today;
        string line = $"{p.Sponsor} is going round {Where(b)} for a motion {p.Title}{(p.Opponent != "" ? $"; {p.Opponent} is against it" : "")}.";
        Minute(b, line);
        if (!quiet && sponsor is Npc sn) Chronicle.Event(line, sn.GlobalPosition, sn, weight: 0.6f);
        if (!quiet) Tell(b, line);
        return p;
    }

    private static string Where(Body b) => b.Kind switch { BodyKind.Town => b.Town.Name, BodyKind.Band => "the camp", _ => "London" };

    // Somebody with an interest, the tongue and the ambition gets it into his
    // head to change a law. Rare: a body sees a motion a year or two, most
    // of which fail.
    private static void Agitators(Body b)
    {
        if (!b.Sits || OpenFor(b) != null) return;
        var voters = b.Voters();
        if (voters.Count < 3) return;
        foreach (var n in Candidates(b))
        {
            if (n.Personality.Ambition < 0.55f || n.Personality.Talkative < 0.45f) continue;
            if (Hash(n.DisplayName + Today) % 10000 > 2 + (int)(n.Personality.Ambition * 5)) continue;
            // The change he most wants.
            (LawKind k, Motion m, float v, float want) bestM = (LawKind.KeepBread, Motion.Enact, 0f, 0.45f);
            foreach (var k in Competent(b))
            {
                if (_lastTried.TryGetValue(b.Key + "|" + (int)k, out int last) && Today - last < 120) continue;
                var cur = b.Get(k);
                var tries = new List<(Motion, float)>();
                if (cur != null) { tries.Add((Motion.Repeal, 0f)); if (k != LawKind.NoSpirits) { tries.Add((Motion.Enact, cur.Value * 0.5f)); tries.Add((Motion.Enact, Mathf.Min(1f, cur.Value * 1.5f))); } }
                else tries.Add((Motion.Enact, DefaultValue(k)));
                foreach (var (m, v) in tries)
                {
                    var probe = new Proposal { Body = b, Kind = k, Motion = m, Value = v };
                    float want = Stance(n, b, k) * Tightening(probe);
                    if (want > bestM.want) bestM = (k, m, v, want);
                }
            }
            if (bestM.want <= 0.45f) continue;
            Propose(b, bestM.k, bestM.m, bestM.v, n);
            return;
        }
    }

    // Who might take it up: its own people, and anyone visiting.
    private static IEnumerable<Npc> Candidates(Body b)
    {
        foreach (var n in Sim.Npcs)
        {
            if (n.IsDead || n.IsFemale || n.Age < 21 || n.OffMap != "" || !GodotObject.IsInstanceValid(n) || n.Role is Role.Soldier or Role.Child) continue;
            if (b.Kind == BodyKind.Town)
            {
                bool own = n.HomeStores is TownHousehold th && th.Town == b.Town && th.Key != "garrison";
                if (own || !n.IsBandPerson && n.GlobalPosition.DistanceSquaredTo(b.Town.Centre) < 1800f * 1800f) yield return n;
            }
            else if (n.HomeStores == b.Band) yield return n;
        }
    }

    // ---- the calendar of it -----------------------------------------------------

    public static void Tick(GameCalendar cal)
    {
        if (!Net.Net.IsServer || cal == null) return;
        Ensure();
        int today = Today;
        if (today != _lastDay)
        {
            _lastDay = today;
            foreach (var b in Bodies) Agitators(b);
            foreach (var p in Open.ToArray()) DayOf(p);
            if (cal.Day == 1) Relief();
            Open.RemoveAll(p => p.Stage == MotionStage.Decided && today - p.MeetingDay > 30);
        }
        if (Mathf.Abs(cal.Hour - _lastHour) < 0.05f) return;
        _lastHour = cal.Hour;
        foreach (var p in Open.ToArray()) HourOf(p, cal.Hour);
    }

    // A day of a motion's life: canvassing out of sight, and the sponsor
    // deciding whether to call the meeting.
    private static void DayOf(Proposal p)
    {
        if (p.Stage != MotionStage.Canvassing) return;
        var voters = p.Body.Voters();
        // Out of anyone's sight the canvass goes on as a sum: a few houses a day.
        foreach (var (who, side) in new[] { (p.Sponsor, 1), (p.Opponent, -1) })
        {
            var sp = Speaker(who);
            if (sp == null || sp is Npc { Tier: Tier.Live }) continue;
            for (int i = 0; i < 3 && voters.Count > 0; i++) Persuade(p, sp, voters[Hash(who + Today + i) % voters.Count], side);
        }
        if (!p.Body.Sits) return;
        var sponsor = Speaker(p.Sponsor);
        if (sponsor is Player.PlayerController) return;   // the player calls his own meeting (UI/ConversationLaws)
        float support = Support(p);
        int days = Today - p.Opened;
        if (Trace) GD.Print($"[laws] {Cal?.DateString}: {p.Title} - day {days}, {support:P0} would vote for it, {p.Swayed.Count} spoken to");
        float patience = 10f + ((sponsor as Npc)?.Personality.Ambition ?? 0.5f) * 30f;
        if (sponsor == null || days > 60 && support < 0.3f)
        {
            p.Stage = MotionStage.Decided; p.MeetingDay = Today; p.Result = "dropped";
            Minute(p.Body, $"The motion {p.Title} was let drop{(sponsor == null ? $", {p.Sponsor} being gone" : "")}.");
            return;
        }
        if (days >= 5 && (support >= p.Body.Majority + 0.08f || days >= patience)) Call(p, 3);
    }

    // The meeting called for some days hence, at two in the afternoon (not a Sunday).
    public static void Call(Proposal p, int inDays)
    {
        if (p.Stage != MotionStage.Canvassing) return;
        p.Stage = MotionStage.Called;
        p.MeetingDay = Today + inDays;
        if (Cal != null && DateOf(p.MeetingDay).DayOfWeek == DayOfWeek.Sunday) p.MeetingDay++;
        string line = $"{p.Sponsor} has called {(p.Body.Kind == BodyKind.Town ? $"a town meeting at {p.Body.Town.Name}" : "the council")} for {When(p)}, on the motion {p.Title}.";
        Minute(p.Body, line);
        Tell(p.Body, line);
        if (Speaker(p.Sponsor) is Npc sn) { sn.Bark($"Meeting {When(p)}. Come and be heard."); Chronicle.Event(line, sn.GlobalPosition, sn, weight: 0.5f); }
    }

    public static DateTime DateOf(int day)
    {
        int y = day / 366, doy = day % 366;
        try { return new DateTime(Mathf.Clamp(y, 1, 9999), 1, 1).AddDays(Mathf.Max(0, doy - 1)); } catch { return DateTime.MinValue; }
    }
    public static string When(Proposal p)
    {
        if (p.MeetingDay < 0) return "a day not yet set";
        var d = DateOf(p.MeetingDay);
        return p.MeetingDay == Today ? "two o'clock today" : $"{d.DayOfWeek} the {d.Day}{Th(d.Day)} of {d:MMMM}, at two";
    }
    private static string Th(int d) => d is 11 or 12 or 13 ? "th" : (d % 10) switch { 1 => "st", 2 => "nd", 3 => "rd", _ => "th" };

    // The meeting day, hour by hour: gathering from one o'clock, the debate
    // from half past two, the vote at four. The Committee simply decides.
    private static void HourOf(Proposal p, float hour)
    {
        if (p.Stage is MotionStage.Decided or MotionStage.Canvassing || Today < p.MeetingDay) return;
        if (!p.Body.Sits) { Vote(p, true); return; }
        bool late = Today > p.MeetingDay;   // a save loaded, or the clock run past it
        var hall = IsoGrid.ToWorld(p.Body.Hall());
        if (p.Stage == MotionStage.Called && !p.Summoned && hour >= p.MeetingHour - 2.5f && !late) Summon(p);
        if (p.Stage == MotionStage.Called && (hour >= p.MeetingHour + 0.5f || late))
        {
            // Who came: those standing in the hall, and of those out of sight
            // the more interested (and the sponsor's friends) most likely.
            foreach (var v in p.Body.Voters())
            {
                if (v.Npc == null) continue;
                if (p.Coming.Contains(v.Name) || v.Npc.GlobalPosition.DistanceSquaredTo(hall) < 500f * 500f) p.Present.Add(v.Name);
                else if (v.Npc.Tier == Tier.Background && Hash(v.Name + p.Id) % 100 < 55 + (int)(Mathf.Abs(Lean(p, v)) * 40f)) p.Present.Add(v.Name);
            }
            p.Stage = MotionStage.Debating;
            // Who rises: the sponsor, the champion against, and whoever of
            // those present feels it most and can say so, turn about.
            var fors = new List<(string, float)>(); var againsts = new List<(string, float)>();
            foreach (var v in p.Body.Voters())
            {
                if (v.Npc == null || !p.Present.Contains(v.Name) || v.Name == p.Sponsor || v.Name == p.Opponent) continue;
                float l = Lean(p, v);
                if (Mathf.Abs(l) > 0.35f && Persuasion(v.Npc) > 0.5f) (l > 0 ? fors : againsts).Add((v.Name, Persuasion(v.Npc) * Mathf.Abs(l)));
            }
            fors.Sort((a, b) => b.Item2.CompareTo(a.Item2)); againsts.Sort((a, b) => b.Item2.CompareTo(a.Item2));
            var order = new List<(string, int)>();
            if (Speaker(p.Sponsor) != null) order.Add((p.Sponsor, 1));
            if (p.Opponent != "" && Speaker(p.Opponent) != null) order.Add((p.Opponent, -1));
            for (int i = 0; i < 2; i++) { if (i < fors.Count) order.Add((fors[i].Item1, 1)); if (i < againsts.Count) order.Add((againsts[i].Item1, -1)); }
            // The player, if he is there and has a side.
            if (p.PlayerSign != 0 && Sim.Player is Agent me && me.GlobalPosition.DistanceSquaredTo(hall) < 600f * 600f) order.Insert(Mathf.Min(2, order.Count), (me.DisplayName, p.PlayerSign));
            foreach (var s in order) p.Speakers.Enqueue(s);
            p.NextSpeech = hour;
            Minute(p.Body, $"The {p.Body.Meeting} sat on the motion {p.Title}, {p.Present.Count} of {p.Body.Voters().Count} present.");
        }
        if (p.Stage == MotionStage.Debating)
        {
            if (p.Speakers.Count > 0 && (hour >= p.NextSpeech || late))
            {
                var (who, side) = p.Speakers.Dequeue();
                p.NextSpeech = hour + 0.2f;
                Speak(p, who, side);
            }
            else if (p.Speakers.Count == 0 && (hour >= p.MeetingHour + 2f || late)) Vote(p, false);
        }
    }

    // The bell, two and a half hours before: whoever means to go puts down
    // his work and walks over. The more he cares, one way or the other, the
    // likelier he goes; the sponsor and the champion always do.
    private static void Summon(Proposal p)
    {
        p.Summoned = true;
        var names = new List<(Npc, bool)>();
        foreach (var v in p.Body.Voters()) if (v.Npc != null) names.Add((v.Npc, Hash(v.Name + p.Id + "go") % 100 < 45 + (int)(Mathf.Abs(Lean(p, v)) * 55f)));
        foreach (var who in new[] { p.Sponsor, p.Opponent }) if (Speaker(who) is Npc c && !names.Exists(x => x.Item1 == c)) names.Add((c, true));
        foreach (var (n, goes) in names)
        {
            if (!goes || n.IsDead || !GodotObject.IsInstanceValid(n) || n.Tier != Tier.Live || n.HeldBy != null || n.Following != null || n.Away || n.Brain.HasTier0()) continue;
            if (n.Brain.Top is ChoreGoal { Label: var l } && l.Contains(p.Body.Meeting)) continue;
            n.Brain.Clear(n);
            n.Brain.Push(n, MeetingGoal(n, p));
        }
    }
    private static Goal MeetingGoal(Npc npc, Proposal p)
    {
        p.Coming.Add(npc.DisplayName);
        var spot = Spot(npc, p.Body.Hall());
        return new ChoreGoal($"to the {p.Body.Meeting}", Walk($"on the way to the {p.Body.Meeting}", spot, 90f), Work($"at the {p.Body.Meeting}", Mathf.Max(0.5f, p.MeetingHour + 2.2f - (Cal?.Hour ?? 12f))));
    }

    private static void Speak(Proposal p, string who, int side)
    {
        var sp = Speaker(who);
        if (sp == null) return;
        foreach (var v in p.Body.Voters())
        {
            if (!p.Present.Contains(v.Name) || v.Name == who) continue;
            float l = Lean(p, v);
            Persuade(p, sp, v, side, 0.18f * (1f - Mathf.Abs(l) * 0.6f));
        }
        string line = Speech(p, side, sp);
        if (sp is Npc n) n.Bark(line);
        else if (sp is Player.PlayerController pc) pc.Say($"You rise and speak {(side > 0 ? "for" : "against")} the motion.");
        Chronicle.Event($"At the {p.Body.Meeting}, {who} spoke {(side > 0 ? "for" : "against")} the motion {p.Title}.", IsoGrid.ToWorld(p.Body.Hall()), sp, weight: 0.5f);
    }

    private static string Speech(Proposal p, int side, Agent who)
    {
        bool tighter = Tightening(p) > 0f;
        bool forTighter = side > 0 == tighter;
        int pick = Hash(who.DisplayName + p.Id) % 2;
        return (p.Kind, forTighter) switch
        {
            (LawKind.KeepBread, false) => pick == 0 ? "The grain is ours. Why should a man sell at the town's price when Montreal pays better?" : "Ship it and the money comes back to every farm. A full till is a full town.",
            (LawKind.KeepBread, true) => pick == 0 ? "Sell the winter's bread down the lakes and what do we eat in March? Money?" : "Every one of you remembers a hungry spring. Keep the bread.",
            (LawKind.ExportDuty or LawKind.ImportDuty, true) => "A little on every barrel, and no family in this town goes on the parish.",
            (LawKind.ExportDuty or LawKind.ImportDuty, false) => "A tax on honest trade. It's the merchant pays it today and you tomorrow.",
            (LawKind.NoSpirits, true) => p.Body.Kind == BodyKind.Band ? "Look at the young men after the traders come. The rum makes us poor and foolish." : "Drink has ruined better towns than this one.",
            _ => p.Body.Kind == BodyKind.Band ? "A man may take what he likes for his furs. Who tells a hunter what to want?" : "A man may have his dram. This is a free country.",
        };
    }

    private static void Vote(Proposal p, bool absent)
    {
        float yes = 0f, no = 0f, all = 0f;
        foreach (var v in p.Body.Voters())
        {
            all += v.Weight;
            if (!absent && !p.Present.Contains(v.Name)) continue;
            if (VotesFor(p, v)) yes += v.Weight; else no += v.Weight;
        }
        p.Stage = MotionStage.Decided;
        var b = p.Body;
        string tally = b.Kind == BodyKind.Band ? "" : $" ({yes:0} to {no:0})";
        if (all <= 0f || (yes + no) < all * 0.3f) { p.Result = "no quorum"; Announce(p, $"The {b.Meeting} at {Where(b)} could not decide the motion {p.Title}: too few came."); return; }
        bool carried = yes / (yes + no) > b.Majority;
        p.Result = carried ? "carried" : "lost";
        if (carried)
        {
            b.Laws.RemoveAll(l => l.Kind == p.Kind);
            if (p.Motion == Motion.Enact) b.Laws.Add(new Law { Kind = p.Kind, Value = p.Value, Since = Cal?.Year ?? 1811, By = $"moved by {p.Sponsor}" });
        }
        string verb = b.Kind == BodyKind.Band ? (carried ? "agreed" : "would not agree") : carried ? "carried" : "threw out";
        Announce(p, $"The {b.Meeting} {(b.Kind == BodyKind.Town ? "of " + b.Town.Name + " " : "")}{verb} the motion {p.Title}{tally}.");
        // The sponsor's standing rises or falls with it.
        if (Speaker(p.Sponsor) is Npc sn) sn.Standing = Mathf.Clamp(sn.Standing + (carried ? 6f : -3f), 0f, 100f);
        if (Speaker(p.Opponent) is Npc on) on.Standing = Mathf.Clamp(on.Standing + (carried ? -3f : 4f), 0f, 100f);
    }

    private static void Announce(Proposal p, string line)
    {
        Minute(p.Body, line);
        Tell(p.Body, line, true);
        if (p.Body.Sits) Chronicle.Event(line, IsoGrid.ToWorld(p.Body.Hall()), Speaker(p.Sponsor), weight: 1f);
    }

    // The first of the month: the duties in the chest go to the houses with
    // nothing in their box.
    private static void Relief()
    {
        foreach (var b in Bodies)
        {
            if (b.Kind != BodyKind.Town || b.Chest <= 0) continue;
            var poor = new List<TownHousehold>();
            foreach (var h in b.Town.Houses.Values) if (h != b.Town.Market && h.Key != "garrison" && h.Alive() > 0 && h.Stores.Count(ItemId.Coin) < 24) poor.Add(h);
            if (poor.Count == 0) continue;
            int each = b.Chest / poor.Count;
            if (each <= 0) continue;
            foreach (var h in poor) h.Stores.Add(ItemId.Coin, each);
            b.Chest -= each * poor.Count;
            Minute(b, $"{Money.Format(each * poor.Count, b.Town)} of the duties given out to {poor.Count} poor houses.");
        }
    }

    // ---- what people do about it -----------------------------------------------

    // A sponsor's or champion's day while a motion is canvassed (RoleBehaviour,
    // in working hours): a call at a voter's house; and on the meeting day,
    // every voter's walk to the hall.
    private static readonly Dictionary<Npc, int> _calls = new();
    public static Goal Errand(Npc npc)
    {
        if (Open.Count == 0 || Cal == null) return null;
        foreach (var p in Open)
        {
            if (!p.Body.Sits) continue;
            bool champion = npc.DisplayName == p.Sponsor || npc.DisplayName == p.Opponent;
            if (!champion && !Belongs(npc, p.Body)) continue;
            if (p.Stage == MotionStage.Called && Today == p.MeetingDay && Cal.Hour >= p.MeetingHour - 3f && Cal.Hour < p.MeetingHour + 0.5f)
            {
                bool voter = npc.DisplayName == p.Sponsor || npc.DisplayName == p.Opponent || p.Body.Voters().Exists(v => v.Npc == npc);
                if (!voter) continue;
                if (!p.Summoned || p.Coming.Contains(npc.DisplayName)) return MeetingGoal(npc, p);
            }
            if (p.Stage == MotionStage.Debating && Today == p.MeetingDay && (npc.DisplayName == p.Sponsor || p.Present.Contains(npc.DisplayName)))
                return new ChoreGoal($"at the {p.Body.Meeting}", Work($"hearing the debate at the {p.Body.Meeting}", 0.5f));
            if (p.Stage != MotionStage.Canvassing || Cal.Hour < 9f || Cal.Hour >= 17f) continue;
            int side = npc.DisplayName == p.Sponsor ? 1 : npc.DisplayName == p.Opponent ? -1 : 0;
            if (side == 0) continue;
            if (_calls.GetValueOrDefault(npc) >= Today * 4 + 3) continue;   // three calls a day
            var voters = p.Body.Voters();
            // The house he has been to least, the waverers first.
            Voter best = null; float score = float.MaxValue;
            foreach (var v in voters)
            {
                if (v.Npc == null || v.Npc == npc) continue;
                float s = p.Visits.GetValueOrDefault(v.Name + (side > 0 ? "+" : "-") + npc.DisplayName) * 2f + Mathf.Abs(Lean(p, v));
                if (s < score) { score = s; best = v; }
            }
            if (best == null) continue;
            _calls[npc] = Mathf.Max(_calls.GetValueOrDefault(npc), Today * 4) + 1;
            var target = best;
            var at = target.Npc.HomeStores?.AccessCell ?? IsoGrid.FromWorld(target.Npc.GlobalPosition);
            return new ChoreGoal($"canvassing {target.Name}",
                Walk($"going to talk to {target.Name} about the motion", at, 120f),
                Do(n =>
                {
                    // Someone out of sight is taken to be at home; someone in sight has to be found there.
                    bool met = GodotObject.IsInstanceValid(target.Npc) && (target.Npc.Tier == Tier.Background || target.Npc.GlobalPosition.DistanceSquaredTo(n.GlobalPosition) < 260f * 260f);
                    Persuade(p, n, target, side, met ? 0.25f : 0.1f);
                    n.Bark(met ? Canvass(p, side, target.Npc) : $"Tell {Barks.CallName(target.Npc)} I called about the motion.");
                    if (met) { n.ChangeRapport(target.Npc, 1f); target.Npc.ChangeRapport(n, 0.5f); }
                }),
                Work("making his case", 0.4f));
        }
        return null;
    }

    private static bool Belongs(Npc npc, Body b) => b.Kind == BodyKind.Town ? npc.HomeStores is TownHousehold th && th.Town == b.Town : npc.HomeStores == b.Band;

    private static string Canvass(Proposal p, int side, Npc to) =>
        $"{Barks.CallName(to)}, {(side > 0 ? "vote for" : "vote against")} {Short(p.Kind)} at the meeting. {(Tightening(p) > 0 == side > 0 ? "Think of the winter." : "Think what you could get for it.")}";

    private static Vector2I Spot(Npc npc, Vector2I door)
    {
        int h = Hash(npc.DisplayName);
        for (int k = 0; k < 6; k++)
        {
            var c = new Vector2I(door.X + (h >> (k * 3)) % 7 - 3, door.Y + (h >> (k * 3 + 1)) % 7 - 3);
            if (Sim.Paths?.IsWalkable(c) ?? false) return c;
        }
        return door;
    }

    // ---- small things -----------------------------------------------------------

    public static Agent Speaker(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        foreach (var pc in Sim.Players()) if (pc != null && GodotObject.IsInstanceValid(pc) && pc.DisplayName == name) return pc;
        var n = Sim.Npcs.Find(x => x.DisplayName == name);
        return n != null && GodotObject.IsInstanceValid(n) && !n.IsDead ? n : null;
    }

    private static int Hash(string s) { unchecked { uint h = 2166136261; foreach (char c in s) h = (h ^ c) * 16777619; return (int)(h >> 1); } }

    // The minutes of every body, each line "key<tab>date: what was done".
    private static void Minute(Body b, string line)
    {
        Minutes.Add($"{b.Key}	{Cal?.DateString ?? ""}: {line}");
        if (Minutes.Count > 200) Minutes.RemoveAt(0);
        GD.Print($"[laws] {line}");
    }

    public static List<string> MinutesOf(Body b, int last)
    {
        var list = new List<string>();
        for (int i = Minutes.Count - 1; i >= 0 && list.Count < last; i--)
            if (Minutes[i].StartsWith(b.Key + "	")) list.Insert(0, Minutes[i][(b.Key.Length + 1)..]);
        return list;
    }

    // Said aloud to whoever is near enough to hear of it.
    private static void Tell(Body b, string line, bool headline = false)
    {
        Vector2 at = b.Kind == BodyKind.Town ? b.Town.Centre : b.Band != null ? IsoGrid.ToWorld(b.Band.Camp) : Vector2.Zero;
        foreach (var pc in Sim.Players())
            if (pc != null && GodotObject.IsInstanceValid(pc) && (b.Kind == BodyKind.Company ? pc.Faction == Faction.HBC : pc.GlobalPosition.DistanceSquaredTo(at) < 4000f * 4000f))
            {
                if (headline) Sim.Headline(line); else Sim.Notice(line);
                return;
            }
    }

    // Dev: `-- --motion=Town:KeepBread:repeal` puts the motion now, moved by
    // the town's best talker (or `:enact` for a new law); `:repeal:silver`
    // gives him a silver tongue - the stranger who can talk a town into anything.
    private static bool _devDone;
    private static readonly bool Trace = Array.IndexOf(OS.GetCmdlineUserArgs(), "--lawtrace") >= 0;
    public static void DevMotion()
    {
        if (_devDone) return;
        _devDone = true;
        foreach (var a in OS.GetCmdlineUserArgs())
        {
            if (!a.StartsWith("--motion=")) continue;
            var parts = a[9..].Split(':');
            if (parts.Length < 2 || !Enum.TryParse<LawKind>(parts[1], true, out var k)) continue;
            Ensure();
            var b = Bodies.Find(x => x.Key.EndsWith(":" + parts[0]) || x.Name.EndsWith(parts[0]));
            if (b == null) { GD.Print($"[laws] no body called {parts[0]}"); continue; }
            var m = parts.Length > 2 && parts[2] == "repeal" ? Motion.Repeal : Motion.Enact;
            Npc best = null;
            foreach (var n in Candidates(b)) if (best == null || Persuasion(n) > Persuasion(best)) best = n;
            if (best != null && parts.Length > 3 && parts[3] == "silver") { best.Personality.Talkative = 1f; best.Personality.Ambition = 1f; best.Skills.Trade = 1f; best.Standing = 80f; }
            Propose(b, k, m, DefaultValue(k), best);
        }
    }

    // ---- saves ------------------------------------------------------------------

    public static void Clear() { Bodies.Clear(); Open.Clear(); Minutes.Clear(); _lastTried.Clear(); _calls.Clear(); _seeded = false; _lastDay = -1; _lastHour = -1f; _nextId = 1; }

    public static Godot.Collections.Dictionary Save()
    {
        Ensure();
        var bodies = new Godot.Collections.Array();
        foreach (var b in Bodies)
        {
            var laws = new Godot.Collections.Array();
            foreach (var l in b.Laws) laws.Add(new Godot.Collections.Array { (int)l.Kind, l.Value, l.Since, l.By });
            bodies.Add(new Godot.Collections.Dictionary { ["key"] = b.Key, ["chest"] = b.Chest, ["laws"] = laws });
        }
        var open = new Godot.Collections.Array();
        foreach (var p in Open)
        {
            var sw = new Godot.Collections.Dictionary(); foreach (var (k, v) in p.Swayed) sw[k] = v;
            var vi = new Godot.Collections.Dictionary(); foreach (var (k, v) in p.Visits) vi[k] = v;
            var pr = new Godot.Collections.Array(); foreach (var s in p.Present) pr.Add(s);
            open.Add(new Godot.Collections.Dictionary
            {
                ["id"] = p.Id, ["body"] = p.Body.Key, ["kind"] = (int)p.Kind, ["motion"] = (int)p.Motion, ["value"] = p.Value,
                ["sponsor"] = p.Sponsor, ["opponent"] = p.Opponent, ["opened"] = p.Opened, ["day"] = p.MeetingDay,
                ["stage"] = (int)(p.Stage == MotionStage.Debating ? MotionStage.Called : p.Stage), ["result"] = p.Result, ["swayed"] = sw, ["visits"] = vi, ["present"] = pr, ["player"] = p.PlayerSign,
            });
        }
        var tried = new Godot.Collections.Dictionary(); foreach (var (k, v) in _lastTried) tried[k] = v;
        var minutes = new Godot.Collections.Array(); foreach (var m in Minutes) minutes.Add(m);
        return new Godot.Collections.Dictionary { ["bodies"] = bodies, ["open"] = open, ["tried"] = tried, ["minutes"] = minutes, ["next"] = _nextId };
    }

    public static void Restore(Godot.Collections.Dictionary d)
    {
        Clear();
        Ensure();
        if (d.Count == 0) return;
        foreach (var bv in d["bodies"].AsGodotArray())
        {
            var bd = bv.AsGodotDictionary();
            var b = Bodies.Find(x => x.Key == bd["key"].AsString());
            if (b == null) continue;
            b.Chest = bd["chest"].AsInt32();
            b.Laws.Clear();
            foreach (var lv in bd["laws"].AsGodotArray()) { var a = lv.AsGodotArray(); b.Laws.Add(new Law { Kind = (LawKind)a[0].AsInt32(), Value = a[1].AsSingle(), Since = a[2].AsInt32(), By = a[3].AsString() }); }
        }
        foreach (var pv in d["open"].AsGodotArray())
        {
            var pd = pv.AsGodotDictionary();
            var b = Bodies.Find(x => x.Key == pd["body"].AsString());
            if (b == null) continue;
            var p = new Proposal
            {
                Id = pd["id"].AsInt32(), Body = b, Kind = (LawKind)pd["kind"].AsInt32(), Motion = (Motion)pd["motion"].AsInt32(), Value = pd["value"].AsSingle(),
                Sponsor = pd["sponsor"].AsString(), Opponent = pd["opponent"].AsString(), Opened = pd["opened"].AsInt32(), MeetingDay = pd["day"].AsInt32(),
                Stage = (MotionStage)pd["stage"].AsInt32(), Result = pd["result"].AsString(), PlayerSign = pd["player"].AsInt32(),
            };
            foreach (var (k, v) in pd["swayed"].AsGodotDictionary()) p.Swayed[k.AsString()] = v.AsSingle();
            foreach (var (k, v) in pd["visits"].AsGodotDictionary()) p.Visits[k.AsString()] = v.AsInt32();
            foreach (var s in pd["present"].AsGodotArray()) p.Present.Add(s.AsString());
            Open.Add(p);
        }
        foreach (var (k, v) in d["tried"].AsGodotDictionary()) _lastTried[k.AsString()] = v.AsInt32();
        foreach (var m in d["minutes"].AsGodotArray()) Minutes.Add(m.AsString());
        _nextId = d["next"].AsInt32();
    }
}
