using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Agents.Goals;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Economy;
using IsometricFurTradeGame.Scripts.Items;
using IsometricFurTradeGame.Scripts.Player;
using NetHub = IsometricFurTradeGame.Scripts.Net.Net;

namespace IsometricFurTradeGame.Scripts.World;

// The country's boatbuilders (design doc 12.6, 13): every band in the birch
// country makes its own canoes in the spring and early summer, when the bark
// peels; the Rainy Lake and lakes-country Ojibwe made north canoes for the
// North West Company; at York, Oxford House and the Bay posts the Orkneymen
// built York boats through the winter. Each is a yard: a bed by the water
// with a boat on it (World/Canoe, Stage < done), the people of that camp or
// post as its hands.
//
// Every stage is done by the same rules as the player's (World/Boatbuilding):
// the stage's materials gathered and laid by, its hours of work put in, its
// hand-work played - by the builders' own skill (Boats.AiScore). A builder in
// sight of a player walks out to the bed (or to the birches) and works there;
// out of sight, the day's work is reckoned (Day()).
//
// The posts' yards also keep the brigades' boats (Draw/Return): a convoy
// sails with what its yard has fit to go, brings them back worn, and the
// worst are condemned each New Year - so a yard whose builders are idle,
// dead or gone sends fewer boats every year.
public static class Boatwrights
{
    public sealed class Yard
    {
        public string Name = "";
        public bool IsPost;
        public BoatKind Kind;
        public Vector2 Bed;                 // where the bed is made
        public Household Home;
        public CountryCamp Camp;
        public int SiteIndex = -1;          // the boat on the bed, if any
        public readonly List<int> Afloat = new();   // the camp's finished canoes (by index)
        public int Want = 1;
        public readonly List<(string Player, BoatKind Kind, float Paid)> Orders = new();
        public int Built;                   // boats this yard has finished, all told
        public float Cut = 0.2f;            // at a post: the craft of its second-best builder (the two best build)
    }

    public static readonly List<Yard> Yards = new();
    private static bool _laid;

    // The posts' boats for the brigades, by post and kind: how many, how sound.
    private static readonly Dictionary<string, (int Count, float Sound)> _fleet = new();

    public static void Reset() { Yards.Clear(); _laid = false; _fleet.Clear(); _devDone = false; _stocked = false; _liveToday.Clear(); }

    private static readonly bool Trace = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--boattrace") >= 0;
    private static void Log(string s) { if (Trace) GD.Print("[boats] " + s); }

    private static GameCalendar Cal => GameCalendar.Instance;

    // ---- where the yards are ----------------------------------------------------

    private static void Lay()
    {
        if (_laid || Sim.Spawner == null) return;
        _laid = true;
        foreach (var camp in Camps.All)
        {
            if (camp.IsFamily || camp.Ledger == null || !BarkPeople(camp.Nation)) continue;
            var g = WorldMap.At(camp.Centre);
            if (g is not (Ground.Boreal or Ground.LakesForest or Ground.Shield or Ground.Lowland)) continue;
            var bed = BedNear(IsoGrid.ToWorld(camp.Centre), 60);
            if (bed == null) continue;
            var kind = g == Ground.LakesForest && camp.Nation == Faction.Ojibwe && camp.People >= 60 ? BoatKind.North : BoatKind.Small;
            Yards.Add(new Yard { Name = camp.Name, Kind = kind, Bed = bed.Value, Home = camp.Ledger, Camp = camp, Want = Mathf.Clamp(1 + camp.People / 60, 1, 3) });
        }
        // The bands by the posts (the homeguard Cree at York, the Assiniboine): their own canoes.
        foreach (var band in Sim.Bands)
        {
            if (band is CountryBand || band == null || !BarkPeople(band.Nation) || Yards.Exists(y => y.Home == band)) continue;
            var g = WorldMap.At(band.Camp);
            if (g is not (Ground.Boreal or Ground.LakesForest or Ground.Shield or Ground.Lowland)) continue;
            var bed = BedNear(IsoGrid.ToWorld(band.Camp), 60);
            if (bed == null) continue;
            string name = !string.IsNullOrEmpty(band.Name) && band.Name != "the band" ? band.Name : $"the {Camps.NationName(band.Nation)} camp";
            Yards.Add(new Yard { Name = name, Kind = BoatKind.Small, Bed = bed.Value, Home = band, Want = 2 });
        }
        void PostYard(string name, BoatKind kind, Household home)
        {
            if (home == null) return;
            var at = IsoGrid.ToWorld(home.AccessCell);
            var bed = BedNear(at, 50);
            if (bed == null) return;
            Yards.Add(new Yard { Name = name, IsPost = true, Kind = kind, Bed = bed.Value, Home = home, Want = 2 });
        }
        PostYard("York Factory", BoatKind.York, Sim.Fort);
        PostYard("Oxford House", BoatKind.York, Sim.Outpost);
        if (Convoys.Ledger("Fort William") is { } fw) PostYard("Fort William", BoatKind.North, fw);
        if (Convoys.Ledger("Norway House") is { } nh) PostYard("Norway House", BoatKind.York, nh);
        Log($"{Yards.Count} yards laid out");
    }

    // The birchbark peoples: the Cree, the Anishinaabe, the Dene. (The plains
    // nations had the bullboat, the coast the cedar dugout.)
    private static bool BarkPeople(Faction f) => f is Faction.Cree or Faction.Ojibwe or Faction.Dene;

    // Dry ground with water a stone's throw off, nearest the place first.
    private static Vector2? BedNear(Vector2 at, int reach)
    {
        var c = IsoGrid.FromWorld(at);
        for (int r = 2; r <= reach; r++)
            for (int dx = -r; dx <= r; dx++)
                for (int dy = -2 * r; dy <= 2 * r; dy += Mathf.Abs(dx) == r ? 1 : 4 * r)
                {
                    var n = new Vector2I(c.X + dx, c.Y + dy);
                    if (!WorldMap.InBounds(n) || WorldMap.IsWater(n) || WorldLayout.ForestAt(n)) continue;
                    bool water = false;
                    foreach (var e in IsoGrid.EdgeNeighbours(n)) foreach (var e2 in IsoGrid.EdgeNeighbours(e)) if (WorldMap.IsWater(e2)) water = true;
                    if (!water) continue;
                    if (Sim.Paths != null && Sim.Terrain != null && Sim.Terrain.IsLoaded(n) && !Sim.Paths.IsWalkable(n)) continue;
                    return IsoGrid.ToWorld(n);
                }
        return null;
    }

    public static Yard YardOf(Canoe c) => c == null || c.Yard == "" ? null : Yards.Find(y => y.Name == c.Yard);
    public static Yard YardOf(Household h) => h == null ? null : Yards.Find(y => y.Home == h);
    public static Canoe Site(Yard y) => y.SiteIndex >= 0 ? Sim.Spawner?.CanoeAt(y.SiteIndex) : null;

    // Who builds at a yard: the camp's grown people; at a post, its labouring men.
    public static bool IsBuilder(Npc n, Yard y)
    {
        if (n == null || n.IsDead || n.HomeStores != y.Home || n.HeldBy != null || n.Away) return false;
        return y.IsPost ? n.Role is Role.Labourer or Role.Voyageur && n.Skills.Crafting >= Mathf.Max(0.12f, y.Cut) : n.Role is Role.BandWoman or Role.Hunter or Role.Chief;
    }
    public static bool IsBuilderFor(Npc n, Canoe c) => YardOf(c) is { } y && IsBuilder(n, y);

    // Bark work in the bark season; planked work through the winter in the shed.
    public static bool InSeason(Yard y)
    {
        var cal = Cal; if (cal == null) return false;
        int m = cal.Month;
        if (Boats.Of(y.Kind).Planked) return m >= 10 || m <= 5;
        return m >= 5 && m <= 9 && !(m == 5 && cal.Day < 15);
    }

    // ---- the day's round ------------------------------------------------------------

    private static bool _stocked;
    public static void Day()
    {
        if (!NetHub.IsServer || Cal == null) return;
        Lay();
        // The Bay posts' boat stores, once, at a new world's start: what the
        // ship had brought for the boatbuilders.
        if (!_stocked)
        {
            _stocked = true;
            foreach (var y in Yards)
                if (y.IsPost && Boats.Of(y.Kind).Planked && y.Home.Stores.Count(ItemId.Nails) == 0)
                { y.Home.Stores.Add(ItemId.Nails, 40); y.Home.Stores.Add(ItemId.Oakum, 30); y.Home.Stores.Add(ItemId.Pitch, 12); y.Home.Stores.Add(ItemId.Plank, 30); }
            if (Sim.Fort != null && Sim.Fort.Stores.Count(ItemId.CrookedKnife) == 0) { Sim.Fort.Stores.Add(ItemId.CrookedKnife, 4); Sim.Fort.Stores.Add(ItemId.Awl, 6); Sim.Fort.Stores.Add(ItemId.SpruceGum, 10); }
            if (Sim.Nwc != null && Sim.Nwc.Stores.Count(ItemId.CrookedKnife) == 0) { Sim.Nwc.Stores.Add(ItemId.CrookedKnife, 4); Sim.Nwc.Stores.Add(ItemId.Awl, 6); Sim.Nwc.Stores.Add(ItemId.SpruceGum, 10); }
        }
        DevArgs();
        // Who builds where, once a day.
        var hands = new Dictionary<Yard, (int Live, int Away, float Best)>();
        var byHome = new Dictionary<Household, Yard>();
        foreach (var y in Yards) byHome[y.Home] = y;
        // A post's boatbuilders are its two best hands at it.
        var crafts = new Dictionary<Yard, List<float>>();
        foreach (var n in Sim.Npcs)
            if (n != null && !n.IsDead && n.HomeStores != null && byHome.TryGetValue(n.HomeStores, out var py) && py.IsPost && n.Role is Role.Labourer or Role.Voyageur)
            { if (!crafts.TryGetValue(py, out var l)) crafts[py] = l = new List<float>(); l.Add(n.Skills.Crafting); }
        foreach (var (py, l) in crafts) { l.Sort((a, b) => b.CompareTo(a)); py.Cut = l[Mathf.Min(1, l.Count - 1)]; }
        foreach (var n in Sim.Npcs)
        {
            if (n == null || n.IsDead || n.HomeStores == null || !byHome.TryGetValue(n.HomeStores, out var y) || !IsBuilder(n, y)) continue;
            var h = hands.GetValueOrDefault(y);
            if (n.Tier == Tier.Live) h.Live++; else h.Away++;
            h.Best = Mathf.Max(h.Best, n.Skills.Crafting);
            hands[y] = h;
        }
        bool newYear = Cal.Month == 1 && Cal.Day == 1;
        foreach (var y in Yards)
        {
            var h = hands.GetValueOrDefault(y);
            YardDay(y, h.Live, h.Away, h.Best);
        }
        if (newYear) Condemn();
        AgeBoats();
    }

    private static void YardDay(Yard y, int live, int away, float best)
    {
        y.Afloat.RemoveAll(i => Sim.Spawner.CanoeAt(i) is not { } c || c.Wrecked || c.Owner != y.Name);
        var site = Site(y);
        if (site != null && (site.Wrecked || site.Launched)) { y.SiteIndex = -1; site = null; }
        int builders = live + away;
        if (builders == 0) return;
        if (site == null)
        {
            if (!InSeason(y)) return;
            int fleet = Fleet(y.Name, y.Kind).Count; if (fleet < 0) fleet = WantFleet(y);
            bool want = y.Orders.Count > 0 || (y.IsPost ? fleet < WantFleet(y) : y.Afloat.Count < y.Want);
            if (!want || GD.Randf() > 0.35f) return;
            var kind = y.Orders.Count > 0 ? y.Orders[0].Kind : y.Kind;
            if (best + 0.05f < Boats.Of(kind).MinSkill) kind = BoatKind.Small;
            site = Sim.Spawner.PlaceCanoe(-1, y.Bed, kind);
            site.Stage = 0; for (int i = 0; i < 4; i++) site.Parts[i] = 0f; site.Shape = 0f;
            site.Yard = y.Name; site.Owner = y.Name; site.BuiltAt = $"at {y.Name}";
            if (y.Orders.Count > 0) { site.CommissionedBy = y.Orders[0].Player; site.PaidMb = y.Orders[0].Paid; y.Orders.RemoveAt(0); }
            site.Refresh();
            y.SiteIndex = site.Index;
            Log($"{y.Name}: a {site.Spec.Name} begun on the bed{(site.CommissionedBy != "" ? $" for {site.CommissionedBy}" : "")}");
            return;
        }
        // What the stage wants is gathered by the out-of-sight hands, and by
        // those in sight too where there is nothing at hand to gather (bark
        // brought down from inland, in a country without birch).
        if (builders > 0 && !Cal.Weather.FoulAt(site.GlobalPosition) && site.NextStage is BoatStage gs)
        {
            int gatherers = away + (live + 1) / 2;
            foreach (var (item, n) in Boatbuilding.NeedsFor(site, gs))
            {
                int lack = n - site.Stock.Count(item);
                if (lack <= 0 || !CanGather(item, Cal.Month)) continue;
                int take = Mathf.Min(lack, Mathf.Max(1, gatherers * GatherPerDay(item)));
                // Nails, oakum and pitch come out of the post's store (the ship
                // brings them); planks the men saw at the pit.
                if (item is ItemId.Nails or ItemId.Oakum or ItemId.Pitch) take = y.Home.Stores.TakeUpTo(item, take);
                site.Stock.Add(item, take);
            }
        }
        // Out-of-sight hands work the day through; those in sight do it with
        // their own feet (Errand).
        if (away > 0 && !Cal.Weather.FoulAt(site.GlobalPosition))
        {
            site.WorkHours += Mathf.Min(away, y.IsPost ? 2 : 4) * 6f;
        }
        // The hands in sight who were kept from the bed (other work, or a
        // clock run too fast for them to walk it): the day's work is still done.
        if (live > 0)
        {
            float owed = Mathf.Min(live, y.IsPost ? 2 : 4) * 4f - _liveToday.GetValueOrDefault(y);
            if (owed > 0f && !Cal.Weather.FoulAt(site.GlobalPosition)) site.WorkHours += owed;
        }
        _liveToday[y] = 0f;
        TryFinishStage(y, site, best);
    }

    private static int GatherPerDay(ItemId item) => item switch { ItemId.BirchBark => 1, ItemId.Watap => 3, ItemId.SpruceGum => 2, ItemId.Cedar => 2, ItemId.Plank => 3, _ => 2 };
    private static bool CanGather(ItemId item, int month) => item switch
    {
        ItemId.BirchBark => Boats.BarkSeason(month) >= 0.5f,
        ItemId.Watap => month >= 5 && month <= 10,
        // The posts draw these from their stores (the ship brings nails, oakum and pitch).
        ItemId.Plank or ItemId.Nails or ItemId.Oakum or ItemId.Pitch => true,
        _ => true,
    };

    // A stage is done when its hours are in and its stuff laid by.
    public static void TryFinishStage(Yard y, Canoe site, float best)
    {
        if (site.NextStage is not BoatStage s) return;
        int idx = System.Array.IndexOf(site.Spec.Stages, s);
        if (site.WorkHours < site.Spec.Hours[idx]) return;
        foreach (var (item, n) in Boatbuilding.NeedsFor(site, s)) if (site.Stock.Count(item) < n) return;
        if (Boats.NeedsThaw(s) && Cal.IsFrozen && !site.Spec.Planked) return;
        foreach (var (item, n) in Boatbuilding.NeedsFor(site, s)) site.Stock.Remove(item, n);
        site.WorkHours = 0f;
        // The best hands set the standard; a second or third pair helps.
        float q = Boats.AiScore(best);
        var misses = new List<float>();
        int k = Mathf.RoundToInt((1f - q) * 5f);
        for (int i = 0; i < k; i++) misses.Add(GD.Randf());
        Boatbuilding.ApplyStageWork(site, s, q, misses);
        Log($"{y.Name}: {Boats.StageName(s).ToLower()} done ({Boats.Grade(q)})");
        if (site.Launched) Finished(y, site);
        site.Refresh();
    }

    public static void Finished(Yard y, Canoe site)
    {
        y.SiteIndex = -1; y.Built++;
        site.Yard = "";
        var cal = Cal;
        site.BuiltDay = cal != null ? Campaign.DayOf(cal.Year, cal.Month, cal.Day) : 0;
        site.Builders = $"the people of {y.Name}";
        Chronicle.Event($"A {site.Spec.Name} was finished at {y.Name}.", site.GlobalPosition, null, y.Name, 0.4f);
        if (site.CommissionedBy != "")
        {
            site.Owner = site.CommissionedBy;
            foreach (var p in Sim.Players())
                if (p.DisplayName == site.CommissionedBy)
                {
                    p.Say($"Word comes from {y.Name}: the {site.Spec.Name} you paid for is finished and waiting for you on the shore there.");
                    p.Journal.Add($"{cal?.DateString}: The {site.Spec.Name} built for you at {y.Name} is finished ({Boats.Grade(site.Quality)} work).");
                }
            return;
        }
        if (y.IsPost)
        {
            // Into the yard's boats, for the brigades.
            var f = Fleet(y.Name, y.Kind);
            if (f.Count < 0) f = (WantFleet(y), 0.85f);   // a yard never counted has the boats it always had
            _fleet[Key(y.Name, y.Kind)] = (f.Count + 1, (f.Sound * f.Count + site.Condition) / (f.Count + 1));
            Sim.Spawner.RemoveCanoe(site);
            Sim.NoticeAt(IsoGrid.ToWorld(y.Home.AccessCell), $"A new {site.Spec.Name} is finished at {y.Name}.");
            return;
        }
        site.Owner = y.Name;
        y.Afloat.Add(site.Index);
    }

    // ---- the boats as the days go by ---------------------------------------------------

    // Gum dries and cracks in the sun and in the frost; a boat neglected leaks.
    private static void AgeBoats()
    {
        var cal = Cal; if (cal == null || Sim.Spawner == null) return;
        foreach (var c in new List<Canoe>(Sim.Spawner.Canoes))
        {
            if (c.Building) continue;
            if (c.Wrecked) { if (c.IsFree) Sim.Spawner.RemoveCanoe(c); continue; }
            float dry = c.Spec.Planked ? 0.002f : cal.Month is 7 or 8 ? 0.008f : cal.IsFrozen ? 0.006f : 0.004f;
            c.Wear(HullPart.Seams, dry);
            if (c.Part(HullPart.Seams) < 0.5f && GD.Randf() < (0.5f - c.Part(HullPart.Seams)) * 0.8f) c.Hole(GD.Randf(), 0.15f);
            // A camp's canoes are kept up by their owners.
            if (YardOwner(c) is { } y && Site(y) == null && GD.Randf() < 0.3f)
            {
                c.Mend(HullPart.Seams, 0.85f); if (c.Leaks.Count > 0) c.Leaks.RemoveAt(0);
            }
            c.Refresh();
        }
    }

    private static Yard YardOwner(Canoe c) => c.Owner == "" ? null : Yards.Find(y => y.Name == c.Owner && !y.IsPost);

    // ---- the posts' boats for the brigades ------------------------------------------------

    private static string Key(string post, BoatKind k) => post + "|" + (int)k;
    private static (int Count, float Sound) Fleet(string post, BoatKind k) => _fleet.GetValueOrDefault(Key(post, k), (-1, 0.85f));
    private static int WantFleet(Yard y)
    {
        int want = 0;
        foreach (var r in Convoys.Runs) foreach (var l in r.Legs) if (l.From == y.Name && Boats.ForCraft(r.Craft.Name) == y.Kind) want = Mathf.Max(want, r.Boats + 1);
        return Mathf.Max(want, 2);
    }

    // What a brigade sails with: as many as are fit, at most what it wants.
    // A yard never asked before has the boats the brigade always had.
    public static (int Got, float Sound) Draw(string post, BoatKind kind, int want)
    {
        var (count, sound) = Fleet(post, kind);
        if (count < 0) { count = want + 1; sound = 0.85f; }
        int got = Mathf.Min(want, count);
        if (got <= 0) { _fleet[Key(post, kind)] = (0, sound); return (1, 0.35f); }   // an old condemned hull patched up to go
        _fleet[Key(post, kind)] = (count - got, sound);
        return (got, sound);
    }

    public static void Return(string post, BoatKind kind, int n, float sound)
    {
        if (n <= 0) return;
        var (count, s) = Fleet(post, kind);
        if (count < 0) count = 0;
        _fleet[Key(post, kind)] = (count + n, count + n > 0 ? (s * count + sound * n) / (count + n) : sound);
    }

    // Each New Year the worst boats are condemned for firewood; a yard's
    // men mend the rest through the winter.
    private static void Condemn()
    {
        foreach (var key in new List<string>(_fleet.Keys))
        {
            var (count, sound) = _fleet[key];
            if (count <= 0) continue;
            bool york = key.EndsWith("|" + (int)BoatKind.York);
            int gone = Mathf.RoundToInt(count * (sound < 0.5f ? 0.5f : york ? 0.15f : 0.3f));
            _fleet[key] = (count - gone, Mathf.Min(0.9f, sound + 0.15f));
            if (gone > 0) Log($"{key}: {gone} boats condemned at the New Year");
        }
    }

    public static string FleetLine(string post)
    {
        var parts = new List<string>();
        foreach (BoatKind k in System.Enum.GetValues(typeof(BoatKind)))
        {
            var (count, sound) = Fleet(post, k);
            if (count >= 0) parts.Add($"{count} {Boats.Of(k).Name}{(count == 1 ? "" : "s")} ({Boats.Condition(sound)})");
        }
        return parts.Count == 0 ? "" : string.Join(", ", parts);
    }

    // ---- live builders: the walk out to the birches and the bed ----------------------------

    // Called from RoleBehaviour.NextGoal: a builder in sight of a player, in
    // working hours, with a boat on the yard's bed.
    // Hours the builders in sight put in with their own hands today, by yard.
    private static readonly Dictionary<Yard, float> _liveToday = new();
    public static Goal Errand(Npc npc)
    {
        if (npc.Tier != Tier.Live || npc.HomeStores == null) return null;
        var y = YardOf(npc.HomeStores);
        if (y == null) return null;
        if (!IsBuilder(npc, y)) { if (Trace && y.IsPost && GD.Randf() < 0.02f) Log($"{npc.DisplayName} ({npc.Role}, craft {npc.Skills.Crafting:0.00}, cut {y.Cut:0.00}) is not a builder at {y.Name}"); return null; }
        var site = Site(y);
        if (site == null || site.NextStage is not BoatStage s) return null;
        if (npc.GlobalPosition.DistanceTo(site.GlobalPosition) > 3500f) { if (Trace) Log($"{npc.DisplayName} too far from the bed ({npc.GlobalPosition.DistanceTo(site.GlobalPosition):0})"); return null; }
        if (GD.Randf() > 0.55f) return null;
        // First what's wanted on the bed.
        foreach (var (item, n) in Boatbuilding.NeedsFor(site, s))
        {
            if (site.Stock.Count(item) >= n || !CanGather(item, Cal.Month)) continue;
            var g = GatherErrand(npc, site, item);
            if (g != null) return g;
        }
        if (Boats.NeedsThaw(s) && Cal.IsFrozen && !site.Spec.Planked) return null;
        var bed = IsoGrid.FromWorld(site.GlobalPosition);
        string label = Boats.StageLabel(s);
        Log($"{npc.DisplayName} goes to {label} at {y.Name} ({site.WorkHours:0} h in)");
        return new ChoreGoal(label,
            ChoreGoal.Walk("walk down to the canoe bed", Sim.Paths?.NearestWalkable(bed, 6) ?? bed, 110f),
            ChoreGoal.Do(n => { n.FaceToward(site.GlobalPosition); }),
            ChoreGoal.Work(label, 2f),
            ChoreGoal.Do(n =>
            {
                if (!GodotObject.IsInstanceValid(site) || site.Launched) return;
                site.WorkHours += 2f;
                _liveToday[y] = _liveToday.GetValueOrDefault(y) + 2f;
                n.Skills.Learn(ref n.Skills.Crafting, 0.002f);
                if (Sim.Player != null && n.GlobalPosition.DistanceTo(Sim.Player.GlobalPosition) < 900f && GD.Randf() < 0.3f)
                    Sim.NoticeAt(n, $"{n.DisplayName} is at the {site.Spec.Short} on the bed: {label}.");
                float best = 0f; foreach (var m in Sim.AgentsWithin(site.GlobalPosition, 600f)) if (m is Npc b && IsBuilder(b, y)) best = Mathf.Max(best, b.Skills.Crafting);
                TryFinishStage(y, site, Mathf.Max(best, n.Skills.Crafting));
            }));
    }

    private static Goal GatherErrand(Npc npc, Canoe site, ItemId item)
    {
        System.Func<Vector2I, bool> which = item switch
        {
            ItemId.BirchBark => Boats.IsBirch,
            ItemId.Watap or ItemId.SpruceGum => Boats.IsConifer,
            ItemId.Cedar => _ => true,
            _ => null,
        };
        if (which == null) return null;   // planks, nails and the like come out of the store
        var tree = Boats.NearestTreeWhere(npc.GlobalPosition, 2400f, which);
        if (tree == null) return null;
        string label = item switch { ItemId.BirchBark => "strip birch bark for the canoe", ItemId.Watap => "dig spruce roots for watap", ItemId.SpruceGum => "gather spruce gum", _ => "split cedar for ribs" };
        var stand = Sim.Paths?.NearestWalkable(tree.Value, 4) ?? tree.Value;
        Log($"{npc.DisplayName} goes out to {label}");
        return new ChoreGoal(label,
            ChoreGoal.Walk("walk out to the trees", stand, 90f),
            ChoreGoal.Work(label, 1.5f),
            ChoreGoal.Do(n =>
            {
                if (!GodotObject.IsInstanceValid(site)) return;
                int got = item switch { ItemId.BirchBark => 1, ItemId.Cedar => 2, _ => 2 } + (GD.Randf() < n.Skills.Crafting ? 1 : 0);
                site.Stock.Add(item, got);
                if (item == ItemId.Cedar) Sim.Structures?.FellTreeAt(tree.Value);
            }));
    }

    // ---- commissions and sales (UI/ConversationBoats) --------------------------------------

    public static float Price(BoatKind k) => Mathf.Round(Boats.ValueMb(k, 0.75f, 1f));

    public static void Order(Yard y, PlayerController p, BoatKind k, float paid) => y.Orders.Add((p.DisplayName, k, paid));

    public static IEnumerable<Canoe> ForSale(Yard y)
    {
        foreach (int i in y.Afloat) if (Sim.Spawner.CanoeAt(i) is { } c && !c.Wrecked && c.IsFree && c.Owner == y.Name) yield return c;
    }

    // ---- save ---------------------------------------------------------------------------

    public static Godot.Collections.Dictionary Save()
    {
        var yards = new Godot.Collections.Array();
        foreach (var y in Yards)
        {
            var orders = new Godot.Collections.Array();
            foreach (var (pl, k, paid) in y.Orders) orders.Add(new Godot.Collections.Array { pl, (int)k, paid });
            var afloat = new Godot.Collections.Array(); foreach (var i in y.Afloat) afloat.Add(i);
            yards.Add(new Godot.Collections.Dictionary { ["name"] = y.Name, ["site"] = y.SiteIndex, ["afloat"] = afloat, ["orders"] = orders, ["built"] = y.Built });
        }
        var fleet = new Godot.Collections.Dictionary();
        foreach (var kv in _fleet) fleet[kv.Key] = new Godot.Collections.Array { kv.Value.Count, kv.Value.Sound };
        var stripped = new Godot.Collections.Array();
        foreach (var c in Boatbuilding.Stripped) { stripped.Add(c.X); stripped.Add(c.Y); }
        return new Godot.Collections.Dictionary { ["yards"] = yards, ["fleet"] = fleet, ["stripped"] = stripped };
    }

    public static void Load(Godot.Collections.Dictionary d)
    {
        Lay();
        _stocked = true;   // a saved world has its stores as they were
        if (d.TryGetValue("fleet", out var fv))
            foreach (var kv in fv.AsGodotDictionary()) { var a = kv.Value.AsGodotArray(); _fleet[kv.Key.AsString()] = (a[0].AsInt32(), a[1].AsSingle()); }
        if (d.TryGetValue("stripped", out var sv))
        {
            var a = sv.AsGodotArray(); var cells = new List<Vector2I>();
            for (int i = 0; i + 1 < a.Count; i += 2) cells.Add(new Vector2I(a[i].AsInt32(), a[i + 1].AsInt32()));
            Boatbuilding.RestoreStripped(cells);
        }
        if (!d.TryGetValue("yards", out var yv)) return;
        foreach (var v in yv.AsGodotArray())
        {
            var r = v.AsGodotDictionary();
            var y = Yards.Find(x => x.Name == r["name"].AsString());
            if (y == null) continue;
            y.SiteIndex = r["site"].AsInt32(); y.Built = r["built"].AsInt32();
            y.Afloat.Clear(); foreach (var i in r["afloat"].AsGodotArray()) y.Afloat.Add(i.AsInt32());
            y.Orders.Clear(); foreach (var o in r["orders"].AsGodotArray()) { var a = o.AsGodotArray(); y.Orders.Add((a[0].AsString(), (BoatKind)a[1].AsInt32(), a[2].AsSingle())); }
        }
    }

    // ---- dev ------------------------------------------------------------------------------

    // `--boatyard` starts a canoe on the nearest yard's bed at once (any season)
    // and puts its stuff by it, so builders near the player go to work on it;
    // `--boattrace` logs every yard's doings.
    private static bool _devDone;
    private static void DevArgs()
    {
        if (_devDone) return;
        _devDone = true;
        if (System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--boatyard") < 0 || Sim.Player == null) return;
        Yard best = null; float bd = float.MaxValue;
        foreach (var y in Yards) { float d = y.Bed.DistanceTo(Sim.Player.GlobalPosition); if (d < bd) { bd = d; best = y; } }
        if (best == null) return;
        var site = Sim.Spawner.PlaceCanoe(-1, best.Bed, best.Kind);
        site.Stage = 0; for (int i = 0; i < 4; i++) site.Parts[i] = 0f; site.Shape = 0f;
        site.Yard = best.Name; site.Owner = best.Name; site.BuiltAt = $"at {best.Name}";
        foreach (var stage in site.Spec.Needs) foreach (var (item, n) in stage) site.Stock.Add(item, n);
        site.Refresh();
        best.SiteIndex = site.Index;
        GD.Print($"[dev] boatyard: a {site.Spec.Name} on the bed at {best.Name} ({bd:0} px away)");
    }
}
