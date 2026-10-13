---
title: Friends and Bonds
aliases: [Friendship, Bond, Kin Bond, Friends]
tags: [system, friends, social, npc]
status: implemented
---

# Friends and Bonds

**Status:** Implemented (design doc P.8, lever 4). A close friend **acts without being asked**: he slips you food when you are starving, tells you what is being said about you, keeps your misdeeds to himself, will not believe a lie about you, and stands with you when you are attacked. It works the same between any two people, the player included.

## What makes a friend

`Friends.Bond(n, a)`: the sum of

- his regard for the other / **25**
- **+1** for every firsthand, weight 1.0+ memory the two of them both witnessed ("we went through that together")
- **+3** if they are spouse, father, child, or kin by [[Starting Paths|the character sheet]]

`IsFriend` = regard **20 or more** *and* Bond **3 or more**.

Prologue example: the two Orkney lads on the [[HBC Path]] begin at +12 regard and one shared memory (bond 1.5). Swearing to stand by each other adds +27 regard and another memory (bond 3.6), so they become friends the moment you shake hands.

Kin from the band paths start at regard 50 with two shared memories (bond 2 + 2 + 3 = 7), so they are friends immediately.

## What friends do

Every few game hours a friend within **220 px** checks (in this order):

| Act | Trigger | Detail |
|---|---|---|
| **Share food** | You are under 30 hunger and he has over 45 | Gives you one food item from his pack, once a day per pair. "Here. Eat something." A witnessed event records it ("shared his food ... when *you* was starving"). It later becomes part of [[The Year Told]]. |
| **Warn you** | He has heard bad news about you, or a grudge is held against you | "There's a story going round that *you struck X*. I don't believe it. It started with *liar*." or "Word is going round ... Your master will hear of it before long." or "Watch yourself. *holder* has not forgotten *victim*." You also learn the liar's name. |
| **Keep quiet** | He holds a misdeed by a friend of his | When gossiping he repeats it only **10 percent + 15 percent x Talkative** of the time. |
| **Refuse a lie** | A made-up story names a friend | Half the time: "Not *X*. I'd stake my life on it." ([[Gossip, Lies and Leverage]]) |
| **Fight for you** | A friend within 400 px was struck within 2 seconds by a non-friend | If he is armed and has Courage 0.25+, he joins your side ("Leave *X* be!"). He will not draw on his own faction's quarrel. |

Warnings land in the player's journal and the Focus panel ("a friend's warning ...") for two days.

## Silent friends and kin asks

A friend's usefulness runs both ways:
- **Ask them to come with you** works at regard 5 for your own company; others need 20 unless kin.
- Spouses and wives' fathers count as kin for this.
- The [[Favours|favour system]] gives you debts you can call in.

## Bonds and mourning

When someone dies, `Losses.Bond` measures how deeply each survivor was tied (kin 10; else shared memories + regard/25). Strong bonds mourn aloud and keep things from the dead; a household's [[Memory and Knowledge|barks]] mention his empty bunk. See the Barks section there.

## Tips

> [!tip]
> The fastest road to a friend is the prologue's shared memory plus a gift of food when they are hungry (+8 regard). A friend is also the cheapest insurance against a liar in the room.

> [!info]
> Friends also protect you from the Factor's gossip: a man who owes you a bond will not repeat your sins.

## See also
[[Favours]] | [[Gossip, Lies and Leverage]] | [[Standing]] | [[Memory and Knowledge]] | [[Heirs and Succession]] | [[Marriage and Country Wives]]
