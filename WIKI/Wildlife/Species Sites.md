---
title: Species Sites
aliases: [Wildlife Sites, Spawn Sites, Lodges Dens and Herds, Hunting Grounds]
tags: [wildlife, world, system]
status: implemented
---

# Species Sites

**Status:** Implemented (design doc 16.4, 16.5). 637 sites are placed in `Scenes/World/YorkFactoryExterior.tscn`.

A **wildlife site** is the unit of the [[Ecology]]: a beaver lodge, a wolf den, a goose marsh, a hare thicket, a ptarmigan flat, a woodland-caribou range or a mobile herd. Each has a **count**, a **capacity**, a **radius** and (for herds) a **route**. Hunters, trappers and snarers draw down a specific site, not a regional pool.

> [!info] Where the data lives
> The game reads sites from `WildlifeSite` nodes in the York Factory exterior scene: 10 hand-placed under `Wildlife/` around York and 627 under `Wildlife/Country/`. `Data/World/world.json` carries a matching `layout.sites` list (635 entries with species, cell, count, capacity, radius and route; written by `tools/world/build_world.py`), which is two short of the scene (one hare and one ptarmigan site) because the scene was edited afterwards. The scene is authoritative. In the editor each draws a coloured circle for its radius, a label with its count, and for a herd a line through its month markers; in game they are invisible. Beaver sites also make the game paint a lodge in the water.

![[tile-beaver_lodge.png|140]] ![[tile-ice_hole.png|140]] ![[tile-leg_trap.png|140]] ![[tile-fish_trap.png|140]]

## Site types

| Type | Species | Fixed or mobile | Dies if over-hunted? | Count on the map | Total count / capacity |
|---|---|---|---|---|---|
| Beaver lodge | [[Beaver]] | fixed | yes, permanently | 236 | about 9,470 / 14,770 |
| Wolf den | [[Wolf]] | fixed | yes | 85 | about 520 / 850 |
| Goose marsh | [[Goose]] | fixed (seasonal) | yes | 32 | about 3,970 / 9,600 (and eight times in the autumn flight) |
| Hare thicket / flats | [[Hare]] | fixed | yes | 128 | about 8,340 / 17,900 |
| Ptarmigan willows / flats | [[Ptarmigan]] | fixed | yes | 47 | about 4,740 / 9,360 |
| Woodland caribou | [[Caribou]] | fixed | yes | about 84, capacity 50 each | |
| Caribou herd | [[Caribou]] | **mobile** (12-month route) | no: scatters | 7, capacity 400 (York's is 140) | |
| Buffalo plain | [[Buffalo]] | **mobile** | no: scatters | 18, capacity 260 | about 3,200 / 4,680 |

These totals are counted from the scene file and are approximate; the game reads sites live. Caribou sites total 91 (3,760 / 6,740).

## Around York Factory

The ten hand-placed sites (`Wildlife/`), which include the lodges on the road to Oxford House:

| Site | Species | Count / capacity | Radius (px) | Notes |
|---|---|---|---|---|
| Caribou herd | Caribou | 90 / 140 | 900 | 12 route markers; the herd the post lives on |
| Goose marsh | Goose | 180 / 300 | 700 | On the bay shore just beyond Sloops Creek |
| Beaver lodge (upper) | Beaver | 40 / 60 (defaults) | 350 | |
| Beaver lodge (bend) | Beaver | 45 / 60 | 350 | |
| Beaver lodge (lower) | Beaver | 36 / 60 | 350 | |
| Wolf den | Wolf | 6 / 10 | 800 | |
| Hare thicket | Hare | 70 / 120 | 500 | South-west of the fort |
| Ptarmigan willows | Ptarmigan | 90 / 160 | 600 | North of the fort |
| Beaver lodge (Knee Lake) | Beaver | 50 / 70 | 350 | On the Hayes road |
| Beaver lodge (Oxford) | Beaver | 55 / 75 | 380 | Near [[Oxford House]] |

"Which lodge" matters: all York trapping happens at these few lodges, so a post that traps hard can empty its nearby lodges within a few seasons. See [[Trapping]].

## Around the country

The wider map was populated on 11 October 2026 with **611 generated sites** (commit "Game across the whole country"): beaver lodges along the forest rivers and lakeshores, goose marshes on the Bay coasts, barren-ground herds crossing between tundra and forest edge, woodland caribou, wolf dens, ptarmigan in the north, and hare flats by every post and camp. They are named for the post or nation whose country they lie in:

- "Beaver lodge (Fort Chipewyan country)", "Beaver lodge (Dease Lake)", "Beaver lodge (Sekani country) 2"...
- "Goose marsh (Great Whale River country)", "Goose marsh (Churchill (Pr. of Wales Fort) country)"
- "Ptarmigan flats (Chipewyan country) 4"
- "Wolf den (Dease Lake)"
- "Woodland caribou (Oxford House country)", "Woodland caribou (Lac la Ronge country)"
- "Caribou herd (Fort Nelson (BC) country)", "Caribou herd (Dene (Beaver) country)", four "Caribou herd (Fort Chipewyan country)"

The numeric suffix distinguishes several sites near the same place. They are editable markers: the generator keeps hand-made camps (for example the Métis ones) when it regenerates.

## Buffalo plains

Eighteen mobile herds, each capacity 260, radius 900 to 1300 px, wintering in the parkland and wooded river valleys and out on the open grass April to October. Their winter quarters are near the camps that hunt them, the summer ground a journey away.

| Herd | Count | Capacity | Farthest route marker from the start |
|---|---|---|---|
| Blackfoot | 167 | 260 | about 64 km |
| Blood (Kainai) | 180 | 260 | about 32 km |
| Peigan (Piikani) | 161 | 260 | about 122 km |
| Sarcee (Tsuut'ina) | 160 | 260 | about 153 km |
| Cypress Hills | 185 | 260 | about 68 km |
| Touchwood Hills | 170 | 260 | about 125 km |
| Nakoda (Stoney) | 184 | 260 | about 37 km |
| Dakota (Santee Sioux) | 198 | 260 | about 162 km |
| Lakota (Teton Sioux) | 188 | 260 | about 267 km |
| Turtle Mountain | 182 | 260 | about 163 km |
| Moose Mountain | 191 | 260 | about 100 km |
| Moose Mountain 2 | 174 | 260 | about 157 km |
| Arikara | 169 | 260 | about 71 km |
| Crow | 193 | 260 | about 78 km |
| Shoshone | 185 | 260 | about 74 km |
| Cheyenne | 176 | 260 | about 303 km |
| Metis country, Forks | 164 | 260 | about 50 km |
| Metis country, at Pembina | 176 | 260 | about 157 km |

Distances use the world scale of 40 px per km (the "old York-Oxford scale"). None is within reach of York.

## Caribou herds

| Herd | Count | Capacity | Route extent |
|---|---|---|---|
| Caribou herd (York) | 90 | 140 | |
| Fort Nelson (BC) country | 281 | 400 | about 58 km |
| Dene (Beaver) country | 225 | 400 | about 83 km |
| Fort Chipewyan country | 271 | 400 | about 128 km |
| Fort Chipewyan country 2 | 276 | 400 | about 32 km |
| Fort Chipewyan country 3 | 209 | 400 | about 122 km |
| Fort Chipewyan country 4 | 222 | 400 | about 26 km |

## Who knows what

A household hunts only grounds it **knows**, and a household's *country* is the grounds within about **600 km** (24,000 px) of its home. Nobody at York knows the beaver of the Athabasca; a hungry house "looks further for game, but not off its own country" (commit d303251). Sites are learned by passing within a fraction of their radius (60% for NPCs, 70% for players). The player gets "You have found Beaver lodge (upper). The Factor would want to know." For herds that move the player gets "So this is where X has got to." A herd the camp has just lost counts as "lost today" and is not retried.

## Mapping a new site

In the Godot editor drop a `WildlifeSite` node (script `Scripts/World/WildlifeSite.cs`) under `Wildlife/`, choose its **Species**, **Count**, **Capacity** (default 40 and 60) and **RadiusPx** (100 to 2000, default 350). Add up to twelve `Marker2D` children in order (January to December) to make it a herd; they snap to the isometric grid along with the site. `WildlifeSite.Collect` hands the defs to `WorldLayout.Sites` at game start.

## See also

[[Ecology]] | [[Wildlife]] | [[Hunting]] | [[Trapping]] | [[Locations]] | [[World Map]] | [[Regions]] | [[Architecture]]
