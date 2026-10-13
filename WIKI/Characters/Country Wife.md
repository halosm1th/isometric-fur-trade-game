---
title: Country Wife
aliases: [Woman, Wife, Marriage a la facon du pays, Women of the Post]
tags: [character, role, marriage]
status: implemented
---
# Country Wife

**Status:** Implemented (the Woman and Wife roles; the wider marriage-track system is partial)

![[char-country_wife.png|140]] ![[sheet-char-country_wife.png|420]]

| | |
|---|---|
| **Role values** | `Role.Woman` (3) and `Role.Wife` (10) |
| **Faction** | `HBC` (or `NWC`; Metis women also wear this sheet) |
| **Behaviour** | `HouseholdBehaviour` |
| **Sprite sheet** | `country_wife` (idle x3, walk x3, eat, drink, carry_wood, carry_water, carry_baby, scrape, death) |
| **Carry capacity** | 45 lb |
| **Skills** | Woodcraft 0.3-0.6; others low |
| **Kit** | Point Blanket, Knife |
| **Tint / scale** | Pinkish (1, 0.8, 0.85), scale 0.72 |
| **Cast at York** | Nancy Isbister, Margaret Flett, Jane Spence |

## Routine

The women are the post's household economy. In order:

1. Keep the fire (threshold 5).
2. Fill the barrel if it has under 12 water.
3. **Fish.** In open water, if the post has under 14 days of food, 50 per cent chance; in winter, if the house wants fresh food or has under 20 days, 60 per cent, through the ice. Fresh food is what stops scurvy ([[Health, Wounds and Illness]], [[Fishing]]).
4. Salt geese (8 or more in the store) or dry venison (3 or more).
5. Drink if thirst is under 60; else wander the work cell.

She will not carouse (the evening dram is for men only) and does not fight.

## Marriage

A Company man may marry a daughter of a band "in the custom of the country": the player needs rapport 30 with her father and gifts worth 8 MB. On marriage she becomes a `Wife`, moves into the post's depot and her family becomes the player's family; the Company allows one wife ([[Marriage and Country Wives]], [[Favours]]). Children are born a year or so later ([[Children]]). In the historical record Simpson's later marriages echo this (design doc 4.3, [[George Simpson]]).

## See also

[[Cree Woman]], [[Children]], [[Labourer]], [[Hudson's Bay Company]], [[Heirs and Succession]], [[Roles]]
