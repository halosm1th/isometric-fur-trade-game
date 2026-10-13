# Asset Prompts for Gemini

Last verified against the repo: 2026-10-13

Art the game still needs, as prompts ready to paste into Gemini, with the sheet to attach as a style reference for each. First written 2026-10-10; pruned 2026-10-11 against commit `b2064f4` and again **2026-10-13 against `1c73b55`**: everything drawn since (the Factor, all seven nations' men, the York Factory log walls and palisade, the berry, corn and spruce-beer icons, berry bushes, the Missouri earth-lodge set) was deleted from this file rather than left here marked done. What remains is what is still missing, plus a short list of sheets that were drawn but still need cutting or redoing.

For what the game already has — and for finished art that exists but nothing loads yet, including the effects, panel, cursor and night-sky sheets that came back from Gemini but have not been cut — see `asset-inventory.md`. Sound is in `audio-prompts.md`. Nations, people and places missing from the *code* are in `world-content-needed.md`.

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

## Priority 1: the women of the seven nations

All ten nations' **men** are drawn and wired (`Appearance.ForNation`). Their women are not: `ForNation(f, female: true)` returns `CreeWomanFrames` for every nation except the Métis, so a Blackfoot, Dakota, Dene, Plateau, Coast, Ojibwe or Missouri woman currently wears a Cree woman's dress. (The Assiniboine have no women's sheet either and use the Cree one.) This is the largest remaining gap in the characters.

Use the CHARACTER HEADER. Attach `assets/external/pioneerpixel_hbc_trader/Cree WOman.jpg`, `assets/sprites/cree_woman/idle_front.png` (for the 180×170 scale) and the matching man's `idle_front.png` (`assets/sprites/<nation>/idle_front.png`, so the dress and beadwork agree). Rows, matching `cree_woman` so `CreeWomanFrames.tres` can be copied and re-pathed:

```
Rows: IDLE FRONT, IDLE BACK, IDLE SIDE, WALK FRONT (4), WALK BACK (4), WALK SIDE (4), EAT (4), DRINK (4), CARRY WOOD (4, a bundle of firewood on her back held by a tumpline across the forehead), SCRAPE (4, kneeling, scraping a hide stretched on a frame with a bone tool), DEATH (4).
```

Then one sheet per nation, each dressed as that nation's man (see his sheet) but in a long hide or trade-cloth dress to mid-calf, with the nation's own beadwork or ornament, hair in two braids unless the nation's custom differs: **Blackfoot** (painted hide dress with elk-tooth or bead yoke), **Dakota** (long hide dress with a deep beaded yoke), **Dene** (caribou-hide dress with quillwork bands), **Plateau** (plain hide dress, woven spruce-root cap), **Coast** (cedar-bark cape and conical cedar hat, woven robe), **Ojibwe** (trade-cloth dress with floral beadwork, blue wool blanket), **Missouri / Mandan** (hide dress, bone hair-pipe choker). An optional eighth sheet for the **Assiniboine** woman would fill the same gap.

---

## Priority 2: the interface

The code builds one dark-wood-and-brass theme (`UI/MenuTheme.cs`) for the menus and settings; there is no font and no `theme.tres`, the in-world panels are Godot's default grey, and six places fall back to `ThemeDB.FallbackFont` (`MapPanel`, `TravelMode`, `Wayfinding`, `NpcSpawn`, `PostSign`, `WildlifeSite`).

### 2.1 Fonts (not a Gemini job)

Gemini cannot produce a usable font file. Pick two free, properly licensed faces and drop them in `assets/ui/fonts/` (the folder does not exist yet):

- **A display face** for headings, the menu and signs — something with an early-19th-century printed feel. *IM FELL English* (OFL) or *EB Garamond* (OFL) both suit.
- **A body face** for every panel, journal entry and label — must stay readable at 14 px. *Alegreya* (OFL) or plain *Source Serif* (OFL). Avoid a pixel font; the art is hand-painted, not 8-bit.

Then build one `assets/ui/theme.tres` (or extend `MenuTheme`) and set it on the root control, so the six `ThemeDB.FallbackFont` call sites can take the theme font instead.

### 2.2 Panel and cursor art: drawn, not cut

`NEW HBC ASSETS/Panels.jpg` and `NEW HBC ASSETS/Mouse cursors.jpg` came back from Gemini. Neither is sliced into `assets/ui/` or wired. This is a slicing job (extend `tools/sprites/ui.py`), plus a check that the panel pieces nine-slice cleanly. Only if they do not should the old prompts be reused; they are in git history (`docs/asset-prompts.md` before 2026-10-13, section 5.2 and 5.3).

---

## Priority 3: effects and weather: drawn, partly needs redoing

The sheets exist as raw Gemini output in `assets/external/NEW HBC ASSETS/`: `fire.jpg`, `light snow.jpg`, `Heavy snow.jpg`, `blizzard.jpg`, `rain.jpg`, `fog.jpg`, `Combat effects in theory.jpg`, `Night Sky.jpg`. None is cut or wired (`WorldAtmosphere.cs` still only tints; `Blaze.cs` still tints `campfire.png`). Only the following needs a new Gemini pass; everything else is a slicing job (`asset-inventory.md`, wiring backlog 12).

**Redo: LARGE FIRE row.** In `fire.jpg` the large fire sits on a baked-in stone wall instead of magenta, so it cannot be cut. Use the EFFECTS HEADER, attach `assets/tiles/props/campfire.png` and `NEW HBC ASSETS/fire.jpg`:

```
One row, LARGE FIRE (6 frames, each about 128 pixels wide and 160 tall): the same fire grown to engulf a log wall, flames leaning left, looping. The flames only, drawn on the flat magenta background: no wall, no stones, no ground behind or under them.
```

Not opened or judged when this was checked: the weather, night-sky, panel and cursor sheets. If any came back with a checkerboard, a grid or a baked-in scene, redo it with the reply from "How to use this" step 4. The combat sheet has a copyright line along its bottom edge (crop it out).

---

## Priority 4: the buffalo, which the code has

`Species.Buffalo` is in `Ecology/Species.cs` with its own yield (seventy pieces of meat and a robe off one beast), the plains camps hunt it before they hunt caribou, 18 herds graze the plains, and `ItemId.BuffaloRobe` is a trade good. **It is still drawn as a caribou** (`Species.cs` points `Buffalo` at `CaribouFrames.tres`) and the robe's inventory icon is the caribou hide's. No buffalo sheet exists in `assets/`, so this is the biggest outstanding piece of creature art.

Attach a finished frame from `assets/sprites/animals/caribou/` and `Cree man with bow.jpg`. ANIMAL HEADER, then:

```
A sprite sheet of a North American plains buffalo (bison) in 1811, in the same isometric three-quarter view and pixel style as the reference, drawn large: a bull stands six feet at the shoulder and is roughly one and a half times the height and three times the bulk of the 110-pixel man in the reference. Dark brown woolly cape over the humped shoulders and forelegs, shorter dark coat behind, a heavy bearded head carried low, short black upcurved horns, a tufted tail. Rows, each labelled: IDLE (4 frames, standing, head low, grazing and lifting the head); WALK (6 frames, side, a heavy slow plod); RUN (6 frames, side, a full gallop with the hump pitching); CHARGE (4 frames, head down, horns forward); DEATH (5 frames, from hit, to staggering, to down on the side); and three single frames beside them: A COW, lighter and smaller with no shoulder cape; A CALF, reddish-brown; and a BUFFALO ROBE, a dressed winter hide with the hair on, folded in the way a trade robe was folded, as an inventory icon.
```

(The ANIMAL HEADER asks for two directions, DOWN-RIGHT and UP-RIGHT; the sheet above is single-row side views, so say "down-right and up-right" for each row if the slicer needs the four-way layout the other animals use.)

---

## Priority 4b: the horse, the travois and the Red River cart, which the code has

Horses are in the code (`Economy/Herd.cs`, `Economy/Plains.cs`, `Agents/Mount.cs`): every plains camp and every parkland post owns a herd, hunters ride out to the buffalo and the communal run is ridden, raiders cut horses out of each other's herds at night, the player buys, borrows, rides and loses them, and the Métis haul their pemmican to the posts in carts. **There is no horse art at all**: a horse is drawn as the caribou sheet tinted to a horse's coat, and the rider is the walking figure lifted onto its back. The code loads `Scenes/Animals/HorseFrames.tres` the moment it exists, with the rows `idle_*`, `walk_*`, `run_*` and `graze_*` in the four directions `se`, `ne`, `sw`, `nw` (the slicer mirrors `se`/`ne` into `sw`/`nw`), and stops tinting it.

Attach a finished frame from `assets/sprites/animals/caribou/`, `Cree man with bow.jpg` and one of the newer men (`assets/sprites/blackfoot/idle_front.png`) for scale. ANIMAL HEADER, then one sheet each:

**The horse, bare and saddled** (the main sheet):

```
A sprite sheet of an Indian pony of the northern plains in 1811, in the same isometric three-quarter view and pixel style as the reference: a small, tough, short-backed horse about fourteen hands, its back level with the chest of the 110-pixel man in the reference, a heavy mane and tail, unshod. Draw it with a pad saddle of buckskin stuffed with buffalo hair and a single rawhide rein looped round the lower jaw - the way the plains peoples rode - and nothing else. Rows, each labelled, each in two directions DOWN-RIGHT and UP-RIGHT: IDLE (4 frames, standing, ear and tail flicks); GRAZE (4 frames, head down cropping grass, lifting it once); WALK (6 frames); RUN (6 frames, full gallop, mane and tail streaming); DEATH (5 frames, going down on its side). Then the same IDLE row once more in each of four coats so the herd is not all one horse: a BAY, a GREY, a BLACK-AND-WHITE PAINT and a BUCKSKIN DUN.
```

**The horse ridden** (so the rider sits in the saddle instead of standing on the horse's back):

```
The same pony as the first sheet, now ridden by a plains hunter in breechcloth, leggings and a buffalo-hide shirt, his legs down either side of the pad saddle, a bow cased at his back, holding the rein in his left hand. Rows, each labelled, each DOWN-RIGHT and UP-RIGHT: RIDE IDLE (4 frames, horse standing, rider looking about); RIDE WALK (6 frames); RIDE GALLOP (6 frames, rider leaning forward); RIDE SHOOT (5 frames, at a full gallop, drawing and loosing an arrow to the right side, as in the buffalo run); and a single frame of the same pony with an empty pad saddle and the rein trailing, standing PICKETED to a stake.
```

**The travois and the Red River cart** (PROP HEADER, not the animal header):

```
Two sheets of the plains' carrying in 1811. A HORSE TRAVOIS: two long lodgepoles crossed over a pony's withers and dragging behind on the ground, a platform of netted rawhide lashed across them loaded with rolled lodge covers and parfleches; draw it hitched to the pony (WALK, 6 frames, DOWN-RIGHT and UP-RIGHT) and unhitched, lying on the ground. A RED RIVER CART: an all-wood two-wheeled Métis cart, the wheels taller than a man (about 120 pixels), dished and with twelve spokes, no iron anywhere, rawhide lashings, the box loaded with pemmican bags of buffalo hide; draw it drawn by a single ox in harness (WALK, 6 frames, DOWN-RIGHT and UP-RIGHT), standing unhitched, and as an icon-sized PEMMICAN BAG (a taut rectangular rawhide bag sewn shut, ninety pounds) for the inventory.
```

Wiring note for whoever slices these: the ridden sheet replaces both the horse and the rider while mounted, so `Mount.cs` will want a second `SpriteFrames` (`HorseRiddenFrames.tres`) and to hide the rider's own sprite when it is loaded; the plain horse sheet needs nothing but the `.tres`.

---

## Priority 5: animals the code does not have yet

Do these only once `Ecology/Species.cs` grows past its seven species (caribou, goose, beaver, wolf, hare, ptarmigan, buffalo) — there is no point drawing an animal the simulation cannot spawn.

moose, black bear, polar bear (it walks the shore at York Factory in autumn), red fox, arctic fox, marten, mink, muskrat, otter, lynx, wolverine, pronghorn (plains), sled dogs. ANIMAL HEADER, the same row set as the wolf (IDLE, WALK, RUN, ATTACK if it fights, DEATH), and give its size against a 110-pixel man. Attach `Cree man with bow.jpg` and a finished frame from `assets/sprites/animals/wolf/`.

---

## Summary checklist

| # | Asset | Why | Attach |
|---|---|---|---|
| 1 | The women of the seven nations (and the Assiniboine) | all ten nations' men are done; every nation's women wear the Cree woman's sheet | `Cree WOman.jpg`, `cree_woman/idle_front.png`, the man's `idle_front.png` |
| 2.1 | Two licensed fonts | no font in the repo | *not a Gemini job* |
| 2.2 | Panels and cursors | drawn (`Panels.jpg`, `Mouse cursors.jpg`), not cut | slicing job |
| 3 | LARGE FIRE row, redone on magenta | drawn on a baked-in wall | `props/campfire.png`, `fire.jpg` |
| 3 | Weather, combat, aurora, fire, smoke sheets | drawn, not cut or wired | slicing job |
| 4 | **Buffalo** (idle, walk, run, charge, death, cow, calf, robe) | `Species.Buffalo` is in the code and hunted; it is drawn as a caribou | an `animals/caribou/` frame + `Cree man with bow.jpg` |
| 4b | **Horse** (bare, four coats, ridden, picketed), **travois**, **Red River cart**, pemmican bag | horses are ridden, raided and traded in the code; drawn as a tinted caribou | an `animals/caribou/` frame + `Cree man with bow.jpg` + a `blackfoot/` frame |
| 5 | Moose, bear, fox, marten, lynx, muskrat, sled dogs | only when `Species.cs` grows | `Cree man with bow.jpg` + a wolf frame |

**Delivered and removed from this file (2026-10-13):** the Factor sheet (`assets/sprites/factor/`); Ojibwe, Blackfoot, Dakota, Dene, Plateau, Coast and Mandan men; the York Factory reference painting and the whitewashed log walls and palisade (`NEW HBC ASSETS/HBc YOrk Factory PIc.jpg`, `Facotry Segment Sprites.jpg`; the same sheet's weathered set, gates, grey floor and steep roof are drawn but not cut: see the wiring backlog); the berries, corn and spruce-beer icons; the berry bushes and berry basket; the Mandan earth lodges, corn scaffolds, gardens, cache pit, bullboat, mortar and village palisade.

**Not covered here: ground tiles.** The terrain comes from a streamed world map with thirteen ground types (`World/WorldMap.cs`: ocean, lake, river, ford, boreal, tundra, shield, parkland, grassland, tall grass, lakes forest, cordillera, lowland). The regional ground tiles are still recolours of the Kenney grass (`tools/world/make_region_tiles.py`), and seamless isometric tiles are the one thing Gemini reliably gets wrong, so they wait until the tile shapes have settled.

**Also not here: anything already drawn.** `asset-inventory.md` catalogues the characters, animals, items, props, vehicles, menu art, need icons and crests, including the finished files the game does not yet load.
