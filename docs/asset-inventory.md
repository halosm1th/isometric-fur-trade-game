# Asset Inventory

What art the game has, where it came from, what is still a stand-in, and what is cut but not yet wired up. Rewritten 2026-10-11 against commit `b2064f4`, checked by following what the code actually loads (`GD.Load` calls, the `*Frames.tres` resources, `terrain_tileset.tres`, `Items.cs`, `Appearance.cs`, `NeedsHud.cs`) rather than by listing files.

**This file is the catalogue of what exists.** Art the game does *not* have is not listed here — it lives in the companion docs, which this file deliberately does not duplicate:

- `asset-prompts.md` — every piece of art still to be made, as Gemini prompts with reference sheets. Pruned to outstanding work only: the Factor sheet, the seven nations with no art, York Factory's log walls, weather and combat effects, and the UI theme.
- `audio-prompts.md` — all sound, which is still entirely unmade.
- `world-content-needed.md` — nations, people, places and species missing from the code.

Status key: **Final** (game-specific art, loaded and in use) · **Placeholder** (in use, but a stand-in: stock pack or an older sheet still wired up) · **Cut, unwired** (art is in the repo and imported, nothing loads it).

Where this file notes a gap, it is only to explain the status of a file that exists. The job of filling it is described in the companion docs.

---

## What changed since the last inventory (2026-09-20)

The Gemini art pass (`2cea7c4`) and the world-scale commit (`adc11ab`) cleared most of the old priority list:

- Character sheets went from **5 to 14**. Voyageurs, sailors, clerks, NWC partners, country wives, Cree captains, Assiniboine and children all have their own art instead of a tint.
- Animals went from 4 two-frame stand-ins to **6 species, fully 4-way, 80–112 frames each**, with seasonal coats for hare and ptarmigan.
- Items went from 32 to **51 icons**; every one of the 35 `ItemId`s now has its own, with no sharing.
- The UI got an icon, a menu background, **12 need icons** and **4 faction crests**.
- Props went from 12 to **37**; furniture gained a stove and writing desk; the terrain set gained a road.
- `icon.svg` (the Godot logo) is gone — `project.godot` points at `assets/ui/icon.png`.

Three things from the old list produced no files and so appear nowhere below: **audio**, a **UI theme and font**, and **effects art** for weather, fire and combat. All three are now prompted for in `asset-prompts.md` and `audio-prompts.md`.

---

## 1. Characters (`assets/sprites/`, 14 sheets)

Which sheet a role wears is decided by `Agents/Appearance.cs`, which swaps the real sheet in when an NPC's look is set. Older sheets stay wired in `Population.cs` / `Starts.cs` as the spawn-time default, so a role not listed in `Appearance.For()` keeps whatever it was spawned with.

New sheets are 180×170 per frame; the three original sheets (`player`, `cree`, `rival`) are 150×130.

| Sheet | Files | Animations | Worn by | Status |
|---|---|---|---|---|
| `player` | 79 | idle, idle_side, walk, walk_back, walk_side, run_side, sneak ×4, shoot, attack, eat, drink, paddle, carry, carry_side, chop, sleep, shiver, drunk, death | Player | **Final** — the most complete sheet in the game |
| `voyageur` | 39 | idle ×3, walk ×3, attack, eat, drink, portage, paddle, death | NWC voyageurs | **Final** |
| `clerk` | 31 | idle ×3, walk ×3, eat, drink, write, death | Clerks, postmasters | **Final** |
| `nwc_partner` | 36 | idle ×3, walk ×3, eat, drink, shoot, talk, death | NWC partners | **Final** |
| `sailor` | 31 | idle ×3, walk ×3, drink, attack, haul, death | Ship's crew | **Final** |
| `country_wife` | 43 | idle ×3, walk ×3, eat, drink, carry_wood, carry_water, carry_baby, scrape, death | HBC women and wives | **Final** |
| `labourer` | 47 | idle ×3, walk ×3, shoot, attack, eat, drink, haul, chop, death | Orkney labourers | **Final** |
| `cree` | 41 | idle ×3, walk ×3, run_side, shoot, attack, butcher, eat, drink, death | Cree hunters | **Final** |
| `cree_woman` | 37 | idle ×3, walk ×3, eat, drink, carry_wood, scrape, death | Cree women and daughters | **Final** |
| `cree_captain` | 31 | idle ×3, walk ×3, eat, talk, smoke, death | Cree chief | **Final** |
| `assiniboine` | 40 | idle ×3, walk ×3, attack, shoot, musket, eat, drink, death | Assiniboine | **Final** |
| `child_boy` | 17 | idle ×3, walk ×3, run_side | HBC boys under 14 | **Final** — no eat/death, deliberate |
| `child_girl` | 21 | idle ×3, walk ×3, run_side, eat | HBC girls under 14 | **Final** |
| `rival` | 13 | idle, walk, walk_back, eat | **Factor** (`Population.cs:32`), and NWC voyageurs in one spawn path (`Starts.cs:159`) | **Placeholder** — the last of the old 150×130 sheets. No side walk, no death, no drink. |

**Notes on the sheets that exist**

- **`rival` is the only sheet left behind**, and the Factor — a named, central NPC — still wears it (`Population.cs:32`). `Starts.cs:159` also spawns NWC voyageurs with it even though `VoyageurFrames` exists; `Appearance.cs` corrects them afterwards, so the stale spawn path is cosmetic but worth fixing. Replacement sheet: `asset-prompts.md` §1.
- **Seven of the nine nations on the map have no sheet of their own** and fall through `Appearance.For()` to whatever they were spawned with. Prompts: `asset-prompts.md` §2.
- **Orphan frames** that exist but no `.tres` references: `player/idle_back.png`, `player/sneak_back_3.png`, `player/musket_ready.png`, `labourer/musket_ready.png`, `assiniboine/shoot_{0,1,2}.png`. The two `musket_ready` frames look like a deliberate aiming pose that never got an animation entry — no new art needed, just rows in the `.tres`.

---

## 2. Animals (`assets/sprites/animals/`, 6 species)

Every species is a full 4-way set (SE/SW/NE/NW) cut into per-species folders. The old loose two-frame files are still in `assets/sprites/animals/*.png` and are **unreferenced** — 20 files safe to delete.

| Species | Files | Animations | In `Species.cs`? | Status |
|---|---|---|---|---|
| Wolf | 96 | idle, walk, run, attack, howl, death | Yes | **Final** |
| Caribou | 80 | idle, walk, run, death + `cow_idle`, `cow_walk` | Yes | **Final** — cow variant for herds |
| Beaver | 112 | idle, walk, gnaw, swim, slap, death | Yes | **Final** |
| Goose | 84 | idle, walk, takeoff, fly, death | Yes | **Final** — migration is now visible |
| Hare | 104 | idle, walk, run, death × winter **and** `summer_*` | Yes (`Species.Hare`, 2026-10-11) | **Final** |
| Ptarmigan | 104 | idle, walk, fly, death × winter **and** `summer_*` | Yes (`Species.Ptarmigan`, 2026-10-11) | **Final** |

**Hare and ptarmigan are the quickest win in the repo.** `HareFrames.tres` and `PtarmiganFrames.tres` are complete, including seasonal coats, but `Ecology/Species.cs` still defines only four species, so neither animal exists in the world. Adding two `Species` entries lights up 208 finished frames. The seasonal `summer_*` sets also have no selection logic — nothing reads `Season` to pick a coat.

---

## 3. Items (`assets/sprites/items/`, 51 icons at 64×64)

**38 `ItemId`s** in `Items.cs`. 35 have an icon of their own; the three food items added on 2026-10-11 (`b488764`) are **Placeholder** and borrow another item's picture:

| Item | Borrowing | Why it needs its own |
|---|---|---|
| `Berries` | `pemmican.png` | The main answer to the scurvy, gathered by every camp and post July-September. Reads as pemmican in the hotbar. |
| `SpruceBeer` | `ale_tankard.png` | The post's winter scurvy remedy, brewed with a kettle and firewood. Sharing ale's tankard is actively confusing — it is a keg and a horn cup. |
| `Corn` | `pemmican_bag.png` | The Missouri towns' harvest and the staple of the plains trade. Braided ears and shelled kernels would read better. |

Prompts for all three are in `asset-prompts.md` 5.5.1. `salt_geese.png` (the cask the goose flight goes into) was drawn in `0a7d14d` and is wired.

**15 icons are cut but have no `ItemId`** — drawn, imported, and waiting on code (`pemmican_bag` left this list when `Corn` borrowed it):

`bread_plate` · `dispatch` · `fur_pack` · `journal` · `ledger` · `letter` · `medicine_bag`

Wired 2026-10-11: `awl` · `beads` · `fishing_net` · `ice_chisel` · `mb_token` · `powder_horn` (as Powder and ball) · `stroud` · `tobacco`. The new `Ptarmigan` food item borrows the bird's own death frame as its icon (**Placeholder**).

Several back systems that already run without a physical object: `letter` and `dispatch` for the message system, `ledger` for the post accounts, `journal` for the journal panel, `mb_token` for made-beaver valuation. The individually-named trade goods — tobacco, beads, stroud, awls, powder — currently collapse into the single `TradeGoods` item.

---

## 4. Terrain, buildings and props (`assets/tiles/`)

Painted through `terrain_tileset.tres` (61 texture sources) by `TerrainPainter` / `StructurePainter`.

### Landscape — **Placeholder**

`dirt` `grass` `hill` `ice` `road` `sand` `snow` `stone_floor` `water` `water_ice` (132×99, Kenney Isometric Landscape, CC0). All ten are in the tileset. Still stock: flat colour, no edge transitions, no marsh or muskeg ground, no rock. `road.png` is new since the last pass.

### Buildings — **Placeholder**

30 wall/door/window/fence/floor/roof tiles, all Kenney (CC0). Generic European stone and plaster where York Factory wants squared log and whitewashed plank; the palisade is still fenced rather than stockaded. The 8 directional `roof_*` / `roof_corner_*` tiles remain **unwired** — only flat `roof.png` is in the tileset.

### Furniture — mixed

`bed` `candle_stand` `chair` `display_case` `rug` `shelf` `shelf_tall` `table` `table_chairs` are Kenney **Placeholder** and in the tileset. `stove.png` and `writing_desk.png` are new game art but **cut, unwired** — neither is in `terrain_tileset.tres`.

### Props — 37 files, 12 wired

In the tileset: `campfire` `crate` `fur_bale` `hay_bales` `hearth_left` `hearth_right` `ladder` `spruce_0/1/2` `tipi` `woodpile`.

**Cut but unwired (25):** `beaver_dam` `beaver_lodge` `boulder` `cache` `cannon` `canoe_beached` `dead_tree` `drying_rack` `fish_trap` `flagstaff` `fur_press` `grave` `hide_frame` `ice_hole` `leg_trap` `muskeg_pool` `pelt_frame` `reeds` `rocks` `sled` `snowdrift` `tamarack` `willow` `willow_winter` `woodpile_winter`.

This is the largest single block of finished-but-invisible art in the project. Many back live systems — `leg_trap` and `fish_trap` for trapping and fishing, `ice_hole` for winter fishing, `beaver_lodge`/`beaver_dam` for the beaver ecology, `grave` for deaths, `pelt_frame`/`drying_rack`/`fur_press` for the fur economy. Adding them to `terrain_tileset.tres` and giving `StructurePainter` placement rules would change how the world reads more than any new art would.

---

## 5. Vehicles

| Asset | File | Status |
|---|---|---|
| Canoe | `sprites/canoe.png` | **Final** — loaded by `PlayerController.cs:71`, `Canoe.cs` |
| Supply ship | `sprites/ship.png` | **Final** — loaded by `Ship.cs` |
| Loaded canoe | `sprites/vehicles/canoe_loaded.png` | **Cut, unwired** |
| York boat | `sprites/vehicles/york_boat.png` (400×130) | **Cut, unwired** |
| Dog sled | `sprites/vehicles/dog_sled.png` | **Cut, unwired** |

All three unwired vehicles correspond to transport modes in design doc §12 that the code does not model yet.

---

## 6. UI (`assets/ui/`)

| Asset | Status |
|---|---|
| `icon.png` (256×256) | **Final** — set as `config/icon`, replaces the Godot logo |
| `menu_background.jpg` | **Final** — used by `MainMenu.tscn` |
| `needs/` — 12 icons at 48×48 | **Partly wired.** `NeedsHud.cs:47` builds the path from the row name, and `Rows` holds only four: Health, Warmth, Food, Water. So `cold` `drunk` `fatigue` `fever` `load` `scurvy` `standing` `wounded` are cut and unused — eight icons waiting on eight HUD rows for states the sim already tracks. |
| `crests/` — hbc, nwc, cree, assiniboine | **Cut, unwired.** Nothing loads a crest. Natural homes: the journal panel, dialogue headers, the map, the standing display. |

No `.theme`, `.ttf` or `.otf` file exists in the repo, so there is nothing to catalogue here; six call sites fall back to `ThemeDB.FallbackFont` (`MapPanel`, `TravelMode`, `Wayfinding`, `NpcSpawn`, `PostSign`, `WildlifeSite`). Prompts: `asset-prompts.md` §5.

---

## 7. Still drawn in code, no art file

- **Fire / blaze** (`Blaze.cs`) — tinted, scaled `campfire.png` plus a `PointLight2D`. No spreading-fire frames, no smoke, no scorched ground.
- **Hearth glow** (`Hearth.cs`) — radial `GradientTexture2D` light.
- **Muzzle flash** (`CombatFx.cs`) — light only; no projectile, smoke, or blood.
- **Health bar / ready bar** — `DrawRect`.
- **Map panel** (`MapPanel.cs`) — draws `Data/World/ground.png` with coloured squares for posts; the four crests would fit here.
- **Day/night and weather** (`WorldAtmosphere.cs`) — `CanvasModulate` only. No snowfall, rain, fog, stars or aurora despite `SkyState` having Snow, Blizzard, Rain and Storm.
- **World labels** (`PostSign.cs`, `NpcSpawn.cs`, `WildlifeSite.cs`) — fallback-font text.

---

## 8. Audio

**First batch in, 2026-10-11.** Played by the `Audio` autoload (`Scripts/Core/Audio.cs`) on three buses (Music, Ambience, Effects) with volume sliders in Settings > Sound.

| Folder | What's there |
|---|---|
| `assets/audio/music/` | 6 Gemini tracks (MP3): main menu theme, two winter pieces, one for breakup and freeze-up, a summer piece, the men's house reel |
| `assets/audio/ambient/` | Loops: winter wind, blizzard, river, mosquitoes, fort chatter, children playing, hearth fire, canoe paddling, ship creak |
| `assets/audio/sfx/` | Footsteps (snow, snow crust, mud: 5 takes each), axe (5), tree fall, musket, distant cannon, punch (2), work bell, alarm bell, ice cracks (3), door, flint, dog sled, goose, raven, loon, wood creak |
| `assets/audio/ui/` | Page turn, quill |

All the effects are CC0 from Freesound; sources in `assets/audio/CREDITS.md`. Raw downloads (~550 MB) stay in `assets/external/soundFX/`, which is git-ignored and has a `.gdignore`. One of them (`769368`, a blizzard) is CC BY-NC and is **not** used.

Synthesised stand-ins (also 2026-10-11, listed in CREDITS.md) cover plank footsteps, rain, wolves, a body falling and the musket reload until real recordings replace them. What plays when, and what's still wanted, is in `audio-prompts.md` under **In the game now**. The biggest gaps: real wolf recordings, fight grunts, UI clicks, and tension music.

---

## 9. Scenes

`Scenes/World/` contains one scene: `YorkFactoryExterior.tscn`, and every start path begins in it (the rest of the country streams in round it). The old map in `GameManager.cs` to `FortWilliamExterior.tscn` and `FortGarryExterior.tscn` was dead code (nothing called it, so no start could crash) and was removed 2026-10-11.

---

## 10. External packs (`assets/external/`)

5,343 files, all committed. The Gemini pass means much less of this is load-bearing than before.

| Pack | Files | Licence | Used? |
|---|---|---|---|
| `kenney_isometric-landscape` | 130 | CC0 | Yes — terrain tiles |
| `kenney_isometric-buildings` | 132 | CC0 | Yes — wall/door/fence tiles |
| `kenney_isometric-miniature-library` | 291 | CC0 | Partly — furniture |
| `kenney_isometric-miniature-farm` | 459 | CC0 | Partly — crate, hay bales |
| `kenney_isometricDungeon` (+ .zip) | 747 | CC0 | No |
| `kenney_isometricPrototypeTiles` (+ .zip) | 651 | CC0 | No |
| `critters/` (wolf, stag, badger, boar) | 110 | **no licence file** | No longer — superseded by the new animal sheets |
| `isometric tileset/separated images` | 116 | **no licence file** | No |
| `Assets/` (IsometricTRPG, 5 sheets) | 5 | **no licence file** | No |
| `pioneerpixel_hbc_trader` | 11 JPG | own work | Yes — style reference for every Gemini prompt; keep |

**Housekeeping.** `critters/` is now fully superseded and can go. With the dungeon, prototype-tile, `isometric tileset` and `Assets/` packs that's roughly 1,600 files and two `.zip`s that nothing uses, all scanned by Godot on every import. Moving them out of `assets/` or adding a `.gdignore` would speed up imports. The three packs with no licence file still need their terms recorded before any release.

---

## Wiring backlog — finished art the game does not load

**No new art is needed for anything on this list.** Every file below is already drawn, imported and sitting in the repo; each item is a code change. This is where the cheapest visible improvement in the project currently sits — roughly 150 finished files are invisible at runtime.

| # | Job | Files it brings in | Where |
|---|---|---|---|
| 1 | Add the 25 unwired props as tileset sources with placement rules — traps, ice holes, beaver lodges, graves, pelt frames, flagstaff, cannon | 25 | `terrain_tileset.tres`, `StructurePainter.cs` |
| 2 | Add `Hare` and `Ptarmigan` entries pointing at the existing `.tres` files. (All six animal folders were sitting un-imported until 2026-10-11 — every png had no `.import` and the frame resources failed to parse, throwing 744 errors a run; `--headless --path . --import` fixed that, so the art is genuinely ready now.) | 208 frames | `Ecology/Species.cs` |
| 3 | Extend `Rows` to the eight unused need icons (cold, fever, fatigue, drunk, scurvy, wounded, load, standing) — all already simulated | 8 | `UI/NeedsHud.cs:20` |
| 4 | Season-switch the `summer_*` animations on hare and ptarmigan | — | `Agents/Animal.cs` |
| 5 | Spawn voyageurs with `VoyageurFrames` instead of `RivalFrames` | — | `Core/Starts.cs:159` |
| 6 | Load the four faction crests somewhere — journal, dialogue header, map, standing display | 4 | `UI/` |
| 7 | Add `stove.png` and `writing_desk.png` to the tileset | 2 | `terrain_tileset.tres` |
| 8 | Use `york_boat`, `dog_sled`, `canoe_loaded` once those transport modes exist | 3 | `World/` |
| 9 | Give the orphan frames `.tres` rows (`musket_ready` ×2, `player/idle_back`, `sneak_back_3`, `assiniboine/shoot_*`) | 7 | `Scenes/*/`*`Frames.tres` |
| 10 | Wire the 15 item icons that have no `ItemId` | 15 | `Items/Items.cs` |
| 12 | Draw the three borrowed food icons (berries, spruce beer, corn) — the only `ItemId`s without art of their own | 3 | `asset-prompts.md` 5.5.1 |
| 13 | Berry bushes as a gatherable prop, and Mandan earth lodges for the three Missouri towns, which are farming towns in code and drawn as plains tipis | — | `asset-prompts.md` 5.5.2-3 |
| 11 | Add the 8 directional `roof_*` tiles, or delete them | 8 | `terrain_tileset.tres` |

## Repo hygiene

- Delete the 20 superseded loose PNGs in `assets/sprites/animals/*.png` — replaced by the per-species folders, referenced by nothing.
- Delete `assets/external/critters/` (110 files) — fully superseded by the new animal sheets.
- Consider a `.gdignore` on, or removal of, the unused external packs (`kenney_isometricDungeon`, `kenney_isometricPrototypeTiles`, `isometric tileset`, `Assets/`) and the two `.zip`s: ~1,600 files Godot scans on every import for no benefit.
- Record licences for `critters/`, `isometric tileset/` and `Assets/` before any release, or remove them.
