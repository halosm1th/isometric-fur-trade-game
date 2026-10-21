# Getting started

This page takes you from nothing to a campaign running in the game. You need the .NET 8 SDK; the tool is part of the game's repository and runs from inside it.

## 1. Open the editor

```
dotnet run --project tools/CampaignTool -- serve
```

Then open <http://localhost:8765/>. The left side lists the campaigns in `tools/CampaignTool/src`, the examples, and the campaigns that were written as JSON. Press **New** for an empty script, or open `01-first-beat.camp`.

(You can use any text editor instead and the command line: see [the command-line tool](Command-Line-Tool). The editor is the same compiler with a window on it.)

## 2. A campaign is a header and some beats

```c
campaign FirstBeat(
    title: "A first beat",
    description: "One letter, on one day."
);

beat TheLetter(
    title: "A letter from London",
    date: date(1812, 3, 1)
) {
    OnActivate() {
        news(
            from: "the Governor and Committee, London",
            to: Person.Factor,
            subject: "the spring letter",
            text: "The Committee expects the returns to answer.",
            days: 60);
        flags.set("letter_sent");
    }
}
```

The **header** gives the campaign its name in the menu. Everything after it is a **beat**: something that may happen to the world. This one is *fixed* to a date, so on 1 March 1812 it happens: a letter is sent to the Factor, due in sixty days, and a flag is raised.

A beat is declared as `beat Name(arguments) { members }`. The arguments are the beat's *data* (what it is called, when it is due). The members are its *behaviour*: the hooks, named for when they run.

## 3. Make it react

```c
beat TheAnswer(title: "The Factor writes back") [TheLetter] {
    OnActivate() {
        say("The Factor has written his answer.", Company.HBC);
    }
}
```

The `[TheLetter]` after the header says *this beat waits for that one*: it cannot happen before TheLetter has. It has no date, so it happens as soon as it may.

Now let it look at the world:

```c
beat TheBoatsAreLate(repeat: days(300)) {
    AwakenWhen() => brigade("the York boats").isLate && world.inMonths(7, 9);
    OnActivate() {
        shout("The York boats are overdue!", Company.HBC);
        world.raiseTension(3, "the boats are overdue");
    }
}
```

`AwakenWhen` is a *condition*: the beat does nothing until it holds. `repeat: days(300)` lets it happen again the next year. The other hooks (`OnAwaken`, `OnApproach`, `ActivateWhen`, `ResolveWhen`, `CancelWhen` ...) are explained in [Beats and the lifecycle](Beats-and-the-Lifecycle).

## 4. Check it

The editor checks as you type and underlines what is wrong. Click a problem to jump to it. On the command line:

```
dotnet run --project tools/CampaignTool -- check mycampaign.camp
```

The checker also looks for things that compile but will not do what you meant: a flag nothing ever raises, a beat that can never happen, two beats that wait for each other.

## 5. Play it forward without starting the game

In the editor, open the **Simulate** tab, choose a world to play against and press *Play it forward*. You get every beat that happened and when, what they did, and the beats that never happened. See [Simulation scenarios](Simulation-Scenarios) for how to describe a world.

## 6. Put it in the game

Press **Install in game** (or run `... -- install mycampaign.camp`). That writes `Campaigns/mycampaign.json`, and the game's campaign menu lists it. Campaigns you keep in the repository live in `tools/CampaignTool/src`, and `build` regenerates all their JSON; a test fails if the two disagree, so you cannot forget.

## Where next

- [The language guide](Language-Guide) for everything the syntax can do.
- [Examples](Examples): five small campaigns that each teach one thing, and one long one.
- [Library reference](Library-Reference) when you need the name of something.
