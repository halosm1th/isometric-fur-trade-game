---
title: Caribou
aliases: [Deer, Barren-ground Caribou, Woodland Caribou, Reindeer]
tags: [wildlife, prey, big-game, food]
status: implemented
---

# Caribou

**Status:** Implemented (`Species.Caribou`). Final sprites (bull; cow variant cut but not yet used).

![[animal-caribou.png|220]]

| | |
|---|---|
| **Diet** | Prey (only attacks people if starving) |
| **Health** | 70 |
| **Speed** | 170 |
| **Bite** | 12 (when hungry and cornered) |
| **Meat** | 120 lb nominal |
| **Drops** | Fresh meat x18 (each 2 lb, nutrition 30), plus a caribou hide (6 lb, 0.5 MB) or a holed hide (0.2 MB) if shot |
| **Regrowth** | 0.35 a year |
| **Sustain floor** | 20%: a herd hunted below it scatters and quits the country |
| **Max live at a site** | 7 |
| **Behaviour** | Grazes around its site; flees from any human within 260 px (about 104 px if you sneak) and from shots for 8 s |
| **Seasons** | Herds are mobile: they pass through the country in April and May and again in September and October |
| **Found at** | The York caribou herd (90 of 140) and six big country herds (capacity 400); plus about 84 small woodland caribou sites (capacity 50 each) |

## Sprites

Idle, walk, run and death, with extra cow idle and walk animations: 80 frames in four directions.

![[sheet-animal-caribou.png|520]]

## Why the deer are not farmed

In the first builds the York herd was a plain logistic population with 12 deer a year of regrowth, which could never feed five households, so every spring it was hunted out. The 10 October commit "The deer pass, they are not farmed" replaced it: a barren-ground herd fills the country in September and October and again in spring, leaves a quarter of its capacity wintering behind it, and when a ground is worked past bearing the deer quit it outright until the next passage. See [[Ecology]] for the numbers.

This is a *design* statement as much as a mechanic: between the deer passing and the geese coming back there is nothing fresh in the country but hares, the lodges and what comes through the ice. The post's winter is built around that gap. See [[Needs and Survival]] for scurvy, and [[Fishing]].

## Hunting tips

- A caribou is 70 health: one musket ball (55) does not kill it; two do (the second needs a reload of about 4 seconds, and it will bolt). A bow takes three arrows; a sneak attack with a knife on an unaware animal does 2.5 times damage (45).
- Sneak (Ctrl) to cut the distance at which it notices you from 260 px to about 104 px.
- Fresh meat answers scurvy; dried meat does not. Eat it.
- A caribou is more meat than one man can carry. Butcher (E) and the game takes hide first, then meat up to your carry capacity. The rest stays on the carcass for 36 game hours; bring a second man.
- Shot hides sell for less than whole ones. If the hide matters, use the bow.
- Do not hunt the same herd flat out: below 20% capacity it leaves, and nothing will be had till it passes again. A herd that has "quit" shows as such in the notices.

## Trivia

- The barren-ground herds follow twelve markers placed by the level designers, one for each month, from tundra edge to forest edge.
- In the Gemini art pass the old stock stag was replaced by a bull and cow caribou.
- Buffalo borrow the caribou's sprites; see [[Buffalo]].

## See also

[[Wildlife]] | [[Ecology]] | [[Species Sites]] | [[Hunting]] | [[Wolf]] | [[Buffalo]] | [[Food and Drink]]
