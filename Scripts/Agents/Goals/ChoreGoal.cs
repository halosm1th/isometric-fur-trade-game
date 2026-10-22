using System;
using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.World;

namespace IsometricFurTradeGame.Scripts.Agents.Goals;

// A job of work as the steps it is made of (design doc 3.2): walk out to the
// field, plough it; walk home for the seed, carry it out, sow it. Each step
// is labelled with what the person is doing at that moment - which is what
// the trace, a bark and a player who knows them can read - and is either a
// goal to run (a walk, a spell of work) or a thing done there and then,
// which may fail the job ("the seed wasn't there").
public class ChoreGoal : Goal
{
    public record Step(string Label, Func<Npc, object> Run);

    private readonly List<Step> _steps;
    private readonly string _name;
    private int _at = -1;
    private bool _waiting;
    private Goal _last;

    public ChoreGoal(string name, params Step[] steps) { _name = name; _steps = new List<Step>(steps); }

    // What to do with whatever is in hand if the job is cut off before the
    // end (Tiers.Demote sets a person down at their own door): usually, put
    // it in the house.
    public Action<Npc> Abandon;
    public override void OnExit(Npc npc) { if (_at < _steps.Count) Abandon?.Invoke(npc); }

    public override string Label => _at >= 0 && _at < _steps.Count && _steps[_at].Label != "" ? _steps[_at].Label : _name;

    public override GoalStatus Tick(Npc npc, float dt, float hours)
    {
        if (_waiting)
        {
            if (!ChildFinished(out var r)) return GoalStatus.Running;
            _waiting = false;
            if (r != GoalStatus.Done) return Fail(npc);
        }
        while (++_at < _steps.Count)
        {
            var o = _steps[_at].Run(npc);
            if (o is Goal g) { _waiting = true; _last = g; return Delegate(g); }
            if (o is false) return Fail(npc);
        }
        return GoalStatus.Done;
    }

    // Dev: `-- --chorefail` prints every job that fails, and at which step.
    private static readonly bool Trace = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--chorefail") >= 0;
    private GoalStatus Fail(Npc npc)
    {
        if (Trace) GD.Print($"[chore] {npc.DisplayName} ({npc.Role}): {_name} failed at '{Label}' from {npc.Cell}" + (Child is GoToGoal g ? $" to {g.Target}" : _last is GoToGoal l ? $" to {l.Target} (walkable {Core.Sim.Paths?.IsWalkable(l.Target)}, fresh path {Core.Sim.Paths?.FindPath(npc.Cell, l.Target)?.Count})" : ""));
        return GoalStatus.Failed;
    }

    // The common steps.
    public static Step Walk(string label, Vector2I cell, float within = 60f) => new(label, _ => new GoToGoal(cell, within));
    public static Step Work(string label, float hours) => new(label, _ => new WaitGoal(hours));
    public static Step Do(Func<Npc, bool> act) => new("", n => act(n));
    public static Step Do(Action<Npc> act) => new("", n => { act(n); return true; });
}
