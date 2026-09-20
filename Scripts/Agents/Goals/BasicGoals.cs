using Godot;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Items;
using IsometricFurTradeGame.Scripts.World;

namespace IsometricFurTradeGame.Scripts.Agents.Goals;

// Walk to a cell. Fails if no route exists or we stall for too long.
public class GoToGoal : Goal
{
    public readonly Vector2I Target;
    public readonly float ArriveDistance;
    private float _stall;
    private bool _started;

    public GoToGoal(Vector2I target, float arriveDistance = 40f)
    {
        Target = target; ArriveDistance = arriveDistance;
    }

    public override string Label => $"go to {Target}";

    public override GoalStatus Tick(Npc npc, float dt, float hours)
    {
        if (!_started)
        {
            _started = true;
            if (!npc.SetDestination(Target)) return GoalStatus.Failed;
        }
        if (npc.GlobalPosition.DistanceTo(IsoGrid.ToWorld(Target)) <= ArriveDistance || npc.Arrived)
        {
            npc.StopMoving();
            return GoalStatus.Done;
        }
        if (npc.MoveFailed) return GoalStatus.Failed;
        _stall += dt;
        if (_stall > 90f) return GoalStatus.Failed;
        return GoalStatus.Running;
    }

    public override void OnExit(Npc npc) => npc.StopMoving();
}

// Stand still for some game-hours (optionally playing an animation).
public class WaitGoal : Goal
{
    private float _left;
    private readonly string _anim;
    public WaitGoal(float hours, string anim = null) { _left = hours; _anim = anim; }
    public override string Label => $"wait {_left:0.#}h";
    public override void OnEnter(Npc npc) { npc.StopMoving(); if (_anim != null) npc.PlayAnimation(_anim); }
    public override GoalStatus Tick(Npc npc, float dt, float hours)
    {
        _left -= hours;
        return _left <= 0f ? GoalStatus.Done : GoalStatus.Running;
    }
}

// Idle around a cell, taking a few steps now and then.
public class WanderGoal : Goal
{
    private readonly Vector2I _centre;
    private readonly int _radius;
    private float _hoursLeft;
    private float _pause;
    private bool _moving;

    public WanderGoal(Vector2I centre, int radius, float hours)
    {
        _centre = centre; _radius = radius; _hoursLeft = hours;
    }
    public override string Label => "about";

    public override GoalStatus Tick(Npc npc, float dt, float hours)
    {
        _hoursLeft -= hours;
        if (_hoursLeft <= 0f) { npc.StopMoving(); return GoalStatus.Done; }

        if (_moving)
        {
            if (npc.Arrived || npc.MoveFailed) { _moving = false; _pause = (float)GD.RandRange(2.0, 6.0); }
            return GoalStatus.Running;
        }
        _pause -= dt;
        if (_pause <= 0f)
        {
            var target = new Vector2I(_centre.X + GD.RandRange(-_radius, _radius), _centre.Y + GD.RandRange(-2 * _radius, 2 * _radius));
            if (npc.SetDestination(target)) _moving = true; else _pause = 2f;
        }
        return GoalStatus.Running;
    }
}

// Tier 0: eat. From the pack if there's food, else from the household's
// stores, else fail (and stay hungry).
public class EatGoal : Goal
{
    public override int Tier => 0;
    private int _phase;
    public override string Label => "eat";

    public override GoalStatus Tick(Npc npc, float dt, float hours)
    {
        switch (_phase)
        {
            case 0:
                if (npc.Inventory.FirstOfKind(ItemKind.Food) != null) { _phase = 2; return GoalStatus.Running; }
                var store = npc.HomeStores;
                if (store == null || store.Stores.FirstOfKind(ItemKind.Food) == null) return GoalStatus.Failed;
                _phase = 1;
                return Delegate(new GoToGoal(store.AccessCell, 90f));
            case 1:
                if (!ChildFinished(out var r)) return GoalStatus.Running;
                if (r != GoalStatus.Done) return GoalStatus.Failed;
                var s = npc.HomeStores;
                if (s == null || !s.TakeRation(npc, 2)) return GoalStatus.Failed;
                _phase = 2;
                return GoalStatus.Running;
            case 2:
                if (!npc.Eat()) return GoalStatus.Failed;
                npc.PlayAnimation("eat");
                _phase = 3;
                return Delegate(new WaitGoal(0.25f));
            default:
                if (!ChildFinished(out _)) return GoalStatus.Running;
                if (npc.Needs.Hunger < 60f && npc.Inventory.FirstOfKind(ItemKind.Food) != null) { _phase = 2; return GoalStatus.Running; }
                return GoalStatus.Done;
        }
    }
}

// Tier 0: drink from the nearest water, melting snow if it's frozen.
public class DrinkGoal : Goal
{
    public override int Tier => 0;
    private int _phase;
    public override string Label => "drink";

    public override GoalStatus Tick(Npc npc, float dt, float hours)
    {
        if (_phase == 0)
        {
            if (npc.Inventory.Has(ItemId.Ale)) { npc.Consume(ItemId.Ale); return GoalStatus.Done; }
            var cell = npc.NearestWaterCell(2600f);
            if (cell == null) return GoalStatus.Failed;
            _phase = 1;
            return Delegate(new GoToGoal(cell.Value, 100f));
        }
        if (!ChildFinished(out var r)) return GoalStatus.Running;
        if (r != GoalStatus.Done) return GoalStatus.Failed;
        return npc.Drink() ? GoalStatus.Done : GoalStatus.Failed;
    }
}

// Tier 0: get to a fire and stay until warm; feed it if it's dying.
public class WarmUpGoal : Goal
{
    public override int Tier => 0;
    private int _phase;
    private Hearth _hearth;
    public override string Label => "warm up";

    public override GoalStatus Tick(Npc npc, float dt, float hours)
    {
        switch (_phase)
        {
            case 0:
                _hearth = npc.PreferredHearth();
                if (_hearth == null) return GoalStatus.Failed;
                _phase = 1;
                return Delegate(new GoToGoal(npc.StandCellNear(_hearth.Cell), 110f));
            case 1:
                if (!ChildFinished(out var r)) return GoalStatus.Running;
                if (r != GoalStatus.Done) return GoalStatus.Failed;
                _phase = 2;
                return GoalStatus.Running;
            default:
                if (_hearth == null || !GodotObject.IsInstanceValid(_hearth)) return GoalStatus.Failed;
                if (_hearth.FuelHours < 2f)
                {
                    if (npc.Inventory.Has(ItemId.Firewood)) { npc.Inventory.Remove(ItemId.Firewood); _hearth.Feed(3f); }
                    else if (npc.HomeStores != null && npc.HomeStores.Stores.Has(ItemId.Firewood) && npc.HomeStores.AccessCell.DistanceTo(_hearth.Cell) < 12)
                    {
                        npc.HomeStores.Stores.Remove(ItemId.Firewood); _hearth.Feed(3f);
                    }
                }
                if (!_hearth.IsBurning) return GoalStatus.Failed;
                return npc.Needs.Warmth >= 90f ? GoalStatus.Done : GoalStatus.Running;
        }
    }
}

// Go home and lie down till morning.
public class SleepGoal : Goal
{
    private int _phase;
    public override string Label => "sleep";

    public override GoalStatus Tick(Npc npc, float dt, float hours)
    {
        var cal = GameCalendar.Instance;
        if (_phase == 0)
        {
            _phase = 1;
            return Delegate(new GoToGoal(npc.BedCell, 30f));
        }
        if (_phase == 1)
        {
            if (!ChildFinished(out var r)) return GoalStatus.Running;
            _phase = 2;
            npc.PlayAnimation("idle");
            npc.Sleeping = true;
        }
        bool morning = cal.Hour >= 6f && cal.Hour < 20f;
        if (morning || npc.Needs.Hunger < 15f || npc.Needs.Warmth < 20f)
        {
            npc.Sleeping = false;
            return GoalStatus.Done;
        }
        return GoalStatus.Running;
    }

    public override void OnExit(Npc npc) => npc.Sleeping = false;
}

// Walk to the nearest standing tree, fell it, carry the wood back to the
// household woodpile.
public class ChopWoodGoal : Goal
{
    private int _phase;
    private Vector2I _tree;
    public override string Label => "cut firewood";

    public override GoalStatus Tick(Npc npc, float dt, float hours)
    {
        switch (_phase)
        {
            case 0:
                var stores = npc.HomeStores;
                var tree = Sim.Structures?.NearestTree(stores != null ? IsoGrid.ToWorld(stores.AccessCell) : npc.GlobalPosition, 3200f);
                if (tree == null) return GoalStatus.Failed;
                _tree = tree.Value;
                _phase = 1;
                return Delegate(new GoToGoal(_tree, 110f));
            case 1:
                if (!ChildFinished(out var r)) return GoalStatus.Running;
                if (r != GoalStatus.Done) return GoalStatus.Failed;
                _phase = 2;
                return Delegate(new WaitGoal(0.6f));
            case 2:
                if (!ChildFinished(out var r2)) return GoalStatus.Running;
                if (Sim.Structures.FellTreeAt(_tree)) npc.Inventory.Add(ItemId.Firewood, 4);
                else return GoalStatus.Failed;
                if (npc.HomeStores == null) return GoalStatus.Done;
                _phase = 3;
                return Delegate(new GoToGoal(npc.HomeStores.WoodpileCell, 100f));
            default:
                if (!ChildFinished(out var r3)) return GoalStatus.Running;
                int n = npc.Inventory.Count(ItemId.Firewood);
                // Keep one log for yourself.
                if (n > 1) { npc.Inventory.Remove(ItemId.Firewood, n - 1); npc.HomeStores.Stores.Add(ItemId.Firewood, n - 1); }
                return GoalStatus.Done;
        }
    }
}

// Check the household's fires; feed any that are low from the woodpile.
public class TendFiresGoal : Goal
{
    private int _phase;
    private Hearth _target;
    public override string Label => "tend the fires";

    public override GoalStatus Tick(Npc npc, float dt, float hours)
    {
        var stores = npc.HomeStores;
        if (stores == null) return GoalStatus.Failed;
        switch (_phase)
        {
            case 0:
                _target = stores.LowestHearth(4f);
                if (_target == null) return GoalStatus.Done;
                if (!npc.Inventory.Has(ItemId.Firewood))
                {
                    if (!stores.Stores.Has(ItemId.Firewood)) return GoalStatus.Failed;
                    _phase = 1;
                    return Delegate(new GoToGoal(stores.WoodpileCell, 100f));
                }
                _phase = 2;
                return GoalStatus.Running;
            case 1:
                if (!ChildFinished(out var r)) return GoalStatus.Running;
                if (r != GoalStatus.Done) return GoalStatus.Failed;
                npc.Inventory.Add(ItemId.Firewood, stores.Stores.TakeUpTo(ItemId.Firewood, 2));
                if (!npc.Inventory.Has(ItemId.Firewood)) return GoalStatus.Failed;
                _phase = 2;
                return GoalStatus.Running;
            case 2:
                _phase = 3;
                return Delegate(new GoToGoal(npc.StandCellNear(_target.Cell), 110f));
            default:
                if (!ChildFinished(out var r2)) return GoalStatus.Running;
                if (r2 != GoalStatus.Done || !GodotObject.IsInstanceValid(_target)) return GoalStatus.Failed;
                while (_target.FuelHours < 8f && npc.Inventory.Remove(ItemId.Firewood)) _target.Feed(3f);
                return GoalStatus.Done;
        }
    }
}

// Close with a target and fight it until one of you is dead or it flees
// out of reach. Tier 0: being attacked preempts the day's work.
public class AttackGoal : Goal
{
    public override int Tier => 0;
    public readonly Agent Target;
    private float _giveUp = 60f;
    public AttackGoal(Agent target) { Target = target; }
    public override string Label => $"fight {Target?.Describe()}";

    public override GoalStatus Tick(Npc npc, float dt, float hours)
    {
        if (Target == null || !GodotObject.IsInstanceValid(Target) || Target.IsDead)
        {
            npc.StopMoving();
            if (Target is Animal kill && GodotObject.IsInstanceValid(kill) && kill.IsDead && npc.GlobalPosition.DistanceTo(kill.GlobalPosition) < 160f) kill.Butcher(npc.Inventory);
            return GoalStatus.Done;
        }
        _giveUp -= dt;
        float dist = npc.GlobalPosition.DistanceTo(Target.GlobalPosition);
        if (_giveUp <= 0f || dist > 1400f) { npc.StopMoving(); return GoalStatus.Failed; }

        if (npc.InAttackRange(Target))
        {
            npc.StopMoving();
            npc.FaceToward(Target.GlobalPosition);
            if (npc.Attack(Target)) npc.PlayAnimation("attack");
            return GoalStatus.Running;
        }
        npc.ChaseToward(Target.GlobalPosition, dt);
        return GoalStatus.Running;
    }

    public override void OnExit(Npc npc) => npc.StopMoving();
}

// Run from something for a while.
public class FleeGoal : Goal
{
    public override int Tier => 0;
    private readonly Agent _from;
    private float _left;
    public FleeGoal(Agent from, float seconds = 6f) { _from = from; _left = seconds; }
    public override string Label => "flee";

    public override GoalStatus Tick(Npc npc, float dt, float hours)
    {
        _left -= dt;
        if (_left <= 0f || _from == null || !GodotObject.IsInstanceValid(_from)) { npc.StopMoving(); return GoalStatus.Done; }
        var away = (npc.GlobalPosition - _from.GlobalPosition).Normalized();
        npc.SteerDirect(away, true);
        return GoalStatus.Running;
    }
    public override void OnExit(Npc npc) => npc.StopMoving();
}
