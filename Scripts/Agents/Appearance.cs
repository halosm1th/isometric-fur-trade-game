using Godot;

namespace IsometricFurTradeGame.Scripts.Agents;

// Which sprite sheet a role wears. The cast in the scene and older saves
// still name the stand-ins (a factor tinted pink for a voyageur, the player
// shrunk for a wife); the server swaps in the real sheet when an NPC's look
// is set, and clients get the swapped path in the snapshot.
public static class Appearance
{
    private const string Dir = "res://Scenes/Npc/";

    public const string VoyageurFile = "VoyageurFrames.tres";
    public const string VoyageurSheet = Dir + VoyageurFile;

    // A sheet PixelLab has redrawn from the painted one, posed by the 3D doll
    // (tools/sprites/pixellab), sits beside it as <Name>PLFrames.tres and is
    // worn in its place. Dev: `--painted` keeps the painted sheets, to compare.
    // Paths are kept, saved and sent by the painted name; only loading
    // turns them into the redrawn one.
    public static readonly bool Painted = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--painted") >= 0;
    private static readonly System.Collections.Generic.Dictionary<string, string> _resolved = new();

    public static string Resolve(string path)
    {
        if (Painted || string.IsNullOrEmpty(path) || !path.EndsWith("Frames.tres") || path.EndsWith("PLFrames.tres")) return path;
        if (_resolved.TryGetValue(path, out var r)) return r;
        string pl = path[..^"Frames.tres".Length] + "PLFrames.tres";
        return _resolved[path] = ResourceLoader.Exists(pl) ? pl : path;
    }

    // The painted name of a sheet, whichever of the two was loaded.
    public static string Canonical(string path) =>
        path != null && path.EndsWith("PLFrames.tres") ? path[..^"PLFrames.tres".Length] + "Frames.tres" : path;

    // The sheet for this NPC, or null to keep what it has.
    public static string For(Npc n)
    {
        bool child = (n.FatherName != "" || n.Role == Role.Child) && n.Age < 14;
        if (child && n.Faction is Faction.HBC or Faction.Crown or Faction.American or Faction.Metis) return Dir + (n.IsFemale ? "ChildGirlFrames.tres" : "ChildBoyFrames.tres");
        // A camp's children wear their own nation's dress, small (the scene
        // gave the Blackfoot's and the Dene's boys the Assiniboine's).
        if (child && n.Role == Role.Child && n.Faction == Faction.Cree && Drawn("CreeBoyFrames.tres"))
            return Dir + (n.IsFemale ? "CreeGirlFrames.tres" : "CreeBoyFrames.tres");
        if (child && n.Role == Role.Child) return ForNation(n.Faction, n.IsFemale);
        return n.Role switch
        {
            Role.Voyageur => VoyageurSheet,
            // The North West Company's men: its engages were Canadiens, in
            // the voyageur's dress; its clerks wore the Nor'Westers' coat; a
            // post of theirs was kept by a wintering partner.
            Role.Labourer when n.Faction == Faction.NWC => VoyageurSheet,
            Role.Clerk or Role.Postmaster when n.Faction == Faction.NWC => Dir + "RivalFrames.tres",
            Role.Factor when n.Faction == Faction.NWC => Dir + "NwcPartnerFrames.tres",
            Role.NwcPartner => Dir + "NwcPartnerFrames.tres",
            Role.Woman or Role.Wife when n.Faction is Faction.HBC or Faction.NWC or Faction.American or Faction.Crown => Dir + "CountryWifeFrames.tres",
            // The towns' people keep the look they were placed with: the
            // soldier's coat, the merchant's broadcloth, the habitant's capote.
            Role.Farmer or Role.Merchant or Role.Artisan or Role.Soldier or Role.Officer or Role.Priest => null,
            // The Missouri's engages were St. Louis Creoles: a voyageur's dress.
            Role.Labourer when n.Faction == Faction.American => VoyageurSheet,
            // The Company's servants were placed in the player's coat (the
            // scene's default sheet), so every man at York was his double.
            Role.Labourer => Either(n, "LabourerFrames.tres", "OrkneymanFrames.tres"),
            Role.Sailor => Dir + "SailorFrames.tres",
            Role.Clerk or Role.Postmaster => Dir + "ClerkFrames.tres",
            Role.Factor => Dir + "FactorFrames.tres",
            // The trading captain's sheet is a Cree one: a Blackfoot or a
            // Mandan chief wears his own nation's instead.
            Role.Chief when n.Faction is Faction.Cree or Faction.HBC => Dir + "CreeCaptainFrames.tres",
            Role.Woman or Role.Wife or Role.BandWoman or Role.BandDaughter when n.Faction == Faction.Cree => Either(n, "CreeWomanFrames.tres", "CreeWomanClothFrames.tres"),
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
            Faction.Metis => Drawn("MetisFrames.tres") ? "MetisFrames.tres" : VoyageurFile,
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

    // Sheets for roles not drawn yet (tools/sprites/pixellab/roles.py
    // NEW_ROLES): worn as soon as they are there, until then the old one.
    private static readonly System.Collections.Generic.Dictionary<string, bool> _drawn = new();
    // With --painted they are left out: their painted sheet is only the
    // three idles PixelLab started from.
    private static bool Drawn(string file) =>
        !Painted && (_drawn.TryGetValue(file, out var d) ? d : _drawn[file] = ResourceLoader.Exists(Dir + file));

    // Half of a people in one sheet, half in the other once it is drawn,
    // by name, so a person keeps theirs.
    private static string Either(Npc n, string a, string b)
    {
        if (!Drawn(b)) return Dir + a;
        uint h = 2166136261;
        foreach (char c in n.DisplayName ?? "") h = (h ^ c) * 16777619;
        return Dir + ((h >> 3) % 2 == 0 ? a : b);
    }

    // A grown child goes back to the sheet for the work they now do.
    public static bool IsChildSheet(string path) => path != null && path.Contains("/Child");
}
