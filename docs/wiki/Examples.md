# Examples

All of these are in `tools/CampaignTool/examples` (small ones) and `tools/CampaignTool/src` (campaigns the game ships). Open them in the [web editor](Web-Editor), or read them here. Each one compiles and checks clean; a test keeps it that way. For short patterns rather than whole campaigns, see the [Cookbook](Cookbook), which ends with a complete eight-beat campaign explained part by part.

## 1. A first beat

`examples/01-first-beat.camp`: the smallest useful campaign. A header, a fixed beat that sends a letter on a day, and a second beat that waits for the first.

Learn: the header; `beat Name(title:, date:) { OnActivate() { ... } }`; `news(...)`, `say(...)`; `[Requires]`.

## 2. The life of a beat

`examples/02-the-lifecycle.camp`: every one of the nine hooks, once, in a single beat about a pemmican embargo, with comments saying what each is for. Read it next to [Beats and the lifecycle](Beats-and-the-Lifecycle).

Learn: `AwakenWhen`, `OnAwaken`, `OnApproach`, `ActivateWhen`, `OnActivate`, `ResolveWhen`, `OnResolve`, `CancelWhen`, `OnCancel`; `window:` and `lead:`.

## 3. Brigades on the rivers

`examples/03-brigades.camp`: reading and acting on the brigades. A beat that notices the York boats are late and escalates (or is relieved when they come in); a beat that slows the Montreal canoes during a war; a beat that stands the canoes down for good when the companies unite.

Learn: `brigade("...").isLate`, `.daysLate`, `.isIn`, `.delay(n)`, `.cancel()`; `repeat:`; `world.inMonths`; `world.date`.

## 4. Counters, helpers and loops

`examples/04-counters-and-helpers.camp`: the things that make a long script short. A variable that counts incidents, a `void` helper and a `bool` helper, a `foreach` that writes a ship's-letter beat for each year, `random` with weights, `after` with a block, and an `if` on a compile-time constant.

Learn: `int x;`, `x++`, `const`, `void f(...) {}`, `bool f() => ...`, `foreach (int y in a..b)`, `random(option(...))`, `after(days(n), () => ...)`.

## 5. Every word

`examples/05-every-word.camp`: every condition and every effect in the library, used once. Not a campaign to play: it is the reference page written as a script, and it compiles, so it cannot go out of date without a test failing.

## The Pemmican War (historical)

`src/historical.camp` (installed as `Campaigns/historical.json`). The 1811-1821 arc as the game plays it: thirteen dated beats (the blockade, Selkirk's grant, the war, the Proclamation, Seven Oaks, the union) followed by nine beats that act on the brigades. About 450 lines.

## Blood on the Bay (action from the first hour)

`src/blood-on-the-bay.camp`. Thirty-three beats, and the first lands the moment you step off the ship at York Factory (20 August 1811): a squall swamps the outfit, thieves are at the landing two days later, the Nor'Westers are at the gate by the 28th, the first brigade is ambushed on the Hayes within a fortnight, then a fire, a raid on the stores, hunger and a siege. A test holds it to *no lull longer than a week* before winter.

It is also a campaign you can end: `player.did("truce")` turns the siege into a standoff, and riding a brigade in (`"escort"`) breaks the blockade of the river and brings relief. Two simulator worlds show the roads: `blood-on-the-bay.scenario.json` (nobody keeps the peace) and `blood-on-the-bay-peace.scenario.json`.

Learn: how to make a campaign dense (a `foreach` of weekly troubles, dateless beats woken by what the world did, `kind: Kind.Fixed` on a dated beat that has `OnApproach` pressure but must happen anyway), and branching with `CancelWhen`.

## The Long Peace (a branching campaign)

`src/the-long-peace.camp`. Thirty beats, 1811-1821. The same grant, the same settlers and the same blockade as the historical script, but the war is only a possibility. If the player brokers a truce (`player.did("truce")`) or the tension is low, the Pemmican Proclamation becomes a *bargain*, the colony is not dispersed and Seven Oaks is *averted*; if not, the historical beats are waiting on the tension and happen as before. Two simulator worlds show both roads: `the-long-peace.scenario.json` (a truce) and `the-long-peace-at-war.scenario.json` (none).

Learn: `player.did`, `CancelWhen` as a branch, `rumour:`, a `foreach` that writes the seasons (freshet and low water, rolled with `chance` and `random`).

## The Rivers Run (a brigades campaign)

`src/the-rivers-run.camp`. Eighteen beats about the York boats, the Saskatchewan brigade, the Montreal canoes and the Athabasca brigade: late, lost, robbed and wintering, an early ice each autumn, and a counter (`troubles`) that makes the Committee ask questions. Riding a brigade in or carrying a packet (`player.did("escort")`, `"courier"`) is noticed by the boatmen.

Learn: brigade conditions (`isLate`, `daysLate`, `isRobbed`, `isWintering`) and effects (`delay`, `turnBack`), the full lifecycle on a repeating beat, and counters.

## The Company's Servant (a story with quests)

`src/company-servant.camp`. The HBC story's first chapter, and the example to read for [quests](Quests). It `extends: "historical"`, so the Pemmican War's beats run underneath, and adds three quest lines for HBC players:

| Line | Quests | What it shows |
|---|---|---|
| *The Company's Servant* | `Landfall` (report to the Factor, carry the cargo, report back), `UpTheRivers` (go up with the York boats), `Edmonton` (report to the master at Edmonton House) | `talk` with `Says` and choices, a `task` step (`Task.CarryCargo`, `Task.Brigade` with `convoy:`), `reach`, `player.postTo` |
| *The Other House* | `Neighbours` (call on the Nor'Westers at Fort Augustus), then `QuietArrangement` or `SpoilingTheTrade` | a three-way choice that sets a flag (`nwc_friendly`, `nwc_hostile`), quests offered by a `giver` that wait on those flags (`StartWhen`), `goto` between steps |
| *The Company's Friends* | `TobaccoAndTalk`, `TheInvitation` (the Beaver Hills Cree, and their hunt) | `bring`, `hunt`, `world.addRegard`, a role at a camp (`"chief@Beaver Hills Cree (Amiskwacîwiyiniwak)"`) |

One beat closes the chapter: `HorsesTaken` wakes when `TheInvitation.done` (a beat waiting on a quest) and raises `beaver_hills_raided` for the next chapter to take up. Named people are given as constants at the top (`const string master = "leader@Edmonton House";`), which keeps the steps readable.

## The Open Country (free roam)

`src/open-country.camp`. A header and nothing else: `extends: "historical"` with no quest lines, so the history runs and every player, of any company or band, makes their own way. The shortest campaign that ships, and the pattern for a free-roam game.

## The Company Years (the long one)

`src/company-years.camp` (installed as `Campaigns/company-years.json`). Fifty-nine beats over twenty years, 1811-1830, in five acts:

| Act | Years | What happens |
|---|---|---|
| I | 1811-13 | The blockade; Selkirk's grant; the settlers *winter at York Factory* and eat the Factory's pemmican; the war of 1812; the colony's first summer; Astoria sold |
| II | 1814-16 | The Pemmican Proclamation; peace in Europe; the canoes go hungry; the colony dispersed and restored; **Seven Oaks, which can be averted** by a quiet spring; Selkirk takes Fort William |
| III | 1817-20 | Selkirk's treaty; the trials at York; three summers of interior war and ambushes on the brigades; Grand Rapids; Selkirk dies |
| IV | 1821 | The union; the canoes stood down; Simpson's first council at York Factory; the voyageurs let go |
| V | 1822-30 | Simpson to the Columbia; the great flood at Red River; the Company without a rival |

It is the biggest script the game ships and it is written to show the language: every lifecycle hook is used, `foreach` writes a ship's letter for every year and the three years of the interior war, helper functions send the letters, and two pairs of counters keep a running opinion of how the Company is doing (`hardYears`, `goodYears`) that later beats read.

Things worth reading it for:

- **`SevenOaks`** has a `CancelWhen`: if the spring of 1816 is quiet enough (`world.tension < cold` after 1 May) the beat is cancelled, whatever the adherence, and an `OnCancel` says so. Cancelling is how a campaign lets the world *change the story*.
- **`YorkBoatsOverdue`** uses `AwakenWhen`, `OnAwaken`, `ActivateWhen`, `OnActivate`, `ResolveWhen` and `OnResolve` together (see [Beats and the lifecycle](Beats-and-the-Lifecycle)).
- **`CommitteeLosesPatience`** reads counters another beat keeps: three bad years, then a reckoning that resets them; `CancelWhen` makes it stand down if the returns recover.
- **`InteriorAmbushes`** rolls dice (`chance(25)`, then `random(option(...))`) to decide how the brigades are set upon.

To see it run: choose the `company-years` world in the editor's Simulate tab, or

```
dotnet run --project tools/CampaignTool -- sim tools/CampaignTool/src/company-years.camp \
    --scenario tools/CampaignTool/examples/company-years.scenario.json --log
```
