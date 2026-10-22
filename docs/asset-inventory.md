# Asset Inventory

Last verified against the repo: 2026-10-22 (sections "What changed since 2026-10-13", 2, 3, 6 and 7; the rest as of 2026-10-13)

What art the game has, where it came from, what is still a stand-in, and what is cut but not yet wired up. Rewritten 2026-10-11 against commit `b2064f4`, brought up to date 2026-10-13 against `1c73b55`, and again 2026-10-22 against `717a843`. Checked by following what the code actually loads (`GD.Load` calls, the `*Frames.tres` resources, `terrain_tileset.tres`, `Items.cs`, `Appearance.cs`, `NeedsHud.cs`, `Audio.cs`) and by listing `assets/**` against those references.

**This file is the catalogue of what exists.** Art the game does *not* have is not listed here — it lives in the companion docs, which this file deliberately does not duplicate:

- `asset-prompts.md` — art still to be made, as Gemini prompts with reference sheets. Outstanding work only: the towns' people and buildings, the women of every nation and the new Lakes peoples, the buffalo, elk and horse, the borrowed item icons, fonts and portraits, the large fire, and the animals the code does not have.
- `audio-prompts.md` — what sound is in the game (70 files) and what is still wanted.
- `world-content-needed.md` — nations, people, places and species missing from the code.

Status key: **Final** (game-specific art, loaded and in use) · **Placeholder** (in use, but a stand-in: stock pack, recolour or an older sheet still wired up) · **Cut, unwired** (art is in the repo and imported, nothing loads it) · **Drawn, not cut** (a finished Gemini sheet sits in `assets/external/NEW HBC ASSETS/` but has not been sliced into `assets/`).

Where this file notes a gap, it is only to explain the status of a file that exists. The job of filling it is described in the companion docs.

---

## What changed since 2026-10-13

- **Cut and wired** (most of it on 2026-10-12, which the 13 Oct pass missed): the weather overlays and the aurora (`assets/ui/weather/`, played by `UI/WeatherOverlay.cs`), the five cursors (`assets/ui/cursors/`, `UI/Cursors.cs`), the combat effects (`assets/sprites/fx/`: muzzle and pan flash, smoke, blood hit and pool, impact dust, slash, arrow, blast, lightning; `Agents/CombatFx.cs`), the hearth and campfire flames, embers, smoke and sparks (`746b483`), and the UI panels, buttons, tabs, divider and scrollbar as nine-patches (`assets/ui/panels/`, `acd6e17`). Section 7's table below is kept only for the sheets still uncut.
- **New ground tiles:** `scree`, `alpine`, `snowfield` for the mountains (recolours, `fe645f8`).
- **New stand-ins, no new art:** the horse is a generated grey pony (`assets/sprites/animals/horse_standin/`, 80 frames, `tools/sprites/make_horse_standin.py`), tinted to the coat. `Species.Elk` (the southern woods and parklands, `cbfc52d`) wears the caribou's frames, as the buffalo does.
- **New people, all in borrowed sheets.** The Lakes towns (`0423a15`) placed 683 soldiers, 38 officers, 333 farmers, 53 artisans, 19 merchants and 4 priests, plus town women and children, dressed by `tools/world/place_towns.py` (`look()`) in tinted labourer, Factor, clerk, voyageur and country-wife sheets. The Haudenosaunee, Wyandot, Sauk, Meskwaki, Ho-Chunk and Menominee (`cbfc52d`) fall through `Appearance.ForNation` to the Cree hunter and the Cree woman.
- **New buildings, old walls.** `BuildingKind` gained Townhouse, Church, Barracks, Store, Workshop, Hall, Cabin and House. All are the whitewashed log set with Kenney furniture (pews are `table_chairs`). The farms' fields (`World/Fields.cs`) paint as the Missouri garden plot.
- **Items: 53 `ItemId`s** (was 48). `Roots`, `Wheat`, `Grain`, `Flour` and `Coin` were added, and all five borrow another item's icon (section 3).
- **Audio:** unchanged, still 70 files.

What all of that still needs is in `asset-prompts.md` (art) and `audio-prompts.md` Priority 0 (sound).

## What changed between 2026-10-11 and 2026-10-13

- Character sheets went from **14 to 22**: a `factor` sheet and seven nation men (`ojibwe`, `blackfoot`, `dakota`, `dene`, `plateau`, `coast`, `mandan`) arrived in `8a69ec7`. `Appearance.ForNation` now covers all ten nations' men.
- Items: **48 `ItemId`s** (was 38). Berries, corn and spruce beer have icons of their own.
- Props went from 37 to **56** files and the tileset from 61 to **105 sources**: regional grounds and trees, berry bushes, the Missouri earth-lodge set, and most of the previously unwired props are now in the tileset.
- **Species.cs has seven species**: hare and ptarmigan are in (their seasonal coat is selected from the calendar), and `Buffalo` was added, drawn with the caribou's frames.
- Audio: the first batch is in (70 files, played by `Core/Audio.cs`; section 8).
- The effects, weather, panel, cursor and night-sky sheets were drawn (raw JPGs in `assets/external/NEW HBC ASSETS/`) but are **not cut or wired** (section 7).

---

## 1. Characters (`assets/sprites/`, 22 sheets)

Which sheet a role wears is decided by `Agents/Appearance.cs`: `For()` swaps the real sheet in when an NPC's look is set, and `ForNation()` picks a nation's sheet (also used for the player's own start, `CharacterSheet.cs`). Older sheets stay wired in `Population.cs` / `Starts.cs` / the scene as the spawn-time default, so a role not listed in `Appearance.For()` keeps whatever it was spawned with.

New sheets are 180×170 per frame; the three original sheets (`player`, `cree`, `rival`) are 150×130.

| Sheet | Files | Animations | Worn by | Status |
|---|---|---|---|---|
| `player` | 79 | idle, idle_side, walk, walk_back, walk_side, run_side, sneak ×4, shoot, attack, eat, drink, paddle, carry, carry_side, chop, sleep, shiver, drunk, death | Player | **Final** — the most complete sheet in the game |
| `voyageur` | 39 | idle ×3, walk ×3, attack, eat, drink, portage, paddle, death | NWC voyageurs; Métis men | **Final** |
| `clerk` | 31 | idle ×3, walk ×3, eat, drink, write, death | Clerks, postmasters | **Final** |
| `factor` | 34 | idle ×3, walk ×3, eat, drink, talk, write, death | The Factor | **Final** |
| `nwc_partner` | 36 | idle ×3, walk ×3, eat, drink, shoot, talk, death | NWC partners | **Final** |
| `sailor` | 31 | idle ×3, walk ×3, drink, attack, haul, death | Ship's crew | **Final** |
| `country_wife` | 43 | idle ×3, walk ×3, eat, drink, carry_wood, carry_water, carry_baby, scrape, death | HBC women and wives; Métis women | **Final** |
| `labourer` | 47 | idle ×3, walk ×3, shoot, attack, eat, drink, haul, chop, death | Orkney labourers | **Final** |
| `cree` | 41 | idle ×3, walk ×3, run_side, shoot, attack, butcher, eat, drink, death | Cree hunters (and the default for any nation without its own) | **Final** |
| `cree_woman` | 37 | idle ×3, walk ×3, eat, drink, carry_wood, scrape, death | Cree women and daughters, and the women of **every other nation** | **Final** for the Cree; stand-in for the rest |
| `cree_captain` | 31 | idle ×3, walk ×3, eat, talk, smoke, death | Cree and HBC-side chiefs | **Final** |
| `assiniboine` | 40 | idle ×3, walk ×3, attack, shoot, musket, eat, drink, death | Assiniboine men | **Final** |
| `ojibwe` | 44 | idle ×3, walk ×3, attack, shoot, musket, eat, drink, death | Ojibwe men | **Final** |
| `blackfoot` | 41 | same rows as `ojibwe` | Blackfoot men and chiefs | **Final** |
| `dakota` | 43 | same rows | Sioux (`Faction.Sioux`) men | **Final** |
| `dene` | 33 | same rows | Dene men | **Final** |
| `plateau` | 37 | same rows | Plateau men | **Final** |
| `coast` | 33 | same rows | Coast men | **Final** |
| `mandan` | 45 | same rows | Missouri (`Faction.Missouri`) men | **Final** |
| `child_boy` | 17 | idle ×3, walk ×3, run_side | HBC boys under 14 | **Final** — no eat/death, deliberate |
| `child_girl` | 21 | idle ×3, walk ×3, run_side, eat | HBC girls under 14 | **Final** |
| `rival` | 13 | idle, walk, walk_back, eat | Nothing by design; still the spawn-time default (see notes) | **Placeholder** — the last of the old 150×130 sheets. No side walk, no death, no drink. |

**Notes on the sheets that exist**

- **Every nation's men have a sheet.** `Appearance.ForNation` maps all ten factions: Cree (default), Assiniboine, Ojibwe, Dene, Blackfoot, Sioux → `dakota`, Plateau, Coast, Missouri → `mandan`, and Métis → `voyageur`.
- **Only the men are done.** `ForNation(f, female: true)` returns `CreeWomanFrames` for every nation except the Métis (who get `CountryWifeFrames`), so a Blackfoot or Coast woman currently wears a Cree woman's sheet. Prompts: `asset-prompts.md` §2.8.
- **`rival` is no longer anyone's final look**, but it is still referenced: `Starts.cs:172` spawns the NWC start's two voyageurs with it (`Appearance.For` corrects them afterwards, so that is cosmetic), `CharacterSheet.cs:146` gives an **NWC-path player** `RivalFrames` directly (so that player has no side walk, drink or death animation), and `YorkFactoryExterior.tscn` uses it as the placeholder frames on its scene-placed NPCs until `Appearance` swaps them. The Factor now loads `FactorFrames` (`Population.cs:32`).
- **Orphan frames** that exist but no `.tres` references (9 files): `player/idle_back.png`, `player/sneak_back_3.png`, `player/musket_ready.png`, `labourer/musket_ready.png`, `assiniboine/shoot_{0,1,2}.png`, `cree/shoot_{0,1}.png`. The two `musket_ready` frames look like a deliberate aiming pose that never got an animation entry — no new art needed, just rows in the `.tres`.

---

## 2. Animals (`assets/sprites/animals/`, 6 sheets for 7 species)

Every sheet is a full 4-way set (SE/SW/NE/NW) cut into per-species folders. The old loose two-frame files are still in `assets/sprites/animals/*.png` and are **unreferenced** — 20 files safe to delete.

| Species | Files | Animations | In `Species.cs`? | Status |
|---|---|---|---|---|
| Wolf | 96 | idle, walk, run, attack, howl, death | Yes | **Final** |
| Caribou | 80 | idle, walk, run, death + `cow_idle`, `cow_walk` | Yes | **Final** — cow variant is never selected by `Animal.cs` |
| Beaver | 112 | idle, walk, gnaw, swim, slap, death | Yes | **Final** |
| Goose | 84 | idle, walk, takeoff, fly, death | Yes | **Final** — migration is visible |
| Hare | 104 | idle, walk, run, death × winter **and** `summer_*` | Yes (`Species.Hare`) | **Final** |
| Ptarmigan | 104 | idle, walk, fly, death × winter **and** `summer_*` | Yes (`Species.Ptarmigan`) | **Final** |
| Buffalo | — | none: `Species.Buffalo` points at `CaribouFrames.tres` | Yes (`Species.Buffalo`, `9110382`) | **Placeholder** — a bull plains buffalo drawn as a caribou. Prompt: `asset-prompts.md` 4.1 |
| Elk | — | none: `Species.Elk` points at `CaribouFrames.tres` | Yes (`cbfc52d`) | **Placeholder** — drawn as a caribou. Prompt: `asset-prompts.md` 4.2 |
| Horse | 80 (`horse_standin/`) | idle, walk, run, graze, 4-way | Not a `Species`: `Agents/Mount.cs`, `Economy/Herd.cs` | **Placeholder** — a generated grey pony tinted to the coat; `Mount.cs` takes `HorseFrames.tres` when it exists. Prompt: `asset-prompts.md` 4.3 |

`Animal.cs` (`Anim()`) picks `idle`, `walk`, `run` or `fly`, `attack` and `death`, and **does** switch hare and ptarmigan between the white winter coat and `summer_*` (white only in `Season.Winter`; Breakup, OpenWater and FreezeUp all use the summer coat). Cut but never selected: wolf `howl`, beaver `gnaw` / `swim` / `slap`, goose `takeoff`, caribou `cow_*`. No new art is needed for those, only behaviour that plays them.

---

## 3. Items (`assets/sprites/items/`, 54 icons at 64×64)

**53 `ItemId`s** in `Items.cs`. Every one has an icon, but seven do not have a file of their own (prompts: `asset-prompts.md` 5):

| Item | Icon it uses | Status |
|---|---|---|
| `BuffaloRobe` | `caribou_hide.png` (shared with `CaribouHide`) | **Placeholder** — a robe is a dressed hide with the hair on and should look like one. |
| `Ptarmigan` | `animals/ptarmigan/death_se_0.png`, the bird's own death frame | **Placeholder** |
| `Roots`, `Wheat` | `corn.png` | **Placeholder** (the towns, 2026-10-21) |
| `Grain` | `pack_basket.png` | **Placeholder** |
| `Flour` | `pemmican_bag.png` | **Placeholder** |
| `Coin` | `mb_token.png` | **Placeholder** (`Economy/Money.cs`, 2026-10-22) |

The other 46 items each load their own file. `Berries` (`berries.png`), `Corn` (`corn.png`) and `SpruceBeer` (`spruce_beer.png`) were drawn in `8a69ec7`; `Powder` uses `powder_horn.png`; `salt_geese.png` is the goose cask.

**8 icons are cut but no `ItemId` loads them** — drawn, imported, and waiting on code:

`bread_plate` · `dispatch` · `fur_pack` · `journal` · `ledger` · `letter` · `medicine_bag` · `pemmican_bag`

(`journal`, `ledger`, `letter` and `dispatch` are drawn for the message and accounts systems, which already run without a physical object; `medicine_bag` for scurvy and wounds; `fur_pack` for the packed bale; `bread_plate` and `pemmican_bag` as alternative food pictures.) The individually-named trade goods — tobacco, beads, stroud, awls, powder — are now their own `ItemId`s alongside the generic `TradeGoods`.

---

## 4. Terrain, buildings and props (`assets/tiles/`)

Painted through `terrain_tileset.tres` (**105 sources**, 104 distinct textures; `floor_planks` is used twice) by `TerrainPainter` / `StructurePainter`, using the source ids in `WorldLayout.cs` / `StructurePainter.cs`.

### Landscape — **Placeholder** (17 files)

`dirt` `grass` `hill` `ice` `road` `sand` `snow` `stone_floor` `water` `water_ice` (132×99, Kenney Isometric Landscape, CC0), plus seven regional grounds recoloured from the Kenney tiles by `tools/world/make_region_tiles.py`: `prairie` `tallgrass` `parkland` `tundra` `shield` `muskeg` `mountain`. All 17 are in the tileset. Still stock or recolour: flat colour, no edge transitions, no painted marsh, rock or mountain art. Seamless tiles are the thing Gemini does worst, so these wait until the tile shapes have settled.

### Buildings — walls and palisade **Final**, floor and roofs **Placeholder** (38 files)

The 24 wall, window, door, open-door and corner tiles are the **whitewashed log set** and the 6 fence tiles are the **palisade** (pointed stakes), both from the Gemini sheet `NEW HBC ASSETS/Facotry Segment Sprites.jpg`. `tools/sprites/walls.py` fits each piece onto the old Kenney tile's exact edges (Kenney originals are in git history). Still Kenney: `floor_planks`, `roof` and the 8 directional roof pieces. **The same sheet also holds, drawn but unused:** the weathered log set (plain, window, door open and closed, corner), the palisade gates (closed and open, both facings), a grey plank floor tile and a steep plank roof tile. Using them is a slicing and fitting job (extend `walls.py`), not a new-art job. Only flat `roof.png` is in the tileset; the 8 `roof_*` / `roof_corner_*` tiles are **unwired**.

### Furniture — mixed (11 files)

`bed` `candle_stand` `chair` `display_case` `rug` `shelf` `shelf_tall` `table` `table_chairs` are Kenney **Placeholder**, in the tileset and placed by `StructurePainter`. `stove.png` and `writing_desk.png` are new game art but **cut, unwired** — neither is in `terrain_tileset.tres`.

### Props — 56 files, 48 in the tileset

**In the tileset (48):** `aspen` `beaver_dam` `beaver_lodge` `berry_basket` `boulder` `bullboat` `bushes_snow` `cache` `cache_pit` `campfire` `canoe_beached` `corn_mortar` `corn_scaffold` `cranberry_patch` `cranberry_picked` `crate` `dead_tree` `drying_rack` `earth_lodge` `earth_lodge_winter` `fish_trap` `flagstaff` `fur_bale` `garden_harvested` `garden_plot` `grave` `hay_bales` `hearth_left` `hearth_right` `jackpine` `ladder` `muskeg_pool` `reeds` `rocks` `rosehip_bare` `rosehip_bush` `saskatoon_bush` `saskatoon_picked` `snowdrift` `spruce_0/1/2` `tamarack` `tipi` `village_palisade` `willow` `willow_winter` `woodpile`.

Placed by code (checked): trees and shrubs by region (`WorldLayout.cs`), the berry bushes (in fruit, picked, bare, snow), `beaver_lodge`, `canoe_beached`, `cache` (`StructurePainter.cs:231-239`), the Missouri earth-lodge towns, `flagstaff` and `drying_rack` from `Data/World/post_plans.json`. **In the tileset but no placement code found** (a grep for their source ids and names): `beaver_dam`, `snowdrift`, `fish_trap`, `grave`, `willow_winter`, `berry_basket` (a constant exists for it in `WorldLayout.cs` but nothing uses it).

**Cut, unwired (8, not in the tileset):** `cannon` `fur_press` `hide_frame` `ice_hole` `leg_trap` `pelt_frame` `sled` `woodpile_winter`.

Many of these back live systems — `leg_trap` and `fish_trap` for trapping and fishing, `ice_hole` for winter fishing, `beaver_dam` for the beaver ecology, `grave` for deaths, `pelt_frame` / `hide_frame` / `fur_press` for the fur economy. Giving `StructurePainter` placement rules for them would change how the world reads more than any new art.

---

## 5. Vehicles

| Asset | File | Status |
|---|---|---|
| Canoe | `sprites/canoe.png` | **Final** — loaded by `PlayerController.cs`, `Canoe.cs` |
| Supply ship | `sprites/ship.png` | **Final** — loaded by `Ship.cs` |
| Loaded canoe | `sprites/vehicles/canoe_loaded.png` | **Wired** — the NWC brigades' boats (`ConvoyView.cs`, `Data/World/brigade_runs.json`) |
| York boat | `sprites/vehicles/york_boat.png` (400×130) | **Wired** — the Company's brigades' boats (`ConvoyView.cs`, `Data/World/brigade_runs.json`) |
| Dog sled | `sprites/vehicles/dog_sled.png` | **Cut, unwired** |

Nothing in `Scripts/` or `Scenes/` references the three `vehicles/` files; they correspond to transport modes in design doc §12 that the code does not model yet.

---

## 6. UI (`assets/ui/`)

| Asset | Status |
|---|---|
| `icon.png` (256×256) | **Final** — set as `config/icon` |
| `menu_background.jpg` | **Final** — used by `MainMenu.tscn` |
| `needs/` — 12 icons at 48×48 | **Partly wired.** `NeedsHud.cs:47` builds the path from the row name, and `Rows` holds only four: Health, Warmth, Food, Water. So `cold` `drunk` `fatigue` `fever` `load` `scurvy` `standing` `wounded` are cut and unused — eight icons waiting on eight HUD rows for states the sim already tracks. |
| `crests/` — hbc, nwc, cree, assiniboine | **Cut, unwired.** Nothing loads a crest. Natural homes: the journal panel, dialogue headers, the map, the standing display. |
| `panels/` — panel_wood, panel_paper, button (×3 states), tab_on/off, divider, scrollbar | **Final** — nine-patches for every panel, menu, the journal and the map (`acd6e17`) |
| `cursors/` — default, talk, trade, attack, busy | **Final** — `UI/Cursors.cs` |
| `weather/` — snow light and heavy, blizzard, rain, fog (4 frames each), aurora | **Final** — `UI/WeatherOverlay.cs` |

**Fonts.** There is still no `.ttf` or `.otf` file in the repo; everything uses the engine default, and seven call sites use `ThemeDB.FallbackFont` directly (`MapPanel`, `ObserverView`, `TravelMode`, `Wayfinding`, `NpcSpawn`, `PostSign`, `WildlifeSite`). The font choice is `asset-prompts.md` 6.1.

---

## 7. Drawn, not cut, and still drawn in code

**Drawn but not cut or wired** — raw Gemini sheets in `assets/external/NEW HBC ASSETS/`; no script in `tools/sprites/` slices them and nothing loads them:

As of 2026-10-22 every sheet that was here has been cut and wired (fire, weather, aurora, combat effects, panels, cursors) except the **LARGE FIRE** row of `fire.jpg`, which sits on a baked-in stone wall and has to be redrawn (`asset-prompts.md` 7). `Visual FX Sheet.jpg` is a fantasy-spell sheet that does not suit the setting and is not used.

**Still drawn in code, no art file:**

- **A burning building** (`Blaze.cs`): a tinted, stretched `campfire.png` plus a `PointLight2D`. It now has to carry the towns burned in the war.
- **Health bar / ready bar:** `DrawRect`.
- **Map panel** (`MapPanel.cs`): parchment and brass now, with `Data/World/ground.png` and coloured squares for posts; the crests would fit here.
- **World labels** (`PostSign.cs`, `NpcSpawn.cs`, `WildlifeSite.cs`): fallback-font text.

---

## 8. Audio

**70 files** in `assets/audio/` (6 music, 10 ambient, 52 sfx, 2 ui), played by the `Audio` autoload (`Scripts/Core/Audio.cs`) on three buses (Music, Ambience, Effects) with volume sliders in Settings > Sound. What plays when, row by row, is in `audio-prompts.md` under **In the game now**.

| Folder | What's there |
|---|---|
| `assets/audio/music/` | 6 Gemini tracks (MP3): main menu theme, two winter pieces, one for breakup and freeze-up, a summer piece, the men's house reel |
| `assets/audio/ambient/` | Loops: winter wind, blizzard, river, mosquitoes, fort chatter, children playing, hearth fire, canoe paddling, ship creak, rain (synthesised) |
| `assets/audio/sfx/` | Footsteps (snow, snow crust, mud, planks: 5 takes each), axe (5), tree fall, musket fire and reload, distant cannon, punch (2), work bell, alarm bell, ice cracks (3), door, flint, dog sled, goose, raven, loon, wood creak, wolf howls (3), pack howl, growls (2), bite, body fall (2) |
| `assets/audio/ui/` | Page turn, quill |

The Freesound effects are CC0; sources in `assets/audio/CREDITS.md`. Synthesised stand-ins (planks, rain, wolves, body falls, musket reload; generated by `tools/audio/synth.py`) are original to the project. The raw downloads were kept in `assets/external/soundFX/`, which `.gitignore` excludes; that folder is **not present in this checkout** (and no `.gdignore` exists anywhere under `assets/external/`), so I could not check the claim that Godot skips it. `ui_quill_write` and `wood_creak` are on disk and nothing calls them. One download (`769368`, a blizzard) is CC BY-NC and is **not** used.

---

## 9. Scenes

`Scenes/World/` contains one scene: `YorkFactoryExterior.tscn`, and every start path begins in it (the rest of the country streams in round it). The main scene is `Scenes/UI/MainMenu.tscn`.

---

## 10. External packs (`assets/external/`)

5,463 files in git (about 2,750 once the `.import` sidecars are left out; the counts below leave them out). Nothing loads from `assets/external/` at runtime: "used" means the pack was copied into `assets/tiles/` or is slicer input for `tools/sprites/`.

| Pack | Files | Licence | Used? |
|---|---|---|---|
| `kenney_isometric-landscape` | 134 | CC0 | Yes — source of the terrain tiles |
| `kenney_isometric-buildings` | 136 | CC0 | Partly — floor and roof tiles (walls and fence were replaced) |
| `kenney_isometric-miniature-library` | 297 | CC0 | Partly — furniture |
| `kenney_isometric-miniature-farm` | 463 | CC0 | Partly — crate, hay bales |
| `kenney_isometricDungeon` (+ .zip) | 753 | CC0 | No |
| `kenney_isometricPrototypeTiles` (+ .zip) | 657 | CC0 | No |
| `critters/` (wolf, stag, badger, boar) | 114 | **no licence file** | No — superseded by the new animal sheets |
| `isometric tileset/separated images` | 116 | **no licence file** | No |
| `Assets/` (IsometricTRPG, 5 sheets) | 5 | **no licence file** | No |
| `pioneerpixel_hbc_trader` | 15 JPG | own work | Yes — style reference for every Gemini prompt; keep |
| `NEW HBC ASSETS` | 56 JPG | own work (Gemini) | Yes — the source sheets `tools/sprites/*.py` cut the characters, animals, items, props, crests, need icons and wall tiles from; keep. Also holds the uncut sheets in section 7. |

**Housekeeping.** `critters/` is fully superseded and can go. With the dungeon, prototype-tile, `isometric tileset` and `Assets/` packs that is roughly 1,650 files (about 3,300 with sidecars) and two `.zip`s that nothing uses, all scanned by Godot on every import. Moving them out of `assets/` or adding a `.gdignore` would speed up imports. The three packs with no licence file still need their terms recorded before any release.

---

## Wiring backlog — finished art the game does not load

**No new art is needed for anything on this list.** Every file below is already drawn, imported and sitting in the repo; each item is a code (or slicing) change.

| # | Job | Files it brings in | Where |
|---|---|---|---|
| 1 | Add the 25 unwired props as tileset sources with placement rules — traps, ice holes, beaver lodges, graves, pelt frames, flagstaff, cannon | 25 | `terrain_tileset.tres`, `StructurePainter.cs` |
| 2 | Add `Hare` and `Ptarmigan` entries pointing at the existing `.tres` files. (All six animal folders were sitting un-imported until 2026-10-11 — every png had no `.import` and the frame resources failed to parse, throwing 744 errors a run; `--headless --path . --import` fixed that, so the art is genuinely ready now.) | 208 frames | `Ecology/Species.cs` |
| 3 | Extend `Rows` to the eight unused need icons (cold, fever, fatigue, drunk, scurvy, wounded, load, standing) — all already simulated | 8 | `UI/NeedsHud.cs:20` |
| 4 | Season-switch the `summer_*` animations on hare and ptarmigan | — | `Agents/Animal.cs` |
| 5 | Spawn voyageurs with `VoyageurFrames` instead of `RivalFrames` | — | `Core/Starts.cs:159` |
| 6 | Load the four faction crests somewhere — journal, dialogue header, map, standing display | 4 | `UI/` |
| 7 | Add `stove.png` and `writing_desk.png` to the tileset | 2 | `terrain_tileset.tres` |
| 8 | Use `dog_sled` once that transport mode exists (`york_boat` and `canoe_loaded` are wired to the brigades) | 1 | `World/` |
| 9 | Give the orphan frames `.tres` rows (`musket_ready` ×2, `player/idle_back`, `sneak_back_3`, `assiniboine/shoot_*`) | 7 | `Scenes/*/`*`Frames.tres` |
| 10 | Wire the 15 item icons that have no `ItemId` | 15 | `Items/Items.cs` |
| 12 | Draw the three borrowed food icons (berries, spruce beer, corn) — the only `ItemId`s without art of their own | 3 | `asset-prompts.md` 5.5.1 |
| 13 | Berry bushes as a gatherable prop, and Mandan earth lodges for the three Missouri towns, which are farming towns in code and drawn as plains tipis | — | `asset-prompts.md` 5.5.2-3 |
| 11 | Add the 8 directional `roof_*` tiles, or delete them | 8 | `terrain_tileset.tres` |

## Repo hygiene

- Delete the 20 superseded loose PNGs in `assets/sprites/animals/*.png` — replaced by the per-species folders, referenced by nothing.
- Delete `assets/external/critters/` (114 files) — fully superseded by the new animal sheets.
- Consider a `.gdignore` on, or removal of, the unused external packs (`kenney_isometricDungeon`, `kenney_isometricPrototypeTiles`, `isometric tileset`, `Assets/`) and the two `.zip`s: ~1,650 files Godot scans on every import for no benefit.
- Record licences for `critters/`, `isometric tileset/` and `Assets/` before any release, or remove them.
- Delete `Visual FX Sheet.jpg` or move it out of `assets/`: it is off-theme and unused.
