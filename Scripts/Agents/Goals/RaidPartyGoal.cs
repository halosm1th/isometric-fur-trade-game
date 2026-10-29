using Godot;
using IsometricFurTradeGame.Scripts.World;

namespace IsometricFurTradeGame.Scripts.Agents.Goals;

// A party come from outside (Core/YorkHappenings: the Nor'Westers down the
// Hayes in the night): up to the stores, take what can be carried, and back
// to the boats they came in. At the boats they are gone, with whatever they
// got away with. Unlike RaidGoal there is no house of theirs to walk home to.
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
                npc.QueueFree();
                return GoalStatus.Done;
        }
    }

    public override void OnExit(Npc npc) { npc.Raiding = false; npc.RaidTimer = 0f; }
}
