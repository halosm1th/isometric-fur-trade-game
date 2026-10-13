---
title: Trading Post Ledger
aliases: [TradingPostLedger, Country Post Ledger]
tags: [system, economy, ledger]
status: implemented
---

# Trading Post Ledger

**Status:** Implemented (`Scripts/Economy/TradingPostLedger.cs`).

![[ui-crest-hbc.png|90]] ![[ui-crest-nwc.png|90]]

The books of one of the wider country's posts, such as [[Cumberland House]] or [[Fort William]] (see [[All Trading Posts]]). It is the same ledger as York Factory's, but the country around it is not drawn: the nets, the post's own hunters and the bands that trade there come and go as a weekly sum (design doc 18.2's background tier). A master and two men a thousand miles off neither starve nor walk to York for meat.

Which company it belongs to is read from the post: `Company` returns the post's faction (HBC by default).

## Settings

| Setting | Value |
|---|---|
| PeltPrice | 1.0 |
| Quota | 25 MB |
| Standing orders | 'Hold the post, keep the Indians in your interest, and send the returns out with the brigade.' |

## Starting stores

[[Pemmican]] 25, [[Dried Meat]] 20, [[Firewood]] 25, [[Trade Goods (item)]] 40, [[Twist of Tobacco]] 8, [[Water]] 10, [[Axe]] 1, [[Fishing Line]] 2, [[Beaver Pelt]] 4.

## Weekly sum

Each week, if the post has any mouths:
- **Food.** Adds dried meat: 5 per man in winter (when the country is frozen), 7 otherwise; in winter also 1 pemmican per man. Fresh would 'spoil on the books before anyone ate it', so only dried food is added.
- **Firewood.** If under 20, adds 6.
- **Trade.** From October through May, if there are more than 2 trade goods in store: 0 to 3 pelts come in, each costing one piece of goods (up to what is in stock); adds `FursTakenMb`.

There is no ship, no player-visible counter, and no London.

## See also

- [[Fort Ledger]]
- [[Outpost Ledger]]
- [[Economy]]
- [[Simulation Tiers]]
- [[All Trading Posts]]
