---
title: Red River Colony
aliases: [Assiniboia, Selkirk Settlement, Selkirk Colony]
tags: [faction, colony, historical, campaign]
status: partial
---
# Red River Colony

**Status:** Partial. The colony exists as campaign beats, news letters, rivalry tension and atlas posts. Its settlers and governors are not spawned as people.

| | |
|---|---|
| **Faction value** | None (flags `red_river_colony`, `pemmican_proclamation`, `colony_dispersed`, `seven_oaks`, `governor_killed`) |
| **Home territory** | The Forks of the Red and Assiniboine rivers ([[Red River Valley]], [[Red River Posts]]) |
| **Founder** | [[Lord Selkirk]], who was granted 116,000 square miles of Assiniboia |
| **Governors** | [[Miles Macdonell]], then [[Robert Semple]] |
| **Atlas post** | Fort Douglas, "Selkirk-colony fort at the Forks", HBC, 1815 (Fort Gibraltar, the NWC post of 1809, stands beside) |

## History in the game

The *Pemmican War* campaign (`Campaigns/historical.json`) runs the colony's story as ordered beats:

| Date | Beat | Kind | Effect |
|---|---|---|---|
| 12 Jun 1811 | Selkirk's grant of Assiniboia | fixed | Flag `red_river_colony`; letter from the Governor and Committee to the York Factor (70 days) |
| 8 Jan 1814 | Macdonell's Pemmican Proclamation | event | Rivalry +20; NWC and Bay letters about the ban on provisions leaving the colony |
| 25 Jun 1815 | Colony attacked and dispersed | event | Rivalry +15; "some are coming down to the Bay" |
| 19 Jun 1816 | Seven Oaks: Semple and about twenty colonists killed | event | Rivalry +25; flags `seven_oaks`, `governor_killed` |
| 420 days after Seven Oaks | Selkirk takes Fort William | conditional | Only if the governor was killed; arrests McGillivray and the partners; Rivalry +10 |

"Fixed" beats always run; "event" beats are pressed according to the chosen adherence level; "conditional" beats fire only when their trigger is met ([[Campaigns and Adherence]]). The design doc holds the founding fixed and the survival open (4.4).

## Why it matters to play

- The proclamation attacks the [[North West Company]]'s food supply: brigades eat pemmican or do not run. Provisions are a real mechanic ([[Food and Drink|pemmican]] stocks, [[Daily Orders]]).
- Settlers are expected to come "out by the Bay": York Factory is where they winter first (Macdonell wintered there in 1811-12, noted in `docs/world-content-needed.md`).
- Rivalry tension crossing 70 causes armed men to go about both posts ([[Rivalry]]).
- The [[Metis]] are the pemmican suppliers and the force at Seven Oaks under [[Cuthbert Grant]].

## People tied to the colony

[[Lord Selkirk]], [[Miles Macdonell]], [[Robert Semple]], [[Cuthbert Grant]], [[Peguis]], [[Jean-Baptiste Lagimodiere]], [[Pierre Falcon]], [[Duncan Cameron]], [[Colin Robertson]], [[Peter Fidler]].

## Trivia

- The design doc places Selkirk's march on Fort William in 1817; historically it was August 1816. The game schedules it from the Seven Oaks date plus a 420-day delay, so it falls in 1817.
- The alternative campaign *The Plains Ablaze* carries the same colony beats alongside its Lewis and Clark storyline.

## See also

[[Hudson's Bay Company]], [[Setting and Timeline]], [[Historical People]], [[Red River Posts]]
