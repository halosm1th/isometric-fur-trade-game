using Godot;
using IsometricFurTradeGame.Scripts.Core;

namespace IsometricFurTradeGame.Scripts.World;

// Sits on the scene root: clears the static registry when the scene
// (re)loads so nothing points at freed nodes.
public partial class WorldRoot : Node2D
{
    public override void _EnterTree()
    {
        Sim.Reset();
    }
}
