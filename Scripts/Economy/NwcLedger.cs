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
    // What the council settled (design doc 3.8.1), or what the partner does regardless.
    public float RumShareSetting = 0.35f;
    public override float RumShare => RumShareSetting;
    public int OutfitPieces = 90;
    public bool PriceAtDiscretion;
    public bool RaidsSanctioned;
    public int LastAmbushDay = -999;
    public string DirectiveSummary() => $"price {PeltPrice:0.00}{(PriceAtDiscretion ? " at his discretion" : "")}, {OutfitPieces} pieces, rum {(RumShareSetting > 0.3f ? "free" : "sparing")}{(RaidsSanctioned ? ", the Bay to be pressed" : "")}";
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
        Stores.Add(ItemId.FishingLine, 5);   // winter fish through the ice
        foreach (var s in WorldLayout.Sites) KnownSites.Add(s.Name);   // they winter inland; they know the country
        KnowsTheCountry = true;
        var cal = GameCalendar.Instance;
        if (cal != null) cal.CalendarEvent += OnEvent;
    }

    private void OnEvent(string name)
    {
        if (name != "Brigade" || !Net.Net.IsServer) return;
        // The brigade lands the year's outfit and carries the furs off.
        float shipped = FursInStoreMb;
        foreach (var id in new[] { ItemId.BeaverPelt, ItemId.ThinPelt, ItemId.ShotPelt, ItemId.ShotHide, ItemId.CaribouHide, ItemId.WolfPelt }) Stores.TakeUpTo(id, 9999);
        // The partner answers to his peers at the rendezvous: the council's
        // word comes back with the brigade as a packet he must sit down and
        // read (design doc 10.1.3).
        // The council sits (design doc 3.8.1) and its minutes come north as
        // a packet; the outfit it voted is what the canoes carry.
        Agents.Npc partner = null; foreach (var n in Sim.Npcs) if (n.Role == Agents.Role.NwcPartner && !n.IsDead) partner = n;
        RaidsSanctioned = false;
        var mandate = Council.Convene(this, partner, shipped, QuotaMb);
        int pieces = OutfitPieces;
        Stores.Add(ItemId.TradeGoods, pieces); Stores.Add(ItemId.Pemmican, pieces / 3); Stores.Add(ItemId.Rum, RumShareSetting > 0.3f ? 60 : 25); Stores.Add(ItemId.Musket, 4); Stores.Add(ItemId.SteelTrap, 8);
        Sim.Notice($"The brigade from Fort William has come and gone: {shipped:0} MB of furs off to Montreal, {pieces} pieces landed, and the minutes of the council for the partner.");
        if (partner != null) Mail.Give(mandate, partner); else Mail.Open(mandate, null);
        // A partner left to his own judgment sets the price the country requires.
        if (PriceAtDiscretion && Sim.Fort != null) PeltPrice = Mathf.Clamp(Mathf.Max(PeltPrice, Sim.Fort.PeltPrice * 1.25f), 0.5f, 3f);
        (GetTree().GetFirstNodeInGroup("population") as Population)?.LandVoyageurs(3);
    }
}
