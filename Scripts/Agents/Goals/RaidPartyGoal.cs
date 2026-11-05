using Godot;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.World;

namespace IsometricFurTradeGame.Scripts.Agents.Goals;

// The Nor'Westers' men from their house across the river, by night
// (Core/YorkHappenings): up to the stores, take what can be carried, and
// back to the boats they came in - and home in them, where what they got
// away with goes into their own house's stores.
public class RaidPartyGoal : Goal
{
    private readonly Economy.FortLedger _target;
    private readonly Vector2I _boats;
    private int _phase;
    public bool Away { get; private set; }
    public RaidPartyGoal(Economy.FortLedger target, Vector2I boats) { _target = target; _boats = boats; }
    public override string Label => _phase < 2 ? $"rob {_target?.Title}" : "back to the boats";

    public override void OnEnter(Npc npc) { npc.Raiding = true; npc.RaidTarget = _target.Company; npc.RaidTimer = 0f; }

    public override GoalStatus Tick(Npc npc, float dt, float hours)
    {
        switch (_phase)
        {
            case 0:
                _phase = 1;
                return Delegate(new GoToGoal(_target.AccessCell, 130f));
            case 1:
                if (!ChildFinished(out var r)) return GoalStatus.Running;
                // At the door, or turned off the road to it: either way they take what they can and go.
                if (r == GoalStatus.Done) Economy.Rivalry.Plunder(npc, _target, 4);
                npc.RaidTimer = 0f;
                _phase = 2;
                return Delegate(new GoToGoal(_boats, 90f));
            default:
                if (!ChildFinished(out _)) return GoalStatus.Running;
                Away = true;
                npc.Raiding = false;
                Home(npc);
                return GoalStatus.Done;
        }
    }

    public override void OnExit(Npc npc) { npc.Raiding = false; npc.RaidTimer = 0f; }

    // Paddled home across the river: the plunder into the house's stores, and back to his own work.
    public static void Home(Npc npc)
    {
        if (npc.HomeStores is not Economy.Household home) return;
        foreach (var (id, c) in new System.Collections.Generic.List<(Items.ItemId, int)>(npc.Inventory.All()))
        {
            var k = Items.Items.Of(id).Kind;
            if (k is Items.ItemKind.Weapon or Items.ItemKind.Clothing or Items.ItemKind.Tool || id == Items.ItemId.Powder) continue;
            npc.Inventory.Remove(id, c); home.Stores.Add(id, c);
        }
        npc.GlobalPosition = IsoGrid.ToWorld(home.AccessCell);   // the boats land them at their own door
        npc.ResetBehaviour();
    }
}

// The house's men when the alarm rings (Core/YorkHappenings): stand to by
// the stores and go for any raider in reach - the one at the door, or the
// one shooting at one of their own out in the yard. Pacing by the door
// while a raider fired on the new hand fifty yards off was no defence.
public class DefendHouseGoal : Goal
{
    private readonly Vector2I _post;
    private float _left, _shift;
    public DefendHouseGoal(Vector2I post, float hours) { _post = post; _left = hours; }
    public override string Label => "stand to";

    public override GoalStatus Tick(Npc npc, float dt, float hours)
    {
        ChildFinished(out _);
        _left -= hours; _shift -= hours;
        if (_left <= 0f) return GoalStatus.Done;
        Agent foe = null; float best = 1100f;
        foreach (var a in Sim.AgentsWithin(npc.GlobalPosition, 1100f))
        {
            if (a.IsDead || !a.Raiding || a.RaidTarget != npc.Faction) continue;
            float d = a.GlobalPosition.DistanceTo(npc.GlobalPosition);
            if (d < best) { best = d; foe = a; }
        }
        if (foe != null) return Delegate(new AttackGoal(foe));
        if (_shift <= 0f)
        {
            _shift = 0.5f;
            npc.SetDestination(new Vector2I(_post.X + GD.RandRange(-1, 1), _post.Y + GD.RandRange(-2, 2)));
        }
        return GoalStatus.Running;
    }
    public override void OnExit(Npc npc) => npc.StopMoving();
}
