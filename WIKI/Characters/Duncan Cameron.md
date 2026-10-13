---
title: Duncan Cameron
aliases: [Mr. Duncan Cameron (NWC)]
tags: [character, historical, nwc]
status: implemented
---
# Duncan Cameron

**Status:** Implemented as the partner at the Nor'Westers' house across from York. The in-game man is a stand-in with the historical partner's name.

| | |
|---|---|
| **Side** | NWC |
| **Role in game** | NWC Partner, leader of the house at York |
| **Role** | `NwcPartner`; Council district "the Bay (opposite York Factory)" |
| **Council** | 1 share, standing 45, Ambition 0.6, Suspicion 0.5, Loyalty 0.5 |
| **Sprite** | `nwc_partner` (the scene uses the older `rival` sheet; `Appearance` replaces it) |
| **Kit** | Musket, Capote, 2 Pemmican |

## Who they were

A real Nor'Wester who, as a partner in the Red River department around 1814 to 1815, persuaded many settlers to leave Selkirk's colony. The content list calls him the man at Fort Gibraltar who "turned settlers against the colony in 1815".

## In the game

- **The leader of the house.** `Mr. Duncan Cameron (NWC)` is the NWC house's chief, with three voyageurs ([[NWC Partner]], [[Nor'Westers' House]]). He gives the day's orders, sets the price within the council's mandate, and is the person an NWC player first reports to.
- **The Council.** `Council.Local()` returns him (the councillor of "the Bay") when the July council weighs the price against the Company at the Bay, where the local partner argues for letting his post set its own price.
- **Caution.** The historical Cameron was never at York Factory; the game borrows the name for a fictional local post. The campaign letters instead name "the wintering partner".

## See also

[[North West Company]], [[Voyageur]], [[Fort William]], [[William McGillivray]], [[Councils and Accords]], [[Historical People]]
