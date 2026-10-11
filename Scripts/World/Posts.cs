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
    public int Year, Closed;   // Closed: the year it was given up (0 = still kept)
    public Vector2I Top;
    public BuildingDef House;
    public TradingPostLedger Ledger;
    public bool Standing => Ledger != null;

    // An ordinary post is 12 x 9; a headquarters is bigger (Load).
    public int Width = 12, Depth = 9, GateOffset = 5;
    public Vector2I GateCell => IsoGrid.Cell(Top, GateOffset, Depth - 1);
    public Vector2I GateOutside => IsoGrid.DownLeft(IsoGrid.DownLeft(GateCell));
    public Vector2I WoodpileAt = new(9, 6), CrateAt = new(10, 3);   // in the palisade's own cells
    public Vector2I WoodpileCell => IsoGrid.Cell(Top, WoodpileAt.X, WoodpileAt.Y);
    public Vector2I CrateCell => IsoGrid.Cell(Top, CrateAt.X, CrateAt.Y);
    // More buildings than the one house (Fort William's Great Hall), and how
    // much further than an ordinary post the bush is kept back.
    public readonly List<BuildingDef> Extra = new();
    public float Extent;
    // From a plan (Data/World/post_plans.json): props, and the size of the staff.
    public readonly List<(string Kind, Vector2I Cell)> Props = new();
    public int StaffMen = 2, StaffWomen;
    public bool StaffClerk, Planned;
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
                Closed = d.TryGetValue("closed", out var cl) ? cl.AsInt32() : 0,
                House = new BuildingDef(BuildingKind.TradingPost, d["name"].AsString(), IsoGrid.Cell(top, 2, 2), 7, 5, 3),
            };
            if (!ApplyPlan(p) && p.Name == "Fort William") FortWilliam(p);
            list.Add(p);
            _byName[p.Name] = p;
            var key = new Vector2I(Mathf.FloorToInt(p.Centre.X / GridPx), Mathf.FloorToInt(p.Centre.Y / GridPx));
            if (!_grid.TryGetValue(key, out var cell)) _grid[key] = cell = new List<TradingPost>();
            cell.Add(p);
        }
        return list;
    }

    // The North West Company's inland capital (design doc 5.2): where the
    // partners and the Montreal agents meet each July in the Great Hall.
    // The real place was a walled town of forty buildings; this keeps its
    // heart - the store, the Great Hall, a partners' house - inside a
    // palisade on the Kaministiquia shore, as big as the ground allows
    // between the lake and the Ojibwe camp beside it.
    private static void FortWilliam(TradingPost p)
    {
        p.Width = 17; p.Depth = 10; p.GateOffset = 5;
        p.WoodpileAt = new Vector2I(3, 8); p.CrateAt = new Vector2I(14, 8);
        p.Extra.Add(new BuildingDef(BuildingKind.GreatHall, "the Great Hall", IsoGrid.Cell(p.Top, 9, 1), 7, 4, 6));
        p.Extra.Add(new BuildingDef(BuildingKind.PartnersHouse, "the partners' house", IsoGrid.Cell(p.Top, 9, 5), 5, 3, 2));
        p.Extent = 450f;
    }

    // A building kind's size and where its door is (as York's).
    public static (int W, int D, int Door) SizeOf(BuildingKind k) => k switch
    {
        BuildingKind.Depot => (7, 5, 3), BuildingKind.Warehouse => (8, 6, 3), BuildingKind.MensHouse => (7, 5, 3),
        BuildingKind.FactorsHouse => (5, 6, 2), BuildingKind.ClerksHouse => (5, 6, 2), BuildingKind.ProvisionStore => (5, 4, 2),
        BuildingKind.SummerMess => (5, 5, 2), BuildingKind.GuestHouse => (4, 5, 1), BuildingKind.PowderMagazine => (3, 3, 1),
        BuildingKind.GreatHall => (9, 7, 4), BuildingKind.PartnersHouse => (5, 4, 2), _ => (7, 5, 3),
    };

    // The major posts are laid out from Data/World/post_plans.json (hand
    // editable): their palisade, gate, buildings, props and staff.
    private static Godot.Collections.Dictionary _plans;
    private static bool ApplyPlan(TradingPost p)
    {
        _plans ??= GD.Load<Json>("res://Data/World/post_plans.json")?.Data.AsGodotDictionary() ?? new Godot.Collections.Dictionary();
        if (!_plans.TryGetValue(p.Name, out var pv)) return false;
        var plan = pv.AsGodotDictionary();
        p.Planned = true;
        p.Width = plan["width"].AsInt32(); p.Depth = plan["depth"].AsInt32(); p.GateOffset = plan["gate"].AsInt32();
        var buildings = new List<BuildingDef>();
        foreach (var bv in plan["buildings"].AsGodotArray())
        {
            var b = bv.AsGodotDictionary();
            if (!System.Enum.TryParse(b["kind"].AsString(), out BuildingKind kind)) continue;
            var (w, dd, door) = SizeOf(kind);
            buildings.Add(new BuildingDef(kind, b["name"].AsString(), IsoGrid.Cell(p.Top, b["i"].AsInt32(), b["j"].AsInt32()), w, dd, door));
        }
        if (buildings.Count == 0) return false;
        // The house the post is kept from is its trading room, else its first building.
        int main = buildings.FindIndex(b => b.Kind == BuildingKind.Depot);
        p.House = buildings[main < 0 ? 0 : main];
        foreach (var b in buildings) if (b != p.House) p.Extra.Add(b);
        p.WoodpileAt = new Vector2I(p.Width - 2, p.Depth - 2); p.CrateAt = new Vector2I(1, p.Depth - 2);
        if (plan.TryGetValue("props", out var props))
            foreach (var qv in props.AsGodotArray())
            {
                var q = qv.AsGodotDictionary(); var at = new Vector2I(q["i"].AsInt32(), q["j"].AsInt32());
                if (q["kind"].AsString() == "woodpile") p.WoodpileAt = at;
                else p.Props.Add((q["kind"].AsString(), IsoGrid.Cell(p.Top, at.X, at.Y)));
            }
        if (plan.TryGetValue("staff", out var sv))
        {
            var st = sv.AsGodotDictionary();
            p.StaffMen = st["men"].AsInt32(); p.StaffWomen = st["women"].AsInt32(); p.StaffClerk = st["clerk"].AsBool();
        }
        p.Extent = Mathf.Max(p.Width, p.Depth) * 30f;
        return true;
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
                    foreach (var p in ps) if (p.Centre.DistanceTo(at) < r + p.Extent) return true;
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
