---
title: Country Camps
aliases: [Camps of the wider country, Homeguard families, Bands of 1811]
tags: [location, camp, nations]
status: implemented
---

# Country Camps

**Status:** Implemented. Besides the two bands at York ([[Cree Camps]], [[Assiniboine Camps]]), `world.json` places **124 camps** in the wider country: **38 bands** where each people lived in 1811, and **86 "family" camps**, one lodge and fire outside each of the 86 standing-post sites (the homeguard). They are drawn from the start and simulated lightly (see [[Simulation Tiers]]).

![[map-world-overview.png|600]]

![[loc-plains-tipi-camp.png|450]] ![[loc-earth-lodge-village.png|450]]

*A plains tipi camp (left) and a Missouri earth-lodge village (right), built from the real tiles.*

## How a camp works

- **Bands** are rings of tipis (five round a campfire) with 8 or 12 people. The three Missouri farming towns (Mandan, Hidatsa, Arikara) are painted as **earth-lodge villages** instead (earth lodges, garden plots, corn scaffold, cache pit, corn mortar, village palisade, bullboat; see [[Props and Furniture]]).
- **Families** are a lodge and fire beside a post, four people, with a cache of their own, belonging to the nearest nation (for example the Chipewyan family at Fort Chipewyan, the Blackfoot family at Edmonton House).
- Each camp is a `CountryBand` ledger whose hunt and trade is resolved weekly in numbers: a week of hunt, fishery and snares (6 dried meat a head, 4 in Feb-Apr, 8 in Sep-Oct), plus 2 venison and 1 fish a head eaten fresh. Starting cache: 30 dried meat, 10 pemmican, 20 firewood, 10 water, 8 beaver pelts (2 for a family). Bands trade at the posts of their own country, not at York.
- Missouri towns grow corn and run the summer fair (see [[Gathering and Gardens]], [[Missouri Villages]]).
- On the map (M), camps show as brown triangles once the area is explored.

## The 38 bands

| Band | Nation | People | Cell (x, y) |
|---|---|---|---|
| Blackfoot | [[Blackfoot Confederacy]] | 12 | 294, 1233 |
| Blood (Kainai) | [[Blackfoot Confederacy]] | 8 | 264, 1509 |
| Peigan (Piikani) | [[Blackfoot Confederacy]] | 8 | 268, 1653 |
| Sarcee (Tsuut'ina) | [[Blackfoot Confederacy]] | 8 | 252, 1342 |
| Gros Ventre | [[Blackfoot Confederacy]] | 12 | 349, 1622 |
| Plains Cree | [[Cree]] | 12 | 425, 1196 |
| Woodland Cree | [[Cree]] | 12 | 545, 899 |
| Swampy Cree | [[Cree]] | 8 | 722, 804 |
| Assiniboine | [[Assiniboine]] | 12 | 471, 1529 |
| Nakoda (Stoney) | [[Assiniboine]] | 12 | 251, 1277 |
| Plains Ojibwa (Saulteaux) | [[Ojibwe]] | 12 | 663, 1603 |
| Ojibwa (Anishinaabe) | [[Ojibwe]] | 12 | 781, 1692 |
| Dakota (Santee Sioux) | [[Sioux]] | 12 | 666, 2205 |
| Lakota (Teton Sioux) | [[Sioux]] | 12 | 529, 2347 |
| Mandan | [[Missouri Villages]] | 12 | 538, 1959 |
| Hidatsa | [[Missouri Villages]] | 8 | 523, 1914 |
| Arikara | [[Missouri Villages]] | 8 | 559, 2145 |
| Crow | [[Missouri Villages]] | 12 | 382, 2177 |
| Shoshone | [[Missouri Villages]] | 12 | 283, 2132 |
| Kootenay (Ktunaxa) | [[Plateau Peoples]] | 12 | 206, 1541 |
| Salish (Flathead) | [[Plateau Peoples]] | 8 | 222, 1872 |
| Chipewyan | [[Dene]] | 12 | 469, 416 |
| Dene (Beaver) | [[Dene]] | 12 | 221, 451 |
| Odawa (Ottawa) | [[Ojibwe]] | 8 | 947, 1908 |
| Coast Salish | [[Coast Peoples]] | 8 | 24, 1449 |
| Dakelh (Carrier) | [[Dene]] | 12 | 55, 714 |
| Tsilhqot'in | [[Dene]] | 8 | 53, 1061 |
| Sekani | [[Dene]] | 8 | 72, 393 |
| Secwépemc (Shuswap) | [[Plateau Peoples]] | 8 | 122, 1225 |
| Syilx (Okanagan) | [[Plateau Peoples]] | 8 | 127, 1473 |
| Nlaka'pamux | [[Plateau Peoples]] | 8 | 93, 1353 |
| Cheyenne | [[Missouri Villages]] | 12 | 494, 2291 |
| Oji-Cree | [[Cree]] | 8 | 736, 1159 |
| Menominee | [[Ojibwe]] | 8 | 849, 2152 |
| Ho-Chunk (Winnebago) | [[Missouri Villages]] | 8 | 832, 2337 |
| Métis of the Forks | [[Metis]] | 12 | 633, 1638 |
| Métis at Pembina | [[Metis]] | 8 | 634, 1716 |
| Métis of the Qu'Appelle | [[Metis]] | 8 | 523, 1500 |

## Family camps by nation

| Nation | Family camps |
|---|---|
| [[Cree]] | 19 (Swampy, Woodland, Plains Cree, Oji-Cree) |
| [[Metis]] | 18 (the Forks, Pembina, Qu'Appelle) |
| [[Ojibwe]] | 13 (Ojibwa, Odawa, Nipissing, Saulteaux) |
| [[Dene]] | 13 (Chipewyan, Beaver, Carrier, Sekani, Tsilhqot'in) |
| [[Blackfoot Confederacy]] | 8 |
| [[Plateau Peoples]] | 7 |
| [[Assiniboine]] | 4 |
| [[Coast Peoples]] | 4 |

Every family camp is named "*people* family at *post*" and is found in the [[All Trading Posts]] table's posts.

## Tips

- Visit a family camp to learn what its nation is called and where its hunters go; a friendly hunter will tell you "about the hunting".
- Dev command: `--spawn="Mandan"` jumps to a camp by name; `--nocamps` leaves the country empty of peoples.

## See also

[[Locations]] - [[World Map]] - [[Regions]] - [[Factions]] - [[Band Ledger]]
