---
title: Multiplayer
aliases: [Co-op, Hosting, Joining, Dedicated Server]
tags: [game, system, multiplayer, network]
status: implemented
---

# Multiplayer

**Status:** Implemented (first pass; the project's own notes call the numbers and some flows provisional). Code: `Scripts/Net/Net.cs`, `Scripts/UI/MainMenu.cs`.

Several people can share one world. There is **one rule**: the server runs the simulation, and clients are windows onto it. Solo play is simply a host with nobody else connected, running through the same code path (Godot's offline peer carries the calls locally), so there is no separate single-player simulation.

## Ways to play

| Mode | How | Notes |
|---|---|---|
| **Solo** | New game / Continue | An offline host. The pause menu stops time. |
| **Host** | Multiplayer > "Host a new game (port 7777)", or "Begin and host" on the character page, or "Continue the saved game and host" | You play and also run the world. |
| **Join** | Multiplayer > address box (`ip` or `ip:port`) > Join | You first make a character on the Who You Are page, which is sent to the host with your name. |
| **Dedicated server** | Multiplayer > "Run a dedicated server" | No player of its own; "this window stays open as the world". |

- **Port:** `7777` (UDP, ENet). The default is a constant (`DefaultPort`); a different port can be typed as `ip:port` when joining.
- **Capacity:** the server is created with a limit of 8 client connections.
- **Command line** (after `--`): `--solo`, `--host`, `--server` (headless dedicated), `--join=ip[:port]`, `--name=Whoever`, `--menu=settings|controls|multiplayer|newgame|character`.
- Firewall: the project notes that Windows asks once to allow Godot through when you host, and that friends on the internet need UDP 7777 forwarded or a VPN such as Tailscale or Hamachi.

## What the host owns

- Every NPC and animal brain, the ledgers, the ecology, the clock, the weather and the ship.
- The **save file** ([[Save and Load]]): F5 / F9 and the autosave only work for the host. Clients are reloaded and re-seated when the host loads.
- The **clock**: Space stops it, `[` and `]` slow and quicken it (the seconds-per-hour value is doubled or halved, clamped to 0.5-120 s), shown on the calendar HUD. Everyone's clock follows; only the host may touch it.
- The F1 test console.
- Campaign progress ([[Campaigns and Adherence]]) is decided by the host.

## What a client owns

A client owns **its own player's movement**. Everything that touches the world (E, hotbar use, attacks, trade, talk) is sent as a request that the server carries out, with the result sent back as a notice. Needs, inventory, standing and task are mirrored to the owner a few times a second. Late joiners receive the full world state on arrival (agents, doors, felled trees, clock, weather).

## Shared time

Sleeping and waiting are **agreed**: pressing the wait key (Z) or using a bed marks you as turning in, and the clock only jumps when every living player has. The HUD shows `Turning in: 1/3`. Moving cancels yours. Since the host's simulation is everyone's, the **pause menu cannot stop the world** when others are present; it just floats over the game. "Pause when the window loses focus" only applies when playing alone.

When anyone is on the river road (the short route between York Factory and Oxford House) the clock marches at a minimum of 6x for everyone.

## Characters and rejoining

Players are matched **by name**. If you have played in that world before under the same name, you come back as you were and what you choose on the character page is set aside. A new HBC joiner is given the "report to the Factor" task. Death is handled per player: R asks the server to respawn you as an heir ([[Heirs and Succession]]).

## Chat

`Tab` opens a line, `Enter` sends it to everyone, `Esc` cancels. Lines arrive as world notices prefixed with the sender's name.

## Simulation across players

Anyone near *any* player is simulated at full fidelity; the rest of the world is resolved once a day in batches ([[Simulation Tiers]]). The [[The Director|director]] weights across every player.

## See also

[[Save and Load]] - [[Controls]] - [[Settings and Options]] - [[Sleeping and Resting]] - [[Simulation Tiers]]
