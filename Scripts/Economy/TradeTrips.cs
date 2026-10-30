using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Agents.Goals;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Items;
using IsometricFurTradeGame.Scripts.World;

namespace IsometricFurTradeGame.Scripts.Economy;

// A camp going in to trade (design doc 3.6.1, 18.2): two to four of its men
// take the furs to the house they have chosen, are gone the days the road
// takes - riding, if the camp has horses to spare - trade on the day they
// arrive, at what is on the shelves that day, and come home with the goods.
// Out of sight they are away, as a brigade's men are. If a player is near the
// post when they come in, they walk up to it from the trail and trade at the
// door (TradeVisitGoal), and go home on foot.
public class TradeTrip
{
    public BandLedger Camp;
    public TradingPostLedger Post;
    public int StartDay, ArriveDay, HomeDay;
    public bool Traded, Visiting;
    public readonly Inventory Load = new(), Goods = new();
    public readonly List<Npc> Party = new();
}

public static class TradeTrips
{
    public static readonly List<TradeTrip> All = new();
    public static void Reset() => All.Clear();
    public static bool Away(BandLedger b) { foreach (var t in All) if (t.Camp == b) return true; return false; }

    // Set out, if the camp has furs worth the walk and men to carry them.
    public static bool Start(BandLedger b, TradingPostLedger post, GameCalendar cal)
    {
        if (Away(b)) return false;
        var furs = StandardOfTrade.FursFor(b);
        var food = StandardOfTrade.ProvisionsFor(b);
        if (furs.Count == 0 && food.Count == 0) return false;
        var t = new TradeTrip { Camp = b, Post = post };
        foreach (var n in Sim.Npcs)
        {
            if (t.Party.Count >= Mathf.Clamp(b.Mouths / 4, 2, 4)) break;
            if (n.HomeStores != b || n.IsDead || n.Away || n.IsFemale || n.HeldBy != null || n.Role is not (Role.Hunter or Role.Chief)) continue;
            // A man a player is watching sets out on foot; the rest are simply gone.
            if (n.Tier == Tier.Live) { if (n.Brain.Has<BuffaloRunGoal>() || n.Brain.Has<HorseRaidGoal>()) continue; n.Brain.Clear(n); n.Brain.Push(n, new GoToGoal(post.Post.GateOutside, 200f)); }
            else TakeAway(n, t);
            t.Party.Add(n);
        }
        if (t.Party.Count == 0) return false;
        foreach (var (id, n) in furs) { b.Stores.Remove(id, n); t.Load.Add(id, n); }
        // Provisions go as far as the carts, the horses and the men's backs will carry.
        float lbs = b.Herd.Carts * 800f + b.Herd.Free * 150f + t.Party.Count * 60f - t.Load.TotalWeightLbs;
        foreach (var (id, n) in food)
        {
            int k = Mathf.Min(n, Mathf.FloorToInt(lbs / Mathf.Max(0.1f, Items.Items.Of(id).WeightLbs)));
            if (k <= 0) continue;
            b.Stores.Remove(id, k); t.Load.Add(id, k);
            lbs -= k * Items.Items.Of(id).WeightLbs;
        }
        float km = IsoGrid.ToWorld(b.Camp).DistanceTo(IsoGrid.ToWorld(post.AccessCell)) / WorldMap.PxPerKm;
        bool riding = b.Herd.Free >= t.Party.Count;
        int oneWay = Mathf.Max(1, Mathf.CeilToInt(km / (riding ? 45f : 25f)));
        t.StartDay = Plains.Today;
        t.ArriveDay = Plains.Today + oneWay;
        t.HomeDay = t.ArriveDay + 1 + oneWay;
        All.Add(t);
        if (Plains.Log) GD.Print($"[trade] {cal.DateString} {t.Party.Count} men of {b.Name} set out for {post.Post.Name} ({km:0} km, {oneWay} days {(riding ? "riding" : "afoot")}) with {StandardOfTrade.Worth(t.Load):0} MB of furs");
        return true;
    }

    // Where the party is on the road now, as the days have gone (for the observer).
    public static Vector2 Where(TradeTrip t)
    {
        foreach (var n in t.Party) if (n != null && GodotObject.IsInstanceValid(n) && n.Visible && !n.Away) return n.GlobalPosition;
        var camp = IsoGrid.ToWorld(t.Camp.Camp); var post = IsoGrid.ToWorld(t.Post.Post.GateOutside);
        float day = Plains.Today + (GameCalendar.Instance?.Hour ?? 12f) / 24f;
        if (!t.Traded) return camp.Lerp(post, Mathf.Clamp((day - t.StartDay) / Mathf.Max(1f, t.ArriveDay - t.StartDay), 0f, 1f));
        return post.Lerp(camp, Mathf.Clamp((day - t.ArriveDay - 1f) / Mathf.Max(1f, t.HomeDay - t.ArriveDay - 1f), 0f, 1f));
    }

    // Once a day.
    public static void Day(GameCalendar cal)
    {
        int today = Plains.Today;
        for (int i = All.Count - 1; i >= 0; i--)
        {
            var t = All[i];
            t.Party.RemoveAll(n => n == null || !GodotObject.IsInstanceValid(n) || n.IsDead);
            // A man who set out under a player's eye is away once nobody is watching him.
            foreach (var n in t.Party) if (!n.Away && n.Tier == Tier.Background && !t.Visiting) TakeAway(n, t);
            if (t.Party.Count == 0 || t.Post == null || !GodotObject.IsInstanceValid(t.Post))
            {
                // Nobody came home: the furs went with them.
                All.RemoveAt(i);
                continue;
            }
            if (!t.Traded && today >= t.ArriveDay)
            {
                var gate = IsoGrid.ToWorld(t.Post.Post.GateOutside);
                if (Plains.NearPlayer(gate, Tiers.LiveRadius)) Visit(t);
                else { StandardOfTrade.Trade(t.Post, t.Camp, t.Load, t.Goods, cal); t.Traded = true; }
            }
            // Came in but never reached the door (turned back, held, killed on the way): it is done in numbers.
            if (t.Visiting && !t.Traded && today > t.ArriveDay + 3) TradeAtDoor(t);
            if (t.Traded && !t.Visiting && today >= t.HomeDay) Home(t, i, cal);
        }
    }

    // They come in from the trail where a player can see them.
    private static void Visit(TradeTrip t)
    {
        t.Visiting = true;
        var gate = IsoGrid.ToWorld(t.Post.Post.GateOutside);
        var from = (IsoGrid.ToWorld(t.Camp.Camp) - gate).Normalized();
        int k = 0;
        foreach (var n in t.Party)
        {
            if (n.Away) PutBack(n, gate + from * 900f + new Vector2(k * 60, k * 30));
            n.Tier = Tier.Live; n.ProcessMode = Node.ProcessModeEnum.Inherit; n.Visible = true;
            Net.Net.I?.AnnounceTier(n, false);
            n.Brain.Clear(n);
            n.Brain.Push(n, new TradeVisitGoal(t, k == 0));
            k++;
        }
        Sim.Notice($"Men of {t.Camp.Name} are coming in to {t.Post.Post.Name} with furs to trade.");
    }

    // The leader of the party has reached the door.
    public static void TradeAtDoor(TradeTrip t)
    {
        if (t.Traded) return;
        StandardOfTrade.Trade(t.Post, t.Camp, t.Load, t.Goods, GameCalendar.Instance);
        t.Traded = true;
        t.Visiting = false;
        int today = Plains.Today;
        t.HomeDay = today + Mathf.Max(1, t.HomeDay - t.ArriveDay - 1);
    }

    private static void Home(TradeTrip t, int index, GameCalendar cal)
    {
        foreach (var (id, n) in new List<(ItemId, int)>(t.Goods.All())) { t.Camp.Stores.Add(id, n); Wants.OnTraded(t.Camp, id, n); }
        foreach (var (id, n) in new List<(ItemId, int)>(t.Load.All())) t.Camp.Stores.Add(id, n);
        foreach (var n in t.Party) if (n.Away) PutBack(n, IsoGrid.ToWorld(n.StandCellNear(t.Camp.AccessCell)));
        All.RemoveAt(index);
        if (Plains.Log) GD.Print($"[trade] {cal.DateString} {t.Camp.Name}'s men are home from {t.Post.Post.Name}");
    }

    private static void TakeAway(Npc n, TradeTrip t) => TakeAway(n, $"trading at {t.Post.Post.Name}");

    // Off the map on an errand (a trading party, a meeting with another camp's men).
    public static void TakeAway(Npc n, string with)
    {
        n.Away = true;
        n.AwayWith = with;
        n.Tier = Tier.Background;
        n.Brain.Clear(n);
        n.StopMoving();
        n.Velocity = Vector2.Zero;
        n.Visible = false;
        n.ProcessMode = Node.ProcessModeEnum.Disabled;
        Net.Net.I?.AnnounceTier(n, true);
    }

    public static void PutBack(Npc n, Vector2 at)
    {
        n.Away = false;
        n.AwayWith = "";
        n.GlobalPosition = at;
        n.Visible = true;
        n.ProcessMode = Node.ProcessModeEnum.Inherit;
        Net.Net.I?.AnnounceTier(n, false);
    }

    // ---- saving: the furs and goods on the road, and who carries them ----

    public static Godot.Collections.Array Save()
    {
        var a = new Godot.Collections.Array();
        foreach (var t in All)
        {
            var party = new Godot.Collections.Array(); foreach (var n in t.Party) party.Add(n.DisplayName);
            a.Add(new Godot.Collections.Dictionary
            {
                ["camp"] = t.Camp.Name, ["post"] = t.Post.Post.Name, ["arrive"] = t.ArriveDay, ["home"] = t.HomeDay, ["traded"] = t.Traded,
                ["load"] = Pack(t.Load), ["goods"] = Pack(t.Goods), ["party"] = party,
            });
        }
        return a;
    }

    public static void Load(Godot.Collections.Array a)
    {
        All.Clear();
        if (a == null) return;
        foreach (var v in a)
        {
            var d = v.AsGodotDictionary();
            var camp = Camps.Named(d["camp"].AsString())?.Ledger;
            var post = Posts.Named(d["post"].AsString())?.Ledger;
            if (camp == null || post == null) continue;
            var t = new TradeTrip { Camp = camp, Post = post, ArriveDay = d["arrive"].AsInt32(), HomeDay = d["home"].AsInt32(), Traded = d["traded"].AsBool() };
            Unpack(t.Load, d["load"].AsGodotDictionary()); Unpack(t.Goods, d["goods"].AsGodotDictionary());
            foreach (var pn in d["party"].AsGodotArray())
                foreach (var n in Sim.Npcs) if (n.DisplayName == pn.AsString() && !n.IsDead) { t.Party.Add(n); TakeAway(n, t); break; }
            All.Add(t);
        }
    }

    private static Godot.Collections.Dictionary Pack(Inventory inv)
    {
        var d = new Godot.Collections.Dictionary();
        foreach (var (id, n) in inv.All()) d[id.ToString()] = n;
        return d;
    }

    private static void Unpack(Inventory inv, Godot.Collections.Dictionary d)
    {
        foreach (var kv in d) if (System.Enum.TryParse(kv.Key.AsString(), out ItemId id)) inv.Add(id, kv.Value.AsInt32());
    }
}

// A trading party's leader walking up to the post's door, and the rest with
// him; at the door the trade is made, and then they go home.
public class TradeVisitGoal : Goal
{
    private readonly TradeTrip _trip;
    private readonly bool _leader;
    private int _phase;
    public TradeVisitGoal(TradeTrip trip, bool leader) { _trip = trip; _leader = leader; }
    public override string Label => _phase < 2 ? $"come in to {_trip.Post.Post.Name} to trade" : "go home with the goods";

    public override GoalStatus Tick(Npc npc, float dt, float hours)
    {
        switch (_phase)
        {
            case 0:
                _phase = 1;
                return Delegate(new GoToGoal(_trip.Post.Post.GateOutside, 160f));
            case 1:
                if (!ChildFinished(out _)) return GoalStatus.Running;
                if (_leader) { npc.Bark($"We have furs. What have you got for us?"); TradeTrips.TradeAtDoor(_trip); }
                _phase = 2;
                return Delegate(new GoToGoal(_trip.Camp.AccessCell, 300f));
            default:
                if (!ChildFinished(out _)) return GoalStatus.Running;
                return GoalStatus.Done;
        }
    }
}
