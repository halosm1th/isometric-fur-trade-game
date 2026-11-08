# Agency audit: is anything happening that no agent did?

Date: 2026-11-04. Branch `agency-audit`, from `master` at `0a57e88f`.

**The rules checked:**
- **Design doc §2:** behaviour comes from agents pursuing goals, never from a trigger faking the outcome.
- **Design doc §10:** nobody knows what they did not see or were not told.
- **The exceptions the design allows:**
  - the director (§2.2), which biases what reaches the player and never makes anything happen;
  - campaign beats and their adherence (§2.3);
  - the named set pieces (§2.4);
  - the out-of-sight day's work done as a sum (§18.2);
  - the metropole as a background abstraction (§10.7): London, Montreal and the outside world sending goods, pay and orders.

**Method.** I searched the whole of `Scripts/` (about 48,000 lines) for five kinds of shortcut:
- **Ambient knowledge:** every `Sim.Notice` and `Sim.Headline`, 212 calls, each read and classified.
- **Goods from nowhere:** every `Stores.Add` and `Inventory.Add` with no matching removal, harvest or production nearby (105 sites).
- **People from nowhere:** every mid-game `Spawn`.
- **Omniscient decisions:** agents' choices that read another household's true stores, prices or purse.
- **Ownerless events:** random events not caused by any agent.

I also read the director in full. It does not add goods, spawn anyone, push goals, raise flags or send notices. It only biases (`MayActNow`, `Saw`, `Relevance`).

This was a search, not a line-by-line proof. A shortcut written in some other form than the five searched for would not be found.

---

## 1. Fixed in this pass

| # | What it was | Where | Now |
|---|---|---|---|
| 1 | **Every notice and headline went to every player at once.** A post on the Columbia running out of food, a war party on the plains, an accord at Fort William, meat spoiling at Spokane House: all on every screen the moment they happened. This was the largest breach of §10 in the code. | 187 call sites in 56 files | `Sim.NoticeAt` / `Sim.HeadlineAt` (by agent, household or place) tell only the players within sight (2,400 px). Everyone else hears it as the story travels (`Chronicle`, gossip, letters). `Sim.Log` is for what nobody in the country could see (the London sale, the Committee's sittings). `Sim.Notice` is kept for the game's own housekeeping and the documented exceptions in §2. |
| 2 | **Camps were fed every week from nowhere.** Dried meat, venison and fish per mouth, whatever their hunters did and whatever game was left. The out-of-sight hunt in `Tiers.cs` ran on top of it. | `Economy/CountryBand.cs` | `Economy/Subsistence.cs`: the camp's own able hands at home (people up close excluded), scaled by the game left in its own country, and taken off those grounds. Each ground gives in proportion to what it holds, and the take is counted in animals by kind: a buffalo feeds far more than a hare. The geese are left out, since they are away half the year. The floor is 0.5: snares, the fishery and the hare flats never fail. |
| 3 | **Beaver pelts appeared at every camp every week**, with no lodge losing an animal. The fur trade's supply never touched the ecology. | `CountryBand.cs` | `Subsistence.Pelts`: trapped from the lodges of the camp's own country, limited by what they hold. No lodge near, no beaver. |
| 4 | **The trading posts of the wider country were fed every week from nowhere** (dried meat, winter pemmican). | `Economy/TradingPostLedger.cs` | Same rule as camps: the post's hands, its country's game, taken off the grounds. |
| 5 | **Berries for every camp and post each summer**, picked by nobody. **The Assiniboine's pemmican**, made weekly with nobody making it. | `Economy/Gardens.cs`, `Economy/BandLedger.cs` | Both are scaled by the hands at home. The pemmican is not tied to game: see §3, item 6. |
| 6 | **The York raid made its raiders from nothing** (six named men spawned at the landing) and deleted them at the boats, along with the plunder. | `Core/YorkHappenings.cs`, `Goals/RaidPartyGoal.cs` | The raiders are the Nor'Westers' own men at York, armed from their house's rack. Afterwards they go home and the plunder goes into their house's stores. A house without three fit men does not raid that night. |
| 7 | **The "drive off the rival" ambition showed the player the exact count of goods on the Nor'Westers' shelves.** | `Agents/Ambition.cs` | Shows only what everyone at York knows: they are still there, and how things stand between the companies. |
| 8 | **Laws' `Tell` broadcast to every player** from inside a loop meant for those nearby. A company's law changes reached its players anywhere, instantly. | `Core/Laws.cs` | Town and band laws: told near the meeting. Company laws: logged; they reach the country only as their effects (see §3, finding 4). |
| 9 | **The towns' war news** (a town fallen, a garrison marched, a building burning) went to everyone. | `World/Towns.cs` | Told at the town. |
| 10 | **Winter strain told every player of a post**, wherever they were. | `Agents/WinterStrain.cs` | Told at the post. |

---

## 2. Kept, and why

These do something no agent in the country did, and are allowed by the design:

- **The union of 1821** (`Economy/Union.cs`): the merger is applied across the map at once, and every player gets the end screen at once. It is the fixed core of history (§4.4). Its two global notices are kept.
- **Campaign beats** (`Core/Campaign.cs`): a script's `headline` action is the author's call (§2.3). Forced beats at Firm or Strict adherence are labelled as forced.
- **The set pieces** (§2.4): the York raid's timing, the York happenings' pages, and the journey's incidents. The raid now uses real men (fixed above). The happenings' small pages can still conjure their props: the hunter is real, but his canoe-load of geese comes from the scene. See §3.
- **The metropole** (§10.7):
  - the outfits arriving at the map's edge (`Economy/Outfits.cs`);
  - the garrisons' rations and pay from Quebec, set against stoppages (`Economy/Money.cs`);
  - the ship's crew coming aboard (`World/Ship.cs`);
  - the companies holding or flooding the London and Montreal sales on what the City knew of a rival's purse (`Economy/Companies.cs`).
- **The work of people out of sight, done as a daily sum** (`Core/Tiers.cs`, §18.2): the same people, the same grounds, resolved in batches.
- **Starting stores and starting people:** the world's state on the first day.
- **Weather, the seasons and the fur market's outside shocks** (§13.6, §14): treated like weather on purpose.
- **Disease** arrives with the ship and passes by contact (`Population.cs`). That is agentic.

---

## 3. The open items, fixed (2026-11-06)

The first pass left eight items open. All eight are now fixed: the last two stand-ins (item 6) on 2026-11-07.

1. **Rivalry is each house's own grievance** (`Economy/Rivalry.cs`).
   - Each house at York holds its own (`TensionOf`).
   - A wrong done at York is felt there at once. One done elsewhere reaches each house only when the news does, a day for every 4,000 px. A flood at the London sale comes with the next year's ship and canoes (`RaiseFromAway`).
   - Each master decides from his own house's grievance: raids, the ambush on the river road, the convoys and brigades waylaid, the size of the York raid, the raid and truce a player proposes. A truce between the houses needs both to be sick of it.
   - `Tension` remains as what is said at York (the two houses together): for talk, the Fort William council and campaign conditions.
   - **Also:** the Fort William council's decisions took effect at the sitting. They now take effect when the partner at York reads the minutes. Minutes lost or intercepted on the way carry no orders.
2. **An offer of exclusive trade needs the master to have heard** (`Economy/Accords.cs`). A camp trading at a post is now a remembered event at that post's counter, and it travels as talk does. A house offers a camp exclusive trade only if its master remembers where the camp took its furs.
3. **The partner's price follows what he is told** (`Economy/NwcLedger.cs`). He prices against what the band hunters at his counter say the Factor pays (`HeardFortPrice`), not the Factor's books.
4. **Company orders travel** (`Core/Laws.cs`, `FortLedger.OrderedDry`).
   - A spirits order from the Committee comes out with the next ship to York, then up the rivers to each house. A partners' order travels from Fort William.
   - A post holds to the order only once it arrives, and its people are told why.
   - Holding furs back at the sale, and the rum loaded at Montreal, rightly apply where they are decided.
5. **A band's mood comes from its own dealings** (`Economy/BandLedger.cs`).
   - It moves only after its hunters have traded at the Company's counter in the last two months, by the price they were actually paid. With no dealings it eases toward indifference.
   - The news that a band will not trade reaches only the players who are there, and travels from there.
6. **The test-world Assiniboine's pemmican comes off a real herd** (`Subsistence.Herd`): the nearest buffalo on the map, as full as it is, with the meat taken off it.
   - **Every country now has real grounds of its own** (2026-11-07, `EcologyManager.LocalGrounds`). Where a household's or a town's country has no hare or ptarmigan ground on the map, a hare flat is made on the land near it. Where it has no fishery, one is made in the nearest water within a few days. The houses there know them. They are built from the world's own layout, so they are the same every game; they are saved and restored with the rest. There are 38 in the full world.
   - **Fish is a ground** (`Species.Fish`, added at the end of the list). It has a count and a capacity, it fills again in a season, and it never walks about on land. Every fish now comes out of one through `EcologyManager.Catch`: snares and lines out of sight, a camp's nets, a man's fishing goal, the player's line, the brigades and convoys on the road, and the fish bought or caught on the journey. A water fished out gives nothing until it recovers.
   - **The bush's free hare is gone** (`Core/Tiers.cs`): snares take only what the hare flats hold.
   - **The 0.6 share for unmapped country is gone** (`Subsistence`): a country with no meat ground at all gets the floor (the snares). Only a scene with no ecology loaded, such as a test room, skips the scaling.
7. **Small conjured items:**
   - A released prisoner's two days' pemmican comes from his captors' stores.
   - A man placed with a musket gets his powder from his house's store.
   - The homeguard's geese come off the actual marsh; with no geese there, the scene does not happen.
   - The sailor's skins go under his jacket.
   - Winnings at cards and on a fight are another man's stake, if he has it.
   - The tobacco for a jig is one of the men's.
   - An inspection puts back the very furs that were kept back.
8. **"Task done" goes to the player whose task it was** (`PlayerTask.Owner`).

---

## 4. Effects to watch

- **Hunger.** Tying food to the land was the riskiest change: a hunted-out country now feeds its people at the floor. A winter soak before and after is in §5. If camps starve too readily, the dial to turn is the floor in `Subsistence` (0.2) or the lean-month rations in `CountryBand`. The rule itself should stay.
- **The fur supply.** The camps' beaver now comes off real lodges, so a heavily trapped country will send fewer furs to the posts. That is the §16.4 ecology working as designed, and it changes the company purses (and so the 1821 union) in long runs.
- **Quieter screens.** Players far from events no longer see them. News reaches them through talk ("Any news?"), letters and the director's choice of what visitors talk about. If the world now feels silent, the fix is more carriers of news, not putting the global notices back.

---

## 5. Soak results

The same winter each time: 1 November 1811, about seven months at `--speed=0.02`, towns off. Counted from the world log: deaths from hunger, and "short commons" warnings.

| Code | Hunger deaths | Short commons |
|---|---|---|
| Before this pass (`master`) | 30 | 30 |
| This pass, with the old fixed food (`--oldfood`, a temporary switch) | 31 | – |
| First version: floor 0.2; up-close people left out of the hands | 61 | 38 |
| People up close counted as hands | 55 | 44 |
| Take spread across grounds by species; geese left out | 62 | 39 |
| Assiniboine pemmican by hands only | 66 | – |
| Floor 0.5 (the first commit) | 39 | 35 |
| With the open items fixed (2026-11-06) | 40 | 35 |
| **With real local grounds and fisheries (2026-11-07)** | **33** | **39** |

- **Everything except the food is neutral.** With the old fixed food back on, deaths match `master` (31 against 30).
- **The extra deaths came from the floor.** At 0.2, the York-area camps were starving in winter once the caribou had gone on.
- **At 0.5** the country costs about nine more lives a winter than before. That is a world where a hunted-out or deserted country matters.
- **Turning it:** the floor (`Subsistence.Floor`) is the one dial. Raising it toward 0.6–0.7 brings hunger back toward the old level.
