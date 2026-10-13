---
title: Ptarmigan
aliases: [Willow Ptarmigan, Partridge, Willow Grouse]
tags: [wildlife, prey, small-game, food]
status: implemented
---

# Ptarmigan

**Status:** Implemented 11 October 2026 (`Species.Ptarmigan`). Final sprites with summer and winter plumage.

![[animal-ptarmigan.png|200]] ![[animal-ptarmigan-summer.png|200]]

*Winter plumage (left) and summer plumage (right).*

On the Hudson Bay lowlands "thin margins, matching the region's bottleneck character" (design doc 16.2). A flock of willow ptarmigan comes down to the willows in winter by the hundred, and a gun fired into it brings home several birds.

| | |
|---|---|
| **Diet** | Prey |
| **Health** | 3 |
| **Speed** | 150 |
| **Bite** | None |
| **Meat** | 1 lb nominal |
| **Drops** | Ptarmigan x1 (0.8 lb, nutrition 14, 0.05 MB); x3 per volley if shot into a flock with a musket |
| **Regrowth** | 1.5 a year |
| **Sustain floor** | 10% |
| **Max live at a site** | 8 |
| **Seasons** | White in winter, brown otherwise; snared only in winter |
| **Found at** | "Ptarmigan willows" north of York (90 of 160, radius 600 px); 47 ptarmigan sites on the map, mostly "Ptarmigan flats" in the north |

## Sprites

Idle, walk, fly and death in winter and summer plumage, four directions: 104 frames. The item icon for the Ptarmigan food item borrows the bird's own death frame (a placeholder).

![[sheet-animal-ptarmigan.png|520]]

## Mechanics

- A snarer in **winter** has a 25% chance per try, scaled by density, to take a ptarmigan from the nearest ptarmigan site (`ForageGoal`).
- A shot into the flock takes **three** birds; other kills take one.
- They fly (the fly animation) when startled.
- Fresh meat, so it answers the scurvy.

## Tips

- Visit the willows between November and April. In summer the flocks are scattered and nobody counts on them.
- A bow or a knife works as well as a musket on a 3-health bird; save the powder.
- Hunted below 10% of capacity the willows are empty forever.

## See also

[[Wildlife]] | [[Hare]] | [[Ecology]] | [[Hunting]] | [[Species Sites]] | [[Food and Drink]]
