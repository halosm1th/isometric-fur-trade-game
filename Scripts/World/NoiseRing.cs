using Godot;

namespace IsometricFurTradeGame.Scripts.World;

// Sound you can see: a ring that spreads to the distance the sound carried
// and fades. Footsteps are faint; a musket is not.
public partial class NoiseRing : Node2D
{
    public float Radius = 200f;
    public float Life = 0.6f;
    public Color Tint = new(1f, 0.95f, 0.8f, 0.35f);
    private float _age;

    public override void _Process(double delta)
    {
        _age += (float)delta;
        if (_age >= Life) { QueueFree(); return; }
        QueueRedraw();
    }

    public override void _Draw()
    {
        float t = _age / Life;
        float r = Radius * Mathf.Sqrt(t);
        var c = Tint; c.A *= 1f - t;
        // The world is isometric: sound rings are squashed to the ground plane.
        DrawSetTransform(Vector2.Zero, 0f, new Vector2(1f, 0.5f));
        DrawArc(Vector2.Zero, r, 0f, Mathf.Tau, 32, c, 2f);
    }
}
