---
title: Canoe
aliases: [Birchbark Canoe, Canot du Nord, Paddling]
tags: [transport, vehicle, water]
status: implemented
---

# Canoe

**Status:** Implemented for the player (`Scripts/World/Canoe.cs`). A bigger, loaded canoe sprite exists but is not wired. NPC brigades use abstract canoes ([[Brigade]]).

![[vehicle-canoe.png|260]] ![[vehicle-canoe_loaded.png|260]] ![[tile-canoe_beached.png|140]]

The birchbark canoe is "the design doc's 'enter vehicle' verb in its simplest form": board it from the shore, paddle on open water, step onto any bank and it stays where you left it.

| | |
|---|---|
| **Capacity** | 300 lb of cargo (a historic north canoe took about 3,000 lb; this one is a small one and "rides low") |
| **Speed** | Water speed factor 1.5x walking; x0.75 at full load; x0.6 when overloaded |
| **Crew** | The rider (player) |
| **Seasons** | Not Winter (frozen in from November to April) |
| **Found at** | Two canoes at York: one by the wharf, one outside the Nor'Westers' gate. Portage landings have a beached canoe prop. |

## Using a canoe

- Walk to an empty canoe (within about 130 px) and press the interact key (E): "You push off. Paddle onto the water; step onto any bank to land."
- While aboard you move at paddling speed over water or ice. The moment you step onto a walkable land cell you are ashore: "You step ashore and draw the canoe up." The canoe is left at the nearest water cell.
- Cargo: interact with the canoe from within about 150 px for the stow and unstow dialogue (`CanoeLoad`): "You stow N lots in her (X/300 lb)." Stowing stops when the canoe is full (300 lb); a canoe over that weight counts as overloaded and paddles at 0.6 of the normal rate.
- A canoe with a rider is hidden while it is in use (the player sprite takes over); an empty one sits drawn up on the shore.
- In winter: "The canoes are frozen in till breakup. Walk on the ice."
- Paddling plays a looping paddling sound (`canoe_paddling_loop`); a real "paddling a kayak" recording is its stand-in. See [[Sound Effects]].

## Tips

- Open water gets you across lakes and rivers that travel mode will not cross on foot: "There's no way there on foot that you can see - open water lies between. Wait for the freeze-up, or go by canoe."
- A canoe is the way to carry 300 lb of furs or trade goods past the fort; but a laden canoe is slow, so do not take more than you must.
- The brigade's canoes are real in the design (condition, spruce gum, portage damage) but not for the player: see [[Brigade]].

## Design notes

Planned (design doc 12.2): vessel *condition* for the player's own canoe (wear on rapids and portages, field repair with spruce gum, bark patches and resin); canot du nord versus canot du maitre; a canoe-to-sledge conversion. See [[Transport]] and [[Roadmap]].

## See also

[[Transport]] | [[Brigade]] | [[Ship]] | [[York Boat]] | [[Travel and Wayfinding]] | [[Routes and Portages]]
