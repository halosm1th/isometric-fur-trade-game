using System.Collections.Generic;
using System.Linq;

namespace IsometricFurTradeGame.Scripts.Items;

public enum ItemId
{
    // Food and drink
    Pemmican,
    DriedGoose,
    SaltGeese,
    Bread,
    Ale,
    Rum,
    Water,
    Venison,
    Goose,
    DriedMeat,
    Fish,
    Hare,
    // Fuel
    Firewood,
    // Clothing
    Capote,
    PointBlanket,
    HideCoat,
    // Furs and trade
    BeaverPelt,
    ThinPelt,
    ShotPelt,
    ShotHide,
    CaribouHide,
    WolfPelt,
    TradeGoods,
    SteelTrap,
    Kettle,
    Flint,
    FishingLine,
    Tumpline,
    PackBasket,
    Snowshoes,
    Axe,
    // Weapons
    Musket,
    Bow,
    Knife,
    Tomahawk,
}

public enum ItemKind { Food, Drink, Fuel, Clothing, Fur, Goods, Weapon, Tool }

// Weights are real: a bag of pemmican was ~90 lb, so one "piece" here is a
// day-ration slab. Warmth is the clothing rating used by exposure; FuelHours
// is how long one piece keeps a hearth burning. ValueMb is worth in Made
// Beaver, the HBC's unit of account (one prime beaver pelt).
public readonly record struct ItemDef(
    string Name, ItemKind Kind, float WeightLbs, string Icon,
    float Nutrition = 0f, float Thirst = 0f, float Warmth = 0f, float FuelHours = 0f, float ValueMb = 0f,
    float Damage = 0f, float Range = 0f, float Cooldown = 1f, float Intoxication = 0f, float Capacity = 0f);

public static class Items
{
    private const string IconDir = "res://assets/sprites/items/";

    public static readonly Dictionary<ItemId, ItemDef> Defs = new()
    {
        { ItemId.Pemmican,     new("Pemmican",       ItemKind.Food,     1.5f, IconDir + "pemmican.png",     Nutrition: 45f, ValueMb: 0.3f) },
        { ItemId.DriedGoose,   new("Dried goose",    ItemKind.Food,     1.0f, IconDir + "dried_goose.png",  Nutrition: 30f, ValueMb: 0.2f) },
        // A post's winter came in casks: eight birds salted down, and a man
        // does not eat one at a sitting (design doc 13.1, 14.1).
        { ItemId.SaltGeese,    new("Cask of salt geese", ItemKind.Food, 60f, IconDir + "salt_geese.png", Nutrition: 240f, ValueMb: 1.6f) },
        { ItemId.Bread,        new("Loaf of bread",  ItemKind.Food,     1.0f, IconDir + "bread_loaf.png",   Nutrition: 25f, ValueMb: 0.15f) },
        { ItemId.Ale,          new("Tankard of ale", ItemKind.Drink,    2.0f, IconDir + "ale_tankard.png",  Nutrition: 6f, Thirst: 45f, Warmth: 4f, ValueMb: 0.2f, Intoxication: 12f) },
        { ItemId.Rum,          new("Dram of rum",    ItemKind.Drink,    1.0f, IconDir + "rum.png",          Thirst: 10f, Warmth: 8f, ValueMb: 0.8f, Intoxication: 35f) },
        { ItemId.Water,        new("Water",          ItemKind.Drink,    2.0f, IconDir + "water.png",        Thirst: 50f) },
        { ItemId.Venison,      new("Fresh meat",     ItemKind.Food,     2.0f, IconDir + "venison.png",      Nutrition: 30f, ValueMb: 0.15f) },
        { ItemId.DriedMeat,    new("Dried meat",     ItemKind.Food,     0.8f, IconDir + "dried_meat.png",  Nutrition: 35f, ValueMb: 0.25f) },
        { ItemId.Goose,        new("Goose",          ItemKind.Food,     1.5f, IconDir + "goose_item.png",   Nutrition: 35f, ValueMb: 0.15f) },
        { ItemId.Firewood,     new("Firewood",       ItemKind.Fuel,     5.0f, IconDir + "firewood.png",     FuelHours: 3f) },
        { ItemId.Capote,       new("Capote",         ItemKind.Clothing, 4.0f, IconDir + "capote.png",       Warmth: 4.5f, ValueMb: 3f) },
        { ItemId.PointBlanket, new("Point blanket",  ItemKind.Clothing, 5.0f, IconDir + "blanket_pelt.png", Warmth: 5f, ValueMb: 4f) },
        { ItemId.HideCoat,     new("Hide coat",      ItemKind.Clothing, 5.0f, IconDir + "hide_coat.png",    Warmth: 7f, ValueMb: 2f) },
        { ItemId.BeaverPelt,   new("Beaver pelt",    ItemKind.Fur,      1.5f, IconDir + "beaver_pelt.png",  ValueMb: 1f) },
        { ItemId.ThinPelt,     new("Summer pelt",    ItemKind.Fur,      1.2f, IconDir + "thin_pelt.png",  ValueMb: 0.45f) },
        // A ball tears a pelt; the graders in London pay a fraction (design doc 13.6, 14.4).
        { ItemId.ShotPelt,     new("Shot-through pelt", ItemKind.Fur,   1.5f, IconDir + "shot_pelt.png",    ValueMb: 0.35f) },
        { ItemId.ShotHide,     new("Holed hide",     ItemKind.Fur,      6.0f, IconDir + "shot_hide.png",    ValueMb: 0.2f) },
        { ItemId.CaribouHide,  new("Caribou hide",   ItemKind.Fur,      6.0f, IconDir + "caribou_hide.png", ValueMb: 0.5f) },
        { ItemId.WolfPelt,     new("Wolf pelt",      ItemKind.Fur,      3.0f, IconDir + "wolf_pelt.png",    ValueMb: 0.6f) },
        { ItemId.TradeGoods,   new("Trade goods",    ItemKind.Goods,    3.0f, IconDir + "trade_goods.png",  ValueMb: 1f) },
        { ItemId.SteelTrap,    new("Steel trap",     ItemKind.Tool,     4.0f, IconDir + "steel_trap.png",   ValueMb: 1.5f) },
        { ItemId.Kettle,       new("Copper kettle",  ItemKind.Tool,     3.0f, IconDir + "kettle.png",       ValueMb: 2f) },
        { ItemId.Flint,        new("Flint and steel",ItemKind.Tool,     0.3f, IconDir + "flint.png",        ValueMb: 0.5f) },
        { ItemId.FishingLine,  new("Fishing line",   ItemKind.Tool,     0.5f, IconDir + "fishing_line.png", ValueMb: 0.5f) },
        // Packs add to what a body can carry before it slows (design doc 12.4, 12.5).
        { ItemId.Tumpline,     new("Tumpline",       ItemKind.Tool,     1.0f, IconDir + "tumpline.png",     ValueMb: 0.5f, Capacity: 40f) },
        { ItemId.Snowshoes,    new("Snowshoes",      ItemKind.Tool,     3.0f, IconDir + "snowshoes.png",    ValueMb: 1.0f) },
        { ItemId.Axe,          new("Axe",            ItemKind.Tool,     4.0f, IconDir + "axe.png",          ValueMb: 1.5f) },
        { ItemId.PackBasket,   new("Pack basket",    ItemKind.Tool,     3.0f, IconDir + "pack_basket.png",  ValueMb: 0.8f, Capacity: 30f) },
        { ItemId.Hare,         new("Hare",           ItemKind.Food,     0.8f, IconDir + "hare.png",         Nutrition: 16f, ValueMb: 0.05f) },
        { ItemId.Fish,         new("Fish",           ItemKind.Food,     1.0f, IconDir + "fish.png",         Nutrition: 22f, ValueMb: 0.1f) },
        { ItemId.Musket,       new("Trade musket",   ItemKind.Weapon,   9.0f, IconDir + "musket.png",       ValueMb: 12f, Damage: 55f, Range: 340f, Cooldown: 4f) },
        { ItemId.Bow,          new("Bow",            ItemKind.Weapon,   2.0f, IconDir + "bow.png",          ValueMb: 2f,  Damage: 30f, Range: 260f, Cooldown: 1.6f) },
        { ItemId.Knife,        new("Knife",          ItemKind.Weapon,   0.5f, IconDir + "knife.png",        ValueMb: 0.5f, Damage: 18f, Range: 60f, Cooldown: 0.7f) },
        { ItemId.Tomahawk,     new("Tomahawk",       ItemKind.Weapon,   2.0f, IconDir + "tomahawk.png",     ValueMb: 1.5f, Damage: 28f, Range: 70f, Cooldown: 1.0f) },
    };

    // Many of these are mass nouns or have an odd plural, and "5 dried gooses"
    // reads like a machine wrote it.
    private static readonly Dictionary<ItemId, string> PluralNames = new()
    {
        { ItemId.Pemmican, "pemmican" }, { ItemId.DriedGoose, "dried geese" }, { ItemId.Goose, "geese" },
        { ItemId.SaltGeese, "casks of salt geese" },
        { ItemId.Bread, "loaves of bread" }, { ItemId.Ale, "tankards of ale" }, { ItemId.Rum, "drams of rum" },
        { ItemId.Water, "water" }, { ItemId.Venison, "fresh meat" }, { ItemId.DriedMeat, "dried meat" },
        { ItemId.Firewood, "firewood" }, { ItemId.TradeGoods, "trade goods" }, { ItemId.Fish, "fish" },
        { ItemId.Knife, "knives" }, { ItemId.Flint, "flints and steels" }, { ItemId.FishingLine, "fishing line" },
        { ItemId.Snowshoes, "pairs of snowshoes" }, { ItemId.ShotPelt, "shot-through pelts" },
    };

    // "a beaver pelt" / "8 beaver pelts" / "5 dried geese", lower case.
    public static string Lot(ItemId id, int n)
    {
        string one = Defs[id].Name.ToLower();
        if (n == 1) return one;
        return n + " " + (PluralNames.TryGetValue(id, out string many) ? many : one + "s");
    }

    public static ItemDef Of(ItemId id) => Defs[id];
    public static bool IsFood(ItemId id) => Defs[id].Kind == ItemKind.Food;
}

// Stacks keep their insertion order so the hotbar's slots stay put while
// counts change underneath them. Also used as a stockpile/ledger.
public class Inventory
{
    private readonly Dictionary<ItemId, int> _stacks = new();
    private readonly List<ItemId> _order = new();

    public int Count(ItemId id) => _stacks.GetValueOrDefault(id);
    public bool Has(ItemId id) => Count(id) > 0;

    public void Add(ItemId id, int n = 1)
    {
        if (n <= 0) return;
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

    // Take up to n; returns how many actually came out.
    public int TakeUpTo(ItemId id, int n)
    {
        int got = System.Math.Min(n, Count(id));
        if (got > 0) Remove(id, got);
        return got;
    }

    public void Clear() { _stacks.Clear(); _order.Clear(); }

    public int SlotCount => _order.Count;

    public (ItemId id, int count)? Slot(int i) =>
        i >= 0 && i < _order.Count ? (_order[i], _stacks[_order[i]]) : null;

    public float TotalWeightLbs =>
        _stacks.Sum(kv => Items.Of(kv.Key).WeightLbs * kv.Value);

    public float TotalValueMb =>
        _stacks.Sum(kv => Items.Of(kv.Key).ValueMb * kv.Value);

    // Kit carried in the pack rather than on the body (design doc 12.5's
    // worn / carried states). Clothing and snowshoes are worn unless taken off.
    public readonly HashSet<ItemId> Unworn = new();
    public bool IsWorn(ItemId id) => Has(id) && !Unworn.Contains(id) && (Items.Of(id).Kind == ItemKind.Clothing || id == ItemId.Snowshoes);
    public bool Wearable(ItemId id) => Items.Of(id).Kind == ItemKind.Clothing || id == ItemId.Snowshoes;
    public void ToggleWorn(ItemId id) { if (!Unworn.Remove(id)) Unworn.Add(id); }

    // Sum of everything worn. Layers stack, which is how the real kit worked.
    public float ClothingWarmth =>
        _stacks.Where(kv => Items.Of(kv.Key).Kind == ItemKind.Clothing && !Unworn.Contains(kv.Key))
               .Sum(kv => Items.Of(kv.Key).Warmth * kv.Value);

    // Put the slot at `from` where `to` is: the hotbar is the first nine.
    public void MoveSlot(int from, int to)
    {
        if (from < 0 || from >= _order.Count || to < 0 || to >= _order.Count || from == to) return;
        var id = _order[from]; _order.RemoveAt(from); _order.Insert(to, id);
    }

    // Extra carrying capacity from packs worn (only one of each counts).
    public float CarryBonusLbs
    {
        get { float b = 0f; foreach (var id in _order) b += Items.Of(id).Capacity; return b; }
    }

    public ItemId? FirstOfKind(ItemKind kind)
    {
        foreach (var id in _order) if (Items.Of(id).Kind == kind) return id;
        return null;
    }

    public int CountOfKind(ItemKind kind) =>
        _stacks.Where(kv => Items.Of(kv.Key).Kind == kind).Sum(kv => kv.Value);

    public float FoodNutritionTotal =>
        _stacks.Where(kv => Items.Of(kv.Key).Kind == ItemKind.Food).Sum(kv => Items.Of(kv.Key).Nutrition * kv.Value);

    // Best weapon carried by damage; null if unarmed.
    public ItemId? BestWeapon()
    {
        ItemId? best = null; float bestDmg = 0f;
        foreach (var id in _order)
        {
            var d = Items.Of(id);
            if (d.Kind == ItemKind.Weapon && d.Damage > bestDmg) { best = id; bestDmg = d.Damage; }
        }
        return best;
    }

    public IEnumerable<(ItemId id, int count)> All() =>
        _order.Select(id => (id, _stacks[id]));

    public string Summary()
    {
        return string.Join(", ", _order.Select(id => $"{Items.Of(id).Name} x{_stacks[id]}"));
    }
}
