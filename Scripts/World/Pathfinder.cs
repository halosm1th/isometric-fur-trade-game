using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Core;

namespace IsometricFurTradeGame.Scripts.World;

// A* over the staggered grid. Reads walkability straight off the painted
// layers, so it works for hand-edited maps too: water blocks (ice doesn't),
// props block, wall tiles block the edge they sit on, closed doors cost a
// little and get opened by whoever walks through.
public class Pathfinder
{
    // The map changed under us (a camp moved): nothing cached here yet, but callers say so.
    public void Invalidate() { }

    private const int MaxExpansions = 15000;
    private const float BushCost = 60f;

    private readonly TerrainPainter _terrain;
    private readonly StructurePainter _structures;

    public Pathfinder(TerrainPainter terrain, StructurePainter structures)
    {
        _terrain = terrain;
        _structures = structures;
    }

    public bool InBounds(Vector2I c) =>
        c.X >= 0 && c.Y >= 0 && c.X < WorldLayout.MapWidth && c.Y < WorldLayout.MapHeight;

    // Can an agent stand on this cell?
    public bool IsWalkable(Vector2I c)
    {
        if (!InBounds(c)) return false;
        if (_terrain.IsWater(c)) return false;
        if (_structures.IsCellSolid(c)) return false;
        return true;
    }

    // Can an agent step from a to an edge-adjacent b?
    public bool CanStep(Vector2I a, Vector2I b, out bool throughDoor)
    {
        throughDoor = false;
        if (!IsWalkable(b)) return false;
        if (_structures.EdgeBlocked(a, b, out bool door)) return false;
        throughDoor = door;
        return true;
    }

    // Within one search the map can't change, so each step and tree is asked
    // of the tile layers once: the corner moves re-ask the same edges the
    // straight moves did, and every ask is several calls into the engine.
    // 0 = blocked, 1 = open, 2 = through a door.
    private readonly Dictionary<(Vector2I, Vector2I), byte> _stepMemo = new();
    private readonly Dictionary<Vector2I, bool> _treeMemo = new();

    private bool CanStepMemo(Vector2I a, Vector2I b, out bool throughDoor)
    {
        if (!_stepMemo.TryGetValue((a, b), out byte s))
        {
            s = CanStep(a, b, out bool d) ? (d ? (byte)2 : (byte)1) : (byte)0;
            _stepMemo[(a, b)] = s;
        }
        throughDoor = s == 2;
        return s != 0;
    }

    private bool IsTreeMemo(Vector2I c)
    {
        if (!_treeMemo.TryGetValue(c, out bool t)) _treeMemo[c] = t = _structures.IsTree(c);
        return t;
    }

    private IEnumerable<(Vector2I cell, float cost, bool door)> Neighbours(Vector2I c)
    {
        foreach (var n in IsoGrid.EdgeNeighbours(c))
        {
            if (CanStepMemo(c, n, out bool door)) yield return (n, 71.5f + (door ? 40f : 0f) + (IsTreeMemo(n) ? BushCost : 0f), door);
        }
        // Corner moves are allowed only when both ways round are clear, so a
        // path can't slip diagonally through a wall's corner.
        var ul = IsoGrid.UpLeft(c); var ur = IsoGrid.UpRight(c);
        var dl = IsoGrid.DownLeft(c); var dr = IsoGrid.DownRight(c);
        // left: via UpLeft->DownLeft(UpLeft)... left neighbour = DownLeft(UpLeft(c)) = UpLeft(DownLeft(c))
        yield return Corner(c, new Vector2I(c.X - 1, c.Y), ul, dl, 128f);
        yield return Corner(c, new Vector2I(c.X + 1, c.Y), ur, dr, 128f);
        yield return Corner(c, new Vector2I(c.X, c.Y - 2), ul, ur, 64f);
        yield return Corner(c, new Vector2I(c.X, c.Y + 2), dl, dr, 64f);
    }

    private (Vector2I, float, bool) Corner(Vector2I from, Vector2I to, Vector2I via1, Vector2I via2, float cost)
    {
        if (CanStepMemo(from, via1, out bool d1) && !d1 && CanStepMemo(via1, to, out bool d2) && !d2 &&
            CanStepMemo(from, via2, out bool d3) && !d3 && CanStepMemo(via2, to, out bool d4) && !d4)
        {
            return (to, cost + (IsTreeMemo(to) ? BushCost : 0f), false);
        }
        return (to, -1f, false);
    }

    // Nearest walkable cell to `c`, searching outward a few rings.
    public Vector2I? NearestWalkable(Vector2I c, int rings = 4)
    {
        if (IsWalkable(c)) return c;
        for (int r = 1; r <= rings; r++)
        {
            for (int dx = -r; dx <= r; dx++)
            {
                for (int dy = -2 * r; dy <= 2 * r; dy++)
                {
                    var n = new Vector2I(c.X + dx, c.Y + dy);
                    if (IsWalkable(n)) return n;
                }
            }
        }
        return null;
    }

    public static int Calls, SlowCalls;
    public static double TotalMs;

    // A search that just failed is not run again for a couple of seconds from
    // about the same place to the same goal: a man whose way home is blocked
    // (or too long to find) used to search the whole budget every frame, and
    // under fast-forward that was most of the frame.
    private readonly Dictionary<(Vector2I, Vector2I), ulong> _failed = new();
    public static int CachedFailures;

    public List<Vector2I> FindPath(Vector2I start, Vector2I goal)
    {
        var key = (new Vector2I(start.X >> 2, start.Y >> 3), goal);
        ulong now = Time.GetTicksMsec();
        if (_failed.TryGetValue(key, out ulong until) && now < until) { CachedFailures++; return new List<Vector2I>(); }
        var sw = System.Diagnostics.Stopwatch.StartNew();
        List<Vector2I> r;
        try { r = FindPathInner(start, goal); }
        finally { _stepMemo.Clear(); _treeMemo.Clear(); }
        sw.Stop();
        if (r.Count == 0 && start != goal)
        {
            if (_failed.Count > 4000) _failed.Clear();
            _failed[key] = now + 2000;
        }
        Calls++; TotalMs += sw.Elapsed.TotalMilliseconds;
        if (sw.Elapsed.TotalMilliseconds > 30) { SlowCalls++; GD.Print($"[path] slow {sw.Elapsed.TotalMilliseconds:0} ms {start}->{goal} len {r.Count}"); }
        return r;
    }

    // Is `goal` shut in a small pocket (a fenced plot, a room with no way
    // in) that `start` is outside of? Searching toward such a goal from the
    // open country used to spend the whole budget - hundreds of milliseconds
    // on one frame - before giving up; flooding the pocket from the goal's
    // side says so in a few dozen cells. Steps are symmetric (walls block
    // their edge from both sides, doors open both ways), so a pocket that
    // can't get out can't be got into. Only small pockets are flooded; a
    // goal with room round it is left to the search.
    private const int PocketLimit = 400;
    private readonly HashSet<Vector2I> _pocket = new();
    private readonly Queue<Vector2I> _pocketQueue = new();

    private bool Enclosed(Vector2I goal, Vector2I start)
    {
        _pocket.Clear(); _pocketQueue.Clear();
        _pocket.Add(goal); _pocketQueue.Enqueue(goal);
        while (_pocketQueue.Count > 0)
        {
            var c = _pocketQueue.Dequeue();
            foreach (var (n, cost, _) in Neighbours(c))
            {
                if (cost < 0f || !_pocket.Add(n)) continue;
                if (n == start || _pocket.Count > PocketLimit) return false;
                _pocketQueue.Enqueue(n);
            }
        }
        return true;
    }

    private List<Vector2I> FindPathInner(Vector2I start, Vector2I goal)
    {
        var result = new List<Vector2I>();
        if (!InBounds(start) || !InBounds(goal)) return result;
        var g = NearestWalkable(goal);
        if (g == null) return result;
        goal = g.Value;
        if (start == goal) { result.Add(goal); return result; }
        if (IsWalkable(start) && Enclosed(goal, start)) return result;

        var open = new PriorityQueue<Vector2I, float>();
        var cameFrom = new Dictionary<Vector2I, Vector2I>();
        var gScore = new Dictionary<Vector2I, float> { [start] = 0f };
        var closed = new HashSet<Vector2I>();
        var goalPos = IsoGrid.ToWorld(goal);
        // Weighted A* (design doc 19: cheap beats optimal): a long haul up
        // the river road doesn't need the shortest path, only a good one.
        float far = IsoGrid.ToWorld(start).DistanceTo(goalPos);
        float weight = far > 6000f ? 2.2f : far > 2500f ? 1.6f : 1f;
        open.Enqueue(start, 0f);
        int expansions = 0;

        while (open.Count > 0 && expansions++ < MaxExpansions)
        {
            var current = open.Dequeue();
            if (current == goal)
            {
                var c = goal;
                while (c != start) { result.Add(c); c = cameFrom[c]; }
                result.Reverse();
                return result;
            }
            if (!closed.Add(current)) continue;

            foreach (var (n, cost, _) in Neighbours(current))
            {
                if (cost < 0f || closed.Contains(n)) continue;
                float tentative = gScore[current] + cost;
                if (gScore.TryGetValue(n, out float known) && tentative >= known) continue;
                gScore[n] = tentative;
                cameFrom[n] = current;
                open.Enqueue(n, tentative + IsoGrid.ToWorld(n).DistanceTo(goalPos) * weight);
            }
        }
        return result; // empty: no route
    }
}
