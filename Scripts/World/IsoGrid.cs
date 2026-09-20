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
    private static int Mod2(int v) => ((v % 2) + 2) % 2;

    public static Vector2I DownRight(Vector2I c) =>
        Mod2(c.Y) == 0 ? new Vector2I(c.X, c.Y + 1) : new Vector2I(c.X + 1, c.Y + 1);

    public static Vector2I DownLeft(Vector2I c) =>
        Mod2(c.Y) == 0 ? new Vector2I(c.X - 1, c.Y + 1) : new Vector2I(c.X, c.Y + 1);

    // Cell reached by walking `right` steps down-right then `down` steps
    // down-left from `topCorner`. Together these span a rhombus that reads as
    // a rectangular room on screen.
    public static Vector2I Cell(Vector2I topCorner, int right, int down)
    {
        var c = topCorner;
        for (int i = 0; i < right; i++) c = DownRight(c);
        for (int j = 0; j < down; j++) c = DownLeft(c);
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
}
