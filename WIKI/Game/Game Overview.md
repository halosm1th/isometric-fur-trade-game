---
title: Game Overview
aliases: [York Factory 1811, About the Game, The Game]
tags: [game, overview, system]
status: partial
---

# Game Overview

**Status:** Partial. The survival, trade, travel and social loops run end to end in a working vertical slice (York Factory, Oxford House, the Nor'Westers' house and the Cree and Assiniboine camps). The wider country, a Metis/freeman path, horses and many late-game systems are described in the design document but are not playable yet. See [[Development Status]].

![[ui-menu-background.jpg|500]]

*York Factory at dusk, the supply ship in the roads: the main-menu art.*

| | |
|---|---|
| **Working title** | York Factory, 1811 (subtitle on the menu: "Hudson's Bay Company - the Pemmican War years") |
| **Genre** | Isometric open-world fur-trade simulation / role-playing game |
| **Engine** | Godot 4.7 with C# (.NET 8) |
| **Default start** | 1 January 1811 on the raw calendar; a character made on the New Game page starts in summer 1811 (see below) |
| **Era** | 1811-1821, the Pemmican War years, ending at the union of the two companies |
| **Players** | Solo, or host/join multiplayer (port 7777) |
| **Perspective** | Isometric 2D, one protagonist |
| **Save system** | Single quicksave slot plus autosave |

## What the game is

You are one person in a vast, cold country, trying to make something of yourself between two companies at war, among nations that were here first. You can arrive as a new hand of the [[Hudson's Bay Company]] off the annual ship, as a Nor'Wester up with the summer brigade, or as a person born to a [[Cree]] or [[Assiniboine]] band. The tone is dark: death is real and permanent, but it is treated as a turn of the story rather than a reset, because another member of your house takes your place (see [[Health, Wounds and Illness]]).

The world is meant to be a genuine simulation rather than a backdrop. Every person is a named agent with needs, memory and goals; news has to physically travel to reach anyone; food, fuel and fur are finite. The player is mechanically one agent among the rest: you are hungry and cold in the same way a labourer is. See [[Design Principles]].

## The loop

A typical day looks like this:

1. The Factor gives the morning's orders (see [[Daily Orders]]); doing them raises your **standing**.
2. You keep your four bars up: health, warmth, food and water (see [[Needs and Survival]]), usually by tending a fire ([[Fire and Warmth]]) and eating from the stores.
3. In your own time you hunt ([[Hunting]]), run a trapline ([[Trapping]]), fish ([[Fishing]]), gather berries ([[Gathering and Gardens]]), barter ([[Trading and Barter]]) and talk to people ([[Dialogue and Conversation]]).
4. Time passes at 30 real seconds per game hour. Sleep at night ([[Sleeping and Resting]]); long trips are made by [[Travel and Wayfinding|travel mode]].
5. The year is the heartbeat: the supply ship arrives on 20 August and sails on 15 September, and the post is measured on the furs it ships (see [[The Reckoning of 1821]] and [[Calendar and Seasons]]).

## Starting a game

From the main menu: **Continue** (if a save exists), **New game**, **Multiplayer**, **Settings**, **Quit**. New game asks for:

- a **campaign** and how strictly history is held to ([[Campaigns and Adherence]]);
- a **character**: your road in (HBC, NWC, Cree, Assiniboine or another nation), a background, up to two traits and an optional ambition. See [[Starting Paths]].

A character made on the New Game page is dropped into a short prologue and the world is dated to match: **20 August 1811 for the HBC path** (the ship's day), **12 July 1811 for the NWC path** (the brigade's day), and **10 June 1811 for the band paths** (the summer camp by the post), all at 07:00. Characters started from developer arguments begin on 1 January 1811 at 09:00. The [[Beginner's Guide]] walks through the first days.

## What is in the world

- A hand-built playable area around the mouth of the Hayes River: [[York Factory]], the [[Nor'Westers' House]], [[Oxford House]], two Cree and Assiniboine camps, the river road between them, marshes, muskeg and bush. See [[World Map]].
- The wider country as a fogged atlas map (Hudson Bay to the Pacific, the Missouri villages, the Great Lakes), with named posts, camps and routes. See [[Regions]].
- A simulation of roughly nine hundred bodies in the country, only those near a player being walked about at full fidelity (see [[Simulation Tiers]]).
- A director that decides which real events reach you, never inventing any (see [[The Director]]).
- Wildlife with population dynamics: caribou, geese, beaver, wolves, hares and ptarmigan, plus buffalo (18 herds near the plains peoples' camps, drawn with caribou sprites for now) (see [[Wildlife]]).

## Not yet in the game

Taken from the design document and the project's own "longer goals" list: horses and mounted plains warfare, farming, crafting and multi-step manufacturing, characters ageing visibly with a face/clothing character creator, on-screen weather effects, a Metis/freeman starting path, and a timeline drawn back to the 1760s. See [[Roadmap]].

## See also

[[Design Principles]] - [[Setting and Timeline]] - [[Campaigns and Adherence]] - [[Controls]] - [[Beginner's Guide]] - [[Multiplayer]]
