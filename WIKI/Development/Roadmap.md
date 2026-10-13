---
title: Roadmap
aliases: [Planned Features, To Do, Longer Goals, Future Plans]
tags: [development, planned, roadmap]
status: planned
---

# Roadmap

**Status:** Planned. There is no dated release schedule: this page gathers what the project's own documents say is still to be made. Sources: `docs/longer_goals.md`, `docs/world-content-needed.md` (priorities P1 for the York Factory year being played, P2 once the player leaves York, P3 the wider map and later years), the open questions in `docs/fur-trade-game-design.md` section 20, and the wiring backlog in `docs/asset-inventory.md`.

## The owner's longer goals (`docs/longer_goals.md`)

1. **Draw out the timeline from about 1766 to 1821.** The game currently runs 1811 to 1821 (design doc 4.1; an 1801 prologue is an open question). See [[Setting and Timeline]].
   - 1.1 If Lewis and Clark are killed, America should "go haywire and launch a whole army". Partly prototyped as an alternate campaign script, "The Plains Ablaze (1801-1821)" (`Campaigns/Gemini and Clark.json`, features the Lewis and Clark expedition and possible U.S. military escalation); the in-world escalation itself is not built.
2. **Characters age over time.** See [[Heirs and Succession]] for what exists.
3. **Character creator** with different faces, stats and clothing-driven looks, "using this system to generate people". The start-path sheet exists ([[Starting Paths]]); visual customisation does not.
4. **Horseback riding and plains warfare** between and with Indigenous peoples (design doc 12.3).
5. **Farming.**
6. **Crafting and complex multi-step manufacturing chains** (design doc 12.6).
7. **Weather effects on screen** (snow, rain, fog; today only a colour modulate).
8. **Expanded American parts** (design doc 7.3 and 10.7).

## Systems from the design doc not yet built

| Area | Planned item | Source |
|---|---|---|
| Transport | York boats, dog sleds, toboggans, Red River carts; vessel condition for the player | §12; [[York Boat]], [[Dog Sled]] |
| Plains | Horses as property and mount; the communal buffalo hunt with hunt-captain discipline; horse raiding | §12.3, §16.1; [[Buffalo]] |
| Ecology | The 10-year hare/lynx cycle; herd fragmentation; moose, black bear, marten, muskrat, foxes, lynx, otter, mink, fisher, wolverine, polar bear; elk, pronghorn, grizzly, mountain goat and sheep; salmon; seals and beluga | §16.2; `world-content-needed.md` section 6 |
| People | Aging; slavery; tradesmen (blacksmith, cooper, carpenter, boatbuilder, tailor, cook, surgeon); interpreters; settlers; a colony governor and sheriff; De Meuron soldiers; Métis hunter and hunt captain | `world-content-needed.md` section 7 |
| Economy | Crafting and smithing recipes; perks; founding a new post; warehouse quality-bucketing; the Continental Blockade's fur-market effects | §12.6-12.7, §13 |
| Information | Black-market intelligence; message forgery and verification; the express courier | §10.3 |
| Water | Per-waterway freeze and breakup; the Autumn Scramble and Spring Lockout; canoe-to-sledge conversion | §14.1 |
| Disease | Smallpox backstory; measles and whooping cough, 1819 to 1820 | §13.5 |
| Culture | Barks in several voices (Orkney Scots, Canadien French, Cree, Michif, Gaelic); name lists; Hogmanay, the King's birthday and other holidays | `world-content-needed.md` section 9 |
| Campaign | More beats: Macdonell wintering at York, the Athabasca starvation 1815-16, Lagimodiere's walk, the 1817 Selkirk treaty with Peguis, the 1818 missionaries, the arrests at Grand Rapids 1819 (check) | `world-content-needed.md` section 9 |
| Metis path | A full third starting road (§5.3), the Forks, Red River farms, carts | [[Metis]], [[Starting Paths]] |
| Places | Sacred sites as a map layer (with care); battle sites (Seven Oaks); interiors; named York buildings; Fort William's building set | `world-content-needed.md` section 5 |

## Art still wanted

From `docs/asset-prompts.md`: the Factor sheet (done), nations with no sheet, effects art for weather, fire and combat; a UI theme and fonts; portraits for the conversation screen; a period world map image; the weathered log set for the NWC house; a purpose-drawn **buffalo** sheet (prompt 5.7); palisade gates; plank roofs; Mandan earth lodges as enterable buildings. Details in [[Asset Inventory]].

## Wiring backlog (no new art needed)

Per the asset inventory: place the remaining unwired props (traps, ice holes, graves, pelt frames, drying racks, cannon...); extend the HUD to the eight unused need icons (cold, fever, fatigue, drunk, scurvy, wounded, load, standing); show the four faction crests in the journal, dialogue, map and standing display; add the 15 item icons with no ItemId (letter, dispatch, ledger, journal, fur_pack, medicine_bag...); `stove` and `writing_desk`; the three vehicles when their modes exist; orphan animation rows.

## Audio still wanted

Real wolf recordings (the current wolves are synthesised); more takes of musket, punch, door, goose, raven; footsteps on dirt and river ice; summer-day and freeze-up beds; fight grunts; UI clicks and stingers; tension, combat and story music; Hogmanay, Metis fiddle, voyageur songs, NWC rendezvous, a bagpipe lament; campaign set-piece cues; sounds for the goose flight, salting, berries, gumming a hull, portaging. Indigenous music cues to be licensed or commissioned from real musicians. See [[Music]], [[Sound Effects]].

## Housekeeping and risk

- Record or remove the three external packs with no licence file; delete about 1,600 unused files and two `.zip`s. See [[Credits and Licenses]].
- Verify names, dates and attributions against the Dictionary of Canadian Biography (items marked "check" in the content doc).
- Tune the planning numbers: tier counts and cadences, canoe condition values, travel-mode compression, the winter strain curve, Fort William council tuning.
- Open design questions: 1801 vs 1811 start, marriage-track mutability, herd fragmentation, how many agents may be live at the Fort William rendezvous, the skill-point budget.

## See also

[[Development Status]] | [[Changelog]] | [[Asset Inventory]] | [[Architecture]] | [[Game Overview]] | [[Design Principles]]
