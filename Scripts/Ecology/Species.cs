namespace IsometricFurTradeGame.Scripts.Ecology;

public enum Species
{
    Caribou,
    Goose,
    Beaver,
    Wolf,
}

public enum Diet { Prey, Predator }

public readonly record struct SpeciesDef(
    string Name, Diet Diet, float Speed, float Health, float BiteDamage,
    float MeatLbs, float PeltValueMb, float RegrowthPerYear, float SustainFloor, string Frames);

public static class SpeciesInfo
{
    public static readonly System.Collections.Generic.Dictionary<Species, SpeciesDef> Defs = new()
    {
        // MB = Made Beaver, the HBC unit of account: one prime beaver pelt.
        { Species.Caribou, new("Caribou", Diet.Prey, 170f, 70f, 12f, 120f, 0.5f, 0.35f, 0.2f, "res://Scenes/Animals/CaribouFrames.tres") },
        { Species.Goose,   new("Goose",   Diet.Prey, 140f, 8f,  0f,  6f,   0f,   1.2f,  0.15f, "res://Scenes/Animals/GooseFrames.tres") },
        { Species.Beaver,  new("Beaver",  Diet.Prey, 90f,  20f, 4f,  25f,  1.0f, 0.5f,  0.25f, "res://Scenes/Animals/BeaverFrames.tres") },
        { Species.Wolf,    new("Wolf",    Diet.Predator, 230f, 50f, 16f, 40f, 0.6f, 0.4f, 0.2f, "res://Scenes/Animals/WolfFrames.tres") },
    };
    public static SpeciesDef Of(Species s) => Defs[s];

    // How many animals one go at them accounts for. A volley into a flock
    // settled on the marsh is a bag of birds, not a bird; everything else is
    // one beast at a time.
    public static int PerKill(Species s, bool shot) => s == Species.Goose ? (shot ? 8 : 4) : 1;

    // What a kill puts in the hunter's pack. `shot` = killed with a musket:
    // a ball tears the skin and the pelt grades as damaged; traps, arrows
    // and the knife leave it whole (design doc 13.6).
    public static void Yield(Species s, IsometricFurTradeGame.Scripts.Items.Inventory into, bool shot = false)
    {
        switch (s)
        {
            case Species.Caribou:
                into.Add(IsometricFurTradeGame.Scripts.Items.ItemId.Venison, 18);   // ~100 lb of meat: a man eats three to five pounds a day
                into.Add(shot ? IsometricFurTradeGame.Scripts.Items.ItemId.ShotHide : IsometricFurTradeGame.Scripts.Items.ItemId.CaribouHide, 1);
                break;
            case Species.Goose: into.Add(IsometricFurTradeGame.Scripts.Items.ItemId.Goose, PerKill(s, shot)); break;
            case Species.Beaver:
            {
                // Prime in winter, thin and worth half from June to September (design doc 14.4).
                int m = IsometricFurTradeGame.Scripts.Core.GameCalendar.Instance?.Month ?? 1;
                var pelt = shot ? IsometricFurTradeGame.Scripts.Items.ItemId.ShotPelt : m >= 6 && m <= 9 ? IsometricFurTradeGame.Scripts.Items.ItemId.ThinPelt : IsometricFurTradeGame.Scripts.Items.ItemId.BeaverPelt;
                into.Add(pelt, 1);
                into.Add(IsometricFurTradeGame.Scripts.Items.ItemId.Venison, 3);   // 25 lb of beaver, and the tail is the best of it
                break;
            }
            case Species.Wolf: into.Add(shot ? IsometricFurTradeGame.Scripts.Items.ItemId.ShotPelt : IsometricFurTradeGame.Scripts.Items.ItemId.WolfPelt, 1); break;
        }
    }
}
