---
title: Audio
aliases: [Sound, Sound System, Audio Autoload]
tags: [audio, system, index]
status: implemented
---

# Audio

**Status:** Implemented (first batch in, 11 October 2026; `Scripts/Core/Audio.cs`, 697 lines). Six music tracks, 10 ambient loops, 52 sound effects and 2 interface sounds (70 files) are in the game. About a third of the sound effects are synthesised stand-ins; a large wish list remains in `docs/audio-prompts.md`.

The game's sound is built to be heard rarely: "the design wants silence and wind most of the time, with music as a reward or a warning." Music plays now and then over the ambience, with minutes of quiet between pieces.

## How it works

`Audio` is an **autoload** (`project.godot`: `Audio="*res://Scripts/Core/Audio.cs"`) so music carries across scene changes. It mostly *listens* rather than being told:

- Sounds the simulation already makes (`Sim.Noise`: footsteps, shots, axes, bites, blows) reach it through `Sim.ShowNoise`, which runs on every machine, so in [[Multiplayer]] every peer hears the same world.
- Everything else it reads off the world each frame: the weather ([[Weather]]), the [[Calendar and Seasons|season]], the hour, the ship ([[Ship]]), nearby fires, open water, crowds at a post, and whether the player is indoors.

### Buses

Three buses under Master, each with a volume slider in **Settings > Sound** ([[Settings and Options]]):

| Bus | Default volume | Contents |
|---|---|---|
| Master | 0.8 | everything |
| Music | 0.6 | the six tracks |
| Ambience | 0.8 | wind, river, rain, fires, ship creak, children (low-pass filtered through log walls) |
| Effects | 0.9 | footsteps, shots, bells, animals, voices at a post (chatter is routed here so walls don't muffle it) |

The Ambience bus gets a low-pass filter at 900 Hz while you are indoors, so everything goes dull through a log wall, and a door sounds as you cross it.

### Finding sounds

A sound is asked for **by name**: `step_snow` finds `step_snow_01` to `step_snow_09` and picks one at random, so *more numbered takes can be dropped in with no code changes*. Files are searched for in `res://assets/audio/{sfx,ui,ambient,music}` as `.wav`, `.ogg` or `.mp3`. A synthesised stand-in is replaced by dropping a recording in under the same name.

`-- --audiotrace` prints every sound as it starts, for checking without ears. In a headless run the audio system switches itself off.

### Voices

6 flat `AudioStreamPlayer`s and 24 positional `AudioStreamPlayer2D`s (attenuation 1.6). A sound is skipped if it is too far to hear or every voice is busy: a crowd of footsteps shouldn't drown a shot. Pitch varies a few percent per play.

## What plays when

| Layer | Behaviour |
|---|---|
| **Music** | See [[Music]] |
| **Ambient beds** | Weather and season: wind always (most in winter), blizzard, rain, river near open water, mosquitoes in high summer, fort chatter near people, ship creak |
| **Positional loops** | Up to three hearths at their places, a building on fire, the ship, children at the nearest camp |
| **Critters** | Every 18 to 50 s: geese overhead in spring and autumn, ravens in winter, a dog team in winter, loons on summer nights; wolves howl at night |
| **Events** | The work bell at 07:00 and 18:00, alarm for a fire, the ship's gun, ice booming at freeze-up and breakup |
| **Noises** | Steps by surface, shots (plus a distant boom beyond 1,400 px and a reload 1.4 s later), chops, tree falls, blows, bites, falls |

The full file-by-file list with credits is in [[Sound Effects]] and [[Credits and Licenses]].

## Not yet in

From `docs/audio-prompts.md` and `docs/asset-inventory.md`: real wolf recordings (the current wolves are synthesised), fight grunts, UI clicks and notice stingers, tension and combat music, the Hogmanay, Métis fiddle, voyageur songs, the NWC rendezvous, a bagpipe lament, campaign set-piece cues, a summer-day ambient bed, sounds for the goose flight, salting, berry picking, gumming a hull and portaging. 13 further prompts were written for the goose flight and brigade on 11 October. The Indigenous music cues are deliberately only a plain hand drum and are to be replaced with licensed real recordings. See [[Roadmap]].

## See also

[[Music]] | [[Sound Effects]] | [[Credits and Licenses]] | [[Asset Inventory]] | [[Settings and Options]] | [[Architecture]]
