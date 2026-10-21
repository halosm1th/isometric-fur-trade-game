using System.Collections.Generic;
using System.Runtime.InteropServices;
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

    // Within one search each step and tree is asked of the tile layers once:
    // the corner moves re-ask the same edges the straight moves did, and
    // every ask is several calls into the engine. The answers belong to the
    // search being worked (_cur). 0 = blocked, 1 = open, 2 = through a door.
    private Search _cur;

    private bool CanStepMemo(Vector2I a, Vector2I b, out bool throughDoor)
    {
        if (!_cur.StepMemo.TryGetValue((a, b), out byte s))
        {
            s = CanStep(a, b, out bool d) ? (d ? (byte)2 : (byte)1) : (byte)0;
            _cur.StepMemo[(a, b)] = s;
        }
        throughDoor = s == 2;
        return s != 0;
    }

    private bool IsTreeMemo(Vector2I c)
    {
        if (!_cur.TreeMemo.TryGetValue(c, out bool t)) _cur.TreeMemo[c] = t = _structures.IsTree(c);
        return t;
    }

    // The moves out of `c`, written into _moves (no allocation per cell: a
    // search looks at thousands, and the garbage was a good share of what the
    // collector stopped the game to sweep). Returns how many there are.
    private readonly (Vector2I cell, float cost)[] _moves = new (Vector2I, float)[8];

    private int Neighbours(Vector2I c)
    {
        int count = 0;
        var ul = IsoGrid.UpLeft(c); var ur = IsoGrid.UpRight(c);
        var dl = IsoGrid.DownLeft(c); var dr = IsoGrid.DownRight(c);
        Edge(c, ul, ref count); Edge(c, ur, ref count); Edge(c, dl, ref count); Edge(c, dr, ref count);
        // Corner moves are allowed only when both ways round are clear, so a
        // path can't slip diagonally through a wall's corner.
        // left: via UpLeft->DownLeft(UpLeft)... left neighbour = DownLeft(UpLeft(c)) = UpLeft(DownLeft(c))
        Corner(c, new Vector2I(c.X - 1, c.Y), ul, dl, 128f, ref count);
        Corner(c, new Vector2I(c.X + 1, c.Y), ur, dr, 128f, ref count);
        Corner(c, new Vector2I(c.X, c.Y - 2), ul, ur, 64f, ref count);
        Corner(c, new Vector2I(c.X, c.Y + 2), dl, dr, 64f, ref count);
        return count;
    }

    private void Edge(Vector2I from, Vector2I to, ref int count)
    {
        if (CanStepMemo(from, to, out bool door)) _moves[count++] = (to, 71.5f + (door ? 40f : 0f) + (IsTreeMemo(to) ? BushCost : 0f));
    }

    private void Corner(Vector2I from, Vector2I to, Vector2I via1, Vector2I via2, float cost, ref int count)
    {
        if (CanStepMemo(from, via1, out bool d1) && !d1 && CanStepMemo(via1, to, out bool d2) && !d2 &&
            CanStepMemo(from, via2, out bool d3) && !d3 && CanStepMemo(via2, to, out bool d4) && !d4)
        {
            _moves[count++] = (to, cost + (IsTreeMemo(to) ? BushCost : 0f));
        }
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

    // ---- a search that can be worked a little at a time ----------------------
    //
    // Most searches finish in a millisecond or two, but a long way round a
    // building or through the bush can take tens of milliseconds, and done in
    // one go that was a hitch on screen. A Search is worked for a slice of
    // time, set down, and taken up again next frame; all the searches
    // together get a few milliseconds a frame. The map may change between
    // slices (a door opens, a wall goes up): a man walking a path that has
    // gone stale gets stuck and asks again, as he always did.
    public sealed class Search
    {
        internal Vector2I Start, Goal, Asked;
        internal Vector2 GoalPos;
        internal float Weight;
        internal int Expansions;
        internal (Vector2I, Vector2I) FailKey;
        internal readonly PriorityQueue<Vector2I, float> Open = new();
        internal readonly Dictionary<Vector2I, Node> Nodes = new();
        internal readonly Dictionary<(Vector2I, Vector2I), byte> StepMemo = new();
        internal readonly Dictionary<Vector2I, bool> TreeMemo = new();
        public bool Done { get; internal set; }
        public List<Vector2I> Result { get; internal set; }
        // The cell the caller asked for (the search may aim at the nearest walkable one).
        public Vector2I Destination => Asked;
    }

    internal struct Node { public float G; public Vector2I From; public bool Closed; }

    // Searches are lent out and given back, so their tables keep the size the
    // big searches grew them to instead of being made anew each time; a
    // search dropped by a man who died mid-way is simply never given back.
    private readonly Stack<Search> _pool = new();

    // How long one call may work before setting a search down. Every search
    // started or still going in a frame shares the frame's budget; one
    // started after the budget is spent still gets a short first slice, which
    // is all most searches need, so nearly all still finish the moment asked.
    private const double MinFirstSliceMs = 0.5;
    private const double FrameBudgetMs = 4.0;
    private ulong _budgetFrame;
    private double _budgetSpent;

    private double BudgetLeft()
    {
        ulong frame = Engine.GetProcessFrames();
        if (frame != _budgetFrame) { _budgetFrame = frame; _budgetSpent = 0; }
        return FrameBudgetMs - _budgetSpent;
    }

    // Start a search and work it for its first slice. Done may already be
    // true; if not, call Continue each frame until it is.
    public Search Begin(Vector2I start, Vector2I goal)
    {
        var s = _pool.Count > 0 ? _pool.Pop() : new Search();
        s.Asked = goal; s.Start = start; s.Goal = goal; s.Done = false; s.Result = null; s.Expansions = 0;
        s.Open.Clear(); s.Nodes.Clear(); s.StepMemo.Clear(); s.TreeMemo.Clear();
        s.FailKey = (new Vector2I(start.X >> 2, start.Y >> 3), goal);
        if (_failed.TryGetValue(s.FailKey, out ulong until) && Time.GetTicksMsec() < until) { CachedFailures++; Finish(s, new List<Vector2I>(), cached: true); return s; }
        _cur = s;
        try { Setup(s, start, goal); }
        finally { _cur = null; }
        if (!s.Done) Work(s, System.Math.Max(MinFirstSliceMs, BudgetLeft()));
        return s;
    }

    // Work a search on for this frame's share of time. True when it is done.
    public bool Continue(Search s)
    {
        if (s.Done) return true;
        double left = BudgetLeft();
        if (left <= 0) return false;
        Work(s, left);
        return s.Done;
    }

    // Work it to the end now, whatever the time (the old way: for callers
    // that must have the answer this frame).
    public List<Vector2I> Complete(Search s)
    {
        if (!s.Done) Work(s, double.MaxValue);
        return s.Result;
    }

    // Give a search back, done or not, once its answer has been taken.
    public void Release(Search s)
    {
        if (s == null) return;
        s.Result = null; s.Done = true;
        // A search that ran up huge tables gives them back to the collector
        // rather than holding them for good.
        if (s.Nodes.Count > 40000) return;
        s.Open.Clear(); s.Nodes.Clear(); s.StepMemo.Clear(); s.TreeMemo.Clear();
        if (_pool.Count < 16) _pool.Push(s);
    }

    // The old one-call form: the whole search, now.
    public List<Vector2I> FindPath(Vector2I start, Vector2I goal)
    {
        var s = Begin(start, goal);
        var r = Complete(s);
        Release(s);
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
            int moves = Neighbours(c);
            for (int i = 0; i < moves; i++)
            {
                var n = _moves[i].cell;
                if (!_pocket.Add(n)) continue;
                if (n == start || _pocket.Count > PocketLimit) return false;
                _pocketQueue.Enqueue(n);
            }
        }
        return true;
    }

    private void Setup(Search s, Vector2I start, Vector2I goal)
    {
        if (!InBounds(start) || !InBounds(goal)) { Finish(s, new List<Vector2I>()); return; }
        var g = NearestWalkable(goal);
        if (g == null) { Finish(s, new List<Vector2I>()); return; }
        goal = g.Value;
        s.Goal = goal;
        if (start == goal) { Finish(s, new List<Vector2I> { goal }); return; }
        if (IsWalkable(start) && Enclosed(goal, start)) { Finish(s, new List<Vector2I>()); return; }

        s.Nodes[start] = new Node { G = 0f, From = start };
        s.GoalPos = IsoGrid.ToWorld(goal);
        // Weighted A* (design doc 19: cheap beats optimal): a long haul up
        // the river road doesn't need the shortest path, only a good one.
        float far = IsoGrid.ToWorld(start).DistanceTo(s.GoalPos);
        s.Weight = far > 6000f ? 2.2f : far > 2500f ? 1.6f : 1f;
        s.Open.Enqueue(start, 0f);
    }

    private void Work(Search s, double budgetMs)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        _cur = s;
        try { Expand(s, sw, budgetMs); }
        finally { _cur = null; }
        sw.Stop();
        double ms = sw.Elapsed.TotalMilliseconds;
        _budgetSpent += ms; TotalMs += ms;
        if (ms > 30) { SlowCalls++; GD.Print($"[path] slow {ms:0} ms {s.Start}->{s.Asked} len {s.Result?.Count ?? -1}"); }
    }

    private void Expand(Search s, System.Diagnostics.Stopwatch sw, double budgetMs)
    {
        var open = s.Open; var nodes = s.Nodes;
        while (open.Count > 0 && s.Expansions < MaxExpansions)
        {
            // The clock is read every so often, not every cell.
            if ((s.Expansions & 63) == 63 && sw.Elapsed.TotalMilliseconds >= budgetMs) return;
            s.Expansions++;
            var current = open.Dequeue();
            if (current == s.Goal)
            {
                var result = new List<Vector2I>();
                var c = s.Goal;
                while (c != s.Start) { result.Add(c); c = nodes[c].From; }
                result.Reverse();
                Finish(s, result);
                return;
            }
            ref var here = ref CollectionsMarshal.GetValueRefOrNullRef(nodes, current);
            if (here.Closed) continue;
            here.Closed = true;
            float gHere = here.G;

            int moves = Neighbours(current);
            for (int i = 0; i < moves; i++)
            {
                var (n, cost) = _moves[i];
                float tentative = gHere + cost;
                ref var next = ref CollectionsMarshal.GetValueRefOrAddDefault(nodes, n, out bool seen);
                if (seen && (next.Closed || tentative >= next.G)) continue;
                next.G = tentative;
                next.From = current;
                open.Enqueue(n, tentative + IsoGrid.ToWorld(n).DistanceTo(s.GoalPos) * s.Weight);
            }
        }
        Finish(s, new List<Vector2I>()); // empty: no route
    }

    private void Finish(Search s, List<Vector2I> result, bool cached = false)
    {
        s.Result = result; s.Done = true;
        if (cached) return;
        Calls++;
        if (result.Count == 0 && s.Start != s.Goal)
        {
            if (_failed.Count > 4000) _failed.Clear();
            _failed[s.FailKey] = Time.GetTicksMsec() + 2000;
        }
    }
}
