# Spec: The Global Trade System

Status: **spec / not built.** Written 2026-10-13. Extends design doc 10.7 (the metropole), 13.3 (credit), 13.6 (fur market and the blockade) and 13.7 (solvency). Owner's ask: *"be able to get a sense of the global trade system in the game."*

## 1. What exists, and what is missing

**Built** (all in `Scripts/Economy/`; see `WIKI/Economy/`):

- One unit, the **Made Beaver (MB)**; every item has a worth in MB.
- **Ledgers** per post, camp and the NWC (`Household`, `FortLedger`, `OutpostLedger`, `TradingPostLedger`, `NwcLedger`, `BandLedger`).
- A **standard of trade**: each counter's `PeltPrice` = goods paid per MB of fur. London sets York's once a year (`LondonCommittee.cs`, 47 lines) from one ratio: furs shipped / quota.
- **Barter** at the counter, **credit and band debt**, the **annual ship** and the **brigades** (real crews, real hull wear).
- Regional trade on the plains: the **Missouri horse fair**, winter corn, the December guns-for-robes expedition (`MissouriTrade.cs`).

**Missing** (the reason there is no "global" feel):

1. Furs never *sell* anywhere. They leave the map on the ship and London's reaction is a ratio, so nothing outside Rupert's Land has a price, a stock or a mood.
2. No money. Everything is goods and MB, so the Company cannot be rich, poor or bankrupt; "solvency" (13.7) has nothing to count.
3. Prices are **flat across space**: a kettle costs the same at York as at Edmonton, which hides the single most important fact about the trade (distance eats margin).
4. No **external shocks** except the blockade's one-time 85 percent order cut in `historical.camp`.
5. No way for the player to **see** the system: no price list, no cargo in flight, no news of a market.

## 2. Principles

1. **Small, causal, and lagged.** The metropole is "the one place this design admits deliberate abstraction" (10.7). A handful of nodes, a handful of commodities, annual clearing. No simulated London people beyond the Committee.
2. **The player knows late.** Market information travels like everything else: by letter, ship and rumour. The player's Trade Ledger shows the *last known* price with its **age** ("beaver 14s, news of March 1812, 7 months old").
3. **Distance is the mechanic.** The system teaches itself through one gradient: goods cost more, and furs pay less, the further you are from a depot.
4. **Pure core, thin shell.** Like `CampaignRunner`, put the model in Godot-free C# (`Scripts/Core/MarketCore.cs`) so a CLI can simulate fifty years in a second (tool T1). The game feeds it ledgers and applies its results.
5. **History drives the shocks, agents drive the rest.** Demand shocks are a dated, authored calendar (13.6: "deliberately scripted layer"); stock, supply and company decisions are simulated.

## 3. The model

### 3.1 Nodes

| Node | Is | Buys | Sells | Lag to the frontier |
|---|---|---|---|---|
| **London** | HBC's sales and the entrepot to Europe | Furs (auctioned), by quality | Outfit (trade goods, guns, cloth) | Ship: leave Sep, arrive Oct-Nov; outfit lands the **next** Aug (indent two years earlier, 13.2/10.7) |
| **Montreal / Lachine** | NWC's agents, McTavish Frobisher / McGillivrays | Furs | Outfit, credit | Canoe: Fort William in July; furs back down in Aug-Sep |
| **New York** | Astor's AFC | Furs, sea-otter by sea | Goods; Congress's 1816 rule | Via the Missouri (St. Louis) and, to 1813, the Columbia |
| **St. Louis** | Missouri Fur Co. | Robes, furs | Goods, horses (via the towns) | River in spring; overland winter |
| **Canton** (off-map) | China market | Sea-otter and beaver by Pacific ship | Tea, silks, specie | Pacific: 1-2 years. *(check historical weight for this window)* |
| **Continental Europe** (off-map) | The consumer: hatters; Leipzig; Russia | Via London | n/a | The **blockade** acts on this edge, not on London itself |

Representation: each node is a record `{ stock, price[commodity], mood, solvency?, in_flight[] }` held in one `World.Market` object, saved with the game.

### 3.2 Commodities

Start with **seven**, each already an item or a clear sum of items:

| Commodity | Unit | Tied to today's items |
|---|---|---|
| Beaver (prime) | MB | `Beaver Pelt`, `Summer Pelt` (quality), `Shot-through Pelt` |
| Other furs | MB | `Wolf Pelt`, `Caribou Hide`, later marten, otter, fox... |
| Robes | MB | `Buffalo Robe` (2 MB) |
| Provisions | food-days | `Pemmican`, `Dried Meat` |
| Horses | head | `Herd.Horses` |
| Arms and powder | pieces | `Trade Musket`, `Powder and Ball` |
| Outfit (staple trade goods) | "trade goods" pieces | `Trade Goods`, `Yard of Stroud`, `Point Blanket`, `Copper Kettle`, `Twist of Tobacco` |

Optional later: liquor (`Dram of Rum`), tea, iron, corn.

### 3.3 Prices: two layers, not one

Today `PeltPrice` conflates two prices that were distinct in fact. Split them:

- **Standard of trade (existing, unchanged): goods per MB at a counter.** What the player and the bands see every day.
- **Market price (new): shillings per MB at a node.** What London or Montreal gets for the furs. The margin between the two is the company's real income.

For commodity *c* at node *n*, year *t*:

```
market(c,n,t) = base(c) * demand_shock(c,t) * (1 - k_supply * (stock_offered / stock_normal - 1))
              * quality(c) * (1 + noise(seed))
```

- `demand_shock` comes from the **shock calendar** (3.5).
- Supply pressure: flood the market and the price falls (13.6), clamped.
- `quality` comes from the fur's season and kill method (already modelled by item ids).
- `noise`: small, seeded, deterministic.

**The standard of trade follows the market with a lag and a margin** (this replaces the current `ratio -> x1.35` rule, keeping it as fallback): a company sets the next year's `PeltPrice` from last year's margin, solvency and the rival's price. If the margin is fat, it can afford a more liberal standard; if thin it tightens. That is the existing intuition, now with something real underneath.

### 3.4 Space: the distance gradient (ship this first)

Every inland post gets a **transport surcharge** on goods and a **discount** on furs, derived from route length and mode already in the map (brigade legs, portages):

```
goods_cost_MB(post) = goods_cost_MB(depot) * (1 + s * route_cost(depot, post))
fur_pay_goods(post) = PeltPrice(depot) / (1 + s * route_cost(...))   // approx.
```

`route_cost` is days on the existing brigade/portage graph (`Scripts/World/Brigade.cs`, `Routes and Portages`). Effect: a kettle that costs 1 MB at York costs ~2 MB at Edmonton and ~3 at Chipewyan, as the historical "standard of trade" differed by district *(check figures)*. Players see it at once: carrying goods inland and furs out is the trade; and *why* the Athabasca was where the fight was hottest.

This is the smallest change that makes the system legible, and needs no new nodes.

### 3.5 External shocks: an authored calendar

A data file, `Data/market_shocks.json` (or a `market.shock(...)` call in the campaign language, tool T4), listing dated multipliers per commodity and per route edge:

| Shock | Dates | Effect |
|---|---|---|
| Continental Blockade | 1806-1814 *(check)*; HBC hit hardest (13.6) | Europe demand for beaver x0.3-0.5 via London; NWC partly bypasses via New York |
| British-US tension, Embargo | 1807-1809 *(check)* | American routes disrupted |
| War of 1812 | 18 Jun 1812 - Dec 1814 | New York and St. Louis flows cut; Great Lakes route risky for NWC |
| Peace in Europe | 30 May 1814 | Europe demand recovers; a glut of held stock floods the market |
| Congress excludes foreigners | 1816 | Foreign traders out of the American trade |
| Columbia sold / Astoria | Oct 1813 | Pacific edge changes owner |
| Merger | 26 Mar 1821 | One seller, cost savings; market power shifts |

`historical.camp` already has some of these as *beats*; the shock file is the **numbers** underneath them, so beats announce and the market behaves.

### 3.6 Flows: cargo in flight

Each leg (York-London, Fort William-Montreal-London, St. Louis-New York, Columbia-Canton) carries `Shipment { commodity, qty, owner, leaves, arrives, risk }`. Arrival triggers the *sale* (furs) or the *landing* (outfit). The pipeline is **visible**: the Trade Ledger lists cargo in flight with a bar. This reuses the Ship and Brigade classes for the York leg; other legs are abstract timers with a loss chance (weather, capture in 1812).

### 3.7 Money and solvency (13.7)

Add a **pounds-sterling cash ledger** per company (`HBC`, `NWC`, later `AFC`, `MFC`). Furs *sold* add cash, outfit *bought* subtracts, wages and losses (burned post, lost brigade, legal fees) subtract. Thresholds from 13.7 become real: when a company cannot fill an indent, the *next year's ship brings less*; austerity, calling in debts, a bailout. The 1821 merger (13.7) is then the **crossing of an exhaustion threshold by both ledgers**, tuned to land near 1821, not a timer.

### 3.8 The player's seat in it

| Player role | What they see and do |
|---|---|
| Labourer / clerk | The **standard of trade** in the post ledger; news of the market in Auld's letters; nothing to decide |
| Post master | Set the local price within the Company band; decide what to buy for the post; **indent** for next year's outfit and live with the lag |
| Freeman / trader | **Arbitrage**: buy robes or horses where they are cheap, sell where they are dear; carry them. Private trade, with its risk of discovery (already an event on the ship) |
| Factor / partner | Read the Company's ledger and set price, quota and the allocation to districts (council: `Council.cs`) |

## 4. Interface: "The Trade Ledger"

New journal page (pattern: `Journal, Map and UI`). Four panes, all age-stamped:

1. **Prices**: a small table per commodity per node, last known value, arrow, age. Tooltip shows the shock responsible ("blockade").
2. **Routes**: a stylised map of nodes and legs (not the game map), cargo markers, expected arrival.
3. **Our books**: the player's company (cash, stock, debt, last year's returns vs quota), as far as the player is entitled to see it.
4. **What I carry**: the player's pack valued at each reachable node, to make arbitrage visible.

Fog rule: a pane shows only what has *reached* the player through a letter, ship, rumour or ledger they can read.

## 5. Build order

| Phase | Deliver | Test | Size |
|---|---|---|---|
| **P0 Observe** | `--dump-economy` (CSV of ledger totals, prices, shipments each week) and the Trade Ledger skeleton with *today's* numbers only | Any run produces a CSV; the page opens | S-M |
| **P1 Space** | Distance gradient (3.4) on goods and furs; show it in the post ledger | Price at post matches table for 5 posts; no goods created or destroyed | M |
| **P2 Market + money** | `MarketCore` with London only; cash ledger; sell on arrival; shock calendar; London's price rule from margin | CLI sim: 50 runs, 1811-1821, HBC lands in a historic band (see 6) | L |
| **P3 Montreal and the NWC leg** | NWC cash and Montreal; credit; Fort William council uses cash | Same; NWC strained by 1818-21 | M |
| **P4 Americans and the Pacific** | St. Louis, New York, Canton; 1812 and 1816 shocks bite | Asymmetry holds: AFC unaffected by the blockade | M |
| **P5 Player ventures** | Indents, arbitrage, freeman trade, flooding the market | Playtest | L |
| **P6 Ledger polish** | Fog and age; news items in letters; charts | | M |

## 6. How to know it is right (acceptance)

- **Conservation**: across any run, goods in = goods out + stock + wastage; cash balances. (Cheap, automatic, kills most bugs.)
- **Determinism**: same seed, same CSV.
- **Qualitative history, not point values.** Backtest to *shapes* until sourced numbers exist: HBC strained in the blockade years and again by 1818-20; NWC hurt less by the blockade but more by the war; both near exhaustion by 1820; merger plausible 1819-1823 under default play. Do **not** put invented prices in docs: add a `// source:` comment to every base price and keep a `docs/specs/global-trade-sources.md` list of what is still unsourced. Sources to pursue: HBC Archives (sales and prices), DCB biographies, Selkirk Papers (the `canadiana-selkirk` skill reads them).
- **Legibility test**: a new player, shown only the Trade Ledger, can answer "why is my kettle dearer here?" and "why did London cut the quota?"

## 7. Risks and decisions for the owner

1. **Pounds sterling or stay in MB?** Pure MB keeps the whole game in one unit but cannot express wages, dividends or the Canton leg. Recommendation: MB for goods and furs, £ only for the company cash ledger, with one conversion constant per year.
2. **How much can the player move the market?** If a single freeman can crash it, it is a toy; if they can't, it is wallpaper. Suggested: the player moves *local* prices and, only with the Company, the annual sale.
3. **Scope creep to a full economy sim.** Hold to seven commodities and six nodes until P4 passes the acceptance tests.
4. **Information lag already fights the "dashboard" instinct.** It is intentional (P.7: knowledge is progression); do not add an omniscient mode except as a debug flag (T2).
