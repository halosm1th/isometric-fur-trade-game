---
title: Fishing
aliases: [Ice Fishing, Fishing Line, Fresh Fish]
tags: [gameplay, system, fishing, food]
status: partial
---

# Fishing

**Status:** Partial. Open-water line fishing works for the player. Ice fishing (a hole, a chisel, a net) works for NPC households but the player's line refuses in winter ("You could jig through the ice if you had the patience - not yet."). Code: `PlayerController.UseSlotServer` (`FishingLine`), `Scripts/Core/Tiers.cs` and `Scripts/Agents/Goals` (NPC `FishGoal`).

![[item-fishing_line.png|64]] ![[item-fish.png|64]] ![[item-fishing_net.png|64]] ![[item-ice_chisel.png|64]]

Fish is **fresh food**: it feeds 22 Food, spoils like fresh meat, and (like venison, goose, hare, ptarmigan, berries and spruce beer) **resets the scurvy clock**. In winter, between the October deer and the May geese, it is one of the few fresh foods in the country, so fishing is a survival skill ([[Health, Wounds and Illness]]).

## Your own line

You start with **one fishing line** (0.5 lb) in your pack.

1. Stand at the edge of open water (a river, lake or shore; the game checks for water within about a cell and a half).
2. Select the line on the hotbar and use it (or click).
3. **Open water only** (not in Winter). There is a **45% chance** of landing **1 or 2 fish** ("You land 2 fish."), otherwise "Nothing on the line."
4. A **3-second cooldown** applies (it shares the attack cooldown, so the reload bar shows over your head).

If you try away from water it says "Cast where there's water." In winter the line will not work through ice for the player, so winter fish must come from the stores, an NPC household, or trade.

## How the households do it

- **Women fish for the pot** when food runs short in open water, and in winter whenever anybody *in the house* has gone about a fortnight without fresh food, not only when the larder is low.
- **Ice fishing.** The household needs a line (every house is issued them), and optionally an **ice chisel** (4 lb) to keep the hole open and a **fishing net** (6 lb) to take more. A hole takes about four hours and yields nought to two fish, where open water gives nought to four. The tile for the hole is `tile-ice_hole`.
- The Factor treats three or more men a month without fresh meat as a food shortage and sends hunters.

![[tile-ice_hole.png|96]] ![[tile-fish_trap.png|96]]

## Items

| Item | Weight | Worth (MB) | Use |
|---|---|---|---|
| Fishing line | 0.5 lb | 0.5 | Open-water fishing |
| Fish | 1.0 lb | 0.1 | Food 22; fresh |
| Ice chisel | 4 lb | 1 | NPC ice fishing |
| Fishing net | 6 lb | 2 | NPC fishing, more fish |

See [[Tools]] and [[Food and Drink]].

## Tips

> [!tip]
> Fish when you are near water and idle: it costs nothing but a few seconds and a fresh-food reset. Kept fish spoils quickly in summer (about 12% a day in a cache, half that in winter).

## See also

[[Hunting]] - [[Trapping]] - [[Gathering and Gardens]] - [[Needs and Survival]] - [[Food and Drink]]
