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
    public static NwcLedger Nwc;

    public static IEnumerable<FortLedger> Posts()
    {
        if (Fort != null) yield return Fort;
        if (Nwc != null) yield return Nwc;
    }
    public static BandLedger Band;
    public static EcologyManager Ecology;
    public static Ship Ship;
    public static Agent Player;

    public static readonly List<Agent> Agents = new();
    public static readonly List<Npc> Npcs = new();
    public static readonly List<Animal> Animals = new();

    public static void Register(Agent a)
    {
        Net.Net.I?.Register(a);
        if (!Agents.Contains(a)) Agents.Add(a);
        if (a is Npc n && !Npcs.Contains(n)) Npcs.Add(n);
        if (a is Animal an && !Animals.Contains(an)) Animals.Add(an);
    }

    public static void Unregister(Agent a)
    {
        Net.Net.I?.Unregister(a);
        Agents.Remove(a);
        if (a is Npc n) Npcs.Remove(n);
        if (a is Animal an) Animals.Remove(an);
    }

    public static void Reset()
    {
        Terrain = null; Structures = null; Spawner = null; Paths = null;
        Fort = null; Band = null; Ecology = null; Ship = null; Player = null;
        Agents.Clear(); Npcs.Clear(); Animals.Clear();
        // The old scene's HUD and painters hung handlers here; they're gone
        // with it, and the new scene subscribes afresh in its _Ready.
        LocalPlayerChanged = null;
        Nwc = null;
        IsometricFurTradeGame.Scripts.Agents.Grudges.All.Clear(); Mail.Abroad.Clear(); Economy.Council.Letters.Clear();
    }

    // Log line to the console and, if a HUD listener exists, the screen.
    public static event System.Action<string> OnNotice;
    public static System.Action<Player.PlayerController> LocalPlayerChanged;

    // World-wide notice: shown here and, from the server, on every client.
    public static void Notice(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        GD.Print($"[world] {text}");
        OnNotice?.Invoke(text);
        Net.Net.I?.BroadcastNotice(text);
    }

    // A notice that arrived from the server (don't re-broadcast).
    public static void NoticeLocal(string text) => OnNotice?.Invoke(text);

    public static IEnumerable<Player.PlayerController> Players()
    {
        if (Net.Net.I == null) { if (Player is Player.PlayerController p) yield return p; yield break; }
        foreach (var p in Net.Net.I.Players.Values) if (p != null && GodotObject.IsInstanceValid(p)) yield return p;
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
