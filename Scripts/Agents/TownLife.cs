using System;
using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents.Goals;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Economy;
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
    private static readonly ItemId[] Carried = { ItemId.Coin, ItemId.Bread, ItemId.Flour, ItemId.Grain, ItemId.Wheat, ItemId.Corn, ItemId.Roots, ItemId.Fish, ItemId.Venison, ItemId.DriedMeat, ItemId.Pemmican };
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
        if (Bachelors(home) && Shopping(npc, home, Mathf.Max(1, home.Alive())) is { } basket) return basket;
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
                    Walk("take money from the box", home.AccessCell, 100f),
                    Do(n => { n.Inventory.Add(ItemId.Coin, home.Stores.TakeUpTo(ItemId.Coin, 120)); }),
                    Walk($"go to market for seed {crop}", Spot(npc, market.AccessCell), 50f),
                    Do(n =>
                    {
                        // As much as the unsown ground wants, he can carry and can pay for.
                        int want = 0;
                        foreach (var q in farm.Plots) if (q.Crop == plot.Crop && q.Stage is PlotStage.Stubble or PlotStage.Ploughed) want += Fields.SeedPer(q.Crop);
                        want = Mathf.Max(0, want - home.Stores.Count(seed));
                        int price = Money.Price(market, seed);
                        int k = Mathf.Min(market.Stores.Count(seed), Mathf.Min(want, Mathf.FloorToInt((n.CarryCapacityLbs - n.Inventory.TotalWeightLbs) / Items.Items.Of(seed).WeightLbs)));
                        k = Mathf.Min(k, (n.Inventory.Count(ItemId.Coin) + Mathf.Max(0, Money.CreditLimit(home) - Money.Owed(home))) / Mathf.Max(1, price));
                        if (k <= 0 || !Money.Pay(n, home, market, k * price)) return false;
                        market.Stores.Remove(seed, k);
                        n.Inventory.Add(seed, k);
                        return true;
                    }),
                    Walk($"carry the seed {crop} home", home.AccessCell, 100f),
                    Do(n => { home.Stores.Add(seed, n.Inventory.TakeUpTo(seed, 9999)); SetDown(n, home); })).SetDownAt(home);
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

    // A load of the surplus on his back to the market, sold for what the
    // merchants will pay, and the money - and whatever they could not pay
    // for - carried home again.
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
                foreach (var id in Fields.SellOrder(market))
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
            Do(n => { foreach (var (id, k) in load) SellTo(n, market, id, k, home); }),
            Walk("carry the money home", home.AccessCell, 100f),
            Do(n => SetDown(n, home))).SetDownAt(home);
    }

    private static Goal Rest(Npc npc, TownHousehold home) =>
        new ChoreGoal("resting, it being Sunday", Walk("walk home", home.AccessCell, 120f), Work("resting, it being Sunday", 0.5f));

    // ---- the woman of the house ---------------------------------------------------------

    // Trips to market today for each house: one for a family, a party of
    // several for a garrison - a basket feeds about eight for a week.
    private static readonly Dictionary<TownHousehold, (int day, int trips)> _marketing = new();
    private static bool MayGoToMarket(TownHousehold home, int mouths)
    {
        var (day, trips) = _marketing.GetValueOrDefault(home);
        if (day != Today) trips = 0;
        if (trips >= Mathf.Max(1, mouths / 8)) return false;
        _marketing[home] = (Today, trips + 1);
        return true;
    }
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
        // Money to spare: store goods from the market, once a week.
        if (StoreGoods(npc, home) is { } goods) return goods;
        // Once a week, if there is money in the box, work for a tradesman: the
        // kettle mended, a barrel made, shoes soled - paid at his door.
        if (Errand(npc, home) is { } errand) return errand;
        // Washing or sewing taken in for a better house, paid at its door.
        if (Hour < 12f && TakesInWork(npc, home) is { } employer)
        {
            _hired[npc] = Today;
            return new ChoreGoal($"take in washing for {employer.Building.Name}",
                Walk($"fetch the washing from {employer.Building.Name}", Spot(npc, employer.AccessCell), 60f),
                Walk("carry the washing home", home.AccessCell, 100f),
                Work($"wash and mend for {employer.Building.Name}", 4f),
                Walk($"take the washing back to {employer.Building.Name}", Spot(npc, employer.AccessCell), 60f),
                Do(n => Pay(employer, n, home, WomansWage)),
                Walk("go home", home.AccessCell, 100f),
                Do(n => SetDown(n, home))).SetDownAt(home);
        }
        // And the endless rest of it: mending, spinning, the children.
        return new ChoreGoal("mend and spin", Walk("go in", home.AccessCell, 100f), Work("mend and spin by the fire", 1.5f));
    }

    private static readonly Dictionary<TownHousehold, int> _errandWeek = new(), _goodsWeek = new();

    // What a house keeps by against a lean week: a fortnight's bread, and a
    // few shillings. Anything over it may be spent.
    private static int Reserve(TownHousehold home) => Mathf.Max(1, home.Alive()) * 14 * 5 + 60;
    private static int SpendOnGoods(TownHousehold home)
    {
        var market = home.Town?.Market;
        if (market == null || home == market || Money.Owed(home) > 0) return 0;
        int spare = home.Stores.Count(ItemId.Coin) - Reserve(home);
        int price = Money.Price(market, ItemId.TradeGoods);
        return spare < price ? 0 : Mathf.Min(market.Stores.Count(ItemId.TradeGoods), spare / 2 / price);
    }
    private static Goal StoreGoods(Npc npc, TownHousehold home)
    {
        int week = Today / 7;
        if (_goodsWeek.GetValueOrDefault(home) == week || Hash(npc, Today) % 4 != 0) return null;
        int n = SpendOnGoods(home);
        if (n <= 0) return null;
        _goodsWeek[home] = week;
        var market = home.Town.Market;
        int price = Money.Price(market, ItemId.TradeGoods);
        return new ChoreGoal("buy store goods",
            Walk("take money from the box", home.AccessCell, 100f),
            Do(m => { m.Inventory.Add(ItemId.Coin, home.Stores.TakeUpTo(ItemId.Coin, n * price)); }),
            Walk("go to the store", Spot(npc, market.AccessCell), 50f),
            Work("choosing cloth and tea at the store", 0.5f),
            Do(m => { int k = Mathf.Min(n, Mathf.Min(market.Stores.Count(ItemId.TradeGoods), m.Inventory.Count(ItemId.Coin) / price)); if (k > 0 && Money.Pay(m, null, market, k * price)) { market.Stores.Remove(ItemId.TradeGoods, k); m.Inventory.Add(ItemId.TradeGoods, k); } }),
            Walk("carry the parcels home", home.AccessCell, 100f),
            Do(m => { home.Stores.Add(ItemId.TradeGoods, m.Inventory.TakeUpTo(ItemId.TradeGoods, 9999)); SetDown(m, home); })).SetDownAt(home);
    }

    // Day labour: a town labourer hires out to whoever will pay - the store
    // or a merchant for portering, a tradesman, an officer's or the priest's
    // house, any day; a farm at the harvest, August to October - eighteen
    // pence for the day, paid at the end of it.
    private static TownHousehold Hiring(Npc npc, TownHousehold home)
    {
        var who = new List<TownHousehold>();
        bool harvest = Cal.Month >= 8 && Cal.Month <= 10;
        foreach (var h in home.Town.Houses.Values)
        {
            if (h == home) continue;
            // The store itself: porters paid out of the till over its float, or
            // in store pay when the till is short (Pay). A town without a
            // merchant of its own still has its market, and without it the
            // labourers' money went over the counter for good.
            if (h == home.Town.Market) { if (h.Stores.Count(ItemId.Coin) >= Float(home.Town) + DayWage || Money.PaysInKind(home.Town)) who.Add(h); continue; }
            if (h.Stores.Count(ItemId.Coin) < Reserve(h) + DayWage) continue;
            if (harvest && Fields.IsFarm(h)) who.Add(h);
            // A merchant's porter, a tradesman's labourer, an officer's or
            // the priest's man about the house and yard.
            else foreach (var m in h.Members) if ((m.Role is Role.Merchant or Role.Artisan or Role.Officer or Role.Priest) && !m.IsDead) { who.Add(h); break; }
        }
        return who.Count == 0 ? null : who[Hash(npc, Today) % who.Count];
    }
    public const int DayWage = 18;
    // The day's wage, paid at the end of it into the labourer's pocket - or,
    // from a store whose till is short, written to his house as store pay.
    public static readonly Dictionary<TownHousehold, int> Earned = new();   // dev (--lawtrace)
    private static void Pay(TownHousehold employer, Npc n, TownHousehold home, int wage = DayWage)
    {
        Earned[home] = Earned.GetValueOrDefault(home) + wage;
        if (employer == employer.Town?.Market && employer.Stores.Count(ItemId.Coin) < Float(employer.Town) + wage) { Money.CreditTo(home, wage); return; }
        n.Inventory.Add(ItemId.Coin, employer.Stores.TakeUpTo(ItemId.Coin, wage));
    }
    private static TownHousehold Tradesman(TownHousehold home, Npc npc)
    {
        var shops = new List<TownHousehold>();
        foreach (var h in home.Town.Houses.Values) if (h != home && h.Building.Kind == BuildingKind.Workshop) shops.Add(h);
        return shops.Count == 0 ? null : shops[Hash(npc, Today / 7) % shops.Count];
    }
    private static Goal Errand(Npc npc, TownHousehold home)
    {
        int week = Today / 7;
        if (_errandWeek.GetValueOrDefault(home) == week || home.Stores.Count(ItemId.Coin) < 60 || Hash(npc, Today) % 5 != 0) return null;
        if (Tradesman(home, npc) is not { } shop) return null;
        _errandWeek[home] = week;
        return new ChoreGoal($"go to {shop.Building.Name}",
            Walk("take money from the box", home.AccessCell, 100f),
            Do(n => { n.Inventory.Add(ItemId.Coin, home.Stores.TakeUpTo(ItemId.Coin, 12)); }),
            Walk($"go to {shop.Building.Name}", Spot(npc, shop.AccessCell), 60f),
            Work($"wait while the work is done at {shop.Building.Name}", 0.6f),
            Do(n => { shop.Stores.Add(ItemId.Coin, n.Inventory.TakeUpTo(ItemId.Coin, 12)); }),
            Walk("go home", home.AccessCell, 100f)).SetDownAt(home);
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
        if (!MayGoToMarket(home, mouths)) return null;
        var got = new List<(ItemId, int)>();
        return new ChoreGoal(home.Key == "garrison" ? "draw provisions at the market" : "go to market",
            Walk("take money from the box", home.AccessCell, 100f),
            Do(n => { n.Inventory.Add(ItemId.Coin, home.Stores.TakeUpTo(ItemId.Coin, Purse(home, mouths))); }),
            Walk("go to market", Spot(npc, market.AccessCell), 50f),
            Do(n => { Money.PayDown(home, n, market); return Buy(n, market, mouths * 7, got, home) > 0; }),
            Work("haggle at the market", 0.4f),
            Walk("carry the marketing home", home.AccessCell, 100f),
            Do(n => SetDown(n, home))).SetDownAt(home);
    }

    // What a basket holds from the market: bread first, then meal and wheat
    // to bake, corn and potatoes - as many pieces as the house wants and the
    // buyer can carry. Into the buyer's own pack; `got` says what.
    private static readonly ItemId[] MarketFood = { ItemId.Roots, ItemId.Bread, ItemId.Flour, ItemId.Grain, ItemId.Corn, ItemId.Fish, ItemId.Venison, ItemId.DriedMeat, ItemId.Pemmican };
    // Each lot paid for at the market's price out of the buyer's pocket, or
    // on the house's account while it has credit (Economy/Money).
    public static int Buy(Npc buyer, TownHousehold market, int want, List<(ItemId, int)> got, TownHousehold house = null)
    {
        int total = 0;
        // A third of it potatoes and roots, first: they are what keeps a town
        // clear of the scurvy that a house on bread alone gets.
        int rootsWant = want / 3;
        foreach (var id in MarketFood)
        {
            if (want <= 0) break;
            float room = buyer.CarryCapacityLbs - buyer.Inventory.TotalWeightLbs;
            int cap = id == ItemId.Roots ? Mathf.Max(1, rootsWant) : want;
            int k = Mathf.Min(market.Stores.Count(id), Mathf.Min(cap, Mathf.FloorToInt(room / Items.Items.Of(id).WeightLbs)));
            int price = Money.Price(market, id);
            // As many as can be paid for, cash first and then the book.
            int afford = (buyer.Inventory.Count(ItemId.Coin) + (house == null ? 0 : Mathf.Max(0, Money.CreditLimit(house) - Money.Owed(house)))) / Mathf.Max(1, price);
            k = Mathf.Min(k, afford);
            if (k <= 0 || !Money.Pay(buyer, house, market, k * price)) continue;
            market.Stores.Remove(id, k);
            buyer.Inventory.Add(id, k); got.Add((id, k));
            want -= id == ItemId.Flour ? k * Fields.LoavesPerMeal : k; total += k;
            if (id == ItemId.Roots) rootsWant = 0;
        }
        return total;
    }

    // What the house sends to market with: enough for a week's food at
    // ordinary prices, and what it owes the store.
    private static int Purse(TownHousehold home, int mouths) => mouths * 7 * 5 + Money.Owed(home);

    // Selling k of a thing to the merchants: paid in coin as far as the till
    // goes, and the rest in store pay to the seller's house (Economy/Money).
    public static int SellTo(Npc seller, TownHousehold market, ItemId id, int k, TownHousehold house = null)
    {
        int price = Money.BuyPrice(market, id);
        k = Mathf.Min(k, seller.Inventory.Count(id));
        if (house == null) k = Mathf.Min(k, market.Stores.Count(ItemId.Coin) / Mathf.Max(1, price));
        if (k <= 0) return 0;
        market.Stores.Add(id, seller.Inventory.TakeUpTo(id, k));
        int coin = market.Stores.TakeUpTo(ItemId.Coin, k * price);
        seller.Inventory.Add(ItemId.Coin, coin);
        Money.CreditTo(house, k * price - coin);
        return k;
    }

    // Everything of the house's in someone's arms, put away in the house.
    private static void SetDown(Npc n, TownHousehold home)
    {
        foreach (var id in Carried) { int k = n.Inventory.Count(id); if (k > 0) home.Stores.Add(id, n.Inventory.TakeUpTo(id, k)); }
    }

    // The same trip out of sight (Core/Tiers, once a day for each person):
    // a house under a week's food sends one of its own, who comes back with
    // what the market had and they could carry - through their own hands.
    public static void Provision(TownHousehold home, Npc shopper)
    {
        // A house of men grinds and bakes for itself, or its meal is no use to it.
        if (Bachelors(home)) Bake(home);
        var market = home.Town?.Market;
        if (market == null || home == market || shopper.Role == Role.Officer && !OnHisOwn(shopper, home) || shopper.Role == Role.Child && shopper.Age < 10) return;
        int mouths = Mathf.Max(1, home.Alive());
        if (home.FoodDays(mouths) >= 7f || !MayGoToMarket(home, mouths)) return;
        var got = new List<(ItemId, int)>();
        shopper.Inventory.Add(ItemId.Coin, home.Stores.TakeUpTo(ItemId.Coin, Purse(home, mouths)));
        Money.PayDown(home, shopper, market);
        Buy(shopper, market, mouths * 7, got, home);
        SetDown(shopper, home);
    }

    // A day of the house's work in a sum, for a woman out of sight: the
    // wheat ground and the bread baked from what the house has.
    public static void HouseDay(Npc npc, TownHousehold home)
    {
        Bake(home);
        int week = Today / 7;
        // Store goods, if there is money to spare, through her own pocket.
        if (_goodsWeek.GetValueOrDefault(home) != week && SpendOnGoods(home) is var g and > 0 && home.Town.Market is { } mk)
        {
            _goodsWeek[home] = week;
            int price = Money.Price(mk, ItemId.TradeGoods);
            npc.Inventory.Add(ItemId.Coin, home.Stores.TakeUpTo(ItemId.Coin, g * price));
            if (Money.Pay(npc, null, mk, g * price)) { mk.Stores.Remove(ItemId.TradeGoods, g); npc.Inventory.Add(ItemId.TradeGoods, g); }
            home.Stores.Add(ItemId.TradeGoods, npc.Inventory.TakeUpTo(ItemId.TradeGoods, 9999));
            SetDown(npc, home);
        }
        // The week's errand to a tradesman, through her own pocket.
        if (_errandWeek.GetValueOrDefault(home) != week && home.Stores.Count(ItemId.Coin) >= 60 && Tradesman(home, npc) is { } shop)
        {
            _errandWeek[home] = week;
            npc.Inventory.Add(ItemId.Coin, home.Stores.TakeUpTo(ItemId.Coin, 12));
            shop.Stores.Add(ItemId.Coin, npc.Inventory.TakeUpTo(ItemId.Coin, 12));
        }
        // Washing and sewing taken in, in a house that lives by its wages.
        if (TakesInWork(npc, home) is { } employer)
        {
            _hired[npc] = Today;
            Pay(employer, npc, home, WomansWage);
            SetDown(npc, home);
        }
    }

    // A woman of a labourer's house - his wife, a grown daughter - took in
    // washing and sewing, or went out by the day to a better house: about
    // half a man's wage. Without it a family of seven lived on one man's
    // eighteenpence and went into debt every winter.
    public const int WomansWage = 8;
    private static TownHousehold TakesInWork(Npc npc, TownHousehold home)
    {
        if (Weekday == DayOfWeek.Sunday || _hired.GetValueOrDefault(npc) == Today || npc.Role is not (Role.Woman or Role.Wife)) return null;
        if (home == home.Town?.Market || Fields.IsFarm(home) || home.Building.Kind is BuildingKind.Store or BuildingKind.Workshop) return null;
        foreach (var m in home.Members) if (m.Role is Role.Soldier or Role.Officer) return null;
        return Hiring(npc, home);
    }

    // The wheat ground and the bread baked, in the day's sum.
    private static void Bake(TownHousehold home)
    {
        int mouths = Mathf.Max(1, home.Alive());
        if (home.Stores.Count(ItemId.Bread) < mouths * 4) home.Stores.Add(ItemId.Bread, home.Stores.TakeUpTo(ItemId.Flour, 6) * Fields.LoavesPerMeal);
        if (home.Stores.Count(ItemId.Flour) < 6) home.Stores.Add(ItemId.Flour, home.Stores.TakeUpTo(ItemId.Grain, Mathf.Min(6, Grindable(home))));
    }

    // The wheat the house may grind: all of it, less a farm's seed.
    private static int Grindable(TownHousehold home) => Mathf.Max(0, home.Stores.Count(ItemId.Grain) - Fields.SeedKept(home, ItemId.Grain));

    // ---- the men of the town -------------------------------------------------------------

    // Nobody else in the house who goes to market: the merchants, tradesmen,
    // clergy and officers leave it to the women and the servants.
    private static bool OnHisOwn(Npc npc, TownHousehold home)
    {
        foreach (var m in home.Members)
            if (m != npc && GodotObject.IsInstanceValid(m) && !m.IsDead && !(m.Role is Role.Merchant or Role.Artisan or Role.Priest or Role.Officer) && !(m.Role == Role.Child && m.Age < 10)) return false;
        return true;
    }

    public static Goal Townsman(Npc npc, TownHousehold home)
    {
        if (Sunday(npc, home.Town) is { } church) return church;
        if (Weekday == DayOfWeek.Sunday) return Rest(npc, home);
        if (!WorkHours) return null;
        // A man with nobody at home to send buys his own bread.
        if (OnHisOwn(npc, home) && Shopping(npc, home, Mathf.Max(1, home.Alive())) is { } basket) return basket;
        switch (npc.Role)
        {
            case Role.Merchant:
                // First thing, money for a till that has run low; at the end of
                // the day, his share of what is over carried home.
                if (Hour < 10f && home.Town.Market is { } low && Stake(npc, home) is var stake and > 0 && _takings.GetValueOrDefault(npc) != -Today)
                {
                    _takings[npc] = -Today;
                    return new ChoreGoal("take money to the till",
                        Walk("take money from the box", home.AccessCell, 100f),
                        Do(n => { n.Inventory.Add(ItemId.Coin, home.Stores.TakeUpTo(ItemId.Coin, stake)); }),
                        Walk("take money to the market house", Spot(npc, low.AccessCell), 50f),
                        Do(n => { low.Stores.Add(ItemId.Coin, n.Inventory.TakeUpTo(ItemId.Coin, stake)); })).SetDownAt(home);
                }
                // At the end of the day, his share of the market's till carried home.
                if (Hour >= 16.5f && _takings.GetValueOrDefault(npc) != Today && home.Town.Market is { } till)
                {
                    _takings[npc] = Today;
                    return new ChoreGoal("carry the day's takings home",
                        Walk("go to the market house", Spot(npc, till.AccessCell), 50f),
                        Work("count the till", 0.3f),
                        Do(n => { n.Inventory.Add(ItemId.Coin, till.Stores.TakeUpTo(ItemId.Coin, Takings(n, home))); }),
                        Walk("carry the takings home", home.AccessCell, 100f),
                        Do(n => SetDown(n, home))).SetDownAt(home);
                }
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

    // The merchants own the market's stock between them: each takes home his
    // share of whatever the till holds over its float, once a day.
    private static readonly Dictionary<Npc, int> _takings = new();
    private static int Takings(Npc merchant, TownHousehold home)
    {
        var market = home.Town.Market;
        int merchants = 0;
        foreach (var h in home.Town.Houses.Values) foreach (var m in h.Members) if (m.Role == Role.Merchant && !m.IsDead) merchants++;
        int over = market.Stores.Count(ItemId.Coin) - Float(home.Town);
        return over <= 0 ? 0 : over / 4 / Mathf.Max(1, merchants);
    }

    // A till below half its float: the merchants make it up out of their own
    // boxes, each his share, so they can go on buying.
    private static int Float(Town t) => 400 + t.Houses.Count * 20;
    private static int Stake(Npc merchant, TownHousehold home)
    {
        int merchants = 0;
        foreach (var h in home.Town.Houses.Values) foreach (var m in h.Members) if (m.Role == Role.Merchant && !m.IsDead) merchants++;
        int short_ = Float(home.Town) - home.Town.Market.Stores.Count(ItemId.Coin);
        return short_ <= 0 ? 0 : Mathf.Min(home.Stores.Count(ItemId.Coin) - 120, short_ / Mathf.Max(1, merchants));
    }

    private static readonly Dictionary<Npc, int> _hired = new();

    // A town labourer's day out of sight: hired at the harvest if a farm
    // will pay, the wage carried home in his own pocket.
    public static void LabourerDay(Npc npc, TownHousehold home)
    {
        if (Fields.IsFarm(home) || home.Building.Kind is BuildingKind.Store or BuildingKind.Workshop || Weekday == DayOfWeek.Sunday) return;
        if (Hiring(npc, home) is not { } farm || _hired.GetValueOrDefault(npc) == Today) return;
        _hired[npc] = Today;
        Pay(farm, npc, home);
        SetDown(npc, home);
    }

    // Out of sight: the same walks, in the day's sum.
    public static void MerchantDay(Npc merchant, TownHousehold home)
    {
        if (home.Town.Market is not { } till || _takings.GetValueOrDefault(merchant) == Today) return;
        _takings[merchant] = Today;
        if (Stake(merchant, home) is var stake and > 0) { merchant.Inventory.Add(ItemId.Coin, home.Stores.TakeUpTo(ItemId.Coin, stake)); till.Stores.Add(ItemId.Coin, merchant.Inventory.TakeUpTo(ItemId.Coin, stake)); }
        merchant.Inventory.Add(ItemId.Coin, till.Stores.TakeUpTo(ItemId.Coin, Takings(merchant, home)));
        SetDown(merchant, home);
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

    // A house of men with no woman in it: one of them takes the basket to
    // market himself, or nobody does.
    private static bool Bachelors(TownHousehold home)
    {
        foreach (var m in home.Members) if (GodotObject.IsInstanceValid(m) && !m.IsDead && m.Role is Role.Woman or Role.Wife) return false;
        return true;
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
        if (Bachelors(home) && Shopping(npc, home, Mathf.Max(1, home.Alive())) is { } basket) return basket;
        var kind = home.Building.Kind;
        if (kind == BuildingKind.Store) return new ChoreGoal("clerking in the store", Walk("go to the store", npc.WorkCell, 60f), Work("clerking in the store", 2f));
        if (kind == BuildingKind.Workshop) return new ChoreGoal("an apprentice at the trade", Walk("go to the shop", npc.WorkCell, 60f), Work($"{Trade(npc)}, learning the trade", 2f));
        if (home.Stores.Count(ItemId.Firewood) < 30) return new ChopWoodGoal();
        if (Hour < 9f && Hiring(npc, home) is { } farm && _hired.GetValueOrDefault(npc) != Today)
        {
            _hired[npc] = Today;
            bool field = Fields.IsFarm(farm);
            var plot = field ? Fields.Of(farm)?.Plots.Find(p => p.Stage is PlotStage.Ripe or PlotStage.Stooked) : null;
            return new ChoreGoal(field ? $"hired for the harvest at {farm.Building.Name}" : $"portering at {farm.Building.Name}",
                Walk($"go to {farm.Building.Name} for a day's work", plot?.Cell ?? Spot(npc, farm.AccessCell), 80f),
                Work(field ? "a hired hand at the harvest" : $"carrying goods at {farm.Building.Name}", 6f),
                Do(n => Pay(farm, n, home)),
                Walk("walk home with the day's wage", home.AccessCell, 100f),
                Do(n => SetDown(n, home))).SetDownAt(home);
        }
        // Carting: a load to somebody's door somewhere in the town, and back.
        var blds = home.Town.Buildings;
        if (blds.Count > 0 && Hash(npc, Cal.Day * 24 + (int)Hour) % 3 == 0)
        {
            var b = blds[Hash(npc, Cal.Day * 31 + (int)Hour) % blds.Count];
            var to = b.Def;
            // Whoever the load is for pays the carter fourpence at the door.
            var payer = Towns.House(home.Town.Name + "|" + b.Home.Split(';')[0]);
            return new ChoreGoal("carting in the town",
                Walk($"cart a load to {to.Name}", Spot(npc, to.DoorCell), 60f),
                Work($"unloading at {to.Name}", 0.5f),
                Do(n => { if (payer != null && payer != home) n.Inventory.Add(ItemId.Coin, payer.Stores.TakeUpTo(ItemId.Coin, 4)); }),
                Walk("back with the empty cart", home.AccessCell, 100f),
                Do(n => SetDown(n, home))).SetDownAt(home);
        }
        return new ChoreGoal("odd jobs", Walk("look for work", npc.WorkCell, 80f), Work("at odd jobs about the town", 1.5f));
    }

    // ---- the garrison ----------------------------------------------------------------------

    public static Goal Soldier(Npc npc, TownHousehold home, bool sentry, bool onDuty)
    {
        if (Sunday(npc, home.Town, sentry && onDuty) is { } church) return church;
        // The evening, off duty: a few pence of his pay spent in town.
        if (Hour >= 16f && Hour < 21f && !(sentry && onDuty) && Liberty(npc, home) is { } evening) return evening;
        if (!WorkHours) return null;
        if (Weekday == DayOfWeek.Sunday && !(sentry && onDuty)) return Rest(npc, home);
        // Morning parade: the whole garrison on the square, eight till nine.
        if (Hour >= 8f && Hour < 9f && !(sentry && onDuty)) return new ChoreGoal("on parade", Walk("fall in for parade", npc.WorkCell, 50f), Work("on parade", 9f - Hour));
        if (onDuty) return new ChoreGoal(sentry ? "on sentry" : "at drill", Walk(sentry ? "relieve the sentry" : "fall in for drill", npc.WorkCell, 50f), Work(sentry ? "on sentry" : "at drill", 1f));
        if (Shopping(npc, home, Mathf.Max(1, home.Alive())) is { } party) return party;
        if (home.Stores.Count(ItemId.Firewood) < 30 && Hash(npc, Cal.Day) % 3 == 0) return new ChopWoodGoal();
        return new ChoreGoal("off duty", Walk("back to the barracks", home.AccessCell, 120f), Work("cleaning his musket and kit", 1f));
    }

    // A soldier's pay went over the counter (design doc 12.5's coin, carried):
    // once a day, about one man in two takes ninepence of his pay from the
    // barracks chest and buys a dram, or ale, at the market, drinks it there,
    // and brings back what he didn't spend. Without it the garrison's pay piled
    // up in the barracks and never reached the town.
    public const int Spending = 9;
    private static readonly Dictionary<Npc, int> _liberty = new();
    private static bool HasLiberty(Npc npc, TownHousehold home) =>
        _liberty.GetValueOrDefault(npc) != Today && Hash(npc, Today) % 2 == 0 && home.Stores.Count(ItemId.Coin) >= Spending && home.Town?.Market != null;

    private static Goal Liberty(Npc npc, TownHousehold home)
    {
        if (!HasLiberty(npc, home)) return null;
        _liberty[npc] = Today;
        var market = home.Town.Market;
        return new ChoreGoal("an evening in town",
            Walk("draw his pay from the chest", home.AccessCell, 120f),
            Do(n => { n.Inventory.Add(ItemId.Coin, home.Stores.TakeUpTo(ItemId.Coin, Spending)); }),
            Walk("go into town", Spot(npc, market.AccessCell), 60f),
            Do(n => { Drink(n, market); }),
            Work("drinking in town", 1.5f),
            Walk("back to the barracks", home.AccessCell, 120f),
            Do(n => SetDown(n, home))).SetDownAt(home);
    }

    // Rum if he can run to it, ale if not, at the market's price; drunk where he stands.
    private static void Drink(Npc n, TownHousehold market)
    {
        foreach (var id in new[] { ItemId.Rum, ItemId.Ale })
        {
            int price = Money.Price(market, id);
            if (!market.Stores.Has(id) || n.Inventory.Count(ItemId.Coin) < price) continue;
            n.Inventory.Remove(ItemId.Coin, price); market.Stores.Add(ItemId.Coin, price);
            market.Stores.Remove(id); n.Inventory.Add(id); n.Consume(id);
            if (n.Inventory.Count(ItemId.Coin) < Money.Price(market, ItemId.Ale)) break;
        }
    }

    // Out of sight: the same evening, in the day's sum.
    public static void SoldierDay(Npc npc, TownHousehold home)
    {
        if (!HasLiberty(npc, home)) return;
        _liberty[npc] = Today;
        npc.Inventory.Add(ItemId.Coin, home.Stores.TakeUpTo(ItemId.Coin, Spending));
        Drink(npc, home.Town.Market);
        npc.Intoxication = 0f;   // slept off by morning
        SetDown(npc, home);
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
