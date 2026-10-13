---
title: Hudson's Bay Company
aliases: [HBC, The Company, The Bay, Honourable Company]
tags: [faction, hbc, company]
status: implemented
---
# Hudson's Bay Company

**Status:** Implemented (York Factory, Oxford House and ship cycle fully built; other posts partly)

![[ui-crest-hbc.png|140]]

| | |
|---|---|
| **Faction value** | `HBC` (enum 0) |
| **Home territory** | The Hudson Bay lowlands: York Factory on the Hayes River, with Oxford House inland ([[Hudson Bay Lowlands]]) |
| **Language** | English (Orkney Gaelic colours are in the barks, not in the code) |
| **Sprites** | `factor`, `clerk`, `labourer`, `country_wife`, `child_boy`, `child_girl`, `player` |
| **Starting path** | [[HBC Path]] (arrive aboard the ship on 20 August 1811) |
| **Allies / friends** | The [[Cree]] and [[Assiniboine]] bands that trade at its counter |
| **Rival** | [[North West Company]] (see [[Rivalry]]) |
| **Masters** | The [[London Committee]] |
| **Atlas posts** | 51 HBC posts in `world.json`, founded in years from 1673 (Moose Factory) |

![[char-factor.png|110]] ![[char-clerk.png|110]] ![[char-labourer.png|110]] ![[char-country_wife.png|110]]

## History

Chartered in 1670, the Company held Rupert's Land and traded from the shore of the Bay, waiting for the bands to come down to its "factories". Its furs were entirely committed to the London ship route, which is why the Continental Blockade hurt it worst (design doc 13.6). In 1811 it was still a salaried hierarchy: no profit-sharing ranks, which only arrived with the 1821 merger. The 1811 grant of Assiniboia to [[Lord Selkirk]] and the later [[Red River Colony]] drew the Company into the Pemmican War. See [[Setting and Timeline]].

## How it plays

- **One ship a year.** The supply ship arrives on **20 August** and sails on **15 September** ([[Ship]]). It lands the outfit and recruits from Orkney, loads the furs, and carries the Factor's accounts to London, where the [[London Committee]] reads them in the autumn and sets next year's standard of trade and quota.
- **The post is a household.** York Factory runs on a [[Fort Ledger]]: a pelt price (starts at 1.0), a yearly quota (60 MB), stores, a leader and a roster. The leader hands out the day's work each morning ([[Delegation]], [[Daily Orders]]).
- **Ladder of standing.** When the ship sails, the Factor's report rates each HBC player by standing: 60 or more is *postmaster*, 35 or more *clerk*, 15 or more *labourer*, anything less *on notice* ([[Standing]]). Clerks and postmasters are paid in trade goods (6 and 12 pieces).
- **Cast at York Factory.** One Factor ([[William Auld]] in the scene), two [[Clerk|clerks]], six [[Labourer|labourers]] and three [[Country Wife|women]]. Oxford House has a [[Postmaster]], three labourers and a woman.
- **Combat.** Only labourers carrying a weapon turn out as fighters. The Factor, clerks and women do not ([[Combat]]).
- **Recruiting.** New labourers are drawn from a list of ten Orkney surnames (Isbister, Flett, Spence, Linklater, Corrigal, Sinclair, Setter, Tait, Garrick, Sabiston) and land with a Capote, a Knife and two Bread ([[Labourer]]).

## Notable people

[[William Auld]] (York's Factor), [[George Simpson]] (the 1821 governor), [[William Williams]], [[Peter Fidler]], [[Lord Selkirk]] (a major shareholder). Also the fictional cast: John Fidler and Alex Kennedy (clerks), James Isbister, William Flett and the rest of the Orkney men.

## Locations

[[York Factory]], [[Oxford House]], [[Norway House]], [[Cumberland House]], [[Edmonton House]], [[Moose Factory]], [[Albany Fort]], [[Churchill]], [[All Trading Posts]].

## Trivia

- Under a 1821-style ladder, "Chief Factor" and "Chief Trader" belong to the epilogue, not the playable window (design doc 5.1).
- The Company's women are drawn on the `country_wife` sheet; its small children use `child_boy` and `child_girl`.

## See also

[[North West Company]], [[London Committee]], [[Trading Post Ledger]], [[Outpost Ledger]], [[Brigade]], [[The Reckoning of 1821]]
