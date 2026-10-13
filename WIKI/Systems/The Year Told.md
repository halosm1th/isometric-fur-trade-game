---
title: The Year Told
aliases: [Year Story, Your Year, The Year, As It Will Be Told]
tags: [system, narrative, journal, year]
status: implemented
---

# The Year Told

**Status:** Implemented (design doc P.1). When the year closes ([[The Reckoning of 1821|the ship sails, the brigade goes down, the band's spring]]), the year's journal and what people hold of you are **told back as a short account**: what you did, what was done to you, who you were with, who is gone. "Nothing is invented; it is the year's own record, chosen and put in order."

The Reckoning *tallies* the year in numbers. The Year Told *tells* it in a paragraph.

## What is written

`YearStory.Write(player)` adds one journal entry:

```
<date>: THE YEAR, AS IT WILL BE TOLD - <Name>'s year to <date>: <event>. Then <event>. Then <event>. You end it <rank>, standing <n> with the Company.
```

and a line on screen: "Your year is written up in your book (J)." The text is also stored as `LastYearStory`.

For a band-born player the last line reads "You end it among your own people." For a Nor'Wester: "with the partners".

If nothing scored: "*Name* kept his head down and his name out of the talk. A quiet year, and nobody's tale."

## How events are chosen

`Compose` reads every journal line since the **previous telling** (it stops at the last "THE YEAR, AS IT WILL BE TOLD" marker) and scores each:

`score = Director.Relevance(line, player) + Weight(line)`

where the weight adds **+1 for each** of these words found in the line:

`died`, `killed`, `struck`, `lie`, `married`, `prisoner`, `blizzard`, `hungry`, `the ship`, `sold`, `warned you`, `shared his food`, `bought`, `robbed`, `brigade`

(Relevance gives +3 for your name, +2 for your spouse, and so on; see [[The Director]].)

Lines with a score over 0.5 qualify. The **top seven** are kept, then **put back in the order they happened**. Orders and instructions are not the story: anything from " Report to" is cut. Each is rewritten from second person ("You struck X") into the third person and joined with ". Then ".

So the account naturally prefers: deaths, fights, a lie told about you, your wedding, a captivity, a hungry spell, a friend who shared his food, and what you bought or sold.

## What goes into the journal in the first place

The journal records what you saw or learned ([[Memory and Knowledge]]): witnessed events within 650 px, favours done or failed, a friend's warning, your hearing of a rumour, a Reveal line, and the year-end lines. Trivial chores (weight under 0.5) of other men stay out of it "unless the player is in it".

## Tips

> [!tip]
> The story is a record you can play to. A friend who shares his food, a warning you took, a marriage and a debt paid all weigh +1 or more each. Tell stories at the fire (they raise relevance too) and avoid the dull. See [[Friends and Bonds]] and [[Favours]].

> [!info]
> Because it reads the journal, it is only as good as the journal, which is capped (about 60 entries from witnessed events, 120 for Reveal lines). Old events can fall off before the year ends if you are very busy.

## See also
[[The Reckoning of 1821]] | [[The Director]] | [[Memory and Knowledge]] | [[Journal, Map and UI]] | [[Heirs and Succession]] | [[Ambitions]]
