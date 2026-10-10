using System;
using System.Collections.Generic;
using Godot;

namespace IsometricFurTradeGame.Scripts.World;

// What the ground is, by ecozone (design doc 6) or water.
public enum Ground : byte { Ocean, Lake, River, Ford, Boreal, Tundra, Shield, Parkland, Grassland, TallGrass, LakesForest, Cordillera }

public readonly record struct WorldPlace(string Name, string Kind, string Company, int Year, Vector2I Cell, double Lon, double Lat, string Note);

// The whole play area (design doc 6.1) at half scale: 20 world px a km,
// one pixel of Data/World/ground.png per cell. Built from the Pemmican
// Empire atlas by tools/world/build_world.py, which also fits the hand-built
// posts onto dry ground and lays the York Factory roads (world.json).
//
// Everything that asks "what is this cell" asks here, not the painted
// tiles: only the country round the players is painted (TerrainPainter
// streams it in chunks), but a man walking a hundred miles off still needs
// to know a lake is a lake.
public static class WorldMap
{
    public const string GroundPath = "res://Data/World/ground.png";
    public const string MetaPath = "res://Data/World/world.json";

    public static int Columns { get; private set; } = 1;
    public static int Rows { get; private set; } = 1;
    public static float PxPerKm { get; private set; } = 20f;
    public static Image GroundImage { get; private set; }
    public static readonly List<WorldPlace> Places = new();

    private static byte[] _ground = new byte[1];
    private static Godot.Collections.Dictionary _layout = new();
    private static Godot.Collections.Dictionary _projection = new();

    static WorldMap() => Load();

    public static void Load()
    {
        var json = GD.Load<Json>(MetaPath);
        var img = GD.Load<Image>(GroundPath);
        if (json == null || img == null) { GD.PushError("[world] Data/World is missing - run tools/world/build_world.py"); return; }
        var meta = json.Data.AsGodotDictionary();
        _layout = meta["layout"].AsGodotDictionary();
        _projection = meta["projection"].AsGodotDictionary();
        PxPerKm = (float)meta["px_per_km"].AsDouble();
        GroundImage = img;
        Columns = img.GetWidth(); Rows = img.GetHeight();

        // Each pixel is matched to the nearest legend colour, so a hand-painted
        // ground.png needn't hit the colours exactly.
        var legend = new List<Color>();
        foreach (var e in meta["legend"].AsGodotArray())
        {
            var rgb = e.AsGodotDictionary()["rgb"].AsGodotArray();
            legend.Add(Color.Color8((byte)rgb[0].AsInt32(), (byte)rgb[1].AsInt32(), (byte)rgb[2].AsInt32()));
        }
        _ground = new byte[Columns * Rows];
        var raw = (Image)img.Duplicate();
        if (raw.GetFormat() != Image.Format.Rgb8) raw.Convert(Image.Format.Rgb8);
        byte[] px = raw.GetData();
        var cache = new Dictionary<int, byte>();
        for (int i = 0; i < Columns * Rows; i++)
        {
            int r = px[i * 3], gr = px[i * 3 + 1], b = px[i * 3 + 2];
            int key = (r << 16) | (gr << 8) | b;
            if (!cache.TryGetValue(key, out byte g))
            {
                float best = float.MaxValue;
                for (int k = 0; k < legend.Count; k++)
                {
                    var l = legend[k];
                    float d = (l.R8 - r) * (l.R8 - r) + (l.G8 - gr) * (l.G8 - gr) + (l.B8 - b) * (l.B8 - b);
                    if (d < best) { best = d; g = (byte)k; }
                }
                cache[key] = g;
            }
            _ground[i] = g;
        }
        // Where the road crosses a river it fords it.
        if (_layout.TryGetValue("crossings", out var cr))
            foreach (var v in cr.AsGodotArray()) { var a = v.AsGodotArray(); Set(new Vector2I(a[0].AsInt32(), a[1].AsInt32()), Ground.Ford); }

        Places.Clear();
        foreach (var v in meta["places"].AsGodotArray())
        {
            var p = v.AsGodotDictionary();
            var cell = p["cell"].AsGodotArray();
            Places.Add(new WorldPlace(p["name"].AsString(), Str(p, "kind"), Str(p, "company"), p.TryGetValue("year", out var yr) ? yr.AsInt32() : 0,
                new Vector2I(cell[0].AsInt32(), cell[1].AsInt32()), p["lon"].AsDouble(), p["lat"].AsDouble(), Str(p, "note")));
        }
    }

    private static string Str(Godot.Collections.Dictionary d, string k) => d.TryGetValue(k, out var v) ? v.AsString() : "";
    private static void Set(Vector2I c, Ground g) { if (InBounds(c)) _ground[c.Y * Columns + c.X] = (byte)g; }

    // Hand-laid water the atlas is too coarse to have (a creek behind a post).
    public static void Carve(IEnumerable<Vector2I> cells, Ground g) { foreach (var c in cells) Set(c, g); }

    public static bool InBounds(Vector2I c) => c.X >= 0 && c.Y >= 0 && c.X < Columns && c.Y < Rows;

    // Off the edge of the map is open water: nobody walks off the world.
    public static Ground At(Vector2I c) => InBounds(c) ? (Ground)_ground[c.Y * Columns + c.X] : Ground.Ocean;
    public static bool IsWater(Vector2I c) => At(c) is Ground.Ocean or Ground.Lake or Ground.River;
    public static bool IsSea(Vector2I c) => At(c) == Ground.Ocean;

    // Within `rings` cells of open water of the given kind (beaches, banks).
    public static bool Near(Vector2I c, Func<Ground, bool> kind, int rings = 1)
    {
        for (int dy = -2 * rings; dy <= 2 * rings; dy++)
            for (int dx = -rings; dx <= rings; dx++)
                if (kind(At(new Vector2I(c.X + dx, c.Y + dy)))) return true;
        return false;
    }

    // ---- the fitted layout ---------------------------------------------------

    public static Vector2I Anchor(string key, Vector2I fallback)
    {
        if (!_layout.TryGetValue(key, out var v)) return fallback;
        var a = v.AsGodotArray();
        return new Vector2I(a[0].AsInt32(), a[1].AsInt32());
    }

    public static Vector2[] Polyline(string key)
    {
        if (!_layout.TryGetValue(key, out var v)) return Array.Empty<Vector2>();
        var list = new List<Vector2>();
        foreach (var p in v.AsGodotArray()) { var a = p.AsGodotArray(); list.Add(new Vector2((float)a[0].AsDouble(), (float)a[1].AsDouble())); }
        return list.ToArray();
    }

    public static Godot.Collections.Array LayoutArray(string key) =>
        _layout.TryGetValue(key, out var v) ? v.AsGodotArray() : new Godot.Collections.Array();

    // The carrying places on the York Factory - Oxford House road, upstream
    // order, as a fraction of the way along WorldLayout.RiverRoad.
    public static List<(string Name, float Fraction, Vector2I Cell)> Portages()
    {
        var list = new List<(string, float, Vector2I)>();
        foreach (var v in LayoutArray("portages"))
        {
            var d = v.AsGodotDictionary(); var c = d["cell"].AsGodotArray();
            list.Add((d["name"].AsString(), (float)d["fraction"].AsDouble(), new Vector2I(c[0].AsInt32(), c[1].AsInt32())));
        }
        return list;
    }

    public static WorldPlace? Place(string name)
    {
        foreach (var p in Places) if (p.Name == name) return p;
        return null;
    }

    // ---- lon/lat -> cell (Lambert conformal conic, as the importer) -------------

    public static Vector2I CellOf(double lon, double lat)
    {
        double r(double d) => d * Math.PI / 180.0;
        double lat1 = _projection["lat1"].AsDouble(), lat2 = _projection["lat2"].AsDouble(), lat0 = _projection["lat0"].AsDouble(), lon0 = _projection["lon0"].AsDouble();
        double n = Math.Log(Math.Cos(r(lat1)) / Math.Cos(r(lat2))) / Math.Log(Math.Tan(Math.PI / 4 + r(lat2) / 2) / Math.Tan(Math.PI / 4 + r(lat1) / 2));
        double f = Math.Cos(r(lat1)) * Math.Pow(Math.Tan(Math.PI / 4 + r(lat1) / 2), n) / n;
        double rho(double la) => 6371.0 * f / Math.Pow(Math.Tan(Math.PI / 4 + r(la) / 2), n);
        double th = n * r(lon - lon0), p = rho(lat);
        double x = p * Math.Sin(th), y = rho(lat0) - p * Math.Cos(th);
        var world = new Vector2((float)((x - _projection["x0_km"].AsDouble()) * PxPerKm), (float)((_projection["ytop_km"].AsDouble() - y) * PxPerKm));
        return IsoGrid.FromWorld(world);
    }
}
