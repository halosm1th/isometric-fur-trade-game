using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

namespace IsometricFurTradeGame.Scripts.World;

// Long routes over the whole country (design doc 18.3's travel mode): the
// cheapest way on foot from here to there over WorldMap's ground - round
// the lakes, over the rivers at the fords, through the bush slowly, on the
// ice in winter. Separate from the Pathfinder, which walks a man about the
// yard round walls and doors; this one plans hundreds of miles, on a
// background thread, and hands back a few hundred points to follow.
public static class Routes
{
    private const int MaxExpansions = 2_500_000;

    // swim: a man on the march (UI/TravelMode) may cross a river where he
    // finds it - by raft, by a canoe borrowed on the bank, or swimming - at a
    // steep price, so a crossing a few cells wide beats a long way round but
    // nobody is routed down the river itself. Without it only the fords
    // serve: men walked by the yard Pathfinder (war parties) cannot swim, and
    // a route through the water would strand them on the bank. With fords
    // only, Fort Douglas to Fort Gibraltar, across the Red, was 424 km.
    public static Task<List<Vector2>> PlanAsync(Vector2 from, Vector2 to, bool frozen, bool swim = false) =>
        Task.Run(() => Plan(IsoGrid.FromWorld(from), IsoGrid.FromWorld(to), frozen, swim));
    private const float SwimCost = 12f;

    public static bool Passable(Vector2I c, bool frozen)
    {
        if (!WorldMap.InBounds(c)) return false;
        var g = WorldMap.At(c);
        if (frozen) return true;   // the rivers and lakes are the winter road
        return g is not (Ground.Ocean or Ground.Lake or Ground.River) || WorldMap.IsShallow(c);
    }

    private static float Cost(Vector2I c)
    {
        var g = WorldMap.At(c);
        float k = g switch { Ground.Cordillera => 2.2f, Ground.Ford => 1.6f, Ground.Ocean or Ground.Lake or Ground.River => 0.9f, _ => 1f };
        if (WorldLayout.ForestAt(c)) k *= 1.4f;
        return k;
    }

    public static List<Vector2> Plan(Vector2I start, Vector2I goal, bool frozen, bool swim = false)
    {
        if (!Passable(goal, frozen)) goal = Nearest(goal, frozen) ?? goal;
        if (!Passable(start, frozen)) start = Nearest(start, frozen) ?? start;
        var goalW = IsoGrid.ToWorld(goal);
        var dist = new Dictionary<Vector2I, float> { [start] = 0f };
        var prev = new Dictionary<Vector2I, Vector2I>();
        var open = new PriorityQueue<Vector2I, float>();
        var closed = new HashSet<Vector2I>();
        open.Enqueue(start, 0f);
        int expanded = 0;
        while (open.Count > 0 && expanded < MaxExpansions)
        {
            var c = open.Dequeue();
            if (c == goal) break;
            if (!closed.Add(c)) continue;
            expanded++;
            float d = dist[c];
            foreach (var (n, step) in Neighbours(c))
            {
                bool river = swim && !frozen && WorldMap.InBounds(n) && WorldMap.At(n) == Ground.River;
                if (closed.Contains(n) || !river && !Passable(n, frozen)) continue;
                float nd = d + step * (river ? SwimCost : Cost(n));
                if (dist.TryGetValue(n, out float old) && old <= nd) continue;
                dist[n] = nd; prev[n] = c;
                open.Enqueue(n, nd + IsoGrid.ToWorld(n).DistanceTo(goalW) * 0.98f);
            }
        }
        if (!prev.ContainsKey(goal) && goal != start) return null;
        var cells = new List<Vector2I> { goal };
        while (cells[^1] != start) cells.Add(prev[cells[^1]]);
        cells.Reverse();
        // Every few cells is enough to steer by.
        var pts = new List<Vector2>();
        for (int i = 0; i < cells.Count; i += 6) pts.Add(IsoGrid.ToWorld(cells[i]));
        pts.Add(IsoGrid.ToWorld(goal));
        return pts;
    }

    private static IEnumerable<(Vector2I, float)> Neighbours(Vector2I c)
    {
        foreach (var n in IsoGrid.EdgeNeighbours(c)) yield return (n, 71.5f);
        yield return (new Vector2I(c.X - 1, c.Y), 128f);
        yield return (new Vector2I(c.X + 1, c.Y), 128f);
        yield return (new Vector2I(c.X, c.Y - 2), 64f);
        yield return (new Vector2I(c.X, c.Y + 2), 64f);
    }

    private static Vector2I? Nearest(Vector2I c, bool frozen)
    {
        for (int r = 1; r < 30; r++)
            for (int dx = -r; dx <= r; dx++)
                for (int dy = -2 * r; dy <= 2 * r; dy++)
                {
                    var n = new Vector2I(c.X + dx, c.Y + dy);
                    if (Passable(n, frozen)) return n;
                }
        return null;
    }
}
