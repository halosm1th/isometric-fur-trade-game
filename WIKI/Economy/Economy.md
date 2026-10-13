---
title: Economy
aliases: [Economy Overview, The Trade]
tags: [system, economy, index]
status: partial
---

# Economy

**Status:** Partial. The ledgers, prices, barter, credit, the ship and the brigades are implemented. The market, solvency and blockade layers of the design doc are planned.

![[vehicle-ship.png|300]]

The fur trade is a loop of goods going in and furs coming out, and every number in it is counted in one unit: the [[Made Beaver]] (MB), the worth of one prime beaver pelt. Posts and camps are households with a shared pile of goods (a *ledger*); a company counter pays for furs in trade goods at a price London sets; the annual ship or the brigade carries the furs out and the outfit in; and the bands who hunt the furs trade with whichever house treats them better.

## How the loop runs

1. **Outfit arrives.** The ship from London lands about 20 August with trade goods, food, drink, muskets, traps, kettles, blankets and capotes ([[Ship]]). The Nor'Westers' canoes from Fort William land theirs on 12 July ([[NWC Ledger]]).
2. **The outfit goes inland.** From 18 September a brigade carries trade goods and supplies from York to [[Oxford House]] ([[Brigade]]).
3. **Bands trade.** Cree and Assiniboine come to the counter in the trading season, sell furs for goods at the post's price, and are sometimes advanced goods on credit ([[Credit and Debt]]).
4. **Furs are trapped** in the cold months, when pelts are prime ([[Fur Prices]]), and stored.
5. **Returns go out.** From 8 June the spring brigade brings the Oxford furs down to York; the ship takes the hold's furs home on 15 September ([[Fort Ledger]]).
6. **London reads the books** months later and sets next year's price and quota ([[London Committee Orders]]), and the cycle begins again.

## The ledgers

| Ledger | Who | Price (goods per MB) | Quota (MB) | Page |
|---|---|---|---|---|
| FortLedger | [[York Factory]] (HBC) | 1.0 | 60 | [[Fort Ledger]] |
| OutpostLedger | [[Oxford House]] (HBC inland post) | 1.0 | 40 | [[Outpost Ledger]] |
| TradingPostLedger | The wider country's posts | 1.0 | 25 | [[Trading Post Ledger]] |
| NwcLedger | [[Nor'Westers' House]] | 1.3 | 80 | [[NWC Ledger]] |
| BandLedger / CountryBand | A Cree or Assiniboine band's cache; the wider country's camps | n/a | n/a | [[Band Ledger]] |

All of them are a `Household`: an `Inventory` called Stores (items by count), a leader, hearth cells, and a food sum (food-days = total nutrition divided by 33.6 per mouth per day). Fresh food in a household spoils at 12 percent a day (5 percent when the country is frozen). A household that runs out of loose food will break open a [[Cask of Salt Geese]]. See [[Items]] for the items.

## Pages in this section

- [[Made Beaver]]: the unit, and how every item is worth something in it.
- [[Fur Prices]]: what a post pays, by fur and season, and how London changes it.
- [[Barter Rates]]: haggling, the formula behind each lot, and worth of every good.
- [[Credit and Debt]]: advances, debts and how bands drift between houses.
- [[London Committee Orders]]: quotas, the annual packet and the reckoning.
- [[Fort Ledger]], [[Outpost Ledger]], [[Trading Post Ledger]], [[NWC Ledger]], [[Band Ledger]].

## Planned (design doc 13)

| Feature | Design doc | Status |
|---|---|---|
| Fur quality rating per pack and a periodic market clearing | 13.6 | Planned. Quality is expressed today by different item ids ([[Summer Pelt]], [[Shot-through Pelt]]) and a flat price per post |
| Continental Blockade, 1812 war and 1816 exclusion as fixed external price shocks | 13.6 | Planned; no scripted price shock found |
| Flooding the market | 13.6 | Planned |
| Company solvency ledger, austerity, bailout, merger by exhaustion | 13.7 | Planned. The 1821 close is told through [[The Reckoning of 1821]] |
| Debt as a coercive weapon (a rival forgives it), calling in debts | 13.3 | Partial: debt exists per band and per company; no forgiveness or calling-in |
| Trade liquor dilution and the Fire Test | 13.4 | Planned; rum itself and band rum-taste are implemented |
| Founding a new post | 13.8 | Planned |
| Finite recruit pool for replacements | 13.2 | Partial: recruits land with the ship, no attrition pool simulated |

Other systems feed the economy: [[Trading and Barter]] (the conversation), [[Standing]] (what the Factor thinks of you), [[Rivalry]] (the two companies' price war), and [[Gathering and Gardens]] (food supply).

## See also

- [[Items]]
- [[Hudson's Bay Company]]
- [[North West Company]]
- [[Glossary]]
