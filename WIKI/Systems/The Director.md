---
title: The Director
aliases: [Director, Pacing, Simulate Then Curate, Drama Manager]
tags: [system, director, pacing, ai]
status: implemented
---

# The Director

**Status:** Implemented (design doc 2.2, "simulate, then curate"). The director **never makes an event, never changes an outcome, and never hands anyone knowledge the propagation system would not have carried.** It only biases things that were going to happen anyway: which of a man's memories he brings out, which of two equally good lodges a party walks to, and whether a thing that has been waiting to happen happens this afternoon or next week.

> [!info]
> The honesty rule: switch it off with the launch argument `--nodirector` and the world must still be valid. "Every beat it put on stage would have happened somewhere; it decides what is on stage, never what is in the play." The main loop logs `[director] off: the world runs with nothing curated`.

## 1. Pacing: should a waiting thing happen now?

`Director.MayActNow(kind, weight, where)` is asked by systems that already have a pending event (a [[Rivalry]] raid, a blood debt, a message landing). It returns false ("not yet, ask again later") when:

| Rule | Value |
|---|---|
| Nobody could possibly witness it (nearest player over **6500 px**) | always allowed |
| **Spacing** since the last piece of real trouble ("quiet hours", beats with weight 1.5 or more) | needs `quiet >= (5 + 12 x weight)` game hours; after 48 hours of quiet the spacing is multiplied by **0.4** |
| A headline just reached the player | wait **3 hours** |
| The same kind of beat | wait **40 hours** (**18** after 72 hours of quiet) |

Beats the director remembers (it keeps at most 64): `Saw(kind, weight)`. Ordinary news (a birth, a ship, a death at another post) is remembered but does not count as trouble. Weights in use: `raid` 2, `blood` 1.5, `headline` 1.

Where it is consulted:
- **Raids** between the companies: `OrderRaid` asks `MayActNow("raid", 2)` at the target.
- **Blood debts**: a holder only attacks you when `MayActNow("blood", 1.5)` agrees.
- **Headlines**: each headline registers as a beat.

## 2. Whose route crosses whose

`Director.Equal(options, score, at, tolerance=0.12)`: among options the chooser thinks are equally good (within 12 percent of the best score), pick the one that passes nearer a player. Used for where a hunting party goes.

## 3. Who talks about what

`Director.Relevance(text, player)` scores how much a line touches your life:

| Match | Score |
|---|---|
| Your name | +3 |
| Your spouse's name | +2 |
| A forebear of your house | +2 each |
| An open grudge against or held by you (the other name) | +2 each |
| A campaign keyword for a beat that is near (window: 120 days before to twice its window after) | +1 |
| A keyword of one of your active [[Ambitions]] | +1 each |

`MostRelevant` picks the best of several **true** things; ties keep the caller's order. It is used by the **Focus panel's "Heard"** line, the year's story ([[The Year Told]]), and any bark that chooses between true memories. It never invents.

## 4. Campaign pressure

The campaign script (see [[Campaigns and Adherence]]) feeds the director keywords for beats due soon, so news that bears on what is coming (the union of the companies, Seven Oaks) tends to be told first.

## Tips

> [!tip]
> If a fight "wouldn't happen" all week and then happens right after a long quiet stretch, that is the director letting the next one through. The debug readout (`Describe()`) reports "Quiet *n* h; beats held: *n*".

## See also
[[Rivalry]] | [[Grudges and Vendettas]] | [[Memory and Knowledge]] | [[The Year Told]] | [[Ambitions]] | [[Campaigns and Adherence]] | [[Design Principles]]
