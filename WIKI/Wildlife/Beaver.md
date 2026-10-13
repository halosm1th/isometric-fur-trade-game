---
title: Beaver
aliases: [Castor, Made Beaver Source, Beaver Lodge]
tags: [wildlife, prey, fur, trapping]
status: implemented
---

# Beaver

**Status:** Implemented (`Species.Beaver`). Final sprites. The beaver lodge and dam props are drawn but not yet placed in the world.

![[animal-beaver.png|220]] ![[tile-beaver_lodge.png|160]] ![[tile-beaver_dam.png|160]]

"The actual economic engine" of the trade (design doc 14.4). The Company counted its accounts in the [[Made Beaver]]: one prime beaver pelt.

| | |
|---|---|
| **Diet** | Prey |
| **Health** | 20 |
| **Speed** | 90 (slowest animal) |
| **Bite** | 4 |
| **Meat** | 25 lb nominal; yields fresh meat x3 ("the tail is the best of it") |
| **Drops** | Beaver pelt (1.0 MB, prime), summer pelt (0.45 MB) from June to September, shot-through pelt (0.35 MB) if shot |
| **Regrowth** | 0.5 a year |
| **Sustain floor** | 25% of capacity: below it the lodge is dead forever |
| **Max live at a lodge** | 3 |
| **Seasons** | Pelts prime November to May; thin June to September (months 6 to 9 in the code) |
| **Found at** | Beaver lodges: the largest site type, about 236 on the map |

## Sprites

Idle, walk, gnaw, swim, slap and death in four directions: 112 frames, the biggest set in the game. The game uses idle, walk and death; gnaw, swim and slap are cut but unused.

![[sheet-animal-beaver.png|520]]

## The economy of a pelt

| Pelt | Value | How |
|---|---|---|
| Beaver pelt | 1.0 MB | Killed with bow, knife, tomahawk or trap, October to May |
| Summer pelt | 0.45 MB | Same, June to September |
| Shot-through pelt | 0.35 MB | Killed by a musket ball, any month |

Trapping in season is therefore a real economic decision, and a musket in the lodge is a bad bargain. See [[Furs and Pelts]] and [[Fur Prices]].

## Trapping

The steel trap is the beaver's nemesis: a player (or a labourer given a task) sets up to six at a lodge. See [[Trapping]] for the player's steps and [[Ecology]] for the maths:

- Each trap catches overnight with chance 7% x (lodge count / capacity) x (0.6 + skill x 0.8), taking a beaver out of the lodge.
- The line can be checked after 18 hours; others may rob it if they get there first (poaching).
- A lodge hunted below 25% of capacity collapses permanently: "X has been hunted out. It will not recover." That patch of map is changed for the rest of the game.
- Posts send men trapping in winter if they have steel traps; the Factor's [[Delegation]] hands the job out.

## Hunting tips

- Don't use the musket on beaver unless you want meat. The knife is nearly enough (18 damage vs 20 health; a sneak attack does 2.5 times damage).
- Every lodge is a finite resource. Rotate between lodges, or rest one for a season; see [[Species Sites]] for how many are near each post.
- Watch rivals: a lodge other parties are already trapping scores worse for NPC hunters (its score is divided by 1 + 0.8 for each rival line), and shared ground yields only 45% to the second party in a day.

## See also

[[Wildlife]] | [[Ecology]] | [[Trapping]] | [[Made Beaver]] | [[Furs and Pelts]] | [[Fur Prices]] | [[Species Sites]]
