# Pitfalls

Things that compile, often pass the checker, and still do not do what you meant. Each one is here because the rules that cause it are easy to miss. Read this once before your first long campaign, and again when a beat misbehaves.

## Timing

### `chance` in a condition rolls every day

A condition is asked **every day**, and `chance(n)` rolls the dice every time it is asked. So

```c
AwakenWhen() => world.tension >= 50 && chance(10);
```

is not "a one-in-ten chance that this happens": it is one in ten **each day** the tension is 50 or more, which over a month is a 96% chance. That is often what you want (a *rate*), but if you want a single throw:

```c
beat MaybeAmbush(date: date(1817, 6, 1)) {
    OnActivate() {
        if (chance(10)) { flags.set("ambush_coming"); }   // in an effect: rolled once
    }
}
```

Two more things follow. A condition list stops at the first part that fails, so put `chance` **last** (`world.tension >= 50 && chance(10)`), and the dice are only rolled on days the rest holds. And every roll moves the campaign's dice on, so a `chance` in one beat's condition changes the rolls of every other beat: the same seed gives the same history only for the same script.

Per-day chances add up quickly: `chance(1)` a day is about a 26% chance in a month and 97% in a year; `chance(5)` a day is 79% in a month.

### An event with no `ResolveWhen` never happens at Steady

A beat with a date *and* any of `window`, `lead`, `OnApproach` or `ResolveWhen` is an **event**: the world is meant to make it happen. Without a `ResolveWhen` the world has no way to, so at **Gentle** and **Steady** adherence (Steady is the default) it is never done. It stays awake forever: its rumour reads *any day now* in the journal for the rest of the game, and every beat that requires it waits for ever.

Fix it one of these ways:

- If it must happen on its day: leave out `window`, `lead`, `OnApproach` and `ResolveWhen` (it becomes **fixed**), or keep them and write `kind: Kind.Fixed`.
- If the world can do it: give it a `ResolveWhen` the world can make true.
- If it might simply not happen: give it a `CancelWhen`, such as `world.date >= date(1817, 1, 1)`, so it is cleared away and its rumour goes.

### Adding `OnApproach` to a fixed beat makes it an event

The kind is [worked out](Language-Guide#the-kind-is-worked-out) from what the beat has, so adding pressure to a dated beat (an `OnApproach`, or just a `lead:`) silently turns it from *fixed* into *event*, and it stops happening at Steady. When a beat must happen on its day **and** press on the world beforehand, say so:

```c
beat SevenOaks(date: date(1816, 6, 19), lead: days(60), kind: Kind.Fixed) {
    OnApproach() { world.raiseTension(15, "Grant's riders gather"); }
    OnActivate() { flags.set("governor_killed"); }
}
```

### `ResolveWhen` is asked from the day the beat wakes

`ResolveWhen` is not kept for the date: it is asked every day the beat is awake. An event dated 1816 with `ResolveWhen() => world.tension >= 80;` is resolved the first time the tension touches 80, even in 1812. If the world should only be able to do it near its day, say so:

```c
ResolveWhen() => world.date >= date(1816, 5, 1) && world.tension >= 80;
```

or keep the beat asleep until then with `AwakenWhen`.

### `OnApproach` runs once, not every day

`OnApproach` runs on the first awake day within `lead` of the due day, once per round. It is not a daily pressure. For pressure that builds, use several beats, a `foreach` that writes one a month, or a repeating beat.

For a beat **without** a date, the due day is the day it woke plus its `delay` (0 by default), and `lead` defaults to 30 days, so `OnApproach` runs on the day it wakes.

### A dated repeating beat keeps its calendar

A dated beat with `repeat: days(365)` is due on its date, then its date plus 365, plus 730 ... **whatever day it actually happened**. If it was held back by `ActivateWhen` until autumn, it is still due again next spring. (365-day repeats drift by a day every leap year.) A dateless repeating beat is different: it sleeps for `repeat` days *from the day it happened*.

### A repeating beat dated in the past catches up

A repeating beat is never [history](State-Flags-and-Variables#history-before-the-start). If its date is before the game's first day, it is due at once; when it happens its date moves on by `repeat`, which may still be in the past, so it is due again the next day. A yearly beat dated 1805 happens **once a day for the first week** of an 1811 game, until its date has caught up. Date the first time on or after the start: a yearly summer beat in a game that begins in 1811 should first be dated in the summer of 1812.

### A heartbeat

A dateless repeating beat with no `AwakenWhen` happens every `repeat` days for ever. That is a heartbeat, which is sometimes what you want; give it an `AwakenWhen` or a `CancelWhen` otherwise.

### Beats run in the order they are written

On each day the beats are taken in file order. A flag raised by a beat further down is seen by beats above it the **next** day; a flag raised by a beat above is seen by the beats below **the same day**. Usually a day does not matter; when it does (a chain that must all happen on one day), write the beats in that order, or use `trigger`.

`after(days(n), ...)` runs at the start of day *today + n*, before any beat. `after(days(0), ...)` runs tomorrow, not today.

## Beats, flags and state

### `.done` is false for a repeating beat

A repeating beat is taken off the done list as soon as it happens, so that it can come round again. Ask `Raid.count >= 1` or `flags.has("Raid")` instead of `Raid.done`. (`[Raid]` in a requires list works either way.) See [State, flags and variables](State-Flags-and-Variables#repeating-beats-and-done).

### A cancelled beat raises no flag

A beat that is cancelled is not done, and its id is not raised as a flag. Anything that requires it never wakes. If the cancellation is itself a turn of the story, raise your own flag in `OnCancel` and let other beats require that.

### `trigger` makes even a cancelled beat happen

`trigger(X)` runs `X`'s `OnActivate` at the end of the day without asking anything: not its date, its requires, its `AwakenWhen`, its `ActivateWhen`, or whether it was cancelled. Only a beat that is already done is skipped. If a triggered beat should respect a cancellation, test the same condition in the beat that triggers it.

### History keeps flags and posts, and nothing else

A beat dated before the game's first day is [already history](State-Flags-and-Variables#history-before-the-start): its flags are raised and its posts founded, but its letters are not sent, its tension not added, its variables not counted, its `after`s not set. A counter that a historical beat adds to stays at 0. Carry anything later beats need in flags.

### The first day of play depends on the starting path

History is decided on the first player's first day, and that day depends on the path: 10 June (Cree, Assiniboine), 12 July (NWC) or 20 August 1811 (HBC). A beat dated between 10 June and 20 August 1811 is history for one player and happens live, letters and all, for another. The simulator's `--start` defaults to the ship's day; run a script with beats in that summer at `--start 1811-06-10` too. Date beats either before 10 June 1811 (history on every path) or after 20 August 1811 (live on every path) if they should be the same for everyone. See [when the game starts](World-Reference#when-the-game-starts).

### Variables cannot hold world numbers

`grudge = world.tension;` is an error: a variable changes only by numbers known when the script is compiled. To remember that the world crossed a line, raise a flag when it does, or count the times with a variable.

## Names

### A name the game does not know is silently false

Posts, runs and items are checked against the game's data (the checker errors on a misspelling). **People are not**: `world.isDead("Robert Semple")` compiles, checks, and is never true, because nobody in the world has that name. The historical figures of the story are not people in the world. Use flags for their fates, and only use `isDead` with a name from the [World reference](World-Reference#people), exactly as written there (`"Mr. William Auld"`, `"Maskwa (chief)"`).

### A post that does not stand answers nothing

`world.stock("Norway House", Item.Pemmican) < 10` is **false** before Norway House is founded in 1814, and so is `>= 10`: a post with no ledger has no number to compare. `world.addStock` on it does nothing. If a beat depends on an inland post, require it: `AwakenWhen() => world.hasPost("Norway House") && world.stock(...) < 10`.

The simulator is different here: it answers 0 for any stock the scenario does not list, so `< 10` is true. A beat can fire in the simulator and never in the game.

### `isOut` never goes false

`brigade("...").isOut` means the run has set out at least once. For "on the river now" write `brigade("...").isOut && !brigade("...").isIn`. The `brigade_late`, `brigade_robbed` and other `brigade_*` flags, and `open_hostility`, are likewise never cleared. See [the flags the game raises](State-Flags-and-Variables#flags-the-game-raises-by-itself).

### A run has more than one crossing a year

`brigade("the York boats").isIn` in July may be about June's crossing. Narrow it with the month: `world.inMonths(7, 8) && brigade("the York boats").isIn`. The [leg table](World-Reference#brigade-runs) says when each crossing leaves.

### Letters that cannot be delivered wait for ever

A `news(to: ...)` letter goes to a living person with that exact name or role address. A letter to a man who is dead and has no successor, to `"the master at Norway House"` before 1814, or to a misspelt name, waits and never arrives, and its `heard:` flag is never raised. `Person.Chief` goes to whichever of the forty-odd chiefs the game finds first; write to a chief by name if it matters which.

## The player

### Lines go only to their company

`say("...", Company.HBC)` is seen only by HBC players: an NWC player, or a Cree or Assiniboine player, sees nothing. A campaign meant for every starting path should either say things to everyone, or choose its words with `world.playerFaction` (remembering that in multiplayer both companies may be playing). A line meant for the player should usually be `say`, not `news`: a letter to the Factor reaches the Factor, and the player only hears it if they are where the Factor talks of it.

### Rumours are promises

A rumour shows while its beat is awake. A beat that is awake but can never happen (an event under Steady with no `ResolveWhen`; a beat waiting on an `ActivateWhen` that will never hold) leaves its rumour in the journal for good. Give every rumoured beat a way to end: it happens, it resolves, or it is cancelled.

## Quests

### A beat's `player.` words mean every player

In a quest, `player.give(Item.Rum)` gives the player the quest runs for a dram. In a **beat** it gives **every** player one, and a beat's `Q.done` is true once **any** player has done the quest. A beat's `player.posted(...)` and `player.rapport(...)` ask the host's own player, which on a dedicated server is nobody. If a beat should answer one player's story, let the quest raise a flag and the beat read it. See [Quests and beats together](Quests#quests-and-beats-together).

### Quest requirements are per player

`quest Inland(...) [Landfall]` starts for a player once *that player* has done `Landfall`. Another player's `Landfall` does not count. For "once anyone has done it", use `StartWhen() => flags.has(...)` with a flag the first quest raises.

### A refused quest is gone

A player who refuses a giver's quest is never offered it again (*Not now* keeps it on offer). If the story must go on regardless, have the refusal's alternative waiting in another quest, or start it from an action (`Q.start()` starts a quest whatever was refused).

### Quests are not in the simulator, and `extends` is not either

The simulator runs only this file's beats: no quests (their conditions are false there) and none of the beats of a campaign this one `extends`. Simulate the base campaign on its own, and test quest lines in the game.

## The start

### A later start does not change the people

`start: date(1818, 6, 1)` sets the calendar and the posts that stand; the cast is the starting cast whatever the year. Mr. William Auld is still the Factor at York in 1818 unless the script makes it otherwise. Write what has changed since 1811 as beats dated before the start (their flags hold) and as effects on the first day.

### `start` is the same day on every path

With `start:`, the ship, the brigade and the summer camp all begin on that one day, whatever the season: a January start lands the ship's passengers in the snow. Use `startYear:` to keep each path's own day.

## Testing

### The simulator is not the game

The simulator runs the same scheduler but an invented world. In particular:

| | In the game | In the simulator |
|---|---|---|
| tension | falls 1.2 a day; moved by raids, truces, robberies ... | the scenario's table plus the script's own changes; never falls |
| letters | delivered and read, raising `heard:` | never delivered; `heard:` only if the scenario raises it |
| deeds | when the player does them | only as scenario `events` |
| brigade flags | from the boats on the rivers | only from scenario `events`, `random_events` and the script's own `turnBack`, `lose` and `launch` |
| `daysOut`, `isWintering` | the crossing's | always 0 and false |
| a stock not listed | nothing (no ledger) if the post does not stand | 0 |
| a person's death | when they die | only those in the scenario's `dead` |

A campaign is tested when it does what you meant in several worlds: a calm one, a violent one, and one where the player does nothing. See [Simulation scenarios](Simulation-Scenarios#a-testing-routine).

### The checker cannot see everything

It finds misspelt posts, runs and items, flags nobody sets, beats that require something that can never happen, cycles, and dates that contradict their requires. It cannot know that a person exists, that a letter has anyone to go to, that a condition is possible in the world, or that an event will never be forced at Steady. The simulator's *never happened* list is where those show up.
