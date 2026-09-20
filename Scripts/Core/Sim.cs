using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Ecology;
using IsometricFurTradeGame.Scripts.Economy;
using IsometricFurTradeGame.Scripts.World;

namespace IsometricFurTradeGame.Scripts.Core;

// Where the running scene's pieces register themselves so agents can find
// the terrain, the buildings, the ledgers and each other without walking
// the node tree. Cleared when the scene reloads.
public static class Sim
{
    public static TerrainPainter Terrain;
    public static StructurePainter Structures;
    public static WorldSpawner Spawner;
    public static Pathfinder Paths;
    public static FortLedger Fort;
    public static BandLedger Band;
    public static EcologyManager Ecology;
    public static Ship Ship;
    public static Agent Player;

    public static readonly List<Agent> Agents = new();
    public static readonly List<Npc> Npcs = new();
    public static readonly List<Animal> Animals = new();

    public static void Register(Agent a)
    {
        if (!Agents.Contains(a)) Agents.Add(a);
        if (a is Npc n && !Npcs.Contains(n)) Npcs.Add(n);
        if (a is Animal an && !Animals.Contains(an)) Animals.Add(an);
    }

    public static void Unregister(Agent a)
    {
        Agents.Remove(a);
        if (a is Npc n) Npcs.Remove(n);
        if (a is Animal an) Animals.Remove(an);
    }

    public static void Reset()
    {
        Terrain = null; Structures = null; Spawner = null; Paths = null;
        Fort = null; Band = null; Ecology = null; Ship = null; Player = null;
        Agents.Clear(); Npcs.Clear(); Animals.Clear();
    }

    // Log line to the console and, if a HUD listener exists, the screen.
    public static event System.Action<string> OnNotice;
    public static void Notice(string text)
    {
        GD.Print($"[world] {text}");
        OnNotice?.Invoke(text);
    }

    public static IEnumerable<Agent> AgentsWithin(Vector2 pos, float radius)
    {
        float r2 = radius * radius;
        foreach (var a in Agents)
        {
            if (a == null || !GodotObject.IsInstanceValid(a) || a.IsDead) continue;
            if (a.GlobalPosition.DistanceSquaredTo(pos) <= r2) yield return a;
        }
    }
}
