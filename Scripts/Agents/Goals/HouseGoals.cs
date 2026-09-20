using Godot;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Items;
using IsometricFurTradeGame.Scripts.World;

namespace IsometricFurTradeGame.Scripts.Agents.Goals;

// Keep the household's water up (design doc 17.2): in open water, kettles
// to the shore and back; in winter, melt snow at the fire. Water is not
// scarce - the cost is the walk and the fuel.
public class FetchWaterGoal : Goal
{
    private int _phase;
    public override string Label => "fetch water";

    public override GoalStatus Tick(Npc npc, float dt, float hours)
    {
        var stores = npc.HomeStores;
        if (stores == null) return GoalStatus.Failed;
        var cal = GameCalendar.Instance;
        switch (_phase)
        {
            case 0:
                if (!npc.Inventory.Has(ItemId.Kettle))
                {
                    if (!stores.Stores.Has(ItemId.Kettle)) return GoalStatus.Failed;
                    _phase = 1;
                    return Delegate(new GoToGoal(stores.AccessCell, 100f));
                }
                _phase = 2;
                return GoalStatus.Running;
            case 1:
                if (!ChildFinished(out var r0)) return GoalStatus.Running;
                if (r0 != GoalStatus.Done || !stores.Stores.Remove(ItemId.Kettle)) return GoalStatus.Failed;
                npc.Inventory.Add(ItemId.Kettle);
                _phase = 2;
                return GoalStatus.Running;
            case 2:
                if (cal.IsFrozen)
                {
                    // Melt snow at the household fire.
                    var h = stores.LowestHearth(999f) ?? Sim.Structures?.NearestHearth(npc.GlobalPosition);
                    if (h == null) return GoalStatus.Failed;
                    _phase = 3;
                    return Delegate(new GoToGoal(npc.StandCellNear(h.Cell), 110f));
                }
                var cell = npc.NearestWaterCell(9000f);
                if (cell == null) return GoalStatus.Failed;
                _phase = 4;
                return Delegate(new GoToGoal(cell.Value, 30f));
            case 3:
            {
                if (!ChildFinished(out var r)) return GoalStatus.Running;
                if (r != GoalStatus.Done) return GoalStatus.Failed;
                var h = Sim.Structures?.HearthNear(npc.GlobalPosition, 200f);
                if (h == null || !h.IsBurning) return GoalStatus.Failed;
                h.FuelHours = Mathf.Max(0.1f, h.FuelHours - 0.6f);
                npc.Inventory.Add(ItemId.Water, 4);
                _phase = 5;
                return Delegate(new WaitGoal(0.5f));
            }
            case 4:
            {
                if (!ChildFinished(out var r)) return GoalStatus.Running;
                if (r != GoalStatus.Done) return GoalStatus.Failed;
                if (Sim.Terrain == null || !Sim.Terrain.IsWaterNear(npc.GlobalPosition)) return GoalStatus.Failed;
                npc.Inventory.Add(ItemId.Water, 6);
                npc.Needs.Thirst = 100f;
                _phase = 5;
                return Delegate(new WaitGoal(0.2f));
            }
            case 5:
                if (!ChildFinished(out _)) return GoalStatus.Running;
                _phase = 6;
                return Delegate(new GoToGoal(stores.AccessCell, 100f));
            default:
                if (!ChildFinished(out _)) return GoalStatus.Running;
                stores.Stores.Add(ItemId.Water, npc.Inventory.TakeUpTo(ItemId.Water, 99));
                if (npc.Inventory.Remove(ItemId.Kettle)) stores.Stores.Add(ItemId.Kettle);
                return GoalStatus.Done;
        }
    }
}

// An evening with the rum (design doc 13.4). Company men and Cree hunters
// alike: take a dram from the cache, drink it, talk. Drunk men quarrel.
public class CarouseGoal : Goal
{
    private int _phase;
    private float _left = 2.5f;
    public override string Label => "drinking";

    public override GoalStatus Tick(Npc npc, float dt, float hours)
    {
        var stores = npc.HomeStores;
        if (stores == null) return GoalStatus.Failed;
        switch (_phase)
        {
            case 0:
                if (!stores.Stores.Has(ItemId.Rum) && !stores.Stores.Has(ItemId.Ale)) return GoalStatus.Failed;
                _phase = 1;
                return Delegate(new GoToGoal(stores.AccessCell, 100f));
            case 1:
            {
                if (!ChildFinished(out var r)) return GoalStatus.Running;
                if (r != GoalStatus.Done) return GoalStatus.Failed;
                var drink = stores.Stores.Has(ItemId.Rum) ? ItemId.Rum : ItemId.Ale;
                if (!stores.Stores.Remove(drink)) return GoalStatus.Failed;
                npc.Inventory.Add(drink);
                npc.Consume(drink);
                npc.PlayAnimation("drink");
                _phase = 2;
                return GoalStatus.Running;
            }
            default:
                _left -= hours;
                if (_left <= 0f || GameCalendar.Instance.Hour >= 23f) return GoalStatus.Done;
                // Another dram now and then; the drunker, the likelier a quarrel.
                if (GD.Randf() < hours * 0.5f && stores.Stores.Has(ItemId.Rum) && npc.Intoxication < 80f && npc.GlobalPosition.DistanceTo(IsoGrid.ToWorld(stores.AccessCell)) < 160f)
                {
                    stores.Stores.Remove(ItemId.Rum); npc.Inventory.Add(ItemId.Rum); npc.Consume(ItemId.Rum); npc.PlayAnimation("drink");
                }
                if (npc.Intoxication > 55f && GD.Randf() < hours * 0.25f * npc.Personality.Courage)
                {
                    foreach (var a in Sim.AgentsWithin(npc.GlobalPosition, 160f))
                    {
                        if (a is Npc other && other != npc && !other.IsDead && other.Intoxication > 30f && !other.IsFemale)
                        {
                            Chronicle.Event($"{npc.DisplayName} and {other.DisplayName} came to blows over the rum.", npc.GlobalPosition, npc, other.DisplayName, 1f);
                            Sim.Notice($"{npc.DisplayName} and {other.DisplayName} are brawling, drunk.");
                            return Delegate(new BrawlGoal(other));
                        }
                    }
                }
                return GoalStatus.Running;
        }
    }
}

// Fists only, a few blows, then it's over.
public class BrawlGoal : Goal
{
    public override int Tier => 0;
    private readonly Agent _target;
    private float _left = 12f;
    private int _blows;
    public BrawlGoal(Agent target) { _target = target; }
    public override string Label => $"brawl with {_target?.Describe()}";

    public override GoalStatus Tick(Npc npc, float dt, float hours)
    {
        _left -= dt;
        if (_target == null || !GodotObject.IsInstanceValid(_target) || _target.IsDead || _left <= 0f || _blows >= 3 || _target.Needs.Health < 40f) { npc.StopMoving(); return GoalStatus.Done; }
        if (npc.GlobalPosition.DistanceTo(_target.GlobalPosition) < 70f)
        {
            npc.StopMoving();
            var savedWeapon = npc.PreferredWeapon;
            npc.Unarmed = true;
            if (npc.Attack(_target)) _blows++;
            npc.Unarmed = false;
            return GoalStatus.Running;
        }
        npc.ChaseToward(_target.GlobalPosition, dt);
        return GoalStatus.Running;
    }
    public override void OnExit(Npc npc) => npc.StopMoving();
}
