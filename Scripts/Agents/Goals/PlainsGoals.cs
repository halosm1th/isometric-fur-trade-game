using Godot;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Ecology;
using IsometricFurTradeGame.Scripts.Economy;
using IsometricFurTradeGame.Scripts.World;

namespace IsometricFurTradeGame.Scripts.Agents.Goals;

// One rider's part in the communal hunt (Ecology/BuffaloRun): out to the
// muster on a horse from the camp's herd, wait for the word, run, and home.
public class BuffaloRunGoal : Goal
{
    private readonly BuffaloRun _run;
    private int _phase;
    private int _kills, _cap;
    private float _try, _fret;
    public bool Running => _phase == 2;
    public BuffaloRunGoal(BuffaloRun run) { _run = run; }
    public override string Label => _phase is < 2 or 4 ? $"wait at the muster for {_run.Where}" : _phase == 2 ? $"run the buffalo at {_run.Where}" : "ride home from the hunt";

    public override void OnEnter(Npc npc)
    {
        Plains.TakeHorse(npc);
        // A good hunter on a good horse drops five or six in a run; a green one, two.
        _cap = 2 + Mathf.RoundToInt(npc.Skills.Hunting * 4f);
    }
    public override void OnExit(Npc npc) { npc.StopMoving(); Plains.ReturnHorse(npc); }

    public override GoalStatus Tick(Npc npc, float dt, float hours)
    {
        if (_run.Phase is RunPhase.Home or RunPhase.Over && _phase != 3) return Home(npc);
        switch (_phase)
        {
            case 0:
                _phase = 1;
                return Delegate(new GoToGoal(_run.Muster, 200f));
            case 1:
                if (!ChildFinished(out _)) return GoalStatus.Running;
                _run.Arrived(npc);
                _phase = 4;
                return GoalStatus.Running;
            case 4:
                if (_run.Phase == RunPhase.Running) { _phase = 2; return GoalStatus.Running; }
                // The young man who cannot wait (design doc 16.1): ambition and nerve
                // enough, and the credit of the first kill in front of him.
                _fret += hours;
                if (npc != _run.Captain && npc.Personality.Ambition > 0.85f && npc.Personality.Courage > 0.75f && _fret > 0.5f && GD.Randf() < 0.002f)
                    _run.Break(npc, "broke from the line");
                return GoalStatus.Running;
            case 2:
                npc.ChaseToward(_run.Site.WorldPos, dt);
                if (npc.GlobalPosition.DistanceTo(_run.Site.WorldPos) < _run.Site.RadiusPx)
                {
                    _try -= hours;
                    if (_try <= 0f)
                    {
                        _try = 0.12f;
                        float p = (0.45f + npc.Skills.Hunting * 0.4f) * (_run.Broken ? 0.35f : 1f);
                        if (GD.Randf() < p) { _run.Kill(npc); _kills++; npc.Skills.Learn(ref npc.Skills.Hunting, 0.01f); }
                    }
                }
                if (_kills >= _cap || _run.Site.Count < 1f) return Home(npc);
                return GoalStatus.Running;
            default:
                if (!ChildFinished(out _)) return GoalStatus.Running;
                return GoalStatus.Done;
        }
    }

    private GoalStatus Home(Npc npc)
    {
        _phase = 3;
        npc.StopMoving();
        if (npc.HomeStores == null) return GoalStatus.Done;
        return Delegate(new GoToGoal(npc.HomeStores.AccessCell, 150f));
    }
}

// A horse raid (design doc 12.3): young men of one camp go on foot by night
// to another's herd, cut out what they can and ride home on it. Not a fight
// over goods or ground - the herd is the whole object, and a raid that is
// seen turns into a running fight or a run for home.
public class HorseRaidGoal : Goal
{
    public readonly Household Target;
    private readonly Household _home;
    private readonly Vector2I _herd;
    private int _phase;
    private float _wait;
    public bool Seen;
    public int Taken;
    public HorseRaidGoal(Household target, Household home, Vector2I herd) { Target = target; _home = home; _herd = herd; }
    public override string Label => _phase != 2 ? $"steal horses from {Target.Title}" : "drive the horses home";

    public override void OnExit(Npc npc) => npc.StopMoving();

    public override GoalStatus Tick(Npc npc, float dt, float hours)
    {
        switch (_phase)
        {
            case 0:
                _phase = 1;
                return Delegate(new GoToGoal(_herd, 160f));
            case 1:
                if (!ChildFinished(out _)) return GoalStatus.Running;
                _phase = 3;
                return GoalStatus.Running;
            case 3:
                if (Seen) return Flee(npc);
                // A quiet moment among the horses, then cut the hobbles.
                _wait += hours;
                if (_wait < 0.3f) return GoalStatus.Running;
                Taken = Plains.CutOut(npc, Target);
                return Flee(npc);
            default:
                if (!ChildFinished(out _)) return GoalStatus.Running;
                return GoalStatus.Done;
        }
    }

    private GoalStatus Flee(Npc npc)
    {
        _phase = 2;
        // Off toward home, as far as the night will take them: the raid is
        // over once they are out of the camp's reach.
        var home = _home is BandLedger b ? b.Camp : _home?.AccessCell ?? npc.BedCell;
        var dir = (IsoGrid.ToWorld(home) - npc.GlobalPosition).Normalized();
        var away = IsoGrid.FromWorld(npc.GlobalPosition + dir * 5000f);
        return Delegate(new GoToGoal(Sim.Paths != null && Sim.Paths.IsWalkable(away) ? away : home, 300f));
    }
}
