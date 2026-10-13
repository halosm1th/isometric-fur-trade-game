---
title: Marriage and Country Wives
aliases: [Marriage, Country Marriage, Marrying In, A la facon du pays, Wives]
tags: [system, marriage, kinship, family]
status: partial
---

# Marriage and Country Wives

**Status:** Partial. One marriage track (the *HBC way*: the wife moves into the post household) is built, with a one-wife rule. The *Nor'West way* (living with the band), plural wives, the Metis track and the full "leaving wives behind" consequences are **Planned** (design doc 8).

![[char-country_wife.png|110]] ![[char-cree_woman.png|110]]

## Courting

You can court the unmarried daughter of a Cree chief or hunter. **Family and marriage...** (shown for Cree chiefs and hunters) offers two options if you can talk to them (fluency 0.3 or better):

- **Ask about marriage in the custom of the country** - the explanation: "A man who wants a wife from us shows two things: that he is a friend, and that he can provide. He brings gifts to her father - blankets, a gun, cloth, kettles - and he comes back more than once. Then he asks. Your Company allows a man one wife; we do not hold you to that, but your Factor will." It shows your progress and the unmarried daughters.
- **Ask for *Name*'s hand** (one option per unmarried daughter).

### The requirements

| Test | Value |
|---|---|
| Not already married | The Company allows **one** |
| Father's regard for you | **30 or more** |
| Gifts to the father, totalled in MB | **8 or more** (`GiftsFromPlayerMb`) |

Under 30: "You are a stranger to him yet." Under 8: "A daughter is not given for nothing."

Gifts count when you use **Give a gift** or open a trade with a gift. Rum and ale are drunk at once and count for double regard, but for worth only their value.

On success she becomes your **Wife** (role changes, home becomes York Factory, bed in the Depot), the whole of her father's band gains +10 regard for you, you gain +0.3 to your fluency in her tongue, a notice runs ("*x* has married *you* in the custom of the country and moves into the post"), and she is bound to you as kin for [[Friends and Bonds]] and as a Wife.

The seed for this is in the prologue: promise your cousin on the [[Cree Path]] that you will speak for him, and the SpeakForCourting [[Favours|favour]] later asks you to do exactly that for another suitor.

## Living as married

| Effect | Detail |
|---|---|
| **Interpreter** | A wife within 300 px gives you full fluency with any people; for the Assiniboine in particular, any Assiniboine Wife nearby or your own spouse does ([[Languages]]) |
| **Negotiation** | +0.3 on matters touching the band or "the country" (you are "kin to *wife*'s people") |
| **Children** | At least 300 days after the wedding, 2 percent a day: a boy (52 percent) or girl is born at the post. Names: boys William, James, Thomas, John, Robert; girls Nancy, Isabella, Margaret, Christina, plus your surname. Headline and journal line; her band's household gains +8 regard for you. A child grows into the post at 16. |
| **Spring due** | On 6 June her father looks for gifts: if you have given at least `3 MB x (year - 1810)` he speaks well of you (+5); otherwise -12 regard and the band's standing with the Company drops 4 |
| **Neglect** | If you do not come within 900 px of her for **25 days** she sends a warning; at **45 days** she returns to her people: the band's standing with the Company -15, every band member's regard for you -20, a Chronicle event (weight 2) |
| **Death** | She is widowed; she becomes part of the [[Heirs and Succession]] ties |
| **The Ambition** | "Marry into a family of the country" reaches 1.0 on wedding |

## Tips

> [!tip]
> Plan the 8 MB of gifts well before you ask. A kettle and a blanket are both worth about 1 MB apiece. See [[Trading and Barter]] and [[Trade Goods]].

> [!warning]
> Marrying across the line to the Nor'Westers' side is not built: only one wife at one post. The design's HBC-vs-NWC marriage asymmetry (design doc 8.3) is aspirational. See [[Country Wife]] for the character.

## Planned (design doc section 8)

- Nor'West way (marry in and live with the band), Metis / freeman way.
- Plural marriage for NWC and freemen.
- Costs of moving between tracks, and wives left behind remarrying as real agents.

## See also
[[Country Wife]] | [[Cree Woman]] | [[Languages]] | [[Friends and Bonds]] | [[Ambitions]] | [[Heirs and Succession]] | [[Children]]
