# Asset Prompts for Gemini

Last verified against the repo: 2026-10-22

Art the game still needs, as prompts ready to paste into Gemini, with the sheet to attach as a style reference for each. First written 2026-10-10, pruned 2026-10-11 and 2026-10-13, and **rewritten 2026-10-22 against `717a843`**. Since the last pass the map has grown to the Pacific, the St. Lawrence and Lake Erie. It now has the Lakes frontier towns (Detroit, Amherstburg, York, Niagara, Queenston) with their farmers, merchants, tradesmen, soldiers, officers and priests, plus the War of 1812, six new peoples, elk, lacrosse, money and the farmer's year. All of that is drawn today with borrowed or tinted sheets, so it is most of the new work below.

Delivered since 2026-10-13 and removed from this file: the weather overlays, the aurora, the cursors and the UI panels (all cut and wired: `assets/ui/weather/`, `cursors/`, `panels/`); the combat effects (`assets/sprites/fx/`: muzzle and pan flash, smoke, blood, dust, slash, arrow); and the hearth and campfire flames, embers, smoke and sparks.

For what the game already has, see `asset-inventory.md`. Sound is in `audio-prompts.md`. Nations, people and places missing from the *code* are in `world-content-needed.md`.

---

## How to use this

1. **Open a fresh Gemini chat per sheet.** Old sheets in the same chat bleed into new ones.
2. **Attach the reference image(s) listed for that asset.** All paths are in this repo. The `assets/external/pioneerpixel_hbc_trader/*.jpg` sheets are the earlier Gemini sheets this game's characters were cut from; they fix the style. Attaching a single cut frame (e.g. `assets/sprites/clerk/idle_front.png`) as well fixes the scale.
3. **Paste the matching HEADER block first** (Character, Animal, Item, Prop or Effects), **then the asset's prompt** underneath it, as one message.
4. If a sheet comes back with the wrong layout (checkerboard, parchment, grid lines, numbers in cells), reply: *"Same sheet again, but on a flat solid magenta #FF00FF background with no borders, numbers or text except the row labels."*
5. Drop the result in `assets/external/NEW HBC ASSETS/` with a clear name and tell me; I'll slice it into frames and wire the animations.

**Why magenta:** the slicer finds sprites by cutting along runs of background colour. Checkerboards, parchment and cell borders all have to be cleaned by hand. One flat colour that nothing in 1811 was dyed cuts cleanly.

**Scale note.** The three oldest sheets (`player`, `cree`, `rival`) are 150×130 per frame; everything drawn since is **180×170**. Always attach one of the newer sheets (`clerk`, `voyageur`, `assiniboine`, `factor`) so new art comes back at the current scale.

**Animation names the code plays** (a sheet with these rows drops straight in): `idle`, `idle_back`, `idle_side`, `walk`, `walk_back`, `walk_side` (faces right, mirrored for left), `run_side`, `eat`, `drink`, `attack`, `shoot`, `musket`, `death`, plus the role-specific ones already in use: `talk`, `write`, `smoke`, `butcher`, `scrape`, `haul`, `chop`, `portage`, `paddle`, `carry_wood`, `carry_water`, `carry_baby`, `sleep`, `shiver`, `drunk`, `sneak`/`sneak_back`/`sneak_side`. Animals: `idle_se`, `walk_se`, `run_se`, `attack_se`, `death_se` and the same for `ne` (`sw`/`nw` are mirrored), plus `summer_*` where the coat changes and `graze_*` for the horse.

---

## Headers (paste one of these first)

### CHARACTER HEADER

```
Create one sprite sheet image for a 2D top-down game set in British North America in 1811, matching the attached reference sheet exactly in art style: detailed hand-painted pixel art in the 16-bit RPG manner, seen from a three-quarter top-down view (the camera looks down at the figure at about 30 degrees, as in an isometric game), crisp dark-brown 1-pixel outlines, a muted and historically plausible palette (indigo and navy wool, madder red, undyed linen, smoke-tanned buckskin, spruce green, iron grey), soft light from the upper left, no blur, no glow, no painterly smearing.

Layout rules - follow them strictly:
- The whole background is one flat solid magenta colour (#FF00FF). No checkerboard, no parchment, no paper texture, no shadows on the background.
- No cell borders, no grid lines, no frame numbers, no title banner, no copyright line, no logos, no extra item pictures.
- Each row is one animation. Write the row's name in small plain black capital letters at the left end of the row, clear of the figures.
- Leave at least 30 pixels of empty magenta between every figure and between rows; no figure may touch another or the label.
- Same scale in every frame: an adult man is about 110 pixels tall from heel to the top of his hat. All figures in a row stand on the same baseline.
- The same person in every frame: identical face, clothing, colours and proportions.
- "Front" means walking toward the viewer (down the screen); "Back" means walking away (up the screen); "Side" always faces RIGHT (the game mirrors it for left).
- Walk cycles are 4 frames: right foot forward, passing, left foot forward, passing.
- Nothing cropped. Output as a PNG.
```

### ANIMAL HEADER

```
Create one sprite sheet image for a 2D top-down game set in the North American interior in 1811, matching the attached reference sheet exactly in art style: detailed hand-painted pixel art in the 16-bit RPG manner, three-quarter top-down view (camera looking down at about 30 degrees, as in an isometric game), crisp dark 1-pixel outlines, naturalistic muted colours, soft light from the upper left, anatomically correct for the species, no cartoon faces, no blur.

Layout rules - follow them strictly:
- The whole background is one flat solid magenta colour (#FF00FF). No checkerboard, no texture, no ground, no grass under the animal, no cast shadow on the background.
- No cell borders, grid lines, frame numbers, titles or copyright lines.
- Each row is one animation in one direction. Write the row's name in small plain black capital letters at the left end of the row.
- Two directions only: "DOWN-RIGHT" (the animal heading toward the viewer and to the right, three-quarter view) and "UP-RIGHT" (heading away from the viewer and to the right). The game mirrors these for the left-hand directions.
- At least 30 pixels of empty magenta between every figure and between rows.
- Same scale in every frame (the size is given below, against a man 110 pixels tall). Feet on one baseline per row.
- Nothing cropped. Output as a PNG.
```

### ITEM HEADER

```
Create one sheet of inventory icons for a 2D game set in the North American fur trade in 1811, matching the attached reference in art style: detailed hand-painted pixel art in the 16-bit RPG manner, each object seen from slightly above at a three-quarter angle, crisp dark-brown 1-pixel outlines, muted historically accurate colours, soft light from the upper left. Every object must read clearly as a silhouette when shrunk to 64x64 pixels: bold shapes, no tiny fussy detail, no drop shadows.

Layout rules - follow them strictly:
- The whole background is one flat solid magenta colour (#FF00FF). No texture, no table, no cloth under the objects.
- The objects in a grid in exactly the order listed, left to right, top to bottom, each one centred in its own square space of about 160x160 pixels, filling most of it.
- Under each object, after a clear gap, its name in small plain black letters.
- No cell borders, grid lines, numbers, titles or copyright lines. At least 30 pixels of magenta between objects.
- Output as a PNG.
```

### PROP HEADER

```
Create one sheet of world objects for a 2D isometric game set in British North America in 1811, matching the attached reference in art style: detailed hand-painted pixel art in the 16-bit RPG manner, true 2:1 isometric projection (ground lines run at about 26.6 degrees, one ground tile is a diamond 128 pixels wide and 64 pixels tall), crisp dark 1-pixel outlines, weathered and historically plausible materials, soft light from the upper left so the left faces are lit and the right faces are in shade.

Layout rules - follow them strictly:
- The whole background is one flat solid magenta colour (#FF00FF). No ground drawn around the objects unless the object is ground (a field, a pond, a hearth pit).
- Each object stands alone with at least 40 pixels of magenta around it, its base sitting on an imaginary isometric ground diamond.
- Write each object's name in small plain black letters under it, after a clear gap.
- No borders, grid lines, numbers, titles or copyright lines. Nothing cropped. Output as a PNG.
```

### EFFECTS HEADER

For things that are not isometric objects, such as fire.

```
Create one sprite sheet of visual effects for a 2D hand-painted pixel art game in the 16-bit RPG manner, matching the attached reference in line quality and palette. Every effect is drawn on a flat solid magenta background (#FF00FF) with nothing else behind it - no ground, no characters, no borders, no grid lines, no text except the row label in small plain black capitals at the left end of each row. Each row is one animation, read left to right. At least 30 pixels of magenta between frames and between rows. Soft light from the upper left. No blur, no modern lens glow. Output as a PNG.
```

---

## Priority 1: the people of the towns

The five Lakes towns hold about 3,300 people (`World/Towns.cs`, placed by `tools/world/place_towns.py`), and none of them has a sheet of their own. `place_towns.py` dresses them in tinted borrowings (`look()`): a **soldier** is the Orkney labourer tinted red, an **officer** is the Factor, a **priest** is the clerk tinted grey, a **merchant** is the clerk, an **artisan** is a pale labourer, a **farmer** is the voyageur (French names) or the labourer, and **every town woman** is the country wife. `Appearance.For` keeps these looks for town roles (`Role.Farmer or Merchant or Artisan or Soldier or Officer or Priest => null`), so a finished sheet needs only a `*Frames.tres` and one line in each of `look()` and `Appearance.For`.

Use the CHARACTER HEADER. For every sheet attach `assets/external/pioneerpixel_hbc_trader/HBC Clerk.jpg` (style), `assets/sprites/factor/idle_front.png` (180×170 scale) and the old sheet it replaces. Unless the sheet says otherwise, the rows are:

```
Rows: IDLE FRONT, IDLE BACK, IDLE SIDE, WALK FRONT (4), WALK BACK (4), WALK SIDE (4), EAT (4), DRINK (4), TALK (4, gesturing with one hand), DEATH (4).
```

| # | Sheet | Replaces | Prompt (after the header and the rows) |
|---|---|---|---|
| 1.1 | `soldier_british` | red-tinted labourer | A private of the British 41st Regiment of Foot in 1812: a short red wool coatee with buff-white facings and lace, white crossbelts crossed over the chest with a brass belt plate, grey trousers, a black stovepipe "Belgic" shako with a brass plate and a white plume, black half-gaiters, a Brown Bess musket with bayonet. Add rows MUSKET (4: present, fire with a puff of smoke, recover), MARCH SIDE (4, the musket shouldered, a stiff drill step) and ATTACK (4, a bayonet thrust). |
| 1.2 | `soldier_american` | red-tinted labourer (the Americans' garrisons at Detroit and Fort Niagara) | A private of the United States infantry in 1812: a dark blue wool coatee with red facings and white lace, white crossbelts, white linen trousers, a black leather stovepipe cap with a brass plate and a white cord, a Springfield or Charleville-pattern musket with bayonet. Same extra rows as 1.1. |
| 1.3 | `officer_british` | the Factor | A British infantry officer in 1812: a scarlet long-tailed coat with gold lace and epaulette, a crimson silk sash round the waist, white breeches, tall black boots, a bicorne hat worn fore-and-aft, a sword at the hip and a spyglass. Rows as above plus WRITE (4, writing in an order book held in the left hand) and POINT (4, pointing with the drawn sword). |
| 1.4 | `officer_american` | the Factor | A United States infantry officer in 1812: a dark blue long-tailed coat with red facings and silver lace, a red sash, white breeches, black boots, a bicorne hat, a sword. Same rows as 1.3. |
| 1.5 | `priest` | grey-tinted clerk | A town clergyman in 1811, in plain black: a long black cassock (for the Catholic priest of Ste. Anne's at Detroit) with a white collar band, a wide-brimmed black hat, a breviary held to the chest. Add PRAY (4, standing, head bowed over the open book) and BLESS (4, raising the right hand). *(A second, Anglican variant in a black frock coat, white bands at the throat and knee breeches would serve York and Niagara; make it the same sheet's lower half if Gemini will.)* |
| 1.6 | `merchant` | the clerk | A prosperous town merchant of Detroit or York in 1811: a dark green or brown broadcloth tailcoat, a buff waistcoat, a white stock at the throat, pale breeches and white stockings, buckled shoes, a round beaver hat, spectacles, a ledger under the arm. Add WRITE (4, standing at a counter, quill to ledger) and COUNT (4, counting coins from one hand into the other). |
| 1.7 | `artisan` | pale-tinted labourer | A town blacksmith and tradesman in 1811: a leather apron to the shins over a linen shirt with the sleeves rolled up, brown wool waistcoat, knee breeches, wool stockings, heavy shoes, a red wool cap. Add HAMMER (4, beating hot iron on an anvil with a hand hammer; draw no anvil, the game has a prop), SAW (4, a two-handed cut on a trestle) and CARRY (4, a plank on one shoulder). |
| 1.8 | `habitant` | the voyageur (French-named farmers) | A French-Canadian habitant farmer of the Detroit River in 1811: a long hooded blanket capote of grey-white wool with a blue stripe, a woven red-and-blue arrow sash (ceinture fléchée) round the waist, a red wool tuque, deerskin leggings and moccasins (bottes sauvages), a clay pipe. Farm rows below. |
| 1.9 | `settler` | the labourer (English-named farmers) | A Loyalist or American settler farmer of Upper Canada in 1811: a homespun linen hunting frock or shirt, a brown wool waistcoat, linen trousers, heavy leather shoes, a battered round felt hat. Farm rows below. |
| 1.10 | `town_woman` | the country wife | A town woman of Upper Canada or Detroit in 1811: a high-waisted printed cotton gown in a faded blue or madder print, a white kerchief over the shoulders, a white linen apron, a white cap under a straw bonnet, a dark wool shawl. Add CARRY WATER (4, two wooden buckets on a yoke), CARRY BASKET (4, a basket of bread on the hip), KNEAD (4, at a table; draw no table) and SCRAPE replaced by SPIN (4, seated at a spinning wheel; draw no wheel). |
| 1.11 | `habitant_woman` | the country wife | A French-Canadian habitant woman in 1811: a short wool jacket (mantelet) over a striped petticoat to the ankle, a blue apron, a white linen cap, a bright kerchief, moccasins. Same rows as 1.10, plus BAKE (4, sliding loaves into an outdoor clay oven with a long wooden peel; draw no oven). |

**Farm rows** for 1.8 and 1.9 (they match the steps in `World/Fields.cs`: ploughing, sowing, hoeing, reaping, carrying the crop in, threshing):

```
PLOUGH SIDE (4, walking behind a plough, both hands on the handles; draw no plough and no ox), SOW SIDE (4, broadcasting seed from a linen bag slung on the hip, arm sweeping), HOE (4, chopping weeds with a long-handled hoe), REAP (4, a sickle in the right hand, a fistful of wheat stalks gathered in the left), BIND (4, stooping to tie a sheaf with a twist of straw), CARRY SHEAF (4, walking with a sheaf on the shoulder), THRESH (4, beating grain on a floor with a two-piece wooden flail swung overhead).
```

A chore step today is a `ChoreGoal.Work` (a `WaitGoal`: the farmer stands idle on the plot for the hours it takes), so these rows also need `Work` to take an animation name. That is a small code change.

**Town children.** HBC, Crown and American children already wear `child_boy` / `child_girl`. A habitant girl in a cap and short gown and a habitant boy in a tuque and sash would fill the towns better. Prompt them only after the adults.

---

## Priority 2: the peoples who still wear someone else's clothes

### 2.1 The women of every nation

All of the original nations' **men** are drawn. Their women are not: `ForNation(f, female: true)` returns `CreeWomanFrames` for every nation except the Métis, so a Blackfoot, Dakota, Dene, Plateau, Coast, Ojibwe, Missouri or Assiniboine woman currently wears a Cree woman's dress. Since 2026-10-21 the same is true of the Haudenosaunee, Wyandot, Sauk, Meskwaki, Ho-Chunk and Menominee.

Attach `assets/external/pioneerpixel_hbc_trader/Cree WOman.jpg`, `assets/sprites/cree_woman/idle_front.png` (for the 180×170 scale) and the matching man's `idle_front.png` (`assets/sprites/<nation>/idle_front.png`, so the dress and beadwork agree). Rows, matching `cree_woman` so `CreeWomanFrames.tres` can be copied and re-pathed:

```
Rows: IDLE FRONT, IDLE BACK, IDLE SIDE, WALK FRONT (4), WALK BACK (4), WALK SIDE (4), EAT (4), DRINK (4), CARRY WOOD (4, a bundle of firewood on her back held by a tumpline across the forehead), SCRAPE (4, kneeling, scraping a hide stretched on a frame with a bone tool), DEATH (4).
```

Then one sheet per nation, each dressed as that nation's man (see his sheet) but in a long hide or trade-cloth dress to mid-calf, with the nation's own beadwork or ornament, hair in two braids unless the nation's custom differs:

- **Blackfoot:** painted hide dress with an elk-tooth or bead yoke.
- **Dakota:** long hide dress with a deep beaded yoke.
- **Dene:** caribou-hide dress with quillwork bands.
- **Plateau:** plain hide dress and a woven spruce-root cap.
- **Coast:** cedar-bark cape, conical cedar hat and a woven robe.
- **Ojibwe:** trade-cloth dress with floral beadwork and a blue wool blanket.
- **Missouri / Mandan:** hide dress and a bone hair-pipe choker.
- **Assiniboine:** hide dress with a quilled yoke.

**Order:** Ojibwe, Assiniboine and Dakota first; they are the most numerous women on the map. Then the Haudenosaunee (2.2) for the towns, then the rest.

### 2.2 The new peoples of the Lakes and the Mississippi (men and women)

Added on 2026-10-21 (`cbfc52d`) as camps and factions: `Faction.Haudenosaunee` (the Six Nations of the Grand River, and the Wyandot), `Faction.SaukFox` (Sauk and Meskwaki), and the Ho-Chunk and Menominee camps (under the nearest faction). `ForNation` has no case for them, so **every one is drawn as a Cree hunter.** Those camps play lacrosse (2.3), trade at the towns and fight in the war of 1812. Of all the nations, they most need their own look.

The rows match `ojibwe` (so `OjibweFrames.tres` copies over): IDLE ×3, WALK ×3, ATTACK, SHOOT (bow), MUSKET, EAT, DRINK, DEATH. Attach `assets/external/NEW HBC ASSETS/ojibway man.jpg` and `assets/sprites/ojibwe/idle_front.png`.

| Sheet | Prompt (after the header and the rows) |
|---|---|
| `haudenosaunee` (man) | A Mohawk or Seneca man of the Grand River in 1812: a calico trade shirt in a small print worn loose to the thigh, a blue wool breechcloth, red wool leggings with ribbon-work, a woven sash, silver armbands and a silver gorget, a gustoweh (a fitted cap of splints with a single upright eagle feather and a few lying ones), moccasins with a puckered seam up the front, a trade musket and a ball-headed war club. |
| `haudenosaunee_woman` | A Haudenosaunee woman in 1812: a calico overdress over a dark blue wool skirt edged with white beadwork and ribbon-work, leggings, moccasins, silver brooches pinned down the front of the overdress, a blanket over one shoulder, hair in one long tied braid. |
| `sauk` (man) | A Sauk or Meskwaki warrior in 1811: the head shaved except for a scalplock carrying a red deer-hair roach, face painted with a red band, a blue wool breechcloth, leggings, a bear-claw necklace, a buffalo or trade blanket over one shoulder, a trade musket, a pipe tomahawk. |
| `sauk_woman` | A Sauk or Meskwaki woman in 1811: a wrap skirt of dark trade cloth with ribbon-work along the hem, a calico blouse, many silver brooches, leggings, moccasins, hair in one braid wrapped with a beaded tie. |
| `hochunk` (man) | A Ho-Chunk man in 1811: a calico shirt, a beaded otter-skin turban, a bandolier bag across the chest, blue wool leggings with ribbon-work, a trade musket. *(Optional; the camps can borrow `sauk` until this exists.)* |

The Wyandot and Menominee can wear the Haudenosaunee and Ojibwe sheets for now; that is a code line, not art.

### 2.3 Lacrosse rows

`Economy/Lacrosse.cs` plays a summer game between neighbouring camps of the Ojibwe, Haudenosaunee, Sauk and Meskwaki, Dakota, Ho-Chunk and Menominee: two camps' young men on an open field with both camps standing round. Today the players run with nothing in their hands. Three extra rows on the men's sheets of those nations would carry it. Prompt them **as their own sheet** of the same man so the existing frames are not redrawn:

```
The same man as the attached sheet, stripped for the ball game: breechcloth and moccasins only, body painted, hair tied up, carrying a lacrosse stick (a long hickory stick bent into a hook at one end, the small round pocket strung with rawhide; for the Haudenosaunee the stick is longer with a wider hooked net). Rows: RUN STICK SIDE (6 frames, running fast, the stick held across the body, a small deerskin ball in the pocket), THROW SIDE (4 frames, slinging the ball forward overhand from the stick), SCOOP (4 frames, stooping to scoop a ball off the ground), and a single frame of the BALL alone, about 6 pixels across.
```

Do the Ojibwe and Haudenosaunee first.

---

## Priority 3: the buildings of the towns

Every town building is drawn with the York Factory **whitewashed log** walls and the Kenney furniture (`StructurePainter.cs`, `BuildingKind.Townhouse / Church / Barracks / Store / Workshop / Hall / Cabin / House`). So Detroit's church is a log hut furnished with dining tables for pews, and a merchant's frame house is a fur-trade post. The walls fit onto the old Kenney tile edges by `tools/sprites/walls.py`, so a new set needs the same pieces as `Facotry Segment Sprites.jpg`.

### 3.1 Wall sets

PROP HEADER. Attach `assets/external/NEW HBC ASSETS/Facotry Segment Sprites.jpg` and `assets/tiles/buildings/wall_N.png`, `wall_window_N.png`, `wall_door_N.png`, `wall_corner_front.png`. Ask for each set as the factory sheet laid them out:

```
One isometric wall set, every piece one tile wide and drawn to fit exactly the same footprint and height as the attached wall pieces: PLAIN WALL facing left, PLAIN WALL facing right, WALL WITH WINDOW (each facing), WALL WITH DOOR CLOSED (each facing), WALL WITH DOOR OPEN (each facing), FRONT CORNER, BACK CORNER. The material is:
```

- **3.1a Clapboard frame (`House`, `Store`, `Hall`, `Townhouse`):** white-painted horizontal clapboard on a timber frame, sash windows of twelve small panes with dark green shutters, a six-panel door with a fanlight over it, a fieldstone foundation showing at the base.
- **3.1b Squared log (`Cabin`, the farms):** dark weathered squared logs, dovetailed at the corners, chinked with pale clay, small windows of four panes or oiled paper, a plank door. *(The York sheet's "weathered log set", drawn but not cut, may already be this. Check it first: `asset-inventory.md` §4.)*
- **3.1c Stone (`Church`, the garrison's magazine, Fort Niagara's "French Castle"):** rough-coursed grey limestone with dressed quoins at the corners, tall round-headed windows, a heavy round-headed plank door.
- **3.1d Barracks (`Barracks`):** long two-storey squared-timber barracks, whitewashed, with a covered verandah gallery along the front.

### 3.2 Roofs and landmarks

PROP HEADER. Attach `assets/tiles/buildings/roof.png` and `assets/tiles/props/flagstaff.png`.

```
Roof tiles to cap the attached wall sets, each a full isometric tile: a PITCHED ROOF of cedar shingles (ridge, left slope, right slope, gable end facing left, gable end facing right), the same in dark weathered shingles, a BELL-CAST HABITANT ROOF with flared eaves. Then single objects: a CHURCH STEEPLE (a white wooden bell tower and spire with a cross, about 260 pixels tall, to stand on a gable end), a BRICK CHIMNEY STACK, a STONE CHIMNEY STACK.
```

### 3.3 The town and the farm

PROP HEADER. Attach `assets/tiles/props/garden_plot.png`, `garden_harvested.png` and `hay_bales.png`. These are what the farmer's year (`World/Fields.cs`, `PlotStage { Stubble, Ploughed, Sown, Growing, Ripe, Stooked }`) and the town plan (`StructurePainter` §"A town") draw on. Today every field is a Missouri garden plot.

```
Ground tiles for a farm field, each one full isometric ground diamond 128 x 64 pixels, drawn as flat ground seen from above: WHEAT STUBBLE (pale cut stalks in rows), PLOUGHED (dark turned furrows running diagonally), SOWN (furrows harrowed smooth, a few green specks), YOUNG WHEAT (short green rows), RIPE WHEAT (tall gold wheat, standing above the tile by about 30 pixels), STOOKED (stubble with three upright sheaves leaned together in a stook), CORN (Indian corn standing tall), POTATOES (low rows of dark green plants).
Then objects: a SNAKE RAIL FENCE (split rails stacked in a zigzag, one straight piece facing left, one facing right, one corner), a HAYSTACK (a tall round stack with a pole up the middle), a LOG BARN (a single object, about three tiles long, with a big double door), an OUTDOOR BREAD OVEN (a habitant clay dome oven on a timber platform with a little shingle roof over it), a WELL WITH A SWEEP (a long counterweighted pole on a forked post), a WINDMILL (a Detroit River stone tower windmill with four lattice sails, about 300 pixels tall), a WOODEN PLOUGH, a HARROW, an OX-CART (two solid wheels), a ROOT CELLAR DOOR (two plank doors in a mound of earth).
```

### 3.4 Furniture and fittings

PROP HEADER. Attach `assets/tiles/furniture/table_chairs.png` and `assets/tiles/props/stove.png`. Each fills one floor tile.

```
Interior furniture for 1811 town buildings: a CHURCH PEW (a plain wooden bench with a back, seen from behind, facing left), the same FACING RIGHT, an ALTAR (a draped table with two candlesticks and a cross), a PULPIT (a raised wooden box with steps), a SHOP COUNTER (a long plank counter with a set of brass scales on it), SHELVES OF BOLTS OF CLOTH, BARRELS (three stacked), a BLACKSMITH'S FORGE (a brick hearth with a leather bellows and glowing coals), an ANVIL on a stump, a CARPENTER'S WORKBENCH with tools, a SPINNING WHEEL, a WEAVING LOOM, a GUN RACK of muskets, a SOLDIER'S COT (narrow, with a grey blanket), a LONG COUNCIL TABLE with benches, a TALL CASE CLOCK, a CRADLE.
```

### 3.5 The garrisons and the war

PROP HEADER. Attach `assets/tiles/props/cannon.png` (cut, unwired) and `assets/tiles/buildings/fence_N.png`.

```
Objects for the forts of the Lakes frontier in 1812: a FIELD GUN on a two-wheeled carriage, facing left; the same facing right; a GARRISON GUN on a wooden truck carriage behind an earthen embrasure; a PYRAMID OF ROUND SHOT; a POWDER BARREL; a WOODEN BLOCKHOUSE (two storeys, the upper one overhanging, loopholes, about two tiles square); an EARTHWORK RAMPART piece (a sloped grassy bank faced with sod, one straight piece each facing and a corner); a SENTRY BOX; FLAGSTAFF WITH THE UNION FLAG (the 1801 Union flag); FLAGSTAFF WITH THE AMERICAN FLAG (fifteen stars and fifteen stripes); a BURNT-OUT HOUSE (charred timbers and a standing chimney, for Newark and York after they were burned).
```

---

## Priority 4: the animals the code has but draws wrong

### 4.1 The buffalo

`Species.Buffalo` is hunted, run and robed on the plains (18 herds), and **it is still drawn as a caribou** (`Species.cs` points it at `CaribouFrames.tres`). The robe's icon is the caribou hide's.

Attach a finished frame from `assets/sprites/animals/caribou/` and `Cree man with bow.jpg`. ANIMAL HEADER, then:

```
A sprite sheet of a North American plains buffalo (bison) in 1811, in the same isometric three-quarter view and pixel style as the reference, drawn large: a bull stands six feet at the shoulder and is roughly one and a half times the height and three times the bulk of the 110-pixel man in the reference. Dark brown woolly cape over the humped shoulders and forelegs, shorter dark coat behind, a heavy bearded head carried low, short black upcurved horns, a tufted tail. Rows, each labelled, each in DOWN-RIGHT and UP-RIGHT: IDLE (4 frames, standing, head low, grazing and lifting the head); WALK (6 frames, a heavy slow plod); RUN (6 frames, a full gallop with the hump pitching); CHARGE (4 frames, head down, horns forward); DEATH (5 frames, from hit, to staggering, to down on the side); and three single frames beside them: A COW, lighter and smaller with no shoulder cape; A CALF, reddish-brown; and a BUFFALO ROBE, a dressed winter hide with the hair on, folded the way a trade robe was folded, as an inventory icon.
```

### 4.2 The elk

New on 2026-10-21: `Species.Elk` is the big game of the southern woods and parklands, hunted by the Lakes and Mississippi camps, and **it is drawn as a caribou** (`Species.cs:48`). Same references as the buffalo. ANIMAL HEADER, then:

```
A sprite sheet of an eastern elk (wapiti) bull in 1811, in the same isometric view and pixel style: bigger than a caribou, about the height of the 110-pixel man at the shoulder, a red-brown summer coat with a dark brown neck mane and a pale straw-coloured rump patch, huge swept-back many-tined antlers. Rows, each in DOWN-RIGHT and UP-RIGHT: IDLE (4, grazing and lifting the head), WALK (6), RUN (6, a bounding trot), DEATH (5). Then COW IDLE and COW WALK (4 each, no antlers, smaller, paler) and a single frame of a BULL BUGLING (head up, mouth open).
```

### 4.3 The horse, the travois and the Red River cart

Horses are ridden, raided, traded and herded (`Economy/Herd.cs`, `Economy/Plains.cs`, `Agents/Mount.cs`). They are drawn by a **stand-in** that `tools/sprites/make_horse_standin.py` generates: a plain grey pony tinted to the coat, with the rider's walking figure lifted onto its back. `Mount.cs` loads `Scenes/Animals/HorseFrames.tres` the moment it exists, with the rows `idle_*`, `walk_*`, `run_*` and `graze_*` in `se`/`ne`/`sw`/`nw`.

Attach a finished frame from `assets/sprites/animals/caribou/`, `Cree man with bow.jpg` and `assets/sprites/blackfoot/idle_front.png` for scale. ANIMAL HEADER, then one sheet each:

**The horse, bare and saddled** (the main sheet):

```
A sprite sheet of an Indian pony of the northern plains in 1811, in the same isometric three-quarter view and pixel style as the reference: a small, tough, short-backed horse about fourteen hands, its back level with the chest of the 110-pixel man in the reference, a heavy mane and tail, unshod. Draw it with a pad saddle of buckskin stuffed with buffalo hair and a single rawhide rein looped round the lower jaw - the way the plains peoples rode - and nothing else. Rows, each labelled, each in two directions DOWN-RIGHT and UP-RIGHT: IDLE (4 frames, standing, ear and tail flicks); GRAZE (4 frames, head down cropping grass, lifting it once); WALK (6 frames); RUN (6 frames, full gallop, mane and tail streaming); DEATH (5 frames, going down on its side). Then the same IDLE row once more in each of four coats so the herd is not all one horse: a BAY, a GREY, a BLACK-AND-WHITE PAINT and a BUCKSKIN DUN.
```

**The horse ridden** (so the rider sits in the saddle instead of standing on the horse's back):

```
The same pony as the first sheet, now ridden by a plains hunter in breechcloth, leggings and a buffalo-hide shirt, his legs down either side of the pad saddle, a bow cased at his back, holding the rein in his left hand. Rows, each labelled, each DOWN-RIGHT and UP-RIGHT: RIDE IDLE (4 frames, horse standing, rider looking about); RIDE WALK (6 frames); RIDE GALLOP (6 frames, rider leaning forward); RIDE SHOOT (5 frames, at a full gallop, drawing and loosing an arrow to the right side, as in the buffalo run); and a single frame of the same pony with an empty pad saddle and the rein trailing, standing PICKETED to a stake.
```

**The town horse** (the towns and garrisons, officers and farmers; optional, after the pony):

```
A heavier farm and saddle horse of Upper Canada in 1812, sixteen hands, a bay with a black mane, shod, with a leather English saddle and bridle. The same rows as the pony sheet, and a single frame of the same horse in collar and harness for a cart.
```

**The travois and the Red River cart** (PROP HEADER, not the animal header):

```
Two sheets of the plains' carrying in 1811. A HORSE TRAVOIS: two long lodgepoles crossed over a pony's withers and dragging behind on the ground, a platform of netted rawhide lashed across them loaded with rolled lodge covers and parfleches; draw it hitched to the pony (WALK, 6 frames, DOWN-RIGHT and UP-RIGHT) and unhitched, lying on the ground. A RED RIVER CART: an all-wood two-wheeled Métis cart, the wheels taller than a man (about 120 pixels), dished and with twelve spokes, no iron anywhere, rawhide lashings, the box loaded with pemmican bags of buffalo hide; draw it drawn by a single ox in harness (WALK, 6 frames, DOWN-RIGHT and UP-RIGHT), standing unhitched, and as an icon-sized PEMMICAN BAG (a taut rectangular rawhide bag sewn shut, ninety pounds) for the inventory.
```

Wiring note: the ridden sheet replaces both the horse and the rider while mounted, so `Mount.cs` will want a second `SpriteFrames` (`HorseRiddenFrames.tres`) and to hide the rider's own sprite when it is loaded. The plain horse sheet needs nothing but the `.tres`.

---

## Priority 5: item icons the code borrows

ITEM HEADER. Attach `assets/external/NEW HBC ASSETS/Food Items.jpg` and `assets/sprites/items/corn.png` (64×64 scale). These `ItemId`s exist and are bought, sold, eaten and carried, but they wear another item's picture (`Items/Items.cs`):

| `ItemId` | Borrows | Draw |
|---|---|---|
| `Coin` | `mb_token.png` | A small heap of coin: a silver Spanish dollar, a few English shillings and copper halfpennies |
| `Roots` | `corn.png` | Potatoes, turnips and a cabbage in a split-ash basket |
| `Wheat` | `corn.png` | A sheaf of wheat bound with a twist of straw |
| `Grain` | `pack_basket.png` | A tied linen sack of threshed wheat, a few grains spilled |
| `Flour` | `pemmican_bag.png` | A wooden barrel of meal, the lid off, white flour heaped in it |
| `BuffaloRobe` | `caribou_hide.png` | (on the buffalo sheet, 4.1) |
| `Ptarmigan` | the bird's own death frame | A plucked-ready ptarmigan, white in winter plumage, hung by the feet |

```
Icons in this order: A HEAP OF COIN, ROOTS IN A BASKET, A SHEAF OF WHEAT, A SACK OF GRAIN, A BARREL OF MEAL, A PTARMIGAN.
```

---

## Priority 6: the interface, what's still missing

The panels, buttons, tabs, scrollbars and cursors are drawn and wired (`acd6e17`), and the book is on parchment (`3eed148`). Still missing:

### 6.1 Fonts (not a Gemini job)

There is still **no font file in the repo**; every panel uses the engine default, and seven places call `ThemeDB.FallbackFont` (`MapPanel`, `ObserverView`, `TravelMode`, `Wayfinding`, `NpcSpawn`, `PostSign`, `WildlifeSite`). Pick two free, OFL-licensed faces and drop them in `assets/ui/fonts/`:

- **A display face** for headings, the menu and signs: *IM FELL English* or *EB Garamond*.
- **A body face** for every panel, journal entry and label, readable at 14 px: *Alegreya* or *Source Serif*. Avoid a pixel font; the art is hand-painted, not 8-bit.

Then set them in `UI/MenuTheme.cs` and replace the seven `ThemeDB.FallbackFont` call sites.

### 6.2 Portraits for the conversation window (new)

The talk (`UI/Conversation*.cs`) now has depth: getting acquainted, reading people, passing the time, talking them round. It still shows only names. A head-and-shoulders portrait per sheet would give it a face. Prompt (CHARACTER HEADER's style lines only, not its layout rules):

```
A sheet of head-and-shoulders portraits, each 128 x 128 pixels, in the style of the attached sheet but seen straight on at eye level, each framed in a thin dark oval, on flat magenta, labelled underneath: [list the sheets, e.g. FACTOR, CLERK, VOYAGEUR, ORKNEY LABOURER, CREE HUNTER, CREE WOMAN, ...]. Same face, colours and clothing as each person's sprite in the attached sheets.
```

Attach two or three of the full sheets per batch. Do the people you talk to most (the post's officers and the trading captains) first.

### 6.3 Crests for the new polities (low)

The four existing crests (`assets/ui/crests/`: HBC, NWC, Cree, Assiniboine) are still unwired. If they get a home (the journal's standing page, the map), the rest will be wanted: the Crown (royal arms), the United States (the eagle), the American and Pacific Fur Companies, and a sign for each nation. Attach `Faction Symbols.jpg`.

---

## Priority 7: effects still to draw

**Redo: LARGE FIRE row.** A burning building (`Blaze.cs`) is still the campfire tile tinted and stretched, and the war now burns whole towns (York in April 1813, Newark in December). In `fire.jpg` the large fire sits on a baked-in stone wall instead of magenta, so it cannot be cut. Use the EFFECTS HEADER, attach `assets/sprites/fx/campfire_0.png` and `NEW HBC ASSETS/fire.jpg`:

```
One row, LARGE FIRE (6 frames, each about 128 pixels wide and 160 tall): the same fire grown to engulf a log wall, flames leaning left, looping. The flames only, drawn on the flat magenta background: no wall, no stones, no ground behind or under them. A second row, BURNING ROOF (6 frames, 128 wide by 96 tall), flames running along a roof ridge with heavy dark smoke rising.
```

**New: cannon and volley.** The war brings artillery and line volleys; `CombatFx` has a musket muzzle flash only. EFFECTS HEADER, attach `assets/sprites/fx/muzzle_flash_0.png` and `smoke_0.png`:

```
Rows: CANNON BLAST (5 frames, a big flash and a rolling white cloud from the muzzle of a field gun, pointing right), ROUND SHOT IMPACT (5 frames, earth and splinters thrown up), VOLLEY SMOKE (6 frames, a long low bank of white powder smoke drifting right from a line of muskets, about 300 pixels wide).
```

---

## Priority 8: animals the code does not have yet

Do these only once `Ecology/Species.cs` grows past its eight species (caribou, goose, beaver, wolf, hare, ptarmigan, elk, buffalo; the horse is in `Economy/Herd.cs`). There is no point drawing an animal the simulation cannot spawn.

Moose, black bear, polar bear (it walks the shore at York Factory in autumn), white-tailed deer (the Lakes towns' venison), red fox, arctic fox, marten, mink, muskrat, otter, lynx, wolverine, pronghorn (plains), sled dogs, and the towns' livestock (ox, cow, pig, chickens) once the farms keep them. ANIMAL HEADER, the same row set as the wolf (IDLE, WALK, RUN, ATTACK if it fights, DEATH), and give its size against a 110-pixel man. Attach `Cree man with bow.jpg` and a finished frame from `assets/sprites/animals/wolf/`.

---

## Priority 9: boats of the new country (low)

The goods now come onto the map at its edges (`Economy/Outfits.cs`): the Missouri keelboats to Fort Manuel Lisa, the Pacific Fur Company's ship to Astoria, the Montreal canoes up the Ottawa. On the map they are drawn only as the brigades' `canoe_loaded` and `york_boat` (`Data/World/brigade_runs.json`, `ConvoyView.cs`). PROP HEADER, attach `assets/sprites/vehicles/york_boat.png`:

```
Boats seen from above at the isometric angle, each pointing DOWN-RIGHT and again UP-RIGHT, empty of crew: a MISSOURI KEELBOAT (a long narrow cargo boat about 60 feet, a covered cargo box amidships, a single mast, a long steering oar at the stern); a MACKINAW BOAT (a flat-bottomed, pointed-ended Great Lakes freight boat with a square sail); a DURHAM BOAT (a long flat-bottomed St. Lawrence freight boat poled by men walking its side-boards); a LAKE SCHOONER (a two-masted armed schooner of the Lake Erie squadron, 1813, sails set).
```

---

## Summary checklist

| # | Asset | Why | Attach |
|---|---|---|---|
| 1.1-1.4 | **British and American soldiers and officers** | 683 soldiers and 38 officers in the garrisons wear a red-tinted labourer or the Factor | `HBC Clerk.jpg`, `factor/idle_front.png` |
| 1.5-1.7 | **Priest, merchant, artisan** (4, 19 and 53 in the towns) | the towns' heart is clerks and labourers in tint | same |
| 1.8-1.9 | **Habitant and settler farmers** (333), with the farm rows | the farmer's year is simulated step by step with no work animation | same + `voyageur/idle_front.png` |
| 1.10-1.11 | **Town woman and habitant woman** | every town woman is the country wife | same + `country_wife/idle_front.png` |
| 2.1 | The women of every nation | all of them wear the Cree woman's sheet | `Cree WOman.jpg`, `cree_woman/idle_front.png`, the man's frame |
| 2.2 | **Haudenosaunee, Sauk/Meskwaki (men and women), Ho-Chunk** | new peoples drawn as Cree hunters | `ojibway man.jpg`, `ojibwe/idle_front.png` |
| 2.3 | Lacrosse rows | the ball game is played with empty hands | the nation's sheet |
| 3.1 | Clapboard, squared-log, stone and barracks wall sets | every town building is a whitewashed fur-trade log hut | `Facotry Segment Sprites.jpg`, `buildings/wall_*.png` |
| 3.2 | Shingled roofs, steeple, chimneys | | `buildings/roof.png` |
| 3.3 | Field stages, rail fence, barn, bread oven, windmill, well | fields are Missouri garden plots | `props/garden_plot.png` |
| 3.4 | Town furniture (pews, altar, counter, forge, anvil, loom...) | pews are dining tables | `furniture/table_chairs.png` |
| 3.5 | Field guns, blockhouse, earthworks, flags, burnt house | the war of 1812 on the Lakes | `props/cannon.png` |
| 4.1 | **Buffalo** | drawn as a caribou | a caribou frame + `Cree man with bow.jpg` |
| 4.2 | **Elk** | drawn as a caribou | same |
| 4.3 | **Horse** (pony, ridden, town horse), travois, Red River cart | a generated grey stand-in | same + a `blackfoot/` frame |
| 5 | Coin, roots, wheat, grain, meal, ptarmigan icons | borrowed icons | `Food Items.jpg`, `items/corn.png` |
| 6.1 | Two licensed fonts | no font in the repo | *not a Gemini job* |
| 6.2 | Conversation portraits | the talk has no faces | the full sheets |
| 6.3 | Crests for the Crown, the US and the new companies | only once crests are wired | `Faction Symbols.jpg` |
| 7 | LARGE FIRE redone on magenta, burning roof, cannon blast, volley smoke | burning towns and artillery | `fx/campfire_0.png`, `fire.jpg`, `fx/muzzle_flash_0.png` |
| 8 | Moose, bears, deer, foxes, marten, lynx, sled dogs, livestock | only when `Species.cs` grows | `Cree man with bow.jpg` + a wolf frame |
| 9 | Keelboat, Mackinaw boat, Durham boat, lake schooner | low: goods arrive at the map's edges unseen | `vehicles/york_boat.png` |

**Not covered here: ground tiles.** The terrain comes from a streamed world map; the regional grounds (now including `scree`, `alpine` and `snowfield` for the mountains, `fe645f8`) are still recolours of the Kenney grass (`tools/world/make_region_tiles.py`). Seamless isometric tiles are the one thing Gemini reliably gets wrong, so they wait until the tile shapes have settled.

**Also not here: anything already drawn.** `asset-inventory.md` catalogues the characters, animals, items, props, vehicles, UI and effects, including the finished files the game does not yet load.
