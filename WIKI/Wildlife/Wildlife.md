---
title: Wildlife
aliases: [Animals, Fauna, Species]
tags: [wildlife, index, system]
status: implemented
---

# Wildlife

**Status:** Implemented. Seven species are in the code; six have finished 4-way sprite sets and the buffalo wears the caribou's sprites until its own sheet is drawn.

The country of the fur trade runs on animals. Beaver pay the Company's accounts, caribou and geese feed the posts through the winter, hares and ptarmigan stand between a house and the scurvy, wolves follow the game and eventually follow it to the post gate. In the game every animal belongs to a **wildlife site** (a lodge, a den, a marsh, a herd) whose population is simulated as a number until somebody comes near enough to see it. See [[Ecology]] for the rules and [[Species Sites]] for where the grounds are.

![[sheet-animal-caribou.png|420]]

## The seven species

| Species | Image | Health | Speed | Diet | Drops | Page |
|---|---|---|---|---|---|---|
| Caribou | ![[animal-caribou.png\|90]] | 70 | 170 | Prey | Fresh meat x18, caribou hide | [[Caribou]] |
| Buffalo | (caribou sprites) | 260 | 200 | Prey | Fresh meat x70, buffalo robe | [[Buffalo]] |
| Beaver | ![[animal-beaver.png\|90]] | 20 | 90 | Prey | Beaver pelt, fresh meat x3 | [[Beaver]] |
| Wolf | ![[animal-wolf.png\|90]] | 50 | 230 | Predator | Wolf pelt | [[Wolf]] |
| Goose | ![[animal-goose.png\|90]] | 8 | 140 | Prey | Goose x4 (x8 if shot into a flock) | [[Goose]] |
| Snowshoe hare | ![[animal-hare.png\|90]] | 5 | 210 | Prey | Hare | [[Hare]] |
| Willow ptarmigan | ![[animal-ptarmigan.png\|90]] | 3 | 150 | Prey | Ptarmigan (x3 if shot into a flock) | [[Ptarmigan]] |

All figures are from `Scripts/Ecology/Species.cs`. Speed is in pixels per second at ordinary time.

## How wildlife works, in short

- **Sites, not herds of agents.** Roughly 640 sites are placed in the world scene. Each has a count, a capacity and a regrowth rate. Individual animals appear only when a player (or a hungry site) is near, and fold back into the count when you walk away. See [[Simulation Tiers]].
- **Hunting draws down a specific site.** Over-hunt a lodge or a den and it is gone for good; over-hunt a herd and it quits the country until it next passes. Two parties working the same ground find it picked over.
- **Seasons matter.** Beaver pelts are prime in winter and thin from June to September; geese are only on the marsh between breakup and freeze-up; hares and ptarmigan turn white in winter; caribou and buffalo move on a twelve-month route.
- **Wolves follow the prey.** When game is plentiful the packs grow; when it is thin they starve and turn toward the post.
- **Knowledge.** A household only hunts grounds it knows, and only grounds in its own country (about 600 km). Nobody at York has ever seen a buffalo.

## Ways to take animals

| Method | Species | Notes |
|---|---|---|
| Shooting or stalking | All | See [[Hunting]]. A musket ball ruins the pelt: "shot-through pelt" or "holed hide" sells for a fraction. |
| Steel traps | Beaver | See [[Trapping]]. Set at a lodge, checked next day. |
| Snares | Hare, ptarmigan | Done by the camps and posts as the `ForageGoal`; see [[Gathering and Gardens]]. |
| The goose hunt | Goose | The autumn flight (20 Aug to 15 Oct) is the biggest event of the Bay post's year. |

## Seasonal calendar of the game

| Month | What the wildlife is doing |
|---|---|
| Jan to Mar | Geese gone south. Beaver prime. Hares and ptarmigan white. Caribou and buffalo in winter quarters. |
| Apr to May | Herd passage: caribou fill the country; buffalo move out to open grass. Geese return (spring flight 5 to 30 May). |
| Jun to Sep | Beaver pelts thin. Hares and ptarmigan brown. Buffalo out on the open plains. |
| Sep to Oct | Second caribou passage. Autumn goose flight 20 Aug to 15 Oct. |
| Nov | Geese leave. Beaver prime again. |

## See also

[[Ecology]] | [[Species Sites]] | [[Hunting]] | [[Trapping]] | [[Fishing]] | [[Calendar and Seasons]] | [[Furs and Pelts]] | [[Made Beaver]] | [[Needs and Survival]]
