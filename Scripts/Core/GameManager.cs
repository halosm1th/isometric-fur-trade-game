using System.Collections.Generic;
using Godot;

namespace IsometricFurTradeGame.Scripts.Core;

public enum StartLocation
{
    YorkFactory,
    FortWilliam,
    FortGarry,
}

// Autoloaded singleton. Holds which post the player starts/arrives at and
// maps each one to its exterior scene, so new posts can be added without
// touching the scenes that reference them.
public partial class GameManager : Node
{
    public static GameManager Instance { get; private set; }

    public StartLocation ChosenStart = StartLocation.YorkFactory;

    private readonly Dictionary<StartLocation, string> _scenePaths = new()
    {
        { StartLocation.YorkFactory, "res://Scenes/World/YorkFactoryExterior.tscn" },
        // Not built yet - added here so the selection system already knows about them.
        { StartLocation.FortWilliam, "res://Scenes/World/FortWilliamExterior.tscn" },
        { StartLocation.FortGarry, "res://Scenes/World/FortGarryExterior.tscn" },
    };

    public override void _Ready()
    {
        Instance = this;
    }

    public string GetStartScenePath()
    {
        return _scenePaths[ChosenStart];
    }
}
