---
title: Settings and Options
aliases: [Options, Settings Menu, Key Rebinding]
tags: [game, system, ui, settings]
status: implemented
---

# Settings and Options

**Status:** Implemented. Code: `Scripts/Core/Settings.cs`, `Scripts/Core/Keybinds.cs`, `Scripts/UI/SettingsPanel.cs`, `Scripts/UI/SettingsRuntime.cs`.

The same Settings screen opens from the main menu and from the in-game pause menu (`Esc`). **Every change takes effect and is saved the moment it is made** to `user://settings.cfg`. "Restore defaults" resets every option and saves; "Restore default keys" resets only the keyboard.

## Display tab

| Option | Values | Default |
|---|---|---|
| Window | Windowed / Full screen (borderless) / Full screen (exclusive) | Windowed |
| Window size (used when windowed) | 1280 x 720, 1600 x 900, 1920 x 1080, 2560 x 1440 | 1280 x 720 |
| Vertical sync | on / off | on |
| Frame rate limit | Unlimited, 30, 60, 90, 120, 144, 240 | Unlimited |

`F11` toggles full screen anywhere (main menu, pause menu, in game). A window size larger than the desktop is shrunk to fit.

## Interface and view tab

| Option | Range | Default |
|---|---|---|
| Interface size | 50% - 150% (applied when you release the slider) | 100% |
| World zoom | 50% - 250% | 100% |
| Smooth camera | on / off | on |
| Speech lingers (how long dialogue bubbles stay) | 50% - 300% in 25% steps | 100% |
| Show frame rate (a small counter at the top centre of the screen) | on / off | off |

The UI is laid out for 1280 x 720 and stretches. On bigger screens it grows by **half** as much as the screen did (1080p becomes 1.25x, 1440p 1.5x, 4K 2x), and the Interface size slider scales that again. The interface is never allowed to grow so large that the window holds less than 960 x 540 of layout. World zoom is applied through the camera and is divided by the interface scale so the two never fight. In game, zoom is also on `Ctrl` + mouse wheel, `Ctrl` + `=` / `-`, and `Ctrl` + `0` to reset.

## Game tab

| Option | Values | Default |
|---|---|---|
| Pause when the window loses focus | on / off. Only when playing alone; a shared world cannot stop for one player. | on |
| Autosave | Off, every 5, 10, 20 or 30 real minutes. Writes the quicksave file (the same one as `F5`) when you are alone or hosting. | every 10 minutes |

See [[Save and Load]].

## Sound tab

Four sliders from 0% to 100% on separate buses (see [[Audio]]):

| Bus | Default | Description from the menu |
|---|---|---|
| Master | 80% | |
| Music | 60% | "Plays now and then, with long quiet between pieces; a fiddle in the men's house of an evening." |
| Ambience | 80% | "Wind, weather, the river, fires, the flies." |
| Effects | 90% | "Footsteps, shots, axes, bells, animals, the interface." |

See [[Music]] and [[Sound Effects]].

## Controls tab (key rebinding)

Every action has **two key slots** (for example `W` and `Up` both walk up). Click a slot, then press the new key; `Esc` cancels; **right-click clears** the slot. A key belongs to one action only: giving it to another action takes it from the first, and the panel tells you which action lost it. Only changed bindings are stored in the `[keys]` section of `settings.cfg`.

**Reserved keys that cannot be rebound:** `Esc` (menus), `F5` / `F9` (save / load), `F11` (full screen). Also fixed: `Ctrl` + wheel (zoom), left click (strike), the mouse wheel (hotbar). `G` ("set out" for a map waypoint) is registered separately and is not in the rebinding list.

The rebindable actions are: walk (four), run, crouch/sneak, interact, strike the nearest hostile, drink, wait, use hotbar slot, hotbar slots 1-9, pack, journal, map, log, chat, help, hold-to-fast-forward, stop/start the clock (host), slower/faster clock (host), begin again after death, and the test console (host). Defaults are listed on [[Controls]].

On-screen hints (the hotbar numbers, "E  Talk to ...", the help panel) update to show whatever you have bound.

## Command-line options

Useful developer arguments (after `--`): `--hour=N`, `--date=YYYY-MM-DD`, `--speed=SECONDS` (seconds per game hour), `--campaign=ID`, `--adherence=LEVEL`, `--nodirector`, `--start=HBC|NWC|Cree|Assiniboine[...]`, `--load`, `--spawn=PLACE`, `--revealmap`. See [[Multiplayer]] for the networking arguments.

## See also

[[Controls]] - [[Save and Load]] - [[Multiplayer]] - [[Journal, Map and UI]]
