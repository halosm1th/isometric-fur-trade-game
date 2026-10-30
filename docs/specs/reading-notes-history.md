# Spec: History from the Reading Notes

Status: **spec / not built.** Written 2026-10-30. Draws on the owner's reading notes in `docs/NOtes/` and turns them into proposals for systems that already exist. Owner's ask: *"look through them and see how they might be relevant to the game."*

This is a list of proposals, not one system. Each one names its source, what the game does today, what changes, the period numbers to tune against, and where it plugs in. They are independent; section 4 suggests an order.

## 1. Sources

| Note (in `docs/NOtes/`) | Book or article | Weight for 1811-21 |
|---|---|---|
| `Fur Trade Books/Notes on Indians in the Fur Trade.md` | Arthur J. Ray, *Indians in the Fur Trade* (1974) | **High.** Bands, demand, provisions, ecology, disease |
| `Fur Trade Books/Notes on Servants of the Honourable Company.md` | Edith Burley, *Servants of the Honourable Company* | **High.** Servants, discipline, private trade |
| `Fur Trade Books/Notes on The Hudson's Bay Company and the Fur Trade by Glyndwr Williams.md` | Glyndwr Williams | **High.** Post life, wages, the 1810 reform, the union |
| `Fur Trade Books/Notes on Unsettled.md` | Robert Lower, *Unsettled* | **High.** Red River 1812-17 |
| `HBC Article Notes/Notes on Various HBC Articles on Specific Posts.md` | Ens; Pannekoek; Carlos & Lewis; Keith | **High.** Private trade, ship captains, consumption |
| `HBC Article Notes/Notes on Various HBC Articles on Religion.md` | Tolly Bradford and others | Medium. Religion at the posts and Red River |
| `HBC Article Notes/Notes on Various HBC Articles on the Company State and Metamorphosis Thesis.md` | Bradford on the "New System"; the family-firm article | Medium. London in 1810-11 |
| `HBC Article Notes/Notes on Various HBC Articles on Soverignty.md` | "Fantasies of Sovereignty"; the Selkirk Treaty | Medium. The 1817 treaty |
| `Fur Trade Books/Notes on Lakota America.md` | Pekka Hämäläinen, chapters 3-4 | Medium. The Missouri |
| `Indigenous Government Structures, societies, institions, etc..md` | The owner's notes on Assiniboine and Ojibwe government | Medium. Leadership inside a band (3.4) |
| `Notes on French Furtrade and Growth of Metis people.md` | Berthelette, *Heirs of an Ambivalent Empire* | Medium. Kinship, generosity, the freemen |
| `Fur Trade Books/Notes on The Hudson's Bay Company as An imperial Factor 1821-1869.md` | John S. Galbraith | Low. After 1821 only |
| `Red River 1835-1851 Notes/*`, `NOtes on White Possesive.md` | Red River council and institutions; Moreton-Robinson | Low. After 1821, or theory |
| `Capital and Company State/*`, `Canadian History/*` | Beckert, Stern, *Going the Distance*, *Maritime Capital*, *Planting the Province*, Taylor & Baskerville | Background. Stern's Coltman and the Dobbs fights; the 1815-22 deflation for the Montreal side |

If the timeline is drawn back to 1766 (longer goal 1), Ray and Williams on the years before 1774 become core material: the middleman era, Henley House, Cumberland House.

## 2. Principles

1. **Agents, not scripts.** Every proposal is a pressure that agents respond to. Historical outcomes like the 1811 mutiny or the 1816 battle are what the pressure tends to produce, and stay campaign beats only where a campaign wants them.
2. **Numbers calibrate; they don't dictate.** The figures below are targets for the simulation's averages, not constants to hard-code.
3. **Check before it ships.** The notes are quick, typo-heavy and in places drafted with Claude. Names and dates go through the Dictionary of Canadian Biography first (section 6).
4. **Two readings, one dial.** Where historians disagree (3.2), make it a tunable choice, not a settled fact.

## 3. Proposals

### A. Trade at the counter

#### 3.1 The factor's standard and the overplus
*Source: Ray ch. 3, 8; Williams "The Indians and the Bay Trade".*

**Today.** `StandardOfTrade` prices goods in MB, shaded by the post. `LondonCommittee` already catches a Factor who shades the *hold* (furs missing between York's books and London's).

**Proposal.** Give the Factor a second, quieter dial: the **measure**. The official standard barely moves, because the bands hold it as custom. A Factor makes his margin by short measures of powder, cloth and shot and by watering the brandy. The gain is the **overplus**, and it goes in the post's books as furs above the official worth of the goods paid.
- Bands bargain hard over things they can see (an extra pelt, a missing awl) and forgive measure cheats more slowly. A shaded measure is a tell for an agent with a good eye for trade: "the powder horn is light".
- Overplus rises under monopoly and falls under competition. It doubles as a **readout of rivalry** (feed it to `Rivalry` and to the Factor's reports).
- Past about **50%**, furs walk to the rival counter.
- Under the 1810 system (3.15) the officers share in the profit, so overplus becomes a private incentive.

**Numbers.** Beale at York, 1705-06: goods of 12,538 MB official worth brought in furs of 18,171 MB (about 45% overplus). The 1738 inquiry found much of it came from watered brandy.

**Hooks.** `StandardOfTrade.cs`, `Barter.cs` (tells), `FortLedger.cs`, `LondonCommittee.cs`, `Rivalry.cs`.

**Done when.** A York Factor with no rival runs a steady overplus; after an NWC post opens close by it falls, and furs start going to the NWC if he holds it.

#### 3.2 What a camp does once its needs are met: the council and its traders
*Source: Ray ch. 3, 8 (fixed demand); Carlos & Lewis (growing demand). Owner's decision, 2026-10-30.*

**Today.** `Wants.cs` gives a camp a target stock from use, adoption, taste and threat. A camp that has what it needs simply buys less. The trading party (`TradeTrips.Start`) is two to four hunters or the chief, and the camp's wants decide what it buys (`StandardOfTrade.Trade`).

**Decision.** Ray and Carlos & Lewis are not a global dial. Each describes what *some* camps did, and what a given camp does comes out of its own people. It is a **principal-agent problem**: the camp (the principal) sends men to trade, and the men who go (the agents) have interests of their own.

**Proposal.**
- **The council decides how hard to hunt.** Once the camp's pressing wants are met, the camp's council (`Laws.cs`, `BodyKind.Band`) votes on the season: hunt as before, or hunt less and keep the time. The vote leans on the people's own life (a camp whose leaders' standing rests on giving things away hunts for goods to give; a camp with no debt and full stores leans to leisure) and on its leaders' tempers.
- **The trader decides what he buys.** The party's leader carries the camp's furs and the camp's wants, but he chooses. Three kinds of trader, set by his personality and ambitions:
  - **For his people:** buys the camp's deficits first and brings everything home. Ray's camp.
  - **Hunting less:** brings in only what the camp needs and turns the rest of the season to leisure. Ray's camp under competition.
  - **The middleman:** buys beyond the camp's needs, keeps the extra in his own household's stores, and trades it on to a far camp for furs, horses and robes, which he turns into luxuries and gifts. Carlos & Lewis's growing luxury share, carried by men like him (3.3).
- **The people judge the trader.** He comes home and gives things out, or doesn't. A trader who keeps too much for himself loses followers and the council's trust; one who gives well gains standing, and next year the council sends him again. Being generous was a duty, not a nicety: in 1712 caribou hunters who wouldn't share with starving Crees met violent reprisals (Berthelette).
- **Rum** taste (`BandLedger.RumTaste`) grows fastest when goods are cheap and the trader is the drinking kind, not only when rum is given.

**Numbers.** Andrew Graham: a family needed about 70 MB of goods a year plus about 30 MB "to squander". Producer goods were 73% of York's trade in 1715, 57% in 1720 and 41% in 1770.

**Hooks.** `Laws.cs` (the council's vote), `TradeTrips.cs` (choosing the leader), `StandardOfTrade.Trade` (what he buys), `Wants.cs`, `CountryBand.cs`, the luxury items (new: beads by colour, combs, looking glasses), `Favours.cs` and standing (giving out).

**Done when.** Under the same three years of undercutting competition, two camps of the same people diverge: one with a giving trader hunts less and fills its stores; one with a middleman trader shows a growing luxury share and a richer, less-trusted trader. Both are readable in the band's ledger and in what people say about him.

#### 3.3 Middlemen resell, and the price grows with distance: **essential**
*Source: Ray ch. 3-4, 8; Williams; Hämäläinen ch. 3-4. Owner's decision, 2026-10-30: this must be built.*

**Today.**
- Post prices already rise with distance inland and with weight (`StandardOfTrade`, the distance from `Outfits`).
- A camp trades only at a post it can reach (`ReachPx`, a few days' travel) and that isn't in an enemy's country. A camp with no house in reach **keeps its furs**: there is no other outlet.
- Camps never trade goods with each other. The two exceptions are the Missouri towns (`MissouriTrade.cs`: horses north, guns south, corn) and the Assiniboine's corn run (`Gardens.cs`).
- Goods have no wear state: a camp's gun is a count in its stores, worn out by `Wants` use.

**Why it matters.** For a century the Cree, Assiniboine and Ojibwe were the trade's middlemen, and in 1811 they still are toward the Blackfoot, the Chipewyan and the far plains. Without camp-to-camp trade, far nations either reach a post or have nothing. That hides the main fact of the interior trade: goods arrive through people, used, dear and late, and whoever stands between a post and a far nation has power over both.

**Proposal.**
1. **Middlemen buy for resale.** A middleman trader (3.2) at a post buys beyond his camp's needs: guns, kettles, knives, awls, tobacco and powder, the goods far camps want. The extra goes into his household's stores, marked as **trade stock**.
2. **They use it first.** Trade stock is used for a season or two before it's sold on (Henday: the middlemen used their goods, then traded them in the spring). A good sold on is **second-hand**: it has some life spent, so it wears out sooner in the buyer's camp. Keep this light: a per-camp count of second-hand pieces per good, which `Wants` wears out at a higher rate. No per-item condition.
3. **Meetings are decisions, not dates** (owner, 2026-10-30: "move it from scripted to agentic"). Each side decides whether a ride to the other is worth it, from what it believes of the other camp: where it was, what it had, and when. The seasons come out of the camps' own rounds and stores, not a calendar. Built: `Middlemen.MidWillGo`, `FarWillGo`, `Journey`, `MeetVisitGoal`. The far camp pays in furs, horses and robes. The middleman carries the furs to the post next season, so a far nation's furs reach the post a year late, through other hands.
4. **The markup is a cost, not greed.** The middleman's price covers what he paid (including the post's double standard, 3.1), the wear, the carrying, and the gifts he owes his own partners. Then haggling adds what the far camp's need and the middleman's temper allow. Expect about 3x the post price for a gun, more for small goods far out.
5. **Heavy goods thin out.** Kettles and other goods heavy for their worth are carried in small numbers. The far camp gets few, and treasures them.
6. **The middleman defends his place.** A middleman people blocks far camps from reaching the post directly: escorting them, warning them off, fighting if pressed. This is how the Cree and Assiniboine kept their position after 1720, and how the Lakota held the Missouri (3.22). A post that opens inland past them (as the NWC and HBC did after 1774) breaks the position: the far camp now trades directly, and the old partners become rivals in the same market (Ray ch. 5).
7. **What the far camp believes.** Like post shelves (`StandardOfTrade` beliefs), a far camp knows what a middleman had last time and what neighbours say. It doesn't know the post price, so the markup can only be found out by someone who has seen both.

**Numbers.**
- A gun: 12 MB at the Bay, 20 from the NWC inland, 36 from the Cree to the Blackfoot, 25-30 even half-worn.
- A knife: 1 MB at the post, 9 MB from the Cree to the Chipewyan.
- Andrew Graham: few middlemen did any trapping themselves.
- Henry the Elder's 1776 Assiniboine camp had exactly one kettle.
- Before 1720, 172 canoes came to York in 1715 from as far as the Missouri; by the 1750s nearly all were Cree or Assiniboine. Far peoples refused to come in themselves: too far, no canoes, afraid of starving on the way (Cocking, 1772).

**Hooks.** `TradeTrips.cs` and `StandardOfTrade.Trade` (buying for resale), `Wants.cs` (second-hand wear), `MissouriTrade.cs` and `Gardens.cs` (the existing camp-to-town trade as the pattern), `MarketNews.cs` (beliefs about a middleman's stock), `Plains.cs` (meeting places and seasons), `Rivalry.cs` and `War.cs` (blocking), and the distance gradient in `docs/specs/global-trade.md` 3.3.

**Done when.**
- A far camp out of reach of any post (a Blackfoot camp, a Chipewyan camp) ends a year with guns, kettles and knives it got from a middleman, at about 3x the post price, wearing out sooner than new ones.
- The middleman camp carries the far camp's furs to the post the next season, and the post's books show them.
- Opening a post within the far camp's reach changes both: the far camp trades directly, the middleman's resale falls off, and their relations sour.

### B. Bands and chiefs

#### 3.4 The captain's outfit
*Source: Ray ch. 8; Williams.*

**Today.** "Trading captain" is a sprite (`Appearance.cs`). The gift and pipe open a barter (`Barter.Gift`).

**Proposal.** A company can **make a captain**: a laced coat, a hat with feathers and a flag (the NWC also gave flags), plus a share of gifts to hand out. A chief's standing rests on giving things away, so:
- A clothed chief's influence over his band rises while he has goods to give, and falls when he can't.
- Both companies can clothe *different* men in the same band. The band splits into factions that lean to different counters (a per-member lean, not a single band standing).
- A company can take the outfit back over debts. It works as debt collection, and it often fails.
- Outfits escalate under competition and cost money: a line in the post's books.

**Rival captains are in (owner's decision, 2026-10-30).** A company backs a man, but the split it can open follows the lines the people's own government already has. From the owner's notes on Indigenous government and Berthelette:
- **Assiniboine:** leadership comes from the hunt. More food means more wives, more respect, and followers; it is close to an aristocracy of the successful hunter. Institutions are few and short-lived. The rival to a good hunter is often a **medicine man who speaks well**, and the notes say such men could lead a group astray until it broke up or joined another. So an Assiniboine split is *the best hunter against the best talker*, and a split band can actually divide into two camps.
- **Ojibwe:** civil chiefs come by inheritance from the **Crane and Loon** clans, which check each other; the **Fish** clan settles disputes between them and casts the deciding vote; **Martin** is the war clan, and war leaders rise by skill, not birth; **Bear** keeps order. So an Ojibwe split is *Crane against Loon*, with Fish as the swing vote, and a company backing a war leader against the civil chiefs is making a different kind of trouble. Clan totems also link people across bands, which gives a second network for news and favours.
- **Cree:** not in the new notes. Use Ray and Williams (a captain's authority lasts the trading season and covers his own kin outside it) until there's a source.
- **Kinship comes first.** In Berthelette's watershed, a trader had influence only once he was taken into a family, usually by marriage. A company's captain is strongest when the company man is kin to him. Breaking that kinship (refusing a daughter, failing to be generous) is an insult, not a business choice: Loisel's refusal of a chief's daughter turned the chief against him (Hämäläinen), and in 1752 the Nakoda staged an attack on Fort La Reine to humiliate a commander who behaved without kinship.

So a rival captain works like this: a company finds the man on the other side of the people's own divide and clothes him. The split grows if both men can keep giving, and ends when one of them can't, or when the camp divides.

**Numbers.** Before 1763 York clothed about 15 big captains. M'Gillivray clothed 22 chiefs in one day at Fort George in 1795.

**Hooks.** `BandLedger.cs` (factions; a camp dividing), `Barter.cs`, `Favours.cs`, `Laws.cs` (band councils, with clan-weighted votes for the Ojibwe), `Marriage.cs` (kinship with the company man), `Appearance.cs`.

#### 3.5 Credit taken from one company, furs sold to the other
*Source: Ray ch. 8.*

**Today.** Each band carries `DebtMb` and `DebtNwcMb`, and debt is worked down first at that counter.

**Proposal.** A band in debt to both companies takes its furs where the *goods* come cheapest, not where it owes most. Credit in the fall followed by a sale to the rival in the spring becomes the normal behaviour of a rational camp under competition. Company responses are debt-forgiving bids to win a hunter (planned in `WIKI/Economy/Credit and Debt.md`), withholding the captain's outfit (3.4), and after 1821 one ledger and the calling-in of old debts.

**Hooks.** `TradeTrips.cs` (where to sell), `BandLedger.cs`, `Union.cs`.

#### 3.6 Provisioners have leverage
*Source: Ray ch. 7.*

**Proposal.** Plains bands know the posts need their meat, and act on it:
- **Burning the prairie** around a post so the herds keep away and meat costs more (the Assiniboine at Hudson House, autumn 1780). This makes the planned grass fires (design doc 18.4) something agents choose to set, with a chance of burning their own country.
- **Food boycotts:** refusing provisions to a post that sells powder to their enemies (the threat to Forts Augustus and Edmonton).
- Posts that depend on them tolerate a great deal; the Franklin expedition said traders "overlooked even murder".

**Numbers** (for pemmican demand): a post man ate about 5 lb of fresh meat a day and a voyageur 8 lb, or 1-1.5 lb of pemmican. Henry's 41-person Pembina post ate about 63,000 lb of bison in one winter. The NWC called for 644 ninety-pound bags in 1813.

**Hooks.** `Plains.cs`, `BandLedger.cs` (the plains provisioning cache), `Herd.cs`, weather and fire.

### C. The country

#### 3.7 Seasons that complement each other
*Source: Ray ch. 2, 9.*

**Proposal.**
- The **parkland** is the winter larder: bison drift into it for shelter just when the woods and the open plains are hungriest. A **mild winter is a hungry one** there, because the herds stay out on the plains.
- Woodland Cree leave their trap lines in mid-winter for the pound camps ("idle, playing and eating buffalo", Henry). Traders try to keep them in the woods for the furs. That puts a band's goals against the company's, and the company can bid to keep them (credit, gifts, food sold to them).
- Ojibwa trappers move in where the Cree have moved out, taking furs from districts the Cree call exhausted.

**Hooks.** `CountryBand.cs` (seasonal round), `Herd.cs`, `Weather.cs`.

#### 3.8 Horses and dogs
*Source: Ray ch. 8.*

**Numbers to tune against.** Fidler, 1815: Assiniboine about 500 horses for about 800 men, Cree about 200, Ojibwa about 100 (around one per lodge). The Assiniboine left their herds untended at the first snow and lost many over the winter; the Blackfoot moved and fed theirs. Henry counted 230 dog travois in a 35-man band (about 13 dogs to a family). Henry the Younger had his first two horses on the Red River only in 1800.

**Hooks.** `Herd.cs`, `HorseTrade.cs`, `Mount.cs`, the planned dog sled.

#### 3.9 Furs running out, 1811-21
*Source: Ray ch. 6.*

**Proposal.** Trapping pressure plus natural shocks:
- A **beaver epizootic** around 1800 (a backstory, with a small chance of returning): lodges full of dead and dying beaver.
- **Muskrat boom and bust** with water levels. High water drowns the young, low water freezes them or brings disease; recovery takes about 3 years. Muskrat is also on the ecology backlog.
- Districts trapped out in the 1790s (Cumberland) live on muskrat alone, swinging between "the most wealthy" and "the most wretched".

**Numbers.** Fort Dauphin marten: 2,196 in 1817, 366 in 1821. Cumberland: 37,007 muskrat in 1819.

**Hooks.** `Species.cs`, `Trapline.cs`, `EcologyManager.cs`.

#### 3.10 The 1819 epidemics
*Source: Ray ch. 5.*

**Proposal.** Fever contagion is built. Add **measles and whooping cough, 1819-20**, entering from the south and spreading along trade routes, so who you traded with decides who gets sick. Men die out of proportion to everyone else, and survivors pack into fewer lodges. Smallpox in 1780-82 is a memory old people carry (and a reason to dread a sickness from the south).

**Numbers.** The Brandon Assiniboine fell from about 5,000 (460 lodges of 11) to about 2,600 (370 lodges of 7); Carlton lost about 42%. Adult men were 42-44% of the dead.

**Hooks.** `Agent.cs` (fever), `Memory.cs`, `TradeTrips.cs`.

### D. Servants and masters

#### 3.11 The moral economy
*Source: Burley.*

**Today.** Men at York have an opinion of the player (`YorkHappenings.cs`). A man can refuse to go back (`PostGoals.cs`, which raises rivalry). I found no rations or grog allowance in a post's books.

**Decision (owner, 2026-10-30).** The dues follow Burley as closely as the sources allow. Her findings, as rules:
- **Grievances are not mainly about wages.** They are about the master breaking custom: bad or short provisions, cruel officers, work on Sundays and holidays, withheld grog. Wages are a dues item, not the main one.
- **The officers and the men live in two worlds.** Separate tables, food, drink and company. The men's opinion of an officer comes from the men's side, and an officer can't easily read it.
- **One man's refusal stays common.** Payne argued protest became collective and contract-based after 1800; Burley found individual refusal didn't decline, and contract arguments were rare outside the Norwegian case. So most incidents are one man, and combination is rare and serious.
- **Their culture is shared across nationalities:** drink, rough sexuality, spending everything, suspicion of authority, working slowly. Orkneymen are not docile; the Committee only thought they were.
- **The contract is weak.** Prosecuting a man cost more than it recovered; by 1859 the Committee gave up and raised wages and added tea and sugar. A master's real tools are rations, postings, humiliation and sending a man home.
- **Recruiting runs on reputation.** Men going home talk; notices on Orkney church doors bring the next men in.

**Proposal.** Servants accept the hierarchy, but expect their customary dues in return. Track a post's **dues**: rations by the customary scale, the grog allowance, Sunday and holiday rest (Christmas, New Year), no inland posting outside the contract, and a master of their own kind. Breaking a due lowers the men's temper. As it falls, from mild to serious:
1. Foot-dragging: slower work, "negligence".
2. A **barrack lawyer** arguing the contract ("it was never stamped").
3. **Combination:** the men refuse together. One man refusing is discipline; all of them refusing is a strike.
4. Desertion to the rival, or home on the ship.

And men going home on the ship carry the post's name to **Orkney**, so next year's recruits are fewer or dearer.

**Numbers.** Burley sampled one outfit a decade. 1810-11 had 15 incidents; 1820-21 had 68, 28 of them from Simpson's Athabasca campaign. Refusal of orders was the commonest, then negligence.

**Hooks.** `FortLedger.cs` (dues), `Laws.cs` (a post's rules), `PostGoals.cs`, `Ship.cs` (going home), recruiting in `Population.cs`.

#### 3.12 Two incidents in the game's own years
*Source: Burley.*

- **Brandon House, 1811:** Orkneymen refused to serve under a Canadian master, Hugh Heney. William Yorston, quietly insubordinate since 1797, was one of the leaders. (Yorston is already in `OrkneySurnames`.)
- **Nelson Encampment, winter 1811-12:** the "Glasgow insurgents" (William Finlay's group) among Selkirk's first party wintering near York under Miles Macdonell. Auld blamed Glaswegian republicanism. This lands in the York year being played and isn't in the project yet.

Both should come out of 3.11, not be scripted: a Canadian master and a winter of short rations are the conditions, and these are what tend to follow.

#### 3.13 The ship's captain runs the private trade
*Source: Ens; Pannekoek.*

**Today.** The Private Trade ambition exists. A sailor buying furs quietly is a York happening.

**Proposal.** While in port the captain is the hub of the private trade:
- He buys furs and sells liquor off the books, and his mates take shares (Graham, about 1770: 100, 31 and 21 skins to the captain, first and second mates).
- He **carries the letters home**, so he can lose a complaint against himself. That makes him a source of leverage and lies (`Leverage.cs`, `Lies.cs`).
- He sits on the post council while the ship is in, and the Factor's word weakens around the wharf.
- London listens to him: a Factor who fights him may be the one recalled (Duffield at Moose, 1741-44).

**Numbers.** Private trade between York and Severn about 1770: 3,136 skins in a year, worth £1,254.

**Hooks.** `Ship.cs`, `Ambition.cs`, `LondonCommittee.cs`, `Leverage.cs`.

#### 3.14 How a Factor rules
*Source: Pannekoek; Ens; Williams.*

**Proposal.** A personality axis for masters, from **tolerant** (gates open, credit easy, private trade winked at, the men content, the books short) to **strict** (gates locked at night, searches, no brandy, rule by humiliation; the men sullen, the books clean). Duffield is the extreme: when a servant threatened to hang himself, Duffield handed him a rope, and when the man said it was too short he gave him a very long one in front of everyone. A post council (Factor, surgeon, accountant and two or three long-serving men) can advise, but the Factor doesn't have to listen.

**Hooks.** `Personality` (wiki), `RoleBehaviour.cs`, 3.11.

**Flavour for the men's lives** (Williams): until 1770 a labourer earned £6-10 a year, a tradesman £20-36 and a chief factor up to £100. Wages were held in London; a man could draw up to a third at the warehouse. Henday saved £113 of £120. Officers drank wine and brandy; the men drank porter. Factory gardens grew lettuce, turnips, radishes and cabbage.

### E. London

#### 3.15 The "New System" of 1810
*Source: Williams; Bradford on Wedderburn and Jamaica.*

**Today.** `LondonCommittee.cs` sets York's standard once a year from returns against quota.

**Proposal.** The game opens a year into Wedderburn's (later Colvile's) retrenchment. London's orders should carry it:
- Officers take **half the profits** of their trade, so overplus (3.1) and the men's dues (3.11) are now their own money.
- Officers may **set their own standard of trade** within limits.
- **Live off the country:** less food sent from England, inland posts told to grow and hunt their own (Edmonton was trying to farm in 1811-14), and provisioning posts for the others. Hungry posts are the expected cost.
- Northern and Southern Departments with a superintendent over each.
- Wedderburn wanted indentured Western Scots on £18 for three years, plus land, in place of Orkneymen. It never happened for servants, but the Red River settlers' terms were close.

**Hooks.** `LondonCommittee.cs` (orders), `FortLedger.cs`, `Outfits.cs`, `PostWants.cs`, `Laws.cs`.

#### 3.16 Who owns the Company
*Source: the family-firm article.*

**Proposal.** London news, not a simulated boardroom. By 1811 the Selkirk-Wedderburn-Halkett family bloc held about 30% of the stock. The 1811 General Court passed the plan and the Red River grant 86 to 14, with NWC-linked shareholders against. This news reaches the posts by ship and the NWC by Montreal. The money side (no dividend in 1809, an overdraft of £50,000 then £75,000 by 1820, the Continental Blockade) belongs to the solvency ledger in `docs/specs/global-trade.md`.

### F. Red River, 1812-17

#### 3.17 A grievance made on purpose
*Source: Lower, ch. 22.*

**Proposal.** A Nor'Wester, **Dugald Cameron**, deliberately told Miles Macdonell that the Métis were driving the herds away, which led to the ban on running the buffalo. Cameron's aim was to stir up the Métis. Robertson got the confession out of Seraphin Lamar in 1815. This is `Lies.cs` exactly: a lie with a liar's name that does what the truth would until someone who knows better says so. Model the Pemmican Proclamation and the buffalo-running ban as `Laws.cs` motions that a lie can carry.

Keep the two Camerons apart: Dugald engineered the ban; Duncan talked the settlers into deserting in 1815. Only Duncan is in the game.

#### 3.18 Other beats
- 1813: the settlers' ship, with typhus aboard, puts them ashore at Churchill, not York. About 50 walk to York in the spring of 1814 (12 days).
- 1814: John McDonald of Garth defuses the first standoff with a pemmican-for-oatmeal swap. The Nor'Westers will deal; Macdonell won't.
- 1815: Duncan Cameron offers free passage to Upper Canada and more than half the colonists go. Robertson brings the rest back.
- 1816: Alexander Macdonell turns Semple against Robertson whenever Robertson leaves the room. Grant plans a surprise attack and gets a fight on the open plain because his men are seen. He stops the killing of prisoners.
- 1816-17: the *Prince of Wales* hits ice in Hudson Strait and winters at Moose.

**Hooks.** `Campaign.cs` beats, `Lies.cs`, `Grudge.cs`, the characters in `WIKI/Characters/`.

#### 3.19 Justice from far away
*Source: Lower; Stern; the sovereignty notes.*

**Proposal.** There is no court in the country. Under the Canada Jurisdiction Act (1803) a capital charge goes to **Montreal**: the accused and the witnesses travel there, at the cost of a season. Witnesses scatter, die or are bought. Coltman, who led the inquiry, held NWC shares. After Seven Oaks nobody was convicted. An arrest is therefore a way to remove a man for a year, more than a way to punish him.

**Hooks.** `Laws.cs`, `Musters.cs` (arrests), `Delegation.cs`, travel.

#### 3.20 The Selkirk Treaty as a rent
*Source: the sovereignty notes.*

**Proposal.** In the 1817 treaty with Peguis, the Company and Selkirk see a land purchase, while Peguis sees a **rent**, kept alive by the yearly gift. Model it as an Accord with a yearly payment: paid, it holds and standing grows; missed, Peguis's people treat the land as theirs again. (The owner's notes disagree with the "Fantasies of Sovereignty" article's wider thesis; this proposal uses only its account of how the treaty was read.)

**Hooks.** Councils and Accords, `BandLedger.cs`, `Favours.cs`.

### G. Religion and marriage

#### 3.21
*Source: Bradford; Williams; Burley.*

- No chaplain served at the posts from the 1690s to 1820. Prayers, where held, were a "cultural palisade" against going native, and were logged mainly so London could show them. A Sunday service at York is the Factor's choice. Holding it pleases London; making the men attend can cost their goodwill.
- The school plan of 1806-08 for "factory boys" at York, Albany, Moose and Eastmain mostly failed. That sets a low ceiling on literacy for country-born children (a roadmap item).
- At Red River each people was meant to keep its own minister. Miles Macdonell held Catholic services and James Sutherland Protestant ones; priests came from Quebec in 1818 and John West in 1820.
- The NWC banned its men from new country marriages in 1806. That can be a `Laws.cs` rule with its own evasions, alongside "the Nor'West way" on the roadmap.
- Burley finds more abandonment, bigamy and casual liaisons than the stable country marriages Van Kirk and Brown describe. The marriage system should let a man leave a wife behind, and let that be remembered.

**Hooks.** `Marriage.cs`, `Laws.cs`, `Heirs.cs`, `Languages.cs`.

### H. The Missouri

#### 3.22 The Lakota tollway
*Source: Hämäläinen ch. 3-4.*

**Proposal.** The Lakota hold the Missouri as a toll road: a policed stretch of about 400 miles, where threats do the work violence once did. Traders pay to pass (Manuel Lisa paid half his goods; Astor's 1811 party paid). The Arikara are held as tributaries who grow corn for the Lakota. The Lakota also tell the Arikara that the St. Louis traders are the real cheats. In the 1811 agent's words, the Sioux "command the balance of power". The James River fair drew 1,000-1,200 lodges. In the War of 1812 "all the rivers are to be shut".

**Hooks.** `MissouriTrade.cs` (a toll on goods moving through), the `Sioux` and `Missouri Villages` factions.

### I. After 1821

#### 3.23 Material for the Reckoning
*Source: Williams; Galbraith; Ray ch. 11.*

- Simpson cut the workforce from about 2,000 to under 700 by 1826. He opened a post-master rank for country-born men and cut gifts and liquor.
- Of the 53 commissioned officers in 1821, 32 were former Nor'Westers.
- From 1826, beaver quotas by district; steel traps banned in 1822 as "the scourge of the Country".
- Starving trappers ate beaver and hid the pelts.

**Hooks.** `Union.cs`, `Reckoning.cs`, `YearStory.cs`.

## 4. Suggested order

1. **3.3 (middlemen and resale)**, which the owner marked essential, together with the trader half of **3.2**, since the middleman *is* one kind of trader.
2. **3.11 + 3.12 (the moral economy)**: it lands in the York year being played and uses systems already there.
3. **3.1 (overplus)** and **3.5 (credit both ways)**: small changes to code that exists, with a large effect on rivalry.
4. **3.4 (rival captains)** and the council half of **3.2**: they share the band council and the giving-out.
5. **3.17 (the engineered grievance)**: `Lies.cs` and `Laws.cs` already do the work.
6. **3.13 (the ship's captain)**: pairs with the current `Ship.cs` work.
7. **3.15 (the New System)** as London's orders.
8. **3.6, 3.7**, then **3.9, 3.10**, then the rest.

## 5. Open questions

Settled on 2026-10-30: 3.2 is decided per camp by its council and its traders, not a global dial; 3.11's dues follow Burley; rival captains (3.4) are in; 3.3 must be built.

Still open:
- **3.11:** are the men's dues per post, or per man (an old hand expects more than a new one)? Burley suggests custom is held in common at a post, with long-service men as its keepers.
- **3.3:** where are the meeting places for middlemen and far camps on the current map, and which far camps exist in 1811 (Blackfoot, Chipewyan, Plateau)?
- **3.4:** the Cree have no source yet on leadership inside a band.
- **3.19:** does the player ever go to Montreal for a trial, or is it resolved off the map?

## 6. Facts to verify (DCB first)

- Brandon House, 1811: Hugh Heney as master, William Yorston as a leader.
- The "Glasgow insurgents", William Finlay, and the Nelson Encampment winter of 1811-12.
- Dugald Cameron, and Seraphin Lamar's confession in 1815.
- The 1811 General Court vote (86/14) and the bloc's share (about 30%).
- Coltman's NWC shareholding.
- Fidler's 1815 horse counts and the 1819 epidemic figures.
- Lisa's toll and Astor's 1811 payment.
- The Ojibwe clan roles (3.4). Part of that note is marked as AI-generated and much follows a modern teaching text, so check it with a better source, and with care. The same note's migration story has some Ojibwe "becoming the Cree", which is wrong: the Cree are a separate people.
