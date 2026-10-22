# Audit: the code against the design docs

Date: 2026-10-21. Checked against: `master` at `acd6e17` plus the uncommitted work in the tree (`Economy/Outfits.cs`, `Shipping.cs`, `StandardOfTrade.cs`, `TradeTrips.cs` and the edits listed in `git status`).

Documents audited:
- `docs/fur-trade-game-design.md` (§1-21 and P)
- `docs/specs/*.md`
- `docs/longer_goals.md`, `docs/world-content-needed.md`, `docs/york-factory-test-world.md`
- `WIKI/Development/*`, plus spot checks of `WIKI` pages marked `implemented`

**Method and limits.**
- I read the whole design doc and the specs.
- For the code I read the header comment of every script (about 36,100 lines of C# in 130 files) and searched for each design concept under several likely names.
- I read the code wherever a claim needed checking: skills, personality, pacing, tiers, adherence, marriage, combat damage, multiplayer, recruits, the union, ageing, contagion, skimming and item use.

This is a spot-check, not a line-by-line review. "Not built" means nothing was found under any likely name. That is strong evidence but not proof. The first pass marked two systems missing that are partly built (ageing and fever contagion), so a "Missing" verdict here is worth a quick grep before you act on it.

**Docs updated in this pass.** These now match the code: the design doc's implementation notes (9 refreshed, 10 added), `longer_goals.md`, `WIKI/Development/Development Status.md`, `Roadmap.md` and `Changelog.md` (entries for 13-20 October), `WIKI/Systems/The Reckoning of 1821.md`, `docs/specs/story-plains-years.md`, `docs/york-factory-test-world.md`, `docs/world-content-needed.md` and `docs/README.md`.

---

## 1. Summary

- **The core architecture is built and faithful to the design.** That covers the unified agent, the goal stack with Tier 0 interrupts, messages that act only when read, memory with hops and witness lists, the Director, campaign adherence, delegation, the share-weighted council, spawn-site ecology with moving herds, fire as an object, travel mode, horses, war parties, and multiplayer with agreed skips. The player-experience layer (§P) is the most complete part of the game.
- **The status docs had drifted badly.** They were last checked on 2026-10-13, and at least 15 claims in them had since become false. They are corrected in this pass (§3).
- **Seven places where the code contradicts the design doc** (§4). The worst is marriage, which applies HBC rules to everyone.
- **Twelve places where a comment or doc claims more than the code does** (§5). Examples: London never catches the Factor who shades the books; muskets never use powder; recruits come from a list of 24 names that runs out; the "Reckoning of 1821" weighs nobody.
- **About 70 design ideas have no code** (§7). The largest group is the economic endgame: money, solvency, the market, the recruit pool and the merger from exhaustion. Together those decide whether 1821 is simulated or scripted. Today it is scripted.
- **Quests are the one large system the design doc never mentions** (§6). They sit in tension with §2's "simulate, don't script".

---

## 2. Section-by-section verdict

Legend: **Built** = matches the doc in substance · **Partial** = the shape is there, notable parts missing · **Diverges** = built differently · **Missing** = nothing found.

| § | System | Verdict | Evidence / gap |
|---|---|---|---|
| 1, 11.1 | Player as an equal agent | Built | `Agents/Agent.cs`: player and NPCs share needs, inventory and verbs |
| 1 | 1821 weighs the player | Missing | `Economy/Union.cs` keeps every player on. Nothing measures them against their ambitions |
| 2.2 | Director | Built | `Core/Director.cs`: memory choice, `Equal()` tie-break by player proximity (7 callers), `MayActNow`, `--nodirector` |
| 2.3 | Campaign scripts and adherence | Built | `Core/CampaignRunner.cs`: Firm forces at 2× grace, Strict at 1×; logged `[forced]` |
| P.2 | Ambitions | Partial | Five offered. (`FoundAPost` removed until it can be built) |
| P.3 | A year in 4-6 hours | **Not met** | About 73 h flat. Uneven pacing gives about 57 h for a player alone in fair weather (my arithmetic from `GameCalendar.Pacing`; not measured in play) |
| P.4 | Journal chains, focus panel, notices | Built | `Core/Reveal.cs`, `Core/Focus.cs`, `UI/NoticeLog.cs` |
| P.6 | Succession | Built | `Agents/Heirs.cs` |
| P.8 / P.8.1 / P.10 | Attachment, favours, leverage, lies, year told | Built | `Friends.cs`, `Favours.cs`, `Leverage.cs`, `Lies.cs`, `Losses.cs`, `Barks.cs`, `YearStory.cs` |
| 3.1-3.4 | Goal tiers, chains, shared verbs | Built | `Goals/Goal.cs` |
| 3.5 | Goal-reading skill | Missing | "Get to know them" (`ConversationAcquaint.cs`) is the nearest thing |
| 3.6.1 | Barter | Built | `Economy/Barter.cs`: gift, tells, counter, patience, walking off |
| 3.7 | Career rival | Built | `Agents/Rival.cs` |
| 3.8.1 | Fort William council | Partial | Share votes, carried agenda, interceptable minutes. **No proxies, splinters or arrival windows** |
| 3.8.2 | Accords | Missing | Truces are a pair table with an end day (`Economy/War.cs:51`) |
| 3.9 | Delegation | Partial | Morning tasks, matching, overdue reports, the clerk posting. Morning orders take effect at once rather than travelling as messages. **No audits**. Skimming has no detection (see §5) |
| 3.10 | Skills | **Diverges** | 4 against about 12 (§4.3) |
| 4.2 | 1801 prologue | Missing | Open question (§20) |
| 4.4 | Merger from exhaustion | **Diverges** | A flag (§4.7) |
| 5.1-5.2 | HBC and NWC paths | Built | `Core/Starts.cs`, `CharacterSheet.cs` |
| 5.3 | Métis/freeman path | Missing | The faction, camps, carts and Michif exist; there is no start path |
| 5.4 | Character creation | Partial | No personality sliders or skill points |
| 5.5 | Arrival as an order | Built | `ReportForDutyGoal`, for recruits too |
| 6.1 | Map and date gating | Built | `World/WorldMap.cs`, `World/Posts.cs` |
| 6.2 | Southern Department | Missing | Moose Factory is a post with a plan. No department, review or ship |
| 7.2 | Intertribal war | Partial | Horse raids, war parties with morale and retreat, truces, guns through the Missouri trade |
| 7.3 | Americans | Partial | `Faction.American`, posts, men, regard, a story road. No Astor or St. Louis nodes, and no poaching |
| 7.4 | Vendetta | Built | `Agents/Grudge.cs`. No settlement Accord |
| 8 | Marriage | **Diverges** | §4.1 |
| 9.1 | Captivity | Built | Captivity, parole, ransom |
| 9.2 | Slavery | Missing | |
| 9.3 | Death branches | Partial | Succession only |
| 9.4 | Ageing | Partial | +1 a year; NPCs past 45 slow; children born and grown (`World/Population.cs:178`). No decline for the player, and nobody dies of age |
| 10.1 | Message object | Built | `Core/Message.cs` |
| 10.3 | Interception | Partial | `SellPacket` sells a carried packet. **No forgery, express couriers or blowback** |
| 10.4 | Propagation | Built | `Agents/Memory.cs` |
| 10.5 | Gossip rolling up into faction trust | Partial | A band's regard moves with events (`BandLedger`). A periodic review of "unexplained sightings" was not found |
| 10.6 | Languages | Partial | Ten tongues, kinship, interpreters. **No literacy**, and no interpreter who lies |
| 10.7 | Metropole | Partial | London is a ratio (`LondonCommittee.cs`). No Montreal, named London agents or American nodes |
| 10.8 | Maps as items | Missing | Fog and site knowledge exist |
| 10.9 | Legacy across kin | Partial | The heir inherits blood debts. Other memories of the dead are not re-pointed at kin |
| 11.4 | Personality | **Diverges** | §4.2 |
| 11.5 | Getting ahead of the story | Not checked | No "authorised" memory flag was found by name |
| 12.1-12.2 | Vessels and portages | Partial | Brigades with hull wear and gum. York boats only as convoy cargo. **No player canoe condition** |
| 12.3 | Horses | Built | `Herd.cs`, `HorseTrade.cs`, `PlayerHorse.cs` |
| 12.4 | Carry capacity | Built | A constant plus bag bonus; overload slows |
| 12.5 | Items, worn/readied, caches | Partial | No hidden player caches |
| 12.6 | Crafting | Missing (except NPC food chains) | `MakePemmicanGoal`, drying meat, spruce beer |
| 12.7 | Book pages; perks | Partial | *Yourself* page built. Perks missing |
| 13.1 | Starvation cascade | Partial | Short commons are tracked. The fallback ladder (famine foods, eating draft animals) was not found |
| 13.2 | Recruit pool | Partial | Hiring limited by the company's purse and the war (`Economy/Companies`); no finite pool of men or lagged news yet |
| 13.3 | Debt | Partial | Band debt per company. Debt is not used as a weapon |
| 13.4 | Alcohol | Partial | Intoxication only |
| 13.5 | Disease | Partial | Fever spreads between people standing together (`Population.SicknessAndYears`). No epidemics, contagion through goods, or quarantine |
| 13.6 | Fur market and blockade | Missing | The blockade is campaign flags |
| 13.7 | Solvency | Missing | |
| 13.8 | Founding a post | Missing | |
| 14.1 | Water state per waterway | Missing | The calendar and chapters are built |
| 14.2 | Weather | Built | `Core/Weather.cs`, `UI/WeatherOverlay.cs` |
| 14.3 | Day length by latitude | **Diverges** | Fixed at 57° N |
| 14.4 / 16.2 | Species | Partial | 7 species |
| 15.3 | Hit location | **Diverges** | §4.4 |
| 15.4 / 15.6 | Morale, setup | Built | `Agents/Nerve.cs`, `War.cs` morale |
| 15.5 | Surrender and bluff | Partial | Only captivity |
| 16.1 | Hunting verbs | Partial | The communal run is built. Stalking is the ordinary hunt |
| 16.3-16.5 | Sites, overharvest, herds | Built | `Ecology/EcologyManager.cs` |
| 17.1-17.2 | Hunger, thirst | Built | |
| 17.3 | Wet clothing | Missing | |
| 17.5 | Wound treatment | Missing | |
| 17.6 | Winter strain | Missing | |
| 18.1 | Continuous world | Built | |
| 18.2 | Tiers | **Diverges** | §4.5 |
| 18.3 | Travel mode | Built | `UI/TravelMode.cs` |
| 18.4 | Fire | Partial | Weather scales spread. **No wind direction, firebreaks or prairie fire** |
| 21.3 | Consensus skip | Partial | No auto-pilot fallback |
| 21.4 | Disconnect hand-off | **Diverges** | §4.6 |

---

## 3. Stale claims corrected in this pass

| Where | Claimed | Now says |
|---|---|---|
| Design doc §12.3 note | No horses | Built, with files |
| Design doc §7.3 note | No American faction or people | `Faction.American`, posts, men, story road |
| Design doc §5.3 note | Communal hunt not built | Built (`BuffaloRun`) |
| Design doc §9.4 note | No ageing | Partly built |
| Design doc §P.3 and §18.3 | Year about 73 h | 73 h flat, about 57 h with uneven pacing; still not met |
| `longer_goals.md` #2, #4, #7, #8 | Ageing, war parties, on-screen weather and Americans not built | Partly built or built |
| Dev Status | Ageing, war parties and truces, morale, UI theme, on-screen weather, black market: Planned or "not verified" | Corrected, with files |
| Dev Status | Marriage "Implemented (partial)" | Partial, with the HBC-only caveat |
| Dev Status "What is missing" | Horses, mounted warfare, hunt-captain discipline | Removed; real gaps listed |
| Roadmap Plains, Information, Disease, People rows | Built work listed as planned | Marked built; the remaining gaps named |
| `The Reckoning of 1821.md` | The union is "a scripted beat that changes the rivalry and sends news" | It happens in the world; the player is never weighed |
| `story-plains-years.md` | Act 6 foundation only; captain, war party and truce not built | Updated |
| `york-factory-test-world.md` | "No horses" | Corrected |
| `world-content-needed.md` §2, §10 | No American faction; cursor and UI theme wanted | Corrected |
| Changelog | Ended 2026-10-12 | Entries for 13-20 October added |

---

## 4. Where the code contradicts the design

Each of these now has an implementation note in the design doc. They still need a decision: change the code, or rewrite the section.

1. **Marriage is HBC-only for everyone (§8).** `Propose()` (`UI/Conversation.cs:1632`) refuses a second wife to anyone and always moves the bride into York's depot. That is wrong for a Nor'Wester and for a band-born player. The Nor'West way, polygamy, transition costs and the wives left behind are all missing.
2. **Personality (§11.4).** The code has Loyalty, Talkative, Suspicion, Ambition and **Courage**. The doc lists sociability and discretion instead of Talkative and Courage. Talkative does the work of both missing traits.
3. **Skills (§3.10).** The code has 4: Hunting (which also covers shooting), Trapping, Trade and Woodcraft. They are seeded from background and traits, not personality.
4. **Combat (§15.3).** Damage is a single health pool. There are no hit locations, crippling blows or limb wounds.
5. **Tiers (§18.2).** Two tiers resolved daily, not three.
6. **Disconnection (§21.4).** The character is despawned, not run by the planner.
7. **The union of 1821 (§4.4, §13.7).** It fires on the campaign's `merger` flag. Under "no script" it never happens.

---

## 5. Implementation lies: claims the code does not back

Places where a code comment, a citation or a doc describes behaviour that isn't there, or only partly is. None of these is a crash. They mislead whoever reads the code next.

| # | The claim | Where | What the code actually does |
|---|---|---|---|
| 1 | "A Factor ... shades the books (design doc 3.9 step 8)". Step 8 says "the lie resolves at that read event" | `World/Ship.cs:115` | **Fixed (Oct 2026):** London compares the furs landed with York's account (`LondonCommittee.Audit`); the chance of noticing grows with the gap. Once is a letter and lost standing; twice, or a gross theft, and the Factor is recalled on the next ship and the post passes to the next man (`Npc.Recall`) |
| 2 | "Powder and ball" is stocked, traded and named as the musket's charge | `Items.cs`, the stores at every post | **Fixed (Oct 2026):** a horn is twenty charges and a shot spends one, refilled from the pack or the house's store; without powder a gun is a club (`Agent.UseCharge`); hunts in the day's sum need powder to use the gun |
| 3 | "The ship lands enough new hands to bring the post back to strength (design doc 5.5)" | `World/Population.cs:390` | **Fixed (Oct 2026):** names are drawn from Orkney (and Canadian) given names and surnames, hundreds of them; how many are hired is the company's purse and the war (`Economy/Companies.Hires`) |
| 4 | "A year should take four to six hours of play" | `Core/GameCalendar.cs:32` | The uneven pacing beneath the comment slows the busy seasons as much as it speeds winter nights. About 57 h |
| 5 | "Four traits" | `Agents/Npc.cs:19` | Five fields, and not the doc's five |
| 6 | Skills cite "design doc 3.10" | `Agents/Needs.cs:11` | 4 of the 12 or so skills |
| 7 | `AmbitionKind.FoundAPost` with title, pledge, rival target and Director keywords | `Ambition.cs`, `Rival.cs:173`, `Director.cs:170` | **Removed (Oct 2026)** until founding a post can be built properly |
| 8 | Role names `CreeChief`, `CreeHunter`, `CreeWoman`, `CreeDaughter` | `Agents/Npc.cs:14` | **Fixed (Oct 2026):** `Chief`, `Hunter`, `BandWoman`, `BandDaughter`, with the old names kept as aliases of the same numbers for saves and code not yet moved over |
| 9 | Orders are messages, inert until read (§3.9 step 4, §10.1.1) | Design doc | The Factor's morning orders take effect at once. Only London's directive and the council's minutes travel (the York test-world notes admit this) |
| 10 | "The Reckoning of 1821": the new order weighs what the player has become (design doc §1) | `WIKI/Systems/The Reckoning of 1821.md`, `Union.cs` | Every player is kept on; nobody is weighed. (Page corrected in this pass) |
| 11 | Fire "spreads ... according to the weather; a gale drives it" | `World/Fire.cs:9`, `:92` | Weather scales how often it spreads, never which way. There is no wind direction |
| 12 | The made-beaver token "worth exactly 1 MB in barter" | `world-content-needed.md` §8 | `ItemId.MbToken` has no use anywhere outside the item table. (The wiki item page says so honestly) |

**Honest by contrast:** the wiki's Personality, Skills, Simulation Tiers, Powder and Ball, and Made-Beaver Token pages all describe the build as it is, divergences included. The drift was in the summary pages, not the system pages.

---

## 6. Built but not in the design doc

| System | Where | Note |
|---|---|---|
| **Quests** | `Core/Quests.cs`, `QuestScript.cs`, campaigns, `tools/CampaignTool` | Authored chains of objectives that branch on conversation choices. The design doc should say where they sit relative to §2: they ask things of the player and never change the world except through ordinary actions |
| Campaign language, compiler and web editor | `tools/CampaignTool`, `docs/scripting`, `docs/wiki` | The two docs folders overlap |
| Market news | `Economy/MarketNews.cs` | A good instance of §10 |
| Gardens and Missouri trade | `Economy/Gardens.cs`, `MissouriTrade.cs` | Covers part of farming |
| Births and children growing up | `World/Population.cs` | Feeds §P.6 ("the family is the save file") but isn't described there |
| Standard of trade by people; outfits from the map's edges; ship-time carrying; camp trade trips | Four **uncommitted** files | Physical logistics groundwork. Not the `global-trade` spec |
| Courage and nerve | `Agents/Nerve.cs` | §4 item 2 |

---

## 7. Suggested but not built: the full catalogue

Grouped by area, with the source of each idea. "Doc" = `fur-trade-game-design.md`. "Content" = `world-content-needed.md`. "Plains" / "Trade" / "Tools" = the specs.

### 7.1 The economy and the endgame
- Money and a company cash ledger (Trade §3.7; Doc 13.7).
- A solvency ledger, crisis moves (austerity, calling in debts, a bailout) and the merger from exhaustion (Doc 13.7, 4.4).
- A fur market with quality-weighted clearing, holding furs off a low market, and flooding the market as a weapon (Doc 13.6).
- An authored shock calendar for prices: the blockade, 1812, 1816 (Doc 13.6; Trade §3.5).
- A distance gradient, so goods cost more inland (Trade P1).
- A Trade Ledger page and `--dump-economy` (Trade P0; Tools T2).
- Warehouse quality-bucketing (Doc 12.5, 20).
- A finite recruit pool per company, refilled by finances and lagged news; an NWC pipeline; Pacific Fur Company men after 1813 (Doc 13.2, 10.7).
- Debt as a weapon: a rival forgiving a trapper's debt, and Simpson calling debts in (Doc 13.3).
- Audits, and London detecting shaded books (Doc 3.9).
- Founding a post: the site, approval, vulnerable construction, staffing (Doc 13.8); NPC leaders founding posts (Doc 20).
- The 1821 judgement of the player, the Simpson epilogue and the Williams Southern Department coda (Doc 1, 4.3).

### 7.2 Companies and the metropole
- Fort William council: proxies, splinters on the XY Company model, arrival windows by district distance (Doc 3.8.1).
- The NWC's two-years-in-three rule, shares a clerk can earn, and the French-Canadian rank ceiling (Doc 5.2).
- Montreal agents and their yearly routine; named London Committee members (Doc 10.7; Content §2).
- The Southern Department: Moose Factory as a third depot with its own review and ship (Doc 6.2).
- Astor/New York and St. Louis background nodes; the 1816 exclusion law; American trapline poaching (Doc 7.3).
- The XY Company and an 1801 start (Doc 4.2).
- The Red River Colony as people: settlers, Fort Douglas, Macdonell, Semple, De Meuron soldiers (Content §2, §3).
- Named wintering partners per department (Content §2).

### 7.3 Agreements, justice and information
- Accords as one object, covering peace, trade exclusivity, safe passage, alliance, debt restructuring and vendetta settlement (Doc 3.8.2).
- Forged dispatches and a means of verifying a real one (Doc 10.3).
- Express couriers; a hidden blowback roll for selling mail (Doc 10.3).
- Literacy: who can read and write dispatches (Doc 10.6).
- Interpreters who lie on purpose (Doc 10.6).
- Maps as physical items that can be bought, stolen and sold (Doc 10.8).
- Faction trust rolling up periodically from unexplained sightings (Doc 10.5).
- Briefing people ahead so a sighting files as authorised (Doc 11.5).
- Memories of the dead passing to kin in general, not only blood debts (Doc 10.9).
- Letters, dispatches and ledgers as items (Content §8).

### 7.4 People
- The Métis/freeman starting path (Doc 5.3).
- Fuller arrivals for the NWC and Métis (Doc 5.5).
- Personality sliders and skill points at creation (Doc 5.4).
- Faces, bodies and clothes in a character creator (`longer_goals` #3).
- Marriage: the Nor'West way, polygamy, transition costs, wives left behind, and whether tracks can change more than once (Doc 8, 20).
- Slavery: escape, ransom and adoption (Doc 9.2).
- Death branches: a new character in the same world, or a new world (Doc 9.3).
- Decline with age for the player, and dying of age (Doc 9.4).
- Perks earned from notable events (Doc 12.7).
- The goal-reading skill (Doc 3.5).
- Skills: stealth, riding, sprinting, medicine, strength, crafting, smithing (Doc 3.10).
- Sociability as its own trait (Doc 11.4).
- Roles: tradesmen (blacksmith, cooper, carpenter, boatbuilder, tailor, cook, surgeon), boat ranks (steersman, bowsman, guide), interpreter, children, settlers, governor and sheriff, soldier, ship's captain, Métis hunter and hunt captain, Iroquois voyageur, elders (Content §7).
- Relationship tiers and adoption or naming into a band (Plains engine #2).
- The player made a soldier in the buffalo run (Plains Act 5).
- Barks in several voices (Orkney Scots, Canadien French, Cree, Michif, Gaelic) and name lists per people (Content §9).
- Holidays: Christmas and Hogmanay, the King's birthday, Sundays (Content §9).

### 7.5 Survival, health and the body
- Wet clothing losing its warmth; fords and thin ice (Doc 17.3).
- Winter strain from isolation, rising to mutiny, with a Factor's countermeasures (Doc 17.6).
- Wound treatment: herbal versus European, and the medicine skill (Doc 17.5).
- Hit location and severity; muskets using powder (Doc 15.3; Content §8).
- Surrender and bluff (Doc 15.5).
- Epidemics: smallpox, measles, contagion carried in trade goods, quarantine (Doc 13.5).
- Addiction, diluted liquor and the Fire Test (Doc 13.4).
- The starvation fallback ladder: rationing, famine foods, eating draft animals (Doc 13.1).
- Food variety: grease, marrow fat, wild rice, maple sugar (Content §8).
- Clothing beyond three items: sash, leggings, moccasins, mittens, tuque (Content §8).

### 7.6 Making things
- Crafting recipes anywhere, facility-gated smithing at a forge, canoe building by season and forest, and repair-only manufactured goods (Doc 12.6).
- The player running the pemmican chain (Plains engine #5).
- Farming by the player (`longer_goals` #5).

### 7.7 Transport and the land
- York boats, dog sleds, toboggans and Red River carts as owned transport (Doc 12; Content §8).
- Player canoe condition (Doc 12.2).
- Water state per waterway; the Autumn Scramble and Spring Lockout; caching a canoe to go on by sledge (Doc 14.1).
- Day length by latitude (Doc 14.3).
- A local simulation tier (Doc 18.2).
- Prairie grass fires, wind direction, firebreaks, green wood that resists fire (Doc 18.4).
- Species: moose, bear, marten, lynx, fox, muskrat, otter, fish as animals, salmon runs, and the hare/lynx cycle (Doc 16.2; Content §6).
- Herd fragmentation (Doc 16.5).
- A map marker for brigades you haven't met; Métis winter hunt; Columbia and Pacific brigades (Doc 12.2a).
- Seven Oaks and Brandon House as places; sacred sites; Fort William's building set (Plains #9; Content §5).

### 7.8 Multiplayer and tools
- Disconnected players handed to the goal planner; auto-pilot for the players who wanted to skip (Doc 21.3, 21.4).
- Tools T1 (market simulator), T2 (economy dump and dashboard), T4-T8; T3 partly met by `--quest` and "play from here" (Tools).
- `check_wiki.py --code-claims`: the guard against the drift in §3 (Tools T0 and T6).

### 7.9 History content
- Campaign beats still wanted: Macdonell wintering at York, the Athabasca starvation, Lagimodière's walk, the Selkirk-Peguis treaty, the missionaries, the arrests at Grand Rapids (Content §9).
- Timeline back to 1766, and America's response if Lewis and Clark are killed (`longer_goals` #1).

---

## 8. Specs status

| Spec | Status |
|---|---|
| `global-trade.md` | **Not started.** P0-P6 have no code |
| `story-plains-years.md` | Acts 0-1 and 7 built; 5 mostly built; 6 partly built; 2-4 partly built. Engine items #4 and #6 are built; #1, #3, #5, #8 and #10 are partly done; #2, #7 and #9 are not (table updated in place) |
| `tools.md` | T0 built; T3 partly done; the rest not started |

---

## 9. Documentation hygiene

- Two copies of the scripting docs (`docs/scripting/`, `docs/wiki/`, and `WIKI/scripting/` after `b823b0e`). Keep one.
- The wiki still has no pages for horses, war parties and truces, Missouri trade, the union, or market news (listed in `docs/README.md`).
- Asset and audio inventories are still dated 2026-10-13 and were not re-checked here.

---

## 10. Recommendations, in order

1. **Fix marriage for non-HBC players** (§4.1). It is player-facing and wrong on two of the four roads in.
2. **Decide the design-versus-code questions** (§4: traits, skills, tiers, disconnection) and rewrite the sections either way.
3. **Clear the small lies** (§5): powder use, the recruit list, `FoundAPost`, the Cree-named roles, London detecting shaded books. Each is a few hours.
4. **Add a quests section** to the design doc (§6).
5. **Commit the uncommitted economy files**, then start global-trade P0 and tools T2. That is the road to an emergent 1821 (§7.1).
6. **Add `check_wiki.py --code-claims`** or re-run this audit after every large merge. The drift in §3 built up in eight days.
