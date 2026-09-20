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

    // How much of a fur payment goes out as drink. The Nor'Westers lean on it.
    public virtual float RumShare => 0.15f;

    // Trade goods per MB of food when a band buys provisions from the post.
    public float FoodPriceMultiplier = 1.5f;

    // Running totals for the year (for the Factor's report and the HUD).
    public float FursTakenMb;
    // Furs the band carried to the other company instead (the Factor hears of it).
    public float LostToRivalMb;
    public float GoodsPaidMb;
    public int Year;

    // Which company this counter belongs to, for standing and debt.
    public virtual Agents.Faction Company => Agents.Faction.HBC;
    public virtual float BandStanding
    {
        get => Sim.Band?.StandingWithCompany ?? 50f;
        set { if (Sim.Band != null) Sim.Band.StandingWithCompany = value; }
    }
    public virtual float BandDebt
    {
        get => Sim.Band?.DebtMb ?? 0f;
        set { if (Sim.Band != null) Sim.Band.DebtMb = value; }
    }

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
        Stores.Add(ItemId.SteelTrap, 10);
        Stores.Add(ItemId.Kettle, 3);
        Stores.Add(ItemId.Water, 24);
        Stores.Add(ItemId.Rum, 12);
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
    // Debt on the books is settled first (design doc 13.3).
    public float BuyFurs(Inventory seller, Inventory buyerGoodsTo)
    {
        float paidMb = 0f;
        var band = Sim.Band;
        if (band != null && BandDebt > 0f)
        {
            float settled = 0f;
            foreach (var (id, n) in new List<(ItemId, int)>(seller.All()))
            {
                if (Items.Items.Of(id).Kind != ItemKind.Fur) continue;
                while (BandDebt > 0f && seller.Remove(id))
                {
                    float v = Items.Items.Of(id).ValueMb * PeltPrice;
                    Stores.Add(id); FursTakenMb += Items.Items.Of(id).ValueMb;
                    BandDebt = Mathf.Max(0f, BandDebt - v); settled += v;
                }
            }
            if (settled > 0f) Sim.Notice($"The band's debt at {Title} is worked down by {settled:0.#} MB of furs; {BandDebt:0.#} MB still owing.");
        }
        // A fair deal is remembered too.
        if (Sim.Band != null) BandStanding = Mathf.Min(100f, BandStanding + (PeltPrice >= 1f ? 2f : 0.5f));
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
                // Part of the price in rum, when there's rum and the band takes it.
                int rum = 0;
                if (RumShare > 0f && Stores.Has(ItemId.Rum) && GD.Randf() < RumShare * 2f) { rum = Mathf.Min(goods, Mathf.Max(1, Mathf.RoundToInt(goods * RumShare * 2f))); }
                Stores.Remove(ItemId.TradeGoods, goods - rum);
                buyerGoodsTo.Add(ItemId.TradeGoods, goods - rum);
                if (rum > 0)
                {
                    rum = Stores.TakeUpTo(ItemId.Rum, rum);
                    buyerGoodsTo.Add(ItemId.Rum, rum);
                    // Drink buys goodwill quickly and cheaply - that's the trap.
                    BandStanding = Mathf.Min(100f, BandStanding + 1.5f * rum);
                    Sim.Band?.OnRum(rum);
                }
                paidMb += goods;
                FursTakenMb += Items.Items.Of(id).ValueMb;
                GoodsPaidMb += goods;
            }
        }
        return paidMb;
    }

    // A band buys food with trade goods; a band in good standing is
    // advanced food on credit against next season's furs (design doc 13.3).
    public int SellFood(Inventory buyer, int goodsToSpend)
    {
        int sold = 0;
        var band = Sim.Band;
        bool credit = band != null && BandStanding > 40f && BandDebt < 15f;
        int creditUnits = credit ? 5 : 0;
        while ((goodsToSpend > 0 && buyer.Has(ItemId.TradeGoods)) || creditUnits > 0)
        {
            bool onCredit = !(goodsToSpend > 0 && buyer.Has(ItemId.TradeGoods));
            if (onCredit) { creditUnits--; BandDebt += 1f; }
            ItemId? food = null;
            foreach (var id in new[] { ItemId.DriedGoose, ItemId.DriedMeat, ItemId.Pemmican, ItemId.Bread })
                if (Stores.Count(id) > 20) { food = id; break; }   // keeps a reserve for its own men
            if (food == null) break;
            // One unit of goods buys a few days of food at the post's mark-up.
            int pieces = Mathf.Max(1, Mathf.RoundToInt(3f / FoodPriceMultiplier));
            if (!onCredit) { buyer.Remove(ItemId.TradeGoods); goodsToSpend--; }
            for (int i = 0; i < pieces && Stores.Remove(food.Value); i++) { buyer.Add(food.Value); sold++; }
        }
        return sold;
    }
}
