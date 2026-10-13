using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;

namespace IsometricFurTradeGame.Scripts.World;

// A camp's horses grazing on the open ground beside the lodges, or a post's
// horse guard outside the palisade (design doc 12.3): the herd a household
// owns, drawn where a player can see it - and where a horse thief can find
// it. Economy/Plains keeps one for every herd a player is near; the numbers
// are the household's Herd.
public partial class HerdView : Node2D
{
    public string Seed = "";
    private readonly List<Mount> _horses = new();
    private readonly List<Vector2> _goals = new();
    private float _timer;
    private RandomNumberGenerator _rng;

    public override void _Ready()
    {
        _rng = new RandomNumberGenerator { Seed = (ulong)Mathf.Abs(Seed.GetHashCode()) };
    }

    public void SetCount(int n)
    {
        _rng ??= new RandomNumberGenerator { Seed = (ulong)Mathf.Abs(Seed.GetHashCode()) };
        while (_horses.Count > n) { _horses[^1].QueueFree(); _horses.RemoveAt(_horses.Count - 1); _goals.RemoveAt(_goals.Count - 1); }
        while (_horses.Count < n)
        {
            var at = Spot();
            var m = new Mount { Coat = Economy.Horses.CoatFor(Seed + _horses.Count), Ridden = false, Position = at };
            AddChild(m);
            _horses.Add(m); _goals.Add(at);
        }
    }

    private Vector2 Spot() => new(_rng.RandfRange(-260f, 260f), _rng.RandfRange(-130f, 130f));

    // Grazing: a step or two now and then, never far from the others.
    public override void _Process(double delta)
    {
        float motion = Mathf.Min(Core.GameCalendar.Instance?.MotionScale ?? 1f, 8f);
        _timer -= (float)delta;
        if (_timer <= 0f && _horses.Count > 0)
        {
            _timer = _rng.RandfRange(1.5f, 4f);
            int i = _rng.RandiRange(0, _horses.Count - 1);
            _goals[i] = (_goals[i] + new Vector2(_rng.RandfRange(-70f, 70f), _rng.RandfRange(-35f, 35f))).Clamp(new Vector2(-300, -150), new Vector2(300, 150));
        }
        for (int i = 0; i < _horses.Count; i++)
        {
            var to = _goals[i] - _horses[i].Position;
            float step = 30f * motion * (float)delta;
            _horses[i].Position += to.Length() <= step ? to : to.Normalized() * step;
        }
    }
}
