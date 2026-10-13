---
title: Outpost Ledger
aliases: [OutpostLedger, Oxford House Ledger]
tags: [system, economy, hbc, ledger]
status: implemented
---

# Outpost Ledger

**Status:** Implemented (`Scripts/Economy/OutpostLedger.cs`).

![[ui-crest-hbc.png|100]]

[[Oxford House]] is the Company's inland post. It keeps the same books as [[Fort Ledger|York Factory]], with a postmaster instead of a Factor, no ship and no London of its own: its orders and its outfit come up the river road from the Bay, and its furs and reports go down it.

## Settings

| Setting | Value |
|---|---|
| PeltPrice | 1.0 (copies York's when the Factor's orders are read) |
| Quota | 40 MB |
| Known sites | Beaver lodge (Knee Lake), Beaver lodge (Oxford), Caribou herd |
| Standing orders | 'Hold the post, trade fairly, send the furs down before the ship.' |

## Starting stores

[[Pemmican]] 30, [[Dried Goose]] 30, [[Firewood]] 30, [[Trade Goods (item)]] 50, [[Trade Musket]] 2, [[Steel Trap]] 8, [[Copper Kettle]] 1, [[Water]] 10, [[Axe]] 2, [[Snowshoes]] 2, [[Fishing Line]] 3, [[Beaver Pelt]] 8.

## The inland bands

Bands trade here abstractly, week by week, from design doc 18.2's background tier. Each week (except in breakup, July and August when they are at the fisheries):

- A party wants `(2 + 5 x PeltPrice) x random(0.6 to 1.4)` MB of fur and pays `round(want x PeltPrice)` pieces of goods.
- If the shelves cannot cover it the party goes 'on to the Canadians' and the post's `LostToRivalMb` grows by the amount.
- Otherwise the post pays the goods, receives that many beaver pelts, logs 'an inland party traded N beaver at Oxford House for M pieces', and may hand over a dram of rum at the rum-share chance.
- If food is under 14 days, the postmaster spends up to 10 goods on provisions: 20 pemmican and 10 dried meat. If there is neither food nor goods, a notice says so.

## The brigade

York sends the **autumn outfit** upriver on or after 18 September: 40 trade goods, 2 muskets, 6 point blankets, 6 steel traps, 1 kettle, 4 rum and 6 casks of salt geese. In spring, from 8 June, the **spring returns** come down to York: 60 beaver pelts, 10 wolf pelts, 8 caribou hides, 10 summer pelts. See [[Brigade]].

The Factor can also order two labourers to carry a smaller outfit (20 goods, 10 pemmican, 3 rum, 4 traps in one canoe; 15 goods, 12 dried goose, 1 musket, 4 blankets in the other) with a letter of the winter's orders, after the ship sails. The postmaster reads it, which sets the post's price to York's.

## Reports

The post sends the Bay a report on its furs, food-days and men; York's ledger holds what it believes from the last report (`OutpostBelievedFursMb`), and notes when the post has no master. See [[Information and Messages]].

## See also

- [[Fort Ledger]]
- [[Trading Post Ledger]]
- [[Economy]]
- [[Postmaster]]
- [[Oxford House]]
