---
title: Gossip, Lies and Leverage
aliases: [Lies, Leverage, Blackmail, Rumours, Telling Lies]
tags: [system, lies, leverage, social, information]
status: implemented
---

# Gossip, Lies and Leverage

**Status:** Implemented (design doc P.10 and P.8.1). Three tools of one idea: **what people believe about each other is the playfield.**

- **Gossip** spreads true things ([[Memory and Knowledge]]).
- **Lies** are what you make up.
- **Leverage** is what you saw.

## Lies

A lie is an **ordinary memory with a liar's name on it**. It travels by the normal rumour rules and does to its subject what the true thing would have done, **until it reaches someone who knows better**. Lies are rich in rules, below.

### The deeds a lie can claim

Misdeeds the world reacts to (`Lies.Deeds`): `robbed`, `struck`, `killed`, `on his own account`, `talking with their master`, `telling lies about`, `broke into`, `stole`.

### How you tell one

**Talk... > "Tell them something..."** (needs 0.3 fluency with a band person) or **"Make something up..."** at a hearth to tell the whole fire. Pick whose name goes in the story (up to six: clerks of your own post score 3, the other company's people 2, the listener's own house 1) and what he did:

- ...robbed the traps at *site*
- ...struck *a kinsman of the listener*
- ...has been trading furs on his own account
- ...was at the other company's house after dark, talking with their master
- Say it was **him, not you** (a cover story that takes the place of the truth if he believed the true one only by hearsay; +5 regard)

A lie told is a `Weight 1.6` memory, dated 0.4 to 2 days back ("the other night") and vouched for only by you. A listener who believes it gains +1 regard for you; a listener who knows better will say so.

### Who sees through it (`SeesThrough`)

| Who | Line |
|---|---|
| The accused | "I never did any such thing." |
| Anyone named in the story | "I'd know - I'm in the story, and it never happened." |
| Whoever saw the real deed | "I saw who did it, and it wasn't him." |
| Anyone who was with the accused within about 3 hours of that day | "*X* was with me that day." (50 percent) |
| A friend of the accused ([[Friends and Bonds]]) | "Not *X*. I'd stake my life on it." (50 percent) |
| A suspicious man (above 0.7) with negative regard for the liar | Disbelieves quietly (40 percent), no scene |

When one speaks up, the liar's regard drops by 12 and the Chronicle records "*liar* has been telling lies about *accused*" (weight 1.8), which spreads like anything else. If you are the liar you get a journal line.

### When others hear it

`Consequences`: each house reacts once. A **master** who hears his man did it marks him down `3 x Weight / Directness`; a **band** that hears a Company man robbed, struck or killed loses 3 x weight of standing for that company. Hearing that someone "has been telling lies about" *him* costs the listener's regard for that teller 20.

### Denying a story about you

**Talk > What are they saying about me?** lists up to three misdeed stories he holds about you (not ones the teller started). To deny:

- If he **saw it himself**: hopeless, -6 regard.
- If it is a **real lie**: believed if `his regard for you + 10 >= his regard for the liar`.
- If it is **true**, the denial is a lie of its own: believed with chance `clamp(0.2 + regard/100, 0.05, 0.8)`.
- Believed: the memory is forgotten; if he is a leader and it was about you, +2 standing. If you can **name the liar** (you learned who started it by overhearing or from a friend), his regard for the liar falls 10 and he holds a new, true memory of the lie.

### NPCs lie too (`Lies.Consider`)

Once a day at most, a man with Loyalty below 0.5 who is not band-born and has someone within 200 px may lie to them, with the first reason that fits:

1. **Cover**: his own fresh misdeed (under 10 days old), laid on whoever of the witnesses he likes least.
2. **Spite**: someone at -25 regard or lower, to anyone who will listen.
3. **Career**: an ambitious clerk, labourer or voyageur pulling down a rival of his rank in the house, to their master (chance `bent x Ambition x 0.5`).
4. **Rivalry**: the other company's men, to a band, when [[Rivalry]] tension is 50+.

A player nearby overhears the whisper and learns who started it. Each liar starts a story about the same man at most once every 5 days.

## Leverage

**Take him aside: "I saw what you did..."** appears when you **witnessed a misdeed first-hand** (Directness 1, you in the witness list, not a lie, not "telling lies") by the man in front of you. Up to three recent ones. Pick one and name a price:

| Price | Result |
|---|---|
| Make it worth my while (goods) | He hands over `2 + 2 x gravity` pieces of goods, furs, rum or pemmican |
| Speak for me to your master | Master's regard for you +8, your standing +3 (own company) |
| No more stories about me | Every lie he told about you is withdrawn from everyone |

**Gravity** is 3 for a killing, 2 for a robbery, break-in, theft, private trade or "talking with their master", else 1.

**He may refuse.** Defiance is `Courage + 0.5 x Suspicion - 0.35 x gravity (+0.2 if he already dislikes you)`. Above 0.8 he refuses and *goes first*: his master hears you have been threatening him, which is true. Either way he loses 15 regard for you (30 if he defies), and you cannot press the same deed twice. A successful press leaves a memory "*you* has a hold over *him*" that he never forgets.

## Tips

> [!tip]
> A cover story only works if the **real** witnesses are few or friendly. Lies about someone with loyal friends in the room are short-lived. Friends who warn you ("There's a story going round that...") also tell you who started it.

> [!warning]
> The Factor's year-end report docks standing for "trading on your own account" and for unexplained river crossings. Those stories can be real or invented.

## See also
[[Memory and Knowledge]] | [[Friends and Bonds]] | [[Favours]] | [[Standing]] | [[Rivalry]] | [[Grudges and Vendettas]] | [[Dialogue and Conversation]]
