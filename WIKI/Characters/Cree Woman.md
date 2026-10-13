---
title: Cree Woman
aliases: [Band Woman, Cree Daughter, Women of the Band]
tags: [character, role, cree, nation]
status: implemented
---
# Cree Woman

**Status:** Implemented

![[char-cree_woman.png|140]] ![[sheet-char-cree_woman.png|420]]

| | |
|---|---|
| **Role values** | `Role.CreeWoman` (7) and `Role.CreeDaughter` (8) |
| **Faction** | Any nation (this sheet is shared by all women of the bands except Metis) |
| **Behaviour** | `CreeWomanBehaviour` |
| **Sprite sheet** | `cree_woman` (idle x3, walk x3, eat, drink, carry_wood, scrape, death) |
| **Spawn age** | Women 20-38; daughters 16-20 |
| **Carry capacity** | 45 lb |
| **Skills** | Woodcraft 0.3-0.6; others low |
| **Kit** | Hide Coat, Knife |
| **Tint** | Varies by nation (e.g. Assiniboine Wiyaka 0.9, 0.78, 0.62) |
| **Cast at York** | Cree: Niska, Sakihitowin, Wapun, Atim-iskwew; daughters Kimiwan, Wawatew. Assiniboine: Wiyaka, Ptesan, Wakanda; daughter Winona |

## The camp's economy

The women do most of the camp's food work. In priority order:

1. Tend the fire (threshold 5).
2. Fill the water barrel (under 10).
3. Fish: open water, if under 8 days of food, 50 per cent; in winter, if under 14 days or the house wants fresh food, 60 per cent.
4. Dry venison (3 or more), salt geese (8 or more), **make pemmican** (8 or more dried meat, 50 per cent).
5. Chop wood if the woodpile is under 12.
6. Otherwise wander the camp.

"Scraping" is the hide work shown by the `scrape` animation. Fresh meat and fish from the women's fishing keep the whole camp free of scurvy ([[Health, Wounds and Illness]]).

## Daughters and marriage

A `CreeDaughter` is marriageable. "My father decides whom I marry. But he listens to me." A suitor needs rapport 30 with the father and 8 MB of gifts; the marriage turns her into a `Wife` of the post ([[Marriage and Country Wives]], [[Favours]] "Speak for ... who wants to marry your daughter").

## Singing

In the evening at the fire, a Cree woman may tell the children a story of the Thunderbirds, or sing a lullaby ([[Dialogue and Conversation]]).

## See also

[[Country Wife]], [[Cree Chief]], [[Cree Hunter]], [[Children]], [[Cree]], [[Roles]]
