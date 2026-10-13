---
title: The Reckoning of 1821
aliases: [The Reckoning, Year-End Reckoning, Year's Account, The Union of 1821, The Merger]
tags: [system, reckoning, year, campaign, 1821]
status: partial
---

# The Reckoning of 1821

**Status:** Partial. The yearly **Reckoning** (the "year's account") is Implemented for every road in. The campaign's **1821 union of the companies** is a scripted beat that changes the rivalry and sends news, but there is no dedicated end screen or "epilogue" in the code (design doc 4.3, 13.7 are **Planned**).

> *"The year has a close, and the close is the ship."* (design doc P.3, 14.1)

The trade year ends when the ship sails with the returns. At that moment the game sets down what the year came to. **Nothing here is knowledge you did not already have**: it is your own book, written up, with your ambitions measured against what you did.

![[vehicle-ship.png|300]]

## Three closes

| Who | Closes when | Function | Counted against |
|---|---|---|---|
| HBC men | The ship sails (about 20 August) | `AtShipSailing` | The post's shipped MB against the quota |
| Nor'Westers | The Brigade calendar event (12 July) takes the furs off | `AtBrigadeAway` | The house's sent MB against the partners' quota |
| Band-born | The ice goes out and the geese are back (day **135**, about 15 May) | `AtBreakUp` | The camp's winter: mouths, food days, debts |

The HUD counts the days down for company men ("*n* days to the ship - packs *x* of *y*", as you last counted) and the stores are only known as of your last visit.

## What the account says

Each close writes lines into your journal and then flashes a headline. In order:

1. **The returns**: "The post shipped *x* MB against a quota of *y* - made, and then some / *n* short. You had a hand in it." (the last if you sold furs to your own company this year).
2. **Standing**: "You are *rank*, standing *n* (up *m* / down *m* / as before)." If rank changed: "You came into the year a *rank*."
3. **Work**: "Work set you: *n* done, *n* refused." and, if you have any, "Your own account stands at *n* MB - the Company's books do not show it."
4. **House**: your wife (and children at the post) and any forebears who died this year.
5. **Ambitions**: each active ambition's journal line, or "You have declared nothing you mean to do. 1821 comes either way."
6. **The country**: "Between the houses: *an uneasy peace / a cold rivalry / bad blood / open hostility*."

The band version reports: "*Band* came through the winter *n* strong, with *x* days' food in the cache"; "You stand *well / civilly / coldly / not at all* with the Company at the post, and *n* MB of the band's debt is still on their books"; and, if relevant, "You are at odds with the other band."

Then the counters reset (`TasksDone`, `TasksRefused`, `StoodGuardOverTheQuota`) and your last-year standing and rank are stored. After that, [[The Year Told]] writes the same year as a story.

## Standing's reckoning on the ship

Before the account, the HBC ship's sailing also applies **hidden consequences** from what the Factor has heard all year:

| Item | Cost |
|---|---|
| "Was seen at" the Nor'Westers' house, not authorized | `min(25, 4 x weight)` standing |
| "Traded on his own account" | `min(20, 4 + 3 x n)` |
| Parole to the Canadians never confessed | -12 |

Then rank is re-rated: 60+ postmaster, 35+ clerk, 15+ labourer, else on notice. See [[Standing]]. If the Factor has Loyalty under 0.35 and Ambition over 0.5 he also **skims 10 percent** of the beaver and wolf pelts from the hold.

The ship carries the accounts to London (75 days), where the committee rewrites next year's price and quota ([[Information and Messages]]).

## 1821

The historical campaign ([[Campaigns and Adherence]]) has a final `merger` beat dated **26 March 1821**:

- `kind: event`, with a 120-day window and 365 days' lead; pressure from the year before sets London's price x 0.9 and quota x 0.9.
- When it happens: it sets the `merger` flag, lowers [[Rivalry]] by **70**, and sends two letters. One to the Factor from the Governor and Committee ("joined under the Company's charter. Mr. George Simpson will govern the Northern Department. Expect economies") after 90 days; one to the wintering partner from Montreal ("the North West Company goes into the Hudson's Bay Company ... Many of the men will be let go") after 75.
- The lead-in is a set of "lawless years" pressure beats: 1818, 1819 and 1820 each add rivalry tension (+15, +15, +10) and in 1820 London's orders tighten again.

The Reckoning reads "1821 comes either way" for players with no declared ambition. What happens to the player after the union (the epilogue) is **not implemented**: see [[Roadmap]].

## Tips

> [!tip]
> The best Reckoning is a quiet one. Leave nothing the Factor can find, keep wood on the pile and the quota near made, and tell the stories yourself. See [[Gossip, Lies and Leverage]].

## See also
[[The Year Told]] | [[Standing]] | [[Rivalry]] | [[Ambitions]] | [[Information and Messages]] | [[Setting and Timeline]] | [[London Committee]]
