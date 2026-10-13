---
title: Barter Rates
aliases: [Prices, Haggling Math, Exchange Rates]
tags: [system, economy, barter]
status: implemented
---

# Barter Rates

**Status:** Implemented (`Scripts/Economy/Barter.cs`). The conversation that surrounds it is on [[Trading and Barter]].

![[item-trade_goods.png|96]] ![[item-tobacco.png|96]] ![[item-stroud.png|96]]

A trade is a session, not a shop. You may open with a gift, read the other man's *tells*, and press for a better price; each press spends his patience. This page records the numbers.

## Two counters, two currencies

| Counter | Wants from you | Pays you in | You buy with |
|---|---|---|---|
| A company post's clerk or Factor | Furs only | [[Trade Goods (item)]] | Trade goods |
| A band person at a camp | Goods, tools, weapons, clothing | [[Beaver Pelt]]s | Trade goods |

Lots are priced together, not piece by piece: the whole lot goes over the counter at once and the haggling shows in the rounding.

## The formulas

Let `W` be the item's worth in MB, `n` the lot size, `Swing = 1 + clamp(Margin, -0.45, +0.75)`.

- **You sell:** `pay = max(n, round(W x n x PeltPrice x Swing x want))`. `PeltPrice` is the post's price (1 at a camp). `want` is 1, or `1 + 0.5 x Need` for food when his need is above 0.2.
- **You buy:** `cost = max(n, ceil(W x n x mult / Swing))`.
  - `mult` is 1 for non-food. For food it is the post's food multiplier (1.5 at York; 1.2 at a camp).
  - A band at odds with its neighbours charges 1.3 times for a weapon (what he is short of, he parts with dearly).
  - If you share no language you pay a further 20 percent on the lot ('a part more, not a piece more on every article').
- Because of `max(n, ...)` no piece costs less than one counter, however small its worth: ten [[Awl]]s at 0.1 MB cost 10, not 1.

### Margin (how far the price has moved your way)

`Margin = 0.08 x Trade skill + clamp(rapport, -40, +60) / 500` (and -0.08 if you have no common tongue with a band person).

- **Gift:** +0.05 + 1 percent of the gift's worth. A **pipe** (a [[Twist of Tobacco]]) gives +0.12 instead of +0.05, restores 0.4 patience (a normal gift 0.25), and earns 8 rapport (4) plus the gift's worth in rapport.
- **Press:** if it lands, margin +0.07 + 0.05 x skill and patience -0.22; if it fails, patience -0.34.
- **Relent:** margin -0.05, patience +0.3, rapport +1.5.
- **Hold back the guns:** at a band at odds with its neighbours, refusing to deal muskets costs 5 standing with the Company and rapport -6; a cause is stored for the journal.

### Patience

Starts at 1, capped at 1.4 with gifts. Press until it reaches 0 and he walks: rapport -5, the band's standing with the Company -4 for 'haggled at past all patience', and he will not sit down again for 6 hours. A band person may say that the Nor'Westers' house is across the river.

The chance a press lands is `0.42 + 0.4 x skill + rapport/400 + 0.3 x Need + (0.08 if a gift was given, else -0.05) - 0.15 x presses - 0.12 if crude - 0.1 x his ambition`, clamped to 0.03 to 0.92. Each press adds 0.012 to the Trade skill.

### Need (what he wants)

`Need` runs from 0 to 1. At a camp: a band under 10 food-days adds up to 0.7; one at odds with its neighbours adds 0.4 if you carry a musket. At a post short of its quota with the ship coming adds up to 0.8 (it scales from nothing to full over the 180 days before 20 August) and a food shortfall (under 12 days for 8 men) adds 0.2. The tells (a thin child by the fire, a glance at the calendar, 'the book does not quite add up') hint at these numbers; a sharper trader notices more (Trade skill 0.35, or 0.6, or rapport 30 or 55).

## Reading the table of goods

At a post (PeltPrice 1.0, Swing 1.0) a single piece sells for `max(1, round(W))` goods and costs `ceil(W)` (food `ceil(W x 1.5)`). Examples:

| Good | Worth (MB) | You sell for (goods) | You buy for (goods, non-food) |
|---|---|---|---|
| [[Beaver Pelt]] | 1 | 1 | |
| [[Wolf Pelt]] | 0.6 | 1 (min one counter) | |
| [[Trade Musket]] | 12 | | 12 |
| [[Point Blanket]] | 4 | | 4 |
| [[Capote]] | 3 | | 3 |
| [[Copper Kettle]] | 2 | | 2 |
| [[Steel Trap]] | 1.5 | | 2 |
| [[Axe]] | 1.5 | | 2 |
| [[Yard of Stroud]] | 1.5 | | 2 |
| [[Twist of Tobacco]] | 1 | | 1 |
| [[Knife]] | 0.5 | | 1 |
| [[Awl]] | 0.1 | | 1 |

Haggling moves these figures: a Swing of 1.2 turns the musket's 12 into 10, for instance. See the full list of worth values at [[Made Beaver]].

## Provisions

Posts also buy food from bands: two [[Pemmican]] for one piece of goods (a post that has fewer than 60 days' food for its men plus one). A post's 'sell food' trade gives a few days' food per piece of goods at its mark-up: `pieces = round(3 / FoodPriceMultiplier)`, so 2 pieces at York's 1.5. A band in good standing may take it on credit; see [[Credit and Debt]].

Band to post surplus sales: a camp with more than `max(30, 3 x mouths)` pemmican sells up to 20 at `round(20 x 0.3 MB x PeltPrice)` goods.

## Rivalry and prices

- Furs, packs and desertions that cross to the other counter raise [[Rivalry]] tension; open hostility starts at 70.
- The Nor'Westers' price starts 30 percent higher and responds to London's packet when intercepted. The two prices pull against each other: if the Company's falls under 0.9 hungry bands blame the Company; if the NWC's is better the band's standing drifts to the other house.
- A counter that has been raided lately is short with you, and a sharp trader sees it ('the other house was at his door').

## See also

- [[Trading and Barter]]
- [[Fur Prices]]
- [[Made Beaver]]
- [[Credit and Debt]]
- [[Standing]]
- [[Items]]
