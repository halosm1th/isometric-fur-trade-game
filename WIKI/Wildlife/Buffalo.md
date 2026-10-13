---
title: Buffalo
aliases: [Bison, Plains Bison, Buffalo Hunt, Buffalo Robe]
tags: [wildlife, prey, big-game, plains, pemmican]
status: partial
---

# Buffalo

**Status:** Partial. The species, its yield, its eighteen herds and their twelve-month routes are in the game (`Species.Buffalo`, 11 and 12 October 2026). It has no sprite sheet of its own: it is drawn with the **caribou's** sprites. There are no horses, no mounted hunt and no communal hunt-captain discipline yet (design doc 12.3, 16.1).

![[animal-caribou.png|220]]

*A buffalo as the game currently draws it: the caribou stand-in. A purpose-drawn sheet is prompted for in `docs/asset-prompts.md` 5.7.*

"A bull is fifteen hundred pounds on the hoof and runs at thirty miles an hour; what a hunter actually brings away is the four hundred pounds of meat he can butcher off it." (comment in `Species.cs`)

| | |
|---|---|
| **Diet** | Prey (attacks people only if starving) |
| **Health** | 260 (the toughest animal) |
| **Speed** | 200 (about as fast as a man at a run) |
| **Bite** | 25 |
| **Meat** | 400 lb nominal |
| **Drops** | **Fresh meat x70** (2 lb each: 140 lb of carryable food, nutrition 30 each), plus a **buffalo robe** (8 lb, warmth 8, 2.0 MB) or, if the killing blow was a musket ball, a holed hide (0.2 MB) |
| **Regrowth** | 0.3 a year |
| **Sustain floor** | 25% of capacity (below it the herd scatters) |
| **Max live at a site** | 9 |
| **Seasons** | Wintering in the parkland and wooded river valleys; out on the open grass from April to October |
| **Found at** | Eighteen herds on the plains. None within reach of York. |

## The shape of a buffalo hunt

Seventy pieces of meat is more than any man can carry. So the whole shape of the hunt is that **the camp comes to the kill**, or most of it is left on the plain. When you butcher (E) the game hands over the robe first, then meat up to your carry capacity ([[Inventory and Hotbar]]); the rest stays on the carcass until it rots (36 game hours). Bring other hands.

A robe is valued at 2.0 MB, twice a prime beaver pelt. Meat is the raw stuff of [[Pemmican]], the food on which the brigades, the plains peoples and the Métis ran. See [[Food and Drink]].

## Herds

All eighteen are `WildlifeSite` nodes with 12 route markers, count about 160 to 200 of capacity 260. They are named for their country and sit near the camps that hunt them. Summer grounds are a journey out onto the grass, tens of kilometres to over 100 km from the winter quarters, so the herd is not in the middle of the camps it feeds. The nearest of them to a camp is the one that camp's hunters work (`BestBigGame` prefers buffalo to caribou).

| Herd | Herd | Herd |
|---|---|---|
| Blackfoot | Blood (Kainai) | Peigan (Piikani) |
| Sarcee (Tsuut'ina) | Cypress Hills | Touchwood Hills |
| Nakoda (Stoney) | Dakota (Santee Sioux) | Lakota (Teton Sioux) |
| Turtle Mountain | Moose Mountain (two herds) | Arikara |
| Crow | Shoshone | Cheyenne |
| Metis country, Forks | Metis country, at Pembina | |

See [[Species Sites]] for the full table and [[Missouri and Upper Plains]], [[Blackfoot Confederacy]], [[Metis]] for the peoples.

## Behaviour

Like the caribou, a buffalo grazes, flees from a human within 260 px, bolts from shots for 8 seconds. Because it has 260 health, a musket ball (55 damage) does not stop it; it takes about five. When a herd is passing (April, May, September, October) its count is raised to 70 to 100% of capacity; in the other months it drops to 25%. The passage and scatter logic is the caribou's code reused. See [[Ecology]].

The calendar fires "SummerBisonHunt" (15 June) and "FallBisonHunt" (15 October) events, which the calendar HUD announces as "The summer hunt is out on the plains" and "The fall hunt is out on the plains". At present they are announcements; the hunts themselves are the ordinary weekly sums of the camps.

## What is not here yet

- **No horses at all.** No mount, no mounted movement, no horse as property or wealth. The plains peoples in the game walk (`docs/world-content-needed.md` section 6).
- **No communal hunt.** Design doc 16.1 describes a formation chase with hunt-captain discipline, most felt on the Métis path. Not built.
- **No dedicated sprite**; no buffalo-specific sound.
- Fragmenting herds under pressure is an open design question.

See [[Roadmap]].

## Hunting tips

- Test it with `-- --buffalo` or the F1 test console ("Buffalo at the gate (caribou frames)").
- Use the musket (55 damage, 340 reach); a bow does 30.
- A ball makes the hide "holed": kill it with the bow (or finish it with a blade) if you want the 2.0 MB robe rather than a 0.2 MB hide.
- A herd hunted below 25% of capacity scatters and is gone until it next passes.
- Don't leave the meat to rot: bring helpers or a camp to the kill.

## See also

[[Wildlife]] | [[Caribou]] | [[Ecology]] | [[Species Sites]] | [[Pemmican]] | [[Hunting]] | [[Metis]] | [[Roadmap]]
