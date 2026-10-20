# The web editor

```
dotnet run --project tools/CampaignTool -- serve
```

opens an editor at <http://localhost:8765/>. It runs on your machine (nothing is sent anywhere), uses the same compiler, checker and simulator as the command-line tool, and edits the real files in the repository.

![The editor with a long campaign open](images/editor-light.png)

## The screen

- **Left:** the files. *Parts (included)* are the pieces in `tools/CampaignTool/src/parts` that a campaign's header includes (`include: "american-road"`), tagged with the campaign that includes them: opened, a part is checked, simulated and drawn on the map inside that campaign, *Install in game* installs that campaign, and clicking a problem or a pin that is in the other file opens it at the line. *Campaigns* are the scripts in `tools/CampaignTool/src` (with a green *installed* tag when `Campaigns/<name>.json` exists), *Examples* are the small teaching scripts, and *Shipped as JSON* are campaigns that were written as JSON and have no script yet. Opening one of those *converts* it: you see the same campaign written in the language, and saving writes a new `src/<name>.camp`.
- **Middle:** the script, with line numbers. Errors are underlined in red; the line number goes red too.
- **Right:** six tabs: **Problems**, **JSON**, **Simulate**, **Graph**, **Map**, **Reference**.

The three panes can be **resized**: drag the thin bar between the file list and the script, or between the script and the tabs. Double-click a bar to put it back; with a bar focused (Tab to it), the left and right arrow keys move it. The widths are remembered for next time. (On a narrow window the panes stack instead.)

## Highlighting

Colours say what a word *is*, so a misspelling shows up as the wrong colour before the checker says a word:

| Colour | What |
|---|---|
| purple, bold | keywords: `campaign beat const void if else for foreach` |
| teal | types and the named-value families: `int string Company Person Item` |
| gold, bold | the hooks: `OnActivate`, `ResolveWhen` ... |
| blue | library functions: `days`, `date`, `news`, `say`, `brigade` |
| green | *your* functions |
| orange | `world` and `flags` |
| light blue | members after a dot: `.tension`, `.isLate` |
| pink | named values: `HBC` in `Company.HBC` |
| yellow-brown | beat names (everywhere they are used) |
| grey italic | comments |

## As you type

- **Checking.** Half a second after you stop typing the whole script is compiled and checked. The status at the top says *56 beats, clean* or *1 error*. The underlined span is exactly what the compiler objected to; hover over it for the message, or read it in **Problems**.
- **Completion.** Type the start of a name, or press **Ctrl+Space**. After `world.` you get `world`'s members; after `Company.` its values; after `brigade("...").` the brigade's; in a beat's braces, the hooks. **Tab** or **Enter** accepts; **Esc** closes. A line below the list says what the highlighted item is.
- **Hover** over any library word to see its signature, what it does, what each of its arguments is, and an example. Hover over a **named argument** in a call (`to:` in `say("...", to: Company.HBC)`) to see what that argument is; over a type (`Company`, `Days`) for what it holds; over a header argument (`window:`, `start:`, `giver:`) for what it means; over a quest hook or step hook (`StartWhen`, `Says`) for when it runs.
- **Enter** keeps the indent (and adds one after `{`); **Tab** inserts four spaces.

![An error underlined in the editor](images/editor-error.png)

## The tabs

**Problems** lists the compile error (if any), the checker's errors and warnings, and, if you tick *show notes*, the gentler notes (a flag that nothing reads). Click one to jump to its line.

**JSON** is what the game will read. *Copy* puts it on the clipboard.

**Simulate** plays the script forward. Pick a world (a scenario from `examples/`; see [Simulation scenarios](Simulation-Scenarios)), an adherence and a seed, press *Play it forward*, and read what happened, when, and how (*on its day*, *by the world*, *forced*...), the beats that never happened, what they did, and the variables at the end. Click a row to jump to the beat.

![The simulator](images/editor-simulate.png)

**Graph** draws the beats left to right, with a line for every *requires* (solid) and every *trigger* (dashed), coloured by kind. Click a beat to jump to it.

![The beat graph](images/editor-graph.png)

**Map** draws the world the campaign plays in: the ground, every post coloured by company (HBC red, NWC blue, American green; a hollow square for a post given up), the band camps (and, if you tick *family camps*, the families camped by the posts), and the cast placed in the game's scene. On top go the campaign's own places: a numbered pin for each quest step's place or person (`reach Souris(place: "Fort Souris")`, `talk Report(who: "leader@Fort Souris")`, `Person.Factor`), with the quest's steps joined in order so you can see the road it sends the player down, a *G* for a quest's giver, and a small diamond for the places beats and quest effects act on (`posting`, `regard`, `stock`, `war_party`...). Pick one quest in the list to see only its road; *Fit* frames whatever is shown.

- **Hover** a place for what it is and who lives there (the leader starred); hover a pin for the step.
- **Click** a pin to jump to its step in the script; click a place to list, below the map, everything the campaign does there and everyone placed there.
- **Find a place** centres the map on it by name - a quick way to check the spelling of a camp before you write it.
- A name the game would not find (a misspelt camp, a post that does not exist, a person nobody of that name is placed as) is listed above the map, with the line it is on, the same way the checker reports it.
- Drag to pan, the wheel (or + and -, with the map focused) to zoom, the arrow keys to move. It follows the script: the pins are redrawn each time it is checked.

**Reference** is the whole language, searchable: the campaign and beat headers, the beat hooks, the quest lines' arguments and hooks, every function and member (`world`, `flags`, `player`, beats, quests, brigades), the named values and the types. Each function lists its **arguments with what each one is**, and in its signature every argument name and type can be hovered for the same. An *Insert* button puts the example into the script at the cursor. It is written from the same table the compiler runs, so it cannot be out of date.

**Open in its own tab ↗** (at the top of the Reference) opens the reference as a page of its own, at <http://localhost:8765/?view=reference>, to keep beside the editor or on another screen. It searches the same way; its buttons copy an example to the clipboard instead of inserting it.

![The reference, in the dark theme](images/editor-reference-dark.png)

## Saving and installing

- **Save** (or **Ctrl+S**) writes the file. A new script asks for a name and is saved in `tools/CampaignTool/src`.
- **Install in game** saves, then compiles into `Campaigns/<name>.json`, where the game's menu finds it. It refuses if there are errors.

The theme follows your system's light or dark setting.

## Keys

| | |
|---|---|
| Ctrl+S | save |
| Ctrl+Enter | check now |
| Ctrl+Space | completion |
| Tab / Enter / Esc | accept / accept / dismiss a completion |

## What the editor will not do

It does not reformat your script, because the compiler throws comments away and rewriting your file would lose yours. (Opening a campaign that was written as JSON is the one place text is generated for you: the *Decompiler* writes it, and it compiles back to exactly the JSON you opened.)
