# World reference

Every name a script can pass to the game, and where the game gets it from. A misspelt post, run or item is not an error the game shows you: it quietly answers *nothing* (a condition is false, an effect does nothing). The [checker](Command-Line-Tool) catches most of them, because it reads the same data files listed here, but it helps to know what exists before you write.

| You write | It must be | Listed below | Read from |
|---|---|---|---|
| `world.hasPost("...")`, `world.foundPost("...")` | a post on the world map | [Posts](World-Reference#posts) | `Data/World/world.json` (`places`, `kind: "post"`) |
| `world.stock("...", ...)`, `world.furs("...")`, `world.addStock("...", ...)` | a post that keeps books | [Posts](World-Reference#posts), [Ledgers](World-Reference#ledgers-which-posts-keep-books) | the same, plus York Factory and Oxford House |
| `brigade("...")` | a brigade run's name, exactly | [Brigade runs](World-Reference#brigade-runs) | `Data/World/brigade_runs.json` |
| `Item.X` | an item | [Items](World-Reference#items) | `enum ItemId` in `Scripts/Items/Items.cs` |
| `news(to: ...)` | a role or a person's exact name | [Letters: who can be written to](World-Reference#letters-who-can-be-written-to) | the cast (`Scenes/World/YorkFactoryExterior.tscn`) |
| `world.isDead("...")` | a person's exact name | [People](World-Reference#people) | the cast |
| `who:` in a quest, `player.rapport(...)`, `player.addRapport(...)` | a name, `Person.X`, or `"role@<post or camp>"` | [Quests: who](Quests#who-exactly) | the cast, posts and camps |
| `world.regard("...")`, `world.addRegard("...")`, `"chief@..."` | a band camp's exact name | [Camps](World-Reference#camps-bands) | `Data/World/world.json` (`layout.camps`) |
| `player.posted("...")`, `player.postTo("...")` | a post that keeps books | [Posts](World-Reference#posts) | |
| `Task.X` (a quest's task step) | one of the Factor's tasks | [Quests: tasks](Quests#tasks) | `enum TaskKind` in `Scripts/Player/PlayerTask.cs` |
| `world.season == Season.X` | one of four seasons | [Seasons](World-Reference#seasons-and-the-calendar) | `Scripts/Core/GameCalendar.cs` |
| `Company.X`, `world.playerFaction` | HBC, NWC, American, Crown, Free (or All) | [Companies](World-Reference#companies) | |
| `startPath: Path.X`, `startAt: "..."` | a road in; a post, town, fort, camp or place | [Who the players are](World-Reference#who-the-players-are-and-where-they-begin) | `enum StartPath` in `Scripts/Core/CharacterSheet.cs`; the map |

Names are **exact and case-sensitive**: `"the York boats"`, not `"York boats"` or `"The York Boats"`; `"Ile-a-la-Crosse"` with no accents; `"Churchill (Pr. of Wales Fort)"` with its brackets. Copy them from the tables.

## When the game starts

Unless the campaign says otherwise, a new world starts on the day the first player's path arrives, in 1811:

| Starting path | First day |
|---|---|
| HBC (the ship) | 20 August 1811 |
| NWC (the brigade) | 12 July 1811 |
| Cree, Assiniboine, another people (the summer camp) | 10 June 1811 |
| American (up the Columbia to Fort Okanogan) | 2 September 1811 |
| Crown (the King's birthday parade at the garrison) | 4 June 1811 |

### Who the players are, and where they begin

A campaign can bring every player in by one road, and set them all down at one place, in its header:

```c
campaign TheRogue(title: "The rogue", start: date(1812, 6, 4), startPath: Path.Crown, startAt: "Niagara");
```

`startPath` is the road in, the same for every character made for this world (the character page shows it fixed; each player still picks a background, traits and an ambition on it). Without it, players choose their own. `startAt` is where they are set down, by name: a post, a town, a town's fort (`"Fort George"`), a camp of the country, or a place on the map. Either can be given alone, and a campaign that `extends` another takes that one's if it gives none.

| Road (`Path.`) | Side (`Company.`) | Opens with | With `startAt` |
|---|---|---|---|
| `HBC` | `HBC` | the last day of the crossing, ashore at York Factory | no crossing: set down there, and on the books of the post there if it is the Company's |
| `NWC` | `NWC` | above the last portage with the brigade | the same, for a Nor'Wester house |
| `Cree`, `Assiniboine` | (the band) | a morning in the summer camp | set down there; a camp of the country there makes it `Nation`, born to that camp |
| `Nation` | (the band) | a morning in the camp chosen | the same |
| `American` | `American` | below the Okanogan with Mr. Stuart | set down there, and on the books of an American post there |
| `Crown` | `Crown` | **the King's birthday parade** at his garrison: the men of his mess, the sergeant, a stranger at the gate | put on the strength of the Crown garrison there (the town or its fort), or the nearest one, and still stands his parade; sent on to the place after it if it is not his garrison's |

The soldier's garrison is the town named, if it stands with a garrison of the Crown when the game begins, else the nearest that does, else York's: Fort York (York), Fort George (Niagara), Fort Malden (Amherstburg) and Fort St. Joseph (St. Joseph Island) are the Crown's in 1811; Detroit, Fort Niagara and Michilimackinac are the Americans', and Queenston has no fort. He carries a musket, powder, a capote, a knife, bread, water and two shillings, has no company post, and is fed by nobody's books but his own pack. He may go over to the other side, or to nobody's, with [`player.desert`](Effects-in-the-Game#the-player).

On his parade, the stranger at the gate offers a boat across: remembering the name raises the flag `start:knows-the-boatman`, for a campaign to follow up.

### A campaign's own start

A campaign can choose when its world begins, in its header:

```c
// one or the other:
campaign AfterTheWar(title: "After the war", start: date(1815, 5, 1));   // this day, on every path
campaign TheLateYears(title: "The late years", startYear: 1819);         // each path's own day, in 1819
```

| Header | The first day | Use it for |
|---|---|---|
| (neither) | 10 June, 12 July or 20 August **1811**, by starting path | the shipped campaigns |
| `start: date(1815, 5, 1)` | **1 May 1815 on every path**: the ship, the brigade and the summer camp all begin that day | a story that must begin on a particular day |
| `startYear: 1819` | 10 June, 12 July or 20 August **1819**, by path | a later era where each path keeps its own season |

What follows from the start is the same as for 1811: every beat dated before the first day is [history](State-Flags-and-Variables#history-before-the-start) (its flags raised, its posts founded, nothing else), and the world is that year's: the posts that stand are those founded by then and not yet given up (Norway House, founded 1814, stands in a game that begins in 1815; Fort Douglas, founded 1815, stands from 1815 on), and with `postsByFoundingYear` the later ones appear on 1 January of their years. The menu shows "Begins 1 May 1815." under the campaign's description. A campaign that `extends` another takes that one's start if it gives none of its own.

Things to know:

- The start only sets the calendar: the people are the starting cast, whatever the year. A historical figure who died before your start, or a post's master in 1811, is still there unless your script sees to it (raise a flag for a death; the beats you write are the history).
- Choose the day with the starting paths in mind: `start: date(1815, 1, 10)` puts the ship's passengers ashore in midwinter.
- A developer's `--date=YYYY-MM-DD` argument wins over the campaign's start.
- The [simulator](Simulation-Scenarios) begins on the campaign's start too (on 20 August of a `startYear`, the ship's day), unless a scenario or `--start` says otherwise, and runs ten years if its default end would come first.

### History and the first day

(Without a campaign start, the year is 1811 unless the game is started with `--date=YYYY-MM-DD`, which sets the year the starting path uses.) Which beats count as **history** is decided on that first day, once the first player is in and the path has dated the world. So:

- a beat dated **before the path's first day** is history, whether it falls in 1810 or in June 1811: see [State, flags and variables](State-Flags-and-Variables#history-before-the-start). The shipped scripts' blockade (1 June 1811) is history on every path: its flag is raised and London's prices are not touched. Selkirk's grant (12 June 1811) is history on the ship's and the brigade's paths, but happens two days into play from the summer camp, letter and all;
- a beat dated on or after the first day happens in play as usual.

This is what the simulator does with `--start` set to the path's first day (`--start 1811-08-20`, its default, for the ship; `1811-07-12` for the brigade; `1811-06-10` for the summer camp).

On a dedicated server the campaign waits, with nothing past and nothing happening, until the first player joins; history is decided on the day the first character arrives. A loaded game keeps the history it was saved with.

Because the paths start on different days, a beat dated between 10 June and 20 August 1811 is history on one path and live on another: test such beats with each path's `--start`, or date them before 10 June or after 20 August 1811 if they must be the same on every path. (A character started from developer arguments, with no starting path, keeps the raw calendar's 1 January 1811, and history is decided on that day.)

## Posts

Every post on the world map, by founding year. **Founded** is what `world.hasPost` and `postsByFoundingYear` go by: a post stands at the start if it was founded in or before the start year and is not **gone by** then (given up before the game begins, from `tools/world/extras.json`'s `post_closures`). A post founded later stands only once a beat founds it (`world.foundPost`) or, with `postsByFoundingYear: true` in the header, on 1 January of its year.

**Postmaster** is the man in charge there, exactly as `world.isDead` wants his name; he is in the world only while his post stands. **Write to** is the address a `news(to: ...)` letter can use to reach whoever is in charge there now (see [Letters](World-Reference#letters-who-can-be-written-to)). Posts with neither have nobody to write to.

| Post | Founded | Company | Gone by | Brigade stop | Postmaster (exact name) | Write to |
|---|---|---|---|---|---|---|
| `Moose Factory` | 1673 | HBC |  |  | Mr. William McIntosh | `the master at Moose Factory` |
| `Albany Fort` | 1679 | HBC |  |  | Mr. Charles Hodgson | `the master at Albany Fort` |
| `York Factory` | 1684 | HBC |  | the York boats | Mr. William Auld (the Factor) | `the Factor at York Factory` |
| `Fort Severn` | 1689 | HBC |  |  | Mr. Donald Corrigal | `the master at Fort Severn` |
| `Churchill (Pr. of Wales Fort)` | 1717 | HBC |  |  | Mr. John Heron | `the master at Churchill (Pr. of Wales Fort)` |
| `Michipicoten` | 1725 | HBC |  |  | Mr. Hugh Robertson | `the master at Michipicoten` |
| `Fort la Reine` | 1738 | French (old) |  |  |  |  |
| `Fort Dauphin` | 1741 | French (old) |  |  |  |  |
| `Henley House` | 1743 | HBC |  |  | Mr. Hugh Hughes | `the master at Henley House` |
| `The Pas (Le Pas)` | 1750 | French (old) |  |  |  |  |
| `Fort a la Corne` | 1753 | French (old) |  |  |  |  |
| `Cumberland House` | 1774 | HBC |  | the York boats, the Saskatchewan brigade | Mr. George Leith | `the master at Cumberland House` |
| `Gloucester House` | 1777 | HBC |  |  | Mr. Thomas McKay | `the master at Gloucester House` |
| `Grand Portage` | 1778 | NWC | 1803 |  | Mr. James Leith | `the master at Grand Portage` |
| `Hudson House` | 1779 | HBC | 1787 |  | Mr. Robert Charles | `the master at Hudson House` |
| `Ile-a-la-Crosse` | 1779 | NWC |  | the Athabasca brigade | Mr. Adam Fraser | `the master at Ile-a-la-Crosse` |
| `Michilimackinac` | 1780 | NWC |  |  | Mr. John McIntosh | `the master at Michilimackinac` |
| `Green Lake` | 1782 | HBC |  |  | Mr. Donald Kennedy | `the master at Green Lake` |
| `Lac la Ronge` | 1782 | NWC |  |  | Mr. Peter Stuart | `the master at Lac la Ronge` |
| `Sault Ste. Marie` | 1783 | NWC |  |  | Mr. Peter Campbell | `the master at Sault Ste. Marie` |
| `Pine Fort` | 1785 | NWC | 1811 |  | Mr. Robert Cameron | `the master at Pine Fort` |
| `Manchester House` | 1786 | HBC | 1793 |  | Mr. Charles Fidler | `the master at Manchester House` |
| `Osnaburgh House` | 1786 | HBC |  |  | Mr. David Corrigal | `the master at Osnaburgh House` |
| `South Branch House` | 1786 | HBC | 1794 |  | Mr. John Hodgson | `the master at South Branch House` |
| `Fort Esperance` | 1787 | NWC |  |  | Mr. Andrew Wentzel | `the master at Fort Esperance` |
| `Rainy Lake House` | 1787 | NWC |  | the Montreal canoes | Mr. Thomas Shaw | `the master at Rainy Lake House` |
| `Brunswick House` | 1788 | HBC |  |  | Mr. David Fidler | `the master at Brunswick House` |
| `Fort Chipewyan` | 1788 | NWC |  | the Athabasca brigade | Mr. George Henry | `the master at Fort Chipewyan` |
| `La Cloche` | 1790 | HBC |  |  | Mr. Joseph Robertson | `the master at La Cloche` |
| `Montagne a la Bosse` | 1790 | NWC |  |  | Mr. John Fraser | `the master at Montagne a la Bosse` |
| `Nipawi` | 1790 | NWC |  | the Saskatchewan canoes | Mr. Peter McTavish | `the master at Nipawi` |
| `Red Lake Post` | 1790 | NWC |  |  | Mr. Peter Henry | `the master at Red Lake Post` |
| `Swan River House` | 1790 | NWC |  |  | Mr. George Wentzel | `the master at Swan River House` |
| `Buckingham House` | 1792 | HBC | 1800 |  | Mr. Joseph Sutherland | `the master at Buckingham House` |
| `Fort Bas de la Riviere` | 1792 | NWC |  | the Montreal canoes, the Saskatchewan canoes | Mr. David McTavish | `the master at Fort Bas de la Riviere` |
| `Fort George` | 1792 | NWC | 1800 | the Saskatchewan canoes | Mr. Donald Henry | `the master at Fort George` |
| `Brandon House` | 1793 | HBC |  |  | Mr. James Robertson | `the master at Brandon House` |
| `Martin Falls` | 1793 | HBC |  |  | Mr. John Charles | `the master at Martin Falls` |
| `Fort Souris` | 1794 | NWC |  |  | Mr. Andrew McTavish | `the master at Fort Souris` |
| `Carlton House` | 1795 | HBC |  | the Saskatchewan brigade | Mr. Adam Vincent | `the master at Carlton House` |
| `Edmonton House` | 1795 | HBC |  | the Saskatchewan brigade | Mr. David Leith | `the master at Edmonton House` |
| `Fort Alexandria` | 1795 | NWC |  |  | Mr. David Cameron | `the master at Fort Alexandria` |
| `Fort Augustus` | 1795 | NWC |  | the Saskatchewan canoes | Mr. Hugh Grant | `the master at Fort Augustus` |
| `Manitoba House` | 1795 | HBC |  |  | Mr. John Fidler | `the master at Manitoba House` |
| `Portage la Prairie` | 1795 | NWC |  |  | Mr. James Henry | `the master at Portage la Prairie` |
| `Fort Vermilion` | 1798 | NWC |  |  | Mr. Thomas McTavish | `the master at Fort Vermilion` |
| `Oxford House` | 1798 | HBC |  |  | Mr. James Sutherland (postmaster) | `the postmaster at Oxford House` |
| `Turtle River House` | 1798 | NWC |  |  | Mr. John Stuart | `the master at Turtle River House` |
| `Nut Lake` | 1799 | HBC |  |  | Mr. George Hughes | `the master at Nut Lake` |
| `Rocky Mountain House` | 1799 | NWC |  |  | Mr. Alexander McLeod | `the master at Rocky Mountain House` |
| `Chesterfield House` | 1800 | HBC | 1802 |  | Mr. Peter Bird | `the master at Chesterfield House` |
| `Fort Nelson (BC)` | 1800 | NWC |  |  | Mr. James Thomson | `the master at Fort Nelson (BC)` |
| `Pembina Post` | 1801 | NWC |  |  | Mr. Peter McLeod | `the master at Pembina Post` |
| `Lesser Slave Lake Ho.` | 1802 | HBC |  |  | Mr. Magnus Fidler | `the master at Lesser Slave Lake Ho.` |
| `Fort William` | 1803 | NWC |  | the Montreal canoes | Mr. Adam Fraser (partner) | `the master at Fort William` |
| `Dunvegan` | 1805 | NWC |  |  | Mr. Donald McDonald | `the master at Dunvegan` |
| `Fort McLeod` | 1805 | NWC |  |  | Mr. Charles Henry | `the master at Fort McLeod` |
| `Fort Fraser` | 1806 | NWC |  |  | Mr. James Campbell | `the master at Fort Fraser` |
| `Fort St. James` | 1806 | NWC |  |  | Mr. John McTavish | `the master at Fort St. James` |
| `Fort George (BC)` | 1807 | NWC |  |  | Mr. Robert Stuart | `the master at Fort George (BC)` |
| `Kootenay House` | 1807 | NWC | 1812 |  | Mr. William Shaw | `the master at Kootenay House` |
| `Flathead House` | 1809 | NWC |  |  | Mr. George Shaw | `the master at Flathead House` |
| `Fort Gibraltar` | 1809 | NWC |  |  | Mr. James Grant | `the master at Fort Gibraltar` |
| `Island Lake House` | 1810 | HBC |  |  | Mr. Donald Spencer | `the master at Island Lake House` |
| `Spokane House` | 1810 | NWC |  |  | Mr. Adam Grant | `the master at Spokane House` |
| `Fort Okanogan` | 1811 | American |  |  |  |  |
| `Fort Kamloops` | 1812 | NWC |  |  | Mr. William McDonald | `the master at Fort Kamloops` |
| `Fort Manuel Lisa` | 1812 | American |  |  |  |  |
| `Jasper House` | 1813 | HBC |  |  | Mr. James Fidler | `the master at Jasper House` |
| `Norway House` | 1814 | HBC |  | the York boats | Mr. Charles Davey | `the master at Norway House` |
| `Fort Douglas` | 1815 | HBC |  |  | Mr. Hugh Thomas | `the master at Fort Douglas` |
| `Lac Seul House` | 1815 | HBC |  |  | Mr. Andrew Heron | `the master at Lac Seul House` |
| `Fort Nez Perces` | 1818 | NWC |  |  | Mr. William Stuart | `the master at Fort Nez Perces` |
| `Great Whale River` | 1820 | HBC |  |  | Mr. George Kennedy | `the master at Great Whale River` |
| `Fort Alexandria (BC)` | 1821 | NWC |  |  | Mr. Thomas Grant | `the master at Fort Alexandria (BC)` |
| `Fort Babine` | 1822 | HBC |  |  | Mr. Adam Corrigal | `the master at Fort Babine` |
| `Upper Fort Garry` | 1822 | HBC |  |  | Mr. Joseph Hughes | `the master at Upper Fort Garry` |
| `Fort Assiniboine` | 1824 | HBC |  |  | Mr. George Clarke | `the master at Fort Assiniboine` |
| `Fort Pelly` | 1824 | HBC |  |  | Mr. Alexander Hughes | `the master at Fort Pelly` |
| `Fort Colvile` | 1825 | HBC |  |  | Mr. Andrew McKay | `the master at Fort Colvile` |
| `Fort Vancouver` | 1825 | HBC |  |  | Mr. Alexander Clarke | `the master at Fort Vancouver` |
| `Fort Connolly` | 1826 | HBC |  |  | Mr. Robert Swain | `the master at Fort Connolly` |
| `Fort Langley` | 1827 | HBC |  |  | Mr. Adam Sinclair | `the master at Fort Langley` |
| `Fort Union` | 1828 | American |  |  |  |  |
| `Fort Chilcotin` | 1829 | HBC |  |  | Mr. John Corrigal | `the master at Fort Chilcotin` |
| `Fort Pitt` | 1829 | HBC |  |  | Mr. Joseph Davey | `the master at Fort Pitt` |
| `Lower Fort Garry` | 1830 | HBC |  |  | Mr. Robert Kennedy | `the master at Lower Fort Garry` |
| `Fort Clark` | 1831 | American |  |  |  |  |
| `Fort Ellice` | 1831 | HBC |  |  | Mr. Alexander Heron | `the master at Fort Ellice` |
| `Fort Piegan` | 1831 | American |  |  |  |  |
| `Fort McKenzie` | 1832 | American |  |  |  |  |
| `Fort Nisqually` | 1833 | HBC |  |  | Mr. Donald Sinclair | `the master at Fort Nisqually` |
| `Rat Portage` | 1836 | HBC |  |  | Mr. Donald Hughes | `the master at Rat Portage` |
| `Fort George (Chisasibi)` | 1837 | HBC |  |  | Mr. Andrew Bird | `the master at Fort George (Chisasibi)` |
| `Dease Lake` | 1838 | HBC |  |  | Mr. Charles Charles | `the master at Dease Lake` |
| `Fort Victoria` | 1843 | HBC |  |  | Mr. Andrew Charles | `the master at Fort Victoria` |
| `Fort Berthold` | 1845 | American |  |  |  |  |
| `Fort Benton` | 1846 | American |  |  |  |  |
| `Touchwood Hills` | 1852 | HBC |  |  | Mr. Thomas Heron | `the master at Touchwood Hills` |
| `Fort Qu'Appelle` | 1864 | HBC |  |  | Mr. Hugh Heron | `the master at Fort Qu'Appelle` |

Two names that look like posts but are not: **Grand Rapids** and **Methye Portage (Portage La Loche)** are stops on brigade roads (landmarks), so `world.hasPost` is false for them and they keep no books. **Nor'Westers' house (York)**, the rival post a few miles up from York Factory, is the NWC's house in the starting scene; it is not in the post list, so scripts cannot name it.

### Ledgers: which posts keep books

`world.stock`, `world.furs` and `world.addStock` work on a post's **ledger** (its stores). York Factory's ledger is the Company's stores at the Factory, Oxford House's is the outpost's, and every other post in the table keeps its own once it stands.

- A post that does not stand has no ledger: `world.stock` and `world.furs` answer nothing, so **every** comparison with them is false (`< 10` and `>= 10` alike), and `world.addStock` does nothing (the game logs a warning).
- `world.furs` counts the furs in store in **Made Beaver** (a prime beaver pelt is 1 MB; see [Items](World-Reference#items) for what each is worth).
- `world.stock` counts **pieces** of one item. Taking more than is there takes what there is.

Know the scale before you move stock. York Factory's stores begin the game with 90 trade goods, 60 pemmican, 120 dried goose, 30 loaves, 40 firewood, 12 rum, 20 powder and ball, 30 twists of tobacco, 12 point blankets, 6 muskets and 22 beaver pelts, among other things (the ship's cargo adds to them each August). So `-5` of trade goods is a nuisance and `-40` of pemmican is two-thirds of the Factory's supply. `Scripts/Economy/FortLedger.cs` has the full list; `TradingPostLedger.cs` is the inland posts.

## Brigade runs

The five runs in `Data/World/brigade_runs.json`. Each run is a company's boats on a named road; each **leg** is one crossing of the year, leaving on its day (MM-DD) with an **outfit** (goods and provisions from the sending post's books) or the **returns** (the furs on its books). A leg runs only while both of its end posts stand. A crossing that cannot get in before freeze-up **winters** on the bank and goes on at the ice-out.

| `brigade("...")` | Company | Craft | Boats x men | Stops |
|---|---|---|---|---|
| `the York boats` | HBC | york boat | 3 x 7 | York Factory, Norway House, Grand Rapids, Cumberland House |
| `the Saskatchewan brigade` | HBC | york boat | 2 x 6 | Cumberland House, Carlton House, Edmonton House |
| `the Montreal canoes` | NWC | canoe | 5 x 8 | Fort William, Rainy Lake House, Fort Bas de la Riviere |
| `the Saskatchewan canoes` | NWC | canoe | 3 x 7 | Fort Bas de la Riviere, Nipawi, Fort George, Fort Augustus |
| `the Athabasca brigade` | NWC | canoe | 4 x 7 | Ile-a-la-Crosse, Methye Portage (Portage La Loche), Fort Chipewyan |

The legs, with the number `launch(leg)` takes (counting from 0):

| Run | Leg | From | To | Leaves | Carries |
|---|---|---|---|---|---|
| the York boats | 0 | York Factory | Cumberland House | 12 June | outfit |
| | 1 | Cumberland House | York Factory | 8 July | returns |
| | 2 | York Factory | Cumberland House | 5 September | outfit |
| the Saskatchewan brigade | 0 | Edmonton House | Cumberland House | 25 May | returns |
| | 1 | Cumberland House | Edmonton House | 5 August | outfit |
| the Montreal canoes | 0 | Fort William | Fort Bas de la Riviere | 5 May | outfit |
| | 1 | Fort Bas de la Riviere | Fort William | 12 June | returns |
| | 2 | Fort William | Fort Bas de la Riviere | 4 August | outfit |
| the Saskatchewan canoes | 0 | Fort Augustus | Fort Bas de la Riviere | 28 May | returns |
| | 1 | Fort Bas de la Riviere | Fort Augustus | 15 August | outfit |
| the Athabasca brigade | 0 | Fort Chipewyan | Ile-a-la-Crosse | 1 June | returns |
| | 1 | Ile-a-la-Crosse | Fort Chipewyan | 12 August | outfit |

Things worth knowing when you write brigade beats:

- **Two of the roads need posts that come later.** The York boats stop at Norway House (founded 1814); the Saskatchewan canoes run to Fort George, which was given up in 1800. A leg only needs its *end* posts, so these still run.
- **Craft speed:** a canoe makes about 55 km a day upstream and 85 down; a york boat 28 up and 55 down. Short-handed, heavily loaded or hungry crews go slower, and the weather slows everyone.
- **When a crossing is late:** its due day is set when it leaves (the road's length at its craft's speed, plus the portages, times 1.3). The day after that, it is late. A crossing that leaves with empty shelves (nothing to load) does not leave at all, and is marked late at once.
- **Which crossing?** A run has several legs a year, and `brigade("...")` always means the crossing **now on the river** (or none). Its flags (`isLate`, `isRobbed`, ...) are about the latest crossing and are cleared when the next one sets out. See [State, flags and variables](State-Flags-and-Variables#flags-the-game-raises-by-itself) for exactly when each is raised and cleared, and [Effects in the game](Effects-in-the-Game#brigades) for what `delay`, `turnBack`, `lose`, `cancel` and `launch` really do.
- To add a run, add it to `brigade_runs.json`; the checker and the editor pick it up from there.

## Items

The values `Item.` takes, in `enum ItemId` order. **Worth** is the item's value in Made Beaver (MB), the HBC's unit of account; it is a fair guide to how much a script should move at a time.

| `Item.` | In the game | Kind | Worth (MB) | Weight (lb) |
|---|---|---|---|---|
| `Pemmican` | Pemmican | Food | 0.3 | 1.5 |
| `DriedGoose` | Dried goose | Food | 0.2 | 1.0 |
| `SaltGeese` | Cask of salt geese | Food | 1.6 | 60 |
| `Berries` | Berries | Food | 0.1 | 0.6 |
| `SpruceBeer` | Spruce beer | Drink | 0.15 | 2.0 |
| `Corn` | Indian corn | Food | 0.25 | 1.0 |
| `BuffaloRobe` | Buffalo robe | Clothing | 2 | 8.0 |
| `Bread` | Loaf of bread | Food | 0.15 | 1.0 |
| `Ale` | Tankard of ale | Drink | 0.2 | 2.0 |
| `Rum` | Dram of rum | Drink | 0.8 | 1.0 |
| `Water` | Water | Drink | - | 2.0 |
| `Venison` | Fresh meat | Food | 0.15 | 2.0 |
| `DriedMeat` | Dried meat | Food | 0.25 | 0.8 |
| `Goose` | Goose | Food | 0.15 | 1.5 |
| `Firewood` | Firewood | Fuel | - | 5.0 |
| `Capote` | Capote | Clothing | 3 | 4.0 |
| `PointBlanket` | Point blanket | Clothing | 4 | 5.0 |
| `HideCoat` | Hide coat | Clothing | 2 | 5.0 |
| `BeaverPelt` | Beaver pelt | Fur | 1 | 1.5 |
| `ThinPelt` | Summer pelt | Fur | 0.45 | 1.2 |
| `ShotPelt` | Shot-through pelt | Fur | 0.35 | 1.5 |
| `ShotHide` | Holed hide | Fur | 0.2 | 6.0 |
| `CaribouHide` | Caribou hide | Fur | 0.5 | 6.0 |
| `WolfPelt` | Wolf pelt | Fur | 0.6 | 3.0 |
| `TradeGoods` | Trade goods | Goods | 1 | 3.0 |
| `SteelTrap` | Steel trap | Tool | 1.5 | 4.0 |
| `Kettle` | Copper kettle | Tool | 2 | 3.0 |
| `Flint` | Flint and steel | Tool | 0.5 | 0.3 |
| `FishingLine` | Fishing line | Tool | 0.5 | 0.5 |
| `Tumpline` | Tumpline | Tool | 0.5 | 1.0 |
| `Snowshoes` | Snowshoes | Tool | 1.0 | 3.0 |
| `Axe` | Axe | Tool | 1.5 | 4.0 |
| `PackBasket` | Pack basket | Tool | 0.8 | 3.0 |
| `Hare` | Hare | Food | 0.05 | 0.8 |
| `Fish` | Fish | Food | 0.1 | 1.0 |
| `Musket` | Trade musket | Weapon | 12 | 9.0 |
| `Bow` | Bow | Weapon | 2 | 2.0 |
| `Knife` | Knife | Weapon | 0.5 | 0.5 |
| `Tomahawk` | Tomahawk | Weapon | 1.5 | 2.0 |
| `Ptarmigan` | Ptarmigan | Food | 0.05 | 0.8 |
| `Tobacco` | Twist of tobacco | Goods | 1 | 1.0 |
| `Beads` | Bunch of beads | Goods | 0.5 | 0.5 |
| `Awl` | Awl | Goods | 0.1 | 0.1 |
| `Stroud` | Yard of stroud | Goods | 1.5 | 1.5 |
| `MbToken` | Made-beaver token | Goods | 1 | 0.05 |
| `Powder` | Powder and ball | Goods | 1 | 2.0 |
| `IceChisel` | Ice chisel | Tool | 1 | 4.0 |
| `FishingNet` | Fishing net | Tool | 2 | 6.0 |

Furs as they come to the counter: `BeaverPelt` is a prime winter pelt, `ThinPelt` a summer one, `ShotPelt` and `ShotHide` were spoilt by the ball. `TradeGoods` is the general bale of the trade (the posts' main currency of goods); `MbToken` is a made-beaver token. `Venison` is shown in the game as *Fresh meat*.

## Letters: who can be written to

A `news(...)` letter travels for its `days`, then goes to the **first living person** whose name, or whose *role's address*, is exactly its `to`. If nobody fits, it **waits**, undelivered, until somebody does (a postmaster arrives when his post is founded, a successor takes a dead man's place). Nobody knows what is in it until it is read. See [Effects in the game](Effects-in-the-Game#letters) for the rest.

| `to:` | The address it becomes | Who takes it |
|---|---|---|
| `Person.Factor` | `the Factor at York Factory` | the Factor of York Factory: Mr. William Auld at the start, or whoever succeeds him |
| `Person.Postmaster` | `the postmaster at Oxford House` | the postmaster of Oxford House: Mr. James Sutherland at the start |
| `Person.Partner` | `the wintering partner` | the NWC's wintering partner at York: Mr. Duncan Cameron at the start |
| `Person.Chief` | `the chief of the band` | whichever living chief the game finds first (there are about forty, of every nation). To be sure of reaching one, write to him by name |
| `"the master at Cumberland House"` | as written | the postmaster of that post (any post in the [table](World-Reference#posts) with one) |
| `"Mr. George Leith"` | as written | that man, while he lives |

The checker warns about any address that is not one of the four roles. That is only a reminder: a name or a `the master at ...` address is fine if you know who it reaches.

`company:` is whose letter it is: `Company.HBC` (the default) or `Company.NWC`. A letter read by someone of the other company has been *intercepted*: it still counts as read (and raises `heard:`), and it is how the player can learn the other side's news.

`from:` is free text: the letter says who it is from, and nothing checks it. The shipped campaigns use, for example, `"the Governor and Committee, London"`, `"Montreal"`, `"the agents at Fort William"`, `"Lord Selkirk"` and `"the colony at Red River"`.

## People

`world.isDead("...")` is true once someone **with exactly that name** is dead. It looks only at the people in the world; a name nobody has is simply never dead, with no warning. The names are as the game shows them over people's heads, titles and brackets included:

| Who | Exact name |
|---|---|
| The Factor at York Factory | `Mr. William Auld` |
| The NWC wintering partner at York | `Mr. Duncan Cameron (NWC)` |
| The postmaster at Oxford House | `Mr. James Sutherland (postmaster)` |
| The York Factory clerks | `John Fidler (clerk)`, `Alex Kennedy (clerk)` |
| The chief of the York Cree band | `Maskwa (chief)` |
| The chief of the Assiniboine camp | `Tchatka (chief)` |
| The postmasters of the inland posts | see the [Posts](World-Reference#posts) table |
| The Métis hunt captains | `Antoine Hogue (hunt captain)` (the Forks), `Louis Bottineau (hunt captain)` (Pembina), `Alexis Lacerte (hunt captain)` (the Qu'Appelle) |
| The chiefs of the wider country | `Yellow Antelope (chief)` (Blackfoot), `Big Fox (chief)` (Blood), `Grey Wolf (chief)` (Peigan), `Askihk (chief)` (Plains Cree), `Igmu (chief)` (Assiniboine), `Ajijaak (chief)` (Saulteaux), `Hinhan (chief)` (Dakota), `Calling Bull (chief)` (Lakota), `White Antelope (chief)` (Mandan), `Two Lynx (chief)` (Hidatsa), and the rest in the cast |

**The historical figures of the wiki are not people in the world.** Lord Selkirk, Miles Macdonell, Robert Semple, Cuthbert Grant, Simon McGillivray and the others are written about in letters and beats, but nobody in the world carries their names, so `world.isDead("Robert Semple")` is never true. To make a story turn on a historical death, raise a flag in the beat where it happens (`flags.set("governor_killed")`) and test the flag.

The cast is about 940 named people. To find a name, search `Scenes/World/YorkFactoryExterior.tscn` for `DisplayName`, or look at the people in the game. Names given to children born in play, and to the men of a later successor, cannot be known when you write the script.

## Camps (bands)

The band camps of the wider country, by their exact names, which is how `world.regard`, `world.addRegard` and a quest's `"chief@<camp>"` or `reach(place: ...)` name them. `world.regard` is how the band regards the Company, 0-100.

| Nation | Camps |
|---|---|
| Blackfoot | `Blackfoot`, `Blood (Kainai)`, `Peigan (Piikani)`, `Sarcee (Tsuut'ina)`, `Gros Ventre` |
| Cree | `Plains Cree`, `Beaver Hills Cree (Amiskwacîwiyiniwak)`, `Woodland Cree`, `Swampy Cree`, `Oji-Cree` |
| Assiniboine | `Assiniboine`, `Nakoda (Stoney)` |
| Ojibwe | `Plains Ojibwa (Saulteaux)`, `Ojibwa (Anishinaabe)`, `Odawa (Ottawa)`, `Menominee` |
| Sioux | `Dakota (Santee Sioux)`, `Lakota (Teton Sioux)` |
| Missouri | `Mandan`, `Hidatsa`, `Arikara`, `Crow`, `Shoshone`, `Cheyenne`, `Ho-Chunk (Winnebago)` |
| Plateau | `Kootenay (Ktunaxa)`, `Salish (Flathead)`, `Secwépemc (Shuswap)`, `Syilx (Okanagan)`, `Nlaka'pamux` |
| Dene | `Chipewyan`, `Dene (Beaver)`, `Dakelh (Carrier)`, `Tsilhqot'in`, `Sekani` |
| Coast | `Coast Salish` |
| Metis | `Métis of the Forks`, `Métis at Pembina`, `Métis of the Qu'Appelle` |

There are also 86 family camps by the posts, named for their people and their post (`"Plains Cree family at Carlton House"`, `"Blackfoot family at Edmonton House"` ...): look them up in `Data/World/world.json` under `layout.camps`. The York Cree and Assiniboine bands of the starting map are separate: their chiefs are `Person.Chief` (Maskwa) and `"Tchatka (chief)"`; their camps are on the world map as `"Cree winter camp (York)"`, `"Cree summer camp (York)"`, `"Assiniboine winter camp"` and `"Assiniboine summer camp"`.

## Seasons and the calendar

`world.season` is the season of the river at York Factory, by the month:

| `Season.` | Months | What it means |
|---|---|---|
| `Winter` | November to April | the Bay is frozen; travel by snowshoe and sled |
| `Breakup` | May, June | the ice goes out, the geese come, the rivers open |
| `OpenWater` | July, August | the ship can reach the Bay |
| `FreezeUp` | September, October | the ship must leave; the last brigades come in |

For anything finer, use months: `world.inMonths(6, 8)` is June to August, and `world.inMonths(11, 3)` wraps round the year's end (November to March). `world.month`, `world.day`, `world.dayOfYear` and `world.year` are numbers to compare; `world.date` compares with a `date(...)`.

The real freeze and thaw in the game come from the weather and differ from place to place (the Red River opens in April while York is still frozen); `world.season` does not: it is the calendar's season, the same everywhere.

## Companies

| | |
|---|---|
| `Company.HBC` | the Hudson's Bay Company |
| `Company.NWC` | the North West Company |
| `Company.American` | the American companies (Pacific, Missouri, American Fur), and the United States' garrisons |
| `Company.Crown` | His Majesty's garrisons: a player on the `Crown` road, or one who went over to them |
| `Company.Free` | nobody's: a player who deserted and went over to no one (`player.desert()`) |
| `Company.All` | everyone (for `say` and `shout` only; the default) |

`world.playerFaction == Company.NWC` is true if **any** player works for the NWC (in multiplayer there may be both). The Cree and Assiniboine starting paths are none of these, so every comparison is false for them. A player who deserts changes side at once: `world.playerFaction`, `say(..., Company.X)` and a questline's `forCompany` follow the side they are on now. `world.standing` is the standing of the local player, or 0 on a dedicated server.

## See also

- [Effects in the game](Effects-in-the-Game): what each effect does to these posts, runs and people.
- [State, flags and variables](State-Flags-and-Variables): the flags the game raises on its own.
- [Library reference](Library-Reference): every function and member.
