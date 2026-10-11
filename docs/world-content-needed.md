# World Content Still Needed

Nations, groups, people, animals, posts, places and assets the game still needs: either missing from the code, or present only as a name. Written 2026-10-10 against the code as it stands. For each entry: **what's there now**, **what's missing**, and a rough priority (**P1** affects the York Factory year being played now; **P2** needed once the player leaves York; **P3** the wider map and later years).

Sources checked: `Agents/Npc.cs` (13 `Role`s), `Ecology/Species.cs` (4 `Species`), `Items/Items.cs` (37 `ItemId`s), `Data/World/world.json` (105 places), `Campaigns/historical.json` (13 beats), `Scenes/World/`, `assets/`, and the design doc. Art prompts are in `asset-prompts.md`; sound prompts in `audio-prompts.md`.

> **Historical caution.** Names, dates and attributions below are from general history and the design doc, not checked against primary sources. Anything marked *(check)* needs confirming before it goes into dialogue or a campaign beat. The DCB (biographi.ca) is the fastest check for people.

---

## 1. Indigenous nations and peoples

**There now:** two nations, and only at York. `Cree` (roles `CreeChief`, `CreeHunter`, `CreeWoman`, `CreeDaughter`; sprites `cree`, `cree_captain`, `cree_woman`) and `Assiniboine` (sprite `assiniboine`, no role of its own). Four camps in `world.json`: Cree winter/summer and Assiniboine winter/summer, all near York.

Every nation below needs, at least: a band/camp record with homeland and seasonal round, leaders, language (for the literacy/language barrier, design doc 10.6), trade wants and furs offered, alliances and enemies (design doc 7.2), a clothing look for sprites, and where it sits on the map.

### 1.1 Already in, needs depth (P1)

| People | What needs fleshing out |
|---|---|
| **Cree: Homeguard / Swampy Cree (Omushkego, Ininiwak)** at York | Should be split from the inland trading Cree. The Homeguard lived near the post, hunted geese and caribou for it, and were tied to it by credit and kinship. Need: named families, the goose-hunt role (`World/GooseHunt.cs`), their own seasonal round, the post's dependence on them in a bad year. |
| **Cree: inland trading bands** (Woodland Cree, Rocky Cree) | The ones who come down by canoe in summer with a trading captain (`cree_captain`). Need: the ceremony of arrival (gun salute, pipe, gift exchange before trade: the "trade ceremony" is a real, well-documented ritual), a trading captain's lieutenants, bands from different rivers competing for credit. |
| **Plains Cree** | The plains branch: horses, bison, alliance with the Assiniboine (the "Iron Confederacy"), war with the Blackfoot. P2. |
| **Assiniboine (Nakota, Nakoda)** | Has a sprite, no role and no behaviour beyond a camp. Need: a role, leaders, horses, the Iron Confederacy alliance with the Cree, provisioning trade (pemmican, dried meat, grease) into the Saskatchewan posts. |

### 1.2 Missing, needed for the main arc (P2)

| People | Why the game needs them |
|---|---|
| **Métis (Bois-Brûlés, Michif)** | A whole starting path (design doc 5.3) with no role, sprite or group in code. Buffalo hunt captains, pemmican suppliers, Cuthbert Grant's riders at Seven Oaks. Need: Red River and Pembina communities, the hunt organisation (captains, scouts, soldiers; design doc 16.1), Red River carts, sashes and capotes, the Michif language, the Métis flag (first flown c. 1815 *(check)*). |
| **Ojibwe / Saulteaux (Anishinaabe)** | Red River, Lake of the Woods, Rainy Lake, Lake Superior. **Chief Peguis** and his Saulteaux helped Selkirk's settlers survive (1812 on). The canoe route from Fort William crosses their country: wild rice, maple sugar, fish. Southern Department bands too (design doc 6.2). |
| **Dene: Chipewyan (Denesuline)** | Athabasca and Churchill country. Fort Chipewyan is on the map; the Athabasca was the richest fur district and the hottest HBC/NWC battleground in 1815 to 1821. Need: a group, caribou-dependent round, relations with the Cree (historically hostile, then peace). |
| **Blackfoot Confederacy: Siksika, Kainai (Blood), Piikani (Peigan)** | Named in design doc 7.2 as the plains military power, with war parties that physically move (15.1). None in code. Need: three nations with their own leaders, horse herds, war-party agents, trade at Edmonton / Rocky Mountain House. |
| **Tsuut'ina (Sarcee)** | Dene-speaking ally of the Blackfoot. Small, P3. |
| **A'aninin (Gros Ventre, Atsina)** | Allied with the Blackfoot. Destroyed South Branch House and attacked Manchester House in 1793 to 1794 *(check)*. A ready-made reason posts get burned. |
| **Dakota (Sioux)** | South of Red River, enemies of the Ojibwe and the Assiniboine. Design doc 15.1 already uses "a Sioux hunting party" as its example. |
| **Haudenosaunee (Iroquois) canoemen** | Mohawk men from Kahnawake and Kanesatake were hired by the NWC as voyageurs and free hunters, and some went as far as the Rockies. A distinct group inside the NWC's workforce, not a "nation on the map". |
| **Inuit** | North of Churchill; traded at Churchill (Prince of Wales Fort). Hostility and later trade with the Dene. P3. |

### 1.3 Missing, wider map (P3)

- **Southern Department:** Moose Cree, East Main Cree (Eeyou), Algonquin, Ojibwe of the Albany river. Design doc 6.2.
- **Mandan and Hidatsa** (Missouri villages): the great trade centre the NWC sent men to from the Assiniboine posts. Corn, horses, guns. Fort Clark etc. on the map are post-1821.
- **Northern plateau and Rockies:** Ktunaxa (Kootenay) at Kootenay House; Salish (Flathead) at Flathead House; Secwepemc (Shuswap) at Kamloops; Syilx (Okanagan) at Fort Okanogan; Nez Perce (Nimiipuu) at Fort Nez Perces; Spokane at Spokane House.
- **New Caledonia (NWC, Simon Fraser's posts):** Dakelh (Carrier) at Fort St. James / Fraser / George; Sekani; Dane-zaa (Beaver) at Dunvegan and Fort Vermilion.
- **Columbia coast:** Chinook (Chinookan peoples) at Astoria. Salmon economy (design doc 16.2), the Chinook trade jargon as a language mechanic.
- **Shoshone, Crow, Cheyenne** sit just south of the play area; at most a rumour or a raiding party.

---

## 2. Companies, factions and groups

**There now:** HBC (York, the London Committee in `Economy/LondonCommittee.cs`), NWC (the Nor'Westers' house at York, `NwcLedger.cs`, `Council.cs`), `AMER` posts on the map, and `FR` (French-regime) ruins.

| Group | Status | What's missing |
|---|---|---|
| **HBC Northern Department** (York) | Built | Officer ranks need the source check the design doc already asks for (5.1). Inland posts run as `TradingPostLedger`s but have no named masters with personalities. |
| **HBC Southern Department** (Moose Factory) | On map only | Its own chief, council and ship (design doc 6.2). |
| **HBC London Committee** | Built | Named members: the Governor (Joseph Berens, 1812 to 1822 *(check)*), Andrew Wedderburn (Colvile), Selkirk himself as shareholder. |
| **NWC Montreal Agents** (McTavish, McGillivrays & Co.) | Partly | William and Simon McGillivray as agents. The council at Fort William needs Fort William itself (section 5). |
| **NWC wintering partners** | One generic `NwcPartner` | A roster of named partners (one per department: Athabasca, English River, Red River, Fort des Prairies, Lac la Pluie, Upper Red River...). |
| **Red River Colony / Selkirk settlers** | Campaign flag only (`red_river_colony`) | Settlers (Scots from Sutherland, the Kildonan people, and Irish), Governor Miles Macdonell, later Robert Semple. Fort Douglas. Sheriff, surveyors, seed and cattle. |
| **De Meuron soldiers** | Missing | Swiss and German veterans Selkirk hired in 1816 to retake Red River and seize Fort William (the `selkirk-fort-william` beat). They later settled at Red River. |
| **Freemen** | Missing | Ex-company men living independently with Indigenous families (design doc 5.3). Many of the Métis path's NPCs. |
| **Orkneymen** | `Labourer` sprite | They were the core of HBC's labour force. Need: their own names (Flett, Isbister, Linklater, Spence, Sinclair...), dialect lines in `Barks.cs`, the contract cycle (3 or 5 years, then home or stay). |
| **Canadiens (French-Canadian engagés)** | `Voyageur` role | Need the NWC canoe hierarchy: *mangeurs de lard* (Montreal to Fort William) vs *hommes du nord* (winterers), plus guides, *avant* (bowsman) and *gouvernail* (steersman), and the rank ceiling (design doc 5.2). |
| **Pacific Fur Company (Astor)** | Missing | Fort Astoria, its 1811 founding, the 1813 sale to the NWC (`astoria-sold` beat exists, but Astoria isn't on the map; see 4.2). |
| **Missouri Fur Company (Manuel Lisa)** | `Fort Manuel Lisa` 1812 on map | Agents and a reason to appear. |
| **American Fur Company (Astor, New York)** | Missing | Background node (design doc 7.3, 10.7). |
| **XY Company** | Missing | Only for the optional 1801 start (design doc 4.2). |
| **The Church** | None | Correct for 1811: no missionaries until the 1818 Catholic mission at Red River (Provencher, Dumoulin) and the 1820 Anglican one (John West). A late-game arrival. |
| **Royal Navy / the supply ship's crew** | `Sailor` role | Ship names: the HBC ships of the period (*Prince of Wales*, *Eddystone*, *Edward and Ann* in 1811 *(check)*), a captain NPC. |

---

## 3. Named historical people

**There now:** William Auld (the factor, sprite `rival`). Everyone else is generic. Each name below wants a personality, goals, a portrait or sprite variant, and an entry in the campaign where they matter.

| Person | Side | Where / when | Why |
|---|---|---|---|
| William Auld | HBC | York, superintendent of the Northern Department to 1815 *(check)* | Already in; needs his real feuds with Selkirk's people. |
| William Hemmings Cook | HBC | York, chief there *(check years)* | Long-serving York officer with a Cree family. |
| Thomas Thomas | HBC | Governor of the Northern Department 1814 to 1815 *(check)* | Auld's successor. |
| Miles Macdonell | Colony | Red River governor 1811 to 1815 | Wintered at York 1811 to 1812 with the first settlers: **directly in the York campaign**. Issued the Pemmican Proclamation. |
| Robert Semple | Colony | Governor, killed at Seven Oaks 1816 | |
| Colin Robertson | HBC (ex-NWC) | Red River, Athabasca 1815 to 1819 | Turned the HBC's strategy to fighting the NWC with its own methods. |
| John Clarke | HBC (ex-NWC, ex-Astorian) | Athabasca 1815 | The disastrous Athabasca expedition (men starved). |
| Peter Fidler | HBC | Surveyor; Brandon House, Red River | Mapmaker; good companion NPC. |
| Lord Selkirk (Thomas Douglas) | Colony | London, then Fort William 1816 to 1817 | The triggered beat in design doc 4.4. |
| Cuthbert Grant | Métis / NWC | Leader at Seven Oaks | Central to the Métis path. |
| Duncan Cameron | NWC | Fort Gibraltar | Turned settlers against the colony in 1815. |
| Alexander Macdonell ("Greenfield") | NWC | Red River | Cameron's partner in it *(check)*. |
| William McGillivray | NWC | Chief agent, Fort William | Arrested by Selkirk in 1816. |
| Simon McGillivray | NWC | London agent | Negotiated the 1821 union. |
| John McLoughlin | NWC | Fort William, Rainy Lake | Later of Fort Vancouver; arrested with McGillivray. |
| David Thompson | NWC | Columbia; reached Astoria July 1811 | Leaves the West in 1812. |
| Alexander Henry the younger | NWC | Pembina, later drowned at Astoria 1814 | His journal is the best source on Red River posts. |
| Peguis | Saulteaux | Red River | Ally of the settlers. |
| Jean-Baptiste Lagimodière and Marie-Anne Gaboury | Freeman | Red River | Walked to Montreal with news for Selkirk in 1815 to 1816: a perfect courier set-piece (design doc 10.1). |
| George Simpson | HBC | Athabasca 1820; governor 1821 | The ending. |
| William Williams | HBC | Governor 1818 to 1826 | Southern Department epilogue. |
| John Jacob Astor, Manuel Lisa | American | Off map | Background nodes. |
| A named Cree leader at York | Cree | York | The Homeguard captain needs a real-sounding name chosen with care (better invented than borrowed from a real person without context). |

---

## 4. Forts and posts

**There now:** 100 posts in `world.json` (HBC, NWC, AMER, FR), each with a ledger and founding year. York Factory is the only one built as a scene (`Scenes/World/YorkFactoryExterior.tscn`). Every other post is a map point and a ledger with no buildings, people or layout.

### 4.1 Broken right now (P1)

- **`FortWilliamExterior.tscn` and `FortGarryExterior.tscn` are referenced in `Core/GameManager.cs` but don't exist.** Picking the NWC or Red River start will crash. Build them or hide those start options.
- **Fort Garry is anachronistic as the Red River start.** Upper Fort Garry is 1822 in `world.json`. For 1811 to 1821, the Red River start should be **Fort Douglas** (HBC / colony, 1812 to 1813; the map says 1815 *(check)*) or **Fort Gibraltar** (NWC, 1809), or a Métis camp at **Pembina** / **Fort Daer**.

### 4.2 Missing posts that matter in the window (P2)

| Post | Company | Founded | Why |
|---|---|---|---|
| **Fort Astoria** (Fort George from 1813) | PFC, then NWC | 1811 | The campaign has the `astoria-sold` beat but the post isn't on the map. (The `Fort George` in `world.json` is the 1792 NWC post on the North Saskatchewan, a different place.) |
| **Fort Wedderburn** | HBC | 1815 | Lake Athabasca, beside Fort Chipewyan: the HBC's Athabasca beachhead and a centre of the 1815 to 1821 violence. |
| **Fort Daer** | HBC / colony | 1812 | At Pembina, where the settlers wintered. |
| **Jack River House** | HBC | 1801 *(check)* | Forerunner of Norway House (1814). |
| **Fort Qu'Appelle (NWC) / Rivière Qu'Appelle posts** | NWC, HBC | 1810s | Pemmican posts; where the Métis gathered before Seven Oaks. (The `Fort Qu'Appelle` in `world.json` is the 1864 HBC post.) |
| **Fort Chipewyan's HBC rival posts, Île-à-la-Crosse HBC post** | HBC | 1810s | The opposition posts HBC built next to NWC ones. |
| **Bas de la Rivière (Fort Alexander)** | NWC | on map | Pemmican depot of the NWC; needs to be a real supply node. |
| **Lachine / Montreal** | NWC | off map | Departure point of the brigades (an off-map node, design doc 10.7). |

### 4.3 Posts on the map that need a playable layout (by priority)

1. **Fort William** (NWC HQ, the July rendezvous, Great Hall, the council). Its own building set: Montreal-style squared-timber buildings, the Great Hall, a palisade, the canoe yard, the voyageurs' camp outside the walls.
2. **Fort Douglas / Fort Gibraltar** (Red River pair, Seven Oaks between them).
3. **Norway House** (HBC's inland depot from 1814, the hub of the York boat brigades).
4. **Cumberland House** (HBC's first inland post, 1774; the Saskatchewan gateway).
5. **Churchill** (Prince of Wales Fort, the stone fort the French blew up in 1782, and the wooden Churchill post beside it).
6. **Oxford House** (already used by the campaign and the dead-master logic).
7. **Edmonton House / Fort Augustus** (the plains pair; Blackfoot trade).
8. **Moose Factory** (Southern Department HQ).
9. **Fort Chipewyan / Fort Wedderburn** (Athabasca).

### 4.4 Posts dated after 1821 (P3: leave in, but they should never spawn)

Fort Pitt 1829, Fort Pelly 1824, Fort Ellice 1831, Fort Qu'Appelle 1864, Upper/Lower Fort Garry 1822/1830, Rat Portage 1836, Touchwood Hills 1852, Fort Vancouver 1825, Fort Nisqually 1833, Fort Colvile 1825, Fort Langley 1827, Fort Victoria 1843, Fort Connolly 1826, Fort Babine 1822, Dease Lake 1838, Fort Chilcotin 1829, Fort Assiniboine 1824, Fort George (Chisasibi) 1837, Great Whale River 1820, all the AMER Missouri posts except Fort Manuel Lisa. Confirm `posts_by_founding_year` keeps them out.

---

## 5. Buildings and places

**There now:** Kenney stone walls and roofs (`assets/tiles/buildings`, placeholders), props (beaver dam and lodge, cache, campfire, cannon, beached canoe, crate, drying rack, fish trap, flagstaff, fur bale, fur press, grave, hay bales, hearth...), a ship and a canoe.

| Needed | Priority | Notes |
|---|---|---|
| **Log / squared-timber wall set** to replace the Kenney stone | P1 | York was wood: piece-sur-pièce and log construction, plank roofs, palisade. Also listed in `asset-inventory.md`. |
| **York Factory buildings by name** | P1 | The Factor's house, the men's house, the trade store and warehouse, the provision store, the cooper's and blacksmith's shops, the boat shed, the powder magazine, the observatory and flagpole, the graveyard, the "Homeguard" Cree tents outside the walls. |
| **Interiors** | P1 | Men's house, Factor's mess and office, trade room (with the trading window and counter), store. Furniture beyond Kenney's library set. |
| **Indigenous dwellings** | P1 | Cree conical lodges covered in birchbark or moose/caribou hide; summer vs winter lodge. Plains tipis (Assiniboine, Blackfoot) for P2. Ojibwe wigwams. Drying racks, smoking racks, caches on stilts (cache prop exists). |
| **Berry patches** | P1 | Berries are now gathered by every camp and post through July, August and September (`Economy/Gardens.cs`) and are the main answer to the scurvy, but there is nothing to see: no bush in fruit, no picked bush, no bare winter bush. Saskatoon, cranberry and rosehip are the three that matter here. A gatherable prop like the woodpile would also give the player something to walk to. |
| **Mandan / Hidatsa / Arikara villages** | P2 | The Missouri towns are farming towns in code now (`Economy/Gardens.cs`) and they are drawn as plains tipis, which is flatly wrong. They lived in **dome-shaped earth lodges** - a timber frame carried on four central posts, covered with willow, grass and sod, twelve metres across, with a smoke hole and an entry passage - in villages behind a **ditch and palisade**, surrounded by **garden plots** of corn, beans, squash and sunflower on the bottomland, with **corn-drying scaffolds** on the lodge roofs and in the village, and **bullboats** (a round hide boat on a willow frame) at the river. This is the great market of the plains and it should not look like a hunting camp. |
| **Red River farms and Métis houses** | P2 | River lots (long narrow strips back from the river), Red River frame houses, Red River carts, hay stacks, the colony's windmill (later). |
| **Fort William building set** | P2 | See 4.3. |
| **Portages and landmarks** | P2 | Key portages are already a map layer (design doc 6.1). Need: the trails, put-in points, the Hayes River route from York (Hill River, Robinson Portage, Painted Stone), Methye Portage to the Athabasca. |
| **Sacred sites S1 to S11** | P3 | A map layer in the design doc. Treat with care: show as places of significance and consequence, not as loot or quest markers. |
| **Battle sites** | P2 | Seven Oaks (Frog Plain), the site of the 1815 colony attack. |

---

## 6. Animals

**There now:** four `Species` in code: caribou, goose, beaver, wolf. Art also exists for **hare** and **ptarmigan** with no species behind them. `Hare` is already an item.

| Animal | Region | Why it matters | Priority |
|---|---|---|---|
| **Snowshoe hare** | Everywhere | Art and item exist; add the species. Winter food when everything else fails; snaring. | P1 |
| **Willow ptarmigan** | Bay | Art exists; add the species. Shot in thousands at York in winter. | P1 |
| **Fish** (whitefish, pike, sturgeon, lake trout) | Everywhere | `Fish` item exists, no animal. Nets under ice keep the post alive (`scurvy` commit). Sturgeon matter on the Saskatchewan and Rainy Lake. | P1 |
| **Polar bear** | Bay coast | Walks the shore at York in autumn: a real hazard. | P1 |
| **Sled dogs** | Everywhere | Winter travel, barking, dog teams as property. | P1 |
| **Marten** | Boreal | The second most valuable fur after beaver; trapline animal. | P1 |
| **Muskrat** | Marshes | Cheap but numerous fur; food. | P2 |
| **Red fox, arctic fox** (and cross and silver phases) | Bay | Arctic fox was a Churchill specialty; silver fox the prize pelt. | P2 |
| **Lynx** | Boreal | Follows the 10-year hare cycle; pelt trade. | P2 |
| **Otter, mink, fisher, wolverine** | Boreal | Trapline furs; wolverine robs traps and caches (good antagonist). | P2 |
| **Moose** | Boreal | Main big game of the forest (design doc 16.1 stalking hunt). | P2 |
| **Black bear** | Boreal | Food, grease, hazard. | P2 |
| **Bison** | Plains | The Métis path depends on it: herd simulation, pemmican. | P2 |
| **Horses** | Plains | Mounted hunt, raiding, wealth (design doc 12.3). | P2 |
| **Elk (wapiti), pronghorn, woodland caribou, grizzly bear** | Plains, Shield, Rockies | Regional identity (design doc 16.2). | P3 |
| **Mountain goat, bighorn sheep** | Rockies | | P3 |
| **Salmon** | Columbia | A whole food economy (design doc 16.2). | P3 |
| **Seals, beluga whales** | Bay | HBC ran beluga fisheries at Churchill *(check dates)*; seals for Inuit trade. | P3 |
| **Ducks, swans, loons, ravens, grey jays** | Everywhere | Mostly ambience; ducks and swans as food in season. | P3 |
| **Mosquitoes and blackflies** | Summer | Not an animal to render; a summer misery modifier on needs. | P2 |

Population cycles worth modelling (design doc 16.4): the **hare / lynx 10-year cycle**, beaver trapped out of a district, caribou migration failing.

---

## 7. People roles (the `Role` enum)

**There now:** Factor, Clerk, Labourer, Woman, Postmaster, CreeChief, CreeHunter, CreeWoman, CreeDaughter, Sailor, Wife, NwcPartner, Voyageur. Sprites also exist for `child_boy` and `child_girl`.

Missing roles, roughly in order of usefulness at York first:

- **Tradesmen of the post:** blacksmith/armourer (repairs guns: a big deal), cooper (barrels for salt geese), carpenter, boatbuilder (York boats from c. 1800 to 1820s), tailor, cook, surgeon.
- **Boat crew:** steersman, bowsman, guide (the NWC rank ceiling, design doc 5.2).
- **Interpreter / linguist:** often a mixed-descent man or a wife; key to the language barrier (design doc 10.6).
- **Children** as a role (sprites exist; `CreeDaughter` is the only child role).
- **Homeguard hunter / goose hunter** (separate from the visiting `CreeHunter`).
- **Settler** (man, woman, child), **colony governor**, **sheriff**.
- **Soldier** (De Meuron), **ship's captain**.
- **Métis hunter, hunt captain, cart driver, freeman.**
- **Iroquois voyageur, NWC clerk, NWC guide.**
- **Other nations' leaders and warriors** (from section 1).
- **Elder / medicine person** as a respected figure in camps (portrayed as a person with standing and knowledge, not a magic class).

---

## 8. Items

**There now:** 37 `ItemId`s. Icons already drawn but **not in the enum**: letter, dispatch, journal, ledger, made-beaver token, tobacco, beads, awl, stroud, ice chisel, pemmican bag, fur pack, powder horn, fishing net, medicine bag.

| Needed | Why |
|---|---|
| **Tobacco** (Brazil twist) | The single most important trade and diplomatic good: smoked before every trade and council. Icon exists. |
| **Shot, ball and powder** (powder horn icon exists) | Muskets currently have no ammunition economy; design doc 15.3 needs reloads to cost something. |
| **Beads, awls, needles, vermilion, brass kettles, files, scrapers, mirrors** | The real trade goods list (the HBC "standard of trade"). `TradeGoods` is one lump right now. |
| **Stroud cloth, point blankets by size** (1, 1.5, 2, 2.5, 3, 4 point) | Blankets were priced by points; ties straight into the made-beaver economy. |
| **Made-beaver token** | The HBC unit of account. Icon exists. |
| **Letter, dispatch, journal, ledger** | The physical message system (design doc 10.1.1) needs them as real objects you can carry, lose, steal, read. Icons exist. |
| **Ice chisel, fishing net** | Winter fishing. Icons exist. |
| ~~**Berries**~~ **in code 2026-10-11** | Gathered by every camp and post July-September, eaten first off the shelf, spoils with the fresh meat, and answers the scurvy. **Still wants its own icon** - it borrows `pemmican.png`. And berry bushes as a world prop you can see and pick (section 5). |
| ~~**Spruce beer**~~ **in code 2026-10-11** | A post with a kettle and firewood brews it when its men have gone a fortnight without anything fresh, as the Company really did. **Wants its own icon** - it borrows `ale_tankard.png`, and should not look like ale. |
| ~~**Corn**~~ **in code 2026-10-11** | The Missouri towns' harvest and the staple of the plains trade: thirty a mouth comes in each September, goes out to every camp within a fortnight's travel at two for one, and the Assiniboine carry it north to the Bay. Food and trade, not physic. **Wants its own icon** - it borrows `pemmican_bag.png`; braided ears and a bag of shelled corn would read better. |
| **Grease, marrow fat, wild rice, maple sugar, flour, oatmeal, potatoes** | The rest of the food variety by region. Grease and marrow fat are the other half of real pemmican; wild rice is Ojibwe country, maple sugar the Great Lakes. |
| **Gun repair / gunflints** (`Flint` exists) | Ties into the armourer role. |
| **Furs:** marten, otter, fox (red, cross, silver, arctic), lynx, muskrat, bear, wolverine, mink, buffalo robe | Each animal in section 6 needs its pelt, and summer/winter grades (`ThinPelt` already does this for beaver). |
| **Medicine bag** | Icon exists. Handle with care; frame as a person's private property with meaning, not a buff item. |
| **Sash (ceinture fléchée), capote by colour, leggings, moccasins, mittens, tuque** | Clothing beyond the three now; the Cold need wants layers. |
| **Red River cart, dog sled, toboggan, York boat** (as owned transport) | Logistics (design doc 12). Canoe and ship already exist as objects. |

---

## 9. Systems with content hooks but no content (quick list)

- **Barks** (`Agents/Barks.cs`): lines per group in their voice: Orkney Scots, Canadien French phrases, Cree words, Michif, Gaelic for Highland settlers. Currently one voice.
- **Campaign beats** (`historical.json`, 13 beats): add Macdonell wintering at York 1811 to 1812; the Athabasca starvation 1815 to 1816; Lagimodière's walk to Montreal; the 1817 Selkirk treaty with Peguis; the 1818 missionaries; the HBC's arrest of NWC men at Grand Rapids 1819 *(check)*.
- **Disease** (design doc 13.5): smallpox (1781 to 1782 epidemic as backstory), measles and whooping cough (1819 to 1820 epidemic across the plains *(check)*).
- **Holidays at the post:** Christmas and New Year (Hogmanay), the King's birthday (4 June), the ship's departure day, Sundays. All have music cues in `audio-prompts.md`.
- **Names:** name lists per group (Orkney, Highland, Canadien, Métis, Cree, Ojibwe, Dene...) for spawned NPCs.

---

## 10. Other assets still to make

Art (`asset-prompts.md`) and audio (`audio-prompts.md`) cover most of it. Also outstanding:

- **Audio: most of it.** A first batch is in as of 2026-10-11: six music tracks, wind, river, fires, footsteps, musket, bells, a few birds. See `audio-prompts.md` > **In the game now**. Still silent: wolves and every animal added from section 6, indoor and wharf footsteps, rain, fight grunts, UI clicks, tension music.
- **Animals:** sprite sheets for every new species in section 6 (`asset-prompts.md` 3.7 has the header and rows).
- **People:** sheets for the new roles and nations in sections 1 and 7: Métis man and woman, Ojibwe, Dene, Blackfoot, Iroquois voyageur, settler family, De Meuron soldier, tradesmen, ship's captain, the named people in section 3 who need a recognisable look (Selkirk, Macdonell, Grant, McGillivray).
- **Animations listed as missing** in `asset-prompts.md` priority 2 (death, side-walk, eat, shoot for existing characters) and 1.7 (children).
- **Buildings:** the log set and named York buildings (section 5), the Fort William set, lodges and tipis.
- **Map:** a period-style world map image for the journal/map screen; region and district boundary overlays.
- **UI:** a font (a period serif or hand-written ledger script for the journal; a clean readable one for menus), a cursor, a proper UI theme, portraits for the conversation screen, the journal / ledger book layout.
- **Window icon:** `icon.svg` is still the Godot logo (export blocker per `asset-inventory.md`); `asset-prompts.md` 8.3 says the art is done, so check it's been swapped in.
- **Licences:** `critters`, `isometric tileset` and `Assets/` still have no licence on file (`asset-inventory.md` section 10).
