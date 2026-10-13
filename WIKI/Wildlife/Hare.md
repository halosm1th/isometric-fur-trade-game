---
title: Hare
aliases: [Snowshoe Hare, Rabbit]
tags: [wildlife, prey, small-game, food]
status: implemented
---

# Hare

**Status:** Implemented 11 October 2026 (`Species.Hare`). Final sprites with summer and winter coats. The ten-year hare cycle is planned.

![[animal-hare.png|200]] ![[animal-hare-summer.png|200]]

*Winter coat (left) and summer coat (right).*

The snowshoe hare is the snare line's catch. Between the deer passing and the geese coming back, hares, the lodges and what comes through the ice are all there is fresh in the country, and fresh meat is what answers the scurvy.

| | |
|---|---|
| **Diet** | Prey |
| **Health** | 5 |
| **Speed** | 210 |
| **Bite** | None |
| **Meat** | 3 lb nominal |
| **Drops** | Hare (0.8 lb, nutrition 16, 0.05 MB) |
| **Regrowth** | 2.5 a year (the highest in the game; hares breed fast) |
| **Sustain floor** | 10% |
| **Max live at a site** | 5 |
| **Seasons** | White coat in winter; brown ("summer") coat in breakup, open water and freeze-up |
| **Found at** | Hare thickets: "Hare thicket" south-west of York (70 of 120, radius 500 px), 128 across the country, "by every post and camp" |

## Sprites

Idle, walk, run and death, in two coats, in four directions: 104 frames. The coat is chosen at animation time: the white set is used only when the season is Winter, otherwise the `summer_` set.

![[sheet-animal-hare.png|520]]

## Snares

Camps and posts snare hares as a background task (`ForageGoal`). Every 0.75 seconds a snarer has a 40% chance (30% in winter) of a hare, scaled by how thick the thicket is (density clamped 0.1 to 1.3). The hare comes out of the site's count, so a thicket that is snared out stays thin until it breeds back. A snarer keeps one hare for himself and puts the rest into his house's stores.

Background people use the same arithmetic, so the camps' weekly take draws on the nearest hare and ptarmigan grounds rather than meat appearing out of the bush (45af70a).

## Hunting tips

- Anything kills a hare. A musket on a hare is a waste of powder.
- Fresh hare counts against scurvy (see [[Needs and Survival]]); the post's food reckoning counts "days since fresh".
- It bolts at speed 210, nearly as fast as a wolf (230). Sneak up on it, or let the snare do it.
- Don't over-snare: below 10% of capacity the thicket is permanently dead.
- Planned: the lynx and the ten-year hare crash (design doc 16.4). See [[Roadmap]].

## See also

[[Wildlife]] | [[Ptarmigan]] | [[Ecology]] | [[Hunting]] | [[Gathering and Gardens]] | [[Species Sites]]
