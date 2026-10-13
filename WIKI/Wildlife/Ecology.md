---
title: Ecology
aliases: [Wildlife Ecology, Spawn Sites, Population Dynamics]
tags: [wildlife, system, simulation]
status: implemented
---

# Ecology

**Status:** Implemented (design doc 16.4, 16.5). Not yet built: the ten-year hare/lynx cycle, fragmenting herds, horses and mounted hunts, fish as animals.

The ecology is a simulation of **numbers first, bodies second**. Each wildlife site holds a count; a weekly tick grows or shrinks it; live animals are "promoted" out of the count only when someone is close enough to see them. Everything is in `Scripts/Ecology/` (`Species.cs`, `EcologyManager.cs`, `Trapline.cs`), `Scripts/World/WildlifeSite.cs` and `Scripts/Agents/Animal.cs`.

![[animal-wolf.png|160]] ![[animal-caribou.png|160]] ![[animal-beaver.png|160]]

## The site (SpawnSite)

| Field | Meaning |
|---|---|
| Count | Abstract remainder plus living promoted animals |
| Capacity | Carrying capacity |
| Breeding capacity | 1 normally; 0 means the lodge or den is gone for good |
| Food stock | Local browse, 0 to 1 |
| Scarcity | True when over capacity and browse is eaten out |
| Route | Twelve cells (Jan to Dec) for a mobile herd, otherwise none |
| Traplines | Steel traps set by owners at a beaver lodge |

Sites are placed in the editor as `WildlifeSite` nodes ([[Species Sites]]). They are invisible in game.

## The weekly tick

Once a game week (`EcologyManager._Process`):

1. **Growth.** Each site's yearly regrowth rate is divided by 52. Prey grow logistically: `Count += r * Count * (1 - Count / Capacity)` (plus a small 0.1 trickle below 2 animals). Regrowth per year: caribou 0.35, goose 1.2, beaver 0.5, wolf 0.4, hare 2.5, ptarmigan 1.5, buffalo 0.3.
2. **Predators track prey.** Wolf growth is multiplied by `clamp(preyPressure * 2 - 0.6, -1.5, 1.5)`, where prey pressure is total prey count over total prey capacity. Plenty of game and the packs grow; thin game and they shrink (never below 1).
3. **Browse.** Over capacity, browse drops by `(over - 1) * 0.6` per week; under capacity it regrows by 0.15.
4. **Scarcity.** If browse falls under 0.35 while the site is over 95% of capacity, the site enters scarcity: "too many animals for the ground. They are hungry and ranging toward the post." Animals spawned from a scarce site are `Hungry` and, being hungry, are dangerous to people even if they are normally prey. A scarce site settles again when browse recovers.
5. **Migration and passage** for mobile sites (below), and the **goose migration**.

## Two ways to lose a site

| Failure | Cause | Result |
|---|---|---|
| **Overharvest (fixed sites)** | Count falls below `capacity x sustain floor` (caribou 0.2, goose 0.15, beaver 0.25, wolf 0.2, hare 0.1, ptarmigan 0.1, buffalo 0.25) | Breeding capacity goes to 0. "X has been hunted out. It will not recover." Remaining count bleeds away 0.5 a week. Permanent. |
| **Overpopulation** | Left alone past capacity | Scarcity state; hungry animals range toward the post (see above). |

A **mobile herd** hunted below its floor does not die; it *scatters*: "the deer have been worked so hard they have quit this country". Count drops to 1 and nothing is to be had until the herd next passes (or returns thin in spring, 40% of capacity in May).

## Herds that move

A site with a twelve-marker route is a herd (all caribou herds, all buffalo plains). Each week it moves about 40% of the remaining distance toward the current month's marker, with a random monthly drift of up to 3 cells east-west and 6 north-south, so it is not a timetable. Only people who see the herd move know it has; scouting reports go stale for a physical reason. When a herd moves more than 200 px in a week, the [[Information and Messages|chronicle]] records "the caribou were on the move, heading north toward the coast / south into the bush".

**The passage.** In April, May, September and October every mobile site is "passing": the count is raised to at least 70% of capacity in spring or 100% in autumn ("the deer are passing - the country is full of caribou"). In the other months it drops to at most a quarter of capacity ("the main herd has gone on. A few deer winter in the country"). This logic was written for the caribou; the code runs it on every mobile site, so the buffalo herds are subject to the same counts and the same notice text.

A game started midsummer places herds at that month's marker on the first frame rather than in January quarters.

## Geese

Geese are the only species with a hard migration: from November to March every goose site is set to 0 ("The geese have gone south"); in April it fills to 70% of capacity. The [[Goose]] page covers the 20 August to 15 October autumn flight, when the marsh is temporarily set to eight times capacity.

## Promotion and demotion

`EcologyManager.Promote` runs every 1.5 seconds. A site within **2600 px** of any live player (or any scarce site) spawns real `Animal` agents up to a per-species cap, and animals more than **3600 px** from every player fold back into the count.

| Species | Max live at once per site |
|---|---|
| Buffalo | 9 |
| Goose | 12 |
| Ptarmigan | 8 |
| Caribou | 7 |
| Wolf, Hare | 5 |
| Beaver | 3 |

A promoted animal is a full agent with its own needs and a small state machine (graze, flee, hunt, feed). Kills decrement the site. See [[Simulation Tiers]] for the wider scheme.

## Animal behaviour

- **Prey** graze around the home site and flee from any human within 260 px (a sneaking hunter "reads as much farther away": distance is divided by a stealth factor of 0.4). They flee from the sound of a shot for 8 seconds and from footsteps for 3.
- **Predators** (wolves) and **hungry** animals hunt. They pick the closest person or (for wolves) prey animal within 900/700 px, and prefer prey animals to armed men (an armed target's distance is counted 1.6 times). Failing that they drift toward the post's store. Wolves attack with a 55 px reach and 1.3 s cooldown.
- A wolf has a 60% chance to bolt at a shot.
- Dead animals leave a carcass that rots away after 36 game hours; you can butcher it (E) and take as much meat as your pack holds, with hides and pelts always coming first. Meat you cannot carry stays on the carcass for whoever comes back.

## Traplines and poaching

Steel traps are set at a beaver lodge, up to six at a time. Each trap catches, overnight (processed once a day after 05:00), with chance `0.07 x lodge density x (0.6 + skill x 0.8)` and removes one beaver from the lodge. A line is ready to check after 18 hours. Anyone at the lodge who does not own a line may help themselves with a chance of `boldness x 0.6` per line; the victim learns "someone has been at your traps". Rival parties on the same lodge also reduce each other's pickings (a second party working a ground in one day gets 0.45 of the yield). Poaching feeds the rumour system: a robbery can become "I know you robbed the traps" leverage, and an NPC's lie about a rival may be that "the other side's men robbed your traps". See [[Trapping]], [[Gossip, Lies and Leverage]] and [[Rivalry]].

## Who knows the ground

Sites are not known by default. NPCs learn a site by passing within 60% of its radius (a player, 70%), the household stores its belief of where it is, and herds are re-believed as they move. `BestSite` scores each *known* site by its count, discounted for rival traplines, thin stock and the walk. `BestBigGame` returns buffalo before caribou for a house that knows a buffalo ground. A house's "country" is the grounds within **24,000 px (about 600 km)** of home.

## Dev tools

`-- --wolves` drops a hungry pack outside the gate; `-- --buffalo` puts four buffalo beside the player; `-- --scarcity` forces scarcity everywhere; `-- --herdmoved` snaps herds to this month's marker; `-- --spawn=hares` / `--spawn=ptarmigan` spawn those animals; the F1 test console has buttons for a wolf pack, starving caribou and a buffalo at the gate.

## See also

[[Wildlife]] | [[Species Sites]] | [[Hunting]] | [[Trapping]] | [[Simulation Tiers]] | [[Architecture]] | [[The Director]]
