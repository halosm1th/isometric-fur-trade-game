---
title: World Map
aliases: [The Map, Play area, Map panel, ground.png, world.json]
tags: [location, map, system, world]
status: implemented
---

# World Map

**Status:** Implemented. The whole play area from the Pacific to James Bay exists as one continuous world: a 1,002 x 2,361 cell ground map generated from real geography, streamed in chunks around whoever is looking.

![[map-world-overview.png|700]]

## Scale and projection

| | |
|---|---|
| **Size** | 1,002 columns x 2,361 rows = 128,256 x 75,552 world px |
| **Scale** | **40 world pixels per kilometre** (`px_per_km` in `world.json`) |
| **Real size** | about 3,206 km west-east x 1,889 km north-south |
| **Cell** | A map cell is a 128 x 32 px diamond-tile slot (rows stagger by half a tile): **3.2 km across, 0.8 km tall**; one isometric edge step is 71.5 px (about 1.8 km) |
| **Projection** | Lambert conformal conic: standard parallels 49 N and 59 N, origin 52.5 N / 102.75 W, so distances hold across the whole area |
| **Frame** | West 127 W, east 78.5 W, south 44.5 N, north 61.5 N: Pacific coast to Sault Ste. Marie and Moose Factory |
| **Ground data** | `Data/World/ground.png`, one pixel per cell, hand-editable in the 13 legend colours (see [[Terrain and Biomes]]) |
| **Generator** | `tools/world/build_world.py` from the *Pemmican Empire* atlas data (Natural Earth coasts, lakes, rivers, ecozones, posts) plus `tools/world/extras.json` (the Hayes, game places, wildlife sites, post closures, Métis bands) |

Buildings stay at life size: a fort covers what the map calls tens of kilometres. The docs' older figures (20 px/km, 501 x 1,180 cells, "80 x 320 cells") are stale; the current map is the "old York-Oxford scale" of 40 px/km.

### How a place is found

Everything that asks "what is this cell" asks `WorldMap`, not the painted tiles: only the country round the viewer is painted (in 16 x 64 cell chunks, 2,048 px square, with a 2,800 px margin; at most 160 chunks), so a man walking a hundred miles off still knows a lake is a lake. Off the edge of the map is open ocean.

Hand-built places keep their shape; the importer found "the nearest dry spot" to their real position and recorded it as a `top` corner in `world.json` (`fort_top` 677,532; `nwc_top` 675,642; `outpost_top` 646,882). The York-Oxford road and the Cree trail are laid by the importer along dry ground, fording the river where they must.

## What is on it

| Layer | Count | Page |
|---|---|---|
| Places (posts, rival house, camps) | 105 | [[All Trading Posts]] |
| Posts with a layout | 86 (6 custom plans) | [[All Trading Posts]] |
| Camps (bands + family) | 124 (38 + 86) | [[Country Camps]] |
| Wildlife sites | 635 (236 beaver, 127 hare, 91 caribou, 85 wolf, 46 ptarmigan, 32 goose, 18 buffalo) | [[Species Sites]] |
| Brigade routes | 5 | [[Routes and Portages]] |
| Landmarks | 83 (8 portages, 41 lakes, 18 rivers, 8 hills, 6 waters, 2 ranges) | [[Landmarks]] |
| Ground classes | 13 | [[Terrain and Biomes]] |

The wiki divides the map into seven regions: [[Hudson Bay Lowlands]], [[Red River Valley]], [[Saskatchewan Country]], [[Athabasca Country]], [[Great Lakes]], [[Columbia Country]], [[Missouri and Upper Plains]]. See [[Regions]].

## The map in the game (M)

Press **M** (default) to open "The country as you know it". Geography is knowledge (design doc §10.8): the map is fogged except where you have been, plus the ground round your own post that the men would have told you about (7,000 px round it, and the road and Oxford House if you belong to York or Oxford House).

| Mark | Meaning |
|---|---|
| White dot | You (other players are gold) |
| Red / blue square | HBC / NWC post, shown once you have seen it |
| Brown triangle | Camp |
| Circle | Wildlife site you know of (herds drawn where you last heard they were: "last known") |
| Gold diamond | Your waypoint (click the map to set it; right-click clears it) |
| Blue diamond | Where your current task is to be done |
| Dashed red line | A brigade route you have seen |
| Dashed brown line | The river road |
| Dark blue diamond | The ship, if in sight |

Controls: **drag** to pan, **wheel** to zoom (0.6x to 8x, starting at 1.3x), **click** to mark a waypoint, **G** to set out for it (travel mode; the route is planned over the world by A* on a background thread, see [[Routes and Portages]]). On screen, a waypoint or task target shows as a diamond over the spot, or an arrow at the screen edge with the distance in km (or m). The fog is coarse: each cell is 1,024 px (about 25 km), a walk reveals 2,000 px either side. See [[Travel and Wayfinding]] and [[Journal, Map and UI]].

### Dev aids

`-- --map` opens the panel after 2.5 s; `--dumpmap` writes the whole country to `user://map.png`; `--revealmap` lifts the fog; `--spawn="Fort William"` (or a camp or landmark name, or `x,y` cells) places you.

![[map-york-factory-game.png|500]]

## See also

[[Locations]] - [[Regions]] - [[All Trading Posts]] - [[Terrain and Biomes]] - [[Landmarks]] - [[Routes and Portages]] - [[Travel and Wayfinding]]
