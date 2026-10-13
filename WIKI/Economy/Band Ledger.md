---
title: Band Ledger
aliases: [BandLedger, Band Cache, Country Band]
tags: [system, economy, ledger, indigenous]
status: implemented
---

# Band Ledger

**Status:** Implemented (`Scripts/Economy/BandLedger.cs`, `CountryBand.cs`, `Gardens.cs`).

![[ui-crest-cree.png|100]] ![[ui-crest-assiniboine.png|100]]

A band's cache at its camp: the Cree by the river, the Assiniboine up-country. Each keeps its own standing and debt with each company, its own seasonal round, and its own camp. A band is a `Household`, like a post; its `Stores` is the cache.

## Fields

| Field | Meaning | Start (Cree / Assiniboine) |
|---|---|---|
| StandingWithCompany | Feeling toward the HBC post | 50 / 40 |
| StandingWithNwc | Feeling toward the Nor'Westers | 50 / 45 |
| DebtMb, DebtNwcMb | What it owes each house | 0 |
| StandingWithOtherBand | Alliance with the other nation | 65 |
| RumTaste | Appetite for drink | 0 |
| Mouths | People calling it home | counted live |

A band is *at odds* with the other when that standing is under 25 (it asks after guns first). Old alliances mend 0.3 a day. See [[Credit and Debt]] for the daily drift and the thresholds.

## Cache at the start

| | Cree cache | Assiniboine cache |
|---|---|---|
| Food | [[Fresh Meat]] 30, [[Dried Goose]] 20 | [[Pemmican]] 40 |
| Furs | [[Beaver Pelt]] 6 | Beaver 4, [[Wolf Pelt]] 3, [[Caribou Hide]] 6 |
| Goods | [[Trade Goods (item)]] 4, [[Steel Trap]] 8, [[Copper Kettle]] 2, [[Fishing Line]] 4 | Trade goods 6, kettle 1 |
| Other | [[Firewood]] 20, [[Water]] 12 | Firewood 20, water 10 |

They know the sites near their camp (it is their country).

## The seasonal round

- **Cree:** strike camp at *Breakup* (5 June) for the trading season on the trail beside the post; back to the bush winter camp at *Freeze-up* (10 November).
- **Assiniboine:** come down the river road to the trail below York Factory on 1 July and leave for the buffalo country on 1 September.
- Moves send the people walking: a long move goes by the river road, tipis and all.

## Pemmican and surplus

While wintering on their upland ground the Assiniboine cache gains **14 pemmican and 1 caribou hide a week**. A camp holding more than `max(30, 3 x mouths)` pemmican, with at least 10 spare, takes up to 20 to whichever house will have it (the Company if it trades, otherwise the Nor'Westers), for `round(20 x 0.3 x PeltPrice)` pieces of goods. See [[Barter Rates]].

## The wider country

`CountryBand` is a band of the wider camps outside York's books: not courted by York's counter, with standing 0 toward both companies, trading at its own country's posts. Each week it adds `mouths x lean` dried meat (4 in February to April, 8 in September and October, 6 otherwise), `mouths x 2` fresh meat, `mouths` fish and, in winter, a few pelts (0 to 3, or 0 to 1 for a family). Its cache starts with dried meat 30, pemmican 10, firewood 20, water 10, beaver 8 (2 for a family).

## Gardens and berries

The Mandan, Hidatsa and Arikara towns are farming towns, not hunting bands. They add 30 [[Indian Corn]] per mouth in September and October and berries in July and August; in summer nomadic camps within 26,000 px swap dried meat for corn (2 corn per piece), and the Assiniboine carry corn north and exchange it for pemmican. Berry season runs July to September for every camp and post. See [[Gathering and Gardens]] and [[Missouri Villages]].

## What angers a band

- Killing one of theirs: standing with the killer's company -25.
- An insult in a trade: -4.
- Refusing guns in a war: -5.
- Rum: the more they are given, the more they want it.

## See also

- [[Economy]]
- [[Credit and Debt]]
- [[Cree]]
- [[Assiniboine]]
- [[Cree Camps]]
- [[Assiniboine Camps]]
