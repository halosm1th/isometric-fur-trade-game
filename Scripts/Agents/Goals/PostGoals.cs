using Godot;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Ecology;
using IsometricFurTradeGame.Scripts.World;

namespace IsometricFurTradeGame.Scripts.Agents.Goals;

// Sit down at your desk and open what's addressed to you (design doc
// 10.1.1: the read event). Half an hour with the packet.
public class ReadMailGoal : Goal
{
    private int _phase;
    public override string Label => "read the post";

    public override GoalStatus Tick(Npc npc, float dt, float hours)
    {
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
    private float _patience = 2f;
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
                return Delegate(new GoToGoal(_where, 300f));
            case 1:
            {
                if (!ChildFinished(out _)) return GoalStatus.Running;
                _searched += hours;
                bool valid = _missing != null && GodotObject.IsInstanceValid(_missing);
                if (valid && npc.GlobalPosition.DistanceTo(_missing.GlobalPosition) < 600f)
                {
                    string news = _missing.IsDead ? $"found {_missing.DisplayName} dead" : $"found {_missing.DisplayName} alive and well";
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
