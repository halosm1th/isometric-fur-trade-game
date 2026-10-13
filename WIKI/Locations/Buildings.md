---
title: Buildings
aliases: [Building tiles, Walls and roofs, Palisades, Building kinds]
tags: [location, buildings, tiles, system]
status: implemented
---

# Buildings

**Status:** Implemented. Every building in the world is built from the same few tiles by `StructurePainter`: a plank floor, log walls on the four edges of a rhombus of cells, a door and a window, a lifted roof, and a few furniture tiles. There is no separate interior scene: "a building's interior is just more cells, furnished in place."

![[gallery-buildings.png|700]]

## How a building is made

A building is a `BuildingDef` (kind, name, top corner, width, depth, door offset). Its cells are the rhombus from the top corner walking `width` steps down-right and `depth` steps down-left (so a 7 x 5 building reads as a rectangle on screen). `PaintBuilding` then:

1. lays **plank floor** on every cell;
2. puts a **back wall** along the north-east edge (`wall_S`, with a `wall_window_S` in the middle), a back-left wall (`wall_E`), and on the **front layer** a front wall (`wall_N`, the door cell as `wall_door_N`) and a right wall (`wall_W`);
3. caps the top corner with `wall_corner_back` and the bottom with `wall_corner_front`;
4. furnishes the interior by kind (see below);
5. in a running game adds a **roof layer** (`roof`), and places a hearth fire object on every hearth tile.

Tile edge names: **E** = back-left (NW edge), **S** = back-right (NE edge), **N** = front-left (SW edge), **W** = front-right (SE edge). Back walls live on the structure layer and front walls on a separate front layer so a corner cell can hold both, and the front layer **fades to 20% opacity** while you stand inside so you can see your room. The roof fades to zero as you walk under it. A roofed plank-floor cell counts as **indoors** ("sheltered"), as does a tipi's footprint.

**Doors.** Closed doors (sources 10 to 13) and open doors (39 to 42) swap when anyone toggles them; the pathfinder treats a closed door as passable with effort and NPCs open them on the way in. Collision is a thin strip along each wall edge, so a room keeps its perimeter floor, and open doors have none.

**Fire.** Wood burns: floors, walls, doors, fences, furniture and trees. Stone hearth tiles do not. A burnt-out cell loses its wall, floor, roof and furniture for good. See [[Fire and Warmth]].

## Building kinds

The `BuildingKind` enum has 15 values. Sizes are width x depth in cells, with the door's offset along the front wall.

| Kind | Size | Door | Where it is used |
|---|---|---|---|
| Depot | 7 x 5 | 3 | York's "Indian trading shop"; the trading room of every planned post |
| Warehouse | 8 x 6 | 3 | York's "the Depot" (fur and cargo store); stores at Moose Factory, Edmonton House, Fort William, Fort Gibraltar |
| MensHouse | 7 x 5 | 3 | Bunkhouses |
| NwcHouse | 7 x 5 | 3 | The Nor'Westers' house at York |
| OutpostHouse | 7 x 5 | 3 | Oxford House |
| TradingPost | 7 x 5 | 3 | The single house of every generic post |
| FactorsHouse | 5 x 6 | 2 | Master's, partner's and agents' houses |
| ClerksHouse | 5 x 6 | 2 | Clerks' house, counting house |
| ProvisionStore | 5 x 4 | 2 | Food stores |
| SummerMess | 5 x 5 | 2 | York's summer mess; Fort William's kitchen |
| GuestHouse | 4 x 5 | 1 | York's guest house |
| BoatShed | 5 x 6 | 2 | York's boat-building shed (outside the palisade) |
| PowderMagazine | 3 x 3 | 1 | Magazines at York, Moose Factory and Fort William |
| GreatHall | 9 x 7 | 4 | Fort William's Great Hall |
| PartnersHouse | 5 x 4 | 2 | Fort William's code-built first-pass layout (superseded by the plan in `post_plans.json`) |

### Furniture by building

Positions are (i, j) from the building's top corner.

| Building | Interior |
|---|---|
| Depot (trading shop) | Hearth (1,1); shelf (1,2); tall shelf (2,1); display case (1,3); crate (5,1); rug (3,2); table with chairs (4,2); candle stand (5,3); four beds (3,1), (4,1), (2,3), (4,3) |
| Warehouse | Fur bales in the first column (1,1..4) and alternating bales and crates in the second (2,1..4); a crate column (6,1..4); crate (5,1); hay bales (5,2) |
| NwcHouse | Hearth, shelf, tall shelf, crate, table, candle stand, five beds |
| OutpostHouse / TradingPost | Hearth, shelf, tall shelf, a fur bale, table, candle stand, five beds |
| FactorsHouse | Hearth (1,1); bed (2,2); tall shelf (3,1); table with chairs (3,3); candle stand (1,4); rug (2,4) |
| ClerksHouse | Hearth; two beds (1,2), (3,2); shelf (3,1); table (2,4); candle stand (3,4) |
| SummerMess | Three tables with chairs and a crate |
| ProvisionStore | Three crates, a hay bale, a shelf |
| GuestHouse | Two beds, a table, a candle stand |
| BoatShed | Two crates, a hay bale, a woodpile |
| PowderMagazine | One crate |
| GreatHall | Hearth (1,1); four tables with chairs in a row (2..5, 2); rug (3,1); tall shelf (5,1); two candle stands |
| PartnersHouse | Hearth; two beds |
| MensHouse | Big hearth (1,1); table (3,1); chair (4,1); crate (5,1); candle stand (5,3); nine bunks (a row of five along the middle, four along the front) |

See [[York Factory]] for who lives and works in each building of the 22 x 34 palisade; [[All Trading Posts]] for which posts have which plan.

## Palisades

York's palisade is the long stockade along the river with a two-cell gate in the south-east (river) wall; every other post is a rectangular stockade with a two-cell gate in the front wall (`gate` and `gate + 1`), a name board outside (the `PostSign`: two posts and a painted board) and a woodpile and crate inside. Sizes in cells: 12 x 9 (generic and Oxford House), 12 x 10 (the Nor'Westers' house), 16 x 14 (Cumberland House), 18 x 14 (Fort Chipewyan, Fort Gibraltar), 20 x 16 (Edmonton House), 24 x 20 (Moose Factory), 30 x 26 (Fort William), 22 x 34 (York Factory).

## Building tile set

The 38 tiles in `assets/tiles/buildings` (128 x 256 px, except `floor_planks` at 132 x 99). The base set is Kenney's *Isometric Buildings* (CC0); the walls have since been replaced by whitewashed log walls and a stake palisade, fitted from the Gemini sheets by `tools/sprites/walls.py`. Source ids are those of `terrain_tileset.tres`.

| Tile | Source | Used for |
|---|---|---|
| ![[tile-wall_N.png\|70]] wall_N | 6 | Front-left (SW-edge) log wall: the front wall of every building, with the door cell swapped for door_N |
| ![[tile-wall_E.png\|70]] wall_E | 7 | Back-left (NW-edge) wall along the left side of every building |
| ![[tile-wall_S.png\|70]] wall_S | 8 | Back-right (NE-edge) wall: the back wall of every building |
| ![[tile-wall_W.png\|70]] wall_W | 9 | Front-right (SE-edge) wall: the right side of every building |
| ![[tile-wall_door_N.png\|70]] wall_door_N | 10 | The door in the front wall (closed). Walkers open it on the way in; the interact key toggles it |
| ![[tile-wall_door_E.png\|70]] wall_door_E | 11 | In the tileset (door family N, E, S, W); never placed by the generator |
| ![[tile-wall_door_S.png\|70]] wall_door_S | 12 | In the tileset; never placed by the generator |
| ![[tile-wall_door_W.png\|70]] wall_door_W | 13 | In the tileset; never placed by the generator |
| ![[tile-wall_window_N.png\|70]] wall_window_N | 14 | In the tileset; not placed (the generator uses wall_window_S) |
| ![[tile-wall_window_E.png\|70]] wall_window_E | 15 | In the tileset; not placed |
| ![[tile-wall_window_S.png\|70]] wall_window_S | 16 | The window in the middle of each building's back wall |
| ![[tile-wall_window_W.png\|70]] wall_window_W | 17 | In the tileset; not placed |
| ![[tile-wall_corner_N.png\|70]] wall_corner_N | 18 | In the tileset; not placed (corners use the composite tiles below) |
| ![[tile-wall_corner_E.png\|70]] wall_corner_E | 19 | In the tileset; not placed |
| ![[tile-wall_corner_S.png\|70]] wall_corner_S | 20 | In the tileset; not placed |
| ![[tile-wall_corner_W.png\|70]] wall_corner_W | 21 | In the tileset; not placed |
| ![[tile-fence_N.png\|70]] fence_N | 22 | Palisade front-left run (stockade stakes): the front of every post |
| ![[tile-fence_E.png\|70]] fence_E | 23 | Palisade back-left run |
| ![[tile-fence_S.png\|70]] fence_S | 24 | Palisade back-right run |
| ![[tile-fence_W.png\|70]] fence_W | 25 | Palisade front-right run (the river wall at York; the gate gap is left empty) |
| ![[tile-floor_planks.png\|70]] floor_planks | 26 | The plank floor under every building (also source 51: the wharf and boardwalk). A roofed plank cell counts as indoors and sounds as boards underfoot |
| ![[tile-wall_door_open_N.png\|70]] wall_door_open_N | 39 | An open front door (the toggled form of door_N) |
| ![[tile-wall_door_open_E.png\|70]] wall_door_open_E | 40 | Open form of door_E (never placed) |
| ![[tile-wall_door_open_S.png\|70]] wall_door_open_S | 41 | Open form of door_S (never placed) |
| ![[tile-wall_door_open_W.png\|70]] wall_door_open_W | 42 | Open form of door_W (never placed) |
| ![[tile-wall_corner_back.png\|70]] wall_corner_back | 45 | Composite corner for the top (north) corner of a building: two back walls on one tile |
| ![[tile-wall_corner_front.png\|70]] wall_corner_front | 46 | Composite corner for the bottom (south) corner: two front walls on the front layer |
| ![[tile-fence_corner_back.png\|70]] fence_corner_back | 47 | The palisade's top corner |
| ![[tile-fence_corner_front.png\|70]] fence_corner_front | 48 | The palisade's bottom corner |
| ![[tile-roof.png\|70]] roof | 59 | The roof: one tile over every cell of a building, lifted 118 px, fading to nothing while you stand under it |

The art folder also holds four directional roofs and four roof corners. None of them is in the tileset; every roof in the game is the single flat `roof` tile.

| Tile | Status |
|---|---|
| ![[tile-roof_N.png\|70]] roof_N | In the art folder only; not in the tileset |
| ![[tile-roof_E.png\|70]] roof_E | In the art folder only; not in the tileset |
| ![[tile-roof_S.png\|70]] roof_S | In the art folder only; not in the tileset |
| ![[tile-roof_W.png\|70]] roof_W | In the art folder only; not in the tileset |
| ![[tile-roof_corner_N.png\|70]] roof_corner_N | In the art folder only; not in the tileset |
| ![[tile-roof_corner_E.png\|70]] roof_corner_E | In the art folder only; not in the tileset |
| ![[tile-roof_corner_S.png\|70]] roof_corner_S | In the art folder only; not in the tileset |
| ![[tile-roof_corner_W.png\|70]] roof_corner_W | In the art folder only; not in the tileset |

## Tips

- Stand under a roof and the roof fades; step out and the front wall fades back in.
- A hearth must be fed: it holds up to 18 game-hours of fuel (6 when freshly laid), full heat within 90 px and nothing past 330 px.
- A fire in the men's house is the fastest way to lose a post; the fort bell rings an alarm (see [[Fire and Warmth]]).

## See also

[[Props and Furniture]] - [[Terrain and Biomes]] - [[York Factory]] - [[All Trading Posts]] - [[Asset Inventory]]
