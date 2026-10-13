---
title: Rivalry
aliases: [The Pemmican War, Company Tension, Raids, Career Rival, Rival Clerk]
tags: [system, rivalry, hbc, nwc, conflict, career]
status: implemented
---

# Rivalry

**Status:** Implemented. Two rivalries share this name in the game:

1. **Company rivalry**: how close the Hudson's Bay Company and the North West Company are to blows (design doc 4.1, 7.1, 15.1). "The Pemmican War in miniature."
2. **Career rivalry**: the named clerk who wants the seat you want (design doc 3.7).

![[char-rival.png|110]] ![[char-clerk.png|110]]

## Company rivalry: Tension

`Rivalry.Tension` is a number from 0 to 100, **starting at 20**.

| Tension | Description (`Describe()`) |
|---|---|
| 0-24 | an uneasy peace |
| 25-49 | a cold rivalry |
| 50-69 | bad blood |
| **70+** | **open hostility** (`HostileAt = 70`) |

Time lowers it by **1.2 per day**. Crossing 70 sets the campaign flag `open_hostility` and the notice "Bad blood between the companies has become open hostility ... Armed men go about both posts now"; falling back below 70 says "The companies have drawn back."

### What raises it

| Event | Change |
|---|---|
| A party seizes the other house's stores | +12 (+6 if they find little) |
| A leader sends a raiding party | +8 |
| A Company packet read across the river | +6 |
| The inland report read across the river | +4 |
| The council's order to press the Bay | +15 |
| Breaking your parole | +8 |
| Campaign beats ("the interior war", Seven Oaks etc.) | typically +10 to +15 |

### What lowers it

| Event | Change |
|---|---|
| A truce agreed in conversation | **-35** |
| The council orders peace at the Bay | -20 |
| The 1821 union (campaign) | -70 |

## Raids

When the rivalry is hot, leaders send **raiding parties** ([[Delegation]] decides each morning):

- **Conditions:** open hostility (70+) or Nor'Westers with raids sanctioned and Tension 45+; at least two free men; no ship in; at least 4 days since the last raid; and a reason (low provisions under 30 days, under 15 trade goods, Tension 90+, or having been raided).
- **Chance:** `0.35 + 0.4 x leader's Ambition`.
- **Party:** up to 3 men with muskets from the stores; one party out at a time; goes via the [[The Director|director]] (`MayActNow("raid", 2)`).
- **Loot order:** pemmican, dried goose, dried meat, rum, trade goods, beaver, bread, musket.

The house **alarm** counts defenders against raiders: if the house has at least as many fighters, courageous men (Courage above 0.2) go for the intruder; otherwise only the very brave (above 0.7) do and the rest flee, "The Nor'Westers are outgunned and stand aside." Every defender's regard for the raider falls **25**.

## Player options

In **The companies...**: *Propose seizing the other company's stores* (Tension 40+, the leader weighs `0.6 x Ambition + 0.3 x Courage + Tension/200 + 0.15 if your standing is over 30 + 0.3 if raided - 0.2 x Loyalty`, needs 0.55); *Propose a truce* (Tension 35+, with the other leader: `0.4 x (1 - Ambition) + regard/100 + 0.2 if he is hurt + 0.15 if Suspicion under 0.4 - 0.3 if he was just raided`, needs 0.45; a good one is **-35**); *Sell a sealed packet* ([[Information and Messages]]); *Ask about signing on* with the NWC and *Defect* ([[NWC Path]]); *Return* to the Company needs 20 regard from the Factor ("You start at the bottom").

Changing sides resets standing to 10, all former-company men's regard -25, new-company men +5, and logs a witnessed event.

**Captives:** a beaten man may be taken prisoner and held a few days (ransom **12 trade goods**, or a **120-day parole**: no arms against them until it runs out; the Factor learns of an unconfessed parole at the sailing for -12). See [[Combat]].

## Career rivalry

If you declare **Rise at the post** ([[Ambitions]]), the game finds the living clerk or labourer of your faction with Ambition 0.45+ and highest score, announces "*Him* is after *the clerk's desk* as well as you", and each week updates the scores:

- **His score:** `Standing + 12 x Ambition + 0.25 x (Factor's regard for him) + 10 if a clerk`.
- **Yours:** `Standing + 3 x tasks done - 4 x tasks refused + 10 (clerk) or +20 (postmaster) + 0.25 x (Factor's regard)`.

Each week he gets a word at the right time (Factor's regard +1.5). Every 7+ days, if he is behind he may:

1. **Tell the Factor something true he saw you do** (chance `0.3 + 0.4 x Ambition`, if he has dirt): your standing -8, Factor's regard for you -6.
2. **Lie about you** (if Loyalty under 0.5, chance `0.2 + 0.25 x Ambition`): "has been trading on his own account", to the Factor ([[Gossip, Lies and Leverage]]).
3. **Take the work you refused** (+3 standing for him, +4 Factor's regard).

You can fight back: **Ask him for the seat** (a negotiation, difficulty 0.55, or 0.8 if he is ahead; success +6 standing), or **Tell him what *rival* did** if you have a true, damaging line in your journal (difficulty 0.55; success: his standing -10, Factor's regard for him -8, yours +2, his regard for you -25; failure: your standing -4).

The seat falls vacant with the posting of a clerk to Oxford House ([[Delegation]]). The loser keeps the grudge: "Passed over for Oxford House". The Reveal line tells you who got it and why.

## Tips

> [!tip]
> Make a friend of the rival or outwork him. Dirt works only if you saw it yourself and the Factor trusts you more than the man.

> [!warning]
> Past 70 tension, expect armed men on the river road and an ambush for your inland packet.

## See also
[[Councils and Accords]] | [[Delegation]] | [[Standing]] | [[Ambitions]] | [[Hudson's Bay Company]] | [[North West Company]] | [[Combat]]
