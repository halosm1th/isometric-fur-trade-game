using Godot;

namespace IsometricFurTradeGame.Scripts.World;

// The picture of a cell on fire: flames and a hot light. The fire itself
// is state in StructurePainter; this is only what you see.
public partial class Blaze : Node2D
{
    private PointLight2D _light;
    private Sprite2D _flame;
    private float _t;

    public override void _Ready()
    {
        var grad = new Gradient();
        grad.SetColor(0, new Color(1f, 0.85f, 0.5f, 1f));
        grad.SetColor(1, new Color(0f, 0f, 0f, 0f));
        var tex = new GradientTexture2D { Gradient = grad, Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(0.5f, 0f), Width = 128, Height = 128 };
        _light = new PointLight2D { Texture = tex, TextureScale = 2.2f, Color = new Color(1f, 0.6f, 0.25f), Energy = 2f, Position = new Vector2(0, -30) };
        AddChild(_light);
        _flame = new Sprite2D { Texture = GD.Load<Texture2D>("res://assets/tiles/props/campfire.png"), Centered = false, Offset = new Vector2(-64, -214), Modulate = new Color(1f, 0.8f, 0.6f, 0.95f), Scale = new Vector2(1.4f, 1.6f) };
        AddChild(_flame);
        ZIndex = 3;
    }

    public override void _Process(double delta)
    {
        _t += (float)delta * 11f;
        float pulse = 1f + 0.2f * Mathf.Sin(_t) * Mathf.Sin(_t * 0.43f);
        _light.Energy = 2f * pulse;
        _flame.Scale = new Vector2(1.4f, 1.6f * (0.9f + 0.1f * Mathf.Sin(_t * 1.7f)));
    }
}
