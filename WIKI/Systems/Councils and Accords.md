---
title: Councils and Accords
aliases: [Fort William Council, July Council, The Rendezvous, Accord, Mandate]
tags: [system, council, nwc, politics, accord]
status: partial
---

# Councils and Accords

**Status:** Partial. The **July council at Fort William** (design doc 3.8, 3.8.1, 10.1.3) is Implemented as an off-map, share-weighted vote with six councillors, five to four agenda items and a minutes message. A general-purpose **Accord** object (treaties, safe passage, trade exclusivity, vendetta settlement; design doc 3.8.2) is **Planned**: no Accord class exists in the code yet.

![[loc-fort-william.png|400]]

## The council

Each year, when the **Brigade** calendar event (12 July) arrives at the Nor'Westers' house, `NwcLedger.OnEvent` calls `Council.Convene`. The furs are carried off, the council sits, and its **minutes come north as a sealed message** ([[Information and Messages]]) for the wintering partner.

### The six voices

| Councillor | District | Shares | Ambition | Suspicion | Loyalty | Standing |
|---|---|---|---|---|---|---|
| William McGillivray | Montreal agents (McTavish, McGillivrays & Co) | **6** (agent firm) | 0.8 | 0.5 | 0.7 | 80 |
| Mr. Duncan Cameron | the Bay (opposite York Factory) | 1 | 0.6 | 0.5 | 0.5 | 45 |
| John McDonald of Garth | Red River | 2 | 0.7 | 0.6 | 0.6 | 60 |
| Alexander Henry the younger | the Saskatchewan | 1 | 0.4 | 0.3 | 0.7 | 55 |
| Duncan McDougall | Athabasca | 2 | 0.9 | 0.7 | 0.4 | 50 |
| Roderick McKenzie | Lake Superior | 1 | 0.3 | 0.4 | 0.8 | 50 |

(The partner at the York post is Cameron of the Bay district, with a single share.)

**Presence:** the agent firm and the local partner always attend; every other voice misses the year with probability 1 in 5.

### How the vote works

For each agenda item, each present councillor computes a **leaning** toward each option and picks his favourite. Standing shifts this: for every other present councillor whose standing is more than 15 above his and who likes an option (leaning over 0.5), he adds `0.15 x (their standing minus his) / 100`. Then **his shares are added to that option's weight**. Letters from the player ("Write to the partners at Fort William...") add their weight. Highest weight wins.

### The agenda

| Key | Question | Options | Effect |
|---|---|---|---|
| `price` | The standard against the Company at the Bay | hold the price; undercut the Company by a fifth; let the Bay post set its own | Price unchanged, to `max(price, Bay x 1.2)`, or `PriceAtDiscretion` |
| `outfit` | The outfit for the Bay district | lean (**60 pieces**); usual (**90**); full (**130**) | `OutfitPieces`; every other district wants those pieces |
| `rum` | Liquor in the trade | restraint (a dram, no more); plenty of rum | Rum share **0.2** or **0.4**; the brigade lands 25 or 60 rum |
| `bay` | The Company's post at the Bay | keep the peace and take their trade; press them hard | Rivalry **-20**, or **+15** and raids sanctioned. **Only on the table if** the partner carried it in (he knows of seizure, break-in or blow) or Rivalry is 50+ |
| `share` | The Bay partner's conduct | censure (returns under 0.6 of plan); confirm; a second share | Partner standing -15 / +3 / +15, plus a share |

"The agenda comes from the world, not a script": the Bay item exists only if someone in the room knows about it. Leanings depend on `returnsRatio = shipped / quota` and each councillor's temper.

### Dissent

If the Bay partner lost the price vote (his leaning for the winner under 0.4), has Ambition over 0.6 and Loyalty under 0.5, he leaves "saying he would set the price the country required, whatever the minutes said": `PriceAtDiscretion` is set and his standing falls by 8. A partner at his own discretion sets the price to **at least 1.25 x** the Company's.

### Minutes as a message

Subject "the minutes of the council of *year*". Read by the partner it sets the directive text and notices ("the Nor'Westers will pay 1.30, with 90 pieces landed"). **If intercepted by a Company man**: he reads "the Factor knows their whole year's plan", Rivalry +6 and `KnowsRivalPlan`. An older, simpler **mandate** letter (price x 1.2 if returns were under 70 percent of quota, x 0.95 otherwise) is still defined for saved games but the council no longer uses it.

## Writing to the council

As a Nor'Wester with standing 20+ before July: **The companies... > Write to the partners at Fort William...** adds a letter to the next council: Urge them to undercut the Company's price / hold the price / ask for a full outfit / urge peace with the Company's post / urge them to press the Company hard / speak for the partner's second share. Weight is `clamp(standing / 25, 0.3, 2)` of a share, and your standing rises +1.

## Accords (planned)

Design doc 3.8.2 sketches one generic **Accord** object: parties, terms, and a breach condition always a witnessed event. Named types are parameterizations of the same object: peace treaty, trade exclusivity, safe passage, alliance/mutual defence, debt restructuring, vendetta settlement, and the 1821 merger as "the largest instance". None of that exists as a system yet; the closest built behaviours are a truce proposal ([[Rivalry]]) and restitution ([[Grudges and Vendettas]]).

## Tips

> [!tip]
> The council weighs shares, not heads. The Montreal agents hold 6 of 13 total shares: nothing passes if McGillivray votes the other way unless two or three others stack up against him and the standing pull works in your favour.

## See also
[[North West Company]] | [[Information and Messages]] | [[Rivalry]] | [[NWC Partner]] | [[Fort William]] | [[NWC Path]] | [[The Reckoning of 1821]]
