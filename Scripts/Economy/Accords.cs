using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.World;

namespace IsometricFurTradeGame.Scripts.Economy;

// What a council's word is worth (design doc 3.8.2). One agreement object,
// specialised by its terms, between two households: two camps, a camp and a
// company's house, or the two houses at the Bay.
//   Peace      - no war party, raid or party sent against the other.
//   Exclusive  - the camp carries its furs to that company's houses alone,
//                and the house pays it better for them (Bonus).
//   Alliance   - a war party or raid against one brings the other out.
// It is broken by a deed the world can see: riding against a partner,
// carrying furs to the other company. A broken word is the worst thing a
// people can be caught at: standing falls hard, the feud is hot, the two
// houses at the Bay are close to blows, and the story travels (Chronicle).
//
// Accords are made by the world's own people (Week: camps worn down by a
// feud sue for peace, the houses agree a truce when the raiding costs them
// both, a house buys a camp away from its rival) and by a player, in talk
// with a chief or a master of a post (UI/ConversationAccords).
//
// Server only. Dev: `--accord=A:B:Peace+Exclusive[:days]`, `--accordtrace`.
public enum Term { Peace, Exclusive, Alliance }   // saved as ints: append only

public class Accord
{
    public int Id;
    public Household A, B;
    public readonly List<Term> Terms = new();
    public int Made, Until;
    public string By = "";
    public float Bonus;          // Exclusive: how much better the house pays the camp
    public bool Broken;
    public string BrokenBy = "";
    public bool Has(Term t) => Terms.Contains(t);
    public bool Live => !Broken && Plains.Today < Until;
    public bool Binds(Household h) => h == A || h == B;
    public Household Other(Household h) => h == A ? B : A;
}

public static class Accords
{
    public static readonly List<Accord> All = new();
    private static int _seq;
    private static readonly Dictionary<BandLedger, (FortLedger Post, int Day)> _lastTraded = new();
    private static readonly Dictionary<BandLedger, int> _lastOffer = new();
    private static readonly string[] Args = OS.GetCmdlineUserArgs();
    public static readonly bool Trace = System.Array.IndexOf(Args, "--accordtrace") >= 0;
    private static bool _devDone;

    public static void Reset() { All.Clear(); _seq = 0; _lastTraded.Clear(); _lastOffer.Clear(); _devDone = false; }

    // ---- names ------------------------------------------------------------------------------

    public static string Name(Household h) => h switch
    {
        BandLedger b => b.Name,
        TradingPostLedger tp when tp.Post != null => tp.Post.Name,
        NwcLedger => "the Nor'Westers' house at York",
        OutpostLedger => "Oxford House",
        FortLedger f when f == Sim.Fort => "York Factory",
        null => "nobody",
        _ => h.Title,
    };

    private static string Key(Household h) => h switch
    {
        BandLedger b => "band:" + b.Name,
        NwcLedger => "nwc",
        FortLedger f => "post:" + Name(f),
        _ => "",
    };

    private static Household FromKey(string k)
    {
        if (k == "nwc") return Sim.Nwc;
        if (k.StartsWith("post:")) return Convoys.Ledger(k[5..]);
        if (k.StartsWith("band:")) { string n = k[5..]; foreach (var c in Camps.All) if (c.Ledger != null && c.Ledger.Name == n) return c.Ledger; foreach (var b in Sim.Bands) if (b.Name == n) return b; }
        return null;
    }

    public static Faction CompanyOf(Household h) => h is FortLedger f ? f.Company : Faction.Wild;

    // ---- asking -------------------------------------------------------------------------------

    public static Accord Between(Household a, Household b, Term t)
    {
        if (a == null || b == null) return null;
        foreach (var ac in All) if (ac.Live && ac.Has(t) && ac.Binds(a) && ac.Binds(b)) return ac;
        return null;
    }

    public static bool AtPeace(Household a, Household b) => Between(a, b, Term.Peace) != null;

    public static IEnumerable<Accord> Of(Household h) { foreach (var ac in All) if (ac.Live && ac.Binds(h)) yield return ac; }

    // The company a camp has promised its furs to, if any.
    public static Accord ExclusiveOf(BandLedger b) { foreach (var ac in Of(b)) if (ac.Has(Term.Exclusive)) return ac; return null; }

    // How much better a house pays a camp it holds by an exclusive accord.
    public static float PriceBonus(FortLedger post, BandLedger b) =>
        ExclusiveOf(b) is { } ac && CompanyOf(ac.Other(b)) == post.Company ? ac.Bonus : 0f;

    // Would this camp, keeping its word, carry its furs here?
    public static bool MayTradeAt(BandLedger b, FortLedger post) =>
        ExclusiveOf(b) is not { } ac || CompanyOf(ac.Other(b)) == post.Company;

    public static string Describe(Accord ac)
    {
        var parts = new List<string>();
        if (ac.Has(Term.Peace)) parts.Add("peace");
        if (ac.Has(Term.Exclusive)) parts.Add($"{Name(ac.A is BandLedger ? ac.A : ac.B)}'s furs to {Name(ac.A is BandLedger ? ac.B : ac.A)}'s company alone, at {ac.Bonus * 100f:0}% over the standard");
        if (ac.Has(Term.Alliance)) parts.Add("each to stand by the other");
        int left = ac.Until - Plains.Today;
        return $"{Name(ac.A)} and {Name(ac.B)}: {string.Join("; ", parts)} ({(left >= 365 ? $"{left / 365} year{(left >= 730 ? "s" : "")}" : $"{left} days")} to run{(ac.By != "" ? $", made by {ac.By}" : "")})";
    }

    // ---- making -------------------------------------------------------------------------------

    public static Accord Make(Household a, Household b, IEnumerable<Term> terms, int days, string by, float bonus = 0f)
    {
        if (a == null || b == null || a == b) return null;
        // A new accord between the same two takes the place of the old.
        foreach (var old in All) if (old.Live && old.Binds(a) && old.Binds(b)) old.Until = Plains.Today;
        var ac = new Accord { Id = ++_seq, A = a, B = b, Made = Plains.Today, Until = Plains.Today + Mathf.Max(1, days), By = by ?? "", Bonus = bonus };
        foreach (var t in terms) if (!ac.Terms.Contains(t)) ac.Terms.Add(t);
        if (ac.Terms.Count == 0) return null;
        All.Add(ac);
        if (ac.Has(Term.Peace))
        {
            Plains.CoolFeud(a, b); Plains.CoolFeud(b, a);
            if (Rivalry.AreRivals(CompanyOf(a), CompanyOf(b))) Rivalry.Tension = Mathf.Max(0f, Rivalry.Tension - 20f);
        }
        if (ac.Has(Term.Exclusive)) { if (a is BandLedger ba) _lastOffer[ba] = Plains.Today; if (b is BandLedger bb) _lastOffer[bb] = Plains.Today; }
        string text = Describe(ac);
        Chronicle.Event($"An accord: {text}.", IsoGrid.ToWorld(War.CampOf(a)), subject: Name(a), weight: 1.6f);
        Sim.Notice($"An accord: {text}.");
        if (Trace) GD.Print($"[accord] made #{ac.Id}: {text}");
        return ac;
    }

    // ---- breaking -----------------------------------------------------------------------------

    public static void Breach(Accord ac, Household by, string what)
    {
        if (ac == null || !ac.Live) return;
        ac.Broken = true; ac.BrokenBy = Name(by);
        var wronged = ac.Other(by);
        // The wronged side's standing with the breaker falls hard.
        if (wronged is BandLedger wb && by is FortLedger bf) Lower(wb, bf.Company, 25f);
        if (by is BandLedger bb && wronged is FortLedger wf) Lower(bb, wf.Company, 20f);
        Plains.HeatFeud(wronged, by, 4f);
        if (Rivalry.AreRivals(CompanyOf(by), CompanyOf(wronged))) Rivalry.Raise(25f, "a truce broken");
        if (by.Leader != null && wronged.Leader != null) wronged.Leader.ChangeRapport(by.Leader, -30f);
        string line = $"{Name(by)} broke its word to {Name(wronged)}: {what}";
        Chronicle.Event(line + ".", IsoGrid.ToWorld(War.CampOf(wronged)), subject: Name(by), weight: 2.2f);
        Sim.Notice(line + ". Nobody will forget it soon.");
        if (Trace) GD.Print($"[accord] broken #{ac.Id} by {Name(by)}: {what}");
    }

    private static void Lower(BandLedger b, Faction f, float by)
    {
        switch (f)
        {
            case Faction.NWC: b.StandingWithNwc = Mathf.Max(0f, b.StandingWithNwc - by); break;
            case Faction.American: b.StandingWithAmericans = Mathf.Max(0f, b.StandingWithAmericans - by); break;
            default: b.StandingWithCompany = Mathf.Max(0f, b.StandingWithCompany - by); break;
        }
    }

    // ---- the deeds that keep or break them ------------------------------------------------------

    // A camp's war party rides out (War.Launch): a breach if it rides against
    // a partner in peace; the victim's allies come out.
    public static void WarParty(BandLedger from, Household against)
    {
        if (Between(from, against, Term.Peace) is { } peace) Breach(peace, from, $"its men rode against {Name(against)}");
        Allies(against, from);
    }

    // A house sends men against another (Rivalry.OrderRaid).
    public static void Raid(FortLedger from, FortLedger target)
    {
        if (Between(from, target, Term.Peace) is { } peace) Breach(peace, from, $"it sent armed men against {Name(target)}");
        Allies(target, from);
    }

    // Whoever stands by the attacked: their feud is hot, and a camp may ride.
    private static void Allies(Household attacked, Household attacker)
    {
        foreach (var ac in new List<Accord>(Of(attacked)))
        {
            if (!ac.Has(Term.Alliance)) continue;
            var ally = ac.Other(attacked);
            if (ally == attacker) { Breach(ac, attacker, $"it turned on its own ally {Name(attacked)}"); continue; }
            Plains.HeatFeud(ally, attacker, 3f);
            if (Trace) GD.Print($"[accord] {Name(ally)} takes up {Name(attacked)}'s quarrel with {Name(attacker)}");
            Chronicle.Event($"{Name(ally)} took up {Name(attacked)}'s quarrel with {Name(attacker)}, as their accord bound them.", IsoGrid.ToWorld(War.CampOf(ally)), subject: Name(ally), weight: 1.3f);
            if (ally is BandLedger ab && GD.Randf() < 0.5f) War.Launch(ab, attacker, $"to stand by {Name(attacked)}");
        }
    }

    // A camp's men have traded at a house (StandardOfTrade.Trade). Carrying
    // furs to the other company breaks an exclusive accord, if word gets back.
    public static void Traded(BandLedger b, FortLedger post)
    {
        _lastTraded[b] = (post, Plains.Today);
        if (ExclusiveOf(b) is { } ac && CompanyOf(ac.Other(b)) != post.Company && GD.Randf() < 0.6f)
            Breach(ac, b, $"its men carried their furs to {Name(post)}");
    }

    // ---- the world's own making ----------------------------------------------------------------

    public static void Week()
    {
        if (!Net.Net.IsServer) return;
        Dev();
        foreach (var ac in All)
            if (!ac.Broken && ac.Until == Plains.Today && Trace) GD.Print($"[accord] ran out #{ac.Id}: {Name(ac.A)} and {Name(ac.B)}");
        SuePeace();
        HousesTruce();
        BuyAway();
        if (Trace) foreach (var ac in All) if (ac.Live) GD.Print($"[accord] in force: {Describe(ac)}");
    }

    // Two camps worn down by a feud: the weaker sues, and the stronger may take it.
    private static void SuePeace()
    {
        var camps = new List<BandLedger>();
        foreach (var c in Camps.All) if (c.Ledger != null) camps.Add(c.Ledger);
        for (int i = 0; i < camps.Count; i++)
            for (int j = i + 1; j < camps.Count; j++)
            {
                BandLedger a = camps[i], b = camps[j];
                float fa = Plains.Feud(a, b), fb = Plains.Feud(b, a);
                if (fa < 1.5f || fb < 1.5f || AtPeace(a, b) || War.AtPeace(a, b)) continue;
                if (GD.Randf() > 0.15f) continue;
                int ma = War.Fighters(a).Count, mb = War.Fighters(b).Count;
                var (weak, strong) = ma <= mb ? (a, b) : (b, a);
                float strongFeud = Plains.Feud(strong, weak);
                bool takes = strongFeud < 4f || GD.Randf() < 0.3f + (strong.Leader?.Personality.Loyalty ?? 0.5f) * 0.2f;
                if (!takes) { if (Trace) GD.Print($"[accord] {strong.Name} turned away {weak.Name}'s pipe"); continue; }
                Make(weak, strong, new[] { Term.Peace }, 365, $"the chiefs of {weak.Name} and {strong.Name}");
            }
    }

    // The two houses at York, when the raiding has cost them both.
    private static void HousesTruce()
    {
        var hbc = Sim.Fort; var nwc = Sim.Nwc;
        if (hbc?.Leader == null || nwc?.Leader == null || hbc.Leader.IsDead || nwc.Leader.IsDead || Rivalry.United) return;
        if (Rivalry.Tension < 60f || AtPeace(hbc, nwc) || GD.Randf() > 0.1f) return;
        if (hbc.LotsLostToRaids + nwc.LotsLostToRaids < 2) return;   // it has cost them both something
        float willing = 1.4f - hbc.Leader.Personality.Ambition - nwc.Leader.Personality.Ambition * 0.8f;
        if (GD.Randf() > willing) { if (Trace) GD.Print("[accord] the houses at York could not agree a truce"); return; }
        Make(hbc, nwc, new[] { Term.Peace }, 120, $"{hbc.Leader.DisplayName} and {nwc.Leader.DisplayName}");
    }

    // A house buys a camp's furs away from its rival with a better price.
    private static void BuyAway()
    {
        foreach (var (b, last) in new List<KeyValuePair<BandLedger, (FortLedger Post, int Day)>>(_lastTraded))
        {
            if (Plains.Today - last.Day > 120 || ExclusiveOf(b) != null || IsFamily(b)) continue;
            if (_lastOffer.TryGetValue(b, out int o) && Plains.Today - o < 90) continue;
            var rival = Rivalry.RivalOf(last.Post.Company);
            if (!Rivalry.AreRivals(last.Post.Company, rival)) continue;
            // The rival's nearest house with a master, within a camp's reach.
            FortLedger theirs = null; float best = StandardOfTrade.ReachPx;
            foreach (var p in Sim.Posts())
            {
                if (p.Company != rival || p.Leader == null || p.Leader.IsDead) continue;
                float d = IsoGrid.ToWorld(p.AccessCell).DistanceTo(IsoGrid.ToWorld(b.Camp));
                if (d < best) { best = d; theirs = p; }
            }
            if (theirs == null || GD.Randf() > 0.25f) continue;
            _lastOffer[b] = Plains.Today;
            float standing = b.StandingWith(rival);
            bool yes = standing >= 45f && GD.Randf() < 0.3f + (b.Leader?.Personality.Ambition ?? 0.5f) * 0.3f + (standing - 45f) / 100f;
            if (!yes) { if (Trace) GD.Print($"[accord] {b.Name} would not give {Name(theirs)} its furs alone"); continue; }
            Make(b, theirs, new[] { Term.Exclusive }, 730, $"{theirs.Leader.DisplayName} and {b.Leader?.DisplayName ?? b.Name}", 0.15f);
        }
    }

    // A homeguard family lives by its post: nobody bargains for its furs.
    private static bool IsFamily(BandLedger b) { foreach (var c in Camps.All) if (c.Ledger == b) return c.IsFamily; return false; }

    // Dev: `--accord=A:B:Peace+Exclusive[:days]` (names of camps or posts; "nwc" for the Nor'Westers at York).
    public static void Dev()
    {
        if (_devDone) return;
        _devDone = true;
        foreach (string arg in Args)
        {
            if (!arg.StartsWith("--accord=")) continue;
            var bits = arg.Substring(9).Split(':');
            if (bits.Length < 3) continue;
            Household Find(string s) => s == "nwc" ? Sim.Nwc : Quests.HouseholdAt(s);
            var a = Find(bits[0]); var b = Find(bits[1]);
            var terms = new List<Term>();
            foreach (var t in bits[2].Split('+')) if (System.Enum.TryParse<Term>(t, true, out var term)) terms.Add(term);
            int days = bits.Length > 3 && int.TryParse(bits[3], out int dd) ? dd : 365;
            if (a == null || b == null) { GD.Print($"[accord] no such place: {(a == null ? bits[0] : bits[1])}"); continue; }
            Make(a, b, terms, days, "(dev)", terms.Contains(Term.Exclusive) ? 0.15f : 0f);
        }
    }

    // ---- save ---------------------------------------------------------------------------------

    public static Godot.Collections.Dictionary Save()
    {
        var list = new Godot.Collections.Array();
        foreach (var ac in All)
        {
            if (!ac.Live && Plains.Today - ac.Until > 730) continue;
            var terms = new Godot.Collections.Array(); foreach (var t in ac.Terms) terms.Add((int)t);
            list.Add(new Godot.Collections.Dictionary { ["a"] = Key(ac.A), ["b"] = Key(ac.B), ["terms"] = terms, ["made"] = ac.Made, ["until"] = ac.Until, ["by"] = ac.By, ["bonus"] = ac.Bonus, ["broken"] = ac.Broken, ["brokenBy"] = ac.BrokenBy });
        }
        return new Godot.Collections.Dictionary { ["all"] = list };
    }

    public static void Load(Godot.Collections.Dictionary d)
    {
        All.Clear(); _seq = 0;
        if (d == null || !d.ContainsKey("all")) return;
        foreach (var v in d["all"].AsGodotArray())
        {
            var e = v.AsGodotDictionary();
            var a = FromKey(e["a"].AsString()); var b = FromKey(e["b"].AsString());
            if (a == null || b == null) continue;
            var ac = new Accord { Id = ++_seq, A = a, B = b, Made = e["made"].AsInt32(), Until = e["until"].AsInt32(), By = e["by"].AsString(), Bonus = e["bonus"].AsSingle(), Broken = e["broken"].AsBool(), BrokenBy = e["brokenBy"].AsString() };
            foreach (var t in e["terms"].AsGodotArray()) ac.Terms.Add((Term)t.AsInt32());
            All.Add(ac);
        }
    }
}
