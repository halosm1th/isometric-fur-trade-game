---
title: Save and Load
aliases: [Saving, Quicksave, Autosave, F5, F9]
tags: [game, system, save]
status: implemented
---

# Save and Load

**Status:** Implemented. Code: `Scripts/Core/SaveGame.cs`, `Scripts/UI/SettingsRuntime.cs` (autosave), `Scripts/UI/PauseMenu.cs`, `Scripts/UI/MainMenu.cs`.

The game has **one save slot**, the *quicksave*. There is no slot picker. The host owns the save; a client cannot save or load ("Only the host can save" / "Only the host can load").

## How to save and load

| Action | How |
|---|---|
| Save | `F5` in game, or **Save (F5)** in the pause menu (`Esc`; host only), or the Save button in the F1 test console, or the autosave |
| Load | `F9` in game, **Load (F9)** in the F1 console, or **Continue** on the main menu |
| Continue and host | Multiplayer menu > "Continue the saved game and host" |

`F5` and `F9` are reserved keys and cannot be rebound ([[Settings and Options]]). A successful save shows "The world is written down." and reports the file path. If no save exists, loading says "No save to load."

The main menu shows a **Continue** button only when a save exists, with a one-line summary such as `19 Aug 1811, 9h - Thomas Ross`. The note beneath **New game** says the saved game is kept until you save over it.

## Autosave

Settings > Game > **Autosave**: Off, or every 5, 10, 20 or 30 real minutes (default 10). It writes the same quicksave file as `F5`, only while you are alone or hosting, only in a world scene, and not while the pause menu is open. A brief "Autosaved." notice appears.

## Where the file is

`user://saves/quicksave.json` (a pretty-printed JSON file, `"version": 1`). That is inside Godot's standard per-project user-data folder (on Windows normally `%APPDATA%\Godot\app_userdata\IsometricFurTradeGame\saves\`); the settings file `settings.cfg` sits beside the `saves` folder. The save message prints the full path after each save. Campaign scripts you write live in `user://campaigns/` ([[Campaigns and Adherence]]).

## What is saved

The map itself is regenerated from the world layout every time; the save stores only what has **drifted from it**:

- The date, hour and the current sky and its hours remaining.
- Rivalry tension; campaign progress (id, adherence, flags, beats done, letters in transit, the log).
- Your **explored area** (the fog of the [[Journal, Map and UI|map]]).
- Every ledger and household: stores, pelt price, quota, furs taken, debts, standing, known sites and beliefs, raid dates, directives. See [[Fort Ledger]], [[Band Ledger]], [[NWC Ledger]], [[Outpost Ledger]].
- Mail in transit, [[Councils and Accords|council]] state, [[Grudges and Vendettas]], [[Favours]] and losses.
- Every wildlife site (count, breeding, scarcity, position, **traplines**), see [[Ecology]].
- Hearths and their fuel, open doors, felled and burnt cells, picked-up items, canoes, the brigades and the ship (state, position, cargo).
- Every NPC: position, needs, kit, skills, age, fever, personality, appearance, family, rapport, memories, task, and whether they are dead (and where they lay).
- Every player, **matched by name**: needs, inventory, skills, standing, rank, task, known sites, journal, ambitions, house (forebears), languages, start path, kin and year story.

Because players are matched by name, a friend who joins later gets their own character back ([[Multiplayer]]).

## What loading does

`F9` stores the save in memory, announces the reload to all clients, and re-enters the world scene; the population spawner then restores everything over the freshly generated map. Clients drop their world and come back in as if just connected, re-seated after about a second and a half. The campaign is re-read from its script by id.

> [!warning]
> Loading discards everything since the save, including a death. There is one slot, so saving over it is permanent. Quicksaving just before a risky fight is the closest the game has to a safety net.

> [!tip]
> A death does not need a reload: pressing `R` respawns you as an heir ([[Heirs and Succession]]). A save taken before a death keeps your original character.

## Known limits

- A single slot only; no named saves.
- Saves from older builds may lack newer fields (the loader supplies defaults for many, e.g. a missing campaign block starts a fresh one).
- The save does not record which window size or settings you used; those are in `settings.cfg`.

## See also

[[Settings and Options]] - [[Multiplayer]] - [[Campaigns and Adherence]] - [[Controls]] - [[Health, Wounds and Illness]]
