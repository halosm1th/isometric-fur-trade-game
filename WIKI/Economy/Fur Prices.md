---
title: Fur Prices
aliases: [Pelt Price, Standard of Trade, PeltPrice]
tags: [system, economy, fur]
status: partial
---

# Fur Prices

**Status:** Partial. Per-post price, seasonal pelt quality, ruined pelts and London's yearly adjustment are implemented. A market that clears quality-weighted stock, and the external price shocks of design doc 13.6, are planned.

![[item-beaver_pelt.png|96]] ![[item-thin_pelt.png|96]] ![[item-wolf_pelt.png|96]]

Furs have no fixed price in goods. Every counter has a **PeltPrice**: how many trade goods it pays for one [[Made Beaver]] of fur. The fur's own worth is fixed by the item (a prime [[Beaver Pelt]] is 1 MB), so what you actually receive is `worth in MB x PeltPrice`, rounded.

## Prices by house

| House | Starting PeltPrice | Quota (MB) | Notes |
|---|---|---|---|
| York Factory | 1.0 | 60 | London sets it each year; see [[London Committee Orders]] |
| Oxford House | 1.0 | 40 | Takes York's price whenever the Factor's orders are read |
| Wider-country posts | 1.0 | 25 | Fixed in code |
| Nor'Westers' House | 1.3 | 80 | 'Usually a shade better than the Company's'; see [[NWC Ledger]] |

A price can be changed by:
- **London's packet**, when the Factor reads it: sets York's price and quota.
- **The Factor's autumn orders**, read at Oxford House: copy York's price.
- **The Fort William council:** hold the price, undercut the Company by a fifth (the NWC price becomes at least 1.2 times York's), or let the Bay partner set his own (then it is at least 1.25 times York's, up to 3).
- **Interception:** a Nor'Wester who reads London's packet sets the NWC price to at least 1.15 times London's.
- All prices are clamped to 0.5 to 3.0 (London itself clamps its next-year price to 0.3 to 3.0).

## What a fur is worth

| Fur | Worth (MB) | Pays at 1.0 | Pays at 1.3 (NWC) |
|---|---|---|---|
| [[Beaver Pelt]] (prime) | 1.0 | 1 goods | 1.3 |
| [[Wolf Pelt]] | 0.6 | 0.6 | 0.78 |
| [[Caribou Hide]] | 0.5 | 0.5 | 0.65 |
| [[Summer Pelt]] | 0.45 | 0.45 | 0.585 |
| [[Shot-through Pelt]] | 0.35 | 0.35 | 0.455 |
| [[Holed Hide]] | 0.2 | 0.2 | 0.26 |

At the counter each pelt is paid separately: the goods owed round to the nearest whole piece, and if that rounds to zero the counter still pays one piece with a chance equal to the pelt's value (so a 0.45 summer pelt pays 1 about 45 percent of the time). If the post cannot cover the goods the sale stops. 'Pays' therefore averages to the table figure over many pelts.

When a house pays in drink as well, part of the goods are handed out as rum: the Company counters have a 15 percent rum share, the Nor'Westers 35 percent (set to 20 or 40 by the council). See [[Dram of Rum]] and [[Credit and Debt]].

## Quality and seasons

- **Beaver in season.** A beaver killed from June to September (months 6 to 9) yields a [[Summer Pelt]] instead of a prime one: 0.45 MB against 1.0. Trapping season is an economic decision (design doc 14.4).
- **The ball tears the pelt.** A beaver or wolf killed with a musket gives a [[Shot-through Pelt]] (0.35 MB); a buffalo or caribou killed that way yields a [[Holed Hide]] (0.2 MB). Bow, trap or knife keep the fur whole. A buffalo killed cleanly gives a [[Buffalo Robe]] (2 MB, 8 lb).
- **Meat is separate.** A beaver also gives 3 [[Fresh Meat]]; a caribou 18; a buffalo 70.
- **Catching them.** A steel trap at a lodge catches overnight with a base 7 percent chance per trap at a full lodge, multiplied by lodge density (count over capacity, to 1.2) and `0.6 + 0.8 x skill`. Another trapper can poach the catch ([[Trapping]]).

## Price as a lever

- The **band's mood** follows the price. A band that is hungry blames the Company if its price is below 0.9 (standing -3 a day, otherwise -1); fed, it gains 1 a day at a price of 1.2 or more, and 0.3 otherwise. See [[Band Ledger]].
- Fair prices are remembered. Each fur sale to a counter with a price of 1.0 or better gives the band +2 standing with that house (+0.5 if lower).
- The **rival's price** matters: bands that find the other house paying better walk their packs across the river and the Factor hears of it (`LostToRivalMb`). See [[Rivalry]].
- A **short return** makes London raise the price next year; a fat one lets it tighten. See [[London Committee Orders]].

## The sale at home

`Economy/Companies.cs` (design doc 13.6). The furs are sold where they leave the country: the Bay's at the London sale when the ship is home, the Nor'Westers' at Montreal, the Americans' at New York and St. Louis.

What a beaver fetches there depends on four things:
- **The blockade**: the Bay's price ×0.7 and the Nor'Westers' ×0.85 until it lifts (some of their furs go out by New York); the Americans' untouched. Then 1.08.
- **The war** (0.9 for the Nor'Westers).
- **This year's volume:** above 6,000 MB sold across all the companies, the price falls. A sale counts against its own price.
- **Last years' gluts:** each year's excess carries into the next, halving yearly.

**Holding furs back.** A company with the law "hold furs off a poor sale" (`LawKind.HoldFurs`, a Committee or partners' rule) keeps that share in its warehouse whenever the sale won't pay par (0.95). It brings the lot to the first sale that pays 1.0 or more. Warehoused furs lose 6% a year. London starts with half held back, a rule made in 1808 during the blockade. A company in debt has to sell everything, warehouse and all.

**Flooding the market.** A company with 300 MB or more in the warehouse may throw everything on the sale at once. It does so when its rival is short of money, its own purse can bear a bad year, and bad blood runs at 40 or more. It beats the price down for everyone that year and the next, its own sale included. It won't do it two years running.

**Your part.** Ask your Factor or partner how the furs are selling ("The companies..."). Urge him to throw the warehoused furs on the sale while the other company is short. Or write to the Committee or the partners proposing to change the rule on holding furs back (design doc 3.8.3).

## Planned

A quality rating on every pack.

## See also

- [[Economy]]
- [[Made Beaver]]
- [[Barter Rates]]
- [[Furs and Pelts]]
- [[Beaver]]
- [[Calendar and Seasons]]
