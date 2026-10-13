---
title: NWC Path
aliases: [North West Company Start, Nor'Wester Start, Brigade Start]
tags: [starting-paths, nwc, prologue]
status: implemented
---

# NWC Path

**Status:** Implemented, with one rough edge (see "Known quirk" below).

![[ui-crest-nwc.png|120]]

| | |
|---|---|
| **Road in** | A *canot du nord* with the summer brigade from Fort William |
| **Start date** | 12 July (the brigade's day), 07:00 |
| **Place** | On the river a short way below the Nor'Westers' landing, across from the Bay men |
| **Faction / home post** | North West Company, [[Nor'Westers' House]] |
| **Backgrounds** | Voyageur, Clerk from Montreal, Freeman's son |
| **Sprite** | The Nor'Westers' red (`RivalFrames`) |
| **First order** | Report to the wintering partner, Mr. Duncan Cameron |

Blurb: *You come up the river in a canot du nord with the brigade from Fort William, to winter at the partner's house across from the Bay men. Mobile, stretched thin, and ruled by the partners in council.*

![[vehicle-canoe.png|300]]

## The prologue: "Above the last carrying place"

Opening line: *The North West Company's house, across from the Bay men. You are the partner's new man.* The brigade is camped above the last portage, a day's paddle from the partner's house; the canoes are turned over and the kettle is on. If you are not the first character in the world and it is not July, it is "a light canoe" rather than "the brigade".

Your canoe-mates are **Michel Allary** and **Jean Paul**, voyageurs (capote, knife, 2 pemmican each) who "came up from Fort William in the same canoe" (shared memory, +12 regard each). Three optional scenes, then you sleep.

![[char-rival.png|110]] ![[char-voyageur.png|110]]

### Sit with Michel
Michel shows you how to wrap a cracked blade with spruce root. *"Two winters I've done for the Company. A third and I'll have enough for a farm at Sorel - if the partners pay what they promise."*
- **Help him with the paddle:** +12 regard from both mates, a shared memory ("taught you to mend a paddle on the brigade"), and a journal note about the farm at Sorel.

### Play cards with Jean
*"Three pieces a hand. The winner buys the next regale."*
- **Stake three pieces:** you need 3 trade goods. Win chance is `0.45 + 0.2 x your Trade skill`.
  - **Win:** +3 goods; Jean's regard -3.
  - **Lose:** -3 goods; Jean gains +8 regard ("I like a good loser") and pockets the goods.
- **Not tonight:** nothing.

### Ask the guide about the post
The guide tells you the partner winters across from the Bay men "and he'll want you the moment we land". The Bay men are under the Factor (William Auld), and the band of the chief (Maskwa) trades with them more than with the North West Company: *"The partners at Fort William want that turned round this year."* Taking it logs the hint in your journal. This is the NWC path's rumour that "names the men who matter".

### Sleep
Noon next day: the brigade sweeps round the last bend singing and runs up on the landing. *Across the river the Bay men's flag.* **Step ashore** puts you at the Nor'Westers' gate.

![[loc-nwc-house.png|400]]

![[char-nwc_partner.png|110]]

## Backgrounds

- **Voyageur** - Woodcraft 0.3, Hunting 0.25, tumpline.
- **Clerk from Montreal** - Trade 0.4, clerk, standing 30.
- **Freeman's son** - Hunting 0.45, Cree 0.8, one kinsman in the Cree band at regard 25.

## First task and what follows

The game assigns `Report` (need 1) on landing. The NWC year then follows the partners: the **Brigade** calendar event (12 July) lands the outfit, carries the furs off to Montreal, convenes the [[Councils and Accords|Fort William council]], and writes your year-end account via [[The Reckoning of 1821|the Reckoning]]. Unlike the Company's ship, the brigade's sailing does **not** re-rate your rank from standing (that code only runs for HBC men); you keep the rank your background gave you (see [[Standing]]).

> [!warning]
> **Known quirk.** The `Report` task points you at the partner (the way-finding arrow does), but in code only talking to a *Factor* marks it complete. A Nor'Wester's report task therefore stays open unless you cross the river and speak to Mr. Auld, and while it is open the partner's morning orders skip you ("report first"). Treat it as a wart, not an intended hurdle.

> [!tip]
> The partners want the Cree band turned from the Bay men. Rum and a pipe are the NWC's tools (the council votes liquor policy each July: restraint 0.2 rum share or plenty 0.4). See [[Trading and Barter]] and [[Rivalry]].

## See also
[[Starting Paths]] | [[HBC Path]] | [[North West Company]] | [[NWC Partner]] | [[Voyageur]] | [[Fort William]] | [[Councils and Accords]]
