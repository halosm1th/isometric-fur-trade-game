using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Ecology;
using IsometricFurTradeGame.Scripts.Economy;
using IsometricFurTradeGame.Scripts.World;

namespace IsometricFurTradeGame.Scripts.Core;

// Where the running scene's pieces register themselves so agents can find
// the terrain, the buildings, the ledgers and each other without walking
// the node tree. Cleared when the scene reloads.
public static class Sim
{
    public static TerrainPainter Terrain;
    public static StructurePainter Structures;
    public static WorldSpawner Spawner;
    public static Pathfinder Paths;
    public static FortLedger Fort;
    public static NwcLedger Nwc;
    public static OutpostLedger Outpost;

    public static IEnumerable<FortLedger> Posts()
    {
        if (Fort != null) yield return Fort;
        if (Nwc != null) yield return Nwc;
        if (Outpost != null) yield return Outpost;
    }
    public static BandLedger Band;
    public static BandLedger Assiniboine;
    public static readonly System.Collections.Generic.List<BandLedger> Bands = new();
    public static EcologyManager Ecology;
    public static Ship Ship;
    public static Agent Player;

    public static readonly List<Agent> Agents = new();
    public static readonly List<Npc> Npcs = new();
    public static readonly List<Animal> Animals = new();

    public static void Register(Agent a)
    {
        Net.Net.I?.Register(a);
        if (!Agents.Contains(a)) Agents.Add(a);
        if (a is Npc n && !Npcs.Contains(n)) Npcs.Add(n);
        if (a is Animal an && !Animals.Contains(an)) Animals.Add(an);
    }

    public static void Unregister(Agent a)
    {
        Net.Net.I?.Unregister(a);
        Agents.Remove(a);
        if (a is Npc n) Npcs.Remove(n);
        if (a is Animal an) Animals.Remove(an);
    }

    public static void Reset()
    {
        Terrain = null; Structures = null; Spawner = null; Paths = null;
        Fort = null; Band = null; Ecology = null; Ship = null; Player = null;
        Agents.Clear(); Npcs.Clear(); Animals.Clear();
        // The old scene's HUD and painters hung handlers here; they're gone
        // with it, and the new scene subscribes afresh in its _Ready.
        LocalPlayerChanged = null; OnHeadline = null;
        IsometricFurTradeGame.Scripts.World.WorldLayout.ForgetSites();
        Nwc = null; Outpost = null; Assiniboine = null; Bands.Clear(); Economy.Union.Reset();
        IsometricFurTradeGame.Scripts.Agents.Grudges.All.Clear(); UI.Acquaintance.Clear(); Mail.Abroad.Clear(); Economy.Council.Letters.Clear(); IsometricFurTradeGame.Scripts.Agents.Barks.Reset(); IsometricFurTradeGame.Scripts.Agents.Losses.Reset(); IsometricFurTradeGame.Scripts.Agents.Lies.Reset(); IsometricFurTradeGame.Scripts.Agents.Friends.Reset(); IsometricFurTradeGame.Scripts.Agents.Favours.Reset(); IsometricFurTradeGame.Scripts.Agents.Leverage.Reset(); Core.Starts.Reset(); Core.CharacterSheet.ResetKin();
    }

    // A sound in the world (design doc 15, 16): animals in range react, and
    // everyone sees the ring. Radius is how far it carries in px.
    public static void Noise(Vector2 pos, float radius, Agent source, string kind)
    {
        if (!Net.Net.IsServer) return;
        foreach (var a in AgentsWithin(pos, radius))
        {
            if (a is Animal an && an != source) an.Hear(pos, radius, kind);
            // A shot puts a whole house on its guard: nobody is taken unawares
            // twice in the same fight (design doc 15.6).
            else if (kind == "shot" && a is Agents.Npc man && man != source) man.Alerted = 25f;
        }
        Net.Net.I?.BroadcastNoise(pos, radius, kind);
    }

    public static void ShowNoise(Vector2 pos, float radius, string kind)
    {
        Audio.Noise(pos, radius, kind);
        var parent = Player?.GetParent() as Node2D;
        if (parent == null) return;
        // Only rings you could see: every footstep in the country used to get
        // a ring, drawn every frame wherever it was.
        var vp = parent.GetViewport();
        if (vp != null)
        {
            var seen = vp.GetCanvasTransform().AffineInverse() * vp.GetVisibleRect();
            if (!seen.Grow(radius).HasPoint(pos)) return;
        }
        var ring = new World.NoiseRing { Position = pos, Radius = radius, ZIndex = 5 };
        if (kind == "shot") { ring.Tint = new Color(1f, 0.85f, 0.5f, 0.7f); ring.Life = 1.1f; }
        else if (kind == "step") ring.Tint = new Color(1f, 1f, 1f, 0.16f);
        parent.AddChild(ring);
    }

    // Something that takes the screen for a moment: a death in the family,
    // a post lost, an accord broken (design doc P.4).
    public static event System.Action<string> OnHeadline;
    public static void Headline(string text)
    {
        // The director keeps a pacing memory of what has reached the player
        // lately (design doc 2.2).
        Director.Saw("headline", 1f);
        Notice(text);
        OnHeadline?.Invoke(text);
        if (Net.Net.IsServer) Net.Net.I?.BroadcastHeadline(text);
    }

    // Log line to the console and, if a HUD listener exists, the screen.
    public static event System.Action<string> OnNotice;
    public static System.Action<Player.PlayerController> LocalPlayerChanged;

    // World-wide notice: shown here and, from the server, on every client.
    public static void Notice(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        GD.Print($"[world] {text}");
        OnNotice?.Invoke(text);
        Net.Net.I?.BroadcastNotice(text);
    }

    // A notice that arrived from the server (don't re-broadcast).
    public static void NoticeLocal(string text) => OnNotice?.Invoke(text);
    public static void NoticeHeadlineLocal(string text) { OnNotice?.Invoke(text); OnHeadline?.Invoke(text); }

    public static IEnumerable<Player.PlayerController> Players()
    {
        if (Net.Net.I == null) { if (Player is Player.PlayerController p) yield return p; yield break; }
        foreach (var p in Net.Net.I.Players.Values) if (p != null && GodotObject.IsInstanceValid(p)) yield return p;
    }

    // Who is near a point. This is the most-called question in the game -
    // hearths, barks, witnesses, noise, a fight's odds - and it used to be a
    // scan of every agent in the world. With the country's own peoples in it
    // that is some nine hundred bodies, so the evening systems at a populated
    // place cost a frame each. The agents are bucketed into a coarse grid
    // instead, rebuilt once a frame (nine hundred cheap writes), and the
    // question only looks at the buckets it has to.
    private const float Bucket = 512f;
    private static readonly Dictionary<long, List<Agent>> _grid = new();
    private static ulong _gridFrame = ulong.MaxValue;

    private static long Key(int bx, int by) => ((long)bx << 32) ^ (uint)by;

    private static void BuildGrid()
    {
        ulong frame = Engine.GetProcessFrames();
        if (frame == _gridFrame) return;
        _gridFrame = frame;
        foreach (var list in _grid.Values) list.Clear();
        foreach (var a in Agents)
        {
            if (a == null || !GodotObject.IsInstanceValid(a) || a.IsDead) continue;
            if (a is Player.PlayerController { Ghost: true }) continue;   // the observer (Core/Observer)
            var p = a.GlobalPosition;
            long k = Key(Mathf.FloorToInt(p.X / Bucket), Mathf.FloorToInt(p.Y / Bucket));
            if (!_grid.TryGetValue(k, out var list)) _grid[k] = list = new List<Agent>(8);
            list.Add(a);
        }
    }

    // A struct, walked with foreach, so the question makes no garbage: it is
    // asked by every animal and every man every frame, and the iterator it
    // used to return was one of the largest things the collector swept.
    public static AgentsNear AgentsWithin(Vector2 pos, float radius)
    {
        BuildGrid();
        return new AgentsNear(pos, radius);
    }

    public readonly struct AgentsNear : IEnumerable<Agent>
    {
        private readonly Vector2 _pos;
        private readonly float _radius;
        public AgentsNear(Vector2 pos, float radius) { _pos = pos; _radius = radius; }
        public Enumerator GetEnumerator() => new(_pos, _radius);
        IEnumerator<Agent> IEnumerable<Agent>.GetEnumerator() => GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

        public struct Enumerator : IEnumerator<Agent>
        {
            private readonly Vector2 _pos;
            private readonly float _r2;
            private readonly int _x0, _x1, _y0, _y1;
            private int _bx, _by, _i;
            private List<Agent> _list;
            public Agent Current { get; private set; }
            object System.Collections.IEnumerator.Current => Current;

            public Enumerator(Vector2 pos, float radius)
            {
                _pos = pos; _r2 = radius * radius;
                _x0 = Mathf.FloorToInt((pos.X - radius) / Bucket); _x1 = Mathf.FloorToInt((pos.X + radius) / Bucket);
                _y0 = Mathf.FloorToInt((pos.Y - radius) / Bucket); _y1 = Mathf.FloorToInt((pos.Y + radius) / Bucket);
                _bx = _x0; _by = _y0 - 1; _i = 0; _list = null; Current = null;
            }

            public bool MoveNext()
            {
                while (true)
                {
                    if (_list != null)
                    {
                        while (_i < _list.Count)
                        {
                            var a = _list[_i++];
                            // The bucket was filled this frame, but a caller may have
                            // killed somebody since.
                            if (a == null || !GodotObject.IsInstanceValid(a) || a.IsDead) continue;
                            if (a.GlobalPosition.DistanceSquaredTo(_pos) <= _r2) { Current = a; return true; }
                        }
                        _list = null;
                    }
                    // Next bucket, column by column as before.
                    if (++_by > _y1) { _by = _y0; if (++_bx > _x1) { Current = null; return false; } }
                    if (_grid.TryGetValue(Key(_bx, _by), out var list)) { _list = list; _i = 0; }
                }
            }

            public void Reset() { _bx = _x0; _by = _y0 - 1; _i = 0; _list = null; Current = null; }
            public void Dispose() { }
        }
    }
}
