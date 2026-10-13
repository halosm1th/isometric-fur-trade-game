---
title: North West Company
aliases: [NWC, Nor'Westers, Canadians, The Pedlars]
tags: [faction, nwc, company]
status: implemented
---
# North West Company

**Status:** Implemented (the house across the river, brigades and the July council); Fort William itself is an atlas post

![[ui-crest-nwc.png|140]]

| | |
|---|---|
| **Faction value** | `NWC` (enum 4) |
| **Home territory** | The Canadian Shield and Great Lakes corridor, Montreal to Athabasca; in the playable map, the Nor'Westers' house opposite York Factory |
| **Language** | English and French (voyageurs are French-Canadian by name) |
| **Sprites** | `nwc_partner`, `voyageur`, `clerk` |
| **Starting path** | [[NWC Path]] (arrive with the brigade on 12 July 1811) |
| **Rival** | [[Hudson's Bay Company]] |
| **Agents** | McTavish, Fraser and (later) McTavish, McGillivrays & Co, Montreal |
| **Atlas posts** | 38 NWC entries (37 posts and one `nwc_post`) in `world.json` |

![[char-nwc_partner.png|110]] ![[char-voyageur.png|110]]

## History

Founded as a partnership of Montreal merchants and wintering partners, the Company built its trade on the canoe brigade and on wintering deep in the country, often marrying into it. In 1804 it absorbed the [[XY Company]] and moved to a hundred shares (75 old NWC, 25 former XY). Its capital was Fort William on Lake Superior, named in 1807 for [[William McGillivray]]. Its fortunes in the game run through the [[Red River Colony]] affair: the Pemmican Proclamation, [[Cuthbert Grant]] at Seven Oaks, [[Lord Selkirk]] seizing Fort William, and the 1821 union. See [[Setting and Timeline]].

## How it plays

- **No London, no ship.** The house across the river is restocked each July by a canoe brigade from Fort William ([[Brigade]], [[Canoe]]). The [[NWC Ledger]] starts with a pelt price of **1.3** (better than the Bay) and a quota of **80**, with 40 rum, 70 trade goods and a few muskets.
- **A partnership, not a hierarchy.** Decisions come out of the July council at Fort William ([[Councils and Accords]]). The seeded councillors are [[William McGillivray]] (6 shares, agent firm), [[Duncan Cameron]], [[John McDonald of Garth]], [[Alexander Henry the younger]], [[Duncan McDougall]] and [[Roderick McKenzie]].
- **Directive.** The council's output sets the price, the outfit size (90 pieces), how free the rum is and whether raids on the Bay are sanctioned.
- **Rivalry.** Furs walking to the other counter, desertions and blood raise [[Rivalry]] tension (starts at 20; open hostility at 70). Past that, leaders send armed parties to plunder each other's stores. A beaten fighter of the rival company can be held prisoner rather than killed.
- **Cast.** [[Duncan Cameron]] (the partner at the house) and three [[Voyageur|voyageurs]]: Baptiste Lagimodiere, Pierre Falcon and Antoine Desjarlais. Inland NWC posts have a [[Postmaster]] and a crew of voyageurs.
- **The ceiling.** In the design, French-Canadian employees rarely rose above guide or steersman, which is what makes the [[Metis]] path attractive (design doc 5.2).

## Notable people

[[Simon McTavish]], [[William McGillivray]], [[Simon McGillivray]], [[Alexander Mackenzie]], [[David Thompson]], [[Alexander Henry the younger]], [[John McLoughlin]], [[Duncan Cameron]], [[Cuthbert Grant]], [[Pierre Falcon]], [[Jean-Baptiste Lagimodiere]].

## Locations

[[Nor'Westers' House]], [[Fort William]], [[Grand Portage]], [[Fort Chipewyan]], [[Red River Posts]], [[All Trading Posts]].

## Trivia

- When the Pacific Fur Company sold Fort Astoria in October 1813, many of its clerks became Nor'Westers, a real recruiting pipeline noted in the campaign news ([[John Jacob Astor]]).
- In the 1821 union, the NWC wintering partners become chief factors and chief traders (campaign news, Montreal).

## See also

[[Hudson's Bay Company]], [[Rivalry]], [[Red River Colony]], [[The Reckoning of 1821]]
