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
        foreach (var c in WorldLayout.TipiCellsAt(Centre)) yield return c;
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

    public static void Reset() { foreach (var c in All) c.Ledger = null; }

    // Every peer, at world start: the camps are there from the beginning.
    public static void Begin(Node host)
    {
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
        Faction.Missouri => "Missouri people", _ => f.ToString(),
    };
}
