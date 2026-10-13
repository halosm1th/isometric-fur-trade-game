---
title: Weather
aliases: [Sky, Blizzard, Rain, Gale, Exposure]
tags: [gameplay, system, weather, survival]
status: implemented
---

# Weather

**Status:** Implemented in its simplest useful form (a sky state rolled from the season). Code: `Scripts/Core/Weather.cs`. On-screen weather particles are listed among the project's longer goals and are not in the game; weather shows as dimmed light and as the word on the calendar HUD.

The weather is one of six **sky states**. It is re-rolled each time the current one runs out (every 6 - 30 game hours; blizzards last 6 - 18, gales 4 - 12). The roll depends on the [[Calendar and Seasons|season]].

## Sky states and what they do

| Sky (HUD word) | Exposure multiplier (cold bites harder) | Light | Other effects |
|---|---|---|---|
| Clear ("clear") | x1.0 | 100% | Fire spreads fast (x1.2) |
| Overcast ("overcast") | x1.05 | 90% | |
| Snow ("snowing") | x1.3 | 80% | Dampens fire (x0.35) |
| **Blizzard** ("BLIZZARD") | **x2.2** | 55% | Fire x0.5; map travel and the river road both stop; hunters and wood parties turn for home |
| Rain ("rain") | x1.4 (wet clothes) | 80% | Fire x0.25; counts as "wet" |
| **Gale** ("gale", internally *Storm*) | x1.7 | 60% | Fire x1.6; stops travel; counts as wet |

The exposure multiplier only applies when you are **outdoors**. Indoors, shelter blocks the wind and exposure is multiplied by 0.35 instead (see [[Needs and Survival]]).

## What each season rolls

| Season | Clear | Overcast | Snow | Blizzard | Rain | Gale |
|---|---|---|---|---|---|---|
| Winter (Nov-Apr) | 45% | 25% | 20% | 10% | - | - |
| Breakup (May-Jun) | 40% | 30% | 10% | - | 15% | 5% |
| Open water (Jul-Aug) | 50% | 25% | - | - | 18% | 7% |
| Freeze-up (Sep-Oct) | 35% | 30% | 13% | 7% | 15% | - |

These are the exact thresholds in the code (winter: <0.45 clear, <0.70 overcast, <0.90 snow, else blizzard, and so on).

## Effects on play

- **Cold.** A blizzard multiplies outdoor cold by 2.2. In deep winter at night the base exposure is 6 warmth points an hour; in a blizzard that is 13.2 an hour before clothing. A capote (warmth 4.5) leaves a loss of 8.7 an hour, so you freeze in about twelve hours outdoors. Get to a fire or a roof. See [[Fire and Warmth]].
- **Travel.** [[Travel and Wayfinding|Map travel]] and the river road both stop in a blizzard or gale with a message ("The weather closes in. Nobody walks the road in this - find shelter or make a fire.").
- **Pacing.** A blizzard or gale slows the clock slightly (pacing x0.8; see [[Calendar and Seasons]]).
- **Fire.** Rain and snow damp fires; a gale drives them. See [[Fire and Warmth]].
- **Light.** The light multiplier dims the scene; firelight shows only once the day fades.
- **Social.** A long blizzard (8 hours or more) is recorded as a shared memory in every household: "a blizzard shut everyone in by the fire", which NPCs can retell. See [[Memory and Knowledge]].
- **Multiplayer.** The host's sky is sent to clients and forced onto them for six hours at a time; the host's console can force a blizzard, clear sky or gale.

## Tips

> [!tip]
> Winter nights are the killers, not winter days: day exposure is 3 an hour, night 6. A capote and a point blanket worn together (4.5 + 5) hold off a plain winter night entirely. A blizzard changes that. Lay a fire *before* the sky turns.

> [!warning]
> Clothing counts only when **worn**. Check the Wear/Take off buttons in the pack ([[Inventory and Hotbar]]).

## See also

[[Needs and Survival]] - [[Fire and Warmth]] - [[Calendar and Seasons]] - [[Travel and Wayfinding]]
