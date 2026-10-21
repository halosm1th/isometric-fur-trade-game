# Beats and the lifecycle

A **beat** is something that may happen to the world. This page is what the game does with one, day by day, and what the nine hooks of a beat are for. (The *syntax* of a beat is in the [Language guide](Language-Guide); the names you can use inside the hooks are in the [Library reference](Library-Reference).)

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

Each day the game takes every beat that has not finished, in the order they are written, and asks:

1. **Cancelled?** If `CancelWhen` holds, the beat is cancelled: `OnCancel` runs and it is over. This happens before anything else, on every day, whatever the adherence: a cancelled beat cannot be forced.
2. **Awake?** The beat needs everything in its `[Requires]` to have happened, *and* `AwakenWhen` to hold. If not, it sleeps and nothing else is asked. (A beat is awake on the days these hold; if they stop holding it goes back to sleep.)
3. **First day awake?** `OnAwaken` runs, once.
4. **Resolved by the world?** If `ResolveWhen` holds, `OnResolve` runs and the beat is over without `OnActivate` ever running. This is how an *event* the world produced by itself is recognised.
5. **Is it time to press?** A beat is **due** on its `date` (a dateless beat is due `delay` days after it woke). `OnApproach` runs once, when today is within `lead` days of the due day (30 by default). Under the *Gentle* adherence it never runs.
6. **Is it due?** If not, that is all for today.
7. **Happen.** What follows depends on the beat's *kind*.

Beats that other beats `trigger(...)` happen at the end of the day, whatever their date, even if they are asleep.

## The four kinds

The kind is [worked out from what the beat has](Language-Guide#the-kind-is-worked-out); this is what each means to the game.

**Fixed** (a date and nothing else): the world cannot prevent it. On its day, once `ActivateWhen` holds, `OnActivate` runs. The blockade, the founding of the colony.

**Conditional** (no date; wakes on its conditions): when it is awake and due (waking plus `delay`), and `ActivateWhen` holds, it happens. The brigade beats ("the boats are late and London hears of it") are of this kind.

**Event** (a date, with a `window`, a `lead`, `OnApproach` or `ResolveWhen`): *the world is given the chance to do it itself.* The game applies `OnApproach` pressure as the day nears and watches `ResolveWhen`. If the day comes and the world has not done it, whether it is made to happen depends on the adherence (next section), and then only once `ActivateWhen` holds. The Pemmican Proclamation, Seven Oaks.

**Pressure** (no `OnActivate`): it never happens as such; it only presses on the world through `OnApproach`. The lawless years.

## Adherence: how hard the campaign holds the world

The player (or the server) chooses how closely the world is held to the script:

| Adherence | Pressure (`OnApproach`) | Events not done by the world |
|---|---|---|
| **Gentle** | never | never forced |
| **Steady** | runs as the day nears | never forced: only pressed |
| **Firm** | runs | forced when `2 × window` days late |
| **Strict** | runs | forced when `window` days late |

Fixed and conditional beats are the same at every level. When an event is *forced*, `OnActivate` runs and the log says so (`[forced] ...`).

## Repeating beats

`repeat: days(n)` lets a beat come round again `n` days after it happened. A dated beat's date moves on by `n`; a dateless beat sleeps for `n` days, then may wake again. `OnAwaken` runs again each time round; `Beat.count` says how many times it has happened.

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

`[A, B]` after a beat's header means the beat is not awake until A and B have happened. A name in the list is a beat, or a flag (a string). When a beat finishes, a flag with its id is raised, so `flags.has("seven-oaks")` and `SevenOaks.done` are the same question.

## What the game remembers

A saved game keeps which beats are done, cancelled and awake, when each woke, the campaign's flags and variables, and what is waiting on an `after(...)`. The script itself is not saved: loading a game reads the campaign's JSON again, so a campaign can be corrected between sessions.
