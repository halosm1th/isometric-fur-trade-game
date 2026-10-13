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
| `events` | Things the *world* does on a day: raise (`flag`) or lower (`unflag`) a flag, or set how late a run is (`convoy_days_late` and `run`). This is how you stand in for the player and for history: an *event* beat with a `ResolveWhen` that waits for a flag only happens by the world if the scenario raises it. |
| `random_events` | Dice: raise `flag` about `per_year` times a year in `months` (`"7-9"`), lowering it again after `clear_after_days`. |

A brigade flag is `late:`, `lost:`, `robbed:`, `turned:`, `out:` or `in:` plus the run's name; `brigade("...").isLate` and the others read these.

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
