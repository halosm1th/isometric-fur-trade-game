# Campaign tool

Campaign scripts are programs. You write them in a small Pascal-like language,
and this tool checks them, draws them, plays them forward without starting the
game, and compiles them to the JSON the game reads.

```
dotnet run --project tools/CampaignTool -- check     story.camp      # errors and warnings (-v notes, --strict)
dotnet run --project tools/CampaignTool -- compile   story.camp      # -> story.json
dotnet run --project tools/CampaignTool -- install   story.camp      # -> Campaigns/story.json (the menu lists it)
dotnet run --project tools/CampaignTool -- build                     # every src/*.camp -> Campaigns/*.json
dotnet run --project tools/CampaignTool -- decompile Campaigns/x.json -o x.camp    # move an old JSON script across
dotnet run --project tools/CampaignTool -- graph     story.camp      # mermaid; --dot for graphviz
dotnet run --project tools/CampaignTool -- sim       story.camp --scenario s.json [--runs 200] [--adherence Strict] [--log] [--state] [--json]
```

`src/historical.camp` is the source of `Campaigns/historical.json`: edit the
`.camp`, run `build`. A test fails if the two disagree.
`examples/showcase.camp` uses every feature once.

## The language

```pascal
campaign PemmicanWar;

const
  Title       = 'The Pemmican War, 1811-1821';       { these four are the campaign's details }
  Description = 'Selkirk''s colony, Seven Oaks, the union.';
  PostsByFoundingYear = true;
  Seed        = 7;                                   { the dice the script rolls }
  Hot         = 50;                                  { your own constants }

var
  mood, incidents : integer;                         { script variables: declared, start at 0 }

procedure Dispatch(recipient, subject : string; days : integer);   { a macro, expanded where it is called }
begin
  News('Montreal', recipient, subject, 'A letter.', days);
end;

beat 'seven-oaks' : event on 1816-06-19 'The Battle of Seven Oaks';
  window 45;                                         { grace before it is forced }
  lead 90;                                           { pressure starts this many days before }
  requires 'pemmican-proclamation';                  { beats or flags that must already hold }
  keywords 'Semple', 'Red River';                    { the director tells news with these words first }
  when (Tension >= Hot) and not Flag('peace');       { several `when`s must all hold }
  done when Flag('seven_oaks');                      { the world got there by itself }
  pressure Tension(10, 'the colony is armed');
  happens
  begin
    Flag('governor_killed');
    Inc(mood, 2);
    if ConvoyLate('the York boats') then
      Shout('The Company''s boats are overdue.', HBC)
    else if Stock('York Factory', Rum) < 10 then
      Stock('York Factory', Rum, -5);
    case random of                                   { the labels are weights }
      30: Brigade('the York boats', TurnBack);
      70: begin Brigade('the York boats', Delay, 4); Inc(incidents); end;
    end;
    after 10 days do Trigger('the-end');
    Dispatch(Factor, 'Blood on the plain', 40);
  end;
end;

for y := 1818 to 1820 do                             { a loop writes a beat for each value }
  beat 'interior-war-' + y : pressure on y + '-06-01';
    requires 'seven-oaks';
  end;

end.
```

Pascal's shape, with three kindnesses: a comparison binds tighter than `and` / `or`
(so `Tension >= 40 and Flag('x')` needs no brackets); strings are joined with `+`
(`'one'#10'two'` has a new line in it; `#10` is a character); and a procedure is a
macro, so its arguments must be known when the script is compiled.
Comments are `{ ... }`, `(* ... *)` and `// ...`. Keywords and the built-in names are not
case sensitive; your own names and strings are. Reserved words: `campaign const var
procedure beat begin end if then else for to do after days case random of and or not on`.

Beat kinds: `fixed` (on its date, always), `conditional` (once its `requires` / `when` hold, always),
`event` (the world may produce it; Firm and Strict force it), `pressure` (only its pressure).
Clauses: `window lead delay repeat requires keywords when done when pressure happens`.
`repeat N` makes a beat happen again N days later (a conditional one rests that long).

### Conditions

| Written | Means |
|---|---|
| `Flag('x')`, `Beat('x')` | the flag is raised / the beat has happened (a beat raises a flag of its own id) |
| `Post('Fort William')`, `Dead('Name')` | the post stands / the person is dead |
| `Today >= 1816-06-19`, `Today < ...` | the date (`>`, `<=` too) |
| `InMonths(6, 8)`, `InMonths(11, 3)` | in these months (wraps round the year); `InMonths(7)` is one month |
| `Season(Winter)` | Breakup, OpenWater, FreezeUp or Winter |
| `Chance(30)` | 30%, rolled each time it is looked at |
| `PlayerFaction(HBC)` | a player of that company is in the game |
| `ConvoyLate('the York boats')` | also `ConvoyLost`, `ConvoyRobbed`, `ConvoyTurnedBack`, `ConvoyOut`, `ConvoyIn`, `ConvoyWintering` |
| `Tension >= 40`, `Standing >= 10` | the rivalry (0-100); the player's standing |
| `Stock('York Factory', Rum) < 10` | what a post holds; `Furs('York Factory')` its furs in made beaver |
| `ConvoyDaysLate('the York boats') > 5`, `ConvoyDaysOut(...)`, `ConvoysActive` | the boats |
| `Year`, `Month`, `Day`, `DayOfYear` | the calendar |
| `mood >= 3` | a declared variable |
| `Count('seven-oaks') >= 2`, `BeatAge('seven-oaks') > 30` | how often, and how long ago |
| `Raw('heard:x')` | a condition in the game's own JSON form (an escape hatch) |

Join with `and`, `or`, `not` and brackets. A number the game does not know is never true.
Either side of a comparison may hold the number, the other must be a constant.

### Statements

| Written | Does |
|---|---|
| `Flag('x')`, `Unflag('x')` | |
| `mood := 2`, `mood := mood + 3`, `Inc(mood)`, `Inc(mood, 3)`, `Dec(mood)` | script variables |
| `Tension(15, 'why')` | the rivalry |
| `London(0.8, 0.9)` | multiplies next year's price and quota |
| `FoundPost('Fort William')` | |
| `News(from, to, subject, text, days [, company])` | a sealed letter. `to`: `Factor`, `Postmaster`, `Partner`, `Chief`, or a person |
| `Brigade('the York boats', Delay, 5)` | `Delay` (days), `TurnBack`, `Lose` (half the cargo), `Cancel` (stood down, cargo home), `Launch, leg` (send a leg now) |
| `Stock('York Factory', Rum, -10)` | add (or take) an item from a post's shelves |
| `Say('text', HBC)`, `Shout('text')` | a line for one company's players (`HBC`, `NWC`, `All`; `All` by default); `Shout` also puts it on the screen |
| `Trigger('other-beat')` | make another beat happen now |
| `after 10 days do ...` | do this later (kept in the save) |
| `if ... then ... else ...`, `begin ... end` | |
| `case random of 30: ...; 70: ...; end` | pick one, by weight |
| `for i := 1 to 3 do ...` | written out three times |
| `Action('name', 'key', value, ...)` | anything the game does that the language has no word for |

## What each command is for

* **check** catches what the game would warn about or silently ignore: a typo in a name, a post, brigade run or item
  that is not in the game's data, a beat that requires something that never happens, cycles, a date earlier than what it
  requires, flags and variables nothing sets. Most mistakes are caught earlier still, at compile time, with a line number.
* **sim** runs the game's own `CampaignRunner` (the same code `Campaign.cs` uses) over a made-up world. A *scenario*
  (see `examples/pemmican-war.scenario.json`) says what the world does: a tension timeline, flags raised on given
  days or at random, stocks. `--runs N` varies the dice and shows how often each beat happens, how often it is forced,
  and when. It does not simulate the boats themselves: tell it when they go late or are lost.
* **graph** draws requires, triggers, and flag-sets-flag-read links.

## Keeping it honest

`dotnet test tests/CampaignTests` runs: the language's own cases (`cases.json`), the compiler and decompiler (every
shipped script survives JSON -> program -> JSON unchanged), the checker, the simulator, that the shipped JSON is what
its source builds, and that the tool knows every action and condition the game does (and the other way round).
The language itself is evaluated by `Scripts/Core/CampaignLogic.cs` and `CampaignRunner.cs`, which the game, the tool
and the tests all compile.
