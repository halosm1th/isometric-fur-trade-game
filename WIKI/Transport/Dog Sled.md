---
title: Dog Sled
aliases: [Dog Team, Sledge, Toboggan, Sled Dogs]
tags: [transport, vehicle, planned, winter]
status: planned
---

# Dog Sled

**Status:** Planned (design doc 14.1 and 12.5 "emergency conversion"). The sprite (`assets/sprites/vehicles/dog_sled.png`) and a `sled` prop tile are cut but unwired; there are no sled dogs in `Species`. Currently the only trace is sound: a dog team passing.

![[vehicle-dog_sled.png|300]] ![[tile-sled.png|140]]

In winter "water routes close, travel shifts to dog sled/snowshoe" (design doc 14.1). Posts become isolated and live off what the last ship and brigade delivered. A brigade caught by an early freeze "can cache its canoe and cargo and convert to dog sledge or snowshoe".

## In the game now

- **Winter travel is on foot.** Snowshoes (an item, worn from the pack) improve speed on snow. See [[Travel and Wayfinding]].
- **A dog team passes by** as an ambient sound: in winter, by day, with a 12% chance on each critter roll when within about 2,500 px of York, the game plays `dogsled_pass.wav` (a CC0 "Dogsled passes by" recording by newagesoup). See [[Sound Effects]].
- **Winter conditions**: the canoes are frozen in until breakup; ice carries a walker ("on the ice in winter" in travel mode).
- The sled prop and the dog-sled sprite exist for placement.

## What is planned

| Item | Source |
|---|---|
| Sled dogs as a species: "Winter travel, barking, dog teams as property" (P1) | `docs/world-content-needed.md` section 6 |
| Dog sled, toboggan, Red River cart and York boat as owned transport | `docs/world-content-needed.md` section 8 |
| Emergency conversion of a stranded brigade to sledge or snowshoe | Design doc 12.5, 14.1, 20 |
| The "Spring Lockout" when ice is too weak to cross and too solid to paddle, making both canoe and sledge impossible | Design doc 14.1 |

## See also

[[Transport]] | [[Canoe]] | [[Travel and Wayfinding]] | [[Calendar and Seasons]] | [[Weather]] | [[Roadmap]]
