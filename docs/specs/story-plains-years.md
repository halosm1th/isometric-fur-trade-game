# Story Spec: "The Plains Years" (working title)

Status: **spec / not built.** Written 2026-10-13 against the code as it stands. Owner's brief, in their words: *arrive for HBC, go to Auld, get sent inland to the plains, slowly make friends there, fight the NWC and some native groups, get drawn into hunting, and eventually join a war against other Indigenous groups.*

This is a **campaign plus the engine work it needs**. Per design principle 2 ("simulate, don't script") the arc is not a cutscene track: it is a set of *pressures and opportunities* written in the campaign language (`tools/CampaignTool`), with every act resting on a system the world already runs for everyone. Where a system is missing, this spec says so and lists it in section 5.

## 1. The shape in one page

| Act | Years | Player is | The question the act asks | Mostly built? |
|---|---|---|---|---|
| 0. Landfall | Aug, year 1 | A new hand off the ship | Who are you, and who do you trust? | **Yes** (HBC Path prologue) |
| 1. York | Aug - Sep | A labourer under Auld, with a rival clerk | Will you be useful enough to be sent somewhere that matters? | **Mostly** (Delegation, Standing, Rivalry) |
| 2. Posting | Sep - Nov | Carried inland on the autumn brigade | What does the country look like when the Company is a day's walk behind you? | **No**: nothing reassigns the player's post |
| 3. The Grass | Year 2 | The Company's man at a plains post | Who will feed you, and what will you owe them? | **Partly** (camps, languages, horses, bonds, favours) |
| 4. The Opposition | Years 2-5 | A rival at the next palisade | How far will you go for the Company, and for your friends? | **Partly** (Rivalry, Pemmican Proclamation beats) |
| 5. The Hunt | Years 2-6 | A rider and a provider | Can you feed a post and a band at once? | **Mostly** (buffalo runs, horses, the captain and soldiers; the player cannot yet be made a soldier) |
| 6. The War | Years 4-8 | Drawn in by kin and debt | Whose war is it, and what does it cost? | **Partly** (horse raids, war parties with morale and retreat, timed truces: `Economy/War.cs`) |
| 7. The Union | Mar 1821 | Whoever you have become | What is left? | **Yes** (Reckoning of 1821) |

Acts 3-6 overlap; they are a *sequence of emphasis*, not a rail. The player who never leaves York still has a valid game.

## 2. Where the player goes, and why that place

The plains arc needs one **anchor post** where the Company, the opposition and a band all sit within a day's ride. Candidates already on the map (see `WIKI/Locations/All Trading Posts.md`):

| Anchor | Why | Risk |
|---|---|---|
| **Brandon House** (HBC, 1793) with **Fort Souris** (NWC) alongside, Assiniboine River | Pemmican post; the paired-rivals shape is already in the atlas; Assiniboine and Plains Cree around it; the Pemmican Proclamation (Jan 1814) lands here; the Red River Colony is two weeks east | Fidler is its real man; do not put the player over him |
| **Edmonton House** (HBC) with **Fort Augustus** (NWC), North Saskatchewan | Custom plan already exists; **Blackfoot** camps actually come here (family camps are set); the horse and war country | Further from the Colony beats; Blackfoot war is the *other* side of Act 6 |

**Recommendation: Brandon House for the first version.** It carries Acts 2-5 and the Colony beats with the least new content, and it puts the player *beside* the Cree-Assiniboine-Saulteaux side of the intertribal war, which is the side an HBC man there would have been tied to. Edmonton House is the second playthrough ("the other side of the Saskatchewan"), reusing the same beats with different parameters.

## 3. Act by act

For each act: **hook** (what the player sees), **pressure** (the campaign beats), **systems** it uses, and **gaps** it needs filled. Sizes: S = a day, M = a week, L = weeks.

### Act 0-1. Landfall and York *(built)*
- Hook: the existing HBC Path: the crossing, Hugh and Andrew, Davy Gunn's rum, "Report to Mr. Auld."
- Pressure: Auld's orders each morning; the rival clerk; the ship's sailing report on 15 Sep.
- **Add (S):** a beat, `AuldTakesNote`, that fires after the player's first report and the first ship sailing *if standing is 25 or more or they have a Cree language of 0.3 or better*. It makes Auld name the player in a letter to the Oxford House master: "he has the tongue" or "he works." This gives the posting a *reason* the player can influence.

### Act 2. The Posting *(new)*
- Hook: Auld sends for you: *"There is a want of hands at Oxford, and a want of a man who can talk at Brandon."* You may argue, ask for a different post, or accept. The autumn brigade (leaves 18 Sep) is already in the game; you are on it.
- Pressure: the first real winter away from York. The post master at Oxford passes you on in spring with the returns brigade going the other way.
- Systems: Brigade (crews are real men with real hull wear), Delegation, Travel mode.
- **Gaps:**
  1. **Posting** (M): `PlayerController` has a home post but nothing changes it. A *Posting* is an order from a leader ("you will winter at X") that sets `HomeHousehold`, moves the player's bed, rank and standing to the new ledger, and carries an expectation (a goal) at the new place. This also makes NPC reassignments possible (clerks moved between posts) and is the base for founding posts (design doc 13.8).
  2. **Standing is per-company, not per-post** (S): decide whether a master at Brandon starts you at your York standing or lower. Suggested: carry 60 percent, so a posting is a small fresh start.
  3. **Letters of introduction** (S): an item the player carries that the new master reads; its text comes from Act 1's beat.

### Act 3. The Grass *(partly built; this is the heart)*
- Hook: Brandon House is a palisade, a pemmican store and eleven people. The Assiniboine camp half a day out will decide whether you eat.
- The slow friendships (the owner's "slowly make friends"):
  - **A guide-interpreter** NPC (Métis or Cree) who becomes your first real bond if you pay, feed and cover for them. Uses Friends and Bonds, Favours, Languages.
  - **A family in the camp**: a hunter who shows you the ground, a woman who trades with you at a fair price, a boy who follows you. Uses the existing camp casts (chief, hunter, wife, daughter per band).
  - **Gifts, tobacco and the pipe** at each meeting (Barter's ceremony exists; see Trading and Barter).
- Pressure beats (examples, all to be written in `.camp`):
  - `FirstGuns`: the band asks to be outfitted on credit before the buffalo run. Say yes and you are in debt to the Company on their behalf; say no and the NWC clerk says yes.
  - `HungryMonth` (Feb): the post is short; the band is shorter. Whose pemmican do you break?
  - `TheHorseFair`: the summer fair at the Missouri towns (built in `MissouriTrade`) is where horses come from. Send goods with the winter expedition; the band remembers who did.
- **Gaps:**
  1. **Relationship tiers with visible rewards** (M): bonds exist; what they *unlock* does not read yet. Define three: *known* (they trade fairly), *trusted* (they will guide and warn you; adoption talk), *kin* (they ask things of you, see Act 6).
  2. **Naming and adoption** (M): an Indigenous name given by the band when trusted; a small ritual scene; a persistent memory every NPC in the band carries. This is the emotional pay-off of the act. Needs consultation, see section 6.
  3. **Interpreter role** (M): `Roles` has no interpreter; design doc 10.6 calls for one. Required for Act 6's diplomacy.

### Act 4. The Opposition *(partly built)*
- Hook: Fort Souris is a hundred paces from your gate. Their clerk is friendly until the price war starts.
- Pressure: `Rivalry` tension already runs; the Pemmican Proclamation (8 Jan 1814), Seven Oaks (19 Jun 1816) and the lawless years (1818-20) are beats in `historical.camp`. This act **binds them to the anchor post**:
  - `ProclamationAtBrandon`: the Colony's governor forbids pemmican leaving the district; the NWC and Métis brigades take it anyway; your post is told to enforce it with a handful of men.
  - `TheStoppedBrigade` (exists as a brigade beat): a York boat or Métis cart train is stopped on the river; the player's response (turn back, fight, bargain, tip off the other side) leaves a memory on both camps.
  - `SevenOaksNews`: news reaches the post weeks late via letters; Métis kin of your friends were on the other side. Regard splits.
- Systems: Rivalry, Combat, Information and Messages, Grudges and Vendettas, Councils and Accords.
- **Gaps:**
  1. **Non-lethal outcomes and de-escalation** (design doc 15.2, 15.5; not verified in code): the opposition is *people the player knows*, so a fight must have off-ramps (parley, hostage, taken stores, a fine).
  2. **Bribery, price war and debt buying** (design doc 13.3): "rival forgives their debt" is the economic weapon; it is the Act 4 equivalent of a gunfight. Lives in the [global trade spec](global-trade.md) phase 2.
  3. **Seven Oaks as a place** (L): listed as a wanted battle site in `world-content-needed.md`.

### Act 5. The Hunt *(partly built)*
- Hook: the spring and fall buffalo runs. The band rides out; you ride with them if they trust you.
- Built (see commit `d3215ae`): herds per camp, buy/borrow/give/steal a horse, ride at the ground's pace, runs by the captain's word, raids between enemy nations, Métis carts to the posts.
- **Gaps:**
  1. **Hunt discipline and the captain** (design doc 16.1; M): whoever goes early spoils it and is punished; the player can be *invited* to be a soldier (enforcer) once trusted. A lovely consequence loop that tests Act 3's friendships.
  2. **The pemmican chain** (M): kill -> dry -> pound -> fat -> bag -> cart -> post -> brigade. It is the economy of the plains *and* the spark of the war between Colony and Nor'Westers; it also makes a good first multi-step "crafting chain" (longer_goals item 6) without a general crafting system.
  3. **Robes and horses as trade goods** (S): `Buffalo Robe` exists at 2 MB; add its seasonal quality and a horse price in MB so the market (global-trade spec) can see them.

### Act 6. The War *(partly built, 2026-10-21)*
This is the act the brief ends on, and the one that most needs care.

**Design stance.** The player is *drawn in*, never dropped in. The war is the plains' war (Cree and Assiniboine against the Blackfoot confederacy and Gros Ventre is the one the atlas and `Factions/*.md` already frame; other pairings: Dakota and Ojibwe on the Red and Lake Winnipeg side), fought for horses, hunting ground and honour, and **the Company's guns are one cause of its scale**. The player has three levers and each is a real moral and mechanical cost:

| Lever | What it is | Systems | Cost |
|---|---|---|---|
| **Supply** | Sell, withhold or give guns, powder and ball to a band | Barter, ledgers (`Powder and Ball`, `Trade Musket`) | Standing with the Company (London disapproves arming or *not* arming), the enemy's memory, and each shot sold becomes a casualty somewhere (reported in the Year Told) |
| **Standing beside** | Ride with a war party as a guest, interpreter or fighter | A war-party goal; Combat; horses | Wounds, death, regard won or lost with kin; the Company hears of it |
| **Make peace** | Interpret and carry messages between bands; broker a truce at a post | Accords (3.8.2), Languages, Interpreter role | Needs real language and trust on *both* sides; fails often; the only path to a lasting good outcome |

- **Hook:** a band the player has become kin to loses horses, then a man, then a boy to a raid. A feud (`Plains.Feuds`) heats. The chief or the hunter asks the player: *"Will you come?"*
- **Pressure:** the beats escalate through the existing feud score; seasonal rhythm (raids in summer, winter camps, the horse fair) sets when.
- **Outcome space** (all valid): ride and survive; ride and die (heir takes over: Heirs and Succession); refuse and lose kin status; trade guns to both sides and be known as that; broker a truce that holds a year; leave the plains.
- **Gaps:**
  1. **War parties as agents** (L, design doc 15.1): a leader with followers, an objective (raid, revenge, defend, scout), morale (15.4), and retreat. `PlainsGoals.cs` and `Plains.cs` have the horse-raid core; a war party generalises it.
  2. **Casualty and consequence plumbing** (M): deaths in a raid should reach named kin, shift `Feuds`, change band population, and show up in the Year Told and the Reckoning.
  3. **Truce as an Accord** (M): reuse `Council.cs`/Accords for a truce with terms (horses returned, blood price, hunting-ground lines) and a term (a season or a year).
  4. **Disease and arms balance** (design doc 13.5, 7.2): do not add this lightly. Section 6.

### Act 7. The Union *(built)*
The 26 Mar 1821 merger and the Reckoning measure what the player did. **Add (S):** a plains-specific reckoning section: your bands' standing, horses held, the feud scoreboard, the truce, and "who still speaks your name."

## 4. Campaign file plan

One new script, `tools/CampaignTool/src/plains-years.camp`, built with `dotnet run --project tools/CampaignTool -- build`, plus `tools/CampaignTool/examples/plains-years.scenario.json` so `sim` can run the pressure beats headlessly. Structure it as **modules** (the language has procedures as macros):

```
campaign PlainsYears(title: "The Plains Years", description: "From York to the grass, and what you owe there.");
// historical spine: include the historical.camp beats by copy (no import yet; see tool spec T4)
beat AuldTakesNote ...        // Act 1
beat Posted ...               // Act 2   (new library call: posting())
beat FirstGuns ...            // Act 3
beat ProclamationAtBrandon ...// Act 4
beat SpringRun / FallRun ...  // Act 5   (repeat: yearly)
beat TheAsk / FeudHeats / TruceTalk ... // Act 6
```

Library calls the script will want and the tool does not yet have (add to `Library.cs`/`Known.cs`; each is a small engine hook): `posting(person, post)`, `band("name").regard`, `band("name").feud(other)`, `horses(band)`, `plains.raid(raider, target)`, `accord.propose(...)`. The tool's drift test will force the game and the tool to agree.

## 5. Engine work, ranked

| # | Work | Unlocks | Size | Notes |
|---|---|---|---|---|
| 1 | **Posting**: reassign the player's (and NPCs') home post | Act 2, founding posts | M | Partly exists: a campaign action `posting` calls `PlayerController.PostTo`, and the Factor posts a clerk to Oxford House (`Delegation.PostAClerk`). No general reassignment of NPCs |
| 2 | **Relationship tiers + adoption/naming** | Acts 3, 6 | M | Needs consultation (section 6) |
| 3 | **Interpreter role + faction language gates** | Acts 3, 6 | M | Language kinship already modelled |
| 4 | ~~**Hunt captain and discipline**~~ **built** (2026-10-21 check) | Act 5 | M | `BuffaloRun`: captain, soldiers, the early rider. Inviting the player to be a soldier is still open |
| 5 | **Pemmican chain** | Acts 4, 5, and the economy | M | NPC households already dry meat and make pemmican (`MakePemmicanGoal`); the player cannot |
| 6 | ~~**War party agent + morale + retreat**~~ **built** | Act 6 | L | `Economy/War.cs`, `Agents/Goals/WarGoals.cs` (commit `797a6df`) |
| 7 | **Casualty plumbing and reckoning section** | Acts 6, 7 | M | |
| 8 | **Truce as an Accord** | Act 6 | M | Timed truces exist (`War.Truce`); the Accord object with terms and a breach does not |
| 9 | **Seven Oaks and Brandon as places** | Act 4 | L | Content, not code |
| 10 | **Non-lethal combat outcomes** | Act 4 | M | Partly exists: a killing blow from the other company takes you prisoner (§9.1). No surrender or bluff |

A playable "vertical slice" is **1 + 2 (known/trusted only) + 5 (the chain only as far as the pemmican bag) + the four Act 3-4 beats**: roughly a month of work and enough to feel whether the arc is fun before building the war.

## 6. Handling notes (read before writing any Act 3 or 6 content)

- **Real peoples, real harms.** The nations in these acts are living peoples, and the war and the arms trade are part of a history of dispossession. Design doc 13.5 already asks for a deliberate decision about disease; apply the same rule here: decide *before* building how much the player can profit from, glorify or escape the consequences of arming and fighting. The suggested defaults above (costs on every lever, peace as the hardest and best path, no "win the war" screen) are a starting position, not a decision.
- **Consult and cite.** Names, ceremonies, and the adoption scene should be checked against Indigenous-authored sources and, where possible, by people from the nations depicted. Campaign text marked *(check)* in `docs/world-content-needed.md` applies here.
- **History claims to verify before they enter a beat:** the Cree-Assiniboine alliance and its enemies in 1811-21 (and the old term "Iron Confederacy": a modern label, handle with care); who actually fought whom on the Assiniboine in 1814-16; the part Brandon House played; the exact order of Colony events at Brandon House. The `dcb-timeline` and `canadiana-selkirk` skills can build a sourced timeline for these.
- **Keep the old rule**: nothing above is announced to the player. Every beat reaches them as a letter, a rumour or a person turning up at the gate (Information and Messages).

## 7. Open questions for the owner

1. **Brandon House first**, or Edmonton House (the Blackfoot side)?
2. Is **adoption/kin status** wanted as a mechanic at all, given the care it needs, or should friendship stay as regard and bonds?
3. Does the player ever **lead** a war party, or only ride in one? (Leading multiplies the agent work.)
4. Should the **player's company allegiance be able to change** (go freeman, go Métis, go NWC) within the arc? The design doc's Métis path (5.3) says yes; it is not built.
5. Is this a **new campaign** (`plains-years`) alongside the historical one, or a *start* (like HBC Path) that ships its own pressure beats?
