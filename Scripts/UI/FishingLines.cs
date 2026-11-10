using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.World;
using NetHub = IsometricFurTradeGame.Scripts.Net.Net;

namespace IsometricFurTradeGame.Scripts.UI;

// Other people's lines in the water (Agents/Goals FishGoal, World/Angling):
// the float bobbing off the bank, going under when a fish takes it, the line
// drawn taut while he plays it, and the fish swung up onto the bank - or the
// float popping back up when it gets away. Drawn for anyone fishing near the
// local player, from the cue that comes in the snapshot (Agent.FishCue).
public partial class FishingLines : Node2D
{
    private sealed class Line
    {
        public Vector2 Float;
        public int Cue, LastCue;
        public float Since;        // seconds in this cue
        public float Seen;         // last frame it was drawn (seconds)
        public float Phase = (float)GD.RandRange(0.0, 10.0);
    }
    private readonly Dictionary<Agent, Line> _lines = new();
    private float _now;
    private const float Reach = 1500f;

    public override void _Ready()
    {
        TopLevel = true; GlobalPosition = Vector2.Zero;
        ZIndex = 59; ZAsRelative = false;
    }

    private IEnumerable<Agent> Everyone() => NetHub.I != null && NetHub.I.Agents.Count > 0 ? NetHub.I.Agents.Values : Sim.Agents;

    public override void _Process(double delta)
    {
        float dt = (float)delta; _now += dt;
        var me = Sim.Player;
        if (me == null || !IsInstanceValid(me)) return;
        foreach (var a in Everyone())
        {
            if (a == null || !IsInstanceValid(a) || a == me || a.IsDead) continue;
            int cue = a.FishCue;
            if (cue <= 0) continue;
            if (a.GlobalPosition.DistanceSquaredTo(me.GlobalPosition) > Reach * Reach) continue;
            if (!_lines.TryGetValue(a, out var l))
            {
                var f = FloatFor(a.GlobalPosition);
                if (f == null) continue;
                l = new Line { Float = f.Value, LastCue = cue, Cue = cue };
                _lines[a] = l;
            }
            if (cue != l.Cue) { l.LastCue = l.Cue; l.Cue = cue; l.Since = 0f; }
            l.Since += dt; l.Phase += dt; l.Seen = _now;
        }
        // Lines nobody renewed: wound in.
        List<Agent> gone = null;
        foreach (var (a, l) in _lines) if (_now - l.Seen > 0.5f || !IsInstanceValid(a)) (gone ??= new()).Add(a);
        if (gone != null) foreach (var a in gone) _lines.Remove(a);
        QueueRedraw();
    }

    // Where his float sits: open water a tile or two out from where he stands.
    private static Vector2? FloatFor(Vector2 at)
    {
        if (Sim.Terrain == null) return null;
        var c = IsoGrid.FromWorld(at);
        Vector2? best = null; float bestScore = float.MaxValue;
        for (int dx = -3; dx <= 3; dx++)
            for (int dy = -6; dy <= 6; dy++)
            {
                var n = new Vector2I(c.X + dx, c.Y + dy);
                if (!Sim.Terrain.IsWaterOrIce(n)) continue;
                var w = IsoGrid.ToWorld(n);
                float d = w.DistanceTo(at);
                if (d < 40f || d > 260f) continue;
                float score = Mathf.Abs(d - 120f);
                if (score < bestScore) { bestScore = score; best = w; }
            }
        return best;
    }

    public override void _Draw()
    {
        foreach (var (a, l) in _lines)
        {
            if (!IsInstanceValid(a)) continue;
            var who = a.GlobalPosition;
            var tip = who + new Vector2(l.Float.X >= who.X ? 14f : -14f, -44f);
            bool fighting = l.Cue == (int)Agent.FishState.Fighting;
            var bob = l.Float;
            if (fighting) bob += new Vector2(Mathf.Sin(l.Phase * 9f) * 5f, Mathf.Cos(l.Phase * 7f) * 2.5f);

            // The line: slack while he waits, taut while he plays a fish.
            var mid = (tip + bob) / 2f + new Vector2(0, fighting ? -6f : 14f);
            var pts = new Vector2[9];
            for (int i = 0; i < pts.Length; i++)
            {
                float k = i / (pts.Length - 1f);
                pts[i] = tip * (1 - k) * (1 - k) + mid * 2 * k * (1 - k) + bob * k * k;
            }
            DrawPolyline(pts, new Color(0.92f, 0.9f, 0.82f, 0.75f), 1.1f, true);

            switch ((Agent.FishState)l.Cue)
            {
                case Agent.FishState.Waiting:
                {
                    float twitch = Mathf.Max(0f, Mathf.Sin(l.Phase * 1.3f) - 0.93f) * 40f;
                    var b = bob + new Vector2(0, Mathf.Sin(l.Phase * 2.5f) * 1.2f + twitch);
                    DrawFloat(b);
                    if (l.LastCue == (int)Agent.FishState.Lost && l.Since < 1.2f) Ripple(l.Float, l.Since);
                    break;
                }
                case Agent.FishState.Fighting:
                    Ripple(bob, l.Phase % 0.9f);
                    Fish(bob + new Vector2(-6f, 2f), Mathf.Sin(l.Phase * 11f) * 0.6f, 16f);
                    DrawCircle(bob + new Vector2(0, 3f), 3f, new Color(0.8f, 0.15f, 0.1f, 0.6f));
                    break;
                case Agent.FishState.Landed:
                {
                    // Swung up out of the water and onto the bank.
                    float k = Mathf.Clamp(l.Since / 0.7f, 0f, 1f);
                    var p = l.Float.Lerp(who + new Vector2(0, -20f), k) + new Vector2(0, -4f * k * (1f - k) * 70f);
                    Fish(p, k * 6f, 16f, new Color(0.75f, 0.78f, 0.7f, 1f - 0.6f * Mathf.Max(0f, k - 0.8f) / 0.2f));
                    if (l.Since < 1.2f) Ripple(l.Float, l.Since);
                    break;
                }
                case Agent.FishState.Lost:
                    DrawFloat(bob + new Vector2(0, -Mathf.Max(0f, 4f - l.Since * 12f)));
                    Ripple(l.Float, l.Since);
                    break;
            }
        }
    }

    private void DrawFloat(Vector2 b)
    {
        DrawCircle(b, 4f, new Color(0.85f, 0.15f, 0.1f));
        DrawCircle(b + new Vector2(0, -3f), 2.7f, new Color(0.96f, 0.95f, 0.9f));
    }

    private void Ripple(Vector2 at, float age)
    {
        if (age > 1.4f) return;
        DrawSetTransform(at, 0f, new Vector2(1f, 0.5f));
        DrawArc(Vector2.Zero, 6f + age * 24f, 0f, Mathf.Tau, 24, new Color(0.9f, 0.95f, 1f, 0.5f * (1f - age / 1.4f)), 1.4f);
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }

    private void Fish(Vector2 at, float angle, float len, Color? colour = null)
    {
        var c = colour ?? new Color(0.04f, 0.08f, 0.12f, 0.45f);
        DrawSetTransform(at, angle, new Vector2(1f, 0.6f));
        var body = new Vector2[12];
        for (int i = 0; i < body.Length; i++) { float t = Mathf.Tau * i / body.Length; body[i] = new Vector2(Mathf.Cos(t) * len, Mathf.Sin(t) * len * 0.38f); }
        DrawColoredPolygon(body, c);
        var root = new Vector2(-len * 0.9f, 0f);
        DrawColoredPolygon(new[] { root, root + new Vector2(-len * 0.55f, len * 0.4f), root + new Vector2(-len * 0.55f, -len * 0.4f) }, c);
        DrawSetTransform(Vector2.Zero, 0f, Vector2.One);
    }
}
