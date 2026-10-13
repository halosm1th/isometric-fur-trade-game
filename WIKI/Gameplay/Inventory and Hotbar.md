---
title: Inventory and Hotbar
aliases: [Pack, Inventory, Hotbar, Carry Weight, Load, Tumpline]
tags: [gameplay, system, inventory, ui]
status: implemented
---

# Inventory and Hotbar

**Status:** Implemented. Code: `Scripts/Items/Items.cs` (`Inventory`, item definitions), `Scripts/UI/Hotbar.cs`, `Scripts/UI/JournalPanel.cs` (Pack tab), `Scripts/Agents/Agent.cs` (carry weight).

![[item-tumpline.png|56]] ![[item-pack_basket.png|56]] ![[item-capote.png|56]] ![[sheet-items-all.png|420]]

Your pack is a **list of stacks with no slot limit**: only **weight** limits you. Stacks keep the order they were picked up in, and the **first nine** are the hotbar.

## The hotbar

Nine slots along the bottom of the screen, each with an icon, a count and the key bound to it (default `1` - `9`).

| Input | Result |
|---|---|
| `1` - `9` | Select and **use** that slot |
| Mouse wheel | Move the selection |
| `Enter` (or keypad `Enter`) | Use the selected slot |
| Left click | Use the selected item on the spot where you click, or strike with a selected weapon |

Using an item does what its kind says:

| Kind | Use |
|---|---|
| **Food / drink** | Eat or drink it (water thrown on nearby flames first, if any); plays the eat/drink animation |
| **Fuel** (firewood) | Put it on the nearest fire. Click a fire ring or open ground to lay a campfire. |
| **Clothing** | Toggle worn / stowed: "You put the capote on. (+4.5 warmth)" |
| **Weapon** | Ready it: "You ready the knife (damage 18, reach 60)". It becomes your preferred weapon. |
| **Tools** | Axe fells a tree; snowshoes toggle worn; kettle drinks / fills; fishing line casts; steel trap sets at a lodge; flint lights a fire |
| **Furs / goods** | Shows its worth at the counter |

The line above the hotbar always says what `E` would do. The selected item's name shows below.

## Weight and capacity

Everything has a real weight in pounds. Your **carry capacity** is **50 lb** plus any packs: a **tumpline +40 lb** (you start with one), a **pack basket +30 lb**. Each pack type counts once.

- Over capacity, your speed factor falls by 1/160 for every pound over, down to **x0.35** (at 160 lb over). Walking over capacity uses food 2.0x instead of 1.5x.
- The HUD line reads `Load 75/90 lb` and adds "(overloaded)".
- Other creatures differ: the design says labourers and hunters carry about 65, a voyageur 110 and women 45 (NPC figures; see [[Roles]]).

**Your starting kit** (HBC hand) weighs about 75 lb:

| Item | Count | Weight each |
|---|---|---|
| Pemmican | 4 | 1.5 lb |
| Dried goose | 2 | 1.0 lb |
| Firewood | 4 | 5.0 lb |
| Capote | 1 | 4.0 lb |
| Knife | 1 | 0.5 lb |
| Trade goods | 6 | 3.0 lb |
| Steel trap | 3 | 4.0 lb |
| Copper kettle | 1 | 3.0 lb |
| Water | 2 | 2.0 lb |
| Flint and steel | 1 | 0.3 lb |
| Tumpline | 1 | 1.0 lb |
| Axe | 1 | 4.0 lb |
| Fishing line | 1 | 0.5 lb |

Band characters begin instead with a hide coat, snowshoes, a knife, 4 dried meat, flint, a fishing line, 2 firewood and 2 traps (plus background extras). See [[Starting Paths]].

> [!tip]
> Firewood is the heaviest thing you carry (5 lb a piece). Four pieces plus a kettle, water and traps already uses most of a 90 lb capacity; a stack of furs (1.5 lb each) is light by comparison.

## Wearing things

Only **worn** clothing and snowshoes count. In the Pack tab each clothing item and snowshoes has a **Wear / Take off** button; using them on the hotbar toggles too. Warmth adds up across layers (capote 4.5 + point blanket 5 + hide coat 7 + robe 8). See [[Clothing]].

## Storing and withdrawing

Inside your own warehouse (HBC) or the Nor'Westers' house, `E` at the **storeroom chest** lets you **stow goods and furs** (and provisions, keeping two meals) and **draw kit**: a ration, traps, an axe, snowshoes, a kettle, a tumpline, a blanket (standing 10), a musket (standing 15, one per man), or four trade goods (clerk). See [[Fort Ledger]].

Other containers: **canoes** (load/unload from the bank menu; capacity in pounds), and **bodies and carcasses** (`E` to search or butcher). Items on the ground are picked up with `E`.

## Tips

> [!tip]
> Use the **◄ ►** buttons in the Pack tab to put the items you press most (food, firewood, flint, weapon) in slots 1 - 9.

## See also

[[Journal, Map and UI]] - [[Needs and Survival]] - [[Items]] - [[Clothing]] - [[Weapons]] - [[Tools]] - [[Controls]]
