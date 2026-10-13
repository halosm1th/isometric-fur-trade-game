---
title: Favours
aliases: [Good Turns, Favors, Owed Turns]
tags: [system, favours, social, npc]
status: implemented
---

# Favours

**Status:** Implemented (design doc P.8, "lever 4 turned round"). A man's own trouble (scurvy, fever, a girl he cannot approach, a robbed trapline) becomes a **small request of the player standing beside him.** Done, it is a shared memory and a debt he remembers, and the debt can be **called in**. Left undone past its day, it is remembered too.

## The five kinds

```
FavourKind: None, TradeForKin, FreshMeat, CoverWork, SpeakForCourting, FindTrapThief
```

| Kind | He asks | Who asks | You deliver by | Short form |
|---|---|---|---|---|
| **TradeForKin** | "Take the furs *to the Bay house* and bring back a kettle and some goods. Don't let them cheat you." | Your elder in the camp prologue (auto-accepted) | Hand him a kettle while holding no beaver | "bring *him* a kettle for the furs" |
| **FreshMeat** | "My gums are going. Bring me something fresh - a goose, a fish, a bit of venison." | A man with more than **28 days** since fresh food | Give a Goose, Venison, Fish or Hare | "bring *him* fresh meat" |
| **CoverWork** | "I've the fever and the woodpile won't fill itself. Bring **four** armfuls of firewood to our pile for me?" | A sick man with a household | Stack 4+ firewood on his household's woodpile | "stack four firewood on his woodpile" |
| **SpeakForCourting** | "You've the ear of *the girl's father*. Speak for me." | An unmarried hunter, labourer or voyageur with Ambition above 0.4 when there is an unmarried daughter whose father isn't him | Talk to the father and pick the "Speak for..." option | "speak for *him* to *father*" |
| **FindTrapThief** | "Somebody's been at my traps. Find out who, and tell me." | A man who has a firsthand memory of his traps being "robbed" | Name a thief from the menu (up to six men) | "find out who robbed *his* traps" |

## How a favour starts

- Each NPC considers asking **at most once every 2 days**, from his own tick, when he is idle (not conversing, sleeping, or in a Tier 0 goal), a player is within **220 px**, he is not hostile to you, his regard for you is not below -10, and he has no open favour with you.
- He barks "*Name*! A word, if you've a moment." The ask itself happens when you talk to him: **Hear what *x* wants of you** > "I'll do it." (journals it) or "Not this time." (the favour lapses, his regard -3).
- Default due date is **6 days**; the prologue's TradeForKin is **8**.
- At most 60 favours are kept (oldest dropped).

## Doing it

`Favours.Complete`:
- His regard for you **+15**.
- You are owed: he now owes you **one good turn** (stackable).
- A memory (weight 1.3, firsthand, both of you witnesses): "*You* did *him* a good turn: brought him a kettle."
- A journal line and a bark: "I'll not forget this", "I owe you one", "You're a good man. I owe you."

Special rules:
- **SpeakForCourting:** you tell the father `6 + 10 x Trade + (his regard / 10)` of regard for the suitor.
- **FindTrapThief:** if you name the man who really did it (checked against the memories of the robbed man and of everyone who saw the deed) it is a true Directness-2 memory. If you name the wrong man, it becomes a **lie** (see [[Gossip, Lies and Leverage]]) about the named man from then on, and the asker believes it.
- **CoverWork** can be done in the yard by stacking wood, without talking.

## Failing it

If the due day passes without completion the favour **lapses**: his regard falls by **8**, a journal line and a speech ("You never did what *x* asked. He has noticed.") fire. Favours can also be refused outright.

## Calling it in

**Call in the good turn he owes you...** is shown whenever he owes you at least one. It spends one turn:

| Ask | Result |
|---|---|
| "Come with me." | He follows you ("Where you go, I go - this once") |
| "Something for my trouble." | Up to **3** of trade goods, pemmican, beaver or dried meat from his pack, in that order |
| "Put in a word for me with your master." | His leader's regard for you **+6**, and your standing **+2** if same company |
| "Forget anything you've seen me do." | Deletes his memories of your misdeeds ("What I saw, I never saw") |

If he cannot deliver (no goods, no master above him) the turn is refunded.

## Tips

> [!tip]
> The kin favour at the camp prologue is a free tutorial in the loop: carry the furs to the post, sell them at the best counter, buy the kettle, bring it back. See [[Cree Path]].

> [!tip]
> Keep a friend in debt. A "forget what you saw" can wipe a dangerous witness before the ship's report reaches the Factor. See [[Standing]].

## See also
[[Friends and Bonds]] | [[Gossip, Lies and Leverage]] | [[Marriage and Country Wives]] | [[Dialogue and Conversation]] | [[Memory and Knowledge]]
