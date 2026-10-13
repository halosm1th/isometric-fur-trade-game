---
title: Cree
aliases: [Nehiyawak, Swampy Cree, Plains Cree, Woodland Cree, Homeguard]
tags: [faction, nation, cree]
status: implemented
---
# Cree

**Status:** Implemented. The York Factory band is the deepest simulation in the game; the other Cree camps are lighter "country bands".

![[ui-crest-cree.png|140]]

| | |
|---|---|
| **Faction value** | `Cree` (enum 1) |
| **Home territory** | The Bay lowlands to the parkland: Swampy Cree on the coast, Woodland Cree in the boreal forest, Plains Cree on the Saskatchewan ([[Hudson Bay Lowlands]], [[Saskatchewan Country]]) |
| **Language** | Cree (the trade ran on it in the north) |
| **Sprite sheets** | `cree` (hunters), `cree_captain` (the chief), `cree_woman` (women and daughters) |
| **Starting path** | [[Cree Path]] |
| **Atlas bands** | Plains Cree (12), Woodland Cree (12), Swampy Cree (8), Oji-Cree (8) |
| **Family camps** | 19 homeguard families living by posts (Cumberland House, Norway House, Churchill and others) |
| **Relations** | Company 50, Nor'Westers 50, Assiniboine 65 at the start |

![[char-cree.png|110]] ![[char-cree_captain.png|110]] ![[char-cree_woman.png|110]]

## History

The Cree were the Company's middlemen at the Bay and the first partners of the Nor'Westers inland. In the game they stand at the centre of the trade: the pelt price decides how hard they hunt, and hunger or an insult can close the post to them. See [[Setting and Timeline]].

## The York Factory band

A camp of eleven people beside the Bay house: the chief **Maskwa**, four hunters (**Wapistan**, **Mahihkan**, **Pisiskiw**, **Kihiw**), four women (**Niska**, **Sakihitowin**, **Wapun**, **Atim-iskwew**) and two daughters (**Kimiwan**, **Wawatew**). The cache starts with 30 Venison, 20 Dried Goose, 20 Firewood, 6 Beaver Pelt, 4 Trade Goods, 8 Steel Trap, 2 Kettle, 12 Water and 4 Fishing Line.

- **Seasonal round.** At *Breakup* (early June) the band strikes camp and pitches its tipis on the trail by the post for the trading season; at *FreezeUp* (10 November) it returns to winter camp in the bush ([[Cree Camps]], [[Calendar and Seasons]]).
- **The chief** weighs two counters for his furs: price, standing, the walk, rum and debt, and carries the furs himself when the load reaches 8 MB or the camp is short of food ([[Cree Chief]]).
- **Hunters** set and check traplines, hunt big game or geese when meat is short, fish or forage otherwise ([[Cree Hunter]]).
- **Women** fish, dry meat, salt geese, make pemmican, chop wood and carry water ([[Cree Woman]]).
- **Standing drifts daily.** A hungry band loses 3 points against the Company when its price is under 0.9 (1 otherwise); a fed band gains 1 when the price is 1.2 or better, otherwise 0.3. At 20 or below the band will not bring its furs ([[Standing]], [[Band Ledger]]).
- **Rum.** Each dram raises the camp's "rum taste", and a counter with rum scores higher in the chief's decision ([[Trading and Barter]]).
- **Killing one of them** drops standing with the killer's company by 25.

## Other Cree bands

The atlas puts a band at each of four places and 19 family camps by posts. They are `CountryBand` ledgers: not in York's books, they trade at the posts of their own country, and their hunt is resolved weekly in numbers (6 units of dried meat per mouth, 4 in Feb-Apr, 8 in Sep-Oct; plus 2 venison and 1 fish per mouth; 0-3 beaver pelts a week in winter).

## Languages

Cree is mutually intelligible to a degree with [[Metis]] (0.8 in the kinship table), [[Ojibwe]] (0.6) and barely with [[Blackfoot Confederacy]] (0.1). See [[Languages]].

## Notable people

Maskwa and the York band are fictional; names are placeholder romanisations "to be replaced with properly researched period names" (code comment in `Population.cs`).

## See also

[[Assiniboine]], [[Hudson's Bay Company]], [[North West Company]], [[Friends and Bonds]], [[Marriage and Country Wives]]
