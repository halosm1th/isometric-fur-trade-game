# Spec: Tools for the Game

Status: **spec**, except T0 which is built. Written 2026-10-13.

The project already has one excellent pattern: **`tools/CampaignTool`** compiles the game's own pure C# (`CampaignLogic.cs`, `CampaignRunner.cs`) into a CLI and a web editor, so the tool and the game cannot disagree, with a drift test to prove it. Almost every tool below copies that pattern: *put the model in Godot-free C#, test it with xunit, and let a CLI/web shell drive it.*

Existing tools (do not duplicate): `tools/CampaignTool` (campaign language, check, sim, graph, serve), `tools/audio/*.py`, `tools/sprites/*.py`, `tools/world/*.py`.

## Priority table

| ID | Tool | Why now | Needs | Size | Priority |
|---|---|---|---|---|---|
| **T0** | Wiki checker | Keeps docs honest. **Built**: `tools/docs/check_wiki.py` | none | done | done |
| **T1** | Market simulator (CLI) | Tune the global-trade model without launching Godot | `MarketCore` (global-trade P2) | M | **High** (with P2) |
| **T2** | Economy dump and Trade dashboard | See the whole trade: prices, flows, cash. First thing that gives "a sense of the global system" | `--dump-economy` (global-trade P0) | M | **High** (do first) |
| **T3** | Scenario seeds (jump-to-state saves) | Test Act 6 without playing three years | Save format is JSON v1 | M | **High** |
| **T4** | Campaign tool: arc coverage + `market.*` + import | Write the plains arc and its market in one language | CampaignTool | M | Medium |
| **T5** | Soak harness (headless long run + report) | Catch the "632 people froze" class of bug automatically | A headless run flag | M | Medium |
| **T6** | Wiki generator from code | Stops the Development Status and Economy pages drifting | `Items.cs`, ledgers | M | Medium |
| **T7** | Cast and relationship browser | See who knows whom: needed to author Acts 3 and 6 | Save loader | M | Medium |
| **T8** | Research pipeline (sources to beats) | Historical claims marked *(check)* get sourced | existing skills | S | Medium |

Order I would build in: **T2 -> T3 -> (global-trade P1) -> T1 -> T4 -> T5 -> T6 -> T7 -> T8.**

---

## T0. Wiki checker  *(built)*

`python3 tools/docs/check_wiki.py [--strict] [--quiet]`. Verifies every `link` (names and aliases), every image, frontmatter (`title`, `status` in `implemented | partial | planned | reference | index | stub`), and warns on orphan pages. Today: 239 pages, 0 errors. **Next:** add to CI, and add `--code-claims` (see T6).

---

## T1. Market simulator

**Purpose.** Run the global-trade model for N years, M seeds, with a chosen shock calendar, and print what a designer needs.

```
dotnet run --project tools/MarketTool -- sim  --scenario scenarios/default.market.json --years 1811-1821 --runs 200
dotnet run --project tools/MarketTool -- sweep --param k_supply=0.1:0.6:0.1 --metric merger_year
dotnet run --project tools/MarketTool -- shocks check Data/market_shocks.json
dotnet run --project tools/MarketTool -- dump --seed 7 --csv out.csv
```

**Inputs.** A scenario file: starting stocks and cash per node, the shock calendar, seeds, and optional *policies* for each company (a simple scripted manager) so no game is needed.

**Outputs.** Per run and aggregated: price series per node and commodity; cash and stock per company; shipments in flight; year of merger/exhaustion; histogram over runs; invariant failures (conservation, determinism).

**Checks (the acceptance tests of global-trade section 6, as commands):** `--assert conservation`, `--assert deterministic`, `--assert merger-between 1819..1823 --p 0.8`.

**Shape.** `tools/MarketTool/` (CLI, like CampaignTool) referencing `Scripts/Core/MarketCore.cs` as a linked source file; `tests/MarketTests` (xunit). Reuse CampaignTool's `Server.cs` pattern for T2.

---

## T2. Economy dump and Trade dashboard

**Purpose.** Make the existing trade *visible* before changing it. Fastest way to answer "what is the economy doing?" and the first deliverable of the global-trade spec (P0).

**Two parts.**
1. **`--dump-economy`** (game flag, like `--climate`, `--plainslog`): every in-game week, append a row per ledger (food-days, goods, MB stock, PeltPrice, debt, quota progress), plus ship, brigade and horse-fair events, to `user://economy.csv` (and `.jsonl` for events).
2. **Dashboard**: `dotnet run --project tools/CampaignTool -- serve` already serves static HTML; add `web/trade.html` (or a separate `tools/TradeBoard`) that reads that CSV and draws: price lines per house with London's packets marked; furs-in vs furs-shipped vs quota; goods stock by post; a **flow map** of the brigade routes and Missouri trade; debts by band. A scrubber for time.

**Why first.** It needs no new model, it will expose bugs in the current economy, and the charts become the Trade Ledger's design reference. It is also how you will *feel* the "global" system the owner asked for.

---

## T3. Scenario seeds

**Purpose.** Start the game already in Act 3, 5 or 6 of an arc, with the right people, bonds, debts, horses, herds and date.

**Format.** A small `*.seed.json` that overlays a fresh world: date, player (name, post, rank, language skills, kit, bonds `{npc: regard}`), post stores, band regard and feud scores, herds, flags set (`flags.set`), campaign beats already fired.

**Use.**
```
Godot --path . -- --seed=seeds/plains-act6.seed.json     # in game
dotnet run --project tools/CampaignTool -- sim story.camp --scenario seeds/plains-act6.seed.json   # headless (reuse the sim's scenario)
```

**Why.** Reuses the campaign sim's scenario idea; unlocks fast iteration on late acts and QA of save/load. Include a `seed record` dev command that snapshots the live game into a seed file, so any interesting state becomes a test case.

---

## T4. Campaign tool upgrades

1. **`coverage`**: for a `.camp`, list every library call it uses and mark each *built / stub / missing in the engine* (from `Known.cs` and a `capabilities.json`). Gives a feature-gap report per arc. For the plains arc it would print the section-5 table of the story spec, automatically.
2. **`market.shock(...)` and `market.price(...)`** library calls (needs `MarketCore`).
3. **`import`**: let a campaign include another's beats (historical spine plus an arc module) without copy-paste.
4. **Arc board**: the graph view with beats grouped by Act, colour by built/missing, plus simulated frequency from `--runs`.
5. New library vocabulary listed in the story spec section 4: `posting()`, `band().regard`, `band().feud()`, `horses()`, `plains.raid()`, `accord.propose()`.

---

## T5. Soak harness

**Purpose.** A one-command headless long run with a human-readable verdict.

```
Godot --headless --path . -- --soak=10y --seed=3 --report=soak.md
```

**Report.** Population by nation and post over time (and deaths by cause: cold, hunger, violence, illness); food-days low-water marks; ledger anomalies (negative stock, NaN price); feud and raid counts; unhandled exceptions; wall-clock per game-year. Fails (exit 1) on any invariant: population collapse of a nation, a ledger gone negative, an exception.

**Why.** The commit log shows serious balance bugs found by accident ("a year's run lost 632 people to the cold"). A scheduled soak finds them first.

---

## T6. Wiki generator from code

**Purpose.** Tables in the wiki that are really data (items and worths, ledger parameters, posts, camps, campaign beats, `ItemId` list) should be generated, not typed.

**How.** A script (Python or a `dotnet run` mode of CampaignTool, which already has `docs` -> `Library-Reference.md`) reads `Items.cs`, `world.json`, `Data/*`, `Campaigns/*.json` and rewrites fenced blocks between `<!-- generated:items -->` markers in the matching wiki pages. Plus `check_wiki.py --code-claims`: grep the code for each "**Implemented**" claim's file/symbol listed in a front-matter `code:` field, and flag pages whose cited files vanished or changed since `verified:`.

**Why.** `Development Status.md` today says *Horses: Planned, nothing exists* while `Scripts/Economy/Plains.cs` (650 lines) implements them: the wiki's own rule ("Implemented only if the code runs it") needs a machine to enforce it.

---

## T7. Cast and relationship browser

**Purpose.** For authoring Acts 3 and 6: see, for any save, who knows whom, regard, bonds, kin, debts, who carries what memory of the player.

**Shape.** Reads a save JSON (version 1) or a live `--dump-cast`; renders a force-directed graph per post/camp with filters (nation, role, regard above N); click an NPC for their memories (`Memory and Knowledge`) and goals. Doubles as a debugging view for the Gossip, Lies and Leverage system.

---

## T8. Research pipeline (sources to beats)

**Purpose.** The design is blunt about its own history risk: many names and dates are marked *(check)*. Give each claim a source before it becomes dialogue.

**Process.** For each *(check)* item: look up the Dictionary of Canadian Biography entry (`dcb-timeline` skill crawls biographi.ca links into a dated timeline), the Selkirk Papers (`canadiana-selkirk`, reel to page), and HBC Archives finding aids; write the result into `docs/sources/<topic>.md` with a citation; update the claim in the content doc and remove the *(check)* mark. Output is a checklist that shrinks, tracked in `docs/world-content-needed.md`.

---

## Conventions for every new tool

- Lives in `tools/<name>/` with a `README.md` of five-minute usage (see `tools/CampaignTool/README.md` as the model).
- Pure model code in `Scripts/Core/` or `Scripts/Economy/` with no Godot types; tests in `tests/`.
- CLI first, web second. A tool that cannot run headless cannot run in CI.
- A drift test where the tool and game share a vocabulary.
- Add a row to this table and to `docs/README.md` when it lands.
