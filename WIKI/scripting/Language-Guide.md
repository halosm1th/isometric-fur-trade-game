# The language guide

A campaign script is a text file ending in `.camp`. It looks like C# or Java: braces, semicolons, `//` comments, typed declarations. This page is what every piece of it means. The names you can call (`world`, `flags`, `news(...)`) are in the [Library reference](Library-Reference); *when* each part of a beat runs is in [Beats and the lifecycle](Beats-and-the-Lifecycle).

## The one idea to hold on to: two times

A script is run twice, at two different times.

1. **When it is compiled**, by the tool. Constants, loops, helper functions and `if` on a constant are all worked out *then*, and are gone by the time the game runs.
2. **When the game plays it.** What is left is a list of beats, each with conditions the game asks of the world and effects it does to the world. The game never runs your loops or your functions: it reads the result.

That is why a condition like `world.tension >= 50` is *kept* (the game asks it every day) while `hot + 10` is *folded* to `60` before the game ever sees it, and why a `for` loop is a way of **writing** beats, not something that happens in the world.

The editor's JSON tab shows exactly what the game receives.

## Layout of a file

```text
campaign Name(title: "...", description: "...");     // 1. the header, once

int counter;                                          // 2. declarations, in any order:
const int hot = 50;                                   //    variables, constants,
void helper(string s) { say(s); }                     //    functions,
beat Something(...) { ... }                           //    beats,
foreach (int y in 1818..1820) { beat Each(...); }     //    and loops that write them
```

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
| `Season` | the river's season | `Season.Breakup`, `OpenWater`, `FreezeUp`, `Winter` |
| `Kind` | a beat's kind | `Kind.Fixed`, `Conditional`, `Event`, `Pressure` |
| `Beat` | a beat | its name, `SevenOaks` |
| `Brigade` | a run on the rivers | `brigade("the York boats")` |
| `string[]`, `int[]` | a list | `["a", "b"]`, `[1, 2, 3]` |

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

The right-hand side must be a number known at compile time: a variable cannot be set to *another variable* or to the tension. (If you need that, raise a flag and test the flag.)

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

## Escape hatches

For anything the game can do that the language has no word for yet:

```c
raw("tension>=40")                         // a condition in the game's own form
action("tension", add: 5, why: "because")  // an effect, any verb and any keys
```

The checker still validates what it can.

## Reserved words

`campaign beat const void int float string bool if else for foreach in true false`, plus the names `world`, `flags`, and the type names in the table above. The compiler tells you if you use one for something else.

## Grammar

```
script    = "campaign" Name "(" args ")" ";" { declaration }
declaration
          = "const" type Name "=" expr ";"
          | ("int" | "float") Name [ "=" expr ] ";"
          | ("void" | type) Name "(" params ")" ( block | "=>" expr ";" )
          | beat
          | loop
beat      = "beat" Name "(" args ")" [ "[" expr {"," expr} "]" ] ( ";" | "{" { member } "}" )
member    = hook | "const" ... | function
hook      = HookName "(" ")" ( block | "=>" expr ";" )
loop      = "for" "(" "int" i "=" a ";" i op b ";" i step ")" | "foreach" "(" "int" i "in" range-or-list ")"
              ( declaration | "{" { declaration } "}" )
statement = block | "if" "(" expr ")" stmt [ "else" stmt ] | loop-statement
          | type name "=" expr ";" | target ( "=" | "+=" | "-=" ) expr ";" | target ( "++" | "--" ) ";" | call ";"
expr      = ternary: || && == != < > <= >= + - * / %  ! -   .member   call(args)   [list]   a..b   () => expr | block
```
