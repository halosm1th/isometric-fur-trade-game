using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Items;
using IsometricFurTradeGame.Scripts.World;

namespace IsometricFurTradeGame.Scripts.Economy;

// The Cree band's cache at the camp.
public partial class BandLedger : Household
{
    // How the band feels about the Company: fed and fairly paid, it trades;
    // starved or shot at, it stops coming (design doc 3.6, 10.5).
    public float StandingWithCompany = 50f;
    // ...and with the Nor'Westers across the river.
    public float StandingWithNwc = 50f;
    public float DebtNwcMb;
    public bool WillTradeWith(FortLedger post) => post is NwcLedger ? StandingWithNwc > 20f : WillTrade;
    // What the band owes the post for goods advanced against next season's furs.
    public float DebtMb;
    // A letter from the Factor invites them in to trade.
    public bool InvitedToTrade;
    public bool WillTrade => StandingWithCompany > 20f;
    private int _lastDay = -1;

    public override void _Process(double delta)
    {
        base._Process(delta);
        var cal = GameCalendar.Instance;
        if (cal == null || cal.Day == _lastDay || !Net.Net.IsServer) return;
        _lastDay = cal.Day;
        int mouths = 0;
        foreach (var n in Sim.Npcs) if (n.Faction == Agents.Faction.Cree && !n.IsDead) mouths++;
        bool hungry = FoodDays(mouths) < 3f;
        float price = Sim.Fort?.PeltPrice ?? 1f;
        // Hunger is blamed on the Company when its price is poor.
        float drift = hungry ? (price < 0.9f ? -3f : -1f) : (price >= 1.2f ? 1f : 0.3f);
        bool was = WillTrade;
        StandingWithCompany = Mathf.Clamp(StandingWithCompany + drift, 0f, 100f);
        if (was && !WillTrade) Sim.Notice("The band has had enough of the Company. They will not bring their furs to the post.");
        if (!was && WillTrade) Sim.Notice("The band is willing to trade with the post again.");
    }

    // Rum in the camp (design doc 13.4): the band comes to want it, and the
    // post that has it gets the furs.
    public float RumTaste;
    public void OnRum(int drams) { RumTaste = Mathf.Min(100f, RumTaste + drams * 4f); }

    public void OnInsult() { StandingWithCompany = Mathf.Max(0f, StandingWithCompany - 4f); }

    public void OnMemberKilledBy(Agents.Agent killer)
    {
        if (killer == null || killer.Faction != Agents.Faction.HBC) return;
        StandingWithCompany = Mathf.Max(0f, StandingWithCompany - 25f);
        Sim.Notice("The band will remember who killed one of theirs.");
    }

    public override void _Ready()
    {
        Title = "the band's cache";
        Sim.Band = this;
        AccessCell = IsoGrid.Cell(WorldLayout.CampCentre, 1, 1);
        WoodpileCell = IsoGrid.Cell(WorldLayout.CampCentre, 2, -2);
        HearthCells.Add(WorldLayout.CampFire);

        Stores.Add(ItemId.Venison, 30);
        Stores.Add(ItemId.DriedGoose, 20);
        Stores.Add(ItemId.Firewood, 20);
        Stores.Add(ItemId.BeaverPelt, 6);
        Stores.Add(ItemId.TradeGoods, 4);
        Stores.Add(ItemId.SteelTrap, 8);
        Stores.Add(ItemId.Kettle, 2);
        Stores.Add(ItemId.Water, 12);
        foreach (var s in WorldLayout.Sites) KnownSites.Add(s.Name);   // it's their country
        KnowsTheCountry = true;
    }
}
