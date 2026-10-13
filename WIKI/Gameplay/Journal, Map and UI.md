---
title: Journal, Map and UI
aliases: [HUD, User Interface, Journal, Log, Help Panel, Pause Menu, Interface]
tags: [gameplay, ui, hud, system]
status: implemented
---

# Journal, Map and UI

**Status:** Implemented. Code: `Scripts/UI/` (`JournalPanel`, `MapPanel`, `NeedsHud`, `CalendarHud`, `Hotbar`, `HelpPanel`, `PauseMenu`, `NoticeLog`, `ChatBox`, `DialogueMenu`, `DebugMenu`) and `Scripts/Core/Focus.cs`.

The interface is designed to be read at a glance and to explain *why* things happen (see [[Design Principles]]). The UI is laid out for 1280 x 720 and stretches; see [[Settings and Options]] to scale it.

## The screen

| Element | Where | What it shows |
|---|---|---|
| **Calendar HUD** | top | Date, time, season chapter and the sky. A line appears for 8 seconds on annual events ("The ship is in from England"). See [[Calendar and Seasons]]. |
| **Needs HUD** | bottom-left | Four bars (Health, Warmth, Food, Water); beneath them: "Indoors", `Load N/M lb`, the weapon in hand (or "bare hands"), flags such as `drunk`, `FEVER`, `SCURVY (eat fresh meat)`, `PRISONER of ...`; then your countdown ("N days to the ship - packs 30 of 60") and, for Company men, `Standing with the Company: 12 (labourer)`; and the current task. See [[Needs and Survival]]. |
| **What matters now** | top-right | Three lines: **Now** (the most pressing thing), **Aim** (the next step on your ambition), **Heard** (the freshest thing that touches you). |
| **Hotbar** | bottom-centre | Nine slots, the selected item's name, and a prompt line for what `E` will do. See [[Inventory and Hotbar]]. |
| **Reload bar / health bars** | over heads | A cooldown bar over your head while a weapon recharges; a health bar over anyone you hit. |
| **Speech bubbles** | over heads | What people say (barks) and your lines; duration set in Settings. |
| **Headlines** | upper centre | Big events (a death in the family, a broken accord) take the screen for about six seconds. Everything else goes quietly into the log. |
| **Noise rings** | in the world | A ring shows where a shot, a step or a blow was heard. |

The old scrolling top-left log no longer flashes: nothing appears on screen except headlines and your hotbar notices (2.5 s). The full history lives in the Log tab.

### What matters now

The panel (rebuilt twice a second on the host) picks **Now** from: captivity; Health under 35 ("you are badly hurt"); Warmth under 25 ("you are freezing"); Food under 25 ("you are starving"); Water under 25 ("you are parched"); a blood-debt holder within 900 px; a friend's warning in the last two days; an open sale of a dead man's effects; your assigned task; or a post food shortage ("the post has about N days of food in the stores"). Otherwise: "nothing presses. A good day to hunt, trade, or sit at the fire." **Aim** shows your first ambition ([[Ambitions]]) or invites you to choose one. **Heard** picks, from your last twelve journal entries, the one the [[The Director|director]] rates most relevant to you.

## The book (`J` / `L` / `I`)

One panel with three tabs; pressing the same key closes it, and `Esc` closes it too.

### Journal (`J`)
Newest first. At the top:

- **Your house**: forebears who went before you (name, rank, years, how they died, what they did).
- **The cellar**: casks of salt geese and weeks of food in hand, plus the days left of the goose flight ([[Hunting]]).
- **The seat you want**: the rival race for the next place going ([[Rivalry]]).
- **What you mean to do**: your ambitions and progress.

Below are dated entries of **only what you saw with your own eyes** or were told. When a consequence reaches you, the entry names the chain "as far as you can know it". When the year closes it appends **THE YEAR, AS IT WILL BE TOLD**, a short account (see [[The Year Told]]).

### Log (`L`)
Every notice the world announced, with a timestamp (day, month, time), newest first, up to 400 lines. Also on the host's F1 console.

### Pack (`I`)
Load against capacity (with "overloaded - you are slow and hungry"), what is in hand, the warmth of what you wear, the worth of everything in Made Beaver, your **Cree** fluency ("signs", "a few words", "getting by", "fluent"), your **skills**, **blood debts** held against you, and **papers** (letters) in your pack, with the text of those you have read. Then one row per item with its slot number, weight, worth, effect, **Wear / Take off** for clothing and snowshoes, and **◄ ►** to reorder (slots 1-9 are the hotbar). See [[Inventory and Hotbar]].

## The map (`M`)

"The country as you know it." A parchment map of the entire country at 1/32 scale, fogged except where you have been or been told. **Drag** pans, **wheel** zooms (0.6x - 8x) about the cursor; **left click** sets a waypoint, **right click** clears it, `G` sets out. Legend: you (dot), posts (HBC red / NWC blue squares), camps (triangles), wildlife sites (circles), waypoint (gold diamond), your task (blue diamond). Brigade roads show as dashed red lines once seen, portages as crosses, landmarks by name when zoomed in. See [[Travel and Wayfinding]], [[World Map]] and [[Landmarks]].

![[map-world-overview.png|520]]

## Help (`H`)

A panel titled "York Factory, 1811 - how to live here" with the *current* key bindings and a five-line primer: you are the Company's new hand; keep warm, fed and watered; do the Factor's morning task; furs are money (trap beaver, sell at the counter, trade with the band, or take the Nor'Westers' better price across the river); flint and wood make a fire in the bush; talk to people because they remember what you do.

## Pause menu (`Esc`)

Resume, **What you mean to do...** (declare up to two ambitions: "Two is as many as a man can honestly carry"), **Save (F5)** (host only), Settings, Main menu, Exit. Alone, the clock stops (Godot's time scale is set to 0). With others it cannot, and says "You are hosting: the world goes on while this is up." See [[Settings and Options]] and [[Multiplayer]].

## Other panels

- **Conversation** menus appear when you press `E` beside someone ([[Dialogue and Conversation]]).
- **Chat** (`Tab`) ([[Multiplayer]]).
- **Test console** (`F1`, host only): tabs for calendar speed and skips, player needs and god mode, London and the post, the rivalry, save/load, the Cree band, wildlife, ship and people. A development aid.
- The **death screen** and heir hand-over ([[Heirs and Succession]]).

## See also

[[Controls]] - [[Needs and Survival]] - [[Inventory and Hotbar]] - [[Travel and Wayfinding]] - [[Daily Orders]] - [[Settings and Options]]
