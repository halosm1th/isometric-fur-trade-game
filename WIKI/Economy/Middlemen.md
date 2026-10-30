---
title: Middlemen
aliases: [Middleman, Resale, Second-hand Trade]
tags: [system, economy, trade]
status: partial
---

# Middlemen

**Status:** Partial. Far camps, partnerships, buying for resale, the twice-yearly meetings, the markup, second-hand wear and the grievance when a far camp goes past its middleman are implemented (`Scripts/Economy/Middlemen.cs`). Middlemen actively blocking far camps from the posts by force, and a far camp's own beliefs about the markup, are planned (spec 3.3 items 6 and 7, `docs/specs/reading-notes-history.md`).

For a century the Cree, the Assiniboine and the Ojibwe stood between the posts and the far nations. A people whose nearest post was too far to reach got the trade's goods through the camp between them and it: used, dear and late.

## Who is far

A camp is **far** when there is no post it can safely reach (one not in an enemy's country) within three-quarters of a camp's usual trading range. On the 1811 map that is about seventeen camps, among them the Chipewyan, the Gros Ventre, the Lakota, the Crow, the Cheyenne and several Coast peoples.

A far camp looks for a **middleman**: a camp within a trading range of it that has a post in reach, isn't its enemy (or is under a truce with it), and whose trader leans to buying beyond his people's needs. Its own nation is preferred.

## The trader's lean

Each camp's trader has a lean from 0 to 1: the chief's ambition where the chief is a person in the world, or the camp's own fixed lean. Under 0.35 he buys only for his own people and won't act as a middleman.

## Buying for resale

At the post, after the camp's own wants are paid for, a middleman spends part of the furs left over (as much as his lean) on what his far partners said they lacked at the last meeting. Dear, light goods come first. The goods go into his camp's own stores and are used until they are sold on.

## The meetings

There's no calendar for meetings. Each week, each side decides whether a ride to the other is worth it, from what it believes of the other camp: where its lodges stood, what it had to trade, and when that was seen.

- **The middleman rides out** when the goods he holds for his partner (at his price) are worth the days on the road, against what he believes the far camp can pay. That belief starts from what he saw last time and drifts toward "a camp has something after a season's hunt" as the months pass. The cost is the men's days away from the hunt plus the gifts on arrival. A trader with a strong lean goes for less. He won't go more than 14 days each way.
- **The far camp rides in** when it's short of something it can't do without (guns and powder count for more when it's in danger), it has something to pay with, and what it saw in the middleman's hands, of the goods it lacks, is worth more than the ride costs. That belief comes from the last meeting and is good for about 300 days.
- **Neither goes** while its men are off trading at a post, at war, running buffalo, or already away on a meeting.

The party (two to six of the camp's men, by its size) rides to where it last knew the other camp to be. If the lodges have moved, the men may follow a fresh, short trail, or turn for home. When a player is near the host camp, the men ride in and the leader trades face to face at the lodges. Otherwise it's done in numbers.

At the meeting, the far camp takes what it's short of, as far as the meeting party can carry (sixty pounds a man and half a horse-load for each spare horse) and as far as it can pay. It pays in its best furs first, then robes, then horses beyond its riding herd (a horse for every two of its people). The middleman carries those furs to the post on his next trip. Both sides go home knowing how the other stands that day.

## The price

The middleman's price is his cost at the post times a markup: (2.2 + 0.8 × his lean) × (1 + 0.6 × the distance in thousands of km × how heavy the good is for its worth). That's about two and a half to three times over for a gun at a short distance. Ray's figures: a gun at 12 MB at the Bay went for 36 from the Cree to the Blackfoot.

## Second-hand goods

What a far camp buys from a middleman comes used. Its kettles, axes, guns and knives that came second-hand wear out twice as fast ([[Trading and Barter]] and camp wants).

## When a post comes within reach

A far camp that gets a post within reach takes its furs in itself. The partnership ends, and the middleman's feud with it heats. A partnership not used for over a year lapses, and so does one where the two camps become enemies.

## Reading it

`--plainslog` prints `[middlemen]` lines: partnerships formed and ended, purchases for resale at the posts (or what was ordered and missing from the shelf), each party setting out and why, empty camp grounds, and every meeting with what changed hands and what a gun cost. Meetings go into the chronicle, and a player near either camp hears of them.

## See also

[[Economy]] · [[Made Beaver]] · [[Credit and Debt]] · [[Global Trade]] · [[Assiniboine]] · [[Cree]]
