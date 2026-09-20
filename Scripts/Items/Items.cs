using System.Collections.Generic;
using System.Linq;

namespace IsometricFurTradeGame.Scripts.Items;

public enum ItemId
{
    Pemmican,
    DriedGoose,
    Bread,
    Ale,
    Firewood,
    Capote,
    PointBlanket,
}

public enum ItemKind { Food, Drink, Fuel, Clothing }

// Weights are real: a bag of pemmican was ~90 lb, so one "piece" here is a
// day-ration slab. Warmth is the clothing rating used by exposure; FuelHours
// is how long one piece keeps a hearth burning.
public readonly record struct ItemDef(
    string Name, ItemKind Kind, float WeightLbs, string Icon,
    float Nutrition = 0f, float Thirst = 0f, float Warmth = 0f, float FuelHours = 0f);

public static class Items
{
    private const string IconDir = "res://assets/sprites/items/";

    public static readonly Dictionary<ItemId, ItemDef> Defs = new()
    {
        { ItemId.Pemmican,     new("Pemmican",      ItemKind.Food,     1.5f, IconDir + "pemmican.png",     Nutrition: 45f) },
        { ItemId.DriedGoose,   new("Dried goose",   ItemKind.Food,     1.0f, IconDir + "dried_goose.png",  Nutrition: 30f) },
        { ItemId.Bread,        new("Loaf of bread", ItemKind.Food,     1.0f, IconDir + "bread_loaf.png",   Nutrition: 25f) },
        { ItemId.Ale,          new("Tankard of ale", ItemKind.Drink,   2.0f, IconDir + "ale_tankard.png",  Nutrition: 6f, Thirst: 45f, Warmth: 4f) },
        { ItemId.Firewood,     new("Firewood",      ItemKind.Fuel,     5.0f, IconDir + "firewood.png",     FuelHours: 3f) },
        { ItemId.Capote,       new("Capote",        ItemKind.Clothing, 4.0f, IconDir + "capote.png",       Warmth: 3f) },
        { ItemId.PointBlanket, new("Point blanket", ItemKind.Clothing, 5.0f, IconDir + "blanket_pelt.png", Warmth: 4f) },
    };

    public static ItemDef Of(ItemId id) => Defs[id];
}

// Stacks keep their insertion order so the hotbar's slots stay put while
// counts change underneath them.
public class Inventory
{
    private readonly Dictionary<ItemId, int> _stacks = new();
    private readonly List<ItemId> _order = new();

    public int Count(ItemId id) => _stacks.GetValueOrDefault(id);
    public bool Has(ItemId id) => Count(id) > 0;

    public void Add(ItemId id, int n = 1)
    {
        if (!_stacks.ContainsKey(id)) _order.Add(id);
        _stacks[id] = Count(id) + n;
    }

    public bool Remove(ItemId id, int n = 1)
    {
        if (Count(id) < n) return false;
        _stacks[id] -= n;
        if (_stacks[id] == 0)
        {
            _stacks.Remove(id);
            _order.Remove(id);
        }
        return true;
    }

    public int SlotCount => _order.Count;

    public (ItemId id, int count)? Slot(int i) =>
        i >= 0 && i < _order.Count ? (_order[i], _stacks[_order[i]]) : null;

    public float TotalWeightLbs =>
        _stacks.Sum(kv => Items.Of(kv.Key).WeightLbs * kv.Value);

    // Sum of everything worn. Layers stack, which is how the real kit worked.
    public float ClothingWarmth =>
        _stacks.Where(kv => Items.Of(kv.Key).Kind == ItemKind.Clothing)
               .Sum(kv => Items.Of(kv.Key).Warmth * kv.Value);

    public IEnumerable<(ItemId id, int count)> All() =>
        _order.Select(id => (id, _stacks[id]));
}
