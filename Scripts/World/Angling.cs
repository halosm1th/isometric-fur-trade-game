using Godot;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Ecology;
using IsometricFurTradeGame.Scripts.Items;
using IsometricFurTradeGame.Scripts.Player;

namespace IsometricFurTradeGame.Scripts.World;

// The host's half of fishing with a line (UI/Fishing plays the cast, the
// wait and the fight on the player's machine): a fish landed is taken from
// the nearest fishery. Where no fishery has been counted (open country) the
// water gives it anyway - one line barely dents a lake; where the fishery
// has been netted out, what bites is not worth keeping.
public static class Angling
{
    public static string Land(PlayerController p, int size, Vector2 at)
    {
        if (p == null || p.IsDead || !p.Inventory.Has(ItemId.FishingLine)) return "";
        if (GameCalendar.Instance != null && GameCalendar.Instance.IsFrozen) return "";
        if (at.DistanceTo(p.GlobalPosition) > 520f || Sim.Terrain == null || !Sim.Terrain.IsWaterNear(at, 80f)) return "";
        size = Mathf.Clamp(size, 1, 3);
        int want = size == 3 ? 2 : 1;
        int got = Sim.Ecology?.Catch(at, want) ?? 0;
        if (got == 0 && FisheryNear(at)) return "A sucker no longer than your hand. You throw it back - this water has been fished hard.";
        if (got == 0) got = want;
        p.Inventory.Add(ItemId.Fish, got);
        p.Skills.Learn(ref p.Skills.Woodcraft, 0.004f * size, "fishing with a line");
        return size switch
        {
            3 => "A pike as long as your arm - two meals in it. (+2 fish)",
            2 => "A good whitefish, fat from the summer. (+1 fish)",
            _ => "A little pickerel, but it will eat. (+1 fish)",
        };
    }

    // Is there a counted fishery within reach, even an emptied one?
    private static bool FisheryNear(Vector2 at)
    {
        if (Sim.Ecology == null) return false;
        foreach (var s in Sim.Ecology.Sites)
            if (s.Species == Species.Fish && s.WorldPos.DistanceTo(at) < WorldLayout.HomeCountryPx) return true;
        return false;
    }
}
