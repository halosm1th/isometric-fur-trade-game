---
title: Heirs and Succession
aliases: [Death, Succession, Your House, Forebears, Respawn, Permadeath]
tags: [system, death, heirs, succession, house]
status: implemented
---

# Heirs and Succession

**Status:** Implemented (design doc P.6, "Lose before you die; die into succession"). **Death is a turn of the story, not a reset.** Someone of your house takes your place and inherits what a man really inherits: the grudges held against your name, your debts, your wife's people, your half-finished ambitions, and whatever the men did not buy at the sale of your effects.

![[char-player.png|110]]

## The death screen

When you die the overlay reads along these lines (names illustrative):

```
<Your name> is dead.
<Heir> takes his place - your brother, come out on the ship, 22.
  <Heir> has come out from Orkney on the Company's ship to take his brother's place
  He inherits: the work you left undone: ...
  Unfinished: rise at the post
Press <restart key> to take up his place.
```

(The restart key is bound to the `restart` action; see [[Controls]].) The game computes the inheritance when you die, and again freshly when you press the key.

## Who takes your place

Nearest first:

| Order | Heir | Condition | Detail |
|---|---|---|---|
| 1 | **Your son** | A living son, age 15 or older, whose `FatherName` is yours | "Raised at the post and knows the country a little". Steps out of the world to become you. |
| 2 | **Your wife's brother** | Married, and a Cree hunter of your wife's household who is not her | "He is of the family you married into." |
| 3 | **A kinsman off the ship** | Otherwise | Random Orkney given name (Alexander, Donald, Hugh, Andrew, Colin, Angus, Murdo, Finlay) with your surname, age 18-26, "your brother, come out on the ship" |

## What comes with the name

| Thing | Rule |
|---|---|
| **Standing** | `clamp(old standing x 0.4 + goodwill x 2, 0, 45)`, where goodwill = +1 per living person with regard over 25 for you and -1 per person under -25. Goodwill over 1 adds a tie ("your name stood well with *n* of the men"); under -1 a burden ("your name was ill thought of here"). |
| **Rank** | reset to labourer |
| **Everyone's regard** | multiplied by **0.4**, then clamped to -40..+40: "what they thought of the house, not the man" |
| **Grudges against you** | Transfer to the heir with the debt cut to **60 percent**. Burden line: "*holder* still holds your house to account for *victim* - *n* MB would cover it" |
| **Wife** | Cleared (`Spouse = null`). If she lives and there is a son: "*wife* is his mother and the band is his people"; otherwise "a widow of your house; her people will watch what he does" |
| **Private worth** | Kept at **25 percent** |
| **Ambitions** | Your active (non-abandoned) ambitions are copied across with their progress high-water mark as "unfinished business" |
| **Undone task** | A burden: "the work you left undone: *task*" |
| **Parole** | A parole to the other company "dies with you" |
| **Captivity, tasks done and refused** | Cleared; tasks counters reset |
| **Skills** | Keep your own skills; if the heir was a person in the world he lifts Hunting and Trapping to his if higher, and Cree fluency to 0.8 for a Cree kinsman |
| **Kit** | Pack cleared and reset to the starting kit; the heir appears at the gate outside ([[HBC Path]] kit). The inheritance screen lists weapons, clothing and tools as "items" but the code resets the pack. |
| **Faction, path, house** | Unchanged |

## The house

Your line is recorded as a **Forebear** in `House`:

```
Name, Rank, Born-Died. End. Did.
```

- **End**: "Killed by *attacker*." or "Died in the country."
- **Did** (any of): "He had a post of his own." / "He rose to clerk." / "He married *wife*." / "He put by *n* MB of his own." (over 40 MB) / "He did what he set out to do: *ambition*." (progress reached 1.0)

The journal's **Your house** section lists every forebear. Forebear names also count as relevant to the [[The Director|director]] (+2 each), so you are told news about your dead.

A headline and a Chronicle event (weight 2) announce "*Old* is dead. *Heir* takes his place: *relation*." and the journal adds "Inherited - *burden*" lines.

## Tips

> [!tip]
> A grown son is the best heir: you keep skills and the band's goodwill. A wife's brother gives you a kin network on the band's side. Keep your debts paid. See [[Grudges and Vendettas]].

> [!warning]
> Dying leaves your wife widowed and your heir facing both your enemies and your unfinished ambitions. Check the pack tab for blood debts before you take risks. See [[Combat]].

## See also
[[Marriage and Country Wives]] | [[Ambitions]] | [[Grudges and Vendettas]] | [[Standing]] | [[The Year Told]] | [[Health, Wounds and Illness]]
