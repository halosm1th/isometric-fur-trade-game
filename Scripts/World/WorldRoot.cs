using Godot;
using IsometricFurTradeGame.Scripts.Core;

namespace IsometricFurTradeGame.Scripts.World;

// Sits on the scene root: clears the static registry when the scene
// (re)loads, and hands the spawn parent to the network layer once every
// child has had its _Ready.
public partial class WorldRoot : Node2D
{
    public override void _EnterTree()
    {
        Sim.Reset();
        Net.Net.I?.ResetWorld();
        WorldLayout.SetCamp(WorldLayout.WinterCamp);
    }

    public override void _Ready()
    {
        var ysort = GetNodeOrNull<Node2D>("YSort");
        if (ysort != null) Net.Net.I?.CallDeferred(nameof(Net.Net.WorldReady), ysort);
        Net.Net.I?.ClientWorldReady();
    }
}
