---
title: Credits and Licenses
aliases: [Credits, Licences, Attribution, Licensing]
tags: [development, legal, credits]
status: implemented
---

# Credits and Licenses

**Status:** Implemented for audio (every clip is documented in `assets/audio/CREDITS.md`) and for the Kenney packs; **partial** for the rest. Three external art packs have no licence file and must be resolved or removed before any release (see below). This page summarises the repository; it is not legal advice.

## Engine and tooling

| Component | What | Notes |
|---|---|---|
| **Godot Engine** | 4.7 (`config/features` 4.7, C#, Forward Plus); project SDK `Godot.NET.Sdk/4.7.2` | Open source (MIT) |
| **.NET** | `net8.0`, `EnableDynamicLoading` | Root namespace `IsometricFurTradeGame` |
| **Export presets** | Windows Desktop and macOS (`export_presets.cfg`) | |
| **Python tools** | `tools/sprites`, `tools/world`, `tools/audio` | Use numpy, scipy and Pillow (PIL). See [[Architecture]] |

## Art

| Source | Used for | Licence |
|---|---|---|
| **Kenney** (Kenney Vleugels, www.kenney.nl): Isometric Landscape, Isometric Buildings, Isometric Miniature Library, Isometric Miniature Farm | Terrain tiles (dirt, grass, hill, ice, sand, snow, stone floor, water, water ice; the regional ground tiles are recolours of these), furniture (bed, candle stand, chair, display case, rug, shelves, table), crate, hay bales, floor planks, roof pieces | **CC0** (Creative Commons Zero). `LICENSE.txt` ships in `assets/tiles/landscape/` and `assets/tiles/buildings/`; credit "would be nice but is not mandatory" |
| Kenney Isometric Dungeon, Isometric Prototype Tiles (+ two `.zip` archives) | Not used | CC0 (License.txt present) |
| **Google Gemini image generation** | All current character sheets (14 plus 7 nation sheets added later), animals (6 species), 51 item icons, 37 props, vehicles (canoe, ship, York boat, loaded canoe, dog sled), UI icons, crests, menu background, York Factory walls and palisade | Generated for this game; prompts in `docs/asset-prompts.md`. Cut into frames by `tools/sprites/` |
| `assets/external/pioneerpixel_hbc_trader` (11 JPG) | Style reference attached to every Gemini prompt | "Own work"; keep; not shipped |
| `assets/external/critters/`, `isometric tileset/`, `Assets/` (IsometricTRPG) | Superseded or unused | **No licence file**: record terms before any release, or delete |
| Map data: the "Pemmican Empire" atlas (`tools/world/atlas_data.json`) | Coasts, lakes, rivers, ecozones, posts, routes, landmarks: the map | Natural Earth for physical geography (public domain, per the atlas data note); the atlas itself is a project-internal source |
| Historical plan: G. Ray's plan of York Factory and the post survey | Layout of York Factory | Source reference only |

Per the asset inventory, the `external/` folder holds 5,343 files, about 1,600 of which nothing uses.

## Audio

All music was generated with **Google Gemini** on 2026-10-11 (see [[Music]]). Every Freesound clip used is **CC0**, so credit is not required; it is given anyway:

| Clip(s) | Source | Author |
|---|---|---|
| step_snow_01..05 | Freesound 341923, Snow footsteps | seth-m |
| step_snow_crust_01..05 | 614137, Snow footsteps crunch 1 (mono) | JRWEntertainment |
| step_mud_01..05 | 543775, Walking through thick wet mud | Walthamstow_Walker |
| axe_chop_01..05 | 847818, Chopping wood medium | elschorscho |
| tree_fall | 670300, Tree cut down | brunoauzet |
| musket_fire | 244345, Musket explosion | willlewis |
| cannon_distant | 565794, Distant cannon fire (simulated) | stomachache |
| punch_01..02 | 276600, Body hit | insanity54 |
| fort_bell_work, fort_bell_alarm | 724854, Cast iron bell ringing | tsp-talk |
| ice_crack_01..03 | 737896, Ice lake fractures | vrymaa |
| wood_creak | 506665, Wood creak single | Rudmer_Rotteveel |
| dogsled_pass | 348247, Dogsled passes by | newagesoup |
| goose_honk | 805297, Goose honk | jahjahjahjah |
| door_open | 683434, Door open | saha213131 |
| flint_strike | 538024, Flint and steel | tairblenn |
| raven_croak | 275466, Raven call | adharca |
| loon_call | 39339, Loon snippets | stackpool |
| amb_winter_wind, amb_blizzard | 505999, Howling winter storm | dblover |
| amb_river | 869104, Mountain stream flowing past | bassimat |
| amb_mosquitoes | 462440, Mosquito 01 | hectorsoto19 |
| canoe_paddling_loop | 503208, Paddling a kayak | danjocross |
| fire_hearth_loop | 410404, Fire crackling | alexanderche |
| ship_creak_loop | 195193, Large wooden ship | pimfeijen |
| amb_kids_playing | 469613, Kids playing | brunoboselli |
| amb_fort_chatter | 850562, Walla teatro | mariayolanda511 |
| ui_page_turn | 737005, Page turn 2 | redagee |
| ui_quill_write | 856167, Ink quill writing on parchment | brktkrgll |

**Synthesised stand-ins** (original to the project, no licence attached, generated with numpy/scipy by `tools/audio/synth.py`): plank steps, rain, wolf howls, growls and bite, body fall, musket reload. They are placeholders.

> [!warning] Do not ship
> `assets/external/soundFX/769368...snowstorm...howling.mp3` is **CC BY-NC 4.0** (no commercial use) and `614136...snow-footsteps-crunch-1-stereo.wav` is CC BY 4.0 (needs credit). Neither is used in the game; the CC0 versions are. The raw downloads (about 550 MB) are git-ignored and hidden from Godot with a `.gdignore`.

## Music and Indigenous cultural material

Songs of the Cree, Assiniboine, Ojibwe and other nations belong to those communities. The audio plan never imitates ceremonial music; drum cues are placeholders until real recordings can be commissioned. See [[Music]].

## Historical content

Names, dates and attributions in the game come from the design doc and general history, "not checked against primary sources", and are flagged for verification against the Dictionary of Canadian Biography (biographi.ca). See `docs/world-content-needed.md` and the open questions in the design doc. The Indigenous nations are portrayed in a game; the design notes ask for depth and consultation, not stereotype.

## Contributors

Per the git history: the game owner (GitHub `halosm1th`) with Claude Code sessions (commits co-authored by "Claude Opus 5" and "Claude Opus 5.5"). The repository has no `LICENSE` or `README` file at its root.

## See also

[[Audio]] | [[Asset Inventory]] | [[Architecture]] | [[Changelog]] | [[Development Status]]
