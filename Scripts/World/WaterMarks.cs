using System.Collections.Generic;
using Godot;

namespace IsometricFurTradeGame.Scripts.World;

// What the water tiles don't show (WorldMap.Depth): the shallows, sandy
// bottom showing through, and the white water of a rapid, broken and
// moving. One of these over each streamed chunk that has any (TerrainPainter).
public partial class WaterMarks : Node2D
{
    public TerrainPainter Painter;
    public readonly List<Vector2> Shallow = new();
    public readonly List<Vector2> Rapids = new();
    private float _t, _redraw;

    private static readonly Vector2[] Diamond = { new(0, -32), new(64, 0), new(0, 32), new(-64, 0) };
    private static readonly Color Sand = new(0.86f, 0.79f, 0.58f, 0.32f);
    private static readonly Color Ripple = new(0.92f, 0.95f, 1f, 0.35f);
    private static readonly Color Foam = new(0.96f, 0.98f, 1f, 0.8f);

    public override void _Process(double delta)
    {
        if (Rapids.Count == 0) { SetProcess(false); return; }
        _t += (float)delta;
        // The white water moves; ten pictures a second is plenty for foam.
        if ((_redraw -= (float)delta) <= 0f) { _redraw = 0.1f; QueueRedraw(); }
    }

    public override void _Draw()
    {
        // Not over ice: a frozen river is the winter road, shallow or not.
        foreach (var p in Shallow)
        {
            if (Frozen(p)) continue;
            var pts = new Vector2[4];
            for (int i = 0; i < 4; i++) pts[i] = p + Diamond[i] * 0.94f;
            DrawColoredPolygon(pts, Sand);
            DrawLine(p + new Vector2(-30, -4), p + new Vector2(-6, -9), Ripple, 2f);
            DrawLine(p + new Vector2(8, 7), p + new Vector2(30, 3), Ripple, 2f);
        }
        foreach (var p in Rapids)
        {
            if (Frozen(p)) continue;
            // Streaks of foam running down the cell, each at its own pace.
            uint seed = (uint)(p.X * 7 + p.Y * 13);
            for (int k = 0; k < 7; k++)
            {
                float lane = ((seed + (uint)k * 2654435761u) % 1000u) / 1000f;
                float run = Mathf.PosMod(_t * (34f + 9f * k) + lane * 90f, 90f) - 45f;
                var a = p + new Vector2(run, (lane - 0.5f) * 40f + run * 0.5f * 0.5f);
                DrawLine(a, a + new Vector2(14f, 4f), Foam, 2.5f);
            }
            DrawCircle(p + new Vector2(-18, 6), 4f, Foam);
            DrawCircle(p + new Vector2(20, -8), 3f, Foam);
        }
    }

    private bool Frozen(Vector2 local) => Painter != null && Painter.FrozenAt(Painter.LocalToMap(local));
}
