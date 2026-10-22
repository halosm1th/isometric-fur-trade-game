---
title: Asset Inventory
aliases: [Assets, Art Inventory, Asset List]
tags: [development, assets, art, audio]
status: implemented
---

# Asset Inventory

**Status:** Implemented (a catalogue). Based on `docs/asset-inventory.md` (rewritten 2026-10-11, last brought up to date 2026-10-22 against `717a843`). Where the inventory doc and the current code disagree, this page follows the code and says so. What is still to be drawn or recorded is in `docs/asset-prompts.md` and `docs/audio-prompts.md`.

**New since 13 October, still in borrowed art:** the Lakes towns' soldiers, officers, farmers, tradesmen, merchants and priests (tinted labourer, Factor and clerk sheets); the Haudenosaunee, Wyandot, Sauk, Meskwaki, Ho-Chunk and Menominee (the Cree hunter's sheet); the elk (the caribou's); the horse (a generated grey stand-in); every town building (the York log walls); and the coin, roots, wheat, grain and meal icons. **Cut and wired since 13 October:** the weather and aurora, the cursors, the UI panels, the combat effects, and fire in every hearth.

Status key: **Final** (game-specific art in use), **Placeholder** (in use but a stand-in), **Cut, unwired** (art in the repo, nothing loads it).

## Characters (assets/sprites/, 21 sheets)

Which sheet a role wears is decided by `Agents/Appearance.cs`. New sheets are 180x170 per frame; the old `player`, `cree` and `rival` are 150x130.

| Sheet | Worn by | Status |
|---|---|---|
| `player` (79 frames) | The player | Final: the most complete sheet |
| `voyageur`, `clerk`, `nwc_partner`, `sailor`, `country_wife`, `labourer` | Matching roles | Final |
| `cree`, `cree_woman`, `cree_captain`, `assiniboine` | Cree hunters, women, chief; Assiniboine | Final |
| `child_boy`, `child_girl` | HBC children under 14 | Final |
| `factor` | The Factor (added 11 Oct, replacing the NWC partner's red coat) | Final |
| `blackfoot`, `dene`, `coast`, `dakota`, `mandan`, `plateau`, `ojibwe` | The nations of the wider country (added 11 Oct, `8a69ec7`) | Final |
| `rival` (13 frames) | NWC voyageurs in one spawn path (`Starts.cs`); otherwise obsolete | Placeholder: the last old-scale sheet |

Missing: the Métis have no sheet of their own (they use existing ones); nations with no sheet fall through `Appearance.For()` to whatever they spawned with. Prompts: `docs/asset-prompts.md` section 2. See [[Characters]] and the portraits embedded on each person's page.

## Animals (assets/sprites/animals/, 6 species)

All are full 4-way sets (SE/SW/NE/NW). The old loose two-frame files in `assets/sprites/animals/*.png` are unreferenced.

| Species | Frames | Animations | Status |
|---|---|---|---|
| Wolf | 96 | idle, walk, run, attack, howl, death | Final |
| Caribou | 80 | idle, walk, run, death, plus cow idle and walk | Final |
| Beaver | 112 | idle, walk, gnaw, swim, slap, death | Final |
| Goose | 84 | idle, walk, takeoff, fly, death | Final |
| Hare | 104 | idle, walk, run, death x winter and summer coat | Final, wired 11 Oct |
| Ptarmigan | 104 | idle, walk, fly, death x winter and summer plumage | Final, wired 11 Oct |
| **Buffalo** | n/a | none: uses `CaribouFrames.tres` | **Placeholder** (prompt: asset-prompts 4.1) |
| **Elk** | n/a | none: uses `CaribouFrames.tres` | **Placeholder** (prompt: asset-prompts 4.2) |
| **Horse** | 80 | idle, walk, run, graze (a generated stand-in) | **Placeholder** (prompt: asset-prompts 4.3) |

The animal controller selects only idle, walk, run or fly, attack and death; howl, gnaw, swim, slap, takeoff and the cow variants are cut but unused. See [[Wildlife]].

![[sheet-animal-wolf.png|400]]

## Items (assets/sprites/items/)

51 icons at 64x64; **38 ItemIds** in `Items.cs`. The three borrowed icons (berries, spruce beer, corn) were drawn on 11 Oct (`8a69ec7`) and now have their own. Ptarmigan borrows the bird's own death frame (placeholder). Roughly 15 icons are cut with no ItemId yet (`bread_plate`, `dispatch`, `fur_pack`, `journal`, `ledger`, `letter`, `medicine_bag`...); many were wired on 11 October (awl, beads, fishing net, ice chisel, mb_token, powder horn, stroud, tobacco). See [[Items]].

![[sheet-items-all.png|500]]

## Terrain, buildings and props (assets/tiles/)

Painted through `terrain_tileset.tres` (more than 100 sources now) by `TerrainPainter` and `StructurePainter`.

| Group | Status |
|---|---|
| Landscape (10 Kenney tiles + road) | Placeholder (flat colour, no edge transitions) |
| Regional grounds (prairie, tallgrass, parkland, tundra, Shield, muskeg, mountain) | Placeholder: recolours of the grass tile (`tools/world/make_region_tiles.py`) |
| Walls, windows, doors, corners, palisade fence | Final since 11 Oct: whitewashed log and stake palisade from the Gemini factory sheet (`tools/sprites/walls.py`) |
| Floor planks, roof, directional roofs | Placeholder (Kenney); 8 directional `roof_*` tiles unwired |
| Furniture (bed, chair, table, shelves, rug, display case, candle stand) | Placeholder (Kenney); `stove` and `writing_desk` are final but unwired |
| Props (37+) | Wired: campfire, crate, fur_bale, hay_bales, hearths, ladder, spruce, tipi, woodpile, plus (per the code) tamarack, willow, rocks, boulder, reeds, muskeg_pool, dead_tree, aspen, jackpine, beaver_lodge, canoe_beached, cache, flagstaff, berry bushes, gardens, earth lodge, and others added from 10 to 11 Oct |
| Still unwired per the inventory | beaver_dam, cannon, drying_rack, fish_trap, fur_press, grave, hide_frame, ice_hole, leg_trap, pelt_frame, sled, snowdrift, woodpile_winter... (check the code before relying on this list: the placement rules in `StructurePainter` have grown since the inventory was written) |

![[gallery-props.png|600]]
![[gallery-landscape.png|600]]

See [[Terrain and Biomes]], [[Buildings]], [[Props and Furniture]].

## Vehicles

| Asset | Status |
|---|---|
| `canoe.png` | Final: loaded by `Canoe.cs` and the player |
| `ship.png` | Final: loaded by `Ship.cs` |
| `vehicles/canoe_loaded.png`, `york_boat.png`, `dog_sled.png` | Cut, unwired |

![[vehicle-canoe.png|160]] ![[vehicle-ship.png|220]] ![[vehicle-york_boat.png|260]] ![[vehicle-dog_sled.png|160]]

See [[Transport]].

## UI

| Asset | Status |
|---|---|
| `icon.png` (256x256) | Final: `config/icon` |
| `menu_background.jpg` (York Factory at dusk) | Final: `MainMenu.tscn` |
| 12 need icons (cold, drunk, fatigue, fever, food, health, load, scurvy, standing, warmth, water, wounded) | Partly wired: the HUD shows four rows (Health, Warmth, Food, Water); the other eight are cut and unused |
| 4 faction crests (hbc, nwc, cree, assiniboine) | Cut, unwired |
| Panels, buttons, tabs, divider, scrollbar (nine-patch) | Final since 20 Oct |
| 5 cursors; weather overlays and aurora | Final |

No `.ttf` or `.otf` exists; seven call sites use `ThemeDB.FallbackFont`. See [[Journal, Map and UI]].

![[ui-crest-hbc.png|90]] ![[ui-crest-nwc.png|90]] ![[ui-crest-cree.png|90]] ![[ui-crest-assiniboine.png|90]]

## Drawn in code (no art file)

A burning building (tinted campfire plus a PointLight2D: the large-fire sheet must be redrawn), health and ready bars, the map's post markers, world labels. Hearth fires, combat effects and weather now have art. See [[Weather]].

## Audio

70 files: 6 music tracks, 10 ambient loops, 52 effects, 2 UI sounds. See [[Audio]], [[Music]], [[Sound Effects]].

## External packs (assets/external/, 5,343 files)

Kenney Landscape, Buildings, Library, Farm (CC0, used); Kenney Dungeon and Prototype Tiles (CC0, unused); `critters/`, `isometric tileset/`, `Assets/` (no licence file, unused); `pioneerpixel_hbc_trader` (style reference); `NEW HBC ASSETS` (the Gemini source sheets). About 1,600 files could go. See [[Credits and Licenses]].

## The wiring backlog

Finished art the game does not load, per the inventory: the remaining unplaced props; the eight unused need icons; the four crests; summer-coat selection (since done for hare and ptarmigan); `stove` and `writing_desk`; the three vehicles; orphan frames (`musket_ready` x2, `player/idle_back`, `sneak_back_3`, `assiniboine/shoot_*`); the 15 icons with no ItemId. "No new art is needed for anything on this list." See [[Roadmap]].

## See also

[[Development Status]] | [[Roadmap]] | [[Credits and Licenses]] | [[Architecture]] | [[Audio]] | [[Wildlife]]
