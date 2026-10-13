---
title: Grudges and Vendettas
aliases: [Blood Debt, Vendetta, Restitution, Informal Justice]
tags: [system, grudge, vendetta, justice, bands]
status: partial
---

# Grudges and Vendettas

**Status:** Partial. The *blood debt* half of design doc 7.4 is built: a killing creates an unanswered debt owed to particular people, who pursue it, and it is settled with goods or blood or passed down. The wider "Accord / vendetta settlement as treaty" is **Planned** (see [[Councils and Accords]]).

An unanswered wrong is "not a number on a reputation sheet but a debt owed to particular people." The record (`Grudge`) holds: **Holder** (who carries it), **Against** (who owes), **Victim**, **Day**, **DebtMb** (what would make it right), **Settled**, and **Blood** (answered in kind).

## When one is created

When a **band person** (Cree, Assiniboine, other nations) is killed by an agent that is not an animal, the victim's kin take up the debt:

- spouse, father, living children, **plus** the band's chief and all hunters of the same band (the band is one family for this).
- Each holder gets the grudge and their regard for the killer drops **-60**.

| Victim | Debt |
|---|---|
| Chief | **24** |
| Woman | **14** |
| Anyone else | **16** |

A headline reads "*Victim*'s people hold *killer* to account. It will be made right with goods, or with blood." and a witnessed event (weight 3) is recorded. A company man's killing is *not* a family grudge: it is the house's business and feeds [[Rivalry]]. (A band also loses standing with the killer's company: -25 for an HBC or NWC killer.)

## The holder's behaviour

`Grudges.Pursue(holder, debtor)`:
- Only a holder who is a fighter with Courage above 0.45 and Health above 50 will attack.
- Against a **player** the [[The Director|director]] gets a say: it asks `MayActNow("blood", 1.5)` so "a man who means to settle a death will settle it; whether today is the day is the director's to nudge." A no only means not this afternoon.
- The Focus panel warns when a holder is within 900 px ("*Name* is close by, and holds you to account for *victim*").
- In conversation the greeting reads "There is blood between you: *victim*. Their hand is on their knife."

## Settling it

| Way | Cost | Effect |
|---|---|---|
| **Restitution** (offer from the talk menu) | `ceil(DebtMb)` trade goods, laid before the family | The grudge and every sibling grudge for the same victim and killer are settled; holder regard **+25**; the band's standing with the Company **+12** |
| **Blood** (the killer is killed in turn) | none | Marked "answered in blood" |
| **Holder dies** | none | The debt passes to the next living chief or hunter of the camp; if none, it is settled |

If you are **married into the band**, "Your wife's people have a debt unanswered ... As kin, it is yours to help settle" and the restitution option appears on the husband's behalf too ("on *x*'s behalf").

The journal's Pack tab lists outstanding "Blood debt" lines for you with their MB.

## When you die

[[Heirs and Succession]]: grudges against you follow the family to your heir at **60 percent** of the debt ("a debt a generation can actually settle"). A friend may also warn you: "Watch yourself. *Holder* has not forgotten *victim*" (see [[Friends and Bonds]]).

## Tips

> [!warning]
> Killing any band person invites a whole camp's hunters to hold you to account. Pay early: `ceil(DebtMb)` goods is cheap compared to a camp's vendetta.

> [!tip]
> A band's standing with the Company drops 25 when a Company man kills one of theirs, and rises 12 when restitution is paid. See [[Standing]].

## See also
[[Combat]] | [[Standing]] | [[Heirs and Succession]] | [[Friends and Bonds]] | [[Cree]] | [[Assiniboine]] | [[Marriage and Country Wives]]
