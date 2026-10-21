using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Items;
using IsometricFurTradeGame.Scripts.World;

namespace IsometricFurTradeGame.Scripts.Economy;

// The Missouri towns as the market of the plains (design doc 7.2, 12.3): the
// corn fair is Gardens'; this is the rest of it. Horses came up from the
// south-west - the Crow, the Shoshone and the Cheyenne brought them in - and
// went north from the towns to the Assiniboine and the Cree, who had too few
// and paid in guns and Company goods. The guns came up the other way, from
// the Assiniboine River posts, whose men went down to the towns every winter
// with a few horse-loads of goods and came back with robes, corn and horses.
// That is how a musket sold at Brandon House ended up in a Crow lodge, and
// why trade was never neutral. Resolved weekly in numbers; Plains.Weekly calls it.
public static class MissouriTrade
{
    private const float MarketReachPx = 26000f;   // a fortnight's travel each way
    private static int _expeditionYear = -1;

    public static void Week(GameCalendar cal)
    {
        var towns = new List<CountryBand>();
        foreach (var c in Camps.All) if (c.Ledger != null && Gardens.IsTown(c.Ledger)) towns.Add(c.Ledger);
        if (towns.Count == 0) return;
        if (cal.Month is >= 6 and <= 9) HorseFair(towns, cal);
        if (cal.Month is 10 or 11 or 1 or 2 or 3) WinterCorn(towns, cal);
        if (cal.Month == 12 && _expeditionYear != cal.Year) { _expeditionYear = cal.Year; Expedition(towns, cal); }
    }

    private static CountryBand NearestTown(List<CountryBand> towns, Vector2I camp, out float dist)
    {
        CountryBand best = null; dist = float.MaxValue;
        foreach (var t in towns)
        {
            float d = IsoGrid.ToWorld(t.Camp).DistanceTo(IsoGrid.ToWorld(camp));
            if (d < dist) { dist = d; best = t; }
        }
        return best;
    }

    private static float PerHead(BandLedger b) => b.Herd.Horses / (float)Mathf.Max(1, b.Mouths);

    // The summer fair: horses in from the south-west, out to the north.
    private static void HorseFair(List<CountryBand> towns, GameCalendar cal)
    {
        var bands = new List<BandLedger>(Sim.Bands);
        foreach (var c in Camps.All) if (c.Ledger != null && !Gardens.IsTown(c.Ledger) && !c.IsFamily) bands.Add(c.Ledger);
        // Sellers first, so the towns have horses for the buyers.
        foreach (var b in bands)
        {
            // The horse country is the south-west; the Métis and the north buy, they do not sell.
            if (PerHead(b) < 2.5f || b.Herd.Free < 6 || b.Nation == Faction.Metis) continue;
            var town = NearestTown(towns, b.Camp, out float d);
            if (town == null || d > MarketReachPx || GD.Randf() > 0.5f) continue;
            int n = Mathf.Min(b.Herd.Free / 5, GD.RandRange(2, 6));
            if (n <= 0) continue;
            // Paid in what the town has: guns and goods it had from the posts first,
            // then corn and meat; they part with as many horses as it pays for.
            string paid = Pay(town, b, n * Herd.ValueMb, out float owing);
            n = Mathf.FloorToInt(n * (1f - owing / (n * Herd.ValueMb)) + 0.01f);
            if (n <= 0) continue;
            b.Herd.Lose(n);
            town.Herd.Horses += n;
            Log(cal, $"{b.Name} brought {n} horses to {town.Name} for {paid}");
            MarketNews.Observe(b, town); MarketNews.Meet(b, town);
            Chronicle.Event($"{b.Name} came in to {town.Name} with {n} horses from the south and went home with {paid}.", IsoGrid.ToWorld(town.Camp), subject: town.Name, weight: 0.5f);
        }
        foreach (var b in bands)
        {
            if (PerHead(b) >= 1.5f || b.Mouths < 3) continue;
            var town = NearestTown(towns, b.Camp, out float d);
            if (town == null || d > MarketReachPx || town.Herd.Free < 4 || GD.Randf() > 0.5f) continue;
            int n = Mathf.Min(town.Herd.Free - 3, GD.RandRange(1, 4));
            if (n <= 0) continue;
            // What a horse costs the north: a gun, or its worth in goods, robes or pemmican.
            float owed = n * Herd.ValueMb;
            string paid = Pay(b, town, owed, out float short_);
            if (short_ > owed * 0.5f) continue;   // came with too little; went home afoot
            town.Herd.Lose(n);
            b.Herd.Horses += n;
            Log(cal, $"{b.Name} bought {n} horses at {town.Name} for {paid}");
            MarketNews.Observe(b, town); MarketNews.Meet(b, town);
            Chronicle.Event($"{b.Name} went down to {town.Name} and came home with {n} horses, paid for in {paid}.", IsoGrid.ToWorld(b.Camp), subject: b.Name, weight: 0.6f);
            if (Plains.NearPlayer(IsoGrid.ToWorld(b.Camp), 12000f) || Plains.NearPlayer(IsoGrid.ToWorld(town.Camp), 12000f))
                Sim.Notice($"{b.Name} are back from the Missouri towns with {n} horses, paid for in {paid}.");
        }
    }

    // After the harvest the camps come in through the winter for corn as well,
    // bringing robes and meat - the robe season is the winter.
    private static void WinterCorn(List<CountryBand> towns, GameCalendar cal)
    {
        foreach (var c in Camps.All)
        {
            var b = c.Ledger;
            if (b == null || Gardens.IsTown(b) || c.IsFamily || GD.Randf() > 0.35f) continue;
            // A camp sets out for the town it believes has corn - which is the town it
            // last saw or heard of, not the town that has it. The trip may find the
            // lodges bare; the camp comes home knowing it.
            var town = MarketNews.Best(b, towns, MarketReachPx, Mathf.Max(5f, b.Mouths * 2f), cal);
            if (town == null) continue;
            // The town keeps its winter's corn and its seed.
            int corn = Mathf.Min(town.Stores.Count(ItemId.Corn) - town.Mouths * 20, b.Mouths * 8);
            if (corn <= 0)
            {
                Log(cal, $"{b.Name} went to {town.Name} for corn and found none to spare");
                MarketNews.Observe(b, town); MarketNews.Meet(b, town);
                continue;
            }
            string paid = Pay(b, town, corn * 0.25f, out float owing);
            if (owing <= corn * 0.25f * 0.5f)
            {
                town.Stores.Remove(ItemId.Corn, corn);
                b.Stores.Add(ItemId.Corn, corn);
                Log(cal, $"{b.Name} took {corn} corn from {town.Name} for {paid}");
            }
            MarketNews.Observe(b, town); MarketNews.Meet(b, town);
        }
    }

    // Settle a debt in Made Beaver out of a household's stores, guns first
    // (the thing everybody wanted), then goods, robes, pemmican and corn.
    private static string Pay(Household from, Household to, float mb, out float shortBy)
    {
        var parts = new List<string>();
        (ItemId id, float value, string name)[] media =
        {
            (ItemId.Musket, 14f, "guns"), (ItemId.Powder, 1f, "powder and ball"), (ItemId.Kettle, 2f, "kettles"), (ItemId.Knife, 0.5f, "knives"), (ItemId.Tobacco, 1f, "tobacco"), (ItemId.Beads, 0.5f, "beads"), (ItemId.TradeGoods, 1f, "goods"), (ItemId.BuffaloRobe, 2f, "robes"),
            (ItemId.Pemmican, 0.3f, "pemmican"), (ItemId.Corn, 0.25f, "corn"), (ItemId.DriedMeat, 0.25f, "dried meat"),
        };
        float left = mb;
        foreach (var (id, value, name) in media)
        {
            if (left <= 0.01f) break;
            int want = id == ItemId.Musket ? Mathf.FloorToInt(left / value + 0.3f) : Mathf.CeilToInt(left / value);
            // A camp keeps a gun or two of its own, and its winter's food.
            int spare = from.Stores.Count(id) - (id == ItemId.Musket ? 1 : id is ItemId.Pemmican or ItemId.Corn or ItemId.DriedMeat ? (from as BandLedger)?.Mouths * 10 ?? 0 : 0);
            int n = Mathf.Min(want, Mathf.Max(0, spare));
            if (n <= 0) continue;
            from.Stores.Remove(id, n);
            to.Stores.Add(id, n);
            left -= n * value;
            parts.Add($"{n} {name}");
        }
        shortBy = Mathf.Max(0f, left);
        return parts.Count > 0 ? string.Join(", ", parts) : "promises";
    }

    // The winter trip down to the towns from the nearest post on the
    // Assiniboine: a few men, a few horse-loads of goods and guns, and back
    // in the new year with robes, corn and horses.
    private static void Expedition(List<CountryBand> towns, GameCalendar cal)
    {
        foreach (var standing in Posts.Standing())
        {
            var post = standing.Ledger;
            if (post == null) continue;
            // The post sends its men where it believes the corn is: what the
            // Assiniboine and the camps have told it, or the habit of the country
            // if it has heard nothing. Bad news keeps them at home.
            var town = MarketNews.Best(post, towns, 20000f, 40f, cal);
            if (town == null)
            {
                // Only a post with a town in reach has a decision to make.
                foreach (var t in towns)
                    if (MarketNews.Knows(post, t) && IsoGrid.ToWorld(post.AccessCell).DistanceTo(IsoGrid.ToWorld(t.Camp)) <= 20000f)
                    { Log(cal, $"{post.Title} sent no one down (it has heard: {MarketNews.Describe(post)})"); break; }
                continue;
            }
            // A few horse-loads of what the towns trade for, out of the post's own outfit:
            // guns and powder first, then kettles, knives, awls, tobacco, beads and cloth.
            float worth = 0f;
            int guns = 0, goods = 0;
            var load = new System.Collections.Generic.List<string>();
            foreach (var (id, most) in new (ItemId, int)[] { (ItemId.Musket, 4), (ItemId.Powder, 20), (ItemId.Kettle, 4), (ItemId.Knife, 10), (ItemId.Awl, 30), (ItemId.Tobacco, 10), (ItemId.Beads, 10), (ItemId.Stroud, 6), (ItemId.TradeGoods, 15) })
            {
                int k = Mathf.Min(most, post.Stores.Count(id) / 3);
                if (k <= 0) continue;
                post.Stores.Remove(id, k);
                town.Stores.Add(id, k);
                worth += k * Items.Items.Of(id).ValueMb;
                if (id == ItemId.Musket) guns = k; else goods += k;
                load.Add($"{k} {Items.Items.Of(id).Name.ToLower()}");
            }
            if (worth < 10f) { Log(cal, $"{post.Title} has nothing to send down to {town.Name}"); continue; }
            Log(cal, $"{post.Title} sends men to {town.Name} with {string.Join(", ", load)} (it has heard: {MarketNews.Describe(post)})");
            // What the town gives for it, at the town's prices.
            int robes = town.Stores.TakeUpTo(ItemId.BuffaloRobe, Mathf.RoundToInt(worth * 0.3f / 2f));
            // The town sells its surplus, not its seed or its winter.
            int corn = town.Stores.TakeUpTo(ItemId.Corn, Mathf.Min(Mathf.RoundToInt(worth * 0.3f / 0.25f), Mathf.Max(0, town.Stores.Count(ItemId.Corn) - town.Mouths * 10)));
            int horses = Mathf.Min(town.Herd.Free / 4, Mathf.RoundToInt(worth * 0.25f / Herd.ValueMb));
            int beaver = town.Stores.TakeUpTo(ItemId.BeaverPelt, 10), wolf = town.Stores.TakeUpTo(ItemId.WolfPelt, 10);
            int furs = beaver + wolf;
            post.Stores.Add(ItemId.BeaverPelt, beaver);
            post.Stores.Add(ItemId.WolfPelt, wolf);
            post.Stores.Add(ItemId.BuffaloRobe, robes);
            post.Stores.Add(ItemId.Corn, corn);
            if (horses > 0) { town.Herd.Lose(horses); post.Herd.Horses += horses; }
            // They are at the town now and see how it stands; the post learns it when they are home.
            MarketNews.Observe(post, town); MarketNews.Meet(post, town);
            string brought = $"{robes} robes, {corn} corn, {horses} horses and {furs} furs";
            Log(cal, $"{post.Title} men to {town.Name}: {goods} goods and {guns} guns for {brought}");
            Chronicle.Event($"Men from {post.Title} went down to {town.Name} with {goods} in goods and {guns} guns and came back with {brought}.", IsoGrid.ToWorld(post.AccessCell), subject: post.Title, weight: 0.8f);
            if (Plains.NearPlayer(IsoGrid.ToWorld(post.AccessCell), 12000f))
                Sim.Notice($"The men are back at {post.Title} from the Missouri towns: {brought}.");
        }
    }

    private static void Log(GameCalendar cal, string text) { if (Plains.Log) GD.Print($"[plains] {cal.DateString} {text}"); }
}
