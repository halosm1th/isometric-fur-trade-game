# Asset Inventory

Snapshot as of 2026-09-20. What art the game actually loads, where it came from, what is a stand-in, and what is still missing. Audio, fonts and UI art are covered at the bottom — all three are currently empty.

Status key:

- **Final-ish** — game-specific art, fits the style, no known reason to redo it.
- **Placeholder** — works, but is a stand-in (wrong style, stock pack, reused frames, tinted copy of another sprite).
- **Missing** — the code or design doc calls for it and nothing exists; either drawn in code (rectangles, lights) or not shown at all.
- **Unused** — sits in `assets/` but nothing references it.

---

## 1. Characters (`assets/sprites/`)

Character sheets are 150×130 frames, cut from the `pioneerpixel_hbc_trader` source JPGs. All human NPC roles reuse one of five sheets with a colour tint (`Population.cs`).

| Sheet | Frames on disk | Animations in `.tres` | Used by | Status | Notes |
|---|---|---|---|---|---|
| `player/` | idle_front, idle_back, walk_front 0–3, walk_back 0–3, eat 0–3, drink 0–3, sneak_front/back/side 0–3, death 1–3 | idle, walk, walk_back, eat, drink, death, sneak, sneak_back, sneak_side, sneak_idle | Player; HBC clerks (blue tint), HBC women (pink tint, 0.72 scale), sailors (grey-blue tint, 0.78 scale) | Final-ish for player; **Placeholder** for women/sailors | No **walk_side** frames — side movement plays the front walk. `sneak_back_3` and `idle_back` on disk but not in the `.tres`. Women and sailors are tinted men. |
| `rival/` | idle_front, walk_front 0–3, walk_back 0–3, eat 0–3 | idle, walk, walk_back, eat | Factor (Mr. Auld), NWC partner, NWC voyageurs | Final-ish for factor; **Placeholder** for NWC | No side walk, no death, no drink. Voyageurs are tinted factors — they should look like voyageurs (sash, tuque). |
| `labourer/` | idle_front, walk_front 0–3, walk_back 0–1, walk_side 0–3 | idle, walk, walk_back, walk_side, death | Orkney labourers | Final-ish | Only 2 walk_back frames vs 4 elsewhere. Death anim reuses walk frames (check `.tres`). No eat/drink. |
| `cree/` | idle_front, walk_front 0–3, walk_back 0–3, walk_side 0–1, shoot 0–1 | idle, walk, walk_back, walk_side, attack | Cree chief, Cree hunters | Final-ish | Only 2 side frames. No death, eat, drink. Attack is a bow shot — fine for hunters, wrong for knife/tomahawk. |
| `cree_woman/` | idle_front, walk_front 0–3, walk_back 0–3, walk_side 0–1 | idle, walk, walk_back, walk_side | Cree women, Cree daughters (0.74–0.78 scale, tinted) | Final-ish | Only 2 side frames. No death/eat/drink. Daughters are scaled-down women. |

**Missing character art**

- Voyageur sheet (currently tinted `rival/`).
- HBC woman / wife sheet (currently tinted, shrunk `player/`).
- Sailor sheet (currently tinted `player/`).
- Side-walk frames for `player/` and `rival/`.
- Death frames for every sheet except `player/` and `labourer/` — NPCs die in combat and brawls; a missing death anim means they drop with no visible change other than what `Agent.cs` does (check: corpses currently just stop animating).
- Drunk / staggering frames — intoxication is simulated (`carousing`, brawls) but there is no visual.
- Cold / shivering, sick (fever) states — simulated, not shown.
- Carrying / hauling frames (tumpline, pack basket, fur bales) — the logistics system tracks load; no visual.
- Paddling frames for canoe — `PlayerController` swaps in `canoe.png` beneath the standing sprite.
- Fighting frames: melee swing (knife/tomahawk), musket fire, brawl punch. Only `cree/` has a shoot anim.
- Sleeping / lying-down frame (beds are used every night).
- Children (design doc §8 kinship) — none.
- Métis, Montreal agents, soldiers (Selkirk timeline §4) — none, future.

---

## 2. Animals (`assets/sprites/animals/`)

| Species | Files | Animations | Source | Status | Notes |
|---|---|---|---|---|---|
| Wolf | wolf_idle / run / bite / death (256×256 sheets) | idle, walk, attack, death × SE/SW/NW/NE | `external/critters/wolf` | **Placeholder** | Stock pack, different render style from the hand-drawn humans. Works well mechanically (4-way, full anim set). |
| Caribou | caribou_{NE,NW,SE,SW}_{idle,walk,run} (768×41 strips) | idle/walk/run × 4 dirs | `external/critters/stag` (a stag, not a caribou) | **Placeholder** | Stock stag. No death, no attack. |
| Beaver | beaver_0, beaver_1 (48×32) | idle, walk (2 frames) | Hand-made | **Placeholder** | Two-frame bob, no direction, no death. |
| Goose | goose_0, goose_1 (48×40) | idle, walk (2 frames) | Hand-made | **Placeholder** | Two frames. No flying frames — geese migrate and get shot on the wing. |
| Hare | — | — | — | **Missing** | Exists only as an item (`hare.png`); snaring is simulated with no animal in the world. |
| Fish | — | — | — | **Missing** | Fishing is simulated; no fish/ripple visual. |
| Dogs, horses, bison, moose, bear, fox, marten, lynx, muskrat | — | — | — | **Missing** | Design doc §16 wildlife list. Not yet simulated either. |

Unused external critters: `critters/badger` (20), `critters/boar` (60) — available if you want to repurpose them.

---

## 3. Items / icons (`assets/sprites/items/`, 64×64)

All 32 icons are hand-made in a consistent style. Every `ItemId` in `Items.cs` has an icon, but several **share** one:

| Item | Icon | Status |
|---|---|---|
| Pemmican, Bread, Ale, Rum, Water, Venison, Goose, Firewood, Capote, Point blanket, Hide coat, Beaver pelt, Shot pelt, Holed hide, Caribou hide, Wolf pelt, Trade goods, Steel trap, Kettle, Flint, Fishing line, Tumpline, Pack basket, Hare, Fish, Musket, Bow, Knife, Tomahawk | own icon | Final-ish |
| Dried goose | `dried_goose.png` | Final-ish |
| **Dried meat** | reuses `dried_goose.png` | **Placeholder** — needs its own |
| **Summer (thin) pelt** | reuses `beaver_pelt.png` | **Placeholder** — should look thinner/paler |
| `bread_plate.png`, `medicine_bag.png` | — | **Unused** (no ItemId) |

**Missing item art**: nothing for items the design doc names but the code doesn't have yet — powder & shot, tobacco, beads, awls, axes, cloth, snowshoes, letters/dispatches (the message system §10 has no physical letter icon), journal (the journal panel is text-only), coin/made-beaver token.

---

## 4. Terrain, buildings, props (`assets/tiles/`)

All go through `assets/tiles/landscape/terrain_tileset.tres` and are painted by `TerrainPainter` / `StructurePainter`.

### Landscape (132×99 isometric, from Kenney Isometric Landscape, CC0)

dirt, grass, hill, ice, sand, snow, stone_floor, water, water_ice — **Placeholder**. Stock Kenney; flat-colour style. Works, but no transitions/edges, no seasonal variants beyond snow/ice swap, no river bank, no marsh/muskeg, no rock, no burnt ground.

### Buildings (Kenney Isometric Buildings, CC0)

wall_{N,S,E,W}, wall_corner_{N,S,E,W,back,front}, wall_door_{N,S,E,W}, wall_door_open_{N,S,E,W}, wall_window_{N,S,E,W}, floor_planks, roof, fence_{N,S,E,W}, fence_corner_{back,front} — **Placeholder**. Generic European stone/plaster; York Factory should be squared-log and whitewashed plank. Palisade / stockade wall does not exist (fences stand in for it).

**Unused**: roof_{N,S,E,W}, roof_corner_{N,S,E,W} — only the flat `roof.png` is in the tileset.

### Furniture

bed, candle_stand, chair, display_case, rug, shelf, shelf_tall, table, table_chairs — **Placeholder** (Kenney miniature-library style). Missing: water barrel (the household barrel is a core survival object with no visual), stove, press (fur press), scales, writing desk/ledger, chest, bunk.

### Props (128×256)

campfire, crate, fur_bale, hay_bales, ladder, spruce_0/1/2, tipi, woodpile, hearth_left/right — mixed. Spruce and tipi look like game-specific art (Final-ish); crate/hay/ladder are Kenney (Placeholder). Missing: canoe-on-shore rack, drying rack for pelts/meat, snare stake, steel trap in the world, grave/cairn, flagpole (HBC ensign), cannon, wharf posts, beaver lodge/dam, dead tree, bush/willow, rocks, cache pit.

---

## 5. Vehicles & big objects (`assets/sprites/`)

| Asset | File | Status | Notes |
|---|---|---|---|
| Canoe | `canoe.png` (110×44) | **Placeholder** | Single orientation; no paddling, no beached/loaded variants. |
| Ship (annual supply ship) | `ship.png` (460×340) | **Placeholder** | Single static image, offset by hand in `Ship.cs`. No sails-furled, no York boat. |
| York boat, dog sled, snowshoes | — | **Missing** | Design doc §12 transport. |

---

## 6. Drawn in code (no art file)

These are rendered with `DrawRect`, `PointLight2D` or `GradientTexture2D` and would benefit from real art:

- **Fire / blaze** (`Blaze.cs`) — reuses `campfire.png` scaled and tinted plus a light. Needs a spreading-fire sheet, smoke, charred tiles.
- **Hearth glow** (`Hearth.cs`) — radial gradient light only.
- **Muzzle flash** (`CombatFx.cs`) — light only. No projectile, no smoke, no blood/hit.
- **Health bar** (`HealthBar.cs`), **ready bar** (`ReadyBar.cs`) — rectangles.
- **Map panel** (`MapPanel.cs`) — coloured squares on a base texture; no proper map art, no icons for fort/post/camp.
- **Main menu background** (`MainMenu.cs`) — flat `ColorRect`.
- **Day/night** (`WorldAtmosphere.cs`) — `CanvasModulate` only; no stars, aurora, snowfall, rain, fog.

---

## 7. UI

**All missing.** Every panel (Hotbar, NeedsHud, CalendarHud, DialogueMenu, JournalPanel, MapPanel, HelpPanel, PauseMenu, NoticeLog, ChatBox, DebugMenu) uses Godot's default theme and `ThemeDB.FallbackFont`. Needed:

- A theme (`.tres`): panel backgrounds, buttons, scrollbars.
- A period-appropriate font (and a readable pixel/UI font).
- Icons for needs (hunger, thirst, cold, fatigue, intoxication, health, fever).
- Faction crests (HBC, NWC, Cree band), rank insignia.
- Cursor(s).
- Window icon — `icon.svg` is still the Godot default logo. Needs replacing before export (Windows and macOS presets both pick it up).

`external/Assets/IsometricTRPGAssetPack_UI.png` is an unused stock UI sheet that could be a temporary theme.

---

## 8. Audio

**Nothing.** No `AudioStreamPlayer` anywhere in `Scripts/` or `Scenes/`, no sound files. Needed at minimum:

- Ambient: wind, river, waves at the wharf, fire crackle, birds by season, geese overhead, wolves at night, blizzard.
- Footsteps on snow / planks / dirt / ice.
- Actions: musket, bow, knife, tomahawk, brawl hits, eating, drinking, chopping wood, trap snap, fishing line, canoe paddle.
- UI: click, notice ping, journal page, dialogue open/close.
- Events: ship's bell / cannon on arrival, church bell, fire alarm.
- Music: menu theme and a few in-world cues (fiddle at the men's house, drums at the camp).

---

## 9. Scenes referenced but absent

`GameManager.cs` maps start locations to `res://Scenes/World/FortWilliamExterior.tscn` and `res://Scenes/World/FortGarryExterior.tscn`. Neither scene exists; only `YorkFactoryExterior.tscn` does. Choosing those starts will crash. These need scenes and, for Fort William, a whole set of Great-Hall / palisade / Montreal-style art.

---

## 10. External packs on disk (`assets/external/`, 5,343 tracked files)

All committed to git; most of it is not referenced by anything.

| Pack | Files | Licence | Used? |
|---|---|---|---|
| `kenney_isometric-landscape` | 130 | CC0 | Yes — copied into `tiles/landscape` |
| `kenney_isometric-buildings` | 132 | CC0 | Yes — copied into `tiles/buildings` |
| `kenney_isometric-miniature-library` | 291 | CC0 | Partly — furniture |
| `kenney_isometric-miniature-farm` | 459 | CC0 | Partly — hay bales, crate |
| `kenney_isometricDungeon` (+ .zip) | 747 | CC0 | No |
| `kenney_isometricPrototypeTiles` (+ .zip) | 651 | CC0 | No |
| `critters/wolf`, `critters/stag` | 30 | check pack licence | Yes — copied into `sprites/animals` |
| `critters/badger`, `critters/boar` | 80 | check pack licence | No |
| `isometric tileset/separated images` | 116 | unknown | No |
| `Assets/` (IsometricTRPG pack, 5 sheets) | 5 | unknown | No |
| `pioneerpixel_hbc_trader` | 11 JPG | source sheets for character cuts | Yes — origin of all human sprites |

Housekeeping: the two `.zip` files and the unused packs (dungeon, prototype tiles, `isometric tileset`, `Assets/`) add ~1,500 files to the repo and to the Godot import scan. Consider moving them out of `assets/` or adding a `.gdignore`. The `critters`, `isometric tileset` and `Assets/` packs have no licence file in the repo — track down and record their terms before shipping.

---

## Priority list

1. `icon.svg` — replace the Godot logo (blocks a clean export).
2. Water barrel prop — core survival object, invisible today.
3. Death frames for cree, cree_woman, rival; side-walk for player and rival.
4. Voyageur and HBC-woman sheets (stop tinting).
5. UI theme + font + needs icons.
6. Any audio at all — footsteps, fire, wind, musket.
7. Log-wall / palisade tile set to replace Kenney stone.
8. Beaver and goose with real animation; hare and fish in the world.
9. Fort William and Fort Garry scenes (or remove the start options).
10. Licences for `critters`, `isometric tileset`, `Assets/`.
