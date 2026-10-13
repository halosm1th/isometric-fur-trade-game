---
title: Sound Effects
aliases: [SFX, Ambience, Ambient Sounds, Foley]
tags: [audio, sfx, ambience]
status: implemented
---

# Sound Effects

**Status:** Implemented. Every file below is in `assets/audio/` and is played by the `Audio` autoload ([[Audio]]). Sources: **CC0 Freesound** recordings, and **synthesised stand-ins** (generated in code by `tools/audio/synth.py`, original to the project). Authors and licences: [[Credits and Licenses]].

## Footsteps

Chosen by the surface under the feet (`Audio.StepSound`) and by how far the step carried (louder when running, near-silent when sneaking). Your own steps always play; others' only within about 650 px and a few a second.

| Name | Files | Source | When |
|---|---|---|---|
| `step_snow` | `sfx/step_snow_01` to `05` (.wav, 0.5 s) | Freesound, CC0 | Winter; also freeze-up after 15 October and breakup before 10 May |
| `step_snow_crust` | `sfx/step_snow_crust_01` to `05` (0.6 s) | Freesound, CC0 | Snow footsteps on a clear day, half the time (a hard crust after a clear night) |
| `step_mud` | `sfx/step_mud_01` to `05` (0.7 s) | Freesound, CC0 | The rest of the year: muskeg mud |
| `step_planks` | `sfx/step_planks_01` to `05` (0.4 s) | Synth | House floors and the wharf, all year |

## People and tools

| Name | Files | Source | When |
|---|---|---|---|
| `axe_chop` | `axe_chop_01..05.wav` | Freesound | Four strokes while someone cuts firewood |
| `tree_fall` | `tree_fall.wav` (12.6 s) | Freesound | The tree coming down |
| `musket_fire` | `musket_fire.wav` (1.9 s) | Freesound | Any musket shot |
| `musket_reload` | `musket_reload.wav` (4.2 s) | Synth | 1.4 s after a shot, heard close by |
| `cannon_distant` | `cannon_distant.wav` (3.8 s) | Freesound | Under a shot over 1,400 px away; the ship's salute |
| `punch` | `punch_01..02.wav` | Freesound | A melee blow landing |
| `body_fall` | `body_fall_01..02.wav` | Synth | A person dying |
| `flint_strike` | `flint_strike.mp3` (8.6 s) | Freesound | Lighting a fire with flint and steel |
| `door_open` | `door_open.mp3` | Freesound | Crossing a threshold |
| `wood_creak` | `wood_creak.wav` | Freesound | **Unused** (a bunk or shutter in future) |

## Animals

| Name | Files | Source | When |
|---|---|---|---|
| `wolf_howl` | `wolf_howl_01..03.wav` (6 to 7 s) | Synth | Night (20:00 to 06:00), nearest wolf within 3,500 px, every 35 to 90 s (21 to 54 in winter) |
| `wolf_pack_howl` | `wolf_pack_howl.wav` (12 s) | Synth | Three or more wolves about |
| `wolf_growl` | `wolf_growl_01..02.wav` | Synth | A wolf within 420 px, every 5 to 10 s |
| `wolf_bite` | `wolf_bite.wav` | Synth | A bite landing |
| `goose_honk` | `goose_honk.mp3` | Freesound | A skein of 2 to 5 voices overhead in breakup and freeze-up |
| `raven_croak` | `raven_croak.ogg` | Freesound | Ravens round the post in winter and freeze-up (45% of rolls) |
| `loon_call` | `loon_call.ogg` (168 s of calls; a 7 s stretch played) | Freesound | Summer evenings and nights near water |
| `dogsled_pass` | `dogsled_pass.wav` (10.5 s) | Freesound | Winter days within about 2,500 px of York, 12% of rolls |

See [[Wolf]], [[Goose]] and [[Dog Sled]].

## Bells and the fort

| Name | File | When |
|---|---|---|
| `fort_bell_work` | `fort_bell_work.wav` (9.9 s) | 07:00 and 18:00 within 3,500 px of York, unless blizzard or storm; the ship arriving and sailing |
| `fort_bell_alarm` | `fort_bell_alarm.wav` (12.8 s) | A fire starting in the fort (1.5 s delay) |
| `ice_crack` | `ice_crack_01..03.wav` | Three booms as winter sets in and as breakup starts |

## Ambient loops

All played on the Ambience bus unless noted; levels fade over a few seconds.

| Name | File | Length | Source | Plays |
|---|---|---|---|---|
| `amb_winter_wind` | `ambient/amb_winter_wind.wav` | 43 s | Freesound | Always some: 0.4 in winter, 0.3 freeze-up, 0.2 breakup, 0.1 open water; plus 0.1 overcast and 0.2 in snow or rain |
| `amb_blizzard` | `amb_blizzard.wav` | 48 s | Freesound | Blizzard (full) and storm (0.6) |
| `amb_rain` | `amb_rain.wav` | 44 s | Synth | Rain (0.75, 0.6 indoors) and storm (1.0) |
| `amb_river` | `amb_river.wav` | 43 s | Freesound | Near open water (within 640 px), louder closer; silent when frozen |
| `amb_mosquitoes` | `amb_mosquitoes.wav` | 8.5 s | Freesound | High summer, by day, outdoors, clear or cloudy |
| `amb_fort_chatter` | `amb_fort_chatter.wav` | 54 s | Freesound | Near a post with more than one waking person (Effects bus) |
| `amb_kids_playing` | `amb_kids_playing.wav` | 45 s | Freesound | Children at the nearest band camp, 08:00 to 20:00, fair weather |
| `fire_hearth_loop` | `fire_hearth_loop.wav` | 15 s | Freesound | The three nearest burning hearths, plus a louder copy for a building on fire |
| `canoe_paddling_loop` | `canoe_paddling_loop.wav` | 30 s | Freesound | The player paddling a canoe: see [[Canoe]] |
| `ship_creak_loop` | `ship_creak_loop.mp3` | 60 s | Freesound | Near the ship while she is in: see [[Ship]] |

## Interface

| Name | File | Notes |
|---|---|---|
| `ui_page_turn` | `ui/ui_page_turn.ogg` (0.6 s) | Opening or closing the journal, log or pack |
| `ui_quill_write` | `ui/ui_quill_write.mp3` (6 s) | **Unused**: wants a letter-writing screen |

## File counts

`sfx/` 52 files, `ambient/` 10, `ui/` 2, `music/` 6: 70 audio files in all (the `.import` sidecars are Godot's).

## Wishlist

Real wolf recordings (the weakest imitation); more takes of musket, punch, door, goose, raven; footsteps on dirt and river ice; summer day and freeze-up ambient beds; fight grunts (`grunt_hurt`); UI clicks and notice stingers; animal sounds for caribou, buffalo and beaver; sounds for the goose flight (a volley into a settled flock, salting birds into casks), berry picking, spruce beer boiling, gumming a hull, shouldering a portage piece, hauling a canoe out; the Missouri market. Detailed prompts are in `docs/audio-prompts.md`; tools in `tools/audio/` (`process.py` cuts the raw downloads, `synth.py` generates the stand-ins, `audiolib.py` helps).

## See also

[[Audio]] | [[Music]] | [[Credits and Licenses]] | [[Asset Inventory]] | [[Weather]] | [[Wolf]]
