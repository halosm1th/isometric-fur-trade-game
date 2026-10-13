---
title: Campaigns and Adherence
aliases: [Campaign Scripts, Adherence, Historical Campaign, The Plains Ablaze]
tags: [game, campaign, system, history]
status: implemented
---

# Campaigns and Adherence

**Status:** Implemented. Code: `Scripts/Core/Campaign.cs`, `CampaignRunner.cs`, `CampaignLogic.cs`; scripts: `Campaigns/*.json`, written in the campaign language in `tools/CampaignTool/src/*.camp`. **To write campaigns, see the campaign scripting wiki in [`docs/wiki`](../../docs/wiki/Home.md)**: the language, a cookbook, and full references to every post, brigade, item, person and flag a script can use.

A **campaign** is a JSON file of dated *beats* (the blockade, the Pemmican Proclamation, Seven Oaks, the merger) that the [[The Director|director]] works toward. **Adherence** is a dial on how hard the world is held to that script. Both are chosen on the New Game page of the main menu. Nothing in a campaign is announced to you directly: every beat becomes a sealed letter that must arrive and be read ([[Information and Messages]]), so you hear about Seven Oaks weeks after it happens, from whoever carries the news.

## Choosing a campaign

| Choice | What it is |
|---|---|
| **No script** | The world runs on its own pressures. Posts that stand at the start date exist; later ones appear only if someone founds them. |
| **The Pemmican War, 1811-1821** (`historical.json`) | The shipped historical arc: Selkirk's colony, the Pemmican Proclamation, Seven Oaks, Fort William seized, the lawless years, and the union of the companies; then nine beats that act on the brigades. 22 beats. |
| **The Company Years, 1811-1830** (`company-years.json`) | The long one, in five acts: the same arc, then Simpson's council, the Columbia and the Red River flood. Seven Oaks can be averted by a quiet spring or a truce. 59 beats. |
| **The Long Peace, 1811-1821** (`the-long-peace.json`) | The same grant and blockade, but the war is only a possibility: a truce the player brokers turns the Proclamation into a bargain and Seven Oaks is averted. 30 beats. |
| **The Rivers Run, 1811-1821** (`the-rivers-run.json`) | The brigades: late, lost, robbed and wintering boats, early ice, and a Committee that counts the troubles. Riding a brigade in or carrying a packet is noticed. 18 beats. |
| **Landfall: Blood on the Bay, 1811** (`blood-on-the-bay.json`) | Action from the first hour at York Factory: a squall, thieves, the Nor'Westers at the gate, an ambush on the Hayes, fire, hunger and a siege, all before winter. A truce or a relief brigade can turn it. 33 beats. |
| **The Plains Ablaze (1801-1821)** (`Gemini and Clark.json`) | The arc plus an alternate-history opening: the Lewis and Clark expedition is destroyed and the U.S. Army invades the Plains. 13 beats. Written as JSON only (the editor can convert it). |
| **My campaign** (user folder) | `user://campaigns/*.json`. The menu button "Open the campaigns folder" creates `my-campaign.json` (a copy of the historical script) and a `README.txt` describing the format. |

All the shipped scripts set `posts_by_founding_year: true`: posts in the atlas appear on 1 January of their founding year.

## The four adherence levels

| Level | Menu text | What the world may do |
|---|---|---|
| **Gentle** | "the world finds its own way" | Only fixed facts (the blockade, a post's founding) and conditional beats are certain. The director favours news that bears on what history has coming, and nothing more. |
| **Steady** (default) | "pressure toward the script" | As Gentle, and each event's *pressure* actions run as its date nears (tension between the companies, London's orders). Whether the event happens is up to the world. |
| **Firm** | "late events are made to happen" | As Steady, and an event the world has not produced by **twice its grace window** is forced. |
| **Strict** | "events happen on time" | As Steady, and every event happens once its grace window runs out. |

Forced beats are logged as `[forced]`. Gentle is the design document's director exactly: with it, switching the script off still leaves a valid world. Firm and Strict break that on purpose. Command-line overrides: `--campaign=ID`, `--adherence=Gentle|Steady|Firm|Strict`.

## Beat kinds

- **fixed**: happens on its date at every adherence (outside anyone's reach).
- **conditional**: happens `delay_days` after its requirements hold, at every adherence.
- **event**: the world may produce it itself (`done_when`); at Firm/Strict it is forced after its window.
- **pressure**: only its pressure actions, from Steady upward.

Actions a beat can run: `flag`, `unflag`, `tension` (rivalry, 0-100), `london` (multiplies the next year's pelt price and quota), `found_post`, and `news` (a sealed letter delivered after N days to a named addressee: the Factor at York Factory, the wintering partner, the postmaster at Oxford House or the chief of the band).

A beat dated before the first player's first day (10 June 1811 for the Cree and Assiniboine, 12 July for the North West Company, 20 August for the Hudson's Bay Company) counts as already history: its flags hold (and a post it founds stands) but nobody is sent news of it and nothing else it does is carried out. So the blockade (1 June) is already history on every path; Selkirk's grant (12 June) is history for the Company and the Nor'Westers, and news two days in for a camp start. On a dedicated server, history is decided when the first player joins. Campaign progress is part of the save ([[Save and Load]]).

## The historical script, beat by beat

The thirteen beats of the main Pemmican War arc (the script also has nine dateless beats that act on the brigades: the war slowing the Montreal canoes, the Proclamation emptying their provisions, ambushes in the interior, the York boats late for the ship, and the canoes stood down after the union).

| Beat | Date | Kind | Window / lead | Effect |
|---|---|---|---|---|
| Continental Blockade | 1811-06-01 | fixed | | London price and quota x0.85; flag `blockade` |
| Selkirk's grant of Assiniboia | 1811-06-12 | fixed | | Flag `red_river_colony`; letter to the Factor, 70 days |
| War of 1812 | 1812-06-18 | fixed | | Letters to the NWC partner (75 days) and the Factor (400 days) |
| Astoria sold (Fort George) | 1813-10-16 | fixed | | Flag `fort_george`; letter to the NWC partner (250 days) |
| Pemmican Proclamation | 1814-01-08 | event | 60 d window, 120 d lead | Needs the grant. Pressure: tension +10. Happens: tension +20; letters to the NWC partner (45 d) and the Factor (120 d) |
| Peace in Europe (blockade lifted) | 1814-05-30 | fixed | | Removes `blockade`; London price x1.18, quota x1.15; letter, 80 d |
| Colony dispersed | 1815-06-25 | event | 60 d | Needs the Proclamation. Pressure: tension +8. Happens: tension +15 |
| Battle of Seven Oaks | 1816-06-19 | event | 45 d | Needs the Proclamation. Pressure: tension +15. Happens: tension +25, flags `seven_oaks` and `governor_killed`; letters to the Factor (35 d) and NWC partner (60 d) |
| Selkirk takes Fort William | none | conditional | delay 420 d | Needs Seven Oaks and `governor_killed`. Tension +10; letter, 70 d |
| Interior war 1818 | 1818-06-01 | pressure | | Needs Seven Oaks. Tension +15 |
| Interior war 1819 | 1819-06-01 | pressure | | Needs Seven Oaks. Tension +15 |
| Interior war 1820 | 1820-06-01 | pressure | | Needs Seven Oaks. Tension +10; London price x0.85, quota x0.9 |
| The union of the companies | 1821-03-26 | event | 120 d window, 365 d lead | Pressure: London x0.9. Happens: tension -70; letters to the Factor (90 d) and NWC partner (75 d) |

## The Plains Ablaze: the extra beats

This file keeps the nine 1811-1821 beats above (without the three "interior war" pressure beats and the peace beat) and adds four at the front:

| Beat | Date | Kind | Effect |
|---|---|---|---|
| Lewis and Clark depart St. Louis | 1804-05-14 | fixed | Flag; letters to the NWC partner (60 d) and the Factor (120 d) |
| Lewis and Clark destroyed | 1805-09-15 | event (60 d window, 60 d lead) | Flag `lewis_clark_killed`; tension +20; letters from Fort Mandan and Red River |
| U.S. punitive expedition | none | conditional, delay 90 d | Needs the destruction. Flag `us_military_retaliation`; tension +40; London price and quota x0.6; letters about General Wilkinson's army |
| Plains warzone | none | conditional, delay 180 d | Needs the invasion. Flag `plains_warzone_crisis`; tension +25; letters about pillaged villages and refugees |

Because the game's calendar starts in 1811, the two 1804-1805 beats are already in the past when a new world begins. The code marks past-dated beats as done at start, so on a default start the Lewis and Clark flags are set and the invasion conditional appears to become ready from day one, firing about 90 days in, with the warzone about 180 days after that. This is inferred from the code, not confirmed in play.

## Writing your own

Campaigns are written in a small C-like language and compiled to this JSON by the campaign tool, which also checks them and plays them forward without the game:

```
dotnet run --project tools/CampaignTool -- serve
```

opens an editor at <http://localhost:8765/>, with completion, live checking, a simulator and a graph; **Install in game** puts the campaign in the menu. Everything about it (the language, when each part of a beat runs, a cookbook of patterns, and references to every post, brigade run, item, person, flag and effect) is in the campaign scripting wiki, [`docs/wiki`](../../docs/wiki/Home.md).

A campaign can also be written directly as JSON in the campaigns folder from the New Game page (`my-campaign.json`); the menu re-reads the folder when the page opens. The format is in the scripting wiki's *JSON format* page and in the folder's `README.txt`.

## See also

[[Setting and Timeline]] - [[The Director]] - [[Information and Messages]] - [[Rivalry]] - [[London Committee]] - [[The Reckoning of 1821]] - [[Save and Load]]
