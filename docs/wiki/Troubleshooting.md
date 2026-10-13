# Troubleshooting

Compile errors say *line N* and underline the place in the [editor](Web-Editor). The compiler suggests a fix when it can ("Did you mean ..."). The ones that need an explanation:

## "X is not defined"

You used a name the compiler has never seen. Check the spelling (names are case-sensitive: `World` is not `world`), declare it (`const int hot = 50;`, `int counter;`, `beat Name(...)`), and remember a flag is a **string**: `flags.has("governor_killed")`, not `flags.has(governor_killed)`.

## "this is a number, not a condition: compare it"

`world.tension` is a number the game knows; a condition is a yes/no. Write `world.tension >= 50`.

## "the game can compare a number it knows with a fixed number, not with another changing number"

`world.tension > world.standing` cannot be asked. Compare with a number you know when you compile (`world.tension > 50`, or `world.tension > limit` with `const int limit = 50;`), or raise a flag in an effect and test the flag.

## "X changes the world, so it belongs in an effect, not in a condition"

`flags.set(...)`, `news(...)`, `world.raiseTension(...)` and friends *do* things. They go in `OnAwaken`, `OnApproach`, `OnActivate`, `OnResolve` or `OnCancel`. Conditions (`AwakenWhen`, `ActivateWhen`, `ResolveWhen`, `CancelWhen`) only ask.

## "AwakenWhen is a condition: write AwakenWhen() => condition;"

The `...When` hooks are one expression after `=>`, not a block. The `On...` hooks are blocks.

## "'X' is not one of a beat's members"

Inside a beat only the nine hooks, `const`s and your own helper functions go. A helper needs a return type (`void` or a type).

## "a beat has no 'wat'"

A beat's header takes `id`, `title`, `kind`, `date`, `window`, `lead`, `delay`, `repeat`, `keywords`. Anything else belongs in a hook.

## "title is text, but got ..." / "should be a whole number" / "... expects Days"

Header values have types: `title: "..."`, `date: date(1816, 6, 19)`, `window: days(45)` (not `window: 45`), `keywords: ["a", "b"]`.

## "there are two beats with the id ..."

Beats made in a loop need their own ids: `id: "ship-" + year`.

## "more than one beat is called X; use its id as a string"

A loop made several beats with the same name, so `[X]` or `trigger(X)` cannot tell which you mean. Use the id string (`trigger("ship-1816")`).

## "no variable called X"

Variables are declared at the top of the script: `int X;`. (A `const` cannot be assigned.)

## "this does nothing: it only works out ..."

A statement that only calculates something (`world.tension > 3;`, `flags.has("x");`) does nothing in an effect block. Perhaps you meant `if (...) { ... }`.

## "requires 'x', which is not a beat and no action sets it as a flag"

A requires list names something that can never be true, so the beat could never wake. It is an error, not a warning. Either the name is misspelt (beat names are case-sensitive, and a beat made in a loop is required by its id string, `"ship-1816"`), or the flag is raised somewhere the checker cannot see: then wake the beat with `AwakenWhen() => flags.has("x");` instead of requiring it. The flags the game raises (`late:...`, `heard:...`, `deed:...`, `open_hostility`, `brigade_late` ...) are always allowed.

## "a ? : needs a condition known when the script is compiled"

`?:` is worked out when compiling. To choose by the world, use `if` in an effect.

## "today's date is compared with <, <=, > or >="

`world.date == date(...)` is not allowed: a day is a range in the game's terms. Use a dated beat, or `world.date >= date(1816, 6, 19) && world.date < date(1816, 6, 20)`.

## "cannot use * with the variable x and the number 2"

A campaign variable is not a number the compiler knows: it can only be changed (`x += 2`) and compared (`x >= 3`). Put it into text with `{var:x}` (see [State, flags and variables](State-Flags-and-Variables#putting-numbers-into-text)).

## "stock(POST, ITEM): got ..." / "is not a post with books" / "is not a brigade run" / "is not a post on the map"

The name is not in the game's data. Copy it from the [World reference](World-Reference): names are exact (`"Ile-a-la-Crosse"`, `"Churchill (Pr. of Wales Fort)"`, `"the York boats"`).

## A warning: "news to 'X': the usual addressees are ..."

Only a reminder: the four roles always have someone to take a letter. A person's exact name, or `"the master at <post>"`, works too while that person is alive and the post stands; otherwise the letter waits for ever. See [who can be written to](World-Reference#letters-who-can-be-written-to).

## A warning: "reads variable 'x', which nothing sets (it will always be 0)"

The variable is compared but never changed. Usually a typo, or a counter whose `x++` you have not written yet.

## A warning: "dated ... but requires ... : it will wait for it"

A beat is dated before a beat it requires. It is not an error (it will happen once the other has), but it is probably not what the dates say.

## A warning: "reads flag 'x', which nothing sets"

The checker found a beat waiting on a flag that no beat raises. Either a typo, or something outside the script. The flags the game raises itself (the brigade ones, `heard:...`, `deed:...`, `open_hostility`) are known to the checker and never warned about; a flag a scenario raises for testing is. If it is deliberate, nothing is wrong.

## A beat never happens in the simulator

Look at the *never happened* list and ask, in order:

1. Does its `[Requires]` hold? A cancelled beat never counts as done.
2. Does `AwakenWhen` ever hold *in this scenario*? Remember the simulator's tension never falls on its own, letters are never delivered (`heard:` is never raised), and brigades do nothing unless the scenario says so ([the differences](Simulation-Scenarios#how-the-simulator-differs-from-the-game)).
3. Is it an *event* (a date with a `window`, `lead`, `OnApproach` or `ResolveWhen`) with no `ResolveWhen` the scenario makes true, under Gentle or Steady? Events are only pressed, not forced, until Firm.
4. Does `ActivateWhen` ever hold?
5. Did `CancelWhen` hold first? A cancelled beat is in the *never happened* list too; `--log` shows what its `OnCancel` did, and on what day.

Run with `--log` and `--state` to see what the world looked like.

## A beat happens every day at the start of the game

It repeats and is dated before the start: it is catching up, one round a day. See [Pitfalls](Pitfalls#a-repeating-beat-dated-in-the-past-catches-up).

## A beat happens on the first day of play, though it is dated earlier

In the game, only beats dated before 1 January 1811 are history; a fixed beat dated in 1811 before the starting path's day happens on the first day of play. See [when the game starts](World-Reference#when-the-game-starts).

## A beat did nothing in the game

Look at the game's console for `[campaign]` lines: every beat that happens is logged there, and so are warnings for an unknown action, a `trigger` of a beat that does not exist, and stock on a post without books. If the beat happened and the effect still seems missing, check that its target exists *then* (a post that stands, a brigade on the river, a living addressee): see [Effects in the game](Effects-in-the-Game).

## "the installed JSON is stale"

You edited a script in `tools/CampaignTool/src` and did not rebuild: run `dotnet run --project tools/CampaignTool -- build`.

## The editor says "cannot listen on port 8765"

Something else is using it: `serve --port 8800`.
