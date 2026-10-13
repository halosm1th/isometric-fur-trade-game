---
title: Made Beaver
aliases: [MB, Made-Beaver, Beaver Standard]
tags: [system, economy, currency]
status: implemented
---

# Made Beaver

**Status:** Implemented as the unit of worth for every item; the physical token and the formal London tally are partly planned.

![[item-mb_token.png|96]]

The *Made Beaver* (MB) was the Hudson's Bay Company's unit of account: the worth of one prime ("made") beaver pelt in good condition. A hunter was paid, and a trade good was priced, in how many made beaver it was worth. In the game this is the number in the `ValueMb` field of every item. A prime [[Beaver Pelt]] is exactly 1 MB; a [[Trade Musket]] is 12.

## Worth of every item

Values come straight from `Items.cs`. Worth is per piece; weight per piece.

| MB | Items |
|---|---|
| 12 | [[Trade Musket]] |
| 4 | [[Point Blanket]] |
| 3 | [[Capote]] |
| 2 | [[Bow]], [[Buffalo Robe]], [[Hide Coat]], [[Copper Kettle]], [[Fishing Net]] |
| 1.6 | [[Cask of Salt Geese]] (60 lb) |
| 1.5 | [[Steel Trap]], [[Axe]], [[Tomahawk]], [[Yard of Stroud]] |
| 1 | [[Beaver Pelt]], [[Trade Goods (item)]], [[Twist of Tobacco]], [[Made-Beaver Token]], [[Powder and Ball]], [[Snowshoes]], [[Ice Chisel]] |
| 0.8 | [[Dram of Rum]], [[Pack Basket]] |
| 0.6 | [[Wolf Pelt]] |
| 0.5 | [[Caribou Hide]], [[Knife]], [[Flint and Steel]], [[Fishing Line]], [[Tumpline]], [[Bunch of Beads]] |
| 0.45 | [[Summer Pelt]] |
| 0.35 | [[Shot-through Pelt]] |
| 0.3 | [[Pemmican]] |
| 0.25 | [[Dried Meat]], [[Indian Corn]] |
| 0.2 | [[Dried Goose]], [[Tankard of Ale]], [[Holed Hide]] |
| 0.15 | [[Spruce Beer]], [[Fresh Meat]], [[Goose (item)]], [[Loaf of Bread]] |
| 0.1 | [[Berries]], [[Fish]], [[Awl]] |
| 0.05 | [[Hare (item)]], [[Ptarmigan (item)]] |
| 0 | [[Water]], [[Firewood]] |

## Where MB is used

- **Fur counted in MB.** A post's *furs in store* is the sum of fur items times their worth. The quota London sets (60 MB at York) is measured against the MB of furs in the hold when the ship sails. See [[Fort Ledger]].
- **Price per MB.** A company's `PeltPrice` is *goods per MB of fur*. At 1.0 a prime pelt buys one [[Trade Goods (item)]]; at 1.3 (the Nor'Westers) it buys 1.3. See [[Fur Prices]].
- **Debt in MB.** Bands owe a house MB of goods advanced; see [[Credit and Debt]].
- **Your own account.** The player's *private worth* is the high-water mark of the MB in furs and goods carried, shown in the yearly [[The Reckoning of 1821|reckoning]] as 'what you have of your own' (it decays slowly if you spend it down). Trading on your own account is noticed by the Factor.
- **Gift value.** An opening gift adds its worth to the margin (1 percent per MB) and to the rapport earned; see [[Barter Rates]].
- **Inventory totals.** The journal and the ship's hold report `TotalValueMb`; the ship's departure notice reads 'sailed with N MB of furs aboard'.

## History and design

The real made-beaver was an accounting fiction: a standard the Company used at York Factory to compare the price of everything from a gun to a pound of tobacco against one prime pelt. The game takes the idea; its MB values are tuned for play and are not copied from a historical Standard of Trade table.

> [!info] Token or tally?
> The [[Made-Beaver Token]] is an ordinary 1 MB item (0.05 lb). The brass token a trader gave was a real thing; here it is only stock. No bank or exchange converts between MB and anything else.

## See also

- [[Economy]]
- [[Barter Rates]]
- [[Fur Prices]]
- [[Items]]
- [[Glossary]]
