---
title: Transport
aliases: [Vehicles, Travel Methods, Logistics, Getting About]
tags: [transport, index, system]
status: partial
---

# Transport

**Status:** Partial. The canoe, the supply ship, the inland brigade and cross-country travel mode are built. The York boat, dog sled, toboggan, Red River cart and horses are in the design (section 12) and drawn (York boat, dog sled, loaded canoe), but none is a working vehicle yet.

The fur trade was a trade in carrying. Furs came out of the interior in 90-pound packs on men's backs and in birchbark canoes; trade goods went in the same way, and everything depended on the few weeks when the rivers were open. The game models this as weight, capacity, distance and a handful of places (the portages) where things go wrong.

![[vehicle-canoe.png|180]] ![[vehicle-ship.png|220]] ![[vehicle-york_boat.png|260]] ![[vehicle-dog_sled.png|180]]

## Modes of travel

| Mode | Status | What it does | Page |
|---|---|---|---|
| **On foot** | Implemented | Base speed; snow, slippery ice, mud and terrain change it; snowshoes help in winter | [[Travel and Wayfinding]] |
| **Travel mode (G)** | Implemented | Auto-march along a planned route to a map waypoint at ~25 km a day of game time | [[Travel and Wayfinding]] |
| **Canoe** | Implemented | Player boards a small canoe: 1.5x walking speed on water, carries 300 lb | [[Canoe]] |
| **Supply ship** | Implemented | The annual ship from London, 20 August to 15 September | [[Ship]] |
| **Brigade** | Implemented | NPC canoe convoys up and down the Hayes road with the outfit and returns | [[Brigade]] |
| **York boat** | Planned (art cut, not wired) | HBC's heavy inland boat | [[York Boat]] |
| **Dog sled** | Planned (art cut, ambient sound only) | Winter travel | [[Dog Sled]] |
| **Horses** | Planned | Plains mobility and raiding (12.3) | [[Roadmap]] |
| **Red River cart, toboggan** | Planned | Listed in `docs/world-content-needed.md` | [[Roadmap]] |

## Design numbers (section 12)

The design doc records historical figures to be used "as literal game balance numbers":

| Vessel | Length | Crew | Cargo |
|---|---|---|---|
| Canot du nord (north canoe) | 24 to 28 ft | 4 to 6 | about 3,000 lb including crew and provisions |
| Canot du maitre (Montreal canoe) | up to 40 ft | 8 to 14 | about 7,000 to 9,000 lb |
| York boat | up to 46 ft | 6 to 8 | up to about 13,000 lb |

A north canoe could be carried by two men; a Montreal canoe by about four; a York boat could not be carried and had to be dragged on rollers, making a York-boat brigade at a portage a far longer ambush window. The York boat is described as largely a Métis invention and becomes the Company's dominant transport in the early 1820s, "new tech" on the edge of the game's end date.

In the game as built, only the player's small canoe (**300 lb**) and the brigade's loads are modelled with real numbers. See [[Canoe]] and [[Brigade]].

## Personal carrying

The same weight/capacity logic scales down to a body: base capacity from the strength skill, bags and packs (a tumpline, pack basket, possibles bag) adding more, and overloading degrading rather than blocking (design doc 12.4). Items have weights; the [[Inventory and Hotbar]] shows your load. A tumpline is described in the [[Glossary]].

## Freeze-up and breakup

Water travel shuts down when the season is Winter (November to April): "The canoes are frozen in till breakup. Walk on the ice." Brigades are "frozen in short of" their destination at freeze-up and winter the pieces on the bank. Planned: a per-waterway multi-week freeze and thaw, "the Autumn Scramble" and "the Spring Lockout", and emergency conversion from canoe to sledge. See [[Calendar and Seasons]] and [[Weather]].

## Portages

A portage is the chokepoint: canoes out of the water, the pieces over on men's backs, and anyone who means to stop you knows where you will be. See [[Routes and Portages]] and [[Brigade]].

## See also

[[Canoe]] | [[Ship]] | [[Brigade]] | [[York Boat]] | [[Dog Sled]] | [[Travel and Wayfinding]] | [[Routes and Portages]] | [[Voyageur]]
