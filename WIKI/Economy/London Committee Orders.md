---
title: London Committee Orders
aliases: [London's Orders, Quota, The Committee's Packet, Annual Orders]
tags: [system, economy, hbc]
status: implemented
---

# London Committee Orders

**Status:** Implemented as a simple background agent (`Scripts/Economy/LondonCommittee.cs`). The Governor and Committee never see the country, only the numbers. For the faction, see [[London Committee]].

![[vehicle-ship.png|320]]

Every year the Governor and Committee in London write the Factor a price and a quota. The game turns that into two numbers on the York ledger, delivered by the ship.

## The yearly cycle

| Date | Event |
|---|---|
| 20 August | The ship arrives. Her cargo (below) is landed, and the Committee's packet is carried ashore by the bosun |
| Ship docked | The Factor must open the packet; until then it does nothing |
| 15 September | The ship sails with the hold's furs. The year's accounts go home aboard her |
| About 75 days later | London 'reads' the accounts: `ReviewReturns` runs |
| Next 20 August | The new price and quota arrive |

Start-of-game values: PeltPrice 1.0, quota 60 MB, next price 1.0 and next quota 60.

## The packet

The directive is a letter 'from the Governor and Committee, London' to the Factor: *Pay [price] MB in goods per MB of fur. The Committee expects [quota] MB of returns by the ship. Keep the Indians in your interest and watch the Canadians.* When read it sets York's PeltPrice and QuotaMb and stores the text as the post's standing orders. If a Nor'Wester intercepts and reads it he learns the standard and the NWC price is raised to at least 1.15 times London's, with +6 [[Rivalry]] tension. If the bosun is killed, the packet is on his body. See [[Information and Messages]].

## How London decides

`ratio = shipped MB / this year's quota`.

| Returns | New price | New quota | Verdict |
|---|---|---|---|
| Under 0.6 | x1.35 | x0.9 | 'displeased... orders a more liberal standard of trade' |
| 0.6 to 0.9 | x1.15 | unchanged | 'notes the shortfall and allows a little more for beaver' |
| 0.9 to 1.4 | unchanged | x1.1 | 'approves... a modest increase' |
| Over 1.4 | x0.85 | x1.25 | 'gratified... trims the standard of trade' |

Price is clamped to 0.3 to 3.0 and quota to 10 to 400. London's committee can be switched off in code (`Automatic`). The notice reads 'In London, the ship home: ... The orders that will sail next year: 1.15 MB per MB, quota 66'.

Short returns make London raise what the post pays per pelt, to draw the country in; a glut tightens the price and raises the target.

## What the ship brings

The cargo landed at the wharf (`Ship.Summon`):

| Item | Count |
|---|---|
| [[Trade Goods (item)]] | 120 |
| [[Pemmican]] | 30 |
| [[Dried Goose]] | 40 |
| [[Loaf of Bread]] | 40 |
| [[Tankard of Ale]] | 30 |
| [[Trade Musket]] | 8 |
| [[Steel Trap]] | 12 |
| [[Dram of Rum]] | 24 |
| [[Copper Kettle]] | 4 |
| [[Point Blanket]] | 20 |
| [[Capote]] | 6 |

Furs go aboard only once the outfit is off. She also lands 6 recruits and an 8-man crew; there is a 35 percent chance of fever aboard. If fewer than three of her crew are left alive she cannot sail and lies stranded at the wharf. See [[Ship]].

## What she takes

The hold's furs. When she sails the notice reads 'sailed for London with N MB of furs aboard (quota was Q)'. A Factor with loyalty under 0.35 and ambition over 0.5 will have skimmed 10 percent of the beaver and wolf pelts out of the hold for himself; the loss is recorded in the post's `SkimmedMb`.

## The player's reckoning

When the ship sails, an HBC player gets a year-end account in the journal: furs shipped against quota ('made, and then some' or 'N short'), standing and rank (postmaster at 60, clerk at 35, labourer at 15, 'on notice' below), work done, your private account in MB, wife and children, ambitions kept, and the state of the rivalry. Unexplained crossings of the river in the Factor's memories and private trade each cost standing (up to 25 and 20). A clerk or postmaster is also paid 6 or 12 pieces of trade goods. See [[The Reckoning of 1821]] and [[Standing]].

## Nor'Westers

The North West Company has no London. Its equivalent is the July council at Fort William; see [[NWC Ledger]] and [[Councils and Accords]].

## Planned

Design doc 10.7 and 13.7 expect London to hold furs off a weak market, to run short of capital and to cap the outfit it will send, and the Committee to be an agent with its own solvency. Here the Committee reads only one number each year.

## See also

- [[London Committee]]
- [[Economy]]
- [[Fur Prices]]
- [[Fort Ledger]]
- [[Ship]]
- [[Hudson's Bay Company]]
