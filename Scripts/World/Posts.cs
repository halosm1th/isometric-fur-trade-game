using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Economy;

namespace IsometricFurTradeGame.Scripts.World;

// One of the atlas's posts beyond the hand-built three: a small palisade,
// one house that is store, counter and bunkhouse, a master and a couple of
// men (their markers are in the scene under Cast/Posts), and its own books.
public class TradingPost
{
    public string Name, Company;
    public int Year;
    public Vector2I Top;
    public BuildingDef House;
    public TradingPostLedger Ledger;
    public bool Standing => Ledger != null;

    public const int Width = 12, Depth = 9, GateOffset = 5;
    public Vector2I GateCell => IsoGrid.Cell(Top, GateOffset, Depth - 1);
    public Vector2I GateOutside => IsoGrid.DownLeft(IsoGrid.DownLeft(GateCell));
    public Vector2I WoodpileCell => IsoGrid.Cell(Top, 9, 6);
    public Vector2I HearthCell => IsoGrid.Cell(House.Top, 1, 1);
    public Vector2I AccessCell => IsoGrid.Cell(House.Top, 2, 2);
    public Vector2 Centre => IsoGrid.ToWorld(IsoGrid.Cell(Top, Width / 2, Depth / 2));
    public Faction Faction => Company == "NWC" ? Faction.NWC : Faction.HBC;
}

// The posts of the wider country (design doc 6.1): they stand once the
// campaign says the timeline has reached them, and only then are painted,
// staffed and given books. The server decides; clients are told which stand.
public static class Posts
{
    // (Field order matters: the lookups must exist before Load fills them.)
    private static readonly Dictionary<string, TradingPost> _byName = new();
    private static readonly Dictionary<Vector2I, List<TradingPost>> _grid = new();
    private const float GridPx = 4096f;
    public static readonly List<TradingPost> All = Load();
    private static Node _host;

    private static List<TradingPost> Load()
    {
        var list = new List<TradingPost>();
        foreach (var v in WorldMap.LayoutArray("posts"))
        {
            var d = v.AsGodotDictionary();
            var t = d["top"].AsGodotArray();
            var top = new Vector2I(t[0].AsInt32(), t[1].AsInt32());
            var p = new TradingPost
            {
                Name = d["name"].AsString(), Company = d["company"].AsString(), Year = d["year"].AsInt32(), Top = top,
                House = new BuildingDef(BuildingKind.TradingPost, d["name"].AsString(), IsoGrid.Cell(top, 2, 2), 7, 5, 3),
            };
            list.Add(p);
            _byName[p.Name] = p;
            var key = new Vector2I(Mathf.FloorToInt(p.Centre.X / GridPx), Mathf.FloorToInt(p.Centre.Y / GridPx));
            if (!_grid.TryGetValue(key, out var cell)) _grid[key] = cell = new List<TradingPost>();
            cell.Add(p);
        }
        return list;
    }

    public static TradingPost Named(string name) => name != null && _byName.TryGetValue(name, out var p) ? p : null;
    public static IEnumerable<TradingPost> Standing() { foreach (var p in All) if (p.Standing) yield return p; }
    public static TradingPost Of(Household h) { foreach (var p in All) if (p.Ledger == h) return p; return null; }

    // Is there a post (standing or still to come) within r of this point?
    // The bush is kept cleared round a post's ground either way.
    public static bool NearAny(Vector2 at, float r)
    {
        int kx = Mathf.FloorToInt(at.X / GridPx), ky = Mathf.FloorToInt(at.Y / GridPx);
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
                if (_grid.TryGetValue(new Vector2I(kx + dx, ky + dy), out var ps))
                    foreach (var p in ps) if (p.Centre.DistanceTo(at) < r) return true;
        return false;
    }

    // ---- standing up -----------------------------------------------------------

    public static void Reset()
    {
        foreach (var p in All) p.Ledger = null;
        _host = null;
    }

    // At world start, on every peer (WorldRoot._Ready). The server raises
    // whatever already stands; a load waits for the save to say what does.
    public static void Begin(Node host)
    {
        _host = host;
        if (!Net.Net.IsServer || SaveGame.HasPending) return;
        Campaign.Begin();
        // Dev aid: `-- --noposts` leaves the wider country unbuilt and unmanned.
        foreach (string arg in OS.GetCmdlineUserArgs()) if (arg == "--noposts") return;
        Sync(spawnCast: false);
    }

    // Server: raise every post the campaign now says stands. At world start
    // the population spawns their people from the markers; later, here.
    public static void Sync(bool spawnCast)
    {
        if (!Net.Net.IsServer) return;
        bool changed = false;
        foreach (var p in All)
        {
            if (p.Standing || !Campaign.PostExists(p.Name)) continue;
            Raise(p);
            if (spawnCast) SpawnCast(p);
            changed = true;
        }
        if (changed) Net.Net.I?.BroadcastPosts(StandingNames());
    }

    public static string[] StandingNames()
    {
        var names = new List<string>();
        foreach (var p in All) if (p.Standing) names.Add(p.Name);
        return names.ToArray();
    }

    // Client: the server's word on which posts stand.
    public static void ApplyStanding(string[] names)
    {
        foreach (var n in names) if (Named(n) is { Standing: false } p) Raise(p);
    }

    private static void Raise(TradingPost p)
    {
        if (_host == null || p.Standing) return;
        p.Ledger = new TradingPostLedger { Name = $"Post_{p.Name.Replace(' ', '_').Replace('.', '_').Replace('\'', '_')}", Post = p };
        _host.AddChild(p.Ledger);
        Sim.Structures?.PaintPost(p, true);
    }

    // A post founded in play gets its people now (their markers name it).
    private static void SpawnCast(TradingPost p)
    {
        if (_host?.GetTree()?.GetFirstNodeInGroup("population") is not Population pop) return;
        foreach (var node in _host.GetTree().GetNodesInGroup(NpcSpawn.Group))
        {
            if (node is not NpcSpawn m || m.Home != HomePost.Post || m.PostName != p.Name) continue;
            var npc = pop.Spawn(m.DisplayName, m.Role, m.Faction, m.BedCell, m.WorkCell, p.Ledger, m.Frames, m.Tint, m.SpriteScale, m.Female);
            if (m.Age > 0) npc.Age = m.Age;
            m.FillInventory(npc);
            if (m.Leader) p.Ledger.Leader = npc;
        }
        Sim.Notice($"{p.Name} is built and manned ({(p.Company == "NWC" ? "the North West Company" : "the Company")}).");
    }
}
