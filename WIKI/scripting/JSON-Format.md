---
title: "JSON format"
aliases: [JSON Format]
tags: [scripting, campaign, tool]
status: reference
---

<!-- Copied from docs/wiki/JSON-Format.md by tools/docs/sync_scripting_wiki.py: edit that, not this. -->

# JSON format

The game does not read `.camp` scripts: it reads the JSON the compiler writes into `Campaigns/`. You rarely need to touch it, but it is worth knowing when you want to

- read what the compiler made of a line (the editor's **JSON** tab shows it live),
- edit a campaign that only exists as JSON (`Campaigns/Gemini and Clark.json`, or a player's file in the user folder),
- add a word to the game before the language has one (`raw(...)`, `action(...)`),
- debug a save or a log.

The game's reader is `CampaignScript.Parse` in `Scripts/Core/CampaignRunner.cs`; conditions and actions are evaluated by `Scripts/Core/CampaignLogic.cs`. To turn any JSON campaign back into a script, open it in the [[Web-Editor|editor]] or run `decompile` (see [[Command-Line-Tool|the command-line tool]]).

## A whole example

This script:

```c
int troubles;

beat Ambush(id: "ambush", title: "An ambush at the portage", date: date(1816, 6, 19), window: days(45),
            rumour: "Riders are gathering on the Assiniboine.") [ "red_river_colony" ] {
    AwakenWhen() => world.tension >= 40 && !flags.has("truce") && world.inMonths(11, 3);
    OnApproach() { world.raiseTension(15, "riders on the plains"); }
    ResolveWhen() => world.stock("York Factory", Item.Pemmican) < 10 || brigade("the York boats").isRobbed;
    OnActivate() {
        troubles++;
        flags.set("ambushed");
        news(from: "Montreal", to: Person.Partner, subject: "the ambush", text: "...", days: 60, company: Company.NWC);
        random(option(30, () => brigade("the York boats").delay(4)), option(70, () => say("Nothing came of it.", Company.HBC)));
        after(days(10), () => world.addStock("York Factory", Item.Rum, -5));
        if (world.date > date(1817, 1, 1)) { shout("Late!"); }
    }
}
```

compiles to this beat (the header becomes `title`, `seed` and the rest at the top):

```json
{
  "id": "ambush",
  "title": "An ambush at the portage",
  "kind": "event",
  "date": "1816-06-19",
  "window_days": 45,
  "requires": [ "red_river_colony" ],
  "rumour": "Riders are gathering on the Assiniboine.",
  "when": [ "tension>=40", { "not": "flag:truce" }, "month:11-3" ],
  "pressure": [ { "do": "tension", "add": 15, "why": "riders on the plains" } ],
  "happens": [
    { "do": "add", "var": "troubles" },
    { "do": "flag", "name": "ambushed" },
    { "do": "news", "from": "Montreal", "to": "the wintering partner", "subject": "the ambush",
      "text": "...", "days": 60, "company": "NWC" },
    { "do": "random", "options": [
        { "weight": 30, "do": [ { "do": "brigade", "run": "the York boats", "effect": "delay", "days": 4 } ] },
        { "weight": 70, "do": [ { "do": "say", "text": "Nothing came of it.", "to": "HBC" } ] } ] },
    { "do": "after", "days": 10, "then": [ { "do": "stock", "post": "York Factory", "item": "Rum", "add": -5 } ] },
    { "do": "if", "cond": "after:1817-01-02", "then": [ { "do": "say", "text": "Late!", "headline": true } ] }
  ],
  "done_when": [
    { "any": [ { "m": "stock", "a": [ "York Factory", "Pemmican" ], "op": "<", "v": 10 },
               "convoy_robbed:the York boats" ] }
  ]
}
```

Notice how `&&` at the top of a condition hook becomes a list (every item must hold), `world.date > date(1817, 1, 1)` becomes `after:1817-01-02` (a day is a range, so *after the 1st* is *from the 2nd*), and `Person.Partner` becomes its address.

## The file

| Key | Type | Language | Meaning |
|---|---|---|---|
| `title` | string | header `title:` | the name in the menu |
| `description` | string | header `description:` | shown under the name |
| `note` | string | header `note:` | free text, for people reading the file |
| `seed` | number | header `seed:` | the dice's seed (default 20240611) |
| `posts_by_founding_year` | bool | header `postsByFoundingYear:` | found posts on 1 January of their year |
| `extends` | string | header `extends:` | another campaign's id whose beats are played too |
| `start` | `YYYY-MM-DD` | header `start:` | the world's first day, on every starting path |
| `start_year` | number | header `startYear:` | the year the world begins (each path keeps its day) |
| `questlines` | list | `questline ...` | what the campaign asks of players: [[JSON-Format#Quest lines|below]] |
| `vars` | object | `int x = 3;` | variables that start at something other than 0 |
| `beats` | list | `beat ...` | the beats, in order (the order matters: see [[Beats-and-the-Lifecycle#One day, in order|one day, in order]]) |

## A beat

| Key | Type | Default | Language |
|---|---|---|---|
| `id` | string | `beat<N>` | the beat's name, or `id:` |
| `title` | string | (empty) | `title:` (the compiler defaults it to the id) |
| `kind` | `fixed` `conditional` `event` `pressure` | **`event`** | worked out, or `kind:` |
| `date` | `YYYY-MM-DD` | none | `date:` |
| `window_days` | number | 60 | `window:` |
| `lead_days` | number | 30 | `lead:` |
| `delay_days` | number | 0 | `delay:` |
| `repeat_days` | number | 0 (never) | `repeat:` |
| `requires` | list of strings | | `[A, "flag"]` |
| `keywords` | list of strings | | `keywords:` |
| `rumour` | string | | `rumour:` |
| `when` | conditions | | `AwakenWhen` |
| `on_awaken` | actions | | `OnAwaken` |
| `pressure` | actions | | `OnApproach` |
| `activate_when` | conditions | | `ActivateWhen` |
| `happens` | actions | | `OnActivate` |
| `done_when` | conditions | | `ResolveWhen` |
| `on_resolve` | actions | | `OnResolve` |
| `cancel_when` | conditions | | `CancelWhen` |
| `on_cancel` | actions | | `OnCancel` |

Note the JSON default kind is **event**, not what the compiler would work out: a hand-written beat with a date and `happens` but no `kind` is an event (the world may do it; only Firm and Strict force it). Write `"kind": "fixed"` if it must happen on its day. The checker rejects any other key.

## Conditions

Wherever a condition goes, any of these is accepted, and a **list means all of them**:

| Form | Example | Meaning |
|---|---|---|
| `true` / `false` | `true` | always / never |
| a string | `"tension>=40"` | the compact form, below |
| `{ "all": [ ... ] }` | | every one holds |
| `{ "any": [ ... ] }` | | at least one holds |
| `{ "not": c }` | `{ "not": "flag:truce" }` | c does not hold |
| a fact: `{ "p": name, "a": [args] }` | `{ "p": "post", "a": ["Fort William"] }` | a predicate, below |
| a number: `{ "m": name, "a": [args], "op": op, "v": number }` | `{ "m": "stock", "a": ["York Factory", "Rum"], "op": "<", "v": 10 }` | a metric, below, compared |

The lists are checked in order and stop at the first one that fails (`all`) or holds (`any`). That matters only for `chance`, which rolls when it is reached.

### The compact string

| Write | Means |
|---|---|
| `!c` | not c |
| `name>=N`, `name < N` ... | a metric with no arguments compared with a number (`>= <= > < == !=`) |
| `name(a, b) < N` | a metric with arguments compared |
| `late:the York boats` (and `heard:`, `lost:`, `robbed:`, `turned:`, `out:`, `in:`, `deed:`) | that flag, exactly |
| `name:arg` | a predicate with one argument (`post:Fort William`, `after:1816-06-19`) |
| `name(a, b)` | a predicate with several |
| anything else | a flag of that name |

Arguments in the compact form are split on commas and cannot contain `(`, `)` or `,`; use the `{ "m": ... }` or `{ "p": ... }` form for names that do (the compiler does this for you: `stock` and post names with brackets always come out in dictionary form).

### Predicates (yes or no)

| Name | Argument | True when | Language |
|---|---|---|---|
| `flag` | a flag | it is raised | `flags.has("x")` |
| `beat` | a beat id | it is done (not waiting to repeat) | `X.done` |
| `after` | `YYYY-MM-DD` | today is that day or later | `world.date >= date(...)` |
| `before` | `YYYY-MM-DD` | today is earlier | `world.date < date(...)` |
| `chance` | a percent | a roll of the dice, every time it is asked | `chance(n)` |
| `month` | `6` or `6-8` or `11-3` | the month is in the range (wrapping the year's end) | `world.inMonths(...)` |
| `post` | a post | it stands | `world.hasPost(...)` |
| `dead` | a person's exact name | they are dead | `world.isDead(...)` |
| `player_faction` | `HBC` or `NWC` | a player works for it | `world.playerFaction == ...` |
| `season` | `Winter` `Breakup` `OpenWater` `FreezeUp` | it is that season | `world.season == ...` |
| `convoy_late`, `convoy_lost`, `convoy_robbed`, `convoy_turned_back`, `convoy_out`, `convoy_in` | a run | the flag `late:<run>` (and so on) is raised | `brigade("...").isLate` ... |
| `convoy_wintering` | a run | its crossing is frozen in on the way | `.isWintering` |
| `quest` | a quest id | it is done (for the player a quest runs for; from a beat, for any player) | `Q.done` |
| `quest_active` | a quest id | it is under way | `Q.active` |
| `quest_failed` | a quest id | it has failed | `Q.failed` |
| `posted` | a post | the player is on its books (from a beat: the host's player) | `player.posted(...)` |

### Metrics (numbers)

| Name | Arguments | Value | Language |
|---|---|---|---|
| `tension` | | rivalry, 0-100 | `world.tension` |
| `standing` | | the local player's standing | `world.standing` |
| `month`, `year`, `day`, `day_of_year` | | today | `world.month` ... |
| `var` | a variable | its value (0 if never set) | the variable's name |
| `count` | a beat id | times it has happened | `X.count` |
| `beat_age` | a beat id | days since it last happened (99999 if never) | `X.age` |
| `stock` | post, item | pieces in the post's stores | `world.stock(...)` |
| `furs` | post | furs in its stores, MB | `world.furs(...)` |
| `convoys_active` | | crossings on the rivers | `world.convoysActive` |
| `convoy_days_late`, `convoy_days_out` | a run | of its crossing on the river (0 if none) | `.daysLate`, `.daysOut` |
| `rapport` | who (a name, `Factor`, `role@place`) | what that person makes of the player, -100 to 100 | `player.rapport(...)` |
| `regard` | a band camp | how the band regards the Company, 0-100 | `world.regard(...)` |

A metric the game cannot answer (a post with no ledger, an unknown name) is *not a number*, and every comparison with it is false. An unknown predicate is false, with a warning in the game's console.

## Actions

Every action is an object with `"do"`. A list of actions runs in order. The first eight are the language's own; the rest are carried out by the world.

| `do` | Fields | Language |
|---|---|---|
| `flag` | `name` (default: the beat's id) | `flags.set` |
| `unflag` | `name` (default: the beat's id) | `flags.clear` |
| `set` | `var`, `to` | `x = 3` |
| `add` | `var`, `by` (default 1) | `x++`, `x += n`, `x--` |
| `if` | `cond`, `then`, `else` | `if (...) {} else {}` |
| `random` | `options`: list of `{ "weight": n, "do": [actions] }` | `random(option(...), ...)` |
| `after` | `days`, `then` | `after(days(n), () => ...)` |
| `trigger` | `beat` (an id) | `trigger(X)` |
| `tension` | `add`, `why` (default: the beat's title) | `world.raiseTension` |
| `london` | `price`, `quota` (each default 1) | `world.setLondon` |
| `found_post` | `place` | `world.foundPost` |
| `stock` | `post`, `item`, `add` | `world.addStock` |
| `news` | `from` ("Montreal"), `to` (the Factor), `subject` (the beat's title), `text`, `days` (30), `company` ("HBC") | `news(...)` |
| `say` | `text`, `to` (`all`, `HBC` or `NWC`; default `all`), `headline` (`true` for a shout) | `say`, `shout` |
| `brigade` | `run`, `effect` (`delay` by default, `turn_back`, `lose`, `cancel`, `launch`), `days`, `leg` | `brigade("...").delay()` ... |
| `posting` | `place` | `player.postTo` |
| `standing` | `add` | `player.addStanding` |
| `rapport` | `who`, `add` | `player.addRapport` |
| `regard` | `place`, `add` | `world.addRegard` |
| `give` | `item`, `count` (1) | `player.give` |
| `take` | `item`, `count` (1) | `player.take` |
| `note` | `text` | `player.note` |
| `start_quest` | `quest` | `Q.start()` |
| `end_quest` | `quest`, `failed` (`true` to fail it) | `Q.complete()`, `Q.fail()` |

The player actions act on the player a quest runs for; from a beat, on every player (see [[Quests#Quests and beats together|Quests and beats together]]).

What each world action really does is in [[Effects-in-the-Game|Effects in the game]]. Strings in world actions (and flag names) may hold `{var:name}` and `{count:beat-id}`, filled in when they run ([[State-Flags-and-Variables#Putting numbers into text|more]]).

## Quest lines

`questlines` is a list of lines; a line holds `quests`; a quest holds its steps as `objectives`. This quest line:

```c
questline Servant(title: "The Company's Servant", forCompany: Company.HBC) {
    quest Landfall(title: "Report to the Factor", summary: "You are the Company's new hand.") {
        talk Report(who: Person.Factor, text: "Find Mr. Auld and report to him") {
            Says() => "\"Can you carry?\"";
            choice Keen(label: "\"Put me to work, sir.\"", reply: "\"Good.\"", goto: Unload) { player.addStanding(2); }
        }
        task Unload(task: Task.CarryCargo, count: 6, text: "Carry six lots to the warehouse");
        reach Inland(place: "Edmonton House", radius: 300);
        OnComplete() { player.postTo("Edmonton House"); }
    }
    quest Hunt(title: "The hunt", giver: "leader@Edmonton House") [Landfall] {
        StartWhen() => world.regard("Beaver Hills Cree (Amiskwacîwiyiniwak)") >= 40;
        hunt Buffalo(species: "Buffalo", count: 2);
    }
}
```

compiles to:

```json
"questlines": [ {
  "id": "Servant", "title": "The Company's Servant", "for": "HBC",
  "quests": [
    { "id": "Landfall", "title": "Report to the Factor", "summary": "You are the Company's new hand.",
      "on_complete": [ { "do": "posting", "place": "Edmonton House" } ],
      "objectives": [
        { "id": "Report", "kind": "talk", "text": "Find Mr. Auld and report to him", "who": "Factor",
          "dialogue": { "text": "\"Can you carry?\"",
            "choices": [ { "label": "\"Put me to work, sir.\"", "reply": "\"Good.\"",
                           "do": [ { "do": "standing", "add": 2 } ], "goto": "Unload" } ] } },
        { "id": "Unload", "kind": "task", "text": "Carry six lots to the warehouse", "count": 6, "task": "CarryCargo" },
        { "id": "Inland", "kind": "reach", "place": "Edmonton House", "radius": 300 } ] },
    { "id": "Hunt", "title": "The hunt", "giver": "leader@Edmonton House", "requires": [ "Landfall" ],
      "start_when": [ { "m": "regard", "a": [ "Beaver Hills Cree (Amiskwacîwiyiniwak)" ], "op": ">=", "v": 40 } ],
      "objectives": [ { "id": "Buffalo", "kind": "hunt", "count": 2, "species": "Buffalo" } ] } ] } ]
```

| Object | Keys |
|---|---|
| a line | `id`, `title`, `summary`, `for` (`HBC` or `NWC`), `quests` |
| a quest | `id`, `title`, `summary`, `giver`, `for`, `requires` (quest ids), `start_when`, `fail_when` (conditions), `on_start`, `on_complete`, `on_fail` (actions), `objectives` |
| a step | `id`, `kind` (`talk` if missing), `text`, `who`, `place`, `radius`, `item`, `count`, `task`, `convoy`, `dest`, `species`, `days`, `cond` (conditions), `dialogue`, `on_done` (actions), `next` (a step id, `complete` or `fail`) |
| `dialogue` | `text` (what is said), `choices` |
| a choice | `label`, `reply`, `when` (conditions), `do` (actions), `goto` |

In a quest, `Person.Factor` is written `"Factor"` (and `Partner`, `Chief`, `Postmaster`): the person, not the letter address a `news(...)` uses. The checker rejects unknown keys here as for beats.

## Hand-editing: what to watch

- A hand-written JSON beat with a date and no `kind` is an **event**; the compiler would have called it fixed.
- `requires` names must be beat ids or flags some action raises; the checker reports anything else as an error, because the beat could never wake.
- The game ignores keys it does not know, so a typo (`"happen"`) silently does nothing. Run `check` on any JSON you edit by hand: it accepts `.json` as well as `.camp`.
- Campaigns that ship in the repository are generated from `tools/CampaignTool/src`: edit the script, not the JSON (a test fails if they disagree).
