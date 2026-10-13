---
title: Sleeping and Resting
aliases: [Sleep, Wait, Rest, Beds, Time Skip]
tags: [gameplay, system, time, resting]
status: implemented
---

# Sleeping and Resting

**Status:** Implemented as a **time skip**, not a need. The game has no fatigue bar and no penalty for staying awake; sleeping is how you pass the long winter nights quickly while your warmth, food and health tick on. (A fatigue icon, ![[ui-need-fatigue.png|24]], exists in the art but is not wired to a mechanic.) Code: `PlayerController.ServerAction("wait")` and `Interact()`, `GameCalendar.SkipHours`.

## Ways to pass time

| Method | What it does |
|---|---|
| **Wait (`Z`)** by day (06:00 - 19:00) | Skips **one hour**: "You wait." |
| **Wait (`Z`)** at night (19:00 - 06:00) | "You settle down for the night." Skips until **07:00**. |
| **A bed (`E`)** from 17:00 to 06:00 | "You turn in till morning": skips until 07:00. |
| A bed (`E`) otherwise | "You lie down for an hour." (From 17:00 a bed takes you through to 07:00.) |
| **Hold `T`** | Fast-forward x40 while held; you stay in control. |

Beds are furniture in buildings such as the men's house. Walk within about 90 px and the prompt reads "E  Sleep till morning" or "E  Rest an hour". See [[Props and Furniture]].

## How a skip works

A skip burns through game hours at **x400 speed**, so a night passes in a few seconds. Simulation continues normally: you still get hungry, cold and thirsty, the fire burns down, NPCs live their day, and anything that attacks you still can. Taking any step **cancels** the wait. In a shared game everyone must agree (see below).

> [!warning]
> Sleeping through a winter night beside a fire that has only an hour or two of fuel is how people die. Stack firewood before you turn in. See [[Fire and Warmth]].

## Shared sleeping

In [[Multiplayer]] the clock is shared, so a wait or sleep is only carried out when **every living player** has asked for it. The HUD shows `Turning in: 1/3`. Moving cancels your vote. Solo, it is just you.

## Healing while you rest

Health heals 1 an hour when you are fed, watered and warm, and slower while moving fast. So a long sleep indoors with food in the pack is also the way to recover after a fight. A woman of the post can dress a wound for +25 once a day. See [[Health, Wounds and Illness]].

## Tips

- Skip nights in the post: the exposure indoors is only 35% of the outdoor figure, and a hearth gives full warmth.
- Do not wait on the river road or in the bush; you are exposed and the clock does not care.
- The prompt "Z wait an hour; at night, sleep until 07:00" appears in the help panel (`H`).

## See also

[[Needs and Survival]] - [[Calendar and Seasons]] - [[Fire and Warmth]] - [[Multiplayer]] - [[Controls]]
