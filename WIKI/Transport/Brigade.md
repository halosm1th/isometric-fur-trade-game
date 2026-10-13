---
title: Brigade
aliases: [Brigades, Canoe Brigade, The Outfit, The Returns, Convoy]
tags: [transport, system, economy, hbc]
status: implemented
---

# Brigade

**Status:** Implemented (`Scripts/World/Brigade.cs`; design doc 12.1, 12.2, 18.3). Brigades run along the **Hayes river road** between York Factory and Oxford House. Other routes (Saskatchewan, Athabasca, Columbia, Montreal) are mapped but not yet driven by brigades.

![[vehicle-canoe_loaded.png|260]] ![[tile-canoe_beached.png|140]] ![[tile-cache.png|140]]

A brigade is **men, canoes and a manifest moving along the river road**: the open water is resolved in numbers, and the portages are where things actually happen to it. Twice a year the Company sends one up with the outfit and brings one down with the returns.

## The two seasons

| Brigade | Goes | When | Manifest | Men |
|---|---|---|---|---|
| **The autumn outfit** | York Factory to Oxford House (upstream) | From 18 September (once the ship has been unloaded) | 40 trade goods, 2 muskets, 6 point blankets, 6 steel traps, 1 kettle, 4 rum, 6 salt geese | 5 |
| **The spring returns** | Oxford House to York Factory (downstream) | From 8 June, once the river is open | 60 beaver pelts, 10 wolf pelts, 8 caribou hides, 10 summer pelts | 4 |

When a brigade arrives, the men who belong to the other end start back as a **homeward brigade** of their own, empty, in their own time, and are off the books until they land.

The `-- --brigade` dev argument sends the outfit up at once, whatever the date.

## The crew are real men

Sending a brigade costs something. The men (Labourers and Voyageurs, whoever can be spared from the sending post) go **off the post's books for the whole journey**: no morning orders, no rations from the store, not at the fire. Their bodies are carried with the canoes and out of sight (they drop to the background tier; see [[Simulation Tiers]]). They eat from the provisions loaded in the canoe, are on short commons if it loaded light, fish and snare at the carrying places, and may sicken or die on the water. "A death out of sight names what took him." A brigade in flight is saved and loaded with the world: her position, hull, gum, manifest, provisions and the names of her men. A load of pieces destroyed by a world reset goes back onto the sending house's books.

### Provisions

A brigade is provisioned for the whole leg: a month's victuals a man, drawn from pemmican, salt geese, dried goose and dried meat (about 22 per man, minimum 8). The homeward leg is out of what the far post can spare. A brigade that runs out lives on "what the country gives, which is not much on the water". Fish eaten fresh on the water is "the only thing in a canoe that answers the scurvy".

## Along the river

- **Pace:** per day, `0.095` of the road downstream, `0.055` upstream ("against the current, half the pace"), scaled by crew size (0.6 to 1.1) and cut to 75% if overloaded (over 240 lb of cargo per two men) and to 30% in a storm or blizzard.
- **Hulls.** Each day there is a 30% chance the rapids wear the hull (condition -0.03 to -0.09). Under 0.4 condition, the crew lie up a day **gumming a hull** with spruce gum (one patch per three men, plus one; a patch restores 0.35). With no gum left, a canoe is stove in: a third of every item in the cargo is lost, and the brigade is held two days. "A hull opening with no gum left to mend it" is the consequence the player is told.
- **Freeze-up** ends the season: a brigade still on the water is "frozen in short of" its destination and "the pieces winter on the bank".

## The carrying places

The Hayes road has three portages. Their positions are published by the world as fractions of the road (`WorldMap.Portages()`):

| Portage | Fraction of road | Time |
|---|---|---|
| **Rock Portage** | 0.404 | Two days: it is the depot where the York boats gave way to canoes, so everything was transshipped as well as carried over |
| **Borwick's Falls** | 0.531 | One day |
| **Trout Falls** | 0.813 | One day |

Positions are approximate and were corrected from sources on 11 October 2026 (Hill's Gates and Robinson Portage lie above Oxford House on the Norway House road and were never on this leg). At each portage the canoes come out of the water and the pieces go over on men's backs; the hull loses 0.05 condition (0.10 at Rock). The player is told "the canoes out of the water and the pieces over on men's backs". Every portage in the country also gets a cleared landing with a board, a canoe drawn up and a cache.

### Interception

The portage is the one place a brigade can be caught. If the rivalry between the houses is hot (tension 45 or more), the other company's men may be waiting: chance = `(tension - 40) / 150`, at most 0.4, plus 0.15 if the rival knows your plan. Half of every item in the cargo is carried off, the rivalry rises by 8, and the sending post's "lots lost to raids" is updated. "The other house knew which day you would be on the carrying place." The Director limits how often this fires. See [[Rivalry]], [[The Director]] and [[Combat]].

## What the player sees

A player of the same company hears of the brigade at once if he went with it, otherwise when word comes down the river. Anyone walking within 1,400 px of the brigade is "with it" and hears everything at first hand. Headlines announce each sailing ("sets off up the river: 5 men, N pieces, 3 portages") and arrival ("is in at Oxford House: N pieces landed after D days"). Brigades in the background are exactly what the tiers are for: "a brigade on the open river" is the model case for [[Simulation Tiers]]; at a portage it drops to full fidelity (design doc 18.3).

## Tips

- The outfit leaves from 18 September, after the ship has been unloaded; the returns leave from 8 June once the river is open.
- Load plenty of provisions and salted geese.
- Do not send a brigade when the rivalry is hot, or go with it.
- A brigade out of gum is a lost cargo: gum is only as plentiful as the crew is large.

## Design notes

Planned: canot du maitre and York boat brigades with different portage costs (design doc 12.1-12.2); vessel condition for the player's own boat; other routes; Métis boatmen. See [[York Boat]], [[Roadmap]] and [[Routes and Portages]].

## See also

[[Transport]] | [[Canoe]] | [[Ship]] | [[Voyageur]] | [[Routes and Portages]] | [[Fort Ledger]] | [[Oxford House]] | [[York Factory]] | [[Pemmican]]
