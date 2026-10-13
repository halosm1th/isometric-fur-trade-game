---
title: Roles
aliases: [Role enum, NPC Roles]
tags: [character, system]
status: implemented
---
# Roles

**Status:** Implemented

`Scripts/Agents/Npc.cs` defines thirteen values for `Role`. Role chooses the behaviour class that runs when an NPC's goal stack is empty (`RoleBehaviour.cs`), the starting skills, the carry weight, and which sprite sheet `Appearance.cs` gives them.

| Value | Page | Behaviour class | Sprite sheet |
|---|---|---|---|
| 0 Factor | [[Factor]] | `FactorBehaviour` | `factor` |
| 1 Clerk | [[Clerk]] | `ClerkBehaviour` | `clerk` |
| 2 Labourer | [[Labourer]] | `LabourerBehaviour` | `labourer` |
| 3 Woman | [[Country Wife]] | `HouseholdBehaviour` | `country_wife` (HBC) or a nation's women |
| 4 Postmaster | [[Postmaster]] | `FactorBehaviour` | `clerk` |
| 5 CreeChief | [[Cree Chief]] | `CreeChiefBehaviour` | `cree_captain`, or the nation's sheet |
| 6 CreeHunter | [[Cree Hunter]] | `CreeHunterBehaviour` | the nation's sheet |
| 7 CreeWoman | [[Cree Woman]] | `CreeWomanBehaviour` | `cree_woman` |
| 8 CreeDaughter | [[Cree Woman]] | `CreeWomanBehaviour` | `cree_woman` |
| 9 Sailor | [[Sailor]] | `SailorBehaviour` | `sailor` |
| 10 Wife | [[Country Wife]] | `HouseholdBehaviour` | `country_wife` |
| 11 NwcPartner | [[NWC Partner]] | `FactorBehaviour` | `nwc_partner` |
| 12 Voyageur | [[Voyageur]] | `LabourerBehaviour` | `voyageur` |

The enum order is stored in saves, so new roles are appended.

## Sprite mapping (`Appearance.For`)

- Children: any HBC person with a father's name set and under 14 wears `child_boy` or `child_girl` ([[Children]]).
- Voyageur, NwcPartner, Sailor, Factor have their own sheets; Clerk and Postmaster share `clerk`.
- A Woman or Wife of the HBC wears `country_wife`.
- A chief of `Cree` or `HBC` faction wears `cree_captain`; other nations' chiefs and hunters wear their nation's sheet (`ForNation`).
- Women and daughters of every nation but the Metis wear `cree_woman`; Metis women wear `country_wife`.
- `ForNation(f)`: Metis `voyageur`, Blackfoot `blackfoot`, Dene `dene`, Coast `coast`, Sioux `dakota`, Missouri `mandan`, Plateau `plateau`, Ojibwe `ojibwe`, Assiniboine `assiniboine`, anything else `cree`.
- The `rival` sheet is the oldest, 13 frames (idle, walk, walk_back, eat) and is now used by the NWC player and as a stand-in for Nor'Westers.

![[char-rival.png|110]] ![[sheet-char-rival.png|360]]

## Seeded skills

Skills run 0 to 1 (green below 0.3, passable to 0.5, practised to 0.75, master above).

| Role group | Hunting | Trapping | Trade | Woodcraft |
|---|---|---|---|---|
| Cree hunter, chief | 0.5-0.8 | 0.5-0.85 | 0.15 (chief 0.5-0.8) | 0.3-0.6 (hunter) |
| Labourer | 0.15-0.4 | 0.1-0.35 | 0.15 | 0.3-0.6 |
| Voyageur | 0.15-0.4 | 0.35 | 0.15 | 0.3-0.6 |
| Factor, clerk, partner, postmaster | 0.1 | 0.1-0.35 | 0.5-0.8 | 0.15 |
| Everyone else | 0.1 | 0.1-0.35 | 0.15 | 0.15 (Cree woman, Woman 0.3-0.6) |

## Start ages

Factor 46-58, NwcPartner 35-50, CreeChief 40-55, Clerk 19-28, CreeDaughter 16-20, Sailor 18-40, everyone else 20-38. Everyone ages one year at New Year, and over 45 slows by 3 per cent a year down to speed 80.

## Fighters

Only men holding a weapon in the roles Labourer, Voyageur, CreeHunter and Sailor are "fighters" and turn out to defend a house ([[Combat]], [[Rivalry]]). Leaders, clerks and women do not.

## Succession

If a leader dies: the most ambitious clerk (by standing plus ten times ambition) becomes acting Factor; a postmaster's post is left without a master and the steadiest labourer keeps the men together; the band's most ambitious hunter becomes chief ([[Heirs and Succession]]).

## See also

[[Characters]], [[Daily Orders]], [[Delegation]], [[Skills]], [[Personality]], [[Simulation Tiers]]
