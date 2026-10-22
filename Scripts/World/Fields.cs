using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Items;

namespace IsometricFurTradeGame.Scripts.World;

// The farms' fields (design doc 3.2, 12.5): every plot is a cell of ground
// with its own state, and the year's farming is the work done on it, one
// step at a time - ploughing, sowing, hoeing, reaping, carrying the crop in,
// threshing - by the farmer himself (Agents/TownLife's chores) or, out of
// sight, by the same steps in a day's sum (WorkDay). Either way it is the
// same plots in the same state, so nothing jumps when he comes into view,
// and the crop that comes home is what was sown and tended on them.
//
// A homestead's plots are the "field" ground laid out beside its house; a
// town farmer's are the block of dry ground round where he works.
public enum PlotStage { Stubble, Ploughed, Sown, Growing, Ripe, Stooked }
public enum Crop { Wheat, Corn, Potatoes }

public class Plot
{
    public Vector2I Cell;
    public PlotStage Stage;
    public Crop Crop;
    public float Weeds;     // 0..0.6, taken off the yield
    public int Lying;       // the reaped crop still in the field, waiting to be carried in
    public int SownDay;
}

public class Farm
{
    public TownHousehold Home;
    public readonly List<Plot> Plots = new();
}

public static class Fields
{
    private static readonly Dictionary<TownHousehold, Farm> _farms = new();

    // What a plot yields, in the pieces it is carried home in, and what it
    // takes to sow it.
    public static ItemId Yield(Crop c) => c switch { Crop.Wheat => ItemId.Wheat, Crop.Corn => ItemId.Corn, _ => ItemId.Roots };
    public static ItemId Seed(Crop c) => c switch { Crop.Wheat => ItemId.Grain, Crop.Corn => ItemId.Corn, _ => ItemId.Roots };
    public static int SeedPer(Crop c) => c == Crop.Potatoes ? 8 : 4;
    private static int Full(Crop c) => c switch { Crop.Wheat => 100, Crop.Corn => 280, _ => 350 };
    // A sheaf threshes out to two pecks of wheat; a peck grinds to a measure of meal; a measure bakes two loaves.
    public const int GrainPerSheaf = 2, LoavesPerMeal = 2;

    public static bool IsFarm(TownHousehold h)
    {
        if (h == null) return false;
        foreach (var m in h.Members) if (m.Role == Role.Farmer && !m.IsDead) return true;
        return false;
    }

    public static Farm Of(TownHousehold h)
    {
        if (h == null || !IsFarm(h)) return null;
        if (_farms.TryGetValue(h, out var f)) return f;
        f = new Farm { Home = h };
        foreach (var c in PlotCells(h)) f.Plots.Add(new Plot { Cell = c });
        // Wheat on half the ground, corn and potatoes on a quarter each.
        for (int i = 0; i < f.Plots.Count; i++) f.Plots[i].Crop = (i % 4) switch { 0 or 1 => Crop.Wheat, 2 => Crop.Corn, _ => Crop.Potatoes };
        StartOfPlay(f);
        if (_pending.Remove(h.FullKey, out var rows)) Apply(f, rows);
        _farms[h] = f;
        foreach (var p in f.Plots) Paint(p);
        return f;
    }

    private static IEnumerable<Vector2I> PlotCells(TownHousehold h)
    {
        var t = h.Town;
        var door = h.Building.DoorCell;
        var found = new List<Vector2I>();
        // A homestead: the field ground laid out beside the house.
        foreach (var (kind, cell) in t.Props)
            if (kind == "field" && IsoGrid.ToWorld(cell).DistanceTo(IsoGrid.ToWorld(h.Building.Centre)) < 900f) found.Add(cell);
        if (found.Count > 0) return found;
        // A town farmer: the dry ground round where he works.
        Npc farmer = null;
        foreach (var m in h.Members) if (m.Role == Role.Farmer) { farmer = m; break; }
        if (farmer == null) return found;
        var at = farmer.WorkCell;
        for (int i = -2; i <= 2; i++)
            for (int j = -2; j <= 2; j++)
            {
                var c = new Vector2I(at.X + i, at.Y + j);
                if (Sim.Terrain?.IsWaterOrIce(c) ?? true) continue;
                if (Towns.IsStreet(c) || c == door) continue;
                int s = Sim.Structures?.GetCellSourceId(c) ?? -1;
                if (s != -1 && s != WorldLayout.GardenPlot && s != WorldLayout.GardenHarvested) continue;
                found.Add(c);
            }
        return found;
    }

    // Where the year stands when play begins: stubble over the winter, the
    // crop in the ground through the summer, standing ripe at the harvest.
    private static void StartOfPlay(Farm f)
    {
        var cal = GameCalendar.Instance;
        int month = cal?.Month ?? 1;
        foreach (var p in f.Plots)
        {
            p.Stage = month switch
            {
                >= 6 and <= 7 => PlotStage.Growing,
                8 => p.Crop == Crop.Wheat ? PlotStage.Ripe : PlotStage.Growing,
                9 => PlotStage.Ripe,
                5 => PlotStage.Sown,
                _ => PlotStage.Stubble,
            };
            p.Weeds = p.Stage == PlotStage.Growing ? 0.1f : 0f;
        }
    }

    // ---- the year on the ground ------------------------------------------------------

    private static int Today => (int)(GameCalendar.Instance?.DayOfYear ?? 0f);

    // Once a day (Towns.Day): the crops come up, the weeds with them, and a
    // crop left standing into the frosts is lost.
    public static void Day()
    {
        var cal = GameCalendar.Instance;
        if (cal == null) return;
        int month = cal.Month;
        foreach (var f in _farms.Values)
            foreach (var p in f.Plots)
            {
                var was = p.Stage;
                if (p.Stage == PlotStage.Sown && Today - p.SownDay >= 12) p.Stage = PlotStage.Growing;
                else if (p.Stage == PlotStage.Growing)
                {
                    p.Weeds = Mathf.Min(0.6f, p.Weeds + 0.02f);
                    if (month >= (p.Crop == Crop.Wheat ? 8 : 9)) p.Stage = PlotStage.Ripe;
                }
                else if (p.Stage == PlotStage.Ripe && month >= 11) { p.Stage = PlotStage.Stubble; p.Weeds = 0f; Sim.Notice($"{f.Home.Title}: a field of {CropName(p.Crop)} was never reaped and is lost to the frost."); }
                if (p.Stage != was) Paint(p);
            }
        if (Report) PrintReport();
    }

    // Dev: `-- --farmreport` prints each town's fields and grain once a day.
    private static readonly bool Report = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--farmreport") >= 0;
    private static void PrintReport()
    {
        var byTown = new Dictionary<string, int[]>();
        foreach (var f in _farms.Values)
        {
            if (!byTown.TryGetValue(f.Home.Town.Name, out var a)) byTown[f.Home.Town.Name] = a = new int[6 + 6];
            foreach (var p in f.Plots) a[(int)p.Stage]++;
            a[6] += f.Home.Stores.Count(ItemId.Wheat); a[7] += f.Home.Stores.Count(ItemId.Grain); a[8] += f.Home.Stores.Count(ItemId.Corn); a[9] += f.Home.Stores.Count(ItemId.Roots);
        }
        foreach (var (town, a) in byTown)
        {
            var m = Towns.Named(town)?.Market?.Stores;
            if (Towns.Named(town) is { } tt) GD.Print($"[money] {GameCalendar.Instance?.DateString} {town}: {Economy.Money.Report(tt)}");
            GD.Print($"[farm] {GameCalendar.Instance?.DateString} {town}: stubble {a[0]} ploughed {a[1]} sown {a[2]} growing {a[3]} ripe {a[4]} stooked {a[5]} | farms: sheaves {a[6]} wheat {a[7]} corn {a[8]} potatoes {a[9]} | market: bread {m?.Count(ItemId.Bread)} meal {m?.Count(ItemId.Flour)} wheat {m?.Count(ItemId.Grain)} corn {m?.Count(ItemId.Corn)} potatoes {m?.Count(ItemId.Roots)}");
        }
    }

    public static string CropName(Crop c) => c switch { Crop.Wheat => "wheat", Crop.Corn => "corn", _ => "potatoes" };

    // The plot's look: bare earth and stubble, or a crop in the ground.
    private static void Paint(Plot p)
    {
        var s = Sim.Structures;
        if (s == null) return;
        int look = p.Stage is PlotStage.Growing or PlotStage.Ripe ? WorldLayout.GardenPlot : WorldLayout.GardenHarvested;
        if (s.GetCellSourceId(p.Cell) == look) return;
        s.SetCell(p.Cell, look, Vector2I.Zero);
        Net.Net.I?.BroadcastCell(0, p.Cell, look);
    }

    // ---- the steps, done on one plot -------------------------------------------------

    public static void Plough(Plot p) { if (p.Stage == PlotStage.Stubble) { p.Stage = PlotStage.Ploughed; p.Weeds = 0f; Paint(p); } }

    // The seed comes out of the sower's own pack.
    public static bool Sow(Plot p, Npc by)
    {
        if (p.Stage != PlotStage.Ploughed) return false;
        if (!by.Inventory.Remove(Seed(p.Crop), SeedPer(p.Crop))) return false;
        p.Stage = PlotStage.Sown; p.SownDay = Today; Paint(p);
        return true;
    }

    public static void Hoe(Plot p) { if (p.Stage == PlotStage.Growing) p.Weeds = 0f; }

    // Reaped and bound (or picked, or dug): the crop lies in the field until
    // it is carried in.
    public static void Reap(Plot p)
    {
        if (p.Stage != PlotStage.Ripe) return;
        p.Lying = Mathf.Max(1, Mathf.RoundToInt(Full(p.Crop) * (1f - p.Weeds)));
        p.Stage = PlotStage.Stooked; p.Weeds = 0f; Paint(p);
    }

    // As much of what lies in the field as the carrier can take on his back.
    public static int PickUp(Plot p, Npc by)
    {
        if (p.Stage != PlotStage.Stooked || p.Lying <= 0) return 0;
        var id = Yield(p.Crop);
        float room = by.CarryCapacityLbs - by.Inventory.TotalWeightLbs;
        int n = Mathf.Min(p.Lying, Mathf.Max(1, Mathf.FloorToInt(room / Items.Items.Of(id).WeightLbs)));
        by.Inventory.Add(id, n);
        p.Lying -= n;
        if (p.Lying <= 0) { p.Stage = PlotStage.Stubble; Paint(p); }
        return n;
    }

    // What wants doing next on this farm, in the order the year asks it.
    // `from` starts the look somewhere else along the field, so a son working
    // beside his father takes the next plot, not the same one.
    public static (string Step, Plot Plot) Next(Farm f, int from = 0)
    {
        if (f == null) return (null, null);
        int month = GameCalendar.Instance?.Month ?? 1;
        Plot Find(System.Func<Plot, bool> want)
        {
            int n = f.Plots.Count;
            for (int k = 0; k < n; k++) { var p = f.Plots[(k + from) % n]; if (want(p)) return p; }
            return null;
        }
        // The harvest first: a crop lying in the field, then one standing ripe.
        if (Find(p => p.Stage == PlotStage.Stooked) is { } lying) return ("carry", lying);
        if (Find(p => p.Stage == PlotStage.Ripe) is { } ripe) return ("reap", ripe);
        if (month >= 4 && month <= 6)
        {
            if (Find(p => p.Stage == PlotStage.Ploughed) is { } bare)
            {
                if (f.Home.Stores.Count(Seed(bare.Crop)) >= SeedPer(bare.Crop)) return ("sow", bare);
                // Short of seed (the cellar's corn and potatoes are eaten over the
                // winter): buy it at the market, if the market has it.
                if (f.Home.Town?.Market?.Stores.Count(Seed(bare.Crop)) >= SeedPer(bare.Crop)) return ("seed", bare);
            }
            if (month <= 5 && Find(p => p.Stage == PlotStage.Stubble) is { } stubble) return ("plough", stubble);
        }
        if (month >= 6 && month <= 8 && Find(p => p.Stage == PlotStage.Growing && p.Weeds >= 0.12f) is { } weedy) return ("hoe", weedy);
        if (f.Home.Stores.Count(ItemId.Wheat) > 0) return ("thresh", null);
        if (Surplus(f.Home) > 0) return ("sell", null);
        return (null, null);
    }

    // ---- what the farm keeps, and what goes to the market ---------------------------

    // The house keeps its year: a year's food for everyone in it, and seed
    // for every plot. Anything over that is for sale.
    public static int Keep(TownHousehold h) => h.Alive() * 400;
    private static int FoodPieces(TownHousehold h) =>
        h.Stores.Count(ItemId.Bread) + h.Stores.Count(ItemId.Corn) + h.Stores.Count(ItemId.Roots) + h.Stores.Count(ItemId.Grain) + h.Stores.Count(ItemId.Flour) * LoavesPerMeal;
    public static int Surplus(TownHousehold h)
    {
        var f = _farms.GetValueOrDefault(h);
        int seed = 0;
        if (f != null) foreach (var p in f.Plots) seed += SeedPer(p.Crop);
        int month = GameCalendar.Instance?.Month ?? 1;
        // Nothing is sold before the harvest is in.
        if (month < 9 || f != null && f.Plots.Exists(p => p.Stage is PlotStage.Ripe or PlotStage.Stooked)) return 0;
        return Mathf.Max(0, FoodPieces(h) - Keep(h) - seed);
    }

    // The seed a farm keeps back from its own table: enough for every plot
    // not yet sown. Through the winter that is the whole of next spring's.
    public static int SeedKept(TownHousehold h, ItemId seed)
    {
        var f = _farms.GetValueOrDefault(h);
        if (f == null) return 0;
        int n = 0;
        foreach (var p in f.Plots) if (Seed(p.Crop) == seed && p.Stage is PlotStage.Stubble or PlotStage.Ploughed) n += SeedPer(p.Crop);
        return n;
    }

    // The goods a load for market is made of: whatever the market is
    // shortest of goes on the farmer's back first, so a town short of
    // potatoes gets potatoes, not a fourth load of wheat.
    public static readonly ItemId[] Sellable = { ItemId.Grain, ItemId.Corn, ItemId.Roots, ItemId.Flour };
    public static List<ItemId> SellOrder(TownHousehold market)
    {
        var l = new List<ItemId>(Sellable);
        if (market != null) l.Sort((a, b) => market.Stores.Count(a).CompareTo(market.Stores.Count(b)));
        return l;
    }

    // ---- out of sight: the same steps, a day at a time -----------------------------

    // A day of one farmer's work done in a sum (design doc 18.2): as many
    // steps as a man gets through in a day, on the same plots, and what he
    // reaps or carries goes home through his own hands. False when there is
    // no field work in the day (he goes hunting, or does the winter's jobs).
    public static bool WorkDay(Npc farmer, TownHousehold home)
    {
        var f = Of(home);
        if (f == null) return false;
        bool any = false;
        for (int step = 0; step < 4; step++)
        {
            var (what, p) = Next(f);
            if (what == null) break;
            any = true;
            switch (what)
            {
                case "plough": for (int k = 0; k < 2 && p != null; k++) { Plough(p); p = f.Plots.Find(q => q.Stage == PlotStage.Stubble); } break;
                case "sow":
                    for (int k = 0; k < 3 && p != null; k++)
                    {
                        int need = SeedPer(p.Crop);
                        farmer.Inventory.Add(Seed(p.Crop), home.Stores.TakeUpTo(Seed(p.Crop), need));
                        if (!Sow(p, farmer)) { home.Stores.Add(Seed(p.Crop), farmer.Inventory.TakeUpTo(Seed(p.Crop), need)); break; }
                        p = f.Plots.Find(q => q.Stage == PlotStage.Ploughed && home.Stores.Count(Seed(q.Crop)) >= SeedPer(q.Crop));
                    }
                    break;
                case "seed": BuySeed(farmer, f, p); break;
                case "hoe": foreach (var q in f.Plots) if (q.Stage == PlotStage.Growing) Hoe(q); break;
                case "reap": Reap(p); break;
                case "carry":
                    // A day of carrying: three loads from the field to the house.
                    for (int k = 0; k < 3 && p.Lying > 0; k++)
                    {
                        PickUp(p, farmer);
                        home.Stores.Add(Yield(p.Crop), farmer.Inventory.TakeUpTo(Yield(p.Crop), 9999));
                    }
                    break;
                case "thresh": Thresh(home, 30); break;
                case "sell": SellLoad(farmer, home); step = 99; break;
            }
        }
        return any;
    }

    // Seed for every unsown plot of this crop, bought at the market and
    // carried home on the farmer's back, as far as his back allows.
    public static int BuySeed(Npc farmer, Farm f, Plot p)
    {
        var market = f.Home.Town?.Market;
        if (market == null) return 0;
        var id = Seed(p.Crop);
        int want = 0;
        foreach (var q in f.Plots) if (q.Crop == p.Crop && q.Stage is PlotStage.Stubble or PlotStage.Ploughed) want += SeedPer(q.Crop);
        want = Mathf.Max(0, want - f.Home.Stores.Count(id));
        float room = farmer.CarryCapacityLbs - farmer.Inventory.TotalWeightLbs;
        int price = Economy.Money.Price(market, id);
        farmer.Inventory.Add(ItemId.Coin, f.Home.Stores.TakeUpTo(ItemId.Coin, want * price));
        int n = Mathf.Min(market.Stores.Count(id), Mathf.Min(want, Mathf.FloorToInt(room / Items.Items.Of(id).WeightLbs)));
        n = Mathf.Min(n, (farmer.Inventory.Count(ItemId.Coin) + Mathf.Max(0, Economy.Money.CreditLimit(f.Home) - Economy.Money.Owed(f.Home))) / Mathf.Max(1, price));
        if (n > 0 && Economy.Money.Pay(farmer, f.Home, market, n * price)) { market.Stores.Remove(id, n); farmer.Inventory.Add(id, n); }
        else n = 0;
        f.Home.Stores.Add(id, farmer.Inventory.TakeUpTo(id, 9999));
        f.Home.Stores.Add(ItemId.Coin, farmer.Inventory.TakeUpTo(ItemId.Coin, 99999));
        return n;
    }

    // Beating sheaves on the barn floor: n of them to grain.
    public static int Thresh(Economy.Household home, int n)
    {
        int done = home.Stores.TakeUpTo(ItemId.Wheat, n);
        home.Stores.Add(ItemId.Grain, done * GrainPerSheaf);
        return done;
    }

    // One load on the farmer's back from the house to the market, sold for
    // what the merchants will pay; the money, and whatever they could not
    // pay for, carried home. Returns what was sold.
    public static int SellLoad(Npc farmer, TownHousehold home)
    {
        var market = home.Town?.Market;
        if (market == null) return 0;
        int left = Surplus(home), sold = 0;
        foreach (var id in SellOrder(market))
        {
            if (left <= 0) break;
            float room = farmer.CarryCapacityLbs - farmer.Inventory.TotalWeightLbs;
            int fit = Mathf.FloorToInt(room / Items.Items.Of(id).WeightLbs);
            int n = home.Stores.TakeUpTo(id, Mathf.Min(fit, left));
            if (n <= 0) continue;
            farmer.Inventory.Add(id, n);
            sold += Agents.TownLife.SellTo(farmer, market, id, n, home);
            home.Stores.Add(id, farmer.Inventory.TakeUpTo(id, 9999));
            left -= n;
        }
        home.Stores.Add(ItemId.Coin, farmer.Inventory.TakeUpTo(ItemId.Coin, 99999));
        return sold;
    }

    // ---- saves -------------------------------------------------------------------------

    public static void Clear() { _farms.Clear(); _pending.Clear(); }

    public static Godot.Collections.Dictionary Save()
    {
        var d = new Godot.Collections.Dictionary();
        foreach (var (h, f) in _farms)
        {
            var rows = new Godot.Collections.Array();
            foreach (var p in f.Plots) rows.Add($"{(int)p.Stage},{(int)p.Crop},{p.Weeds.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)},{p.Lying},{p.SownDay}");
            d[h.FullKey] = rows;
        }
        return d;
    }

    // A save is read before the farmers are back in their houses, so the
    // rows wait here and each farm takes its own when it is first built.
    private static readonly Dictionary<string, Godot.Collections.Array> _pending = new();
    public static void Restore(Godot.Collections.Dictionary d)
    {
        _farms.Clear(); _pending.Clear();
        foreach (var (k, v) in d) _pending[k.AsString()] = v.AsGodotArray();
    }

    private static void Apply(Farm f, Godot.Collections.Array rows)
    {
        for (int i = 0; i < rows.Count && i < f.Plots.Count; i++)
        {
            var a = rows[i].AsString().Split(',');
            if (a.Length < 5) continue;
            var p = f.Plots[i];
            p.Stage = (PlotStage)int.Parse(a[0]); p.Crop = (Crop)int.Parse(a[1]);
            p.Weeds = float.Parse(a[2], System.Globalization.CultureInfo.InvariantCulture); p.Lying = int.Parse(a[3]); p.SownDay = int.Parse(a[4]);
        }
    }
}
