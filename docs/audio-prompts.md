# Audio Prompts for Gemini

Music, ambience and sound effects the game needs, written as prompts to paste into Gemini. It's the sound version of `asset-prompts.md`. Written 2026-10-10. **Updated 2026-10-11:** the first batch is in the game: six Gemini music tracks and 27 Freesound effects, played by the `Audio` autoload in `Scripts/Core/Audio.cs`. The section **In the game now** below says what exists and when it plays. Everything else in this file is still wanted.

The game hooks each prompt is meant for are named where they exist: `SkyState` (Clear, Overcast, Snow, Blizzard, Rain, Storm) in `Core/Weather.cs`, `Season` (Winter, Breakup, OpenWater, FreezeUp) in `Core/GameCalendar.cs`, the four `Species` in `Ecology/Species.cs`, and the campaign beats in `Campaigns/historical.json`.

---

## How to use this

1. **Music: use Gemini's music generator (Lyria).** In the Gemini app, ask it to "create a music track" and paste the MUSIC HEADER, then the track prompt. Clips come back about 30 seconds long. For tracks marked **LOOP**, ask for a version whose ending leads back into its beginning, then trim the seam in Audacity.
2. **Ambience and sound effects:** Gemini's music model is poor at single sounds (a musket shot, a door). Try the SFX HEADER first. If the result sounds musical or mushy, generate the sound in a dedicated sound-effect tool with the same prompt (ElevenLabs Sound Effects, Stable Audio), or use a CC0 library (freesound.org filtered to CC0, Sonniss GDC bundles). Every prompt here works unchanged in those tools.
3. **One sound per chat.** As with the art, earlier results bleed into later ones.
4. **Export and file it.** Convert to **OGG Vorbis, 44.1 kHz** (`.ogg` loops cleanly in Godot; tick "Loop" in the import dock for LOOP files). Mono for SFX that happen at a spot in the world, stereo for music and ambient beds. Put it in `assets/audio/<music|ambient|sfx|ui>/` named as in the ID column (e.g. `assets/audio/sfx/musket_fire_01.ogg`) and tell me; I'll wire it in.
5. **Make variations.** For any sound that repeats (footsteps, axe chops, paddle strokes, hits), make 3 to 5 takes and number them `_01` to `_05`. The game picks one at random so the repetition doesn't wear on the ear.
6. If Gemini adds singing to an instrumental, reply: *"Same track, strictly instrumental, no vocals, no choir, no humming."*

### A note on Indigenous music

Cree, Assiniboine, Ojibwe and other nations' songs belong to those communities. Many are ceremonial and must not be imitated, and a generated "native chant" is the most worn cliché in frontier games. So the prompts below **never ask for imitation singing or ceremonial music**. Drums at the camp are written as plain hand-drum rhythm with no vocals and no sacred context. Before release, the better path is to license or commission real recordings from Cree or Métis musicians for the camp cues and the Métis fiddle. Treat those generated tracks as placeholders until then.

---

## In the game now (2026-10-11)

**How it works.** `Core/Audio.cs` is an autoload with three buses under Master (**Music**, **Ambience**, **Effects**), each with a slider in Settings > Sound.
- Sounds the simulation already makes reach it through `Sim.ShowNoise`, which runs on every machine in co-op: footsteps, shots, blows, axes, a fire being lit.
- Everything else it reads off the world each frame: weather, season, hour, the ship, fires, and whether you're indoors. Ambience goes muffled through a log wall, and a door sounds as you cross it.
- A sound is asked for by name: `step_snow` picks one of `step_snow_01..09` at random. **Dropping in more numbered takes needs no code.**
- Run with `-- --audiotrace` to print every sound as it starts.

The raw downloads stay in `assets/external/soundFX/` (git-ignored, and hidden from Godot by a `.gdignore`). The cut clips are in `assets/audio/`. Credits and licences are in `assets/audio/CREDITS.md`.

| Doc ID | File(s) in `assets/audio/` | When it plays |
|---|---|---|
| `mus_main_menu` | `music/claim_the_north.mp3` | Main menu, on repeat. |
| `mus_winter` | `music/the_long_winter_at_the_bay.mp3`, `music/planks_beneath_the_frost.mp3` | Winter, now and then: 20 to 45 s after you arrive, then 2 to 4 min of quiet between pieces. |
| `mus_breakup`, `mus_freezeup` | `music/waiting_on_the_ice.mp3` (freeze-up also draws on the winter tracks) | Breakup and freeze-up, same rhythm. |
| `mus_summer` | `music/boots_on_the_pine_floor.mp3` | Open water, same rhythm. |
| `mus_mens_house_reel`, `mus_mens_house_jig` | `music/mens_house_reel.mp3`, `music/boots_on_the_pine_floor.mp3` | Inside the men's house, 19:00 to 23:30. |
| `step_snow`, `step_snow_crust` | `sfx/step_snow_01..05.wav`, `sfx/step_snow_crust_01..05.wav` | Footsteps in winter (crust on clear days), early May and late October. Loudness follows how far the step carries, so sneaking is near silent. Your own steps always; other people's within earshot, a few a second at most. |
| `step_mud` | `sfx/step_mud_01..05.wav` | Footsteps the rest of the year. |
| `step_planks`, `step_boardwalk` (synth) | `sfx/step_planks_01..05.wav` | Any plank floor (indoors) and the wharf, all year. |
| `amb_rain` (synth) | `ambient/amb_rain.wav` | Rain and storms; louder under a roof. |
| `wolf_howl_01..03`, `wolf_pack_howl` (synth) | `sfx/wolf_howl_01..03.wav`, `sfx/wolf_pack_howl.wav` | At night (20:00 to 06:00), from the nearest wolf within 3,500 px, every 35 to 90 s (more often in winter); the pack howl when three or more are about. |
| `wolf_growl`, `wolf_bite` (synth) | `sfx/wolf_growl_01..02.wav`, `sfx/wolf_bite.wav` | A wolf within 420 px growls every 5 to 10 s; its bite landing. |
| `body_fall` (synth) | `sfx/body_fall_01..02.wav` | A person dying. |
| `musket_reload` (synth) | `sfx/musket_reload.wav` | 1.4 s after a shot, heard only close by. |
| `amb_winter_wind` | `ambient/amb_winter_wind.wav` | Always some wind: most in winter, more under cloud, snow or rain. |
| `amb_blizzard` | `ambient/amb_blizzard.wav` | Blizzard, and (softer) Storm. |
| `amb_river` | `ambient/amb_river.wav` | Open water within about 640 px, louder closer. Silent when frozen. |
| `mosquitoes_close` | `ambient/amb_mosquitoes.wav` | Open water and June, by day, outdoors, clear or cloudy. |
| `amb_fort_yard_day` (stand-in) | `ambient/amb_fort_chatter.wav` | A murmur of voices at a post, louder the more waking people are near you. |
| `amb_camp_cree` (part) | `ambient/amb_kids_playing.wav` | Children at play round the nearest band camp, 08:00 to 20:00, not in rain or worse. |
| `fire_hearth_loop` | `ambient/fire_hearth_loop.wav` | The three nearest burning hearths, at their places, louder with more fuel. |
| `fire_building_loop` (stand-in) | same file, louder | A building on fire (`World/Fire.cs`). |
| `fire_light` | `sfx/flint_strike.mp3` | A fire lit with flint (`Hearth.Light`). |
| `brigade_paddling_loop` (stand-in) | `ambient/canoe_paddling_loop.wav` | You paddling a canoe. |
| `ship_creak_loop` | `ambient/ship_creak_loop.mp3` | Near the ship while it's in. |
| `ship_cannon_salute`, `ship_bell` | `sfx/cannon_distant.wav` twice, `sfx/fort_bell_work.wav` | The ship standing in: her gun, the fort's answer, the bell. The bell again when she sails. |
| `fort_bell_work` | `sfx/fort_bell_work.wav` | 07:00 and 18:00 near the fort, unless the weather is foul. |
| `fort_bell_alarm` | `sfx/fort_bell_alarm.wav` | A fire starting near the fort. |
| `musket_fire_01..03` | `sfx/musket_fire.wav` | Any musket shot. |
| `musket_distant` | `sfx/cannon_distant.wav` | Added under a shot more than 1,400 px away. |
| `punch_01..05` | `sfx/punch_01..02.wav` | A melee blow landing (fists, knife, tomahawk), from people, not animals. |
| `axe_chop_01..05` | `sfx/axe_chop_01..05.wav` | Someone cutting firewood (`ChopWoodGoal`): four strokes. |
| `tree_fall` | `sfx/tree_fall.wav` | The tree coming down. |
| `ice_crack_boom` | `sfx/ice_crack_01..03.wav` | Three booms when winter sets in and when breakup starts. |
| `door_open_01..03` | `sfx/door_open.mp3` | Going indoors or out. |
| `dogs_*`, `sled_run_loop` (part) | `sfx/dogsled_pass.wav` | Now and then on winter days near the fort. |
| `geese_flock_overhead` | `sfx/goose_honk.mp3` (2 to 5 voices) | Breakup and freeze-up days. |
| `raven_croak_01..03` | `sfx/raven_croak.ogg` | Winter and freeze-up days. |
| `loon_call` | `sfx/loon_call.ogg` (a 7 s stretch) | Summer evenings and nights near water. |
| `ui_page_turn_01..03` | `ui/ui_page_turn.ogg` | Opening or closing the journal, log or pack. |
| (unused yet) | `ui/ui_quill_write.mp3`, `sfx/wood_creak.wav` | On disk but nothing calls them yet. The quill wants a letter-writing screen; the creak could be a bunk or a shutter. |

Rows marked **(synth)** are stand-ins generated in code; a real recording under the same name replaces them.

**Most wanted next**, in order:
1. Real recordings for the synth rows, wolves first: they're the weakest imitation.
2. More takes of the sounds that have only one: musket, punch, door, goose, raven.
3. Footsteps on dirt and river ice.
4. `amb_summer_day` and `amb_freezeup` beds.
5. `grunt_hurt`, so a blow lands with a voice.
6. UI clicks and `ui_notice`.
7. The tension cues and stingers in 6.4.

---

## Headers (paste one of these first)

### MUSIC HEADER

```
Compose an instrumental music track for a historical video game set at York Factory, a Hudson's Bay Company trading post on the frozen shore of Hudson Bay, in 1811. Use only instruments that could plausibly be heard in a fur-trade post or on the rivers of Rupert's Land in 1811: fiddle, wooden flute or tin whistle, Scottish small pipes or a lone bagpipe drone, bodhran or frame drum, a plucked or bowed cello as a drone, jaw harp, hand claps, foot stomping on wooden planks, spoons, a church-style harmonium, sparse solo piano only where the brief says so. No synthesizers, no electric guitar, no drum kit, no orchestral trailer hits, no epic choir, no reverb wash, no modern pop production. Recorded close and dry, as if played in a log room. Instrumental only: no vocals, no humming, no chanting unless the brief below explicitly asks for singing. Mood, tempo and length follow the brief below.
```

### AMBIENT HEADER

```
Create a seamless looping ambient soundscape for a historical video game set on the Hudson Bay lowlands in 1811 (subarctic coast, spruce and tamarack muskeg, wide brown rivers, a wooden trading fort). Realistic field-recording style, no music, no melody, no tonal drones, no voices or speech, no modern sounds (no engines, aircraft, traffic, electrical hum). Stereo, 60 seconds, with the same level all the way through and no fade in or fade out, so the end joins back onto the start without a seam. No single sound so loud or distinctive that the loop becomes noticeable when it repeats.
```

### SFX HEADER

```
Create a single isolated sound effect for a historical video game set at a Hudson's Bay Company fur-trade post in 1811. Realistic, recorded close and dry like a foley recording, no music, no background ambience, no room reverb unless the brief says indoors, no voices unless the brief asks for them. Clean silence before and after the sound, trimmed tight: starts within 0.05 seconds, no long tail. Mono. Length as given below.
```

---

## Priority 1: the sounds you hear every minute

These carry the most weight: the game feels dead without them.

### 1.1 Footsteps (SFX, 5 takes each, ~0.3 s)

| ID | Prompt (after SFX HEADER) |
|---|---|
| `step_snow_01..05` | A single footstep in a soft leather moccasin on deep dry powder snow at minus 30: a soft squeaky crunch. |
| `step_snow_crust_01..05` | A single moccasin footstep breaking through a wind crust on old snow: a brittle crack and then a crunch. |
| `step_snowshoe_01..05` | A single stride on wooden-framed, rawhide-laced snowshoes on deep snow: a soft whump, a slight creak of the lacing, the tail dragging. |
| `step_planks_01..05` | A single footstep in a leather shoe on a thick, old, slightly loose wooden plank floor indoors: a hollow knock with a small creak. |
| `step_boardwalk_01..05` | A single footstep on an outdoor wooden boardwalk raised over wet ground: hollow, slightly springy. |
| `step_mud_01..05` | A single moccasin footstep in wet muskeg mud and moss: a squelch and suck. |
| `step_dirt_01..05` | A single footstep on packed dry earth and gravel. |
| `step_ice_01..05` | A single moccasin footstep on bare river ice dusted with snow: a hard, slightly hollow tap and a faint scrape. |
| `step_shallows_01..05` | A single footstep wading ankle-deep in cold river water over stones. |

### 1.2 Ambient beds by season and weather (AMBIENT, 60 s, LOOP)

The game crossfades between these by `Season` and `SkyState`.

| ID | When | Prompt (after AMBIENT HEADER) |
|---|---|---|
| `amb_winter_calm` | Winter, Clear | Deep subarctic winter, minus 35, perfectly still air. Almost silent: a very faint high wind in the spruce tops, the occasional crack of a tree splitting in the frost, a distant raven croak every 20 seconds or so. Vast and empty. |
| `amb_winter_wind` | Winter, Overcast or Snow | A steady moderate wind across open snow and frozen muskeg, hissing drifting snow, gusts moaning through a log palisade and stunted spruce. Cold and lonely, not stormy. |
| `amb_blizzard` | Blizzard | A full Hudson Bay blizzard: a roaring, shrieking wind with heavy gusts, snow hammering against log walls, a loose shutter rattling now and then. Dangerous and overwhelming. |
| `amb_blizzard_indoors` | Blizzard, inside a building | The same blizzard heard from inside a thick-walled log house: the wind muffled and booming, the walls creaking, a fire crackling quietly close by. Safe but besieged. |
| `amb_breakup` | Breakup | Late May on a big subarctic river during ice breakup: grinding, groaning and booming ice floes, rushing meltwater, dripping eaves, flocks of geese calling as they pass high overhead, the first songbirds. |
| `amb_summer_day` | OpenWater, Clear | A short subarctic summer afternoon by a wide brown river: steady clouds of mosquitoes and blackflies whining, a light breeze in willows and tamarack, the river lapping, gulls and terns far off, the odd white-throated sparrow. |
| `amb_summer_night` | OpenWater, night | A subarctic summer night that never gets fully dark: frogs and toads in the muskeg, mosquitoes, a loon calling far off across the water, a quiet river. |
| `amb_freezeup` | FreezeUp | A raw grey October day on the Bay coast: a cold wind off the sea, waves on a shingle shore, skeins of geese calling overhead heading south, dry grass hissing. Restless and urgent. |
| `amb_rain` | Rain | Steady cold rain on wet ground, moss and a shingled roof, water running off eaves into puddles. |
| `amb_storm` | Storm | A summer thunderstorm over flat muskeg: heavy rain, rolling distant thunder, gusting wind. |
| `amb_shore` | Near the coast | Small grey waves breaking on the flat tidal mudflats and gravel of the Hudson Bay shore, gulls, a wide-open sea wind. |
| `amb_river` | Near a river | A wide, slow, cold river: a steady low rush with small eddies lapping against a muddy bank. |
| `amb_forest` | In the bush | Inside dense spruce forest in autumn: wind high in the treetops, very little at ground level, a woodpecker tapping, a red squirrel chattering, a grey jay. |

### 1.3 Inside the fort (AMBIENT, 60 s, LOOP)

| ID | Prompt (after AMBIENT HEADER) |
|---|---|
| `amb_fort_yard_day` | The yard of a busy wooden fur-trade fort on a working day, heard from the middle: an axe splitting firewood in the distance, a cooper hammering on a barrel, a sled dog barking, a door slamming, wood creaking, footsteps on boardwalks. Indistinct human murmur only, no words. |
| `amb_mens_house` | Inside a crowded log bunkhouse in winter at night: a stove ticking and crackling, men snoring, a cough, a bunk creaking, wind outside. |
| `amb_store` | Inside a log trade store: quiet, a stove, the creak of floorboards, a bale of furs dropped on a counter now and then, a pendulum clock ticking. |
| `amb_camp_cree` | A Cree family camp of hide and birchbark lodges in the forest at evening: a fire crackling, dogs, wood being chopped, children playing far off (laughter, no words), a kettle. No music, no chanting. |

### 1.4 Fire and hearth (SFX / LOOP)

| ID | Prompt |
|---|---|
| `fire_hearth_loop` | AMBIENT HEADER, 30 s: a small wood fire in an open stone hearth, close: steady crackle, soft hiss of spruce sap, occasional pop, no wind. |
| `fire_large_loop` | AMBIENT HEADER, 30 s: a big outdoor bonfire of dry spruce: roaring, crackling, sparks snapping. |
| `fire_building_loop` | AMBIENT HEADER, 30 s: a log building fully on fire: a deep roar, timbers cracking, embers falling, glass bursting once. (For `World/Fire.cs` and `Blaze.cs`.) |
| `fire_light` | SFX HEADER, 2 s: striking flint against a steel fire-striker twice, a spark catching in dry tinder, a small whoosh as it takes. |
| `fire_add_log` | SFX HEADER, 1.5 s: a split log dropped onto a burning fire, a thump and a burst of crackling. |
| `fire_douse` | SFX HEADER, 2 s: a bucket of water thrown on a fire: a loud hiss and steam. |

---

## Priority 2: what the player does

### 2.1 Weapons and fighting (SFX)

Muskets are one-shot weapons (design doc 15.3): the shot must sound heavy and final, and the reload long.

| ID | Length | Prompt (after SFX HEADER) |
|---|---|---|
| `musket_fire_01..03` | 2 s | A single shot from an 1800s flintlock trade musket outdoors: the snap of the flint and a split-second pan flash, then a deep heavy boom with a rolling echo across open country. |
| `musket_misfire` | 0.6 s | A flintlock that flashes in the pan but fails to fire: a sharp click, a short fizzing hiss, nothing more. |
| `musket_click` | 0.4 s | Cocking the hammer of a flintlock musket: two crisp mechanical clicks. |
| `musket_reload` | 6 s | Reloading a flintlock musket: biting a paper cartridge, pouring powder down the barrel, ramming the ball home with an iron ramrod (three scraping thrusts), priming the pan, closing the frizzen. |
| `pistol_fire` | 1.5 s | A flintlock pistol shot: sharper and lighter than a musket. |
| `bow_draw` | 1 s | Drawing a wooden hunting bow: creak of wood and sinew string. |
| `bow_release_01..03` | 0.6 s | Releasing an arrow from a wooden bow: a twang and a quick whoosh. |
| `arrow_hit_flesh` | 0.4 s | An arrow striking an animal body: a dull thud. |
| `arrow_hit_wood` | 0.4 s | An arrow thunking into a wooden post and quivering. |
| `knife_slash_01..03` | 0.4 s | A heavy knife swung through the air and cutting cloth. |
| `tomahawk_swing_01..03` | 0.5 s | A small iron trade axe swung hard and striking. |
| `punch_01..05` | 0.3 s | A bare-fisted punch landing on a body in a brawl: a meaty thud. |
| `body_fall` | 1 s | A man falling to the ground in a heavy wool coat on packed snow. |
| `grunt_hurt_01..05` | 0.5 s | A man grunting in pain when struck. One short grunt, no words. |
| `musket_distant` | 3 s | A musket shot heard from a mile away across a frozen river: a muffled flat pop and a long faint echo. (`NoiseRing.cs` already models gunshots being heard at a distance.) |

### 2.2 Work and survival (SFX)

| ID | Length | Prompt (after SFX HEADER) |
|---|---|---|
| `axe_chop_01..05` | 0.6 s | A felling axe biting into a frozen spruce log. |
| `wood_split_01..03` | 1 s | A split log falling apart onto a chopping block and the pieces clattering. |
| `saw_loop` | 4 s | A two-man pit saw cutting through a log, steady strokes (loopable). |
| `hammer_nail_01..03` | 0.5 s | A hammer driving an iron nail into planking. |
| `trap_set` | 1.5 s | Opening the jaws of an iron leg-hold trap against a stiff spring and setting the pan: creaking metal, a click. |
| `trap_snap` | 0.5 s | An iron leg-hold trap snapping shut: a sharp metallic clack. |
| `snare_set` | 1 s | Tying a brass-wire snare to a sapling: wire twang, twig snap. |
| `net_haul` | 3 s | Hauling a wet gill net out of a hole in the ice: dripping water, net rope rasping on ice, a fish flapping. |
| `ice_chisel_01..03` | 0.6 s | An iron ice chisel stabbing down through thick lake ice: a hard, ringing crunch. |
| `fish_flop` | 1 s | A fish flopping on snow. |
| `skinning` | 3 s | A knife scraping and cutting along the inside of a fresh animal hide. |
| `pelt_drop` | 0.7 s | A stack of dried beaver pelts dropped onto a wooden counter. |
| `bale_drop` | 1 s | A 90-pound canvas-wrapped bale of furs thrown down on planks. |
| `bucket_fill` | 2 s | Dipping a wooden bucket into a water barrel and lifting it out, dripping. |
| `eat_01..03` | 1.5 s | A man chewing tough dried meat with his mouth closed. |
| `drink_01..03` | 1.5 s | A man taking two gulps from a tin cup. |
| `drink_rum` | 2 s | A gulp of strong spirit from a tin cup and a short satisfied exhale. |
| `pipe_puff` | 2 s | Drawing on a clay tobacco pipe and puffing out smoke. |
| `shiver` | 1.5 s | A man shivering hard in the cold, teeth chattering, a shaky breath. No words. |
| `cough_sick_01..03` | 1.5 s | A deep, wet, sick cough. |
| `berry_pick_01..03` | 1.5 s | Picking handfuls of berries off a bush and dropping them into a birchbark basket: leaves rustling, soft patter of fruit on bark. |
| `salt_cask_pack` | 4 s | Packing plucked geese down into a wooden cask with coarse salt: dull thumps of birds, salt poured and crunching, then a lid tapped on with a mallet. |
| `spruce_beer_brew_loop` | 20 s (LOOP) | A copper kettle of spruce tips boiling over a hearth: a steady rolling boil, the odd spit against hot iron, no voices. |
| `corn_mortar_01..03` | 1.2 s | Pounding dried corn in a hollowed log mortar with a long wooden pestle: a deep woody thud and the rattle of kernels. |
| `corn_shell_01..03` | 2 s | Shelling a dried ear of Indian corn by hand: kernels rasping off the cob and falling into a bark tray. |
| `gum_patch` | 4 s | Patching a birchbark canoe: spruce gum melted in a small pot, smeared on with a hot iron, hissing as it sets. |

### 2.3 Travel (SFX / LOOP)

| ID | Prompt |
|---|---|
| `paddle_stroke_01..05` | SFX HEADER, 1 s: a single wooden canoe paddle stroke: blade entering water, a strong pull, dripping on the recovery. |
| `canoe_glide_loop` | AMBIENT HEADER, 30 s: sitting in a birchbark canoe gliding on a calm river: water chuckling against the bark hull, gentle rocking. |
| `brigade_paddling_loop` | AMBIENT HEADER, 30 s: eight men paddling a big freight canoe in rhythm, about one stroke a second, water rushing past, no singing. |
| `york_boat_oars_loop` | AMBIENT HEADER, 30 s: a heavy wooden York boat rowed by six men with long sweeps: oars thudding in their tholes, creaking timbers, water. |
| `rapids_loop` | AMBIENT HEADER, 30 s: standing beside a long stretch of river rapids: loud white water over rocks. |
| `sled_run_loop` | AMBIENT HEADER, 30 s: a dog sled running across packed snow: runners hissing, dogs panting, harness bells jingling faintly. |
| `dogs_bark_01..03` | SFX HEADER, 2 s: a pack of sled dogs barking excitedly. |
| `dogs_howl` | SFX HEADER, 6 s: sled dogs at a fort howling together at night. |
| `ship_creak_loop` | AMBIENT HEADER, 30 s: on deck of a wooden sailing ship at anchor: timbers creaking, rigging slapping, small waves against the hull. |
| `anchor_drop` | SFX HEADER, 5 s: a ship's anchor dropped: a heavy iron chain rattling out through a hawse hole, a splash. |

---

## Priority 3: animals

All of these are SFX HEADER, mono. Animals in the code now: caribou, goose, beaver, wolf. Hare and ptarmigan have art. The rest are for when they're added (see `world-content-needed.md`).

| ID | Prompt |
|---|---|
| `goose_honk_01..05` | (2 s) A single Canada goose honking on the ground. |
| `geese_flock_overhead` | (8 s) A big V of Canada geese flying low overhead, many honking, wingbeats audible as they pass. |
| `geese_takeoff` | (3 s) A flock of geese bursting up off a marsh in alarm: wings beating, splashing, honking. |
| `geese_flight_marsh_loop` | (AMBIENT, 60 s, LOOP) The autumn passage on a Hudson Bay marsh: thousands of geese on the water and in the air at once, a continuous wash of honking with nearer birds over it and wingbeats passing. Not a single flock - the whole flight, heard from a goose tent at the edge of the marsh. |
| `caribou_grunt_01..03` | (1.5 s) A caribou's low snorting grunt. |
| `caribou_herd_loop` | (AMBIENT, 30 s) A herd of caribou passing: many hooves clicking (caribou ankle tendons click when they walk), grunting, snow crunching. |
| `beaver_tail_slap` | (1.5 s) A beaver slapping its tail on still pond water in alarm: a loud flat crack and splash. |
| `beaver_gnaw` | (3 s) A beaver gnawing through a green aspen trunk. |
| `wolf_howl_01..03` | (6 s) A single grey wolf howling far off on a still winter night. |
| `wolf_pack_howl` | (10 s) A wolf pack howling together in the distance. |
| `wolf_growl` | (2 s) A wolf snarling close up, about to attack. |
| `wolf_bite` | (0.5 s) A wolf lunging and snapping its jaws. |
| `wolf_yelp_death` | (1.5 s) A wounded wolf yelping. |
| `ptarmigan_call` | (2 s) A willow ptarmigan's rattling, croaking "go-back go-back" call. |
| `hare_squeal` | (1 s) A snowshoe hare's thin scream when caught. |
| `raven_croak_01..03` | (1.5 s) A common raven's deep croak. |
| `loon_call` | (5 s) A common loon's tremolo and wail across a lake. |
| `moose_call` | (3 s) A bull moose grunt / cow moose call in autumn. |
| `bear_growl` | (2 s) A black bear huffing and growling. |
| `polar_bear_chuff` | (2 s) A polar bear chuffing and growling. |
| `bison_herd_loop` | (AMBIENT, 30 s) A great buffalo herd grazing on the plains: grunting bulls, hooves, wind in grass. |
| `bison_stampede` | (8 s) A buffalo herd stampeding: thunder of hooves building, ground shaking. |
| `horse_whinny` | (2 s) A horse whinnying. |
| `horse_gallop_loop` | (AMBIENT, 10 s) A horse galloping across prairie sod. |
| `mosquitoes_close` | (AMBIENT, 15 s) A cloud of mosquitoes whining right next to the ear. |

---

## Priority 4: events (SFX)

| ID | Length | When | Prompt (after SFX HEADER) |
|---|---|---|---|
| `ship_cannon_salute` | 6 s | The supply ship arrives (`World/Ship.cs`) | A ship's cannon fired in salute, then two seconds later the fort's small cannon answering: two deep booms with long echoes across the water. |
| `ship_bell` | 4 s | Ship arrives / departs | A ship's brass bell rung in pairs: ding-ding, ding-ding. |
| `fort_bell_work` | 4 s | Start / end of the working day | A small iron bell on a post in a fort yard rung briskly to call the men. |
| `fort_bell_alarm` | 6 s | Fire, raid | The same iron bell rung frantically and without rhythm, an alarm. |
| `gate_open` | 3 s | Entering the fort | Heavy wooden palisade gates swung open on iron hinges, creaking, a bar lifted. |
| `door_open_01..03` | 1.5 s | Buildings | A plank door on iron strap hinges opening and thumping shut. |
| `shutter_bang` | 0.6 s | Wind | A wooden shutter banging in the wind. |
| `ice_crack_boom` | 4 s | Freeze-up / breakup | A huge sheet of river ice cracking with a deep boom and a long eerie twang across the surface. |
| `ice_break_through` | 3 s | Falling through ice | Thin ice cracking and giving way under a man, a heavy splash, a gasp. |
| `tree_fall` | 5 s | Felling | A spruce tree cracking and falling, crashing through branches and thudding on snow. |
| `death_sting` | — | A death | (Use music cue `stinger_death` below.) |
| `news_letter` | 2 s | A message arrives (`Core/Message.cs`) | A folded, wax-sealed letter being broken open: the seal cracking, stiff paper unfolding. |
| `trade_coins` | 1 s | A trade closes (`Economy/Barter.cs`) | Small wooden made-beaver tokens and a few metal trade goods set down on a counter. |
| `musket_volley_geese` | 4 s | A goose hunt kills (`World/GooseHunt.cs`) | Three or four muskets fired almost together into a settled flock, then the roar of several hundred geese getting up off water. |
| `portage_shoulder_01..03` | 2 s | A brigade at a carrying place (`World/Brigade.cs`) | A man hoisting two ninety-pound packs onto his back with a tumpline across his forehead: cords creaking, a hard breath out, canvas shifting. |
| `canoe_haul_out` | 5 s | Arriving at a portage | A loaded freight canoe dragged up a rocky landing: bark grating on stone, water running off, men grunting. |
| `trade_fair_loop` | (AMBIENT, 60 s, LOOP) | The Missouri towns' market (`Economy/Gardens.cs`) | A big summer trade fair outside an earth-lodge village: many voices bargaining in several languages at once, horses stamping and whinnying, dogs, corn being poured, no music. |
| `herd_passing_distant` | 20 s | The deer pass (`Ecology/EcologyManager.cs`) | A barren-ground caribou herd crossing a mile off: a low continuous rumble of hooves and clicking tendons carried on the wind, thin grunts. |
| `scurvy_groan` | 2 s | Illness | A sick man groaning quietly in a bunk. |

---

## Priority 5: UI (SFX, short, quiet, unmusical)

| ID | Length | Prompt (after SFX HEADER) |
|---|---|---|
| `ui_click` | 0.1 s | A soft, dry click like a wooden button or a quill tapped on a desk. |
| `ui_hover` | 0.05 s | A very faint paper rustle. |
| `ui_open_journal` | 0.8 s | A leather-bound ledger book opened, pages riffling. |
| `ui_page_turn_01..03` | 0.5 s | A single page of thick rag paper turned. |
| `ui_close_journal` | 0.5 s | A leather-bound book shut with a soft thump. |
| `ui_quill_write` | 1.5 s | A quill pen scratching a quick line on paper. |
| `ui_notice` | 0.6 s | A small brass hand bell rung once, softly. (Notices and new goals.) |
| `ui_warning` | 0.8 s | A low wooden knock twice, like a fist on a door. (Hunger, cold, danger.) |
| `ui_inventory_pick` | 0.3 s | A small cloth bundle lifted. |
| `ui_inventory_drop` | 0.3 s | A small cloth bundle set down. |
| `ui_equip` | 0.4 s | A leather strap buckled. |
| `ui_dialogue_open` | 0.4 s | A short intake of breath and a stool scraping, as someone turns to talk. |
| `ui_save` | 1 s | Blotting sand shaken onto wet ink and a book closed. |

---

## Priority 6: music

Paste the MUSIC HEADER first. Keep in-world music quiet; the design wants silence and wind most of the time, with music as a reward or a warning.

### 6.1 Menus: Done

| ID | Length | Brief (after MUSIC HEADER) |
|---|---|---|
| `mus_main_menu` | 2 min, LOOP | Main menu over the painting of York Factory at dusk. Slow (60 bpm), wistful and wide: a lone fiddle playing a slow Scottish air in a minor key over a low cello drone, a wooden flute answering in the second half, a soft frame drum like a distant heartbeat entering near the end. Lonely, cold, proud, homesick. Ends returning to the opening phrase. |
| `mus_new_game` | 1 min, LOOP | Character creation. The main menu theme's melody played gently on solo tin whistle with a little harmonium underneath. Thoughtful, hopeful, unhurried. |
| `mus_credits` | 3 min | End credits after 1821. The main theme slowly, then building into a full reel with fiddle, flute, bodhran and stomping, then fading back to the lone fiddle. Bittersweet: an era ending. |

### 6.2 Seasons in the world (sparse, plays now and then over the ambience)

| ID | Length | Brief |
|---|---|---|
| `mus_winter` | 90 s | Deep winter. Very sparse and slow, almost silent: long held cello notes, a few soft high fiddle harmonics like frost, lots of space between phrases. Isolation, endurance, beauty in the cold. |
| `mus_breakup` | 90 s | Ice breakup and the return of the geese. Starts hushed, warms gradually: tin whistle melody rising, pizzicato cello like dripping water, light frame drum at the end. Relief and renewal. |
| `mus_summer` | 90 s | The short open-water summer. A relaxed, lilting 6/8 tune on fiddle and wooden flute, jaw harp now and then. Busy, warm, a bit restless. |
| `mus_freezeup` | 90 s | Autumn freeze-up, the ship has gone. A slow minor-key fiddle lament over a drone, a sense of doors closing. The last of the light. |

### 6.3 Places and people

| ID | Length | Brief |
|---|---|---|
| `mus_mens_house_reel:Done` | 2 min, LOOP | The men's house on a Saturday night. A fast, joyful Orkney / Scottish reel on solo fiddle with foot-stomping on wooden floorboards and hand claps, slightly rough and drunken. Played in the room, not a concert. |
| `mus_mens_house_jig` | 2 min, LOOP | Same scene, a different tune: a 6/8 jig on fiddle with spoons and a tin whistle joining. |
| `mus_hogmanay` | 2 min | New Year's at the post (Hogmanay, the biggest feast of the fort year). A rousing reel on fiddle and bagpipe drone, stomping and cheering crowd (no words). |
| `mus_metis_fiddle` | 2 min, LOOP | A Métis-style fiddle tune (Red River Jig style): driving, crooked phrasing with extra and dropped beats, heavy foot-tapping as percussion. Placeholder until it can be licensed from a Métis fiddler. |
| `mus_cree_camp_drum` | 90 s, LOOP | Heard from across a camp at evening: a single hand drum played in a steady even beat (about 2 beats a second), distant and soft. Strictly drum only: no singing, no chanting, no flutes, no "tribal" film-score elements. Placeholder (see the note on Indigenous music above). |
| `mus_voyageur_song` | 60 s | **Singing allowed.** A brigade of eight French-Canadian canoemen singing a call-and-response paddling song in French in rhythm with their strokes, unaccompanied, outdoors on the water: one strong lead voice sings a line, the rest answer in chorus. Use the traditional public-domain voyageur song "En roulant ma boule" (or "À la claire fontaine"). Rough, hearty working voices, not a choir. |
| `mus_voyageur_song_2` | 60 s | As above, with the traditional song "J'ai trouvé le nid du lièvre" or "V'là l'bon vent". |
| `mus_nwc_rendezvous` | 2 min, LOOP | The NWC rendezvous at Fort William in July: a lively French-Canadian reel on fiddle with jaw harp and podorythmie (seated foot-tapping in a heel-toe pattern), festive and loud. |
| `mus_church_psalm` | 60 s | Sunday at York Factory: the Factor reads prayers. A simple slow Scottish metrical psalm tune on a small reed harmonium, nothing else. Plain and dutiful. |
| `mus_bagpipe_lament` | 60 s | A funeral at the post: a lone highland bagpipe playing a slow lament outdoors in wind. |

### 6.4 Tension and story cues

| ID | Length | Brief |
|---|---|---|
| `mus_danger` | 60 s, LOOP | Something is hunting you (wolves, a war party). Low, insistent frame drum pulse speeding very slightly, a tremolo cello on one note, a dissonant fiddle scrape now and then. No melody. Stops dead when the danger passes. |
| `mus_combat` | 45 s, LOOP | A fight has started. Fast pounding bodhran, sharp stabbing fiddle chords, cello ostinato. Short and brutal, not heroic. |
| `mus_aftermath` | 40 s | After a fight. Silence, then a single low drone and one sad fiddle phrase repeated twice, fading. |
| `mus_starving` | 60 s, LOOP | The post is running out of food in late winter. Very slow, hollow: a harmonium chord that won't resolve, a few hesitant flute notes. Dread. |
| `stinger_death` | 6 s | The player or someone close dies. One deep low drum hit, then a single descending fiddle line over a held cello note. |
| `stinger_ship` | 8 s | The supply ship is sighted. A bright rising tin whistle phrase over a sudden drum roll and a held major chord. Joy and relief. |
| `stinger_news` | 5 s | Important news arrives from London or Montreal. A short, formal fanfare on solo fiddle, three notes, then held. |
| `stinger_promotion` | 6 s | A promotion or a good outcome. A short, warm reel phrase on fiddle and flute ending on a firm major chord. |
| `stinger_disgrace` | 6 s | Dismissed, disgraced or betrayed. A single fiddle note bending down, a low drum hit. |

### 6.5 Campaign set-pieces (`Campaigns/historical.json`)

| ID | Beat | Brief |
|---|---|---|
| `mus_pemmican_proclamation` | `pemmican-proclamation` | 60 s. A cold, official, march-like snare-and-fife figure (British army fife) slowly turning sour and minor. Authority provoking trouble. |
| `mus_seven_oaks` | `seven-oaks` | 90 s. Galloping drum rhythm like horses on prairie sod, a fierce minor-key Métis fiddle, building fast, then cutting off into a long silent drone. A massacre, not a victory. |
| `mus_fort_william_seized` | `selkirk-fort-william` | 60 s. A British military snare drum march with fife, steady and grim, under a mournful French-Canadian fiddle melody. Occupation. |
| `mus_merger_1821` | `merger` | 90 s. The two musical worlds (the Scottish fiddle air of the HBC and the French-Canadian reel of the NWC) played together at half speed, finally in the same key, ending unresolved. Union and loss. |

---

## Folder layout and naming

```
assets/audio/
  music/     mus_*.ogg, stinger_*.ogg
  ambient/   amb_*.ogg, *_loop.ogg
  sfx/       everything else
  ui/        ui_*.ogg
```

Lower-case, underscores, numbered takes `_01`. WAV, OGG and MP3 all load, and music can stay MP3. A new numbered take of an existing sound is picked up automatically. A new *kind* of sound needs a line in `Core/Audio.cs` saying when it plays. After adding files, run Godot's import once (`--headless --path . --import`) or open the editor.

---

## Summary checklist

"Partial" means some of the section is in the game; see **In the game now**.

- [ ] 1.1 Footsteps (9 surfaces x 5): partial (snow, snow crust, mud, planks)
- [ ] 1.2 Ambient beds (13): partial (winter wind, blizzard, river, rain)
- [ ] 1.3 Fort interiors and camp (4): partial (fort chatter, children at camp)
- [ ] 1.4 Fire (6): partial (hearth loop, flint)
- [ ] 2.1 Weapons and fighting (15): partial (musket, distant shot, punch, reload, body fall)
- [ ] 2.2 Work and survival (20): partial (axe chops)
- [ ] 2.3 Travel (10): partial (paddling, dog sled, ship creak)
- [ ] 3 Animals (24): partial (geese, raven, loon, mosquitoes, wolves)
- [ ] 4 Events (14): partial (ship salute, work bell, alarm bell, door, ice, tree fall)
- [ ] 5 UI (13): partial (page turn; quill on disk)
- [ ] 6.1 Menu music (3): partial (main menu done; new-game and credits still wanted)
- [x] 6.2 Season music (4)
- [ ] 6.3 Places and people (11): partial (men's house reel and jig)
- [ ] 6.4 Tension and stingers (9)
- [ ] 6.5 Campaign set-pieces (4)
