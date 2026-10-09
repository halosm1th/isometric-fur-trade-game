using Godot;
using IsometricFurTradeGame.Scripts.Items;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Ecology;
using IsometricFurTradeGame.Scripts.World;

namespace IsometricFurTradeGame.Scripts.Agents.Goals;

// Walk the river road (design doc 18.3 in its simplest form): waypoint to
// waypoint, in whichever direction the destination lies, then to the door.
public class RoadGoal : Goal
{
    private readonly Vector2I _dest;
    private System.Collections.Generic.List<Vector2I> _legs;
    private int _leg = -1;
    public RoadGoal(Vector2I dest) { _dest = dest; }
    public override string Label => $"take the road to {_dest}";

    public override GoalStatus Tick(Npc npc, float dt, float hours)
    {
        if (_legs == null)
        {
            var road = WorldLayout.RiverRoad;
            var destW = IsoGrid.ToWorld(_dest);
            bool south = destW.DistanceTo(road[^1]) < destW.DistanceTo(road[0]);
            // Start from the nearest waypoint ahead of us.
            int nearest = 0; float best = float.MaxValue;
            for (int i = 0; i < road.Length; i++) { float d = npc.GlobalPosition.DistanceTo(road[i]); if (d < best) { best = d; nearest = i; } }
            _legs = new System.Collections.Generic.List<Vector2I>();
            if (south) for (int i = nearest; i < road.Length; i++) _legs.Add(IsoGrid.FromWorld(road[i]));
            else for (int i = nearest; i >= 0; i--) _legs.Add(IsoGrid.FromWorld(road[i]));
            _legs.Add(_dest);
            _leg = 0;
            return Delegate(new GoToGoal(_legs[0], 220f));
        }
        if (!ChildFinished(out var r)) return GoalStatus.Running;
        if (r != GoalStatus.Done && _leg < _legs.Count - 1) { /* a leg failed: press on to the next */ }
        _leg++;
        if (_leg >= _legs.Count) return GoalStatus.Done;
        return Delegate(new GoToGoal(_legs[_leg], _leg == _legs.Count - 1 ? 110f : 220f));
    }
}

// Carry the post's furs and its report down to the Bay (or the outfit up),
// hand over, report, and walk back (design doc 3.9 step 6, 10.1.2's
// second leg). Everything is on the man: kill him on the road and it's his.
public class CourierGoal : Goal
{
    private readonly Economy.FortLedger _from, _to;
    private readonly Message _packet;
    private readonly bool _carryFurs;
    private readonly System.Collections.Generic.List<(ItemId id, int n)> _cargo;
    private int _phase;
    public CourierGoal(Economy.FortLedger from, Economy.FortLedger to, Message packet, bool carryFurs, System.Collections.Generic.List<(ItemId, int)> cargo = null)
    { _from = from; _to = to; _packet = packet; _carryFurs = carryFurs; _cargo = cargo; }
    public override string Label => _phase < 3 ? $"carry the post to {_to.Title}" : "walk home";

    public override GoalStatus Tick(Npc npc, float dt, float hours)
    {
        switch (_phase)
        {
            case 0:
                if (_packet != null) npc.Mail.Add(_packet);
                if (_carryFurs) foreach (var id in new[] { ItemId.BeaverPelt, ItemId.ThinPelt, ItemId.ShotPelt, ItemId.CaribouHide, ItemId.WolfPelt, ItemId.ShotHide }) npc.Inventory.Add(id, _from.Stores.TakeUpTo(id, 30 - npc.Inventory.CountOfKind(ItemKind.Fur)));
                if (_cargo != null) foreach (var (id, n) in _cargo) npc.Inventory.Add(id, _from.Stores.TakeUpTo(id, n));
                if (!npc.Inventory.Has(ItemId.Musket) && _from.Stores.Remove(ItemId.Musket)) npc.Inventory.Add(ItemId.Musket);
                npc.Inventory.Add(ItemId.Pemmican, _from.Stores.TakeUpTo(ItemId.Pemmican, 3));
                _phase = 1;
                Sim.Notice($"{npc.DisplayName} sets out on the river road for {_to.Title}{(_carryFurs ? " with the furs" : "")}{(_packet != null ? " and the post" : "")}.");
                return Delegate(new RoadGoal(_to.AccessCell));
            case 1:
            {
                if (!ChildFinished(out var r)) return GoalStatus.Running;
                if (npc.GlobalPosition.DistanceTo(IsoGrid.ToWorld(_to.AccessCell)) > 400f) { _phase = 3; return Delegate(new RoadGoal(_from.AccessCell)); }
                // Deliver: furs and goods into the stores, the packet to whoever is master here.
                int lots = 0;
                foreach (var (id, n) in new System.Collections.Generic.List<(ItemId, int)>(npc.Inventory.All()))
                {
                    var kind = Items.Items.Of(id).Kind;
                    if (kind != ItemKind.Fur && kind != ItemKind.Goods && !(kind == ItemKind.Food && n > 1 && _cargo != null)) continue;
                    int give = kind == ItemKind.Food ? n - 1 : n;
                    npc.Inventory.Remove(id, give); _to.Stores.Add(id, give); lots += give;
                }
                var master = _to.Leader != null && GodotObject.IsInstanceValid(_to.Leader) && !_to.Leader.IsDead ? _to.Leader : Delegation.LeaderOf(npc);
                foreach (var m in new System.Collections.Generic.List<Message>(npc.Mail)) { if (m.Read) continue; npc.Mail.Remove(m); if (master != null) Mail.Give(m, master); else _to.Mailbox.Add(m); }
                Sim.Notice($"{npc.DisplayName} comes in to {_to.Title} with {lots} lots{(master != null ? $" and hands the packet to {master.DisplayName}" : "")}.");
                Chronicle.Event($"{npc.DisplayName} came down the river road from {_from.Title}.", npc.GlobalPosition, npc, weight: 0.6f);
                _phase = 2;
                return Delegate(new WaitGoal(1.5f));
            }
            case 2:
                if (!ChildFinished(out _)) return GoalStatus.Running;
                _phase = 3;
                // A night's rest then home; the way back is the same road.
                return Delegate(new RoadGoal(_from.AccessCell));
            default:
                if (!ChildFinished(out _)) return GoalStatus.Running;
                npc.PendingReport = $"back from {_to.Title}";
                return GoalStatus.Done;
        }
    }
}

// Lie up beside the river road and take what the Company sends along it
// (design doc 10.3, 12.2's chokepoints): the packet, the furs, the man if
// he fights. Then home with it, and the partner reads what he shouldn't.
public class AmbushGoal : Goal
{
    private readonly Vector2I _where;
    private float _hoursLeft;
    private int _phase;
    private Npc _mark;
    private bool _took;
    public AmbushGoal(Vector2I where, float hours) { _where = where; _hoursLeft = hours; }
    public override string Label => _phase == 0 ? "go to the river road" : _phase == 1 ? "lie up by the river road" : "home with the take";

    public override void OnEnter(Npc npc) { npc.Raiding = true; npc.RaidTarget = Faction.HBC; npc.RaidTimer = 0f; }
    public override void OnExit(Npc npc) { npc.Raiding = false; npc.StopMoving(); }

    public override GoalStatus Tick(Npc npc, float dt, float hours)
    {
        switch (_phase)
        {
            case 0:
                _phase = 1;
                return Delegate(new RoadGoal(_where));
            case 1:
            {
                if (!ChildFinished(out _)) return GoalStatus.Running;
                _hoursLeft -= hours;
                if (_mark != null && GodotObject.IsInstanceValid(_mark) && _mark.IsDead && npc.GlobalPosition.DistanceTo(_mark.GlobalPosition) < 200f)
                {
                    // Strip the body: papers and furs.
                    npc.Mail.AddRange(_mark.Mail); _mark.Mail.Clear();
                    foreach (var (id, n) in new System.Collections.Generic.List<(ItemId, int)>(_mark.Inventory.All()))
                        if (Items.Items.Of(id).Kind == ItemKind.Fur || Items.Items.Of(id).Kind == ItemKind.Goods) { _mark.Inventory.Remove(id, n); npc.Inventory.Add(id, n); }
                    _took = true;
                    Chronicle.Event($"{npc.DisplayName} stripped {_mark.DisplayName}'s body on the river road.", npc.GlobalPosition, npc, weight: 2.5f);
                    _phase = 2;
                    return Delegate(new RoadGoal(npc.HomeStores?.AccessCell ?? npc.BedCell));
                }
                if (_hoursLeft <= 0f || npc.Needs.Warmth < 30f || npc.Needs.Hunger < 25f) { _phase = 2; return Delegate(new RoadGoal(npc.HomeStores?.AccessCell ?? npc.BedCell)); }
                // Anyone of the Company on the road with papers or furs.
                foreach (var a in Sim.AgentsWithin(npc.GlobalPosition, 520f))
                {
                    if (a is Npc o && o.Faction == Faction.HBC && !o.IsDead && (o.Mail.Count > 0 || o.Inventory.CountOfKind(ItemKind.Fur) >= 4))
                    {
                        _mark = o;
                        Chronicle.Event($"{npc.DisplayName} fell on {o.DisplayName} on the river road.", npc.GlobalPosition, npc, o.DisplayName, 2f);
                        return Delegate(new AttackGoal(o));
                    }
                }
                if (npc.Arrived || npc.MoveFailed) npc.SetDestination(new Vector2I(_where.X + GD.RandRange(-2, 2), _where.Y + GD.RandRange(-3, 3)));
                return GoalStatus.Running;
            }
            default:
            {
                if (!ChildFinished(out _)) return GoalStatus.Running;
                if (npc.HomeStores != null)
                {
                    foreach (var (id, n) in new System.Collections.Generic.List<(ItemId, int)>(npc.Inventory.All()))
                        if (Items.Items.Of(id).Kind == ItemKind.Fur || Items.Items.Of(id).Kind == ItemKind.Goods) { npc.Inventory.Remove(id, n); npc.HomeStores.Stores.Add(id, n); }
                    var master = npc.HomeStores.Leader;
                    foreach (var m in new System.Collections.Generic.List<Message>(npc.Mail)) { npc.Mail.Remove(m); if (master != null && !master.IsDead) { master.Mail.Add(m); Mail.Open(m, master); } }
                }
                if (_took) { Economy.Rivalry.Raise(10f, "the Company's packet taken on the road"); Sim.Notice($"{npc.DisplayName} is back across the river with what he took on the road."); }
                npc.PendingReport = _took ? "took the Company's packet on the road" : "saw nothing on the road";
                return GoalStatus.Done;
            }
        }
    }
}

// Marched back to the other company's post and kept there a few days
// (design doc 9.1). Nobody troubles a prisoner; then they let him walk.
public class HeldPrisonerGoal : Goal
{
    public override int Tier => 0;
    private readonly Economy.FortLedger _post;
    private int _phase;
    public HeldPrisonerGoal(Economy.FortLedger post) { _post = post; }
    public override string Label => $"held at {_post.Title}";

    public override GoalStatus Tick(Npc npc, float dt, float hours)
    {
        if (npc.HeldBy == null) return GoalStatus.Done;
        var cal = GameCalendar.Instance;
        if (cal != null && cal.Day >= npc.HeldUntilDay)
        {
            var held = npc.HeldBy;
            npc.HeldBy = null;
            npc.Inventory.Add(ItemId.Pemmican, 2);
            Sim.Notice($"{npc.DisplayName} has been let go by the {(held.Company == Faction.NWC ? "Nor'Westers" : "Company")} and is walking home.");
            Chronicle.Event($"{npc.DisplayName} was released and walked home.", npc.GlobalPosition, npc, weight: 0.8f);
            return GoalStatus.Done;
        }
        switch (_phase)
        {
            case 0:
                _phase = 1;
                return Delegate(new GoToGoal(_post.AccessCell, 150f));
            default:
                if (!ChildFinished(out _)) { return GoalStatus.Running; }
                // Sit out the days by their fire.
                if (npc.Arrived || npc.MoveFailed) npc.SetDestination(new Vector2I(_post.AccessCell.X + GD.RandRange(-1, 1), _post.AccessCell.Y + GD.RandRange(-2, 2)));
                return GoalStatus.Running;
        }
    }
}

// Walk across and ask for your man back (design doc 9.1): if the house
// has the odds they say no and it comes to blows; if not, they hand him over.
public class RescueGoal : Goal
{
    private readonly Npc _held;
    private readonly Economy.FortLedger _post;
    private int _phase;
    public RescueGoal(Npc held, Economy.FortLedger post) { _held = held; _post = post; }
    public override string Label => $"fetch {_held?.DisplayName} back";

    public override GoalStatus Tick(Npc npc, float dt, float hours)
    {
        if (_held == null || !GodotObject.IsInstanceValid(_held) || _held.HeldBy == null) return GoalStatus.Done;
        switch (_phase)
        {
            case 0:
                _phase = 1;
                npc.Raiding = true; npc.RaidTarget = _post.Company;
                return Delegate(new GoToGoal(_post.AccessCell, 160f));
            default:
            {
                if (!ChildFinished(out _)) return GoalStatus.Running;
                npc.Raiding = false;
                if (_held.HeldBy == null) return GoalStatus.Done;
                bool theyHaveOdds = Economy.Rivalry.HouseHasOdds(_post.Company, npc.GlobalPosition);
                if (theyHaveOdds && GD.Randf() < 0.6f)
                {
                    Sim.Notice($"{_post.Title} will not give {_held.DisplayName} up. {npc.DisplayName} comes away empty-handed.");
                    Economy.Rivalry.Raise(5f, "a man refused back");
                    return GoalStatus.Failed;
                }
                _held.HeldBy = null; _held.HeldUntilDay = -1;
                _held.Brain.Clear(_held);
                _held.Brain.Push(_held, new GoToGoal(npc.HomeStores?.AccessCell ?? npc.BedCell, 140f));
                Sim.Notice($"{npc.DisplayName} has brought {_held.DisplayName} home from {_post.Title}.");
                Chronicle.Event($"{npc.DisplayName} fetched {_held.DisplayName} back from {_post.Title}.", npc.GlobalPosition, npc, weight: 1.5f);
                return GoalStatus.Done;
            }
        }
    }

    public override void OnExit(Npc npc) { npc.Raiding = false; npc.StopMoving(); }
}

// Sit down at your desk and open what's addressed to you (design doc
// 10.1.1: the read event). Half an hour with the packet.
public class ReadMailGoal : Goal
{
    private int _phase;
    public override string Label => "read the post";

    public override GoalStatus Tick(Npc npc, float dt, float hours)
    {
        if (npc.HomeStores != null && npc.HomeStores.Mailbox.Count > 0 && npc.HomeStores.Leader == npc) { npc.Mail.AddRange(npc.HomeStores.Mailbox); npc.HomeStores.Mailbox.Clear(); }
        bool any = false;
        foreach (var m in npc.Mail) if (!m.Read && (m.To == npc.DisplayName || m.To == Mail.RoleName(npc))) any = true;
        if (!any) return GoalStatus.Done;
        switch (_phase)
        {
            case 0:
                _phase = 1;
                return Delegate(new GoToGoal(npc.WorkCell, 90f));
            case 1:
                if (!ChildFinished(out _)) return GoalStatus.Running;
                _phase = 2;
                return Delegate(new WaitGoal(0.5f));
            default:
                if (!ChildFinished(out _)) return GoalStatus.Running;
                foreach (var m in new System.Collections.Generic.List<Message>(npc.Mail))
                    if (!m.Read && (m.To == npc.DisplayName || m.To == Mail.RoleName(npc))) Mail.Open(m, npc);
                return GoalStatus.Done;
        }
    }
}

// Carry what you hold to whoever it's for. If they're dead, the packet
// goes to whoever holds their office now; if nobody does, you keep it.
public class DeliverMailGoal : Goal
{
    private int _phase;
    private Npc _to;
    private float _retry;
    public override string Label => $"carry the post to {_to?.DisplayName ?? "the post"}";

    public override GoalStatus Tick(Npc npc, float dt, float hours)
    {
        Message carrying = null;
        foreach (var m in npc.Mail) if (!m.Read && m.To != npc.DisplayName) { carrying = m; break; }
        if (carrying == null) return GoalStatus.Done;
        if (_to == null || !GodotObject.IsInstanceValid(_to) || _to.IsDead)
        {
            _to = Mail.Addressee(carrying);
            if (_to == null) { _retry -= hours; return _retry <= 0f ? GoalStatus.Failed : GoalStatus.Running; }
            _phase = 0;
        }
        switch (_phase)
        {
            case 0:
                _phase = 1;
                return Delegate(new GoToGoal(_to.Cell, 120f));
            default:
                if (!ChildFinished(out var r)) return GoalStatus.Running;
                if (npc.GlobalPosition.DistanceTo(_to.GlobalPosition) > 200f) { _phase = 0; return GoalStatus.Running; }
                foreach (var m in new System.Collections.Generic.List<Message>(npc.Mail))
                {
                    if (m.Read || m.To == npc.DisplayName) continue;
                    npc.Mail.Remove(m);
                    Mail.Give(m, _to);
                }
                Sim.Notice($"{npc.DisplayName} hands the packet to {_to.DisplayName}.");
                Chronicle.Event($"{npc.DisplayName} delivered a packet to {_to.DisplayName}.", npc.GlobalPosition, npc, weight: 0.5f);
                return GoalStatus.Done;
        }
    }
}

// Tell the leader how it went (design doc 3.9 step 6): reports travel by
// mouth, so the Factor's picture is whatever his men have told him.
public class ReportGoal : Goal
{
    private readonly string _report;
    private readonly string _site;
    private readonly Vector2I? _siteCell;
    private int _phase;
    private float _patience = 6f;
    public ReportGoal(string report, string site = null, Vector2I? siteCell = null) { _report = report; _site = site; _siteCell = siteCell; }
    public override string Label => "report to the leader";

    public override GoalStatus Tick(Npc npc, float dt, float hours)
    {
        var leader = Delegation.LeaderOf(npc);
        if (leader == null || leader == npc) return GoalStatus.Done;
        _patience -= hours;
        if (_patience <= 0f) return GoalStatus.Done;   // he'll hear it in the morning, or not
        switch (_phase)
        {
            case 0:
                _phase = 1;
                return Delegate(new GoToGoal(leader.Cell, 130f));
            default:
                if (!ChildFinished(out var r)) return GoalStatus.Running;
                if (npc.GlobalPosition.DistanceTo(leader.GlobalPosition) > 220f) { if (r == GoalStatus.Failed) return GoalStatus.Failed; _phase = 0; return GoalStatus.Running; }
                Delegation.Receive(leader, npc, _report, _site, _siteCell);
                return GoalStatus.Done;
        }
    }
}

// Go and see what became of a man who hasn't come back (design doc 3.9's
// overdue report). You find him, or his body, or nothing.
public class LookForGoal : Goal
{
    private readonly Npc _missing;
    private readonly Vector2I _where;
    private int _phase;
    private float _searched;
    public LookForGoal(Npc missing, Vector2I where) { _missing = missing; _where = where; }
    public override string Label => $"look for {_missing?.DisplayName}";

    public override GoalStatus Tick(Npc npc, float dt, float hours)
    {
        switch (_phase)
        {
            case 0:
                _phase = 1;
                if (npc.GlobalPosition.DistanceTo(IsoGrid.ToWorld(_where)) > 3500f) return Delegate(new RoadGoal(_where));
                return Delegate(new GoToGoal(_where, 300f));
            case 1:
            {
                if (!ChildFinished(out _)) return GoalStatus.Running;
                _searched += hours;
                bool valid = _missing != null && GodotObject.IsInstanceValid(_missing);
                if (valid && npc.GlobalPosition.DistanceTo(_missing.GlobalPosition) < 600f)
                {
                    string news = _missing.IsDead ? $"found {_missing.DisplayName} dead" : $"found {_missing.DisplayName} alive and well";
                    if (_missing.IsDead && _missing.Role == Role.Postmaster && Sim.Fort != null) Sim.Fort.KnowsOutpostMasterless = true;
                    Chronicle.Event($"{npc.DisplayName} {news} out by {_where}.", npc.GlobalPosition, npc, _missing.DisplayName, 1.5f);
                    _phase = 2;
                    return Delegate(new ReportGoal(news, null, null));
                }
                if (_searched > 3f || GameCalendar.Instance.Hour >= 16f)
                {
                    _phase = 2;
                    return Delegate(new ReportGoal($"could find no sign of {_missing?.DisplayName ?? "him"}", null, null));
                }
                if (npc.Arrived || npc.MoveFailed) npc.SetDestination(new Vector2I(_where.X + GD.RandRange(-5, 5), _where.Y + GD.RandRange(-10, 10)));
                return GoalStatus.Running;
            }
            default:
                if (!ChildFinished(out _)) return GoalStatus.Running;
                return GoalStatus.Done;
        }
    }
}

// A new hand's first order: report to the Factor (design doc 5.5). Whether
// he goes straight there is his personality's business.
public class ReportForDutyGoal : Goal
{
    private int _phase;
    public override string Label => "report for duty";

    public override GoalStatus Tick(Npc npc, float dt, float hours)
    {
        var leader = Delegation.LeaderOf(npc);
        if (leader == null) return GoalStatus.Done;
        switch (_phase)
        {
            case 0:
            {
                _phase = 1;
                // Detours first: the thirsty find the rum, the sociable the men's house.
                if (npc.Personality.Loyalty < 0.35f && npc.HomeStores != null && (npc.HomeStores.Stores.Has(Items.ItemId.Rum) || npc.HomeStores.Stores.Has(Items.ItemId.Ale)))
                { Sim.Notice($"{npc.DisplayName}, just landed, goes looking for the rum before he goes looking for the Factor."); return Delegate(new CarouseGoal()); }
                if (npc.Personality.Talkative > 0.65f && npc.Personality.Loyalty < 0.6f)
                { Sim.Notice($"{npc.DisplayName} drifts into the men's house to see who's who before reporting."); return Delegate(new WanderGoal(WorldLayout.MensHouse.Centre, 2, 1.5f)); }
                return GoalStatus.Running;
            }
            case 1:
                if (!ChildFinished(out _)) return GoalStatus.Running;
                _phase = 2;
                return Delegate(new GoToGoal(leader.Cell, 130f));
            default:
                if (!ChildFinished(out _)) return GoalStatus.Running;
                if (npc.GlobalPosition.DistanceTo(leader.GlobalPosition) > 260f) { _phase = 1; return GoalStatus.Running; }
                Delegation.ReportedForDuty(leader, npc);
                return GoalStatus.Done;
        }
    }
}
