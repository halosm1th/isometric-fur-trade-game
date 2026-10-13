---
title: Sailor
aliases: [Seaman, Ship's Hand, Hand of the Supply Ship]
tags: [character, role, ship]
status: implemented
---
# Sailor

**Status:** Implemented

![[char-sailor.png|140]] ![[sheet-char-sailor.png|420]]

| | |
|---|---|
| **Role value** | `Role.Sailor` (9) |
| **Faction** | `ShipsCrew` ([[Ship's Crew]]) |
| **Behaviour** | `SailorBehaviour` (overrides the full `NextGoal`) |
| **Sprite sheet** | `sailor` (idle x3, walk x3, drink, attack, haul, death) |
| **Spawn age** | 18 to 40 |
| **Skills** | All low (Hunting 0.1; Trade 0.15; Woodcraft 0.15) |
| **Kit** | Knife, 3 Bread, Capote; two of eight also a Musket |
| **Tint / scale** | Pale blue (0.75, 0.8, 0.9), 0.78 |
| **Names** | Bos'n Hurst, Ned Carrow, Tom Ellery, Will Pascoe, Sam Rudd, Jack Tremayne, Davy Roskilly, Harry Penrose |

## Routine

Sailors have no household schedule. They exist when the ship is in:

1. Ship away or sailing: wander the wharf end.
2. Docked, night: wander the wharf, slowly.
3. Docked, outfit in the hold: **carry cargo** from the ship to the depot.
4. Outfit landed, furs in the depot and loading open: **carry furs** aboard.
5. Otherwise: wander the wharf.

The ship sails when loaded on 15 September, if enough crew are alive ([[Ship]]).

## Combat

A sailor with a weapon counts as a fighter: he will defend the wharf. He does not join the evening hearth ritual.

## Prologue appearance

On the HBC start a sailor, **Davy Gunn**, offers brandy to the new hand: two goods per keg, six drams for four pieces. Buying it is a temptation with a cost; telling the mate wins a little Company standing and the crew's ill-will ([[HBC Path]], [[Ship's Crew]]).

## See also

[[Ship]], [[York Factory]], [[Transport]], [[Roles]]
