# Asset Prompts for Gemini

Art the game still needs, as prompts ready to paste into Gemini, with the sheet to attach as a style reference for each. First written 2026-10-10; **pruned 2026-10-11 against commit `b2064f4`** — everything drawn since then was deleted from this file rather than left here marked done. What remains is what is still missing.

For what the game already has — and for finished art that exists but nothing loads yet — see `asset-inventory.md`. Sound is in `audio-prompts.md`. Nations, people and places missing from the *code* are in `world-content-needed.md`.

---

## How to use this

1. **Open a fresh Gemini chat per sheet.** Old sheets in the same chat bleed into new ones.
2. **Attach the reference image(s) listed for that asset.** All paths are in this repo. The `assets/external/pioneerpixel_hbc_trader/*.jpg` sheets are the earlier Gemini sheets this game's characters were cut from; they fix the style. Attaching a single cut frame (e.g. `assets/sprites/clerk/idle_front.png`) as well fixes the scale.
3. **Paste the matching HEADER block first** (Character, Animal, Item, or Prop), **then the asset's prompt** underneath it, as one message.
4. If a sheet comes back with the wrong layout (checkerboard, parchment, grid lines, numbers in cells), reply: *"Same sheet again, but on a flat solid magenta #FF00FF background with no borders, numbers or text except the row labels."*
5. Drop the result in `assets/external/pioneerpixel_hbc_trader/` (or a new folder for animals/items) with a clear name and tell me; I'll slice it into frames and wire the animations.

**Why magenta:** the slicer finds sprites by cutting along runs of background colour. Checkerboards, parchment and cell borders all have to be cleaned by hand; one flat colour that nothing in 1811 is dyed cuts cleanly.

**Scale note.** The three oldest sheets (`player`, `cree`, `rival`) are 150×130 per frame; everything drawn since is **180×170**. Always attach one of the newer sheets — `clerk`, `voyageur`, `assiniboine` — so new art comes back at the current scale.

**Animation names the code plays** (a sheet with these rows drops straight in): `idle`, `idle_back`, `idle_side`, `walk`, `walk_back`, `walk_side` (faces right, mirrored for left), `run_side`, `eat`, `drink`, `attack`, `shoot`, `death`, plus the role-specific ones already in use — `talk`, `write`, `smoke`, `butcher`, `scrape`, `haul`, `chop`, `portage`, `paddle`, `carry_wood`, `carry_water`, `carry_baby`, `sleep`, `shiver`, `drunk`, `sneak`/`sneak_back`/`sneak_side`. Animals: `idle_se`, `walk_se`, `run_se`, `attack_se`, `death_se` and the same for `ne` (`sw`/`nw` are mirrored), plus `summer_*` where the coat changes.

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

### EFFECTS HEADER

For things that are not isometric objects — fire, weather, gunfire.

```
Create one sprite sheet of visual effects for a 2D hand-painted pixel art game in the 16-bit RPG manner, matching the attached reference in line quality and palette. Every effect is drawn on a flat solid magenta background (#FF00FF) with nothing else behind it - no ground, no characters, no borders, no grid lines, no text except the row label in small plain black capitals at the left end of each row. Each row is one animation, read left to right. At least 30 pixels of magenta between frames and between rows. Soft light from the upper left. No blur, no modern lens glow. Output as a PNG.
```

---

## Priority 1: the Factor:Done

The last stand-in character in the game. Every other role was redrawn at 180×170 in the Gemini pass; Mr. William Auld still wears `RivalFrames` — the original 150×130 sheet, four animations, no side walk, no drink, no death. He is a named NPC the player deals with daily.

Attach `grey_trader_8way.jpg` and `assets/sprites/clerk/idle_front.png` (for the new 180×170 scale), and use the clerk's row list plus `talk`:

```
A Hudson's Bay Company chief factor of 1811, a Scotsman of about fifty, heavy-set and formally dressed: a long grey wool frock coat over a dark waistcoat and white linen stock, breeches and buckled shoes indoors, a black cocked hat, grey hair clubbed at the neck, a severe lined face. He carries himself stiffly and with authority - the man who runs the post. Rows: IDLE FRONT, IDLE BACK, IDLE SIDE, WALK FRONT (4), WALK BACK (4), WALK SIDE (4), EAT (4), DRINK (4), TALK (3, one hand raised making a point), WRITE (3, bent over a ledger at a desk), DEATH (4).
```

---

## Priority 2: the seven nations with no art

`adc11ab` put a camp for every nation in the 1811 atlas on the map — roughly 700 people, against 200 in the posts. Only **Cree** and **Assiniboine** have sheets. The rest fall through `Agents/Appearance.cs` to whatever sheet they were spawned with, so a Blackfoot man on the plains currently walks around dressed as an Orkney labourer. This is the largest remaining gap in the game.

Each nation needs **one man's sheet** at minimum; women are a second pass (§2.8). Use the CHARACTER HEADER. **Attach `assets/external/pioneerpixel_hbc_trader/Assainboine Man.jpg` and `assets/sprites/assiniboine/idle_front.png`** for every one of these — the Assiniboine sheet is the closest existing match in build, scale and palette.

Rows for every nation's man (match the Assiniboine sheet so `AssiniboineFrames.tres` can be copied and re-pathed):

```
Rows, in this order: IDLE FRONT, IDLE BACK, IDLE SIDE, WALK FRONT (4), WALK BACK (4), WALK SIDE (4), ATTACK (4), SHOOT BOW (3), MUSKET (3), EAT (4), DRINK (4), DEATH (4).
```

Then one of these descriptions:

### 2.1 Ojibwe (Saulteaux) man — woodlands, south and east of Lake Winnipeg: Done

```
An Ojibwe man of the woodlands in 1811: smoke-tanned buckskin leggings and breechcloth, a quilled and beaded buckskin shirt with floral beadwork in red, white and blue, soft puckered-seam moccasins, a trade blanket of dark blue wool over one shoulder, hair in two braids with a single eagle feather upright at the back, a bandolier bag of loom-beaded floral work across the chest. Carries a bow; for the MUSKET rows, a short trade gun.
```

### 2.2 Blackfoot man — the plains, far west: DONE

```
A Blackfoot man of the northern plains in 1811, tall and lean: fringed buckskin leggings and a buckskin war shirt with painted horizontal bands and hair-lock fringes across the chest, hard-soled moccasins with geometric beadwork in red, blue and white, hair long and loose with a brow-band, a buffalo robe painted with a geometric box-and-border design worn over one shoulder in the idle poses. Carries a short sinew-backed horn bow; for the ATTACK rows, a stone-headed war club.
```

### 2.3 Dakota (Sioux) man — the parkland and grassland south:DONE

```
A Dakota Sioux man of the grasslands in 1811: buckskin leggings and breechcloth, bare-chested in the warmer poses with a buffalo robe over one arm, a breastplate of long bone hair-pipes across the chest, moccasins with geometric lane-stitch beadwork, hair in two wrapped braids with a single feather worn flat at the back of the head, a pipe bag hanging at the hip. Carries a bow; for the ATTACK rows, a gunstock war club.
```

### 2.4 Dene (Chipewyan) man — the barrens, north and west: DONE

```
A Chipewyan Dene man of the barren grounds in 1811, dressed for cold: a caribou-hide tunic with a distinctive pointed tail front and back, caribou leggings with the fur turned in, caribou mitts on a cord round the neck, high moccasin-boots, the hide pale and smoke-smudged with simple red ochre edging and porcupine quill bands, hair long and loose. Carries a bow and a long-handled ice chisel; for the ATTACK rows, a copper-bladed knife.
```

### 2.5 Plateau man — the western mountains, beyond the Rockies: DONE

```
A Plateau man (Secwepemc or Ktunaxa) of the mountain interior in 1811: a hide tunic and leggings cut plainer than the plains styles, a woven hat of spruce root with a flat top, a robe of woven rabbit-fur strips worn over the shoulders, moccasins with a soft sole, hair gathered and tied simply. Carries a bow; for the ATTACK rows, a stone-headed club.
```

### 2.6 Coast man — the Pacific shore: DONE

```
A Northwest Coast man (Nuu-chah-nulth or Coast Salish) in 1811: bare-legged, wearing a cedar-bark cape and a woven conical cedar hat with a bulbous top, a woven robe with black and red formline design at the border, a large abalone-shell ornament at the neck, hair long and loose, face with a band of red ochre across the eyes. Carries a bow; for the ATTACK rows, a carved wooden club shaped like a fish.
```

### 2.7 Missouri River man — the earth-lodge villages, far south: DONE

```
A Mandan or Hidatsa man of the upper Missouri earth-lodge villages in 1811: buckskin leggings and breechcloth, a buffalo robe worn over one shoulder painted with a feathered circle, a bone hair-pipe choker, moccasins with simple beadwork, hair long and worn forward over the shoulders with a bone ornament. He is a trader more than a raider - draw the IDLE FRONT pose with one hand open at the waist as if offering. Carries a bow; for the MUSKET rows, an old Spanish trade gun.
```

### 2.8 Women's sheets (second pass): NOT DONE ADD TO NEXT ROUND WITH EXPLICIT INSTRUCTIONS

Once the men read correctly, each nation needs a woman. Use the row list from `cree_woman`:

```
Rows: IDLE FRONT, IDLE BACK, IDLE SIDE, WALK FRONT (4), WALK BACK (4), WALK SIDE (4), EAT (4), DRINK (4), CARRY WOOD (4, a bundle of firewood on her back held by a tumpline across the forehead), SCRAPE (4, kneeling, scraping a hide stretched on a frame with a bone tool), DEATH (4).
```

Dress each as her nation's man above, but in a long hide or trade-cloth dress to mid-calf, with the nation's own beadwork, and hair in two braids.

---

---

## Priority 3: York Factory buildings

The fort is still built from Kenney's European stone tiles — the one large piece of stock art left in the world. `StructurePainter` builds each wall from a 128×256 image standing on one edge of a 128×64 ground diamond.

Gemini is poor at exact tiles, so this goes in two steps: a reference painting to fix the look, then tiles drawn against it. Expect to redraw the tiles by hand to fit; the painting is the part that pays off. Step 1 may already have been run — nothing from it is in the repo.

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

---

## Priority 4: effects and weather:DONE

The game simulates a great deal that is never drawn: `Core/Weather.cs` has Clear, Overcast, Snow, Blizzard, Rain and Storm, but `World/WorldAtmosphere.cs` only tints the scene with a `CanvasModulate`. Fire, gunfire and wounds are all lights and rectangles. Nothing here exists in the repo yet.

Use the EFFECTS HEADER for all four.

### 4.1 Fire and smoke: DONE

Attach `assets/tiles/props/campfire.png`:

```
Rows, each frame about 128 pixels wide and 160 tall:
1. SMALL FIRE (6) - a campfire burning low and steadily, looping.
2. LARGE FIRE (6) - the same fire grown to engulf a wall, flames leaning left, looping.
3. SMOKE (6) - a column of grey-white woodsmoke rising and thinning, looping.
4. EMBERS (4) - a bed of dying orange coals with no flame.
5. SPARKS (4) - a scatter of small orange sparks flying up and out.
```

### 4.2 Weather overlays: DONE

These tile seamlessly across the whole screen, so ask for them separately and expect to fix the seams by hand. No reference attachment:

```
Rows, each frame exactly 256 by 256 pixels and seamlessly tileable left-to-right and top-to-bottom:
1. SNOW LIGHT (4) - sparse small white flakes drifting down and slightly left. : DOne
2. SNOW HEAVY (4) - dense white flakes falling faster.: DOne
3. BLIZZARD (4) - dense flakes driven almost horizontally left, with streaks.
4. RAIN (4) - thin pale grey diagonal streaks.
5. FOG (4) - a soft uneven band of pale grey haze, slowly shifting.
```

### 4.3 Combat effects:DONE 

Attach `assets/external/pioneerpixel_hbc_trader/HBc man with musket.jpg`:

```
Rows, each frame about 96 pixels square unless noted:
1. MUZZLE FLASH (3) - the flash and white powder smoke from a flintlock musket firing to the right, the smoke billowing forward.
2. PAN FLASH (2) - the small bright flash at the lock of a flintlock, by the shooter's hand.
3. BLOOD HIT (4) - a small burst of dark red droplets.
4. BLOOD POOL (3) - a dark red stain spreading on the ground, each frame larger, drawn as a flat isometric diamond shape about 96 by 48 pixels.
5. IMPACT DUST (4) - a small puff of pale grey dust and grit.
6. ARROW IN FLIGHT (1) - a single arrow seen side-on, about 48 pixels long.
```

### 4.4 Night sky (optional, for the day/night cycle): DONE

```
One wide image 1920 by 400 pixels, seamlessly tileable left to right: the aurora borealis over a dark sub-arctic night sky, pale green and faint rose curtains hanging in vertical folds, stars showing through, the bottom edge fading to fully transparent black. Hand-painted pixel art, no blur, no text.
```

---

---

## Priority 5: the interface

The game has no theme and no font. Every panel is Godot's default grey, and six places fall back to `ThemeDB.FallbackFont` (`MapPanel`, `TravelMode`, `Wayfinding`, `NpcSpawn`, `PostSign`, `WildlifeSite`). With the world art finished, this is the most visible unfinished thing in the game.

### 5.1 Fonts (not a Gemini job)

Gemini cannot produce a usable font file. Pick two free, properly licensed faces and drop them in `assets/ui/fonts/`:

- **A display face** for headings, the menu and signs — something with an early-19th-century printed feel. *IM FELL English* (OFL) or *EB Garamond* (OFL) both suit.
- **A body face** for every panel, journal entry and label — must stay readable at 14 px. *Alegreya* (OFL) or plain *Source Serif* (OFL). Avoid a pixel font; the art is hand-painted, not 8-bit.

Then build one `assets/ui/theme.tres` and set it on the root control, so the six `ThemeDB.FallbackFont` call sites can take the theme font instead.

### 5.2 Panel and frame art:DONE 

ITEM HEADER, attach `assets/ui/menu_background.jpg` and `assets/sprites/items/journal.png`:

```
Interface pieces for a game set in the fur trade in 1811, to be cut into a nine-slice frame. Everything drawn so it can stretch without the corners distorting:
1. Panel - a rectangle about 400 by 300 pixels of aged parchment, slightly foxed and stained at the edges, with a thin dark brown ruled border inset about 8 pixels from the edge, and a small reinforced corner at each of the four corners.
2. Panel dark - the same rectangle in dark smoke-stained wood planking with iron corner straps, for the heads-up display.
3. Button - a small rectangle about 160 by 40 pixels of tan leather with a stitched border, drawn three times: at rest, pressed (darker, border pushed in), and disabled (grey and flat).
4. Scrollbar - a narrow vertical strip about 16 by 200 pixels of dark wood, and separately a brass slider about 16 by 48 pixels.
5. Divider - a horizontal rule about 300 pixels wide: a thin double line with a small diamond at the centre.
6. Tab - a small parchment tab about 120 by 32 pixels, drawn at rest and selected.
```

### 5.3 Cursors: Done

```
Mouse cursors, each drawn on magenta, each exactly 32 by 32 pixels, the active point at the very top-left of the image:
1. Default - a goose quill pen, nib at the top-left.
2. Talk - the quill with a small speech scroll.
3. Trade - the quill with two small coins.
4. Attack - the quill replaced by a small crossed knife and tomahawk.
5. Busy - a small hourglass with sand running.
```

---

---

## Priority 5.5: the food that answers the scurvy, and the Missouri towns: DONE

Added 2026-10-11 against commit `b488764`. Berries, spruce beer and corn are in the code and working — berries and spruce beer are what keeps the country out of the scurvy, corn is the staple of the plains trade — but all three borrow another item's icon, and the farming towns that grow the corn are drawn as plains tipis, which is simply wrong.

**5.5.1 Three item icons.** Attach `assets/sprites/items/pemmican.png` and `assets/sprites/items/ale_tankard.png`. ITEM HEADER, then:

```
A sheet of three inventory icons, in this order: BERRIES - a loose handful of mixed wild berries of the Hudson Bay country, dark blue-purple saskatoons, red lowbush cranberries and orange-red rosehips together, a few green leaves, as if just tipped from a birchbark basket; CORN - three braided ears of dried Indian corn in mixed colours (yellow, dark red, blue-black, speckled) tied at the husks and hanging together, with a small open bag of loose shelled kernels beside them; SPRUCE BEER - a squat wooden keg bound with two iron hoops, its bung in, a sprig of fresh spruce tips laid against the base, and a horn cup half full of cloudy pale-green beer beside it. The spruce beer must not read as the tankard of ale in the reference: it is a keg and a horn cup, not a pewter tankard.
```

**5.5.2 Berry bushes, as world props.** Attach `assets/tiles/props/woodpile.png` and `assets/tiles/props/campfire.png`. PROP HEADER, then:

```
A sheet of eight low shrub props for the Hudson Bay lowlands and the parkland, each standing alone on its own isometric ground diamond, each about 70 pixels tall: SASKATOON BUSH IN FRUIT - a waist-high grey-green shrub hung with clusters of dark purple-blue berries; SASKATOON BUSH PICKED - the same shrub with the berries gone, a few stalks left; CRANBERRY PATCH IN FRUIT - a low creeping mat of small dark glossy leaves over moss with scattered bright red berries sitting on top; CRANBERRY PATCH PICKED - the same mat, no fruit; ROSEHIP BUSH IN FRUIT - a thin thorny shrub with scarlet hips and a few late leaves; ROSEHIP BUSH BARE - the same shrub in winter, hips shrivelled dark, thorns showing; BUSHES UNDER SNOW - one of the shrubs above as a snow-covered mound with a few dark twigs through the crust; BIRCHBARK BERRY BASKET - a small sewn bark basket set on the ground, three-quarters full of mixed berries, as left by a picker.
```

**5.5.3 A Mandan earth-lodge village.** The Missouri towns are the market of the plains and the one farming culture in the game. Attach `assets/tiles/props/tipi.png` and `assets/tiles/buildings/wall_N.png`. PROP HEADER, then:

```
A sheet of nine world objects for a Mandan or Hidatsa village on the upper Missouri in 1811, all in the same isometric projection and scale as the reference, a man being about 110 pixels tall: EARTH LODGE - a round dome-shaped dwelling about twelve metres across and four high, a heavy timber frame covered in willow mats, grass and then packed earth and sod with grass growing on it, a square smoke hole at the apex with a hide flap, a short covered entry passage facing the river, a bullboat propped against the wall; EARTH LODGE IN WINTER - the same lodge with snow on the dome and a thread of smoke from the smoke hole; CORN DRYING SCAFFOLD - a rectangular frame of peeled poles about two metres high hung thick with braided ears of yellow, red and blue-black corn, squash rings threaded on cords beneath; GARDEN PLOT IN SUMMER - a worked bottomland plot of hilled corn in rows about shoulder high with climbing beans up the stalks and squash sprawling between the hills; GARDEN PLOT HARVESTED - the same plot cut to stubble with the hills showing and a few dry stalks; CACHE PIT - a round pit lined with grass and bark, its wooden cover half off, dried corn in bags inside; VILLAGE PALISADE SECTION - a row of close-set upright split posts about three metres high with a shallow dry ditch along the outside; BULLBOAT - a round tub-shaped boat of a single buffalo hide stretched over a willow frame, about a metre and a half across, a single short paddle laid across it; CORN MORTAR - a hollowed log standing on end with a long wooden pestle resting in it and a bark tray of shelled corn beside it.
```

---

## Priority 5.7: the buffalo, which the code now has

Added 2026-10-11. `Species.Buffalo` is in `Ecology/Species.cs` with its own yield (seventy pieces of meat and a robe off one beast), the plains camps hunt it before they hunt caribou, and `ItemId.BuffaloRobe` is a trade good. **It is drawn as a caribou.** That is the one asset standing between the plains and being a real part of the game, so it comes before everything else in Priority 6.

Attach a finished frame from `assets/sprites/animals/caribou/` and `Cree man with bow.jpg`. ANIMAL HEADER, then:

```
A sprite sheet of a North American plains buffalo (bison) in 1811, in the same isometric three-quarter view and pixel style as the reference, drawn large: a bull stands six feet at the shoulder and is roughly one and a half times the height and three times the bulk of the 110-pixel man in the reference. Dark brown woolly cape over the humped shoulders and forelegs, shorter dark coat behind, a heavy bearded head carried low, short black upcurved horns, a tufted tail. Rows, each labelled: IDLE (4 frames, standing, head low, grazing and lifting the head); WALK (6 frames, side, a heavy slow plod); RUN (6 frames, side, a full gallop with the hump pitching); CHARGE (4 frames, head down, horns forward); DEATH (5 frames, from hit, to staggering, to down on the side); and three single frames beside them: A COW, lighter and smaller with no shoulder cape; A CALF, reddish-brown; and a BUFFALO ROBE, a dressed winter hide with the hair on, folded in the way a trade robe was folded, as an inventory icon.
```

---

## Priority 6: animals the code does not have yet

Do these only once `Ecology/Species.cs` grows past its four species — there is no point drawing an animal the simulation cannot spawn. (The hare and ptarmigan sheets are already drawn and waiting on exactly that; see the wiring backlog in `asset-inventory.md`.)

moose, black bear, polar bear (it walks the shore at York Factory in autumn), red fox, arctic fox, marten, mink, muskrat, otter, lynx, wolverine, bison and pronghorn (plains), sled dogs. ANIMAL HEADER, the same row set as the wolf (IDLE, WALK, RUN, ATTACK if it fights, DEATH), and give its size against a 110-pixel man. Attach `Cree man with bow.jpg` and a finished frame from `assets/sprites/animals/wolf/`.

---

## Summary checklist

| # | Asset | Why | Attach |
|---|---|---|---|
| ~~1~~ **cut and wired 2026-10-11** | **Factor sheet** | last stand-in; still on the old 150×130 `rival` sheet | `grey_trader_8way.jpg`, `clerk/idle_front.png` |
| ~~2.1-2.7~~ **cut and wired 2026-10-11** | Seven nations' men (Ojibwe, Blackfoot, Dakota, Dene, Plateau, Coast, Missouri) | ~700 people on the map with no art of their own | `Assainboine Man.jpg`, `assiniboine/idle_front.png` |
| 2.8 | The same seven nations' women | second pass | as above + `cree_woman/idle_front.png` |
| 3 | York Factory painting, then log wall and palisade tiles | fort is still Kenney European stone | `props/tipi.png`, `buildings/wall_*.png` |
| 4.1 | Fire, smoke, embers, sparks | fire is a tinted campfire plus a light | `props/campfire.png` |
| 4.2 | Snow, blizzard, rain, fog overlays | six `SkyState`s, none drawn | none |
| 4.3 | Muzzle flash, blood, impact, arrow | combat is a `PointLight2D` | `HBc man with musket.jpg` |
| 4.4 | Aurora band | night is a flat tint | none |
| 5.1 | Two licensed fonts | no font in the repo | *not a Gemini job* |
| 5.2 | Panel, button, scrollbar, divider, tab | no theme; default Godot grey | `menu_background.jpg`, `items/journal.png` |
| 5.3 | Five cursors | system cursor | none |
| ~~5.5.1~~ **cut and wired 2026-10-11** | Berries, corn, spruce beer icons | all three in code since `b488764`, all three borrowing another item's icon | `items/pemmican.png`, `items/ale_tankard.png` |
| ~~5.5.2~~ **cut and wired 2026-10-11** | Berry bushes in fruit, picked, bare and under snow | berries are the main answer to the scurvy and there is nothing to see or pick | `props/woodpile.png`, `props/campfire.png` |
| ~~5.5.3~~ **cut and wired 2026-10-11** | Mandan earth lodges, corn scaffolds, gardens, bullboats, palisade | the farming towns of the Missouri are drawn as plains tipis | `props/tipi.png`, `buildings/wall_N.png` |
| 5.7 | **Buffalo** (idle, walk, run, charge, death, cow, calf, robe) | `Species.Buffalo` is in the code and hunted; it is drawn as a caribou | a `animals/caribou/` frame + `Cree man with bow.jpg` |
| 6 | Moose, bear, fox, marten, lynx, muskrat, sled dogs | only when `Species.cs` grows | `Cree man with bow.jpg` + a wolf frame |

**Not covered here: ground tiles.** The terrain comes from a streamed world map with twelve ground types (`World/WorldMap.cs`: ocean, lake, river, ford, boreal, tundra, shield, parkland, grassland, tall grass, lakes forest, cordillera). That work is in progress in another session, and seamless isometric tiles are the one thing Gemini reliably gets wrong. Better drawn once the tile shapes have settled.

**Also not here: anything already drawn.** The characters, animals, items, props, vehicles, menu art, need icons and crests made in the 2026-10-10 pass were removed from this file on 2026-10-11. `asset-inventory.md` catalogues them, including the ~150 finished files the game does not yet load.
