---
title: Credit and Debt
aliases: [Debt, Advances, Credit]
tags: [system, economy, credit]
status: partial
---

# Credit and Debt

**Status:** Partial. Per-band debt, credit for food, debt repayment out of furs and the standing that gates credit are implemented. Calling in debts, rivals forgiving them, and Simpson's 1821 restructuring (design doc 13.3) are planned.

![[ui-crest-hbc.png|90]] ![[ui-crest-nwc.png|90]] ![[ui-crest-cree.png|90]] ![[ui-crest-assiniboine.png|90]]

The trade ran on credit: a company advanced a band its winter goods against next season's furs. The game tracks that as a number per band, per company.

## What is tracked

Each [[Band Ledger]] holds (in MB):
- `StandingWithCompany` (HBC), start 50 (Cree) or 40 (Assiniboine)
- `StandingWithNwc`, start 50 (Cree) or 45 (Assiniboine)
- `DebtMb`: what the band owes the HBC post
- `DebtNwcMb`: what it owes the Nor'Westers
- `RumTaste`, 0 to 100
- `StandingWithOtherBand`, start 65

A FortLedger reads and writes whichever pair belongs to its company (`Company`), so York and the Nor'Westers' house share code but keep separate books ([[NWC Ledger]]).

## Rules

- **Who trades.** A band trades with the Company post only while standing with the Company is above 20 (`WillTrade`); with the Nor'Westers while its NWC standing is above 20.
- **Credit for food.** A band that buys food at a counter and is above standing 40 with less than 15 MB of debt can take up to 5 units on credit: each adds 1 MB of debt. The post keeps a reserve and will not sell the last 20 pieces of a food.
- **Repayment first.** When the band sells furs at that counter, existing debt is worked down first: each fur is taken in at its value times PeltPrice and *no goods are paid* for it until the debt is gone. The notice reads 'N's debt at the post is worked down by X MB of furs; Y MB still owing'.
- **A fair deal is remembered.** Each sale lifts the band's standing with that counter by 2 (0.5 if the price is under 1.0).
- **Drink buys goodwill.** Each dram of rum given in a fur payment adds 1.5 standing for that counter and 4 to the band's rum taste (max 100). A band with a rum taste above 40 'keeps looking at the kegs' (a tell). See [[Dram of Rum]].

## Standing moves

Daily, a band's standing with the Company drifts by:

| Situation | Change per day |
|---|---|
| Hungry (under 3 food-days) and price under 0.9 | -3 |
| Hungry otherwise | -1 |
| Fed and price 1.2 or better | +1 |
| Fed otherwise | +0.3 |

One-off hits: an insult at the camp -4; a member killed by an HBC man -25 (NWC -25 for an NWC killer); refusing guns in a war -5; an insult 'haggled at past all patience' -4. Standing with the other band mends 0.3 a day.

When a band slips to 20 or below the headline reads 'have had enough of the Company; they will not bring their furs to the post' and the journal names the cause. When it recovers a notice says they are willing again.

## Reading it in play

- At a camp a tell hints 'He lets slip he is already in the Nor'Westers' book' once a band owes the NWC more than 8 MB, if you read tells well.
- The yearly reckoning at breakup tells a band player how they stand: 'well' above 65, 'civilly' above 35, 'coldly' if they still trade, 'not at all' otherwise, with the debt left on the books.
- Debt does not compound, accrue interest or run out. It only falls through fur sales.

## Planned

Design doc 13.3 expects debt to be a coercive tool: a rival forgiving a trapper's debt to win him, defaults, and an 1821 restructuring that calls in what predecessors let slide. None of that is wired. Neither is the company solvency ledger of 13.7 that would cap the credit a company can extend.

## See also

- [[Economy]]
- [[Band Ledger]]
- [[Fort Ledger]]
- [[Standing]]
- [[Rivalry]]
- [[Cree]]
- [[Assiniboine]]
