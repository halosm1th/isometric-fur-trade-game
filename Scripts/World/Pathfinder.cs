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

    private IEnumerable<(Vector2I cell, float cost, bool door)> Neighbours(Vector2I c)
    {
        foreach (var n in IsoGrid.EdgeNeighbours(c))
        {
            if (CanStep(c, n, out bool door)) yield return (n, 71.5f + (door ? 40f : 0f) + (_structures.IsTree(n) ? BushCost : 0f), door);
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
        if (CanStep(from, via1, out bool d1) && !d1 && CanStep(via1, to, out bool d2) && !d2 &&
            CanStep(from, via2, out bool d3) && !d3 && CanStep(via2, to, out bool d4) && !d4)
        {
            return (to, cost + (_structures.IsTree(to) ? BushCost : 0f), false);
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

    public List<Vector2I> FindPath(Vector2I start, Vector2I goal)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var r = FindPathInner(start, goal);
        sw.Stop();
        Calls++; TotalMs += sw.Elapsed.TotalMilliseconds;
        if (sw.Elapsed.TotalMilliseconds > 30) { SlowCalls++; GD.Print($"[path] slow {sw.Elapsed.TotalMilliseconds:0} ms {start}->{goal} len {r.Count}"); }
        return r;
    }

    private List<Vector2I> FindPathInner(Vector2I start, Vector2I goal)
    {
        var result = new List<Vector2I>();
        if (!InBounds(start) || !InBounds(goal)) return result;
        var g = NearestWalkable(goal);
        if (g == null) return result;
        goal = g.Value;
        if (start == goal) { result.Add(goal); return result; }

        var open = new PriorityQueue<Vector2I, float>();
        var cameFrom = new Dictionary<Vector2I, Vector2I>();
        var gScore = new Dictionary<Vector2I, float> { [start] = 0f };
        var closed = new HashSet<Vector2I>();
        var goalPos = IsoGrid.ToWorld(goal);
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
                open.Enqueue(n, tentative + IsoGrid.ToWorld(n).DistanceTo(goalPos));
            }
        }
        return result; // empty: no route
    }
}
