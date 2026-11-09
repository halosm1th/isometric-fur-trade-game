using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Ecology;
using IsometricFurTradeGame.Scripts.Items;
using IsometricFurTradeGame.Scripts.Player;

namespace IsometricFurTradeGame.Scripts.World;

// Fishing with a line: one set of rules for everyone.
//
// A fish that takes the hook is small, good or big (RollSize). Landing it is
// the game on the bar: a needle swings, the green is where the fish gives
// you line, and you strike on it - PullsNeeded good strikes land it,
// MaxMisses bad ones lose it. The green is wider for a better hand
// (ZoneWidth, from Woodcraft) and narrower for a bigger fish.
//
// The player plays that game with their own hands (UI/Fishing). A man of
// the world plays the same game in the dice (NpcLands): each strike lands on
// the green as often as his hand and the green's width allow. Both learn
// Woodcraft by it, and both take what they land out of the same fishery
// (Take), so a lake netted out by the post is poor for your line too.
public static class Angling
{
    public const int MaxMisses = 3;
    public const float GiveUpSecs = 6f;   // no good strike for this long and the fish shakes free

    public static int RollSize()
    {
        float r = GD.Randf();
        return r < 0.55f ? 1 : r < 0.88f ? 2 : 3;
    }
    public static int PullsNeeded(int size) => 2 + size;
    public static float NeedleSpeed(int size) => 0.85f + 0.3f * size;          // bar lengths a second
    public static float ZoneDrift(int size) => 0.08f + 0.07f * size;
    public static float ZoneWidth(int size, float skill) => Mathf.Clamp(0.24f - 0.035f * size + 0.12f * skill, 0.1f, 0.34f);
    public static int FishFor(int size) => size == 3 ? 2 : 1;
    public static string Name(int size) => size switch { 3 => "a pike", 2 => "a whitefish", _ => "a pickerel" };

    // One strike by a man of the world: on the green as often as a steady
    // hand finds a green that wide.
    public static float StrikeOdds(int size, float skill) =>
        Mathf.Clamp(0.45f + 0.5f * skill + 1.2f * (ZoneWidth(size, skill) - 0.17f) - 0.04f * (size - 1), 0.15f, 0.96f);

    // The bar game, played in the dice.
    public static bool NpcLands(int size, float skill)
    {
        float p = StrikeOdds(size, skill);
        int good = 0, bad = 0, need = PullsNeeded(size);
        while (good < need && bad < MaxMisses) { if (GD.Randf() < p) good++; else bad++; }
        return good >= need;
    }

    // A landed fish out of the nearest fishery. Where no fishery has been
    // counted (open country) the water gives it anyway - one line barely
    // dents a lake; where the fishery has been netted out, what bites is not
    // worth keeping (0).
    public static int Take(Vector2 at, int size)
    {
        int want = FishFor(size);
        int got = Sim.Ecology?.Catch(at, want) ?? want;
        if (got == 0 && !FisheryNear(at)) got = want;
        return got;
    }

    public static void Learned(Skills skills, int size, bool player) =>
        skills.Learn(ref skills.Woodcraft, 0.004f * size, player ? "fishing with a line" : null);

    // The host's half of the player's game: the fish they landed.
    public static string Land(PlayerController p, int size, Vector2 at)
    {
        if (p == null || p.IsDead || !p.Inventory.Has(ItemId.FishingLine)) return "";
        if (GameCalendar.Instance != null && GameCalendar.Instance.IsFrozen) return "";
        if (at.DistanceTo(p.GlobalPosition) > 520f || Sim.Terrain == null || !Sim.Terrain.IsWaterNear(at, 80f)) return "";
        size = Mathf.Clamp(size, 1, 3);
        int got = Take(at, size);
        if (got == 0) return "A sucker no longer than your hand. You throw it back - this water has been fished hard.";
        p.Inventory.Add(ItemId.Fish, got);
        Learned(p.Skills, size, true);
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
