---
title: Characters
aliases: [People, Cast, NPCs]
tags: [character, index]
status: implemented
---
# Characters

**Status:** Implemented

Every person in the world is an `Agent`: the player, each named [[Roles|role]]-holder, and the children born in the game. They share needs, an inventory, skills, a memory and the same verbs, and they differ in the goal stack that drives them (design doc 11.1). This section covers the player, the thirteen roles, and the historical people who are named in the campaign files and design notes.

![[char-player.png|110]] ![[char-factor.png|110]] ![[char-voyageur.png|110]] ![[char-cree_captain.png|110]] ![[char-country_wife.png|110]] ![[char-sailor.png|110]]

## The player and the roles

| Page | What it covers |
|---|---|
| [[Player Character]] | Character creation, backgrounds, traits, ranks, the `player` sheet |
| [[Roles]] | The 13 values of the `Role` enum, behaviours, sprite mapping |
| [[Factor]] | Head of York Factory (and a general post master) |
| [[Clerk]] | The writer's desk and the rival for promotion |
| [[Labourer]] | Orkney hands: the post's workforce and its fighters |
| [[Postmaster]] | The master of an inland post, e.g. Oxford House |
| [[Sailor]] | Crew of the supply ship |
| [[Voyageur]] | French-Canadian paddlers of the North West Company |
| [[NWC Partner]] | The bourgeois at the Nor'Westers' house |
| [[Country Wife]] | The women of the Company posts |
| [[Cree Chief]] | The leader of a band, of any nation |
| [[Cree Hunter]] | A band's hunters and trappers |
| [[Cree Woman]] | The women and daughters of a band |
| [[Children]] | Children of the post, born to a player's marriage |

## Historical people

[[Historical People]] gathers the real persons who are named in the campaign JSON, the council code, the cast, the atlas notes or the design doc. Each has its own page saying who they were and whether they appear in the build:

[[George Simpson]], [[Lord Selkirk]], [[Simon McTavish]], [[William McGillivray]], [[Simon McGillivray]], [[Cuthbert Grant]], [[Robert Semple]], [[Miles Macdonell]], [[David Thompson]], [[Alexander Mackenzie]], [[Lewis and Clark]], [[John Jacob Astor]], [[Peter Fidler]], [[John McLoughlin]], [[William Williams]], [[Duncan Cameron]], [[William Auld]], [[Alexander Henry the younger]], [[Manuel Lisa]], [[Colin Robertson]], [[Peguis]], [[Pierre Falcon]], [[Jean-Baptiste Lagimodiere]], [[Samuel Hearne]], [[James Wilkinson]], [[John McDonald of Garth]], [[Roderick McKenzie]], [[Duncan McDougall]].

## Numbers

- The scene `YorkFactoryExterior.tscn` places the whole starting cast through `NpcSpawn` markers: **York Factory** (1 Factor, 2 clerks, 6 labourers, 3 women), the **Cree band** (1 chief, 4 hunters, 4 women, 2 daughters), the **NWC house** (1 partner, 3 voyageurs), the **Assiniboine** (1 chief, 3 hunters, 3 women, 1 daughter) and **Oxford House** (1 postmaster, 3 labourers, 1 woman).
- Beyond York the same scene holds 86 postmasters, 112 labourers, 10 women and 82 voyageurs for planned posts, plus 38 chiefs, 266 hunters, 232 women and 104 daughters for the atlas's band camps. They are spawned only when their post stands.

## See also

[[Factions]], [[Skills]], [[Personality]], [[Heirs and Succession]], [[Friends and Bonds]], [[Marriage and Country Wives]]
