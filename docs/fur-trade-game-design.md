# Fur Trade Game — Design & Mechanics Reference

*Working design document. Compiled from planning discussion — intended as a shared reference for continued design and implementation work.*

*Revision note: this version folds in the follow-up planning discussion — population scale and simulation tiers (Sections 11.1, 18.2), the spawn-site ecology system (Section 16.4), the map boundary and the Southern Department / James Bay region (Section 6), pre-1821 HBC and NWC structure (Section 5), the message system rebuilt around read events (Section 10.1), delegation and task assignment (Section 3.9), and the Fort William council (Section 3.8.1). A change list is at the end.*

---

## 1. Core Concept

An isometric, open-world RPG following **one protagonist** across the fur trade wars of the early 19th century. The player can start on one of three paths and can defect or move between them over the course of the game. The tone is **dark**, with real, permanent stakes — combat has real death, for the player and for NPCs and factions alike. The world is meant to feel like an actual **simulation**, not a themed backdrop: factions are made of individual characters who move, remember, believe, and act; information has to physically travel; resources are finite and can run out.

The player is mechanically an equal agent among NPCs (Section 11.1) rather than a superpowered protagonist — same needs, same goal structure, same action vocabulary (Section 3). A small number of distinct player-specific abilities are expected on top of that baseline; the first is defined in Section 3.5.

The single-protagonist framing above is the base case. Because the player is architecturally just another agent, the same model extends to 4–6 concurrent human players sharing one world without a separate multiplayer simulation — see Section 21.

The game's arc runs toward a single historical inevitability: the **1821 forced merger** of the Hudson's Bay Company and the North West Company, and the rise of **George Simpson** as the man who reshapes the surviving company. Whatever path the player has taken, the ending is the moment their world gets absorbed into — or transformed by — the new order.

---

## 2. Design Principle — Simulate, Don't Script

This is the rule every mechanic in this document gets checked against: behavior should come from real agents pursuing real goals under real constraints, never from a scripted trigger faking the same outcome. It shows up differently in each system, but it's one principle:

- **Knowledge** (Section 10): no actor — player or NPC — has ambient knowledge of anything. Everything has to be witnessed directly or told by someone.
- **Movement and encounters** (Section 15): a "roaming encounter" is never a spawn table. It's two independently-moving parties, each actually pursuing its own goal (a hunting party is actually hunting, a war party is actually raiding), whose paths happen to cross.
- **Population** (Section 11): there is no cast of unnamed extras standing in for "the world." Every person who exists in the simulated world is a full, named agent — on the order of 10–20k across the whole map (Section 11.1). That is affordable because existing as data is cheap and update frequency is tiered by distance and relevance (Section 18.2), not because some characters get a cheaper fake version.
- **NPC fates** (Section 10, Section 11): an NPC's death, marriage, or a battle's outcome is a real event the moment it happens, but nothing in the world — including the player — knows about it until it physically reaches them.
- **Goals and objectives** (Section 3): what any agent wants and does about it comes from the same goal/action structure, not per-character scripting.
- **History itself** (Section 4.4): even the game's real-world historical timeline follows this rule, with a small, explicitly-marked deterministic core rather than a fully scripted plot.

The test for any future mechanic: does this rule *produce* the behavior, or does it *fake* it? A system that can only be described as "when X happens, show Y" needs to be rebuilt as an actual interacting rule before it belongs here.

---

## 3. Goal & Action Framework

This is the concrete mechanism that makes Section 2 actually executable rather than just a stated intention. Every agent in the world — the player included — runs on the same structure: goals, decomposed into steps, bottoming out in a shared set of actions.

### 3.1 Three Tiers of Goal
- **Tier 0 — Subsistence** (recurring, universal, preempts everything else): hunger, thirst, exposure (Section 17). A starving or freezing agent interrupts whatever else it's doing to address that before anything higher-order gets attention.
- **Tier 1 — Personal/Ambition** (individual, longer-horizon, not universal): a young man wanting a wife, a chief factor wanting the best returns, a family wanting to climb a hierarchy. These decompose into concrete step-sequences: a marriage goal pulls in relationship-building, gift accumulation, and a formal proposal (Section 8); a returns goal pulls in favorable trades, credit extended to trappers, protecting brigades; a rank-climbing goal can span a family across generations, meshing directly with succession-on-death (Section 9.3.3) — the goal can outlive the character pursuing it.
- **Tier 2 — Faction/Directive** (top-down, issued by leaders): a company's annual objectives, a chief's call to raid. These need no new distribution mechanism — they're the payload of the message system already built (Section 10.1). Reading orders off the annual ship hands a Factor a new active goal, which he then has to decompose into his own steps, possibly by directing subordinates toward pieces of it (Section 3.9). Nothing updates until the message is actually read (Section 10.1.1), so a Tier 2 goal stays locked between read events. The *generator* of the payload differs by faction: HBC's arrives as a one-way order authored in London, while NWC's is the output of a council negotiation among the wintering partners (Section 3.8, Section 10.1.3).

### 3.2 Decomposition Reuses Chains
Goals don't each get bespoke logic. "Acquire trade goods," "travel to X and meet someone," "build standing with a family" are common step-sequences that a chief factor's profit goal, a suitor's bride-price goal, and a family's rank-climbing goal can all slot into. This is the actual payoff of the Unified Agent Model (Section 11.1): you don't hand-write behavior per agent, you build a library of reusable chains that any agent's goal can pull from.

### 3.3 One Action Vocabulary for Everyone
Every plan, at any tier, bottoms out in the same handful of actions the player has: talk, trade, give, open, pick up, attack, use item, enter vehicle. An NPC's plan is never a special AI-only action space — it's built from the identical vocabulary handed to the player, which means the player can always step into the middle of someone else's plan using a verb they already know (interrupt a trade with an attack, help a stranded party with a give). This is what makes combat's sourcing (Section 15.1) honest: a fight isn't a special combat trigger, it's what happens when two agents' active plans both call for attack against each other.

### 3.4 Goals Are Patient
Nothing here resolves in one tick. A factor's profit goal plays out across a trading season, a courtship across months — matching the annual/seasonal pacing already built into messages and hunts. A goal is an ongoing process competing for the agent's attention against Tier 0 interruptions, not a queue that resolves immediately.

### 3.5 Goal Visibility
Goals are not shown to the player as UI — they're inferred the same way everything else in this world has to be learned (Section 2, Section 10): from dialogue responses, visible behavior shaped by personality (Section 11.4), and context (a young unmarried man at a rendezvous is probably goal-seeking a wife; a factor whose post just took losses is probably chasing returns). This keeps the social/political layer genuinely hard-won rather than a readable menu.

A dedicated skill unlocks direct goal-reading later in the game — the first of the small set of player-specific abilities noted in Section 1. It should be earned and limited, not an omniscience toggle: gated behind sustained observation of or interaction with a specific person rather than passive range-reading, and tiered in what it reveals — a vague sense of priority at low skill, the specific active goal at higher skill, visibility into its actual steps only at mastery. This preserves the stakes of not knowing for most of the game, and makes the eventual payoff feel like earned insight rather than a stat unlock.

### 3.6 What This Makes Existing Ideas Actually Be
- **Pemmican-making** is a reusable chain (butcher → dry → render fat → pound → pack), not a bolted-on minigame — it slots into a hunt camp's subsistence goal, a factor's provisioning goal, or a family's trade-goods goal alike. When the player runs it directly, the challenge is the same skill/timing decisions any agent running that chain faces (drying against the weather, fat ratio versus shelf life), not a separate abstracted screen.
- **Fur trading** is a negotiation action between two agents' goals meeting — price and outcome depend on individual rapport, faction standing, seasonal pelt quality (Section 14.4), and what the other party currently needs (a trader who's already hit his quota drives a harder bargain than one desperate to fill it before the ship leaves). Reading the other agent's active goal — or inferring it, per Section 3.5 — is what good bargaining means here.
- **Dialogue** is generated from an NPC's actual memory, active goals, and personality (Sections 10-11), not hand-authored trees — necessary once there's no generic tier (Section 11.1), since bespoke conversations can't be hand-written for every agent in a fully-simulated population. "Talk" opens a topic menu built from what that specific agent knows, wants, and feels about the player right now — ask-about, offer, persuade, threaten, propose-marriage, share-a-rumor, trade — the same verb set surfaced through a conversation interface instead of the world.
- **Reputation** has two layers: fast-moving **individual rapport** (built through direct interaction — helping with someone's goal, honesty, shared hardship) and slower **faction standing** (Section 10.5/11.6's rollup). What "standing" measures differs structurally per faction, which is what makes them play differently without needing separate systems:
  - **HBC** — bureaucratic rank/approval, reviewed on the annual ship cycle. The Southern Department (Section 6.2) is proposed to be reviewed through Moose Factory rather than the Northern Department's chain of command.
  - **NWC** — peer politics settled socially, fastest-moving at the rendezvous. A partner's pull with the wintering partners depends on actually doing the work of wintering: the two-years-out-of-three rule (Section 5.2) means a partner who spends too long in Montreal visibly loses standing.
  - **Indigenous nations** — kinship and reciprocity, close to individual rapport because the communities are the small, real ones the population model (Section 11.1) already commits to.
  - **Métis** — similarly family/brigade-centered rather than institutional.
  - **Americans** — barely an alliance concept at all, mostly a rivalry/threat-level meter.

### 3.7 Career Competition
Promotion (Section 5.1's HBC salary-and-rank ladder, or Section 5.2's NWC path from clerk toward a partnership share) is a Tier 1 ambition goal (Section 3.1) like any other, which means rivals for the same seat are real named colleagues pursuing the identical goal, not a scripted competitor. Two clerks angling for the same advancement are two agents each running their own returns/relationship-building chains (Section 3.2) against the same superior's attention — whichever actually delivers, or maneuvers the other out of favor via the information system (Section 10), gets the seat. This turns career advancement into a legible political contest instead of a threshold on a stat bar.

### 3.8 Council & Multi-Party Negotiation
Dialogue (Section 3.6) is built for one-on-one exchanges; formal moments — treaty talks, a rendezvous partners' meeting (Section 5.2), a council deciding whether to go to war (Section 7.2) — need the same underlying system extended to a room with several agents, each running their own active goal and personality filter (Section 11.4) simultaneously. The player is one voice among several, not the only one steering the outcome — a council can resolve in a direction the player argued against if the other participants' goals and standing outweigh their input. This is the natural home for the biggest diplomatic/political set-pieces in the game without inventing a second dialogue system. It is also the generator for NWC's yearly directive: each July at Fort William the wintering partners and the Montreal Agents run exactly this kind of council, and its outcome becomes the company's Tier 2 goal (Section 10.1.3). Mechanics for that case are in Section 3.8.1.

#### 3.8.1 The Fort William Council (NWC's Tier 2 Generator)
The largest instance of the council mechanic, and the generator behind NWC's yearly directive (Section 10.1.3). It is an event resolved by the same agents and rules as any other conversation, not a scripted cutscene.

**Who's in the room.** The wintering partners and the Montreal Agents — a few dozen deciding agents (planning estimate; the 1787 company had 23 partners, and the count grew as shares were subdivided) — each arriving with their district's returns, their field intelligence (what they personally witnessed or were told, weighted by directness per Section 10.4), their own Tier 1 goals, and their standing (Section 3.6). Clerks and voyageurs are at Fort William in large numbers, but as local-tier background (Section 18.2), not as voters.

**Arrival windows are geographic.** Each partner's presence window is set by his district's distance and the season: partners from distant districts arrive late and have to leave early to beat freeze-up. Whoever is present longest sees the most of the council, and decisions taken before a late partner arrives, or after he leaves, reach him secondhand (Section 10.4). Agenda timing is therefore a real form of power.

**The agenda comes from the world, not a script.** Items are whatever attendees are actually carrying: a rival post burned, a pemmican shortfall, a colony proclamation, a district that wants to expand, who winters where next year, who earns a share, how the outfit is split among districts. An item exists only if someone in the room knows about it (Section 10), and any attendee can table one. Items are taken in order, so late items get squeezed as people depart.

**Deliberation.** Each agent argues from his active goal and personality filter (Section 11.4) using the ordinary verbs — persuade, offer, threaten, call in a favor, produce a witness. What he votes comes out of his goals (what the item does for his district, his standing, his grudges), the favors he is owed and owes, and how the arguments landed. The player is one voice among several (Section 3.8).

**Vote weight follows shares, not heads.** The NWC was a share partnership. The 1787 and 1790 agreements divided it into twenty shares; the partnership was reorganized in 1795 and 1802, subdividing shares to admit more wintering partners; and the 1804 amalgamation with the XY Company produced a hundred shares, seventy-five to the old NWC partners and twenty-five to the former XY partners. Wintering partners held one or two shares each, while the Montreal agent firm held a large block (six of twenty in the 1790 agreement), cast by the representatives who travelled up to the meeting. So an agent firm's pull comes from its block plus the credit it extends, and a wintering partner's from his one or two shares. Standing (Section 3.6) doesn't add votes; it shifts who listens — how easily an agent brings other people's shares along with his own. Which year's share table applies depends on the game date (Section 20).

*Working reading, not confirmed*: the sources consulted establish shares as the basis of ownership, profit, and the agents' dominance, but none spelled out the voting rule itself. Weight-by-shares is the best-supported reading; Wallace's *Documents Relating to the North West Company* (Champlain Society), which prints the 1802 and 1804 agreements and the 1801–1807 meeting minutes, is the place to check.

**The outfit split is the biggest zero-sum item** — how many goods and men each district gets. Every partner wants his district funded and there isn't enough, which is what makes the council political rather than administrative.

**Outputs:**
- A **directive** for the coming year — per-district objectives, the outfit allocation, and posting assignments (including who is promoted toward a share) — which becomes each partner's Tier 2 goal once he has left the room aware of it.
- **No single shared truth.** Every attendee walks out with his own memory of what was decided, at direct-witness weight. The written minutes are a physical message like any other (Section 10.1.1): they can be carried, copied, lost, or intercepted, and intercepting a partner heading inland hands a rival the company's plan (Section 10.3).

**Dissent.** A partner who loses a vote can comply, comply nominally, or defy; his exposure is to peers at next July's rendezvous, not to a distant superior (Section 10.1.4).

**Proxies (decided).** A partner who cannot attend can send a letter or a proxy — both are messages with read events. Under share-weighted votes a proxy carries the absentee's shares, so the proxy holder commands a bigger block than his own. He is an agent with his own goals and may not vote the way the absentee wanted, so trust is the risk.

**Splinters (decided).** A bloc that loses badly enough can leave and set up a rival company. The XY Company is the template. It grew out of conflict between the Montreal agents under McTavish and the wintering partners after the 1795 reorganization, was formally founded in 1798 by two Montreal firms, later drew in Alexander Mackenzie, competed head-on with posts close to the NWC's (one NWC post master was shot in a quarrel), and folded back into the NWC in 1804, after McTavish's death, on 75/25 share terms with Mackenzie left out. Mechanically a splinter needs three things: a grievance shared by a bloc large enough to matter (typically over share distribution or agent dominance); an outside Montreal financier willing to supply goods on credit, which the agents' credit hold otherwise prevents; and an experienced leader. A splinter is then a live rival faction with its own posts and message network until a shock — a leader's death, a bad year — pushes it toward a merger on negotiated shares.

**How non-partners get in.** A clerk usually watches from the edges, but has real routes to influence: giving testimony that shapes a vote, carrying messages between partners between sessions, and lobbying. Earning a share is the path to a seat (Section 5.2).

**If the player isn't there**, the council resolves lazily with the same agents (Section 18.2), and the result reaches the player through the message system — later, and possibly garbled.

### 3.8.2 Council Outcomes — What an Accord Actually Is
A council (Section 3.8) or a smaller negotiation between kin or bands doesn't only produce a Tier 2 directive (Section 3.8.1's NWC case) — when the parties belong to different factions or kin groups, its output can be a durable **Accord**: one generic agreement object, specialized by its terms, rather than a separate mechanic per treaty type.

**Every Accord has:**
- **Parties** — two or more factions, bands, or specific individuals.
- **Terms** — what's actually promised, reusing the goal/action framework (Section 3) rather than inventing new state. A term is a constraint layered onto a party's future goals (a promise not to raid, an obligation to trade preferentially, a commitment to grant passage) or a one-time transfer (a captive returned, a gift given, a marriage arranged, per Section 8).
- **A breach condition** — what counts as violating it, always a witnessed event (Section 10.2) that propagates like anything else (Section 10.4). A broken Accord is one of the worst things an agent can be caught doing — Accords are the actual currency of trust in a world with no central law (Section 7.4), so violating one carries consequences on the scale of Section 7.4's vendetta mechanics, not an ordinary standing hit.

**Named types are parameterizations, not separate systems:**
- **Peace treaty** — caps or resets active hostility between two factions (Section 7.2, Section 4.1's interior war); may include terms like returned captives (Section 9.2) or an agreed territorial line.
- **Trade exclusivity** — a band or nation commits to trading primarily or only with one company (Section 13), typically bought with better prices or gifts; a rival can still poach it by offering better terms or by damaging trust in the exclusive partner (Section 10.3/10.4).
- **Safe passage** — a nation grants passage through its territory without the default ambush risk (Sections 12.2, 15.1); breaking it is a huge propagation event precisely because it suppressed a risk everyone knew was otherwise real.
- **Alliance / mutual defense** — two factions commit to fighting together against a third, extending the personal kin-obligation-to-fight idea (Section 15.2) to an institutional scale; often sealed with a marriage (Section 8), itself just another term on the same Accord.
- **Debt restructuring** — forgiving or renegotiating a debt (Section 13.3) as a negotiated term rather than a unilateral company decision.
- **Vendetta settlement** — a kin-to-kin negotiation, not necessarily a full council, that formally closes an unresolved blood debt (Section 7.4) with restitution terms, retiring the grudge (Section 10.4) instead of leaving it to decay or escalate on its own.
- **The 1821 merger** (Section 13.7) is the largest instance of this same object — two councils negotiating one Accord whose terms happen to be a full corporate merger.

### 3.9 Delegation & Task Assignment
The layer between a Tier 2 directive (Section 3.1) and the shared action vocabulary (Section 3.3): how a leader who has read his orders turns them into work other agents actually do. It is written generically — a leader handing goals to followers — with the HBC Factor as the primary case and NWC partners and hunt captains as other parameterizations (table below), per Section 19.

**What the leader has in hand.** The directive, once its read event fires (Section 10.1.2): objectives with quantities, deadlines, and constraints — a returns quota by ship date, credit or expense limits, expansion or closure orders, a rival to watch. It was written off last year's books, so it is already stale. Alongside it: the outfit that arrived with it, his staff roster (skills, loyalty, health, location), his stock ledger, and his *beliefs* about each outpost and known hunting site (Section 16.4). Those are beliefs, not ground truth — he only knows what reports and his own eyes have told him (Section 10).

**The loop:**
1. **Deliberate on a schedule, not continuously.** Planning fires at a read event, at season boundaries, and when a trigger hits (step 7). A seasonal playbook breaks each objective into tasks built from the reusable chains in Section 3.2 — fall outfit distribution, winter trade and provisioning hunts, spring breakup and the goose hunt, summer freight to the depot before the ship leaves. Each task is {chain, place, deadline, resources, skill needed, risk}.
2. **Allocate the outfit.** Goods, credit, men, and boats are finite, so he splits them across outposts by expected return and by which bands trade where. This is where subordinates start competing (Section 3.7), and where his wrong beliefs cost him.
3. **Match tasks to people.** He scores on capability, availability, and trust, and personality does the rest. A suspicious factor gives key tasks only to men he trusts and checks up more often; an ambitious one might send a rival to the worst posting, or keep him close where he can be watched. Someone who knows a lodge site is worth more, because knowledge is currency (Sections 10, 16.4).
4. **Issue orders at the level of intent** — "winter at X, bring back N packs by breakup" — not step by step. An order is just a message (Section 10.1.1): instant within the post, days to an outpost, and inert until read. Until then the subordinate acts on his last order.
5. **The subordinate decides.** The assignment competes with his own Tier 0 and Tier 1 goals. A clerk courting a wife may lobby for a posting near her band, stall, or ask for a different job; an ambitious one may take a risky task for the credit. Compliance comes out of loyalty, ambition, fear, and pay, not a flag. He then decomposes his own piece with the same loop one level down. Nesting depth is bounded by the org chart (factor → clerk or outpost head → men), not by how long a chain is.
6. **Reports arrive through the propagation system, not telepathy.** The leader's picture of progress is the last report plus elapsed time. A late report is itself information, and it feeds the same rollup as unexplained sightings (Sections 10.4, 10.5).
7. **Replan locally, not wholesale.** Triggers are a shortfall against a deadline, Tier 0 (a post short of food preempts everything and pulls men off trade), an event (a burned outpost, a rival, a death, a storm), or a defection. Only the affected tasks rerun steps 1–5. The directive itself does not change between read events (Section 10.1.2) — replanning changes tasks, not the goal.
8. **The year closes into the next order.** Returns and accounts go on the ship, and London reads them a year later. The factor writes his own books, so he can shade them, and the lie resolves at that read event. This year's output becomes next year's directive input, and his standing (Section 3.6) is reviewed there.

**Audits are a real, triggerable check, not only a year-end reveal.** A suspicious superior (Section 11.4) can order a surprise inspection of a subordinate's kit or a post's ledger at any time, rather than waiting for the books to be read in London a year later (step 8) — a concrete action matching the suspicious-factor behavior already described in Section 3.6. A ledger or kit that doesn't balance exposes skimming (above) or a hidden cache (Section 12.5) directly, with consequences scaled to what's found: wage forfeiture, demotion, or worse.

**One system, different parameters** (Section 19):

| | Mandate comes from | Obedience rests on | Reports |
|---|---|---|---|
| HBC Factor | London directive, reviewed each ship | Salary, rank, London's approval | Journals and accounts; debrief on return |
| NWC partner | July council (Section 3.8.1) plus his own share | Profit share, peer standing, clerks hoping for a share | Debrief at the rendezvous |
| Métis hunt captain | The hunt itself | Kinship and hunt discipline (Section 16.1) | Immediate, in the field |

**Cost and legibility.** The loop is event-driven, so it is cheap. A post in the live bubble runs its deliberation on a visible daily beat — the morning assignments — which gives the player something to watch. Background posts run the whole loop in the weekly batch (Section 18.2). As a clerk, the player gets assignments as dialogue with intent and a deadline but not the leader's reasoning, so priorities have to be inferred (Section 3.5); he can accept, counter, decline at a standing cost, or volunteer. As a factor later, he runs the same loop with the same verbs and no special UI.

**Fulfilling an order is a choice of method, not a single path.** A returns quota can be filled by trapping it out personally, trading fairly with a band, buying from a desperate freeman at a markup he'll remember (Section 3.6), raiding a rival's cache (Section 15.1), or skimming a portion for private trade on the side — the same "shade the books" opportunity already available to a Factor at year's end (step 8 above), now available to anyone filling an order beneath him. Each method trades differently against relationships (Section 10), standing (Section 3.6/3.7), and risk of discovery (Section 10.4's propagation), which is what makes an ordinary quota a real decision instead of a formality — for a delegated NPC and for the player alike.

**Example.** An inland post reads its orders late in the fall: 40 packs by ship. The factor has 12 men and two clerks. He sends the clerk he trusts, with six men, to a far outpost he has heard has lodges, and keeps the ambitious clerk close on a provisioning hunt. In January the outpost report is overdue, and he can't tell whether the clerk is dead, snowed in, or sitting on a site. He pulls two men off provisioning to check, and the post's food margin thins. Nothing scripted that crisis.

### 3.10 Skills
Every agent — player and NPC alike — carries a skill value per action-relevant competency: shooting, hunting, trapping, berry-picking/foraging, talking, stealth, horse riding, sprinting, medicine (Section 17.5), strength (Section 12.4), crafting and smithing (Section 12.6), and others as new action chains need them. Skills determine how well an action in Section 3.3's shared vocabulary resolves — a shooting check, a stealth roll, how much a foraging trip yields — rather than gating whether the action can be attempted at all.

- **Base value from personality.** Each character's starting skill baseline is seeded by their personality profile (Section 11.4 — loyalty, discretion, suspicion, ambition, sociability) and background, not rolled independently — an ambitious clerk starts with a talking edge, a hunter raised in a band starts high on hunting/trapping/stealth, a voyageur starts high on sprinting and canoeing endurance. Personality does double duty: it already filters behavior (Section 11.4), and now it also explains *why* two agents in the same role differ in raw competence.
- **Improves through use**, not a separate XP pool — repeatedly running a chain (Section 3.2) that depends on a skill nudges that skill up, so a clerk who spends a season actually hunting gets measurably better at it, and one who never leaves the trading room doesn't, with no experience-point abstraction sitting on top of the thing already being simulated.
- **Already has obvious hooks into what's built**: shooting and stealth feed combat (Section 15.3) and ambush/interception (Section 15.1); hunting, trapping, and foraging feed the ecology and food systems (Section 16, Section 17.1); talking feeds dialogue, negotiation, and the council mechanic (Sections 3.6, 3.8); horse riding feeds Section 12.3; sprinting affects travel speed and pursuit/de-escalation outcomes (Section 15.2); medicine feeds wound treatment (Section 17.5); strength sets personal carry capacity (Section 12.4).

---

## 4. Setting & Timeline

### 4.1 Core Arc — 1811 to 1821 (current lean)

This window is not a static backdrop — it's the real, escalating history of the Pemmican War, condensed:

- **1811** — Lord Selkirk's land grant establishes the Red River Colony (Assiniboia). Fort William is the NWC's undisputed inland capital.
- **1812–14** — War of 1812 in the background; Astor's Pacific Fur Company post at Fort Astoria (est. 1811) is sold/ceded to the British and becomes Fort George in 1813.
- **1814** — Governor Miles Macdonell issues the **Pemmican Proclamation**, banning pemmican export from the colony — an existential threat to NWC brigades and their Métis suppliers. This is the inciting incident.
- **1815** — First attack on and evacuation of the Red River Colony.
- **June 1816** — **Battle of Seven Oaks**: Cuthbert Grant's Métis riders kill Governor Robert Semple and ~20 colonists. First major dark set-piece.
- **1817** — Selkirk personally marches mercenary soldiers on Fort William itself and arrests the NWC partners there.
- **1818–1820** — Open economic and physical warfare between the companies: rival posts burned, brigades intercepted, violence in the interior far from any court's reach. This is the messy, lawless middle game.
- **1821** — Parliament forces the merger of HBC and NWC. **George Simpson** is installed as Governor of the Northern Department and begins ruthlessly restructuring the combined company — closing posts, laying off large numbers of clerks and voyageurs (many Métis), imposing a cold, efficiency-first regime. Ending point. (The Southern Department around James Bay stayed under a separate governor, William Williams — see Sections 4.3 and 6.2.)

### 4.2 Optional Prologue Era — 1801–1811

Still under consideration as an earlier start. Would add:
- The tail end of the **XY Company** rivalry (a splinter from the NWC's internal conflict, founded in 1798 by two Montreal firms and later identified with Alexander Mackenzie; merged back into the NWC in 1804). Because Section 3.8.1 makes the splinter a general mechanic, an 1801 start can seed the XY split as an initial condition rather than scripting it.
- Background news of Lewis & Clark's expedition (1804–06)
- Astor assembling the Pacific Fur Company (founded 1810)
- Room for the player to build reputation and relationships *before* the Pemmican War kicks the main plot into gear, so alliances feel earned rather than handed out at character creation
- The HBC/NWC structural work (Sections 5, 10.1) targets the full ~1801–1821 window. In an 1801 start the NWC meets at Grand Portage (the July 1802 partnership agreement was signed there), holds its first annual meeting at the new Kaministiquia post in 1803 (renamed Fort William in 1807), and has the XY rivalry live until the 1804 amalgamation. See Section 20.
- An 1801 start also makes the Red River Colony's founding a *live* subplot rather than pre-game backstory — Selkirk's 1808–1811 London campaign (Section 10.7) would actually run during play. See Section 4.4 for how that interacts with treating the founding as a fixed outcome.

### 4.3 Epilogue Beat (post-1821, optional)

Simpson's own biography is a near-perfect thematic echo of the marriage/kinship system (see Section 8) and could work as a closing scroll or epilogue scene: Simpson had at least two documented country wives (Elizabeth Sinclair, 1821–22; Margaret Taylor, 1825–30) before traveling to London in 1830 to marry his English cousin, Frances Ramsay Simpson — a choice that meant setting aside his Indigenous family, and one that helped popularize a new company-wide fashion for officers taking European wives over country ones. This happens nine years after the proposed 1821 ending, but works well as a "this is the world your choices are about to live in" coda.

**Southern Department coda.** At the 1821 union, William Williams — the senior governor, expected to take the more profitable Northern Department — instead chose the Southern Department (headquartered at Moose Factory), which left Simpson holding the Northern Department this game mostly covers. The two departments didn't come under a single governor until 1826. As an epilogue beat this is useful: a real piece of the map that Simpson's restructuring doesn't reach yet, still running under the older regime, is a visible reminder that 1821 was not one moment happening everywhere at once, and it fits the "world runs on stale information about itself" idea (Section 10.5). Simpson and Williams are epilogue material only; the playable structure is the pre-1821 one (Section 5).

### 4.4 Historical Determinism — What's Fixed vs. Emergent

Per Section 2, the timeline above isn't a script the world plays out regardless of what agents do. Most of it is a strongly-seeded pressure, not a checkpoint — but a few things are fixed, because they sit outside any single company's or player's reach, or because they're the precondition the whole arc needs to exist:

- **The Continental Blockade** (Section 13.6) is a fixed external condition, not an event any agent causes or prevents — Europe's war disrupting the market this world's furs sell into, on its own historical calendar regardless of what happens in Rupert's Land.
- **The Red River Colony's founding** (Section 4.1's 1811 entry) is fixed as the game's starting condition in the core 1811 start. Section 10.7 traces the real London politics behind it — Selkirk buying HBC shares from 1808, his brother-in-law Wedderburn's committee maneuvering, the grant obliging him to supply 200 men a year — as Selkirk's own Tier 1 goal (Section 3.1) running through shares, a seat, and recruits, not a cutscene. Under the optional 1801 start (Section 4.2), that causal chain would actually play out during the simulated timeframe rather than as pre-game backstory; it should still be tuned to reliably land on the same outcome, since everything from 1811 onward depends on the colony existing. The colony's *survival* after founding is a completely different matter — genuinely open, per Section 4.1's own 1815/1816 entries.
- **The 1821 merger** follows the same pattern as the colony's founding: structurally inevitable, causally emergent. Section 13.7's solvency ledger is a real, simulate-able mechanic, not a hidden coin-flip — tuned so that under the expected range of play, both companies' exhaustion crosses the merger threshold close to 1821, matching Section 1's framing of this as "a single historical inevitability." A player could in principle shift the date, or change whose terms dominate the negotiation, by radically altering the pressures (crushing the trade war early, keeping both companies flush) — but doing so should be genuinely hard, the same way 1821 felt inevitable in reality without being physically guaranteed.

One conditional rule sits between fixed and emergent: **if the Red River Colony is destroyed and its governor killed, Selkirk personally travels from Montreal with mercenary troops in response** (Section 4.1's 1817 entry). This isn't scheduled to 1817 — it's a triggered consequence of the earlier event actually occurring, whenever and however it does, and it simply doesn't fire if the colony survives or its governor doesn't die.

Everything else in Section 4.1 — the Pemmican Proclamation, Seven Oaks, the 1818–1820 interior war — is a real possible outcome of the pressures the world is seeded with, likely to land close to its historical shape because the underlying conditions (arming factions, a starving colony, two exhausting companies) are real, but none of it is hard-locked to a date or guaranteed to happen in that exact form. The background American beats (War of 1812, Fort Astoria/Fort George, Lewis & Clark) carry none of the weight of the anchors above — seeded color and pressure on the American nuisance faction (Section 7.3, Section 10.7), nothing more.

---

## 5. Starting Paths

Each path should have a genuinely different early-game problem to solve, not just a different sprite/flag.

### 5.1 HBC — York Factory
- Rich but slow: centralized supply model, one ship a year from London.
- Geographically stuck on the Bay lowlands; dependent on Cree and Assiniboine middlemen unless the player pushes inland (breaking HBC's traditional "stay at the Bay" doctrine).
- **Pre-1821 structure**: London sets policy through a Governor, Deputy Governor, and a five-director Committee elected annually by shareholders, working from the annual reports, journals, and accounts the ship brings back. Each factory is run by its own chief factor and a council of officers, and the field has no strong, continuously-coordinating authority above them — a Factor is substantially on his own between ships (Section 10.1.2). The "Governor" title in these final years (William Williams) means something closer to senior officer during a crisis than the single unifying command Simpson builds afterward.
- **The ladder is pay and standing, not ownership.** Officers climb for salary, rank, and London's approval. The fixed Chief Factor / Chief Trader ranks with profit shares are a specific 1821 creation that grafted NWC's ownership-share model onto HBC's salaried hierarchy, so they belong to the epilogue, not the playable window. Keeping this distinction sharpens the contrast with the NWC rather than flattening it. (Pre-1821 rank names still need a source check — Section 20.)

### 5.2 NWC — Fort William
- Aggressive and mobile but stretched thin.
- Built on the voyageur canoe-brigade network and a willingness to winter deep in Indigenous territory, including marrying into it.
- Fort William hosts the annual **rendezvous** each July — partners, wintering clerks, and voyageurs converging for trading, decision-making, and (historically) real drinking and gambling. Strong candidate for a major set-piece "season." (See Section 20 for an open question on how large this can realistically be under the population model in Section 11.)
- **Structure — a partnership, not a hierarchy.** There is no London-style top. At the top are the **Montreal Agents**, the company's financiers and suppliers; beneath them the **partners (proprietors)**, paid through their shares in the trade rather than a salary; beneath them **clerks**, apprenticed five to seven years on a fixed salary in the hope of eventually earning a share; and beneath them the voyageurs, guides, and steersmen.
- **Shares are the unit of both pay and weight.** Twenty shares in the 1787 and 1790 agreements, subdivided in 1795 and 1802, and a hundred from the 1804 amalgamation (75 old NWC, 25 former XY). Wintering partners held one or two each, and the Montreal agent firm held a large block. A partner who retired from wintering gave up his shares — Roderick Mackenzie surrendered one of his two on retiring and the other when the XY was absorbed — which is what makes a vacated share a prize at the council (Section 3.8.1).
- **Two years out of three.** Every partner is bound to spend two years out of every three actually wintering in the interior managing a post; anyone no longer willing to do that is expected to resign. Mechanically this is a standing rule: a partner who's been in Montreal too long should visibly lose pull with the wintering partners (Section 3.6).
- **The ceiling.** French-Canadian employees could rarely rise above guide or steersman, however good they were. That gives the Métis/freeman path (Section 5.3) a sharper reason to exist than "more freedom" — it's the only track open to men the company itself refuses to promote past a certain rank.
- **Company decisions are made in council, not issued.** Wintering partners meet the Montreal Agents at the Fort William rendezvous, and the year's directive is the output of that negotiation (Sections 3.8.1, 10.1.3) — a consultative shareholder system standing in contrast to HBC's hierarchical one.

### 5.3 Métis / Freeman — Red River
- Not really "competing" with the companies — the economic engine that feeds both of them, via pemmican and buffalo hunting.
- Communal buffalo hunts were organized almost like military campaigns (hunt captains, scouts, formation discipline) — see Section 16.1.
- Freemen (independent traders no longer tied to a company) sit outside the formal hierarchy entirely — highest personal freedom, least institutional backing.
- Structurally, the Métis sit between two differently-shaped organizations — HBC's one-way monologue and NWC's negotiated partnership (Section 10.1) — which is a concrete reason they're the natural interpreters and couriers between them, not just a geographic coincidence.

### 5.4 Character Creation
Not a separate minigame — the moment a player supplies the seed values a personality roll would otherwise generate for a new NPC (Section 11.1), so the player is a parameterization of the same agent model rather than a special case.

- **Path** (Sections 5.1–5.3): HBC, NWC, or Métis/freeman. Sets starting location, company affiliation (if any) and its Tier 2 authority structure (Section 3.1), and which marriage track is available by default (Section 8.1).
- **Personality**: the same trait set every NPC carries (Section 11.4 — loyalty, discretion/talkativeness, suspicion, ambition, sociability), set on the same scale rather than a player-only system. This is the concrete, player-facing instance of Section 20's open "skill baseline authoring" question: whatever formula or table ends up mapping personality to starting skills applies identically here.
- **Skill points**: a modest, freely-allocated pool layered on top of the personality-seeded baseline (Section 3.10), representing background rather than replacing the shared mechanism — "grew up trapping," "apprenticed as a clerk," and so on nudge specific skills further without giving the player a second stat system nobody else in the world uses.
- **Name**: every agent is named (Section 11.1); the player is no exception.

### 5.5 Arrival & First Orders
The first order a new arrival ever receives isn't a scripted intro — it's Section 3.9's delegation loop and Section 10.1.1's message object running for the first time on a brand-new agent. Because of that, the same mechanism also handles every ordinary replacement recruit (Section 13.2) arriving in later years, not only the player's own start.

**HBC — the fully worked case.** The character arrives aboard the annual ship (Section 10.1.2) carrying an order: report to the Chief Factor. That order exists as a message the instant they're physically at the post, but per Section 3.9's own subordinate-decision step — "the assignment competes with his own Tier 0 and Tier 1 goals... compliance comes out of loyalty, ambition, fear, and pay, not a flag" — it doesn't fire automatically. It competes with whatever the arrival's personality (Section 11.4) currently makes most urgent:
- High loyalty/ambition → reports promptly.
- Low discretion, or an existing taste for drink (Section 13.4) → finds the rum ration first.
- An existing relationship already at the post — a lover, a spouse (Section 8) → seeks them out first.
- High sociability, no relationship at the post yet (Section 11.4) → drifts toward the men's house or common area to meet people before reporting.

Whenever the report actually happens, the Chief Factor's response draws from the same task-assignment machinery already built (Section 3.9 steps 2–4) rather than a fixed "here's your job": a real assignment, an order to idle at the post if nothing's currently needed, or a reassignment/order to leave for another posting.

**NWC and Métis/freeman** *(proposed, not yet detailed to the same depth)*. NWC arrival is likely at the Fort William rendezvous or by direct recruitment from a wintering partner (Section 5.2) — reporting to a partner/bourgeois rather than a Chief Factor, with the company's kin-network hiring pattern (Section 5.2) making "an existing family connection at the post" a more central case than it is for HBC. Métis/freeman arrival has no company hierarchy to report to at all — more likely an introduction to family or a hunt captain (Section 16.1) than an order in the HBC sense. Both need the same worked-out detail as the HBC case above.

---

## 6. World & Regions

Distinct zones with distinct feel/mechanics — including distinct wildlife (Section 14.4/16.2), which is one of the things that should make a region feel like itself rather than a reskinned map:

- **Hudson Bay lowlands** — subarctic, short trading season, York boats.
- **Canadian Shield / Great Lakes corridor** — the canoe route; endless portages; NWC's mobility advantage shows here.
- **Plains around Red River** — buffalo herds, Blackfoot Confederacy and Cree/Assiniboine territory, open horseback country.
- **Rocky Mountain passage** — the bottleneck between the two worlds.
- **Columbia / Oregon country** — Fort Vancouver, Chinook and other coastal/river nations, and the first trickle of American settlers. (Fort Vancouver is dated 1825 on the map, after this game's window; Fort George — Astoria, 1811, ceded 1813 — is the in-period Columbia anchor. See Section 6.1.)
- **Southern Department / James Bay & East Main** — its own region (Section 6.2): Moose Factory, Albany Fort, Rupert House, Eastmain House, and the Cree, Ojibwe, and Algonquin bands of the James Bay country, with Sault Ste. Marie as its southern anchor.

### 6.1 Map & Play-Area Boundary
- **Reference map**: `Fur_Trade_Game_Map.png` ("The Fur Trade Frontier", c. 1670–1870; Natural Earth base with a historical layer after Colpitts' *Pemmican Empire* and Ray's *Indians in the Fur Trade*), with inset panels for Qu'Appelle / Cumberland / Norway House, Red River & the lower Assiniboine, and Lake Superior & the Grand Portage. The thin black outline drawn on it is the intended play area.
- **East edge**: runs from Sault Ste. Marie to the edge of James Bay, past Moose Factory, so the Southern Department is included (Section 6.2). The larger area around James Bay is optional.
- **West edge**: working assumption is the Pacific coast, as already planned, which keeps the Columbia region and the Americans-in-Oregon thread. A cut at the Rockies was floated, but it would chop the Columbia region already built out here (Section 20).
- **South**: the real cut is depth into the American interior. Only the edge of it is in play, via Astor and Manuel Lisa as the nuisance faction (Section 7.3) — no simulation of St. Louis or the deep Missouri.
- **Date gating**: the map spans c. 1670–1870, and several posts labeled on it post-date the game window (for example Fort Vancouver 1825, Fort Union 1828, Fort Ellice 1831, St. Paul 1841, Fort Victoria 1843). Every post needs a founding date and only exists in-world once the timeline reaches it.
- **Reusable map layers**: post types (HBC, NWC, American, French regime, Russian-American Co.), factory/district HQs, pemmican provision posts, Indigenous sacred sites (S1–S11), historic events and battles, key portages, canoe/brigade routes, nation homelands and language families, the Rupert's Land limit, and HBC district boundaries.

### 6.2 Southern Department (James Bay / East Main)
- **Historical basis**: a separate HBC administrative division headquartered at Moose Factory rather than York Factory, running from James Bay south toward Upper and Lower Canada and including the East Main coast. Albany Fort, Rupert House, and Eastmain House sit inside it on the map (Moose Factory 1673, Albany Fort 1679, Eastmain House 1719, Rupert House 1776).
- **Decision**: it stands as its own region rather than being folded into the Canadian Shield / Great Lakes region, because the point is to replicate the structure of the HBC and NWC as closely as possible across ~1801–1821.
- **Mechanics (proposed)**: Moose Factory is the third Bay depot in the HBC message system (Section 10.1.2), so the department gets the ship-and-depot mechanism for free. Because it's a distinct department, it can also run its own faction-standing review through Moose Factory (Section 3.6).
- **Population cost**: Moose Factory, Albany, Rupert House, Eastmain, plus the surrounding Cree / Ojibwe / Algonquin bands, is a real addition on top of the 15–20k agent budget (Section 11.1).
- **Epilogue**: Williams' Southern Department is the piece of the map Simpson doesn't reach in 1821 (Section 4.3).

---

## 7. Faction Web

### 7.1 The Companies
HBC, NWC, and (after 1821) the merged company. See Section 4 for the historical arc between them.

### 7.2 Indigenous Nations & Intertribal War
This is real intertribal war, not scenery. The **Blackfoot Confederacy** (Siksika, Kainai, Piikani) held military dominance on the northwestern plains, in active conflict with **Cree, Assiniboine**, and other groups over territory and horses. Critically, the fur companies were arming one side or another — every gun a post sells tips a real balance of power, so trade decisions aren't neutral commerce, they're taking a side. This conflict isn't background flavor — it physically plays out via the party-collision model in Section 15: a war party is actually moving to raid, and the player can actually run into it.

### 7.3 Americans — Nuisance Faction
Doesn't need to be a playable path to matter, and resolves into two differently-shaped background nodes rather than one (Section 10.7):
- **Astor / New York** — a single-principal node: the American Fur Company (incorporated 1808) was Astor's alone, so one agent's goals decide everything it does. His **Pacific Fur Company** built Fort Astoria in 1811; lost it to the British in the War of 1812 (became Fort George, 1813) — a real available set-piece for "fight Americans" content. The 1811 Southwest Company briefly partnered Astor with the NWC, and when the Pacific Fur Company sold its Columbia holdings to the NWC in October 1813, many of its employees became Nor'Westers — a real personnel pipeline into Section 13.2's recruit pool, not just a change of flag on a map.
- **St. Louis** — a small merchant association: Manuel Lisa's **Missouri Fur Company** (formed 1809, reorganized 1812 after Astor's buyout offer was rejected) pushing up the Missouri from further south. The Blackfeet besieged its Three Forks post in 1810 — direct fuel for the arming-a-side friction in Section 7.2.
- **The law**: Congress excluded foreigners from the American trade in 1816 (Section 13.6).
- **Unaffected by the blockade** — outside the British blockade situation entirely, a real if background trade advantage during the years it hurts HBC and NWC (Section 13.6).
- As background AI: poaches trap lines, competes for the same Indigenous trade partners, occasional friction/violence, especially around the Columbia and upper Missouri.
- **Kept thin by design** (Section 10.7): no council, no ship cycle, just enough to drive the friction above. The map's St. Paul (1841) is two decades past the game window and drops out; Fort Snelling (under construction from 1819) sits closer to period if a third node is ever wanted. Open in Section 20.

### 7.4 Vendetta & Informal Justice
Section 4.1 already notes that interior violence happens "far from any court's reach" — none of the factions have real judicial infrastructure out there, and disputes have to resolve some other way. This reuses the grudge/memory system already built (Section 10.4) rather than inventing new state: an unresolved wrong doesn't just sit as a reputation penalty, it can escalate into an actual Tier 1 goal (Section 3.1) — get justice for X — that the aggrieved agent or their kin actively pursues.

**Indigenous kinship-based justice is the sharpest case.** Real historical practice among Plains and Subarctic nations centered on restitution owed to the aggrieved family rather than punishment by a distant authority: compensation (goods, horses, sometimes a marriage) to make a killing right with the victim's kin, a blood debt that could pass down through a family if left unanswered, and — in some traditions — the offender or a substitute being adopted into the aggrieved family to literally replace what was lost. This dovetails directly with the marriage/kinship system (Section 8): a killing between bands can be resolved, worsened, or complicated by exactly the same kin ties that govern the player's own married-in obligations, so a player embedded in a band (the Nor'West track, Section 8.1) can find themselves personally responsible for settling, or personally targeted by, a debt they didn't create.

**A blood debt can be formally closed, not just left to fester or explode.** A vendetta-settlement Accord (Section 3.8.2) — negotiated kin-to-kin rather than needing a full council — resolves an unanswered debt with restitution terms and actually retires the underlying grudge (Section 10.4), rather than leaving it to decay on its own or waiting for it to become a Tier 1 goal that erupts into violence.

**Company justice is thinner, not absent.** HBC and NWC both have some internal authority over their own men (a Factor's standing, Section 3.6; a partner's exposure to peers at the rendezvous, Section 10.1.4), but neither has any real reach over a killing involving an outside party — which is exactly why so much of the "dirty war" years (Section 4.1) go unpunished by any formal process and instead pile up as exactly this kind of unresolved, kinship-adjacent debt.

---

## 8. Marriage & Kinship System

Grounded in a real, documented cultural split (see Sylvia Van Kirk's *Many Tender Ties* for further reading) between how NWC and HBC culture handled "country marriage."

### 8.1 The Tracks
- **Nor'West way** — the player marries *into* an Indigenous family and lives with them: joins seasonal camp movement, bison hunts, ceremony, daily life. Alliance/reputation with that nation builds faster because the player is embedded, not visiting. Trade-off: geographically tied to the band, company career/rank progression slows. Being embedded this way also means kinship obligation applies to conflict (Section 15.2) — the player can be pulled into a fight their in-laws start.
- **HBC way** — the wife moves *into* the player's post household. The player runs the brigade circuit: goods out, dispatches carried, trades made, rank climbed. Trade-off: real, documented strain from long absences (relationship health should tick down the longer the player is out on the circuit).
- **Métis / freeman way** — outside both structures. No company obligations, most personal freedom (trade/ally with anyone), least institutional backing (no post to fall back on, no company ransom fund if captured).

### 8.2 Marriage Number Rule
- **HBC**: monogamous — one bride only, matching British/Christian norms.
- **NWC and Métis/freeman**: polygamous — multiple wives allowed, consistent with both some Indigenous kinship-alliance practices and NWC wintering-partner customs of the era.

### 8.3 Transition Costs (asymmetric)
- Moving **into** HBC life (formalizing a post-household marriage) is **harder and more expensive**.
- Moving **from** HBC **to** Métis/NWC life is **easier**.
- Bringing an entire outside family into the post is a much bigger, costlier, more socially taxing move than bringing just a wife — because the family is larger and HBC only recognizes one bride.

### 8.4 Consequences of Leaving Wives Behind
When a player with multiple wives formalizes into HBC life, the wives who don't come with him are not simply removed from the game:
- Causes **judgment from her family and tribe** — a real reputation hit with that specific band/nation.
- Comes at **great material/social cost** regardless of outcome.
- Can lead to **retaliatory attacks** on the player.
- She may be **resentful and find a new partner** — potentially becoming allied with a rival company or family, a wound that can keep causing problems long after the choice is made. Under the unified agent model (Section 11.1), this isn't a scripted outcome — she's a full agent who actually remarries someone, and that someone is a real, simulated political actor from then on.

*(Open question: can a player renegotiate/change tracks more than once? See Section 20.)*

---

## 9. Death, Captivity & Slavery

Two clearly distinct fail-state systems, not one:

### 9.1 Captivity — light, short-term
- Functions close to a "passing time" screen: brief narrative beat, maybe a skill check, resources/goods lost, dropped back into the world.
- Represents brief detention, not an ongoing altered state.
- A lost fight (Section 15.5) can resolve into this instead of death.

### 9.2 Slavery — a persistent state
- A real status effect with limits, represented by literal "chains."
- Should plausibly restrict:
  - **Movement** — bound to wherever captors are; the world effectively shrinks.
  - **Relationships** — existing company/alliance standing decays while the player is gone and unaccounted for.
  - **Actions** — no trading, diplomacy, or advancement; verbs narrow to build-trust / look-for-opening / endure.
- Resolution paths: **escape** (skill/opportunity based), **ransom** (network has to know and be willing to pay), or slow **adoption/integration** into the captor community (the darkest and most narratively interesting option — resolves the state into something other than freedom as originally defined).
- Historically grounded: both Plains intertribal warfare and Pacific Northwest coastal societies (Chinook, Tlingit, Haida, others) had real captive-taking/slave-holding traditions.

### 9.3 True Death — Three Branches
On permanent death, the player chooses:
1. **New character, same persistent world** — richest narratively (factions remember the prior character's actions) but requires world state to be tracked independently of any one character's save from the start.
2. **New character, new world** — a clean roguelike reset; much simpler to build since nothing has to persist.
3. **Succession via kinship network** — play as someone already established in the player's relationship web (spouse, child, in-law). Under the unified agent model (Section 11.1), this isn't a separate system needing its own persistence layer — it's simply handing control to an agent the simulation was already running, with its own real memory, relationships, needs, and goals (Section 3.1) intact.

### 9.4 Aging & Natural Decline
Succession doesn't have to wait for violent death. Voyageur labor historically wore men out physically by their 40s, and a character's declining capacity — slower on the portage, worse in a fight, less suited to the brigade circuit — is a real, non-violent path toward handing control to an heir (Section 9.3.3), and a natural nudge toward the political/administrative side of a career (Section 3.7) as the physical one closes off. It also gives the multi-generational family-ambition goal (Section 3.1) a built-in reason to actually change hands over the course of a long game, rather than only doing so through tragedy.

---

## 10. Information & Knowledge System

**Core epistemic principle** (Section 2): no character knows about anything they did not directly observe or were not told about by someone they know. There is no ambient/omniscient knowledge for any actor, and this applies to *every* event in the world — not just gossip, but an NPC's death, a marriage, or a battle's outcome (Section 11.1) — and, per Section 3.5, extends to goals themselves, which have to be inferred rather than known.

### 10.1 Formal Channel — Physical Message & Courier Network

#### 10.1.1 The Shared Message Object
One generic underlying system, **specialized per faction** with different parameters and — importantly — a different *generator*, rather than rebuilt per faction. Every message or order carries:
- an **origin**, a **destination**, and a **route** between them;
- a **transport mode** and a **base transit time** set by distance and mode;
- **seasonal/weather modifiers** (Section 14.2);
- **interception risk** in transit (Section 10.3);
- and a **read event**.

The read event is the important part: nothing a message carries takes effect until that event fires at the destination. A Tier 2 goal (Section 3.1) doesn't update when a message is *sent*; it updates when someone actually *opens* it. That single rule is what makes "posts act on stale orders" real instead of merely asserted.

#### 10.1.2 HBC — A Monologue
London authors a reliable once-a-year instruction set, reading last year's account books and journals (which arrived on the previous ship). Section 10.7 covers what happens on the London end.
- That instruction set boards **one ship**, constrained to the short ice-free window, and lands at one of the three Bay depots — **York Factory, Churchill, or Moose Factory**. Moose Factory serves the Southern Department (Section 6.2), so the department reuses the same mechanism rather than needing a bespoke one.
- From the depot it's a **second leg inland** to the actual post, with its own transit time. A remote inland Chief Factor might not open this year's orders until deep into his own season, sometimes not until the following winter.
- The **return leg is symmetrical and has a hard deadline**: furs must reach the depot before the ship has to leave, or they miss the year entirely.
- Between one ship and the next, the Factor's Tier 2 goal is **locked**. He improvises at Tier 1 / Tier 0 on his own judgment, but the top-level directive doesn't move until the next read event, full stop. This matches the pre-1821 HBC, where a Factor was substantially on his own between ships (Section 5.1).

#### 10.1.3 NWC — A Negotiation
There is no single author to receive from. Each wintering partner spends the year running his district on last year's plan plus his own judgment, and every July converges on Fort William carrying furs and field intelligence, meeting the Montreal Agents who arrive the other way with capital, trade goods, and news from Europe.
- The new year's directive isn't handed to anyone. It is the actual output of a **Section 3.8 council negotiation** among partners and agent firms whose votes are weighted by the shares they hold (Section 3.8.1), each with real standing (Section 3.6) and their own read on the year. It's the same generic mechanism as HBC's annual update, with a genuinely different generator behind it.
- **Failure mode**: a partner who couldn't make it to Fort William, or who left early, gets the new plan secondhand and distorted the same way any rumor is (Section 10.4) — he could spend a whole season honestly acting on a garbled version of what his own partners decided.
- The structure behind this is in Section 5.2; the council mechanics are in Section 3.8.1; who funds it is in Section 10.7.

#### 10.1.4 Deviation Risk Differs by Structure
When something breaks the standing plan mid-season — a rival burns your post, a war party moves, pemmican runs short — deviating from the plan is a different kind of exposure in each company. This falls out of the structural difference rather than a bolted-on "reputation penalty" number:
- **HBC Factor**: deviating from London's stale orders is real personal risk, because there's an unambiguous authority above him who will review it on the next ship.
- **NWC partner**: improvising against last July's council decision means answering to peers he'll see again at next year's rendezvous, not to a distant, unquestionable superior.

#### 10.1.5 Other Networks and Off-Map Nodes
- **Indigenous nations**: runners, camp criers, news carried at gatherings — potentially the fastest-moving information in some contexts (a mounted plains rider can outpace a canoe brigade), but vulnerable to interception/distortion differently.
- **Métis**: sit in the middle of all of it, often literally serving as couriers/interpreters between the other three. They bridge two differently-shaped networks — HBC's monologue and NWC's negotiation — a natural niche for information-brokering gameplay.
- **London**: a small background council (Section 10.7), not the pure black box earlier drafts assumed — it authors the HBC monologue (Section 10.1.2) but is itself a real, if never directly visited, set of agents.
- **Montreal**: the Montreal Agents travel to Fort William for the July council (Section 10.1.3); Section 10.7 covers who they are and what funds the directive.
- **American nodes**: two, of different shape — Astor/New York and St. Louis (Section 10.7) — plus the 1816 foreign-exclusion law. Thin by design: no council, no ship cycle, just enough to drive the poaching and rivalry friction in Section 7.3.

Posts act on stale orders until new word arrives — a Factor or partner might keep operating under instructions that are already obsolete. Rumor vs. official word are two separately-paced channels: gossip may be faster and less reliable than a sealed letter.

### 10.2 Informal Channel — Witness, Memory & Rumor
The same underlying idea (physical, social propagation of knowledge) applied to what characters personally witness rather than official correspondence. See Section 11 for the memory/personality model this runs on, and Section 10.4 below for the concrete propagation rules.

### 10.3 Interception & Black-Market Intelligence
- The player (and NPCs) can **physically carry messages themselves**.
- The player can **intercept couriers**, kill or capture them, and take their messages.
- Intercepted messages can be **sold to rival factions**, and should actually **influence that faction's objectives/actions** when read — whoever reads a message is affected by it, not just its intended recipient.
- **Value should vary by buyer** — different factions want different things from the same intercepted letter.
- **Discovery/blowback is real**: a faction whose courier was killed and mail sold to a rival can trace it back. This resolves as a **hidden roll** — the player never sees the odds, only the outcome (a grudge forming, or nothing) — but it is a genuine risk, not a free action, and creates a named grudge if discovered.
- **Portages are natural interception chokepoints** (see Section 12.2).
- **Express couriers are a distinct, high-value case of the same message object (Section 10.1.1)** — a light, fast crew carrying no cargo, dispatched specifically for time-critical news: a market price shift (Section 13.6), a declaration of war, merger terms (Section 13.7). They trade interception difficulty for value: faster and harder to catch than an ordinary brigade, but worth far more if caught, since acting on a price shift weeks before a rival does is a real trading edge.
- **A captured message can be replaced, not just read.** Beyond selling or acting on what was intercepted, a captured dispatch can be swapped for a forged replacement carrying false content, which the original recipient then acts on believing it genuine — a much more active manipulation than passive interception, and one that should require actually defeating whatever makes a real dispatch verifiable (a seal, a known hand) rather than being a free swap.

### 10.4 Propagation Mechanics
- Every witnessed event becomes a memory carried by whoever witnessed it: what happened, who was involved, where, when, and **how directly** — a direct eyewitness account carries more weight than something heard secondhand, and directness steps down one level with each retelling.
- Distortion is specific, not random noise: each retelling can shift **actor identity**, **severity/framing**, or **time/location** — not all at once — with the odds shaped by the teller's personality (Section 11.4): a suspicious teller inflates severity, a careless one garbles identity or location.
- Propagation only fires at a few concrete moments (Section 11.3), never a continuous background churn.
- A memory's weight decays over time unless reinforced. A secret deliberately sat on as leverage (Section 11.4's ambitious/resentful case) decays the same way — **except** a secret tied to an ongoing relationship (a hidden family, say) gets its weight refreshed to full every time the player has fresh contact with its subject. Suppressing that kind of secret costs staying away from the thing being hidden.

### 10.5 Feeds Back Into Faction-Level Trust
Individual gossip should occasionally roll up into faction-level reputation — a single unexplained sighting is nothing, but a pattern of them reaching a post over months can tank standing with a whole faction without any single dramatic reveal. This rollup happens at the **same cadence as the formal message cycle** (Section 10.1) rather than continuously — a post reviews its accumulated unexplained sightings once per cycle and converts the weighted pile into a reputation delta. This is also why reputation damage should feel delayed rather than instant: the world runs on stale information about itself, the same way it runs on stale official orders.

### 10.6 Language & Literacy Barriers
Not everyone in this world shares a language or can read, and neither should be assumed away:
- **Language**: a conversation without a shared tongue or an interpreter present degrades — cruder terms in trade, a real chance of the propagation system's distortion (Section 10.4) firing even on a *direct* exchange, not just a retelling. This is what makes the Métis "sitting in the middle of it" (Section 7.2) mechanically true rather than only asserted: an interpreter isn't flavor, they're the thing standing between a clean negotiation and a garbled one.
- **Literacy**: a clerk can read and write dispatches; a voyageur without letters can only report what he saw in his own words, secondhand, whenever he next has someone's ear. This is another reason the formal and informal channels (Section 10.1/10.2) move at genuinely different speeds and reliabilities for different people, not just different factions.
- **Named languages, not an abstract barrier**: English (HBC officers), Canadian French (NWC voyageurs and clerks), Gaelic (the Orkney Islanders already in HBC's recruit pool, Section 10.7), Michif (the Métis), Cree, and Ojibwe are the working set — which two agents share, or don't, is what actually determines whether the degradation above applies to a given exchange.
- **An interpreter can lie on purpose, not just distort by accident.** Section 10.4's distortion is personality-driven and unintentional; an interpreter is a goal-driven agent (Section 3) like anyone else, and can deliberately alter what's translated — softening a threat, inflating a price, swapping an order's content outright — in service of their own loyalty or interest. This is discoverable the same way any deception is (a hidden roll, Section 10.3), but until then the recipient has acted on a message that was never actually sent.
- **Rumor moves fastest within a shared language.** News reaches a Métis network before it reaches English-speaking HBC clerks not because of standing or proximity, but because Section 11.3's propagation triggers fire more freely when no translation step is needed at all.

### 10.7 The Metropole — London, Montreal & the American Nodes
The one place this design admits deliberate abstraction, kept deliberately small and causal: a few dozen named agents who never enter the live tier (Section 18.2), plus the ledgers in Section 13.7, resolved lazily on the ship and rendezvous calendar. The metropole supplies what the frontier can't make for itself — capital, goods, people, demand, and law — and the player only ever touches it secondhand.

**London (HBC).** The Governor, Deputy Governor, and Committee were elected by the proprietors at a general court — modeled as roughly 7–12 named agents (the Committee plus the largest shareholders) who run a Section 3.8 council once a year, when the return ship lands.
- *Inputs*: furs, journals, account books, and the factors' **indents** — requisitions for the outfit written two years in advance. Goods arrive one to two years after being requested, and the year's directive is written from books that are already a year old, reinforcing Section 10.1.2's stale-orders principle at the metropole end too.
- *Decisions*: whether to sell furs now or hold them for a better market (Section 13.6) — the Company historically deferred auctions when prices were low — plus filling the indents, setting dividends, appointing officers, and writing the directive.
- *Politics are causal, not scripted*: Selkirk began buying HBC shares in 1808; his brother-in-law Wedderburn joined the Committee in January 1810 and outmaneuvered Wollaston, who resigned that March; the Red River land grant followed in May 1811 and obliged Selkirk to supply 200 men a year. In the model this is Selkirk's own Tier 1 goal (Section 3.1) running through shares, a committee seat, the grant, and a recruiting stream — not a cutscene. See Section 4.4 for how this interacts with the colony's founding being treated as a fixed condition.

**Montreal (NWC).** A few named agents from the agent firm — McTavish, Frobisher & Co., later McTavish, McGillivrays & Co. — backed by the London house McTavish, Fraser & Co., which supplied goods, arranged credit and shipping, and sold the pelts in London. Montreal's year is more physical than London's:
- *The routine*: goods are baled in winter warehouses, voyageurs are engaged around late March, and goods pile up at Lachine; then the July council at Fort William (Section 3.8.1); then furs travel back down for London.
- *Representation*: only two or three chief partners actually make the trip up to the meeting (feeding into Section 3.8.1's "who's in the room").
- *On the map*: the Montreal-to-Sault Ste. Marie leg is off-map; brigades simply enter at the play area's eastern edge (Section 6.1).
- *Role in the model*: the council writes NWC's directive (Section 10.1.3); Montreal's job is what that directive is actually funded with — goods, credit, and hires.

**American nodes.** St. Paul (1841) drops out entirely — two decades past the window (Section 6.1, Section 7.3). Two nodes of different shape, plus one law:
- **Astor, New York** — a single-principal agent. The American Fur Company was incorporated in 1808 with Astor as sole stockholder, so one agent's goals decide everything that node does.
- **St. Louis** — a small merchant association. The Missouri Fur Company formed in February 1809 and reorganized in 1812 with ownership restricted to St. Louis owners after Astor's buyout offer was rejected.
- **The law**: Congress excluded foreigners from the American trade in 1816 (Section 13.6's demand calendar).
- *What they send to the frontier*: the 1811 Southwest Company briefly partnered Astor with the NWC; the Pacific Fur Company sold its Columbia holdings to the NWC in October 1813, and many of its employees became Nor'Westers from that point — a real personnel pipeline feeding Section 13.2's recruit pool. The Blackfeet besieged the Missouri Fur Company's Three Forks post in 1810, directly feeding the arming-a-side friction already in Section 7.2.

**People.** Recruit pools (Section 13.2) stay abstract counts until someone is mobilized, but they differ by company rather than being one generic well:
- **HBC**: Orkney Islanders formed a real part of the historical labor force, alongside Christ's Hospital boys the London directors indentured as apprentice clerks, and Selkirk's own 200-men-a-year recruiting stream tied to his grant above.
- **NWC**: Lower Canada parishes and kin networks — Simon McTavish paid for his nephews' education and placed each in a career inside his own company, nepotism as a literal recruiting pipeline — plus, after 1813, former Pacific Fur Company men.
- **Refill rate** depends on wages and each company's finances (Section 13.7), plus a lagged reputation effect: bad news of frontier deaths has to actually reach home through the ordinary propagation system (Section 10) before it can slow recruitment, rather than a separate abstract morale number.

**Cost and player contact.** On the order of 30–40 named background-tier-only agents (Section 18.2) across the whole metropole, acting on annual or seasonal beats. The player touches it only through messages, goods, and people: intercepting a ship or brigade, or writing to London directly. A clerk's letter reaches the Committee at a read event a year later and can make or break a factor's standing (Section 3.7).

### 10.8 Exploration & the Map
Geography is knowledge like anything else in this section — no agent, including the player, has ambient awareness of terrain they haven't personally seen or been shown. Most of the map starts unrevealed per-agent, filled in by two means that combine rather than compete:
- **Walking it** — the ordinary way, revealing whatever an agent has actually traveled through, tied to the same live/local/background fidelity an agent is being resolved at (Section 18.2).
- **A map as a physical item** — a chart covering some region, which instantly reveals that region's geography to whoever holds it, regardless of whether they personally explored it. A map is knowledge exactly like an intercepted message (Section 10.3): it can be found, bought, given, stolen, or sold, its value varies by buyer (a chart of unmapped Blackfoot territory is worth far more to a company than to a band that already knows the country), and losing one to a raid hands the finder real, permanent knowledge, not just an item.

A found or purchased map doesn't need to cover the whole world at once — a partial regional chart growing into a fuller one as more pieces are acquired or explored fits the same "grows as it's explored" idea, without requiring a single map object to somehow represent the entire game world from the start.

### 10.9 Legacy — Memory Across Kinship
A dead character's reputation doesn't vanish with them. Memories (Section 10.4) that NPCs already hold about a deceased agent don't just decay in place — when their kin (Section 8) are present, especially an heir who succeeds them (Section 9.3.3), those memories apply to the kin too, one hop further removed than an ordinary retelling, using the exact same weight-and-decay math rather than a new reputation stat.

A Factor who cheated a band and was never caught, dying and passing his post to his son, means the band's chief — who still holds that memory at its original directness — treats the son with inherited suspicion from the day he arrives, entirely on the strength of a debt his father never answered for. A beloved trader's daughter inherits the same kind of banked goodwill. Trivial memories don't matter here any more than they matter anywhere else — they've already decayed away by the time they'd be relevant, so this needs no separate filter, only the propagation system running one hop further than it usually does.

This also gives the multi-generational family-ambition goal (Section 3.1) its other half: a family climbing a hierarchy across generations is now visibly carrying — or living down — what the last generation actually did, rather than resetting clean at every succession.

---

## 11. NPC Simulation — Population, Memory, Personality, Objectives

### 11.1 Unified Agent Model
Every character who exists in the simulated world — not only recurring or "important" ones — is a full agent: memory, personality, goals (Section 3), and (Section 17) the same hunger, thirst, exposure, and marriage/kinship eligibility as the player. There is no separate lightweight tier for generic crew standing in for "the world." Everyone is named.

**Scale target (planning numbers, to be tuned).** Roughly 5,000 named human agents and a comparable number of animal agents as a baseline (~10k), about 15k as a comfortable working population, and around 20k as the ceiling for the whole map, including the Southern Department (Section 6.2) and the metropole (Section 10.7). Animals start as site counts and are promoted to individual named agents when their site enters the live or local tier; survivors stay agents and the dead are culled (Section 16.4).

**Existence and update cost are different costs.** Conflating them is the mistake to avoid:
- *Existing as data is cheap.* A named agent — ID, position, a handful of need/personality floats, a bounded relationship list, a current goal plus step stack — is roughly 1–2 KB even generously (an estimate, not a measurement). Twenty thousand of those is on the order of 20–40 MB. "Everyone exists, everyone's named, everyone has a real memory ledger" costs almost nothing just by existing.
- *Being simulated in detail every tick is what's expensive.* Running a full goal-planner, pathfinder, and dialogue-readiness check on all ~20k agents at frame rate is the part that would bring a machine to its knees.

So the tiered model in Section 18.2 isn't a compromise on "everyone's real" — it separates existence from update frequency. A background-tier agent runs the same goal logic as a live one, resolved lazily: a hunting party's whole week can resolve in one calculation (this goal, this weather, this terrain → where they ended up and what they got) the moment anything needs to know. That's the same agent computed lazily, not a cheaper fake.

**Scene-level counts stay small and historically honest.** A canoe crew is 4–14 people, a wintering post is a factor, a couple of clerks, and a handful of laborers, and even Seven Oaks — the single bloodiest event in this whole timeline — was on the order of 60 riders against under 30 colonists. Nothing at the scale the player actually sees is crowd-scale; the large number lives in the world total, not in any one scene. Rendezvous-scale gatherings are the one place this gets stressed (Section 20).

**Bounded memory keeps the total from ballooning.** Relationship lists are capped and memory weight decays unless reinforced (Sections 10.4, 19), so per-agent records don't grow without limit over a long playthrough.

The one abstraction that remains outside the metropole (Section 10.7) is the replacement pool in Montreal/Britain (Section 13.2): it's a count of *potential* recruits, not people. Nobody is generated — no name, no personality, no simulated existence — until they're actually mobilized and start traveling toward the frontier. From the moment their canoe leaves the metropole, they're a full agent like anyone else, and if that canoe is hit at a portage, a real person with real relationships died, not a number decremented.

Consequence: NPC marriages, deaths, and successions are world-generative, not flavor. A rival factor's advantageous marriage, or his death from exposure on a bad set of rapids, genuinely reshapes who's available to whom — the same way it would if it happened to the player — and the player only learns about it through Section 10's propagation system, same as everything else. This also means off-screen agents don't need frame-by-frame simulation: their needs and events can resolve at a coarser tick (weekly/seasonal) when not directly observed, matching the fidelity model in Section 18.2.

### 11.2 Memory Entry Anatomy
Who saw it, what they think they saw, where, when, and **how directly** (a direct eyewitness account should carry more weight than something heard secondhand). This gives a natural distortion mechanic: the more hops a rumor takes before reaching someone who matters, the more it can drift from what actually happened. See Section 10.4 for the concrete rules.

### 11.3 Propagation Triggers
A few concrete moments, not a fully simulated social graph:
- A traveling party **debriefing its boss on return** (the canonical example: an HBC agent seen entering an NWC fort — if the boss didn't send them there and tell everyone already, the witnessing party may mention it once they're back).
- Casual talk at a post's common area.
- Two traveling parties crossing paths on the trail.

### 11.4 Personality Filters Behavior, Not Just Memory
A small trait set (loyalty, discretion/talkativeness, suspicion, ambition, sociability) decides what an NPC *does* with what they know:
- Loyal companion → may warn the player privately first.
- Talkative NPC → spreads it regardless of sensitivity.
- Ambitious/resentful NPC → may sit on it as leverage/blackmail rather than report it (see Section 10.4's decay/refresh rule for how these secrets age).
- Suspicious factor → interprets ambiguous sightings in the worst light by default.
- Sociable NPC → seeks out casual talk and company on its own initiative, generating more propagation opportunities (Section 11.3) simply by being present, independent of whether it then shares something sensitive once there — that's discretion's job, not sociability's. A low-sociability agent is the inverse: rarely initiates contact, and gravitates toward solitary work (a trapline, Section 16.1's stalking hunts) over communal settings when given the choice.

### 11.5 Player Agency — Getting Ahead of the Story
If the player (or their superior) briefs people *before* they'd otherwise witness something ambiguous, or supplies a plausible explanation that reaches them before the raw sighting does, the memory files as **known/authorized** instead of **unexplained/suspicious**. Cover isn't just "don't get seen," it's "control the narrative."

### 11.6 Feeds Back Into Faction-Level Trust
See Section 10.5 for the full mechanic — individual memories roll up into faction-level reputation at the same cadence as the formal message cycle, so a slow pattern of unexplained sightings can tank standing with a faction without any single dramatic reveal. A slow-burn version of "dark."

---

## 12. Logistics — Weight, Capacity & Transport

Real historical figures, useful as literal game balance numbers:

| Vessel | Length | Crew | Cargo capacity |
|---|---|---|---|
| **Canot du nord** (North canoe) | 24–28 ft | 4–6 (some sources: up to 8) | ~3,000 lbs (incl. crew & provisions) |
| **Canot du maître** (Montreal canoe) | up to 40 ft | 8–14 | ~7,000–9,000 lbs |
| **York boat** | up to 46 ft (largest) | 6–8 | up to ~13,000 lbs (largest); 2–3× a canot du nord's cargo for the same crew size |

- The **canot du nord** was the actual interior workhorse — used west of the Great Lakes, where portaging was frequent.
- The **canot du maître** ran the Montreal-to-Fort William leg — too big for most interior portage country.
- The **York boat** is HBC's efficiency answer and largely a **Métis invention** — developed by the employees of William Sinclair, a Métis Chief Factor, and mainly crewed by Métis boatmen historically. A natural, historically-earned specialization hook for the Métis path. York boats become HBC's dominant transport starting around the early 1820s — meaning their rise could be woven in as "new tech" right as the story approaches its 1821 close.

### 12.2 Portage Vulnerability
- A canot du nord could be carried off by just two men; a canot du maître needed about four.
- A York boat was **far too heavy to carry** — the crew had to cut a path through brush, lay rollers, and drag it overland.
- This makes canoe brigades caught at a portage able to grab the boat and run, while York boat brigades caught at a portage are stuck dragging a multi-ton vessel through brush for as long as it takes — a much longer, more dangerous ambush window. **Portages are the natural chokepoint for interception/raid content** (Section 15.1), and the one place travel mode always drops to full fidelity (Section 18.3).
- **Vessels carry a condition, the same property already tracked for weapons (Section 12.5).** Rapids, rock strikes, and the drag of a portage itself wear down a canoe's hull and pitch seal over a journey, not just the crew's stamina. Field repairs — spruce gum, a bark patch, pine resin, all field-craftable anywhere per Section 12.6 — hold a damaged canoe together; running out of gum mid-rapid is its own emergency. A hull failing outright is exactly the kind of event travel mode (Section 18.3) already drops to live fidelity for, alongside a portage or an encounter.

### 12.3 Horses — Plains Mobility & Raiding
The plains' equivalent of the canoe: a horse herd is both transport and wealth, and losing it mid-journey is a real stranding risk parallel to a wrecked canoe or a York boat stuck at a portage. Historically, horse raiding between Blackfoot, Cree, and Assiniboine was itself a major form of the intertribal conflict already established (Section 7.2) — a raid whose entire goal is the herd, not a fight over trade goods or territory. This slots directly into the existing party-collision combat model (Section 15.1): a horse-raiding party is doing exactly what it says, and running into one is a different flavor of encounter than a war party or an ambush, with a different resolution (chase it off, defend the herd, cut losses and let it go). Herd health and grazing become their own resource-management concern on long plains journeys, mirroring the fuel/food concerns of a canoe brigade (Section 17).

### 12.4 Personal Carry Capacity
The same weight/capacity logic that governs a canoe (this section) and a horse (Section 12.3), scaled down to one body rather than a new mechanic:
- **Base capacity** comes from the strength skill (Section 3.10) — seeded by personality like any other skill, and improved through use: hauling cargo and portaging is what raises it, not a separate training system.
- **Bags and packs add capacity on top of the base** as equippable items — a tumpline, a pack basket, a possibles bag — each with its own stated bonus, per the item model in Section 12.5.
- **Overloading degrades rather than blocks**, matching the existing pattern for vessels and horses: slower travel, worse combat and stealth resolution, higher caloric burn (Section 17.1) — never a hard cap on what can be picked up.

### 12.5 Items & Inventory
One generic item model, not per-category logic. Every item — a trade good, a fur pack, a tool, a weapon, a garment, food, fuel, a map (Section 10.8), a letter (Section 10.1.1) — is a physical object that exists somewhere in the world: carried by an agent, sitting in a vessel or on a horse (Section 12.3), stored in an institutional ledger, or simply on the ground. There's no abstract "player inventory" floating outside world-state; an item's location is just another fact the simulation tracks, consistent with the one-continuous-world principle (Section 18.1).

**Two fidelities, matching the tiering the rest of the design already uses:**
- **Carried inventory is item-level** — discrete objects with their own properties: weight (feeding Section 12.4's personal capacity), and whatever else is relevant to that type — a fur pack's quality rating (Section 13.6), a garment's warmth rating (Section 17.3), a weapon's condition and readied-or-carried state, a map's revealed coverage (Section 10.8).
- **Institutional storage is ledger-level** — a fort's warehouse or a band's cache tracks aggregate quantities by type rather than one row per blanket, matching how the economy already works (Section 13). This is the same fidelity-by-relevance principle as Section 18.2, applied to objects instead of agents: a specific fur pack's quality only matters while someone's actually carrying or trading it; once stowed, it dissolves into the warehouse's aggregate count (or a small quality-bucketed distribution, if that granularity turns out worth keeping — Section 20).

**Stowing and withdrawing are the boundary between the two fidelities**, and they're already just Section 3.3's ordinary verbs (use item, give, a context action against a container) rather than a special inventory-management mode: putting furs in a warehouse converts discrete items into ledger quantity; drawing a ration back out instantiates a fresh discrete item from the ledger.

**Carry capacity nests the same way weight already does everywhere else in this design**: personal capacity (Section 12.4) inside vessel capacity (Section 12's table) inside institutional storage that's effectively uncapped at the scale the player interacts with it. Overloading degrades at every level rather than hard-blocking, the same rule at every scale.

**Equipment has three states, not one inventory list**: worn (clothing layers, Section 17.3), readied (the active weapon or tool a context action or attack draws on), and carried (everything else). Discrete states rather than a single flat list, matching how a character actually holds things.

**A cache is a fourth location, alongside carried, in a vessel, and institutional storage** — an item hidden at a specific point in the world rather than held by anyone. A cache has a location, a decay rate (goods left in the open degrade the same way stored food does, Section 13.1), and a discovery risk if anyone else passes near it or already knows it's there — the same hidden-roll pattern already governing courier blowback (Section 10.3). Caching is how a stranded brigade survives an early freeze (Section 14.1) and how a side-trader keeps goods off the books (Section 3.9's skimming) without needing a separate hidden-inventory system: it's the existing item model's location field, just pointed at an empty tile instead of an agent or a warehouse.

### 12.6 Crafting
Making an item is a reusable chain (Section 3.2) like pemmican-making (Section 3.6) or founding a post (Section 13.8) — a **recipe** is just that chain formalized: inputs (materials, drawn from Section 12.5's item model), a skill check (Section 3.10) that governs the output's quality (the same quality property furs already carry, Section 13.6, generalized to crafted goods), and a location requirement.

**Most things can be made anywhere**, with ordinary materials and time: moccasins and mittens (hide and sinew), snowshoes, a bow and arrows, snares and basic traps, a fishing net, a poultice from foraged plants (Section 17.5), a canoe repair patching bark with spruce gum, a lean-to. These use the **crafting** skill (Section 3.10), improved through use like any other.

**Some things need a fixed facility.** Ironwork — repairing a musket, forging or mending a trap, tools — needs a forge, present only at certain posts, and uses a separate **smithing** skill rather than a variant of general crafting: real specialized training, not the same competence applied differently. A facility is a physical object in the world with its own properties (Section 18.1), built the same way a post itself is (Section 13.8), and it can burn (Section 18.4) or be lost the same way anything else physical can.

**Manufactured trade goods are deliberately not craftable, only repairable.** Blankets, kettles, cloth, and the guns themselves are goods the fur trade exists *because* nobody in Rupert's Land can make them — that's the actual economic premise of the whole game (Sections 10.7, 13). A forge can fix a broken musket; it can't make a new one. This asymmetry — self-sufficient in subsistence goods, permanently dependent on the supply chain for manufactured ones — is a deliberate piece of the design, not a content gap to fill in later.

**Pemmican (Section 3.6) is one example of a subsistence-craft chain among several** — dried/smoked fish, jerky, and rendered fat/tallow follow the same shape (process → preserve → pack), all feeding the same food-storage tension already established (Section 13.1, Section 17.4).

**A third category sits between anywhere and facility-gated: season-and-location-gated crafting.** Building a canoe from scratch — a canot du nord for narrow interior water, a canot du maître for the big lakes (Section 12's table) — doesn't need a fixed building the way smithing does, but does need to be near the right forest ecosystem (birch bark, spruce gum, cedar) at the right time of year, using the crafting skill like any field craft. It's a bigger, slower version of the same field-repair chain in Section 12.2, not a separate system.

### 12.7 Player Interface — Inventory, Character, Skills & Perks
Four screens surfacing state that already exists elsewhere in this design, not new mechanics of their own:
- **Inventory** — the carried side of Section 12.5's item model: what's on the body, in bags, worn, and readied.
- **Character** — wounds and their treatment status (Section 17.5), exposure and hunger/thirst levels (Section 17), and persistent conditions: addiction (Section 13.4), disease progression (Section 13.5), and perks (below). The player's own body isn't epistemically hidden the way another agent's internal state stays inferred (Section 3.5) — this screen is just where existing simulation state gets laid out for the one agent allowed to see it directly.
- **Skills** — the current value and recent trend of every skill in Section 3.10, plus which chains have been feeding it, so a player can see *why* a number moved rather than just that it did.
- **Perks** — discrete, named traits earned from a specific notable event rather than accumulated gradually like a skill: surviving captivity (Section 9.2) might harden resolve or leave real knowledge of a captor community; deep adoption into a band (Section 9.2's darkest resolution) might grant standing beyond what ordinary reputation tracks; skill mastery can unlock a qualitative capability rather than another point of the same number. A perk is a condition like any other (addiction, disease) — persistent agent state, not a separate stat system — so any agent, not only the player, can carry one; a notable NPC who survived the same ordeal carries the same mark. Goal-reading (Section 3.5) stays the one deliberately player-exclusive instance, per Section 1's small set of player-specific abilities; most perks aren't reserved that way.

---

## 13. Economy & Attrition

### 13.1 Pemmican & Supply Chains
Destroying a major pemmican supply should be able to cause real starvation at specific posts, and real deaths among staff/voyageurs/family there — not just a resource number going down. The same tension exists at the individual/party scale in Section 17.4.

**A post doesn't jump straight from fine to lost.** Tier 0 hunger (Section 3.1) escalates through a real sequence of fallback goals first: rationing, emergency fishing or foraging (inner bark, roots — genuinely marginal but real historical famine foods), sending out an unplanned hunting party regardless of season or risk, and, as a true last resort, eating draft animals. Each step trades against some other resource (time, safety, future capability), which is what makes a starving post read as a cascade of real decisions the player or a delegated subordinate could have intervened in, rather than a meter silently crossing zero.

### 13.2 Manpower Attrition & Replacement Pool
- Lost personnel are **not automatically replaced**. Replacements have to physically travel from Montreal (NWC pipeline) or Britain (HBC pipeline via ship), using the same vulnerable logistics network as everything else — meaning a replacement party's canoe/boat can itself be intercepted and slaughtered by a war band.
- There is a **finite pool of available people** in the metropole (Montreal/Britain) — per Section 11.1, this pool is an abstract count of potential recruits, not simulated people, until someone is actually mobilized and starts traveling. Heavy losses deplete the count, making replacements slower and scarcer over time — a genuine attrition mechanic.
- **The pools aren't one generic well** — HBC draws on Orkney Islanders, Christ's Hospital apprentice clerks, and Selkirk's own recruiting stream; NWC draws on Lower Canada parishes, kin networks, and (after 1813) former Pacific Fur Company men (Section 10.7). Refill rate depends on each company's finances (Section 13.7) and on a lagged reputation effect — news of frontier deaths has to actually travel home through the ordinary propagation system (Section 10) before it slows recruitment, rather than a separate morale stat.
- This mirrors real history: the 1818–21 trade war bled both companies so badly in money and manpower that exhaustion, not any single battle, is a major reason the merger happened at all. A player who leans hard into supply-line warfare could be **mechanically causing** the historical endgame rather than just waiting for a scripted 1821 cutscene.

### 13.3 Debt & Credit
The trade ran on it historically — companies extended credit against future furs, and debt was a real coercive tool over trappers and freemen. Extending credit builds short-term loyalty and dependency but risks default; a trapper buried in debt is a real target for a rival company offering to forgive it, giving company rivalry (Section 7.1) an economic weapon distinct from violence (Section 15) or information (Section 10). It also gives Simpson's 1821 restructuring (Section 4.1) real teeth if part of what he does is call in debts a predecessor let slide — the merger stops being only a political event and starts being something that happens *to* specific named people (Section 11.1) the player knows.

### 13.4 Alcohol
Historically central and genuinely dark: trade liquor bought favorable terms in the moment at real, lasting cost to the people it was traded to, and the companies themselves argued over banning it for exactly that reason. Under the unified agent model (Section 11.1), an NPC can develop an actual addiction — a persistent condition that reshapes their goals (Section 3.1) and behavior, not a flavor-text warning — which means a single trade decision can produce a named grudge that outlives the transaction, the same shape as a suppressed secret (Section 10.4). It should also feed faction standing (Section 10.5) directly: a pattern of alcohol trading with a community is exactly the kind of slow, cumulative harm that should tank standing without any single dramatic reveal.

**Trade liquor can be diluted, and diluting is a real, discoverable gamble, not a free profit lever.** Mixing pure spirits with water, tobacco juice, or pepper at a post — a crafting action like any other (Section 12.6) — stretches a limited supply into far more trade volume, but a wary trading partner can run a Fire Test: a splash on the fire flares if the proof is real and smothers it if it's been cut. Getting caught is a witnessed event like any other (Section 10.2), and one that should land hard on faction standing specifically because it's a betrayal of trust in the middle of the trade relationship itself, not a background statistic.

### 13.5 Disease & Epidemics
Disease — smallpox above all — is one of the defining, devastating forces of this actual period and region, and deserves the same weight as the darkest systems already in this document (Section 9's death/captivity/slavery), not a background stat drain. Mechanically, it can reuse machinery already built rather than needing an entirely new system: an outbreak spreads through contact the same way fire spreads through flammable material (Section 18.4) — agent-to-agent instead of tile-to-tile, faster where people are close together (a post, a rendezvous, a winter camp), slower where they're not. Under the population model (Section 11.1), an outbreak reaching a specific band is not an abstract percentage — it can genuinely sicken and kill named people the player knows, and a nation devastated by it has its standing, territory, and role in the faction web (Section 7.2) actually change afterward, not as a scripted event but as a real consequence rolling forward through every system downstream of population (goals, marriage availability, trade). Because this reproduces real historical trauma rather than an invented fail-state, it needs a deliberate decision — separate from the mechanical design — about how much weight, visibility, and player agency (warning a community, refusing to facilitate contact, being blamed regardless) it carries, rather than being tuned only for gameplay balance.

**Disease can travel in a trade good, not only in a person.** A contaminated blanket or garment (Section 12.5's item model) can carry a contagion the same way it already carries weight or warmth — meaning a single trade transaction, not just proximity, can start an outbreak, historically one of the darkest and most consequential vectors of this exact period. Whether this happens by accident or by design is exactly the kind of choice the paragraph above already flags as needing deliberate, careful treatment.

**Quarantine is a real, costly choice, not an automatic response.** A post or camp can declare a hard blockade against travel and trade with an infected area — reusing the Accord framework (Section 3.8.2) between neighboring institutions, or simply standing as a unilateral policy — and honoring it against a genuine supply need (Section 13.1) versus smuggling goods through anyway is a real dilemma with real consequences either way.

### 13.6 Fur Market & the Continental Blockade
Furs aren't sold at a fixed price. Every pack carries a **quality rating** — the seasonal pelt-quality idea already in Section 14.4/16.2, now a formal, tradeable attribute — and quality-weighted stock converts into capital at a periodic, abstracted market clearing rather than a simulated per-lot auction, timed to the same annual cycle as the message system (Section 10.1). London can also choose to hold furs off the market when prices are low rather than sell at a loss (Section 10.7) — the clearing is a real decision point, not an automatic conversion.

**External demand shocks are the one deliberately scripted layer**, treated like weather rather than agent behavior, because nothing in Rupert's Land could plausibly cause or prevent them: the Continental Blockade (which historically cut HBC's fur exports to Europe by roughly 70% between 1800 and 1807 and triggered a real crisis in 1809–10), the War of 1812, and Congress's 1816 exclusion of foreigners from the American trade (Section 7.3, Section 10.7) all sit on a fixed external calendar. Prices derive from that calendar; everything downstream of it is agents and ledgers.

**The blockade hits companies asymmetrically, not as a flat tax:**
- **HBC** is hurt worst — its furs are entirely committed to the London ship route (Section 10.1.2, Section 5.1, Section 10.7), and the blockade disrupts exactly the England-to-Europe leg that route depends on.
- **NWC** is hurt less — some of its furs move out through Montreal and New York instead, sidestepping the blockaded route (Section 10.7).
- **Americans** are unaffected — outside the British blockade situation entirely, so Astor and the St. Louis operators (Section 7.3, Section 10.7) trade freely, a real if background advantage during the blockade years.

**Flooding the market is a real, double-edged weapon.** Selling too large a volume in a single year crashes the price for everyone that year — usable offensively, dumping stock to crush prices right when a cash-strapped rival most needs revenue, at the cost of the flooder's own returns too. Doing this across several consecutive years compounds: the market can't recover fast enough between gluts, and the pressure to keep supplying it drives real overharvest against the finite spawn-site ecology (Section 16.4) — supply and price collapse together. This gives Section 13.7's solvency mechanic, and the "exhaustion, not any single battle, caused the merger" thesis it's built on, a lever a player can actually pull rather than a background truth the game just asserts.

### 13.7 Company Solvency & the Path to 1821
Each company runs a **shared solvency ledger**: capital and available credit, unsold fur stock, outstanding debt (Section 13.3), and running costs including legal fees and violence losses. Trade-war attrition (Section 13.2) feeds directly into it, and it in turn caps how large an outfit can be ordered and how many replacements can actually be hired (Section 13.2, Section 10.7) — a company running low doesn't just feel poorer, it becomes literally unable to fully supply or restaff its own posts next season.

**A company nearing insolvency doesn't just collapse the moment its ledger crosses zero.** The same leadership agents already deciding the yearly directive (Sections 3.8, 10.7) have their own escalating fallback moves first: emergency austerity (voluntarily closing a marginal post rather than losing it to attrition), calling in debts harder (Section 13.3), seeking a bailout or extended credit from the metropole, or cutting wages and rations. These are real decisions made by real agents under the same goal/action framework as everything else (Section 3), not a scripted recovery arc — a company that mismanages its own crisis response can still fail for it.

**This is where the 1821 merger comes from mechanically, not from a date.** Historically, mounting legal costs wore down both companies, each tried to buy the other out, HBC prevailed on deeper pockets and political connections, and by 1818 disaffection with Montreal's leadership was already driving desertions to HBC and to American rivals. The model reproduces the mechanism: when both ledgers cross an exhaustion threshold, the two councils (Section 3.8, Section 10.7) negotiate a merger, on terms shaped by whichever side is less depleted. As Section 4.4 lays out, this is tuned to land close to 1821 under expected play, but the crossing itself is real and simulated, not scheduled — 1821 is where history put it, not a timer this design copies. Mechanically it's the largest instance of a negotiated Accord (Section 3.8.2): the same generic object as a peace treaty or a trade deal, just with corporate-merger terms.

### 13.8 Founding a New Post
Every post on the map currently has a founding date (Section 6.1) — none of them let the player start one, which this closes. Founding a post is a Tier 1 ambition goal (Section 3.1) like any other, decomposing into the same kind of chain Section 3.2 already assumes rather than a bespoke system:

1. **Identify a viable site** — requires either exploration (Section 10.8) or existing knowledge of what makes a location worth it: a good hunting ground (Section 16.4), a trade route junction, or deliberately contested ground already worked by a rival.
2. **Secure approval and funding** — for HBC, an indent-like petition to London (Section 10.7) that waits on the normal read-event delay and can be refused or scaled back if the company's solvency ledger (Section 13.7) is already stretched; for NWC, an actual agenda item at the Fort William council (Section 3.8.1), competing directly against every other district's claim on the same year's outfit — a new post is the same zero-sum allocation fight as everything else the council decides, not a special case.
3. **Build it** — a construction chain drawing on materials and labor (Section 12), producing new tiles with the same wall/roof/flammability properties as any other structure (Section 18.1/18.4) from the moment the first wall goes up, which means an under-construction post is genuinely vulnerable — to a raid before it can defend itself, or to an Indigenous nation objecting to the encroachment outright (Section 7.2).
4. **Staff it** — draws on the same finite recruit pool as every existing post (Section 13.2), so expansion is a real tradeoff against reinforcing what's already there, not free growth.

Once founded, a new post is simply a new location running the same simulation as every other one — its own ledger, its own agents, its own exposure to fire, ambush, and ecological depletion around it. Expansion is a real strategic choice with a real cost, not a checkbox unlocking a new building on a menu.

---

## 14. World Simulation — Weather, Seasons, Day/Night, Wildlife

### 14.1 Seasons
A season model that maps directly onto the operating calendar already built into the logistics/message system:
- **Spring breakup (ice-out)** — water routes open, brigades depart, goose hunt begins.
- **Summer** — main trading/travel season; bison summer hunt; Fort William rendezvous (July).
- **Fall** — second bison hunt; fall goose hunt; last brigades before freeze-up; beaver fur begins "priming" for winter.
- **Freeze-up / Winter** — water routes close, travel shifts to dog sled/snowshoe; the main trapping season (best pelts); posts become isolated and have to live off what the last ship/brigade delivered.

**Freeze-up and breakup aren't instant toggles — each is a real multi-week transition with its own water state** (open water → slush/frail ice → solid ice, and the reverse in spring: solid ice → rotting ice → breakup flood), tracked per waterway rather than map-wide, since a wide river and a sheltered creek don't freeze on the same day.
- **The Autumn Scramble** — late fall becomes a real race: a brigade still on the water when a waterway crosses from frail ice to solid risks being stranded mid-transit, days from any shelter. This is exactly the kind of stake travel mode (Section 18.3) already exists to surface — an incoming freeze is another automatic drop-to-live-fidelity trigger alongside a storm.
- **The Spring Lockout** — rotting ice is too weak to cross and too solid to paddle through; both canoe and sledge travel become genuinely impossible for the transition window, not just slower. Trade and courier traffic (Section 10.1) actually stop, which is a real, physical reason posts and the message system already run on stale information (Section 10.5) beyond the seasonal ship cycle.
- **Emergency conversion is the fallback, not a dead end.** A brigade caught by an early freeze doesn't just fail — per the graceful-degradation principle (Section 19), it can cache its canoe and cargo (Section 12.5) and convert to dog sledge or snowshoe travel to reach shelter on foot, at the cost of speed and whatever had to be left behind.

### 14.2 Weather
Should feed directly into systems already defined rather than being cosmetic:
- Storms/blizzards delay couriers and brigades (extends message transit time — ties into Section 10) and can force an unplanned live-fidelity stop during travel mode (Section 18.3).
- Fog/heavy snow affects visibility for interception and stealth encounters.
- Deep winter reduces game availability, raising the stakes of stored food (pemmican, salted goose, dried fish) — and directly raises hunger/exposure risk (Section 17).

### 14.3 Day/Night Cycle
- Affects stealth and interception opportunity (raiding a courier or post at night vs. day).
- Historically, canoe brigades often rose well before dawn and paddled for hours before breakfast — a realistic travel rhythm worth modeling rather than a flat day/night toggle.
- Daylight length should vary by latitude/region — extreme swings at Hudson Bay/Churchill (long summer days, brutal short winter days) vs. more moderate swings at Red River or the Columbia — a natural way to make the World & Regions (Section 6) feel distinct.

### 14.4 Wildlife
Species should be tied to region (Section 6) rather than one universal roster — this is as much a regional-identity tool as terrain is. See Section 16.2 for the full breakdown by region. Population tracking for huntable species — spawn sites, exhaustion, predator packs — is in Section 16.4.
- **Bison** — central to the Métis path and plains economy. Historically hunted roughly twice a year (early-summer and fall), organized with real hunt-captain discipline, feeding directly into pemmican production. Herd location/movement could be modeled as a shared, contested resource between Métis, Cree, Assiniboine, and Blackfoot hunting parties — a small territorial-conflict layer of its own.
- **Moose, elk, woodland caribou** — the Shield/boreal corridor's game, hunted solo by stalking/tracking rather than communally (Section 16.1).
- **Wolves** — follow game/herds; pack behavior more visible and aggressive in winter when prey is scarcer. A wilderness-hazard system for isolated travelers and posts rather than a faction.
- **Beaver** — the actual economic engine. Pelt quality/value should be tied to season, with prime winter pelts worth meaningfully more than thin summer ones — trapping-season timing becomes a real economic decision, not flavor.
- **Geese** — migratory, and one of the best-documented parts of HBC post life: the spring and fall goose hunts were organized shoots that entire Bay posts depended on for their meat supply (salted or frozen to last the season). A poor goose hunt genuinely threatened a post's food security historically — there's a recorded case of a bad 1816 goose hunt causing real hardship at a Bay post. A post's food security can be genuinely on the line during the fall goose hunt window, compounding with any pemmican shortfall from the plains side.
- **Fish, including salmon runs** — a subsistence supplement everywhere via netting/weirs and winter ice-jigging, but a full food *economy* on the Columbia (Section 16.2), tied to Chinook and other river nations.

---

## 15. Combat

### 15.1 Two Sources of Combat
Per Section 2, encounters are never a scripted trigger — they come from two distinct simulated origins:
- **Ambush/interception** — built on the courier/portage systems (Sections 10.3, 12.2): a real party waits for and hits another real party that's actually traveling that route.
- **Party collision** — two independently-moving, goal-driven parties whose paths cross. A Sioux hunting party is actually hunting; a war party is actually raiding. If the player is traveling with, or kin-bound to, a party whose path crosses a hostile one, a fight is a possible or likely outcome — not a scripted trigger.

### 15.2 Obligation and De-escalation
Whether a fight can be avoided depends on who the player is with and what's already in motion, not on a universal "talk it down" option:
- **Bystander/guest** (traveling with a party the player isn't kin-bound to): more room to hang back or disengage from a collision, at a real but survivable social cost.
- **Kin-bound** (married into the band, per the Nor'West track in Section 8.1): expected to fight — refusing costs real standing with that specific family/nation, feeding the faction trust rollup in Section 10.5.
- **Scale governs whether de-escalation exists at all**: a chance meeting between two small hunting parties can be talked down (a suspicion/diplomacy check). A war party that already committed to a planned raid is already a battle before the player arrives — the only choices left are how to fight, not whether.

### 15.3 Weapons and Lethality
- Era-accurate: trade muskets/fusils and pistols are effectively one-shot weapons — a real hit is fatal or crippling, and reload takes real time, so a musket fight is "one good shot, then it's knives and tomahawks." Bows offer faster follow-up at shorter range. Knives, tomahawks, and clubs are the close-quarters finishers.
- Hits resolve by **location and severity**, not a health bar — a torso/head hit kills or incapacitates; a limb hit or graze becomes a wound that carries forward into Section 17 (infection risk if left untreated).

### 15.4 Group Morale
A party's collective morale can break — early losses or a fallen leader can cause a rout rather than a fight to the last member, matching how Seven Oaks actually played out (minutes, not a long battle of attrition). This also means the player rarely needs to defeat a "full" enemy roster to end an engagement.

### 15.5 Non-Lethal Outcomes
Surrender, bluff, and retreat need to be real options given permadeath (Section 9) — especially early-game, when the player is one person against a party. A lost fight can resolve into captivity or slavery (Section 9.1/9.2) instead of death.

---

## 16. Hunting

### 16.1 Three Verbs, Not One System
- **Communal/mounted (bison)**: hunt-captain discipline, formation riding, a fast mounted chase-and-shoot phase. Breaking formation early for a personal kill risks spooking the whole herd and endangering other riders — individual gain vs. collective yield, a tension felt hardest on the Métis path (Section 5.3).
- **Solitary/stalking (moose, elk, woodland caribou, bear)**: tracking and stealth rather than formation discipline — the opposite design space from the communal hunt, giving two genuinely distinct hunting verbs instead of a reskin.
- **Post-based/seasonal (geese, fish)**: a hard seasonal window tied to Section 14.1's calendar; missing it — bad weather, getting pulled away by other events — can genuinely threaten a post's winter food security, as with the historical 1816 case.

### 16.2 Species by Region
Ties hunting directly to Section 6's regional identity rather than one universal animal list:
- **Shield/boreal corridor**: moose, elk, woodland caribou, black bear.
- **Plains**: bison, pronghorn.
- **Hudson Bay lowlands**: barren-ground caribou, ptarmigan — thin margins, matching the region's bottleneck character (Section 5.1).
- **Rocky Mountain passage**: mountain sheep/goat, elk, bear.
- **Columbia/Oregon**: salmon runs — a distinct food economy (weirs, seasonal abundance rather than a hunt) tied to Chinook and other river nations, worth its own mechanical treatment rather than folding into generic fishing.

### 16.3 Trapline Territory & Poaching
Beaver trapping (Section 16.1) doesn't happen in a vacuum — good grounds are finite and get worked repeatedly. A rival trapper, company or Indigenous, working the same beaver grounds is a slow-burn friction source distinct from open war (Section 15): poaching a line, or being poached, is the kind of thing that shows up as an unexplained sighting in the propagation system (Section 10.4) long before it becomes a fight, and can escalate through the faction trust rollup (Section 10.5) the same way any other pattern of grievance does. Under the spawn-site model (Section 16.4) this friction needs no extra system: two parties working the same finite site is already a conflict.

### 16.4 Population Dynamics & Spawn Sites
Every huntable species in a region isn't one abstract number — it's anchored to a set of discrete **spawn sites** placed at real locations in that species' habitat: a beaver lodge, a wolf den, a goose nesting ground, a caribou wintering yard. A region's total population for a species is just the sum of its sites.

**Per-site state**: its own local count; its own carrying capacity; a natural regrowth rate; and, separately, a **breeding-capacity value** representing the physical structure itself (the lodge, the den), distinct from the animals currently occupying it.

**Hunting draws down a specific site** — whichever one someone is actually working: the player, a post's assigned men, a Cree band, a rival trapper. Two parties pulling from the same finite site is already conflict, so Section 16.3's poaching friction falls out of this with no extra system. It also gives hunting real spatial stakes: hammering one lodge can wipe it out while one two miles downstream is fine, instead of one number everyone is implicitly watching together.

**Two failure modes, both causal:**
- **Overharvest** — hunting a site below a sustainability floor kills the breeding stock itself, not just passing individuals, which destroys the site's breeding-capacity value along with it. The lodge collapses, the den goes cold, and that location stops producing — permanently and discoverably, like a burned post (Section 18.4) or an exhausted pemmican store (Section 13.1). It's a real change to that patch of map, not a number resetting next season. This is the exhaustible-spawn requirement: the spawn can be destroyed.
- **Overpopulation** — left alone too long, a site's count exceeds its carrying capacity, its local food depletes faster than it regrows, and it enters a **scarcity state**: more animals than ever, but hungry ones, ranging toward the nearest post looking for food. That is the "attacks" behavior, earned by real hunger rather than an aggro flag.

**Resting a site is the escape hatch before overharvest actually lands.** A site showing visible signs of nearing its sustainability floor — thinning numbers, reflected in a factor's or hunter's own beliefs about it (Section 3.9) — can be deliberately hunted lightly or left alone for a season, trading short-term yield for keeping it alive. That's a real conservation-versus-exploitation choice available to any agent working the site, the player included, not a warning the game gives only to prevent the player specifically from losing it.

**Predators** layer on top as their own population, tracked as **roaming packs** rather than individuals — so a pack can actually be encountered as a party (Section 15.1) — whose growth tracks prey availability across nearby sites. This gives a symmetry: overhunt a region and the wolves go hungry and turn toward the post; leave it alone and the prey does the same. Both extremes converge on the same danger from opposite causes.

**Knowledge**: sites aren't known by default. Finding one is scouting, or someone who already knows telling you, which reuses Section 10's knowledge system directly. A good lodge location is exactly the kind of thing worth someone's favor to share or sit on.

**Reuse, not a fourth system**: this recombines three pieces already in the design — a destroyable object (Section 18.4), a resource that stays gone once spent (Sections 13.1/13.2), and knowledge that has to be learned (Section 10) — which matches Section 19's one-generic-system principle.

**Promotion to agents.** Individual animals are promoted out of a site's count into full named agents when the site enters the live or local tier (Section 18.2). A promoted animal that lives keeps being an agent — when the player moves off, it drops to the background tier like any other agent — and one that dies is culled from processing.

*Bookkeeping (proposed)*: each promoted animal carries a home-site ID, and a site's effective population is its abstract remainder plus its living promoted agents, so nothing is double-counted. A kill removes the agent and decrements the site. A per-region cap on persistent promoted animals keeps them from accumulating over a long game (Section 20).

*Proposed, not yet decided*: the sites themselves are simulation state like any other and would resolve at the tier cadence of wherever they sit (Section 18.2) — a background-tier site regrows and drifts on the weekly tick and only becomes fine-grained when someone is working it.

### 16.5 Herd Movement & Multi-Faction Contest
Migratory species — bison above all, but also geese and caribou (Section 14.4) — need one addition to the spawn-site model rather than a separate system: a site's location can move on a schedule instead of staying fixed. Everything else about a site (count, carrying capacity, regrowth, breeding-capacity, overharvest/overpopulation) applies unchanged; sedentary species (beaver lodges, wolf dens) simply keep a permanently fixed position.

**A herd is one mobile site** (or a small number per region), migrating along a seasonal route tied to Section 14.1's calendar — toward summer range and back for winter, roughly as history records, with enough drift that it isn't a fixed timetable.

**Contest falls out of the existing model, not a new one.** Section 16.3 already established that two parties working the same finite site is conflict; a migratory herd just means that finite site is shared across a whole region instead of one fixed spot, so a Métis brigade, a Cree band, and a Blackfoot hunting party can all legitimately be pursuing the *same* herd at once — organized bison hunting was serious business for all of these nations historically, not a Métis exclusive, and Section 16.1's communal-hunt model applies as readily to a Plains nation's hunt as to a Red River brigade's. Whoever reaches the herd first, or hunts it hardest, draws down what's left for everyone else — the same zero-sum logic Section 16.3 already gives trapline poaching, now playing out at herd scale. And because these are independently-moving, goal-driven parties (Section 2), two rival hunting parties converging on the same herd is exactly the party-collision case in Section 15.1 — no separate mechanic needed for the friction to turn into a fight.

**Knowledge decays for a physical reason here, not just a social one.** A herd's location is knowledge like any site (Section 10, Section 16.4), but a scout's report goes stale faster than an ordinary rumor — not because the *telling* degraded (Section 10.4), but because the herd has physically moved on since. Same shape of rule as everywhere else in the design, driven by geography instead of gossip.

*Open*: whether sustained heavy pressure on one part of a herd's range should be able to fragment it into two smaller herds, or just push the whole herd's route further off, is left for tuning (Section 20).

---

## 17. Survival — Hunger, Thirst & Exposure

### 17.1 Hunger
Caloric burn scales with season and activity — deep winter and heavy travel (paddling, portaging, riding) cost real calories, matching the historically enormous voyageur diet. Low hunger degrades physical capability (weaker in combat, slower travel, worse hunting accuracy) before it becomes lethal — a visible spiral, not a sudden death.

### 17.2 Thirst
This region isn't water-scarce, so the real mechanic is **access cost**, not scarcity: trivial in summer, but winter means stopping to melt snow/ice, which costs time and fuel, and a party caught on the open plains or mid-chase can hit a real short-term crisis. Mostly a minor constant tax that spikes under specific conditions rather than a constant threat.

### 17.3 Exposure & Clothing
The meatiest of the three, and where clothing stops being cosmetic and becomes a real gear system:
- Layers — capote coats, wool trade cloth, hide/fur garments, HBC point blankets — each carry a warmth rating.
- **Wet clothing loses most of its insulation value**, which matters enormously given how much of the game is spent in canoes, crossing rivers, and portaging through cold water. This creates a real risk/reward: the fast route across thin ice or a ford is quicker but risks a soaking that turns a survivable cold night into a lethal one.
- Fire and shelter are the countermeasures, and they're not the same thing: a fire (Section 18.4) is a real object that radiates heat with falloff — closer is warmer — while walls and a roof (Section 18.1) trap that heat and block wind rather than producing warmth on their own. Real shelter is the combination: a fire burning inside a sealed structure. An unheated stone building in a blizzard is still cold; an open campfire in the wind still loses most of its heat to the air. A blizzard with neither should impose escalating, genuinely lethal cold damage, not a flat stat debuff.

### 17.4 Interlocks
- **Food storage** (Section 13.1): fresh-killed meat solves hunger short-term but spoils; pemmican/dried stores are the hedge against a bad hunt or a storm — the same strategic tension the game already models at the post level, mirrored at the individual/party scale.
- **Combat wounds** (Section 15.3): an untreated wound compounds badly with cold and hunger — a wounded character traveling in the cold without proper care is at meaningfully higher risk than either condition alone, which is what makes a fight's aftermath dangerous, not just the fight itself.

### 17.5 Treating Wounds
A wound (Section 15.3) isn't just a stat until it's addressed — treatment is its own action with real, historically distinct approaches rather than one generic "heal" button:
- **Indigenous herbal medicine** — poultices and plant knowledge tied to a particular band or family, often more effective and lower-risk than the European alternative for the period, with access running through the same relationship/kinship channels as everything else (Section 8, Section 10) — a band has to be willing to treat you.
- **European treatment** — bleeding and amputation, the standard company-post response: genuinely riskier by comparison, but available without needing outside kinship standing.
- A **medicine** skill (Section 3.10) governs how well either approach is applied, whether by an NPC treating the player or the player treating someone else — feeding directly into whether a wound heals clean, leaves lasting impairment, or turns fatal through infection.

### 17.6 Isolation & Winter Strain
A sixth month locked inside a cramped post with the same dozen people is its own kind of survival pressure — a psychological Tier 0-adjacent need (Section 3.1), alongside hunger, thirst, and exposure, that builds from isolation, monotony, and crowding rather than from cold or calories.

**What builds it**: short winter days (Section 14.3), a small, unchanging population (Section 11.1's scene-level counts are exactly small enough for this to bite), food monotony, liquor rationed too tightly (Section 13.4), and a long gap since the last real news (Section 10.1's annual message cycle already guarantees this gap exists).

**Sociability (Section 11.4) sets susceptibility, not the outcome.** A highly sociable agent needs more real contact to stay stable and suffers faster in a quiet, undersized post; a low-sociability agent tolerates isolation better but contributes less to the group activities that would help everyone else. The post's actual mix of personalities determines how much strain accumulates, not a single number anyone controls directly.

**It degrades before it breaks anything, per Section 19's graceful-degradation principle**: irritability and friction first, then real conflict, then desertion attempts or a genuine mutiny in the worst case — an escalation ladder, not a hidden meter that suddenly ends the game.

**Countermeasures are a Factor's real administrative lever** (Section 3.9), on par with provisioning and trade rather than flavor: liquor rations, storytelling around a shared fire (the same hearth already doing double duty for warmth, Section 18.4), organized hunts to break the monotony and vary the food, and set-piece celebrations — a New Year's dance, fiddling — that cost real resources specifically to spend them on morale instead of returns. This gives the quiet middle of a long winter something the player and the post's leadership are actually managing, not just enduring.

---

## 18. World Architecture — Open World, Simulation Fidelity & Travel Mode

### 18.1 One Continuous World
No loading screens, and no separate interior/exterior spaces. A fort is tiles with properties — walled, roofed, flammable/non-flammable (Section 18.4) — not a different scene. Walls and a roof block wind and precipitation and are what being "indoors" mechanically means, plugging directly into the exposure system (Section 17.3); they don't generate warmth themselves, they trap what an actual fire (Section 18.4) is producing nearby. A fort exists at its map location the whole time — walking up to it, or away from it, is a continuous movement, never a load, and everyone inside keeps living (Section 18.2) whether or not the player happens to be there to see it.

### 18.2 Concentric Simulation Fidelity
The spatial version of the same idea already used for off-screen NPC ticking (Section 11.1) — fidelity scales with proximity/relevance, not a hard boundary. There are three tiers; existence is identical across all of them and only update frequency differs:
- **Live bubble** (level 1 — the visible area plus a margin around it): about 100 agents at full tick-rate simulation — physics, pathing, individual combat/hunting rolls, real-time weather effects.
- **Local tier** (level 2 — the larger immediate context): a few hundred agents (the rest of a fort's population, a brigade a day off) on an hourly/daily tick, at reduced resolution but still spatially real, resolving seamlessly into the live bubble on approach — no snap, no load.
- **Background tier** (level 3 — everyone else): the bulk of the world (~15k+ agents) on a weekly tick, **staggered across real time** so the whole population is never resolved in one frame. Distant agents (a brigade weeks out, a courier crossing the map, a war party assembling elsewhere) are updated via travel-time math and lazy resolution — a whole week of goal, weather, and terrain collapses into one calculation the moment something asks. They are still real, positioned, moving agents.

Counts and cadences above are planning estimates to be tuned against profiling, not measurements.

**Promotion and demotion are staggered, not spikes.** A background party walking into local range, or a rendezvous suddenly needing hundreds of agents live, has to be spread across frames/ticks rather than promoted in a single frame. The discipline is the same in both directions: fine-grained processing stays strictly gated to the tier an agent is currently in.

This is what makes Section 11.1's "everyone is a full agent" affordable at open-world scale: existing as data is cheap, and fidelity-by-distance keeps the expensive part — frequent planning and pathing — to the few hundred agents that need it. It's also what generates encounters and ambushes honestly (Section 15.1): the ambushing or colliding party has to have actually simulated its way to that point, not been conjured on approach.

**Bloat guard.** Two reference points bracket the risk. Crusader Kings routinely runs thousands of real, named characters because most are dormant data until something asks them to think. Dwarf Fortress's well-known slowdown is usually pinned on too much full-detail simulation and unpruned history accumulating without tiering hard enough as the world ages. Tier gating (this section) and memory decay (Section 10.4) are two halves of the same discipline.

### 18.3 Travel Mode
- Player-initiated when setting out (loading the canoe, mounting up, starting the trail). The camera pulls back and time compresses, but the party remains a real, positioned token on the real map — not a route line or a menu.
- What still ticks, abstracted: hunger/thirst/exposure resolve per travel-day (matching the real voyageur dawn-paddling rhythm) rather than per-step; distance covered depends on transport mode and vessel capacity/crew (Section 12's table), so an overloaded canot du nord is visibly slower, not just numerically.
- **Automatic drop to live fidelity**:
  - **Portages, always** — Section 12.2's interception tension needs full control the moment you're there, never an abstracted pass-through.
  - **An encounter** — a background-tier party promoted into range (Section 15.1).
  - **Marginal end-of-day conditions** — low supplies, an incoming storm, or wet gear force a real choice (make camp here vs. push on, spend the last dry wood vs. ration it); a fine day auto-resolves as part of the compressed tick.
  - **Entering any fort/post tile** — needs live fidelity to interact with anyone there.
- Manual drop-out is always available — it's a zoom level, not a locked mode.
- The design property worth keeping: every automatic trigger is already a place the design puts real stakes, so the zoom level itself becomes a signal — if you're still zoomed out, nothing that matters is currently happening.

### 18.4 Fire — A Simulated Object, Not a Tile Flag
Fire is a real object that sits on a tile and does things over time, not a boolean "sheltered" flag:
- **Fuel and intensity**: a fire carries a fuel supply that burns down; feeding it sustains or grows it, leaving it untended lets it die out. This makes a hearth something a household actually has to tend, the same way winter travel has to budget fuel to melt water (Section 17.2).
- **Heat radiates with falloff**: warmth is strongest close to a fire and weakens with distance, so the corner of a room is colder than the spot right next to the hearth. Walls and a roof (Section 18.1) don't create warmth — they trap what a fire is producing and block the wind that would otherwise carry it off, which is why real shelter is a fire burning inside a sealed structure, not either one alone.

**Material flammability** — not everything can burn:
- **Non-flammable**: stone, packed earth, water, snow-covered ground.
- **Flammable, with different behavior**: dry wood (log walls, palisades, furniture) catches and sustains a structure fire; green or rain-soaked wood resists ignition, echoing the wet-gear penalty in Section 17.3 applied to fuel instead of clothing; dry prairie grass (Section 6) is extremely flammable and spreads fast on its own, a real historical hazard independent of any building.

**Spread** — fire propagates tile-to-tile and object-to-object based on adjacency, the target's flammability, and current conditions: wind (Section 14.2) pushes it faster in one direction and can carry embers across gaps, rain or wet snow suppresses it, a firebreak (cleared or non-flammable ground) stops it outright. Fire is dangerous and containable by the same physical logic in both directions, rather than a scripted "building burns for N seconds then disappears."

**Consequences**:
- **Structures can actually burn down.** This gives real mechanical teeth to "rival posts burned" already in the historical timeline (Section 4.1, 1818–1820) — arson becomes an available raid option (Section 15) alongside interception and party collision, and one with much larger, longer-lasting consequences than a fight: a burned post is a burned food and fuel store (Section 13), not just a burned building.
- **Accidental fire is a real background hazard**, not only a player tool — an untended hearth during a dry, windy spell can genuinely spread through a wooden post. Nothing needs to script that; it only needs fuel, an ignition source, and the right conditions to actually occur, per Section 2.
- **Fuel becomes its own stockpiled resource**, mirroring the pemmican/food-security tension in Section 13.1 — a post needs a woodpile going into winter the same way it needs dried food, and a bad autumn (rain, being unable to send a wood-gathering party out) can create a real fuel shortage independent of the food situation.

---

## 19. Architecture Notes

- Build **one generic system** for each mechanic (messages, logistics/weight, memory/rumor propagation, goal decomposition, simulation fidelity, fire/flammability, spawn-site ecology, company solvency) and **specialize with per-faction, per-material, or per-context parameters** rather than bespoke logic per case. Where factions differ structurally, specialize the *generator* as well as the numbers: HBC's Tier 2 comes from a one-way London order, NWC's from a Section 3.8 council (Section 10.1).
- **Messages take effect on a read event, not on send** (Section 10.1.1). That one rule is what lets stale orders, garbled secondhand plans, and interception all fall out of the same object.
- There is no lightweight "generic NPC" tier — see Section 11.1. Full agent simulation is affordable because *existing* as data is cheap (~1–2 KB per agent, an estimate) and *update frequency* is tiered by distance and relevance (Section 18.2), not because some characters are cheaper than others.
- The goal/action framework (Section 3) is the mechanism that makes "simulate, don't script" (Section 2) concrete: goals decompose into reusable chains, chains bottom out in one shared action vocabulary used by player and NPC alike.
- The succession-on-death branch (Section 9.3.3) reuses the same relationship, memory, needs, and goal data the unified agent model (Section 11.1) already tracks for every NPC — it's the cheapest of the three death branches precisely because nothing extra has to be built for it.
- Memories/secrets **decay in weight over time unless reinforced** (Section 10.4), both for narrative pacing and to keep the system computationally tractable (see Crusader Kings' "memory" system as a reference point) — with the exception that a secret tied to an ongoing relationship refreshes to full weight on fresh contact with its subject.
- Simulation fidelity (Section 18.2) follows the same decay-without-reinforcement logic spatially: full detail near what's being observed, coarser and coarser the further away, promoted back to full detail the moment it becomes relevant again.
- **Two disciplines against bloat over a long playthrough**: tier gating (fine-grained processing only for the tier an agent is currently in, with promotion/demotion staggered) and bounded, decaying memory (capped relationship lists, Section 10.4).
- The ecology system (Section 16.4) is deliberately a recombination, not a new system: a destroyable object (Section 18.4), a resource that stays gone once spent (Sections 13.1/13.2), and knowledge that has to be learned (Section 10).
- The metropole (Section 10.7) and company solvency (Section 13.7) are the same discipline applied to the highest level of the game: a small set of always-background-tier agents and a shared ledger, rather than a new subsystem, feeding the same council mechanic (Section 3.8) and message object (Section 10.1.1) already built for everything else.
- Historical determinism (Section 4.4) follows Section 2's own test: the two anchors kept fixed (the blockade, the colony's founding) are fixed because no in-world agent could plausibly cause or prevent them, not because they're more dramatically important than anything left emergent.
- **Terminal failure states need an earlier, escapable warning state.** A starving post, an insolvent company, or an overharvested site shouldn't jump straight from fine to permanently lost — Section 13.1's subsistence fallbacks, Section 13.7's solvency crisis responses, and Section 16.4's rested-site option are all the same rule applied to different systems: give agents, the player included, a repertoire of increasingly desperate but real actions to try first, so failure reads as a cascade that could have been intervened in rather than a threshold that silently tripped.
- Injuries (Section 15.3), addiction (Section 13.4), disease progression (Section 13.5), and perks (Section 12.7) are all long-running conditions that alter an agent's goals and capability over time — a strong candidate for one generic "condition" system specialized per case, rather than four separate ones.
- **Every hidden system needs at least one learnable tell.** Section 2 commits to no ambient knowledge anywhere, but a rule that's hidden with no way to learn to read it stops feeling like mystery and starts feeling like arbitrary punishment. Goal inference from behavior (Section 3.5), personality-driven tells (Section 11.4), and visible ecological warning signs (Section 16.4's own beliefs a factor holds) are the existing instances of this; any new hidden system added later should come with its own observable tell as part of the same design pass, not as an afterthought.

---

## 20. Open Design Questions

**Carried over (some updated):**
- **1801 vs. 1811 start** — earned-reputation prologue vs. dropping straight into the Pemmican War. Section 4.1 currently leans 1811, but the HBC/NWC structure work is now scoped to the full ~1801–1821 window. An 1801 start would open with the NWC meeting at Grand Portage, its first annual meeting at the new Kaministiquia post in 1803, and the XY rivalry live until 1804 (Sections 4.2, 3.8.1) — and would also make the Red River Colony's founding a live, simulated London subplot rather than pre-game backstory (Section 4.4). Needs a decision.
- **Marriage track mutability** — can a player renegotiate/change tracks more than once, or does each transition (Section 8.3) apply every time regardless of direction already traveled?
- **Herd fragmentation** — Section 16.5 resolves the movement/contest question (a migratory herd is a mobile spawn site, contest falls out of the existing shared-finite-site logic); what's still open is whether sustained heavy pressure should be able to split a herd into two, or only push its whole route further off.
- **Exact scope of the black-market intel system** — blowback resolves as a hidden roll (Section 10.3), but what that roll should weight (source_chain length, buyer, discretion of those involved) is still open.
- **Travel mode compression ratio** — how many real-world minutes should represent one travel-day is untuned.
- **Rendezvous and major-battle scale** — Section 11.1 now targets ~10–20k named agents map-wide with only ~100 at full tick. How many named agents can be *live at once* at the Fort William rendezvous (Section 5.2) or a large intertribal battle (Section 7.2) still needs a concrete number, along with the staggered promotion that gets them there (Section 18.2). The council itself needs only a few dozen live deciders (Section 3.8.1); the open number is the local-tier crowd around them. Fort William Historical Park puts the rendezvous at perhaps two thousand people, roughly a tenth of the ~20k budget in one place.
- **Goal decomposition depth and authoring** — how deep should chains realistically nest before bottoming out in atomic actions, and how much of the reusable-chain library needs hand-authoring up front versus emerging from combining a smaller primitive set? Section 3.9 bounds nesting by organizational depth (factor → clerk or outpost head → men), which narrows this but does not settle authoring.
- **Fire spread tuning** — how easily a wooden post should actually catch and burn is a real balance question: too forgiving and arson/accidental fire never matters, too easy and it becomes a griefing tool or a source of constant unfair loss. Needs a concrete pass once other numeric placeholders (Section 9's decay rates, travel-day compression) are being tuned together.

**New — from the population, ecology, geography, and message-system discussions:**
- **Animal promotion details** — animals are promoted from a site's count to individual agents on entering the live or local tier and persist if they survive (Section 16.4). Still open: whether a site promotes its whole count or only the animals near the player, and the size of the per-region cap on persistent promoted animals.
- **Population composition** — how the ~5,000 human agents split across HBC, NWC, Métis/freemen, and the Indigenous nations (including the Southern Department's share), and what fraction of each real community is represented by named agents.
- **West edge and interior depth** — Section 6.1 proposes the Pacific coast as the west edge and only the edge of the American interior (via Astor/Lisa) as the south; neither is locked.
- **Southern Department scope** — whether the proposed separate faction-standing review through Moose Factory (Sections 3.6, 6.2) is adopted, how far around James Bay the map extends, how much it adds to the agent budget, and its species roster (Section 16.2 has no James Bay entry yet).
- **American node** — St. Paul (1841, on the map) as a forward-looking echo vs. Fort Snelling (1819) or St. Louis as an in-period source node (Section 7.3, Section 10.7).
- **Delegation tuning (Section 3.9)** — how costly refusal is for a subordinate and for the player as a clerk, how falsified account books are detected at London's read event, and how the seasonal playbooks (fall outfit, winter trade, spring goose hunt, summer freight) are authored per faction.
- **Fort William council tuning (Section 3.8.1)** — the exact voting rule (weight-by-shares is the working reading), how a Montreal firm's block is cast when its representatives disagree, how vacated shares are allocated, how tightly agenda order squeezes late items, the splinter thresholds (size of the aggrieved bloc, availability of a Montreal financier, quality of a leader) and how a splinter later merges back, and the number of deciding agents versus the local-tier crowd around them.
- **Source check on the historical structure** — the pre-1821 HBC/NWC details in Sections 4.3, 5, 6.2, and 10.1 come from a research summary in planning discussion, not primary sources. In particular, verify the pre-1821 HBC rank vocabulary (Section 5.1 treats the profit-share Chief Factor / Chief Trader ranks as 1821 creations) and whether the "Southern Department" as a named division applies before 1821 or is being applied retroactively to a Moose Factory–centered James Bay district. Share figures also vary by source — the Montreal agent firm is credited with eleven of twenty shares in accounts of 1787 but six of twenty in the text of the 1790 agreement — and the exact council voting rule was not found in the sources consulted. Wallace's *Documents Relating to the North West Company* (Champlain Society), which prints the 1802 and 1804 agreements and the 1801–1807 minutes, is the place to settle both.
- **Tier counts and cadences** — the ~100 / few-hundred / weekly-staggered figures and the 1–2 KB per-agent estimate (Sections 11.1, 18.2) are planning numbers that need profiling.
- **Per-post founding dates** — the map spans c. 1670–1870, so every post needs a date gate (Section 6.1), including Fort Vancouver (1825) versus Fort George as the Columbia anchor.

**New — from the metropole and historical-determinism discussions:**
- **Determinism tuning** — Section 4.4 sets the policy (blockade and the colony's founding fixed; the 1821 merger structurally inevitable but causally emergent; everything else open), but how strongly the underlying pressures (Section 13.7's solvency thresholds, Section 7.2's arming dynamics) need to be weighted so the merger actually converges close to 1821 under normal play, rather than drifting years off or failing to trigger at all, is untuned.
- **General Court vote weighting (Section 10.7)** — whether HBC's proprietors' votes at the general court were weighted by shares (paralleling the NWC's share-weighted council, Section 3.8.1) was not confirmed in the sources consulted.
- **NWC order-to-sale lag (Section 10.7)** — the precise delay between the July council's directive and furs actually clearing the London market (Section 13.6) is an open parameter.
- **Metropole agent counts** — the ~7–12 London Committee agents and ~30–40 total metropole agents (Section 10.7) are planning estimates, not confirmed counts.
- **Selkirk's recruiting stream** — how literally the 200-men-a-year obligation (Section 10.7) should translate into HBC's recruit pool refill rate (Section 13.2) is unset.

**New — from the skills, justice, exploration, and multiplayer discussion:**
- **Skill baseline authoring** — how exactly a personality profile (Section 11.4) maps to starting skill values (Section 3.10) — a formula, an authored table per role/background, or something else — is undecided.
- **Multiplayer live-tier scaling** — Section 21.2 generalizes the live tier to one anchor per player; the actual per-anchor and total-live-agent caps for a 4–6 player session need profiling, distinct from the single-anchor numbers tuned elsewhere in this section.
- **Consensus-skip UX** — how the "does everyone want to skip" check in Section 21.3 is actually surfaced to players (a prompt, a lobby-style ready check, a timeout default) is undesigned.
- **Multiplayer session/party formation** — whether players commit to a single shared start (e.g., all as one brigade) or start independently and simply share the same persistent world from different points, and how faction choice interacts with that, is open.

**New — from the character creation and arrival discussion:**
- **NWC and Métis/freeman arrival** (Section 5.5) — worked out for HBC; needs the same depth for the other two paths.
- **Character-creation skill-point budget** (Section 5.4) — how many discretionary points, and how they compose with the personality-seeded baseline, folds into the skill-baseline-authoring question above rather than replacing it.
- **Bag/pack capacity and the strength formula** (Section 12.4, Section 12.5) — the item model is now written; the actual numbers (base capacity by strength, specific bag bonuses) are still untuned placeholders.
- **Warehouse quality-bucketing** (Section 12.5) — whether institutional fur storage should track a single blended quality value or a small distribution of quality buckets is undecided.

**New — from the legacy, post-founding, and Accord discussion:**
- **Legacy hop-decay tuning** (Section 10.9) — how much weight a memory should lose crossing the kinship hop from a deceased agent to their heir, relative to an ordinary retelling hop (Section 10.4), is unset.
- **Accord breach severity scaling** (Section 3.8.2) — whether every broken Accord carries the same weight or scales with what was promised (a minor trade term vs. a peace treaty) needs concrete rules.
- **NPC-initiated post founding** — Section 13.8 is written from the player's side; whether NPC leaders (a Factor, a partner) pursue this same goal autonomously, and how often, is undecided.

**New — from the crafting and interface discussion:**
- **Recipe list and balance** (Section 12.6) — the actual catalog of what's craftable, its material costs, and the skill-to-quality formula all need full authoring; only the shape of the system (anywhere vs. facility-gated, crafting vs. smithing) is decided here.
- **Perk trigger list** (Section 12.7) — which specific events grant which specific perks, and whether any beyond goal-reading (Section 3.5) should also be reserved as player-exclusive, is unwritten.
- **Facility siting** — which existing posts start with a forge (Section 12.6), and whether one can be built at a new or existing post outside the founding process (Section 13.8), is undecided.

**New — from the freeze/breakup, wintering, and courier discussion:**
- **Water-state timing** — per-waterway freeze/breakup duration and how much randomness governs the Autumn Scramble and Spring Lockout windows (Section 14.1) is untuned.
- **Winter strain curve** — the actual thresholds separating irritability, conflict, and mutiny (Section 17.6), and how strongly sociability shifts them, are unset.
- **Canoe condition numbers** — how much damage a rapid, a rock strike, or a portage drop should deal, and how far a given amount of spruce gum goes (Section 12.2), need balancing.
- **Message verification** — forging a replacement dispatch (Section 10.3) presumes something makes a genuine one verifiable (a seal, a known hand); that mechanic isn't designed yet.
- **Express courier speed/risk tradeoff** — the actual numbers making an express canoe harder to catch but more valuable when caught (Section 10.3) are unset.

---

## 21. Multiplayer

### 21.1 The Player Is Already Just an Agent
Section 1 already commits to the player being a mechanically equal agent among NPCs — same needs, same goal structure (Section 3), same action vocabulary. Support for 4–6 concurrent players follows from that commitment rather than requiring a new simulation architecture: it's a matter of letting more than one agent-slot be driven by a human client instead of the goal planner, with the rest of the simulation running exactly as designed for one.

### 21.2 Multiple Live-Tier Anchors
Section 18.2's three-tier fidelity model assumed one live bubble around a single observer. With up to six players scattered across the map, the live tier gets an anchor per player (merging into one bubble when players are near each other), each still resolving at full tick-rate, with local and background tiers filling the space between them. This is a direct generalization of the existing model, not a new one — but the tuning targets in Section 20 (the ~100-agent live-tier estimate) were sized for a single anchor and need revisiting for up to six simultaneous ones.

### 21.3 Tiered Fast-Forward
Single-player time compression can't be unilateral once a second human is in the world. The rule: **if every currently-connected player wants to skip, the shared clock actually skips** — a direct generalization of the ordinary wait/sleep-to-morning mechanic to a full-consensus vote. **If even one player doesn't**, nobody's clock is forced to move faster than real time; instead, whichever players wanted to skip are automatically pulled out into personal auto-pilot — their own character keeps acting sensibly at normal shared speed (auto-traveling, auto-camping, resolving routine chains on its own, per Section 3.2) while everyone else retains full manual control. Nobody's real-time crisis is ever sped through because someone else wanted to fast-forward, and nobody who wants to skip dead time is forced to sit through it manually just because one other player is busy.

### 21.4 Disconnection Hands Off to the Goal Planner
A disconnected player's character doesn't freeze — it falls back to being driven by the same goal planner that already runs every NPC (Section 3), continuing on the character's last known goals and personality (Section 11.4) until the player reconnects and reclaims it. This costs nothing new to build: it's the same planner already driving every Factor, clerk, and hunter, just permitted to claim a slot that isn't currently held by a live connection. It also means the world stays genuinely persistent if every player logs off at once — background-tier simulation (Section 18.2) just keeps running, the same way it already does for anything far from a player.

### 21.5 What Falls Out for Free
Because every system in this document was built to not care whether the agent behind a decision is an AI or a human, most of what multiplayer needs is already covered elsewhere:
- **Mixed-faction and rival play** — players on different companies, or on the same company competing for the same promotion (Section 3.7), get combat, interception, and betrayal for free from Sections 15 and 10.3; those systems never distinguished AI-controlled parties from human ones.
- **Humans in a chain of command** — Section 3.9's delegation loop (a leader assigns, a subordinate decides against his own goals, reports flow through the propagation system) already models leader/subordinate generically; a human factor and a human clerk just run the identical loop. A group could play one company's whole chain of command, split across rival companies, or one side an Indigenous family against a trading post.
- **Shared vs. separate economies** — ledgers (Section 13) belong to institutions, not to "the player," so humans at the same post naturally share one and humans in different institutions naturally don't.
- **Group framing matches the number** — 4–6 players maps directly onto a canot du nord crew (Section 12's table: 4–6, up to 8) — "you and your crew are the brigade" is a strong default session framing alongside "scattered rivals across the map."

### 21.6 Where Permadeath Changes
Section 9.3's three death branches don't all survive a shared world intact. "New character, new world" doesn't make sense once several people share one persistent world — that branch drops out in multiplayer, leaving **succession via kinship** or **a new character in the same shared world** as the two real options for a player who dies.

---

## Appendix — Revision Notes

This revision folds the follow-up planning discussion into the document. Where that discussion contradicted earlier text, the earlier text was updated rather than left alongside it:
- **Population model** (Sections 2, 11.1, 18.2, 19, 20): the earlier "population kept small enough to afford" premise is replaced by a 10–20k named-agent target, with existence separated from update frequency, three tiers, and staggered promotion/demotion.
- **Ecology** (Sections 14.4, 16.3, 16.4, 19): new spawn-site system with destroyable sites, overharvest and overpopulation failure modes, and predator packs.
- **Geography** (Sections 4.1, 4.3, 6, 7.3): new map/boundary subsection, new Southern Department region, date-gating note, and the Williams epilogue beat.
- **HBC/NWC structure** (Sections 3.1, 3.6–3.8, 5.1–5.3): pre-1821 structures for each company; the clerk → trader → chief trader → chief factor ladder was moved out of the playable window.
- **Message system** (Sections 10.1, 3.1): rebuilt around the read event, with HBC as a monologue, NWC as a council, and London/Montreal/American nodes.
- **Animal agents** (Sections 11.1, 16.4, 20): decided — animals are promoted from a site's count into named agents on entering the live/local tier; survivors persist, the dead are culled.
- **Delegation** (Sections 3.9, 3.1, 20): new section on how a leader turns a read directive into assigned work, with HBC Factor, NWC partner, and hunt captain as parameterizations.
- **NWC council** (Sections 3.8.1, 5.2, 10.1.3, 20): new mechanics for the Fort William council as the generator of NWC's yearly directive, with share-weighted votes, proxies, and a splinter mechanic modeled on the XY Company.
- **Historical determinism** (Sections 2, 4.1, 4.2, 4.4, 19, 20): new policy distinguishing fixed anchors (the Continental Blockade, the Red River Colony's founding) from causally-emergent-but-expected outcomes (the 1821 merger) from fully open history (the colony's survival, Seven Oaks, the interior war); the 1817 Selkirk-marches-on-Fort-William beat reframed as a conditional trigger rather than a scheduled date.
- **Fur market and company solvency** (Sections 13.6, 13.7, 7.3, 20): new mechanics for pelt quality and market clearing, the blockade's asymmetric impact by company, market-flooding as a double-edged weapon tied to spawn-site overharvest (Section 16.4), and a shared solvency ledger as the mechanical source of the 1821 merger.
- **The Metropole** (Sections 10.1.5, 10.7, 13.2, 7.3, 20): new section covering London's Committee and Selkirk's causal path to the 1811 grant, Montreal's agent firm and physical yearly routine, the split of the American node into Astor/New York and St. Louis plus the 1816 exclusion law, and company-specific recruit pools with a rumor-lagged refill rate.
- **Skills** (Section 3.10): new system — shooting, hunting, trapping, foraging, talking, stealth, riding, sprinting, medicine, and others, seeded by personality (Section 11.4) and improved through use rather than a separate XP pool.
- **Vendetta & informal justice** (Section 7.4): new subsection reusing the grudge/memory system (Section 10.4) for disputes with no court to resolve them, with Indigenous kinship-based restitution as the sharpest case.
- **Treating wounds** (Section 17.5): new subsection distinguishing Indigenous herbal medicine from European bleeding/amputation, gated by a new medicine skill.
- **Exploration & the map** (Section 10.8): new subsection — geography as knowledge, revealed by walking it or by acquiring a physical, tradeable/stealable map item.
- **Multiplayer** (Section 21, Section 1): new section — 4–6 concurrent players as more agent-slots under the existing model, multiple live-tier anchors, consensus-based fast-forward with automatic per-player auto-pilot as the fallback, disconnection handled by the existing goal planner, and one change to the permadeath branches for a shared world.
- **Character creation and arrival** (Sections 3.10, 5.4, 5.5, 12.4): new subsections — creation as seeding the same personality/skill model every NPC uses rather than a separate system, an arrival sequence built on the existing delegation/message system so personality (not a script) decides whether a new arrival reports for duty promptly, gets sidetracked, or worse, and a strength-skill-based personal carry capacity extending the existing vessel/horse capacity logic down to one body.
- **Fifth personality trait** (Section 11.4, and every place the trait list was cross-referenced: 3.10, 5.4, 20): resolved — added **sociability**, orthogonal to discretion. Discretion governs what an agent says once talking; sociability governs how readily it starts talking at all. Low sociability also explains a preference for solitary work (trapping, stalking hunts) over communal settings.
- **Items & inventory** (Section 12.5): new subsection — one generic item model, item-level fidelity for carried objects and ledger-level fidelity for institutional storage, with stow/withdraw as the ordinary-verb boundary between the two rather than a special inventory mode.
- **Herd movement & contest** (Section 16.5): resolved the standing open question — migratory species are mobile spawn sites on a seasonal route; multi-faction contest and the resulting friction fall entirely out of Sections 16.3 and 15.1's existing logic rather than needing new mechanics.
- **Graceful degradation and delegation agency** (Sections 13.1, 13.7, 16.4, 3.9, 19): subsistence fallbacks before a post starves, solvency crisis responses before a company collapses, a rested-site option before overharvest lands, and expanded method-of-fulfillment choice (including skimming) inside an ordinary delegated order — plus two new cross-cutting principles: terminal failures need an earlier escapable warning state, and every hidden system needs a learnable tell.
- **Council outcomes as Accords** (Section 3.8.2): new generic agreement object — parties, terms, a breach condition — with peace treaties, trade exclusivity, safe passage, alliances, debt restructuring, vendetta settlements, and the 1821 merger itself all as named parameterizations of the same object, matching Section 19's one-generic-system principle.
- **Legacy across kinship** (Section 10.9): new subsection — a dead agent's existing memories apply to their heir one kinship hop further removed, using the same weight/decay math as an ordinary retelling, giving succession and the multi-generational family goal (Section 3.1) real accumulated weight.
- **Founding a new post** (Section 13.8): new subsection — expansion as a Tier 1 goal running through site selection, metropole/council approval, vulnerable construction, and competition for the same finite recruit pool as every existing post.
- **Crafting and player interface** (Sections 12.6, 12.7, 3.10, 19): new subsections — recipes as reusable chains with an anywhere/facility-gated split (crafting vs. a new smithing skill), manufactured trade goods deliberately excluded as repairable-only rather than craftable, and four interface screens (inventory, character, skills, perks) surfacing existing simulation state rather than adding new mechanics. Perks introduced as a discrete-trait condition type alongside addiction and disease, with goal-reading (Section 3.5) as the one deliberately player-exclusive instance.
- **Freeze-up, breakup, and vessel durability** (Sections 14.1, 12.2, 12.6): water state as a real multi-week per-waterway transition (the Autumn Scramble, the Spring Lockout), emergency canoe-to-sledge conversion via caching (Section 12.5), canoe condition and field repair, and canoe construction as a new season-and-location-gated crafting category.
- **Winter isolation** (Section 17.6): new Tier-0-adjacent need — psychological strain from isolation and monotony, susceptibility set by sociability (Section 11.4), escalating per the graceful-degradation principle rather than jumping straight to mutiny, countered by a Factor's own administrative choices (rations, storytelling, hunts, celebrations).
- **Language, embezzlement, disease, and courier extensions** (Sections 10.6, 12.5, 3.9, 13.5, 13.4, 10.3): named languages and deliberate interpreter sabotage; caches as a fourth item location covering both stranded-brigade survival and off-the-books trading; triggerable audits; disease traveling through a contaminated trade good plus quarantine as a real choice; liquor dilution and the discoverable Fire Test; express couriers and dispatch forgery.
- **Open questions** (Section 20): updated and extended.
