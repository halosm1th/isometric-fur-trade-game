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

## A warning: "reads flag 'x', which nothing sets"

The checker found a beat waiting on a flag that no beat raises. Either a typo, or something outside the script (the game raises some flags itself: `late:the York boats` and the other brigade ones, `heard:...`). If it is deliberate, nothing is wrong.

## A beat never happens in the simulator

Look at the *never happened* list and ask, in order: does its `[Requires]` hold? does `AwakenWhen`? is it an *event* with no `ResolveWhen` under *Steady* adherence (events are only pressed, not forced, until *Firm*)? does `ActivateWhen` ever hold? Run with `--log` and `--state` to see what the world looked like.

## "the installed JSON is stale"

You edited a script in `tools/CampaignTool/src` and did not rebuild: run `dotnet run --project tools/CampaignTool -- build`.

## The editor says "cannot listen on port 8765"

Something else is using it: `serve --port 8800`.
