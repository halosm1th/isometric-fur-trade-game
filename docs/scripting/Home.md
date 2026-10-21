# Fur Trade Game wiki

## Campaign scripting

A *campaign* is the story the world is nudged to follow: the dated history (the blockade, Seven Oaks, the union of the companies), the pressure the world is under as each day draws near, and how the brigades on the rivers answer to all of it. Campaigns are written in a small C-like language, checked by a tool, and compiled to the JSON the game reads.

| Read this | When you want to |
|---|---|
| [Getting started](Getting-Started) | write and run your first campaign, in ten minutes |
| [The language guide](Language-Guide) | know exactly what the syntax means: types, constants, functions, loops |
| [Beats and the lifecycle](Beats-and-the-Lifecycle) | understand when each part of a beat runs, and what adherence does |
| [Library reference](Library-Reference) | look up a name: `world`, `flags`, `brigade(...)`, `news(...)` ... |
| [Examples](Examples) | read finished campaigns, small ones first, and the long one last |
| [The web editor](Web-Editor) | edit with syntax highlighting, completion, a simulator and a graph |
| [The command-line tool](Command-Line-Tool) | check, build, install, simulate, draw, convert |
| [Simulation scenarios](Simulation-Scenarios) | play a campaign forward against an invented world |
| [Troubleshooting](Troubleshooting) | decode an error message |

The shortest way to see it work:

```
dotnet run --project tools/CampaignTool -- serve
```

and open <http://localhost:8765/>.
