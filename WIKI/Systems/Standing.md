---
title: Standing
aliases: [Reputation, Rapport, Rank, Regard]
tags: [system, standing, reputation, rank]
status: implemented
---

# Standing

**Status:** Implemented. "Standing" is really four separate numbers in the game; this page keeps them apart.

| Number | Whose | Range | Meaning |
|---|---|---|---|
| **Standing** | the player, with their Company | starts at 10, uncapped but rank tops out at 60 | Slow, bureaucratic, reviewed by results; sets your rank |
| **Rapport / regard** | any person about any other agent | -100 to +100 | How one individual feels about you |
| **StandingWithCompany / StandingWithNwc** | a band | 0 to 100 | Whether the band will trade with a post |
| **NPC Standing** | a clerk, labourer | 0 to 100 | How the master rates him; feeds [[Rivalry]] and [[Delegation]] |

## Company standing and rank

Your standing is the number on the HUD (the "standing" need icon) and in the journal. It is only re-rated into a **rank** when the ship sails (HBC men only), by [[Delegation]]'s master the Factor:

| Standing at sailing | Rank | Reward |
|---|---|---|
| 60 or more | postmaster | +12 trade goods |
| 35 or more | clerk | +6 trade goods |
| 15 or more | labourer | none |
| below 15 | on notice | none |

A rank change is announced ("The Factor writes well of you in the accounts that sail today"). Starting rank is "labourer" at standing 10; the clerk backgrounds start as clerk at 30 but are re-rated at the first sailing like everyone.

### What moves it

| Action | Change |
|---|---|
| Report to the Factor (new hand) | +3 |
| Finish the day's task | +5 (and `TasksDone` rises) |
| Yesterday's task left undone | -2 |
| Decline an order | -(3 + 3 x leader's Suspicion) |
| Ask for a different task | -1 |
| Miss the morning orders | -1 |
| Volunteer for letter, hunt, guard, or inland outfit | +1, +1, +1, +2 |
| Deliver the Factor's letter and bring the chief's answer back | +3 |
| Hand over a letter or packet to its addressee (same company) | +2 |
| Provisions stowed for the mess | +0.5 per lot, max +4 per delivery |
| Tell the Factor where game is | +3 per new site |
| Carry tobacco between the bands | +2 |
| Deny a story the Factor believed (and he believes you) | +2 |
| Tell the mate a sailor trades privately (prologue) | +3 |
| A favour or hold you called in: "speak for me" to his master | +2 (favour) or +3 (leverage), same company only |
| Strike an HBC man | -5 |
| A misdeed story reaches your master | -3 x weight |
| The rival clerk tells on you | -8 |
| Factor's year-end report: unexplained river crossings | up to -25 |
| Factor's year-end report: trading on your own account | -(4 + 3 per instance), max -20 |
| Gave parole to the Canadians and never confessed | -12 |
| Confess parole | -6 |
| Changing sides (HBC to NWC or back) | reset to 10, former company -25 regard, new company +5 |

> [!warning]
> Standing is changed by **reports and sightings**, not by omniscience. A Factor only docks you for what has reached his ears. See [[Gossip, Lies and Leverage]] and [[Information and Messages]].

## What standing unlocks

- **Muskets from the stores**: rank clerk/postmaster or standing 15+.
- **Blankets**: standing 10+. **Trade goods count-out**: rank clerk or postmaster.
- **Give orders to the Company's men**: rank clerk/postmaster or standing 35+ (and they weigh your rank 0.2 labourer, 0.4 clerk, 0.6 postmaster).
- **Posted inland** (Oxford House): standing 12+.
- **Write to the Fort William partners**: Nor'Wester standing 20+ and before July; your letter weighs `clamp(Standing / 25, 0.3, 2)` of a share.
- Everything the negotiation weigh counts (see below).

## Rapport (regard)

Every NPC keeps a rapport number per agent, clamped to -100..100. Thresholds used across the code:

| Rapport | Effect |
|---|---|
| 5 | An own-company man will walk with you for the day |
| 15 | A hunter will tell you where the game is; you can speak for peace between bands |
| 20 | Friend threshold (with bond 3); strangers will follow you; the Factor takes you back after desertion |
| 30 | Father consents to a marriage (with 8 MB of gifts) |
| > 40 / > 10 / < -20 | Greeting reads "warmly", "civilly", "coldly" |
| -10 | Below this an NPC won't even ask you a favour |
| -25 | Counted among the men who "speak against you" |

Typical changes: gift 4 + 6 x worth (doubled for rum and ale), offering food +8, tending a wound +2, a pipe opening a trade +8, a threat -15, a lie believed +1, a lie exposed -12, a blow -15 to -25, a raid -25 for everyone watching, a killing -60 for the victim's kin.

## Band standing with the Company

`StandingWithCompany` (0-100, default 50; Assiniboine 40) drifts daily: if the camp is hungry (under 3 food days) it falls by 3 when the price is under 0.9 and 1 otherwise; if fed it gains 1 at price 1.2 or better, else 0.3. Above 20 the band "will trade"; fall below and the headline reads "have had enough of the Company". Hits include: insult -4, a member killed by a Company man -25, left-behind wife -15, a stolen woodpile -1, holding back guns -5, restitution +12. `StandingWithNwc` is the same for the Nor'Westers.

## Negotiation weighing

When you ask a leader for a task, more time, a man, an advance, or your own way, the Negotiation score is the sum of: rank (+0.55 postmaster, +0.35 clerk, -0.3 on notice), standing/60 clamped to -0.4..+0.6, his regard/100 (max 0.5), tasks done x 0.08 (max 0.45), tasks refused x 0.22 (max 0.6), ill things heard (0.15 each, max 0.5), good things heard (0.08 each, max 0.3), friends and enemies among the house (0.07 or 0.14 for clerks, max 0.4 each), kin (+0.3 on band matters), suspicion and loyalty of the man, and a +/-0.12 roll. It is granted if the score meets `difficulty - 0.5`. Difficulties: more time 0.5, a man 1.0, pay 1.2, your own way 1.4; seeking a promotion 0.55 (0.8 if the rival is ahead). The answer names its top two reasons for and against.

## See also
[[Starting Paths]] | [[Delegation]] | [[Rivalry]] | [[Ambitions]] | [[Trading and Barter]] | [[Friends and Bonds]] | [[Favours]] | [[Grudges and Vendettas]] | [[The Reckoning of 1821]]
