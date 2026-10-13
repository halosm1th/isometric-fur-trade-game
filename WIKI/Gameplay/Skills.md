---
title: Skills
aliases: [Skill System, Hunting Skill, Trapping Skill, Trade Skill, Woodcraft]
tags: [gameplay, system, skills, character]
status: implemented
---

# Skills

**Status:** Implemented. Code: `Scripts/Agents/Needs.cs` (`Skills` class), with uses in `Agent.cs`, `PlayerController.cs`, `Economy/Barter.cs`, `Ecology/Trapline.cs`.

![[char-player.png|160]]

There are **four skills**, each a number from 0 to 1, and they are the same skills every NPC has. Nobody trains in a menu: *you get better by doing the thing, a hair at a time.* Everyone starts at **0.15** unless a background or trait changes it.

| Skill | Grows by | What it improves |
|---|---|---|
| **Hunting** | +0.01 each time you fire a ranged weapon (musket or bow) | Shrinks your chance to miss: the range-based miss chance is multiplied by (1 - 0.55 x skill). Also helps hares/fish for NPCs. |
| **Trapping** | +0.02 each time you set a line of traps | Each trap's overnight catch chance is multiplied by (0.6 + 0.8 x skill): 0.72 at the default 0.15, 1.4 at full skill. |
| **Trade** | +0.012 per barter deal | Widens your margin by 0.08 x skill, raises the chance a haggle works (0.42 + 0.4 x skill), and lets you read how sharp an NPC's price is at 0.35+ ("passable") and 0.6+. See [[Trading and Barter]]. |
| **Woodcraft** | +0.02 per tree felled | At 0.5 or more an axe yields **6 firewood** per tree instead of 4 and swings faster (0.8 s instead of 1.2 s). |

Skills are saved with the player and, when you die, your heir inherits the better of the two hunting and trapping values ([[Heirs and Succession]]).

## Reading your skills

Open the Pack tab (`I`): the Skills line shows each as a word and a number, e.g. `hunting passable (0.32)`. The words are:

| Word | Range |
|---|---|
| green | below 0.30 |
| passable | 0.30 - 0.49 |
| practised | 0.50 - 0.74 |
| master | 0.75 and above |

## Starting values from background and traits

When you create a character ([[Starting Paths]]), the background sets some skills and the traits add to them (capped at 1):

| Background (path) | Effect |
|---|---|
| Orkney labourer (HBC) | Woodcraft 0.35, an extra axe |
| Apprentice clerk (HBC / NWC) | Trade 0.4, rank clerk, standing 30 |
| Country-born (HBC) | Hunting 0.4, Cree fluency 0.7, kin in the band |
| Voyageur (NWC) | Woodcraft 0.3, Hunting 0.25, a tumpline |
| Freeman's son (NWC) | Hunting 0.45, Cree 0.8, kin in the band |
| Hunter (band) | Hunting 0.5, Trapping 0.35, a bow |
| Trapper and go-between (band) | Trapping 0.45, Trade 0.35, two steel traps |
| Nephew of the leading man (band) | Hunting 0.35, a bond with the leader |

| Trait (choose two) | Effect |
|---|---|
| Keen eye | Hunting +0.2 |
| Patient hands | Trapping +0.2 |
| Silver tongue | Trade +0.2 |
| Bushwise | Woodcraft +0.2 and snowshoes |
| Hardy | A point blanket and 3 dried meat |
| Ear for tongues | Cree and Assiniboine fluency +0.3 |
| A bottle in the pack | 3 drams of rum |
| Something put by | 12 extra trade goods |

## Languages are a separate track

Fluency in **Cree** (starting 0.1) and **Assiniboine/Nakoda** (0.05) is a number from 0 to 1 that grows through talk and marriage; it is not one of the four skills but works the same way. Having a spouse or a Company woman near acts as an interpreter. See [[Languages]].

## Tips

> [!tip]
> Skills only matter relative to what you are doing: set traps early (the first line is worth +0.02), keep a good axe in your pack, and shoot from close range once your Hunting is still green.

## See also

[[Hunting]] - [[Trapping]] - [[Trading and Barter]] - [[Combat]] - [[Starting Paths]] - [[Languages]]
