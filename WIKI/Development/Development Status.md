---
title: Development Status
aliases: [Status, Feature Status, What's Built, Project Status]
tags: [development, status, index]
status: partial
---

# Development Status

**Status:** Partial. The game is a playable, networked, first-pass simulation of the fur trade from York Factory out to the whole western country, with placeholder art in places and a long list of design-doc systems still to build. Everything on this page is grounded in the code at commit `bb59c3a` (12 October 2026), the design doc (`docs/fur-trade-game-design.md`, 21 sections) and the asset inventory. The wiki's rule is: a thing is **Implemented** only if the code runs it.

The repo contains about 26,700 lines of C#, 87 commits (10 to 12 October 2026, preceded by the September overnight build), one world scene and a 1002 x 2361 cell map.

## System by system

Legend: Implemented / Partial / Planned. "§" = design doc section.

### World and time

| System | Status | Notes |
|---|---|---|
| Map of the whole western fur-trade country (Hudson Bay to the Pacific, Missouri to Athabasca), 40 px per km | Implemented | Imported from the Pemmican Empire atlas; fogged until explored (§6). See [[World Map]], [[Regions]] |
| Posts across the map, by founding year; major posts hand-laid | Implemented / Partial | York Factory, Fort William, Moose Factory, Edmonton House, Cumberland House, Fort Chipewyan, Fort Gibraltar laid out; the rest are generated plans. See [[All Trading Posts]] |
| Regional terrain and flora | Implemented (placeholder tiles) | Prairie, tallgrass, parkland, tundra, Shield, muskeg, mountain. See [[Terrain and Biomes]] |
| Calendar, seasons, day/night | Implemented | 30 s a game hour; dated events. See [[Calendar and Seasons]] |
| Weather | Implemented | Clear, overcast, snow, blizzard, rain, storm; rolled in regions about 200 km across from each region's climate and asked for by position (`Weather.At`), so the plains are drier and the Pacific slope wetter than York (commit `3c74eb6`); on screen it is still only a colour modulate. See [[Weather]] |
| Fire (hearths, blazes, spread) | Implemented (partial) | Hearths, blazes, arson, fire bell. §18.4 flammability partly built |
| Travel mode (G) | Implemented | §18.3. See [[Travel and Wayfinding]] |
| Portages and brigade routes | Implemented | Hayes road driven by brigades; other routes mapped only |
| Water-state transitions per waterway (Autumn Scramble, Spring Lockout) | Planned | §14.1 |

### People and society

| System | Status | Notes |
|---|---|---|
| Unified agent model: needs, memory, personality | Implemented | §11. See [[Personality]], [[Memory and Knowledge]] |
| Goal framework and delegation | Implemented | §3. See [[Delegation]] |
| Simulation tiers (live / background) | Implemented | See [[Simulation Tiers]] |
| Character creation: 4 paths + any band, backgrounds, traits, ambition | Implemented | §5.4. See [[Starting Paths]]. A Métis/freeman path (§5.3) is not yet a standalone path, but Métis camps exist |
| Standing, ranks, career | Implemented | [[Standing]] |
| Rival for your seat | Implemented | §3.7. [[Rivalry]] |
| Favours, leverage, grudges, vendettas | Implemented | [[Favours]], [[Grudges and Vendettas]] |
| Lies, gossip, denial | Implemented | §P.10. [[Gossip, Lies and Leverage]] |
| Friends and bonds; ambitions | Implemented | [[Friends and Bonds]], [[Ambitions]] |
| Heirs and succession | Implemented | §9.3. [[Heirs and Succession]] |
| Marriage and country wives | Implemented (partial) | §8. [[Marriage and Country Wives]] |
| Captivity, parole, ransom | Partial | Light captivity (§9.1) is in; slavery (§9.2) is not |
| Aging and natural decline | Planned | §9.4, `longer_goals.md` item 2 |
| Languages | Implemented | Ten languages with kinship between them. [[Languages]] |
| Councils and accords | Implemented (first cut) | Fort William council with shares and temper (§3.8.1). [[Councils and Accords]] |
| London Committee | Partial | Orders, packet, quota. [[London Committee]] |
| Information and messages (sealed news, mail, packets) | Implemented | §10. [[Information and Messages]] |
| Black-market intelligence, message forgery | Planned | §10.3 |
| The Director (selection bias) | Implemented | §2.2. [[The Director]] |
| The Reckoning of 1821 and the year as told | Implemented (first cut) | [[The Reckoning of 1821]], [[The Year Told]] |

### Economy and survival

| System | Status | Notes |
|---|---|---|
| Ledgers for posts, camps and the NWC; barter with a pipe | Implemented | [[Economy]], [[Trading and Barter]] |
| Made Beaver and fur prices | Implemented | [[Made Beaver]], [[Fur Prices]] |
| Credit and debt | Partial (band debt is on the ledgers and counted in the year's reckoning; full §13.3 not checked) | [[Credit and Debt]] |
| Needs: hunger, thirst, warmth, fatigue, sickness, scurvy | Implemented | [[Needs and Survival]] |
| Fever, scurvy, wounds | Implemented | [[Health, Wounds and Illness]]. Smallpox, measles (§13.5) planned |
| Gardens, berries, spruce beer | Implemented | [[Gathering and Gardens]] |
| Fishing (open water, through the ice) | Implemented (as a background task) | [[Fishing]]. No fish animal yet |
| Crafting and smithing; recipes; facility-gated | Planned | §12.6 |
| Perks | Planned | §12.7 |
| Company solvency, merger inevitability | Partial | §13.7, §4.4 |
| Founding a new post | Planned | §13.8 |
| Alcohol and addiction | Implemented (intoxication) / Planned (addiction) | §13.4 |

### Wildlife and hunting

| System | Status | Notes |
|---|---|---|
| Spawn-site ecology | Implemented | 637 sites. [[Ecology]], [[Species Sites]] |
| Species: caribou, goose, beaver, wolf, hare, ptarmigan | Implemented | [[Wildlife]] |
| Buffalo | Partial | 18 herds and a full yield; plains camps ride out and run the herd together on the captain's word (`Ecology/BuffaloRun`, commit `d3215ae`); no unique sprites; hunt-captain discipline (§16.1) not yet built |
| Goose hunt | Implemented | [[Goose]] |
| Trapping and poaching | Implemented | [[Trapping]] |
| Hare/lynx cycle; lynx, marten, moose, bear, fox | Planned | `docs/world-content-needed.md` section 6 |
| Horses | Partial | §12.3. Every camp and parkland post owns a herd that grazes, thins in winter, foals in May and is raided; the player can buy, borrow, give, steal and ride one at the ground's pace (`Economy/Plains.cs`, `Economy/HorseTrade.cs`, `Economy/MissouriTrade.cs`). Stand-in pony sprite until the horse sheet is drawn |

### Transport

| System | Status |
|---|---|
| Canoe (player) | Implemented: [[Canoe]] |
| Supply ship | Implemented: [[Ship]] |
| Brigade | Implemented: [[Brigade]] |
| York boat | Planned: [[York Boat]] |
| Dog sled, toboggan, cart | Planned: [[Dog Sled]] |
| Canoe condition for the player | Planned: §12.2 |

### Combat

| System | Status |
|---|---|
| Weapons (musket, bow, knife, tomahawk), melee and ranged, sneak attack | Implemented: [[Combat]] |
| Group morale, non-lethal outcomes, fights won before the first shot | Not verified against code (§15.4 to 15.6); treat as Planned |
| Horse raids between nations (live where a player is near, numbers elsewhere), feuds | Partial: `Economy/Plains.cs`, `Agents/Goals/PlainsGoals.cs` |
| War parties, mounted battle, morale, truce | Planned: see `docs/specs/story-plains-years.md` Act 6 |

### Interface and platform

| System | Status |
|---|---|
| Main menu, new game page, "Who you are" | Implemented |
| Journal, log, pack, map, hotbar, needs HUD | Implemented. [[Journal, Map and UI]] |
| Settings (sound, keybinds) | Implemented. [[Settings and Options]] |
| Multiplayer (host, join, dedicated, up to 8) | Implemented. [[Multiplayer]] |
| Save and load | Implemented (quicksave and autosave). [[Save and Load]] |
| Campaign scripts | Implemented. [[Campaigns and Adherence]] |
| Audio | Implemented (first batch). [[Audio]] |
| UI theme and font | Planned |
| Weather effects on screen | Planned (`longer_goals.md` item 7) |

### Art

See [[Asset Inventory]]: 21 character sheets, 6 animal sets, 51 item icons, 37+ props, 3 unwired vehicles, 8 unused need icons, 4 unused crests.

## What a player can do today

- Begin as an HBC hand at York Factory, a Nor'Wester, a Cree or Assiniboine person, or a member of any band in the country ([[Starting Paths]]).
- Work the post: hunt, trap, fish, gather, haul, trade, write, serve at the table.
- Rise: declare an ambition, rival another man for the master's seat, earn standing with the ship's report.
- Travel across the country by foot or canoe, with travel mode.
- Play with up to seven others over a shared world, with agreed time skips.
- Let the year play out: the goose flight, the brigade, the ship, the winter, the spring.

## What is missing

Crafting, horses, mounted warfare, slavery, aging, the Métis/freeman path as a full third road, hunt-captain discipline, expanded American parts, farming, and the eight-year epilogue. See [[Roadmap]].

## See also

[[Roadmap]] | [[Changelog]] | [[Asset Inventory]] | [[Architecture]] | [[Credits and Licenses]] | [[Game Overview]]
