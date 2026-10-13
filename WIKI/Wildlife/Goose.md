---
title: Goose
aliases: [Canada Goose, Geese, Goose Hunt, The Flight, Salt Geese]
tags: [wildlife, prey, food, seasonal-event]
status: implemented
---

# Goose

**Status:** Implemented (`Species.Goose`, `World/GooseHunt.cs`). Final sprites; the goose honk is a real CC0 recording.

![[animal-goose.png|220]]

The Canada goose is the food on which a Hudson Bay post lives through the winter. The spring and fall goose hunts were organised shoots on which entire posts depended, and a bad goose year genuinely threatened a post (design doc 14.4 records a bad 1816 hunt that caused hardship at a Bay post). In the game the autumn flight is the one event in the year a Bay post cannot afford to miss.

| | |
|---|---|
| **Diet** | Prey |
| **Health** | 8 |
| **Speed** | 140 |
| **Bite** | None |
| **Meat** | 6 lb nominal |
| **Drops** | Goose x4 per kill (1.5 lb, nutrition 35, 0.15 MB each); x8 if shot into a flock with a musket |
| **Regrowth** | 1.2 a year (the fastest of the big sites) |
| **Sustain floor** | 15% |
| **Max live at a marsh** | 12 |
| **Seasons** | Away November to March; back in April (70% of capacity); the spring flight 5 to 30 May; the autumn flight 20 Aug to 15 Oct |
| **Found at** | Goose marshes on the Bay coasts. The York marsh (180 of 300, radius 700 px) is on the bay shore just beyond Sloops Creek; 32 marshes in all |

## Sprites

Idle, walk, takeoff, fly and death in four directions: 84 frames. Migration is visible in the sense that the flying frames exist; the game currently selects idle, walk, fly (when startled) and death; takeoff is cut but unused.

![[sheet-animal-goose.png|520]]

## The autumn flight

`GooseHunt.Tick` runs once a day.

| Dates | Event |
|---|---|
| **20 August** | The flight opens. Every goose site is set to **eight times its capacity**: "The flight is on: the water black with geese." The master of each post gets a headline with the count wanted: "so many casks in the cellar before the flight goes over, or we eat short." |
| every third day | A notice on the progress: casks down, birds on the floor, days of geese left, the target. The journal also shows the countdown. |
| **15 October** | The flight goes over; the marsh drops to at most 40% of capacity. The books close. |

The spring flight (5 to 30 May) is smaller and comes when the cellar is empty, so it is relief rather than a stock; it has a countdown but does not change the marsh count.

### What the post is trying to do

- **A cask holds eight birds** and takes about seven man-days to salt. The women salt the birds; the men leave the marsh to help when the birds come in faster than they can be put down.
- **The target** (`Wanted`) is mouths x 90 x 33.6 / 240 casks, with a floor of 4: about 12.6 casks a head. The aim is the hard three months of the winter, not the whole five; a post never lived on geese alone, and "a mark it could not reach only ever read as failure".
- A cask of salt geese has nutrition 240, weighs 60 lb and is worth 1.6 MB. It travels well and is what a [[Brigade]] eats.
- **No marsh, no verdict.** An inland post with no goose ground it knows and can reach gets no target; it goes into the winter on the brigade's provisions and what the bands will sell.

### The reckoning

When the flight ends each post is judged by casks salted against casks wanted:

| Salted | Verdict |
|---|---|
| At least the target | "a good autumn" |
| At least half | "a thin autumn" |
| Some | "a bad autumn" |
| None | "no autumn at all" |

The notice reports weeks of food in hand. Players attached to the post receive a journal entry with a cause ("the men were at the marsh while the flight lasted" / "the flight went over with the work half done"); a post that gets under half the target runs a headline: "goes into the winter short". A bad autumn is a hungry February, three months later, which is the point. See [[Needs and Survival]] and [[The Year Told]].

## Hunting tips

- **Volleys.** A musket shot into a settled flock is a bag of **eight** birds; bow and melee take four. Use the musket during the flight and the bow the rest of the year to save powder.
- Only the autumn flight fills a cellar. Spring geese are a meal.
- Geese honk overhead in spring and autumn (2 to 5 voices, "a skein"). Use that as your cue. See [[Sound Effects]].
- Salt geese are also the first item a brigade is provisioned with.
- A goose has 8 health; any weapon kills it.
- All sites vanish in winter ("The geese have gone south. X is empty till spring."). Don't send hunters.

## See also

[[Wildlife]] | [[Ecology]] | [[Hunting]] | [[Calendar and Seasons]] | [[Food and Drink]] | [[Brigade]] | [[Species Sites]] | [[York Factory]]
