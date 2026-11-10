using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Items;

namespace IsometricFurTradeGame.Scripts.World;

// What boats there are and how each is made (design doc 12, 12.2, 12.6).
//
// A birchbark canoe is built on the ground, not on a frame: the bark is laid
// on a bed of beaten earth, staked round to the canoe's shape, the gunwales
// lashed on with watap (split spruce root), the seams sewn, the ribs bent in
// hot and driven home under the sheathing, and every seam and stitch-hole
// gummed. A York boat is planked and nailed over a keel and frame at a post,
// its seams caulked with oakum and paid over with pitch. Every stage is a
// piece of hand-work (UI/BoatWork plays it; an NPC's skill plays it for him),
// and how well it was done is in the boat for the rest of her life: her
// shape in her speed, her stitching and gum in her leaks, her ribs and
// gunwales in how much she will carry and how long she lasts.
//
// Enums here are saved as ints: append new members at the end.
public enum BoatKind { Small, North, Master, York }

// A hull's four parts, each 0..1 (1 = as new): the skin (bark or planks),
// the seams (gum or caulking), the ribs (or frames), the gunwales.
public enum HullPart { Skin, Seams, Ribs, Gunwales }

public enum BoatStage { Bed, Gunwales, Sew, Ribs, Gum, Keel, Frame, Plank, Caulk, Pitch }

// The hand-work games (UI/BoatWork).
public enum BoatGame { Stakes, Lash, Sew, Bend, Gum, Nail, Caulk, Patch, Saw }

public sealed class BoatSpec
{
    public BoatKind Kind;
    public string Name, Short;
    public float CapacityLbs, Speed, SpriteScale;
    public int Seats, Carriers;          // men to paddle her well; men to carry her over a portage
    public bool Planked;
    public BoatStage[] Stages;
    public float[] Hours;               // work-hours for one pair of hands, per stage
    public (ItemId Item, int Count)[][] Needs;   // materials per stage
    public float MinSkill;              // the least crafting a man may lead the build with
    public float SkinPerPatch;          // how much one patch of bark or plank puts back
    public string Sprite;
}

public static class Boats
{
    private static readonly BoatStage[] BarkStages = { BoatStage.Bed, BoatStage.Gunwales, BoatStage.Sew, BoatStage.Ribs, BoatStage.Gum };
    private static readonly BoatStage[] PlankStages = { BoatStage.Keel, BoatStage.Frame, BoatStage.Plank, BoatStage.Caulk, BoatStage.Pitch };

    public static readonly Dictionary<BoatKind, BoatSpec> Specs = new()
    {
        // A hunting canoe of fourteen or sixteen feet: one family's, two to paddle.
        [BoatKind.Small] = new BoatSpec
        {
            Kind = BoatKind.Small, Name = "hunting canoe", Short = "canoe", CapacityLbs = 450f, Speed = 1f, SpriteScale = 1f, Seats = 2, Carriers = 1,
            Stages = BarkStages, Hours = new[] { 5f, 7f, 10f, 8f, 5f }, MinSkill = 0f, SkinPerPatch = 0.35f, Sprite = "res://assets/sprites/canoe.png",
            Needs = new[]
            {
                new[] { (ItemId.BirchBark, 4) },
                new[] { (ItemId.Cedar, 1), (ItemId.Watap, 2) },
                new[] { (ItemId.Watap, 3) },
                new[] { (ItemId.Cedar, 2) },
                new[] { (ItemId.SpruceGum, 3) },
            },
        },
        // The canot du nord: twenty-five feet, a ton and a half, four to six
        // men, and two of them to carry her.
        [BoatKind.North] = new BoatSpec
        {
            Kind = BoatKind.North, Name = "north canoe", Short = "north canoe", CapacityLbs = 3000f, Speed = 1.2f, SpriteScale = 1.45f, Seats = 5, Carriers = 2,
            Stages = BarkStages, Hours = new[] { 14f, 22f, 36f, 26f, 14f }, MinSkill = 0.3f, SkinPerPatch = 0.15f, Sprite = "res://assets/sprites/canoe.png",
            Needs = new[]
            {
                new[] { (ItemId.BirchBark, 12) },
                new[] { (ItemId.Cedar, 3), (ItemId.Watap, 5) },
                new[] { (ItemId.Watap, 8) },
                new[] { (ItemId.Cedar, 6) },
                new[] { (ItemId.SpruceGum, 8) },
            },
        },
        // The canot du maître of the Montreal brigades: thirty-six feet, four
        // tons, eight to twelve men. Only a master leads the building of one.
        [BoatKind.Master] = new BoatSpec
        {
            Kind = BoatKind.Master, Name = "Montreal canoe", Short = "Montreal canoe", CapacityLbs = 6000f, Speed = 1.3f, SpriteScale = 1.9f, Seats = 10, Carriers = 4,
            Stages = BarkStages, Hours = new[] { 24f, 36f, 60f, 44f, 24f }, MinSkill = 0.5f, SkinPerPatch = 0.08f, Sprite = "res://assets/sprites/canoe.png",
            Needs = new[]
            {
                new[] { (ItemId.BirchBark, 20) },
                new[] { (ItemId.Cedar, 5), (ItemId.Watap, 8) },
                new[] { (ItemId.Watap, 14) },
                new[] { (ItemId.Cedar, 10) },
                new[] { (ItemId.SpruceGum, 14) },
            },
        },
        // The York boat: the Bay's own, planked and nailed, oars and a sail,
        // three tons, eight men - and too heavy to carry: she is hauled over
        // a portage on rollers by her whole crew.
        [BoatKind.York] = new BoatSpec
        {
            Kind = BoatKind.York, Name = "York boat", Short = "York boat", CapacityLbs = 6000f, Speed = 1.05f, SpriteScale = 0.42f, Seats = 8, Carriers = 6, Planked = true,
            Stages = PlankStages, Hours = new[] { 24f, 30f, 48f, 24f, 12f }, MinSkill = 0.35f, SkinPerPatch = 0.12f, Sprite = "res://assets/sprites/vehicles/york_boat.png",
            Needs = new[]
            {
                new[] { (ItemId.Plank, 4) },
                new[] { (ItemId.Cedar, 6), (ItemId.Nails, 2) },
                new[] { (ItemId.Plank, 20), (ItemId.Nails, 6) },
                new[] { (ItemId.Oakum, 6) },
                new[] { (ItemId.Pitch, 3) },
            },
        },
    };

    public static BoatSpec Of(BoatKind k) => Specs[k];

    // The brigades' own words for their craft (Data/World/brigade_runs.json).
    public static BoatKind ForCraft(string craft) => craft switch
    {
        "york boat" => BoatKind.York, "north canoe" => BoatKind.North, "canoe" => BoatKind.Master, _ => BoatKind.North,
    };

    public static string StageName(BoatStage s) => s switch
    {
        BoatStage.Bed => "Stake out the bark on the bed",
        BoatStage.Gunwales => "Lash on the gunwales",
        BoatStage.Sew => "Sew the seams with watap",
        BoatStage.Ribs => "Bend in the ribs",
        BoatStage.Gum => "Gum the seams",
        BoatStage.Keel => "Saw out and lay the keel",
        BoatStage.Frame => "Set up the frames",
        BoatStage.Plank => "Plank and nail her",
        BoatStage.Caulk => "Caulk the seams with oakum",
        BoatStage.Pitch => "Pay the seams with pitch",
        _ => s.ToString(),
    };

    // What the people at it are called doing (the posture and the trace read these).
    public static string StageLabel(BoatStage s) => s switch
    {
        BoatStage.Bed => "stake the bark on the canoe bed",
        BoatStage.Gunwales => "lash the gunwales",
        BoatStage.Sew => "sew the bark with watap",
        BoatStage.Ribs => "set the ribs in the canoe",
        BoatStage.Gum => "gum the seams",
        BoatStage.Keel => "saw out the keel at the pit",
        BoatStage.Frame => "set up the boat's frames",
        BoatStage.Plank => "plank the boat",
        BoatStage.Caulk => "caulk the boat",
        BoatStage.Pitch => "pitch the boat's seams",
        _ => "work on the boat",
    };

    public static BoatGame GameFor(BoatStage s) => s switch
    {
        BoatStage.Bed => BoatGame.Stakes, BoatStage.Gunwales => BoatGame.Lash, BoatStage.Sew => BoatGame.Sew,
        BoatStage.Ribs => BoatGame.Bend, BoatStage.Gum => BoatGame.Gum, BoatStage.Keel => BoatGame.Saw,
        BoatStage.Frame => BoatGame.Stakes, BoatStage.Plank => BoatGame.Nail, BoatStage.Caulk => BoatGame.Caulk,
        BoatStage.Pitch => BoatGame.Gum, _ => BoatGame.Sew,
    };

    // The tool each stage can't be done without, and why.
    public static (ItemId Tool, string Why)? ToolFor(BoatStage s) => s switch
    {
        BoatStage.Bed or BoatStage.Keel or BoatStage.Frame => (ItemId.Axe, "an axe to cut and drive the stakes"),
        BoatStage.Gunwales or BoatStage.Ribs => (ItemId.CrookedKnife, "a crooked knife to shave the cedar"),
        BoatStage.Sew => (ItemId.Awl, "an awl to open the stitch-holes"),
        BoatStage.Gum or BoatStage.Pitch => (ItemId.Kettle, "a kettle to melt it in"),
        _ => null,
    };

    // Gum and pitch are worked hot: a fire near the bed.
    public static bool NeedsFire(BoatStage s) => s is BoatStage.Gum or BoatStage.Pitch or BoatStage.Ribs or BoatStage.Caulk;

    // Bark and gum work wants the thaw: sewn watap must be soaked, gum cracks
    // in the cold. Planked work goes on all winter (York's boats were built
    // in the carpenter's shed between ship-times).
    public static bool NeedsThaw(BoatStage s) => s is BoatStage.Sew or BoatStage.Gum or BoatStage.Bed;

    // Which part of the hull each stage makes, and how.
    public static HullPart PartOf(BoatStage s) => s switch
    {
        BoatStage.Bed or BoatStage.Keel or BoatStage.Plank => HullPart.Skin,
        BoatStage.Gunwales => HullPart.Gunwales,
        BoatStage.Ribs or BoatStage.Frame => HullPart.Ribs,
        _ => HullPart.Seams,
    };

    public static string PartName(HullPart p, bool planked) => p switch
    {
        HullPart.Skin => planked ? "Planking" : "Bark",
        HullPart.Seams => planked ? "Caulking" : "Seams and gum",
        HullPart.Ribs => planked ? "Frames" : "Ribs",
        _ => "Gunwales",
    };

    // How the hands' score and their craft come together (the player's own
    // hands count most; his practised craft steadies a bad moment).
    public static float Blend(float score, float skill) => Mathf.Clamp(0.75f * score + 0.25f * skill, 0f, 1f);

    // An NPC's hands at a game, from his crafting: a master is reliably good,
    // a green hand all over the place.
    public static float AiScore(float skill, RandomNumberGenerator rng = null)
    {
        float noise = rng != null ? rng.Randfn(0f, 1f) : (float)GD.Randfn(0.0, 1.0);
        return Mathf.Clamp(0.25f + 0.7f * skill + noise * (0.18f - 0.12f * skill), 0.05f, 1f);
    }

    public static string Grade(float q) => q >= 0.9f ? "fine" : q >= 0.75f ? "good" : q >= 0.55f ? "fair" : q >= 0.35f ? "rough" : "poor";
    public static string Condition(float c) => c >= 0.85f ? "sound" : c >= 0.65f ? "worn" : c >= 0.4f ? "tired" : c >= 0.2f ? "bad" : "failing";

    // What a canoe is worth in made beaver at a camp, by kind and how she was built.
    public static float ValueMb(BoatKind k, float quality, float condition) =>
        (k switch { BoatKind.Small => 6f, BoatKind.North => 30f, BoatKind.Master => 55f, _ => 45f }) * (0.5f + 0.5f * quality) * (0.3f + 0.7f * condition);

    // ---- where things grow -----------------------------------------------

    // Birch stands among the aspen and spruce of the forest country; out on
    // the parkland the white trees are aspen, whose bark is no use.
    public static bool IsBirch(Vector2I cell)
    {
        if (Core.Sim.Structures == null) return false;
        int s = WorldLayout.Flora(cell);
        if (s != WorldLayout.Aspen) return false;
        var g = WorldMap.At(cell);
        return g is Ground.Boreal or Ground.LakesForest or Ground.Shield or Ground.Lowland;
    }

    public static bool IsConifer(Vector2I cell)
    {
        int s = WorldLayout.Flora(cell);
        return s is >= WorldLayout.Spruce0 and <= WorldLayout.Spruce0 + 2 || s == WorldLayout.Tamarack || s == WorldLayout.JackPine;
    }

    // Cedar grows in the lakes country and the mountains; elsewhere the ribs
    // were split from spruce or tamarack (heavier, and they crack sooner).
    public static bool CedarCountry(Vector2I cell) => WorldMap.At(cell) is Ground.LakesForest or Ground.Cordillera;

    // The bark peels clean from late May through July; outside it the sheet
    // tears and checks, and in deep winter it must be thawed off over a fire.
    public static float BarkSeason(int month) => month switch { 6 or 7 => 1f, 5 or 8 => 0.6f, 4 or 9 => 0.3f, _ => 0.12f };

    // The nearest tree of a sort within reach.
    public static Vector2I? NearestTreeWhere(Vector2 at, float reach, System.Func<Vector2I, bool> which)
    {
        var st = Core.Sim.Structures;
        if (st == null) return null;
        var c = IsoGrid.FromWorld(at);
        int rings = Mathf.CeilToInt(reach / IsoGrid.TileW) + 1;
        Vector2I? best = null; float bestD = reach;
        for (int dx = -rings; dx <= rings; dx++)
            for (int dy = -2 * rings; dy <= 2 * rings; dy++)
            {
                var n = new Vector2I(c.X + dx, c.Y + dy);
                if (!st.IsTree(n) || !which(n)) continue;
                float d = IsoGrid.ToWorld(n).DistanceTo(at);
                if (d < bestD) { bestD = d; best = n; }
            }
        return best;
    }
}
