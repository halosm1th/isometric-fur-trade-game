---
title: Travel and Wayfinding
aliases: [Travel Mode, Waypoints, River Road, Set Out, Fog of War, Exploration]
tags: [gameplay, system, travel, map, navigation]
status: implemented
---

# Travel and Wayfinding

**Status:** Implemented. Code: `Scripts/UI/TravelMode.cs`, `Scripts/UI/Wayfinding.cs`, `Scripts/UI/MapPanel.cs`, `Scripts/World/Routes.cs`, with the river road in `Scripts/Player/PlayerController.cs`. Boats, dog sleds and horses are not usable by the player yet; see [[Canoe]], [[Dog Sled]] and [[Transport]].

![[map-york-factory-game.png|520]]

*The playable York Factory - Oxford House map.*

The country is far larger than a screen, so there are three ways to get across it: walking, the short **river road**, and **travel mode** over the whole map. The map itself is knowledge: the unexplored country is fogged parchment.

## Walking

Base walking speed is 140, run x1.7, crouch x0.45 (see [[Controls]]). The ground changes your pace:

| Ground | Pace | Notes |
|---|---|---|
| Frozen river or lake ice | x1.25 | **Slippery**: you keep drifting the way you were going |
| Road | x1.1 | |
| Dirt trail | x1.05 | |
| Open ground, grass | x1.0 | |
| Snow | x0.7 (x1.0 with **snowshoes** worn) | Put them on from the pack or by using them in the hotbar |
| Sand | x0.9 | |
| Hill, mountain | x0.8 | |
| Muskeg (summer) | x0.75 | |
| Goose marsh (summer, unfrozen) | x0.6 | |

Water blocks a walker until winter ice forms. Over your carry limit you slow by up to 65% (see [[Inventory and Hotbar]]). In a canoe the pace is x1.5 (x0.6 if overloaded), with loaded canoes slower; canoes are frozen in from freeze-up to breakup ([[Canoe]]).

## The river road (York Factory to Oxford House)

Stand at the **road head** (below the gate at York Factory, or its counterpart at Oxford House) with no one within talking distance and press `E`. You set out along the river road for a long day's march ("A long day's march; any step of your own stops it."). The clock marches for the day (at least x6, scaled to match your motion) and the camera follows. It stops when you take a step, when you press `E` again, or when a **blizzard or gale** closes in. It ends with "Oxford House. The gate is ahead." or "York Factory. The gate is ahead."

## The map and waypoints

Open the map with `M` ("The country as you know it"). Drag to pan, wheel to zoom (0.6x - 8x). **Left click** sets a gold waypoint; **right click** clears it. The legend: you (dot), posts (HBC red, NWC blue squares), camps (triangles), sites (circles), waypoint (gold diamond), your task (blue diamond).

**Fog of war.** The map starts fogged except the ground around your own post and the river road (a radius of about 7000 px, plus the road and Oxford House). As you walk you reveal a radius of 2000 px around you, in fog cells of about 25 km. Brigade routes (dashed red), portages (red crosses) and landmarks appear only where you have been. Wildlife sites appear only once you have found them or been told, and a herd is drawn where you last heard it was, which may be nowhere near where it is now. Your explored area is saved ([[Save and Load]]).

## On-screen markers

Whenever the map is closed, the **waypoint** and the **target of your current task** show on screen: a diamond with a label over the spot if in sight, or an arrow at the screen edge with the distance (`Oxford House - 14 km`). Task targets include the woodpile, a tree, the ship or the Depot, the chief, the other company's post, Oxford House, a goose marsh and the Factor. See [[Daily Orders]].

## Travel mode (`G`)

Mark a waypoint, close the map, press **`G`**. The game plans a walking route; if open water lies between, it says so ("Wait for the freeze-up, or go by canoe."). Then:

- The camera pulls back (zoom x0.55) and your token marches along the route at about **25 km per game day** (1.05 km an hour, rests included).
- The clock runs with the miles; you eat from your pack if Food falls under 45.
- It announces the distance and days: "You set out for <place>: about N km, N days. G or any step of your own stops."
- **It drops back to ordinary play wherever something needs you**, and always says why: any step of your own (or `G` again); people coming into sight (within 1100 px, except those you started among); **wolves** within 1000 px; a post or camp ahead (within 1800 px); a **blizzard or gale**; and **nightfall** (20:00) if you are hungry (Food under 35) or cold (Warmth under 45).
- On arrival: "You have come to <place>." and the waypoint clears.

In multiplayer, a joined player's march moves at a fixed on-screen pace and cannot hurry the shared day, whereas the host's travel drives the clock. See [[Multiplayer]].

## Tips

> [!tip]
> Carry water, flint and wood on a long road, and eat before night falls. Travel mode will not stop for hunger until Food is below 35 at 20:00.

> [!tip]
> Winter is the road season: ice opens the rivers and lakes to walkers, and snowshoes remove the snow penalty. Summer needs a canoe for anything across water.

## See also

[[Journal, Map and UI]] - [[World Map]] - [[Routes and Portages]] - [[Calendar and Seasons]] - [[Weather]] - [[Controls]] - [[Canoe]] - [[Brigade]]
