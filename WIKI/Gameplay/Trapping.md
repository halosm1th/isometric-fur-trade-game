---
title: Trapping
aliases: [Traplines, Steel Traps, Beaver Trapping, Poaching Traps]
tags: [gameplay, system, trapping, furs, economy]
status: implemented
---

# Trapping

**Status:** Implemented. Code: `Scripts/Ecology/Trapline.cs`, `Scripts/Ecology/EcologyManager.cs` (`SetTraps`, nightly catch), `Scripts/Player/PlayerController.cs` (the `SteelTrap` hotbar use and the lodge branch of `Interact`).

Beaver are the economic engine of the whole trade: one prime beaver pelt is the **Made Beaver**, the Company's unit of account ([[Made Beaver]], [[Fur Prices]]). Beaver are **trapped, not hunted**. You set a line, you come back tomorrow.

![[item-steel_trap.png|64]] ![[tile-beaver_lodge.png|96]] ![[animal-beaver.png|96]]

## Setting a line

1. You start with **3 steel traps** (4 lb each). More come from the stores, the ship (the design notes say 12 a year) or trade.
2. Walk to a **beaver lodge** (a wildlife site you know about: you must have *found or been told* of it; see [[Hunting]] and [[Species Sites]]). Sites do not appear on your map until then.
3. Select the traps on the hotbar and use them (or click). You set up to **6 at once** ("You set 3 traps at Beaver Lodge (3 on your line). Come back tomorrow."). Setting traps trains **Trapping** (+0.02).
4. The prompt over the hotbar reads "Use a steel trap here to set a line (<site>)" while you stand at a lodge with traps.

If the lodge is dead (no breeding capacity left) the game says "This lodge is dead. Nothing will come to a trap here." If you have no traps: "No beaver lodge here. Traps go where the beaver are - along the river."

## The catch

Traps work **overnight**: once a day, from 05:00, every trap at every lodge rolls:

> chance per trap = 0.07 x lodge density x (0.6 + 0.8 x trapper's skill)

where *density* is the beaver count over capacity (clamped 0 - 1.2). At a full lodge with the starting skill of 0.15, each trap has about a 5% chance a night; three traps catch on average about one beaver every seven days. At skill 1.0 a trap is 9.8% a night. Each catch also removes a beaver from the lodge, so **a lodge can be trapped out**: the design wants resource crises to build over a season, not a fortnight (see [[Ecology]]).

The catch sits in the traps until someone checks them.

## Checking and collecting

Stand at the lodge and press `E`: "N beaver in the traps. Pelts and meat in your pack." Each beaver gives **1 pelt** (prime beaver pelt from October to May; thin "summer pelt" worth 0.45 MB from June to September) and **3 fresh meat**. The traps stay set. If the traps are empty it reads "The traps are empty. (3 traps set here)."

Pelts are worth **1 Made Beaver** each when prime; see [[Furs and Pelts]] and [[Trading and Barter]]. Take them to the warehouse counter (`E` inside, "stow goods") or trade them to a band or the rival.

## Poaching

Anyone checking a lodge that has *other parties'* traps may rob them. If you press `E` at a lodge with someone else's catch you may "take N beaver from someone else's traps. If anyone saw, they'll remember." Each owner's line has a 60% x boldness chance per attempt of being robbed. The owner later finds "Someone has been at your traps - N taken", and witnesses remember who did it, feeding [[Grudges and Vendettas]]. Cree hunters and the Factor's men run their own lines in winter ([[Cree Hunter]], [[Daily Orders]]).

## Tips

> [!tip]
> The Factor wants furs. Learning where a lodge is from a hunter is cheaper than finding one yourself: ask about the hunting once your rapport with him is 15 or more.

> [!tip]
> Winter pelts are prime. A June-to-September trapper brings in pelts worth less than half; schedule your lines for the cold months.

> [!warning]
> Do not overtrap one lodge. A trapped-out lodge costs the band its living and sours their regard for the Company ([[Band Ledger]]).

## See also

[[Hunting]] - [[Fishing]] - [[Furs and Pelts]] - [[Made Beaver]] - [[Skills]] - [[Beaver]] - [[Ecology]]
