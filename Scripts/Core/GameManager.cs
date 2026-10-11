using Godot;

namespace IsometricFurTradeGame.Scripts.Core;

// Autoloaded singleton. Every start path (HBC, NWC, Cree, Assiniboine;
// Core/Starts.cs) begins in the one world scene, York Factory and the country
// round it; there are no separate per-post scenes. The world map streams the
// rest (WorldMap, chunk streaming), so a post elsewhere is a place in that
// world rather than a scene of its own.
public partial class GameManager : Node
{
    public static GameManager Instance { get; private set; }

    public const string WorldScene = "res://Scenes/World/YorkFactoryExterior.tscn";

    public override void _Ready()
    {
        Instance = this;
    }
}
