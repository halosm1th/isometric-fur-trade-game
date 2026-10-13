---
title: Player Character
aliases: [The Player, You, Protagonist]
tags: [character, player]
status: implemented
---
# Player Character

**Status:** Implemented

![[char-player.png|140]] ![[sheet-char-player.png|420]]

| | |
|---|---|
| **Sprite sheet** | `player` (150x130 frames; 79 files, the most complete sheet in the game) |
| **Class** | `PlayerController`, an `Agent` like any NPC |
| **Health** | 100 (needs: hunger, thirst, warmth all start at 100) |
| **Move speed** | 140, sprint x1.7, sneak x0.45 (`Agent.cs`) |
| **Carry capacity** | 50 lb base, plus bag bonuses; overloaded slows you down to a floor of 35 per cent |
| **Starting skills** | Hunting 0.15, Trapping 0.15, Trade 0.15, Woodcraft 0.15, before background |
| **Starting standing** | 10 (HBC and NWC); 0 "of the band" for band paths |

## Character creation

The new-game page asks for a name, a **road in**, a **background**, up to **two traits**, and an **ambition** (or "decide later"). Choices only set ordinary agent state (skills, tongues, kit, standing, rank): the player is a parameterisation of the same agent model, not a special case. See [[Starting Paths]].

| Road in | Faction | Date of arrival | Backgrounds |
|---|---|---|---|
| [[HBC Path]] | `HBC` | 20 August (the ship's day) | Orkney labourer (Woodcraft 0.35, Axe); Apprentice clerk (Trade 0.4, rank clerk, standing 30); Country-born (Hunting 0.4, Cree fluency 0.7, kin in the Cree band) |
| [[NWC Path]] | `NWC` | 12 July (the brigade's day) | Voyageur (Woodcraft 0.3, Hunting 0.25, Tumpline); Clerk from Montreal (Trade 0.4, rank clerk, standing 30); Freeman's son (Hunting 0.45, Cree fluency 0.8, kin) |
| [[Cree Path]] | `Cree` | 10 June | Hunter (Hunting 0.5, Trapping 0.35, Bow); Trapper and go-between (Trapping 0.45, Trade 0.35, 2 Steel Trap); Nephew of the leading man (Hunting 0.35, bound to the chief) |
| [[Assiniboine Path]] | `Assiniboine` | 10 June | Same three band backgrounds |
| Another people | the camp's nation | 10 June | Same three band backgrounds; pick any of the 38 band camps |

Band-born characters give up the Company's kit: they start with a Hide Coat, Snowshoes, Knife, 4 Dried Meat, Flint, Fishing Line, 2 Firewood and 2 Steel Traps, with three kin in the camp whose regard starts at 50.

## Traits (pick two)

| Trait | Effect |
|---|---|
| Keen eye | Hunting +0.2 |
| Patient hands | Trapping +0.2 |
| Silver tongue | Trade +0.2 |
| Bushwise | Woodcraft +0.2 and Snowshoes |
| Hardy | Point Blanket and 3 Dried Meat |
| Ear for tongues | Cree and Assiniboine fluency +0.3 each |
| A bottle in the pack | 3 Rum |
| Something put by | 12 Trade Goods |

## Ranks (HBC)

The ship's reckoning rates your standing: 60+ postmaster, 35+ clerk, 15+ labourer, below 15 *on notice*. You can give orders to men when you are a clerk or postmaster, or have standing 35. See [[Standing]], [[Delegation]], [[Daily Orders]].

## Ambitions

None, Rise at the post, Marry into a family of the country, Grow rich on private trade, Drive the other company off this river, Keep the band fed and whole, Found a post of your own (the last has no progress mechanic yet). See [[Ambitions]].

## Animations

`idle`, `idle_side`, `walk`, `walk_back`, `walk_side`, `run_side`, `sneak`, `sneak_back`, `sneak_idle`, `sneak_side`, `shoot`, `attack`, `eat`, `drink`, `paddle`, `carry`, `carry_side`, `chop`, `sleep`, `shiver`, `drunk`, `death`.

## Death and heirs

If the player dies, the story passes to an heir ([[Heirs and Succession]], [[The Year Told]]).

## See also

[[Controls]], [[Skills]], [[Needs and Survival]], [[Languages]], [[Beginner's Guide]], [[Characters]]
