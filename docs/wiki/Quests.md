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

## In the game

- The current step is on the HUD's line of what presses, and every quest, under way and done, is at the top of the journal.
- Quest business comes first in a conversation: `[Quest title] Speak of it`, `[Quest title] Give ...`, and `[New] Quest title` for an offer.
- Quests are saved with the game, per player; in multiplayer each player has their own.
