# Campaign tools

Write campaign scripts in a compact language (`.camp`), check them, draw them,
and play them forward without starting the game.

```
python3 tools/campaign/camp.py check    story.camp        # errors and warnings (-v for notes)
python3 tools/campaign/camp.py compile  story.camp        # -> story.json, the file the game reads
python3 tools/campaign/camp.py install  story.camp        # -> Campaigns/story.json (the menu lists it)
python3 tools/campaign/camp.py build                      # every src/*.camp -> Campaigns/*.json
python3 tools/campaign/camp.py decompile Campaigns/x.json # move an old JSON script across
python3 tools/campaign/camp.py graph    story.camp        # mermaid; --dot for graphviz
python3 tools/campaign/camp.py sim      story.camp --scenario s.json [--runs 200] [--adherence Strict] [--log]
```

`src/historical.camp` is the source of `Campaigns/historical.json`: edit the
`.camp`, run `build`. A test fails if the two disagree.

## The language

Indentation (two spaces) nests; `#` is a comment; names with spaces go in quotes;
`"""` opens a long text.

```
campaign "Title"
description "Shown in the menu."
posts_by_founding_year
seed 7                          # the dice the script rolls (random, chance)

beat seven-oaks event 1816-06-19 "The Battle of Seven Oaks"
  window 45                     # grace before it is forced (Firm: two windows, Strict: one)
  lead 90                       # pressure starts this many days before the date
  delay 0                       # for beats with no date: days after they become ready
  repeat 365                    # happens again this many days later (a conditional one rests this long)
  requires pemmican-proclamation, red-river-grant     # beats or flags that must already hold
  keywords Semple, "Red River"  # the director tells news with these words first
  when tension >= 40 and not flag(peace)              # several `when` lines all must hold
  done_when flag(seven_oaks)    # the world got there by itself
  pressure                      # run from `lead` days before, at Steady and up
    tension +10 "the colony is armed"
  happens
    ...actions
```

Kinds: `fixed` (on its date, always), `conditional` (once requires/when hold, always),
`event` (the world may produce it; Firm and Strict force it), `pressure` (only its pressure).

### Conditions

| Written | Means |
|---|---|
| `flag(x)`, or a bare `x` | the flag is raised (beats raise a flag of their own id when they happen) |
| `beat(x)` | the beat has happened |
| `post("Fort William")` | the post stands |
| `dead("Name")` | that person is dead |
| `after(1816-06-19)`, `before(...)` | the date |
| `month(6-8)`, `month(11-3)` | in these months (wraps round the year) |
| `season(Winter)` | Breakup, OpenWater, FreezeUp or Winter |
| `chance(30)` | 30%, rolled each time it is looked at |
| `player_faction(HBC)` | a player of that company is in the game |
| `convoy_late("the York boats")` | also `convoy_lost`, `convoy_robbed`, `convoy_turned_back`, `convoy_out`, `convoy_in`, `convoy_wintering` |
| `tension >= 40` | the rivalry, 0-100 |
| `standing >= 10` | the player's standing |
| `stock("York Factory", Rum) < 10` | what a post holds; `furs("York Factory")` for its furs in made beaver |
| `convoy_days_late("the York boats") > 5`, `convoy_days_out(...)`, `convoys_active` | the boats |
| `year >= 1816`, `month`, `day`, `day_of_year` | the calendar |
| `$hostility >= 3`, or `var(hostility) >= 3` | a script variable (0 until set) |
| `count(seven-oaks) >= 2` | how many times a beat has happened |
| `beat_age(seven-oaks) > 30` | days since it last happened (99999 if it has not) |

Join with `and`, `or`, `not`, and brackets. A number the world does not know is never true.
`heard:x`, `late:the York boats` etc. are flags the engine raises and read as flags.

### Actions

| Written | Does |
|---|---|
| `flag x` / `unflag x` | |
| `set mood = 2`, `add mood += 1`, `add mood -= 1`, `add mood` | script variables |
| `tension +15 "why"` | the rivalry |
| `london price=0.8 quota=0.9` | multiplies next year's orders |
| `found_post "Fort William"` | |
| `news from="Montreal" to=factor days=60 company=HBC` + `subject "..."` / `text """..."""` underneath | a sealed letter. `to`: `factor`, `postmaster`, `partner`, `chief`, or a person |
| `brigade "the York boats" delay days=5` | `delay`, `turn_back`, `lose` (half the cargo), `cancel` (stood down, cargo home), `launch leg=0` (send a leg now) |
| `stock "York Factory" Rum -10` | add (or take) an item from a post's shelves |
| `say "text" to=HBC headline` | a line for one company's players (`to=all` by default); `headline` also shouts it |
| `trigger other-beat` | make another beat happen now |
| `after 10 days:` + block | do these later (kept in the save) |
| `if cond:` / `elif cond:` / `else:` + blocks | |
| `random:` with `30%:` / `weight 2:` blocks | pick one, by weight |
| `anything key=value ...` | passed to the game as written (and the checker will say it is unknown) |

`{var:mood}` and `{count:seven-oaks}` in a text become the number.

### Macros and loops

```
macro dispatch(to, subject, days=40):
  news from="Montreal" to={{to}} subject={{subject}} days={{days}}
  {{block}}                      # whatever is indented under the `use` line

use dispatch to=factor subject="Peace"

for y in 1818, 1819, 1820:
  beat interior-war-{{y}} pressure {{y}}-06-01
```

## What each tool is for

* **check** catches what the game would warn about or silently ignore: a typo in an action or condition,
  a post, brigade run or item that is not in the game's data, a beat that requires something that never happens,
  cycles, a date earlier than what it requires, flags and variables nothing sets.
* **sim** is `Campaign.Tick` ported. A *scenario* (see `examples/pemmican-war.scenario.json`) says what the world
  does: a tension timeline, flags raised on given days or at random, stocks. `--runs N` varies the dice and shows
  how often each beat happens, how often it is forced, and when. It does not simulate the boats themselves:
  tell it when they go late or are lost.
* **graph** draws requires, triggers, and flag-sets-flag-read links.

## Keeping it honest

* `python3 tools/campaign/test_campaign.py` — the tool's tests, including that the shipped JSON is built from the
  source and that the tool knows every action and condition the game does.
* `tools/campaign/conformance.json` — cases for the language itself, run against this tool
  (`run_conformance.py`) and against the game's evaluator, `Scripts/Core/CampaignLogic.cs`
  (`dotnet run --project tests/CampaignTests`). If you change the language, change both and add a case.
