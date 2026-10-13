---
title: Fire and Warmth
aliases: [Fire, Hearth, Campfire, Arson, Firewood, Flint]
tags: [gameplay, system, fire, warmth, survival]
status: implemented
---

# Fire and Warmth

**Status:** Implemented. Code: `Scripts/World/Hearth.cs` (a fire as a simulated object), `Scripts/World/Fire.cs` (spreading fire), `Scripts/World/Blaze.cs` (the visual), `PlayerController.ClickAt` / `UseSlotServer`, `Needs.Tick`.

![[tile-hearth_left.png|96]] ![[tile-campfire.png|96]] ![[item-firewood.png|56]] ![[item-flint.png|56]]

A fire is not a tile flag: it holds **fuel that burns down in game hours** and radiates **heat that falls off with distance**. In Hudson Bay winter nights, fire is the difference between living and freezing ([[Needs and Survival]]).

## Hearths and campfires

| | Building hearth | Campfire (fire ring) |
|---|---|---|
| Where | Inside the men's house, warehouse, lodges, etc. | Any open dry land cell (not water or ice, not under a roof) |
| Fuel at start | 6 hours (lit) | 0 (you must lay wood) |
| Maximum fuel | 18 hours | 18 hours |
| Enclosed | Yes: its heat is not lost to the wind | No: an open fire in the wind loses most of its heat |
| Heat radius | Full within 90 px, falling to zero at 330 px | same |
| Intensity | 0.4 + fuel/18, from 0.4 (embers) to 1.0 | same |

Each **piece of firewood** (5 lb) adds **3 hours** of fuel. A lit fire burns one hour of fuel per game hour.

### How heat reaches your Warmth

Each hour your Warmth changes by `heat x 14` (if you are indoors or the fire is a hearth) or `heat x 14 x 0.4` (an open campfire), minus any cold you are not clothed against. `heat` is the fire's intensity multiplied by the distance falloff. So standing close to a good hearth gives about +14 an hour, more than enough to outrun even a blizzard in a house; an open campfire gives only about +5.6 at best and mainly **stops the loss**. See [[Weather]] for exposure rates.

> [!tip]
> Stay within the inner radius (90 px, about a pace or two) for the full effect. A campfire "warms about six paces and melts snow for drinking."

## Making and tending fire

- **Feed a hearth**: use firewood from the hotbar while near it, or `click` the hearth with firewood selected: "You feed the fire." Using firewood from the hotbar reports "You put a log on the fire. (N h of fuel)". Looking at a fire with `E` prints: "The fire is burning well - about N h of fuel", "burning low", or "down to embers".
- **Make a campfire**: click open ground with **firewood** selected, or select the **flint and steel** on the hotbar. With flint in your pack the fire catches as you lay it: "You lay a fire and strike a spark into it. It catches." Without flint you can still lay the wood but it stays unlit and gives no heat: "you have no flint and steel to strike a spark."
- **Light a cold hearth**: use flint beside it (it uses one firewood if the hearth is empty): "The spark takes. The fire catches."
- **Melt snow / fill the kettle**: `Q` beside a lit fire in winter ([[Needs and Survival]]).
- You cannot lay a fire indoors ("Not indoors. Feed the hearth instead.") or on water ("Not on the water.").

A hearth stuffed past **15 hours of fuel** throws sparks: each game hour there is a 2% chance one lands on the floor and catches. "A spark from an overloaded hearth has caught the floor!" Do not overfeed.

## Building fires

Fire is an object on a **wooden cell** (floors, walls, doors, fences, beds, tables, trees, tipis count as flammable; the hearth and bare ground do not).

| Property | Value |
|---|---|
| Burns a cell for | 26 seconds, then the cell is gone (no wall, floor, roof, bed) |
| Spreads | each second, 9% x weather factor to each adjoining wooden cell |
| Weather factor | clear x1.2, overcast x1.0, snow x0.35, blizzard x0.5, rain x0.25, gale x1.6 |
| Damage to anyone in it | 9 points a second (within 70 px) |
| Putting it out | water thrown on a burning cell: 55% per throw |

To throw water, use a **water** item from the hotbar near flames (within 130 px). Men of a burning household fetch water from the barrel; everyone runs from flames at their feet.

### Arson

Use flint on wood you do not own: "You put the spark to the wood. It catches." On your **own post's** timber you must be crouched (`Ctrl`) or the game refuses: "That's the Company's own timber. Crouch if you truly mean to burn it." Arson is **witnessed**: anyone whose house it is who sees it loses 50 - 60 rapport with you and comes at you armed, a "set fire to the buildings" event with weight 3 goes in the chronicle and travels as rumour ([[Gossip, Lies and Leverage]], [[Rivalry]]). The test console has a "set the men's house alight" button.

## Tips

> [!tip]
> Before dark in winter: wear everything, lay a fire with flint, and put 4+ firewood in the hotbar. A full 18-hour hearth is the best place to sleep ([[Sleeping and Resting]]).

> [!warning]
> The woodpile is not free. Taking from another household's woodpile is theft that anyone who sees will remember ([[Daily Orders]]).

## See also

[[Needs and Survival]] - [[Weather]] - [[Gathering and Gardens]] - [[Sleeping and Resting]] - [[Buildings]] - [[Props and Furniture]]
