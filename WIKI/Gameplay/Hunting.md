---
title: Hunting
aliases: [Hunting Game, Butchering, Goose Hunt, Stalking]
tags: [gameplay, system, hunting, food, wildlife]
status: implemented
---

# Hunting

**Status:** Implemented for caribou, geese, wolves, hares and ptarmigan; buffalo are partial: 18 herds are placed near the plains camps but drawn with caribou sprites, with no horses or communal hunt yet [[Buffalo]]. Code: `Scripts/Agents/Animal.cs`, `Scripts/Ecology/Species.cs`, `Scripts/Ecology/EcologyManager.cs`, `Scripts/World/GooseHunt.cs`.

Hunting is the way a post eats fresh meat (which also keeps off scurvy) and the way you earn standing with the Factor. There are **three verbs** in the design: *hunt* big game with a weapon, *trap* beaver ([[Trapping]]), and *fish or snare* small game ([[Fishing]]).

![[animal-caribou.png|140]] ![[animal-goose.png|140]] ![[animal-wolf.png|140]] ![[animal-hare.png|140]]

## What you can hunt

| Species | Health | Speed | Yield on butchering | Notes |
|---|---|---|---|---|
| **Caribou** | 70 | 170 | 18 fresh meat + 1 hide (caribou hide, or a holed hide if shot) | Herd moves monthly on a seasonal route; dangerous only when starving |
| **Goose** | 8 | 140 | 4 geese per kill with bow/knife, **8 if shot** into a flock | Marsh sites; autumn flight and spring flight |
| **Hare** | 5 | 210 | 1 hare | Snared by NPCs; small, thin fare, counts as fresh food |
| **Ptarmigan** | 3 | 150 | 1 (3 if shot into a flock) | Winter flocks on the willow flats |
| **Wolf** | 50 | 230 | 1 wolf pelt (or shot-through pelt) | Predator; a threat, hunted for the pelt |
| **Beaver** | 20 | 90 | 1 pelt + 3 fresh meat | Trapped rather than hunted ([[Trapping]]) |
| **Buffalo** | 260 | 200 | 70 fresh meat + 1 robe (or holed hide) | 18 herds near the plains camps; caribou sprites as a stand-in |

Pelts are **prime** except from June to September when they are thin. Full detail: [[Furs and Pelts]], [[Caribou]], [[Goose]], [[Wolf]], [[Hare]], [[Ptarmigan]], [[Buffalo]].

## Finding game

Wildlife lives at **sites** (lodges, marshes, a den, a moving herd). You do not see them on the map until you have **found them or been told**. Talk to a hunter or the chief and, if they like you (rapport 15 or more), choose "Ask about the hunting": they tell you where their household believes the game is, which for a herd may be stale. See [[Species Sites]] and [[Memory and Knowledge]]. Telling the Factor what you know earns standing (+3 each).

The caribou herd moves about 40% of the way to each month's ground every week; in the code's notes, hunters walk to where they *believe* it is and search up to four hours if it is not.

## The hunt

1. **Approach.** Crouch (`Ctrl`) to sneak: your footsteps carry only 45 px and animals judge you as if you were much farther away (stealth factor 0.4). Running announces you from 420 px.
2. **Strike.** A grazing animal is *unaware*. A knife stroke while crouched does 2.5x damage; a shot from a sneaking hunter has its miss chance cut to 40%. A musket is heard 1500 px away and sends the herd running; a bow only 200.
3. **Butcher.** Stand near the carcass and press `E` ("Butcher the caribou"). You get **pelts and hides first**, then as much meat as you can carry. A caribou is more meat than one person can carry, so some stays "lying" for you or a friend to return for. Carcasses vanish after about 36 game hours.
4. **Bring it home.** Meat goes to the stores (`E` in the warehouse stows provisions, keeping two meals; standing +0.5 per piece up to +4) or is eaten.

> [!tip]
> A musket ball tears the pelt. For furs, use traps, the bow or the knife; for sheer meat (a flock of geese) the musket's volley is best.

## The goose hunt

The autumn flight (20 August - 15 October) is the post's most important event after the ship. For those weeks the marsh holds every bird going south ("the water black with geese"), the Factor turns the whole post out, and the orders carry a count: "N casks in the cellar before the flight goes over, or we eat short." Women salt the birds into casks (a cask = 8 birds, 240 nutrition, 60 lb). A smaller **spring flight** (5 - 30 May) is relief, not a stock. A bad autumn is a hungry February. The journal's "The cellar" lists casks and weeks of food in hand. The marsh is a swamp (x0.6 pace) in summer. See [[Daily Orders]] and [[Goose]].

## Dangers

Wolves and starving prey attack people: wolf bite 16, caribou 12, buffalo 25. A wolf pack at the gate is a classic test of the guard. See [[Combat]] and [[Wolf]].

## Skill

Each ranged shot trains **Hunting** (+0.01); higher skill shrinks the miss chance. See [[Skills]].

## See also

[[Trapping]] - [[Fishing]] - [[Gathering and Gardens]] - [[Wildlife]] - [[Ecology]] - [[Combat]] - [[Food and Drink]]
