---
title: Dram of Rum
tags: [item, food]
status: implemented
---

# Dram of Rum

**Status:** Implemented

![[item-rum.png|96]]

| | |
|---|---|
| **Category** | [[Food and Drink]] (Drink) |
| **Weight** | 1 lb |
| **Worth** | 0.8 MB ([[Made Beaver]]) |
| **Stackable** | Yes: any number of the same item share one slot |
| **Effects** | Drink: +10 thirst<br>Warms +8 (one-off)<br>Intoxication +35 |
| **Item id** | `Rum` |

## Description

Trade liquor. It buys goodwill quickly and cheaply, and that is the trap: bands come to want it, and the post that has it gets the furs.

## Mechanics

- Quenches only 10 thirst; warms by 8 and adds 35 intoxication, the strongest drink in the game.
- Part of a fur payment goes out as rum when the post has it: 15 percent share at the Company posts, 35 percent default at the Nor'Westers (a council can set it to 20 or 40 percent).
- Each dram given raises the band's rum taste by 4 (to a maximum of 100) and the band's standing with that counter by 1.5; see [[Band Ledger]] and [[Credit and Debt]].
- Design intent (planned): a trade liquor can be diluted and tested with the Fire Test (design doc 13.4); not built.

## Where to find it

- York starts with 12 and the ship lands 24 a year; the Nor'Westers start with 40 and the brigade lands 60 (free-rum policy) or 25 (sparing) each July.
- A sailor will sell you 6 for 4 pieces of trade goods during the ship's visit.

### Starting stock (as coded)

| Where | Count |
|---|---|
| York Factory stores | 12 |
| The annual ship's cargo | 24 |
| Nor'Westers' store | 40 |

## See also

- [[Food and Drink]] | [[Items]]
- [[North West Company]]
- [[Band Ledger]]
- [[Councils and Accords]]
