using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Economy;

namespace IsometricFurTradeGame.Scripts.World;

// A camp of the wider country: a band of one of the atlas's peoples where
// it was in 1811, or a family of the nearest nation living by a post (the
// homeguard). Its people are markers in the scene under Cast/Camps.
public class CountryCamp
{
    public string Name, Kind, Post;
    public Faction Nation;
    public Vector2I Centre;
    public int People;
    public CountryBand Ledger;
    public bool IsFamily => Kind == "family";
    public IEnumerable<Vector2I> Tipis()
    {
        if (IsFamily) { yield return IsoGrid.Cell(Centre, 2, 0); yield break; }
        foreach (var c in LodgeCells(Centre, Lodges)) yield return c;
    }
    // About eight to a lodge: a band of seventy pitches nine lodges.
    public int Lodges => IsFamily ? 1 : Mathf.Max(5, (People + 7) / 8);
    // How far out its lodges stand, for the clearing round a camp.
    public float RadiusPx => IsFamily ? 300f : 260f + 3f * 70f * Mathf.Ceil((Lodges - 5) / 8f + 1f);

    // The lodges in rings round the fire: the first five where a camp has
    // always pitched them (WorldLayout.TipiCellsAt), then eight to a ring,
    // three cells further out each time.
    public static IEnumerable<Vector2I> LodgeCells(Vector2I centre, int n)
    {
        int k = 0;
        foreach (var c in WorldLayout.TipiCellsAt(centre)) { if (k++ >= n) yield break; yield return c; }
        var first = new[] { (-3, -3), (3, -3), (-3, 3) };
        foreach (var (i, j) in first) { if (k++ >= n) yield break; yield return IsoGrid.Cell(centre, i, j); }
        for (int r = 6; k < n; r += 3)
            foreach (var (i, j) in new[] { (r, 0), (0, r), (-r, 0), (0, -r), (r, r), (-r, -r), (r, -r), (-r, r), (r, r / 2), (-r, -r / 2), (r / 2, -r), (-r / 2, r) })
            {
                if (k++ >= n) yield break;
                yield return IsoGrid.Cell(centre, i, j);
            }
    }
}

public static class Camps
{
    private static readonly Dictionary<string, CountryCamp> _byName = new();
    public static readonly List<CountryCamp> All = Load();

    private static List<CountryCamp> Load()
    {
        var list = new List<CountryCamp>();
        foreach (var v in WorldMap.LayoutArray("camps"))
        {
            var d = v.AsGodotDictionary();
            var c = d["centre"].AsGodotArray();
            var camp = new CountryCamp
            {
                Name = d["name"].AsString(), Kind = d["kind"].AsString(), Post = d.TryGetValue("post", out var p) ? p.AsString() : "",
                Nation = System.Enum.TryParse(d["nation"].AsString(), out Faction f) ? f : Faction.Cree,
                Centre = new Vector2I(c[0].AsInt32(), c[1].AsInt32()), People = d["people"].AsInt32(),
            };
            list.Add(camp);
            _byName[camp.Name] = camp;
        }
        return list;
    }

    public static CountryCamp Named(string name) => name != null && _byName.TryGetValue(name, out var c) ? c : null;

    // Ground kept clear round a camp's lodges (WorldLayout.Cleared). By area.
    private const float GridPx = 4096f;
    private static Dictionary<Vector2I, List<CountryCamp>> _grid;
    public static bool NearAny(Vector2 at)
    {
        if (_grid == null)
        {
            _grid = new();
            foreach (var c in All)
            {
                var w = IsoGrid.ToWorld(c.Centre);
                var k = new Vector2I(Mathf.FloorToInt(w.X / GridPx), Mathf.FloorToInt(w.Y / GridPx));
                if (!_grid.TryGetValue(k, out var l)) _grid[k] = l = new();
                l.Add(c);
            }
        }
        int kx = Mathf.FloorToInt(at.X / GridPx), ky = Mathf.FloorToInt(at.Y / GridPx);
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
                if (_grid.TryGetValue(new Vector2I(kx + dx, ky + dy), out var l))
                    foreach (var c in l) if (IsoGrid.ToWorld(c.Centre).DistanceTo(at) < c.RadiusPx) return true;
        return false;
    }

    public static void Reset() { foreach (var c in All) c.Ledger = null; }

    // Every peer, at world start: the camps are there from the beginning.
    public static void Begin(Node host)
    {
        // Dev aid: `-- --nocamps` leaves the wider country empty of its peoples.
        if (System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--nocamps") >= 0) return;
        foreach (var c in All)
        {
            c.Ledger = new CountryBand { Name = c.Name, Nation = c.Nation, Camp = c.Centre, WinterGround = c.Centre, SummerGround = c.Centre, Country = c };
            c.Ledger.Name = c.Name;
            host.AddChild(c.Ledger);
        }
    }

    // What a nation is called in the world's words.
    public static string NationName(Faction f) => f switch
    {
        Faction.Cree => "Cree", Faction.Assiniboine => "Assiniboine", Faction.Ojibwe => "Ojibwe", Faction.Dene => "Dene",
        Faction.Blackfoot => "Blackfoot", Faction.Sioux => "Sioux", Faction.Plateau => "Plateau people", Faction.Coast => "Coast people",
        Faction.Missouri => "Missouri people", Faction.Metis => "Métis", Faction.Haudenosaunee => "Haudenosaunee", Faction.SaukFox => "Sauk and Meskwaki", Faction.Crown => "the Crown", _ => f.ToString(),
    };
}
