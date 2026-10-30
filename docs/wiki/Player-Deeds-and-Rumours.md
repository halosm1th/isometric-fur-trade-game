# Player deeds and rumours

Two ways a campaign talks to the player, and the player talks back.

## Deeds: the player's part in the story

The game records a **deed** when the player does something the story should know of. A campaign asks about deeds with the `player` object:

```c
AwakenWhen() => player.did("truce") && world.tension > 15;
AwakenWhen() => player.deeds("escort") >= 2;
```

| Deed | The game records it when the player |
|---|---|
| `"truce"` | brokers a truce between the companies (talking to a factor or a partner) |
| `"raid"` | agrees to go out on a raid against the other company's post |
| `"escort"` | rides a brigade all the way to its landing (not if it was turned back) |
| `"courier"` | carries a packet through, and the answer is read |
| `"desert"` | deserts their side: only when a campaign says so, with [`player.desert`](Effects-in-the-Game#deserters) |

`player.did(kind)` is true once the player has done it at least once; `player.deeds(kind)` is how many times. Under the hood a deed raises the flag `deed:<kind>` and adds one to the variable of the same name, so both are saved with the game and `raw("...")` and `flags.has("deed:truce")` see them too.

The game also answers some deeds itself, whatever the campaign: a truce brings the tension down by 35 at once, and the raid itself, when it reaches the stores, raises it by 6 to 12 (see [Effects in the game](Effects-in-the-Game#tension)). In the [simulator](Simulation-Scenarios) a deed changes nothing but the deed itself.

A deed is how the world answers the player. The long campaigns use them like this:

- **Seven Oaks can be averted.** In `company-years`, a quiet spring *or* a brokered truce cancels the beat. In `the-long-peace` a truce turns the Pemmican Proclamation into a *bargain*, and the war beats never wake.
- **Reputation beats.** `TrucesAreRemembered`, `TheRaidIsTalkedOf` and `TheBoatsHaveASteadyHand` fire only for a player who has done those things, and move the tension a little.

In the [simulator](Simulation-Scenarios) a scenario stands in for the player with an event: `{ "on": "1813-05-01", "deed": "truce" }`.

## Rumours: what the player is told is coming

Each beat may have a `rumour`: one line, written as something a person at the post could have heard.

```c
beat SevenOaks(
    date: date(1816, 6, 19),
    window: days(45),
    rumour: "Grant's riders are gathering on the Assiniboine, and the colony's governor is on the road out from Fort Douglas."
) { ... }
```

The player's journal (the **J** key) opens with a **What is stirring** list. A beat's rumour is listed while the beat is *awake and unfinished*, and, for a dated beat, only once its day is within its `lead` (so a rumour about 1816 does not appear in 1811). Each is tagged with how near it is: *any day now*, *within a fortnight*, *within weeks*, *in the coming months*, *before long*. When the beat happens, is resolved by the world or is cancelled, its rumour disappears.

Below it, **What you have done** lists the player's deeds.

Things to know when you write rumours:

- **Only the rumour is shown, never the beat's title or anything it will do.** A rumour is the only thing the player learns before the beat happens, so it is where you decide how much to give away: *"Grant's riders are gathering"* hints; *"Seven Oaks will happen on 19 June"* spoils it.
- **A beat with no rumour is silent.** That is the normal case: keep rumours for what you want the player to be able to feel coming, and to *act on*.
- **A rumour that depends on the world is a promise.** `PemmicanBargain` has the rumour *"The colony's governor and the partners at Fort Gibraltar are said to be talking terms"* and wakes only when the peace holds. If you cancel a beat, its rumour goes too, so the player is never left waiting for something that is not coming.
- A rumour on a beat that can never happen stays in the journal for good: an event with no `ResolveWhen` under Steady adherence is the usual case. See [Pitfalls](Pitfalls#rumours-are-promises).
- The list is ordered by how near each beat is, soonest first.
- In multiplayer the host builds the list each day and sends it to the others.

## Where each lives

| | |
|---|---|
| `rumour:` | a beat header argument (a string) |
| `player.did(...)`, `player.deeds(...)` | conditions: see the [Library reference](Library-Reference#player) |
| the journal text | built by `Campaign.BuildJournalText` from `CampaignRunner.Stirring()` and the deed counters |
| deeds recorded | `Conversation.cs` (truce, raid), `Convoys.cs` (escort), `Message.cs` (courier) |

Adding a deed is two lines: call `Campaign.Deed("name")` where the player does it, and add a line to `DeedWords` in `Campaign.cs` so the journal can say it.
