using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Items;
using IsometricFurTradeGame.Scripts.World;

namespace IsometricFurTradeGame.Scripts.Economy;

// Oxford House: the Company's inland post (design doc 3.9's far outpost,
// 10.1.2's second leg). Same books as York Factory, a postmaster instead
// of a Factor, no ship and no London of its own - its orders and its
// outfit come up the river road from the Bay, and its furs and reports
// go down it. The inland bands trade here abstractly, week by week.
public partial class OutpostLedger : FortLedger
{
    // Reports that have reached York about this post (design doc 3.9 step 6).
    public int LastReportSentDay = -999;
    public string Name = "Oxford House";
    private float _hoursToWeek = 24f * 7f;

    public override void _Ready()
    {
        Title = "Oxford House stores";
        Sim.Outpost = this;
        AccessCell = IsoGrid.Cell(WorldLayout.OutpostHouse.Top, 2, 2);
        WoodpileCell = WorldLayout.OutpostWoodpileCell;
        HearthCells.Add(IsoGrid.Cell(WorldLayout.OutpostHouse.Top, 1, 1));
        PeltPrice = 1.0f;
        QuotaMb = 40f;

        Stores.Add(ItemId.Pemmican, 30);
        Stores.Add(ItemId.DriedGoose, 30);
        Stores.Add(ItemId.Firewood, 30);
        Stores.Add(ItemId.TradeGoods, 50);
        Stores.Add(ItemId.Musket, 2);
        Stores.Add(ItemId.SteelTrap, 8);
        Stores.Add(ItemId.Kettle, 1);
        Stores.Add(ItemId.Water, 10);
        Stores.Add(ItemId.Axe, 2);
        Stores.Add(ItemId.Snowshoes, 2);
        Stores.Add(ItemId.BeaverPelt, 8);
        KnownSites.Add("Beaver lodge (Knee Lake)");
        KnownSites.Add("Beaver lodge (Oxford)");
        KnownSites.Add("Caribou herd");
        KnowsTheCountry = true;
        DirectiveText = "The Factor's word from last autumn: hold the post, trade fairly, send the furs down before the ship.";
    }

    // The inland bands come and go without being drawn (design doc 18.2):
    // once a week a party trades what the price and the shelves allow.
    public override void _Process(double delta)
    {
        base._Process(delta);
        var cal = GameCalendar.Instance;
        if (cal == null || !Net.Net.IsServer) return;
        _hoursToWeek -= cal.DeltaGameHours;
        if (_hoursToWeek > 0f) return;
        _hoursToWeek += 24f * 7f;
        if (cal.CurrentSeason == Season.Breakup || cal.Month == 7 || cal.Month == 8) return;   // they're at the fisheries
        float want = (2f + 5f * PeltPrice) * (float)GD.RandRange(0.6, 1.4);
        int pay = Mathf.RoundToInt(want * PeltPrice);
        if (Stores.Count(ItemId.TradeGoods) < pay) { Chronicle.Event("an inland party came to Oxford House and found the shelves bare; they went on to the Canadians.", IsoGrid.ToWorld(AccessCell), subject: Name, weight: 1f); LostToRivalMb += want; return; }
        Stores.Remove(ItemId.TradeGoods, pay);
        int pelts = Mathf.RoundToInt(want);
        Stores.Add(ItemId.BeaverPelt, pelts);
        FursTakenMb += pelts; GoodsPaidMb += pay;
        if (Stores.Has(ItemId.Rum) && GD.Randf() < RumShare) Stores.Remove(ItemId.Rum);
        Chronicle.Event($"an inland party traded {pelts} beaver at Oxford House for {pay} pieces.", IsoGrid.ToWorld(AccessCell), subject: Name, weight: 0.5f);
    }
}
