---
title: Daily Orders
aliases: [Morning Orders, Tasks, Factor's Orders, Standing Tasks, Task System]
tags: [gameplay, system, orders, standing, hbc]
status: implemented
---

# Daily Orders

**Status:** Implemented for Company (HBC and NWC) characters. Band characters receive no orders ("Among your own people, the Cree"). Code: `Scripts/Player/PlayerTask.cs`, `Scripts/Agents/Delegation.cs`, `Scripts/UI/Conversation.cs` (`ShowBargain`), `Scripts/World/Ship.cs` (rank at sailing).

Survival and the Factor's orders happen *to* you; an ambition is what you do *with* them ([[Ambitions]]). The orders are how a Company post tells you what it needs today, and the main way to raise your **standing**.

## The morning beat

From **07:00** the Factor (or the Nor'Westers' partner) looks at the post's situation and hands out the day's tasks to the men: cut wood, hunt at the best known site, work the trapline, stand guard, fetch from the ship, go to the marsh for geese. The same call assigns **you** one task, with a count and a short "why", e.g.:

> Mr. Auld: bring 4 firewood to the woodpile (0/4) - the woodpile

You must be within 900 px of the leader when the orders are given. Miss them and: "You were not at the post when the morning's orders were given. <Leader> noticed." (-1 standing). Your first task is always **Report to the Factor: find Mr. Auld and speak to him**, and it replaces the morning's orders until you have.

The current task shows under your bars and on the screen as a **blue marker** with distance ([[Journal, Map and UI]], [[Travel and Wayfinding]]).

## Task types

| Task | What it asks | Progress |
|---|---|---|
| **Report** | Find the Factor and speak to him | Talk to him |
| **Firewood** | Bring N firewood (default 4) to the woodpile | Stack at the woodpile (`E`) |
| **Carry cargo** | Carry N lots (8) from the docked ship to the warehouse | `E` at the wharf shoulders 4 lots at a time; stow in the warehouse |
| **Drive off animals** | See off the beasts about the post | Kill a dangerous animal; the Factor lends a musket if you lack one |
| **Deliver a letter** | Carry the Factor's letter to the chief at the camp, then bring the answer back | A real paper in your pack; hand it over, then hand the reply to Mr. Auld |
| **Courier** | Carry the outfit up the river road and stow it at Oxford House | Stow the lots there |
| **Geese** | To the marsh while the flight lasts: bring N geese to the stores | Stow geese |
| **Raid** | Seize lots from the other company's stores (during open hostility) | `E` inside their storehouse takes 4 lots |
| Guard / hunt orders | "Let me stand guard", "Let me hunt at <site>" (volunteered, intent only) | Open-ended |

## Doing and not doing

- Finishing a task earns **+5 standing** (applied when the next task is handed out; "Task done. The Factor will hear of it.").
- Letting it lapse costs **-2** the next morning ("Yesterday's task went undone. Mr. Auld noticed.").
- A task is due in one day by default.

### Arguing the order

Talk to the leader and open **Work and orders...**; "It will be done" accepts it. Otherwise you can bargain (the leader weighs your rank, his regard, what you have done for him, and who speaks for you, and says why he decides):

| Option | Effect |
|---|---|
| Ask for more time | +2 days on the deadline if granted |
| Ask for a man to go with you | A companion is sent if one is free |
| Ask for something in hand for it | Pay in trade goods against your account |
| Ask leave to do it your own way | Anything of worth you stow counts toward the task ("[your own way]") |
| Ask for a different task | Costs 1 standing; picks another from the list |
| Decline it | Costs 3 + 3 x his suspicion in standing, drops his rapport by 5, and is remembered |

## Volunteering

Under "Work and orders..." you can volunteer: carry the letter to the band, hunt at a named site (bring back meat), stand guard at the stores (when rivalry tension is 40 or more), or **"Send me up to Oxford House with the outfit"** (standing 12 or more): the Factor gives you up to 10 trade goods and 4 pemmican and a sealed letter for the postmaster, and you are posted inland.

## Giving orders yourself

Once you are a clerk or postmaster (or have standing 35 or more) you can **give orders** to labourers and voyageurs of your company: cut wood, hunt, work the trapline, stand guard, come with me. A sulky man says he takes his orders from the Factor. Anyone else who is not a leader, clerk, sailor or woman can be asked to **come with you** (rapport 20 across factions, 5 in your own; kin always). See [[Delegation]].

## Standing and rank

Your standing with the Company is a number from 0 to 100, shown under the bars. When the ship sails on 15 September the Factor's report rates you, and your **rank** is set from it:

| Standing | Rank | Reward |
|---|---|---|
| under 15 | on notice | |
| 15 - 34 | labourer | |
| 35 - 59 | clerk | +6 trade goods |
| 60 and up | postmaster | +12 trade goods |

Other standing movements: telling the Factor about a game site +3 each; stowing provisions +0.5 per piece (to +4); raiding stores -3 unless open hostility; breaking parole -10; trading on your own account and being found out is reported. See [[Standing]] and [[The Reckoning of 1821]].

## Tips

> [!tip]
> Be at the post at 07:00 and speak to the Factor on your first morning. Doing the small tasks keeps standing climbing so that the muskets, blankets and a posting to Oxford House open up.

> [!tip]
> "Your own way" lets you meet the order with furs or goods, handy if you run a trapline anyway ([[Trapping]]).

## See also

[[Standing]] - [[Delegation]] - [[Factor]] - [[Hudson's Bay Company]] - [[Beginner's Guide]] - [[Journal, Map and UI]]
