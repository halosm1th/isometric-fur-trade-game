---
title: Trading and Barter
aliases: [Barter, Haggling, Trade Session, The Counter]
tags: [system, trade, barter, economy]
status: implemented
---

# Trading and Barter

**Status:** Implemented. Trade is the core fantasy, so it is a **session**, not a shop (design doc 3.6.1). You open with a gift, read what you can see of the other man's position, press for a better price, and every push spends his patience. Push too far and he walks, possibly across the river.

![[item-trade_goods.png|64]] ![[item-beaver_pelt.png|64]] ![[item-tobacco.png|64]] ![[item-rum.png|64]]

## Who you can trade with

| Counterpart | Menu | You sell | You buy | Paid in |
|---|---|---|---|---|
| Factor, Clerk, Postmaster (own company) | "Trade" | Furs only | Anything on the shelf except furs | Trade goods ("furs at 1.00 MB in goods per MB, stores at cost") |
| Nor'Wester partner (if you are NWC) | "Trade" | Furs | Shelf | Trade goods |
| Cree / Assiniboine chief | "Trade" | Goods, tools, weapons, clothing | Meat, hides, **furs** | Beaver pelts |

Trying to trade across the river is refused: Nor'Westers are not served at the Company counter and vice versa. Bands may trade with either post subject to their standing (see [[Standing]]).

## The session: patience, margin and presses

Every session starts with `Patience = 1.0` and a `Margin` (the price moved your way as a fraction):

`Margin = 0.08 x Trade skill + clamp(his regard, -40..60) / 500`, minus 0.08 if the exchange is crude.

The price multiplier ("Swing") is `1 + clamp(Margin, -0.45, +0.75)`. You sell at `value x lot size x post price x Swing` and buy at `value x lot size / Swing`; food at a post costs the post's food multiplier (default 1.2).

### The moves

| Move | Effect |
|---|---|
| **Open with a gift** (band) / "Put something in his hand" (post) | Margin +0.05 (+0.12 for tobacco) plus 0.01 per MB of worth; Patience +0.25 (+0.4 for tobacco, max 1.4); his regard +4 (+8 tobacco) plus the worth. A pipe filled and passed is worth more than the leaf. |
| **Press him for a better price** | Chance = `0.42 + 0.4 x Trade + regard/400 (clamped -40..60) + Need x 0.3 + (0.08 if gift given, else -0.05) - 0.15 x presses - 0.12 if crude - 0.1 x his Ambition`, clamped to 3-92 percent. A hit moves Margin by +0.07 + 0.05 x Trade and costs 0.22 patience; a miss costs 0.34. +0.012 Trade skill either way. |
| **Allow that he has a point** (after a press) | Margin -0.05, Patience +0.3 (max 1.2), one press forgiven, his regard +1.5 |
| **Tell him the muskets are not for trade** | Only when the band is at odds with its neighbours and you carry a musket. Band standing with the Company -5, his regard -6, and you may not offer guns this session. |
| **Break off** | Ends the session cleanly. |

Each press's mood line tells you where you stand: "He has all day" (> 0.85), "still easy" (> 0.6), "beginning to shift his feet" (> 0.35), "had about enough" (below).

**Walking out.** When Patience hits 0 or less he walks: he will not sit down with you again for **6 game hours** (`Sulking`), his regard falls by 5 and a band takes it as an insult (-4 standing with the Company). The text for a band reads "The Nor'Westers' house is across the river, and he knows the way."

## Reading the other man ("tells")

Each session shows up to three tells, said as things seen. A sharper trader (Trade 0.35+ or regard 30+, "sharp"; Trade 0.6+ or regard 55+, "sharper") notices more:

| Tell | Underlying fact |
|---|---|
| "The children at the fire looked thin as you came in." | Band food under 4 days |
| "There is little hanging in the smoke for the time of year." | Under 10 days (sharp) |
| "He asks after guns and ball before he looks at anything else." | Band at odds with its neighbours |
| "He lets slip he is already in the Nor'Westers' book." | Band owes the NWC over 8 MB (sharp, not crude) |
| "His eyes keep going to the kegs." | Band rum taste over 40 (sharp) |
| "He has not forgotten how the Company last dealt with his people." | Band standing under 25 |
| "More packs behind him than he means you to count." | Over 20 beaver and caribou hides (sharper) |
| "He glances at the calendar: N days to the ship, and the warehouse not full." | Post under 70 percent of quota before 20 Aug |
| "The stores are thin and he knows it." | Post under 12 food days |
| "The other house was at his door lately." | Post raided recently |
| "The book in front of him does not quite add up." | Post skimmed over 10 MB (sharper) |
| "He is a hard man over a price." | Ambition above 0.65 (sharper) |
| "He is hurt..." / "hungry and keeps looking at your pack" | Health under 60 / Hunger under 40 |
| "Nothing has been given and no pipe filled. He notices." | No gift yet, at a band |

### His need

`Need()` (0..1) rises when a band has under 10 food days (up to +0.7), when it has a rival band at odds and you carry a musket (+0.4), and when a post is short of its quota with the ship near (+0.8 x shortfall x pressure) or low on food (+0.2). Higher Need makes presses land more often, and food sells for up to 50 percent more.

## Language

If the trader is a band person and your fluency with his tongue is **below 0.5**, the trade is **crude**: "fingers and pointing". You pay about a fifth more on a lot (`cost + 20 percent`) and margin is -0.08. Fluency is covered on [[Languages]]. A wife or a woman of the post within 300 px interprets (fluency 1.0 for that scene).

## What your sale does to the world

- Selling furs to your own company: adds to `FursTakenMb` and `GoodsPaidMb`, and marks that you "stood guard over the quota" for the year's account.
- Selling a **musket** to a band at war: its standing with the Company +6, and a journal line ("a gun of your selling, and they are at war").
- Trading your own goods for a camp's furs is **private trade**: it creates a witnessed event (weight 0.8) that can reach the Factor and cost standing at the sailing. See [[Standing]], [[Gossip, Lies and Leverage]].
- Selling a sealed packet meant for the other company is separate: see [[Information and Messages]].

## Tips

> [!tip]
> Always open a band trade with **tobacco**. It is the best gift per MB in the game and adds a long lasting patience bonus. Watch the tells before you press. Do not press more than twice on a low-patience trader.

> [!tip]
> Trade goods are the coin. If the shelf is empty ("You have nothing he wants, nor he anything you can pay for") then go gather furs first. See [[Fur Prices]] and [[Barter Rates]].

## See also
[[Dialogue and Conversation]] | [[Languages]] | [[Standing]] | [[Credit and Debt]] | [[Made Beaver]] | [[Trade Goods]] | [[Furs and Pelts]] | [[Fort Ledger]] | [[Band Ledger]]
