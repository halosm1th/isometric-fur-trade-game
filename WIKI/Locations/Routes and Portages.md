---
title: Routes and Portages
aliases: [Brigade routes, The river road, Portages, Canoe routes]
tags: [location, routes, portage, transport, logistics]
status: partial
---

# Routes and Portages

**Status:** Partial. The York Factory to Oxford House **river road** and its three carries are fully playable (brigades, travel mode, ambush at the carries). The five great **brigade routes** are map data and a drawn layer; brigades do not yet run on them.

![[map-posts-hbc-nwc.png|600]]

## The river road (York Factory to Oxford House)

| | |
|---|---|
| Length | 316 km (12,660 px) traced in 34 points, gate to gate |
| Straight line | about 295 km |
| Tile | Footprint road tile (`SourceRoad`, pace 1.1), drawn within 60 px of the line; see [[Terrain and Biomes]] |
| Travel mode | Press E at the road head below either gate: you march waypoint to waypoint with the clock at 6x for everyone |
| Fords | The road fords the Hayes where it must (river cells marked `Ford`, shown as sand, pace 0.9, cost 1.6 to route over) |

### The three carries

In upstream order, as a fraction of the way along the road (`world.json` `layout.portages`; positions "approximate and want checking against an HBC map" per `extras.json`):

| Carry | Fraction | Along the road | Notes |
|---|---|---|---|
| **Rock Portage** | 0.404 | about 128 km | "Rock Depot", the depot where York boats gave way to canoes: everything is transshipped as well as carried (two days). The extras note puts it about 200 km upriver at real scale |
| **Borwick's Falls** | 0.531 | about 168 km | On the Hill River stretch |
| **Trout Falls** | 0.813 | about 257 km | Between Knee Lake and Oxford Lake (with the Knife Portage) |

Hill's Gates and Robinson Portage lie *above* Oxford House on the way to [[Norway House]] and were never on this leg (they were in the list by mistake until corrected from the sources).

Each carry has a sign board, a beached canoe and a cache at its landing, and the bush is trodden bare within 700 px.

## Brigades on the road

`Brigade.cs` runs two a year: **the autumn outfit** up to Oxford House once the ship is unloaded (goods, muskets, blankets, traps, rum, salt geese) and **the spring returns** down as soon as the ice is out (the winter's furs). A brigade is men, canoes and a manifest moving along the road, resolved in numbers: about a twentieth of the road a day upstream and twice that coming down, slower with a heavy load, a thin crew or a storm, and frozen in where it lies if freeze-up catches it short. State: Paddling, Portaging, Camped, Arrived or Broken.

- **At a carry** the canoes come out, the pieces go over on men's backs, and anyone who means to stop you knows exactly where you will be. With bad blood between the houses the other company may be waiting and takes half the manifest into its own store, raising tension and landing in your journal ("the other house knew which day you would be on the carrying place").
- **Hulls wear** on rapids and portage drag. A brigade carries one **spruce-gum patch per three men** (default 2); a hull that opens with no gum left loses a third of the manifest and costs two days.
- **The crew** are real men taken off the post: no morning orders, no rations from the store. They eat out of the canoe, so the house loads provisions as well as trade goods; they fish and snare at the carries. Landing puts them ashore; men whose own post is the far end start back as a homeward brigade, empty.
- Anyone walking the road beside a brigade counts as with it and hears everything at first hand.
- A brigade on the water is saved: a quicksave halfway up the Hayes loads halfway up the Hayes.

See [[Brigade]] for the vehicle and [[Canoe]], [[York Boat]] for the boats.

## The five brigade routes

`world.json` carries five dashed routes, drawn on the map (M) in dark red where you have seen them (route length is the sum of the polyline legs):

| Route | Length | From | To | Passes |
|---|---|---|---|---|
| **Montréal canoe route** | 1,223 km | East edge of the map near La Cloche and Sault Ste. Marie | Fort Bas de la Riviere | Michipicoten, Fort William, Grand Portage, Rainy Lake House, Rat Portage (see [[Great Lakes]]) |
| **Saskatchewan brigade** | 1,344 km | Fort Bas de la Riviere | Edmonton House | Lake Winnipeg, the Norway House area, The Pas, Cumberland House, Nipawi, Carlton House, Manchester House (see [[Saskatchewan Country]]) |
| **Northern (Athabasca) brigade** | 1,033 km | Cumberland House | Fort Chipewyan and the Slave River beyond | Lac la Ronge, Frog Portage, Ile-a-la-Crosse, Methye Portage |
| **York Factory inland route** | 620 km | York Factory | Norway House, then Lake Winnipeg | Oxford House, the Hayes, Norway House |
| **Columbia / New Caledonia brigade** | 1,235 km | Fort Vancouver | Fort St. James | Fort Nez Perces, Fort Okanogan, Fort Kamloops, Fort Alexandria (BC), Fort George (BC) |

(Distances are those of the traced polylines.)

## Great carrying places on the map

Eight portages are marked in `layout.landmarks`: **Methye Portage** (Portage La Loche, a 20-km divide between the Churchill and Mackenzie watersheds), **Frog Portage** (Portage du Traite, Churchill to Saskatchewan), **Grand Rapids** (head of navigation where the Saskatchewan enters Lake Winnipeg), **Grand Portage** (13.5-km carry around the Pigeon River falls), **Portage la Prairie** (Assiniboine to Lake Manitoba), and the three Hayes carries above. See [[Landmarks]].

## Long journeys on foot

For a hundred miles off the road, `Routes.Plan` (A*, on a background thread) plans the cheapest way from here to there over the world map: round the lakes, over the rivers at the fords, through the bush slowly (cost x1.4 in forest), up mountains (x2.2), over fords (x1.6), and **over lakes and rivers in winter** ("the rivers and lakes are the winter road"). The result is a few hundred waypoints to follow. Waypoints come from clicking the map and pressing G; see [[Travel and Wayfinding]].

## Tips

- Time a trip to the carries: an interception needs the other company to know you are coming.
- A heavy load slows a canoe (300 lb is the small canoe's capacity); overloaded is much slower.
- Don't leave a brigade short of gum or provisions.

## See also

[[Oxford House]] - [[York Factory]] - [[Landmarks]] - [[Brigade]] - [[Canoe]] - [[World Map]] - [[Travel and Wayfinding]]
