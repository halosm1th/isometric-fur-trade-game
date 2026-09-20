using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Items;
using IsometricFurTradeGame.Scripts.World;

namespace IsometricFurTradeGame.Scripts.Economy;

// York Factory's books: provisions, trade goods, furs, and the prices London
// tells the Factor to pay. Prices are in Made Beaver.
public partial class FortLedger : Household
{
    // Goods paid per MB of fur brought in. London sets it; the Factor applies
    // it. 1.0 is the "standard of trade"; higher buys more pelts and faster
    // draws the country down.
    public float PeltPrice = 1.0f;
    // What London will send next ship, applied on arrival.
    public float LondonIncentiveNext = 1.0f;
    public float QuotaMb = 60f;
    public float QuotaNextMb = 60f;

    // Trade goods per MB of food when a band buys provisions from the post.
    public float FoodPriceMultiplier = 1.5f;

    // Running totals for the year (for the Factor's report and the HUD).
    public float FursTakenMb;
    public float GoodsPaidMb;
    public int Year;

    public float FursInStoreMb
    {
        get
        {
            float mb = 0f;
            foreach (var (id, n) in Stores.All()) if (Items.Items.Of(id).Kind == ItemKind.Fur) mb += Items.Items.Of(id).ValueMb * n;
            return mb;
        }
    }

    public override void _Ready()
    {
        Title = "the Company's stores";
        Sim.Fort = this;
        AccessCell = IsoGrid.Cell(WorldLayout.Depot.Top, 2, 2);
        WoodpileCell = WorldLayout.WoodpileCell;
        HearthCells.Add(IsoGrid.Cell(WorldLayout.Depot.Top, 1, 1));
        HearthCells.Add(IsoGrid.Cell(WorldLayout.MensHouse.Top, 1, 1));

        // What a post holds at midwinter, after a ship in August.
        Stores.Add(ItemId.Pemmican, 60);
        Stores.Add(ItemId.DriedGoose, 120);
        Stores.Add(ItemId.Bread, 30);
        Stores.Add(ItemId.Ale, 20);
        Stores.Add(ItemId.Firewood, 40);
        Stores.Add(ItemId.TradeGoods, 90);
        Stores.Add(ItemId.Musket, 6);
        Stores.Add(ItemId.PointBlanket, 12);
        Stores.Add(ItemId.BeaverPelt, 22);
        Stores.Add(ItemId.CaribouHide, 4);
        Year = GameCalendar.Instance?.Year ?? 1811;

        // The post knows its goose grounds and the lodge nearest the fort;
        // the rest of the country it has to be told about, or stumble on.
        KnownSites.Add("Goose marsh");
        KnownSites.Add("Beaver lodge (upper)");
    }

    // A Cree party sells furs at the counter. Returns the MB of goods paid.
    public float BuyFurs(Inventory seller, Inventory buyerGoodsTo)
    {
        float paidMb = 0f;
        // A fair deal is remembered too.
        if (Sim.Band != null) Sim.Band.StandingWithCompany = Mathf.Min(100f, Sim.Band.StandingWithCompany + (PeltPrice >= 1f ? 2f : 0.5f));
        var furs = new List<(ItemId, int)>();
        foreach (var (id, n) in seller.All()) if (Items.Items.Of(id).Kind == ItemKind.Fur) furs.Add((id, n));
        foreach (var (id, n) in furs)
        {
            for (int i = 0; i < n; i++)
            {
                float worth = Items.Items.Of(id).ValueMb * PeltPrice;
                int goods = Mathf.RoundToInt(worth);
                if (goods < 1 && GD.Randf() < worth) goods = 1;
                if (Stores.Count(ItemId.TradeGoods) < goods) return paidMb; // the post is bought out
                seller.Remove(id);
                Stores.Add(id);
                Stores.Remove(ItemId.TradeGoods, goods);
                buyerGoodsTo.Add(ItemId.TradeGoods, goods);
                paidMb += goods;
                FursTakenMb += Items.Items.Of(id).ValueMb;
                GoodsPaidMb += goods;
            }
        }
        return paidMb;
    }

    // A band buys food with trade goods. Returns the pieces of food sold.
    public int SellFood(Inventory buyer, int goodsToSpend)
    {
        int sold = 0;
        while (goodsToSpend > 0 && buyer.Has(ItemId.TradeGoods))
        {
            ItemId? food = null;
            foreach (var id in new[] { ItemId.DriedGoose, ItemId.DriedMeat, ItemId.Pemmican, ItemId.Bread })
                if (Stores.Count(id) > 20) { food = id; break; }   // keeps a reserve for its own men
            if (food == null) break;
            // One unit of goods buys a few days of food at the post's mark-up.
            int pieces = Mathf.Max(1, Mathf.RoundToInt(3f / FoodPriceMultiplier));
            buyer.Remove(ItemId.TradeGoods);
            goodsToSpend--;
            for (int i = 0; i < pieces && Stores.Remove(food.Value); i++) { buyer.Add(food.Value); sold++; }
        }
        return sold;
    }
}
