# Fur Trade Game wiki

## Campaign scripting

A *campaign* is the story the world is nudged to follow: the dated history (the blockade, Seven Oaks, the union of the companies), the pressure the world is under as each day draws near, and how the brigades on the rivers answer to all of it. Campaigns are written in a small C-like language, checked by a tool, and compiled to the JSON the game reads.

### Learn

| Read this | When you want to |
|---|---|
| [Getting started](Getting-Started) | write and run your first campaign, in ten minutes |
| [The language guide](Language-Guide) | know exactly what the syntax means: types, constants, functions, loops |
| [Beats and the lifecycle](Beats-and-the-Lifecycle) | understand when each part of a beat runs, day by day, and what adherence does |
| [Cookbook](Cookbook) | copy a pattern: avertable events, forks, yearly beats, dice, reckonings, late boats, endings |
| [Examples](Examples) | read finished campaigns, small ones first, and the long one last |

### Look things up

| Read this | When you want to |
|---|---|
| [Library reference](Library-Reference) | the name and signature of every function and member: `world`, `flags`, `brigade(...)`, `news(...)` ... |
| [World reference](World-Reference) | the names a script can pass in: every post (with its year and postmaster), brigade run and leg, item, person and letter address |
| [State, flags and variables](State-Flags-and-Variables) | what the campaign remembers, the flags the game raises by itself (`late:`, `heard:`, `open_hostility` ...), history before the start, saves |
| [Effects in the game](Effects-in-the-Game) | what each effect really does, with numbers: tension, London, stock, brigades, letters |
| [Player deeds and rumours](Player-Deeds-and-Rumours) | let the player change the story, and let them hear it coming |
| [JSON format](JSON-Format) | the compiled form the game reads, every condition and action verb |

### Get it right

| Read this | When you want to |
|---|---|
| [Pitfalls](Pitfalls) | avoid what compiles but does not do what you meant |
| [Simulation scenarios](Simulation-Scenarios) | play a campaign forward against an invented world, and a routine for testing one |
| [Troubleshooting](Troubleshooting) | decode an error message |
| [The web editor](Web-Editor) | edit with syntax highlighting, completion, a simulator and a graph |
| [The command-line tool](Command-Line-Tool) | check, build, install, simulate, draw, convert |

The shortest way to see it work:

```
dotnet run --project tools/CampaignTool -- serve
```

and open <http://localhost:8765/>.
