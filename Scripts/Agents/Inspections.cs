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

// Surprise inspections (design doc 3.9): a suspicious superior need not wait
// a year for London to read the books. He can send a man to turn out a
// subordinate post's ledger, or walk down the men's house with a lantern
// and have every chest opened.
//
// A post's master who is disloyal and ambitious keeps back a tenth of the
// furs that come over his counter (Skim). The superior's man, arriving after
// the days on the road, finds the books short - or does not - and a master
// caught is docked, and put out of his charge if it was gross.
//
// A player's kit holds the furs he traded on his own account (PrivateFurs):
// at an inspection he turns out his chest and loses them and his standing,
// slips them to a friend, talks his way out, or refuses outright. Being
// reported for private dealing (YorkHappenings) makes it likelier.
//
// Server only; ticked from Population. Dev: `--inspect` brings an
// inspection of your kit this morning and of each subordinate post
// tomorrow (`--inspect=N` puts N skins of private trade in your pack first);
// `--inspecttrace`.
public static class Inspections
{
    private sealed class Visit { public FortLedger Post; public Npc Superior; public string Inspector; public int Due; }
    private static readonly List<Visit> _onTheRoad = new();
    private static readonly Dictionary<FortLedger, int> _lastPost = new();
    private static readonly Dictionary<FortLedger, float> _found = new();          // skim already brought to light
    private static readonly Dictionary<PlayerController, float> _suspect = new();
    // A post someone has told the master about, and who told him.
    private static readonly Dictionary<FortLedger, PlayerController> _toldOf = new();
    private static readonly Dictionary<PlayerController, int> _lastKit = new();
    private static readonly HashSet<PlayerController> _owed = new();               // ordered while he was away
    private static int _lastDay = -1;

    private static readonly string[] Args = OS.GetCmdlineUserArgs();
    private static readonly bool Dev = System.Array.Exists(Args, a => a == "--inspect" || a.StartsWith("--inspect="));
    private static int DevSkins() { foreach (var a in Args) if (a.StartsWith("--inspect=") && int.TryParse(a[10..], out int n)) return n; return 0; }
    private static readonly bool Trace = Dev || System.Array.IndexOf(Args, "--inspecttrace") >= 0;
    private static bool _devDone;

    public static void Reset()
    {
        _onTheRoad.Clear(); _lastPost.Clear(); _found.Clear(); _suspect.Clear(); _toldOf.Clear(); _lastKit.Clear(); _owed.Clear(); _lastDay = -1; _devDone = false;
    }

    private static int Today(GameCalendar cal) => cal.Year * 366 + (int)cal.DayOfYear;

    // Someone told the master that this man deals on the side.
    public static void Suspect(PlayerController p, float by)
    {
        if (p == null) return;
        _suspect[p] = Mathf.Min(5f, _suspect.GetValueOrDefault(p) + by);
    }

    // ---- who answers to whom ----------------------------------------------------------------

    // The company's head house in the country: York for the Bay, Fort William for the Nor'Westers.
    private static FortLedger HeadOf(Faction f) => f switch
    {
        Faction.HBC => Sim.Fort,
        Faction.NWC => Convoys.Ledger("Fort William") ?? Sim.Nwc,
        _ => null,
    };

    private static Npc SuperiorOf(FortLedger post)
    {
        var head = HeadOf(post.Company);
        if (head == null || head == post) return null;
        return head.Leader is { IsDead: false } l ? l : null;
    }

    // ---- a word in the master's ear -------------------------------------------------------------

    // The subordinate posts a player could tell his master about.
    public static List<FortLedger> UnderHim(PlayerController p)
    {
        var list = new List<FortLedger>();
        var boss = p.HomePost?.Leader;
        if (boss == null) return list;
        foreach (var post in Sim.Posts())
            if (post != p.HomePost && post.Leader != null && SuperiorOf(post) == boss && !_toldOf.ContainsKey(post)) list.Add(post);
        return list;
    }

    // He tells his master he thinks this post's books are short: an inspection is likelier soon.
    public static void Report(PlayerController p, FortLedger post) => _toldOf[post] = p;

    // ---- the master's own share ---------------------------------------------------------------

    // Furs have come over a post's counter (StandardOfTrade.Trade): a master
    // who cares little for his masters keeps a tenth back for himself.
    public static void Skim(FortLedger post, float takenMb)
    {
        var m = post?.Leader;
        if (m == null || m.IsDead || takenMb < 1f || SuperiorOf(post) == null) return;
        if (m.Personality.Loyalty > 0.35f || m.Personality.Ambition < 0.5f) return;
        float want = takenMb * 0.1f, got = 0f;
        foreach (var (id, n) in new List<(ItemId, int)>(post.Stores.All()))
        {
            var d = Items.Items.Of(id);
            if (d.Kind != ItemKind.Fur || d.ValueMb <= 0f) continue;
            int take = Mathf.Min(n, Mathf.CeilToInt((want - got) / d.ValueMb));
            if (take <= 0) continue;
            post.Stores.Remove(id, take);
            got += take * d.ValueMb;
            if (got >= want) break;
        }
        post.SkimmedMb += got;
        if (Trace && got > 0f) GD.Print($"[inspect] {m.DisplayName} keeps back {got:0.0} MB at {Accords.Name(post)} (all told {post.SkimmedMb:0.0})");
    }

    // ---- the day ---------------------------------------------------------------------------------

    public static void Tick()
    {
        if (!NetHub.IsServer) return;
        var cal = GameCalendar.Instance;
        if (cal == null || cal.Hour < 9f) return;
        int today = Today(cal);
        if (today != _lastDay)
        {
            _lastDay = today;
            Morning(today);
        }
        // A kit inspection ordered while he was away waits for him to come home.
        foreach (var p in new List<PlayerController>(_owed))
            if (Free(p)) { _owed.Remove(p); OpenKit(p); }
    }

    private static void Morning(int today)
    {
        bool dev = Dev && !_devDone;
        _devDone = true;
        if (dev && DevSkins() > 0) foreach (var p in Sim.Players()) { p.PrivateFurs += DevSkins(); p.Inventory.Add(ItemId.BeaverPelt, DevSkins()); }
        // Dev: `--skimmed=MB` has every subordinate post's master already keeping that much back.
        if (dev) foreach (var a in Args) if (a.StartsWith("--skimmed=") && float.TryParse(a[10..], out float sk)) foreach (var fp in Sim.Posts()) if (SuperiorOf(fp) != null) fp.SkimmedMb += sk;
        foreach (var post in new List<FortLedger>(Sim.Posts()))
        {
            var sup = SuperiorOf(post);
            if (sup == null || post.Leader == null || post.Leader.IsDead) continue;
            if (_lastPost.TryGetValue(post, out int last) && today - last < 120 && !dev) continue;
            if (_onTheRoad.Exists(v => v.Post == post)) continue;
            float chance = 0.002f + sup.Personality.Suspicion * 0.006f + (_toldOf.ContainsKey(post) ? 0.08f : 0f);
            if (!dev && GD.Randf() > chance) continue;
            _lastPost[post] = today;
            var head = HeadOf(post.Company);
            float px = IsoGrid.ToWorld(head.AccessCell).DistanceTo(IsoGrid.ToWorld(post.AccessCell));
            int days = Mathf.Max(1, Mathf.RoundToInt(px / 4000f));
            string who = ClerkOf(head)?.DisplayName ?? sup.DisplayName;
            _onTheRoad.Add(new Visit { Post = post, Superior = sup, Inspector = who, Due = today + (dev ? 1 : days) });
            if (Trace) GD.Print($"[inspect] {sup.DisplayName} {(who == sup.DisplayName ? "goes himself" : $"sends {who}")} to look over the books at {Accords.Name(post)} ({days} days on the road)");
        }
        foreach (var v in new List<Visit>(_onTheRoad))
            if (today >= v.Due) { _onTheRoad.Remove(v); InspectPost(v); }

        foreach (var p in Sim.Players())
        {
            if (p.IsDead || p.HomePost?.Leader is not { IsDead: false } boss) continue;
            if (_owed.Contains(p) || _lastKit.TryGetValue(p, out int lk) && today - lk < 60 && !dev) continue;
            float chance = 0.003f + boss.Personality.Suspicion * 0.01f + _suspect.GetValueOrDefault(p) * 0.05f;
            if (!dev && GD.Randf() > chance) continue;
            _lastKit[p] = today;
            if (Trace) GD.Print($"[inspect] {boss.DisplayName} orders the men's chests turned out (for {p.DisplayName})");
            if (Free(p)) OpenKit(p); else _owed.Add(p);
        }
    }

    private static Npc ClerkOf(FortLedger head)
    {
        foreach (var n in head.Members)
            if (GodotObject.IsInstanceValid(n) && !n.IsDead && !n.Away && n.Role == Role.Clerk) return n;
        return null;
    }

    // ---- the post's books -----------------------------------------------------------------------

    private static void InspectPost(Visit v)
    {
        var post = v.Post; var master = post.Leader;
        if (master == null || master.IsDead) return;
        string place = Accords.Name(post), mn = master.DisplayName;
        float missing = post.SkimmedMb - _found.GetValueOrDefault(post);
        // A man on the spot with the ledger in one hand and the press in the other finds most of it.
        bool caught = missing >= 1f && GD.Randf() < Mathf.Clamp(0.55f + missing / 60f, 0.55f, 0.95f);
        if (!caught)
        {
            master.ChangeRapport(v.Superior, -3f);   // nobody likes being doubted
            if (Trace) GD.Print($"[inspect] {v.Inspector} finds the books at {place} in order{(missing >= 1f ? $" (missing {missing:0} MB, not found)" : "")}");
            if (_toldOf.Remove(post, out var wrong) && GodotObject.IsInstanceValid(wrong))
            {
                // The man who told tales was wrong, and the master learns who it was.
                wrong.Standing -= 3f;
                master.ChangeRapport(wrong, -20f);
                v.Superior.ChangeRapport(wrong, -5f);
                wrong.Journal.Add($"{GameCalendar.Instance?.DateString}: {v.Inspector} found the books at {place} in order. You were wrong about {mn}, and he knows it was you. (standing -3)");
            }
            return;
        }
        _found[post] = post.SkimmedMb;
        post.Stores.Add(ItemId.BeaverPelt, Mathf.RoundToInt(missing));   // what he had put by goes back on the Company's shelf
        master.Standing -= 20f;
        master.ChangeRapport(v.Superior, -15f);
        v.Superior.ChangeRapport(master, -30f);
        // Put out of his charge only where a master will be sent to take his place (Oxford House: Delegation.PostAClerk).
        bool gross = missing >= 30f && post is OutpostLedger;
        Chronicle.Event($"{v.Inspector} found {missing:0} made beaver of furs kept back by {mn} at {place}.", IsoGrid.ToWorld(post.AccessCell), master, mn, 2f);
        if (gross) PutOut(post, master, v.Superior);
        if (_toldOf.Remove(post, out var right) && GodotObject.IsInstanceValid(right))
        {
            right.Standing += 3f;
            v.Superior.ChangeRapport(right, 8f);
            master.ChangeRapport(right, -30f);
            right.Journal.Add($"{GameCalendar.Instance?.DateString}: {v.Inspector} found {mn} keeping furs back at {place}, as you said. (standing +3)");
        }
        Sim.NoticeAt(post, gross
            ? $"{v.Inspector} has found {mn} keeping back {missing:0} made beaver of furs at {place}. {mn} is put out of his charge and called in to answer for it."
            : $"{v.Inspector} has found {mn} keeping back {missing:0} made beaver of furs at {place}. A year's wages docked, and a hard letter.");
        if (Trace) GD.Print($"[inspect] caught: {mn} at {place}, {missing:0} MB{(gross ? ", put out" : "")}");
    }

    // Out of his charge: back to the head house a clerk again, with food for
    // the road, and the post without a master until York sends one.
    private static void PutOut(FortLedger post, Npc master, Npc superior)
    {
        var head = HeadOf(post.Company);
        post.Leader = null;
        if (Sim.Fort != null) Sim.Fort.KnowsOutpostMasterless = true;
        master.Role = Role.Clerk;
        master.DisplayName = (master.DisplayName.StartsWith("Mr. ") ? master.DisplayName[4..] : master.DisplayName).Replace(" (postmaster)", " (clerk)");
        master.Inventory.Add(ItemId.Pemmican, post.Stores.TakeUpTo(ItemId.Pemmican, 4));
        master.HomeStores = head;
        master.BedCell = head.AccessCell; master.WorkCell = head.AccessCell;
        master.ResetBehaviour(); master.Brain.Clear(master);
        master.Brain.Push(master, new RoadGoal(head.AccessCell));
    }

    // ---- the men's chests ------------------------------------------------------------------------

    private static bool Free(PlayerController p) =>
        p != null && GodotObject.IsInstanceValid(p) && !p.IsDead && !p.Conversation.IsOpen && p.HomePost != null
        && Convoys.BoardedBy(p) == null && p.CaptiveUntilDay < 0
        && p.GlobalPosition.DistanceTo(IsoGrid.ToWorld(p.HomePost.AccessCell)) < 1800f;

    private static int FursIn(Inventory inv)
    {
        int n = 0;
        foreach (var (id, c) in inv.All()) if (Items.Items.Of(id).Kind == ItemKind.Fur) n += c;
        return n;
    }

    private static int Confiscate(PlayerController p, FortLedger post, int count)
    {
        int took = 0;
        foreach (var (id, c) in new List<(ItemId, int)>(p.Inventory.All()))
        {
            if (took >= count || Items.Items.Of(id).Kind != ItemKind.Fur) continue;
            int t = Mathf.Min(c, count - took);
            p.Inventory.Remove(id, t); post.Stores.Add(id, t);
            took += t;
        }
        return took;
    }

    private static void OpenKit(PlayerController p)
    {
        var boss = p.HomePost?.Leader;
        if (boss == null) return;
        GD.Print($"[inspect] kit inspection page for {p.DisplayName}");
        NetHub.I?.PushDialogue(p, p.Conversation.StartScene(KitPage(p, p.HomePost, boss)));
    }

    private static ScenePage Page(string title, string text) => new() { Title = title, Text = text, CanLeave = false };
    private static ScenePage Done(string title, string text) => Page(title, text).Add("Go on", () => null);
    private static void Note(PlayerController p, string line) => p.Journal.Add($"{GameCalendar.Instance?.DateString}: {line}");

    private static ScenePage KitPage(PlayerController p, FortLedger post, Npc boss)
    {
        const string T = "The chests";
        string bn = boss.DisplayName, clerk = ClerkOf(post)?.DisplayName ?? "his clerk";
        int evidence = Mathf.Min(p.PrivateFurs, FursIn(p.Inventory));
        var page = Page(T, $"{bn} comes down the men's house with {clerk} and a lantern. Every man's chest is to be turned out on the floor, here and now, and the clerk has the book open to check what he finds against what each man drew from the store."
            + (evidence > 0 ? $" There are {evidence} skin{(evidence == 1 ? "" : "s")} in yours that never went over the Company's counter." : ""));

        void Caught(int n, float extra, string how)
        {
            int took = Confiscate(p, post, n);
            p.PrivateFurs = 0;
            float loss = 4f + took + extra;
            p.Standing -= loss;
            boss.ChangeRapport(p, -10f - took);
            if (took >= 8 && p.Rank != "labourer") p.Rank = "labourer";
            _suspect.Remove(p);
            Chronicle.Event($"{bn} found {took} skins traded on the side in {p.DisplayName}'s chest.", p.GlobalPosition, p, p.DisplayName, 1.5f);
            Note(p, $"{bn} found {took} skins of your own trading in your chest{how}. They went to the Company's store, with your wages for the year, and your name in the book. (standing -{loss:0})");
        }

        page.Add("Turn out your chest", () =>
        {
            if (evidence == 0)
            {
                boss.ChangeRapport(p, 4f);
                p.Standing += 1f;
                _suspect.Remove(p);
                Note(p, $"{bn} had the chests turned out. Yours held nothing it should not.");
                return Done(T, $"Shirts, a razor, a Testament you have not opened since you came out. {clerk} ticks you off in the book. {bn} nods: whatever he heard about you, he has his answer.");
            }
            Caught(evidence, 0f, "");
            return Done(T, $"The skins come out from under your shirts and {clerk} counts them aloud for the whole house to hear. {bn} says nothing at all, which is worse. They go to the store, and your year's wages go with them.");
        });
        if (evidence > 0)
        {
            Npc mate = null;
            foreach (var a in Sim.AgentsWithin(p.GlobalPosition, 900f))
                if (a is Npc n && !n.IsDead && n.HomeStores == post && n != boss && n.RapportWith(p) >= 30f && (mate == null || n.RapportWith(p) > mate.RapportWith(p))) mate = n;
            if (mate != null)
            {
                string mc = Barks.CallName(mate);
                page.Add($"Slip the skins to {mc} before they reach you", () =>
                {
                    bool ok = GD.Randf() < Mathf.Clamp(0.3f + mate.RapportWith(p) / 100f, 0.3f, 0.85f);
                    if (ok)
                    {
                        mate.ChangeRapport(p, -4f);
                        Note(p, $"{mc} hid your skins in his own chest while {bn} turned out yours. You owe him.");
                        return Done(T, $"A nod, a bundle passed under the bench, and {mc}'s chest has already been done. Yours holds shirts and a razor. {bn} moves on. {mc} does not look at you, but he will want something for that.");
                    }
                    Caught(evidence, 2f, $", and {mc} was caught taking them from you");
                    mate.Standing -= 5f; mate.ChangeRapport(p, -10f);
                    return Done(T, $"{clerk} sees the bundle change hands. Now there are two of you in the book, and {mc} will not thank you for it.");
                });
            }
            page.Add("\"Those are my own catch, sir\"", () =>
            {
                float chance = Mathf.Clamp(0.25f + p.Skills.Trade * 0.5f + p.Standing / 100f - boss.Personality.Suspicion * 0.4f + boss.RapportWith(p) / 200f, 0.05f, 0.8f);
                if (GD.Randf() < chance)
                {
                    boss.ChangeRapport(p, -2f);
                    Note(p, $"{bn} found skins in your chest and you told him they were your own catch. He let it go, this time.");
                    return Done(T, $"{bn} turns a skin over in the lantern light, looks at you a long moment, and drops it back in the chest. \"Your own catch. See it goes over the counter, then.\" He knows. He lets it go.");
                }
                Caught(evidence, 2f, ", and you lied to him about it");
                return Done(T, $"\"Your own catch. You were splitting wood all month.\" {bn} does not raise his voice. The skins go to the store, your wages with them, and the lie goes in the book beside them.");
            });
        }
        page.Add("Refuse to open it", () =>
        {
            p.Standing -= 6f;
            boss.ChangeRapport(p, -15f);
            Suspect(p, 1.5f);
            Note(p, $"You refused to turn out your chest for {bn}. (standing -6)");
            return Done(T, $"You sit on the lid. The house goes quiet. {bn} looks at you, then at the men watching, and decides not to make a scene of it tonight. \"We will speak of this again.\" He means it.");
        });
        return page;
    }
}
