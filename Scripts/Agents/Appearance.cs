using Godot;

namespace IsometricFurTradeGame.Scripts.Agents;

// Which sprite sheet a role wears. The cast in the scene and older saves
// still name the stand-ins (a factor tinted pink for a voyageur, the player
// shrunk for a wife); the server swaps in the real sheet when an NPC's look
// is set, and clients get the swapped path in the snapshot.
public static class Appearance
{
    private const string Dir = "res://Scenes/Npc/";

    // Dev: `--pixellab` dresses the voyageurs in the frames PixelLab drew
    // from the painted ones, posed by the 3D doll (tools/sprites/pixellab),
    // to compare the two in the game.
    // The player wears it too, so the new frames can be tried by hand.
    public static readonly bool PixelLab = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--pixellab") >= 0;
    public static readonly string VoyageurFile = PixelLab ? "VoyageurPLFrames.tres" : "VoyageurFrames.tres";
    public static readonly string VoyageurSheet = Dir + VoyageurFile;

    // The sheet for this NPC, or null to keep what it has.
    public static string For(Npc n)
    {
        bool child = (n.FatherName != "" || n.Role == Role.Child) && n.Age < 14;
        if (child && n.Faction is Faction.HBC or Faction.Crown or Faction.American) return Dir + (n.IsFemale ? "ChildGirlFrames.tres" : "ChildBoyFrames.tres");
        return n.Role switch
        {
            Role.Voyageur => VoyageurSheet,
            Role.NwcPartner => Dir + "NwcPartnerFrames.tres",
            Role.Woman or Role.Wife when n.Faction is Faction.HBC or Faction.American or Faction.Crown => Dir + "CountryWifeFrames.tres",
            // The towns' people keep the look they were placed with: the
            // soldier's coat, the merchant's broadcloth, the habitant's capote.
            Role.Farmer or Role.Merchant or Role.Artisan or Role.Soldier or Role.Officer or Role.Priest => null,
            // The Missouri's engages were St. Louis Creoles: a voyageur's dress.
            Role.Labourer when n.Faction == Faction.American => VoyageurSheet,
            Role.Sailor => Dir + "SailorFrames.tres",
            Role.Clerk or Role.Postmaster => Dir + "ClerkFrames.tres",
            Role.Factor => Dir + "FactorFrames.tres",
            // The trading captain's sheet is a Cree one: a Blackfoot or a
            // Mandan chief wears his own nation's instead.
            Role.Chief when n.Faction is Faction.Cree or Faction.HBC => Dir + "CreeCaptainFrames.tres",
            Role.Woman or Role.Wife or Role.BandWoman or Role.BandDaughter => ForNation(n.Faction, true),
            Role.Hunter or Role.Chief => ForNation(n.Faction),
            _ => null,
        };
    }

    // The sheet a man or woman of a nation wears. Every faction in the
    // country has people in it; until now they all borrowed the Cree's or
    // the Assiniboine's face. The women still do - nobody has drawn them.
    public static string ForNation(Faction f, bool female = false)
    {
        if (female) return Dir + (f == Faction.Metis ? "CountryWifeFrames.tres" : "CreeWomanFrames.tres");
        return Dir + f switch
        {
            Faction.Metis => VoyageurFile,
            Faction.Blackfoot => "BlackfootFrames.tres",
            Faction.Dene => "DeneFrames.tres",
            Faction.Coast => "CoastFrames.tres",
            Faction.Sioux => "DakotaFrames.tres",
            Faction.Missouri => "MandanFrames.tres",
            Faction.Plateau => "PlateauFrames.tres",
            Faction.Ojibwe => "OjibweFrames.tres",
            Faction.Assiniboine => "AssiniboineFrames.tres",
            _ => "CreeFrames.tres",
        };
    }

    // A grown child goes back to the sheet for the work they now do.
    public static bool IsChildSheet(string path) => path != null && path.Contains("/Child");
}
