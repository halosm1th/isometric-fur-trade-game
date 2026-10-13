---
title: Beginner's Guide
aliases: [Getting Started, First Hour, How to Play, Tutorial, Walkthrough]
tags: [gameplay, guide, tutorial]
status: implemented
---

# Beginner's Guide

**Status:** Implemented. This walkthrough follows the real prologues in `Scripts/Core/Starts.cs` and the systems documented across this wiki. There is no tutorial level; the game teaches through its prologue, the hotbar prompt, the `H` help panel and the "what matters now" panel.

![[ui-menu-background.jpg|520]]

## 1. Before you begin

1. On the main menu choose **New game**. Leave the campaign on **The Pemmican War, 1811-1821** and adherence on **Steady** for your first run ([[Campaigns and Adherence]]).
2. On *Who you are*, pick your **road in** ([[Starting Paths]]):
   - **The Hudson's Bay Company** ([[HBC Path]]): the most guided and the most forgiving; you have a Factor, orders and a ship to count down to.
   - **The North West Company** ([[NWC Path]]): the rival house across the river; more mobile, fewer supplies.
   - **Cree** or **Assiniboine** ([[Cree Path]], [[Assiniboine Path]]): born to a band; no Company orders, your own kin and camp.
3. Choose a **background**, up to **two traits** and an optional **ambition** (see [[Skills]]). For a first game, *Orkney labourer* with *Bushwise* and *Keen eye* is a solid, simple pick for the Company road.
4. Set **autosave** in Settings if you like (default every 10 minutes) and learn `F5` / `F9` ([[Save and Load]]).

> [!tip]
> Press **`H`** in game for a help panel that always lists your current key bindings. Open the journal with `J`, the pack with `I`, the map with `M`. Everything is in [[Controls]].

## 2. The prologue

A short illustrated scene plays before you are set down. Your choices matter: the people you meet are real and stay real.

### HBC: the last day of the crossing (20 August 1811)

You are aboard the Company's ship (or the sloop from Churchill) with two lads who signed on with you at Stromness, **Hugh Rendall** and **Andrew Flett**. You can:

- **Talk with your mates** and swear to stand by each other (+15 rapport with both, a shared memory that makes them friends from day one).
- **See what Davy Gunn wants**: the sailor offers two kegs of brandy for 4 pieces of goods (you receive 6 drams of rum to trade on your own account). It is profit with a risk; the crew know and it may be reported. Or **tell the mate** for +3 standing and the crew's ill will.
- **Ask the mate** about the Factory: the Factor is "an old Bay man, hard but fair"; watch the senior clerk who wants the next place going; keep off the Canadians across the river.
- **Turn in**, and in the morning **step ashore** at the wharf. Your first order: *report to the Factor*.

### NWC: above the last carrying place (12 July 1811)

You camp with the brigade from Fort William a day's paddle from the partner's house. Sit with **Michel Allary**, play cards with **Jean Paul** (3 goods a hand; your chance of winning is 45% plus a fifth of your Trade skill), and ask the guide: the partners want the Cree band's trade turned from the Bay men.

### Band: morning in the camp (10 June 1811)

You arrive in your people's lodges by the Bay men's house. Sit with your elder: he gives you **four beaver pelts** to take down to the post for powder and a kettle (and a favour is on the books). Go with your cousin to the snares and promise to speak for him to a girl's father. See what the trader's man with a keg wants: drink with him or send him away (the elder notices).

## 3. The first hour (Company road)

Once ashore, the control prompts appear: *"E talks, 1-9 uses, left click strikes, Tab chats, F1 opens the console."* Then:

1. **Find the Factor** (Mr. Auld) and press `E` next to him. Reporting completes your first task and gives you a morning order each day after ([[Daily Orders]]).
2. **Glance at your bars** (bottom-left, see [[Needs and Survival]]). At the post the mess call, the water barrel and the hearth keep them up for you, so you will not starve in the first days.
3. **Know your kit.** Open the pack (`I`): 4 pemmican, 2 dried goose, 4 firewood, a capote (worn), a knife, 6 trade goods, 3 steel traps, a kettle, 2 water, flint and steel, a tumpline, an axe, a fishing line. About 75 of 90 lb ([[Inventory and Hotbar]]).
4. **Follow the blue marker** to your task's target; **gold** marks your waypoint.
5. **Press `E` on things.** The prompt over the hotbar tells you what `E` will do: talk, pick up, butcher, use a bed, open a door.
6. **Save (`F5`)** once you have reported.

## 4. The first month

- **The ship is in.** On 20 August you will probably be set to carrying cargo from the wharf to the warehouse. She sails 15 September; the post's year is measured on the packs shipped, and the HUD counts down "N days to the ship". See [[Calendar and Seasons]] and [[The Reckoning of 1821]].
- **The goose flight** (20 August - 15 October) is the year's second climax: bring geese to the stores ([[Hunting]]).
- **Berries** ripen in July - September: pick them for scurvy insurance ([[Gathering and Gardens]]).
- **Set a trapline** at a beaver lodge as soon as you have found one (ask a hunter about the hunting once he likes you) and check it every day ([[Trapping]]). From October pelts are prime.
- **Talk to everyone.** People know things, remember what you do, and tell others. Ask about rumours; give small gifts; keep promises ([[Dialogue and Conversation]], [[Memory and Knowledge]]).

## 5. Surviving the first winter

Winter is the real teacher. From November the bay freezes and nights run to 6 warmth points an hour of cold.

![[tile-hearth_left.png|80]] ![[item-capote.png|48]] ![[item-snowshoes.png|48]]

- Wear your **capote** and add a **point blanket** or hide coat when you can (stack: 4.5 + 5 + 7). See [[Clothing]].
- Never leave the post without **firewood and flint**. In the open, `click` firewood onto the ground with flint in your pack to make a campfire. ([[Fire and Warmth]])
- Carry water (the kettle at the shore gives 3) or melt snow at a fire with `Q`.
- A **blizzard** (10% of winter weather rolls) doubles the cold; stay indoors and stoke the fire. Travel stops in blizzards ([[Weather]]).
- Put on **snowshoes** (snow slows you to 70% without them).
- Eat something **fresh** (fish, hare, goose, meat, berries) at least every six weeks or scurvy sets in ([[Health, Wounds and Illness]]).
- Sleep at the post: `E` at a bed or `Z` at night skips to 07:00 ([[Sleeping and Resting]]).
- For long trips use the **map** (`M`), set a waypoint, then `G` ([[Travel and Wayfinding]]).

## 6. Common mistakes

> [!warning]
> - **Sprinting everywhere**: it costs 2.4x hunger and makes noise that carries 420 px.
> - **Firing a musket near people or a herd**: it is heard 1500 px away; everyone is alerted and the herd bolts.
> - **Taking from someone else's woodpile or traps**: anyone who sees it remembers, and it feeds grudges ([[Grudges and Vendettas]]).
> - **Setting fire to the wrong building**: arson is witnessed and answered with arms.
> - **Ignoring the Factor's orders**: -2 standing a day; standing opens muskets, blankets and a posting to Oxford House.
> - **Leaving your clothes in your pack**: only worn clothing counts.
> - **Overtrapping one lodge** until the beaver are gone ([[Ecology]]).

## 7. What to do next

Declare an **ambition** (`Esc` > *What you mean to do...*): rise at the post, found a post, marry into a family, grow rich on private trade, drive the other company off the river, or keep the band fed ([[Ambitions]]). Marry into the country ([[Marriage and Country Wives]]), learn Cree ([[Languages]]), and try the other roads ([[Cree Path]], [[NWC Path]]). When you die, an heir takes your place ([[Heirs and Succession]]). The long arc ends at the union of the companies in March 1821 ([[Setting and Timeline]]).

## See also

[[Game Overview]] - [[Controls]] - [[Needs and Survival]] - [[Daily Orders]] - [[Starting Paths]] - [[Journal, Map and UI]] - [[Multiplayer]]
