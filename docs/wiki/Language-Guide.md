# The language guide

A campaign script is a text file ending in `.camp`. It looks like C# or Java: braces, semicolons, `//` comments, typed declarations. This page is what every piece of it means. The names you can call (`world`, `flags`, `news(...)`) are in the [Library reference](Library-Reference); *when* each part of a beat runs is in [Beats and the lifecycle](Beats-and-the-Lifecycle).

## The one idea to hold on to: two times

A script is run twice, at two different times.

1. **When it is compiled**, by the tool. Constants, loops, helper functions and `if` on a constant are all worked out *then*, and are gone by the time the game runs.
2. **When the game plays it.** What is left is a list of beats, each with conditions the game asks of the world and effects it does to the world. The game never runs your loops or your functions: it reads the result.

That is why a condition like `world.tension >= 50` is *kept* (the game asks it every day) while `hot + 10` is *folded* to `60` before the game ever sees it, and why a `for` loop is a way of **writing** beats, not something that happens in the world.

The editor's JSON tab shows exactly what the game receives.

| Worked out when compiled (gone by play time) | Kept for the game to ask or do every day |
|---|---|
| `const`s and locals, arithmetic on them, `+` on strings | conditions on the world: `world.tension >= 50`, `flags.has("x")`, `chance(10)` |
| functions (written out where they are called) | effects: `flags.set`, `news`, `world.raiseTension` ... |
| `for`, `foreach` (they write beats or effects) | variables: `incidents++`, `incidents >= 3` |
| `if` on a constant, `?:` | `if` on a condition the world answers |
| `date(...) + days(...)`, list indexing | timers (`after`), dice (`random`, `chance`), `trigger` |

So a helper `bool tense() => world.tension >= 50;` does not *compute* anything: it is replaced by the condition `world.tension >= 50`, which the game then asks. And `foreach (int y in 1812..1820)` does not loop in 1812: it writes nine beats before the game starts.

## Layout of a file

```text
campaign Name(title: "...", description: "...");     // 1. the header, once

int counter;                                          // 2. declarations, in any order:
const int hot = 50;                                   //    variables, constants,
void helper(string s) { say(s); }                     //    functions,
beat Something(...) { ... }                           //    beats,
foreach (int y in 1818..1820) { beat Each(...); }     //    loops that write them,
questline Story(title: "...") { quest ... }           //    and quest lines (see Quests)
```

## The campaign header

The file begins with one header. Its values are always named:

```c
campaign CompanysServant(
    title: "The Company's Servant",          // required: the name in the menu
    description: "Off the ship at York ...",  // under the name in the menu
    note: "for whoever opens the JSON",       // kept in the JSON, shown nowhere
    seed: 1811,                               // the dice: the same seed gives the same history
    postsByFoundingYear: true,                // posts appear on 1 January of their founding year
    extends: "historical",                    // play that campaign's beats too
    start: date(1815, 5, 1)                   // the world's first day (or startYear: 1815)
);
```

| | |
|---|---|
| `title` | Required. The campaign's name in the menu. |
| `description` | A few sentences under the name in the menu. If the campaign has a start, "Begins 1 May 1815." is added to it there. |
| `note` | Free text kept in the JSON. |
| `seed` | The seed of the campaign's dice (`chance`, `random`). |
| `postsByFoundingYear` | `true`: posts on the world map appear on 1 January of the year they were founded. Without it, a post founded after the start appears only when a beat founds it. |
| `extends` | Another campaign's id (its file name, `"historical"`) whose beats this one plays as well. See [building on another campaign](Quests#building-on-another-campaign). |
| `include` | Parts of this script kept in their own files: `include: "american-road"` reads `tools/CampaignTool/src/parts/american-road.camp` (several: `"a, b"`). A part has no header; its consts, beats and quest lines are this campaign's own, and it all compiles into one JSON. Errors in a part name the part and its own line. |
| `start` | The world's first day, on every starting path: `date(1815, 5, 1)`. See [a campaign's own start](World-Reference#a-campaigns-own-start). |
| `startYear` | Only the year: each starting path keeps its own day in it. Give `start` or `startYear`, not both. |

The name after `campaign` (`CompanysServant`) is only a name for the script; the game knows a campaign by its file name.

## Comments

`// to the end of the line` and `/* between */`. Comments are for people; they are not kept in the JSON.

## Values and types

| Type | What it is | Written |
|---|---|---|
| `int` | a whole number | `3`, `-5` |
| `float` | any number | `0.85` |
| `string` | text | `"York Factory"` |
| `bool` | true or false, or a *condition* the world will be asked | `true`, `world.tension > 40` |
| `Date` | a day | `date(1816, 6, 19)` |
| `Days` | a length of time | `days(45)` |
| `Company` | which company | `Company.HBC`, `Company.NWC`, `Company.All` |
| `Person` | who a letter is for | `Person.Factor`, `Person.Postmaster`, `Person.Partner`, `Person.Chief`, or any text |
| `Item` | a trade good | `Item.Rum`, `Item.Pemmican` ... |
| `Quest` | a quest | its name, `Landfall` |
| `Task` | a task a quest step hands out | `Task.CarryCargo`, `Task.Brigade` ... |
| `Season` | the river's season | `Season.Breakup`, `OpenWater`, `FreezeUp`, `Winter` |
| `Kind` | a beat's kind | `Kind.Fixed`, `Conditional`, `Event`, `Pressure` |
| `Beat` | a beat | its name, `SevenOaks` |
| `Brigade` | a run on the rivers | `brigade("the York boats")` |
| `string[]`, `int[]` | a list | `["a", "b"]`, `[1, 2, 3]` |
| `var` | whatever the value is (for locals and parameters) | `var who = Person.Factor;` |

Strings use double quotes and the escapes `\"`, `\\`, `\n` (new line) and `\t`. A string may not run over a line: join pieces with `+`, which is how the long letters in the examples are written:

```c
text: "The Committee has granted Lord Selkirk a hundred and sixteen " +
      "thousand square miles on the Red River for a settlement."
```

`+` joins a string to anything: `"ship, " + year` gives `ship, 1816`.

A type name never has to be guessed. If you give something the wrong sort of value, the error says what it expected: *`flags.has`: name should be text, but got the number 3*.

## Constants

```c
const int hot = 50;
const string london = "the Governor and Committee, London";
const bool debug = false;
```

A constant is a name for a value known at compile time. Use it anywhere a value goes. `const` is required at the top level; inside a block you may write just `int n = 3;` (a local is a constant too).

Constants combine: `hot + 10`, `"ship, " + 1816`, `date(1816, 6, 19) + days(15)`, `days(30) * 2`.

The arithmetic operators are `+ - * / %` on numbers; `+` and `-` between a date and days, and between days; `*` of days by a number; `+` of a string with anything. A list is indexed from 0: `const string[] posts = ["Cumberland House", "Norway House"];` then `posts[1]`. And `?:` chooses between two values by a condition known now:

```c
const bool hard = true;
const int toll = hard ? 40 : 10;
```

`?:` cannot ask the world (`world.tension > 50 ? ... : ...` is an error): for that, use `if` in an effect. Comparisons between two values known now (`3 < 5`, `"a" == "b"`, `date(1812, 1, 1) < date(1813, 1, 1)`) are worked out now too.

## Variables: numbers the campaign keeps

```c
int incidents;          // starts at 0
float ratio = 0.5;      // or somewhere else
```

Variables are the one thing in a script that *changes while the game plays*. They are declared at the top of the campaign (not inside a beat or a loop), are numbers, and are saved with the game. You change them in effects:

```c
incidents++;            incidents--;
incidents += 2;         incidents -= 1;
incidents = 0;
```

and read them in conditions by comparing with a number: `incidents >= 3`.

The right-hand side must be a number known at compile time: a variable cannot be set to *another variable* or to the tension. (If you need that, raise a flag and test the flag.) `incidents = incidents + 2;` is understood as `incidents += 2;`.

A variable is not a value you can use in arithmetic or text at compile time (`incidents * 2` is an error), but its value can be put into the text of an effect when it runs: `say("{var:incidents} ambushes")`. See [State, flags and variables](State-Flags-and-Variables#putting-numbers-into-text).

## Conditions

A **condition** is a question the game asks the world, every day. It is built from things the world knows (`world.tension`, `flags.has("x")`, `brigade("...").isLate`, `SevenOaks.done` ...) and from these operators:

| | |
|---|---|
| `a && b` | both |
| `a \|\| b` | either |
| `!a` | not |
| `<  <=  >  >=  ==  !=` | compare something the world knows with a fixed number (`world.tension >= 50`), or a name (`world.season == Season.Winter`), or today's date (`world.date >= date(1816, 6, 19)`) |

Brackets group. `a && b && c` is one list of three; `a && (b && c)` keeps its grouping.

You can compare a number the world knows with a number *you know now*. You cannot compare two changing things (`world.tension > world.standing`): the game's conditions have a changing thing on one side only.

What can be compared with what:

| Left | Operators | Right | Example |
|---|---|---|---|
| a world number (`world.tension`, `world.stock(...)`, `Beat.count`, `brigade(...).daysLate` ...) or a variable | `< <= > >= == !=` | a number known now | `world.stock("York Factory", Item.Rum) < 20` |
| `world.date` | `< <= > >=` (not `==`: a day is a range) | a date | `world.date >= date(1816, 6, 19)` |
| `world.season` | `== !=` | a `Season` | `world.season != Season.Winter` |
| `world.playerFaction` | `== !=` | a `Company` | `world.playerFaction == Company.NWC` |

Either side may come first (`50 <= world.tension` is fine). For "on this exact day", use a dated beat, or `world.date >= date(...) && world.date < date(...)` one day apart.

`chance(n)` is a condition too, and a different kind: it rolls the campaign's dice every time it is asked, so in a hook that is asked daily it is a *rate* per day. See [Pitfalls](Pitfalls#chance-in-a-condition-rolls-every-day).

A condition hook is written with `=>` and a single expression:

```c
AwakenWhen() => world.tension >= hot && !flags.has("peace");
```

## Effects and statements

An **effect** changes the world. Effects live in the blocks of `OnAwaken`, `OnApproach`, `OnActivate`, `OnResolve` and `OnCancel` (and in function bodies called from them). A block is a list of statements, each ending in `;`:

```c
OnActivate() {
    flags.set("governor_killed");
    world.raiseTension(25, "Seven Oaks");
    incidents += 2;
    if (brigade("the York boats").isLate) {
        shout("The boats are overdue.", Company.HBC);
    } else if (world.tension > 60) {
        say("Things are bad.");
    }
    after(days(10), () => trigger(TheEnd));
    random(
        option(30, () => brigade("the York boats").turnBack()),
        option(70, () => say("Nothing came of it."))
    );
}
```

- `if (condition) { ... } else { ... }` asks the *world* when the effect runs (if the condition is a compile-time constant it is decided at compile time instead).
- `after(days(n), () => ...)` does something `n` days later. The `() => ...` is a block of effects; use `() => single;` or `() => { several; things; }`.
- `random(option(weight, () => ...), ...)` does one of the options, chosen by weight (and the campaign's `seed`).
- There is no `while` and no `return`: effects run once and the language has nothing to loop *over* while the game plays.
- `if` can ask the world (`if (world.tension > 60)`) or a constant (`if (debug)`); `else if` chains work. A world `if` is decided on the day the effect runs, so in an `after(...)` it is decided when the timer runs, not when it was set.
- A local (`int n = 3;` or `var who = "Montreal";`) inside a block is a constant for the rest of that block.

Using an effect where a condition goes is an error: *`flags.set` changes the world, so it belongs in an effect, not in a condition*.

## Functions

A function names a few lines you would otherwise repeat. It is **inlined**: its body is written out at each call, so it costs the game nothing.

```c
// does something: void
void fromLondon(string subject, string text, int days) {
    news(from: "the Governor and Committee, London", to: Person.Factor,
         subject: subject, text: text, days: days);
}

// works something out: a type and =>
bool openWater() => world.inMonths(5, 9);
```

```c
OnActivate() { fromLondon("peace", "Bonaparte is beaten.", 80); }
AwakenWhen() => openWater() && world.tension >= 50;
```

A function that gives back a value is written with `=>` and may return any type: `bool` (a condition), `int`, `string`, `Date`, `Days`, `Brigade`, a `Company` ... (`Brigade boats() => brigade("the York boats");` then `boats().isLate`). A `void` function may only be called where effects go.

Arguments may be given by position or by name, and parameters are typed. A function may call another; one that calls itself without end is an error. Functions can be declared at the top level (every beat sees them) or inside a beat (only that beat does), A beat's own helper must be written before the hook that uses it; top-level functions may come in any order.

## Named arguments

Wherever something has several arguments you can name them, in any order:

```c
news(from: "Montreal", to: Person.Partner, subject: "war", text: "...", days: 75, company: Company.NWC);
```

Beats and the campaign header *only* take named arguments: they are data, not calls.

## Beats

```c
beat Name(arguments) [Requires, ...] {
    members
}
```

or, for a beat with nothing but a header, `beat Name(arguments);`.

### Header arguments

| | |
|---|---|
| `id` | The beat's id, if not its name. Use it for ids with a hyphen (`id: "seven-oaks"`) and to give each beat in a loop its own (`id: "ship-" + year`). |
| `title` | What the log and the news call it. Defaults to the id. |
| `date` | The day it is due, `date(y, m, d)`. No date means "as soon as it may". |
| `window` | Grace after its date before an *event* is forced. |
| `lead` | How long before its date its `OnApproach` pressure begins (default 30 days). |
| `delay` | For a dateless beat: days after waking before it is due. |
| `repeat` | It comes round again this many days after it happened. |
| `keywords` | Words the news director listens for, `["Selkirk", "colony"]`. |
| `rumour` | One line for the player's journal while the beat is stirring: see [Player deeds and rumours](Player-Deeds-and-Rumours). |
| `kind` | `Kind.Fixed` ... only to override what is worked out (below). |

### `[Requires]`

`[SevenOaks, "some_flag"]` after the header lists what must already hold: beats that have happened, or flags. Beats are named by their *names* (you do not need to declare them first: `[Later]` may name a beat further down the file). A string names a flag, or a beat's id.

### Members

A beat's members are its hooks (`AwakenWhen`, `OnAwaken`, `OnApproach`, `ActivateWhen`, `OnActivate`, `ResolveWhen`, `OnResolve`, `CancelWhen`, `OnCancel`), plus its own `const`s and helper functions. Nothing else belongs in a beat, and the compiler says so (with a suggestion) if you mistype one.

### The kind is worked out

You almost never write `kind:`. It follows from what the beat has:

| The beat has | Kind |
|---|---|
| no `OnActivate` | `pressure`: it only presses on the world |
| `OnActivate` and no `date` | `conditional`: happens when it is awake and due |
| a `date`, plus `window`, `lead`, `OnApproach` or `ResolveWhen` | `event`: the world may do it first |
| a `date` and none of those | `fixed`: happens on its day |

What the kinds mean to the game is in [Beats and the lifecycle](Beats-and-the-Lifecycle).

## Loops

A loop at the top level writes a declaration for each value. Inside a loop a beat needs its own `id`:

```c
foreach (int year in 1818..1820) {
    beat InteriorWar(
        id: "interior-war-" + year,
        title: "The lawless years: " + year,
        date: date(year, 6, 1)
    ) [SevenOaks] {
        OnApproach() { world.raiseTension(15, "the interior war"); }
    }
}
```

`foreach (int x in 1..5)` counts; `foreach (string s in ["a", "b"])` goes over a list; `for (int i = 1; i <= 5; i++)` is the C form (`i--`, `i += 2` work too). Loops can nest, and can be used inside effect blocks to write the same effect several times:

```c
for (int i = 1; i <= 3; i++) world.addStock("York Factory", Item.Rum, i * 10);
```

A loop may write at most 5000 things: more means it is probably counting the wrong way.

Because a loop creates several beats with the same *name*, those beats cannot be referred to by name (`[InteriorWar]` is ambiguous); refer to them by id string, or give the loop beats a different structure.

## Quest lines

Besides beats, a campaign can hold **quest lines**: what it asks of each player (report to the Factor, carry the cargo, go inland). They have their own declarations, written at the top level:

```c
questline Servant(title: "The Company's Servant", forCompany: Company.HBC) {
    quest Landfall(title: "Report to the Factor") {
        talk Report(who: Person.Factor, text: "Find Mr. Auld and report to him");
        task Unload(task: Task.CarryCargo, count: 6);
        OnComplete() { player.postTo("Edmonton House"); }
    }
}
```

A `questline` holds `quest`s; a quest holds steps (`talk`, `bring`, `reach`, `have`, `task`, `hunt`, `when`, `wait`) and hooks (`StartWhen`, `FailWhen`, `OnStart`, `OnComplete`, `OnFail`); a `talk` step can hold `Says`, `Done`, `OnDone` and `choice`s. Everything else in this guide (constants, functions, conditions, effects) works inside them. A quest's name is a value like a beat's: `Landfall.done`, `Landfall.start()`. The whole of it is on the [Quests](Quests) page.

## Escape hatches

For anything the game can do that the language has no word for yet:

```c
raw("tension>=40")                         // a condition in the game's own form
action("tension", add: 5, why: "because")  // an effect, any verb and any keys
```

The checker still validates what it can. The game's own forms (every condition and action verb, with their fields) are listed in [JSON format](JSON-Format).

## Reserved words

`campaign beat questline quest choice const void int float string bool var if else for foreach in true false return while`, plus the names `world`, `flags`, `player`, and the type names in the table above. (The step kinds `talk`, `reach`, `task`, `bring`, `have`, `hunt`, `when`, `wait` are words only inside a quest.) The compiler tells you if you use one for something else.

## Grammar

```
script    = "campaign" Name "(" args ")" ";" { declaration }
declaration
          = "const" type Name "=" expr ";"
          | ("int" | "float") Name [ "=" expr ] ";"
          | ("void" | type) Name "(" params ")" ( block | "=>" expr ";" )
          | beat
          | questline
          | loop
beat      = "beat" Name "(" args ")" [ "[" expr {"," expr} "]" ] ( ";" | "{" { member } "}" )
member    = hook | "const" ... | function
hook      = HookName "(" ")" ( block | "=>" expr ";" )
questline = "questline" Name "(" args ")" "{" { quest } "}"
quest     = "quest" Name "(" args ")" [ "[" expr {"," expr} "]" ] "{" { questhook | step } "}"
step      = StepKind Name "(" args ")" ( ";" | "=>" expr ";" | "{" { stephook | choice } "}" )
choice    = "choice" Name "(" args ")" ( ";" | block )
loop      = "for" "(" "int" i "=" a ";" i op b ";" i step ")" | "foreach" "(" "int" i "in" range-or-list ")"
              ( declaration | "{" { declaration } "}" )
statement = block | "if" "(" expr ")" stmt [ "else" stmt ] | loop-statement
          | type name "=" expr ";" | target ( "=" | "+=" | "-=" ) expr ";" | target ( "++" | "--" ) ";" | call ";"
expr      = ternary: || && == != < > <= >= + - * / %  ! -   .member   call(args)   [list]   a..b   () => expr | block
```
