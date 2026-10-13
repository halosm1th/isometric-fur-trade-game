---
title: Nor'Westers' House
aliases: [NWC house at York, Nor'Westers' house (York), The Canadians' house]
tags: [location, nwc, post, hudson-bay-lowlands]
status: implemented
---

# Nor'Westers' House

**Status:** Implemented. The rival wintering post of the [[North West Company]] up-country from York Factory, in the playable scene. It has its own ledger, cast and brigade.

![[loc-nwc-house.png|600]]

| | |
|---|---|
| **Company** | [[North West Company]] |
| **Founded** | 1801 (`extras.json`: "the game's rival post") |
| **Region** | [[Hudson Bay Lowlands]] |
| **Map position** | 93.55 W, 56.55 N (cell 676, 642); palisade top corner at cell 675, 642 |
| **Distance** | About 91 km from [[York Factory]], across the Hayes |
| **Palisade** | 12 x 10 cells, gate in the front wall at offset 5 |
| **House** | "Nor'Westers' house", 7 x 5 cells at (2,2) in the palisade, door offset 3 |
| **Ledger** | [[NWC Ledger]] (`NwcLedger`, "the Nor'Westers' store") |
| **Resident cast** | Mr. Duncan Cameron (partner) and three voyageurs |

## Description

A palisade and a single log house, the kind of post the Canadians threw up wherever the Bay had one: a wintering house set a day or two up the river from the Company's depot, to catch the Cree's furs before they reached York. Unlike York it has no ship and no London; a **canoe brigade from [[Fort William]]** restocks it each year.

The sign outside the gate reads *Nor'Westers' House / North West Company*. A beached canoe sits at their landing, the second of the two canoes in the scene.

## Inside the house

The one room serves for everything: a hearth at (1,1) on the back-left wall, a shelf and a tall shelf of goods, a crate, a table, a candle stand and **five beds** (a row along the front and one by the hearth). There is a woodpile at cell (9,7) of the palisade. The storeroom chest is here too, as in the Bay post's Depot; standing inside it and pressing the interact key gives the chest if you are an NWC man, or "Plunder their stores" if you are an HBC man at war with them (see [[Rivalry]]).

## People

| Person | Role |
|---|---|
| Mr. Duncan Cameron | [[NWC Partner]]; the household's leader; red coat |
| Baptiste Lagimodiere, Pierre Falcon, Antoine Desjarlais | [[Voyageur]]s; the winterers |

Each July the brigade lands up to three more voyageurs to bring the post back to strength (`LandVoyageurs(3)`), drawn from a short list of names (Joseph Cadotte, Louis Nolin, Francois Deschamps, Jean-Baptiste Lajimoniere and the three above).

## The books

Starting stock: 40 pemmican, 40 dried goose, 30 ale, 30 firewood, 70 trade goods, 4 muskets, 8 steel traps, 10 beaver pelts, **40 rum**, 2 kettles, 16 water, 6 tumplines, 5 fishing lines, 25 tobacco, 15 beads and 15 powder. The Nor'Westers lean on drink: the default rum share of a fur payment is **35 percent** (the Company's is 15 percent), though the partner "usually pays a shade better than the Company's" price. The partner knows the country (`KnowsTheCountry`) and so knows every wildlife site within about 600 km.

### The yearly cycle

| Date | Event |
|---|---|
| 12 July | **The brigade** arrives from Fort William. It carries off all the furs in the store (beaver, thin and shot pelts, caribou hide, wolf), lands the year's outfit (default 90 pieces of trade goods, a third of that in pemmican, 60 rum if the rum share is above 30 percent, 4 muskets, 8 traps) and delivers the minutes of the Fort William council, which the partner must read. The council sets the price, outfit and whether "the Bay is to be pressed" (see [[Councils and Accords]]) |
| Any time | The Cree chief chooses where to sell each trip: price x how the band feels about each company / the walk |
| Tension 55 or more | Cameron may send two men to lie up a day's march out on the river road to ambush the Company's inland packet (30 hours; see [[Rivalry]]) |

## Player notes

- **Signing on.** Talk to Cameron: "Ask about signing on", then "Sign on". The Company's counter then refuses you, its people drop 25 rapport and your standing resets. See [[NWC Path]]. The Factor will take you back once he thinks something of you again.
- **Getting there.** Across the river by the ford south of the Cree camp, by the ice in winter, or by canoe. The NWC start begins in a canoe and steps out of it.
- **Captivity.** A blow from the other company that would have killed you takes you prisoner here instead: three days on the floor of their house, then released with two days of pemmican; or ransom (12 trade goods) or parole (carry no arms against them for four months).

## See also

[[York Factory]] - [[Fort William]] - [[North West Company]] - [[Rivalry]] - [[NWC Ledger]] - [[Hudson Bay Lowlands]] - [[All Trading Posts]]
