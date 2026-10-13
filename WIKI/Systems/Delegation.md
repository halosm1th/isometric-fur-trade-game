---
title: Delegation
aliases: [Daily Orders, Morning Beat, Task Assignment, The Factor's Orders]
tags: [system, delegation, orders, factor]
status: implemented
---

# Delegation

**Status:** Implemented (design doc 3.9). The layer between a Tier 2 directive (London's orders, the council's mandate) and the actions anyone can take: **how a leader who has read his orders turns them into work other agents actually do.** It is generic - a Factor with London's directive, a partner with the council's mandate, even an "acting master" labourer at a post with no master.

![[char-factor.png|110]] ![[char-labourer.png|110]]

## When it runs

- The **Factor's** behaviour: once a day at **07:00** or after, the Factor looks at the post and hands out the day's tasks.
- A post with **no master**: the steadiest labourer (highest standing) gives the orders till one comes.
- The Company's ship calls it too when she is in.
- A new hand's first report (`ReportedForDuty`) draws on the same loop: "You'll *cut wood* - the woodpile is bare."

## Step 1: what needs doing (objectives)

Each post builds a list of tasks with an **urgency** (bigger goes first):

| Task | Urgency | Notes |
|---|---|---|
| Drive off the animals | 100 | Dangerous beasts within 1800 px of the post; need a musket |
| Unload the ship / load the furs | 90 | Whole post goes (99 men). **Everything else waits.** |
| Shoot geese at the marsh | 95 (under half the casks) or 75 | Autumn flight, needs a musket, key task |
| Hunt (food) | 85 (under 10 food days), 70 (3+ scorbutic), 55 or 45 | When provisions are under 30 days |
| Salt the geese down | 80 | 40+ birds on the floor, flight on (3 men at 120+) |
| Go up and see how Oxford House does | 62 | Silence over 45 days |
| Fetch a captive back from the rival | 72 | If a man is held across the river |
| Stand guard at the stores | 60 | After a raid or Rivalry tension 60+; musket; key task |
| Carry furs and report to York Factory (from Oxford House) | 58 | Monthly (30 days) |
| Lie up by the river road (Nor'Westers) | 57 | Tension 55+ or raids sanctioned |
| Work the trapline | 40 + up to 30 | Winter, with traps, if the post is behind quota |
| Cut wood | 65 (under 15 logs) or 20 | 3 / 2 / 1 men by pile size |

Each task carries a **men** count, whether it is a hunt, needs a musket, is a **key task** (only given to trusted men), and the leader's stated **intent** ("we have 18 days of food", "the ship must sail before the ice").

## Step 3: who gets what (fitness)

Best-fit by `Fitness(leader, man, task)`: 1.0 baseline, +0.5 if he has a musket and it needs one, +0.3 x Courage for hunts, +0.3 for knowing the site, +0.8 x Trust for key tasks, -0.6 sick or scurvy, -0.5 drunk (over 30), -0.4 hurt (under 60 health). A **suspicious** leader (over 0.6) keeps key tasks from men he trusts less than 0.55 (-1.5); an **ambitious** leader keeps ambitious men off hunts (-0.8).

`Trust = clamp(0.4 + 0.4 x Loyalty + regard/100, 0, 1)`.

## Step 5: does he do it?

`Decide(leader, man, task)`: probability of willing = `0.55 + 0.4 x Loyalty - 0.15 x Ambition + regard/200`, plus 0.15 for an ambitious man on a hunt, plus 0.3 for urgency 85+ ("the ship, or wolves: nobody argues"), minus 0.15 for an ambitious man told to cut wood, minus 0.2 if hungry or cold, minus 0.2 if sick. Roll: under = **Willing**; within the next 0.25 = **Grudging** (an hour's dawdle first); above = **Refused**.

A refusal: leader's regard for him -8, his for the leader -3, his standing -3, a notice ("says he'll not *task* today - it's beneath him / not on an empty belly"), a witnessed event.

## The player's morning

If you are at the post within 900 px of the leader you receive **one task**: `intent` and a count with a deadline, not steps.

| Task | Typical need |
|---|---|
| Firewood | 4 to the woodpile |
| Carry cargo | 8 lots from the ship to the warehouse |
| Drive off animals | 1 |
| Deliver letter | 2 (carry the Factor's letter to the chief, bring the answer back) |
| Raid | 4 lots, if a raid was sent |
| Geese | 12 |
| Courier | the outfit's lots up to Oxford House |
| Report | 1 (new hand) |

Miss the orders: standing -1 and "You were not at the post when the morning's orders were given." Leave a task undone to the next day: -2; do it: +5. You can bargain over a task ("It will be done", more time, a man to go with you, something in hand, leave to do it your own way, a different task, decline): see [[Standing]] for the weighing and [[Dialogue and Conversation]] for the menu.

## Step 6: reports and the overdue

When a man comes back he reports (`Receive`): standing +0.5 (+1.5 if he took game), the leader remembers it, and the post learns the site. A man out on a site who has not returned by the next morning and is not home gets a search party: the most trusted free man goes to look. The Factor believes whatever he last heard of Oxford House; a monthly report corrects it ([[Information and Messages]]).

## Specials

- **Posting a clerk**: if the Factor learns Oxford House has no master he names the clerk with the best `Standing + Trust x 20 + Ambition x 5` (needs 20 days since the last). The runner-up loses 15 regard for the winner and 5 for the Factor, and the player hears how he placed ([[Rivalry]]).
- **The fall outfit**: after the ship sails, two trusted labourers carry 20 trade goods, 10 pemmican, 3 rum, 4 traps (plus a second load) up the river road to Oxford House.
- **Raids** are decided at the same beat ([[Rivalry]]).

## Tips

> [!tip]
> Standing 35 or a clerk's rank lets you give orders to the Company's men yourself, but they weigh your rank, their regard for you, loyalty and ambition. Drunk men refuse.

## See also
[[Standing]] | [[Information and Messages]] | [[Rivalry]] | [[Personality]] | [[Daily Orders]] | [[Factor]] | [[Councils and Accords]]
