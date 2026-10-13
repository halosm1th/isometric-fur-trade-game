---
title: Languages
aliases: [Fluency, Tongues, Interpreters, Cree Fluency]
tags: [system, language, social]
status: implemented
---

# Languages

**Status:** Implemented (design doc 10.6). Language is a **barrier and a currency**: the trade ran on Cree in the north and on Nakoda (and signs) on the plains, but a man with good Cree is no better off in a Blackfoot lodge than in a Spanish one.

## What each people speaks

| Faction | Language (game name) |
|---|---|
| Cree | Cree |
| Ojibwe | Ojibwe |
| Assiniboine | Assiniboine (Nakoda) |
| Sioux | Dakota |
| Blackfoot | Blackfoot |
| Dene | Chipewyan |
| Plateau | "the Plateau tongue" |
| Coast | "the Coast tongue" |
| Missouri villages | Mandan |
| Metis | Michif |
| everyone else (HBC, NWC, ship's crew) | English |

## How far one tongue carries into another

`Languages.Kin(mine, theirs)` returns 1.0 for the same tongue and:

| Pair | Overlap |
|---|---|
| Cree - Metis | 0.8 |
| Assiniboine - Sioux (Nakoda and Dakota) | 0.8 |
| Cree - Ojibwe | 0.6 |
| Ojibwe - Metis | 0.5 |
| Assiniboine - Missouri | 0.3 |
| Sioux - Missouri | 0.3 |
| Cree - Blackfoot | 0.1 |
| Anything else | 0 |

Chipewyan is Athabaskan; the Plateau and Coast tongues are neither Algonquian nor Siouan; and Blackfoot, "for all that it is Algonquian, cannot be heard by a Cree speaker".

## Your two learned tongues

The player carries two numbers: **Cree fluency** and **Assiniboine fluency** (0 to 1). Your fluency with a given person is:

1. Cree fluency x Kin(Cree, their tongue)
2. Assiniboine fluency x Kin(Assiniboine, their tongue)
3. If you are a band person, your own people's Kin with theirs
4. A wife or woman of the post nearby gives full fluency (see below)

and the best of those. Defaults: Cree 0.1, Assiniboine 0.05. Cree-born players start at 1.0; Assiniboine-born at 1.0 (plus Cree 0.3 minimum); another people's Ojibwe/Metis get Cree 0.6, others 0.15; Sioux get Assiniboine 0.6. The country-born and freeman's-son backgrounds give Cree 0.7 and 0.8; the "Ear for tongues" trait adds 0.3 to both. See [[Starting Paths]].

What you learn goes to whichever of the two languages is nearer the speaker's; **every talk with a band person who is not interpreted adds +0.02**. A marriage adds +0.3 for the wife's tongue. The journal's Pack tab names the level: fluent (0.7+), getting by (0.4+), a few words (0.3+), signs only.

## What the level does

| Fluency | Effect |
|---|---|
| Below 0.3 | You cannot say much: "Tell them something...", rumours and family matters are unavailable; family says "You have not the words for it" |
| Below 0.4 | Greeting warns "you have little of their tongue" |
| Below 0.5 | Barter is **crude**: -0.08 margin, +20 percent cost on a purchase, press chance -0.12, and tells are blunted ([[Trading and Barter]]) |
| Below 0.5 (gossip) | 35 percent chance each line is `Retold` and arrives bent, flagged "(as best you follow it)" ([[Gossip, Lies and Leverage]]) |
| 1.0 | Interpreter or fluent |

## Interpreters

- **Your wife** within 300 px interprets for any people. So does **any Wife or Woman of the Company's post** within 300 px of you, married or not (the "HasInterpreter" check), which is why a woman of the post is the usual go-between at the Bay.
- For the **Assiniboine** specifically, your spouse near you counts, or any Assiniboine Wife within 300 px.
- With an interpreter the effective fluency is 1.0, which is why "bring someone who has it - your wife, or a woman of the post" appears in the no-words text. See [[Marriage and Country Wives]].

## Tips

> [!tip]
> Plan your start around who you want to trade with. The Cree trade is close to the Bay, so Cree is the high-value language; Nakoda unlocks the plains and pemmican suppliers. A bilingual wife is worth more than any trait.

## See also
[[Dialogue and Conversation]] | [[Trading and Barter]] | [[Cree]] | [[Assiniboine]] | [[Ojibwe]] | [[Metis]] | [[Cree Woman]] | [[Starting Paths]]
