using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Core;

namespace IsometricFurTradeGame.Scripts.Ecology;

// Steel traps set at a beaver lodge (design doc 16.1's trapping verb, 16.3's
// poaching). You set the line, you come back tomorrow. Traps catch overnight
// against how thick the beaver are; the catch waits in the traps until the
// owner checks - or someone else does first.
public class Trapline
{
    public string Owner;          // "player" or a household title
    public int Traps;
    public int Caught;
    public float HoursSinceCheck;
    public float HoursSinceSet;
    public int Robbed;            // catch taken by others since last check

    public bool ReadyToCheck => HoursSinceCheck >= 18f;
}

public static class TraplineRules
{
    // Chance per trap per night, at a full lodge.
    public const float CatchChanceAtCapacity = 0.22f;

    public static void NightlyCatch(SpawnSite site)
    {
        if (site.Species != Species.Beaver) return;
        float density = Mathf.Clamp(site.Count / (float)site.Capacity, 0f, 1.2f);
        foreach (var line in site.Traplines)
        {
            for (int i = 0; i < line.Traps && site.Count > 0.5f; i++)
            {
                if (GD.Randf() < CatchChanceAtCapacity * density)
                {
                    line.Caught++;
                    site.Take(1);
                }
            }
        }
    }

    // Somebody at the lodge who doesn't own a line may help themselves.
    public static int Poach(SpawnSite site, string byOwner, float boldness)
    {
        int taken = 0;
        foreach (var line in site.Traplines)
        {
            if (line.Owner == byOwner || line.Caught == 0) continue;
            if (GD.Randf() < boldness * 0.6f)
            {
                taken += line.Caught;
                line.Robbed += line.Caught;
                line.Caught = 0;
            }
        }
        return taken;
    }
}
