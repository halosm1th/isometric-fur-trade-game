using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Core;

namespace IsometricFurTradeGame.Scripts.UI;

// The musket's sights on the pad (Player/PlayerController.ReadyGun): a ring
// in the world that the right stick moves, read straight off the stick
// every frame. It used to be the Windows mouse pointer, warped by the stick,
// which did nothing at all when the warp or the "is a menu up" test failed.
// Over a mark it closes and turns red, and that mark is what RT shoots.
public partial class Sights : Node2D
{
    public Agent Owner;
    public Agent Mark { get; private set; }
    private const float Reach = 700f, Speed = 760f;
    private bool _logged;

    public override void _Ready()
    {
        TopLevel = true;
        ZIndex = 4000;
        ZAsRelative = false;
    }

    public override void _Process(double delta)
    {
        if (Owner == null || !IsInstanceValid(Owner)) { QueueFree(); return; }
        float dt = (float)Mathf.Min(delta, 0.1);
        var rs = Pad.RightStick();
        if (rs != Vector2.Zero)
        {
            // Screen-steady speed whatever the zoom.
            float zoom = GetViewport().GetCamera2D()?.Zoom.X ?? 1f;
            GlobalPosition += rs * rs.Length() * Speed / Mathf.Max(0.2f, zoom) * dt;
            if (!_logged) { _logged = true; GD.Print($"[aim] right stick moves the sights ({rs.X:0.00},{rs.Y:0.00})"); }
        }
        // Held within the gun's reach of the shooter.
        var from = Owner.GlobalPosition + new Vector2(0, -40);
        var off = GlobalPosition - from;
        if (off.Length() > Reach) GlobalPosition = from + off.Normalized() * Reach;

        Agent best = null; float bd = 80f;
        foreach (var a in Sim.AgentsWithin(GlobalPosition + new Vector2(0, 40), 160f))
        {
            if (a == Owner || a.IsDead) continue;
            float d = (a.GlobalPosition + new Vector2(0, -40)).DistanceTo(GlobalPosition);
            if (d < bd) { bd = d; best = a; }
        }
        Mark = best;
        QueueRedraw();
    }

    public override void _Draw()
    {
        bool on = Mark != null;
        var col = on ? new Color(0.95f, 0.25f, 0.2f) : new Color(1f, 0.95f, 0.8f);
        var shadow = new Color(0, 0, 0, 0.6f);
        float r = on ? 11f : 16f;
        DrawArc(Vector2.Zero, r + 1f, 0, Mathf.Tau, 32, shadow, 3f);
        DrawArc(Vector2.Zero, r, 0, Mathf.Tau, 32, col, 2f);
        foreach (var d in new[] { Vector2.Up, Vector2.Down, Vector2.Left, Vector2.Right })
        {
            DrawLine(d * (r - 5f), d * (r + 7f), shadow, 3f);
            DrawLine(d * (r - 5f), d * (r + 7f), col, 1.6f);
        }
        if (on) DrawString(ThemeDB.FallbackFont, new Vector2(-100, -r - 10f), Mark.Describe(), HorizontalAlignment.Center, 200, 13, col);
    }
}
