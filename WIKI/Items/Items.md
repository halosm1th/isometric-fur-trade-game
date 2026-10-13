---
title: Items
aliases: [Item List, All Items]
tags: [item, index]
status: implemented
---

# Items

**Status:** Implemented

![[sheet-items-all.png|600]]

Every physical object in the game is an `ItemId` defined in `Scripts/Items/Items.cs`: a name, a kind, a weight in pounds, a worth in [[Made Beaver]] (MB) and a few effect numbers. There are 48 items in six kinds, each with 64x64 icon art. Inventories everywhere (the player's pack, a post's stores, a canoe's cargo, a band's cache) are the same `Inventory` object: counts by item id, no stack cap, with the first nine slots on the hotbar. See [[Inventory and Hotbar]].

## Categories

| Category | Items | What they are |
|---|---|---|
| [[Weapons]] | 4 | Musket, bow, knife, tomahawk |
| [[Tools]] | 11 | Traps, lines, nets, packs, snowshoes, axe, kettle, flint, firewood |
| [[Food and Drink]] | 16 | Pemmican, geese, game, ale, rum, water, berries, corn |
| [[Clothing]] | 4 | Capote, blankets, hide coat, buffalo robe |
| [[Trade Goods]] | 7 | Tobacco, beads, awl, stroud, powder, tokens, the general 'piece' |
| [[Furs and Pelts]] | 6 | Beaver, wolf, caribou and damaged hides |
| [[Documents]] | 0 | Letters, ledgers, journal: **no ItemId yet** (cut content) |

## All items

Weight in pounds; worth in Made Beaver. The table can be re-sorted with any Obsidian table-sorting plugin; rows are grouped by category and then A to Z.

| Item | Category | Weight (lb) | Worth (MB) | Key stat |
|---|---|---|---|---|
| [[Bow]] | Weapons | 2 | 2 | Damage 30, reach 260, cooldown 1.6s (about 18.8 dps) |
| [[Knife]] | Weapons | 0.5 | 0.5 | Damage 18, reach 60, cooldown 0.7s (about 25.7 dps) |
| [[Tomahawk]] | Weapons | 2 | 1.5 | Damage 28, reach 70, cooldown 1s (about 28.0 dps) |
| [[Trade Musket]] | Weapons | 9 | 12 | Damage 55, reach 340, cooldown 4s (about 13.8 dps) |
| [[Axe]] | Tools | 4 | 1.5 | - |
| [[Copper Kettle]] | Tools | 3 | 2 | - |
| [[Firewood]] | Tools | 5 | 0 | Fuel: 3 hours on a hearth |
| [[Fishing Line]] | Tools | 0.5 | 0.5 | - |
| [[Fishing Net]] | Tools | 6 | 2 | - |
| [[Flint and Steel]] | Tools | 0.3 | 0.5 | - |
| [[Ice Chisel]] | Tools | 4 | 1 | - |
| [[Pack Basket]] | Tools | 3 | 0.8 | Carry capacity +30 lb |
| [[Snowshoes]] | Tools | 3 | 1 | - |
| [[Steel Trap]] | Tools | 4 | 1.5 | - |
| [[Tumpline]] | Tools | 1 | 0.5 | Carry capacity +40 lb |
| [[Berries]] | Food and Drink | 0.6 | 0.1 | Eat: +10 hunger (about 0.3 person-days of food) |
| [[Cask of Salt Geese]] | Food and Drink | 60 | 1.6 | Eat: +240 hunger (about 7.1 person-days of food) |
| [[Dram of Rum]] | Food and Drink | 1 | 0.8 | Drink: +10 thirst |
| [[Dried Goose]] | Food and Drink | 1 | 0.2 | Eat: +30 hunger (about 0.9 person-days of food) |
| [[Dried Meat]] | Food and Drink | 0.8 | 0.25 | Eat: +35 hunger (about 1.0 person-days of food) |
| [[Fish]] | Food and Drink | 1 | 0.1 | Eat: +22 hunger (about 0.7 person-days of food) |
| [[Fresh Meat]] | Food and Drink | 2 | 0.15 | Eat: +30 hunger (about 0.9 person-days of food) |
| [[Goose (item)]] | Food and Drink | 1.5 | 0.15 | Eat: +35 hunger (about 1.0 person-days of food) |
| [[Hare (item)]] | Food and Drink | 0.8 | 0.05 | Eat: +16 hunger (about 0.5 person-days of food) |
| [[Indian Corn]] | Food and Drink | 1 | 0.25 | Eat: +32 hunger (about 1.0 person-days of food) |
| [[Loaf of Bread]] | Food and Drink | 1 | 0.15 | Eat: +25 hunger (about 0.7 person-days of food) |
| [[Pemmican]] | Food and Drink | 1.5 | 0.3 | Eat: +45 hunger (about 1.3 person-days of food) |
| [[Ptarmigan (item)]] | Food and Drink | 0.8 | 0.05 | Eat: +14 hunger (about 0.4 person-days of food) |
| [[Spruce Beer]] | Food and Drink | 2 | 0.15 | Eat: +4 hunger (about 0.1 person-days of food) |
| [[Tankard of Ale]] | Food and Drink | 2 | 0.2 | Eat: +6 hunger (about 0.2 person-days of food) |
| [[Water]] | Food and Drink | 2 | 0 | Drink: +50 thirst |
| [[Buffalo Robe]] | Clothing | 8 | 2 | Worn: warmth 8 (layers stack) |
| [[Capote]] | Clothing | 4 | 3 | Worn: warmth 4.5 (layers stack) |
| [[Hide Coat]] | Clothing | 5 | 2 | Worn: warmth 7 (layers stack) |
| [[Point Blanket]] | Clothing | 5 | 4 | Worn: warmth 5 (layers stack) |
| [[Awl]] | Trade Goods | 0.1 | 0.1 | - |
| [[Bunch of Beads]] | Trade Goods | 0.5 | 0.5 | - |
| [[Made-Beaver Token]] | Trade Goods | 0.05 | 1 | - |
| [[Powder and Ball]] | Trade Goods | 2 | 1 | - |
| [[Trade Goods (item)]] | Trade Goods | 3 | 1 | - |
| [[Twist of Tobacco]] | Trade Goods | 1 | 1 | - |
| [[Yard of Stroud]] | Trade Goods | 1.5 | 1.5 | - |
| [[Beaver Pelt]] | Furs and Pelts | 1.5 | 1 | - |
| [[Caribou Hide]] | Furs and Pelts | 6 | 0.5 | - |
| [[Holed Hide]] | Furs and Pelts | 6 | 0.2 | - |
| [[Shot-through Pelt]] | Furs and Pelts | 1.5 | 0.35 | - |
| [[Summer Pelt]] | Furs and Pelts | 1.2 | 0.45 | - |
| [[Wolf Pelt]] | Furs and Pelts | 3 | 0.6 | - |

## Most valuable (by worth)

| Item | Worth (MB) |
|---|---|
| [[Trade Musket]] | 12 |
| [[Point Blanket]] | 4 |
| [[Capote]] | 3 |
| [[Bow]] | 2 |
| [[Buffalo Robe]] | 2 |
| [[Copper Kettle]] | 2 |
| [[Fishing Net]] | 2 |
| [[Hide Coat]] | 2 |
| [[Cask of Salt Geese]] | 1.6 |
| [[Axe]] | 1.5 |
| [[Steel Trap]] | 1.5 |
| [[Tomahawk]] | 1.5 |

## Mechanics you should know

- **Weight and carrying:** every item has a weight; carry capacity is raised by a [[Tumpline]] (+40 lb) and a [[Pack Basket]] (+30 lb), and being overloaded slows you.
- **Worth is in Made Beaver:** one prime beaver pelt = 1 MB. See [[Made Beaver]] and [[Barter Rates]].
- **Worn gear:** clothing and snowshoes are 'worn' unless stowed; clothing warmth adds up.
- **Plural names:** the game writes 'dried geese', 'casks of salt geese', 'pairs of snowshoes' and so on from a plural table.
- **Naming collisions:** pages for the items [[Goose (item)]], [[Hare (item)]], [[Ptarmigan (item)]] and [[Trade Goods (item)]] carry '(item)' to keep them apart from the animal and category pages.
- **Documents:** letters and dispatches exist in the message system but not as inventory items; see [[Documents]].

## See also

- [[Economy]]
- [[Glossary]]
- [[Asset Inventory]]
