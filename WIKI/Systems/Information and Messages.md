---
title: Information and Messages
aliases: [Mail, Letters, Directive, Packets, Message Object]
tags: [system, information, letters, messages]
status: implemented
---

# Information and Messages

**Status:** Implemented (design doc 10.1). A **message** is something written down, carried by somebody or something, and **inert until it is read**. London's directive, a Factor's letter to the chief, the year's accounts going home, the council's minutes, news of the wider world: all one class (`Message`). What it does happens in `OnRead`, at the *read event*, not before.

![[item-letter.png|64]] ![[item-dispatch.png|64]] ![[item-ledger.png|64]]

## Anatomy

| Field | Meaning |
|---|---|
| Kind | directive, accounts, letter, mandate, rumour, report, reply, news, orders |
| From / To | people or places ("London", "the Factor at York Factory", "the wintering partner", "the chief of the band") |
| Company | whose paper it is |
| TransitHoursLeft | for the abstract legs (a ship at sea, a runner off the map) |
| OnRead | the intended effect when the **right** person opens it |
| OnIntercepted | what a **rival** who opens it learns instead |

`Mail.Open` marks it read and logs it, fires `OnRead` if the reader is of the right company or is the addressee, otherwise `OnIntercepted`, and records a witnessed [[Memory and Knowledge|chronicle]] event (weight 0.5 for the intended reader, 1.5 for an interloper). Addressees can be named by role, so a successor still opens the packet (`the Factor at York Factory`, `the postmaster at Oxford House`, `the wintering partner`, `the chief of the band`).

## The letters this world writes

| Letter | Sent | Effect when read |
|---|---|---|
| **Directive** ("the outfit of *year*") | Annually by London | Sets the post's pelt price (MB in goods per MB of fur) and quota; Factor reads it. If a Nor'Wester reads it: their price becomes the greater of theirs and **1.15 x** the Company's, and Rivalry +6. |
| **Accounts** | Ship to London (75 days in transit) | London's Committee reviews returns (see below) |
| **Mandate / council minutes** | Fort William council, carried by the brigade | Sets Nor'Westers' price, outfit, rum policy ([[Councils and Accords]]). If a Company man reads it: he learns the whole plan, `KnowsRivalPlan`, Rivalry +6. |
| **Letter to the chief** | The Factor, via you or a courier | Band "InvitedToTrade", standing with the Company +6 |
| **Reply** (string of beads) | The chief | Band standing +2; completes your DeliverLetter task (+3 standing) |
| **Report** from Oxford House | Monthly by a courier | The Factor's picture of the inland post: furs, food days, men; "no master" triggers a posting |
| **Rumour** | A blowback of a sold packet, 3-9 days | The Factor docks the named man's standing |
| **News** | The campaign | Sets a flag when read; becomes a witnessed event ([[Campaigns and Adherence]]) |

### London's committee
When the accounts arrive, `LondonCommittee.ReviewReturns` rewrites next year's orders from `ratio = shipped / quota`:

| Ratio | Price (MB per MB) | Quota |
|---|---|---|
| under 0.6 | x 1.35 | x 0.9 |
| 0.6-0.9 | x 1.15 | unchanged |
| 0.9-1.4 | unchanged | x 1.1 |
| over 1.4 | x 0.85 | x 1.25 |

Clamped to 0.3-3.0 and 10-400 MB. The Committee "never sees the country, only the numbers - which is the point."

## Carrying and selling

You can carry a message in your pack (the Pack tab lists "Papers"). Hand it to its addressee and he reads it on the spot (+3 regard; +2 standing if same company). Or **sell** it to the other company's leader:

- **The companies...** menu: "Sell them the *kind* from *name*" for 8 trade goods (directive or mandate) or 3 (anything else).
- The buyer reads it immediately and his regard for you rises +6.
- A witnessed event of weight 2.5 follows. If the paper was the Company's, there is a **40 percent chance** a `rumour` is sent to the Factor that arrives in 3-9 days and costs you **20 standing** with a Reveal line ("Word has reached the Factor and your standing falls by 20").
- The sale only works if the buyer has the goods on the shelf.

This is the espionage loop: **nothing happens until someone reads it, and nobody knows what they were not told.** Rivals can also ambush the inland courier on the river road.

## Transit

`Mail.Send(m, hours)` places a message in the abroad list; each tick reduces its hours and when it lands the far end reads it. Ship accounts take **75 days** to London.

## Tips

> [!tip]
> Keep a rival's packet unread if you can; once you break the seal it is a chronicled event. Selling a directive is the quickest way to hurt your own company and to be caught by a rumour three to nine days later.

## See also
[[Memory and Knowledge]] | [[Councils and Accords]] | [[Rivalry]] | [[Delegation]] | [[Documents]] | [[London Committee]] | [[Gossip, Lies and Leverage]]
