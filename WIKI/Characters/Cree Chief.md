---
title: Cree Chief
aliases: [Band Chief, Captain, Leading Man, Hunt Captain, Homeguard Captain]
tags: [character, role, cree, nation]
status: implemented
---
# Cree Chief

**Status:** Implemented

![[char-cree_captain.png|140]] ![[sheet-char-cree_captain.png|420]]

| | |
|---|---|
| **Role value** | `Role.CreeChief` (5) |
| **Faction** | Any nation (the name is historical, the role is used for every band's leader) |
| **Behaviour** | `CreeChiefBehaviour` |
| **Sprite sheet** | `cree_captain` (idle x3, walk x3, eat, talk, smoke, death) for Cree and HBC faction; other nations' chiefs wear their nation's sheet |
| **Spawn age** | 40 to 55 |
| **Skills** | Hunting 0.5-0.8, Trapping 0.5-0.85, Trade 0.5-0.8 |
| **Kit** | Musket (York Cree, Assiniboine) or Bow, HideCoat, food |
| **Cast** | Maskwa ([[Cree]]), Tchatka ([[Assiniboine]]), Askihk (Plains Cree), Igmu (Assiniboine camp), Ajijaak (Plains Ojibwa), Hinhan (Dakota), White Antelope (Mandan), Two Marten (Kootenay), Calling Otter (Chipewyan), Spotted Hawk (Coast Salish), Yellow Antelope (Blackfoot), Antoine Hogue (Metis hunt captain) and dozens more across the atlas |

## What he does

The chief is the band's leader and its trader. When the camp is not otherwise occupied he tends the fire (threshold 3) or wanders the camp. He goes to the post himself when:

- the band's furs are worth at least **8 MB**, or
- the camp's food is under 4 days per mouth and there is anything to trade or the post's standing is above 40, or
- the Assiniboine have **pemmican to sell** (over 6 per mouth plus 12), or
- the post has invited the band to trade.

He chooses between counters by **score = pelt price x (0.5 + standing/100) / (1 + walk/4000)**, multiplied by 1 + rum taste/200 if the counter has rum, and by 1.15 if the band owes that counter. He will not trade with a post with fewer than 5 pieces of goods, nor with one whose standing is 20 or less. The Nor'Westers are across the river: only the ford or the ice gets him there ([[Trading and Barter]], [[Standing]], [[Credit and Debt]]).

## Why he matters

- He is the band's Tier 1 decision-maker: whom to trade with, where to winter ([[Band Ledger]]).
- A player born to a band is often the **nephew of the leading man**, bound to him by regard 45 ([[Cree Path]]).
- Standing with a band is largely standing with its chief.

## Succession

When the chief dies, the band's most ambitious hunter takes his place with "(chief)" added to his name and takes charge of the dead man's children ([[Heirs and Succession]]).

## Trivia

- The trading captain's sheet is Cree; a Blackfoot or Mandan chief wears his own nation's sheet instead ("Appearance.cs").
- The Metis hunt captain of the Forks wears the voyageur sheet.
- The design asks that the Homeguard captain at York get a carefully chosen, researched name, "better invented than borrowed from a real person" (`docs/world-content-needed.md`).

## See also

[[Cree Hunter]], [[Cree Woman]], [[Cree]], [[Assiniboine]], [[Factions]], [[Roles]]
