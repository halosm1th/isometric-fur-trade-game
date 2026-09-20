using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Core;
using IsometricFurTradeGame.Scripts.Items;
using IsometricFurTradeGame.Scripts.World;

namespace IsometricFurTradeGame.Scripts.Economy;

// A household's shared stock: the post's stores or the band's cache. Agents
// draw rations from it and put what they gather into it. The stock is a
// place - AccessCell is where you stand to use it.
public partial class Household : Node
{
    public readonly Inventory Stores = new();
    public Vector2I AccessCell;
    public Vector2I WoodpileCell;
    public readonly List<Vector2I> HearthCells = new();
    public string Title = "stores";

    // Takes up to n pieces of the plainest food available into the agent's pack.
    public bool TakeRation(Agent agent, int n)
    {
        int taken = 0;
        foreach (var id in new[] { ItemId.Venison, ItemId.Goose, ItemId.DriedGoose, ItemId.Bread, ItemId.Pemmican })
        {
            while (taken < n && Stores.Remove(id)) { agent.Inventory.Add(id); taken++; }
            if (taken >= n) break;
        }
        return taken > 0;
    }

    public float FoodDays(int mouths) => mouths <= 0 ? 999f : Stores.FoodNutritionTotal / (mouths * 1.4f * 24f);

    public Hearth LowestHearth(float belowHours)
    {
        Hearth best = null;
        var s = Sim.Structures;
        if (s == null) return null;
        foreach (var h in s.AllHearths)
        {
            if (!HearthCells.Contains(h.Cell)) continue;
            if (h.FuelHours < belowHours && (best == null || h.FuelHours < best.FuelHours)) best = h;
        }
        return best;
    }
}
