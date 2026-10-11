# Asset Prompts for Gemini

What art the game still needs, as prompts ready to paste into Gemini, with the existing sheet to attach as a style reference for each. Written 2026-10-10 against the code as it stands (roles in `Agents/Npc.cs`, species in `Ecology/Species.cs`, items in `Items/Items.cs`); see `asset-inventory.md` for where the current art came from.

---

## How to use this

1. **Open a fresh Gemini chat per sheet.** Old sheets in the same chat bleed into new ones.
2. **Attach the reference image(s) listed for that asset.** All paths are in this repo. The `assets/external/pioneerpixel_hbc_trader/*.jpg` sheets are the earlier Gemini sheets this game's characters were cut from; they fix the style. Attaching a single cut frame (e.g. `assets/sprites/player/idle_front.png`) as well fixes the scale.
3. **Paste the matching HEADER block first** (Character, Animal, Item, or Prop), **then the asset's prompt** underneath it, as one message.
4. If a sheet comes back with the wrong layout (checkerboard, parchment, grid lines, numbers in cells), reply: *"Same sheet again, but on a flat solid magenta #FF00FF background with no borders, numbers or text except the row labels."*
5. Drop the result in `assets/external/pioneerpixel_hbc_trader/` (or a new folder for animals/items) with a clear name and tell me; I'll slice it into frames and wire the animations.

**Why magenta:** the slicer finds sprites by cutting along runs of background colour. Checkerboards, parchment and cell borders all have to be cleaned by hand; one flat colour that nothing in 1811 is dyed cuts cleanly.

**Animation names the code plays** (so a sheet that has these rows can be dropped straight in): `idle`, `walk` (toward viewer), `walk_back`, `walk_side` (faces right, mirrored for left), `eat`, `drink`, `attack` (melee), `shoot` (musket or bow), `death`, `sneak` / `sneak_back` / `sneak_side`. Animals: `idle_se`, `walk_se`, `run_se`, `attack_se`, `death_se` and the same for `ne` (`sw`/`nw` are mirrored).

---

## Headers (paste one of these first)

### CHARACTER HEADER

```
Create one sprite sheet image for a 2D top-down game set at York Factory, Hudson Bay, in 1811, matching the attached reference sheet exactly in art style: detailed hand-painted pixel art in the 16-bit RPG manner, seen from a three-quarter top-down view (the camera looks down at the figure at about 30 degrees, as in an isometric game), crisp dark-brown 1-pixel outlines, a muted and historically plausible palette (indigo and navy wool, madder red, undyed linen, smoke-tanned buckskin, spruce green, iron grey), soft light from the upper left, no blur, no glow, no painterly smearing.

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
Create one sprite sheet image for a 2D top-down game set on the Hudson Bay lowlands in 1811, matching the attached reference sheet exactly in art style: detailed hand-painted pixel art in the 16-bit RPG manner, three-quarter top-down view (camera looking down at about 30 degrees, as in an isometric game), crisp dark 1-pixel outlines, naturalistic muted colours, soft light from the upper left, anatomically correct for the species, no cartoon faces, no blur.

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
Create one sheet of inventory icons for a 2D game set in the Hudson's Bay Company fur trade, York Factory, 1811, matching the attached reference in art style: detailed hand-painted pixel art in the 16-bit RPG manner, each object seen from slightly above at a three-quarter angle, crisp dark-brown 1-pixel outlines, muted historically accurate colours, soft light from the upper left. Every object must read clearly as a silhouette when shrunk to 64x64 pixels: bold shapes, no tiny fussy detail, no drop shadows.

Layout rules - follow them strictly:
- The whole background is one flat solid magenta colour (#FF00FF). No texture, no table, no cloth under the objects.
- The objects in a grid in exactly the order listed, left to right, top to bottom, each one centred in its own square space of about 160x160 pixels, filling most of it.
- Under each object, after a clear gap, its name in small plain black letters.
- No cell borders, grid lines, numbers, titles or copyright lines. At least 30 pixels of magenta between objects.
- Output as a PNG.
```

### PROP HEADER

```
Create one sheet of world objects for a 2D isometric game set at York Factory, Hudson Bay, 1811, matching the attached reference in art style: detailed hand-painted pixel art in the 16-bit RPG manner, true 2:1 isometric projection (ground lines run at about 26.6 degrees, one ground tile is a diamond 128 pixels wide and 64 pixels tall), crisp dark 1-pixel outlines, weathered and historically plausible materials, soft light from the upper left so the left faces are lit and the right faces are in shade.

Layout rules - follow them strictly:
- The whole background is one flat solid magenta colour (#FF00FF). No ground drawn around the objects unless the object is ground (a pond, a hearth pit).
- Each object stands alone with at least 40 pixels of magenta around it, its base sitting on an imaginary isometric ground diamond.
- Write each object's name in small plain black letters under it, after a clear gap.
- No borders, grid lines, numbers, titles or copyright lines. Nothing cropped. Output as a PNG.
```

---

## Priority 1: people the player meets every day who are still stand-ins

### 1.1 Voyageur (North West Company canoeman)

- **Replaces:** the NWC voyageurs, who are currently the HBC factor's sprite tinted pink (`Population.cs`, `Role.Voyageur`).
- **Attach:** `assets/external/pioneerpixel_hbc_trader/Man WALking.jpg` (layout and scale) and `assets/sprites/rival/idle_front.png` (scale).
- **Two sheets.** Paste the CHARACTER HEADER, then:

**Sheet A: movement: DONE**
```
The character: a French-Canadian voyageur of the North West Company, about 30, short (a head shorter than the HBC men) but very broad-shouldered and thick-armed from paddling, bow-legged, weather-darkened skin, black moustache, black hair to the collar. He wears a red knitted wool tuque with a tassel falling to one side; a loose blue-and-white checked cotton shirt open at the throat with sleeves pushed up; a long woven L'Assomption sash (ceinture flechee) in red, blue and yellow arrow patterns wound twice round his waist with the fringed ends hanging at his left hip; a short dark-blue wool breechclout over tight leggings of red stroud cloth gartered below the knee with beaded garters; smoke-tanned moccasins. A beaded black fire bag (tobacco pouch) hangs from the sash on his right hip, a knife in a sheath tucked in the sash in front, a short white clay pipe clenched in his teeth in every frame. He moves with a quick, short, rolling stride.

Rows:
1. IDLE - three frames: front, back, side.
2. WALK FRONT - 4 frames.
3. WALK BACK - 4 frames.
4. WALK SIDE - 4 frames, facing right.
5. DEATH - 4 frames, seen from the front: struck and staggering, dropping to his knees, falling forward, lying face down on the ground (last frame lying flat, seen from above at the same three-quarter angle).
```

**Sheet B: work and fighting: DONE**
```
The same voyageur as in the attached sheet, identical in every detail.

Rows:
1. PORTAGE SIDE - 4 frames facing right: trotting bent forward under two 90-pound bales (one rectangular canvas-wrapped bale stacked on another) carried high on his back, held by a leather tumpline strap across his forehead, hands holding the strap at his temples.
2. PORTAGE FRONT - 4 frames of the same, walking toward the viewer.
3. PADDLE SIDE - 4 frames facing right: kneeling (as if in a canoe, but draw no canoe), driving a narrow red-painted spruce paddle with a short pointed blade through a stroke: catch, pull, exit, recovery.
4. EAT - 4 frames, front: tearing a piece off a slab of pemmican with his teeth and chewing.
5. DRINK - 4 frames, front: drinking from a tin cup, head tipping back.
6. ATTACK SIDE - 4 frames facing right: a brawler's swing with a heavy fist, wind-up, punch, follow-through, recover.
```

### 1.2 North West Company wintering partner (the bourgeois)

- **Replaces:** the NWC partner across the river, currently the factor's sprite (`Role.NwcPartner`).
- **Attach:** `assets/external/pioneerpixel_hbc_trader/grey_trader_8way.jpg` and `assets/sprites/rival/idle_front.png`.
- CHARACTER HEADER, then:

```
The character: a Scots Highland partner of the North West Company wintering in the north, about 45, tall, upright, heavy-set and used to being obeyed; red-brown side-whiskers greying at the edges, clean chin, hard pale eyes. He wears a tall black beaver-felt hat with a narrow curled brim; a long bottle-green wool greatcoat with a high turned-down collar and two rows of brass buttons, worn open over a buff waistcoat, a white linen shirt and a black silk stock at the throat; a red L'Assomption sash knotted round the greatcoat at the waist; dark grey trousers tucked into tall black leather boots. A flintlock pistol is tucked in the sash. He carries nothing in his hands except as stated below; he walks slowly, chest out.

Rows:
1. IDLE - three frames: front, back, side (side facing right). In the front idle he stands with his hands clasped behind his back.
2. WALK FRONT - 4 frames.
3. WALK BACK - 4 frames.
4. WALK SIDE - 4 frames facing right.
5. TALK FRONT - 4 frames: standing, one hand raised palm-out in a commanding gesture, then pointing, then both hands on the sash, then back.
6. EAT - 4 frames, front: eating from a pewter plate held in one hand with a fork.
7. DRINK - 4 frames, front: drinking from a small glass of spirits.
8. SHOOT SIDE - 4 frames facing right: drawing the pistol from the sash, levelling it at arm's length, firing with a small puff of grey smoke at the muzzle, lowering it.
9. DEATH - 4 frames from the front: hit, staggering back, falling, lying on his back with the hat fallen beside him.
```

### 1.3 HBC woman / country wife:DONE

- **Replaces:** every woman at the fort (`Role.Woman`, `Role.Wife`), who are currently the player's man sprite tinted pink and shrunk.
- **Attach:** `assets/external/pioneerpixel_hbc_trader/Cree WOman.jpg` (style and scale for a woman) and `assets/sprites/cree_woman/idle_front.png`.
- These are the fort's own families: Cree and Orkney-Cree women married to the Company's men "after the custom of the country".
- **Two sheets.** CHARACTER HEADER, then:

**Sheet A: movement: DONE**
```
The character: a woman of mixed Cree and Orkney descent, about 30, married to a Company servant at York Factory; a broad calm face, brown skin, black hair parted in the middle in two long braids wrapped at the ends with red cloth. She wears a mid-calf gown of dark-blue printed cotton with a small white flower print, long sleeves and a plain white collar; under it, leggings of dark-blue stroud cloth edged with a band of white and red beadwork; smoke-tanned moccasins with floral quillwork on the toe; a red-and-black tartan wool shawl round her shoulders and crossed over her chest; a small beaded bag hanging at her waist from a woven belt. She is about 95 pixels tall at the stated scale (shorter than the men). She walks with short, steady steps.

Rows:
1. IDLE - three frames: front, back, side (facing right).
2. WALK FRONT - 4 frames.
3. WALK BACK - 4 frames.
4. WALK SIDE - 4 frames facing right.
5. DEATH - 4 frames from the front: faltering, sinking to her knees, falling to her side, lying still with the shawl spread.
```

**Sheet B: work**
```
The same woman as in the attached sheet, identical in every detail.

Rows:
1. CARRY WOOD SIDE - 4 frames facing right: walking with a bundle of split firewood tied with a cord, carried on her back by a tumpline across her forehead.
2. CARRY WATER FRONT - 4 frames: walking toward the viewer carrying a copper kettle full of water in each hand, arms straight, body upright.
3. SCRAPE HIDE - 4 frames, three-quarter front: kneeling over a stretched caribou hide pegged out flat on the ground, working a bone flesher with both hands in long downward strokes.
4. COOK - 4 frames, front: kneeling and stirring a copper kettle (draw the kettle on a small bed of glowing coals) with a long wooden spoon.
5. EAT - 4 frames, front: eating from a wooden bowl with a horn spoon.
6. DRINK - 4 frames, front: drinking tea from a tin cup held in both hands.
7. CARRY BABY FRONT - 4 frames walking toward the viewer with an infant laced in a moss bag on a wooden cradleboard (tikinagan) carried on her back, the baby's face visible over her shoulder.
```

### 1.4 Sailor (crew of the annual supply ship): DONE

- **Replaces:** the ship's crew (`Role.Sailor`), currently the player tinted blue-grey.
- **Attach:** `assets/external/pioneerpixel_hbc_trader/source_sheet.jpg` and `assets/sprites/player/idle_front.png`.
- CHARACTER HEADER, then:

```
The character: an English merchant seaman from the Hudson's Bay Company's annual supply ship, 1811, about 25, wiry, deeply sunburnt, a tarred pigtail (queue) of brown hair down his back, gold ring in one ear, stubble. He wears a short navy-blue wool round jacket with brass buttons, open over a red wool waistcoat and a blue-and-white striped linen shirt; a black silk neckerchief knotted loosely; wide white canvas trousers (slops) ending above the ankle; black buckled shoes; a low-crowned black round hat of tarred and glazed canvas with a ribbon round the crown. A seaman's clasp knife hangs on a lanyard at his belt. He walks with a rolling sailor's gait, arms loose.

Rows:
1. IDLE - three frames: front, back, side (facing right).
2. WALK FRONT - 4 frames.
3. WALK BACK - 4 frames.
4. WALK SIDE - 4 frames facing right.
5. HAUL SIDE - 4 frames facing right: carrying a wooden cask on his right shoulder, steadied with both hands, walking.
6. DRINK - 4 frames, front: swigging from a leather-covered flask.
7. ATTACK SIDE - 4 frames facing right: swinging a short wooden belaying pin like a club.
8. DEATH - 4 frames from the front: hit, staggering, falling, lying face down.
```

### 1.5 HBC clerk / writer:DONE

- **Replaces:** the clerks (`Role.Clerk`, `Role.Postmaster`), currently the player tinted blue.
- **Attach:** `assets/external/pioneerpixel_hbc_trader/source_sheet.jpg` and `assets/sprites/player/idle_front.png`.
- CHARACTER HEADER, then:

```
The character: a young apprentice clerk ("writer") of the Hudson's Bay Company, about 19, from Orkney or Edinburgh, thin, pale, clean-shaven, fair hair tied back in a short ribbon; ink stains on the fingers of his right hand and a goose-quill pen tucked behind his ear in every frame. He wears a dark navy-blue wool tailcoat with a high collar and brass buttons, a grey waistcoat, a white linen shirt with a white stock at the throat, black knee breeches, grey wool stockings and black buckled shoes; a round black felt hat with a modest brim. Over his left arm he carries a thick leather-bound account ledger.

Rows:
1. IDLE - three frames: front, back, side (facing right).
2. WALK FRONT - 4 frames.
3. WALK BACK - 4 frames.
4. WALK SIDE - 4 frames facing right.
5. WRITE FRONT - 4 frames: standing, the ledger open on his left forearm, writing in it with the quill, dipping the quill in a small horn inkwell held in the same hand, writing again.
6. EAT - 4 frames, front: eating a piece of bread.
7. DRINK - 4 frames, front: drinking tea from a cup and saucer.
8. DEATH - 4 frames from the front: hit, the ledger falling from his arm, collapsing, lying on his side.
```

### 1.6 Cree trading captain (the band's leader):Done

- **Replaces:** Maskwa, the Cree chief (`Role.CreeChief`), currently the hunter sprite.
- **Attach:** `assets/external/pioneerpixel_hbc_trader/cree_trader.jpg` and `assets/sprites/cree/idle_front.png`.
- Historically the Company gave the "captain" of a trading band a captain's outfit each summer, and he wore it to trade.
- CHARACTER HEADER, then:

```
The character: an older Cree man, about 55, the trading captain of a Home Guard Cree band at York Factory, dignified and unhurried; deeply lined face, long grey-streaked black hair worn loose with two thin braids at the temples, one eagle feather tied upright at the back of the head. He wears the Company's gift "captain's coat": a long scarlet wool coat trimmed with gold lace on the cuffs, lapels and edges, worn open; under it a white linen shirt; a large polished silver gorget (a crescent-shaped plate) on a ribbon at his throat; dark-blue stroud leggings with broad bands of white beadwork; smoke-tanned moccasins with quillwork; a red wool blanket folded over his left shoulder. He carries a long-stemmed pipe with a red catlinite bowl and the stem hung with feathers.

Rows:
1. IDLE - three frames: front, back, side (facing right); in the front idle he holds the pipe across his chest in both hands.
2. WALK FRONT - 4 frames.
3. WALK BACK - 4 frames.
4. WALK SIDE - 4 frames facing right.
5. SPEAK FRONT - 4 frames: raising the pipe stem to point outward as he speaks, then holding it upright, then lowering it.
6. SMOKE FRONT - 4 frames: drawing on the pipe and letting out a thin curl of grey smoke.
7. EAT - 4 frames, front: eating dried meat.
8. DEATH - 4 frames from the front: hit, sinking to one knee, falling, lying on his back with the pipe beside him.
```

### 1.7 Children of the fort

- **Replaces:** the sons and daughters born in play (`Population.cs` spawns them as a shrunk labourer or a shrunk HBC man).
- **Attach:** `assets/external/pioneerpixel_hbc_trader/Cree WOman.jpg` and `assets/sprites/labourer/idle_front.png`.
- One sheet with both children. CHARACTER HEADER, then:

```
Two children of the fort, a brother and sister of mixed Cree and Orkney descent, about 8 years old, drawn with true child proportions (bigger head to body, shorter limbs) and about 70 pixels tall at the stated scale, on the same baseline as an adult would stand.

The boy: brown skin, black hair cut straight at the collar, a small white wool blanket capote with a dark-blue stripe near the hem and the hood down, a red sash, dark-blue leggings and moccasins, a little knife in the sash.
The girl: brown skin, black hair in two short braids tied with red cloth, a mid-calf dress of red printed cotton with a small white pattern, dark-blue leggings with a beaded edge, moccasins, a small grey wool shawl.

Rows for the BOY, then the same rows for the GIRL:
1. IDLE - front, back, side (facing right).
2. WALK FRONT - 4 frames.
3. WALK BACK - 4 frames.
4. WALK SIDE - 4 frames facing right.
5. RUN SIDE - 4 frames facing right, running and playing, arms out.
6. EAT - 4 frames, front: eating a piece of bannock.
```

---

## Priority 2: missing animations for characters already in the game

These must look like the same people already on screen. **Attach the original source sheet named below, plus one cut frame of that character**, and paste the CHARACTER HEADER, then the prompt. Start every one of these with the line given.

### 2.1 Player (HBC man, blue coat, tricorn)

- **Attach:** `assets/external/pioneerpixel_hbc_trader/source_sheet.jpg`, `HBc man with musket.jpg` and `assets/sprites/player/idle_front.png`.
- Has: walk front/back, eat, drink, sneak (3 directions), musket shoot, death. **Missing:** side walk (side movement currently plays the front walk), melee, carrying, sleeping, paddling.

```
Use the exact character in the attached sheets: the Hudson's Bay Company man in the black tricorn hat, navy-blue wool coat, striped point-blanket sash and boots. Same face, beard, colours and proportions. Draw only these new rows:

1. IDLE SIDE - 1 frame facing right.
2. WALK SIDE - 4 frames facing right.
3. RUN SIDE - 4 frames facing right, leaning forward, coat flying.
4. ATTACK SIDE - 4 frames facing right: slashing with a short trade knife, wind-up, slash, follow-through, recover.
5. CHOP SIDE - 4 frames facing right: swinging a trade axe down two-handed at about knee height, as if splitting a log on a block (draw the block and log).
6. CARRY SIDE - 4 frames facing right: walking bent under a canvas-wrapped 90-pound bale held on his back by a leather tumpline across his forehead.
7. CARRY FRONT - 4 frames of the same, walking toward the viewer.
8. PADDLE SIDE - 4 frames facing right: kneeling, paddling a narrow wooden paddle through a stroke (no canoe drawn).
9. SLEEP - 2 frames: lying on his back wrapped in a white point blanket with coloured stripes, hat over his face, the blanket rising slightly between the frames.
10. SHIVER FRONT - 4 frames: standing hunched, arms wrapped round himself, trembling with cold, breath showing as a small white puff.
11. DRUNK FRONT - 4 frames: swaying and stumbling while walking toward the viewer, a stoneware jug in one hand.
```

### 2.2 Factor, Mr. William Auld (grey coat, pack)

- **Attach:** `assets/external/pioneerpixel_hbc_trader/grey_trader_8way.jpg` and `assets/sprites/rival/idle_front.png`.
- **Missing:** side walk, death, drink, idle back.

```
Use the exact character in the attached sheet: the trader in the grey coat. Same face, hat, clothing, colours and proportions. Draw only these new rows:

1. IDLE - 2 frames: back, side (facing right).
2. WALK SIDE - 4 frames facing right.
3. DRINK - 4 frames, front: drinking from a small glass of port.
4. TALK FRONT - 4 frames: standing, gesturing firmly with one hand as he gives orders, then pointing off to the right.
5. DEATH - 4 frames from the front: hit, staggering, falling, lying still on his back.
```

### 2.3 Orkney labourer (green coat)

- **Attach:** `assets/external/pioneerpixel_hbc_trader/Man WALking.jpg` and `assets/sprites/labourer/idle_front.png`.
- Has only 2 back-walk frames. **Missing:** proper back walk, eat, drink, death, melee, work.

```
Use the exact character in the attached sheet: the Company labourer in the dark-green coat, tricorn, sash and boots. Same face, beard, colours and proportions. Draw only these new rows:

1. IDLE - 2 frames: back, side (facing right).
2. WALK BACK - 4 frames.
3. EAT - 4 frames, front: eating from a slab of pemmican.
4. DRINK - 4 frames, front: drinking from a pewter tankard of ale.
5. CHOP SIDE - 4 frames facing right: splitting a log on a chopping block with an axe, two-handed.
6. HAUL SIDE - 4 frames facing right: dragging a log behind him with a rope over his shoulder.
7. ATTACK SIDE - 4 frames facing right: a bare-knuckle punch, wind-up, punch, follow-through, recover.
8. DEATH - 4 frames from the front: hit, staggering, falling, lying face down.
```

### 2.4 Cree hunter

- **Attach:** `assets/external/pioneerpixel_hbc_trader/Cree man with bow.jpg`, `cree_trader.jpg` and `assets/sprites/cree/idle_front.png`.
- Has 2 side-walk frames and a bow shot. **Missing:** full side walk, back idle, eat, drink, death, melee.

```
Use the exact character in the attached sheets: the Cree hunter with the bow. Same face, hair, feather, clothing, colours and proportions. Draw only these new rows:

1. IDLE - 2 frames: back, side (facing right).
2. WALK SIDE - 4 frames facing right.
3. RUN SIDE - 4 frames facing right, bow in hand.
4. EAT - 4 frames, front: eating dried meat.
5. DRINK - 4 frames, front: drinking from a birchbark cup.
6. ATTACK SIDE - 4 frames facing right: striking with a wooden-handled iron tomahawk, wind-up, strike, follow-through, recover.
7. BUTCHER - 4 frames, three-quarter front: kneeling over a dead caribou (draw the caribou lying on its side) and working with a knife.
8. DEATH - 4 frames from the front: hit, sinking to his knees, falling, lying on his side with the bow beside him.
```

### 2.5 Cree woman

- **Attach:** `assets/external/pioneerpixel_hbc_trader/Cree WOman.jpg` and `assets/sprites/cree_woman/idle_front.png`.
- Has 2 side-walk frames. **Missing:** full side walk, back idle, eat, drink, death, work.

```
Use the exact character in the attached sheet: the Cree woman in the long smoked-hide dress with the beaded yoke and the feather in her hair. Same face, hair, clothing, colours and proportions. Draw only these new rows:

1. IDLE - 2 frames: back, side (facing right).
2. WALK SIDE - 4 frames facing right.
3. EAT - 4 frames, front: eating from a birchbark dish.
4. DRINK - 4 frames, front: drinking from a birchbark cup.
5. SCRAPE HIDE - 4 frames, three-quarter front: kneeling at a hide stretched on a wooden frame, scraping it with a bone flesher.
6. CARRY WOOD SIDE - 4 frames facing right: firewood bundle carried on her back by a tumpline.
7. DEATH - 4 frames from the front: faltering, sinking down, falling to her side, lying still.
```

### 2.6 Assiniboine man

- **Attach:** `assets/external/pioneerpixel_hbc_trader/Assainboine Man.jpg`, `Assinaobine Shooting Musket.jpg` and `assets/sprites/assiniboine/idle_front.png`.
- **Missing:** back and side idle, eat, drink, death, melee.

```
Use the exact character in the attached sheets: the Assiniboine man with the musket and the buffalo robe. Same face, hair, clothing, colours and proportions. Draw only these new rows:

1. IDLE - 2 frames: back, side (facing right).
2. WALK SIDE - 4 frames facing right (he has only 2 now; redraw all 4 to match).
3. EAT - 4 frames, front: eating pemmican.
4. DRINK - 4 frames, front: drinking from a horn cup.
5. ATTACK SIDE - 4 frames facing right: swinging a stone-headed war club.
6. DEATH - 4 frames from the front: hit, staggering, falling, lying on his back with the robe spread.
```

---

## Priority 3: animals (all four in the game are stand-ins): Done

The wolf and caribou are a stock pack in a different style; the beaver and goose are two-frame placeholders. For every animal, **attach `assets/external/pioneerpixel_hbc_trader/Cree man with bow.jpg`** (to carry the style across from the people), plus the current sprite named below so Gemini sees what it is replacing. Paste the ANIMAL HEADER, then the prompt.

### 3.1 Barren-ground caribou:Done

- **Replaces:** `assets/sprites/animals/caribou_*` (a red stag). The migrating herd is the York Factory hunters' main meat.
- **Also attach:** `assets/sprites/animals/caribou_SE_walk.png`.

```
The animal: a barren-ground caribou (Rangifer tarandus groenlandicus) in autumn coat, Hudson Bay lowlands. A big bull: rich chocolate-brown body and legs, a shaggy creamy-white neck and mane hanging under the throat, a pale grey-white rump patch and short white tail, a pale belly line, a broad dark muzzle, very large round splayed hooves. Huge sweeping antlers curving back then forward in a C shape, with one flattened vertical "shovel" brow tine reaching down over the face and many tines at the tips. At this scale he is about 75 pixels tall at the shoulder and about 125 pixels to the tips of the antlers (against a man 110 pixels tall), about 130 pixels long.

Rows (each in DOWN-RIGHT, then the same row in UP-RIGHT):
1. IDLE - 4 frames: standing, head lowering to graze, nosing the ground, raising the head and looking around.
2. WALK - 6 frames: a slow walk.
3. RUN - 6 frames: the high-stepping caribou trot breaking into a gallop.
4. DEATH - 4 frames: struck, stumbling, collapsing onto its side, lying still.
Then, at the end, one extra row:
5. COW - one IDLE frame and one WALK frame of a cow caribou in each direction: smaller (about 65 pixels at the shoulder), lighter brown, with short thin antlers.
```

### 3.2 Canada goose

- **Replaces:** `assets/sprites/animals/goose_0.png` / `goose_1.png`. The spring and autumn goose hunt feeds the post through the winter (salted geese).
- **Also attach:** `assets/sprites/animals/goose_0.png`.

```
The animal: a Canada goose (Branta canadensis). Glossy black head and long black neck with a clean white "chinstrap" patch across the cheeks and throat; brown-grey back and wings with pale feather edging; pale buff-grey breast; black tail with a white band beneath; black legs and webbed feet; black bill. Standing it is about 40 pixels tall (against a man 110 pixels tall); in flight the wingspan is about 90 pixels.

Rows (each in DOWN-RIGHT, then UP-RIGHT, unless the row says otherwise):
1. IDLE - 4 frames: standing, head down grazing in the marsh, head up alert, a short honk with the bill open.
2. WALK - 4 frames: waddling.
3. TAKE OFF - 4 frames: running and flapping, lifting off.
4. FLY - 6 frames: wingbeat cycle in level flight, neck stretched straight out.
5. SHOT - 4 frames, DOWN-RIGHT only: hit in flight, wings crumpling, tumbling, falling.
6. DEAD - 1 frame: lying on the ground on its side, one wing spread.
7. FLOCK - 1 frame, no direction: eight geese flying in a V formation seen from below at a steep angle, small (each about 25 pixels across), for the sky.
```

### 3.3 Beaver

- **Replaces:** `assets/sprites/animals/beaver_0.png` / `beaver_1.png`. The fur the whole trade is counted in.
- **Also attach:** `assets/sprites/animals/beaver_0.png`.

```
The animal: a North American beaver (Castor canadensis) in prime winter fur. Thick glossy dark chestnut-brown fur, a broad flat paddle-shaped black tail with a scaly texture, small round ears, small dark eyes, bright orange front incisors, large webbed hind feet, small dexterous front paws. On land it is about 30 pixels tall and about 70 pixels long including the tail (against a man 110 pixels tall).

Rows (each in DOWN-RIGHT, then UP-RIGHT):
1. IDLE - 4 frames: sitting up on its haunches, grooming its face with its front paws, looking around.
2. WALK - 4 frames: a slow, hunched waddle on land, tail dragging.
3. GNAW - 4 frames: standing up against a short birch trunk (draw the trunk, about 40 pixels tall, with a pale chewed hourglass notch) gnawing at it.
4. SWIM - 4 frames: only the head and the top of the back above the water line, a V-shaped ripple trailing behind (draw a little flat dark-blue water just around the animal).
5. TAIL SLAP - 4 frames: diving, the tail lifting and slapping down, a white splash.
6. DEATH - 3 frames: struck, rolling onto its side, lying still.
```

### 3.4 Grey wolf: Done

- **Replaces:** `assets/sprites/animals/wolf_*` (stock pack, works mechanically but wrong style).
- **Also attach:** `assets/sprites/animals/wolf_idle.png`.

```
The animal: a grey wolf (Canis lupus) of the Hudson Bay lowlands in winter coat: thick pale grey-buff fur, a darker grizzled grey saddle along the back and a dark tip to the tail, cream legs and belly, a pale muzzle, amber eyes, long legs and big feet. Lean, not cartoonish. About 55 pixels tall at the shoulder and about 110 pixels long (against a man 110 pixels tall).

Rows (each in DOWN-RIGHT, then UP-RIGHT):
1. IDLE - 4 frames: standing, ears turning, sniffing the air.
2. WALK - 6 frames: the wolf's steady trot.
3. RUN - 6 frames: full gallop.
4. ATTACK - 4 frames: crouching, lunging with jaws open, biting, recoiling.
5. HOWL - 4 frames: sitting, head tipping back, howling, lowering the head.
6. DEATH - 4 frames: struck, staggering, falling on its side, lying still.
```

### 3.5 Snowshoe hare

- **New.** Snaring hares is simulated, but there is no animal in the world, only the item `hare.png`.
- **Also attach:** `assets/sprites/items/hare.png`.

```
The animal: a snowshoe hare (Lepus americanus), drawn twice: in winter coat (pure white with black-tipped ears and dark eyes) and in summer coat (mottled grey-brown with a white belly and white feet). Very large hind feet. Sitting it is about 22 pixels tall and about 35 pixels long (against a man 110 pixels tall).

Rows for the WINTER hare (each in DOWN-RIGHT, then UP-RIGHT), then the same rows for the SUMMER hare:
1. IDLE - 4 frames: sitting, nose twitching, ears turning, nibbling.
2. HOP - 4 frames: a slow hop.
3. RUN - 4 frames: bounding at full speed.
4. SNARED - 2 frames, DOWN-RIGHT only: caught by the neck in a brass-wire snare tied to a stake (draw the stake), then lying dead beside it.
```

### 3.6 Willow ptarmigan: Done

- **New.** Named in the design doc for the Hudson Bay lowlands (16.2): winter food when the geese are gone.

```
The animal: a willow ptarmigan (Lagopus lagopus), drawn twice: in winter plumage (all white except a black tail and a small red comb over the eye) and in summer plumage (chestnut-brown head and body with white wings and belly). A plump, round grouse with feathered feet. About 20 pixels tall (against a man 110 pixels tall).

Rows for the WINTER bird (each in DOWN-RIGHT, then UP-RIGHT), then the same rows for the SUMMER bird:
1. IDLE - 4 frames: standing, pecking at willow buds, head up.
2. WALK - 4 frames.
3. FLUSH - 4 frames: bursting up into flight with rapid wingbeats.
4. DEAD - 1 frame: lying on the snow on its side.
```

### 3.7 Later: animals the design doc names but the code does not have yet: Not DONE

Only when they are built: moose, black bear, polar bear (it walks the shore at York Factory in autumn), red fox, arctic fox, marten, mink, muskrat, otter, lynx, wolverine, bison and pronghorn (plains), sled dogs. Use the same ANIMAL HEADER and the same row set as the wolf (IDLE, WALK, RUN, ATTACK if it fights, DEATH), and give its size against a 110-pixel man.

---

## Priority 4: inventory icons: Done

The current icons (`assets/sprites/items/*.png`, 64×64) are flat, simple shapes and don't match the hand-painted people. These four sheets redo the whole set in the characters' style and add the missing ones. **Attach `assets/external/pioneerpixel_hbc_trader/Cree WOman.jpg`** (its "Hudson's Bay Blanket Pelt" and "Beaded Medicine Bag" vignettes are the exact item style wanted) and `assets/external/pioneerpixel_hbc_trader/hbc_eating_variants.jpg` (its loaf and bread-on-plate). Paste the ITEM HEADER, then the list.

### 4.1 Food and drink: DONE

```
The objects, in this order:
1. Pemmican - a dense dark-brown slab of pounded dried meat and fat, flecked with dried berries, cut from a rawhide bag (show the bag's edge, hair side out).
2. Dried goose - a whole split and smoke-dried goose, flattened and dark golden-brown, tied with a strip of bark.
3. Cask of salt geese - a small wooden keg with iron hoops, lid off, salted goose pieces packed inside under coarse salt.
4. Loaf of bread - a round, coarse, dark-crusted loaf with a floury top.
5. Tankard of ale - a pewter tankard with a lid tipped open, brown ale with a little foam.
6. Dram of rum - a small tin cup of dark rum beside a square green-glass case bottle.
7. Water - a tin cup brimming with clear cold water, beads of water on the side.
8. Fresh meat - a raw haunch of caribou, red meat with white fat and a bone end.
9. Dried meat - a bundle of thin, dark, wrinkled strips of dried caribou meat tied with sinew.
10. Goose - a freshly killed Canada goose lying on its side, black neck and white chinstrap.
11. Fish - a fresh lake whitefish, silver with a small head and forked tail.
12. Hare - a dead snowshoe hare in white winter coat, hung by its hind feet from a loop of cord.
```

### 4.2 Furs and clothing: DONE

```
The objects, in this order:
1. Beaver pelt - a prime winter beaver pelt, thick dark-brown fur, stretched into an almost round oval on a bent willow hoop, laced to the hoop with cord.
2. Summer pelt - a thin, poor summer beaver pelt, paler brown, sparse and patchy fur, stretched on a smaller hoop.
3. Shot-through pelt - a beaver pelt on a hoop with two ragged shot holes and a torn edge.
4. Caribou hide - a folded caribou hide, brown hair on the outside, the pale scraped skin side showing at one corner.
5. Holed hide - a caribou hide with a large torn hole and ragged edges.
6. Wolf pelt - a pale grey wolf pelt laid flat with the head and tail, fur thick.
7. Capote - a hooded blanket coat made from a white Hudson's Bay point blanket, with the green, red, yellow and indigo stripes across the hem and cuffs, the hood up, a red sash at the waist.
8. Point blanket - a folded white wool Hudson's Bay point blanket with green, red, yellow and indigo stripes and the short black "point" lines woven at one edge.
9. Hide coat - a smoke-tanned moosehide coat, golden brown, with long fringes on the sleeves and floral quillwork on the chest.
10. Trade goods - a 90-pound bale wrapped in canvas, roped crosswise, marked "HBC" in black paint.
11. Snowshoes - a pair of long Cree snowshoes, curved birch frames with upturned toes, laced with babiche in a fine mesh.
12. Tumpline - a long carrying strap of braided leather with a wide woven head band in red and black.
```

### 4.3 Tools and weapons: DONE

```
The objects, in this order:
1. Steel trap - a long-spring iron leghold beaver trap, jaws set open, with a length of chain and a ring.
2. Copper kettle - a bright copper trade kettle with an iron bail handle and a lid, a little dented.
3. Flint and steel - a C-shaped steel striker, a grey gun flint and a small tin tinderbox with charcloth.
4. Fishing line - twine wound round a wooden stick, with a bone-and-iron hook and a lead sinker.
5. Pack basket - a woven ash-splint pack basket with leather shoulder straps.
6. Axe - a trade axe with a heavy iron head stamped with a mark, on a curved wooden haft.
7. Trade musket - a North West trade gun: a long smooth-bore flintlock with a short forestock, a big iron trigger guard and a brass sideplate shaped like a dragon or serpent.
8. Bow - a short sinew-backed hunting bow, painted red at the grip, with three feathered arrows.
9. Knife - a butcher's trade knife with a wooden handle in a leather sheath decorated with white and blue beadwork.
10. Tomahawk - a pipe tomahawk: a forged iron blade with a pipe bowl on the back, on a wooden haft wrapped with brass tacks and a trailing eagle feather.
11. Powder horn and shot pouch - a cow-horn powder horn engraved with lines and a small leather pouch of lead balls, on one strap.
12. Fishing net - a folded twine gill net with wooden floats and stone sinkers.
```

### 4.4 Things the design doc names that have no icon yet: DONE

```
The objects, in this order:
1. Letter - a folded and sealed letter of thick paper, a red wax seal pressed with a crest, addressed in faded brown ink handwriting.
2. Dispatch packet - a bundle of letters wrapped in oiled cloth and tied, for the express canoe.
3. Journal - a thick brown-leather post journal with a strap, closed.
4. Account ledger - a large ledger bound in vellum, open, columns of figures in ink.
5. Made beaver token - a round brass trade token stamped with a beaver and "HB" and "1 MB".
6. Tobacco - a long twist of dark Brazil tobacco, coiled.
7. Beads - several strings of glass trade beads: white, sky-blue, red and black.
8. Awl and needles - an iron awl with a wooden handle, three steel needles and a skein of thread.
9. Bolt of stroud - a folded bolt of dark-blue wool stroud cloth with a white selvedge edge.
10. Ice chisel - an iron ice chisel blade on a long wooden pole, for cutting fishing holes.
11. Pemmican bag - a whole 90-pound rawhide bag of pemmican (a taureau), sewn shut, hair on the outside.
12. Fur pack - a 90-pound pack of beaver pelts pressed flat into a bale and roped, a pelt's edge showing.
```

---

## Priority 5: world props" DONE

Missing from the world (see `asset-inventory.md` section 4). **Attach `assets/tiles/props/tipi.png` and `assets/tiles/props/spruce_0.png`** (the two props drawn for this game, which fix the isometric angle and the 128×256 box), and one PioneerPixel character sheet so the scale is right. Paste the PROP HEADER, then the list. Split across sheets if Gemini crowds them.

### 5.1 Hunting, trapping and camp

```
The objects (a man is 110 pixels tall at this scale), in this order:
1. Beaver lodge - a dome of peeled sticks and mud rising out of a small pond (draw the pond), about 2 ground tiles across, snow on the top.
2. Beaver dam - a curved low wall of sticks, mud and stones holding back a pond, one ground tile long.
3. Drying rack - a frame of three spruce poles with a crossbar, strips of red meat and split fish hanging from it to dry.
4. Pelt stretching frame - a square frame of lashed poles with a beaver pelt laced inside it on a hoop, leaning on a stump.
5. Hide stretched on frame - a caribou hide laced into a tall upright pole frame for scraping.
6. Cache platform - a small platform on four tall peeled poles, a bundle wrapped in birchbark on top, a notched-log ladder leaning on it.
7. Snare - a brass-wire loop hung from a bent sapling over a hare trail in the snow, a little fence of sticks either side.
8. Set steel trap - an iron leghold trap set at the edge of water, its chain staked to a dry pole.
9. Fish trap / weir - a fence of stakes across a stream with a woven basket trap at the gap.
10. Ice fishing hole - a round hole cut in grey-white ice with a chopped rim, a line running into it from a stick.
11. Overturned canoe - a birchbark north canoe, yellow-tan bark with black-gummed seams and a painted red bow, lying upside down on two short logs on the shore.
12. Campfire with kettle - a small fire of split wood with a copper kettle hanging from a tripod of sticks.
```

### 5.2 The fort

```
The objects (a man is 110 pixels tall at this scale), in this order:
1. Flagstaff - a tall white-painted wooden flagpole with the Hudson's Bay Company ensign: a red flag with the British Union flag in the upper corner and the white letters "HBC" in the red field, flying.
2. Cannon - a small iron cannon on a wooden naval carriage with small wooden wheels, painted black.
3. Water barrel - a big oak barrel with iron hoops and a lid, a tin dipper hanging on the side.
4. Fur press - a tall wooden fur press: a heavy wooden box with a long beam lever above for pressing furs into bales.
5. Trade counter - a heavy plank counter with a set of brass scales, a few blankets and a kettle on it.
6. Writing desk - a slant-topped wooden clerk's desk with an open ledger, an inkwell and a candle.
7. Iron stove - a squat black iron box stove with a long stovepipe.
8. Bunk - a double wooden bunk bed with grey wool blankets.
9. Sea chest - a wooden sea chest with rope handles and an iron lock.
10. Grave - a grave mound with a weathered wooden cross and a small picket fence around it, snow at the base.
11. Woodpile under snow - a long stack of split cordwood with a cap of snow.
12. Sled - a long flat toboggan of bent birch boards, a canvas-wrapped load lashed on it.
```

### 5.3 The land (lowland ground cover)

```
The objects (a man is 110 pixels tall at this scale), in this order:
1. Black spruce - a tall, thin, scraggly lowland black spruce with short drooping branches and a club-like top.
2. Tamarack in autumn - a thin larch with golden-yellow needles.
3. Dead tree - a grey dead spruce snag, bare and leaning.
4. Willow thicket - a low dense clump of red-stemmed willow, in leaf.
5. Willow in winter - the same thicket, leafless, half buried in snow.
6. Muskeg pool - a small dark peat pond with reeds and cotton-grass, flat on the ground, one ground tile.
7. Boulder - a big grey granite glacial erratic, lichen on it.
8. Rocks - a cluster of small grey stones.
9. Reeds - a clump of tall marsh reeds at a water's edge.
10. Snowdrift - a smooth wind-sculpted drift of snow, one ground tile.
```

---

## Priority 6: vehicles: DONE

**Attach `assets/sprites/canoe.png`, `assets/sprites/ship.png`** and one PioneerPixel character sheet. Use the PROP HEADER, then:

```
The vehicles (a man is 110 pixels tall at this scale), each drawn twice: heading DOWN-RIGHT and heading UP-RIGHT (the game mirrors the other two).
1. North canoe - a 25-foot birchbark freight canoe, yellow-tan bark with black-gummed seams, high curved bow and stern painted with a red and white design, empty, floating (draw a little dark water around the hull only), about 300 pixels long.
2. North canoe loaded - the same canoe low in the water with canvas-wrapped bales amidships and four voyageurs in red tuques kneeling and paddling.
3. York boat - a heavy wooden inland freight boat of the Hudson's Bay Company, double-ended, clinker-built of grey-brown planks, a single mast with a square sail furled, eight oarsmen at long oars, about 420 pixels long.
4. Dog sled - a long birch toboggan loaded with a canvas-wrapped bundle, pulled by four husky-type dogs in single file in leather harness, a man in a capote running behind.
5. Supply ship at anchor - a three-masted merchant ship of 1811, black hull with a yellow stripe, sails furled on the yards, the HBC ensign at the stern, riding at anchor, about 520 pixels long.
```

---

## Priority 7: buildings (hard; expect cleanup): TEMP DONE

The buildings are built from wall tiles by `StructurePainter` (each wall piece is a 128×256 image standing on one edge of a 128×64 ground diamond). They are still Kenney's European stone set. Gemini is not good at exact tiles, so ask for a **reference painting first** and only then for tiles; I will probably have to redraw the tiles to fit, but the painting fixes the look.

**Step 1: reference painting.** Attach `assets/tiles/props/tipi.png` and `assets/tiles/buildings/wall_N.png`. No header:

```
Paint a detailed hand-painted pixel art view, in 2:1 isometric projection, of the Hudson's Bay Company's York Factory as it stood in 1811 on the Hayes River near Hudson Bay: a cluster of one- and two-storey buildings of squared spruce logs laid horizontally (piece-sur-piece), some whitewashed with lime and some weathered silver-grey, steep plank roofs, small many-paned sash windows with white frames, heavy plank doors with iron strap hinges, plank walkways raised over the muddy ground, the whole enclosed by a palisade of upright pointed logs with a gate, a white flagstaff with the HBC ensign flying, cordwood stacked against the walls, a few men in blue and green coats, flat marshy ground and a line of black spruce behind, a grey autumn sky. Soft light from the upper left. No text.
```

**Step 2: wall tiles.** Attach the painting from step 1 and `assets/tiles/buildings/wall_N.png`, `wall_corner_N.png`, `wall_door_N.png`, `wall_window_N.png`. PROP HEADER, then:

```
A set of isometric wall pieces to build the buildings in the attached painting, in exactly its style and materials. Every piece is one wall segment standing along one edge of a single 128x64 ground diamond, drawn inside a 128x256 box with the bottom of the wall on the diamond's edge; the wall is about 150 pixels high. Two materials, each as a full set: WHITEWASHED squared logs, and WEATHERED GREY squared logs.

For each material, in this order:
1. Plain wall, facing down-left.
2. Plain wall, facing down-right.
3. Outside corner where the two meet.
4. Wall with a small many-paned sash window, facing down-left.
5. Wall with the window, facing down-right.
6. Wall with a closed plank door with iron hinges, facing down-left.
7. Wall with the door, facing down-right.
8. The same doors standing open.
9. Floor: one 128x64 diamond of worn grey planks.
10. Roof piece: a section of steep plank roof the size of one diamond.
Then, once:
11. Palisade: a section of upright pointed logs, one diamond edge long, facing down-left and facing down-right, and a corner.
12. Palisade gate, closed and open.
```

---

## Priority 8: menu and UI art: DONE

### 8.1 Main menu painting:MDONE

The new main menu draws the ship and the player in front of a gradient. A real painting behind it would do more. No header; attach `assets/external/pioneerpixel_hbc_trader/source_sheet.jpg`:

```
A wide 16:9 hand-painted pixel art landscape, 1920x1080, in the style of the attached sheet: the mouth of the Hayes River at York Factory, Hudson Bay, at dusk in late August 1811. The Company's annual supply ship, a three-masted merchant ship with a black hull, rides at anchor in the grey bay on the right; a birchbark canoe with two men paddles toward the shore; on the low flat shore on the right, the wooden buildings of the fort behind a palisade, a white flagstaff with the red HBC ensign, smoke rising from chimneys, a few tipis of the Home Guard Cree beyond. Flat marsh, black spruce, an enormous sky going from deep blue overhead to a band of orange and rose low over the water, the first stars. The left third of the picture is quiet: dark water and sky only, so a menu can sit over it. No text, no people close up.
```

### 8.2 Needs and status icons: DONE

ITEM HEADER (it suits small icons too), attach `assets/sprites/items/water.png` and `Cree WOman.jpg`:

```
Small status icons for the HUD, each drawn to read clearly at 24x24 pixels, in this order:
1. Health - a red heart drawn as a beaded design.
2. Warmth - a small orange flame.
3. Cold - a white snowflake.
4. Food - a slab of pemmican.
5. Water - a drop of water.
6. Fatigue - a closed eye.
7. Drunk - a stoneware jug.
8. Fever - a bead of sweat on a forehead.
9. Scurvy - a pale gum with a missing tooth.
10. Wounded - a bandage with a red stain.
11. Load - a tumpline bundle.
12. Standing with the Company - a small HBC ensign.
```

### 8.3 Faction crests and the window icon: DOne

ITEM HEADER, attach `source_sheet.jpg` (its HBC crest vignette):

```
The objects, in this order:
1. Hudson's Bay Company crest - a shield with a red cross and four beavers, supported by two elks, a fox on top, on a small banner the words "PRO PELLE CUTEM".
2. North West Company crest - a beaver gnawing a tree, inside a round frame with the word "PERSEVERANCE".
3. Cree band sign - a black bear's paw print painted in red ochre on a round smoked-hide disc.
4. Assiniboine band sign - a red hand print on a round buffalo-hide shield with eagle feathers hanging from the edge.
5. Game window icon - a single beaver pelt stretched on a willow hoop over a crossed paddle and musket, bold and simple, reading clearly at 32x32 pixels.
```

---

## Summary checklist

| # | Asset | Replaces / for | Attach |
|---|---|---|---|
| 1.1 | Voyageur (2 sheets) | tinted factor | `Man WALking.jpg`, `rival/idle_front.png` |
| 1.2 | NWC wintering partner | tinted factor | `grey_trader_8way.jpg`, `rival/idle_front.png` |
| 1.3 | HBC woman / country wife (2 sheets) | tinted, shrunk player | `Cree WOman.jpg`, `cree_woman/idle_front.png` |
| 1.4 | Sailor | tinted player | `source_sheet.jpg`, `player/idle_front.png` |
| 1.5 | Clerk | tinted player | `source_sheet.jpg`, `player/idle_front.png` |
| 1.6 | Cree trading captain | hunter sprite | `cree_trader.jpg`, `cree/idle_front.png` |
| 1.7 | Children (boy and girl) | shrunk adults | `Cree WOman.jpg`, `labourer/idle_front.png` |
| 2.1 | Player: side walk, melee, carry, sleep, paddle, shiver, drunk | missing | `source_sheet.jpg`, `HBc man with musket.jpg`, `player/idle_front.png` |
| 2.2 | Factor: side walk, drink, talk, death | missing | `grey_trader_8way.jpg`, `rival/idle_front.png` |
| 2.3 | Labourer: back walk, eat, drink, chop, haul, punch, death | missing | `Man WALking.jpg`, `labourer/idle_front.png` |
| 2.4 | Cree hunter: side walk, run, eat, drink, tomahawk, butcher, death | missing | `Cree man with bow.jpg`, `cree_trader.jpg`, `cree/idle_front.png` |
| 2.5 | Cree woman: side walk, eat, drink, scrape, carry, death | missing | `Cree WOman.jpg`, `cree_woman/idle_front.png` |
| 2.6 | Assiniboine: idles, side walk, eat, drink, club, death | missing | `Assainboine Man.jpg`, `Assinaobine Shooting Musket.jpg`, `assiniboine/idle_front.png` |
| 3.1 | Barren-ground caribou | stock stag | `Cree man with bow.jpg`, `animals/caribou_SE_walk.png` |
| 3.2 | Canada goose | 2-frame placeholder | `Cree man with bow.jpg`, `animals/goose_0.png` |
| 3.3 | Beaver | 2-frame placeholder | `Cree man with bow.jpg`, `animals/beaver_0.png` |
| 3.4 | Grey wolf | stock pack | `Cree man with bow.jpg`, `animals/wolf_idle.png` |
| 3.5 | Snowshoe hare | new | `Cree man with bow.jpg`, `items/hare.png` |
| 3.6 | Willow ptarmigan | new | `Cree man with bow.jpg` |
| 4.1-4.4 | Item icons (48) | flat icons, 2 shared, 12 missing | `Cree WOman.jpg`, `hbc_eating_variants.jpg` |
| 5.1-5.3 | Props (34) | missing | `props/tipi.png`, `props/spruce_0.png` + a character sheet |
| 6 | Canoe, York boat, dog sled, ship | single static images | `canoe.png`, `ship.png` + a character sheet |
| 7 | York Factory painting, then wall tiles | Kenney stone set | `props/tipi.png`, `buildings/wall_*.png` |
| 8.1 | Main menu painting | gradient | `source_sheet.jpg` |
| 8.2 | HUD need icons | none | `items/water.png`, `Cree WOman.jpg` |
| 8.3 | Crests and window icon | Godot logo | `source_sheet.jpg` |

**Not covered here:** ground tiles. The terrain now comes from a streamed world map with twelve ground types (`World/WorldMap.cs`: ocean, lake, river, ford, boreal, tundra, shield, parkland, grassland, tall grass, lakes forest, cordillera). That work is still in progress in another session, and seamless isometric tiles are the one thing Gemini reliably gets wrong. They are better drawn once that session has settled the tile shapes.
