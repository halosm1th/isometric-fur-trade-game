using Godot;

namespace IsometricFurTradeGame.Scripts.Agents;

// Which sprite sheet a role wears. The cast in the scene and older saves
// still name the stand-ins (a factor tinted pink for a voyageur, the player
// shrunk for a wife); the server swaps in the real sheet when an NPC's look
// is set, and clients get the swapped path in the snapshot.
public static class Appearance
{
    private const string Dir = "res://Scenes/Npc/";

    // The sheet for this NPC, or null to keep what it has.
    public static string For(Npc n)
    {
        bool child = n.FatherName != "" && n.Age < 14;
        if (child && n.Faction == Faction.HBC) return Dir + (n.IsFemale ? "ChildGirlFrames.tres" : "ChildBoyFrames.tres");
        return n.Role switch
        {
            Role.Voyageur => Dir + "VoyageurFrames.tres",
            Role.NwcPartner => Dir + "NwcPartnerFrames.tres",
            Role.Woman or Role.Wife when n.Faction == Faction.HBC => Dir + "CountryWifeFrames.tres",
            Role.Sailor => Dir + "SailorFrames.tres",
            Role.Clerk or Role.Postmaster => Dir + "ClerkFrames.tres",
            Role.CreeChief => Dir + "CreeCaptainFrames.tres",
            _ => null,
        };
    }

    // A grown child goes back to the sheet for the work they now do.
    public static bool IsChildSheet(string path) => path != null && path.Contains("/Child");
}
