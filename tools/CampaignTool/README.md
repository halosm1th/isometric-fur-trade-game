# Campaign tool

Campaigns (the dated history the world is nudged to follow, and how the brigades on the rivers answer to it) are written in a small C-like language, checked by this tool, and compiled to the JSON the game reads. The full documentation is in [`docs/wiki`](../../docs/wiki/Home.md); this page is the five-minute version.

```c
campaign FirstBeat(title: "A first beat", description: "One letter, on one day.");

beat TheLetter(title: "A letter from London", date: date(1812, 3, 1)) {
    OnActivate() {
        news(from: "the Governor and Committee, London", to: Person.Factor,
             subject: "the spring letter", text: "The returns must answer.", days: 60);
        flags.set("letter_sent");
    }
}

beat TheBoatsAreLate(repeat: days(300)) [TheLetter] {
    AwakenWhen()   => brigade("the York boats").isLate && world.inMonths(7, 9);
    OnActivate()   { shout("The York boats are overdue!", Company.HBC); }
    ResolveWhen()  => brigade("the York boats").isIn;
}
```

## The web editor

```
dotnet run --project tools/CampaignTool -- serve
```

Open <http://localhost:8765/>: syntax highlighting, completion, live error checking, a simulator, a graph, the language reference, one click to install in the game.

## The commands

```
dotnet run --project tools/CampaignTool -- check     story.camp      # errors and warnings (-v notes, --strict)
dotnet run --project tools/CampaignTool -- compile   story.camp      # -> story.json
dotnet run --project tools/CampaignTool -- install   story.camp      # -> Campaigns/story.json (the menu lists it)
dotnet run --project tools/CampaignTool -- build                     # every src/*.camp -> Campaigns/*.json
dotnet run --project tools/CampaignTool -- decompile Campaigns/x.json -o x.camp   # write an old JSON campaign as a script
dotnet run --project tools/CampaignTool -- graph     story.camp      # mermaid; --dot for graphviz
dotnet run --project tools/CampaignTool -- sim       story.camp --scenario s.json [--runs 200] [--adherence Strict] [--log] [--state] [--json]
dotnet run --project tools/CampaignTool -- serve     [--port 8765]   # the web editor
dotnet run --project tools/CampaignTool -- docs                      # write docs/wiki/Library-Reference.md
```

## What is here

| | |
|---|---|
| `src/*.camp` | the campaigns the game ships, as scripts: `historical` (1811-21) and `company-years` (1811-30, the long one). `Campaigns/*.json` is generated from these by `build`; a test fails if they disagree |
| `examples/` | five small campaigns that each teach one thing, and the simulator's example worlds |
| `web/` | the editor (plain HTML, CSS and JavaScript; no build step) |
| `*.cs` | the compiler, checker, simulator and server: see the wiki's *Command-line tool* page |

The tool compiles the game's own `CampaignLogic.cs` and `CampaignRunner.cs` (pure C#, no engine types), so a script behaves identically in the simulator and in the game. `dotnet test tests/CampaignTests` runs the suite.
