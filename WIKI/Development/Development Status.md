---
title: Development Status
aliases: [Status, Feature Status, What's Built, Project Status]
tags: [development, status, index]
status: partial
---

# Development Status

**Status:** Partial. The game is a playable, networked, first-pass simulation of the fur trade from York Factory out to the whole western country, with placeholder art in places and a long list of design-doc systems still to build. Everything on this page is grounded in the code at commit `acd6e17` (20 October 2026, re-checked 21 October; see `docs/audit.md`). The marriage, solvency and "what is missing" entries were re-checked at `cf835bd` on 30 October; the rest of the page was not, the design doc (`docs/fur-trade-game-design.md`, 21 sections) and the asset inventory. The wiki's rule is: a thing is **Implemented** only if the code runs it.

The repo contains about 36,100 lines of C# in 130 scripts, 253 commits, one world scene and a 1002 x 2361 cell map.

## System by system

Legend: Implemented / Partial / Planned. "§" = design doc section.

### World and time

| System | Status | Notes |
|---|---|---|
| Map of the whole western fur-trade country (Hudson Bay to the Pacific, Missouri to Athabasca), 40 px per km | Implemented | Imported from the Pemmican Empire atlas; fogged until explored (§6). See [[World Map]], [[Regions]] |
| Posts across the map, by founding year; major posts hand-laid | Implemented / Partial | York Factory, Fort William, Moose Factory, Edmonton House, Cumberland House, Fort Chipewyan, Fort Gibraltar laid out; the rest are generated plans. See [[All Trading Posts]] |
| Regional terrain and flora | Implemented (placeholder tiles) | Prairie, tallgrass, parkland, tundra, Shield, muskeg, mountain. See [[Terrain and Biomes]] |
| Calendar, seasons, day/night | Implemented | 30 s a game hour with uneven pacing (quick winter nights, slow ship days); dated events. Day length is York's (57 N) everywhere. See [[Calendar and Seasons]] |
| Weather | Implemented | Clear, overcast, snow, blizzard, rain, storm; rolled in regions about 200 km across from each region's climate and asked for by position (`Weather.At`), so the plains are drier and the Pacific slope wetter than York (commit `3c74eb6`); drawn on screen as snow, rain, whiteout, river fog and the aurora (`UI/WeatherOverlay.cs`). See [[Weather]] |
| Fire (hearths, blazes, spread) | Implemented (partial) | Hearths, blazes, arson, fire bell. §18.4 flammability partly built |
| Travel mode (G) | Implemented | §18.3. See [[Travel and Wayfinding]] |
| Portages and brigade routes | Implemented | Hayes road driven by brigades; other routes mapped only |
| Water-state transitions per waterway (Autumn Scramble, Spring Lockout) | Planned | §14.1 |

### People and society

| System | Status | Notes |
|---|---|---|
| Unified agent model: needs, memory, personality | Implemented | §11. See [[Personality]], [[Memory and Knowledge]] |
| Goal framework and delegation | Implemented | §3. See [[Delegation]] |
| Simulation tiers (live / background) | Implemented (two of the design's three tiers) | See [[Simulation Tiers]] |
| Character creation: 4 paths + any band, backgrounds, traits, ambition | Implemented | §5.4. See [[Starting Paths]]. A Métis/freeman path (§5.3) is not yet a standalone path, but Métis camps exist |
| Standing, ranks, career | Implemented | [[Standing]] |
| Rival for your seat | Implemented | §3.7. [[Rivalry]] |
| Favours, leverage, grudges, vendettas | Implemented | [[Favours]], [[Grudges and Vendettas]] |
| Lies, gossip, denial | Implemented | §P.10. [[Gossip, Lies and Leverage]] |
| Friends and bonds; ambitions | Implemented | [[Friends and Bonds]], [[Ambitions]] |
| Heirs and succession | Implemented | §9.3. [[Heirs and Succession]] |
| Marriage and country wives | Implemented | §8. By the player's side (`Agents/Marriage.cs`): the Company way (one wife, into his post), the Nor'West way (she stays with her people; he is kin), born to a band (she comes to his lodge). More than one wife off the Company's books; going over to the Company means choosing, and wives left behind remember. Children are born and grow up. [[Marriage and Country Wives]] |
| Captivity, parole, ransom | Partial | Light captivity (§9.1) is a page of choices (`Core/Captivity.cs`): questioned (truth, lie, silence), then wait, be bought back, give parole or escape over the pickets. Slavery (§9.2) is not built |
| Aging and natural decline | Partial | §9.4. A year older each New Year; NPCs past 45 slow; children grow up (`World/Population.cs`). No decline for the player, no death of age |
| Languages | Implemented | Ten languages with kinship between them. [[Languages]] |
| Councils and accords | Implemented (first cut) | Fort William council with shares and temper (§3.8.1). [[Councils and Accords]] |
| London Committee | Partial | Orders, packet, quota. [[London Committee]] |
| Information and messages (sealed news, mail, packets) | Implemented | §10. [[Information and Messages]] |
| Black-market intelligence, message forgery | Partial | §10.3. Carried packets can be sold to the other company (`UI/Conversation.cs`, `SellPacket`); no forgery, express couriers or blowback |
| The Director (selection bias) | Implemented | §2.2. [[The Director]] |
| The Reckoning of 1821 and the year as told | Implemented (first cut) | [[The Reckoning of 1821]], [[The Year Told]] |

### Economy and survival

| System | Status | Notes |
|---|---|---|
| Ledgers for posts, camps and the NWC; barter with a pipe | Implemented | [[Economy]], [[Trading and Barter]] |
| Made Beaver and fur prices | Implemented | [[Made Beaver]], [[Fur Prices]] |
| Credit and debt | Partial (band debt is on the ledgers and counted in the year's reckoning; full §13.3 not checked) | [[Credit and Debt]] |
| Needs: hunger, thirst, warmth, fatigue, sickness, scurvy | Implemented | [[Needs and Survival]] |
| Fever, scurvy, wounds | Implemented | [[Health, Wounds and Illness]]. Fever spreads between people standing together. Wounds are a health pool, not hit locations (§15.3); no treatment choice (§17.5). Smallpox, measles (§13.5) planned |
| Gardens, berries, spruce beer | Implemented | [[Gathering and Gardens]] |
| Fishing (open water, through the ice) | Implemented (as a background task) | [[Fishing]]. No fish animal yet |
| Crafting and smithing; recipes; facility-gated | Planned | §12.6 |
| Perks | Planned | §12.7 |
| Company solvency, merger inevitability | Partial | §13.7, §4.4. A purse per company (`Economy/Companies.cs`): fur sales against goods, freight and wages; it orders the outfit and limits hiring. Two years deep in debt exhausts a company, and the union (`Economy/Union.cs`) follows in any year, as well as on the campaign's `merger` flag. No credit or crisis moves; not yet seen to happen in a long run |
| Founding a new post | Planned | §13.8 |
| Alcohol and addiction | Implemented (intoxication) / Planned (addiction, dilution, the Fire Test) | §13.4 |

### Wildlife and hunting

| System | Status | Notes |
|---|---|---|
| Spawn-site ecology | Implemented | 637 sites. [[Ecology]], [[Species Sites]] |
| Species: caribou, goose, beaver, wolf, hare, ptarmigan | Implemented | [[Wildlife]] |
| Buffalo | Partial | 18 herds and a full yield; plains camps ride out and run the herd together on the captain's word, and the soldiers deal with a man who goes early (`Ecology/BuffaloRun`, §16.1); no unique sprites |
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
| Group morale, fights won before the first shot | Implemented: `Agents/Nerve.cs` (who stands with you, who has fallen, surprise), range-scaled misses, sneak attack; war-party morale and retreat in `Economy/War.cs` |
| Non-lethal outcomes | Partial: a killing blow from the other company takes you prisoner instead (§9.1); no surrender or bluff verb |
| Hit location and lethality (§15.3) | Planned: damage is a health pool; muskets do not use powder |
| Horse raids between nations (live where a player is near, numbers elsewhere), feuds | Partial: `Economy/Plains.cs`, `Agents/Goals/PlainsGoals.cs` |
| War parties, morale, truce | Implemented: `Economy/War.cs`, `Agents/Goals/WarGoals.cs` (commit `797a6df`). Truces are timed, not Accords; no battle larger than one party |

### Interface and platform

| System | Status |
|---|---|
| Main menu, new game page, "Who you are" | Implemented |
| Journal, log, pack, map, hotbar, needs HUD | Implemented. [[Journal, Map and UI]] |
| Settings (sound, keybinds) | Implemented. [[Settings and Options]] |
| Multiplayer (host, join, dedicated, up to 8) | Implemented. [[Multiplayer]] |
| Save and load | Implemented (quicksave and autosave). [[Save and Load]] |
| Campaign scripts | Implemented. [[Campaigns and Adherence]] |
| Quests (quest lines in the campaign language) | Implemented (`Core/Quests.cs`, `Core/QuestScript.cs`). Not described in the design doc; see `docs/wiki/Quests.md` |
| Disconnect hand-off to the goal planner (§21.4) | Planned: a disconnected player's character is despawned and restored on rejoining |
| Audio | Implemented (first batch). [[Audio]] |
| UI theme | Implemented: boards, iron, leather and parchment nine-patches (`UI/MenuTheme.cs`); context cursors (`UI/Cursors.cs`) |
| Weather effects on screen | Implemented (`UI/WeatherOverlay.cs`) |

### Art

See [[Asset Inventory]]: 21 character sheets, 6 animal sets, 51 item icons, 37+ props, 3 unwired vehicles, 8 unused need icons, 4 unused crests.

## What a player can do today

- Begin as an HBC hand at York Factory, a Nor'Wester, a Cree or Assiniboine person, or a member of any band in the country ([[Starting Paths]]).
- Work the post: hunt, trap, fish, gather, haul, trade, write, serve at the table.
- Rise: declare an ambition, rival another man for the master's seat, earn standing with the ship's report.
- Travel across the country by foot, canoe or horse, with travel mode; take a place in a brigade's boats.
- On the plains: buy or borrow a horse, run the buffalo with a camp, ride with a war party.
- Follow a campaign's quest lines (the Company's servant, the Nor'Wester's story, the American road).
- Play with up to seven others over a shared world, with agreed time skips.
- Let the year play out: the goose flight, the brigade, the ship, the winter, the spring.

## What is missing

Crafting, slavery, decline with age, the Métis/freeman path as a full third road, hit-location combat, Accords, winter strain, wound treatment, the fur market's holding back and flooding, Astor and St. Louis as background nodes, farming by the player, and the eight-year epilogue. See [[Roadmap]] and `docs/audit.md`.

## See also

[[Roadmap]] | [[Changelog]] | [[Asset Inventory]] | [[Architecture]] | [[Credits and Licenses]] | [[Game Overview]]
