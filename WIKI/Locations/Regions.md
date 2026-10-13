---
title: Regions
aliases: [World regions, Map regions, Departments]
tags: [location, region, index]
status: partial
---

# Regions

**Status:** Partial. The design doc (§6) names distinct zones "with distinct feel/mechanics, including distinct wildlife"; the game itself stores no region field. What exists in code is the **ground class** of each cell (13 classes, see [[Terrain and Biomes]]) and the posts and camps on it. This wiki groups the map into seven regions by position, and every page here uses the same grouping.

![[map-world-overview.png|700]]

| Region | Posts | Feel | Anchor |
|---|---|---|---|
| [[Hudson Bay Lowlands]] | 19 entries | Subarctic, short season, York boats; James Bay is the Southern Department (design doc §6.2) | [[York Factory]] |
| [[Red River Valley]] | 24 | Buffalo plains, pemmican war, Métis, Cree/Assiniboine/Saulteaux | [[Red River Posts]] |
| [[Saskatchewan Country]] | 16 | Parkland and plains; Blackfoot, Cree, Nakoda; paired HBC/NWC posts | [[Cumberland House]], [[Edmonton House]] |
| [[Athabasca Country]] | 9 | Far north boreal; Dene; the NWC's richest fur country | [[Fort Chipewyan]] |
| [[Great Lakes]] | 10 | The canoe route, endless portages, the NWC's mobility advantage | [[Fort William]] |
| [[Columbia Country]] | 20 | Pacific slope and New Caledonia; plateau and coast peoples; first Americans | (none in window) |
| [[Missouri and Upper Plains]] | 7 | Earth-lodge farming towns, American traders, Sioux | Mandan/Hidatsa/Arikara towns |

The post counts include the four York-country camps and the Nor'Westers' house in the first row. The numbers come from the table in [[All Trading Posts]].

## How the design doc lists them

Design doc §6 lists six zones: the Hudson Bay lowlands; the Canadian Shield / Great Lakes corridor; the plains around Red River; the **Rocky Mountain passage** ("the bottleneck between the two worlds"); the Columbia / Oregon country; and the Southern Department. This wiki folds the Rocky Mountain passage into the Saskatchewan, Athabasca and Columbia pages (Jasper House guards the Athabasca Pass route; Kootenay House is on the far side) and keeps the Southern Department inside the Hudson Bay Lowlands page. The map boundary is set by design doc §6.1: east edge from Sault Ste. Marie to the edge of James Bay, west edge the Pacific, south only the edge of the American interior.

## Ground by region

Sampled in the neighbourhood of each region's posts from `ground.png` (percent of sample points; classes with under 5 percent left out):

| Region | Main ground classes |
|---|---|
| Hudson Bay Lowlands | Lowland 41, Shield 29, Boreal 13, sea 11 |
| Red River Valley | Boreal 32, Grassland 30, Tall-grass 19, Parkland 12 |
| Saskatchewan Country | Boreal 53, Parkland 26, River 9, Grassland 8 |
| Athabasca Country | Boreal 73, Cordillera 16, Lake 7 |
| Great Lakes | Boreal 69, Lake 29 |
| Columbia Country | Cordillera 61, Boreal 28, sea 5 |
| Missouri and Upper Plains | Grassland 73, Boreal 17, River 7 |

Across the whole map (sampled): Boreal 45%, Cordillera 11%, Grassland 10%, Ocean 9.5%, Shield 6%, Lowland 5%, Tundra 4%, Lake 4%, with the rest Parkland, Tall-grass, Great Lakes forest, rivers and fords.

## See also

[[Locations]] - [[World Map]] - [[All Trading Posts]] - [[Terrain and Biomes]] - [[Species Sites]]
