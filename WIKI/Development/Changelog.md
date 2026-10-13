---
title: Changelog
aliases: [Commit History, Version History, Development Log]
tags: [development, history]
status: implemented
---

# Changelog

**Status:** Implemented (reconstructed from `git log`). The repository has **87 commits** in its history, all dated **10 to 12 October 2026** (the project's earlier life, including the September 2026 overnight build of the York Factory test world, predates this history and is described only in `docs/york-factory-test-world.md`). Because the whole history sits inside one month, it is grouped here by month (October 2026) and then by day and theme. Hashes are the first seven characters. Many of the commit messages are long, essay-style design notes with design-doc section numbers; the entries below condense them.

> [!info] Reading the log
> Several sessions worked in parallel on a shared tree ("five sessions share this working tree"), so commits are sometimes snapshots of other sessions' work. One pull request (#1, `fur-trade-simulation`) was merged on 11 October at 16:06 and a final merge of `master` on 12 October.

## October 2026

### 10 October (39 commits): the country, the goose, the brigade

**World scale and geography**
- `2416f99` **The whole play area at half scale.** The Pemmican Empire atlas geography (coasts, lakes, rivers, ecozones, every post) is imported to a cell map (`Data/World/ground.png`, hand-paintable) and the country is painted in chunks round whoever is looking, the editor included. York Factory, the Nor'Westers, Oxford House, the camps and the wildlife sites are fitted onto dry ground near their real positions; the river road runs up the Hayes.
- `adc11ab` **The world at twice the scale** (40 px a km): 1002 x 2361 cells. The country's peoples are in it: a camp for each atlas nation where it was in 1811 (Cree, Ojibwe, Assiniboine, Sioux, Blackfoot, Dene, Plateau, Coast and Missouri peoples), and a homeguard family outside every post: about 700 people against the posts' 200.
- `2e3529b` Snapshot of the other sessions' trading posts across the map (`Posts.cs`, `TradingPostLedger.cs`, posts appear by founding year), campaign hooks, the settings panel, menu theme and keybinds.
- `f79c2ff` **York Factory** laid out after G. Ray's plan and the post survey: palisade on the north bank of the Hayes estuary, the Depot, a boardwalk to the landing, the Indian trading shop, houses, mess, stores, men's house, boat-building shed, Sloops Creek with a ford, powder magazine.
- `658d0b3`, `216fd63`, `a489c13` The Hayes portages published as fractions along the river road; the goose marsh follows the fort onto the bay shore beyond Sloops Creek.

**Goose hunt and brigade (design doc 14.1, 16.1, 12.1-12.2)**
- `223527e` **The goose hunt.** From 20 August the marsh holds the whole passage; the master turns the establishment out with a count in the orders (so many casks in the cellar before the flight goes over); a volley into a settled flock is a bag of eight; eight birds to a cask; what is in the cellar at the end is what the post eats until spring, reckoned in weeks of food. **The brigade:** twice a year the outfit goes up and the returns come down, with portages, hull wear, spruce gum and freeze-up. See [[Goose]], [[Brigade]].
- `55e909b`, `e415b77`, `fd93368`, `e8826bc` Hunters sleep out at far grounds judged by the walk; the cellar target is the hard three months, not five; brigades are provisioned for the whole leg; inland posts with no marsh get no verdict.
- `2f7e81b`, `9a753e5` **Brigade crews are real men** off the post's books for the journey, eating from the canoe, starting homeward on arrival; a brigade in flight saves and loads with the world.
- `fa2b1f0` The journal shows the cellar and the flight countdown.

**Hunting, hunger and scurvy**
- `6b53631` **The deer pass, they are not farmed.** A barren-ground herd fills the country in September, October and spring, leaves a quarter behind, and quits a ground worked past bearing. See [[Caribou]].
- `f4ca253`, `b7428db` **Fishing through the ice** and a faster scurvy trigger (18 cases of scurvy in one simulated winter said the old ones were too slow).
- `46c42bb`, `ce4cde5` Background people and men on the water get "a day of being alive": fever runs its course into immunity, days without fresh meat mount (27 "fever" deaths had been starving men).

**Characters, systems and story**
- `f3d5df1` **A named rival for your seat** (design doc 3.7): weighed weekly like the Factor would; three answers (outwork him, ask the Factor, carry him something true). See [[Rivalry]].
- `2799150`, `00a9b3f` **NPCs lie too** (P.10) and you can deny what's said about you. `f62ef42` consequences now say their cause.
- `227f889`, `f64906a`, `7873f04` **Campaign scripts** (design doc 2.3): none, the historical 1811-1821 arc, or your own JSON; adherence Gentle, Steady, Firm, Strict. See [[Campaigns and Adherence]].
- `89c619a`, `eb5ddc6` **Character sheet** (design doc 5.4): four start paths, backgrounds, traits, ambition; **every man's year closes** at his own moment (the ship, the canoes for Fort William, the ice going out). First cut of `YearStory.Write`. See [[The Year Told]].
- `426b191`, `7a49e06`, `8671c1e` Dead masters, streamed tiles never stored in the scene, main menu over the painting of York at dusk.

**Art**
- `e30ed0f` **New character art** (voyageur, NWC partner, country wife, sailor, clerk, Cree captain, children; side walks and work animations; `tools/sprites`). `b02a866` **New animal art** (caribou bull and cow, wolf, goose, beaver; hare and ptarmigan cut ready). `0a7d14d` item icons for all 36 items plus 12 more. `2cea7c4` props, vehicles and HUD art. `0dc6469` `docs/asset-prompts.md`.

### 11 October (44 commits): peoples, travel, sound, the plains

**Peoples and character creation**
- `efbe2c9`, `d186ba6`, `b7610cf`, `62d6cd9` **Born to any band** in the country (fifth path "Nation"); the new-game page continues to "Who you are"; joining a multiplayer game makes a character too, arriving by the Company sloop from Churchill when the ship isn't in. See [[Multiplayer]].
- `dbb2be9` **The Métis are a people of the country**: three band camps (the Forks, Pembina, the Qu'Appelle) under hunt captains.
- `8a69ec7` **Seven new character sheets** (Blackfoot, Chipewyan Dene, Coast Salish, Dakota, Mandan, Plateau, Ojibwe) and the Factor's own sheet; berry, corn and spruce beer icons; berry bushes as flora.
- `c590c95` (12 Oct) Ten languages, with kinship between them (Michif half Cree; Dakota and Nakoda Siouan; Blackfoot Algonquian but unintelligible to a Cree speaker). See [[Languages]].

**World and posts**
- `abfbe30` The country is **fogged** until you have been there; click the map to set a waypoint. `78460fc` **Travel mode (G)** across the whole country at about 25 km a day. See [[Travel and Wayfinding]].
- `0fbf834` **Each region looks like itself**: prairie, tall-grass, aspen parkland, tundra, Shield, mountain, Hudson Bay lowlands, with region-appropriate flora.
- `7daad36`, `73db7bd`, `0db6f9a` **Fort William** becomes the Nor'Westers' capital; major posts from `Data/World/post_plans.json` (Moose Factory, Edmonton House, Cumberland House, Fort Chipewyan, Fort Gibraltar); a clerk keeps to his own counter.
- `527793a` **Routes and landmarks**: five brigade routes, 83 named places, a cleared landing at every portage. `5766984`, `5f366be` posts given up before the start no longer stand; three portages on the Hayes, corrected from sources (Rock, Borwick's Falls, Trout Falls). See [[Routes and Portages]].
- `97d1298` You **come to the post** from a ship in the roads and "Step ashore"; no one can begin in the river.
- `02e55d9` York is **whitewashed log inside a stake palisade** (walls and fence from the Gemini sheet; `tools/sprites/walls.py`).

**Economy and survival**
- `b488764` **Berries, spruce beer and the Missouri gardens**: the camps gather berries July to September; posts brew spruce beer; corn from Mandan, Hidatsa and Arikara towns is traded out. `15d3d96` art prompts for these.
- `868ec1f` Camps keep to their own country (329 background band people died of scurvy in one simulated winter when 700 were sent to one herd).
- `b5a3b9e` The man after your seat acts against you too (a word with the master, a true or false report, taking up the work you refuse).
- `45af70a` The pipe comes before the goods in barter.
- `58aa1fe` **Hares and ptarmigan** live in the country; snares draw down the thicket; tobacco, beads, awls, stroud, made-beaver tokens, powder and ball, ice chisels and nets become items. See [[Hare]], [[Ptarmigan]].
- `0cb7d40` **Game across the whole country**: 611 more wildlife sites (beaver, goose, caribou, wolf, ptarmigan, hare); a household knows the grounds of its own country (about 600 km). `d303251` a hungry house looks further but not off its own country. See [[Species Sites]].

**Performance and fixes**
- `f5d4ecf` "Who is near a point" answered from a grid (a 14-hour skip fell from 270-600 ms a frame to about 3 seconds total). `5766984` path searches that just failed aren't re-run for two seconds. `5f366be` a live man stops thinking when nobody can see him (7 fps to 145 fps in the travel repro).
- `b2064f4` Winter exposure halved; laying firewood lights it with flint; god mode in the test console. `c870fa4` name boards outside every gate, clock at 30 s a game hour (a day in twelve minutes). `97cdaeb` the "start crash" wasn't one: dead scene map removed. `22e7d6a`, `5e7f483` a duplicated dictionary key in the plural table took the whole game down; now built from a list.

**Sound**
- `6937ae2` **The first music and effects**: six Gemini tracks and 30 CC0 Freesound clips on Music, Ambience and Effects buses. `28e5d12` thirteen further sound prompts and a corrected inventory. `5bfae14` synthesised wolves, plank steps, rain, body fall and musket reload. See [[Audio]].

**Buffalo**
- `9110382` **The buffalo, as far as the code can go without the herds**: `Species.Buffalo` (260 health), 400 lb of meat as seventy pieces, a robe; `BestBigGame`; "there is no horse in the game at all". See [[Buffalo]].

**Merge**
- `fbda8c1` Merge of pull request #1 (`fur-trade-simulation`). `b1a9b57` post plans and exterior scene committed as found.

### 12 October (4 commits): the plains

- `893eb9f` **Buffalo on the plains**: 18 herds near the plains peoples' camps (Blackfoot, Blood, Peigan, Sarcee, Assiniboine, Nakoda, Dakota, Lakota, the Missouri villages, Crow, Shoshone, Cheyenne and the three Métis camps), wintering in the parkland, out on the open grass April to October.
- `c590c95` Every nation its own tongue (ten languages); a herd stands where the year has it (a game begun in July no longer finds buffalo in winter quarters).
- `e683b05` The buffalo graze out on the plains, a journey from the camps that hunt them, instead of in the middle of the Métis camps.
- `bb59c3a` Merge of `master`.

## Summary by theme

| Theme | Representative commits |
|---|---|
| World and map | 2416f99, adc11ab, 527793a, 0fbf834, abfbe30 |
| Posts | f79c2ff, 7daad36, 73db7bd, 2e3529b |
| Hunting and ecology | 6b53631, 58aa1fe, 0cb7d40, 9110382, 893eb9f |
| Food and hunger | 223527e, f4ca253, b7428db, b488764, 868ec1f |
| Transport | 223527e, 2f7e81b, 78460fc, 527793a |
| People and social systems | f3d5df1, 2799150, dbb2be9, c590c95 |
| Characters and start | 89c619a, efbe2c9, d186ba6, b7610cf |
| Campaign and story | 227f889, eb5ddc6, fc2872a |
| Art | e30ed0f, b02a866, 0a7d14d, 2cea7c4, 8a69ec7, 02e55d9 |
| Audio | 6937ae2, 28e5d12, 5bfae14 |
| Performance | f5d4ecf, 5766984, 5f366be |

(Also: `17538cd` / `2db8273` docs for character creation and joining; `fc2872a` the year told as a story; `077a674` favours survive a save; `d7efbbd` leverage reads "I know you robbed the traps".)

## See also

[[Development Status]] | [[Roadmap]] | [[Architecture]] | [[Asset Inventory]] | [[Credits and Licenses]]
