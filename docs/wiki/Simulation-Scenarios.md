# Simulation scenarios

The simulator plays a campaign forward, a day at a time, against a world *you describe*, without starting the game. The scheduling is the game's own code (`CampaignRunner`), so a beat that fires in the simulator fires in the game; only the world (how tense it is, what is in the ledgers, which boats are late) is invented, and the scenario is how you invent it.

A scenario is a JSON file. Everything in it is optional.

```json
{
  "about": "Free text, for you.",
  "start": "1811-08-20",
  "end": "1821-12-31",
  "adherence": "Steady",
  "player_faction": "HBC",
  "standing": 20,
  "tension": { "1811-01-01": 15, "1814-01-01": 40, "1816-06-01": 80, "1821-04-01": 10 },
  "stocks": { "Edmonton House|TradeGoods": 6, "York Factory|furs": 900 },
  "dead": ["Miles Macdonell"],
  "events": [
    { "on": "1816-06-19", "flag": "seven_oaks" },
    { "on": "1816-06-19", "flag": "governor_killed" },
    { "on": "1819-07-01", "flag": "late:the York boats", "convoy_days_late": 12, "run": "the York boats" }
  ],
  "random_events": [
    { "flag": "late:the York boats", "per_year": 0.5, "months": "7-9", "clear_after_days": 25 },
    { "flag": "lost:the York boats", "per_year": 0.12, "months": "6-9", "clear_after_days": 60 }
  ]
}
```

| Key | Meaning |
|---|---|
| `start`, `end` | The first and last day. What the script dates before `start` is already history. |
| `adherence` | `Gentle`, `Steady`, `Firm` or `Strict` (see [Beats and the lifecycle](Beats-and-the-Lifecycle#adherence-how-hard-the-campaign-holds-the-world)). |
| `player_faction` | `HBC` or `NWC`, for `world.playerFaction`. |
| `standing` | A fixed number for `world.standing`. |
| `tension` | A table of date to tension. The world's tension is the latest row not after today, plus whatever the campaign has added with `raiseTension`. |
| `stocks` | What the ledgers hold: `"Post|Item": amount`, or `"Post|furs"`. The campaign's `addStock` changes these. |
| `dead` | People `world.isDead` is true for. |
| `events` | Things the *world* does on a day: raise (`flag`) or lower (`unflag`) a flag, record a player's deed (`"deed": "truce"`, as if the player had done it), or set how late a run is (`convoy_days_late` and `run`). This is how you stand in for the player and for history: an *event* beat with a `ResolveWhen` that waits for a flag only happens by the world if the scenario raises it. One event may do several of these at once. |
| `random_events` | Dice: raise `flag` about `per_year` times a year in `months` (`"7-9"`), lowering it again after `clear_after_days`. |

A brigade flag is `late:`, `lost:`, `robbed:`, `turned:`, `out:` or `in:` plus the run's name; `brigade("...").isLate` and the others read these. The defaults, for anything a scenario leaves out, are: start 20 August 1811, end 31 December 1821, Steady, an HBC player, tension 20, standing 20, every stock 0, nobody dead.

Some useful events:

```json
{ "on": "1813-05-01", "deed": "truce" }
{ "on": "1812-01-15", "flag": "heard:red-river-grant" }
{ "on": "1816-06-25", "flag": "out:the York boats" }
{ "on": "1816-07-20", "flag": "late:the York boats", "convoy_days_late": 12, "run": "the York boats" }
{ "on": "1816-08-10", "unflag": "late:the York boats", "flag": "in:the York boats" }
```

## How the simulator differs from the game

The scheduling is the game's own, so *when* a beat is woken, pressed, resolved, forced or cancelled is exact. The world is not the game's: it is only what the scenario says, plus what the script itself does to it.

| | In the game | In the simulator |
|---|---|---|
| tension | starts at 20, falls 1.2 a day, moved by raids, robberies, truces and the rest ([table](Effects-in-the-Game#tension)) | the scenario's `tension` table plus the script's `raiseTension`; **never falls on its own** |
| letters | travel, are delivered and read; reading raises `heard:<beat>` | logged when sent, never delivered: `heard:` only if an event raises it |
| deeds | when the player does them | only `"deed"` events |
| brigades | five runs on their legs' days, late, robbed, lost and wintering as the rivers decide | only the flags events and `random_events` raise, plus the script's own `turnBack`, `lose` and `launch`; `daysOut` is always 0 and `isWintering` false; `delay` and `cancel` do nothing |
| stock and furs | the ledgers, which everyone eats from and trades with; a post that does not stand answers nothing | the scenario's `stocks` (0 if not listed, even for a post that does not stand), changed only by `addStock` |
| posts | the world map, `foundPost`, `postsByFoundingYear` | the same rules |
| history | beats dated before **1 January 1811** (the calendar when the world is made); the starting path then jumps to June, July or August | beats dated before `--start` |
| deaths | when people die | only the scenario's `dead` |
| `say`, `shout`, `setLondon` | carried out | logged (with `--log`) |

So read a simulation as "if the world did *this*, the campaign would do *that*". To trust a beat that hangs on the world, try it against more than one world.

## A testing routine

For a campaign of any length:

1. **Check clean.** `check story.camp -v`: no errors, and read every warning and note (a flag nothing sets is usually a typo).
2. **The quiet world.** `sim story.camp --log` with no scenario: tension 20, no deeds, no brigade trouble. What happens when the player does nothing? The *never happened* list should hold only beats that need something.
3. **The violent world and the peaceful world.** A scenario with tension rising to 80, and one where the player brokers a truce early (`"deed": "truce"`). Every branch should be reached in one of them.
4. **Each adherence.** `--adherence Gentle` and `--adherence Strict`: events that are only pressed at Steady are forced at Strict, so a beat missing at Steady and present at Strict needs a `ResolveWhen` (or is meant to be optional).
5. **The start.** The game decides history on 1 January 1811 and then jumps to the starting path's day; the simulator makes everything before `--start` history. If the script has beats dated in 1811, run it with `--start 1811-01-01` too, and read what the first days of play would be (see [when the game starts](World-Reference#when-the-game-starts)).
6. **The dice.** `--runs 200` gives, for each beat, how often it fired and when (earliest, median, latest). A beat that fires in 3% of runs, or in every run on the first week, is probably not what you meant.
7. **The letters.** Anything waiting on `heard:` needs an event to raise it in the simulator; in the game, check that the addressee exists when the letter arrives ([who can be written to](World-Reference#letters-who-can-be-written-to)).
8. **In the game.** `install`, start a new game with the campaign at Steady, and watch the console's `[campaign]` lines. The game can be started on a date with `--date=YYYY-MM-DD`, and with a tension with `--tension=N`.

Keep the scenarios next to the script (`examples/*.scenario.json`) so they can be run again after every change, and, for a campaign that ships, add a test in `tests/CampaignTests` (see the existing ones for a beat that must fire, must not, or must come before another).

## Running

In the [editor](Web-Editor) open the *Simulate* tab. On the command line:

```
dotnet run --project tools/CampaignTool -- sim story.camp --scenario world.json
                                              [--adherence Firm] [--seed 7] [--start 1812-01-01] [--end 1820-01-01]
                                              [--log] [--state] [--json] [--runs 200]
```

- `--log` adds *what each beat did* (every effect, with its date).
- `--state` ends with the flags and variables.
- `--runs N` plays the campaign N times with different dice and tells you, for each beat, how often it fired, how often it had to be forced, and the earliest, median and latest date: the way to find a beat that is too rare or too common.
- `--json` is the whole result for other tools.

The output ends with the beats that **never happened**. Read it: a beat there is either waiting on something the scenario never provides (often right) or on something that can never be true (a bug the checker may not catch).
