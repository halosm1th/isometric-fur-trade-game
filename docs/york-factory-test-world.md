# York Factory test world — what's in it and how to drive it

*Written after the overnight build session (Sept 2026). Everything here is placeholder art and first-pass numbers; the point is that the loops run end to end so they can be watched and tuned.*

## Playing together

The main menu offers **Play alone**, **Host a game** (port 7777, up to 8), **Join a game** (ip or ip:port) and **Run a dedicated server**. Solo is just a host with nobody else connected - one code path.

- The **host/server runs the whole simulation**: every NPC and animal brain, the ledgers, the ecology, the clock, the weather, the ship. Clients render what the server tells them and send requests.
- A client **owns its own player's movement**; everything that touches the world (E, hotbar, attacks, trade, talk) is a request the server carries out, with the result sent back as a notice. Needs, inventory, standing and task are mirrored to the owner a few times a second.
- **Time skips are agreed**: sleeping in a bed or pressing Z marks you as turning in; the clock jumps only when everyone alive has. The HUD shows "Turning in: 1/3". Moving cancels yours.
- **Tab** opens chat; Enter sends; Esc cancels.
- The F1 console belongs to the host.
- Late joiners get the world state on arrival (agents, open doors, felled trees, clock, weather).
- Death: R asks the server to respawn you at the gate as a new hand (−5 standing, fresh kit).
- Windows will ask once to allow Godot through the firewall when you host; friends on the internet need port 7777 UDP forwarded or a VPN like Tailscale/Hamachi.

Dev args: `--solo`, `--host`, `--server` (dedicated), `--join=ip[:port]`, `--name=Name`. On a client `--spawn=depot` asks the server to move you there (debug builds), `--talk` opens a conversation after 4 s, `--provoke` picks a fight.

## Controls

| Key | Action |
|---|---|
| W A S D / arrows | walk |
| Shift | sprint (1.7× speed, ~2.4× hunger) |
| Ctrl | sneak: half speed, animals notice you at less than half the distance, a first stroke on an unaware target does 2.5× |
| E | context action: talk to a person · **bed** (sleep till 07:00 at night, rest an hour by day) · pick up · loot a body / butcher a carcass · woodpile (stack or take firewood) · warehouse (stow goods/furs) · stores counter (draw a ration when short) · ship (shoulder cargo) · open/close door · look at a fire |
| 1–9 | use hotbar slot (eat / drink / feed a fire / ready a weapon / read clothing) |
| Mouse wheel + Enter | select and use hotbar slot |
| Left click | attack whoever is under the cursor with your best weapon (knife, tomahawk, bow, musket) |
| F | attack the nearest hostile |
| Q | drink (from water, or melt snow at a fire in winter) |
| Z | wait an hour; at night, sleep until 07:00 |
| T | hold for fast-forward (×40) |
| Tab | chat |
| J | journal: what you have witnessed, dated |
| F1 | test console (host only) |
| F5 / F9 | quick save / quick load (host only; clients are reloaded and re-seated) |
| R | restart after death |
| Esc | close a menu |

The Factor gives you a task each morning (bottom-left). Doing it raises your standing with the Company; ignoring it costs a little.

## The clock

10 real seconds = 1 game hour (day = 4 min, year ≈ 24 h of play). Time runs on the calendar autoload; agents scale their walking speed with the clock so fast-forward and the `Z` skip still resolve their days (a crude version of the design doc's lazy resolution).

Start date: 1 January 1811. Ship arrives 20 August, sails 15 September.

## The world (Scripts/World/WorldLayout.cs)

80 × 320 cells (10 240 px square). Everything is placed by cell in `WorldLayout` — buildings, wharf, camp, sites, trail — and the two painters regenerate the map from it on load (`AutoGenerate = true`). To hand-edit: untick **AutoGenerate** on both `Ground` and `YSort/Structures`, save, paint.

- **York Factory**: palisade with a gate; **Trading room** (Factor, clerks, hearth, counter), **Warehouse** (fur bales, cargo), **Men's house** (labourers and the post's women, hearth). Woodpile in the yard.
- **Wharf** running out onto the bay; the ship ties up at its end.
- **River** down the east side; three **beaver lodges** on it.
- **Cree camp**: five tipis round a fire, west along a trail from the gate.
- **Forest** south and west (trees are passable but slow; woodcutters fell them).
- **Sites**: caribou yard (SW), goose marsh (NE shore), beaver lodges, wolf den (far SW).

## People (Scripts/World/Population.cs)

Everyone is an `Npc` on a goal stack (`Scripts/Agents/Goals`). Tier 0 needs (warmth, thirst, hunger) interrupt anything; the most urgent one wins. Being attacked or seeing a predator pushes fight-or-flee.

- **HBC**: Mr. William Auld (Factor), two clerks, six Orkney labourers, three women. Live in the post, draw rations from its stores.
- **Cree band**: chief Maskwa, four hunters, four women, two unmarried daughters (Kimiwan — the chief's; Wawatew — Wapistan's). Live at the camp, draw on the band's cache.
- **Ship's crew**: eight, spawned when the ship docks.

Roles decide what an idle agent does (`RoleBehaviour.cs`): the Factor hands out **morning orders** (cut wood / hunt / unload the ship / drive off animals) and replans when the ship docks; labourers do their assigned task; women and clerks tend the fires; Cree hunters go out to the sites in the mornings (more often when the Company's price is high); the chief carries furs to the post when there's a load, or buys food when the camp is hungry.

Death is permanent. A dead Factor is succeeded by the most ambitious clerk; a dead chief by a hunter. If all the post's men die, "the post has fallen".

## Economy (Scripts/Economy)

- **FortLedger**: provisions, trade goods, furs (in Made Beaver), muskets, blankets. `PeltPrice` = goods paid per MB of fur (London's incentive). `QuotaMb` = what London expects on the ship.
- **BandLedger**: the camp's meat, hides, firewood, trade goods.
- Trade: Cree furs → goods at `PeltPrice`; Cree buy food from the post with goods (post keeps a reserve for its men).
- **LondonCommittee**: when the ship sails, reviews returns vs quota and writes next year's price and quota (short returns → higher price; fat returns → lower price, higher quota). Toggle in F1.

## Ecology (Scripts/Ecology)

Each site has count, capacity, browse, and a breeding structure. Weekly tick: logistic growth; over capacity eats the browse → **scarcity** → hungry animals range toward the post and attack. Hunting below the sustain floor destroys the breeding capacity → site **dead for good**. Wolves grow with prey and starve without. Animals are promoted to live agents near the player (or when in scarcity), demoted when far.

## Ship (Scripts/World/Ship.cs)

Arrives on the calendar event with the outfit, docks, crew spawn, the Factor reads the orders an hour later (price/quota take effect on the read), everyone carries cargo to the warehouse, then furs aboard. Sails on 15 Sept if ≥ 3 crew are alive; otherwise she's stranded.

## Knowledge (design doc 10 / 16.4)

Wildlife sites are known per household. The band knows its country; the post starts knowing only the goose marsh and the nearest beaver lodge. The Factor can only send men to sites the post knows. Ways to learn: HBC men stumble on a site while out; you ask a Cree hunter who thinks well of you (rapport ≥ 15) "about the hunting", then "Tell him where the game is" to the Factor (+3 standing per site).

## Band standing

`BandLedger.StandingWithCompany` (0–100, starts 50). Fair trades raise it; a hungry camp under a poor Company price lowers it fast; a Cree killed by a Company man costs 25. Below 20 the chief stops bringing furs to the post. Slider in F1.

## Weather

Rolled from the season a few times a day: clear / overcast / snow / **blizzard** / rain / gale. Blizzards double-plus exposure outdoors and dim the light; nobody starts a hunt or a wood trip in one, and hunters turn for home when one comes on. Buttons in F1.

## Roofs

Each building has a roof layer drawn above everything that fades out while you are under it. It's the design doc's "roof is a property of the cells" made visible.

## Marriage (design doc 8)

Talk to the chief or a hunter with a daughter: "Ask about marriage in the custom of the country" explains it and shows your progress. You need rapport ≥ 30 with the father (talk, feed him when hungry, give gifts) and ≥ 8 MB worth of gifts given to *him* (a point blanket is 4, a musket 12). Then "Ask for X's hand". HBC rule: one wife. She moves into the trading room and keeps house.

## Talking

E on a person stops them and opens topics: how things stand, about them (role, temper, family), the others (gossip, scaled by how talkative they are), the hunting (Cree), marriage (Cree men), work (Factor), tell him where the game is (Factor), trade (clerks, Factor, chief), a gift, offer food to someone hungry (big rapport), threaten (they may hand something over, or fight). They walk off if you do.

## Combat

Left click / F. A reload bar shows over your head while your weapon cools down; anyone hit shows a health bar for a few seconds. Hitting someone makes their armed friends within earshot come at you, and costs standing if they're Company men. Sneaking (Ctrl) up on a grazing animal and knifing it is the cheap way to a caribou.

## The Nor'Westers (design doc 5.2, 7.1)

A North West Company wintering post stands across the river (a palisade and a house; partner Duncan Cameron and three voyageurs, red coats). No ship: a **brigade from Fort William** restocks it on 12 July and carries its furs off; the partner sharpens his price if his returns were poor. The Cree chief **chooses where to sell** each trip: price × how the band feels about that company ÷ the walk. Furs that go to the Nor'Westers show up in the Factor's morning grumble and in London's next orders. The only ways across the river: the **ford** south of the camp, the **ice** in winter, or a **canoe**.

Talk to Cameron: "Ask about signing on" then "Sign on" and you are a Nor'Wester — the Company's counter refuses you, its people drop 25 rapport, your standing resets, and the Factor's tasks stop. The Factor will take you back once he thinks something of you again.

## Canoes

Two birchbark canoes: one at the shore by the wharf, one at the Nor'Westers' landing. E next to one pushes off; paddle on water (1.5× walking); step onto any bank and it stays there. Frozen in from freeze-up to breakup.

## Geese

The marsh empties in November and fills again in April — no goose hunt in winter.

## Water (design doc 17.2)

Water is an item now. Each household keeps a barrel (`Water` in the stores); women fill it - kettles to the shore in open water, snow melted at the fire in winter (costs fuel) - and everyone drinks from it when they're at the counter. You start with a copper kettle: **Q** (or using the kettle) at the water's edge drinks and fills it (3 water); at a fire in winter it melts snow. Carry water on long trips or you'll die of thirst in the bush like the first voyageurs did in testing.

## Rum (design doc 13.4)

Rum and ale carry intoxication (35 / 12), which wears off at 8 an hour. Drunk: slower, worse aim, and NPCs quarrel - two drunk men within reach may brawl (fists, a few blows). Men take an evening dram if the household has drink (talkative and disloyal ones more), and a man still drunk at first light does no work. **Trade**: posts pay part of a fur's price in rum when they have it - the Nor'Westers 35%, the Company 15% - and every dram raises the band's standing with that post and its **taste** for rum, which pulls its furs toward whoever has drink. Gifting rum doubles the rapport gain and they drink it on the spot. This is the doc's alcohol trap, in miniature.

## Fever (design doc 13.5)

When the ship docks there's a one-in-three chance a sailor has fever. It spreads between people standing together (more so indoors), lasts 6-11 days, costs ~8 health a day and appetite, weakens the arm. Survivors are immune for the year. Talk to someone feverish and you're told to keep your distance.

## Fire (design doc 18.4)

Fire is an object on a wooden cell. It spreads to wooden neighbours each second (fast in clear weather or a gale, slow in rain and snow), hurts anyone standing in it, and after ~25 s burns the cell to nothing: no wall, no floor (no shelter), no roof, no bed. Starts: a hearth stuffed past 15 h of fuel throws sparks; or a man with **flint** (in your kit - use it from the hotbar on a wooden floor or beside a wall). Arson is witnessed and answered: anyone whose house it is who saw you comes at you armed, and the story travels. Put it out with water (using a water item near flames throws it on; 55% per throw). Men of a burning household fetch water from the barrel and fight it; everyone runs from flames at their feet. F1 has a "set the men's house alight" button.

## Fishing (design doc 14.4)

A fishing line is in your kit: use it at the water's edge in open water (45% for 1-2 fish, a 3 s cooldown). Women fish for the pot when food runs short. Fish spoils like fresh meat.

## Pelt quality and rank

Beaver trapped from June to September come out as **summer pelts** worth 0.45 MB; winter pelts are prime at 1 MB (design doc 14.4). When the ship sails, the Factor's report rates each Company player from their standing: on notice (< 15), labourer, clerk (≥ 35, +6 goods), postmaster (≥ 60, +12). Shown after your standing on the HUD.

## Age (design doc 9.4)

Everyone has an age (Factor ~50, chief ~48, labourers 20s-30s, daughters 16-20); New Year adds one, and past 45 people slow a little each year. "Ask about them" tells you.

## Trapping (design doc 16.1, 16.3)

Beaver are trapped, not hunted. Steel traps are items (the post has 10, the band 8, the ship brings 12). Use a trap from the hotbar while standing at a beaver lodge to set your line (up to 6 at once); traps catch overnight against how thick the lodge is; E at the lodge lifts the catch (pelt + meat per beaver) and leaves the traps. Cree hunters and, in winter, the Factor's men run their own lines. Anyone checking a lodge with other parties' traps may rob them (the less loyal, the likelier); the owner finds the traps robbed, and witnesses remember who did it.

## Memory and rumour (design doc 10.2, 10.4, 11.2)

Events (deaths, blows struck, trades, traplines set or robbed, letters carried) are remembered firsthand by everyone within ~650 px, and passed on when people stand near each other — each retelling one hop less direct, fading after four. "Ask about the others" now draws on what that person actually knows, hedged by how they know it ("I saw it myself" / "I was told" / "They say"). Hearing that someone robbed traps or struck a friend lowers their opinion of the culprit.

## Credit (design doc 13.3)

A band in good standing (> 40) that comes to the post hungry without goods is advanced food on credit; the debt is settled first out of the next furs they bring. `BandLedger.DebtMb`.

## Letters (design doc 10.1)

One task the Factor may hand you: carry his letter to the chief. It takes effect when the chief reads it — the band is invited to trade (they come with what furs they have), standing +6.

## The rivalry (design doc 4.1, 7.1, 15.1)

`Economy/Rivalry.cs` holds one number, **Tension** (0–100), between the Company and the Nor'Westers. It rises when the band's furs go to the other counter (¼ of the MB paid), when a servant of one company strikes one of the other (6), on a raid (12), and when a party turns back at a guarded door (3); it falls 1.2 a day and 35 on a truce. At **70 it is open hostility**: each leader, at morning orders, may send two or three armed men to *seize the other post's stores* if his own are short (or Tension ≥ 90, or he was robbed himself) - `RaidGoal`: walk to the rival's storeroom, take six lots (pemmican, dried goose, rum, goods, furs, muskets), walk home, deposit. One party out at a time, at most every five days per post. The robbed house sounds the alarm: the **armed fighting men** (labourers, voyageurs, hunters - not the Factor, partner, clerks or women) turn out **only if they have the numbers**; outgunned, they stand aside and remember. Raiders who arrive to find muskets and the odds against them turn back unless brave. A robbed post posts a **guard** at its storeroom door for the next days (`GuardGoal`), and a guard who sees a raider with the odds on his side shoots. Men badly hurt fighting other men break off and run; a beaten man running is let go. Every raid is a Chronicle event; the Factor complains of lots lost.

You: E inside the other company's storehouse **plunders** four lots (any time - without open hostility it costs standing too) and raises the alarm; leaving with them counts toward the *Raid* task. Talk to your own leader: *Propose seizing the … stores* (needs Tension ≥ 40; he weighs ambition, courage, your standing, whether he was robbed) - he sends men with you and sets you the task. Talk to the other side's leader: *Propose a truce* (Tension ≥ 35; rapport, his ambition, whether he was just robbed). Readout, slider and "raid now" buttons in F1 under *The rivalry*; `--tension=N` sets it at start.

## Carrying (design doc 12.4, 12.5)

Everyone has a **carry capacity**: 50 lb, 65 for labourers and hunters, 110 for a voyageur, 45 for women. Over it you slow (down to 35% speed at +160 lb) and burn more while walking. Packs add to it: **tumpline** +40 lb (you start with one; the stores hold a few), **pack basket** +30. The HUD shows load/capacity and "(overloaded)".

## The herd (design doc 16.5)

The caribou site is now a **herd**: each month it makes for that month's ground on a seasonal route (wintering in the bush south of the camp, north to the open ground west of the fort for summer, back in autumn), moving about 40% of the way each week, with a little drift so it is never a timetable. Fixed sites (lodges, marsh, den) don't move. Knowledge of where it is goes stale: each household (and you) holds a **believed cell** per site; hunters walk to where they believe the herd is, and if the ground is empty they **search** for up to four hours - those who know the country (the band, the Nor'Westers) cast toward this month's ground, the Company's men guess along the season - then go home and the household writes the herd off for the day. Anyone passing within the herd's radius updates their household's belief; the Cree chief's "where the game is" tells you the current place; telling the Factor passes your belief on. Only witnesses know the herd moved (a Chronicle event, not a notice). F1: *March the herd toward this month's ground*; `--herdmoved` starts it on this month's ground with everyone still believing the winter one.

## The band's seasons (design doc 6)

At **Breakup** (5 June) the band strikes camp and pitches the tipis on the trail beside the post (`WorldLayout.SummerCamp`, 23,100) for the trading season; at **Freeze-up** (10 November) they go back to the winter camp in the bush (14,140). The cells, the campfire hearth, the cache, the woodpile, everyone's bed and work cell move; clients and late joiners get the repaint. `WorldLayout.CampCentre` is wherever they are now.

## Orders and company (design doc 3.9)

Talk to one of your own company's men (labourer or voyageur) with rank *clerk*/*postmaster* or standing ≥ 35 and you get **Give orders**: cut wood · hunt at the best known site · work the trapline · stand guard at the stores · come with me · carry on as the Factor set you. He weighs your rank, his opinion of you and his loyalty to the chain of command; a sulky man says he takes his orders from Mr. Auld. Anyone else who isn't a leader, clerk, sailor or woman can be **asked to come with you** (rapport ≥ 20 across factions, ≥ 5 in your own; kin always) - `FollowGoal`: they keep a few paces behind for the day, fight whoever attacks you, and go home if they lose you or you say "that will do". Take a Cree hunter to the herd, or two labourers across the river.

## Scurvy (design doc 13.5)

Everyone counts days since fresh meat (venison, goose, fish - dried and pemmican don't count). Past 42 days it is scurvy: ~3 health a day, slower, "gums black" in conversation, a HUD flag; fresh meat cures it. The Factor treats three or more men a month without fresh meat as a food shortage and sends hunters. `--scurvy` starts the post a day short of it.

## Recruits (design doc 5.5)

When the ship docks she lands enough Orkneymen to bring the post back to six labourers (green lads, 17–24); the July brigade does the same for the Nor'Westers' three voyageurs. Dead men's beds get filled.

## Save and load

`Core/SaveGame.cs`. **F5** writes `user://saves/quicksave.json` (`%APPDATA%\Godotpp_userdata\IsometricFurTradeGame\saves\`); **F9** reloads the world from it. The map regenerates from `WorldLayout`; the save restores what has drifted: the date and weather, tension, the camp's position, every ledger and household (stores, prices, quota, debts, standing, known sites and beliefs, raid dates), every site (count, breeding, browse, position, traplines), hearth fuel, open doors and felled/burnt cells, picked-up items, canoes, the ship (state, position, cargo, hold), every NPC (position, needs, kit, age, fever, personality, appearance, spouse/father/children, rapport, memories, assigned task, dead where they lay) and every player by name (needs, kit, standing, rank, task, known sites, journal, spouse). A player who joins later gets their character back when their name matches. Clients are told to reload and re-seat themselves. Main menu: *Continue alone* / *Continue and host* when a quicksave exists. Dev: `--load`, `--autosave=SECONDS`, `--autoload=SECONDS`.

## Food chain

Fresh meat spoils (~12% a day in the cache, half that in winter). Women at the fire dry it: 3 fresh → 2 dried, which keeps. Rations come out of the plainest food first.

## F1 test console

Calendar (speed, skip hours/days/months, set date, blizzard/clear/gale), Player (needs, invulnerable, give items), London & the post (pelt price now / next ship, quota, stores, +goods/+food/empty food), The rivalry (tension, raid now), Save (F5/F9), Cree band, Wildlife (site report, ×2, ÷2, force scarcity, wolf pack at the gate, starving caribou at the gate), Ship (summon, depart), People (everyone's goal and needs, teleports).

## Command-line dev args (after `--`)

`--spawn=depot|warehouse|menshouse|wharf|berth|camp|caribou|geese|beaver|wolves|x,y` (e.g. `31,86` is the yard) · `--hour=H` · `--date=YYYY-MM-DD` · `--speed=SECONDS_PER_HOUR` · `--trace` (print everyone's state every game hour) · `--ship` (bring the ship in now) · `--wolves` · `--scarcity` · `--tension=N` · `--herdmoved` · `--follow` (nearest person follows you, then you're moved outside the gate) · `--scurvy` · `--load` · `--autosave=S` · `--autoload=S` · `--provoke` · `--talk` · `--blaze`

## Known gaps / next

- Art is placeholder everywhere new (trees, tipis, ship, women/factor recolours, item icons).
- Roofs are flat placeholder planes; the front wall still fades when you're inside.
- Player's own weapons have no swing animation; shots are a line and a flash.
- Delegation is a fixed playbook, not a planner; no message objects yet (orders take effect immediately, except London's, which wait for the ship).
- Balance numbers (need rates, site sizes, prices) are first guesses. Use `--trace` runs at `--speed=1` to watch a week in a few minutes.
