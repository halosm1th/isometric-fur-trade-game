---
title: "Effects in the game"
aliases: [Effects in the Game]
tags: [scripting, campaign, tool]
status: reference
---

<!-- Copied from docs/wiki/Effects-in-the-Game.md by tools/docs/sync_scripting_wiki.py: edit that, not this. -->

# Effects in the game

A script says *what* should happen: `world.raiseTension(10)`, `brigade("the York boats").lose()`, `news(...)`. This page is what the game then really does, with the numbers, so you can judge how big an effect to write. (The code is `Campaign.cs`, `GodotWorld.Effect`, and the systems it calls.)

The language carries out its own words itself: `flags.set`/`clear`, variables, `if`, `random`, `after` and `trigger` (see [[State-Flags-and-Variables|State, flags and variables]]). Everything else on this page is the world's.

## How the player learns of anything

The game's rule is that **nobody knows what they were not told**. A campaign beat changes the world at once, but the player hears of it only through one of these:

| Way | Effect | Reaches the player |
|---|---|---|
| A letter | `news(...)` | when it is delivered and read at a post, days or months later; then it spreads as talk |
| A line in their journal | `say(...)` | at once, every player of that company (or all) |
| A headline | `shout(...)` | at once, everyone, as a headline notice; and the journal line of `say` |
| What happens around them | brigades, stock, tension, posts | when they see it, or someone who did tells them |
| A rumour | the beat's `rumour:` | in the journal's *What is stirring*, before the beat happens |

Choose deliberately. The Battle of Seven Oaks reaching York Factory as a letter thirty-five days later is the game working as designed; a `shout` of it on the day is a different kind of game. A good default: `news` for anything far away, `say` for what the player's own post would know that day, `shout` for what everyone would hear at once, and `rumour` for what is coming.

## Tension

`world.raiseTension(amount, why)` adds to the **rivalry between the companies**, one number from 0 to 100 (`Scripts/Economy/Rivalry.cs`).

| | |
|---|---|
| Starts at | 20 |
| Kept within | 0-100 (raising it past 100 does nothing more) |
| Falls by itself | **1.2 a day**, every day, toward 0 |
| Open hostility | at **70** and over: the companies' leaders send armed parties for each other's stores; the flag `open_hostility` is raised the first time |
| `why` | a line for the log (and the notice when hostility begins or ends); defaults to the beat's title |

Because it falls by 1.2 a day, a single `raiseTension(10)` is gone in about eight days and `raiseTension(30)` in under a month. To hold the country at war for a season, press more than once: a beat with `repeat`, an `OnApproach` and an `OnActivate`, or a `foreach` of monthly pressure. The shipped campaigns' interior-war years raise it by 15 at the start of each summer and let the brigades' own robberies keep it up.

The game moves tension on its own too, so a campaign is never the only hand on it:

| What happens in play | Tension |
|---|---|
| a truce agreed (the player's `truce` deed) | **-35** |
| the council at York orders peace / orders the Bay pressed | -20 / +15 |
| a raid on the stores (something taken / nothing) | +12 / +6 |
| a packet taken on the road | +10 |
| an inland brigade (the player's company boats) taken at a carrying place | +8 |
| a parole broken | +8 |
| a brigade robbed at a carrying place | +6 |
| a company's packet read by the other side | +6 |
| a man taken prisoner | +5 or +6 |
| a man refused back at the door | +5 |
| a brigade turned back at a carrying place | +4 |
| a prisoner escapes | +4 |
| a party turned back at the door | +3 |
| a blow struck between rival men | +2 (in a raid) or +6 |
| furs traded to the other company | a quarter of their worth in MB |

**In the simulator, tension does not fall by itself** and nothing else moves it: it is the scenario's table plus what the script adds. Keep that in mind when a beat waits for `world.tension < 20` (see [[Simulation-Scenarios#How the simulator differs from the game|Simulation scenarios]]).

## London

`world.setLondon(price, quota)` **multiplies** the orders London will send with next year's ship to York Factory:

- `price` multiplies the *standard of trade*: goods paid per MB of fur. Above 1 the Factor pays more and draws more furs (and the country down faster); below 1 he pays less.
- `quota` multiplies the returns the Committee expects, in MB.

They compound with each other and with the Committee's own yearly verdict: when the ship gets home, short returns raise the price by 15-35%, fat ones cut it and raise the quota. The results are kept within **0.3-3** for the price and **10-400 MB** for the quota (it starts at 60). `setLondon(0.85, 0.85)` is a hard year; `setLondon(1.18, 1.15)` a good one. The orders reach the Factor as the Committee's letter on the ship. This acts on the HBC's Factory only; there is no NWC equivalent.

## Posts

`world.foundPost("Fort William")` founds a post from the world map: it appears in the world (its buildings, its ledger, its postmaster and people), and `world.hasPost` becomes true. Founding a post that already stands does nothing. The log says *"Fort William is founded."*

With `postsByFoundingYear: true` in the header, the game does this for you on 1 January of each post's founding year (after the start year); without it, posts founded after the start appear only when a beat founds them. Posts are never removed by a script.

## Stock

`world.addStock(post, item, amount)` puts `amount` pieces of an item into the post's stores, or with a negative amount takes them out (as many as are there; it never goes below 0). It needs a post that stands; otherwise nothing happens and the game logs a warning. See [[World-Reference#Ledgers: which posts keep books|Ledgers]] for which posts keep books and what York Factory starts with.

Stock is what everyone in the post eats, burns, trades and loads into the boats, so a stock effect has consequences you did not write: taking the pemmican makes men hungry, taking the trade goods means the next outfit leaves light (or not at all: a crossing with nothing to load does not leave, and is marked late), and adding furs fattens the returns London judges.

## Brigades

`brigade("run").effect(...)` acts on the run's crossing **now on the river**. If none is (it has arrived, or not yet left), every effect but `launch` does nothing.

| Effect | What the game does |
|---|---|
| `delay(days)` | the boats are held on the bank for `days` (5 if not given), then go on. Their due day does not move, so a long delay makes them late. |
| `turnBack()` | the boats turn round and go back to the post they left, with the pieces still aboard. Raises `turned:` and `brigade_turned_back`. (`turnBack` takes a `days` argument, but the game does not use it.) |
| `lose()` | half of every kind of piece aboard is lost on the road (rounded up); the boats go on with the rest. Raises `lost:` and `brigade_lost`. |
| `cancel()` | the crossing is stood down: every piece and the provisions go back on the books of the post that loaded them, the crew is taken off the map, and any player riding with them is put off. It never arrives, so `in:` is not raised. The run's **later legs still sail** on their days; to stop a run for good, cancel it whenever it sets out (a repeating beat that waits for `isOut && !isIn`). |
| `launch(leg)` | a crossing of leg number `leg` (from 0; see the [[World-Reference#Brigade runs|legs]]) sets out now, ahead of its day, loading from its post's books as usual. Nothing happens if there is no such leg. |

`delay`, `turnBack` and `lose` are told along the road as a headline (`launch` as ordinary news; `cancel` only in the chronicle). Robbery, turning back and lost boats also happen without any script, at the carrying places and rapids, when the companies are at odds; a campaign can read them all through the brigade conditions.

## Letters

`news(from, to, subject, text, days, company)`:

1. The letter is sent at once, sealed, and travels for `days` days (30 if left out; the default sender is `"Montreal"`, the default addressee the Factor, the default subject the beat's title).
2. Then it is handed to the **first living person** whose name or role's address is exactly `to` (see [[World-Reference#Letters: who can be written to|who can be written to]]). If nobody fits, it waits, day after day, until somebody does.
3. The addressee reads it in their own time. When anyone reads it (or the other company intercepts and reads it), it becomes a real event: it is noticed, told about, carried as gossip from then on, and the flag `heard:<beat id>` is raised.
4. A letter is part of the world: it can be carried, delayed, taken on the road and read by the wrong people like any other packet.

So `days` is how long news took to come that far: from London to the Bay by the ship, about a year (the shipped scripts use 400 days for a war letter that must wait for the next ship), from Montreal to the interior two to three months, from the Red River to York a month or two. Letters waiting on the road are saved with the game.

## Lines and headlines

`say(text, to)` adds a dated line to the journal of every player of that company (`Company.HBC`, `Company.NWC`, or everyone if left out) and shows it to them as a notice. `shout(text, to)` does the same and also makes it a **headline** for everyone, which the [Director](https://github.com/halosm1th/isometric-fur-trade-game/blob/master/WIKI/Systems/The%20Director.md) counts toward its pacing. Players of neither company (the Cree and Assiniboine paths) hear only lines sent to everyone.

## The player

These effects act on a player: the one a quest runs for, or, when a **beat** does them, **every** player in the game (see [[Quests#Quests and beats together|Quests and beats together]]). They are mostly used from [[Quests|quests]].

| Effect | What the game does |
|---|---|
| `player.postTo(place)` | the player is put on the books of that post (one that keeps books, by its exact name): its stores, its master and its men are theirs from now on, and their journal says "You are posted to ...". A place with no books logs a warning and does nothing. |
| `player.addStanding(n)` | adds to the player's standing with their company (what `world.standing` reads). Negative lowers it. |
| `player.addRapport(who, n)` | changes what that person (or, for `"role@place"`, every living person who fits) makes of the player, kept within -100 to 100. A quest's `bring` step adds 4, refusing a giver's quest takes 2. |
| `player.give(item, n)` | puts `n` (default 1) of the item in the player's pack. |
| `player.take(item, n)` | takes up to `n` (default 1) of the item from the pack: as many as the player has. |
| `player.note(text)` | a dated line in the player's journal, also said over their head (like `say`, but to one player). |
| `world.addRegard(camp, n)` | changes how a band regards the Company, kept within 0-100 (a band camp by its exact name: see [[World-Reference#Camps (bands)|Camps]]). |
| `Q.start()` | starts the quest now, whatever it requires and whoever gives it (not again if the player already has it). |
| `Q.complete()`, `Q.fail()` | ends the quest as done or failed, running its `OnComplete` or `OnFail`, if it is under way. |

`world.standing` and `player.rapport(...)` read the same numbers these change.

## The log

Every beat that happens is written to the campaign's log (with `[forced]` before the title if adherence made it happen, `[cancelled]` if it was cancelled), and printed to the game's console as `[campaign] date: title`. A `trigger` of a beat id that does not exist, an unknown action, or a stock effect on a post without books is a warning in the console. This is the place to look when a beat seems to have done nothing.

## Escape hatch

`action("verb", key: value, ...)` writes any action the game knows, for a word the language does not have yet. The verbs and their keys are in [[JSON-Format#Actions|JSON format]]. The game warns about a verb it does not know.

## See also

- [[World-Reference|World reference]]: the posts, runs, items and people these effects name.
- [[Library-Reference|Library reference]]: the signature of every effect.
- [[Pitfalls|Pitfalls]]: effects that do nothing, and why.
