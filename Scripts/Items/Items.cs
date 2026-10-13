using System.Collections.Generic;
using System.Linq;

namespace IsometricFurTradeGame.Scripts.Items;

public enum ItemId
{
    // Food and drink
    Pemmican,
    DriedGoose,
    SaltGeese,
    Berries,
    SpruceBeer,
    Corn,
    // The plains robe (design doc 13.6, 16.1): a winter buffalo hide dressed
    // with the hair on. Warmth first, and a trade good second.
    BuffaloRobe,
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
    // Added 2026-10-11 (kept at the end: keepsakes save item numbers).
    Ptarmigan,
    Tobacco,
    Beads,
    Awl,
    Stroud,
    MbToken,
    Powder,
    IceChisel,
    FishingNet,
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
        // What actually answered the scurvy in this country (design doc 13.5):
        // berries in their season, and spruce beer brewed at the post all
        // winter. Corn from the Missouri towns is food and trade, not physic.
        { ItemId.Berries,      new("Berries",        ItemKind.Food,     0.6f, IconDir + "berries.png",      Nutrition: 10f, ValueMb: 0.1f) },
        { ItemId.SpruceBeer,   new("Spruce beer",    ItemKind.Drink,    2.0f, IconDir + "spruce_beer.png",  Nutrition: 4f, Thirst: 40f, Warmth: 3f, ValueMb: 0.15f) },
        { ItemId.Corn,         new("Indian corn",    ItemKind.Food,     1.0f, IconDir + "corn.png",         Nutrition: 32f, ValueMb: 0.25f) },
        // A robe is a bed and a coat at once, and eight pounds to carry.
        { ItemId.BuffaloRobe,  new("Buffalo robe",   ItemKind.Clothing, 8.0f, IconDir + "caribou_hide.png", Warmth: 8f, ValueMb: 2f) },
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
        // The bird's own picture until it has an icon of its own.
        { ItemId.Ptarmigan,    new("Ptarmigan",      ItemKind.Food,     0.8f, "res://assets/sprites/animals/ptarmigan/death_se_0.png", Nutrition: 14f, ValueMb: 0.05f) },
        // The goods the trade actually ran on (the HBC Standard of Trade):
        // a pound of Brazil tobacco, a bunch of beads, an awl, a yard of
        // stroud, and the brass token a Made Beaver was counted in. Tobacco
        // is smoked before every trade (TradeTripGoal).
        { ItemId.Tobacco,      new("Twist of tobacco", ItemKind.Goods,  1.0f, IconDir + "tobacco.png",      ValueMb: 1f) },
        { ItemId.Beads,        new("Bunch of beads", ItemKind.Goods,    0.5f, IconDir + "beads.png",        ValueMb: 0.5f) },
        { ItemId.Awl,          new("Awl",            ItemKind.Goods,    0.1f, IconDir + "awl.png",          ValueMb: 0.1f) },
        { ItemId.Stroud,       new("Yard of stroud", ItemKind.Goods,    1.5f, IconDir + "stroud.png",       ValueMb: 1.5f) },
        { ItemId.MbToken,      new("Made-beaver token", ItemKind.Goods, 0.05f, IconDir + "mb_token.png",    ValueMb: 1f) },
        // A horn of powder and a bag of ball: about twenty charges.
        { ItemId.Powder,       new("Powder and ball", ItemKind.Goods,   2.0f, IconDir + "powder_horn.png",  ValueMb: 1f) },
        // Ice fishing (FishGoal): a net takes more than a line; a chisel
        // keeps the hole open and the men at it longer.
        { ItemId.IceChisel,    new("Ice chisel",     ItemKind.Tool,     4.0f, IconDir + "ice_chisel.png",   ValueMb: 1f) },
        { ItemId.FishingNet,   new("Fishing net",    ItemKind.Tool,     6.0f, IconDir + "fishing_net.png",  ValueMb: 2f) },
    };

    // Many of these are mass nouns or have an odd plural, and "5 dried gooses"
    // reads like a machine wrote it.
    private static readonly Dictionary<ItemId, string> PluralNames = Plurals(new (ItemId, string)[]
    {
        (ItemId.Pemmican, "pemmican"), (ItemId.DriedGoose, "dried geese"), (ItemId.Goose, "geese"),
        (ItemId.SaltGeese, "casks of salt geese"),
        (ItemId.Berries, "berries"), (ItemId.SpruceBeer, "spruce beer"), (ItemId.Corn, "corn"),
        (ItemId.BuffaloRobe, "buffalo robes"),
        (ItemId.Ptarmigan, "ptarmigan"), (ItemId.Tobacco, "twists of tobacco"), (ItemId.Beads, "bunches of beads"),
        (ItemId.Awl, "awls"), (ItemId.Stroud, "yards of stroud"), (ItemId.MbToken, "made-beaver tokens"),
        (ItemId.Powder, "powder and ball"), (ItemId.IceChisel, "ice chisels"), (ItemId.FishingNet, "fishing nets"),
        (ItemId.Bread, "loaves of bread"), (ItemId.Ale, "tankards of ale"), (ItemId.Rum, "drams of rum"),
        (ItemId.Water, "water"), (ItemId.Venison, "fresh meat"), (ItemId.DriedMeat, "dried meat"),
        (ItemId.Firewood, "firewood"), (ItemId.TradeGoods, "trade goods"), (ItemId.Fish, "fish"),
        (ItemId.Knife, "knives"), (ItemId.Flint, "flints and steels"), (ItemId.FishingLine, "fishing line"),
        (ItemId.Snowshoes, "pairs of snowshoes"), (ItemId.ShotPelt, "shot-through pelts"),
    });

    private static Dictionary<ItemId, string> Plurals((ItemId Id, string Many)[] rows)
    {
        var d = new Dictionary<ItemId, string>();
        foreach (var (id, many) in rows) d[id] = many;   // a repeat wins; it does not throw
        return d;
    }

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

// Stacks sit in fixed slots (Minecraft style): emptying a slot leaves a gap
// rather than shuffling the rest, a new stack takes the first gap, and the
// first nine slots are the hotbar. Also used as a stockpile/ledger.
public class Inventory
{
    private readonly Dictionary<ItemId, int> _stacks = new();
    private readonly List<ItemId?> _order = new();   // null = an empty slot

    public int Count(ItemId id) => _stacks.GetValueOrDefault(id);
    public bool Has(ItemId id) => Count(id) > 0;

    public void Add(ItemId id, int n = 1)
    {
        if (n <= 0) return;
        if (!_stacks.ContainsKey(id)) PlaceInFirstGap(id);
        _stacks[id] = Count(id) + n;
    }

    private void PlaceInFirstGap(ItemId id)
    {
        int gap = _order.IndexOf(null);
        if (gap >= 0) _order[gap] = id; else _order.Add(id);
    }

    // Add a stack at a chosen slot (restoring a layout); falls back to the first gap.
    public void AddAt(int slot, ItemId id, int n)
    {
        if (n <= 0) return;
        if (_stacks.ContainsKey(id)) { _stacks[id] += n; return; }
        if (slot < 0 || slot > 512 || (slot < _order.Count && _order[slot] != null)) { Add(id, n); return; }
        while (_order.Count <= slot) _order.Add(null);
        _order[slot] = id; _stacks[id] = n;
    }

    public bool Remove(ItemId id, int n = 1)
    {
        if (n <= 0) return true;   // nothing to take (a debtor paying nothing)
        if (Count(id) < n) return false;
        _stacks[id] -= n;
        if (_stacks[id] == 0)
        {
            _stacks.Remove(id);
            int at = _order.IndexOf(id);
            if (at >= 0) _order[at] = null;
            while (_order.Count > 0 && _order[^1] == null) _order.RemoveAt(_order.Count - 1);
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

    public int SlotCount => _stacks.Count;       // stacks held
    public int SlotSpan => _order.Count;         // one past the last used slot

    public (ItemId id, int count)? Slot(int i) =>
        i >= 0 && i < _order.Count && _order[i] != null ? (_order[i].Value, _stacks[_order[i].Value]) : null;

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

    // Drag a stack from one slot to another: an empty slot takes it, an
    // occupied one swaps places. The hotbar is the first nine slots.
    public void MoveSlot(int from, int to)
    {
        if (from < 0 || from >= _order.Count || _order[from] == null || to < 0 || to > 512 || from == to) return;
        while (_order.Count <= to) _order.Add(null);
        (_order[from], _order[to]) = (_order[to], _order[from]);
        while (_order.Count > 0 && _order[^1] == null) _order.RemoveAt(_order.Count - 1);
    }

    // Slot index of a stack, or -1.
    public int SlotOf(ItemId id) => _order.IndexOf(id);

    // Extra carrying capacity from packs worn (only one of each counts).
    public float CarryBonusLbs
    {
        get { float b = 0f; foreach (var id in _stacks.Keys) b += Items.Of(id).Capacity; return b; }
    }

    public ItemId? FirstOfKind(ItemKind kind)
    {
        foreach (var id in _order) if (id != null && Items.Of(id.Value).Kind == kind) return id;
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
        foreach (var oid in _order)
        {
            if (oid == null) continue;
            var id = oid.Value;
            var d = Items.Of(id);
            if (d.Kind == ItemKind.Weapon && d.Damage > bestDmg) { best = id; bestDmg = d.Damage; }
        }
        return best;
    }

    public IEnumerable<(ItemId id, int count)> All() =>
        _order.Where(id => id != null).Select(id => (id.Value, _stacks[id.Value]));

    public string Summary()
    {
        return string.Join(", ", All().Select(s => $"{Items.Of(s.id).Name} x{s.count}"));
    }
}
