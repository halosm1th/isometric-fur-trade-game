using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Items;
using IsometricFurTradeGame.Scripts.World;

namespace IsometricFurTradeGame.Scripts.Economy;

// The standard of trade (design doc 13.2, 13.3): what a post gives for furs,
// in goods, at the price the Company set on each - a gun twelve made beaver,
// a blanket four, a kettle two, powder and ball one, an awl a tenth. The
// goods are the trade, not a currency standing in for it: a camp comes in
// wanting particular things, and a post that has the furs' worth on its
// shelves in the wrong things does not get the furs.
//
// What a people wants follows how they live. The plains want guns and powder
// for the buffalo and their wars, kettles, knives, awls for the hides,
// tobacco, beads and cloth; the people of the woods want traps, axes, ice
// chisels and nets as much as guns, and blankets for the winter. A camp
// keeps what it has until it wears out (Wear), so it comes back for more.
//
// Each week a camp with furs to sell sends men in to the house it believes
// will answer it best (Week, TradeTrips); what it believes it learned by
// trading there and from the camps it meets.
public static class StandardOfTrade
{
    public const float ReachPx = 16000f;   // a few days' travel to a post

    // What a good costs at a post: the standard, shaded by how well the post
    // pays for furs (a house that pays more for a pelt asks less for its goods).
    public static float Price(FortLedger post, ItemId id) => Items.Items.Of(id).ValueMb * Carriage(post, id) / Mathf.Clamp(post.PeltPrice, 0.6f, 1.6f);

    // Goods cost more the further they have been carried: the Nor'Westers'
    // prices in the Athabasca were half again and more what they were at the
    // lakes; the Bay's standard of trade was set in London and rose far less
    // inland. Distance from where the company's goods come onto the map
    // (Economy/Outfits), and by the pound: a kettle rises more than beads.
    private static readonly Dictionary<(FortLedger, ItemId), float> _carriage = new();
    public static float Carriage(FortLedger post, ItemId id)
    {
        if (_carriage.TryGetValue((post, id), out float f)) return f;
        var at = World.IsoGrid.ToWorld(post.AccessCell);
        string[] entries = post.Company switch
        {
            Agents.Faction.NWC => new[] { Outfits.Entry },
            Agents.Faction.American => new[] { "Fort Astoria", "Fort Manuel Lisa" },
            _ => new[] { "York Factory" },
        };
        float km = float.MaxValue;
        foreach (var e in entries)
        {
            var where = e == "York Factory" ? World.WorldLayout.FortCentre : World.Posts.Named(e)?.Centre;
            if (where is Vector2 w) km = Mathf.Min(km, w.DistanceTo(at) / World.WorldMap.PxPerKm);
        }
        if (km == float.MaxValue) km = 0f;
        float perThousand = post.Company switch { Agents.Faction.NWC => 0.28f, Agents.Faction.American => 0.22f, _ => 0.1f };
        var d = Items.Items.Of(id);
        float heavy = Mathf.Clamp(0.5f + d.WeightLbs / Mathf.Max(0.1f, d.ValueMb) * 0.6f, 0.6f, 1.6f);   // pounds carried for each beaver of worth
        f = 1f + km / 1000f * perThousand * heavy;
        _carriage[(post, id)] = f;
        return f;
    }

    // The furs a camp has to sell: pelts, and the robes beyond one a head kept for its own beds.
    public static List<(ItemId, int)> FursFor(BandLedger b)
    {
        var lots = new List<(ItemId, int)>();
        foreach (var id in new[] { ItemId.BeaverPelt, ItemId.WolfPelt, ItemId.ThinPelt, ItemId.ShotPelt, ItemId.CaribouHide, ItemId.BuffaloRobe })
        {
            int n = b.Stores.Count(id) - (id == ItemId.BuffaloRobe ? b.Mouths : 0);
            if (n > 0) lots.Add((id, n));
        }
        return lots;
    }

    // The provisions a camp can spare: what is beyond two months of its own keep.
    public static List<(ItemId, int)> ProvisionsFor(BandLedger b)
    {
        var lots = new List<(ItemId, int)>();
        float keep = Mathf.Max(1, b.Mouths) * 60f;   // day-rations
        float have = b.Stores.FoodNutritionTotal / 34f;
        float spare = have - keep;
        if (spare <= 0f) return lots;
        foreach (var id in new[] { ItemId.Pemmican, ItemId.DriedMeat, ItemId.Corn })
        {
            float per = Items.Items.Of(id).Nutrition / 34f;
            int n = Mathf.Min(b.Stores.Count(id), Mathf.FloorToInt(spare / per));
            if (n <= 0) continue;
            lots.Add((id, n));
            spare -= n * per;
            if (spare <= 0f) break;
        }
        return lots;
    }

    public static float Worth(Inventory inv)
    {
        float mb = 0f;
        foreach (var (id, n) in inv.All()) mb += n * Items.Items.Of(id).ValueMb;
        return mb;
    }

    // How much of what this camp needs a post's shelves hold, in made beaver,
    // the pressing needs counting for more.
    private static float Covers(FortLedger post, BandLedger b)
    {
        float mb = 0f;
        foreach (var (id, shortBy, weight) in Wants.Deficits(b))
            mb += Mathf.Min(shortBy, post.Stores.Count(id)) * Items.Items.Of(id).ValueMb * (0.5f + weight);
        return mb;
    }

    // ---- what a camp believes is on a post's shelves (design doc 2, 10) ----
    //
    // Nobody reads another house's stores from a distance. A camp knows what
    // it found the last time it traded at a post, and what the camps it meets
    // say they found; a belief goes stale, back toward "it is a trading house,
    // it will have something", as the months pass and another outfit may have
    // come up. So a post that has run out of powder keeps its country until
    // word of it gets round, and wins it back the same way.
    private const float Unknown = 20f;   // what anyone expects of a house nobody has spoken of
    private static readonly Dictionary<BandLedger, Dictionary<TradingPostLedger, (int Day, float Covers, bool Food)>> _beliefs = new();

    private static readonly Dictionary<BandLedger, int> _lastTrip = new();
    public static void ResetBeliefs() { _beliefs.Clear(); _lastTrip.Clear(); }

    private static float Believed(BandLedger b, TradingPostLedger post)
    {
        if (!_beliefs.TryGetValue(b, out var m) || !m.TryGetValue(post, out var e)) return Unknown;
        float age = Plains.Today - e.Day;
        return Mathf.Lerp(e.Covers, Unknown, Mathf.Clamp(age / 150f, 0f, 1f));
    }

    private static void Learn(BandLedger b, TradingPostLedger post, int day, float covers, bool food)
    {
        if (!_beliefs.TryGetValue(b, out var m)) _beliefs[b] = m = new();
        if (!m.TryGetValue(post, out var e) || e.Day <= day) m[post] = (day, covers, food);
    }

    // Does the camp believe the house is buying provisions? What it found
    // there last; or, if it has never been, what everyone knows - the houses
    // on the brigade roads buy pemmican.
    private static bool BelievesBuysFood(BandLedger b, TradingPostLedger post)
    {
        if (_beliefs.TryGetValue(b, out var m) && m.TryGetValue(post, out var e) && Plains.Today - e.Day < 200) return e.Food;
        return Outfits.IsDepot(post.Post.Name);
    }

    // Word gets round: each week a camp tells a near neighbour what it last saw at the houses.
    private static void Gossip()
    {
        foreach (var b in new List<BandLedger>(_beliefs.Keys))
        {
            BandLedger other = null; float bd = 8000f;
            foreach (var c in Camps.All)
            {
                if (c.Ledger == null || c.Ledger == b || GD.Randf() > 0.3f) continue;
                float d = IsoGrid.ToWorld(c.Ledger.Camp).DistanceTo(IsoGrid.ToWorld(b.Camp));
                if (d < bd) { bd = d; other = c.Ledger; }
            }
            if (other == null) continue;
            // A telling is a few days older than the seeing, and a little blurred.
            foreach (var (post, e) in new List<KeyValuePair<TradingPostLedger, (int Day, float Covers, bool Food)>>(_beliefs[b]))
                Learn(other, post, e.Day - 3, e.Covers * (float)GD.RandRange(0.8, 1.2), e.Food);
        }
    }

    // One week of the country's trade: each camp with furs to sell sends men
    // in to the house it believes will answer it best (a family to the post
    // it lives by); the trip itself is TradeTrips.
    public static void Week(GameCalendar cal)
    {
        // The furs come in through the winter and at the spring's end; the robes in the winter.
        bool season = cal.Month is <= 5 or >= 10;
        foreach (var camp in Camps.All)
        {
            var b = camp.Ledger;
            if (b == null || Gardens.IsTown(b)) continue;
            Wants.Week(b);
            if (TradeTrips.Away(b)) continue;
            // A camp goes in a few times a season - in the fall for its
            // winter's debt, at midwinter, and with the spring hunt - not every week.
            if (_lastTrip.TryGetValue(b, out int last) && Plains.Today - last < 35) continue;
            float furs = 0f; foreach (var (id, n) in FursFor(b)) furs += n * Items.Items.Of(id).ValueMb;
            float food = 0f; foreach (var (id, n) in ProvisionsFor(b)) food += n * Items.Items.Of(id).ValueMb;
            // Furs in their season, worth a trip for a few packs; provisions
            // whenever there is a surplus and a house the camp believes is buying.
            bool forFurs = season && furs >= Mathf.Max(15f, b.Mouths * 2f);
            bool forFood = food >= 12f;
            if (!forFurs && !forFood) continue;
            var post = camp.IsFamily && Posts.Named(camp.Post)?.Ledger is { } own ? own : ChoosePost(b, !forFurs);
            if (post != null && (forFurs || BelievesBuysFood(b, post)) && TradeTrips.Start(b, post, cal)) _lastTrip[b] = Plains.Today;
        }
        Gossip();
    }

    // A camp's choice of house: the one it believes answers its wants best
    // among those within reach, the nearer favoured.
    public static TradingPostLedger ChoosePost(BandLedger b, bool forFood = false)
    {
        TradingPostLedger best = null; float bestScore = 0f;
        var at = IsoGrid.ToWorld(b.Camp);
        foreach (var p in Posts.Standing())
        {
            if (p.Ledger == null) continue;
            float d = IsoGrid.ToWorld(p.Ledger.AccessCell).DistanceTo(at);
            if (d > ReachPx) continue;
            float score = (1f + Believed(b, p.Ledger)) * (1f - 0.5f * d / ReachPx) * Safety(b, p.Ledger);
            if (forFood) score *= BelievesBuysFood(b, p.Ledger) ? 3f : 0.1f;
            if (score > bestScore) { bestScore = score; best = p.Ledger; }
        }
        return best;   // null: no house it can safely reach
    }

    // Nobody carries their furs into their enemies' country (design doc 7.2):
    // a house whose own country - the camps nearest it - is a people this
    // camp is at war with is a house it does not go to; with nowhere else,
    // it keeps its furs. A truce opens it.
    private static float Safety(BandLedger b, FortLedger post)
    {
        var at = IsoGrid.ToWorld(post.AccessCell);
        BandLedger owner = null; float bd = 7000f;
        foreach (var c in Camps.All)
        {
            if (c.Ledger == null || c.Ledger == b) continue;
            float d = IsoGrid.ToWorld(c.Ledger.Camp).DistanceTo(at);
            if (d < bd) { bd = d; owner = c.Ledger; }
        }
        if (owner == null || owner.Nation == b.Nation || !Plains.Enemies(b.Nation, owner.Nation) || War.AtPeace(b, owner)) return 1f;
        return 0f;   // they keep their furs rather than take them there
    }

    // The exchange, made on the day the party is at the post: their furs at
    // the standard, paid in what the camp wants first, then in sundries (the
    // "trade goods" bale: thread, buttons, vermilion, needles) for no more
    // than a third. What is not paid for goes home with them. They see the
    // shelves, and remember them.
    public static void Trade(TradingPostLedger post, BandLedger b, Inventory load, Inventory into, GameCalendar cal)
    {
        var counts = new Dictionary<ItemId, int>();
        var shortOf = new List<string>();
        // The furs first: what the post is there for.
        float owed = 0f;
        foreach (var (id, n) in load.All()) if (!PostWants.IsProvision(id)) owed += n * Items.Items.Of(id).ValueMb;
        float paid = owed >= 1f ? Pay(post, b, owed, into, counts, shortOf, true) : 0f;
        float taken = Hand(load, post, owed > 0f ? paid / owed : 0f, id => !PostWants.IsProvision(id));
        // Then the provisions, as far as the post is short of food: the
        // season's keep and the brigades' rations (Economy/PostWants).
        float foodTaken = 0f;
        float shortRations = PostWants.FoodShort(post);
        if (shortRations > 1f)
        {
            float offered = 0f, offeredRations = 0f;
            foreach (var (id, n) in load.All()) if (PostWants.IsProvision(id)) { offered += n * Items.Items.Of(id).ValueMb; offeredRations += n * Items.Items.Of(id).Nutrition / 34f; }
            if (offered > 0.5f)
            {
                float frac = Mathf.Clamp(shortRations / Mathf.Max(1f, offeredRations), 0f, 1f);
                float foodOwed = offered * frac;
                float foodPaid = Pay(post, b, foodOwed, into, counts, shortOf, false);
                foodTaken = Hand(load, post, offered > 0f ? foodPaid / offered : 0f, PostWants.IsProvision);
                paid += foodPaid;
            }
        }
        var bought = new List<string>();
        foreach (var (id, n) in counts) bought.Add($"{n} {Items.Items.Of(id).Name.ToLower()}");
        post.FursTakenMb += taken;
        post.GoodsPaidMb += paid;
        float lost = owed - taken;
        if (lost > 1f) post.LostToRivalMb += lost;
        post.ShortOf = shortOf;
        Learn(b, post, Plains.Today, Covers(post, b), PostWants.FoodShort(post) > 1f);
        if (Plains.Log)
            GD.Print($"[trade] {cal.DateString} {b.Name} at {post.Post.Name}: {taken:0} MB of furs{(foodTaken > 0f ? $" and {foodTaken:0} MB of provisions" : "")} for {(bought.Count > 0 ? string.Join(", ", bought) : "nothing")}{(lost > 1f ? $"; {lost:0} MB carried home again" : "")}{(shortOf.Count > 0 ? $"; wanted {string.Join(", ", shortOf)}" : "")}");
        if (taken + foodTaken > 0f) Chronicle.Event($"{b.Name} traded {taken + foodTaken:0} made beaver in {(foodTaken > taken ? "provisions" : "furs")} at {post.Post.Name} for {string.Join(", ", bought)}.", IsoGrid.ToWorld(post.AccessCell), subject: post.Post.Name, weight: 0.3f);
        if (lost > owed * 0.5f && shortOf.Count > 0)
        {
            Chronicle.Event($"{b.Name} came to {post.Post.Name} wanting {string.Join(" and ", shortOf)}, found none, and carried their furs home again.", IsoGrid.ToWorld(post.AccessCell), subject: post.Post.Name, weight: 1f);
            if (Plains.NearPlayer(IsoGrid.ToWorld(post.AccessCell), 8000f))
                Sim.Notice($"{b.Name} came in to {post.Post.Name} wanting {string.Join(" and ", shortOf)}. There was none on the shelves, and they took their furs away again.");
        }
    }

    // Pay `owed` made beaver in what the camp most needs for the price, a
    // little at a time, until it is paid or nothing on the shelves is worth
    // having; then sundries for up to a third. What the camp wanted and the
    // post had not got goes on the post's indent (Economy/PostWants).
    private static float Pay(TradingPostLedger post, BandLedger b, float owed, Inventory into, Dictionary<ItemId, int> counts, List<string> shortOf, bool indent)
    {
        float paid = 0f;
        for (int guard = 0; guard < 200 && owed - paid >= 0.1f; guard++)
        {
            ItemId pick = default; int take = 0; float best = 0.05f;
            foreach (var (id, shortBy, weight) in Wants.Deficits(b, into))
            {
                float price = Price(post, id);
                if (post.Stores.Count(id) < shortBy && weight > 0.3f && indent && guard == 0) PostWants.Ask(post, id, shortBy - post.Stores.Count(id));
                if (post.Stores.Count(id) == 0) { if (weight > 0.3f && !shortOf.Contains(Items.Items.Of(id).Name.ToLower())) shortOf.Add(Items.Items.Of(id).Name.ToLower()); continue; }
                if (price > owed - paid + 0.01f) continue;
                float value = weight / Mathf.Sqrt(price);
                if (value <= best) continue;
                best = value; pick = id;
                take = Mathf.Max(1, Mathf.Min(Mathf.Min(shortBy, post.Stores.Count(id)), Mathf.CeilToInt(shortBy / 3f)));
                take = Mathf.Min(take, Mathf.FloorToInt((owed - paid) / price));
            }
            if (take <= 0) break;
            post.Stores.Remove(pick, take);
            into.Add(pick, take);
            paid += take * Price(post, pick);
            counts[pick] = counts.GetValueOrDefault(pick) + take;
        }
        int sundries = Mathf.Min(post.Stores.Count(ItemId.TradeGoods), Mathf.FloorToInt(Mathf.Min(owed - paid, owed / 3f)));
        if (sundries > 0) { post.Stores.Remove(ItemId.TradeGoods, sundries); into.Add(ItemId.TradeGoods, sundries); paid += sundries; counts[ItemId.TradeGoods] = counts.GetValueOrDefault(ItemId.TradeGoods) + sundries; }
        return paid;
    }

    // The goods that were paid for change hands; the rest go home.
    private static float Hand(Inventory load, FortLedger post, float frac, System.Func<ItemId, bool> which)
    {
        frac = Mathf.Clamp(frac, 0f, 1f);
        float mb = 0f;
        foreach (var (id, n) in new List<(ItemId, int)>(load.All()))
        {
            if (!which(id)) continue;
            int k = Mathf.FloorToInt(n * frac + 0.001f);
            if (k <= 0) continue;
            load.Remove(id, k);
            post.Stores.Add(id, k);
            mb += k * Items.Items.Of(id).ValueMb;
        }
        return mb;
    }
}
