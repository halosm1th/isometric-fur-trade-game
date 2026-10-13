---
title: NWC Ledger
aliases: [NwcLedger, Nor'Westers' Store]
tags: [system, economy, nwc, ledger]
status: implemented
---

# NWC Ledger

**Status:** Implemented (`Scripts/Economy/NwcLedger.cs`, `Council.cs`).

![[ui-crest-nwc.png|110]]

The North West Company's wintering post across the river from York Factory. Same books as the Bay post, different masters: no ship and no London, a canoe brigade from Fort William that restocks it each July, and a partner who sets his own price, usually a shade better than the Company's. See [[Nor'Westers' House]] and [[North West Company]].

## Settings

| Setting | Value |
|---|---|
| PeltPrice | 1.3 |
| Quota | 80 MB |
| RumShare | 0.35 (0.2 or 0.4 by council vote) |
| OutfitPieces | 90 (60, 90 or 130 by council vote) |
| PriceAtDiscretion | false |

## Starting stores

[[Pemmican]] 40, [[Dried Goose]] 40, [[Tankard of Ale]] 30, [[Firewood]] 30, [[Trade Goods (item)]] 70, [[Trade Musket]] 4, [[Steel Trap]] 8, [[Beaver Pelt]] 10, [[Dram of Rum]] 40, [[Copper Kettle]] 2, [[Water]] 16, [[Tumpline]] 6, [[Fishing Line]] 5, [[Twist of Tobacco]] 25, [[Bunch of Beads]] 15, [[Powder and Ball]] 15.

## The July brigade (12 July)

The brigade lands the year's outfit and carries the furs off:
1. Every fur in the store goes to Montreal ('N MB of furs off to Montreal').
2. The council sits ([[Councils and Accords]]) and its minutes are bound into a packet for the partner.
3. The store receives `OutfitPieces` of trade goods, one third as many of pemmican, 60 rum (free-rum policy) or 25 (sparing), 4 muskets, 8 steel traps.
4. A canoe party of 3 voyageurs lands.
5. An NWC player gets his year's reckoning ('the brigade is away for Fort William', returns against 80 MB).
6. If the Bay partner has been left to his own judgment, his price becomes at least 1.25 times York's (up to 3).

## The council

At Fort William the partners vote by shares on the standard against the Company, the outfit for the Bay district, liquor, whether to press the Bay, and the partner's own conduct. Votes follow the shares councillors hold; the agenda is only what the room knows about. The options and effects:

| Question | Options and effect |
|---|---|
| The standard | Hold the price; undercut the Company by a fifth (price at least 1.2 times York's); let the Bay set its own price |
| The outfit | Lean (60 pieces), usual (90) or full (130) |
| Liquor | Restraint (rum share 0.2) or plenty (0.4) |
| The Bay | Keep the peace (tension -20) or press them hard (+15, raids sanctioned) |
| The partner | Censure, confirm, or a second share |

## Credit

Bands hold separate NWC standing and debt (StandingWithNwc, DebtNwcMb). The Nor'Westers 'lean on' rum more than the Company: 35 percent of a payment by default. See [[Credit and Debt]] and [[Rivalry]].

## See also

- [[Fort Ledger]]
- [[Economy]]
- [[Fur Prices]]
- [[Fort William]]
- [[Councils and Accords]]
- [[William McGillivray]]
