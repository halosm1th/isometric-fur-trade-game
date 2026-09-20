using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Core;

namespace IsometricFurTradeGame.Scripts.World;

// A birchbark canoe: the design doc's "enter vehicle" verb in its simplest
// form. Board it from the shore, paddle on open water (faster than walking),
// step onto any bank and it stays where you left it. Frozen in all winter.
public partial class Canoe : Node2D
{
    public int Index;
    public int RiderNetId;          // 0 = empty
    private Sprite2D _sprite;

    public bool IsFree => RiderNetId == 0;

    public override void _Ready()
    {
        _sprite = new Sprite2D { Texture = GD.Load<Texture2D>("res://assets/sprites/canoe.png"), Offset = new Vector2(0, -14) };
        AddChild(_sprite);
        AddToGroup("canoes");
    }

    public void SetState(Vector2 pos, int rider)
    {
        RiderNetId = rider;
        Position = pos;
        Visible = rider == 0;
    }

    public override void _Process(double delta)
    {
        Visible = RiderNetId == 0;
    }
}
