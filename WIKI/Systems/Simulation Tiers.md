---
title: Simulation Tiers
aliases: [Tiers, Live and Background, LOD, Concentric Fidelity, Simulation Fidelity]
tags: [system, simulation, performance, architecture]
status: implemented
---

# Simulation Tiers

**Status:** Implemented in two rings (`Tier.Live` and `Tier.Background`). The design doc (18.2) describes concentric fidelity with more rungs; the build has two. Everyone near **any** player is simulated as before; everyone else is a **row of numbers** that their post or camp resolves once a day.

## The rule

> Nothing is despawned: the node stays, so every reference by name, every grudge, every piece of mail and the save file all keep working. The body simply stops moving, stops drawing, and resolves in batches.

| Constant | Value |
|---|---|
| `LiveRadius` | **3200 px**: a background NPC within this distance of a player is promoted |
| `BackgroundRadius` | **4200 px**: a live NPC beyond this from every player is demoted |

The 1000 px gap is hysteresis. The assignment runs every **1.0 second** on the server. Distance is measured to the nearest player (a dead player still anchors; "his body is there and he will be back"). A background man is reckoned to be **at his own door**.

### Who is exempt from demotion

Prisoners (`HeldBy`) and companions (`Following`), people with no household, bodies, and anyone mid-journey: a courier, a raider, an ambusher, a rescuer, a searcher, a man on a site hunt. "A man on a journey is the story; he walks it himself." Away with a brigade is its own state: the brigade speaks for him until he lands.

### What demote and promote do

- **Demote**: clear his goals, stop him, put him on a standing cell at his household's access point, hide him, disable processing, announce over the network.
- **Promote**: re-enable, show, clear the stack so he starts fresh.

## The daily batch

Once a game day the server resolves each background person in numbers (`DailyBatch`):

1. **Body**: +1 day since fresh food; sick: -1 sick day and -8 health, immune when over; scurvy -3 health.
2. **Eat** a ration from the household's cache and actually eat it (scurvy "turns on what a man swallowed"), or something from his pack; no food: hunger -26; fed: hunger at least 60.
3. **Water**: full if there is water, or it isn't frozen, or there is firewood to melt snow; else -10 (min 20).
4. **Warmth**: full if there is firewood in the house (it burns 1) or it isn't frozen; else -30.
5. **Wasting**: no warmth = -10 health/day; no food = -3 health/day plus short-commons counting (a headline on day 8 and every 30 days: "X is on short commons: nothing in the store and the men going thin"); a fed man heals +6.
6. **Death**: at 0 health he dies quietly with a cause (fever, cold, scurvy or want).
7. **Work** (skipped in a blizzard or storm), by role:

| Role | Day's work |
|---|---|
| Labourer, Voyageur | Firewood to 30 (4-5 logs); winter traps (beaver) at the Factor; else hunt |
| Cree hunter | Hunt big game, then geese, then beaver; falls back to snares (hare/ptarmigan) and a line through the ice |
| Woman, Cree woman, Wife, Daughter | Salt geese (8 per cask), firewood to 12, water, dry venison, make pemmican from dried meat, or fish |
| Leaders (Factor, Postmaster, Partner, Chief) | Nothing (their orders wait for a live beat) |

Hunt success chance: `clamp(site count / capacity, 0.05, 1) x (0.16 + 0.24 x Hunting)`. A desperate house (under 8 food days) learns more of its own country's sites. Camps of the wider country (CountryBand) feed themselves in their own weekly sum.

### Under fast-forward

If game speed is above 6x everyone's day is resolved in numbers, not on foot, "or the whole country starves with food on the shelf." Even a live man in his own yard with food on the shelf eats if hunger falls below 25.

## Other daily jobs on the same tick

The same once-a-day call also runs the goose hunt, the band's spring ([[The Reckoning of 1821|year turn for band players]], day 135), gardens, the career rivalry weekly update ([[Rivalry]]), and the brigades.

## What this means to a player

- **The York country is detailed; the rest is numbers.** A camp 5000 px away is still fed, still hunts, still hears news.
- Memories, grudges, letters and debts are the same for live and background people. News that touches you reaches them by the ordinary rules ([[Memory and Knowledge]]).
- Multiple players each anchor their own live ring (design doc 18.2, 21.2); see [[Multiplayer]].

## Tips

> [!tip]
> Because the batch feeds from the household cache, a post that lets its stores empty loses men out of your sight. Check the stores ("The post has about *n* days of food") before long trips. See [[Needs and Survival]].

## See also
[[Architecture]] | [[Multiplayer]] | [[Calendar and Seasons]] | [[Memory and Knowledge]] | [[Delegation]] | [[Wildlife]]
