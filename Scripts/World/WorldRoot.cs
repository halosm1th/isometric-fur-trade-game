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
        Campaign.Reset();
        Posts.Reset();
        Net.Net.I?.ResetWorld();
        WorldLayout.SetCamp(WorldLayout.WinterCamp);
    }

    public override void _Ready()
    {
        // The inland post's books live here rather than in the baked scene.
        AddChild(new Economy.OutpostLedger { Name = "OutpostLedger" });
        AddChild(new Economy.BandLedger { Name = "AssiniboineLedger", Nation = Agents.Faction.Assiniboine });
        Posts.Begin(this);
        var ysort = GetNodeOrNull<Node2D>("YSort");
        if (ysort != null) Net.Net.I?.CallDeferred(nameof(Net.Net.WorldReady), ysort);
        Net.Net.I?.ClientWorldReady();
    }
}
