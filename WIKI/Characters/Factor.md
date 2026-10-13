---
title: Factor
aliases: [Chief Factor, Master of York Factory, Superintendent]
tags: [character, role, hbc]
status: implemented
---
# Factor

**Status:** Implemented

![[char-factor.png|140]] ![[sheet-char-factor.png|420]]

| | |
|---|---|
| **Role value** | `Role.Factor` (0) |
| **Faction** | `HBC` |
| **Behaviour** | `FactorBehaviour` (shared with Postmaster and NWC Partner) |
| **Sprite sheet** | `factor` (180x170; idle x3, walk x3, eat, drink, talk, write, death) |
| **Spawn age** | 46 to 58 |
| **Health** | 100 |
| **Skills** | Trade 0.5 to 0.8; Hunting 0.1; Trapping 0.1 to 0.35; Woodcraft 0.15 |
| **Starting kit** | Musket, Capote, 2 Pemmican |
| **Where he works** | York Factory depot and the Factor's house; wanders his work cell |
| **In the scene** | **Mr. William Auld** ([[William Auld]]), the leader of York Factory |

## What he does

The Factor is the head of the household and the post's decision maker. On the **morning beat** (after 07:00 once per game day) he looks at the post's situation and hands out the day's tasks to the men: this is the `Delegation.AssignDailyTasks` loop ([[Delegation]], [[Daily Orders]]). Between beats he idles around his work cell.

Tasks he can assign (names from `Delegation.cs`): drive off the animals, stand guard at the stores, salt the geese down, snare small game, work the trapline, cut wood, carry the furs and the report down to York Factory (from inland posts), carry the outfit up to Oxford House, lie up by the river road for the Company's packet, and go up to Oxford House to see how they do. Key tasks go only to men he trusts if his personality is suspicious. Whether a man does what he is told depends on loyalty, ambition, fear and pay, not a flag.

He also:
- **Reads London's orders.** The yearly packet from the [[London Committee]] sets the price and quota; the Factor passes them on.
- **Judges the player.** At the ship's sailing, his report decides your rank: postmaster at standing 60, clerk at 35 ([[Standing]], [[Player Character]]).
- **Weighs gossip.** Rumours about a rising hand reach him through ambitious clerks ([[Gossip, Lies and Leverage]], [[Clerk]]).
- **Orders raids.** With tensions at the top, he can send armed labourers to relieve the Nor'Westers' stores ([[Rivalry]]).

## When he dies

The clerk with the highest score of standing plus ten times ambition becomes *acting Factor* and renames himself "Mr. ... (acting Factor)", taking the Factor's bed and work cell. If there is no clerk, a labourer steps up. See [[Heirs and Succession]].

## Tips

- He is not a fighter: he does not turn out to defend the house.
- Introduce yourself early; the new hand's first order is to report to him ([[HBC Path]]).
- He is a Tier 1 planner for the post: his weekly view of provisions, scurvy and quota drives what the men do.

## See also

[[Hudson's Bay Company]], [[Fort Ledger]], [[Postmaster]], [[NWC Partner]], [[York Factory]], [[Roles]]
