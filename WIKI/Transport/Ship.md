---
title: Ship
aliases: [Supply Ship, The Annual Ship, The Ship from London, Company Ship]
tags: [transport, vehicle, hbc, annual-event]
status: implemented
---

# Ship

**Status:** Implemented (`Scripts/World/Ship.cs`). The ship is the heartbeat of an HBC man's year.

![[vehicle-ship.png|360]]

Each summer the Company's ship from London crosses Hudson Bay to York Factory with the year's outfit and the [[London Committee]]'s orders, unloads, takes on the furs, and sails with the accounts. A man at York measures his year from ship to ship. It is also how the fever comes to the Bay.

## The ship's year

| Date | Event |
|---|---|
| **20 August** | `ShipArrives`: "A sail on the bay: the ship from England is standing in." Cargo loaded; the ship sails in from offscreen at 90 px/s (scaled with game time so a skip still brings her in). |
| on docking | "The ship is alongside the wharf. All hands to the landing." Eight crew are spawned, six recruits are landed, and everybody runs to the shore. |
| 20 Aug to 15 Sep | Docked. The cargo must be landed before furs can go aboard (`LoadingOpen` only once the outfit is off). |
| **15 September** | `ShipDeparts`: she sails if at least three of her crew are alive; otherwise she is `Stranded` at the wharf. |

Bell and gun: when the ship arrives the audio plays her gun, the fort's answering gun 2.5 seconds later, and the fort bell 5 seconds on; the bell again when she sails. A creak loop plays near her while she is in. See [[Sound Effects]].

## States

`Away` (at sea), `Arriving`, `Docked`, `Departing`, `Stranded`.

## What she brings

On summoning, the ship's cargo is: 120 trade goods, 30 pemmican, 40 dried goose, 40 bread, 30 ale, 8 muskets, 12 steel traps, 24 rum, 4 kettles, 20 point blankets, 6 capotes. The hold starts empty and is filled with the post's furs.

The Committee's **packet** (the year's directive, quota and incentive from `Letters.Directive`) is carried up to the Factor by the bos'n, who is the first of the crew: he is given a `DeliverMailGoal`, and the packet is inert until the Factor opens it. Kill the bos'n on the way and the packet is on his body. See [[Information and Messages]] and [[London Committee]].

## Fever

When the ship docks there is a 35% chance one of the crew has the fever: "There is fever aboard, the mate says. Keep the men off her if you can." See [[Health, Wounds and Illness]].

## When she sails

`Ship.Depart` is the year's accounting:

1. **The Factor's skim.** A Factor with loyalty below 0.35 and ambition above 0.5 keeps back 10% of the beaver and wolf pelts, logging "kept back furs of his own from the ship's hold".
2. **The report on you.** For HBC players the Factor's report sails with her: crossings of the river you never accounted for (up to -25 standing), trading on your own account (up to -20), a parole not confessed (-12). Your rank is then rated: standing 60 or more is *postmaster*, 35 or more *clerk*, 15 or more *labourer*, below that *on notice*; a clerk or postmaster gets a bonus of 6 or 12 trade goods. See [[Standing]].
3. **The tally.** "The ship has sailed for London with N MB of furs aboard (quota was Q)." See [[Made Beaver]] and [[Fort Ledger]].
4. **The reckoning.** The player's trade year closes with the ship (design doc P.3). See [[The Reckoning of 1821]] and [[The Year Told]].
5. **Supply.** The Factor orders next year's supply (`Delegation.OrderSupply`), and the accounts go home by mail with a 75-day delay for London to read them in the autumn.

## Joining the game

A player who joins when the ship is not in arrives by the Company's sloop from Churchill. The prologue is spoken aboard "out in the roads" and "Step ashore" is the first thing the player does.

## Tips

- Unload and load fast: the ship is only at the wharf for about 26 days.
- Watch your standing; the report sails with her.
- Keep three of her crew alive, or she is stranded and the Company's year stalls.
- The Nor'Westers' year closes differently: a Nor'Wester's year ends when the canoes take the returns off for Fort William (see [[North West Company]]).

## See also

[[Transport]] | [[York Factory]] | [[Hudson's Bay Company]] | [[Calendar and Seasons]] | [[Made Beaver]] | [[Brigade]]
