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
    }
}
