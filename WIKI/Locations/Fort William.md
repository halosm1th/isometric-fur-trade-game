---
title: Fort William
aliases: [Kaministiquia, Fort William (NWC), Nor'Westers' headquarters]
tags: [location, nwc, post, great-lakes, starting-location]
status: partial
---

# Fort William

**Status:** Partial. A planned post with a large custom layout and a cast, but the July rendezvous, the council sitting in the Hall and the full forty-building town are not built (see "What is not there yet").

![[loc-fort-william.png|600]]

| | |
|---|---|
| **Company** | [[North West Company]] |
| **Founded** | 1803 (`world.json`; the NWC moved here from [[Grand Portage]]) |
| **Region** | [[Great Lakes]] (north-west shore of Lake Superior, on the Kaministiquia) |
| **Map position** | 89.25 W, 48.38 N (cell 810, 1701); palisade top corner at cell 804, 1633 |
| **Palisade** | 30 x 26 cells, gate at offset 14 (custom plan in `post_plans.json`) |
| **Staff** | 6 voyageurs, 2 women, a clerk (plus the partner); 10 people in all |
| **Ledger** | [[Trading Post Ledger]] |
| **Neighbour** | An Ojibwa (Anishinaabe) family camp beside it (cell 797, 1635) and the Ojibwa band at cell 781, 1692 |
| **Distances** | Grand Portage 57 km; Sault Ste. Marie 426 km; Rainy Lake House 307 km; Fort Gibraltar 599 km; Norway House 859 km; York Factory 977 km |

## Description

The North West Company's inland capital, where the partners and the Montreal agents meet each July in the Great Hall. The real place was a walled town of forty buildings; the plan keeps eleven of them inside a 30 x 26 palisade, "drawn from what the posts held, not from surveyed plans" (the comment in `post_plans.json`).

### Building plan

| Building (kind) | Name | At (i, j) | Size |
|---|---|---|---|
| FactorsHouse | the partners' house | 2, 2 | 5 x 6 |
| ClerksHouse | the counting house | 9, 2 | 5 x 6 |
| FactorsHouse | the agents' house | 16, 2 | 5 x 6 |
| ProvisionStore | the provision store | 23, 2 | 5 x 4 |
| GreatHall | the Great Hall | 11, 10 | 9 x 7 |
| Warehouse | the fur store | 2, 10 | 8 x 6 |
| Warehouse | the warehouse | 21, 10 | 8 x 6 |
| Depot | the Indian shop | 2, 18 | 7 x 5 |
| PowderMagazine | the powder magazine | 11, 19 | 3 x 3 |
| SummerMess | the kitchen | 16, 19 | 5 x 5 |
| MensHouse | the voyageurs' house | 21, 18 | 7 x 5 |

Props: a flagstaff (14,8), a fur bale (10,17), a woodpile (27,24), a crate (13,23) and two canoes drawn up outside the front wall (18,28) and (22,28). The post is kept "as the trading shop" = the Indian shop (the Depot kind), which is where the ledger's access cell is.

**The Great Hall** is the show-piece: a hearth at one end, a long table (four tables with chairs in a row), a rug, a tall shelf and candle stands. Painted with the same log walls and plank floor as York (see [[Buildings]]).

## People

| Person | Role |
|---|---|
| Mr. Adam Ross (partner) | The post's master ([[Postmaster]] role, NWC) |
| Adam Fraser (clerk) | Clerk |
| Jacques Delorme, Alexis Paradis, Antoine Primeau, Augustin Lafleur, Etienne Lapierre, Alexis Primeau | [[Voyageur]]s |
| Elizabeth Laroque, Isabella Vandal | Women of the post |

(These are invented cast; the real partners are [[Historical People]].)

## The council and the rendezvous

Each year the "Brigade" calendar event on **12 July** triggers the **Fort William council** (`Council.Convene`): wintering partners and the Montreal agents sit with share-weighted votes; their minutes come north as a packet to the partner at [[Nor'Westers' House]], setting the price, the outfit and whether the Bay is to be pressed. See [[Councils and Accords]]. It resolves off the map with the same kind of agents as everyone else; the player does not yet watch it sit in the Hall.

## History in the campaign

- **16 Jun 1816, Seven Oaks.** News goes to the wintering partner from Fort William 60 days later.
- **Selkirk marches on Fort William** (conditional beat, 420 days after Seven Oaks if the Governor is dead): the partners are arrested. See [[The Reckoning of 1821]].
- **26 Mar 1821.** The union of the companies ends the NWC.

## What is not there yet

From `docs/world-content-needed.md`: the July rendezvous gathering people into the Hall, the council actually sitting in it, the voyageurs' camp outside the walls and more of the real forty buildings. The doc's older "17 x 10 palisade" first pass is superseded by the 30 x 26 plan; `Posts.FortWilliam` in code only applies if the plan is missing.

## Player notes

- **Starting here.** The [[NWC Path]] begins in a canoe at the Nor'Westers' house, not here (every start uses the York Factory scene); a start at Fort William itself would be a place in the streamed world.
- `--spawn="Fort William"` jumps to the gate (dev).

## See also

[[Grand Portage]] - [[Nor'Westers' House]] - [[Great Lakes]] - [[North West Company]] - [[All Trading Posts]] - [[Routes and Portages]] - [[Rivalry]]
