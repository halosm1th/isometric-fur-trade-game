---
title: Starting Paths
aliases: [Character Creation, Character Sheet, New Game, Who You Are]
tags: [starting-paths, character, system, new-game]
status: implemented
---

# Starting Paths

**Status:** Implemented (four roads in plus a fifth "another people" option). The design doc's Metis / freeman road (section 5.3) is **Planned**: there is no freeman start yet, though a "Freeman's son" background exists on the [[NWC Path]].

The new-game page ends with a page called *Who you are*. It does not build a second stat system. Every choice you make sets the same skills, tongues, kit, standing, kin and ambitions that any NPC carries, so the player is just another agent whose seed values you chose (design doc 5.4).

![[ui-crest-hbc.png|90]] ![[ui-crest-nwc.png|90]] ![[ui-crest-cree.png|90]] ![[ui-crest-assiniboine.png|90]]

## The five steps of the sheet

| Step | Choices | What it does |
|---|---|---|
| 1. Road in | HBC, NWC, Cree, Assiniboine, or "another people" | Sets faction, home post (or none), sprite, first-day scene, start date |
| 2. Background | 3 per road | Skills, rank, standing, starting kit, kin |
| 3. Traits | pick up to 2 of 8 | Skill bumps and a few items |
| 4. Ambition | one of five, or "decide later" | Puts a goal in your journal (see [[Ambitions]]) |
| 5. Camp | only for "another people" | Which band camp in the country you were born to |

The sheet is encoded as `path|background|traits|ambition|camp` and travels to the host with your name, so joiners in a shared world get the same treatment.

## The roads

| Path | Blurb (from the game) | Start | Where |
|---|---|---|---|
| [[HBC Path]] | Come ashore from the annual ship with a letter for the Factor and a five-year contract. "Rich, slow, and bound by London's orders a year late." | 20 August | York Factory wharf |
| [[NWC Path]] | Up the river in a *canot du nord* with the brigade from Fort William. "Mobile, stretched thin, and ruled by the partners in council." | 12 July | The Nor'Westers' landing across the river |
| [[Cree Path]] | Of the band that hunts the lowlands and trades at the Bay. Family, hunting grounds, and two companies who want your furs. | 10 June | The Cree summer camp by the post |
| [[Assiniboine Path]] | Of the band that winters upland and brings pemmican and furs to the posts. | 10 June | The Assiniboine camp |
| Another people | Ojibwe, Dene, Blackfoot, Sioux, Plateau, Coast, Missouri (and Metis) band camps "far from York and the Pemmican War". | 10 June (if first in the world) | The camp you choose |

Start dates only apply to the **first character of a new world**; the world calendar is set to the year's date (normally 1811) at 07:00. Anyone who joins a world already running arrives on whatever day it is (the HBC man then comes round from Churchill in the Company's sloop if the ship isn't in the roads).

## Backgrounds (three per road)

**HBC**
- *Orkney labourer* - Woodcraft 0.35, an axe.
- *Apprentice clerk* - Trade 0.4, rank clerk, standing 30.
- *Country-born* - Hunting 0.4, Cree fluency 0.7, one kinsman from the Cree band (regard 25).

**NWC**
- *Voyageur* - Woodcraft 0.3, Hunting 0.25, a tumpline.
- *Clerk from Montreal* - Trade 0.4, rank clerk, standing 30.
- *Freeman's son* - Hunting 0.45, Cree fluency 0.8, one kinsman (regard 25).

**Band roads (Cree, Assiniboine, another people)**
- *Hunter* - Hunting 0.5, Trapping 0.35, a bow.
- *Trapper and go-between* - Trapping 0.45, Trade 0.35, two steel traps.
- *Nephew of the leading man* - Hunting 0.35, and the band's leader is bound to you as kin at regard 45.

## Traits (choose up to two, `MaxTraits = 2`)

| Trait | Effect |
|---|---|
| Keen eye | Hunting +0.20 |
| Patient hands | Trapping +0.20 |
| Silver tongue | Trade +0.20 |
| Bushwise | Woodcraft +0.20 and snowshoes |
| Hardy | A point blanket and 3 dried meat |
| Ear for tongues | Cree fluency +0.30 and Assiniboine fluency +0.30 (see [[Languages]]) |
| A bottle in the pack | 3 rum ("friends at first, and a habit later") |
| Something put by | +12 trade goods |

Skills are capped at 1.0. See [[Skills]].

## Ambitions on offer

Rise at the post, Marry into a family of the country, Grow rich on private trade, Drive the other company off this river, Keep the band fed and whole. A sixth kind (Found a post of your own) exists in code but is not offered and cannot progress. Full rules on [[Ambitions]].

## Band-born characters

If you are born to a band (Cree, Assiniboine, or another people):

- Your pack is **cleared** and replaced: hide coat, snowshoes, knife, 4 dried meat, flint, fishing line, 2 firewood, 2 steel traps.
- Rank is "of the band", standing 0 (the Company's standing system does not apply to you).
- **Three** members of the camp are bound to you as kin at regard 50; each gets two shared memories ("wintered together as family", "taught you the country") so they behave as friends from day one (see [[Friends and Bonds]]).
- You speak your people's tongue at 1.0 (Cree or Assiniboine; an Assiniboine start also gets Cree 0.3 minimum).
- A camp far out in the country with nobody named in it spawns you a family: an elder (age 42-55), his wife, and a cousin (age 17-24), all bound at regard 50.

## The prologue

Every road opens on a short scene in the dialogue window, two seconds after the world settles: a hub of three optional conversations and an exit. The people in it are real NPCs and stay real. See the individual path pages for the full text and every number. The design is "a face, a temptation with a cost, and a rumour that names the men who matter" (design doc P.1, 5.5).

## Tips

> [!tip]
> Pick **Silver tongue** or the clerk background if you plan to lean on [[Trading and Barter]]. Pick **Ear for tongues** if you start outside the language of the people you want to trade with.

> [!warning]
> The "Apprentice clerk" and "Clerk from Montreal" backgrounds start at standing 30. Ranks are re-rated from standing when the ship sails, and clerk needs 35, so you must earn 5 more standing before the first sailing or you will be rated labourer. See [[Standing]].

## See also
[[Game Overview]] | [[Beginner's Guide]] | [[Player Character]] | [[Standing]] | [[Ambitions]] | [[Languages]] | [[Personality]]
