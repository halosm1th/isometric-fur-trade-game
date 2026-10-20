---
title: "Fur Trade Game wiki"
aliases: [Campaign Scripting]
tags: [scripting, campaign, tool]
status: reference
---

<!-- Copied from docs/wiki/Home.md by tools/docs/sync_scripting_wiki.py: edit that, not this. -->

# Fur Trade Game wiki

## Campaign scripting

A *campaign* is the story the world is nudged to follow: the dated history (the blockade, Seven Oaks, the union of the companies), the pressure the world is under as each day draws near, and how the brigades on the rivers answer to all of it. Campaigns are written in a small C-like language, checked by a tool, and compiled to the JSON the game reads.

### Learn

| Read this | When you want to |
|---|---|
| [[Getting-Started|Getting started]] | write and run your first campaign, in ten minutes |
| [[Language-Guide|The language guide]] | know exactly what the syntax means: types, constants, functions, loops |
| [[Beats-and-the-Lifecycle|Beats and the lifecycle]] | understand when each part of a beat runs, day by day, and what adherence does |
| [[Quests|Quests]] | ask things of the player: quest lines, steps, talk and choices, givers, and how quests and beats answer each other |
| [[Cookbook|Cookbook]] | copy a pattern: avertable events, forks, yearly beats, dice, reckonings, late boats, endings |
| [[Examples|Examples]] | read finished campaigns, small ones first, and the long one last |

### Look things up

| Read this | When you want to |
|---|---|
| [[Library-Reference|Library reference]] | the name and signature of every function and member: `world`, `flags`, `brigade(...)`, `news(...)` ... |
| [[World-Reference|World reference]] | the names a script can pass in: every post (with its year and postmaster), brigade run and leg, item, band camp, person and letter address; and when the game starts (and how a campaign sets its own start) |
| [[State-Flags-and-Variables|State, flags and variables]] | what the campaign remembers, the flags the game raises by itself (`late:`, `heard:`, `open_hostility` ...), history before the start, saves |
| [[Effects-in-the-Game|Effects in the game]] | what each effect really does, with numbers: tension, London, stock, brigades, letters, and what quests do to the player |
| [[Player-Deeds-and-Rumours|Player deeds and rumours]] | let the player change the story, and let them hear it coming |
| [[JSON-Format|JSON format]] | the compiled form the game reads, every condition and action verb |

### Get it right

| Read this | When you want to |
|---|---|
| [[Pitfalls|Pitfalls]] | avoid what compiles but does not do what you meant |
| [[Simulation-Scenarios|Simulation scenarios]] | play a campaign forward against an invented world, and a routine for testing one |
| [[Troubleshooting|Troubleshooting]] | decode an error message |
| [[Web-Editor|The web editor]] | edit with syntax highlighting, completion, hover help for every argument, a simulator, a graph and a reference you can open in its own tab |
| [[Command-Line-Tool|The command-line tool]] | check, build, install, simulate, draw, convert |

The shortest way to see it work:

```
dotnet run --project tools/CampaignTool -- serve
```

and open <http://localhost:8765/>.
