---
title: Fort Ledger
aliases: [FortLedger, York Factory Ledger, The Company's Stores]
tags: [system, economy, hbc, ledger]
status: implemented
---

# Fort Ledger

**Status:** Implemented (`Scripts/Economy/FortLedger.cs`). It is the base class for the other post ledgers.

![[ui-crest-hbc.png|110]]

The Fort Ledger is [[York Factory]]'s book: the warehouse and the provisions store as an `Inventory`, plus the numbers London wants. 'Prices are in Made Beaver.' All other post ledgers ([[Outpost Ledger]], [[Trading Post Ledger]], [[NWC Ledger]]) extend it.

## What it holds

| Field | Starting value | Meaning |
|---|---|---|
| PeltPrice | 1.0 | Goods paid per MB of fur |
| LondonIncentiveNext | 1.0 | What London will send next ship |
| QuotaMb / QuotaNextMb | 60 / 60 | Returns London expects |
| FoodPriceMultiplier | 1.5 | Trade goods per MB of food when a band buys provisions |
| RumShare | 0.15 | Share of a fur payment paid as drink |
| FursTakenMb, GoodsPaidMb | 0 | Year's running totals (reset when the ship sails) |
| LostToRivalMb | 0 | Furs the band carried to the other company |
| SkimmedMb | 0 | Furs a shading Factor set aside for himself |
| LotsLostToRaids, LastRaidedDay | 0, none | The rivalry's tally |

Also stored: assignments the leader has given out, the Tier 2 directive text and when it was read, what the Bay post believes about its inland post, and which wildlife sites the post knows (the 'Goose marsh' and 'Beaver lodge (upper)' at the start).

## Starting stores

What a post holds at midwinter, after a ship in August:

| Group | Items |
|---|---|
| Food | [[Pemmican]] 60, [[Dried Goose]] 120, [[Loaf of Bread]] 30, [[Tankard of Ale]] 20, [[Water]] 24 |
| Fuel | [[Firewood]] 40 |
| Trade | [[Trade Goods (item)]] 90, [[Twist of Tobacco]] 30, [[Bunch of Beads]] 20, [[Awl]] 24, [[Yard of Stroud]] 12, [[Powder and Ball]] 20, [[Point Blanket]] 12 |
| Arms and tools | [[Trade Musket]] 6, [[Steel Trap]] 10, [[Copper Kettle]] 3, [[Axe]] 4, [[Pack Basket]] 2, [[Tumpline]] 4, [[Snowshoes]] 4 |
| Fishing | [[Fishing Line]] 6, [[Fishing Net]] 2, [[Ice Chisel]] 3 |
| Drink | [[Dram of Rum]] 12 |
| Furs | [[Beaver Pelt]] 22, [[Caribou Hide]] 4 |

## What it does

- **Buys furs.** `BuyFurs` pays in trade goods at the PeltPrice, takes the fur into the store, and records `FursTakenMb` and `GoodsPaidMb`. If the band has debt it is worked down first. It stops when it runs out of goods ('the post is bought out'). See [[Fur Prices]] and [[Credit and Debt]].
- **Pays part in rum**, at the RumShare. Rum raises the band's rum taste.
- **Sells food** to bands: 5 units on credit if standing exceeds 40 and debt is under 15.
- **Buys provisions** from bands that have them, two pemmican for one piece of goods; once a week the master looks to buy meat when the store has under 25 food-days, and says so if nobody is selling.
- **Counts the furs** in the hold for the quota (`FursInStoreMb`).
- **Is raided.** [[Rivalry]]'s `Plunder` carries off pemmican, dried goose, dried meat, rum, trade goods, beaver pelts, bread and muskets in that order.
- **Takes a clerk's drawings.** A clerk can draw one musket (standing 15), up to 2 steel traps, an axe, snowshoes, a point blanket (standing 10), a kettle, a tumpline, or 4 trade goods (rank clerk), and a two-day ration.

## The year

The hold sums to the returns at sailing; the ship notice reads 'N MB of furs aboard (quota was Q)'. After that `FursTakenMb` and `GoodsPaidMb` reset. The player's yearly account is built from the same figures (see [[London Committee Orders]]). Furs come down from [[Oxford House]] with the spring brigade; the autumn outfit goes up.

> [!tip] Keep the shelves full
> A band that arrives with furs and finds nothing to buy with them goes to the Canadians. At Oxford House that is logged as 'the shelves bare; they went on to the Canadians', and the lost furs are counted against the post.

## Planned

A solvency ledger with capital, credit and unsold stock, as in design doc 13.7, does not exist: there is no money, only goods and furs.

## See also

- [[Economy]]
- [[Outpost Ledger]]
- [[Trading Post Ledger]]
- [[NWC Ledger]]
- [[Band Ledger]]
- [[London Committee Orders]]
- [[Factor]]
