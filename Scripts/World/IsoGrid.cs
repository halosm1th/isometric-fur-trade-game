using System.Collections.Generic;
using Godot;

namespace IsometricFurTradeGame.Scripts.World;

// Helpers for Godot's staggered isometric grid.
//
// In this grid x+1 moves purely sideways (a full tile) while y+1 moves half a
// tile down and half a tile across, alternating by row parity. So a "row of
// cells in x" is NOT a contiguous line - those cells only touch at their
// vertices. Anything that has to look like a straight line (a wall, a fence,
// a road) must instead walk one of the two isometric axes below.
public static class IsoGrid
{
    public const int TileW = 128, TileH = 64;

    private static int Mod2(int v) => ((v % 2) + 2) % 2;

    public static Vector2I DownRight(Vector2I c) =>
        Mod2(c.Y) == 0 ? new Vector2I(c.X, c.Y + 1) : new Vector2I(c.X + 1, c.Y + 1);

    public static Vector2I DownLeft(Vector2I c) =>
        Mod2(c.Y) == 0 ? new Vector2I(c.X - 1, c.Y + 1) : new Vector2I(c.X, c.Y + 1);

    public static Vector2I UpLeft(Vector2I c) =>
        Mod2(c.Y) == 0 ? new Vector2I(c.X - 1, c.Y - 1) : new Vector2I(c.X, c.Y - 1);

    public static Vector2I UpRight(Vector2I c) =>
        Mod2(c.Y) == 0 ? new Vector2I(c.X, c.Y - 1) : new Vector2I(c.X + 1, c.Y - 1);

    // The four cells sharing an edge with c, in the order NW, NE, SW, SE.
    public static IEnumerable<Vector2I> EdgeNeighbours(Vector2I c)
    {
        yield return UpLeft(c);
        yield return UpRight(c);
        yield return DownLeft(c);
        yield return DownRight(c);
    }

    // The four cells touching c only at a vertex: left, right, above, below.
    public static IEnumerable<Vector2I> CornerNeighbours(Vector2I c)
    {
        yield return new Vector2I(c.X - 1, c.Y);
        yield return new Vector2I(c.X + 1, c.Y);
        yield return new Vector2I(c.X, c.Y - 2);
        yield return new Vector2I(c.X, c.Y + 2);
    }

    // Same maths as TileMapLayer.MapToLocal for a 128x64 stacked grid, so
    // code with no layer in hand can still place things.
    public static Vector2 ToWorld(Vector2I c) =>
        new(TileW / 2f + TileW * c.X + (Mod2(c.Y) == 1 ? TileW / 2f : 0f), TileH / 2f + TileH / 2f * c.Y);

    public static Vector2I FromWorld(Vector2 p)
    {
        // Nearest of the candidate cells around the point.
        int y = Mathf.RoundToInt((p.Y - TileH / 2f) / (TileH / 2f));
        Vector2I best = default; float bestD = float.MaxValue;
        for (int dy = -1; dy <= 1; dy++)
        {
            int yy = y + dy;
            int x = Mathf.RoundToInt((p.X - TileW / 2f - (Mod2(yy) == 1 ? TileW / 2f : 0f)) / TileW);
            for (int dx = -1; dx <= 1; dx++)
            {
                var c = new Vector2I(x + dx, yy);
                float d = ToWorld(c).DistanceSquaredTo(p);
                if (d < bestD) { bestD = d; best = c; }
            }
        }
        return best;
    }

    // Cell reached by walking `right` steps down-right then `down` steps
    // down-left from `topCorner`. Together these span a rhombus that reads as
    // a rectangular room on screen.
    public static Vector2I Cell(Vector2I topCorner, int right, int down)
    {
        var c = topCorner;
        for (int i = 0; i < Mathf.Abs(right); i++) c = right > 0 ? DownRight(c) : UpLeft(c);
        for (int j = 0; j < Mathf.Abs(down); j++) c = down > 0 ? DownLeft(c) : UpRight(c);
        return c;
    }

    public static IEnumerable<Vector2I> Rhombus(Vector2I topCorner, int width, int depth)
    {
        for (int i = 0; i < width; i++)
        {
            for (int j = 0; j < depth; j++)
            {
                yield return Cell(topCorner, i, j);
            }
        }
    }

    // Deterministic per-cell noise in [0,1).
    public static float Hash(Vector2I c, int salt = 0)
    {
        uint h = (uint)(c.X * 73856093) ^ (uint)(c.Y * 19349663) ^ (uint)(salt * 83492791);
        h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
        return (h & 0xffffff) / 16777216f;
    }
}
