---
title: Design Principles
aliases: [Design Philosophy, Simulate Don't Script]
tags: [game, design, system]
status: partial
---

# Design Principles

**Status:** Partial. These are the rules the project's design document (`docs/fur-trade-game-design.md`) says every mechanic is checked against. Many are fully reflected in the code; some are aspirations that the current build only approximates. Each principle below notes how far it is built.

## 1. Simulate, don't script

> Behaviour should come from real agents pursuing real goals under real constraints, never from a scripted trigger faking the same outcome.

The test for any mechanic is whether the rule *produces* the behaviour or *fakes* it. In practice:

- **Knowledge is not ambient.** Nobody, including you, knows anything they have not witnessed or been told. News is a sealed letter that must physically arrive and be read (see [[Information and Messages]] and [[Memory and Knowledge]]). Built.
- **People are full agents.** Every named person has needs, a goal stack, memory and a personality ([[Personality]]). Built.
- **Encounters are crossings, not spawns.** A wolf pack is a real predator hunting under a real beaver-and-caribou ecology ([[Ecology]]). Built.
- **History itself is mostly pressure.** Only a small fixed core (the blockade, the colony's founding) is certain; everything else is seeded pressure. See [[Campaigns and Adherence]]. Built.

## 2. The player test

A system that is honest but invisible costs CPU and adds nothing. So a second test applies: does the player **see** it happen, **understand** why, and get a **choice** because of it? The game's answers to this are the journal that names the chain of cause and effect ([[Journal, Map and UI]]), the three-line "what matters now" panel, and NPC barks that voice their state.

## 3. Simulate, then curate: the director

A world where nothing reaches the player unless it travels would let most of its drama happen offstage. [[The Director]] biases which of the real events are put in your way: which memory a visitor brings out, which route a courier takes, when a latent pressure surfaces. It never creates an event or grants knowledge. The rule that keeps it honest: **switch it off (`--nodirector`) and the world must still be valid.**

## 4. The player is one agent among many

You have the same needs, the same action vocabulary and the same goal structure as a labourer. The same model extends to several human players sharing one world without a separate multiplayer simulation ([[Multiplayer]]). Built.

## 5. Needs create decisions, not chores

At your own post routine needs resolve themselves (the mess call, the water barrel, the hearth) the way they do for NPCs. Away from the post, needs become planning: what goes in the pack, whether to push on or make camp. A post where the stores run out is the crisis. See [[Needs and Survival]]. Built.

## 6. The year is the heartbeat

The supply ship arrives in August and London expects its packs; everything you do all year is measured against that day. Four seasonal chapters each have a stake: freeze-up (where you are posted), winter (survive and trap), breakup (the year's trade is won), and the ship (the reckoning). The HUD shows a countdown such as "N days to the ship, packs 30 of 60". See [[Calendar and Seasons]] and [[The Reckoning of 1821]]. Built, though the compressed clock currently runs a year in roughly 73 hours of play at base speed (the design target was four to six hours; see [[Calendar and Seasons]]).

## 7. Lose before you die; die into succession

Many ways to lose without dying exist: a rival company's blow that would kill you takes you **prisoner** instead, with ransom or parole available. When you do die, an heir takes your place and inherits grudges, debts, a wife's kin and unfinished ambitions ([[Heirs and Succession]]). Built.

## 8. Knowledge is your progression

The four skills ([[Skills]]) make you better at doing things; your map and journal make you *worth something to other people*. Where a herd or lodge is can be told to the Factor for standing, sold to a rival, or kept for a private trapline. Built (sites are told, not shown).

## 9. Ambitions are what you do with your survival

Survival and orders happen *to* you; an ambition (rise at the post, found a post, marry into a family, grow rich on private trade, drive the other company off the river, keep the band fed) is what you do *with* them. At most two at a time, and they are designed to pull against each other and against orders. See [[Ambitions]].

## 10. 1821 is the reckoning, not the win condition

The arc runs toward the 1821 merger, but that is the frame, not a goal. See [[The Reckoning of 1821]] and [[Setting and Timeline]].

## See also

[[Game Overview]] - [[Simulation Tiers]] - [[The Director]] - [[Development Status]]
