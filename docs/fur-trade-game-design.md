# Fur Trade Game — Design & Mechanics Reference

*Working design document. Compiled from planning discussion — intended as a shared reference for continued design and implementation work.*

*Revision note: this version folds in the follow-up planning discussion — population scale and simulation tiers (Sections 11.1, 18.2), the spawn-site ecology system (Section 16.4), the map boundary and the Southern Department / James Bay region (Section 6), pre-1821 HBC and NWC structure (Section 5), the message system rebuilt around read events (Section 10.1), delegation and task assignment (Section 3.9), and the Fort William council (Section 3.8.1). A change list is at the end.*

---

## 1. Core Concept

An isometric, open-world RPG following **one protagonist** across the fur trade wars of the early 19th century. The player can start on one of three paths and can defect or move between them over the course of the game. The tone is **dark**, with real, permanent stakes — combat has real death, for the player and for NPCs and factions alike. The world is meant to feel like an actual **simulation**, not a themed backdrop: factions are made of individual characters who move, remember, believe, and act; information has to physically travel; resources are finite and can run out.

The player is mechanically an equal agent among NPCs (Section 11.1) rather than a superpowered protagonist — same needs, same goal structure, same action vocabulary (Section 3). A small number of distinct player-specific abilities are expected on top of that baseline; the first is defined in Section 3.5.

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

**One system, different parameters** (Section 19):

| | Mandate comes from | Obedience rests on | Reports |
|---|---|---|---|
| HBC Factor | London directive, reviewed each ship | Salary, rank, London's approval | Journals and accounts; debrief on return |
| NWC partner | July council (Section 3.8.1) plus his own share | Profit share, peer standing, clerks hoping for a share | Debrief at the rendezvous |
| Métis hunt captain | The hunt itself | Kinship and hunt discipline (Section 16.1) | Immediate, in the field |

**Cost and legibility.** The loop is event-driven, so it is cheap. A post in the live bubble runs its deliberation on a visible daily beat — the morning assignments — which gives the player something to watch. Background posts run the whole loop in the weekly batch (Section 18.2). As a clerk, the player gets assignments as dialogue with intent and a deadline but not the leader's reasoning, so priorities have to be inferred (Section 3.5); he can accept, counter, decline at a standing cost, or volunteer. As a factor later, he runs the same loop with the same verbs and no special UI.

**Example.** An inland post reads its orders late in the fall: 40 packs by ship. The factor has 12 men and two clerks. He sends the clerk he trusts, with six men, to a far outpost he has heard has lodges, and keeps the ambitious clerk close on a provisioning hunt. In January the outpost report is overdue, and he can't tell whether the clerk is dead, snowed in, or sitting on a site. He pulls two men off provisioning to check, and the post's food margin thins. Nothing scripted that crisis.

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
A small trait set (loyalty, discretion/talkativeness, suspicion, ambition) decides what an NPC *does* with what they know:
- Loyal companion → may warn the player privately first.
- Talkative NPC → spreads it regardless of sensitivity.
- Ambitious/resentful NPC → may sit on it as leverage/blackmail rather than report it (see Section 10.4's decay/refresh rule for how these secrets age).
- Suspicious factor → interprets ambiguous sightings in the worst light by default.

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

### 12.3 Horses — Plains Mobility & Raiding
The plains' equivalent of the canoe: a horse herd is both transport and wealth, and losing it mid-journey is a real stranding risk parallel to a wrecked canoe or a York boat stuck at a portage. Historically, horse raiding between Blackfoot, Cree, and Assiniboine was itself a major form of the intertribal conflict already established (Section 7.2) — a raid whose entire goal is the herd, not a fight over trade goods or territory. This slots directly into the existing party-collision combat model (Section 15.1): a horse-raiding party is doing exactly what it says, and running into one is a different flavor of encounter than a war party or an ambush, with a different resolution (chase it off, defend the herd, cut losses and let it go). Herd health and grazing become their own resource-management concern on long plains journeys, mirroring the fuel/food concerns of a canoe brigade (Section 17).

---

## 13. Economy & Attrition

### 13.1 Pemmican & Supply Chains
Destroying a major pemmican supply should be able to cause real starvation at specific posts, and real deaths among staff/voyageurs/family there — not just a resource number going down. The same tension exists at the individual/party scale in Section 17.4.

### 13.2 Manpower Attrition & Replacement Pool
- Lost personnel are **not automatically replaced**. Replacements have to physically travel from Montreal (NWC pipeline) or Britain (HBC pipeline via ship), using the same vulnerable logistics network as everything else — meaning a replacement party's canoe/boat can itself be intercepted and slaughtered by a war band.
- There is a **finite pool of available people** in the metropole (Montreal/Britain) — per Section 11.1, this pool is an abstract count of potential recruits, not simulated people, until someone is actually mobilized and starts traveling. Heavy losses deplete the count, making replacements slower and scarcer over time — a genuine attrition mechanic.
- **The pools aren't one generic well** — HBC draws on Orkney Islanders, Christ's Hospital apprentice clerks, and Selkirk's own recruiting stream; NWC draws on Lower Canada parishes, kin networks, and (after 1813) former Pacific Fur Company men (Section 10.7). Refill rate depends on each company's finances (Section 13.7) and on a lagged reputation effect — news of frontier deaths has to actually travel home through the ordinary propagation system (Section 10) before it slows recruitment, rather than a separate morale stat.
- This mirrors real history: the 1818–21 trade war bled both companies so badly in money and manpower that exhaustion, not any single battle, is a major reason the merger happened at all. A player who leans hard into supply-line warfare could be **mechanically causing** the historical endgame rather than just waiting for a scripted 1821 cutscene.

### 13.3 Debt & Credit
The trade ran on it historically — companies extended credit against future furs, and debt was a real coercive tool over trappers and freemen. Extending credit builds short-term loyalty and dependency but risks default; a trapper buried in debt is a real target for a rival company offering to forgive it, giving company rivalry (Section 7.1) an economic weapon distinct from violence (Section 15) or information (Section 10). It also gives Simpson's 1821 restructuring (Section 4.1) real teeth if part of what he does is call in debts a predecessor let slide — the merger stops being only a political event and starts being something that happens *to* specific named people (Section 11.1) the player knows.

### 13.4 Alcohol
Historically central and genuinely dark: trade liquor bought favorable terms in the moment at real, lasting cost to the people it was traded to, and the companies themselves argued over banning it for exactly that reason. Under the unified agent model (Section 11.1), an NPC can develop an actual addiction — a persistent condition that reshapes their goals (Section 3.1) and behavior, not a flavor-text warning — which means a single trade decision can produce a named grudge that outlives the transaction, the same shape as a suppressed secret (Section 10.4). It should also feed faction standing (Section 10.5) directly: a pattern of alcohol trading with a community is exactly the kind of slow, cumulative harm that should tank standing without any single dramatic reveal.

### 13.5 Disease & Epidemics
Disease — smallpox above all — is one of the defining, devastating forces of this actual period and region, and deserves the same weight as the darkest systems already in this document (Section 9's death/captivity/slavery), not a background stat drain. Mechanically, it can reuse machinery already built rather than needing an entirely new system: an outbreak spreads through contact the same way fire spreads through flammable material (Section 18.4) — agent-to-agent instead of tile-to-tile, faster where people are close together (a post, a rendezvous, a winter camp), slower where they're not. Under the population model (Section 11.1), an outbreak reaching a specific band is not an abstract percentage — it can genuinely sicken and kill named people the player knows, and a nation devastated by it has its standing, territory, and role in the faction web (Section 7.2) actually change afterward, not as a scripted event but as a real consequence rolling forward through every system downstream of population (goals, marriage availability, trade). Because this reproduces real historical trauma rather than an invented fail-state, it needs a deliberate decision — separate from the mechanical design — about how much weight, visibility, and player agency (warning a community, refusing to facilitate contact, being blamed regardless) it carries, rather than being tuned only for gameplay balance.

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

**This is where the 1821 merger comes from mechanically, not from a date.** Historically, mounting legal costs wore down both companies, each tried to buy the other out, HBC prevailed on deeper pockets and political connections, and by 1818 disaffection with Montreal's leadership was already driving desertions to HBC and to American rivals. The model reproduces the mechanism: when both ledgers cross an exhaustion threshold, the two councils (Section 3.8, Section 10.7) negotiate a merger, on terms shaped by whichever side is less depleted. As Section 4.4 lays out, this is tuned to land close to 1821 under expected play, but the crossing itself is real and simulated, not scheduled — 1821 is where history put it, not a timer this design copies.

---

## 14. World Simulation — Weather, Seasons, Day/Night, Wildlife

### 14.1 Seasons
A season model that maps directly onto the operating calendar already built into the logistics/message system:
- **Spring breakup (ice-out)** — water routes open, brigades depart, goose hunt begins.
- **Summer** — main trading/travel season; bison summer hunt; Fort William rendezvous (July).
- **Fall** — second bison hunt; fall goose hunt; last brigades before freeze-up; beaver fur begins "priming" for winter.
- **Freeze-up / Winter** — water routes close, travel shifts to dog sled/snowshoe; the main trapping season (best pelts); posts become isolated and have to live off what the last ship/brigade delivered.

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

**Predators** layer on top as their own population, tracked as **roaming packs** rather than individuals — so a pack can actually be encountered as a party (Section 15.1) — whose growth tracks prey availability across nearby sites. This gives a symmetry: overhunt a region and the wolves go hungry and turn toward the post; leave it alone and the prey does the same. Both extremes converge on the same danger from opposite causes.

**Knowledge**: sites aren't known by default. Finding one is scouting, or someone who already knows telling you, which reuses Section 10's knowledge system directly. A good lodge location is exactly the kind of thing worth someone's favor to share or sit on.

**Reuse, not a fourth system**: this recombines three pieces already in the design — a destroyable object (Section 18.4), a resource that stays gone once spent (Sections 13.1/13.2), and knowledge that has to be learned (Section 10) — which matches Section 19's one-generic-system principle.

**Promotion to agents.** Individual animals are promoted out of a site's count into full named agents when the site enters the live or local tier (Section 18.2). A promoted animal that lives keeps being an agent — when the player moves off, it drops to the background tier like any other agent — and one that dies is culled from processing.

*Bookkeeping (proposed)*: each promoted animal carries a home-site ID, and a site's effective population is its abstract remainder plus its living promoted agents, so nothing is double-counted. A kill removes the agent and decrements the site. A per-region cap on persistent promoted animals keeps them from accumulating over a long game (Section 20).

*Proposed, not yet decided*: the sites themselves are simulation state like any other and would resolve at the tier cadence of wherever they sit (Section 18.2) — a background-tier site regrows and drifts on the weekly tick and only becomes fine-grained when someone is working it.

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

---

## 20. Open Design Questions

**Carried over (some updated):**
- **1801 vs. 1811 start** — earned-reputation prologue vs. dropping straight into the Pemmican War. Section 4.1 currently leans 1811, but the HBC/NWC structure work is now scoped to the full ~1801–1821 window. An 1801 start would open with the NWC meeting at Grand Portage, its first annual meeting at the new Kaministiquia post in 1803, and the XY rivalry live until 1804 (Sections 4.2, 3.8.1) — and would also make the Red River Colony's founding a live, simulated London subplot rather than pre-game backstory (Section 4.4). Needs a decision.
- **Marriage track mutability** — can a player renegotiate/change tracks more than once, or does each transition (Section 8.3) apply every time regardless of direction already traveled?
- **Herd/wildlife movement rules** — Section 14.4/16.2 establishes the roster and regional split, and Section 16.4 now specifies site-level population dynamics; the actual movement/contest simulation between rival hunting parties (Métis, Cree, Assiniboine, Blackfoot) still needs full mechanical design.
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
- **Open questions** (Section 20): updated and extended.
