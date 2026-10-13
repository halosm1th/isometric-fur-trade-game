---
title: Terrain and Biomes
aliases: [Ground classes, Ecozones, Landscape tiles, Terrain tileset, Ground]
tags: [location, terrain, biome, tiles, system]
status: implemented
---

# Terrain and Biomes

**Status:** Implemented. The ground of the whole play area is a map of **13 classes** (`Ground` enum, `Data/World/ground.png`); `TerrainPainter` turns each cell into a landscape tile by season, and `WorldLayout` decides what grows on it.

![[gallery-landscape.png|700]]

## The 13 ground classes

The legend lives in `world.json`; `ground.png` is matched pixel by pixel to the nearest legend colour, so a hand-painted map doesn't need exact colours. Shares are sampled across the whole map.

| # | Class | RGB | Map share | Summer tile | Winter tile | What it is |
|---|---|---|---|---|---|---|
| 0 | Ocean | 36, 64, 110 | 9.5% | water (1 cell in 11 is drift ice) | ice | Hudson Bay, James Bay and the Pacific; blocks walkers when open |
| 1 | Lake | 52, 92, 140 | 4.2% | water | ice | Open water; the winter road when frozen |
| 2 | River | 64, 112, 160 | 1.5% | water | ice | Rivers and Sloops Creek |
| 3 | Ford | 196, 176, 120 | 0.1% | sand | snow | A river cell the road crosses; wadeable in summer |
| 4 | Boreal | 109, 138, 83 | 45.3% | grass | snow | Boreal forest: the commonest ground in the game |
| 5 | Tundra | 199, 203, 184 | 4.3% | tundra | snow | The far north shore |
| 6 | Shield | 169, 155, 171 | 6.1% | shield (12% hill patches) | snow | Canadian Shield bedrock |
| 7 | Parkland | 179, 154, 69 | 1.2% | parkland | snow | Aspen parkland: groves on open grass |
| 8 | Grassland | 214, 190, 90 | 10.1% | prairie | snow | Mixed-grass prairie |
| 9 | TallGrass | 205, 173, 60 | 1.2% | tallgrass | snow | Tall-grass prairie (Red River) |
| 10 | LakesForest | 111, 143, 82 | 0.3% | grass | snow | Great Lakes forest |
| 11 | Cordillera | 154, 132, 103 | 11.4% | mountain (65%) / hill (35%) | mountain / hill | The Rockies and Pacific ranges |
| 12 | Lowland | 96, 112, 70 | 4.8% | muskeg | snow | Hudson Bay lowlands: muskeg and tamarack |

Ecozone names from the atlas map to the classes in `build_world.py`: Tundra, Boreal Forest, Canadian Shield, Aspen Parkland, Mixed Grassland, Tall-Grass Prairie, Great Lakes Forest, Cordillera; Lowland is the Hudson Bay lowland the importer adds; Ocean, Lake, River and Ford come from the water layers. The data is hand-editable: paint `ground.png` in the legend colours and re-run (or just edit it; the loader matches nearest colours).

### How a cell becomes a tile

`TerrainPainter.SummerSource` decides in this order:

1. The **wharf and boardwalk** cells: the planks tile.
2. **Ocean**: water (or a drifting-ice cell, one in 11 by a cell hash); **Lake/River**: water; **Ford**: sand.
3. **Beaches** (two cells of land from the sea) and **river/lake banks**: sand.
4. **Yards** of York, the Nor'Westers' house and Oxford House: dirt.
5. Within 40,000 px (1,000 km) of York: **camp clearings** (300 px round the Cree and Assiniboine camps) dirt, the **river road** (within 60 px) the footprint road tile, the **trail** (within 70 px) dirt.
6. Otherwise by class (table above). Shield uses hill in groves of 12% by a hash; Cordillera is mountain 65% and hill 35% in patches "rather than a checkerboard".

In **winter** (Nov-Apr, `IsFrozen`) grass, sand, dirt and every regional ground (prairie to muskeg) becomes **snow**; water and drift ice become **ice**; hills, mountains and the road tile are kept. Frozen water loses its collision wall: the lakes and rivers become the winter road. The season change repaints every loaded chunk.

### Pace on each ground

`TerrainPainter.SpeedFactorAt` multiplies your walking speed:

| Ground | Pace |
|---|---|
| Frozen river, lake or bay (ice) | 1.25, and slippery (velocity carries) |
| River road (footprints) | 1.1 |
| Beaten trail, yard, camp (dirt) | 1.05 |
| Grass, prairie, parkland, tundra, shield, wharf | 1.0 |
| Sand (beaches, banks, fords) | 0.9 |
| Hill, mountain | 0.8 |
| Muskeg in summer | 0.75 |
| Snow | 0.7, or 1.0 on snowshoes (the Cree wear them; four in the stores) |
| Goose marsh in summer | 0.6 (within 0.8 of the site's radius) |

Trees are passable but slow; woodcutters fell them. The long-distance router charges more: Cordillera x2.2, fords x1.6, open water x0.9 (only when frozen), forest x1.4.

## What grows where

Vegetation is not stored: it is a deterministic function of the cell (`WorldLayout.PickFlora`), so it is the same every run, and painted in chunks as you travel. **Cleared** ground grows nothing: water, beaches, 900 px round any post (standing or still to come), 700 px round each portage landing, the fort yard, the trail (130 px), the road (150 px), the camps (460 px) and Oxford House (1,000 px).

**Tree density** by class (before patchiness): Boreal 32%, LakesForest 32%, Shield 24%, Cordillera 20%, Lowland 16%, Parkland 14% (broken into aspen bluffs: 2.6x in groves, 0.1x in openings); prairies and tundra 0. Other classes use 0.55 to 1.45 of that as groves and openings by a hash on 6 x 12 cell patches; the cap is 50%. Near York the forest thickens out from 1,500 to 2,300 px.

| Ground | Trees | Clutter between the trees |
|---|---|---|
| Boreal (and default) | 72% spruce, 14% aspen, 10% tamarack, 4% dead tree | rocks, saskatoon |
| LakesForest | 40% jack pine, 30% spruce, 30% aspen | rocks, saskatoon |
| Shield | 50% jack pine, 42% spruce, 8% dead tree | rocks, boulders, saskatoon, rosehip |
| Lowland | 60% tamarack, 32% spruce, 8% dead tree | muskeg pools, reeds, cranberry |
| Parkland | 85% aspen, 15% willow | willow, saskatoon, rosehip |
| Cordillera | 55% jack pine, 45% spruce | boulders, rocks, saskatoon |
| Grassland, TallGrass | none | rare rocks, boulders, rosehip |
| Tundra | none (a very rare spruce) | willow, rocks, boulders, cranberry |
| River and lake banks (any ground) | none | willow (7%), reeds (5%) |

Berry bushes (saskatoon, cranberry, rosehip) are flora like trees: in fruit through their season, picked over for a few weeks after a picker has stripped them, bare in autumn and a mound of snow in winter (the rosehip is the one that hangs on all winter, which mattered against scurvy). See [[Gathering and Gardens]] and [[Props and Furniture]].

Camps, posts and portages add their own tiles on top (tipis, palisades, canoes and caches). Beaver lodges are painted in the water beside each beaver ground.

## Wildlife and ground

There is no per-ground spawn table: animals come from **wildlife sites** placed on the map by the atlas build, each with a count, capacity and radius, simulated as numbers and promoted to live animals when someone comes near (see [[Species Sites]], [[Wildlife]]). Geese gather in marshes, beaver in lodges on water, caribou follow seasonal routes, buffalo on the prairies, and hares and ptarmigan on open flats. The terrain only shapes pace, cover and what you can eat or cut.

## Landscape tile set

The 17 tiles in `assets/tiles/landscape` (132 x 99 px diamonds; `terrain_tileset.tres` source ids in brackets). Credit: Kenney's *Isometric Landscape* (CC0) for the base tiles; the regional tiles are recolours made by `tools/world/make_region_tiles.py` and "placeholders until painted versions exist".

| Tile | Source id | Used for |
|---|---|---|
| ![[tile-grass.png\|90]] grass | 0 | Boreal and Great Lakes forest ground, the default |
| ![[tile-dirt.png\|90]] dirt | 1 | Yards, camps, the Cree trail |
| ![[tile-hill.png\|90]] hill | 2 | Shield patches and lower Cordillera slopes |
| ![[tile-sand.png\|90]] sand | 3 | Beaches, river banks, fords |
| ![[tile-stone_floor.png\|90]] stone_floor | 4 | In the tileset; nothing in the generated world places it yet |
| ![[tile-water.png\|90]] water | 5 | Ocean, lake and river in the open-water season |
| ![[tile-water_ice.png\|90]] water_ice | 38 | Drift ice scattered on the open bay (1 cell in 11) |
| ![[tile-snow.png\|90]] snow | 43 | Winter ground everywhere |
| ![[tile-ice.png\|90]] ice | 44 | Frozen water: the winter road |
| ![[tile-road.png\|90]] road | 61 | The York-Oxford river road (footprints), pace 1.1 |
| ![[tile-prairie.png\|90]] prairie | 70 | Grassland |
| ![[tile-tallgrass.png\|90]] tallgrass | 71 | Tall-grass prairie |
| ![[tile-parkland.png\|90]] parkland | 72 | Aspen parkland |
| ![[tile-tundra.png\|90]] tundra | 73 | Tundra |
| ![[tile-shield.png\|90]] shield | 74 | Canadian Shield |
| ![[tile-muskeg.png\|90]] muskeg | 75 | Hudson Bay lowland |
| ![[tile-mountain.png\|90]] mountain | 76 | Cordillera bare rock |

(The planks tile `floor_planks` also serves as the **wharf and boardwalk** surface, source 51; see [[Buildings]].)

## Tips

- Don't fight the snow: take snowshoes into the bush from November.
- Ford where the road does; the Hayes is wadeable only in the open-water season.
- The sea is only navigable for about two months; the rest of the year the bay is a road of ice.

## See also

[[World Map]] - [[Regions]] - [[Buildings]] - [[Props and Furniture]] - [[Landmarks]] - [[Calendar and Seasons]] - [[Weather]]
