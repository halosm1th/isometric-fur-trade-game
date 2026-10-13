---
title: Architecture
aliases: [Code Architecture, Project Structure, Tech Stack, Folder Map]
tags: [development, code, architecture]
status: implemented
---

# Architecture

**Status:** Implemented. A map of how the game is built: engine, autoloads, scenes, folders, the simulation-tier scheme, networking, data files and the tooling pipelines. Based on the repository at commit `bb59c3a` (12 October 2026).

## Stack

| | |
|---|---|
| Engine | Godot 4.7 (`config/features`: 4.7, C#, Forward Plus); `Godot.NET.Sdk/4.7.2`, target `net8.0` |
| Language | C# (about 26,000 lines in `Scripts/`); Python for asset and map tooling |
| Namespace | `IsometricFurTradeGame.Scripts.*` |
| Display | 1280x720 viewport, stretch mode `canvas_items`, aspect `expand` |
| View | 2D isometric (staggered grid: columns 128 px apart, rows 32 px) |
| Main scene | `Scenes/UI/MainMenu.tscn` |
| Exports | Windows Desktop and macOS presets |
| Saves | `user://saves/` (quicksave.json etc., JSON, `version: 1`); settings in `user://settings.cfg` |

## Autoloads

Configured in `project.godot`:

| Autoload | File | Role |
|---|---|---|
| `GameManager` | `Scripts/Core/GameManager.cs` | Names the one world scene (`Scenes/World/YorkFactoryExterior.tscn`). Every start path begins there. |
| `GameCalendar` | `Scripts/Core/GameCalendar.cs` | The clock, the date (start 1 Jan 1811, 09:00), seasons, weather, annual events (Breakup 5 Jun, Summer bison hunt 15 Jun, Brigade 12 Jul, ShipArrives 20 Aug, ShipDeparts 15 Sep, Fall bison hunt 15 Oct, FreezeUp 10 Nov), fast-forward and skip. Default 30 real seconds per game hour. |
| `Net` | `Scripts/Net/Net.cs` | Networking: offline / host / client / dedicated |
| `Audio` | `Scripts/Core/Audio.cs` | See [[Audio]] |

## Scenes

| Scene | Contents |
|---|---|
| `Scenes/UI/MainMenu.tscn` | The menu over the York Factory painting; new-game and "Who you are" pages |
| `Scenes/World/YorkFactoryExterior.tscn` | **The only world scene.** It holds the YSort layer, the terrain and structure painters, the `WildlifeSite` markers (`Wildlife/`, `Wildlife/Country/`), camp markers (`Cast/Camps`) and the HUD. Terrain beyond the start is generated in chunks and never stored in the scene. |
| `Scenes/Player/Player.tscn` + `PlayerFrames.tres` | The player agent |
| `Scenes/Npc/Npc.tscn` + 21 `*Frames.tres` | NPCs, one frames resource per sheet |
| `Scenes/Animals/Animal.tscn` + 6 `*Frames.tres` | Animals |
| `Scenes/Items/WorldItem.tscn` | An item lying in the world |

The old `FortWilliamExterior`/`FortGarryExterior` scenes never existed; the map to them was dead code removed on 11 Oct. Posts elsewhere are places inside the one world.

## Folder map (Scripts/)

| Folder | Files | Lines | What lives there |
|---|---|---|---|
| `Agents/` | 24 | about 7,500 | `Agent` (the unified person/animal model), `Npc`, `Animal`, needs, memory, personality, goals (`Goals/`: basic, work, house and post goals), `Delegation` (the Factor's tasks), ambition, favours, grudges, leverage, lies, languages, heirs, `Chronicle` |
| `Core/` | 18 | about 4,600 | `Sim` (static registry), `Tiers`, `GameCalendar`, `Weather`, `Audio`, `Settings`, `SaveGame`, `Campaign`, `Director`, `Reckoning`, `YearStory`, `Starts`, `CharacterSheet`, `Message`, `Reveal` |
| `Ecology/` | 3 | about 650 | [[Ecology]]: `Species`, `EcologyManager`, `Trapline` |
| `Economy/` | 12 | about 1,750 | Ledgers: `Household`, `FortLedger`, `NwcLedger`, `OutpostLedger`, `TradingPostLedger`, `BandLedger`, `CountryBand`; `Barter`, `Council`, `Gardens`, `LondonCommittee`, `Rivalry` |
| `Items/` | 2 | about 360 | `Items.cs` (every `ItemId`, kind, weight, value, nutrition, damage), `WorldItem` |
| `Net/` | 1 | about 700 | RPCs and replication |
| `Player/` | 3 | about 1,450 | `PlayerController`, `PlayerTask`, `ReadyBar` |
| `UI/` | 18 | about 4,600 | HUDs and panels: map, journal, dialogue, conversation, settings, `TravelMode`, `Wayfinding`, debug menu |
| `World/` | 25 | about 5,100 | `WorldLayout`, `WorldMap`, `TerrainPainter`, `StructurePainter`, `Posts`, `Camps`, `Population`, `WorldSpawner`, `Ship`, `Canoe`, `Brigade`, `GooseHunt`, `Fire`, `Hearth`, `WildlifeSite`, `Pathfinder`, `Routes`, `WorldAtmosphere` |

## Simulation tiers

Existence is identical for everyone; only update frequency differs ([[Simulation Tiers]], design doc 18.2). In code (`Scripts/Core/Tiers.cs`):

- `Tier.Live`: within **3,200 px** of any player (promote); `Tier.Background`: beyond **4,200 px** (demote; the gap is hysteresis). Live agents have a goal stack, pathfinding, a body and needs; background agents are rows of numbers resolved **once a day** by their post or camp (they eat from the cache, do their role's work, add to the stores).
- Nothing is despawned; the node stays so references, memories, letters and saves all keep working.
- The daily batch (`Tiers.Tick`) also runs `GooseHunt.Tick`, `Reckoning.Spring`, `Gardens.Tick`, `Rivals.Tick`, `Brigades.Season` and `Brigades.Tick`.
- Animals have their own scheme: sites hold counts and promote real animals within 2,600 px of a player.
- At more than eight times ordinary motion (or during a skip), a live man's goals stop ticking; the batch is resolving his day anyway.
- "Who is near a point" is answered from a spatial grid rather than scanning every agent: that single change took a 14-hour skip from 270 to 600 ms per frame to about 3 seconds in total.
- The design target is about 100 live agents, a few hundred local and 10 to 20 thousand in the background; the current world has about 900 people (700 country people, 200 post people).

## Networking

Described in `Scripts/Net/Net.cs`: **the server runs the simulation**; clients are windows onto it.

- Modes: Offline (solo is a host with nobody connected, same code path), Host, Client, Dedicated. Default port **7777**, up to 8 players.
- The server runs every NPC brain, animal, ledger, the clock, the weather and the ship. A client owns its own player's movement and sends everything else as a request.
- Time skips are agreed: the clock jumps only when everyone alive has turned in.
- Late joiners get world state on arrival; a joiner makes a character through the same "Who you are" page and arrives in the world as it is (the sloop from Churchill, a light canoe, his band's camp).
- See [[Multiplayer]], [[Save and Load]].

## Data files

| File | Purpose |
|---|---|
| `Data/World/ground.png` (88 KB) | One pixel per map cell; colour = ground. Hand-paintable. 1002 x 2361 cells at 40 px per km |
| `Data/World/world.json` (212 KB) | 105 places (posts, with founding year and company), 16 `layout` keys (fort, camps, road, trail, crossings, portages, sites, posts, camps, routes, landmarks), legend. "Generated by `build_world.py`; edit `extras.json` or `ground.png`, not this." |
| `Data/World/post_plans.json` | Hand-editable plans for the major posts (Fort William, Moose Factory, Edmonton House, Cumberland House, Fort Chipewyan, Fort Gibraltar) |
| `Campaigns/historical.json` | The 1811-1821 campaign: title, description, posts by founding year, 13 beats |
| `Campaigns/Gemini and Clark.json` | A second campaign script, "The Plains Ablaze (1801-1821)" (the file name is as committed) |
| `tools/world/atlas_data.json`, `extras.json` | Atlas source and hand additions |
| `user://campaigns/` | Player-authored campaign JSON plus a README, written on first run |

## Tooling (tools/)

| Pipeline | Scripts | What they do |
|---|---|---|
| `tools/sprites/` | `slice.py`, `characters.py`, `animals.py`, `items.py`, `props.py`, `ui.py`, `walls.py`, `tileset.py` | Cut Gemini sheets drawn on flat magenta (or any flat colour) into frames and write the `*Frames.tres`; fit the log-wall sheet onto the Kenney tiles' exact edges; register a prop as a TileSet source |
| `tools/world/` | `build_world.py`, `make_region_tiles.py` | Build `ground.png` and `world.json` from the atlas and extras; recolour the regional ground tiles |
| `tools/audio/` | `process.py`, `synth.py`, `audiolib.py` | Cut Freesound downloads into game clips; synthesise stand-ins. `python -I tools/audio/<script>.py .`, then `--headless --path . --import` |

## Dev arguments

Run the game with `-- --flag` (after Godot's `--`). Examples seen in code: `--solo`, `--host`, `--server`, `--join=ip[:port]`, `--name=`, `--date=`, `--hour=`, `--speed=`, `--skip=HOURS`, `--start=Path[:bg:traits:ambition:camp]`, `--campaign=`, `--adherence=`, `--spawn=` (a post, camp, landmark, hares, ptarmigan, or a bare cell), `--travelto=Place`, `--ship`, `--brigade`, `--wolves`, `--buffalo`, `--scarcity`, `--herdmoved`, `--tension=`, `--standing=`, `--liar=`, `--misdeed=`, `--yearstory=`, `--revealmap`, `--noposts`, `--nocamps`, `--nodirector`, `--perf`, `--jumps`, `--audiotrace`, `--census=`, `--menu=newgame|character`. The F1 test console (host only) has tabs, god mode and spawn buttons.

## Design principles in the code

"One generic system per mechanic, specialised by parameters": a single ledger family for posts and camps, a single goal framework for NPCs and the player, one agent model for animals and people, one site model for lodges and herds ([[The Director]] biases but never decides). See the design doc (sections 2, 11, 19).

## See also

[[Development Status]] | [[Roadmap]] | [[Changelog]] | [[Simulation Tiers]] | [[Multiplayer]] | [[Save and Load]] | [[Credits and Licenses]]
