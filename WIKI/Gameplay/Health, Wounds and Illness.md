---
title: Health, Wounds and Illness
aliases: [Health, Wounds, Fever, Scurvy, Death, Drunkenness, Captivity]
tags: [gameplay, system, health, survival]
status: implemented
---

# Health, Wounds and Illness

**Status:** Implemented (first-pass numbers). Code: `Scripts/Agents/Agent.cs`, `Scripts/Agents/Needs.cs`, `Scripts/Agents/Heirs.cs`, `Scripts/UI/Conversation.cs`.

![[ui-need-health.png|48]] ![[ui-need-wounded.png|48]] ![[ui-need-fever.png|48]] ![[ui-need-scurvy.png|48]] ![[ui-need-drunk.png|48]]

Health is the red bar (0-100). Everything that can hurt you ends up in it. For the other three bars see [[Needs and Survival]].

## Ways to lose Health

| Source | Amount |
|---|---|
| Weapon blows | Weapon damage x your capability factor (0.5-1), x0.6 if you have fever. See [[Combat]]. |
| Fire | 9 points a second while standing in a burning cell. See [[Fire and Warmth]]. |
| Freezing (Warmth 0) | 3 an hour |
| Dying of thirst (Water 0) | 5 an hour |
| Starving (Food 0) | 3 an hour |
| Fever | about 8 a day (0.35 an hour) plus extra hunger |
| Scurvy | about 3 a day (0.12 an hour) |
| Animal bites | Wolf 16, caribou 12, beaver 4; see [[Wildlife]] |

## Healing

- **Natural.** 1 Health an hour when you are fed, watered and warm (Food above 55, Water above 45, Warmth above 50) and not taking damage; half that while moving fast. Nothing while starving or freezing.
- **Dressing a wound.** Talk to a woman at the post (the dialogue shows "Ask her to see to your wounds" while Health is under 70). She washes and binds it for **+25 Health**, **once a day**, using one water (yours or the household barrel). It also earns her a little rapport. If there is no clean water she says so and refuses. See [[Dialogue and Conversation]].
- **Rest and food.** Sleeping in a bed passes hours quickly while the natural healing runs ([[Sleeping and Resting]]).

The "what matters now" panel names the problem: below 35 Health it reads "you are badly hurt - eat, keep warm, and find a woman of the post to dress it."

## Fever

Fever (the code's "the fever") is an infection that spreads between people who stand together, more so indoors. When the ship docks there is a one-in-three chance a sailor has it.

- Lasts **6 to 11 days**.
- Drains about 8 Health a day and extra hunger.
- You move at **x0.7** and strike at **x0.6**.
- Survivors become **immune** for the year ("is over the fever").
- The HUD shows `FEVER`; talk to someone feverish and you are told to keep your distance.

## Scurvy

Everyone counts the days since fresh food. After **42 days** without any of venison/fresh meat, goose, fish, hare, ptarmigan, **berries** or **spruce beer**, you get scurvy: about 3 Health a day, speed x0.8, "gums black" in conversation, and the HUD warns `SCURVY (eat fresh meat)`. Eating a fresh item cures it ("fresh meat at last"). Dried meat, dried goose and pemmican do **not** reset the count. Posts brew spruce beer against it when men go a fortnight without fresh food, and gather berries July-September. See [[Gathering and Gardens]] and [[Food and Drink]].

## Drink

Rum (35) and ale (12) add **intoxication**, which wears off at 8 an hour. Above 40 you are *drunk*: speed falls toward x0.6 at 100 and the chance to miss a shot rises by intoxication/250. Drunk NPCs may brawl, and a man still drunk at first light does no work. Rum also gives 8 Warmth, ale 4. See [[Food and Drink]].

## Captivity (alternative to death)

If a rival company's man lands a blow that would kill you, you are **taken prisoner** instead: your Health is set to 8, they keep all your goods except clothing, your knife and a couple of meals, and you are held **three days** in their post. Options: wait and be turned loose with two days' pemmican, **pay a ransom** of 12 trade goods, **give your parole** (no arms against them for 120 days; breaking it costs 10 standing and is witnessed), or walk out of their yard and become an escaped man (+2 standing, +4 tension, they will remember). See [[Rivalry]].

## Death and the heir

At Health 0 you die. The death screen reads "You died" and names your **heir**: a son if you have one grown, a wife's brother if you married into the country, otherwise a kinsman off the ship. The screen lists what he inherits (grudges, debts, ties, unfinished ambitions). Press **R** to take up his place:

- You return at the gate with a fresh starting kit and your heir's name, age and standing; rank resets to labourer.
- Grudges against your name follow the family at about 60% of the debt; everyone's regard for you is pulled toward neutral (multiplied by 0.4 and clamped to -40...40).
- Your private worth is cut to a quarter; your ambitions are replaced by the unfinished ones.
- The forebear is added to "Your house" in the journal.

See [[Heirs and Succession]], [[Grudges and Vendettas]], and [[Ambitions]].

> [!tip]
> The family is the save file. Marrying and raising children ([[Marriage and Country Wives]]) is the best insurance against a bad death.

## See also

[[Needs and Survival]] - [[Combat]] - [[Fire and Warmth]] - [[Skills]] - [[Heirs and Succession]]
