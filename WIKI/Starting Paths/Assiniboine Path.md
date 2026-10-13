---
title: Assiniboine Path
aliases: [Nakoda Start, Assiniboine Band Start, Born to the Band Upland]
tags: [starting-paths, assiniboine, band, prologue]
status: implemented
---

# Assiniboine Path

**Status:** Implemented.

![[ui-crest-assiniboine.png|120]]

| | |
|---|---|
| **Road in** | Born to the Assiniboine (Nakoda) band that winters upland and brings pemmican and furs to the posts |
| **Start date** | 10 June, 07:00 |
| **Place** | The [[Assiniboine Camps|Assiniboine camp]] |
| **Faction / home post** | Assiniboine; no company post |
| **Backgrounds** | Hunter, Trapper and go-between, Nephew of the leading man |
| **Language** | Assiniboine fluency 1.0; Cree fluency at least 0.3 |
| **Rank / standing** | "of the band", standing 0 |
| **First order** | None. "The day is yours." |

Blurb: *You are of the band that winters upland and brings pemmican and furs down to the posts. Your kin, your horses' country far west, and traders who need what you have.*

![[loc-assiniboine-camp.png|420]]

## How it differs from the Cree start

The prologue is the same three-scene hub as the [[Cree Path]] (the scene is shared code in `Starts.Camp`), with these band-specific differences:

- Your people are named "the Assiniboine"; the band's leading man is **Tchatka (chief)**, and your kin are drawn from the band's household in the editor-placed cast (the Assiniboine group in the York scene includes Mato, Wanbdi, Cetan, Wiyaka, Ptesan, Wakanda and Winona).
- In June the band is in its **summer ground** by the Bay (the Assiniboine come down the river road on 1 July in a running world and go back upland on 1 September "for the buffalo"). The prologue's `where` line reads "stand on the trail by the Bay men's house for the trading" if the camp is in its summer place.
- Your sprite is the Assiniboine sheet. ![[char-assiniboine.png|110]]
- The band's cache is food-rich: it starts with 40 pemmican, 6 caribou hides, 20 firewood, 4 beaver, 3 wolf pelts and 6 trade goods, and makes 14 pemmican a week while wintering ([[Band Ledger]]).
- Its standing with the Company starts at **40** (the Cree's is 50) and with the Nor'Westers at **45**.

## The prologue: "Morning in the camp"

Opening line: *The Assiniboine camp. These are your people.*

| Scene | Choice | Outcome |
|---|---|---|
| Sit with your elder | Take the furs | +4 beaver pelts, elder regard +5, opens the TradeForKin favour (8 days) |
| Go with your cousin to the snares | Promise to speak for him | Cousin regard +10, shared memory |
| | Tease him | Cousin regard +3 |
| See what the Canadian wants | Drink with him | One rum, consumed; journal note |
| | Send him away | Elder regard +4 |
| Go about the day | | Closes the scene |

The elder's words are the same as in the Cree camp: four good beaver, bring back powder and a kettle, and do not let the master or the Canadians make a fool of you.

## Languages

You are born fluent in Assiniboine, and the Nakoda tongue is kin to Dakota (0.8) and loosely to Mandan (0.3). Cree is a trade language you hold at 0.3 or better. With the Cree you therefore sit right at the "can talk" threshold (0.3) but below the 0.5 line, so barter with them is **crude** (a 20 percent surcharge on purchases and a -0.08 price margin) until you learn more. Your own tongue does not help with the Cree: the kinship table puts Assiniboine to Cree at 0. Every conversation with a band person teaches you a little more. See [[Languages]].

> [!tip]
> Your wife, if you marry a woman of the country, acts as an interpreter for the Assiniboine within 300 px of you. See [[Marriage and Country Wives]].

## Tips

> [!tip]
> The Assiniboine's strength is pemmican. The camp keeps the larger of 30 or three pemmican per mouth for itself and carries any surplus of 10 or more (in lots of up to 20) to whichever post will take it. See [[Economy]] and [[Trading and Barter]].

## See also
[[Starting Paths]] | [[Cree Path]] | [[Assiniboine]] | [[Assiniboine Camps]] | [[Languages]] | [[Pemmican]]
