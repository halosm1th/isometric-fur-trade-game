---
title: Controls
aliases: [Keybindings, Keys, Key Bindings, Input]
tags: [gameplay, controls, ui]
status: implemented
---

# Controls

**Status:** Implemented. Defaults come from the `[input]` map in `project.godot`; every action has two slots and can be rebound (see [[Settings and Options]]). Press **H** in game for a help panel that always shows your current bindings.

## Movement

| Action | Default keys | Notes |
|---|---|---|
| Walk | `W` `A` `S` `D` or the arrow keys | Four directions on the isometric grid. Base speed 140. |
| Run (sprint) | `Shift` | 1.7x speed but about 2.4x hunger drain; noisy (footsteps carry 420 px). |
| Crouch / sneak | `Ctrl` | 0.45x speed; footsteps carry only 45 px; animals judge you as if you were much farther away; a first stroke on something unaware does 2.5x damage. Also the "I mean it" key for deliberate arson (see [[Fire and Warmth]]). |
| Interact | `E` | Context action, see below. |
| Drink | `Q` | From water, or melt snow at a fire in winter. |
| Wait / sleep | `Z` | Wait an hour; at night sleep until 07:00. See [[Sleeping and Resting]]. |
| Hold to fast-forward | `T` | Clock x40 while held. |
| Set out for a map waypoint | `G` | See [[Travel and Wayfinding]]. Registered at runtime; not in the rebinding list. |

## Actions and combat

| Action | Default | Notes |
|---|---|---|
| Strike | **Left click** | With a weapon selected (or nothing), strikes whoever is under the cursor. With an *item* selected it uses that item on the spot. |
| Strike nearest hostile | `F` | Hits the nearest hostile within 400 px. |
| Use hotbar slot | `1`-`9` | Selects and uses the slot. |
| Use selected slot | `Enter` / keypad `Enter` | After choosing with the mouse wheel. |
| Select slot | Mouse wheel | Cycles the nine slots. |
| Begin again after death | `R` | Respawn as your heir ([[Heirs and Succession]]). |

What `E` does, in order of priority: take the river road at a road head; talk to the nearest person within 110 px; butcher a carcass or search a body; pick up an item; strip a berry bush in fruit; check your traps at a beaver lodge; use a canoe; use a bed; plunder the other company's stores if you are inside them; take from someone else's woodpile; open the storeroom chest, draw a ration, shoulder cargo from the ship; open or close a door; look at a fire. The prompt above the hotbar always tells you what `E` will do.

## Menus and panels

| Action | Default |
|---|---|
| Pack (inventory) | `I` |
| Journal | `J` |
| Log | `L` |
| Map | `M` |
| Chat | `Tab` (`Enter` sends, `Esc` cancels) |
| Help panel | `H` |
| Pause menu | `Esc` (alone, time stops) |
| Test console (host) | `F1` |
| Full screen | `F11` |
| Quick save / load (host) | `F5` / `F9` |
| Zoom | `Ctrl` + wheel, `Ctrl` + `=` / `-`, `Ctrl` + `0` resets |

`Esc` closes the topmost panel first (help, journal, map, dialogue), and only then opens the pause menu. See [[Journal, Map and UI]] and [[Inventory and Hotbar]].

## Clock (host only)

| Action | Default |
|---|---|
| Stop / start the clock | `Space` |
| Slower clock | `[` (doubles seconds per game hour) |
| Faster clock | `]` (halves it) |

The same three are buttons under the date in the calendar HUD - `<<`, `||` (`>` while stopped) and `>>` - with the pace beside them: `x1` at the usual 30 seconds per game hour, `x2` twice as fast, `x0.5` half, `Paused` when stopped, and the fast-forward pace while `T` is held. Limited to 0.5 - 120 seconds per game hour (x60 to x0.25). Only the host's buttons work; a joined player's are greyed out. See [[Calendar and Seasons]].

## Reserved keys (cannot be rebound)

`Esc`, `F5`, `F9`, `F11`, `Ctrl` + wheel, left click, and the mouse wheel for the hotbar.

## Map panel

Open with `M`. **Drag** to pan; **wheel** to zoom about the cursor (0.6x - 8x); **left click** (without dragging) sets a waypoint; **right click** clears it; `G` sets out for it.

## Conversation

Dialogue is menu driven; click an option or use the keyboard focus. Leaving a conversation closes it. See [[Dialogue and Conversation]].

## See also

[[Settings and Options]] - [[Beginner's Guide]] - [[Inventory and Hotbar]] - [[Journal, Map and UI]]
