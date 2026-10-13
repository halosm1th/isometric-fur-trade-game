---
title: York Factory
aliases: [York, The Factory, York Fort, Hudson's Bay Company depot at York]
tags: [location, hbc, post, hudson-bay-lowlands, starting-location]
status: implemented
---

# York Factory

**Status:** Implemented. This is the one fully hand-built place in the game: the playable scene (`Scenes/World/YorkFactoryExterior.tscn`) is York Factory and the country round it, and every start path begins here (see [[Starting Paths]]).

![[loc-york-factory.png|600]]

| | |
|---|---|
| **Company** | [[Hudson's Bay Company]] |
| **Founded** | 1684 (`world.json`) |
| **Region** | [[Hudson Bay Lowlands]] (north bank of the Hayes, near the mouth of the Hayes and Nelson) |
| **Map position** | 92.3 W, 57.0 N (map cell 697, 567; the palisade's north corner is cell 677, 532) |
| **Palisade** | 22 x 34 cells, gate in the river (south-east) wall |
| **Ledger** | [[Fort Ledger]] (the "Company's stores") |
| **Resident cast** | 12 people at the start: the Factor, 2 clerks, 6 labourers, 3 women (plus the 8-man ship's crew while the ship is in) |
| **Neighbours** | [[Nor'Westers' House]] (about 91 km up-country), [[Oxford House]] (316 km by the river road), the [[Cree Camps|Cree camp]], the [[Assiniboine Camps|Assiniboine summer camp]] |

> [!info] Layout source
> The layout follows "G. Ray's plan and the survey of the post" according to the comments in `WorldLayout.cs`: a long palisade whose length runs west-south-west to east-north-east along the north bank of the Hayes, the trading shop at one end, the Depot in the middle, a boardwalk from it through the river gate to the landings, and Sloops Creek behind.

![[map-york-factory-game.png|500]]

## Scale and distances

The world map runs at **40 world pixels per kilometre** on a Lambert conformal conic projection (`world.json`). A map cell is 128 px wide, so **one cell is 3.2 km across**; one step along an isometric edge (the way walls and rooms are laid out) is 71.5 px, about **1.8 km**. Buildings are drawn at "life size" against the people who walk in them, so on the map's own scale the York palisade (22 x 34 edge-steps) would be roughly 39 x 61 km. The game does not try to reconcile the two; it is simply how a 1,900-km-tall country was fitted around a fort you can walk across in a couple of minutes.

| From York Factory to | Straight line | Notes |
|---|---|---|
| [[Nor'Westers' House]] | about 91 km | Across the Hayes; ford south of the Cree camp, ice in winter, or a canoe |
| [[Oxford House]] | about 295 km (road is 316 km) | The traced river road, 34 points, gate to gate |
| Cree winter camp trail | 44 km | The 5-point trail from the gate to the winter camp |
| [[Churchill]] | about 226 km | Prince of Wales Fort |
| [[Norway House]] | about 482 km | |
| Fort Severn | about 341 km | |
| [[Albany Fort]] | about 861 km | |
| [[Moose Factory]] | about 986 km | |
| [[Fort William]] | about 977 km | |
| [[Cumberland House]] | about 710 km | |
| [[Fort Chipewyan]] | about 1,124 km | |
| [[Edmonton House]] | about 1,384 km | |

(Computed from the cell positions in `world.json`; see [[Routes and Portages]] for the brigade roads.)

## The palisade, building by building

Positions are in palisade cells (i toward the river, j along the length, from the north corner `FortTop`). Sizes are width x depth in cells. "Door" is the door's offset along the front wall. The code's own names are kept in quotation marks: the building the code calls `Depot` is the *Indian trading shop*, and the one it calls `Warehouse` is the great **Depot** (the post's actual storehouse).

| Building (code kind) | In-game name | At (i, j) | Size | Door | Role |
|---|---|---|---|---|---|
| Depot | "Indian trading shop" | 10, 27 | 7 x 5 | 3 | The trading room. The Factor's desk and the counter; the ledger's `AccessCell` is just inside the door |
| Warehouse | "the Depot" | 8, 15 | 8 x 6 | 3 | Fur store and cargo hall, with the **storeroom chest** |
| MensHouse | "Men's house" | 7, 2 | 7 x 5 | 3 | Labourers' bunkhouse; the women of the post sleep here too |
| FactorsHouse | "the Chief Factor's house" | 2, 18 | 5 x 6 | 2 | Mr. Auld's quarters |
| ClerksHouse | "Clerks' house" | 2, 10 | 5 x 6 | 2 | The two clerks' room and books |
| SummerMess | "Summer mess" | 8, 8 | 5 x 5 | 2 | Where the men eat in the open-water season |
| ProvisionStore | "Provision store" | 12, 22 | 5 x 4 | 2 | Pemmican, flour and salt geese under one roof |
| GuestHouse | "Guest house" | 16, 13 | 4 x 5 | 1 | Officers off the ship and brigade guides |
| BoatShed | "Boat-building shed" | 24, 2 (outside the palisade) | 5 x 6 | 2 | York boats built and mended, by the cargo landing |
| PowderMagazine | "Powder magazine" | -10, 6 (outside, beyond the creek) | 3 x 3 | 1 | Kept away from everything that burns |

All ten buildings are painted with the log-wall tiles (see [[Buildings]]), a plank floor and a roof layer that fades when you step under it.

### What stands in each building

- **Indian trading shop (Depot).** Hearth on the back-left wall at (1,1), a shelf and a tall shelf of trade goods, a display case, a crate, a rug, a table with chairs (where the bread and ale are set out at the start), a candle stand and four beds: the Factor's and the clerks' beds and one for the new hand. This is where [[Trading and Barter]] happens at the counter. William Auld and both clerks *work* here during the day.
- **The Depot (Warehouse).** Two columns of fur bales down one side (eight bales in the first column, alternating bales and crates in the second), a column of crates on the far side, a crate and a hay bale by the door. Cargo from the ship is carried to its cell (4,3) (`CarryCargoGoal`); furs bought at the counter end up here. For the player, standing inside gives the *storeroom chest*: stow goods and furs, draw a musket (standing 15), traps, an axe, snowshoes, a blanket (standing 10), a kettle, a tumpline, trade goods (clerks) and a ration. Raiders from [[Rivalry|the other house]] can "plunder their stores" here.
- **Men's house.** The big hearth at (1,1), a table, a chair, a crate, a candle stand, and **nine bunks** (a row of five along the middle, four along the front). Labourers and the post's women sleep here; it is also the cell that a fire "in the men's house" starts in (see [[Fire and Warmth]]).
- **Chief Factor's house.** A hearth, a bed, a tall shelf of books, a table with chairs, a candle stand and a rug. Mr. Auld sleeps here.
- **Clerks' house.** A hearth, two beds, a shelf, a table (the desk) and a candle stand.
- **Summer mess.** Three tables with chairs and a crate. A room for the open-water season.
- **Provision store.** Three crates, a hay bale and a shelf: the post's food. [[Pemmican]], dried goose and bread live in the ledger, not in the tiles.
- **Guest house.** Two beds, a table and a candle stand.
- **Boat shed and powder magazine.** Crates, hay bales and a woodpile in the shed; one crate in the magazine.

Outside the buildings the yard has the post **woodpile** at (18,7), a crate (19,25) and a hay bale (20,26). The boardwalk (planks, not dirt) runs from the yard through the river gate and out along the 14-cell **wharf** into the Hayes. The fort bell sounds from the flag cell (19,19): it rings for work at **07:00 and 18:00** (not in a blizzard or storm), for the ship coming in, and as an alarm when a fire starts.

![[loc-york-factory-wharf.png|500]]

### Sloops Creek and the surroundings

Sloops Creek is carved by the code as river, running along the back of the post (cells -5 and -6) and then along the north end down to the Hayes, with a **ford** at j = 6..7 "where the graveyard path crosses". The goose marsh lies on the bay shore just beyond the creek (180 geese, capacity 300); the upper beaver lodge (40/60) is on the river nearby, and the post knows only these two sites at the start. The caribou herd (90/140) ranges a seasonal route round the post, and the bend and lower lodges and the wolf den lie further up the Hayes (see [[Species Sites]]). Forest starts about 1,500 to 2,300 px (roughly 40 to 60 km on the map scale) from the fort centre and thickens outward, because the ground round a post is kept cleared.

## Who works where

| Person | Role | Sleeps | Works |
|---|---|---|---|
| Mr. William Auld | [[Factor]] (the household's leader) | Chief Factor's house | Trading shop |
| John Fidler, Alex Kennedy | [[Clerk]]s | Clerks' house | Trading shop (at the counter and table) |
| James Isbister, William Flett, Magnus Spence, John Linklater, Thomas Corrigal, Peter Sinclair | Orkney [[Labourer]]s | Men's house bunks | Out in the yard (cut wood, hunt, unload the ship, drive off animals) on the Factor's morning orders |
| Nancy Isbister, Margaret Flett, Jane Spence | Women of the post | Men's house | At the men's house fire: tend it, dress wounds, make pemmican |

Each morning the Factor turns the day's needs into tasks (`Delegation`): unload the ship, load the furs, cut wood, hunt, drive off animals at the gate. Each labourer has a *standing* with him that rises with work done. The Factor can be replaced by the most ambitious clerk if he dies. The ship brings **six new hands** from Stromness every August (up to a post strength of 6 labourers) with five-year contracts, who bunk in the Men's house. See [[Delegation]], [[The Director]], [[Roles]].

## A year at York

10 real seconds are one game hour; a day is 4 minutes (see [[Calendar and Seasons]]).

| Date | What happens |
|---|---|
| 1 Jan | Game start by default (1811). Ledger holds a midwinter stock: 60 pemmican, 120 dried goose, 30 bread, 90 trade goods, 22 beaver pelts, 12 rum, etc. |
| Winter (Nov-Apr) | Bay frozen; the river and bay become the winter road. Ice-fishing through lines issued by the post |
| Monthly | The postmaster at [[Oxford House]] sends a courier down the river road with furs and a report. If no report arrives in 45 days the Factor sends two men up to look |
| 5 May-30 May | Spring goose flight (relief rather than a stock) |
| 5 Jun | **Breakup.** The ice goes out; the Cree band strikes the winter camp and pitches on the trail beside the post for the trading season |
| 15 Jun | Summer bison hunt (event for the plains bands) |
| 1 Jul | The Assiniboine come down the river road to their summer camp below York |
| 12 Jul | The Nor'Westers' **brigade** from [[Fort William]] arrives at their house across the river |
| **20 Aug** | **The ship arrives** (`ShipArrives`). The autumn goose hunt opens (to 15 Oct) |
| 1 Sep | The Assiniboine go home for the buffalo |
| **15 Sep** | **The ship sails** (`ShipDeparts`) |
| 15 Oct | Fall bison hunt |
| 10 Nov | **Freeze-up.** The Cree band returns to the winter camp |

After the ship sails the Factor sends the **fall outfit** inland to Oxford House (two men and written orders); the spring returns come down as soon as the ice is out. See [[Brigade]] and [[Routes and Portages]].

### The ship

The annual ship from London ([[Ship]]) is summoned on 20 August from "offscreen", about 6,000 px (about 150 km) down the Hayes and into the Bay, sails in at 90 px/s and ties up at the end of the wharf. She carries the year's outfit: 120 trade goods, 30 pemmican, 40 dried goose, 40 bread, 30 ale, 8 muskets, 12 steel traps, 24 rum, 4 kettles, 20 point blankets and 6 capotes, plus the Committee's packet (price and quota for the coming year; see [[London Committee]]). Eight crew come ashore, the bos'n carrying the packet to the Factor (kill him and it is on his body). There is a 35 percent chance one sailor brings the fever. Furs go aboard only once the outfit is off. On 15 September she sails if at least three of her crew are alive; otherwise she is stranded at the wharf. As she sails, the Factor's report is written, the player's standing is reviewed (private trade and unexplained river crossings count against you) and the books go to London in a letter that takes 75 days.

## Player notes

- You arrive by sea: the HBC start begins aboard the ship in the roads (or aboard the Company's sloop from Churchill if the ship is not in), and "Step ashore" is your first act. Start positions are always snapped to dry ground.
- Take the **river road** from the road head below the river gate to reach [[Oxford House]]: E at the road head begins travel mode (6x clock for everyone). Take food and a capote.
- Cross the Hayes by the ford, by ice or by canoe (two canoes: one by the wharf, one at the Nor'Westers' landing).
- Beware of the caribou herd and wolves at the edge of scarcity: hungry animals range toward the post.

## Trivia

- Hill's Gates and Robinson Portage were originally in the portage list for this road but are above Oxford House, not on this leg, and were removed from the list after the map session checked the sources.
- `docs/york-factory-test-world.md` describes an older 80 x 320-cell map; the current map is 1,002 x 2,361 cells.

## See also

[[Locations]] - [[World Map]] - [[Nor'Westers' House]] - [[Oxford House]] - [[Cree Camps]] - [[Assiniboine Camps]] - [[Hudson Bay Lowlands]] - [[All Trading Posts]] - [[Fort Ledger]] - [[Ship]] - [[Buildings]]
