using System.Collections.Generic;
using Godot;

namespace IsometricFurTradeGame.Scripts.World;

// How high the ground stands in the mountains (the Cordillera of the map).
// The atlas gives no heights, but in mountain country the valleys are where
// the rivers run and the land falls away at the mountain front: so the
// height of a cell is how far it lies from the nearest river, lake or
// country that is not mountain, roughened so the ranges break into ridges
// and peaks. Worked out once, the first time it is asked.
//
// What it decides: the valley floors green and wooded, the slopes of rock
// and pine, the bare rock and scree above the trees, and the snowfields
// round the peaks that lie all year (TerrainPainter); how thick the trees
// stand (WorldLayout.ForestDensity); and how much colder it is up there
// (Agents/Climate) - the high country is under snow long before the valleys.
public static class Relief
{
    // Where each band begins, in steps above the valley floor: set by how
    // much of the mountain country lies in it (Build) - a fifth valley floor,
    // the top tenth under snow all year, as the ranges are in July.
    public static int Slope = 3, Rock = 9, Alpine = 15, Peak = 21;
    private static byte[] _height;

    public static int At(Vector2I c)
    {
        if (!WorldMap.Loaded) return 0;
        _height ??= Build();
        if (c.X < 0 || c.Y < 0 || c.X >= WorldMap.Columns || c.Y >= WorldMap.Rows) return 0;
        return _height[c.Y * WorldMap.Columns + c.X];
    }

    public enum Band { Low, Valley, Slope, Rock, Alpine, Peak }
    public static Band BandAt(Vector2I c)
    {
        if (WorldMap.At(c) != Ground.Cordillera) return Band.Low;
        int h = At(c);
        // The traders built in the valleys: a post's ground is never above the slopes.
        if (h >= Rock && Posts.NearAny(IsoGrid.ToWorld(c), 1200f)) return Band.Slope;
        return h < Slope ? Band.Valley : h < Rock ? Band.Slope : h < Alpine ? Band.Rock : h < Peak ? Band.Alpine : Band.Peak;
    }

    // Degrees colder than the valley at the same place: fourteen at the
    // snowline, a little more on the summits.
    public static float Cold(Vector2I c) => WorldMap.At(c) == Ground.Cordillera ? Mathf.Clamp(At(c) / (float)Mathf.Max(1, Peak), 0f, 1.25f) * 14f : 0f;

    private static byte[] Build()
    {
        int w = WorldMap.Columns, h = WorldMap.Rows;
        var dist = new byte[w * h];
        var queue = new Queue<Vector2I>();
        // Everything that is not mountain is the floor the mountains rise from.
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var c = new Vector2I(x, y);
                if (WorldMap.At(c) == Ground.Cordillera) dist[y * w + x] = byte.MaxValue;
                else queue.Enqueue(c);
            }
        while (queue.Count > 0)
        {
            var c = queue.Dequeue();
            int d = dist[c.Y * w + c.X];
            if (d >= 60) continue;
            foreach (var n in IsoGrid.EdgeNeighbours(c))
            {
                if (n.X < 0 || n.Y < 0 || n.X >= w || n.Y >= h) continue;
                int i = n.Y * w + n.X;
                if (dist[i] <= d + 1) continue;
                dist[i] = (byte)(d + 1);
                queue.Enqueue(n);
            }
        }
        // The atlas has only the great rivers; the ranges between them are cut
        // by a hundred lesser valleys it does not draw. Those are the low lines
        // of a ridged noise - zero along a winding line, rising to either side
        // - and the ground is as high as the nearer of the two lets it be.
        // Over that, a swell, so some ridges stand higher than others.
        var height = new byte[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                int d = dist[i] == byte.MaxValue ? 60 : dist[i];
                if (d == 0) continue;
                var c = new Vector2I(x, y);
                float side = Mathf.Abs(Smooth(c, 9, 47) - 0.5f) * 2f;          // 0 in a side valley, 1 on the ridge between
                side = Mathf.Min(side, Mathf.Abs(Smooth(c, 5, 53) - 0.5f) * 2.6f); // and its own side creeks
                float swell = (Smooth(c, 16, 41) - 0.5f) * 12f + (Smooth(c, 3, 43) - 0.5f) * 4f;
                float v = Mathf.Min(d * 1.2f, 2f + side * 34f) + swell * Mathf.Clamp(d / 6f, 0f, 1f);
                height[i] = (byte)Mathf.Clamp(Mathf.RoundToInt(v), 1, 60);
            }
        // The bands by share of the mountain country.
        var count = new int[61]; int total = 0;
        for (int i = 0; i < height.Length; i++) if (dist[i] != 0) { count[height[i]]++; total++; }
        int At(float share) { int run = 0; for (int k = 0; k <= 60; k++) { run += count[k]; if (run >= share * total) return Mathf.Min(k + 1, 60); } return 60; }
        if (total > 0) { Slope = At(0.2f); Rock = At(0.5f); Alpine = At(0.75f); Peak = At(0.9f); }
        GD.Print($"[relief] {total} mountain cells: slope from {Slope}, rock {Rock}, alpine {Alpine}, peaks {Peak}");
        return height;
    }

    // Value noise on a grid of `size` cells (rows count half), smoothly blended.
    private static float Smooth(Vector2I c, int size, int salt)
    {
        float fx = c.X / (float)size, fy = c.Y / (float)(size * 2);
        int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
        float tx = fx - x0, ty = fy - y0;
        tx = tx * tx * (3f - 2f * tx); ty = ty * ty * (3f - 2f * ty);
        float a = IsoGrid.Hash(new Vector2I(x0, y0), salt), b = IsoGrid.Hash(new Vector2I(x0 + 1, y0), salt);
        float c2 = IsoGrid.Hash(new Vector2I(x0, y0 + 1), salt), d = IsoGrid.Hash(new Vector2I(x0 + 1, y0 + 1), salt);
        return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c2, d, tx), ty);
    }
}
