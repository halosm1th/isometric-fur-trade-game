---
title: Marriage and Country Wives
aliases: [Marriage, Country Marriage, Marrying In, A la facon du pays, Wives]
tags: [system, marriage, kinship, family]
status: partial
---

# Marriage and Country Wives

**Status:** Partial. The three ways of design doc 8.1 are built (`Agents/Marriage.cs`, October 2026): the *Company way* (one wife, who moves into your post), the *Nor'West way* (she stays with her people and you are kin to the camp) and, for a player born to a band, *to your lodge*. Only the Company and the Crown hold a man to one wife. Going over to the Company with several wives leaves all but the first behind, with the consequences of 8.4. A Métis/freeman road in is still **Planned**.

![[char-country_wife.png|110]] ![[char-cree_woman.png|110]]

## Courting

You can court the unmarried daughter of a Cree chief or hunter. **Family and marriage...** (shown for Cree chiefs and hunters) offers two options if you can talk to them (fluency 0.3 or better):

- **Ask about marriage in the custom of the country** - the explanation: "A man who wants a wife from us shows two things: that he is a friend, and that he can provide. He brings gifts to her father - blankets, a gun, cloth, kettles - and he comes back more than once. Then he asks." He then tells you your own side's custom (`Marriage.Custom`): the Company's one wife in the post, the Nor'Wester's place in her family, or her coming to your lodge. It shows your progress and the unmarried daughters.
- **Ask for *Name*'s hand** (one option per unmarried daughter).

### The requirements

| Test | Value |
|---|---|
| Not already married | Only for the Company (HBC) and the Crown, which allow **one** |
| Father's regard for you | **30 or more**, plus 10 for each wife you already have |
| Gifts to the father, totalled in MB | **8 or more** (`GiftsFromPlayerMb`), and 8 more for each wife you already have |

Under 30: "You are a stranger to him yet." Under 8: "A daughter is not given for nothing."

Gifts count when you use **Give a gift** or open a trade with a gift. Rum and ale are drunk at once and count for double regard, but for worth only their value.

On success she becomes your **Wife**, and where she lives depends on the way you married (`Marriage.WayOf`):

| Way | Who | Where she lives | Her people |
|---|---|---|---|
| The Company's | HBC, American, Crown (with a post) | Your own post's house (`HomePost`), not always York | +10 regard each |
| The Nor'West way | NWC, freemen | She stays in her father's camp | +20 regard each, and the band's standing with the Nor'Westers +10 |
| To your lodge | Born to a band | A lodge in your camp | +10 regard each |

Her father's band gains regard for you as above, you gain +0.3 to your fluency in her tongue, a notice runs ("*x* has married *you* in the custom of the country and moves into the post"), and she is bound to you as kin for [[Friends and Bonds]] and as a Wife.

The seed for this is in the prologue: promise your cousin on the [[Cree Path]] that you will speak for him, and the SpeakForCourting [[Favours|favour]] later asks you to do exactly that for another suitor.

## Living as married

| Effect | Detail |
|---|---|
| **Interpreter** | A wife within 300 px gives you full fluency with any people; for the Assiniboine in particular, any Assiniboine Wife nearby or your own spouse does ([[Languages]]) |
| **Negotiation** | +0.3 on matters touching the band or "the country" (you are "kin to *wife*'s people") |
| **Children** | Each wife separately: at least 300 days after the wedding or her last child, 2 percent a day, a boy (52 percent) or girl is born where she lives. A child born in a camp is of her people (a hunter or a daughter of the band); one born at a post is of the post. Names: boys William, James, Thomas, John, Robert; girls Nancy, Isabella, Margaret, Christina, plus your surname. Headline and journal line; her band's household gains +8 regard for you. A child grows into the post at 16. |
| **Spring due** | On 6 June each wife's father looks for gifts: if you have given at least `3 MB x (year - 1810)` he speaks well of you (+5); otherwise -12 regard and the band's standing with the Company drops 4 |
| **Neglect** | Counted for each wife. If you do not come within 900 px of her for **45 days** (**90** married the Nor'West way, where her people expect a man who winters away) she returns to her people, as for a wife put away (below). A warning comes about halfway |
| **Put away** | A wife left behind (neglect, or going over to the Company): the band's standing with the Company -15, every member's regard for you -20, hers -50, her father holds a 12 MB [[Grudges and Vendettas|grudge]] against you, and she marries again: the nearest unmarried Nor'Wester or man of her people, who thinks less of you (-15) |
| **Going over to the Company** | Deserting to the HBC or the Crown, or the union of 1821: the first wife goes with you, into the post if she was not already (standing -5, her father's regard -5); every other wife is put away |
| **Death** | She is widowed; she becomes part of the [[Heirs and Succession]] ties |
| **The Ambition** | "Marry into a family of the country" reaches 1.0 on wedding |

## Tips

> [!tip]
> Plan the 8 MB of gifts well before you ask. A kettle and a blanket are both worth about 1 MB apiece. See [[Trading and Barter]] and [[Trade Goods]].

> [!warning]
> A Nor'Wester with three wives who goes over to the Company keeps one. The others' families do not forget it. See [[Country Wife]] for the character.

## Planned (design doc section 8)

- A Métis / freeman road in (today the freeman's way is reached only by deserting to nobody, or being let go at the union).
- Bringing a wife's whole family into the post (8.3).
- Living the camp's round yourself the Nor'West way; today you visit her there.

## See also
[[Country Wife]] | [[Cree Woman]] | [[Languages]] | [[Friends and Bonds]] | [[Ambitions]] | [[Heirs and Succession]] | [[Children]]
