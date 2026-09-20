using Godot;

namespace IsometricFurTradeGame.Scripts.Agents;

// Tiny visual grammar for fights: a shot is a line and a flash for a
// tenth of a second. Enough to read who fired at whom.
public static class CombatFx
{
    public static void Shot(Agent from, Vector2 to, bool musket)
    {
        var tree = from.GetTree();
        if (tree?.CurrentScene == null) return;
        var start = from.GlobalPosition + new Vector2(0, -50);
        var line = new Line2D
        {
            Width = musket ? 2f : 1.5f,
            DefaultColor = musket ? new Color(1f, 0.95f, 0.7f, 0.9f) : new Color(0.8f, 0.7f, 0.5f, 0.9f),
            ZIndex = 30, ZAsRelative = false,
        };
        line.AddPoint(start);
        line.AddPoint(to);
        tree.CurrentScene.AddChild(line);
        if (musket)
        {
            var flash = new PointLight2D
            {
                Position = start, Energy = 2.5f, Color = new Color(1f, 0.85f, 0.5f),
                Texture = FlashTexture(), TextureScale = 1.2f, ZIndex = 30, ZAsRelative = false,
            };
            tree.CurrentScene.AddChild(flash);
            tree.CreateTimer(0.08).Timeout += () => { if (GodotObject.IsInstanceValid(flash)) flash.QueueFree(); };
        }
        tree.CreateTimer(musket ? 0.1 : 0.16).Timeout += () => { if (GodotObject.IsInstanceValid(line)) line.QueueFree(); };
    }

    private static Texture2D _flash;
    private static Texture2D FlashTexture()
    {
        if (_flash != null) return _flash;
        var g = new Gradient();
        g.SetColor(0, new Color(1f, 1f, 1f, 1f));
        g.SetColor(1, new Color(0f, 0f, 0f, 0f));
        _flash = new GradientTexture2D { Gradient = g, Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(0.5f, 0f), Width = 128, Height = 128 };
        return _flash;
    }
}
