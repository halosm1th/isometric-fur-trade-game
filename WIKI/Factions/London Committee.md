---
title: London Committee
aliases: [Governor and Committee, The Committee, Governor and Committee of the Hudson's Bay Company]
tags: [faction, hbc, system, economy]
status: partial
---
# London Committee

**Status:** Partial. The yearly review is built as a simple background agent (`Scripts/Economy/LondonCommittee.cs`). The richer design of 7 to 12 named London agents, indents and politics (design doc 10.7) is planned.

| | |
|---|---|
| **Faction value** | None (a static system, not an agent) |
| **Seat** | London |
| **Controls** | The Company's pelt price ("standard of trade") and yearly quota |
| **Speaks through** | Letters carried on the supply ship ([[Information and Messages]]) |

## What it does

When the ship gets home, the Committee reviews the returns and writes next year's orders. The ratio is *furs shipped divided by quota*:

| Returns ratio | Effect on next year's orders | Verdict shown |
|---|---|---|
| Under 0.6 | Price x1.35, quota x0.9 | "displeased... a more liberal standard of trade" |
| 0.6 to 0.9 | Price x1.15 | "notes the shortfall and allows a little more for beaver" |
| 0.9 to 1.4 | Quota x1.1 | "approves the returns and looks for a modest increase" |
| Over 1.4 | Price x0.85, quota x1.25 | "gratified... trims the standard of trade" |

Price is clamped to 0.3 to 3.0 and quota to 10 to 400 MB. The Committee never sees the country, only the numbers, which is the point.

## Why it matters

The price the post pays decides how hard the bands hunt for trade. A Cree hunter chooses a fur hunt with probability 0.25 times the price (clamped 0.05 to 0.95), so London's decisions reach every camp ([[Cree Hunter]], [[Trading and Barter]]). A band that is hungry blames the Company when its price is poor ([[Standing]]).

Campaign beats move the same levers: the 1811 blockade sets price and quota to 0.85 of normal, the 1814 peace lifts them (1.18 and 1.15), the 1820 pressure and the 1821 merger lower them ([[Campaigns and Adherence]]).

## Design intent

- Roughly 7 to 12 named agents (Governor, Deputy Governor, five directors, big shareholders) running a council once a year.
- Inputs: furs, journals, account books and the factors' **indents**, requisitions written two years ahead. Goods arrive a year or two after being asked for.
- Politics as a causal chain, not a cutscene: Selkirk buying shares from 1808, his brother-in-law Wedderburn joining the Committee in January 1810, the land grant of May 1811 obliging Selkirk to supply 200 men a year ([[Lord Selkirk]]).

## See also

[[Hudson's Bay Company]], [[Ship]], [[Fort Ledger]], [[Fur Prices]], [[Economy]]
