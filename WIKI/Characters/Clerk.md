---
title: Clerk
aliases: [Writer, Apprentice Clerk, Accountant]
tags: [character, role, hbc]
status: implemented
---
# Clerk

**Status:** Implemented

![[char-clerk.png|140]] ![[sheet-char-clerk.png|420]]

| | |
|---|---|
| **Role value** | `Role.Clerk` (1) |
| **Faction** | `HBC`, or `NWC` for Nor'Westers' clerks |
| **Behaviour** | `ClerkBehaviour` |
| **Sprite sheet** | `clerk` (idle x3, walk x3, eat, drink, write, death) |
| **Spawn age** | 19 to 28 |
| **Skills** | Trade 0.5 to 0.8, the rest low |
| **Starting kit** | Capote, Knife, Bread |
| **Cast at York** | John Fidler and Alex Kennedy |

## Daily routine

- **Counter work.** A clerk wanders near his post's counter (`HomeStores.AccessCell`), not a fixed place, so a clerk at Edmonton House stays at Edmonton House.
- **Fire duty.** If any hearth in the house is low (threshold 3), a clerk stokes it or cuts wood.
- **Career competition.** An ambitious clerk (Ambition over 0.6) who has heard something bad about a rising rival can, between 09:00 and 10:00, quietly tell the Factor; the rival loses 2 standing. This only happens about a quarter of the time and only if the target out-ranks him ([[Gossip, Lies and Leverage]], [[Rivalry]]).

## The rivalry for promotion

When a player means to rise ("Rise at the post"), another clerk at the same post means to rise too (`Rival.cs`). The seat goes to whichever the Factor would rather have. Score = standing + 12 x ambition (+ 10 if clerk, + a quarter of the leader's rapport). The rival is a real man: outwork him, make a friend of him, or carry something true about him to the Factor and see if it is believed ([[Ambitions]]).

When an inland post loses its master, the clerk with the best name is sent up to take charge: the posting is the prize ([[Postmaster]]).

## Notes

- Several inland posts in the scene have men named "(clerk)" who are labourers in behaviour. The comment in `RoleBehaviour.cs` explains: the clerk behaviour used to walk to York's depot by name, so planned posts use labourer behaviour.
- A player with the "Apprentice clerk" background begins at rank clerk with standing 30 and 6 extra trade goods at each reckoning ([[Player Character]]).

## See also

[[Factor]], [[Postmaster]], [[Labourer]], [[Hudson's Bay Company]], [[Roles]]
