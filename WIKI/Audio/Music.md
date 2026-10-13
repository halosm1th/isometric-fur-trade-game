---
title: Music
aliases: [Soundtrack, Tracks, OST]
tags: [audio, music]
status: implemented
---

# Music

**Status:** Implemented. Six AI-generated tracks (Google Gemini, 11 October 2026), all instrumental. The wishlist (tension cues, stingers, place music) is mostly **Planned**.

Instruments in the brief are only those that could be heard in a 1811 fur-trade post or on the rivers of Rupert's Land: fiddle, wooden flute or tin whistle, a lone bagpipe drone, bodhran or frame drum, cello as a drone, jaw harp, foot stomping, spoons, a harmonium. No synthesizers, no drum kit, no epic choir.

## The six tracks

| Track | File | Length | Plays |
|---|---|---|---|
| **Claim the North** | `music/claim_the_north.mp3` | 2:33 | The main menu, on repeat (2 s gap). Slow, wistful: a lone fiddle air over cello drone. |
| **The Long Winter at the Bay** | `music/the_long_winter_at_the_bay.mp3` | 2:00 | Winter, and freeze-up |
| **Planks Beneath the Frost** | `music/planks_beneath_the_frost.mp3` | 2:59 | Winter |
| **Waiting on the Ice** | `music/waiting_on_the_ice.mp3` | 1:02 | Breakup and freeze-up |
| **Boots on the Pine Floor** | `music/boots_on_the_pine_floor.mp3` | 2:03 | Open water (summer); also in the men's house at night as the second reel |
| **Men's House Reel** | `music/mens_house_reel.mp3` | 2:02 | In the men's house, evenings |

## Which track plays where

From `Audio.cs`:

| Condition | Pool |
|---|---|
| Main menu scene | Claim the North |
| Winter | The Long Winter at the Bay, Planks Beneath the Frost |
| Breakup (May to June) | Waiting on the Ice |
| Open water (July to August) | Boots on the Pine Floor |
| Freeze-up (September to October) | Waiting on the Ice, The Long Winter at the Bay |
| **Men's house, 19:00 to 23:30, you indoors within 450 px of its middle** | Men's House Reel, Boots on the Pine Floor (6 dB quieter, "played in the room, not a concert") |

See [[Calendar and Seasons]] for the season dates and [[Buildings]] for the men's house.

## Rhythm

- On arriving in the world the first piece starts after **20 to 45 seconds** of quiet.
- When a piece ends, **2 to 4 minutes (120 to 240 s)** of silence follows (4 s after a reel).
- When the kind of music changes (world to reel, menu to world) the old piece fades over about 3 seconds before the new one starts.
- Only the main menu gets the menu theme: between a death and the heir taking over, nothing plays.

## Credits

All six tracks were "generated with Google Gemini for this game" on 2026-10-11. There is no composer credit. See [[Credits and Licenses]].

## Planned music (from `docs/audio-prompts.md`)

| Group | Cues |
|---|---|
| Menus | New game (tin whistle version of the main theme), credits (after 1821) |
| Places and people | Hogmanay reel, Métis fiddle (placeholder until a real fiddler is licensed), Cree camp drum (drum only, placeholder), voyageur paddling songs (singing allowed; public-domain French songs such as "En roulant ma boule"), NWC rendezvous, church psalm, bagpipe lament |
| Tension | Danger (something hunting you), combat, aftermath, starving, stingers for death, ship sighted, news, promotion, disgrace |
| Campaign set-pieces | Pemmican Proclamation, Seven Oaks, Fort William seized, the 1821 merger (the Scottish air and the French-Canadian reel played together in the same key) |

> [!info] A note on Indigenous music
> Songs belong to the communities who made them; many are ceremonial. The prompts never ask for imitation singing or sacred music, and the generated drum cues are placeholders until real recordings from Cree or Métis musicians can be licensed.

## See also

[[Audio]] | [[Sound Effects]] | [[Credits and Licenses]] | [[Calendar and Seasons]] | [[Roadmap]]
