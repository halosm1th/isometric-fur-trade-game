---
title: Props and Furniture
aliases: [Furniture tiles, Props, Prop tiles, Gallery of props]
tags: [location, props, furniture, tiles, system]
status: implemented
---

# Props and Furniture

**Status:** Implemented. The world is dressed with 67 tiles: 11 in `assets/tiles/furniture` and 56 in `assets/tiles/props` (128 x 256 px; the object is centred and its base sits on y = 236, so any file can replace an older one without touching `terrain_tileset.tres`). Most are cut from the Gemini prop sheets by `tools/sprites/props.py`, which says "new, for when the world places them" of many of them: **18 of the 67 (about a quarter) are in the art folder but not yet placed** by any code. Those are marked below.

![[gallery-furniture.png|600]]

![[gallery-props.png|700]]

Source ids are those of `assets/tiles/landscape/terrain_tileset.tres`. "Not placed" means no code in `Scripts/` references the tile; a designer could place it in the editor.

## Furniture

| Tile | Source | Used for |
|---|---|---|
| ![[tile-bed.png\|70]] bed | 60 | Bunks and beds: four in the trading shop, nine in the men's house, five in each one-room post house, one or two in the master's, clerks', guest and partners' houses. A bed is where an NPC sleeps (see [[Sleeping and Resting]]) |
| ![[tile-candle_stand.png\|70]] candle_stand | 33 | Light and ornament in the trading shop, men's house, Factor's house, clerks' house, guest house and the Great Hall |
| ![[tile-chair.png\|70]] chair | 31 | A single chair at the men's house table |
| ![[tile-display_case.png\|70]] display_case | 32 | The counter case of trade goods in the trading shop |
| ![[tile-rug.png\|70]] rug | 34 | Floor rug in the trading shop, Factor's house and the Great Hall |
| ![[tile-shelf.png\|70]] shelf | 27 | Low shelf: trading shop, one-room post houses, provision store, clerks' house |
| ![[tile-shelf_tall.png\|70]] shelf_tall | 28 | Tall shelf of goods or books: trading shop, Factor's house, post houses, Great Hall |
| ![[tile-table.png\|70]] table | 29 | A plain table: clerks' desk, post-house table, guest house |
| ![[tile-table_chairs.png\|70]] table_chairs | 30 | A table with chairs: trading shop, Factor's house, summer mess (three), Great Hall (a row of four). The bread and ale are set out on the trading-shop one at the start |
| ![[tile-stove.png\|70]] stove | none | Art only (a cast-iron stove, cut from the Gemini sheet); not in the tileset |
| ![[tile-writing_desk.png\|70]] writing_desk | none | Art only (a writing desk); not in the tileset |

## Trees and plants

| Tile | Source | Used for |
|---|---|---|
| ![[tile-spruce_0.png\|70]] spruce_0 | 52 | Black spruce (the commonest tree). The three variants are picked at random by a hash of the cell; the cell blocks only by its trunk |
| ![[tile-spruce_1.png\|70]] spruce_1 | 53 | Spruce, second variant (mirrored art) |
| ![[tile-spruce_2.png\|70]] spruce_2 | 54 | Spruce, third variant |
| ![[tile-tamarack.png\|70]] tamarack | 80 | Tamarack: 60% of Lowland trees, 10% of Boreal |
| ![[tile-aspen.png\|70]] aspen | 97 | Aspen: the parkland tree (85% of Parkland trees), 30% of Great Lakes forest, 14% of Boreal |
| ![[tile-jackpine.png\|70]] jackpine | 98 | Jack pine: Shield (50%), Great Lakes forest (40%) and mountains (55%) |
| ![[tile-dead_tree.png\|70]] dead_tree | 86 | Standing dead timber: 4 to 8% of trees in Boreal, Shield and Lowland |
| ![[tile-willow.png\|70]] willow | 81 | Willow scrub on every river and lake bank, parkland and tundra |
| ![[tile-willow_winter.png\|70]] willow_winter | 96 | In the tileset; the leafless winter willow is not yet swapped in |
| ![[tile-reeds.png\|70]] reeds | 84 | Reeds on banks and muskeg |
| ![[tile-muskeg_pool.png\|70]] muskeg_pool | 85 | A pool of water in the Hudson Bay lowland |
| ![[tile-rocks.png\|70]] rocks | 82 | Rock clutter on most ground |
| ![[tile-boulder.png\|70]] boulder | 83 | Boulders on Shield, Cordillera, tundra and prairie |
| ![[tile-snowdrift.png\|70]] snowdrift | 89 | In the tileset; not yet placed |

## Berries (design doc 13.5)

| Tile | Source | Used for |
|---|---|---|
| ![[tile-saskatoon_bush.png\|70]] saskatoon_bush | 99 | Saskatoon in fruit (July-August) |
| ![[tile-saskatoon_picked.png\|70]] saskatoon_picked | 100 | Saskatoon stripped (shown for 30 days after a picker) or out of season |
| ![[tile-cranberry_patch.png\|70]] cranberry_patch | 101 | Lowbush cranberry in fruit (August-October) |
| ![[tile-cranberry_picked.png\|70]] cranberry_picked | 102 | Cranberry picked over or out of season |
| ![[tile-rosehip_bush.png\|70]] rosehip_bush | 103 | Rosehip in fruit, July to February: the one that hangs on all winter |
| ![[tile-rosehip_bare.png\|70]] rosehip_bare | 104 | Rosehip bare (March-June) or stripped |
| ![[tile-bushes_snow.png\|70]] bushes_snow | 105 | A mound of snow where a saskatoon or cranberry stands in January-March |
| ![[tile-berry_basket.png\|70]] berry_basket | 106 | Defined (`BerryBasket`) in the tileset; not placed by the generator |

## Camp and post props

| Tile | Source | Used for |
|---|---|---|
| ![[tile-tipi.png\|70]] tipi | 55 | The conical lodge of every Cree, Assiniboine and plains camp. A tipi cell counts as indoors |
| ![[tile-campfire.png\|70]] campfire | 56 | The centre of every camp and every lit fire in the open. Also the flame sprite for a burning building (`Blaze`) |
| ![[tile-woodpile.png\|70]] woodpile | 57 | The yard woodpile of every post and camp; stackable and a menu of stack/take |
| ![[tile-woodpile_winter.png\|70]] woodpile_winter | none | Art only; not in the tileset |
| ![[tile-fur_bale.png\|70]] fur_bale | 58 | Bales in warehouses, post houses and as post props |
| ![[tile-crate.png\|70]] crate | 35 | Boxes in warehouses, stores and yards |
| ![[tile-hay_bales.png\|70]] hay_bales | 36 | Hay bales in warehouses, stores, the boat shed and the York yard |
| ![[tile-drying_rack.png\|70]] drying_rack | 90 | A prop in the planned post layouts (Edmonton House, Fort Gibraltar) |
| ![[tile-flagstaff.png\|70]] flagstaff | 93 | A prop in the planned post layouts (Fort William, Moose Factory, Edmonton, Cumberland, Chipewyan, Gibraltar) |
| ![[tile-canoe_beached.png\|70]] canoe_beached | 92 | A canoe drawn up at every portage landing and as the "canoe" prop of planned posts |
| ![[tile-cache.png\|70]] cache | 95 | A food cache at every portage landing |
| ![[tile-hearth_left.png\|70]] hearth_left | 49 | The hearth on the back-left wall of every heated building; each one becomes a fire object with fuel and light |
| ![[tile-hearth_right.png\|70]] hearth_right | 50 | In the tileset; not placed |
| ![[tile-ladder.png\|70]] ladder | 37 | In the tileset; not placed |
| ![[tile-cannon.png\|70]] cannon | none | Art only (a post cannon); the gun heard at the ship's arrival is a sound, not this tile |
| ![[tile-fur_press.png\|70]] fur_press | none | Art only (a fur press) |
| ![[tile-grave.png\|70]] grave | 94 | In the tileset (`graveyard path` is named in York's creek code); not placed |
| ![[tile-pelt_frame.png\|70]] pelt_frame | none | Art only (a pelt frame) |
| ![[tile-hide_frame.png\|70]] hide_frame | none | Art only (a hide-stretching frame) |
| ![[tile-sled.png\|70]] sled | none | Art only (a sled) |

## Hunting and fishing

| Tile | Source | Used for |
|---|---|---|
| ![[tile-beaver_lodge.png\|70]] beaver_lodge | 87 | Painted in the water beside every beaver site so you can see the lodge |
| ![[tile-beaver_dam.png\|70]] beaver_dam | 88 | In the tileset; not placed |
| ![[tile-fish_trap.png\|70]] fish_trap | 91 | In the tileset; not placed |
| ![[tile-leg_trap.png\|70]] leg_trap | none | Art only (a leg-hold trap) |
| ![[tile-ice_hole.png\|70]] ice_hole | none | Art only (an ice-fishing hole) |

## Missouri towns (design doc 13.1)

| Tile | Source | Used for |
|---|---|---|
| ![[tile-earth_lodge.png\|70]] earth_lodge | 107 | The dome-shaped earth lodge of the Mandan, Hidatsa and Arikara towns (April to November) |
| ![[tile-earth_lodge_winter.png\|70]] earth_lodge_winter | 108 | The same lodge in December-March |
| ![[tile-garden_plot.png\|70]] garden_plot | 109 | A garden plot of corn, beans and squash (June-September) |
| ![[tile-garden_harvested.png\|70]] garden_harvested | 110 | The plot after harvest (October-May) |
| ![[tile-corn_scaffold.png\|70]] corn_scaffold | 111 | A corn-drying scaffold in the village |
| ![[tile-cache_pit.png\|70]] cache_pit | 112 | A storage cache pit |
| ![[tile-corn_mortar.png\|70]] corn_mortar | 115 | A corn mortar beside the central fire |
| ![[tile-village_palisade.png\|70]] village_palisade | 113 | Two stretches of the village palisade |
| ![[tile-bullboat.png\|70]] bullboat | 114 | A round hide boat on a willow frame at the river |

## Where the props go

- **A post's yard.** Every standing post paints a woodpile and a crate inside the gate; planned posts add the props in `post_plans.json` (flagstaff, fur bale, drying rack, canoe, woodpile, crate). Posts without a plan have only the woodpile and crate.
- **A camp.** A campfire at the centre, five tipis at (3,0), (0,3), (-3,0), (0,-3), (3,3) and a woodpile at (2,-2). See [[Cree Camps]] and [[Country Camps]].
- **A Missouri town.** A fire, six earth lodges at (3,0), (0,3), (-3,0), (0,-3), (3,3), (-3,-3), a corn scaffold (2,-2), a cache pit (-2,2), a corn mortar (1,1), two garden plots (5,2) and (2,5), two stretches of village palisade (-5,0) and (0,-5) and a bullboat (-4,3). Lodges and plots change with the season.
- **A portage.** A sign naming the carry, a beached canoe at (2,1) and a cache at (-1,2) from the landing. See [[Routes and Portages]].
- **York.** Woodpile (18,7), a crate (19,25), hay bales (20,26); two canoes (the code's `Canoe` object uses a sprite from `assets/sprites`, not a tile).

## Tips

- The tree and clutter tiles are not scenery only: woodcutters fell the nearest spruce for 4 firewood; berries can be picked (see [[Gathering and Gardens]]).
- A tipi cell counts as indoors; a campfire in the open loses its heat to the wind where an enclosed hearth does not (see [[Fire and Warmth]]).

## See also

[[Buildings]] - [[Terrain and Biomes]] - [[Country Camps]] - [[Asset Inventory]] - [[Missouri Villages]]
