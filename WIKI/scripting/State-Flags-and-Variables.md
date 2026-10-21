---
title: "State, flags and variables"
aliases: [State Flags and Variables]
tags: [scripting, campaign, tool]
status: reference
---

<!-- Copied from docs/wiki/State-Flags-and-Variables.md by tools/docs/sync_scripting_wiki.py: edit that, not this. -->

# State, flags and variables

What a campaign remembers from day to day, which of it the game writes on its own, and what happens to it across the start of a game and a save. If the [[Language-Guide|Language guide]] is the grammar, this page is the memory.

## Everything the campaign remembers

| | What it is | How a script reads it | How a script changes it |
|---|---|---|---|
| **Flags** | a set of names that are raised or not | `flags.has("x")`, `[ "x" ]` in a beat's requires | `flags.set("x")`, `flags.clear("x")` |
| **Variables** | named numbers, starting at 0 (or the value declared) | `incidents >= 3` | `incidents++`, `+=`, `-=`, `=` |
| **Done beats** | beats that have happened (and are not waiting to repeat) | `Beat.done`, `[Beat]` | by happening |
| **Beat days** | the day each beat last happened | `Beat.age` | by happening |
| **Beat counts** | how many times each beat has happened | `Beat.count` | by happening |
| **Timers** | effects waiting on an `after(...)` | | `after(days(n), ...)` |
| **The dice** | the seed of the campaign's random numbers | `chance(...)` | every roll moves it on |
| **Deeds** | what the player has done | `player.did(...)`, `player.deeds(...)` | the game, when the player does it |
| awake, cancelled, pressed beats; letters on the road; posts founded | the scheduler's own bookkeeping | | |
| **Quests** (per player) | which quests each player has under way, done, failed or refused, and at which step | `Q.done`, `Q.active`, `Q.failed` | `Q.start()`, `Q.complete()`, `Q.fail()`, and the player's own doing |

All of it is **saved with the game** (in the save's campaign section) and all of it lives on the **server**: in multiplayer, clients only see the journal text the host sends them.

Two more things the campaign does *not* own but reads every day: the **tension** (`world.tension`, kept by the game's rivalry between the companies) and the world itself (posts, ledgers, brigades, people). See [[Effects-in-the-Game|Effects in the game]].

Quests are kept **per player**, with the player's character in the save; everything else here belongs to the world and is shared. A quest's state is not a flag: to let the world remember what a player chose, raise a flag in the quest (`flags.set("nwc_friendly")` in a choice), and every beat and every other player's quests can read it. See [[Quests#Quests and beats together|Quests]].

## Beats are flags too

When a beat **finishes** (it happens on its day, when its conditions hold, by the world's own doing through `ResolveWhen`, as pressure, forced, or triggered), a flag with its **id** is raised. So:

```c
flags.has("seven-oaks")   // the same question as
SevenOaks.done            // (for a beat that does not repeat)
```

and a requires list can name a beat by its id string: `[ "seven-oaks" ]`. This is how beats made by a loop (which share a name) are required: `[ "interior-war-1818" ]`.

A beat that is **cancelled** does *not* raise its flag, and does not count as done. Anything that requires it never wakes. If something should follow from the cancellation, have `OnCancel` raise a flag of its own (`flags.set("seven_oaks_averted")`) and require that.

### Repeating beats and `.done`

A repeating beat is taken off the done list as soon as it happens, so that it can come round again. Its **flag stays raised**, and its count goes up. So for a repeating beat:

| Question | Answer after it has happened once |
|---|---|
| `Raid.done` | **false** (it is waiting to come round again) |
| `flags.has("Raid")`, `[Raid]` | true |
| `Raid.count >= 1` | true |
| `Raid.age < 30` | true for 30 days after the last time |

Use `count` or `age` to ask about a repeating beat, never `done`. (A requires list checks the done list *or* the flags, so `[Raid]` works either way.)

## Flags the game raises by itself

These are written by the game, not by any script, and can be read like any other flag. The checker knows them, so reading them is not a "flag nothing sets" warning.

### About brigades

`<run>` is a run's name exactly as in the [[World-Reference#Brigade runs|World reference]]: `late:the York boats`.

| Flag | Raised when | Cleared when | Language |
|---|---|---|---|
| `out:<run>` | a crossing of the run sets out (on its leg's day, or by `launch`) | **never** | `brigade("...").isOut` |
| `late:<run>` | the crossing is past its due day; or it is frozen in short of its landing; or there was nothing to load and it could not leave | it arrives; the next crossing sets out | `.isLate` |
| `in:<run>` | the crossing arrives at its landing (also after being turned back home) | the next crossing sets out | `.isIn` |
| `robbed:<run>` | the other company's men take half its pieces at a carrying place | the next crossing sets out | `.isRobbed` |
| `turned:<run>` | it is turned back at a carrying place, or by `turnBack()` | the next crossing sets out | `.wasTurnedBack` |
| `lost:<run>` | a boat is lost at a rapid (men drowned, pieces gone), or `lose()` | the next crossing sets out | `.isLost` |
| `brigade_late`, `brigade_lost`, `brigade_robbed`, `brigade_turned_back`, `brigade_in` | the same, for **any** run | **never** | `flags.has("brigade_late")` |

Read these carefully:

- `isOut` stays true from the first crossing on: it means *has ever set out*. "On the river now" is `brigade("...").isOut && !brigade("...").isIn`.
- `isIn` is true between crossings. A run has two or three crossings a year (see the [[World-Reference#Brigade runs|legs]]), so `isIn` in July may be about the June crossing.
- A crossing stood down by `cancel()` never arrives, so after it the run is `isOut && !isIn` until its next crossing.
- `isWintering` and `daysLate`/`daysOut` are not flags: the game looks at the crossing now on the river. With nothing on the river they are false and 0.
- The `brigade_*` flags are *ever* flags. `flags.has("brigade_robbed")` is true for the rest of the game after the first robbery anywhere, so it is only useful for "the first time".

### About the world and the player

| Flag | Raised when | Cleared |
|---|---|---|
| `open_hostility` | the tension first reaches 70 (the game's threshold for armed parties) | **never**, even when tension falls again; ask `world.tension >= 70` for "now" |
| `heard:<beat id>` | a letter sent by that beat's `news(...)` is read, or intercepted and read by the other company | never |
| `deed:truce`, `deed:raid`, `deed:escort`, `deed:courier` | the player does it (see [[Player-Deeds-and-Rumours|Player deeds and rumours]]) | never |
| `<beat id>` | the beat finishes | never (a repeating beat's stays raised) |

`heard:` is the way to make a story wait for the news to *arrive*, not merely to be sent:

```c
beat TheFactorKnows() ["heard:red-river-grant"] {
    OnActivate() { say("The Factor has read the Committee's letter about the colony.", Company.HBC); }
}
```

A beat with no `id:` argument has its name as its id, so a letter sent by `beat TheLetter(...)` is heard as `heard:TheLetter`.

## Variables

A variable is a number the campaign keeps:

```c
int incidents;          // starts at 0
float mood = 0.5;       // starts at 0.5 (saved in the JSON's "vars")
```

- Declared at the top of the file, never inside a beat or a loop.
- Changed only by a number known when the script is compiled: `incidents += 2` is fine, `incidents = world.tension` is not. (`incidents = incidents + 2` is understood as `+= 2`.)
- Read only by comparing with a number: `incidents >= 3`. Two variables cannot be compared with each other, nor a variable with the tension.

To carry a *fact* from one beat to another, use a flag; to carry an *amount*, use a variable; to carry a *world number* (how tense, how much rum), there is no way to store it: test it again where you need it, or record that it crossed a line (`if (world.tension > 60) { flags.set("it_was_bad"); }`).

### Variables the game keeps

| Name | What | Read with |
|---|---|---|
| `count:<beat id>` | how many times the beat has happened | `Beat.count` |
| `deed:<kind>` | how many times the player has done it | `player.deeds("kind")` |

Both are ordinary variables in the save, and can also be read with `raw("var(count:Raid) >= 2")`.

## Putting numbers into text

In the text of any effect the game carries out (a `say`, a `shout`, a letter's `from`, `subject` and `text`, a tension's `why`, a flag's name), two forms are filled in with numbers when the effect runs:

| Write | Becomes |
|---|---|
| `{var:incidents}` | the variable's value: `3` |
| `{count:Raid}` | how many times the beat with id `Raid` has happened |

```c
OnActivate() {
    incidents++;
    shout("That is {var:incidents} ambushes on the Hayes this season.", Company.HBC);
}
```

The count of the beat that is running is the number *before* this time (the count goes up as the beat finishes), so in its own `OnActivate`, `{count:Raid}` is 0 the first time. A flag name can carry a number too, which gives a flag per round: `flags.set("raid_{count:Raid}")` raises `raid_0`, `raid_1`, ... (The checker cannot follow names built like this.)

## The dice

`chance(n)` and `random(...)` draw from one random sequence that belongs to the campaign. It starts from the header's `seed:` (or a fixed default) and is saved, so:

- the same script, seed and world give the same history, every time, in the simulator and the game alike;
- every roll moves the sequence on. `chance(...)` in a **condition** rolls *every time the condition is asked*, which is every day the rest of the condition lets it be reached (see [[Pitfalls#chance in a condition rolls every day|Pitfalls]]). Adding or moving a beat that rolls changes the dice of every beat after it.

To check a campaign against bad luck as well as good, run the simulator with `--runs 200`: see [[Simulation-Scenarios|Simulation scenarios]].

## History before the start

On the first day of play, the campaign looks at every beat **dated before that day**, in the order they are written. In the game that day is the first player's starting path's first day (10 June, 12 July or 20 August 1811: see [[World-Reference#When the game starts|when the game starts]]); in the simulator it is the `--start` date. A beat that

- has a date before today, and
- does not repeat, and
- has its requires met (by the history before it)

counts as **already history**: it is marked done (its flag raised, its day set to its date), and of its `OnActivate` effects, **only `flags.set`, `flags.clear` and `world.foundPost` are carried out**. Nothing else is: no letters, no tension, no `say`, no stock, no variables, no `after`, no `trigger`, no dice. Its other hooks (`OnAwaken`, `OnApproach`, `AwakenWhen`, `ActivateWhen`, `CancelWhen`) are not asked at all.

So, for a beat that might be in the past:

- Put what later beats need in **flags**, not variables. A counter `n += 5` in a beat that becomes history is lost.
- Put **posts** it founds in `world.foundPost`, which is replayed.
- A beat dated before the start whose requires are not met (because the beat it needs is dateless, say) is **not** history: it is due at once, and a fixed one happens on the first day, letters and all.
- Dateless beats are never history; they wake as usual.

The variables start at their declared values first, so `int grudge = 2;` is 2 on the first day whatever is history.

## Loading a saved game

A save keeps the campaign's id and adherence and all the state above, but **not the script**: loading reads the campaign's JSON again. This means a campaign can be corrected between sessions, with a few rules:

- State is matched to beats by **id**. Renaming a beat's id loses its state (a done beat becomes undone and may happen again).
- A **new beat** added to the script is treated as if it had always been there but had not happened. History is not replayed on a load, so a new beat dated before the save's date is simply due: a fixed one happens on the first day after loading.
- A removed beat's flag stays raised (flags are just names).
- If the campaign file is gone, the game runs with no script and says so in its log.
- A save made before a campaign had a `start` keeps the date it was saved on: the start only applies to a new world.

## See also

- [[Beats-and-the-Lifecycle|Beats and the lifecycle]]: when each hook runs.
- [[Effects-in-the-Game|Effects in the game]]: what the game does with each effect.
- [[Pitfalls|Pitfalls]]: the mistakes these rules make easy.
