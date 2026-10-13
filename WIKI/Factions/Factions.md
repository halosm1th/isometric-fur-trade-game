---
title: Factions
aliases: [Nations, Peoples and Companies]
tags: [faction, index]
status: implemented
---
# Factions

**Status:** Implemented (14 values in the `Faction` enum; see each page for what is built and what is only designed)

Everyone in the world, people and animals alike, belongs to a faction. The `Faction` enum in `Scripts/Agents/Agent.cs` lists fourteen of them, and the order matters because saves store the number: **HBC, Cree, ShipsCrew, Wild, NWC, Assiniboine, Ojibwe, Dene, Blackfoot, Sioux, Plateau, Coast, Missouri, Metis**. New nations are always added on the end.

Faction decides which sprite sheet a person wears, which tongue they speak (see [[Languages]]), whose stores they eat from, and who they will fight. It is the first thing the game asks when two agents meet.

![[ui-crest-hbc.png|120]] ![[ui-crest-nwc.png|120]] ![[ui-crest-cree.png|120]] ![[ui-crest-assiniboine.png|120]]

## The two companies and their world

| Page | In-game faction | Role in the game |
|---|---|---|
| [[Hudson's Bay Company]] | `HBC` | Playable path. The Bay house at York Factory, the annual ship from London. |
| [[North West Company]] | `NWC` | Playable path. The Nor'Westers' house across the river, the July brigade from Fort William. |
| [[XY Company]] | none | Historical rival absorbed in 1804. Mentioned in the design only. |
| [[London Committee]] | none (a system) | Sets the standard of trade and the quota each year. |
| [[Red River Colony]] | none (campaign beats) | Selkirk's settlement. Appears as news and tension, not as people. |
| [[Americans]] | none | Off-map nuisance faction. Only posts on the atlas and the *Plains Ablaze* campaign. |
| [[Ship's Crew]] | `ShipsCrew` | The eight sailors who come with the supply ship. |

## The peoples of the country

Ten nations each have a faction value, a language, a sprite sheet, and camps scattered across the atlas. In total the atlas places **38 band camps** and **86 homeguard family camps** (families living by a post).

| Page | Faction | Language (as the game names it) | Sprite sheet | Atlas bands |
|---|---|---|---|---|
| [[Cree]] | `Cree` | Cree | `cree`, `cree_captain`, `cree_woman` | 4 |
| [[Assiniboine]] | `Assiniboine` | Assiniboine | `assiniboine` | 2 |
| [[Ojibwe]] | `Ojibwe` | Ojibwe | `ojibwe` | 4 |
| [[Dene]] | `Dene` | Chipewyan | `dene` | 5 |
| [[Blackfoot Confederacy]] | `Blackfoot` | Blackfoot | `blackfoot` | 5 |
| [[Sioux]] | `Sioux` | Dakota | `dakota` | 2 |
| [[Plateau Peoples]] | `Plateau` | the Plateau tongue | `plateau` | 5 |
| [[Coast Peoples]] | `Coast` | the Coast tongue | `coast` | 1 |
| [[Missouri Villages]] | `Missouri` | Mandan | `mandan` | 7 |
| [[Metis]] | `Metis` | Michif | `voyageur` (men), `country_wife` (women) | 3 |

`Wild` is not a people at all: it is the faction of animals, hostile to everyone ([[Wildlife]]).

## How factions interact

- **Hostility.** A `Wild` agent is hostile to all. Two company rivals (`HBC` vs `NWC`) become hostile only to armed raiders of their house, and only if the house has the numbers ([[Rivalry]]). Two nations whose standing with one another has fallen below 25 see each other's hunters as enemies in the bush ([[Combat]]).
- **Standing.** Each band keeps a separate standing with the Company and with the Nor'Westers, and each company keeps standing with each band ([[Standing]]).
- **Language.** How much of another people's speech you make out is a 0 to 1 "kinship" number; the table is on [[Languages]].
- **Trade.** Bands carry their furs to whichever counter bids best, weighing price, standing, walk, rum and debt ([[Trading and Barter]]).

## See also

[[Characters]], [[Roles]], [[Starting Paths]], [[Setting and Timeline]], [[Cree Camps]], [[Assiniboine Camps]], [[Glossary]]
