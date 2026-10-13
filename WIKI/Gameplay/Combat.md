---
title: Combat
aliases: [Fighting, Weapons and Combat, Strike, Shooting]
tags: [gameplay, system, combat]
status: implemented
---

# Combat

**Status:** Implemented in a first pass: real-time strikes and shots with cooldowns, miss chances, sneak attacks, noise and an NPC nerve system. The player's weapons have no swing animation (a shot is a line and a flash). Code: `Scripts/Agents/Agent.cs` (`Attack`, `TakeDamage`), `Scripts/Agents/Nerve.cs`, `Scripts/Player/PlayerController.cs`.

![[char-player.png|160]]

Combat is real-time and lethal. Hits cost Health with no armour model; the only protection is not being hit. The design document's rule applies: *fights are won before the first shot.*

## How to attack

- **Left click** strikes whoever is under the cursor, if you have a weapon selected on the hotbar (or nothing). With an *item* selected, the click uses the item instead.
- **`F`** strikes the nearest hostile within 400 px.
- Select a weapon by pressing its hotbar number: you "ready the musket (damage 55, reach 340)". Otherwise your best weapon by damage is used; with no weapon you use fists.
- A **reload bar** shows over your head while the weapon cools down. Anyone hit shows a health bar for a few seconds.
- Reach is the weapon's range plus 20 px. Too far gives "Too far for the knife."

## Weapons

| Weapon | Damage | Range | Cooldown | Weight | Worth (MB) | Notes |
|---|---|---|---|---|---|---|
| Fists | 8 | 60 | 1.2 s | - | - | When unarmed |
| Knife | 18 | 60 | 0.7 s | 0.5 lb | 0.5 | You start with one |
| Tomahawk | 28 | 70 | 1.0 s | 2 lb | 1.5 | |
| Bow | 30 | 260 | 1.6 s | 2 lb | 2 | Ranged; carried by band characters |
| Trade musket | **55** | **340** | **4.0 s** | 9 lb | 12 | Ranged; the Factor lends one to men he trusts (standing 15) |

Range above 100 counts as **ranged**: it can miss, and it trains Hunting. See [[Weapons]] for each item.

## Damage, misses and modifiers

- Damage dealt = weapon damage x your **capability factor** (0.5 - 1.0, falling when Food, Water or Warmth is below 25; see [[Needs and Survival]]), x0.6 if you have fever.
- **Sneak attack.** Crouched and striking something *unaware* (a grazing animal that has not noticed you) multiplies damage by **2.5**. Sneaking up on a grazing caribou and using the knife is the cheap way to a caribou.
- **Ranged miss chance** = intoxication/250 + (0.08 + 0.62 x (distance/range)^2) x (1 - 0.55 x Hunting), plus 0.1 if the target is sprinting. A sneaking shooter firing on an unaware target multiplies the miss chance by 0.4. At ten paces a musket is a "butcher's tool"; at the edge of its reach it mostly throws the ball away. Drink spoils the aim.
- A **musket pelt is damaged**: killing an animal with a musket yields a shot-through pelt or a holed hide worth far less ([[Furs and Pelts]]); traps, bow, knife and tomahawk leave the skin whole.

## Noise

Sound carries and is drawn as a ring on screen. A musket shot carries **1500 px**: prey bolts, predators think twice (a 60% chance a predator flees), and every NPC inside it is put **on his guard** for 25 seconds (cannot be taken unawares). A bow shot carries 200, a blow 160, running 420, walking 190, crouching 45. Animals hear footsteps at 60% of those figures.

## Animals fight back

| Animal | Health | Bite | Speed |
|---|---|---|---|
| Wolf | 50 | 16 | 230 |
| Caribou | 70 | 12 | 170 (dangerous only when starving) |
| Beaver | 20 | 4 | 90 |
| Buffalo | 260 | 25 | 200 |

Predators and starving prey count as dangerous and attack people. See [[Wildlife]] and [[Wolf]].

## Consequences of fighting

- Hitting someone makes their armed friends within earshot come at you and costs standing if they are Company men.
- A rival company's man who would kill you takes you **prisoner** instead ([[Health, Wounds and Illness]]).
- Breaking a parole you gave costs 10 standing and is witnessed.
- Raiding the other company's stores and arson are witnessed and remembered; the story travels ([[Rivalry]], [[Grudges and Vendettas]]).
- A man killed by you may leave a blood debt held by his family ([[Grudges and Vendettas]]).

## NPC nerve

NPCs weigh whether to stand: courage, how many of their own are in sight against how many of yours, whether anyone of rank is present, their wounds, a musket in hand, rum, and whether the house is **shaken** (a quarter hour after being fired on unawares). When a man falls or is walked off a prisoner, those whose nerve fails run together for about a quarter of a minute. Prisoners and sleepers are not counted.

## Tips

> [!tip]
> Hold your fire until the range is right. Use `Ctrl` to creep up on grazing game, take one shot, and expect the herd to bolt from the noise.

> [!warning]
> A musket shot alerts every NPC within 1500 px. Do not fire inside a post unless you mean it.

## See also

[[Hunting]] - [[Skills]] - [[Weapons]] - [[Health, Wounds and Illness]] - [[Rivalry]] - [[Wildlife]]
