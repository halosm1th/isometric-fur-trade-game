using Godot;
using IsometricFurTradeGame.Scripts.World;

namespace IsometricFurTradeGame.Scripts.Agents.Goals;

// The fiddle in the men's house (Core/YorkHappenings): in at the door and
// onto the floor, then round and round in a reel till the fiddler stops -
// or, for the fiddler, a seat by the stove. Cheap on purpose: the dancer is
// stepped round a little circle where he stands, no paths, no physics.
public class DanceGoal : Goal
{
    private readonly Vector2I _spot;
    private readonly bool _fiddler;
    private float _left, _angle, _dir;
    private int _phase;
    private Vector2 _centre;

    public DanceGoal(Vector2I spot, float hours, bool fiddler)
    {
        _spot = spot; _left = hours; _fiddler = fiddler;
        _angle = GD.Randf() * Mathf.Tau;
        _dir = GD.Randf() < 0.5f ? 1f : -1f;
    }
    // "sitting" gives the fiddler the seated pose (Npc.Postures).
    public override string Label => _fiddler ? "sitting with the fiddle" : "dance a reel";
    public bool OnTheFloor => _phase >= 2;

    public override GoalStatus Tick(Npc npc, float dt, float hours)
    {
        _left -= hours;
        if (_left <= 0f) return GoalStatus.Done;
        switch (_phase)
        {
            case 0:
                _phase = 1;
                return Delegate(new GoToGoal(_spot, 40f));
            case 1:
                if (!ChildFinished(out _)) return GoalStatus.Running;
                _centre = npc.GlobalPosition;
                _phase = 2;
                return GoalStatus.Running;
            default:
                npc.StopMoving();
                if (_fiddler) return GoalStatus.Running;
                // Round he goes, and now and then the other way.
                _angle += dt * 3.4f * _dir;
                if (GD.Randf() < dt * 0.15f) _dir = -_dir;
                var to = _centre + new Vector2(Mathf.Cos(_angle) * 26f, Mathf.Sin(_angle) * 13f);
                var step = to - npc.GlobalPosition;
                npc.GlobalPosition = to;
                npc.Velocity = step.Normalized() * 60f;   // so he is drawn on his feet and moving
                npc.FaceToward(to + step);
                return GoalStatus.Running;
        }
    }

    public override void OnExit(Npc npc) => npc.StopMoving();
}
