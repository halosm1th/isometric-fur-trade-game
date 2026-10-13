---
title: Blackfoot Confederacy
aliases: [Blackfoot, Siksika, Blood, Kainai, Piikani, Peigan, Sarcee, Gros Ventre, Niitsitapi]
tags: [faction, nation, blackfoot]
status: partial
---
# Blackfoot Confederacy

**Status:** Partial. Camps, language, sprite and the diplomatic rival role are built; the full mounted war-party model of design doc 7.2 and 15.1 is not.

| | |
|---|---|
| **Faction value** | `Blackfoot` (enum 8) |
| **Home territory** | The northwestern plains: the upper Saskatchewan, Bow and Missouri country ([[Saskatchewan Country]], [[Missouri and Upper Plains]]) |
| **Language** | Blackfoot (Algonquian, but unintelligible to a Cree speaker) |
| **Sprite sheet** | `blackfoot` (men), `cree_woman` (women) |
| **Atlas bands** | Blackfoot (12), Blood (Kainai) (8), Peigan (Piikani) (8), Sarcee (Tsuut'ina) (8), Gros Ventre (12) |
| **Family camps** | 8 (Edmonton House, Fort George, Buckingham House, Fort Augustus, Fort Pitt, Fort Vermilion, Fort Assiniboine, Chesterfield House) |

![[char-blackfoot.png|120]]

## History

The Siksika, Kainai and Piikani held military dominance on the northwestern plains and fought the [[Cree]] and [[Assiniboine]] for territory and horses. The fur companies were arming one side or another; every gun a post sold tipped a real balance of power (design doc 7.2). The Blackfeet besieged Manuel Lisa's Three Forks post in 1810 ([[Manuel Lisa]], [[Americans]]).

## In play

- **Camps.** Each band has a chief (the scene's Yellow Antelope leads the Blackfoot proper), a hunter (Three Loon), a wife (White Fawn Woman) and a daughter (Fawn Woman). A chief wears the Blackfoot sheet, not the Cree captain's: "a Blackfoot or a Mandan chief wears his own nation's" (code comment, `Appearance.cs`).
- **Edmonton House and its neighbours** have Blackfoot family camps: the people who actually come to those posts.
- **At odds.** Two nations with standing under 25 see each other's armed hunters as enemies. Nothing in the build yet forces war parties, but the model for it (`AtOddsWithOtherBand`) is there.
- **Language wall.** The Cree-Blackfoot kinship is 0.1: a man with perfect Cree is nearly deaf in a Blackfoot lodge ([[Languages]]).

## Notable people

None from the historical cast; the Gemini campaign's news names the Plains nations collectively.

## See also

[[Cree]], [[Assiniboine]], [[Plateau Peoples]], [[Edmonton House]], [[Combat]], [[Trading and Barter]]
