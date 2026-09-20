using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Core;

namespace IsometricFurTradeGame.Scripts.World;

// Fire as a simulated object (design doc 18.4): it sits on a wooden cell,
// spreads to wooden neighbours according to the weather, burns what it's
// on to nothing, and hurts anyone standing in it. Started by an untended
// hearth, or by a man with flint and a grudge. Put out with water.
public partial class Fire : Node2D
{
    public static Fire I { get; private set; }

    private readonly Dictionary<Vector2I, float> _burning = new();   // seconds alight
    private readonly HashSet<Vector2I> _burnt = new();
    private readonly Dictionary<Vector2I, Blaze> _blazes = new();
    private float _spreadTimer;

    public const float BurnSeconds = 26f;
    public IEnumerable<Vector2I> BurningCells => _burning.Keys;
    public int Count => _burning.Count;

    private bool _armed;
    public override void _Ready()
    {
        I = this;
        // Dev aid: `-- --blaze` sets the men's house alight a few seconds in.
        foreach (string arg in OS.GetCmdlineUserArgs()) if (arg == "--blaze") _armed = true;
        if (_armed) GetTree().CreateTimer(5.0).Timeout += () => Ignite(IsoGrid.Cell(WorldLayout.MensHouse.Top, 2, 2), "Fire in the men's house!");
    }

    public bool IsBurning(Vector2I c) => _burning.ContainsKey(c);

    public Vector2I? NearestBurning(Vector2 pos, float radius)
    {
        Vector2I? best = null; float bestD = radius;
        foreach (var c in _burning.Keys)
        {
            float d = IsoGrid.ToWorld(c).DistanceTo(pos);
            if (d < bestD) { bestD = d; best = c; }
        }
        return best;
    }

    public bool Ignite(Vector2I c, string cause = null)
    {
        var s = Sim.Structures;
        if (s == null || !Net.Net.IsServer) return false;
        if (_burning.ContainsKey(c) || _burnt.Contains(c) || !s.IsFlammable(c)) return false;
        _burning[c] = 0f;
        ShowBlaze(c, true);
        if (cause != null) Sim.Notice(cause);
        return true;
    }

    public bool Douse(Vector2I c)
    {
        if (!_burning.ContainsKey(c)) return false;
        if (GD.Randf() < 0.55f) { _burning.Remove(c); ShowBlaze(c, false); return true; }
        return false;
    }

    private void ShowBlaze(Vector2I c, bool on)
    {
        if (on && !_blazes.ContainsKey(c))
        {
            var b = new Blaze { Position = IsoGrid.ToWorld(c) };
            AddChild(b);
            _blazes[c] = b;
        }
        else if (!on && _blazes.TryGetValue(c, out var b2)) { b2.QueueFree(); _blazes.Remove(c); }
    }

    // Clients mirror the server's burning set.
    public void ClientSync(Godot.Collections.Array cells)
    {
        var now = new HashSet<Vector2I>();
        foreach (var e in cells) { var r = (Godot.Collections.Array)e; now.Add(new Vector2I((int)r[0], (int)r[1])); }
        foreach (var c in new List<Vector2I>(_blazes.Keys)) if (!now.Contains(c)) ShowBlaze(c, false);
        foreach (var c in now) ShowBlaze(c, true);
    }

    public override void _Process(double delta)
    {
        if (!Net.Net.IsServer || _burning.Count == 0) return;
        float dt = (float)delta;
        var s = Sim.Structures; var cal = GameCalendar.Instance;
        if (s == null || cal == null) return;

        // Weather: rain and snow damp it down; a gale drives it.
        float weather = cal.Weather.Current switch
        {
            SkyState.Rain => 0.25f, SkyState.Snow => 0.35f, SkyState.Blizzard => 0.5f,
            SkyState.Storm => 1.6f, SkyState.Clear => 1.2f, _ => 1f,
        };

        foreach (var c in new List<Vector2I>(_burning.Keys)) _burning[c] += dt;

        _spreadTimer -= dt;
        if (_spreadTimer <= 0f)
        {
            _spreadTimer = 1f;
            foreach (var c in new List<Vector2I>(_burning.Keys))
            {
                foreach (var n in IsoGrid.EdgeNeighbours(c))
                {
                    if (_burning.ContainsKey(n) || _burnt.Contains(n) || !s.IsFlammable(n)) continue;
                    if (GD.Randf() < 0.09f * weather) { _burning[n] = 0f; ShowBlaze(n, true); }
                }
            }
            // People in the flames.
            foreach (var c in _burning.Keys)
            {
                var p = IsoGrid.ToWorld(c);
                foreach (var a in Sim.AgentsWithin(p, 70f)) a.TakeDamage(9f, null);
            }
        }

        // Burnt out: what stood here is gone.
        foreach (var c in new List<Vector2I>(_burning.Keys))
        {
            if (_burning[c] < BurnSeconds) continue;
            _burning.Remove(c);
            _burnt.Add(c);
            ShowBlaze(c, false);
            s.BurnOut(c);
        }
    }
}
