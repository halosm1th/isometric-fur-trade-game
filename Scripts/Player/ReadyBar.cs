using Godot;

namespace IsometricFurTradeGame.Scripts.Player;

// Weapon cooldown over the player's head: fills back to ready.
public partial class ReadyBar : Node2D
{
    public float Fraction;

    public override void _Draw()
    {
        DrawRect(new Rect2(-20, 0, 40, 4), new Color(0, 0, 0, 0.7f));
        DrawRect(new Rect2(-19, 1, 38 * (1f - Fraction), 2), new Color(0.95f, 0.85f, 0.4f));
    }
}
