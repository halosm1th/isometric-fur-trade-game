using System.Collections.Generic;
using Godot;

namespace IsometricFurTradeGame.Scripts.Agents.Goals;

public enum GoalStatus { Running, Done, Failed }

// One step of an agent's plan (design doc 3). Goals are small state
// machines; a goal that needs a sub-step pushes a child and waits for it.
// Tier 0 (subsistence) goals may interrupt anything above them.
public abstract class Goal
{
    public virtual int Tier => 1;
    public virtual string Label => GetType().Name.Replace("Goal", "");

    // Set by the brain while a child is running on top of this goal.
    public Goal Child;
    public GoalStatus ChildResult = GoalStatus.Running;

    public virtual void OnEnter(Npc npc) { }
    public virtual void OnExit(Npc npc) { }
    public abstract GoalStatus Tick(Npc npc, float dt, float hours);

    // Ask the brain to run `child` before this goal ticks again.
    protected GoalStatus Delegate(Goal child)
    {
        Child = child;
        ChildResult = GoalStatus.Running;
        return GoalStatus.Running;
    }

    // True once the last delegated child has finished (and consumes the result).
    protected bool ChildFinished(out GoalStatus result)
    {
        result = ChildResult;
        if (Child == null && ChildResult != GoalStatus.Running)
        {
            ChildResult = GoalStatus.Running;
            return true;
        }
        return false;
    }
}

// The goal stack. Interrupts (Tier 0) are pushed on top by the Npc each
// tick when a need runs low; when they finish the previous plan resumes.
public class Brain
{
    private readonly List<Goal> _stack = new();
    public IReadOnlyList<Goal> Stack => _stack;
    public Goal Top => _stack.Count > 0 ? _stack[^1] : null;
    public GoalStatus LastResult { get; private set; } = GoalStatus.Done;
    public int ConsecutiveFailures { get; private set; }
    public bool IsEmpty => _stack.Count == 0;

    public void Push(Npc npc, Goal g)
    {
        _stack.Add(g);
        g.OnEnter(npc);
    }

    public void Clear(Npc npc)
    {
        for (int i = _stack.Count - 1; i >= 0; i--) _stack[i].OnExit(npc);
        _stack.Clear();
    }

    public bool Has<T>() where T : Goal
    {
        foreach (var g in _stack) if (g is T) return true;
        return false;
    }

    public void Tick(Npc npc, float dt, float hours)
    {
        if (_stack.Count == 0) return;
        var top = _stack[^1];

        // A goal wanting a child gets it pushed and ticked first.
        if (top.Child != null)
        {
            var child = top.Child;
            top.Child = null;
            Push(npc, child);
            top = child;
        }

        var status = top.Tick(npc, dt, hours);
        if (status == GoalStatus.Running) return;

        top.OnExit(npc);
        _stack.RemoveAt(_stack.Count - 1);
        if (_stack.Count > 0) _stack[^1].ChildResult = status;
        else
        {
            LastResult = status;
            ConsecutiveFailures = status == GoalStatus.Failed ? ConsecutiveFailures + 1 : 0;
        }
    }

    public string Describe()
    {
        if (_stack.Count == 0) return "idle";
        var parts = new List<string>();
        foreach (var g in _stack) parts.Add(g.Label);
        return string.Join(" > ", parts);
    }
}
