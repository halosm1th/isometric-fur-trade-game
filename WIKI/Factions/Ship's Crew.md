---
title: Ship's Crew
aliases: [ShipsCrew, Sailors, The Ship]
tags: [faction, hbc, ship, role]
status: implemented
---
# Ship's Crew

**Status:** Implemented

![[char-sailor.png|130]]

| | |
|---|---|
| **Faction value** | `ShipsCrew` (enum 2) |
| **Home** | The Company's annual supply ship ([[Ship]]) |
| **Language** | English |
| **Sprite sheet** | `sailor` |
| **Head count** | 8 sailors per visit |
| **Arrives / sails** | 20 August / 15 September |

## What they are

The ship's company is a faction apart from the Company men at the post. It is spawned on the wharf when the ship ties up (`Population.SpawnCrew(8)`), takes its names from a list (Bos'n Hurst, Ned Carrow, Tom Ellery, Will Pascoe, Sam Rudd, Jack Tremayne, Davy Roskilly, Harry Penrose), and carries a Knife, three Bread and a Capote each. The first two also carry a Musket.

For the person who plays the HBC path, the opening prologue is the last day of the crossing. A sailor, Davy Gunn, offers brandy at three beaver a keg. If you buy it and a mate sees, a witness may report you at the ship's reckoning; if you tell the mate, you gain a little standing and an enemy among the crew ([[HBC Path]]).

## Behaviour

- Sailors only have a purpose while the ship is docked. While she is away or at sea they idle at the wharf end.
- At night they wander the wharf. In the day they carry cargo ashore (outfit from the hold) and then carry furs aboard once the outfit is off ([[Sailor]]).
- The ship cannot sail with too few of her crew alive. The notice is "The ship cannot sail: too few of her crew are left alive. She lies at the wharf."
- Crew do not join a household's evening hearth routine (`Role.Sailor` is excluded) and have no home stores of their own; they eat from the Fort's stores while ashore.

## Why it matters

The crew moves the furs that decide whether the Company meets its quota ([[Fort Ledger]], [[London Committee]]). A raid or fever at the wrong time can leave the ship stranded.

## See also

[[Sailor]], [[Ship]], [[Hudson's Bay Company]], [[York Factory]], [[Transport]]
