---
title: Wolf
aliases: [Grey Wolf, Wolves, Wolf Pack]
tags: [wildlife, predator, hazard]
status: implemented
---

# Wolf

**Status:** Implemented (`Species.Wolf`). Final sprites; howls and growls are synthesised stand-ins.

![[animal-wolf.png|220]]

| | |
|---|---|
| **Diet** | Predator |
| **Health** | 50 |
| **Speed** | 230 (fastest animal in the game; a man cannot outrun one) |
| **Bite** | 16 damage, 55 px reach, 1.3 s cooldown |
| **Meat** | 40 lb nominal (no meat item is dropped) |
| **Drops** | Wolf pelt (0.6 MB), or a shot-through pelt (0.35 MB) if killed by a musket ball |
| **Regrowth** | 0.4 a year, scaled by how much prey is about |
| **Sustain floor** | 20% of capacity: below it the den is dead for good |
| **Max live at a den** | 5 |
| **Behaviour** | Hunts people and prey animals; flees only when badly hurt (under 30% health) and a human is near; 60% chance to bolt at a musket shot |
| **Seasons** | Year-round; howls more in winter |
| **Found at** | Wolf dens: "Wolf den" south-west of York (6 of 10), plus 85 dens across the country |

## Sprites

Idle, walk, run, attack, howl and death, in four isometric directions (SE, SW, NE, NW): 96 frames. The game uses idle, walk, run (when chasing), attack and death; the howl frames are cut but the animal-controller does not select them yet.

![[sheet-animal-wolf.png|520]]

## Behaviour

Wolves are the game's wilderness hazard (design doc 14.4: "a wilderness-hazard system for isolated travelers and posts rather than a faction"). A wolf spawns near a player when its den is in range and:

1. Looks for food: the nearest person or non-predator animal within 900 px. An armed man counts as 1.6 times as far away, so wolves prefer deer to musket-bearers.
2. Sprints at the quarry and bites. After a kill it feeds and its hunger resets.
3. With nothing in sight, drifts toward the fort where the food is.

A hungry den grows hungrier as prey thins: the pack's population follows the prey pressure of all the sites (see [[Ecology]]). Over-hunt the caribou and the wolves starve and turn toward the post; leave game alone and the prey overpopulates and does the same. Both end at the gate.

## Hunting tips

- A single musket ball (55 damage) kills a wolf (50 health); so does a sneak attack with a tomahawk. The bow (30) takes two arrows; the knife (18) takes three, fewer on a surprised target (sneak attacks do 2.5 times damage).
- Shoot rather than trap if you want the animal dead; but a ball spoils the pelt (0.35 MB vs 0.6 MB).
- Travel mode ([[Travel and Wayfinding]]) stops with "Wolves ahead" when a wolf comes within 1000 px.
- At night (20:00 to 06:00) wolves within about 3500 px howl every 35 to 90 seconds (faster in winter); three or more nearby gives the pack howl. A wolf within 420 px growls every 5 to 10 seconds. See [[Sound Effects]].
- Wolves near the post are a symptom of [[Ecology]]: look for scarcity.
- The `-- --wolves` dev argument drops four hungry wolves outside the gate; try fighting them with the [[Combat]] rules.

## Where to find them

See [[Species Sites]]. The home-country den at York holds 6 wolves (capacity 10, radius 800 px).

## See also

[[Wildlife]] | [[Ecology]] | [[Caribou]] | [[Combat]] | [[Furs and Pelts]] | [[Hunting]]
