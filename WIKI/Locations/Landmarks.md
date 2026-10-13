---
title: Landmarks
aliases: [Named places, Map labels, Lakes and rivers]
tags: [location, map, landmark]
status: implemented
---

# Landmarks

**Status:** Implemented as map data. `world.json` carries **83 named landmarks** in its `layout.landmarks` section, built by `tools/world/build_world.py` from the atlas: 8 portages, 2 mountain ranges, 6 big waters, 8 hill ranges, 41 lakes and 18 rivers. The map panel (M) draws them once you have been near enough to see them (fog of exploration: each fog cell is 1,024 px, about 25 km, and a walk reveals 2,000 px either side).

![[map-world-overview.png|600]]

## How they show on the map

| Kind | Drawn as | Visible at |
|---|---|---|
| portage | A red cross, with the name beside it | Name at zoom 1.6 or more |
| water (seas) | Large blue label | Always, once seen |
| mountains | Brown label | Always, once seen |
| lake | Smaller blue label | Zoom 1.2 or more |
| river | Small blue label | Zoom 1.6 or more |
| hills | Brown label | Zoom 1.2 or more |

Each portage also gets a **sign, a beached canoe and a cache** painted at its landing in the world itself (see [[Props and Furniture]]), and the bush is cleared round it. See [[Routes and Portages]] for what happens on the carries, and [[Travel and Wayfinding]] for how the map and waypoints work.

## Portages

| Name | World position (px) | Note |
|---|---|---|
| Methye Portage (Portage La Loche) | 47,706, 21,018 | 20-km divide between the Churchill & Mackenzie (Athabasca) watersheds. |
| Frog Portage (Portage du Traite) | 62,022, 27,111 | Churchill ↔ Saskatchewan (Sturgeon-Weir) watershed carry. |
| Grand Rapids | 73,379, 36,868 | Head of navigation where the Saskatchewan enters Lake Winnipeg. |
| Grand Portage | 102,904, 56,553 | 13.5-km carry from Lake Superior around the Pigeon River falls. |
| Portage la Prairie | 76,895, 50,798 | Assiniboine ↔ Lake Manitoba carrying place. |
| Rock Portage | 86,413, 23,047 | On the York Factory - Oxford House road. |
| Borwick's Falls | 85,811, 24,239 | On the York Factory - Oxford House road. |
| Trout Falls | 83,961, 27,937 | On the York Factory - Oxford House road. |

## Mountains, seas and hills

- **Mountains:** Rocky Mountains, Coast Mountains.
- **Seas and great lakes (labelled "water"):** Hudson Bay, James Bay, Lake Winnipeg, Lake Superior, Lake Michigan, Lake Athabasca.
- **Hills:** Cypress Hills, Wood Mountain, Touchwood Hills, Duck Mountain, Riding Mountain, Turtle Mountain, Moose Mountain, Pasquia Hills.

## Lakes (41)

Wollaston Lake, Lake Manitoba, Lake Winnipegosis, Lake of the Woods, Lake Nipigon, Reindeer Lake, Lake Winnebago, Churchill Lake, Cree Lake, Kasba Lake, Scott Lake, Yellowstone Lake, Flathead Lake, Southern Indian Lake, Lake Clare, Lake St. Joseph, Leech Lake, Lac des Mille Lacs, Big Trout Lake, Trout Lake, Island Lake, Lac la Ronge, Mille Lacs Lake, Gods Lake, Malheur Lake, Rainy Lake, Williston Lake, Split Lake, Ft. Peck Lake, Lake Sakakawea, Lake Oahe, Lower Arrow Lake, Lake Pend Oreille, Lake Chelan, Black Lake, Lower Red Lake, Upper Red Lake, Lake Île-á-la-Crosse, Wholdaia Lake, Kinbasket Lake, Harney Lake.

## Rivers (18)

Albany River, Athabasca River, Columbia River, Nelson River, Peace River, Saskatchewan River, South Saskatchewan River, Winnipeg River, Fraser River, Liard River, North Saskatchewan River, Skeena River, Slave River, Red River, Assiniboine River, Qu'Appelle River, Souris River, Hayes River.

The Hayes, the road from York Factory to Oxford House, is not in Natural Earth's river data and is traced by hand in `tools/world/extras.json` (a 10-point polyline, plus a wide "Hayes estuary" of 700 px).

## Map notes

- Positions are in **world pixels** (x, y), where 40 px = 1 km. Divide by 40 for kilometres from the map's top-left corner.
- Only posts that are standing show on the map, along with the camps; the named landmarks above are background geography and do not change.
- Dev: any landmark name works with `--spawn="Frog Portage (Portage du Traite)"`-style arguments.

## See also

[[World Map]] - [[Routes and Portages]] - [[Terrain and Biomes]] - [[Regions]] - [[Journal, Map and UI]]
