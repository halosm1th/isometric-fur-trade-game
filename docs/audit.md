# Audit: the code against the design docs

Date: 2026-10-30. Checked against: `play-feedback-fixes` at `cf835bd` (336 commits), plus one uncommitted whitespace change to `World/Ship.cs`. The previous audit was 2026-10-21 at `acd6e17`; 82 commits have landed since.

Documents audited:
- `docs/fur-trade-game-design.md` (§1-21 and P)
- `docs/specs/*.md`
- `WIKI/Development/Development Status.md` and `Roadmap.md`

**Method and limits.**
- I started from the 21 October audit and re-checked every row it marked Partial, Missing or Diverges.
- For the code, I read the header comment of all 37 scripts added since then and of the most-changed existing ones (`Tiers.cs`, `Ship.cs`, `Npc.cs`, `Conversation.cs`, `Union.cs`). I searched for each remaining gap under several likely names, and read the code where a verdict turned on it: the company purse, the union trigger, goal reading, day length, disconnects and the tiers.
- The code is now about 47,100 lines of C# in 172 scripts, up from about 36,100 in 130.

This is a spot-check, not a line-by-line review. "Missing" means nothing was found under any likely name. That is strong evidence but not proof. **Nothing here was run.** In particular, nobody has seen the company purse exhaust a company in a long run (§4).

**Docs updated in this pass.** These now match the code: the design doc's "last verified" line, its §13.7 note, and a new §3.5 note; `Development Status.md` (header, marriage row, solvency row, "what is missing"); and `Roadmap.md` (the Marriage, Company, Combat and Survival rows).

---

## 1. Summary

- **The architecture is still faithful to the design**, and the player-experience layer (§P) is still the most complete part. Nothing that was Built on 21 October has regressed.
- **Closed or moved forward since 21 October:** marriage by each side's custom; the goal-reading skill (§3.5); company money, with a merger when a company is exhausted (§13.7, §4.4); London catching shaded books; powder; the recruit names; orders that need a man within earshot; wet clothing (§17.3); shallow, deep and white water; and the distance gradient (global-trade P1). Ten of the twelve false claims in the old §5 are fixed.
- **The biggest change is to the endgame.** The 1821 union can now come from the companies running out of money, not only from the campaign's flag. That was the audit's top structural gap. It is wired in but not proven: no soak run has shown a company exhausting on its own (§4).
- **Large new systems that the design doc doesn't describe** (§6): **laws** made and repealed by meetings and councils, **towns** with farms, fields, coin and store credit, **life at York** and **incidents on the journey**, **musters** run by campaigns, an **observer mode**, and **lacrosse**. Laws and the towns are good instances of §2. Musters and the scripted York raid sit in the same tension with §2 as quests do.
- **The build order runs against §P.9.** That section says to make one river and one year fun before adding regions. Since 21 October the map has grown to the Pacific and the St. Lawrence, with 3,267 town people on the Lakes frontier and the War of 1812. York is getting attention now, but the milestone that one year on the river is fun on its own has not been tested.
- **The pacing target (§P.3) is still unmeasured.**

---

## 2. Section-by-section verdict

Legend: **Built** = matches the doc in substance · **Partial** = the shape is there, notable parts missing · **Diverges** = built differently · **Missing** = nothing found. **Changed** marks a verdict that moved since 21 October.

| § | System | Verdict | Evidence / gap |
|---|---|---|---|
| 1, 11.1 | Player as an equal agent | Built | `Agents/Agent.cs` |
| 1 | 1821 weighs the player | Partial | `Union.Weigh`: given charge, kept on, or cut loose as a freeman. No end screen or epilogue |
| 2.2 | Director | Built | `Core/Director.cs` |
| 2.3 | Campaign scripts and adherence | Built | `Core/CampaignRunner.cs` |
| P.2 | Ambitions | Partial | Five offered; `FoundAPost` removed until founding a post is built |
| P.3 | A year in 4-6 hours | **Not measured** | Quiet days (`Core/QuietDays.cs`) run idle days at x80. Not timed over a real year |
| P.4 | Journal chains, focus panel, notices | Built | `Core/Reveal.cs`, `Core/Focus.cs`, `UI/NoticeLog.cs`; `UI/RewardPopups.cs` adds visible standing and skill gains |
| P.5 | Needs create decisions | Built (Changed) | `Core/YorkHappenings.cs` (waiting at York), `World/JourneyIncidents.cs` (the boats) |
| P.6 | Succession | Built | `Agents/Heirs.cs` |
| P.8 / P.8.1 / P.10 | Attachment, favours, leverage, lies, year told | Built | Unchanged |
| P.9 | One river, one year first | **Not followed** | See §1 |
| 3.1-3.4 | Goal tiers, chains, shared verbs | Built | `Goals/Goal.cs` |
| 3.5 | Goal-reading skill | **Built (Changed)** | `UI/ConversationDepth.cs`: insight per person, tiered from a vague sense of priority to the goal to its steps, and spotting lies. Earned by talking, as the doc asks |
| 3.6 | Persuade | Built (Changed) | `ConversationDepth.cs`: five appeals, chosen by who the listener is |
| 3.6.1 | Barter | Built | `Economy/Barter.cs` |
| 3.7 | Career rival | Built | `Agents/Rival.cs` |
| 3.8 | Councils | Partial (Changed) | `Core/Laws.cs`: motions canvassed, debated and voted in town meetings, band councils and the Committee |
| 3.8.1 | Fort William council | Partial | Share votes, carried agenda, minutes that can be intercepted. **No proxies, splinters or arrival windows** |
| 3.8.2 | Accords | Missing | Truces are still a pair table (`Economy/War.cs`) |
| 3.9 | Delegation | Partial | Orders need earshot (`Delegation.Earshot`). London audits the Factor's books (`LondonCommittee.Audit`). At York the Factor hears of private dealings someone saw. **No surprise inspections by a superior** |
| 3.10 | Skills | **Diverges** | Still 4 (`Agents/Needs.cs`) against about 12 |
| 4.2 | 1801 prologue | Missing | |
| 4.4 | Merger from exhaustion | **Partial (Changed)** | `Economy/Companies.cs`: two years owing more than a year's costs exhausts a company, and `Union.Daily` then makes the union in any year. Not seen in a run |
| 5.1-5.2 | HBC and NWC paths | Built | `Core/Starts.cs`; campaigns can now set their own start, including the King's soldier and the deserter |
| 5.3 | Métis/freeman path | Missing | "Freeman's son" is a background, not a road in |
| 5.4 | Character creation | Partial | No personality sliders, skill points, or face or body customisation. Garments are dyed per person (`Agents/Garb.cs`) |
| 5.5 | Arrival as an order | Built | |
| 6.1 | Map and date gating | Built | The map now runs 127°W-71°W, 42°N up, 1260 × 2713 cells |
| 6.2 | Southern Department | Missing | |
| 7.2 | Intertribal war | Partial | Unchanged; lacrosse can now cool a feud (`Economy/Lacrosse.cs`) |
| 7.3 | Americans | Partial | The War of 1812 on the Lakes frontier (campaign part `lakes-war.camp`). No Astor or St. Louis nodes, and no poaching |
| 7.4 | Vendetta | Built | No settlement Accord |
| 8 | Marriage | **Built (Changed)** | `Agents/Marriage.cs`: the Company way, the Nor'West way, born to a band; plural wives off the Company's books; wives left behind |
| 9.1 | Captivity | Built | |
| 9.2 | Slavery | Missing | |
| 9.3 | Death branches | Partial | Succession only |
| 9.4 | Ageing | Partial | Unchanged |
| 10.1 | Message object | Built | |
| 10.3 | Interception | Partial | No forgery, express couriers or blowback |
| 10.4 | Propagation | Built | |
| 10.5 | Gossip rolling up into faction trust | Partial | Unchanged |
| 10.6 | Languages | Partial | No literacy, and no interpreter who lies |
| 10.7 | Metropole | Partial (Changed) | London's ship and the Montreal canoes enter at the map's edge (`Economy/Outfits.cs`); furs are sold at London, Montreal, New York and St. Louis (`Companies.cs`). No named agents or Montreal routine |
| 10.8 | Maps as items | Missing | |
| 10.9 | Legacy across kin | Partial | Unchanged |
| 11.4 | Personality | **Diverges** | Still Loyalty, Talkative, Suspicion, Ambition, Courage |
| 12.1-12.2 | Vessels and portages | Partial (Changed) | The player sits and paddles a canoe; white water at the portages. **No canoe condition for the player** |
| 12.3 | Horses | Built | |
| 12.5 | Goods by hand; caches | Partial (Changed) | Goods physically carried from the map's edge by brigade and feeder run (`Outfits.cs`, `TradeTrips.cs`, `Shipping.cs`). No hidden player caches |
| 12.6 | Crafting | Missing (except NPC chains) | Farming in the towns is step by step (`World/Fields.cs`), but there are no player recipes |
| 12.7 | Book pages; perks | Partial | Perks missing |
| 13.1 | Starvation cascade | Partial | Posts buy provisions to a season's need (`Economy/PostWants.cs`). No famine-food ladder |
| 13.2 | Recruit pool | Partial | Hiring follows the purse and the war. No finite pool or lagged news |
| 13.3 | Debt | Partial (Changed) | Town houses buy on the store's book (`Economy/Money.cs`). Debt is not used as a weapon |
| 13.4 | Alcohol | Partial | Laws can bar rum from a camp |
| 13.5 | Disease | Partial | No epidemics |
| 13.6 | Fur market and blockade | **Partial (Changed)** | `Companies.Price`: the blockade, the war, a glutted sale, yearly luck. The blockade bites only if the campaign raises its flag. No holding furs back, and no flooding as a weapon |
| 13.7 | Solvency | **Partial (Changed)** | A purse per company: sales, goods, freight and wages; it orders the outfit and limits hiring. No credit, legal fees, violence losses or crisis moves |
| 13.8 | Founding a post | Missing | |
| 14.1 | Water state per waterway | Partial (Changed) | Depth and white water per cell (`WorldMap.Depth`, `World/WaterMarks.cs`). Freezing is still one season for the whole map |
| 14.2 | Weather | Built | |
| 14.3 | Day length by latitude | **Diverges** | Still fixed at 57° N; `LatitudeDeg` is never set |
| 14.4 / 16.2 | Species | Partial | Unchanged |
| 15.3 | Hit location | **Diverges** | Single health pool. Walls now stop shots |
| 15.4 / 15.6 | Morale, setup | Built | |
| 15.5 | Surrender and bluff | Partial | Only captivity |
| 16.1-16.5 | Hunting, sites, herds | Built / Partial | Unchanged |
| 17.1-17.2 | Hunger, thirst | Built | |
| 17.3 | Wet clothing | **Built (Changed)** | `Agents/Needs.cs`: `Wet`, `WetChill`; a soaked coat keeps out a third. Fords and thin ice not checked |
| 17.5 | Wound treatment | Missing | |
| 17.6 | Winter strain | Missing | |
| 18.1 | Continuous world | Built | Ruins of Prince of Wales Fort (`World/Ruins.cs`) |
| 18.2 | Tiers | **Diverges** | Still two tiers (`Core/Tiers.cs`), rewritten for towns of 900 at a playable frame rate |
| 18.3 | Travel mode | Built | |
| 18.4 | Fire | Partial | Downwind spread. Palisades now keep beasts out. No prairie fire or firebreaks |
| 21.3 | Consensus skip | Partial | Quiet days need every player idle. No auto-pilot |
| 21.4 | Disconnect hand-off | **Diverges** | Still despawned (`Net.OnPeerDisconnected`) |

---

## 3. What changed since 21 October

| Area | Then | Now |
|---|---|---|
| Marriage | HBC rules for everyone | By the player's side (`Agents/Marriage.cs`) |
| Goal reading | Missing | Built (`UI/ConversationDepth.cs`) |
| Money and solvency | None | Company purses (`Economy/Companies.cs`); coin and store credit in the towns (`Economy/Money.cs`) |
| The union of 1821 | Only on the campaign's `merger` flag | Also when a company is exhausted, in any year |
| Fur price | None | Blockade, war, glut and yearly luck |
| Distance gradient | None | Goods dearer inland, the Nor'Westers' far more than the Bay's (`StandardOfTrade.cs`) |
| Goods | Some conjured at posts | Carried from the map's edge (`Outfits.cs`, `TradeTrips.cs`, `PostWants.cs`) |
| What a camp wants | By nation | From how it lives: use, adoption, taste, threat (`Economy/Wants.cs`) |
| Wet clothing | Missing | Built |
| Water | One kind | Shallow, deep, white water |
| Player afloat | Walked on the water | Sits and paddles |
| Old §5 claims | 12 open | 10 fixed; 2 fixed in part (§5) |

---

## 4. Where the code contradicts the design

Each of these still needs a decision: change the code, or rewrite the section.

1. **Fixed: marriage** (`Agents/Marriage.cs`).
2. **Personality (§11.4).** Courage and Talkative instead of sociability and discretion. Unchanged.
3. **Skills (§3.10).** 4 against about 12. Unchanged.
4. **Combat (§15.3).** A single health pool. Unchanged.
5. **Tiers (§18.2).** Two tiers, not three. Unchanged.
6. **Disconnection (§21.4).** Despawned, not run by the planner. Unchanged.
7. **The union of 1821 (§4.4, §13.7): now partly fixed.** Exhaustion triggers it. Three gaps remain:
   - Nobody has seen it happen. The starting purses (HBC 3,000 MB, NWC 5,000) and the costs are tuned by hand, and no long run has checked whether either company actually reaches two years of debt, or when. Tools T1/T7's `--assert merger-between 1819..1823` is the test for this, and it isn't built.
   - Under "no script" the blockade and war flags are never raised, so the fur price lacks the shocks that ruined the companies historically. Either company may then never exhaust.
   - The costs leave out violence and legal fees, which the doc names as the main drain of the trade war.
8. **New: day length (§14.3).** Latitude is computed but never set from where the player is, so the plains and the Columbia get York's days. This was listed before as Diverges. It is cheap to fix now that the map spans 42°N to the Arctic.

---

## 5. Claims the code does not back

The 21 October list:

| # | Claim | Status |
|---|---|---|
| 1 | London catches shaded books | Fixed (`LondonCommittee.Audit`) |
| 2 | Muskets use powder | Fixed (`Agent.UseCharge`) |
| 3 | Recruits from a list of 24 names | Fixed (name pools; `Companies.Hires`) |
| 4 | "A year in four to six hours" comment | Comment corrected |
| 5 | "Four traits" comment | Corrected |
| 6 | Skills cite §3.10 | Corrected |
| 7 | `FoundAPost` | Removed |
| 8 | Cree-named generic roles | Renamed, with aliases |
| 9 | Orders are messages, inert until read | **In part:** earshot only; an order does not travel to an outpost as a letter |
| 10 | The Reckoning weighs the player | Fixed (`Union.Weigh`); no end screen |
| 11 | Wind drives fire | Fixed |
| 12 | MB token worth exactly 1 MB | **In part:** priced right; nobody issues them |

New in this pass:

| # | The claim | Where | What the code does |
|---|---|---|---|
| 13 | "No money or solvency ledger" | Design doc §13.7 note, `Development Status.md` | Stale: both exist now. **Corrected in this pass** |
| 14 | "The HBC way only, for every player" | `Development Status.md` marriage row | Stale. **Corrected** |
| 15 | Marriage and the Company rows list built work as planned | `Roadmap.md` | **Corrected** |
| 16 | "Day length comes from latitude and the date" | `Core/GameCalendar.cs:322` | True of the formula, but the latitude is a constant 57°. The comment says "York Factory sits at 57 N", so it isn't false, but a reader would expect it to vary |

---

## 6. Built but not in the design doc

| System | Where | Note |
|---|---|---|
| **Laws** | `Core/Laws.cs` (989 lines), `UI/ConversationLaws.cs` | Rules a town meeting, a band council or the Committee makes and repeals. Motions are canvassed house to house and voted by whoever comes. A strong instance of §2 and of §3.8's councils. The doc should have a section on it |
| **Towns of the Lakes frontier** | `World/Towns.cs`, `Agents/TownLife.cs`, `World/Fields.cs`, `Economy/Money.cs` | Detroit, York, Niagara and the rest, 3,267 people; farms worked step by step; coin, wages, the store's book. Far outside §P.9's first milestone |
| **Life at York; the journey** | `Core/YorkHappenings.cs`, `World/JourneyIncidents.cs` | Pages of choices every few hours. Good §P.5 work. The Nor'Westers' raid on York, once, a day or two after a Company man lands, is scripted: the doc should say so, as for quests |
| **Musters** | `Economy/Musters.cs`, `Goals/MusterGoal.cs` | A campaign calls men to arms, marches them and attacks. This is a script moving people, in the same tension with §2 as quests |
| **Quests** | `Core/Quests.cs` | Still not in the doc (recommended on 21 October) |
| Observer mode | `Core/Observer.cs`, `UI/Observer*.cs` | "Watch the world": a camera with no body. A tool for checking §2 by eye |
| Lacrosse | `Economy/Lacrosse.cs` | Wagers, injuries, and a feud cooled |
| Dyed garments | `Agents/Garb.cs` | Part of `longer_goals` #3 |
| Loading screen | `UI/LoadingScreen.cs`, `LoadingArt.cs` | |

---

## 7. Still not built

The 21 October catalogue, less what has since landed. Sources: "Doc" = the design doc; "Content" = `world-content-needed.md`; "Trade" / "Plains" / "Tools" = the specs.

**Economy and the endgame.** A market where furs can be held back or dumped as a weapon (Doc 13.6). Credit, crisis moves (austerity, calling in debts, a bailout) and violence and legal costs in the purse (Doc 13.7). A shock calendar that runs without a campaign (Trade §3.5). The Trade Ledger page and `--dump-economy` (Trade P0, Tools T2). A finite recruit pool (Doc 13.2). Debt as a weapon (Doc 13.3). Surprise audits by a superior (Doc 3.9). Founding a post (Doc 13.8). The end screen and the Simpson epilogue (Doc 1, 4.3).

**Companies and the metropole.** Fort William's proxies, splinters and arrival windows (Doc 3.8.1). The NWC's two-years-in-three rule and earned shares (Doc 5.2). Montreal agents; named London Committee members (Doc 10.7). The Southern Department (Doc 6.2). Astor and St. Louis as nodes (Doc 7.3). The XY Company and an 1801 start (Doc 4.2). The Red River Colony as people (Content §2-3).

**Agreements and information.** Accords (Doc 3.8.2). Forged dispatches, express couriers and blowback (Doc 10.3). Literacy, and interpreters who lie (Doc 10.6). Maps as items (Doc 10.8). Faction trust rolling up from sightings (Doc 10.5). Authorised sightings (Doc 11.5). Memories passing to kin (Doc 10.9).

**People.** The Métis/freeman road in (Doc 5.3). Personality sliders and skill points (Doc 5.4). Faces and bodies (`longer_goals` #3). Slavery (Doc 9.2). Death branches (Doc 9.3). Decline and death from age (Doc 9.4). Perks (Doc 12.7). The missing skills (Doc 3.10). Sociability as a trait (Doc 11.4). Tradesmen and boat ranks among the trade's roles (Content §7). Adoption into a band (Plains #2).

**The body.** Winter strain and mutiny (Doc 17.6). Wound treatment (Doc 17.5). Hit location (Doc 15.3). Surrender and bluff (Doc 15.5). Epidemics (Doc 13.5). Addiction and diluted liquor (Doc 13.4). The famine-food ladder (Doc 13.1).

**Making things.** Player crafting and smithing; the player running the pemmican chain; player farming (Doc 12.6; Plains #5; `longer_goals` #5).

**Transport and the land.** York boats, sleds and carts as owned transport (Doc 12). Player canoe condition (Doc 12.2). Freezing per waterway (Doc 14.1). Day length by latitude (Doc 14.3). A third simulation tier (Doc 18.2). Prairie fire and firebreaks (Doc 18.4). More species (Doc 16.2). Herd fragmentation (Doc 16.5).

**Multiplayer and tools.** Planner hand-off on disconnect; auto-pilot (Doc 21.3-21.4). Tools T1, T2, T4-T8. `check_wiki.py --code-claims`.

---

## 8. Specs status

| Spec | Status |
|---|---|
| `global-trade.md` | **Started.** P1 (distance gradient) built. P2 in part: a cash ledger, sales on arrival and a price rule, but no `MarketCore`, CLI sim or acceptance runs. P0 (`--dump-economy`, the Trade Ledger) not built; `--plainslog` prints the purse lines |
| `story-plains-years.md` | Not re-checked in this pass; unchanged since 21 October as far as the commit log shows |
| `tools.md` | T0 built; T3 partly done; the rest not started |

---

## 9. Documentation hygiene

- The design doc and the wiki have no pages for laws, the towns, money in the towns, the observer, life at York or musters. Each should be short and linked from `Systems` or `Economy`.
- The wiki pages listed missing on 21 October (horses, war parties and truces, Missouri trade, the union, market news) are still missing.
- `Development Status.md` and `Roadmap.md` drifted again within eight days, the same pattern the last audit found. That is why `--code-claims` is worth building.

---

## 10. Recommendations, in order

1. **Prove the exhaustion merger.** Run a headless soak 1811-1821 under "historical" and "no script", log the purse (`--plainslog`), and tune the starting purses and costs until a company exhausts around 1819-1821 historically and plausibly without a script. This is the cheapest step from here to an emergent 1821, and global-trade P0's `--dump-economy` is the natural tool for it.
2. **Run the §P.9 test on York.** Play one in-game year on the York river with quiet days on, time it against §P.3's target, and note whether it passes §P.1's story test. The recent York work (raid, happenings, journey incidents) is aimed at exactly this, and nothing has measured it.
3. **Write design-doc sections for laws, towns and scripted set pieces** (quests, musters, the York raid), saying where each sits relative to §2.
4. **Decide the five standing divergences** (§4: traits, skills, combat, tiers, disconnection) and rewrite the doc sections either way.
5. **Set latitude from position** (§4.8). Small and visible.
6. **Add `check_wiki.py --code-claims`**, or re-run this audit after every large merge.
