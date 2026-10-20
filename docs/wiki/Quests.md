# Quests

Beats happen to the **world**. Quests are what a campaign asks of a **player**: report to the Factor, carry the cargo up from the ship, go inland, speak to the Nor'Westers, bring the chief tobacco. A campaign file can hold both, and they can answer each other: a beat can wait for a quest (`Landfall.done`), and a quest can wait for a beat or a flag.

## The shape

```c
campaign CompanysServant(title: "The Company's Servant", extends: "historical");

questline Servant(title: "The Company's Servant", forCompany: Company.HBC) {

    quest Landfall(title: "Report to the Factor", summary: "You are the Company's new hand.") {
        talk Report(who: Person.Factor, text: "Find Mr. Auld and report to him") {
            Says() => "Mr. Auld looks you over. \"Can you carry?\"";
            choice Keen(label: "\"Put me to work, sir.\"", reply: "\"Good. The ship.\"") { player.addStanding(2); }
            choice Sullen(label: "\"Is there no supper first?\"", reply: "\"There is not.\"") { player.addStanding(-2); }
        }
        task Unload(task: Task.CarryCargo, count: 6, text: "Carry six lots from the ship to the warehouse");
        talk Orders(who: Person.Factor, text: "Report back to Mr. Auld") {
            Says() => "\"You'll do. Edmonton wants hands.\"";
        }
        OnComplete() { player.postTo("Edmonton House"); }
    }

    quest Inland(title: "Up the rivers") [Landfall] {
        task Boats(task: Task.Brigade, convoy: "the York boats", text: "Go up with the York boats");
        reach Edmonton(place: "Edmonton House", text: "Go on to Edmonton House");
    }
}
```

- A **questline** is a story: its quests in the journal under one title. `forCompany` gives its quests only to players of that company.
- A **quest** is a chain of **steps**, done one after another. It starts by itself once everything in its `[...]` is done and its `StartWhen` holds, or, if it has a `giver`, it is **offered** when the player speaks to that person (the player can take it on, put it off, or refuse).
- Each **step** is one thing to do. When it is done the quest moves to the next step written, or to its `next:`; after the last step the quest is done.

## Kinds of step

| Step | Done when | Needs |
|---|---|---|
| `talk` | the player speaks to the person (and picks a choice, if there are any) | `who` |
| `bring` | the player hands the person the things (they leave the player's pack) | `who`, `item`, `count` |
| `reach` | the player is within `radius` (default 400 px) of the place | `place` |
| `have` | the player's pack holds the things | `item`, `count` |
| `task` | the player has done that much of the work, as for a Factor's task (counted whatever the Factor said that day) | `task`, `count`; `convoy` for `Task.Brigade` |
| `hunt` | the player has killed that many of the species | `species`, `count` |
| `when` | the condition holds: `when Name(text: "...") => condition;` | a condition |
| `wait` | that many days have passed since the step began | `days` |

**Who** is a person's exact name, one of `Person.Factor`, `Person.Partner`, `Person.Chief`, `Person.Postmaster` (the start's own people), or a **role at a place**: `"leader@Edmonton House"`, `"partner@Fort Augustus"`, `"chief@Beaver Hills Cree (Amiskwacîwiyiniwak)"`, `"clerk@York Factory"`, `"hunter@<camp>"`, `"man@<post>"`, `"woman@<post>"`. A role at a place keeps working when the person in it dies and someone else takes their place.

### Who, exactly

| `who:` | Means | Example |
|---|---|---|
| `Person.Factor` | the leader of York Factory (Mr. William Auld at the start, or his successor) | `who: Person.Factor` |
| `Person.Partner` | the leader of the Nor'Westers' house at York (Mr. Duncan Cameron) | |
| `Person.Chief` | the leader of the York Cree band (Maskwa) | |
| `Person.Postmaster` | the postmaster at Oxford House | |
| `"role@<place>"` | whoever holds that role at a post (by its exact name) or a camp (by its band's name) | `"leader@Edmonton House"` |
| any other text | the living person with exactly that name | `"Mr. George Leith"` |

The roles: `leader`, `master` and `factor` (all the head of the household), `chief` (the head, or any chief there), `partner` (the head, or an NWC partner there), `clerk`, `hunter`, `man` (a labourer, voyageur or hunter), `woman`, and `anyone`. The places are the posts that keep books (see the [World reference](World-Reference#posts)) and the band camps ([World reference](World-Reference#camps-bands)). The checker errors on a place that is neither, warns on a role it does not know, and notes every plain name (it works only while someone of that name is alive). A role at a place is the safer choice: it keeps working when the person dies and someone else takes the place.

### Tasks

A `task` step hands the player one of the Factor's own tasks and counts the work, whoever asked for it that day. `Task.` takes:

| `Task.` | The work | `count` counts |
|---|---|---|
| `CarryCargo` | carrying lots from the ship to the warehouse | lots carried |
| `Firewood` | cutting and bringing wood | loads |
| `Geese` | the goose hunt | geese brought in |
| `DriveOffAnimals` | driving wolves and the like away from the post | beasts driven off |
| `DeliverLetter` | carrying a letter to its addressee | letters delivered |
| `Courier` | carrying the packet between posts | packets |
| `Report` | reporting to the master | reports |
| `Raid` | going out against the other company's stores | raids |
| `Brigade` | going with a brigade to its landing; needs `convoy:` (a [brigade run](World-Reference#brigade-runs)) and takes an optional `dest:` (a post; default where the boats are bound) | |

### Hunting

A `hunt` step counts the beasts the player kills of one `species:`, written as text: `"Caribou"`, `"Goose"`, `"Beaver"`, `"Wolf"`, `"Hare"`, `"Ptarmigan"` or `"Buffalo"` (any species if left out).

## Talk, choices and branches

A `talk` step can carry what the person says (`Says`) and **choices**. Each choice has a `label` (what the player says), an optional `reply`, effects in its block, and an optional `goto`: another step of the same quest, `"complete"` or `"fail"`. A choice with `when:` is offered only while that holds. When the next step is more talk with the same person, the conversation runs straight on into it.

```c
talk Meet(who: "partner@Fort Augustus", text: "Call on the Nor'Westers") {
    Says() => "\"So the Company has sent another clerk to count our canoes.\"";
    choice Friendly(label: "Share a pipe and the news from the Bay", goto: Friends) { player.addRapport("partner@Fort Augustus", 15); flags.set("nwc_friendly"); }
    choice Cold(label: "Tell him to keep his men off our side of the river", goto: Rivals) { player.addRapport("partner@Fort Augustus", -15); flags.set("nwc_hostile"); }
}
talk Friends(who: "partner@Fort Augustus", next: "complete") { Says() => "..."; }
talk Rivals(who: "leader@Edmonton House") { Says() => "..."; }
```

In what is spoken (a quest's `summary` when its giver offers it, `Says`, a choice's `label` and `reply`), `{player}` becomes the player's name and `{npc}` the name of the person speaking: `Says() => "So you are {player}. I am {npc}, and this is my post.";`. (This is different from `{var:x}`, which fills in numbers in the text of effects: see [State, flags and variables](State-Flags-and-Variables#putting-numbers-into-text).)

Branches between quests are made the same way a beat would make them: a choice sets a flag, and the next quests' `StartWhen` reads it.

## Hooks

| Quest hook | |
|---|---|
| `StartWhen() => cond;` | the quest may start only while this holds |
| `FailWhen() => cond;` | the quest fails the first time this holds |
| `OnStart() { }`, `OnComplete() { }`, `OnFail() { }` | effects at those moments |

| Step hook | |
|---|---|
| `Says() => "...";` | what the person says (talk, bring) |
| `Done() => cond;` | the step's condition (the long form of `when Name(...) => cond;`) |
| `OnDone() { }` | effects when the step is done, before the quest moves on |

## What quests can ask and do

Asked of the player the quest runs for (from a beat, of any player): `Q.done`, `Q.active`, `Q.failed`, `player.posted("Edmonton House")`, `player.rapport("leader@Fort Augustus")`, `world.regard("<camp>")`, and everything a beat can ask.

Done to that player (from a beat, to every player): `player.postTo(place)`, `player.addStanding(n)`, `player.addRapport(who, n)`, `player.give(item, n)`, `player.take(item, n)`, `player.note(text)`, `world.addRegard(camp, n)`, `Q.start()`, `Q.complete()`, `Q.fail()`, and every effect a beat has. See the [Library reference](Library-Reference).

## Building on another campaign

`extends: "historical"` in the header plays that campaign's beats as well as this one's (its war, its ships, its posts by founding year), with this file's beats winning where the ids meet. A story campaign is quest lines on top of the history; a free-roam campaign is the history with no quest lines.

What is taken from the campaign extended: its **beats** (put before this file's own), its variables' starting values (this file's win), `postsByFoundingYear` if either has it, and its `start` or `startYear` if this file gives neither. Its quest lines are **not** taken, nor its seed. Only one level is followed (a campaign that extends one that extends another gets only the middle one's beats). The checker and the simulator look at this file alone, so a beat here cannot require a beat of the base by name: require its flag (`["seven-oaks"]`), which the checker accepts only if this file sets it; otherwise wake the beat with `AwakenWhen() => flags.has("seven-oaks");`.

The shipped examples: `company-servant.camp` (*The Company's Servant*, the HBC story's first chapter: the ship, the Factor, up the rivers to Edmonton House, the Nor'Westers and the Beaver Hills Cree) and `open-country.camp` (*The Open Country*, free roam: the history and no quest lines).

## How quests run

- **Every few moments**, for every player, the game starts each quest with no `giver` that the player may now have: not already started, for their company (`forCompany`), everything in its `[...]` done *by that player*, and its `StartWhen` true. Quests are per player: in multiplayer, each player has their own state of every quest.
- **A quest with a `giver`** is offered when the player speaks to that person and it may start. *Take it on* starts it. *Not now* leaves it to be offered again. *Refuse* turns it down for good (it is never offered again, though an action can still start it), and the giver thinks a little less of the player (rapport -2).
- **When a quest starts**, its line and title go in the player's journal with its summary, the player is told "New: ...", `OnStart` runs, and the first step is announced.
- **Steps are worked one at a time.** `FailWhen` is asked before the step every time, so a quest can fail in the middle of any step. When a step is done its `OnDone` runs, then the quest moves to its `next:` (or the chosen choice's `goto:`), otherwise the step written after it. Going past the last step, or to `"complete"`, completes the quest; `"fail"` fails it.
- **A `bring` step** takes the things from the player's pack and gives them to the person, who thinks better of the player for it (rapport +4). If the player does not have them all, nothing is taken.
- **A `talk` step without choices** is done as soon as the player raises it; with choices, when one is picked. Only choices whose `when` holds are offered: if none does, the step waits (the checker warns of this).
- **A `wait` step** counts days from the day the step began; **a `reach` step** is done within `radius` pixels (default 400) of the place's door; **a `have` step** is done the moment the pack holds enough, and takes nothing.
- **Finishing.** `OnComplete` or `OnFail` runs, and the journal says "done" or "failed". A quest that has finished never starts again for that player.

A step without `text:` is described for the player from what it is: "Speak to Mr. Auld", "Go to Edmonton House", "Bring 2 Tobacco to Maskwa (chief)", "Kill 3 Buffalo", with a count for tasks and hunts ("(2/6)").

## Quests and beats together

A quest's conditions and effects are the language's own, run *for one player*: `player.addStanding(2)` in a choice is that player's standing, `player.posted(...)` asks where that player is posted. The same words in a **beat** (which belongs to the whole world) mean something else:

| In a beat | Means |
|---|---|
| effects on the player (`player.give`, `postTo`, `addStanding`, `addRapport`, `take`, `note`, `Q.start()` ...) | done to **every** player: a beat's `player.give(Item.Rum)` gives everyone a dram |
| `Q.done`, `Q.active`, `Q.failed` | true if it is so for **any** player |
| `player.posted(...)`, `player.rapport(...)`, `world.standing` | asked of the **host's own** player (on a dedicated server: nobody, so false or 0) |

So:

- Let a beat react to the story with `Q.done`, `Q.active` or `Q.failed`, or with a flag a quest sets (`flags.set(...)` in a choice; flags belong to the world and are shared).
- Let a quest react to history with `[...]`-free conditions: `StartWhen() => SevenOaks.done;` or `flags.has("governor_killed")`.
- A quest can start another with `Q.start()`, whatever it requires; a beat doing so starts it for every player.


## Testing quests

The [simulator](Simulation-Scenarios) plays the beats, not the quests: it has no players, so every quest condition it is asked (`Q.done`, `player.posted`, `player.rapport`, `world.regard`) is false, and quest effects are only logged. Test quest lines in the game:

- `check` lists the quests and catches most mistakes: places and people that do not exist, items and tasks that do not, a `goto` to no step, quests that require each other in a circle, a quest named that the campaign does not have.
- Start the game with `-- --quest=<quest>` to start that quest at once, whatever it requires, or `--quest=<quest>:<step>` to put it at a step, so a long chain can be tried from the middle. `--talkto=@quest` puts you in conversation with the person the current step is about.

## In the game

- The current step is on the HUD's line of what presses, and every quest, under way and done, is at the top of the journal.
- Quest business comes first in a conversation: `[Quest title] Speak of it`, `[Quest title] Give ...`, and `[New] Quest title` for an offer.
- Quests are saved with the game, per player; in multiplayer each player has their own.
