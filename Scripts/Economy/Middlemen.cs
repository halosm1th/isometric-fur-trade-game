using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Agents.Goals;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Ecology;
using IsometricFurTradeGame.Scripts.Items;
using IsometricFurTradeGame.Scripts.World;

namespace IsometricFurTradeGame.Scripts.Economy;

// The middlemen (design doc 13.2; docs/specs/reading-notes-history.md 3.3).
// For a century the Cree, the Assiniboine and the Ojibwe stood between the
// posts and the far nations. A people with a post too far off to reach - no
// canoes, no road, afraid of starving on the way - got the trade's goods
// through the camp between them and it, used, dear and late.
//
//  - A far camp is one with no post it can safely reach within a few days.
//    It finds a middleman: a camp nearer the posts, not its enemy, whose
//    trader is the kind to buy beyond his people's needs.
//  - The middleman's men buy for resale at the post, out of the furs their
//    own camp's wants leave over (StandardOfTrade.Trade -> Buy). The goods go
//    into the camp's own stores and are used for a season before they are
//    sold on (Henday saw them used a year or two and then traded away).
//  - The two camps meet when one of them decides a ride to the other is
//    worth it (MidWillGo, FarWillGo): the middleman when what he carries is
//    worth the days on the road against what he believes the far camp has to
//    pay; the far camp when it is short of something it cannot do without and
//    believes the middleman has it. Each goes to where it last knew the other
//    camp to be, and may find the ground empty. Under a player's eye the men
//    ride in and trade face to face (MeetVisitGoal); elsewhere it is numbers.
//    The far camp takes what it is short of, second-hand - it wears out
//    sooner (Wants) - and pays in furs, robes and horses. The middleman carries
//    those furs to the post on his next trip, so a far nation's furs come in
//    a year late, through other hands.
//  - The price is the post's, times what the carrying, the wear, the
//    middleman's own gifts to his partners and his temper add: about three
//    times over for a gun (Ray: 12 MB at the Bay, 36 from the Cree to the
//    Blackfoot).
//  - While it has its middleman, a far camp does not go to the posts itself.
//    When a post opens within its reach it does, and the old partners are
//    left with a grievance (Plains feuds).
// Resolved weekly in numbers, like the Missouri trade; Plains.Weekly calls it.
public static class Middlemen
{
    private const float FarPx = StandardOfTrade.ReachPx * 0.75f;   // beyond this a post is too far to go in to yourself
    private const float MeetPx = StandardOfTrade.ReachPx;          // how far a middleman's men go to meet a far camp
    private const int LapseDays = 420;                             // a partnership not used in over a year is forgotten
    private const float WillingLean = 0.35f;                       // a trader below this buys only for his own people

    public class Partner
    {
        public BandLedger Mid;
        public int Since, Last = -1;
        public float Markup;   // what the last meeting's gun cost, over the post's price
        public readonly Dictionary<ItemId, int> Wanted = new();   // what the far camp said it lacked, last time they met
        // What each side believes of the other, from the last time they met
        // (or heard): where its lodges stood, what it had to trade, and when.
        // Nobody reads another camp from a distance (design doc 2, 10).
        public Vector2I FarSeenAt, MidSeenAt;
        public float FarSeenMeans, MidSeenStock;
        public int SeenDay;
    }

    // Men of one camp on their way to the other's (a meeting is a journey,
    // not a date on the calendar): who went, where they believe the other
    // camp is, and the days out and home.
    public class Journey
    {
        public BandLedger Far, Goer;
        public Partner P;
        public bool MidGoes, Met, Visiting;
        public Vector2I Toward;
        public int StartDay, ArriveDay, HomeDay, OneWay;
        public readonly List<Npc> Party = new();
        public BandLedger Host => MidGoes ? Far : P.Mid;
    }

    private static readonly List<Journey> _journeys = new();
    public static IReadOnlyList<Journey> Journeys => _journeys;
    public static bool Away(BandLedger b) { foreach (var j in _journeys) if (j.Goer == b) return true; return false; }
    private static bool Busy(Partner p, BandLedger far) { foreach (var j in _journeys) if (j.P == p || j.Far == far) return true; return false; }

    private static readonly Dictionary<BandLedger, Partner> _partners = new();                     // far camp -> its middleman
    private static readonly Dictionary<BandLedger, Dictionary<ItemId, int>> _stock = new();        // pieces a middleman holds for trade
    private static readonly Dictionary<BandLedger, Dictionary<ItemId, int>> _secondHand = new();   // pieces that came to a camp used
    private static readonly Dictionary<BandLedger, float> _lean = new();
    private static readonly Dictionary<BandLedger, TradingPostLedger> _boughtAt = new();

    public static void Reset() { _partners.Clear(); _stock.Clear(); _secondHand.Clear(); _lean.Clear(); _boughtAt.Clear(); _journeys.Clear(); }

    public static Partner PartnerOf(BandLedger far) => _partners.GetValueOrDefault(far);
    public static int Stock(BandLedger b, ItemId id) => _stock.TryGetValue(b, out var s) ? s.GetValueOrDefault(id) : 0;
    public static int SecondHand(BandLedger b, ItemId id) => _secondHand.TryGetValue(b, out var s) ? s.GetValueOrDefault(id) : 0;

    // How far the camp's trader leans to buying beyond his people's needs,
    // 0..1: his own ambition where the chief is a person in the world, else
    // the camp's own lean, fixed when the world begins.
    public static float Lean(BandLedger b)
    {
        if (_lean.TryGetValue(b, out float l)) return l;
        var rng = new RandomNumberGenerator { Seed = (ulong)Mathf.Abs(b.Name.GetHashCode()) + 1774 };
        l = rng.Randf();
        foreach (var n in Sim.Npcs)
            if (n.HomeStores == b && n.Role == Role.Chief && !n.IsDead) { l = n.Personality.Ambition * 0.7f + l * 0.3f; break; }
        _lean[b] = l;
        return l;
    }

    // The nearest house this camp could safely carry its furs to, or null.
    public static TradingPostLedger NearestPost(BandLedger b, out float dist)
    {
        TradingPostLedger best = null; dist = float.MaxValue;
        var at = IsoGrid.ToWorld(b.Camp);
        foreach (var p in Posts.Standing())
        {
            if (p.Ledger == null) continue;
            float d = IsoGrid.ToWorld(p.Ledger.AccessCell).DistanceTo(at);
            if (d > StandardOfTrade.ReachPx || d >= dist || !StandardOfTrade.Safe(b, p.Ledger)) continue;
            dist = d; best = p.Ledger;
        }
        return best;
    }

    public static bool IsFar(BandLedger b) => NearestPost(b, out float d) == null || d > FarPx;

    // A far camp with a middleman leaves the posts to him.
    public static bool TradesThrough(BandLedger b) => _partners.ContainsKey(b) && IsFar(b);

    private static bool Eligible(CountryCamp c) => c.Ledger != null && !c.IsFamily && !Gardens.IsTown(c.Ledger);

    private static bool Hostile(BandLedger a, BandLedger b) => Plains.Enemies(a.Nation, b.Nation) && !War.AtPeace(a, b);

    // ---- the week ----------------------------------------------------------------------------

    public static void Week(GameCalendar cal)
    {
        foreach (var c in Camps.All) if (Eligible(c)) Tidy(c.Ledger);
        Lapse(cal);
        // Far camps without a middleman look for one.
        foreach (var c in Camps.All)
        {
            if (!Eligible(c) || _partners.ContainsKey(c.Ledger) || !IsFar(c.Ledger)) continue;
            var mid = Find(c.Ledger);
            if (mid == null) continue;
            // They know of each other: where the lodges stand and what the far camp has to trade.
            var p = new Partner { Mid = mid, Since = Plains.Today, FarSeenAt = c.Ledger.Camp, MidSeenAt = mid.Camp, FarSeenMeans = Means(c.Ledger), SeenDay = Plains.Today };
            foreach (var (id, shortBy, _) in Wants.Deficits(c.Ledger)) p.Wanted[id] = shortBy;
            _partners[c.Ledger] = p;
            Log(cal, $"{c.Ledger.Name} will trade through {mid.Name} (their nearest house {Describe(c.Ledger)}; {mid.Name}'s trader leans {Lean(mid):0.00})");
        }
        // Each side decides for itself whether a ride to the other is worth it.
        foreach (var (far, p) in new List<KeyValuePair<BandLedger, Partner>>(_partners))
        {
            if (Busy(p, far)) continue;
            if (MidWillGo(far, p, out string why)) Go(far, p, true, why, cal);
            else if (FarWillGo(far, p, out why)) Go(far, p, false, why, cal);
        }
    }

    // ---- deciding to go ----------------------------------------------------------------------

    // A camp can spare men for the ride: they are not off trading, at war,
    // running buffalo or already gone to meet someone.
    private static bool Free(BandLedger b)
    {
        if (TradeTrips.Away(b) || Away(b) || BuffaloRun.Of(b) != null) return false;
        foreach (var w in War.Parties) if (w.From == b && w.Phase is WarPhase.Riding or WarPhase.Fighting) return false;
        return b.Mouths >= 6;
    }

    private static bool Riding(BandLedger b, int men) => b.Herd.Free >= men;
    private static int Men(BandLedger b) => Mathf.Clamp(b.Mouths / 6, 2, 6);

    private static int DaysTo(BandLedger from, Vector2I to)
    {
        float km = IsoGrid.ToWorld(from.Camp).DistanceTo(IsoGrid.ToWorld(to)) / WorldMap.PxPerKm;
        return Mathf.Max(1, Mathf.CeilToInt(km / (Riding(from, Men(from)) ? 45f : 25f)));
    }

    // What the middleman believes the far camp has to trade: what he saw
    // last, and as the months pass, the habit of the country - a camp has
    // something after a season's hunt.
    private static float BelievedMeans(Partner p, BandLedger far)
    {
        float habit = far.Mouths * 1.5f;
        float age = Plains.Today - p.SeenDay;
        return Mathf.Lerp(p.FarSeenMeans, Mathf.Max(habit, p.FarSeenMeans), Mathf.Clamp(age / 150f, 0f, 1f));
    }

    // The middleman's men ride out when what they carry for the far camp,
    // at their price and as far as they believe the far camp can pay, is
    // worth the days on the road. A trader who leans hard to the trade goes
    // for less.
    private static bool MidWillGo(BandLedger far, Partner p, out string why)
    {
        why = "";
        var mid = p.Mid;
        if (!Free(mid)) return false;
        float held = 0f;
        foreach (var (id, n) in p.Wanted)
        {
            int k = Mathf.Min(n, Mathf.Min(Stock(mid, id), mid.Stores.Count(id)));
            if (k > 0) held += k * Cost(mid, id) * Markup(mid, far, id);
        }
        if (held < 5f) return false;
        int days = DaysTo(mid, p.FarSeenAt);
        if (days > 14) return false;
        float worth = Mathf.Min(held, BelievedMeans(p, far));
        float cost = Men(mid) * days * 2f * 0.6f + 6f;   // the men's days away from the hunt, and the gifts on arrival
        if (worth < cost * (2f - Lean(mid))) return false;
        why = $"carrying {held:0} MB of goods to a camp they believe has {BelievedMeans(p, far):0} MB to trade; {days} days each way";
        return true;
    }

    // The far camp sends its own men when it is short of something it
    // cannot do without - guns and powder above all when it is in danger -
    // and believes the middleman has it.
    private static bool FarWillGo(BandLedger far, Partner p, out string why)
    {
        why = "";
        if (!Free(far) || Plains.Today - p.SeenDay > 300 || Means(far) < 10f) return false;
        // Worth the ride only if what they saw in his hands, of what they lack, outweighs the days on the road.
        int d = DaysTo(far, p.MidSeenAt);
        if (p.MidSeenStock < Men(far) * d * 2f * 0.6f + 6f) return false;
        float press = 0f; string first = null;
        foreach (var (id, _, weight) in Wants.Deficits(far)) { press += weight; first ??= Items.Items.Of(id).Name.ToLower(); }
        press *= 1f + 0.3f * Wants.Of(far).Threat;
        int days = DaysTo(far, p.MidSeenAt);
        if (days > 14 || press < 1.2f + days * 0.1f) return false;
        why = $"short of {first} and the rest (need {press:0.0}); they believe {p.Mid.Name} have {p.MidSeenStock:0} MB of goods; {days} days each way";
        return true;
    }

    private static void Go(BandLedger far, Partner p, bool midGoes, string why, GameCalendar cal)
    {
        var goer = midGoes ? p.Mid : far;
        var j = new Journey { Far = far, Goer = goer, P = p, MidGoes = midGoes, Toward = midGoes ? p.FarSeenAt : p.MidSeenAt };
        int men = Men(goer);
        foreach (var n in Sim.Npcs)
        {
            if (j.Party.Count >= men) break;
            if (n.HomeStores != goer || n.IsDead || n.Away || n.IsFemale || n.HeldBy != null || n.Role is not (Role.Hunter or Role.Chief)) continue;
            // A man a player is watching rides out in plain sight; the rest are simply gone.
            if (n.Tier == Tier.Live) { if (n.Brain.Has<BuffaloRunGoal>() || n.Brain.Has<HorseRaidGoal>()) continue; n.Brain.Clear(n); n.Brain.Push(n, new GoToGoal(j.Toward, 400f)); }
            else TradeTrips.TakeAway(n, $"gone to meet {j.Host.Name}");
            j.Party.Add(n);
        }
        j.OneWay = DaysTo(goer, j.Toward);
        j.StartDay = Plains.Today;
        j.ArriveDay = j.StartDay + j.OneWay;
        j.HomeDay = j.ArriveDay + 1 + j.OneWay;
        _journeys.Add(j);
        Log(cal, $"{men} men of {goer.Name} set out to meet {j.Host.Name} ({why})");
    }

    // ---- on the road -------------------------------------------------------------------------

    // Once a day: arrivals, missed camps, meetings and homecomings.
    public static void Day(GameCalendar cal)
    {
        int today = Plains.Today;
        for (int i = _journeys.Count - 1; i >= 0; i--)
        {
            var j = _journeys[i];
            j.Party.RemoveAll(n => n == null || !GodotObject.IsInstanceValid(n) || n.IsDead);
            foreach (var n in j.Party) if (!n.Away && n.Tier == Tier.Background && !j.Visiting) TradeTrips.TakeAway(n, $"gone to meet {j.Host.Name}");
            if (!_partners.TryGetValue(j.Far, out var p) || p != j.P) { Home(j, i, cal, "the partnership is over"); continue; }
            if (!j.Met && today >= j.ArriveDay)
            {
                var host = j.Host;
                float off = IsoGrid.ToWorld(host.Camp).DistanceTo(IsoGrid.ToWorld(j.Toward));
                if (off > 2500f)
                {
                    // The lodges are not where they were. The trail may be followed if it is fresh and short.
                    int more = DaysTo(j.Goer, host.Camp) - j.OneWay;
                    if (GD.Randf() < 0.6f && more <= 4 && j.ArriveDay - j.StartDay < j.OneWay + 6)
                    {
                        j.Toward = host.Camp;
                        j.ArriveDay = today + Mathf.Max(1, more);
                        Log(cal, $"{j.Goer.Name}'s men found {host.Name}'s old camp ground empty and follow the trail");
                        continue;
                    }
                    j.Met = true;
                    j.HomeDay = today + j.OneWay;
                    Log(cal, $"{j.Goer.Name}'s men found {host.Name}'s old camp ground empty and turned for home");
                    Chronicle.Event($"Men of {j.Goer.Name} rode to meet {host.Name} and found only their old camp ground.", IsoGrid.ToWorld(j.Toward), subject: j.Goer.Name, weight: 0.3f);
                    continue;
                }
                if (Plains.NearPlayer(IsoGrid.ToWorld(host.Camp), Tiers.LiveRadius)) Visit(j);
                else Arrive(j, cal);
            }
            // Came within sight but never reached the lodges (turned back, held, killed): done in numbers.
            if (j.Visiting && !j.Met && today > j.ArriveDay + 3) Arrive(j, cal);
            if (j.Met && !j.Visiting && today >= j.HomeDay) Home(j, i, cal, null);
        }
    }

    // They ride in where a player can see them: the men walk up to the lodges.
    private static void Visit(Journey j)
    {
        j.Visiting = true;
        var at = IsoGrid.ToWorld(j.Host.AccessCell);
        var from = (IsoGrid.ToWorld(j.Goer.Camp) - at).Normalized();
        int k = 0;
        foreach (var n in j.Party)
        {
            if (n.Away) TradeTrips.PutBack(n, at + from * 900f + new Vector2(k * 60, k * 30));
            n.Tier = Tier.Live; n.ProcessMode = Node.ProcessModeEnum.Inherit; n.Visible = true;
            Net.Net.I?.AnnounceTier(n, false);
            n.Brain.Clear(n);
            n.Brain.Push(n, new MeetVisitGoal(j, k == 0));
            k++;
        }
        Sim.Notice($"Men of {j.Goer.Name} are riding in to {j.Host.Name}'s lodges to trade.");
    }

    // The meeting itself, at the host's lodges.
    public static void Arrive(Journey j, GameCalendar cal)
    {
        if (j.Met) return;
        j.Met = true;
        j.Visiting = false;
        j.HomeDay = Plains.Today + j.OneWay;
        Meet(j.Far, j.P, cal);
    }

    private static void Home(Journey j, int index, GameCalendar cal, string why)
    {
        foreach (var n in j.Party) if (n.Away) TradeTrips.PutBack(n, IsoGrid.ToWorld(n.StandCellNear(j.Goer.AccessCell)));
        _journeys.RemoveAt(index);
        Log(cal, $"{j.Goer.Name}'s men are home from meeting {j.Host.Name}{(why != null ? $" ({why})" : "")}");
    }

    private static string Describe(BandLedger b) => NearestPost(b, out float d) is { } post ? $"{post.Post.Name}, {d / WorldMap.PxPerKm:0} km" : "out of reach";

    // Nobody keeps for trade what is no longer in the camp, nor counts as
    // used more than it has.
    private static void Tidy(BandLedger b)
    {
        foreach (var map in new[] { _stock, _secondHand })
        {
            if (!map.TryGetValue(b, out var s)) continue;
            foreach (var id in new List<ItemId>(s.Keys))
            {
                int n = Mathf.Min(s[id], b.Stores.Count(id));
                if (n <= 0) s.Remove(id); else s[id] = n;
            }
        }
    }

    private static void Lapse(GameCalendar cal)
    {
        foreach (var (far, p) in new List<KeyValuePair<BandLedger, Partner>>(_partners))
        {
            if (!IsFar(far))
            {
                // A house has come within their reach: they take their furs in
                // themselves now, past the people who used to carry them.
                _partners.Remove(far);
                Plains.HeatFeud(p.Mid, far, 1.5f);
                Log(cal, $"{far.Name} go to {Describe(far)} themselves now, past {p.Mid.Name}");
                Chronicle.Event($"{far.Name} take their furs to the post themselves now, and {p.Mid.Name} do not like it.", IsoGrid.ToWorld(far.Camp), subject: p.Mid.Name, weight: 0.8f);
                continue;
            }
            bool stale = Plains.Today - (p.Last >= 0 ? p.Last : p.Since) > LapseDays;
            if (stale || Hostile(far, p.Mid) || p.Mid.Mouths == 0 || Lean(p.Mid) < WillingLean)
            {
                _partners.Remove(far);
                Log(cal, $"{far.Name} no longer trade through {p.Mid.Name}");
            }
        }
    }

    // The far camp's middleman: a camp within a meeting's ride that has a
    // house of its own in reach, is not its enemy, and whose trader buys
    // beyond his people's needs. Their own nation first.
    private static BandLedger Find(BandLedger far)
    {
        BandLedger best = null; float bestScore = 0f;
        var at = IsoGrid.ToWorld(far.Camp);
        foreach (var c in Camps.All)
        {
            if (!Eligible(c) || c.Ledger == far || _partners.ContainsKey(c.Ledger)) continue;
            var b = c.Ledger;
            float d = IsoGrid.ToWorld(b.Camp).DistanceTo(at);
            if (d > MeetPx || Hostile(far, b) || Lean(b) < WillingLean) continue;
            if (NearestPost(b, out float pd) == null || pd > FarPx) continue;
            float score = Lean(b) * (1f - d / MeetPx) * (b.Nation == far.Nation ? 1.5f : 1f);
            if (score > bestScore) { bestScore = score; best = b; }
        }
        return best;
    }

    // ---- buying for resale at the post --------------------------------------------------------

    // What a middleman's trader means to buy beyond his own people's wants:
    // what his far partners said they lacked, less what he already holds.
    public static List<(ItemId Id, int N)> Orders(BandLedger b)
    {
        var want = new Dictionary<ItemId, int>();
        foreach (var (_, p) in _partners)
            if (p.Mid == b)
                foreach (var (id, n) in p.Wanted) want[id] = want.GetValueOrDefault(id) + n;
        var list = new List<(ItemId, int)>();
        foreach (var (id, n) in want)
        {
            int k = n - Stock(b, id);
            if (k > 0) list.Add((id, k));
        }
        // Dear and light first: a gun or a twist of tobacco carries further than a kettle.
        list.Sort((x, y) => (Items.Items.Of(y.Item1).ValueMb / Mathf.Max(0.2f, Items.Items.Of(y.Item1).WeightLbs)).CompareTo(Items.Items.Of(x.Item1).ValueMb / Mathf.Max(0.2f, Items.Items.Of(x.Item1).WeightLbs)));
        return list;
    }

    // At the counter, out of the furs left over once the camp's own wants are
    // met: as much of that as the trader's lean says, spent on his orders.
    // Returns what was paid, at the post's prices.
    public static float Buy(TradingPostLedger post, BandLedger b, float spare, Inventory into, Dictionary<ItemId, int> counts)
    {
        if (spare < 1f || Lean(b) < WillingLean) return 0f;
        var orders = Orders(b);
        if (orders.Count == 0) return 0f;
        float budget = spare * Lean(b), paid = 0f;
        var bought = new List<string>();
        foreach (var (id, n) in orders)
        {
            float price = StandardOfTrade.Price(post, id);
            int k = Mathf.Min(n, Mathf.Min(post.Stores.Count(id), Mathf.FloorToInt((budget - paid) / price)));
            if (k <= 0) continue;
            post.Stores.Remove(id, k);
            into.Add(id, k);
            paid += k * price;
            counts[id] = counts.GetValueOrDefault(id) + k;
            if (!_stock.TryGetValue(b, out var s)) _stock[b] = s = new();
            s[id] = s.GetValueOrDefault(id) + k;
            bought.Add($"{k} {Items.Items.Of(id).Name.ToLower()}");
        }
        if (paid > 0f)
        {
            _boughtAt[b] = post;
            if (Plains.Log) GD.Print($"[middlemen] {GameCalendar.Instance?.DateString} {b.Name} buy for their partners at {post.Post.Name}: {string.Join(", ", bought)} ({paid:0} MB)");
        }
        else if (Plains.Log)
        {
            var asked = new List<string>();
            foreach (var (id, n) in orders) asked.Add($"{Items.Items.Of(id).Name.ToLower()} {n} (shelf {post.Stores.Count(id)}, {StandardOfTrade.Price(post, id):0.0} MB)");
            GD.Print($"[middlemen] {GameCalendar.Instance?.DateString} {b.Name} found nothing to buy for their partners at {post.Post.Name} with {budget:0} MB: {string.Join(", ", asked)}");
        }
        return paid;
    }

    // ---- the meeting --------------------------------------------------------------------------

    // What a piece cost the middleman: the price at the house he buys at.
    private static float Cost(BandLedger mid, ItemId id) => _boughtAt.TryGetValue(mid, out var post) && post != null ? StandardOfTrade.Price(post, id) : Items.Items.Of(id).ValueMb;

    // The middleman's price over his cost. Not greed alone: the wear, the
    // carrying and the gifts he owes his own partners, then his temper - and
    // more again the further and heavier the carry.
    public static float Markup(BandLedger mid, BandLedger far, ItemId id)
    {
        float km = IsoGrid.ToWorld(mid.Camp).DistanceTo(IsoGrid.ToWorld(far.Camp)) / WorldMap.PxPerKm;
        var d = Items.Items.Of(id);
        float heavy = Mathf.Clamp(0.5f + d.WeightLbs / Mathf.Max(0.1f, d.ValueMb) * 0.6f, 0.6f, 1.6f);
        return (2.2f + 0.8f * Lean(mid)) * (1f + km / 1000f * 0.6f * heavy);
    }

    // A camp keeps its riding herd - a horse for every two of its people - and trades only the rest.
    private static int SpareHorses(BandLedger b) => Mathf.Max(0, b.Herd.Free - Mathf.Max(2, b.Mouths / 2));

    // What the far camp has to pay with: furs and robes, then horses it can spare.
    private static float Means(BandLedger far)
    {
        float mb = 0f;
        foreach (var (id, n) in StandardOfTrade.FursFor(far)) mb += n * Items.Items.Of(id).ValueMb;
        return mb + SpareHorses(far) * Herd.ValueMb;
    }

    private static void Meet(BandLedger far, Partner p, GameCalendar cal)
    {
        var mid = p.Mid;
        float means = Means(far), owed = 0f;
        // What the meeting party can carry out to them.
        int men = Mathf.Clamp(mid.Mouths / 6, 2, 8);
        float lbs = men * 60f + Mathf.Max(0, mid.Herd.Free - 2) * 75f;
        var sold = new List<string>();
        var deals = new List<(ItemId Id, int N)>();
        float gunMarkup = 0f;
        foreach (var (id, shortBy, weight) in Wants.Deficits(far))
        {
            if (weight < 0.05f) continue;
            int avail = Mathf.Min(Stock(mid, id), mid.Stores.Count(id));
            if (avail <= 0) continue;
            float price = Cost(mid, id) * Markup(mid, far, id);
            float pieceLbs = Mathf.Max(0.1f, Items.Items.Of(id).WeightLbs);
            int n = Mathf.Min(Mathf.Min(shortBy, avail), Mathf.FloorToInt(lbs / pieceLbs));
            n = Mathf.Min(n, Mathf.FloorToInt((means - owed) / price));
            if (n <= 0) continue;
            mid.Stores.Remove(id, n);
            far.Stores.Add(id, n);
            _stock[mid][id] -= n;
            if (!_secondHand.TryGetValue(far, out var used)) _secondHand[far] = used = new();
            used[id] = used.GetValueOrDefault(id) + n;
            Wants.OnTraded(far, id, n);
            owed += n * price;
            lbs -= n * pieceLbs;
            deals.Add((id, n));
            sold.Add($"{n} {Items.Items.Of(id).Name.ToLower()}");
            if (id == ItemId.Musket) gunMarkup = price / Mathf.Max(0.1f, Items.Items.Of(id).ValueMb);
        }
        p.Last = Plains.Today;
        // What they still lack is what the middleman will buy for next time.
        p.Wanted.Clear();
        foreach (var (id, shortBy, _) in Wants.Deficits(far)) p.Wanted[id] = shortBy;
        if (deals.Count == 0)
        {
            Saw(far, p);
            Log(cal, $"{mid.Name} met {far.Name} with nothing they wanted, or nothing they could pay for (they have {means:0} MB to trade)");
            return;
        }
        if (gunMarkup > 0f) p.Markup = gunMarkup;
        string paid = Pay(far, mid, owed);
        Saw(far, p);
        Log(cal, $"{mid.Name} met {far.Name}: {string.Join(", ", sold)} for {paid} ({owed:0} MB{(gunMarkup > 0f ? $"; a gun at {gunMarkup * Items.Items.Of(ItemId.Musket).ValueMb:0} MB" : "")})");
        Chronicle.Event($"{mid.Name} met {far.Name} and traded them {string.Join(", ", sold)}, used, for {paid}.", IsoGrid.ToWorld(far.Camp), subject: mid.Name, weight: 0.6f);
        if (Plains.NearPlayer(IsoGrid.ToWorld(far.Camp), 12000f) || Plains.NearPlayer(IsoGrid.ToWorld(mid.Camp), 12000f))
            Sim.Notice($"{mid.Name} have been to meet {far.Name}, and come home with {paid} for {string.Join(", ", sold)} they had used a season.");
    }

    // Each side goes home knowing how the other stands today.
    private static void Saw(BandLedger far, Partner p)
    {
        var mid = p.Mid;
        p.FarSeenAt = far.Camp; p.MidSeenAt = mid.Camp; p.SeenDay = Plains.Today;
        p.FarSeenMeans = Means(far);
        // What the far camp saw in the middleman's hands that it still lacks.
        float held = 0f;
        foreach (var (id, shortBy, _) in Wants.Deficits(far))
            held += Mathf.Min(shortBy, Mathf.Min(Stock(mid, id), mid.Stores.Count(id))) * Cost(mid, id) * Markup(mid, far, id);
        p.MidSeenStock = held;
    }

    // The far camp pays: the best furs first, robes, then horses.
    private static string Pay(BandLedger from, BandLedger to, float mb)
    {
        var parts = new List<string>();
        var furs = StandardOfTrade.FursFor(from);
        furs.Sort((x, y) => Items.Items.Of(y.Item1).ValueMb.CompareTo(Items.Items.Of(x.Item1).ValueMb));
        float left = mb;
        foreach (var (id, have) in furs)
        {
            if (left <= 0.05f) break;
            float v = Items.Items.Of(id).ValueMb;
            int n = Mathf.Min(have, Mathf.CeilToInt(left / v - 0.001f));
            if (n <= 0) continue;
            from.Stores.Remove(id, n);
            to.Stores.Add(id, n);
            left -= n * v;
            parts.Add(Items.Items.Lot(id, n));
        }
        if (left > 0.05f)
        {
            // A horse is not cut in pieces: one changes hands only for half a horse's worth or more owed.
            int h = Mathf.Min(SpareHorses(from), Mathf.FloorToInt(left / Herd.ValueMb + 0.5f));
            if (h > 0)
            {
                from.Herd.Lose(h);
                to.Herd.Horses += h;
                left -= h * Herd.ValueMb;
                parts.Add($"{h} horse{(h == 1 ? "" : "s")}");
            }
        }
        return parts.Count > 0 ? string.Join(", ", parts) : "promises";
    }

    private static void Log(GameCalendar cal, string text) { if (Plains.Log) GD.Print($"[middlemen] {cal.DateString} {text}"); }

    // ---- saving --------------------------------------------------------------------------------

    private static Godot.Collections.Dictionary Counts(Dictionary<ItemId, int> m)
    {
        var d = new Godot.Collections.Dictionary();
        foreach (var (id, n) in m) d[id.ToString()] = n;
        return d;
    }

    private static void Fill(Dictionary<ItemId, int> into, Godot.Collections.Dictionary d)
    {
        foreach (var kv in d) if (System.Enum.TryParse(kv.Key.AsString(), out ItemId id)) into[id] = kv.Value.AsInt32();
    }

    public static Godot.Collections.Dictionary Save()
    {
        var partners = new Godot.Collections.Dictionary();
        foreach (var (far, p) in _partners)
            partners[far.Name] = new Godot.Collections.Dictionary
            {
                ["mid"] = p.Mid.Name, ["since"] = p.Since, ["last"] = p.Last, ["markup"] = p.Markup, ["wanted"] = Counts(p.Wanted),
                ["farAt"] = p.FarSeenAt, ["midAt"] = p.MidSeenAt, ["farMeans"] = p.FarSeenMeans, ["midStock"] = p.MidSeenStock, ["seen"] = p.SeenDay,
            };
        var journeys = new Godot.Collections.Array();
        foreach (var j in _journeys)
        {
            var party = new Godot.Collections.Array(); foreach (var n in j.Party) party.Add(n.DisplayName);
            journeys.Add(new Godot.Collections.Dictionary
            {
                ["far"] = j.Far.Name, ["midGoes"] = j.MidGoes, ["met"] = j.Met, ["toward"] = j.Toward,
                ["start"] = j.StartDay, ["arrive"] = j.ArriveDay, ["home"] = j.HomeDay, ["oneWay"] = j.OneWay, ["party"] = party,
            });
        }
        var stock = new Godot.Collections.Dictionary(); foreach (var (b, m) in _stock) stock[b.Name] = Counts(m);
        var used = new Godot.Collections.Dictionary(); foreach (var (b, m) in _secondHand) used[b.Name] = Counts(m);
        var lean = new Godot.Collections.Dictionary(); foreach (var (b, l) in _lean) lean[b.Name] = l;
        var at = new Godot.Collections.Dictionary(); foreach (var (b, post) in _boughtAt) if (post != null) at[b.Name] = post.Post.Name;
        return new Godot.Collections.Dictionary { ["partners"] = partners, ["stock"] = stock, ["used"] = used, ["lean"] = lean, ["at"] = at, ["journeys"] = journeys };
    }

    public static void Load(Godot.Collections.Dictionary d)
    {
        Reset();
        if (d == null) return;
        static BandLedger Camp(Variant name) => Camps.Named(name.AsString())?.Ledger;
        foreach (var kv in d["partners"].AsGodotDictionary())
        {
            var far = Camp(kv.Key);
            var e = kv.Value.AsGodotDictionary();
            var mid = Camp(e["mid"]);
            if (far == null || mid == null) continue;
            var p = new Partner { Mid = mid, Since = e["since"].AsInt32(), Last = e["last"].AsInt32(), Markup = e["markup"].AsSingle() };
            Fill(p.Wanted, e["wanted"].AsGodotDictionary());
            if (e.ContainsKey("seen"))
            {
                p.FarSeenAt = e["farAt"].AsVector2I(); p.MidSeenAt = e["midAt"].AsVector2I();
                p.FarSeenMeans = e["farMeans"].AsSingle(); p.MidSeenStock = e["midStock"].AsSingle(); p.SeenDay = e["seen"].AsInt32();
            }
            else { p.FarSeenAt = far.Camp; p.MidSeenAt = mid.Camp; p.FarSeenMeans = Means(far); p.SeenDay = Plains.Today; }
            _partners[far] = p;
        }
        if (d.ContainsKey("journeys"))
            foreach (var v in d["journeys"].AsGodotArray())
            {
                var e = v.AsGodotDictionary();
                var far = Camp(e["far"]);
                if (far == null || !_partners.TryGetValue(far, out var p)) continue;
                bool midGoes = e["midGoes"].AsBool();
                var j = new Journey
                {
                    Far = far, P = p, MidGoes = midGoes, Goer = midGoes ? p.Mid : far, Met = e["met"].AsBool(), Toward = e["toward"].AsVector2I(),
                    StartDay = e["start"].AsInt32(), ArriveDay = e["arrive"].AsInt32(), HomeDay = e["home"].AsInt32(), OneWay = e["oneWay"].AsInt32(),
                };
                foreach (var pn in e["party"].AsGodotArray())
                    foreach (var n in Sim.Npcs) if (n.DisplayName == pn.AsString() && !n.IsDead) { j.Party.Add(n); TradeTrips.TakeAway(n, $"gone to meet {j.Host.Name}"); break; }
                _journeys.Add(j);
            }
        foreach (var (key, map) in new[] { ("stock", _stock), ("used", _secondHand) })
            foreach (var kv in d[key].AsGodotDictionary())
                if (Camp(kv.Key) is { } b) { var m = new Dictionary<ItemId, int>(); Fill(m, kv.Value.AsGodotDictionary()); map[b] = m; }
        foreach (var kv in d["lean"].AsGodotDictionary()) if (Camp(kv.Key) is { } b) _lean[b] = kv.Value.AsSingle();
        foreach (var kv in d["at"].AsGodotDictionary()) if (Camp(kv.Key) is { } b && Posts.Named(kv.Value.AsString())?.Ledger is { } post) _boughtAt[b] = post;
    }
}

// A meeting party riding in to the other camp's lodges where a player can
// see it: the leader trades at the lodges, and they go home.
public class MeetVisitGoal : Goal
{
    private readonly Middlemen.Journey _j;
    private readonly bool _leader;
    private int _phase;
    public MeetVisitGoal(Middlemen.Journey j, bool leader) { _j = j; _leader = leader; }
    public override string Label => _phase < 2 ? $"ride in to {_j.Host.Name}'s lodges to trade" : "go home";

    public override GoalStatus Tick(Npc npc, float dt, float hours)
    {
        switch (_phase)
        {
            case 0:
                _phase = 1;
                return Delegate(new GoToGoal(_j.Host.AccessCell, 300f));
            case 1:
                if (!ChildFinished(out _)) return GoalStatus.Running;
                if (_leader)
                {
                    npc.Bark(_j.MidGoes ? "We have brought what you asked for. Have you furs?" : "We have furs. We hear you have guns.");
                    Middlemen.Arrive(_j, GameCalendar.Instance);
                }
                _phase = 2;
                return Delegate(new GoToGoal(_j.Goer.AccessCell, 300f));
            default:
                if (!ChildFinished(out _)) return GoalStatus.Running;
                return GoalStatus.Done;
        }
    }
}
