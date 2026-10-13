---
title: Memory and Knowledge
aliases: [Memories, Chronicle, Witnesses, Rumour Propagation]
tags: [system, memory, information, ai]
status: implemented
---

# Memory and Knowledge

**Status:** Implemented (design doc 10, 11.2). **Knowledge is your progression** (P.7): nobody knows what they were not told. Every person (and the player's journal) knows only what they saw or were told, and what they were told gets worse with each hop.

## A memory entry

One thing a person knows (`MemoryEntry`):

| Field | Meaning |
|---|---|
| Text | what happened |
| Subject / Actor | who it is about / who did it |
| Day | day of the year |
| **Directness** | 1 = saw it, 2 = heard from a witness, 3 = from someone who heard, 4 and up = too garbled to pass on |
| Weight | how much it matters (fades 30 percent per hop: `Weight x 0.7`) |
| Witnesses | everyone who was there (a shared firsthand memory is what "we went through that together" is made of) |
| Liar / Accused / CoversKey | for a made-up story, see [[Gossip, Lies and Leverage]] |

A person holds at most **24** memories; the oldest drop off. Duplicate keys are ignored.

## The Chronicle: how events become memory

`Chronicle.Event(text, where, actor, subject, weight)`:

- Everyone within **650 px** (`WitnessRadius`) becomes a witness and remembers it firsthand.
- Players within range add it to `Known` and to the **journal** unless it is a trivial chore (weight under 0.5) not involving them.
- The journal keeps the last ~60 entries (120 for Reveal lines).
- `Chronicle.Shared(household, text)` makes a whole house live through something (a blizzard, a hungry spell, the ship) - "nobody did it; everyone at home remembers it".

## Retelling and gossip

When two NPCs stand within **140 px**, every 4-9 seconds (divided by Talkative) one tells the other a random memory, **unless** it is already at Directness 4 or it is a misdeed done by one of his friends (he then repeats it only `10 percent + 15 percent x Talkative` of the time; see [[Friends and Bonds]]).

`Retell` (design doc 10.4): an eyewitness tells it straight half the time; otherwise **one** of three things may shift, never all at once:

| Distortion | Chance | Result |
|---|---|---|
| Inflate | `10 percent + 30 percent x Suspicion` | Weight x 1.6, "and worse than that, they say" |
| Garble the actor | `10 percent + 10 percent x (1 - Suspicion) + 15 percent x Talkative` | The blame moves to another person of the same people |
| Shift the date | next band of the same garble chance | +/- 4 days, "(or thereabouts)" |

## What a memory does when it lands

`Npc.Remember`:
- A **robbery, killing, or blow** with an actor sinks his regard for the actor by `6 x Weight / Directness`.
- A lie may be exposed ([[Gossip, Lies and Leverage]]).
- `Lies.Consequences`: his master marks the man down; a band thinks less of the Company.
- A friend hears bad news about a friend and plans to warn him ([[Friends and Bonds]]).

## Speaking aloud: barks

NPCs voice memories unprompted (`Barks`). Kinds: Shared, Sour, Mourning, State, Worry, Want, Reply, Story, Song, Toast, Keepsake. A bark prefers, in order: hearth talk (70 percent when at a fire), a firsthand shared memory (60 percent), a mourning line for the recently dead (60 percent), a body state (hungry, cold, hurt, drunk), a worry about the quota (50 percent, once a day, once per household every three days), and a want (25 percent, once a day). A memory raised aloud rests for **7 days** at that post. Barks tied to a memory the listener witnessed read "You saw it too, *name*", "I haven't forgotten, *name*" (a wrong done by the listener), or "We came through it".

## The player's side: Reveal

The journal records only what you saw or learned. When a consequence lands, `Reveal.Lands(player, what, cause)` states it and names the chain **as far as you could know it**:

- With a cause in your journal: "(you *struck X* on 3 March 1811)".
- With none: "(Nobody will say why.)" - which is itself a hook.
- `Reveal.Word` is the plain version for causes not in your book (a rival's move, a hull opening).
- Importance of 1.5 or more also flashes a headline.

The **Focus** panel in the corner of the screen shows three lines built from what you can know: *Now* (the most pressing thing: held captive, hurt, freezing, starving, parched, a blood debt close by, a friend's warning, an estate sale, your task, low provisions), *Aim* (your first ambition's line) and *Heard* (the most relevant of your last dozen journal lines by the [[The Director|director's]] relevance measure).

## Tips

> [!tip]
> What you saw first-hand carries the most weight. Bring the Factor sites you found yourself, tell stories at the fire, and keep a clear journal. See [[Journal, Map and UI]].

> [!info]
> A chain of three tellings is "too garbled to pass on". News from a far post is almost always bent by the time it reaches you.

## See also
[[Information and Messages]] | [[Gossip, Lies and Leverage]] | [[Friends and Bonds]] | [[The Director]] | [[Personality]] | [[The Year Told]]
