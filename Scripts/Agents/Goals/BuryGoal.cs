using Godot;
using IsometricFurTradeGame.Scripts.World;

namespace IsometricFurTradeGame.Scripts.Agents.Goals;

// A body lying about the post is carried off and put in the ground by one
// of its men (Npc.DecayBodies sends him). Before, the dead lay at the
// Factory door for ten days with their names over them.
public class BuryGoal : Goal
{
    private readonly Npc _body;
    private int _phase;
    public BuryGoal(Npc body) { _body = body; }
    public override string Label => $"bury {_body?.DisplayName}";

    public override GoalStatus Tick(Npc npc, float dt, float hours)
    {
        if (_body == null || !GodotObject.IsInstanceValid(_body) || _body.IsQueuedForDeletion() || !_body.IsInGroup("corpses")) return GoalStatus.Done;
        switch (_phase)
        {
            case 0:
                _phase = 1;
                return Delegate(new GoToGoal(IsoGrid.FromWorld(_body.GlobalPosition), 60f));
            case 1:
                if (!ChildFinished(out var r)) return GoalStatus.Running;
                if (r != GoalStatus.Done) return GoalStatus.Failed;
                _phase = 2;
                return Delegate(new WaitGoal(0.75f));
            default:
                if (!ChildFinished(out _)) return GoalStatus.Running;
                // His things go to the stores of the house he died at.
                var stores = npc.HomeStores;
                foreach (var (id, n) in new System.Collections.Generic.List<(Items.ItemId, int)>(_body.Inventory.All()))
                {
                    _body.Inventory.Remove(id, n);
                    if (stores != null) stores.Stores.Add(id, n);
                }
                _body.Buried(npc);
                return GoalStatus.Done;
        }
    }
}
