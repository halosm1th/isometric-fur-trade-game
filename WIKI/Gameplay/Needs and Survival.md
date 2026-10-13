---
title: Needs and Survival
aliases: [Needs, Hunger, Thirst, Warmth, Survival, Status Bars, HUD Bars]
tags: [gameplay, system, survival, needs]
status: implemented
---

# Needs and Survival

**Status:** Implemented. Code: `Scripts/Agents/Needs.cs`, `Scripts/Agents/Agent.cs`, `Scripts/UI/NeedsHud.cs`.

The player has **four bars**, shown bottom-left, each from 0 to 100 where 100 is fully satisfied. They drain in *game hours*, so the same code that ticks you every frame ticks an NPC once a day. There is no separate sleep or fatigue bar in the simulation (a "fatigue" icon exists in the art folder but is not wired to any mechanic).

| Bar | Icon | Colour | Starts | What drains it |
|---|---|---|---|---|
| **Health** | ![[ui-need-health.png\|28]] | red | 100 | Damage from blows, fire, starvation, thirst, freezing, fever, scurvy |
| **Warmth** | ![[ui-need-warmth.png\|28]] | orange | 100 | Cold exposure beyond what your clothing and fire offset |
| **Food** | ![[ui-need-food.png\|28]] | green | 100 | Hunger, scaled by activity |
| **Water** | ![[ui-need-water.png\|28]] | blue | 100 | Thirst, a steady 1.8 an hour |

Other status icons exist for conditions that show on the HUD: ![[ui-need-cold.png|24]] cold, ![[ui-need-fever.png|24]] fever, ![[ui-need-scurvy.png|24]] scurvy, ![[ui-need-drunk.png|24]] drunk, ![[ui-need-wounded.png|24]] wounded, ![[ui-need-load.png|24]] load, ![[ui-need-standing.png|24]] standing and ![[ui-need-fatigue.png|24]] fatigue. In the current build the HUD prints most of these as text in the line under the bars (`Indoors  Load 62/90 lb  |  Trade musket  |  FEVER`).

## Rates

| Need | Base rate | Notes |
|---|---|---|
| Food | **1.1 per hour** (empty in about 4 days standing still) | Multiplied by activity: x1 still, x1.5 walking, x2.0 walking while overloaded, x2.4 sprinting. Multiplied again by x1.3 in winter. |
| Water | **1.8 per hour** (empty in about 2.5 days) | Not scaled by activity. |
| Warmth | see below | Exposure minus clothing, plus fire. |

### Warmth in detail

Each hour: `warmth change = fire gain - max(0, exposure - clothing)`.

- **Exposure** (points per hour, blended between day and night by daylight): Winter 3 by day / 6 by night; Breakup and Freeze-up 1 / 2.5; Open water 0 / 1. Outdoors it is multiplied by the [[Weather]] (blizzard x2.2 and so on); indoors by x0.35 (a building blocks the wind but is not heated).
- **Clothing** warmth is the sum of what you are *wearing*: capote 4.5, point blanket 5, hide coat 7, buffalo robe 8. Layers stack. An item carried but not worn counts only as weight.
- **Fire** gives `heat x 14` an hour when you are indoors or the fire is enclosed (a hearth), but only `x 0.4` at an open campfire in the wind. Heat falls off with distance from the fire ([[Fire and Warmth]]).

Example: a winter night outdoors in clear weather with a capote loses 6 - 4.5 = 1.5 points an hour; with capote and point blanket, nothing.

## What happens when a bar runs low

- **Weak (below 25).** Any of Food, Water or Warmth under 25 reduces your **capability factor** from 1.0 toward 0.5. Capability multiplies your walking speed and your blow damage. This is the "visible spiral": you slow down before you die.
- **Empty.** At zero, each causes damage to Health every hour: Warmth 3, Water 5, Food 3 (these add together).
- **Recovery.** If you are not taking any of that damage *and* Food is above 55, Water above 45 and Warmth above 50, Health heals **1 point an hour** (0.5 if you are moving fast, activity above 1.2).
- **Death.** At Health 0 you die. See [[Health, Wounds and Illness]].

Other things that move the bars: fever (-0.35 Health and -0.4 Food an hour), scurvy (-0.12 Health an hour), and drink (ale and rum give Warmth but also intoxication). Fire damage hits for 9 points a second.

## Keeping the bars up

| Need | How |
|---|---|
| **Food** | Use food from the hotbar (1-9). Pemmican 45, dried meat 35, goose 35, dried goose 30, fresh meat 30, loaf of bread 25, fish 22, hare 16, ptarmigan 14, berries 10. See [[Food and Drink]]. At your own post the **mess call** feeds you automatically from the stores when Food is under 70 and you are near the post, and `E` at the stores counter draws a ration if you carry fewer than three food items. |
| **Water** | `Q` at a river, lake or shore with a kettle fills three water and refills the bar; without a kettle, you just drink. Carried water gives 50. In winter you must melt snow at a lit fire (`Q` beside it): costs 0.3 hours of fuel without a kettle, 0.6 with one (3 water). Drinking from frozen water costs 8 warmth. At your post the water barrel refills your Water. |
| **Warmth** | Wear clothing, stay indoors, stand near a fire, feed it ([[Fire and Warmth]]). |
| **Health** | Keep the other three high; ask a woman of the post to dress a wound (+25 Health, once a day, uses a water). See [[Health, Wounds and Illness]]. |

At a post, routine needs largely resolve themselves, as they do for NPCs; a post whose stores empty is the crisis the design aims at. Away from a post, survival becomes planning (what goes in the pack, whether to push on or make camp). The "what matters now" panel warns you in plain words: "you are freezing - get to a fire, or make one" (Warmth under 25), "you are starving" (Food under 25), "you are parched" (Water under 25), "you are badly hurt" (Health under 35).

## Load

Your pack also has a weight limit; exceeding it slows you and burns food faster. See [[Inventory and Hotbar]].

## Tips

> [!tip]
> Eat *fresh* meat (venison, goose, fish, hare, ptarmigan) or berries at least every six weeks to stay clear of scurvy; dried meat and pemmican do not count. See [[Health, Wounds and Illness]].

> [!warning]
> Winter is hardest at night. Do not leave a post in a blizzard without a fire-making kit (flint and firewood).

## See also

[[Weather]] - [[Fire and Warmth]] - [[Food and Drink]] - [[Clothing]] - [[Sleeping and Resting]] - [[Inventory and Hotbar]] - [[Health, Wounds and Illness]]
