using System.Collections.Generic;
using Godot;
using IsometricFurTradeGame.Scripts.Agents;
using IsometricFurTradeGame.Scripts.Economy;
using IsometricFurTradeGame.Scripts.Items;
using IsometricFurTradeGame.Scripts.World;

namespace IsometricFurTradeGame.Scripts.UI;

// A town merchant's counter (Economy/Money): the town's stock for sale at
// the market's price, and whatever you carry bought at the merchants' price
// - furs above all, which is what a Detroit or Michilimackinac merchant was
// in business for - as far as the till will stretch. Paid in coin, carried
// in your own pack.
public partial class Conversation
{
    private static TownHousehold MarketOf(Npc npc) => (npc.HomeStores as TownHousehold)?.Town?.Market;

    private string Purse(Town t) => Money.Format(_player.Inventory.Count(ItemId.Coin), t);

    private void ShowStore(Npc npc, string said = null)
    {
        var market = MarketOf(npc);
        if (market == null) { ShowTopics(Greeting(npc)); return; }
        var t = market.Town;
        _text = (said != null ? said + "\n\n" : "") + $"{npc.DisplayName}'s counter at {t.Name}. You have {Purse(t)}; the till has {Money.Format(market.Stores.Count(ItemId.Coin), t)}.";
        ClearOptions();
        AddOption("Buy...", () => ShowStoreBuy(npc));
        AddOption("Sell...", () => ShowStoreSell(npc));
        AddOption("Back", () => ShowTopics(Greeting(npc)));
    }

    private void ShowStoreBuy(Npc npc, string said = null)
    {
        var market = MarketOf(npc); var t = market.Town;
        _text = (said ?? "What will you have?") + $" You have {Purse(t)}.";
        ClearOptions();
        var stock = new List<(ItemId id, int n)>();
        foreach (var (id, n) in market.Stores.All()) if (id != ItemId.Coin && id != ItemId.Water && n > 0) stock.Add((id, n));
        stock.Sort((a, b) => Money.Price(market, a.id).CompareTo(Money.Price(market, b.id)));
        foreach (var (id, n) in stock)
        {
            var item = id; int price = Money.Price(market, id);
            AddOption($"{Items.Items.Of(id).Name} - {Money.Format(price, t)} ({n} in store)", () =>
            {
                if (_player.Inventory.Count(ItemId.Coin) < price) { ShowStoreBuy(npc, $"\"That's {Money.Format(price, t)}, and you haven't it.\""); return; }
                if (!market.Stores.Remove(item)) { ShowStoreBuy(npc, "\"Sold out, I'm afraid.\""); return; }
                _player.Inventory.Remove(ItemId.Coin, price); market.Stores.Add(ItemId.Coin, price);
                _player.Inventory.Add(item);
                ShowStoreBuy(npc, $"You buy {Items.Items.Of(item).Name.ToLower()} for {Money.Format(price, t)}.");
            });
        }
        if (stock.Count == 0) _text = "\"There's nothing on the shelves.\"";
        AddOption("Back", () => ShowStore(npc));
    }

    private void ShowStoreSell(Npc npc, string said = null)
    {
        var market = MarketOf(npc); var t = market.Town;
        _text = (said ?? "\"Let's see what you have.\"") + $" You have {Purse(t)}; the till has {Money.Format(market.Stores.Count(ItemId.Coin), t)}.";
        ClearOptions();
        // Furs first - it is what the merchant is there for - then the dearest.
        var mine = new List<(ItemId id, int n)>();
        foreach (var (id, n) in _player.Inventory.All())
        {
            if (id is ItemId.Coin or ItemId.Water || n <= 0) continue;
            if (Items.Items.Of(id).Kind is ItemKind.Weapon || _player.Weapon == id) continue;   // a man's gun is not for sale over a counter
            if (_player.Inventory.IsWorn(id)) continue;                                          // nor the coat on his back
            mine.Add((id, n));
        }
        mine.Sort((a, b) => (Items.Items.Of(b.id).Kind == ItemKind.Fur).CompareTo(Items.Items.Of(a.id).Kind == ItemKind.Fur) is var f and not 0 ? f : Money.BuyPrice(market, b.id).CompareTo(Money.BuyPrice(market, a.id)));
        foreach (var (id, n) in mine)
        {
            var item = id; int price = Money.BuyPrice(market, id);
            AddOption($"Sell {(n > 1 ? "one " : "")}{Items.Items.Of(id).Name.ToLower()} - {Money.Format(price, t)}{(n > 1 ? $" (you have {n})" : "")}", () => SellToStore(npc, item, 1));
            if (n > 1) AddOption($"   ...all {n} - {Money.Format(price * n, t)}", () => SellToStore(npc, item, n));
        }
        AddOption("Back", () => ShowStore(npc));
    }

    private void SellToStore(Npc npc, ItemId id, int n)
    {
        var market = MarketOf(npc); var t = market.Town;
        int price = Money.BuyPrice(market, id);
        int can = Mathf.Min(n, market.Stores.Count(ItemId.Coin) / Mathf.Max(1, price));
        if (can <= 0) { ShowStoreSell(npc, "\"I've not the money in the till for that today.\""); return; }
        market.Stores.Add(id, _player.Inventory.TakeUpTo(id, can));
        _player.Inventory.Add(ItemId.Coin, market.Stores.TakeUpTo(ItemId.Coin, can * price));
        npc.ChangeRapport(_player, 0.5f);
        ShowStoreSell(npc, $"You sell {(can > 1 ? Items.Items.Lot(id, can) : Items.Items.Of(id).Name.ToLower())} for {Money.Format(can * price, t)}." + (can < n ? " The till won't stretch to the rest." : ""));
    }
}
