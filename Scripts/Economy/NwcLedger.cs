using Godot;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Items;
using IsometricFurTradeGame.Scripts.World;

namespace IsometricFurTradeGame.Scripts.Economy;

// The North West Company's wintering post across the river (design doc
// 5.2, 7.1). Same books as the Bay post, different masters: no ship and no
// London - a canoe brigade from Fort William restocks it each July - and a
// partner who sets his own price, usually a shade better than the Company's.
public partial class NwcLedger : FortLedger
{
    public override Agents.Faction Company => Agents.Faction.NWC;
    public override float RumShare => 0.35f;
    public override float BandStanding
    {
        get => Sim.Band?.StandingWithNwc ?? 50f;
        set { if (Sim.Band != null) Sim.Band.StandingWithNwc = value; }
    }
    public override float BandDebt
    {
        get => Sim.Band?.DebtNwcMb ?? 0f;
        set { if (Sim.Band != null) Sim.Band.DebtNwcMb = value; }
    }

    public override void _Ready()
    {
        Title = "the Nor'Westers' store";
        Sim.Nwc = this;
        AccessCell = IsoGrid.Cell(WorldLayout.NwcHouse.Top, 2, 2);
        WoodpileCell = WorldLayout.NwcWoodpileCell;
        HearthCells.Add(IsoGrid.Cell(WorldLayout.NwcHouse.Top, 1, 1));
        PeltPrice = 1.3f;
        QuotaMb = 80f;

        Stores.Add(ItemId.Pemmican, 40);
        Stores.Add(ItemId.DriedGoose, 40);
        Stores.Add(ItemId.Ale, 30);
        Stores.Add(ItemId.Firewood, 30);
        Stores.Add(ItemId.TradeGoods, 70);
        Stores.Add(ItemId.Musket, 4);
        Stores.Add(ItemId.SteelTrap, 8);
        Stores.Add(ItemId.BeaverPelt, 10);
        Stores.Add(ItemId.Rum, 40);
        Stores.Add(ItemId.Kettle, 2);
        Stores.Add(ItemId.Water, 16);
        Stores.Add(ItemId.Tumpline, 6);
        foreach (var s in WorldLayout.Sites) KnownSites.Add(s.Name);   // they winter inland; they know the country
        KnowsTheCountry = true;
        var cal = GameCalendar.Instance;
        if (cal != null) cal.CalendarEvent += OnEvent;
    }

    private void OnEvent(string name)
    {
        if (name != "Brigade" || !Net.Net.IsServer) return;
        // The brigade lands the year's outfit and carries the furs off.
        Stores.Add(ItemId.TradeGoods, 90); Stores.Add(ItemId.Pemmican, 30); Stores.Add(ItemId.Rum, 50); Stores.Add(ItemId.Musket, 4); Stores.Add(ItemId.SteelTrap, 8);
        float shipped = FursInStoreMb;
        foreach (var id in new[] { ItemId.BeaverPelt, ItemId.ThinPelt, ItemId.CaribouHide, ItemId.WolfPelt }) Stores.TakeUpTo(id, 9999);
        // The partner answers to his peers at the rendezvous: poor returns
        // mean a sharper price next winter (design doc 10.1.4).
        PeltPrice = Mathf.Clamp(shipped < QuotaMb * 0.7f ? PeltPrice * 1.2f : PeltPrice * 0.95f, 0.5f, 3f);
        Sim.Notice($"The brigade from Fort William has come and gone: {shipped:0} MB of furs off to Montreal, the outfit landed. The Nor'Westers will pay {PeltPrice:0.00} for beaver.");
        (GetTree().GetFirstNodeInGroup("population") as Population)?.LandVoyageurs(3);
    }
}
