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

    // (good, how many a person keeps on hand). A camp tops itself up to that.
    private static readonly (ItemId Id, float PerHead)[] PlainsWants =
    {
        (ItemId.Powder, 1.5f), (ItemId.Musket, 0.3f), (ItemId.Kettle, 0.25f), (ItemId.Knife, 0.5f), (ItemId.Awl, 1f),
        (ItemId.Tobacco, 1f), (ItemId.Beads, 0.6f), (ItemId.Stroud, 0.5f), (ItemId.Axe, 0.15f), (ItemId.PointBlanket, 0.15f),
        (ItemId.Flint, 0.3f), (ItemId.Rum, 0.4f),
    };
    private static readonly (ItemId Id, float PerHead)[] Woods =
    {
        (ItemId.SteelTrap, 0.5f), (ItemId.Axe, 0.3f), (ItemId.Powder, 1.2f), (ItemId.Musket, 0.2f), (ItemId.IceChisel, 0.2f),
        (ItemId.FishingNet, 0.12f), (ItemId.PointBlanket, 0.3f), (ItemId.Kettle, 0.25f), (ItemId.Tobacco, 1f), (ItemId.Awl, 1f),
        (ItemId.Knife, 0.5f), (ItemId.Stroud, 0.5f), (ItemId.Beads, 0.4f), (ItemId.Flint, 0.3f), (ItemId.Rum, 0.3f),
    };
    private static readonly (ItemId Id, float PerHead)[] Coast =
    {
        (ItemId.PointBlanket, 0.5f), (ItemId.Kettle, 0.3f), (ItemId.Beads, 0.8f), (ItemId.Knife, 0.5f), (ItemId.Musket, 0.15f),
        (ItemId.Powder, 0.8f), (ItemId.Stroud, 0.5f), (ItemId.Tobacco, 0.8f), (ItemId.Axe, 0.2f), (ItemId.Awl, 0.6f),
    };

    public static (ItemId Id, float PerHead)[] WantsOf(BandLedger b)
        => b.Nation is Faction.Coast or Faction.Plateau ? Coast
         : b.Herd.Horses > 0 || b.Nation is Faction.Blackfoot or Faction.Sioux or Faction.Missouri or Faction.Metis ? PlainsWants
         : Woods;

    // The goods a post may pay in; anything else on its shelves is its own.
    public static bool IsTradeGood(ItemId id) => id is ItemId.Powder or ItemId.Musket or ItemId.Kettle or ItemId.Knife or ItemId.Awl
        or ItemId.Tobacco or ItemId.Beads or ItemId.Stroud or ItemId.Axe or ItemId.PointBlanket or ItemId.Flint or ItemId.Rum
        or ItemId.SteelTrap or ItemId.IceChisel or ItemId.FishingNet or ItemId.Capote or ItemId.TradeGoods;

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

    public static float Worth(Inventory inv)
    {
        float mb = 0f;
        foreach (var (id, n) in inv.All()) mb += n * Items.Items.Of(id).ValueMb;
        return mb;
    }

    // How much of what this camp wants a post's shelves hold, in made beaver.
    private static float Covers(FortLedger post, BandLedger b)
    {
        float mb = 0f;
        foreach (var (id, per) in WantsOf(b))
        {
            int want = Mathf.CeilToInt(per * Mathf.Max(1, b.Mouths)) - b.Stores.Count(id);
            if (want > 0) mb += Mathf.Min(want, post.Stores.Count(id)) * Items.Items.Of(id).ValueMb;
        }
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
    private static readonly Dictionary<BandLedger, Dictionary<TradingPostLedger, (int Day, float Covers)>> _beliefs = new();

    private static readonly Dictionary<BandLedger, int> _lastTrip = new();
    public static void ResetBeliefs() { _beliefs.Clear(); _lastTrip.Clear(); }

    private static float Believed(BandLedger b, TradingPostLedger post)
    {
        if (!_beliefs.TryGetValue(b, out var m) || !m.TryGetValue(post, out var e)) return Unknown;
        float age = Plains.Today - e.Day;
        return Mathf.Lerp(e.Covers, Unknown, Mathf.Clamp(age / 150f, 0f, 1f));
    }

    private static void Learn(BandLedger b, TradingPostLedger post, int day, float covers)
    {
        if (!_beliefs.TryGetValue(b, out var m)) _beliefs[b] = m = new();
        if (!m.TryGetValue(post, out var e) || e.Day <= day) m[post] = (day, covers);
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
            foreach (var (post, e) in new List<KeyValuePair<TradingPostLedger, (int Day, float Covers)>>(_beliefs[b]))
                Learn(other, post, e.Day - 3, e.Covers * (float)GD.RandRange(0.8, 1.2));
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
            Wear(b);
            if (!season || TradeTrips.Away(b)) continue;
            // A camp goes in a few times a season - in the fall for its
            // winter's debt, at midwinter, and with the spring hunt - not every week.
            if (_lastTrip.TryGetValue(b, out int last) && Plains.Today - last < 35) continue;
            float furs = 0f; foreach (var (id, n) in FursFor(b)) furs += n * Items.Items.Of(id).ValueMb;
            // A trip is worth making for a few packs, not for a pelt.
            if (furs < Mathf.Max(15f, b.Mouths * 2f)) continue;
            var post = camp.IsFamily && Posts.Named(camp.Post)?.Ledger is { } own ? own : ChoosePost(b);
            if (post != null && TradeTrips.Start(b, post, cal)) _lastTrip[b] = Plains.Today;
        }
        Gossip();
    }

    // A camp's choice of house: the one it believes answers its wants best
    // among those within reach, the nearer favoured.
    public static TradingPostLedger ChoosePost(BandLedger b)
    {
        TradingPostLedger best = null; float bestScore = 0f;
        var at = IsoGrid.ToWorld(b.Camp);
        foreach (var p in Posts.Standing())
        {
            if (p.Ledger == null) continue;
            float d = IsoGrid.ToWorld(p.Ledger.AccessCell).DistanceTo(at);
            if (d > ReachPx) continue;
            float score = (1f + Believed(b, p.Ledger)) * (1f - 0.5f * d / ReachPx);
            if (score > bestScore) { bestScore = score; best = p.Ledger; }
        }
        return best;
    }

    // The exchange, made on the day the party is at the post: their furs at
    // the standard, paid in what the camp wants first, then in sundries (the
    // "trade goods" bale: thread, buttons, vermilion, needles) for no more
    // than a third. What is not paid for goes home with them. They see the
    // shelves, and remember them.
    public static void Trade(TradingPostLedger post, BandLedger b, Inventory furs, Inventory into, GameCalendar cal)
    {
        float owed = Worth(furs);
        if (owed < 1f) return;
        float paid = 0f;
        var bought = new List<string>();
        var shortOf = new List<string>();
        foreach (var (id, per) in WantsOf(b))
        {
            float value = Items.Items.Of(id).ValueMb;
            int want = Mathf.CeilToInt(per * Mathf.Max(1, b.Mouths)) - b.Stores.Count(id) - into.Count(id);
            if (want <= 0) continue;
            int n = Mathf.Min(want, Mathf.Min(post.Stores.Count(id), Mathf.FloorToInt((owed - paid) / value)));
            if (post.Stores.Count(id) == 0 && want >= 2) shortOf.Add(Items.Items.Of(id).Name.ToLower());
            if (n <= 0) continue;
            post.Stores.Remove(id, n);
            into.Add(id, n);
            paid += n * value;
            bought.Add($"{n} {Items.Items.Of(id).Name.ToLower()}");
            if (owed - paid < 0.1f) break;
        }
        int sundries = Mathf.Min(post.Stores.Count(ItemId.TradeGoods), Mathf.FloorToInt(Mathf.Min(owed - paid, owed / 3f)));
        if (sundries > 0) { post.Stores.Remove(ItemId.TradeGoods, sundries); into.Add(ItemId.TradeGoods, sundries); paid += sundries; bought.Add($"{sundries} sundries"); }
        // The furs change hands in proportion to what was paid.
        float frac = Mathf.Clamp(paid / owed, 0f, 1f);
        float taken = 0f;
        foreach (var (id, n) in new List<(ItemId, int)>(furs.All()))
        {
            int k = Mathf.FloorToInt(n * frac + 0.001f);
            if (k <= 0) continue;
            furs.Remove(id, k);
            post.Stores.Add(id, k);
            taken += k * Items.Items.Of(id).ValueMb;
        }
        post.FursTakenMb += taken;
        post.GoodsPaidMb += paid;
        float lost = owed - taken;
        if (lost > 1f) post.LostToRivalMb += lost;
        post.ShortOf = shortOf;
        Learn(b, post, Plains.Today, Covers(post, b));
        if (Plains.Log)
            GD.Print($"[trade] {cal.DateString} {b.Name} at {post.Post.Name}: {taken:0} MB of furs for {(bought.Count > 0 ? string.Join(", ", bought) : "nothing")}{(lost > 1f ? $"; {lost:0} MB carried home again" : "")}{(shortOf.Count > 0 ? $"; wanted {string.Join(", ", shortOf)}" : "")}");
        if (taken > 0f) Chronicle.Event($"{b.Name} traded {taken:0} made beaver in furs at {post.Post.Name} for {string.Join(", ", bought)}.", IsoGrid.ToWorld(post.AccessCell), subject: post.Post.Name, weight: 0.3f);
        if (lost > owed * 0.5f && shortOf.Count > 0)
        {
            Chronicle.Event($"{b.Name} came to {post.Post.Name} wanting {string.Join(" and ", shortOf)}, found none, and carried their furs home again.", IsoGrid.ToWorld(post.AccessCell), subject: post.Post.Name, weight: 1f);
            if (Plains.NearPlayer(IsoGrid.ToWorld(post.AccessCell), 8000f))
                Sim.Notice($"{b.Name} came in to {post.Post.Name} wanting {string.Join(" and ", shortOf)}. There was none on the shelves, and they took their furs away again.");
        }
    }

    // What a camp has wears out and is used up: powder is shot off, tobacco
    // smoked, rum drunk, and a kettle, an axe or a gun breaks in the end.
    public static void Wear(BandLedger b)
    {
        int mouths = Mathf.Max(1, b.Mouths);
        void Use(ItemId id, float perHeadPerWeek)
        {
            float x = perHeadPerWeek * mouths;
            int n = Mathf.FloorToInt(x) + (GD.Randf() < x % 1f ? 1 : 0);
            if (n > 0) b.Stores.TakeUpTo(id, n);
        }
        Use(ItemId.Powder, 0.12f); Use(ItemId.Tobacco, 0.15f); Use(ItemId.Rum, 0.1f); Use(ItemId.Awl, 0.03f); Use(ItemId.Beads, 0.02f);
        Use(ItemId.Stroud, 0.02f); Use(ItemId.Flint, 0.01f);
        foreach (var id in new[] { ItemId.Kettle, ItemId.Axe, ItemId.Musket, ItemId.SteelTrap, ItemId.Knife, ItemId.IceChisel, ItemId.FishingNet, ItemId.PointBlanket })
        {
            int have = b.Stores.Count(id);
            if (have > 0 && GD.Randf() < have * 0.012f) b.Stores.Remove(id);
        }
    }
}
