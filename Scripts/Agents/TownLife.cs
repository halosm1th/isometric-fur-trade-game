using System;
using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents.Goals;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Items;
using IsometricFurTradeGame.Scripts.World;
using static IsometricFurTradeGame.Scripts.Agents.Goals.ChoreGoal;

namespace IsometricFurTradeGame.Scripts.Agents;

// The towns' daily round (World/Towns), as the people in them live it: the
// farmer's year on his fields one step at a time (World/Fields), the woman
// of the house at the market, the quern, the oven, the wash and the garden,
// the merchant at his counter and the smith at his forge, the priest among
// the sick, the garrison on parade and on sentry, the children at their
// chores - and on Sunday morning, the whole town in church.
//
// Everything here moves goods by hand (design doc 12.5): bread bought at the
// market comes home in the buyer's arms, the harvest is carried in from the
// field, the surplus is carried to the market. Out of sight the same work is
// done in a day's sum by the same people (Fields.WorkDay, HouseDay, and
// Towns.Provision through the shopper's own pack).
public static class TownLife
{
    private static GameCalendar Cal => GameCalendar.Instance;
    private static float Hour => Cal?.Hour ?? 12f;
    private static bool WorkHours => Hour >= 7f && Hour < 18f;

    public static DayOfWeek Weekday
    {
        get
        {
            var c = Cal;
            if (c == null) return DayOfWeek.Wednesday;
            int y = Mathf.Clamp(c.Year, 1, 9999), m = Mathf.Clamp(c.Month, 1, 12);
            return new DateTime(y, m, Mathf.Clamp(c.Day, 1, DateTime.DaysInMonth(y, m))).DayOfWeek;
        }
    }

    private static TownBuilding Church(Town t) => t?.Buildings.Find(b => b.Def.Kind == BuildingKind.Church);

    // A stable choice per person: the same pew every Sunday.
    private static int Hash(Npc npc, int salt) => (int)((uint)(npc.NetId * 2654435761u + (uint)salt * 40503u) >> 4);

    // Whatever of the house's goods someone has in their arms goes into the
    // house: what a chore cut off short leaves behind (ChoreGoal.Abandon).
    private static readonly ItemId[] Carried = { ItemId.Bread, ItemId.Flour, ItemId.Grain, ItemId.Wheat, ItemId.Corn, ItemId.Roots, ItemId.Fish, ItemId.Venison, ItemId.DriedMeat, ItemId.Pemmican };
    private static ChoreGoal SetDownAt(this ChoreGoal g, TownHousehold home)
    {
        g.Abandon = n => { foreach (var id in Carried) { int k = n.Inventory.Count(id); if (k > 0) home.Stores.Add(id, n.Inventory.TakeUpTo(id, k)); } };
        return g;
    }

    // A place of one's own in front of a busy door - the market's, say - so a
    // crowd stands as a crowd and not all on the one step.
    private static Vector2I Spot(Npc npc, Vector2I door)
    {
        int h = Hash(npc, 11);
        for (int k = 0; k < 6; k++)
        {
            var c = new Vector2I(door.X + (h >> (k * 3)) % 5 - 2, door.Y + (h >> (k * 3 + 1)) % 7 - 3);
            if (Sim.Paths?.IsWalkable(c) ?? false) return c;
        }
        return door;
    }

    // Dev: `-- --townpaths` walks every town once the world is up: from its
    // market to every house door, and from the garrison's gate to the church,
    // and says what can't be reached on foot.
    public static void CheckPaths()
    {
        foreach (var t in Towns.All)
        {
            if (!t.Standing || t.Market == null) continue;
            var from = t.Market.AccessCell;
            int bad = 0, all = 0; var sample = new List<string>();
            foreach (var h in t.Houses.Values)
            {
                if (h == t.Market) continue;
                all++;
                if ((Sim.Paths?.FindPath(from, h.AccessCell)?.Count ?? 0) == 0) { bad++; if (sample.Count < 4) sample.Add($"{h.Title} {h.AccessCell}"); }
            }
            string fort = "";
            if (t.HasFort && Church(t) is { } ch)
            {
                var gate = IsoGrid.Cell(t.FortTop, t.FortGate, t.FortDepth + 1);
                fort = (Sim.Paths?.FindPath(gate, ch.Def.DoorCell)?.Count ?? 0) == 0 ? $"; {t.FortName} cut off from the church" : $"; {t.FortName} reaches the church";
            }
            GD.Print($"[townpaths] {t.Name}: {all - bad}/{all} houses reachable from the market{fort}{(bad > 0 ? " - e.g. " + string.Join(", ", sample) : "")}");
        }
    }

    // ---- Sunday ------------------------------------------------------------------------

    // Morning service, nine till half past eleven: everyone who can walk is
    // in the church, the priest at the front. A sentry keeps his post.
    public static Goal Sunday(Npc npc, Town town, bool sentry = false)
    {
        if (Weekday != DayOfWeek.Sunday || Hour < 8f || Hour >= 11.5f || sentry) return null;
        // The garrison and its families hold divine service on their own
        // parade, the senior officer reading it.
        if (npc.HomeStores is TownHousehold { Key: "garrison" } && town.HasFort)
        {
            var square = IsoGrid.Cell(town.FortTop, town.FortWidth / 2, town.FortDepth / 2);
            return new ChoreGoal("church parade", Walk("fall in for church parade", square, 220f), Work(npc.Role == Role.Officer ? "reading divine service on the parade" : "at divine service on the parade", 11.5f - Hour));
        }
        var church = Church(town);
        if (church == null) return null;
        // Somewhere to stand or sit that a body can reach: not the pews themselves.
        var seats = new List<Vector2I>();
        foreach (var c in church.Def.Interior()) if (Sim.Paths?.IsWalkable(c) ?? true) seats.Add(c);
        if (seats.Count == 0) seats.Add(church.Def.Threshold);
        if (npc.Role == Role.Priest)
            return new ChoreGoal("at church", Walk("on the way to church", npc.WorkCell, 70f), new("taking the service", _ => new WaitGoal(11.5f - Hour)));
        var seat = seats[Hash(npc, 7) % seats.Count];
        return new ChoreGoal("at church", Walk("on the way to church", seat, 70f), Work("at church", 11.5f - Hour));
    }

    // ---- the farmer ----------------------------------------------------------------------

    public static Goal Farmer(Npc npc, TownHousehold home)
    {
        if (Sunday(npc, home.Town) is { } church) return church;
        if (Weekday == DayOfWeek.Sunday) return Rest(npc, home);
        if (!WorkHours) return null;
        var farm = Fields.Of(home);
        // The farmer starts at the top of his field; a son of the house further along it.
        var (step, plot) = Fields.Next(farm, npc.Role == Role.Farmer ? 0 : 1 + Hash(npc, 3) % Math.Max(1, farm?.Plots.Count ?? 1));
        string crop = plot != null ? Fields.CropName(plot.Crop) : "";
        switch (step)
        {
            case "plough":
                return new ChoreGoal("plough the field",
                    Walk("walk out to the field", plot.Cell),
                    Work("plough the field", 1.5f),
                    Do(_ => Fields.Plough(plot)));
            case "sow":
            {
                var seed = Fields.Seed(plot.Crop); int need = Fields.SeedPer(plot.Crop);
                return new ChoreGoal($"sow the {crop}",
                    Walk($"fetch seed {crop}", home.AccessCell, 100f),
                    Do(n => { int got = home.Stores.TakeUpTo(seed, need); n.Inventory.Add(seed, got); return got == need; }),
                    Walk($"carry the seed {crop} to the field", plot.Cell),
                    Work($"sow the {crop}", 1f),
                    Do(n => Fields.Sow(plot, n))).SetDownAt(home);
            }
            case "seed":
            {
                var market = home.Town.Market;
                var seed = Fields.Seed(plot.Crop);
                return new ChoreGoal($"buy seed {crop}",
                    Walk($"go to market for seed {crop}", Spot(npc, market.AccessCell), 50f),
                    Do(n =>
                    {
                        // As much as the unsown ground wants and he can carry.
                        int want = 0;
                        foreach (var q in farm.Plots) if (q.Crop == plot.Crop && q.Stage is PlotStage.Stubble or PlotStage.Ploughed) want += Fields.SeedPer(q.Crop);
                        want = Mathf.Max(0, want - home.Stores.Count(seed));
                        int k = market.Stores.TakeUpTo(seed, Mathf.Min(want, Mathf.FloorToInt((n.CarryCapacityLbs - n.Inventory.TotalWeightLbs) / Items.Items.Of(seed).WeightLbs)));
                        n.Inventory.Add(seed, k);
                        return k > 0;
                    }),
                    Walk($"carry the seed {crop} home", home.AccessCell, 100f),
                    Do(n => { home.Stores.Add(seed, n.Inventory.TakeUpTo(seed, 9999)); })).SetDownAt(home);
            }
            case "hoe":
                return new ChoreGoal($"hoe the {crop}",
                    Walk("walk out to the field", plot.Cell),
                    Work($"hoe the {crop}", 1.5f),
                    Do(_ => Fields.Hoe(plot)));
            case "reap":
                return new ChoreGoal(Reaping(plot.Crop),
                    Walk("walk out to the field", plot.Cell),
                    Work(Reaping(plot.Crop), 2f),
                    Do(_ => Fields.Reap(plot)));
            case "carry":
            {
                var what = Fields.Yield(plot.Crop);
                return new ChoreGoal($"carry the {crop} in",
                    Walk("walk out to the field", plot.Cell),
                    Do(n => Fields.PickUp(plot, n) > 0),
                    Walk($"carry the {crop} home", home.AccessCell, 100f),
                    Do(n => { home.Stores.Add(what, n.Inventory.TakeUpTo(what, 9999)); })).SetDownAt(home);
            }
            case "thresh":
                return new ChoreGoal("thresh the wheat",
                    Walk("go to the barn floor", home.AccessCell, 100f),
                    Work("thresh the wheat", 2f),
                    Do(_ => { Fields.Thresh(home, 12); }));
            case "sell":
                return ToMarket(npc, home);
        }
        // Between the jobs of the year: the stock, the fences, the wood.
        if (home.Stores.Count(ItemId.Firewood) < (Cal.Month >= 10 || Cal.Month <= 4 ? 30 : 10)) return new ChopWoodGoal();
        return new ChoreGoal("see to the stock and the fences", Walk("walk the fences", npc.WorkCell, 80f), Work("see to the stock and the fences", 1.5f));
    }

    private static string Reaping(Crop c) => c switch { Crop.Wheat => "reap and bind the wheat", Crop.Corn => "pick the corn", _ => "dig the potatoes" };

    // A load of the surplus on his back to the market, and left there.
    private static Goal ToMarket(Npc npc, TownHousehold home)
    {
        var market = home.Town?.Market;
        if (market == null) return null;
        var load = new List<(ItemId, int)>();
        return new ChoreGoal("take the surplus to market",
            Walk("load up for market", home.AccessCell, 100f),
            Do(n =>
            {
                int left = Fields.Surplus(home);
                foreach (var id in Fields.Sellable)
                {
                    if (left <= 0) break;
                    float room = n.CarryCapacityLbs - n.Inventory.TotalWeightLbs;
                    int k = home.Stores.TakeUpTo(id, Mathf.Min(Mathf.FloorToInt(room / Items.Items.Of(id).WeightLbs), left));
                    if (k > 0) { n.Inventory.Add(id, k); load.Add((id, k)); left -= k; }
                }
                return load.Count > 0;
            }),
            Walk("carry the grain to market", Spot(npc, market.AccessCell), 50f),
            Work("selling at the market", 0.4f),
            Do(n => { foreach (var (id, k) in load) market.Stores.Add(id, n.Inventory.TakeUpTo(id, k)); })).SetDownAt(home);
    }

    private static Goal Rest(Npc npc, TownHousehold home) =>
        new ChoreGoal("resting, it being Sunday", Walk("walk home", home.AccessCell, 120f), Work("resting, it being Sunday", 0.5f));

    // ---- the woman of the house ---------------------------------------------------------

    // Who went to market today for each house, so two don't go.
    private static readonly Dictionary<TownHousehold, int> _marketing = new();
    private static readonly Dictionary<Npc, int> _washed = new(), _gardened = new();
    private static int Today => Cal?.Day + (Cal?.Month ?? 0) * 31 + (Cal?.Year ?? 0) * 400 ?? 0;

    public static Goal Woman(Npc npc, TownHousehold home)
    {
        if (Sunday(npc, home.Town) is { } church) return church;
        if (!WorkHours) return null;
        if (Weekday == DayOfWeek.Sunday) return Rest(npc, home);
        int mouths = Mathf.Max(1, home.Alive());
        // Marketing: a house under a week's food sends someone with a basket.
        if (Shopping(npc, home, mouths) is { } shop) return shop;
        // Meal into bread at the oven; wheat into meal at the quern.
        if (home.Stores.Count(ItemId.Flour) > 0 && home.Stores.Count(ItemId.Bread) < mouths * 4)
            return new ChoreGoal("bake bread",
                Walk("go in to the oven", home.AccessCell, 100f),
                Work("bake bread", 2f),
                Do(_ => { int m = home.Stores.TakeUpTo(ItemId.Flour, 6); home.Stores.Add(ItemId.Bread, m * Fields.LoavesPerMeal); if (home.Stores.Has(ItemId.Firewood)) home.Stores.Remove(ItemId.Firewood); }));
        if (Grindable(home) > 0 && home.Stores.Count(ItemId.Flour) < 6)
            return new ChoreGoal("grind meal at the quern",
                Walk("go in to the quern", home.AccessCell, 100f),
                Work("grind meal at the quern", 1.5f),
                Do(_ => { home.Stores.Add(ItemId.Flour, home.Stores.TakeUpTo(ItemId.Grain, Mathf.Min(6, Grindable(home)))); }));
        // Monday is wash day, down at the water.
        if (Weekday == DayOfWeek.Monday && Hour < 14f && _washed.GetValueOrDefault(npc) != Today && !Cal.IsFrozen && npc.NearestWaterCell(4000f) is { } water)
        {
            _washed[npc] = Today;
            return new ChoreGoal("do the washing",
                Walk("carry the washing to the water", water, 40f),
                Work("do the washing at the water", 2.5f),
                Walk("carry the washing home", home.AccessCell, 100f),
                Work("hang out the washing", 0.5f));
        }
        // The kitchen garden, spring to fall; its potatoes dug in September.
        if (Cal.Month >= 5 && Cal.Month <= 9 && _gardened.GetValueOrDefault(npc) != Today && Garden(home) is { } bed)
        {
            _gardened[npc] = Today;
            bool dig = Cal.Month == 9;
            return new ChoreGoal(dig ? "dig the garden" : "weed the garden",
                Walk("go out to the garden", bed, 60f),
                Work(dig ? "dig potatoes in the garden" : "weed the garden", 1.5f),
                Do(n => { if (dig) n.Inventory.Add(ItemId.Roots, 6); }),
                Walk("carry them in", home.AccessCell, 100f),
                Do(n => { home.Stores.Add(ItemId.Roots, n.Inventory.TakeUpTo(ItemId.Roots, 9999)); })).SetDownAt(home);
        }
        // And the endless rest of it: mending, spinning, the children.
        return new ChoreGoal("mend and spin", Walk("go in", home.AccessCell, 100f), Work("mend and spin by the fire", 1.5f));
    }

    // The house's own garden bed: the nearest garden ground to its door.
    private static Vector2I? Garden(TownHousehold home)
    {
        Vector2I? best = null; float d = 700f;
        var door = IsoGrid.ToWorld(home.Building.DoorCell);
        foreach (var (kind, cell) in home.Town.Props)
        {
            if (kind != "garden") continue;
            float e = IsoGrid.ToWorld(cell).DistanceTo(door);
            if (e < d) { d = e; best = cell; }
        }
        return best;
    }

    // Off to the market with a basket and back with the week's bread, if the
    // market has any. One of the house goes, not all of them.
    private static Goal Shopping(Npc npc, TownHousehold home, int mouths)
    {
        var market = home.Town?.Market;
        if (market == null || home == market || home.FoodDays(mouths) >= 7f) return null;
        // Each house has its own hour for the market, so the town isn't all there at once.
        if (Hour < 8f + Hash(npc, 5) % 8) return null;
        if (_marketing.GetValueOrDefault(home) == Today) return null;
        _marketing[home] = Today;
        var got = new List<(ItemId, int)>();
        return new ChoreGoal("go to market",
            Walk("go to market", Spot(npc, market.AccessCell), 50f),
            Do(n => Buy(n, market, mouths * 7, got) > 0),
            Work("haggle at the market", 0.4f),
            Walk("carry the marketing home", home.AccessCell, 100f),
            Do(n => { foreach (var (id, k) in got) home.Stores.Add(id, n.Inventory.TakeUpTo(id, k)); })).SetDownAt(home);
    }

    // What a basket holds from the market: bread first, then meal and wheat
    // to bake, corn and potatoes - as many pieces as the house wants and the
    // buyer can carry. Into the buyer's own pack; `got` says what.
    private static readonly ItemId[] MarketFood = { ItemId.Bread, ItemId.Flour, ItemId.Grain, ItemId.Roots, ItemId.Corn, ItemId.Fish, ItemId.Venison, ItemId.DriedMeat, ItemId.Pemmican };
    public static int Buy(Npc buyer, TownHousehold market, int want, List<(ItemId, int)> got)
    {
        int total = 0;
        foreach (var id in MarketFood)
        {
            if (want <= 0) break;
            float room = buyer.CarryCapacityLbs - buyer.Inventory.TotalWeightLbs;
            int k = market.Stores.TakeUpTo(id, Mathf.Min(want, Mathf.FloorToInt(room / Items.Items.Of(id).WeightLbs)));
            if (k <= 0) continue;
            buyer.Inventory.Add(id, k); got.Add((id, k));
            want -= id == ItemId.Flour ? k * Fields.LoavesPerMeal : k; total += k;
        }
        return total;
    }

    // The same trip out of sight (Core/Tiers, once a day for each person):
    // a house under a week's food sends one of its own, who comes back with
    // what the market had and they could carry - through their own hands.
    public static void Provision(TownHousehold home, Npc shopper)
    {
        var market = home.Town?.Market;
        if (market == null || home == market || shopper.Role is Role.Soldier or Role.Officer || shopper.Role == Role.Child && shopper.Age < 10) return;
        int mouths = Mathf.Max(1, home.Alive());
        if (home.FoodDays(mouths) >= 7f || _marketing.GetValueOrDefault(home) == Today) return;
        _marketing[home] = Today;
        var got = new List<(ItemId, int)>();
        Buy(shopper, market, mouths * 7, got);
        foreach (var (id, k) in got) home.Stores.Add(id, shopper.Inventory.TakeUpTo(id, k));
    }

    // A day of the house's work in a sum, for a woman out of sight: the
    // wheat ground and the bread baked from what the house has.
    public static void HouseDay(Npc npc, TownHousehold home)
    {
        int mouths = Mathf.Max(1, home.Alive());
        if (home.Stores.Count(ItemId.Bread) < mouths * 4) home.Stores.Add(ItemId.Bread, home.Stores.TakeUpTo(ItemId.Flour, 6) * Fields.LoavesPerMeal);
        if (home.Stores.Count(ItemId.Flour) < 6) home.Stores.Add(ItemId.Flour, home.Stores.TakeUpTo(ItemId.Grain, Mathf.Min(6, Grindable(home))));
    }

    // The wheat the house may grind: all of it, less a farm's seed.
    private static int Grindable(TownHousehold home) => Mathf.Max(0, home.Stores.Count(ItemId.Grain) - Fields.SeedKept(home, ItemId.Grain));

    // ---- the men of the town -------------------------------------------------------------

    public static Goal Townsman(Npc npc, TownHousehold home)
    {
        if (Sunday(npc, home.Town) is { } church) return church;
        if (Weekday == DayOfWeek.Sunday) return Rest(npc, home);
        if (!WorkHours) return null;
        switch (npc.Role)
        {
            case Role.Merchant:
                return new ChoreGoal("keep the store", Walk("open the store", npc.WorkCell, 60f), Work("behind the counter", 2f));
            case Role.Artisan:
                return new ChoreGoal("at his trade", Walk("go to the shop", npc.WorkCell, 60f), Work(Trade(npc), 2f));
            case Role.Priest:
                // The sick of the parish first; then the church and the parish's papers.
                foreach (var h in home.Town.Houses.Values)
                    foreach (var m in h.Members)
                        if (!m.IsDead && m.IsSick && m != npc)
                            return new ChoreGoal("visit the sick", Walk($"go to {m.DisplayName}", h.AccessCell, 100f), Work($"sit with {m.DisplayName}, who is sick", 1f));
                return new ChoreGoal("at the church", Walk("go to the church", npc.WorkCell, 60f), Work("at the parish registers", 2f));
            case Role.Officer:
                if (Hour < 9f) return new ChoreGoal("take the morning parade", Walk("go out to the parade", npc.WorkCell, 60f), Work("take the morning parade", 9f - Hour));
                return new ChoreGoal("at the orderly room", Walk("go to the orderly room", npc.WorkCell, 60f), Work("at the orderly room", 2f));
        }
        return null;
    }

    // A tradesman's work, by the name of his shop where it says.
    private static string Trade(Npc npc)
    {
        string shop = (npc.HomeStores as TownHousehold)?.Building.Name.ToLowerInvariant() ?? "";
        if (shop.Contains("smith")) return "at the forge";
        if (shop.Contains("cooper")) return "making barrels";
        if (shop.Contains("tann")) return "at the tan pits";
        if (shop.Contains("baker")) return "at the bake oven";
        if (shop.Contains("carpent") || shop.Contains("joiner")) return "at the bench";
        if (shop.Contains("tailor")) return "sewing";
        if (shop.Contains("shoe") || shop.Contains("cobbler")) return "at the last";
        return "at work in the shop";
    }

    // A town's labourer: a farmer's son in the fields with his father, a
    // merchant's or tradesman's in the family store or shop, and anyone
    // else carting and fetching about the town.
    public static Goal Labourer(Npc npc, TownHousehold home)
    {
        if (Fields.IsFarm(home)) return Farmer(npc, home);
        if (Sunday(npc, home.Town) is { } church) return church;
        if (Weekday == DayOfWeek.Sunday) return Rest(npc, home);
        if (!WorkHours) return null;
        var kind = home.Building.Kind;
        if (kind == BuildingKind.Store) return new ChoreGoal("clerking in the store", Walk("go to the store", npc.WorkCell, 60f), Work("clerking in the store", 2f));
        if (kind == BuildingKind.Workshop) return new ChoreGoal("an apprentice at the trade", Walk("go to the shop", npc.WorkCell, 60f), Work($"{Trade(npc)}, learning the trade", 2f));
        if (home.Stores.Count(ItemId.Firewood) < 30) return new ChopWoodGoal();
        // Carting: a load to somebody's door somewhere in the town, and back.
        var blds = home.Town.Buildings;
        if (blds.Count > 0 && Hash(npc, Cal.Day * 24 + (int)Hour) % 3 == 0)
        {
            var to = blds[Hash(npc, Cal.Day * 31 + (int)Hour) % blds.Count].Def;
            return new ChoreGoal("carting in the town", Walk($"cart a load to {to.Name}", Spot(npc, to.DoorCell), 60f), Work($"unloading at {to.Name}", 0.5f), Walk("back with the empty cart", home.AccessCell, 100f));
        }
        return new ChoreGoal("odd jobs", Walk("look for work", npc.WorkCell, 80f), Work("at odd jobs about the town", 1.5f));
    }

    // ---- the garrison ----------------------------------------------------------------------

    public static Goal Soldier(Npc npc, TownHousehold home, bool sentry, bool onDuty)
    {
        if (Sunday(npc, home.Town, sentry && onDuty) is { } church) return church;
        if (!WorkHours) return null;
        if (Weekday == DayOfWeek.Sunday && !(sentry && onDuty)) return Rest(npc, home);
        // Morning parade: the whole garrison on the square, eight till nine.
        if (Hour >= 8f && Hour < 9f && !(sentry && onDuty)) return new ChoreGoal("on parade", Walk("fall in for parade", npc.WorkCell, 50f), Work("on parade", 9f - Hour));
        if (onDuty) return new ChoreGoal(sentry ? "on sentry" : "at drill", Walk(sentry ? "relieve the sentry" : "fall in for drill", npc.WorkCell, 50f), Work(sentry ? "on sentry" : "at drill", 1f));
        if (home.Stores.Count(ItemId.Firewood) < 30 && Hash(npc, Cal.Day) % 3 == 0) return new ChopWoodGoal();
        return new ChoreGoal("off duty", Walk("back to the barracks", home.AccessCell, 120f), Work("cleaning his musket and kit", 1f));
    }

    // ---- children ------------------------------------------------------------------------------

    public static Goal Child(Npc npc, TownHousehold home)
    {
        if (Sunday(npc, home.Town) is { } church) return church;
        if (!WorkHours) return null;
        // The older ones have chores: the water and the woodbox.
        if (npc.Age >= 8)
        {
            if (home.Stores.Count(ItemId.Water) < 6 && home.Stores.Has(ItemId.Kettle) && Hash(npc, Cal.Day) % 2 == 0) return new FetchWaterGoal();
            if (Hash(npc, (int)Hour) % 3 == 0)
                return new ChoreGoal("fetch wood for the fire", Walk("go to the woodpile", home.WoodpileCell, 80f), Work("fetch wood for the fire", 0.3f), Walk("carry the wood in", home.AccessCell, 100f));
        }
        // And then play, in the street before the house.
        Vector2I street = home.AccessCell;
        foreach (var c in home.Town.Streets) if (IsoGrid.ToWorld(c).DistanceTo(IsoGrid.ToWorld(home.AccessCell)) < 260f) { street = c; break; }
        return new ChoreGoal("play in the street", Walk("run out to play", street, 80f), Work("play in the street", 1f));
    }
}
