---
title: Dialogue and Conversation
aliases: [Talk, Talking, Dialogue Menu, Conversation Window]
tags: [system, dialogue, ui, social]
status: implemented
---

# Dialogue and Conversation

**Status:** Implemented. "Talk" (design doc 3.6) is a **topic menu built from what this particular person is, wants, and thinks of you.** It runs on the server (where the people live); the client only shows text and choices.

![[char-factor.png|100]] ![[char-cree_captain.png|100]] ![[char-clerk.png|100]]

## The window

- A panel at bottom-centre with a title (the person's name), a paragraph of text, and numbered options.
- **Keys:** `1`-`9` pick options, `0` picks the tenth; `E` (interact) or `Esc` closes. Walking more than 220 px away ends the talk.
- Options ending in "..." open a submenu; "Leave", "Back" and "Close the chest" are shown as exits.
- Long menus scroll after about 380 px.
- Dev flags `--talktrace` and `--pick=1,3,2` walk a tree headlessly.
- **Chat** is separate: `Tab` opens a line to type, `Enter` sends it to everyone, shown through the world notices (the multiplayer chat box).

The same window drives non-person "conversations": the storeroom chest, woodpiles, an unattended canoe, and the prologue scenes of [[Starting Paths]].

## The greeting

The first paragraph is composed: *"[Name], [role], greets you [warmly|civilly|coldly]. They were [doing X]. [state] [blood between you] [tongue]"*.

- Mood: warmly (regard over 40), civilly (over 10), coldly (under -20).
- State: grey with fever, black gums (scurvy), drunk, hungry (under 35), shivering, hurt (under 50), "on his guard" with a musket, or "His house is still short of X; it is in his face" after a recent death.
- If there is blood owed you hear it, and "their hand is on their knife".
- Without a shared tongue: "You have little of their tongue; you get by with signs and a few words." Every talk teaches a little (+0.02 fluency, see [[Languages]]).

## The top-level menu

Options appear only when they make sense. In order:

| Option | When |
|---|---|
| Hand him the Factor's letter / Hand over the *kind* from *name* | You carry sealed mail addressed to him |
| Offer restitution for *victim* (goods) | He holds a grudge on you, or you are a band-kin husband (see [[Grudges and Vendettas]]) |
| Bid at the sale of *name*'s effects... | Your own leader is auctioning a dead man's kit that evening |
| Tell a story at the fire... | He is at a hearth within 300 px |
| Hear what he wants of you; Give him what he asked; Tell him who robbed his traps; Speak for *x*; Call in the good turn... | An open [[Favours|favour]] |
| Take him aside: "I saw what you did..." | You witnessed a misdeed (see [[Gossip, Lies and Leverage]]) |
| **Talk...** | Always |
| **Work and orders...** | Your own leader, your own man (if you can command), a follower, or anyone who can be asked along |
| **Trade** | Factor, Clerk, Postmaster, Cree chief, NWC partner |
| Ask her to see to your wounds | A woman, and your health under 70 |
| **Give a gift** | Always |
| Offer them something to eat | He is under 45 hunger and you carry food |
| Family and marriage... | Cree chief or hunter (see [[Marriage and Country Wives]]) |
| The companies... | A leader of a rival company, or your own leader in a two-post world |
| Offer a ransom / Give your parole | You are a captive of this leader's post |
| Confess you gave your parole | Own leader, parole running |
| Threaten them | Anyone who is not Company (HBC) staff, plus Company labourers |
| Leave | Always |

## "Talk..." submenu

| Option | Result |
|---|---|
| How do things stand? | Stores, ship, quota, rivalry or camp hunger, the Company's price as the band sees it |
| Tell me about yourself | Role speech, age, a temperament read (bold/steady/cautious, talkative/civil/close-mouthed, wants to rise/loyal/wants a quiet life), family, current regard |
| What's the talk? (rumours) | 1, 2 or 4 lines (by Talkative) from gossip about hurt/hungry/set-to men and everything he **actually remembers**, framed "I saw it myself", "I was told", "They say" |
| Tell them something... (it needn't be true) | Opens the [[Gossip, Lies and Leverage|lie]] menu; needs fluency 0.3 with a band person |
| What are they saying about me? | His stored misdeed memories of you (up to 3) with a "deny it" choice |
| Where is the game? | Hunters and labourers; reveals sites if his regard for you is 15 or more |
| Tell him where the game is | The Factor, for sites you know that he does not (+3 standing and +4 regard each) |
| What does London want of us? / What do the partners want? | His standing directive and the stores' progress |
| What did the council decide? | NWC partner, after the council has read |
| Speak for peace with *other band* | Cree chief with the neighbour at odds (regard 15 and 3 trade goods, +15 band relation) |

A man who has had four questions from you in a day "has had enough".

## Work and orders

- **Ask for work** (Factor), **About this task...**, **Volunteer for something...**, **Ask for the seat** (if you are in a contest), **Give orders...** to your own men, **Ask them to come with you**. See [[Delegation]] and [[Rivalry]].
- **Bargain over a task** ("It will be done", more time, a man, something in hand, leave to do it your own way, a different task, decline) uses the weighing on [[Standing]]; the answer says why.
- **Orders** (Cut wood, Snare small game, Hunt at *site*, Work the trapline, Stand guard at the stores, Come with me): obedience is `0.2/0.4/0.6` by your rank + his regard/100 + `(1 - Loyalty) x 0.2` +/- 0.1 for his Ambition, and it needs 0.4. A drunk man refuses.

## Gifts and meals

Gift: regard `+4 + 6 x worth MB` (doubled for rum and ale, which he drinks at once). Offering food to a hungry man: `+8` regard. Wounds: a woman washes and binds it once a day for `+25` health for a clean water.

## Tips

> [!tip]
> Close the loop: favours, leverage and call-ins all live under the top menu. If an NPC has something to say to you it is usually the **first** option.

> [!info]
> You can interrupt a barter without losing the session by backing out, but a pushed man sulks for six game hours. See [[Trading and Barter]].

## See also
[[Languages]] | [[Trading and Barter]] | [[Favours]] | [[Friends and Bonds]] | [[Memory and Knowledge]] | [[Journal, Map and UI]] | [[Controls]]
