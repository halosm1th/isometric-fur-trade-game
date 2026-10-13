---
title: Ambitions
aliases: [What You Mean to Do, Aims, Goals, Player Ambitions]
tags: [system, ambitions, goals, player]
status: implemented
---

# Ambitions

**Status:** Implemented (design doc P.2). An ambition is "what the player is doing with the year, as against what the year is doing to them." It is an ordinary Tier 1 goal, the same object an ambitious clerk carries, except the player chose it and the journal keeps it in the open. Ambitions pull against orders on purpose: the quota wants the lodge trapped out, the marriage wants the band's country spared.

You declare one on the character sheet ([[Starting Paths]]) or in the journal (*Esc - What you mean to do*). A character who declares nothing is told at year-end "You have declared nothing you mean to do. 1821 comes either way."

## The five on offer

| Ambition | Pledge | How progress is measured (0 to 1) |
|---|---|---|
| **Rise at the post** | "to be master of a post before the Company is done with me" | postmaster = 1.0; clerk = 0.6; otherwise `standing / 60` capped at **0.55** |
| **Marry into a family of the country** | "to marry in the custom of the country and be of two peoples" | married = 1.0; else best courtship: `0.5 x clamp(father's regard / 30) + 0.5 x clamp(gifts / 8 MB)`, capped **0.9** |
| **Grow rich on private trade** | "to come out of this country with something of my own" | `private worth MB / 120`, max 1 |
| **Drive the other company off this river** | "to see the Canadians off this river" | `0.6 x (1 - NWC trade goods / 90) + 0.4 x (Rivalry tension / 100)` |
| **Keep the band fed and whole** | "that the band shall not starve for the Company's price" | `0.4 x clamp(food days / 30) + 0.3 x clamp(band standing with the Company / 100) + 0.3 x (living members / 11)` |

A sixth, **Found a post of your own**, exists in code but is *not offered* and always reports 0 progress: "not yet buildable; the doc's 13.8 work" (**Planned**).

Each ambition keeps a high-water mark (`Best`) so "a setback still reads as progress made". The journal line reads *Title - state. next step*, for example:

- Rise: "standing 14 - a clerk's place comes at 35"; once clerk: "a post wants a master; be the clerk the Factor names".
- Marry: "gifts to her father, and keep coming back".
- Private trade: "*N* MB of your own, counting what you carry and what you've cached".
- Drive off: "their shelves hold *N* pieces; the rivalry stands at *N*".
- Keep the band whole: "the band has *N* days of food, standing *N*".

## What an ambition does to the world

- **The [[The Director|director]]** counts the ambition's keywords as relevant when choosing what a man tells you. Rise: clerk, postmaster, Factor, stands well. Marry: wife, married, daughter, the band. Private trade: furs, trade, goods, price. Drive off: Nor'Wester, the other house, raid, seize. Keep the band: the band, hungry, want, camp. (Found a post: Oxford House, post, the charge of, inland.)
- **Rise at the post** opens a **contest** with another clerk or labourer at your post ([[Rivalry]]): the seat goes to whichever of you the Factor would rather have. "Rise at the post" at clerk rank targets "the charge of a post"; otherwise "the clerk's desk".
- Ambitions appear in the **Focus** panel's *Aim* line, in the journal's "What you mean to do" section, and in every year's account ([[The Year Told]]).
- They pass to your heir as **unfinished business** ([[Heirs and Succession]]).
- NPCs have the same machinery; a man with Ambition above 0.65 shows it in a bark ("I'll not carry wood all my life").

## Tips

> [!tip]
> Combine ambitions with traits: *Silver tongue* plus *Grow rich on private trade* is the classic fur-runner build, but remember private trade is exactly what the Factor docks at the sailing. See [[Trading and Barter]].

> [!warning]
> "Drive the other company off this river" means escalation nobody fully controls. At Rivalry 70+ armed parties go about both posts.

## See also
[[Starting Paths]] | [[Rivalry]] | [[Standing]] | [[Heirs and Succession]] | [[Marriage and Country Wives]] | [[The Year Told]] | [[Journal, Map and UI]]
