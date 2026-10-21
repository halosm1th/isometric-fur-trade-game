---
title: "Beats and the lifecycle"
aliases: [Beats and the Lifecycle]
tags: [scripting, campaign, tool]
status: reference
---

<!-- Copied from docs/wiki/Beats-and-the-Lifecycle.md by tools/docs/sync_scripting_wiki.py: edit that, not this. -->

# Beats and the lifecycle

A **beat** is something that may happen to the world. This page is what the game does with one, day by day, and what the nine hooks of a beat are for. (The *syntax* of a beat is in the [[Language-Guide|Language guide]]; the names you can use inside the hooks are in the [[Library-Reference|Library reference]].)

## The nine hooks

```
        sleeping ──AwakenWhen──▶ awake ──────────────────────────────────────────▶ happens
                    │ OnAwaken     │  ResolveWhen ──▶ OnResolve ──▶ over (by the world)     OnActivate
                    │              │  lead time before the date: OnApproach (pressure)
                    │              │  due, and ActivateWhen holds ───────────────────────────▶
          CancelWhen holds on any day ──▶ OnCancel ──▶ over (cancelled)
```

| Hook | Runs | Use it to |
|---|---|---|
| `AwakenWhen() => condition;` | checked every day | keep a beat out of the way until the world is ready for it |
| `OnAwaken() { ... }` | once, the first day the beat is awake | announce that something is stirring |
| `OnApproach() { ... }` | once, when the lead time before the date begins | make the world ready: raise tension, move stock, send a rumour |
| `ActivateWhen() => condition;` | checked every day once the beat is due | hold a due beat back until one more thing is true |
| `OnActivate() { ... }` | when the beat happens | the beat's consequences |
| `ResolveWhen() => condition;` | checked every day | notice that the world got there by itself |
| `OnResolve() { ... }` | when it does | acknowledge it |
| `CancelWhen() => condition;` | checked every day, first | say that the beat can no longer happen |
| `OnCancel() { ... }` | when it does | say what happened instead |

Every hook is optional, and a beat with none is perfectly legal (it exists to be waited for).

## One day, in order

Each day (on the server, once the calendar turns) the game does this, and nothing else:

1. **Timers.** Effects waiting on an `after(...)` whose day has come run first.
2. **Every beat, in the order they are written.** Beats that are done, cancelled, or resting between repeats are skipped. For each of the others:
   1. **Cancelled?** If `CancelWhen` holds, `OnCancel` runs and the beat is over for good. This is asked even of a beat that is still asleep, whatever the adherence: a cancelled beat cannot be forced (only `trigger` can still make it happen).
   2. **Awake?** Everything in its `[Requires]` must be done (or raised as flags), *and* `AwakenWhen` must hold. If not, nothing more is asked of it today. A beat that was awake and stops being so simply pauses: it does not run `OnAwaken` again, and a dateless one keeps counting its `delay` from the day it first woke.
   3. **First day awake?** `OnAwaken` runs, once (once per round, for a repeating beat).
   4. **Resolved by the world?** If `ResolveWhen` holds, `OnResolve` runs and the beat is over without `OnActivate` ever running. This is asked every awake day, **not only once it is due**: guard it with a date if it should not be possible early.
   5. **Too early?** The beat is **due** on its `date` (a dateless beat: the day it first woke, plus `delay`). Until `lead` days before that (30 by default), nothing more today.
   6. **Pressure.** The first day within the lead, `OnApproach` runs, once per round, at **Steady** adherence and above.
   7. **Not yet due?** Then that is all for today.
   8. **Happen.** What follows depends on the beat's *kind* (next section). If it happens, its flag is raised, its count goes up, and the log notes it.
3. **Triggered beats.** Beats that a `trigger(...)` asked for today happen now, at the end of the day: their `OnActivate` runs whatever their date, requires, `AwakenWhen` or `ActivateWhen`, and even if they were cancelled. A beat already done is skipped. Triggers can chain (a triggered beat that triggers another), up to twenty rounds in a day.

Because the beats are taken in order, a flag raised by one beat is seen by the beats written *after* it the same day, and by those *before* it the next day.

## The four kinds

The kind is [[Language-Guide#The kind is worked out|worked out from what the beat has]]; this is what each means to the game.

**Fixed** (a date and nothing else): the world cannot prevent it. On its day, once `ActivateWhen` holds, `OnActivate` runs. The blockade, the founding of the colony.

**Conditional** (no date; wakes on its conditions): when it is awake and due (waking plus `delay`), and `ActivateWhen` holds, it happens. The brigade beats ("the boats are late and London hears of it") are of this kind.

**Event** (a date, with a `window`, a `lead`, `OnApproach` or `ResolveWhen`): *the world is given the chance to do it itself.* The game applies `OnApproach` pressure as the day nears and watches `ResolveWhen`. If the day comes and the world has not done it, whether it is made to happen depends on the adherence (next section), and then only once `ActivateWhen` holds. The Pemmican Proclamation, Seven Oaks.

  An event with no `ResolveWhen` gives the world no way to do it, so under Gentle and Steady it is never done: it stays awake (its rumour saying *any day now*) and everything that requires it waits. Give such a beat a `ResolveWhen`, a `CancelWhen`, or `kind: Kind.Fixed` (see [[Pitfalls#An event with no ResolveWhen never happens at Steady|Pitfalls]]).

**Pressure** (no `OnActivate`): it never happens as such; it only presses on the world through `OnApproach`. On its due day it is over (its flag is raised, so later beats can require it); at Gentle it is over without pressing at all. The lawless years.

## Adherence: how hard the campaign holds the world

The player (or the server) chooses how closely the world is held to the script:

| Adherence | Pressure (`OnApproach`) | Events not done by the world |
|---|---|---|
| **Gentle** | never | never forced |
| **Steady** | runs as the day nears | never forced: only pressed |
| **Firm** | runs | forced when `2 × window` days late |
| **Strict** | runs | forced when `window` days late |

Fixed and conditional beats are the same at every level, except that their `OnApproach` is skipped at Gentle. When an event is *forced*, `OnActivate` runs and the log says so (`[forced] ...`).

For example, an event dated 1 June with `window: days(30)` that the world has not done is forced on **1 July** at Strict and on **31 July** at Firm, and never at Steady or Gentle. With no `window`, the window is 60 days. A forced event still waits for its `ActivateWhen`.

## Repeating beats

`repeat: days(n)` lets a beat come round again after it has happened (or been resolved by the world):

- a **dated** beat's date moves on by `n` from its *date*, not from the day it happened, so a yearly beat keeps its calendar even if it was late one year (and one dated in the past [[Pitfalls#A repeating beat dated in the past catches up|catches up]]);
- a **dateless** beat rests for `n` days from the day it happened, then may wake again.

Each time round it starts fresh: `OnAwaken` and `OnApproach` run again. `Beat.count` says how many times it has happened, and `Beat.done` is **false** between rounds (use `count` or `age`). A cancelled repeating beat is over for good.

```c
beat BoatsOverdue(repeat: days(300)) {
    AwakenWhen() => brigade("the York boats").isLate && world.inMonths(7, 9);
    OnAwaken()   { say("The York boats are overdue.", Company.HBC); }
    ActivateWhen() => brigade("the York boats").daysLate >= 10;
    OnActivate() { shout("The boats are still not in.", Company.HBC); }
    ResolveWhen() => brigade("the York boats").isIn;
    OnResolve()  { say("The boats are in at last.", Company.HBC); }
}
```

Read as a story: *when the boats are late in summer, the Factor worries; if they are still late ten days on, London hears of it; if they come in first, he is relieved and it is over for the year.*

## Requires, flags and beat names

`[A, B]` after a beat's header means the beat is not awake until A and B have happened. A name in the list is a beat, or a flag (a string). When a beat finishes, a flag with its id is raised, so `flags.has("seven-oaks")` and `SevenOaks.done` are the same question. A **cancelled** beat raises no flag, so whatever requires it never wakes; raise a flag of your own in `OnCancel` if something should follow from the cancellation. The flags the game raises by itself (`late:the York boats`, `heard:<beat id>`, `deed:truce`, `open_hostility` ...) can be required too: see [[State-Flags-and-Variables#Flags the game raises by itself|State, flags and variables]].

## Rumours and keywords

A beat's `rumour:` is listed in the player's journal (*What is stirring*) while the beat is awake and unfinished, and for a dated beat only from `lead` days before its date. It is tagged *any day now* once due, *within a fortnight* (14 days or less), *within weeks* (45 or less), *in the coming months* (120 or less), else *before long*. See [[Player-Deeds-and-Rumours|Player deeds and rumours]].

A dated beat's `keywords:` are listened for by the news director from 120 days before its date until two windows after it, while it is not done: talk that mentions them is told to the player first.

## What the game remembers

A saved game keeps which beats are done, cancelled and awake, when each woke, the campaign's flags and variables, the dice, what is waiting on an `after(...)` and the letters still on the road. The script itself is not saved: loading a game reads the campaign's JSON again, so a campaign can be corrected between sessions (with care: see [[State-Flags-and-Variables#Loading a saved game|loading a saved game]]). A new game starts by treating every beat dated before its first day as [[State-Flags-and-Variables#History before the start|history]].
