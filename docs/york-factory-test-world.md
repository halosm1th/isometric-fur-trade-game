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
| Left click | with a **weapon** selected on the hotbar (or nothing): strike whoever is under the cursor · with an **item** selected: use it on the spot - firewood lays a fire ring (or feeds a hearth), flint lights a ring/hearth or, on wood you don't own (own post: crouch first), sets it alight; food/drink/tools are used · otherwise it interacts |
| I | your pack: **Wear / Take off** clothing and snowshoes, **◄ ►** reorder (slots 1-9 are the hotbar) |
| F11 | full screen (also in the main and pause menus; the UI is laid out at 1280x720 and scales, so 1920x1080 is ×1.5) |
| F | attack the nearest hostile |
| Q | drink (from water, or melt snow at a fire in winter) |
| Z | wait an hour; at night, sleep until 07:00 |
| T | hold for fast-forward (×40) |
| Space | stop / start the clock (host only; everyone's clock follows; bodies stop too) |
| [ / ] | slower / faster clock - doubles or halves the seconds per game hour (host only; shown on the calendar) |
| Tab | chat |
| J / L / I | your book: **Journal** (what you witnessed, dated) · **Log** (everything the world announced, timestamped - the old scrolling top-left log now lives here; only the newest line or two flash on screen) · **Pack** (every item with weight, worth and what it does) |
| F1 | test console: now also a **World log** section with the last 30 notices (nothing flashes in the corner any more) |
| Esc | pause menu: resume, key bindings (read from the InputMap), save, main menu, exit. Alone, time stops; hosting or joined, the world goes on |
| M | map: coast, river, posts, the road, the camp where it stands, the sites you know (a herd where you last heard of it), other players, the ship. **Drag to pan, wheel to zoom**; opens centred on you |
| H | help: controls and the gist |
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

*Ask about the hunting* works on Cree hunters and the chief, Company labourers, voyageurs and the partner: with rapport ≥ 15 they tell you the sites **their household** knows, at the places their household believes them to be.

## Roofs

Each building has a roof layer drawn above everything that fades out while you are under it. It's the design doc's "roof is a property of the cells" made visible.

## Marriage (design doc 8)

Talk to the chief or a hunter with a daughter: "Ask about marriage in the custom of the country" explains it and shows your progress. You need rapport ≥ 30 with the father (talk, feed him when hungry, give gifts) and ≥ 8 MB worth of gifts given to *him* (a point blanket is 4, a musket 12). Then "Ask for X's hand". HBC rule: one wife. She moves into the trading room and keeps house.

## Talking

E on a person stops them and opens topics: how things stand, about them (role, temper, family), the others (gossip, scaled by how talkative they are), the hunting (Cree), marriage (Cree men), work (Factor), tell him where the game is (Factor), trade (clerks, Factor, chief), a gift, offer food to someone hungry (big rapport), threaten (they may hand something over, or fight). They walk off if you do.

## Life out of sight (design doc 18.2)

The background batch has to be livable, or the far places quietly die while the player is elsewhere. A background household now gathers its own firewood (a camp with no cordwood used to freeze to death), melts snow at the fire for water, hunts its own country if it is a band, goes looking further when it has under a fortnight's food (and comes back knowing where the game is, which is real knowledge it keeps), falls through from an empty trapline to hunting instead of standing about, and keeps enough on snares and lines not to starve. Oxford House buys country provisions off the inland bands with its goods when its shelves get thin, before it buys furs. A man who freezes now dies of cold rather than of want.

## A named rival for your seat (design doc 3.7)

`Agents/Rival.cs`. Declare that you mean to rise and some other ambitious man at the post turns out to want the same seat, by name: *"John Fidler is after the clerk's desk as well as you - he keeps the books and the Factor's ear."* Each week both of you are weighed the way the Factor would weigh you - standing, rank, what the master thinks of you, and for the player the work he has done and refused - and when the lead changes hands the player hears so, with the reason being whatever the other man actually did that the post would be talking about.

Three things you can do about it, all through the ordinary verbs. **Outwork him** (the score is your own record). **Ask the Factor for the seat**, weighed by `Negotiation` like any other request and harder while the other man is ahead. Or **carry something true about him to the Factor** - but only something you saw yourself or had in your journal, and a man who runs another down had better be believed: if the Factor takes it the rival's standing falls and he learns who told, and if he doesn't, you lose standing and both of them remember it.

## The brigade (design doc 12.1, 12.2, 18.3)

`World/Brigade.cs`. Twice a year the river road carries a brigade: **the autumn outfit** up to Oxford House once the ship has been unloaded (goods, muskets, blankets, traps, rum and salt geese), and **the spring returns** down as soon as the ice is out (the winter's furs). A brigade is men, canoes and a manifest moving along the road - the open water resolved in numbers at about a twentieth of the road a day upstream and twice that coming down, slower with a heavy load, a thin crew or a storm, and frozen in where it lies if freeze-up catches it short.

The four portages of the Hayes - the Rock portage, Borrowick Falls, Trout Falls and the Hill Gates - are where things happen to it. The canoes come out of the water, the pieces go over on men's backs (two days at the heavy ones), and **anyone who means to stop you knows exactly where you will be**: with bad blood between the houses the other company may be waiting, and takes half the manifest into its own store, raising the tension and landing in your journal with the cause named - *the other house knew which day you would be on the carrying place*. The director paces those interceptions like any other heavy beat.

Hulls wear on the rapids and on the drag of the portage itself. Spruce gum patches hold them (a brigade carries one patch per three men); a hull that opens with no gum left loses a third of the manifest in the river and costs two days. The crew are real men taken off the post for the journey: no morning orders, no rations out of the store, not at the fire, their bodies carried along with the canoes and out of sight - which is what makes sending a brigade cost the post its hands for a month or two. They eat out of the canoe, so the house loads provisions as well as trade goods, and a brigade that set out light is on short commons on the water; they fish and snare at the carrying places, which is thin and not enough on its own for a long haul. Landing at the far end puts them ashore there, and the men whose own post is the other end start back down the river as a homeward brigade of their own, empty, still off the books until they land. Anyone walking the road beside a brigade counts as with it and hears everything at first hand.

A brigade on the water is part of the world's record: her position, her hull, her gum, her manifest, what the men have left to eat and which men they are all save and load, so a quicksave halfway up the Hayes comes back halfway up the Hayes ("2 brigades are still on the river"). The men are reclaimed by name on load and taken off the post's books again.

## The deer pass, they are not farmed (design doc 16.2, 16.4, 17.1)

A barren-ground herd was never a resident stock a post could live off: it passes. The site's logistic regrowth yields about twelve deer a year on a capacity of 140, and five households cannot live off twelve deer, so the old herd was always going to be hunted out of the country by spring - which is exactly what the long runs showed.

`SpawnSite.HerdPassage` makes the herd seasonal instead. In **September and October** the country fills to capacity ("the deer are passing - the country is full of caribou") and in **April and May** to seven tenths of it; when the main herd goes on, the ground keeps **a quarter of capacity** as the deer that winter there. So October is fat, February is lean, and the post that filled its cellar during the goose flight gets through while the one that did not has to buy provisions off the bands or go short.

**Scattering is now a thing that happens rather than a line of text.** A ground worked past its sustain floor is quit outright - the deer are gone, and nothing is to be had there until the next passage. Orders stop sending men to it while it is bare, because the notice went round the country, so nobody walks a day out to empty ground.

The demand side needed no change once supply was seasonal: a house with a full cellar is not short of food, so it issues no hunting orders at all, and what sends men out in midwinter is men going too long without **fresh** meat - which is the scurvy pressure, answered by hares, fish through the ice and the beaver lodges rather than by the herd.

## The goose hunt (design doc 14.1, 16.1)

`World/GooseHunt.cs`. The autumn flight is the one event in the year a Bay post cannot afford to miss, and it is now the year's second climax after the ship. From 20 August to 15 October the marsh holds the whole passage going south ("the water black with geese"), the master turns the **whole establishment** out to it - not two men - and the orders carry the count: *"84 casks in the cellar before the flight goes over, or we eat short"*, with the days of the flight left. A volley into a settled flock is a bag of eight birds with a musket, four with a bow, and that many come off the marsh.

Fresh geese spoil; **casks** do not. The women of the post salt eight birds to a cask (`SaltGeeseGoal`, and the same in the background batch), a cask holds about a week's food for a man, and a household with nothing loose on the shelf breaks one open. What is in the cellar when the flight goes over is what the post eats until spring, so the reckoning at the end of the season - casks salted against casks wanted, in weeks of food - is a cause the player can point at in February, written into the journal as a reveal and headlined if the post goes into the winter short.

The player gets his own place in it: the Factor's orders include *"to the marsh while the flight lasts: bring 12 geese to the stores"*, counted as he hands them in at the counter.

A hunting trip to a far ground is no longer a day trip either: whether a man sleeps out is judged by the walk - his own pace, the distance to the ground and a quarter added for the road not running straight, against a nine-hour working day with a couple of hours at the game in it - so he stays out where the game is (the post kept goose tents at the marsh) and comes in when he is loaded rather than when the light goes, and the rule still means the same thing when the map is redrawn - and, the bug that hid all of this, **a hunting party outside the live ring is no longer parked back at its own door with the day's work undone**, which is why nothing had ever come back from the goose marsh.

## Provisions at a post, and what a compressed clock does (design doc 13.1, 18.2)

Besides the hunters' kills and what band parties sell at the counter, a post whose store falls under 25 days' food now sends out for meat once a week: a few pieces of goods for pemmican or dried meat from any band that will still deal with it, named in the console with the days' food in hand. A post with no provisions and nobody selling says so, and one with no goods to buy them with says that.

A man in his own yard with food on the shelf no longer starves when a day passes faster than he can walk in and eat, and above about six times ordinary motion (fast-forward, a long time skip) **everyone's** day is resolved in numbers rather than on foot, as a background person's already was. Every death now names its cause: of hunger, of the cold, of the fever, of the scurvy, or killed by whoever did it.

**Known balance limit:** at an ordinary clock three weeks of February pass at York Factory with nobody dying. Driven at twenty-five times that (`--speed=0.1`, a day every two and a half seconds), the post and the camp still lose people to hunger over a month or more of simulated famine - forty mouths need more than the live hunt and the weekly purchases deliver at that rate. Judge food balance at a human clock; the compressed runs are for watching events, not for reading the provisions economy.

## Your own book (design doc 3.6.1, 3.9 step 8)

Furs bought at a camp with your own goods, or your own goods sold there, is private trade, and it is recorded where it happens: whoever is within earshot remembers it, and the word travels by the ordinary rumour rules. If it has reached the Factor by the time the ship sails, his report names it and your standing falls by four and three a time, with the cause shown in your journal - the same shape as the river crossings he cannot account for. Trading without the words now costs a fifth more on the lot instead of a piece on every article.

Dev args `--quitafter=S` ends a run cleanly (a run killed from outside dies mid-print and leaves a scary-looking CLR line in the log that means nothing), and a crash in one branch of the talk no longer slams the door: the conversation says the fault is in the game and offers Back, with the exception on the console.

## Provisions that move (design doc 13.1, 17.1)

The Assiniboine make pemmican to sell, so a camp sitting on more than its own winter's worth (thirty pieces, or three a mouth) takes up to twenty pieces down to whichever house will have it and takes goods for it - the Company's if they will deal with the Company, else the Canadians'; if neither will, the country hears that they have pemmican to spare and nobody to sell it to. A background house now fishes through the ice where it has a line (Oxford House is issued three), and a house with nothing in the store goes on **short commons** - thin over weeks rather than dead in days - with a headline out after eight days of it, so there is time for somebody to do something about it.

## The year's account (design doc P.3, P.4)

`Core/Reckoning.cs`. The trade year closes when the ship sails, and that is when the player's own book is written up: what the post shipped against its quota and whether you had a hand in it, your rank and standing with the change on last year, the work set you that you did and refused, what your own account stands at, your wife and children, anyone of your house who died this year, every declared ambition measured against what you actually did, and how things stand between the houses. Nothing in it is knowledge you did not have. The counters reset with it, so next year's entry is about next year.

The journal is no longer a log of other men's chores: events of no weight to anyone are kept out of it unless the player is in them, and it holds sixty entries rather than forty. `--dumpbook=S` prints it to the console.

## The director (design doc 2.2)

`Core/Director.cs`. Simulate, then curate. It never makes an event, never changes an outcome and never hands anyone knowledge the propagation system would not have carried; it only biases things that were going to happen anyway.

- **Who talks about what.** Of the memories two people both witnessed, and of the stories a man could tell at the fire, the one that touches the player's own business comes out first - his name, his wife, his house's dead, the men who hold a death against him or he against them, and the words of whatever he has declared he means to do.
- **Whose route crosses whose.** Among game sites a household rates within 12% of each other, hunting and trapping parties take the one nearer a player. The party would have gone to one of them regardless.
- **When latent pressure surfaces.** A blood debt coming for the player and a raid across the river ask the director first. A heavy thing wants room either side of it (about 29 hours for a raid), the same kind of trouble does not come twice inside a day and a half, nothing lands within three hours of the last piece of news, and a long quiet stretch (two days with no trouble) makes it readier to let the next one through. "Not now" always means not yet, never no.

Dev arg `--nodirector` switches it off; the test that keeps it honest is that the world must still be valid with it off, because every beat it staged would have happened anyway. `--trace` prints its pacing state.

## The barter (design doc 3.6.1)

`Economy/Barter.cs`. Trade is a session with a man, not a shop. Opening it reads out up to three **tells** - what you can actually perceive of his position: thin children at the fire, little hanging in the smoke, he asks after guns before anything else, he is already in the Nor'Westers' book, his eyes go to the kegs, he glances at the calendar with the warehouse not full, the stores are thin, more packs behind him than he means you to count. A sharper trader (trade skill, or standing with him) sees more; without the words you get only what his body shows, and pay a piece more besides.

Goods go over the counter **by the lot**, which is where the haggling shows. **Opening with a gift** (the ceremony came before the goods) moves the price and buys patience; skipping it at a camp is one of the tells he can read on you. **Press him for a better price** spends his patience whether it lands or not - the odds are your trade skill, his regard for you, how badly he needs what you carry, whether you opened with a gift, and how many times you have already asked. **Allow that he has a point** gives back a little of the margin for a lot of the patience. Push past the end of it and he breaks off: the camp man gathers his furs and the Nor'Westers' house is across the river; the clerk closes the counter to you for six hours, and the band marks the insult. **Holding back** - telling him the muskets are not for trade while his people are at war - costs you with the band and lands in your journal, and selling them the gun lands there too.

His patience, the price as it stands and his mood are all in words ("He is beginning to shift his feet"), never numbers. Every round teaches the trade skill a little.

## Fights are won before the first shot (design doc 15.6)

`Agents/Nerve.cs`. A smooth bore at the edge of its reach mostly throws the ball away and at ten paces it is a butcher's tool: a ranged shot's chance of missing now grows with the square of the range you take it at, eased by the shooter's hunting, worsened against a running man, and cut to a fraction when a sneaking man fires on someone who has not seen him (on top of the sneak stroke's own damage). Every man carries an **on his guard** timer - a shot heard anywhere within 1500 px, a blow taken, or a fight he is already in sets it for 25 seconds - and while it runs he cannot be taken unawares. Firing on a house that was not on its guard leaves it **shaken** for a quarter hour, which counts against every nerve check its men make.

A man weighs whether to **stand**: his courage, how many of his own are in sight against how many of theirs, whether anyone of rank is there to give orders, his own wounds, a musket in his hands rather than a knife, his own country, rum, and whether the house is shaken. Allies now turn out on that reckoning instead of courage alone. When a man **falls or is walked off a prisoner**, everyone who saw it weighs it again, and those whose nerve fails drop what they are doing and run together for a quarter of a minute - a party that breaks, with the console line to say who ran and a chronicle event others will retell. Prisoners and sleepers are not counted; they have nowhere to run.

Dev args: `--brawl` sets three of the Company's armed men against three Nor'Westers at the gate; `--provoke=12` keeps you swinging at whoever is nearest every two seconds.

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

**Shot pelts**: an animal killed with a musket yields a *shot-through pelt* (0.35 MB) or a *holed hide* (0.2) instead of the prime skin; traps, bow, knife and tomahawk leave it whole. NPC fur hunters carrying a bow sling the musket for the lodge. **Small game**: `ForageGoal` - men snare hares along the edge of the bush (no site drawn down, thin fare, counts as fresh meat); the Factor sets men to it when there's no caribou or goose to be had, and a beaver lodge is only shot for food when it's thick.

**Flint** (hotbar): beside a cold hearth it lights it (uses a firewood); in the open with firewood in your pack it builds a **campfire** on the next clear cell - a hearth like any other (feed it, warm at it, melt snow at it), saved and replicated; **crouched (Ctrl) against wood** it is arson.

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

## Messages (design doc 10.1.1)

`Core/Message.cs`: one message object - kind, from, to, subject, text, whose company's paper it is, a payload, and two effects: **OnRead** (the intended one) and **OnIntercepted** (what a rival learns). Nothing happens until `Mail.Open` - the read event. `Mail.Abroad` holds the abstract legs (ship to London, brigade to Fort William) and delivers when the hours run out; `Agent.Mail` is the papers a person carries; loot a body and you take its papers. Written so far (`Letters`): London's **directive** (price + quota; the Nor'Westers reading it undercut the Company), the year's **accounts** (London reviews them ~75 days after the ship sails and writes next year's directive from them), the council's **mandate** to the partner (carried by the brigade), the Factor's **letter to the chief** (now a real object in your pack), and **rumours** (the blowback of a sold packet, days in transit). The ship carries the directive; the bos'n walks it up to the Factor (`DeliverMailGoal`), who sits at his desk to open it (`ReadMailGoal`). Kill the bos'n and the packet is on his body; hand it to Cameron, or **sell** either company's packet to the other leader (8 goods for a directive/mandate, 3 for a letter) and he reads it on the spot - with a 40% hidden chance that word reaches the Factor a few days later (standing -20). A Factor with low loyalty and some ambition keeps back a tenth of the beaver from the hold (witnessed; gossip). The Pack tab lists your papers; F1 shows the directive text and the post's mail log.

## Delegation (design doc 3.9)

`Agents/Delegation.cs`. Each morning the leader (Factor or partner; the same code) builds the day's **objectives** from what he has in hand: threats (urgency 100), the ship (90), a guard after a robbery or with tension ≥ 60, provisions when food is under 30 days or three men are a month without fresh meat, the **returns quota at the pace the ship's deadline leaves** (trapline men in winter, more when behind), wood. Men are scored per task on capability (musket, courage, knowing the ground, health) and **trust** (loyalty + rapport); a **suspicious** leader keeps key tasks for men he trusts, an **ambitious** one keeps his most ambitious man off the hunts. Each man then **decides** (loyalty, ambition, hunger, the task's dignity; the ship and wolves brook no argument): willing, grudging (an hour's dawdle first), or **refuses** (rapport both ways, a Chronicle event, "he'll not cut wood today"). Men back from a hunt **report** to the leader in person (`ReportGoal`: what they took, how thick the ground was → the post's site belief updates from the report). A man sent out who **hasn't come back** by the next morning gets someone sent to look (`LookForGoal`: finds him, his body, or nothing, and reports). New hands off the ship carry the order *report to the Factor* (`ReportForDutyGoal`) - the disloyal find the rum first, the sociable the men's house - and get a real assignment when they arrive. You start with the same order (talk to Mr. Auld). Your tasks carry the leader's **intent** and a deadline; at the Factor you can *ask what London wants*, **decline** (standing -3 to -6 by his temper), **ask for a different task**, or **volunteer** (carry his letter, hunt, guard).

## The Fort William council (design doc 3.8, 3.8.1)

`Economy/Council.cs`. Six councillors (the Montreal agents' man with 6 shares, five wintering partners with 1–2 - Cameron is the Bay's), each with ambition, suspicion, loyalty and standing. On the Brigade event (12 July) the council sits off-map: the **agenda comes from what the room knows** (the Bay rivalry is only tabled if Cameron carries memories of blows, or tension is high), each councillor leans on each option from his interests, **standing pulls the undecided**, and **shares decide**. Items so far: the price against the Company (hold / undercut by a fifth / leave it to the Bay partner), the outfit for the district (60/90/130 pieces - the zero-sum one), rum (sparing/free), the Bay (peace/press - sanctioned raids lower the raid threshold), and Cameron's share (censure / confirm / a second share, by his returns). A losing partner with ambition and little loyalty **defies** the price vote. The minutes are a message the brigade carries; Cameron reads them at his desk; sold or looted, they tell the Factor the Nor'Westers' whole plan. A Nor'Wester player with standing ≥ 20 can **write to the partners** before July (weight ≈ standing/25 of a share) and ask Cameron what was decided. F1 → *The rivalry* shows the councillors, the last minutes and letters going up; a button convenes it at will. *This is a first cut; the user is revising the council design in another document and it will be refreshed to match.*

## Rumour distortion and the tongue (design doc 10.4, 10.6, 11.5)

Retellings now bend (`MemoryEntry.Retell`): a **suspicious** teller inflates ("and worse than that, they say", weight ×1.6); a **talkative/careless** one puts the wrong man of the same people in the story, or shifts the day ("or thereabouts"). One shift per telling, never all; eyewitnesses mostly tell it straight. **Language**: you have a Cree fluency (0.1 at start; +0.02 per talk, +0.3 on marriage; Pack tab shows it). Under 0.3 the weighty topics with the band (marriage) are closed to you; under 0.5 trade with the band costs a piece more and a third of what they tell you comes through garbled. An **interpreter** - your wife or a woman of the post within 300 px - counts as fluent. **Seen across the river**: a Company man inside the Nor'Westers' palisade (or the reverse) is a witnessed event once a day; at ship time the Factor rolls the year's unexplained sightings into standing (-4 each, up to -25). Tell him first (*"I'll be across the river a few days"*) and for five days the sightings file as authorized.

## Blood debts (design doc 7.4)

`Agents/Grudge.cs`. Kill one of the band (spouse, father, children, the leading man and the hunters all take it up) and each carries a **grudge**: a debt in goods (16 MB for a hunter, 24 for the chief, 14 for a woman) or blood. An armed hunter with nerve who sees the debtor within 450 px goes for him; the chief and the women wait to hear what is offered. Talk to a holder: the greeting names the debt, and *Offer restitution* (trade goods to the debt) settles the whole family's claim (+12 band standing, +25 rapport). The debtor's death pays it in blood; a holder's death passes it to the next hunter. Married in, **your wife's people's debts are yours to help settle** - the option appears against other men's killings too. Pack tab lists debts against you; F1 lists them all.

## Letters (design doc 10.1)

One task the Factor may hand you: carry his letter to the chief. It takes effect when the chief reads it — the band is invited to trade (they come with what furs they have), standing +6.

## The rivalry (design doc 4.1, 7.1, 15.1)

`Economy/Rivalry.cs` holds one number, **Tension** (0–100), between the Company and the Nor'Westers. It rises when the band's furs go to the other counter (¼ of the MB paid), when a servant of one company strikes one of the other (6), on a raid (12), and when a party turns back at a guarded door (3); it falls 1.2 a day and 35 on a truce. At **70 it is open hostility**: each leader, at morning orders, may send two or three armed men to *seize the other post's stores* if his own are short (or Tension ≥ 90, or he was robbed himself) - `RaidGoal`: walk to the rival's storeroom, take six lots (pemmican, dried goose, rum, goods, furs, muskets), walk home, deposit. One party out at a time, at most every five days per post. The robbed house sounds the alarm: the **armed fighting men** (labourers, voyageurs, hunters - not the Factor, partner, clerks or women) turn out **only if they have the numbers**; outgunned, they stand aside and remember. Raiders who arrive to find muskets and the odds against them turn back unless brave. A robbed post posts a **guard** at its storeroom door for the next days (`GuardGoal`), and a guard who sees a raider with the odds on his side shoots. Men badly hurt fighting other men break off and run; a beaten man running is let go. Every raid is a Chronicle event; the Factor complains of lots lost.

You: E inside the other company's storehouse **plunders** four lots (any time - without open hostility it costs standing too) and raises the alarm; leaving with them counts toward the *Raid* task. Talk to your own leader: *Propose seizing the … stores* (needs Tension ≥ 40; he weighs ambition, courage, your standing, whether he was robbed) - he sends men with you and sets you the task. Talk to the other side's leader: *Propose a truce* (Tension ≥ 35; rapport, his ambition, whether he was just robbed). Readout, slider and "raid now" buttons in F1 under *The rivalry*; `--tension=N` sets it at start.

## Ambitions (design doc P.2)

Esc → **What you mean to do**: declare up to two — rise at the post, marry into a family of the country, grow rich on private trade, drive the other company off this river, keep the band fed and whole. Each is read off real state (rank and standing; a father's regard and the gifts you've given; the worth of the furs and goods you hold or have cached, at a high-water mark; the Nor'Westers' shelves and the tension; the band's food, standing and numbers), tracked at the top of the Journal with the next concrete step, and written into the journal when you declare it. They pull against the Factor's orders on purpose.

## Orders you can argue with (design doc 3.9 step 5)

At your leader, *Work and orders… → About this task…*: **accept**, **ask for more time**, **ask for a man to go with you**, **ask for something in hand**, **ask leave to do it your own way**, ask for a different task, or **decline**. `Agents/Negotiation.cs` weighs it from what the world actually holds — your **rank**, your **standing**, his **regard** for you, your **history** (tasks done, times you refused, a parole you're on), **what he has heard** (the memories he holds with your name on them), your **connections** (men of the house who speak for or against you — a clerk's word counts double), **kin** (married into the country, on a matter touching the band), and the man's own temper — and the reply **names the two reasons each way**: *"Two days, then. No more. (your standing is 38; men of the house speak for you - but he is a suspicious man)"*. Leave to do it your own way turns the task open: anything of worth you stow in the warehouse counts toward it.

## Consequences you can read (design doc P.4)

`Core/Reveal.cs`. When something lands on you the journal names the chain **as far as you could know it**: *"Maskwa's people will not trade with the post. (you threatened Wapistan on 3 March 1811)"* — traced through your own journal, which is the only record you're entitled to — and when the cause is something you couldn't have known it says *"Nobody will say why."* Heavy news (a wife gone home, a band closing its counter, a grudge coming for you, your standing cut at the reckoning) takes the middle of the screen for a few seconds as a **headline**; routine work still passes as a line in the Log.

**Crises build over a season now** (P.3): a trapline catches at 0.07 a trap-night, a hunt takes at 0.22 a try, and a second party on the same ground the same day finds it picked over (0.45×). A lodge should take a season to trap out, not a fortnight.

## Simulation tiers (design doc 18.2)

`Core/Tiers.cs`. Everyone within **3200 px of any player** is **live**: a goal stack, pathfinding, a body on the screen, as before. Outside **4200** (hysteresis) a person goes **background**: the node stays - same name, memories, mail, grudges, save entry - but he stops moving, stops drawing, is left at his household's door, is cut from the snapshot stream, and his day resolves once in a **daily batch**: he eats from the cache (and starves if it's empty), burns a firewood for warmth, and does the work his role does in numbers - labourers cut wood or work the trapline or hunt a known site by skill, hunters draw sites down and bring the yield in, women fetch water, dry meat, make pemmican and fish. Promotion clears the brain and he picks up live. Men on a **journey** (courier, raid, ambush, rescue, search, the road) are never demoted - they're the story. **Multiple players are multiple anchors**, so a host at the Bay and a guest at Oxford House keep both places live (verified: 36 live / 4 background with the Assiniboine upland).

The saving: a fortnight that cost **63 s of pathfinding and 548 slow calls** before now costs **2.5 s and 23**, with the same world. F1 → *Simulation* shows the counts.

## Oxford House and the river road (design doc 3.9's far outpost, 10.1.2, 18.3)

The map is now 80 × **460** cells. A day's march south along the west bank sits **Oxford House** (`WorldLayout.OutpostTop` 36,396): a palisade, one house (hearth, counter, bunks, bales), a woodpile, its own books (`Economy/OutpostLedger.cs`, `Sim.Outpost`, in `Sim.Posts()`), a **postmaster** (Mr. James Sutherland - `Role.Postmaster`, runs the same `Delegation` loop as the Factor over *his* household), three men and a wife. Two rich lodges lie near it (Knee Lake, Oxford); the wolf den moved south. **Inland bands** trade there abstractly once a week (furs for goods by the price; bare shelves send them to the Canadians and count as furs lost).

**The river road** (`WorldLayout.RiverRoad`, dashed on the map) runs gate to gate. `RoadGoal` walks NPCs along it. Every month the postmaster sends a man down with the **furs and a report** (`CourierGoal` + `Letters.OutpostReport`: furs, food days, men fit, news); Auld reads it at his desk and his belief about the inland post updates (F1 → *Oxford House*). If **no report reaches York in 45 days** he sends two men up the road to see (`LookForGoal` by the road, reporting back). After the ship sails the Factor sends the **fall outfit** inland with two men and written orders (`Delegation.OrderSupply`). Everything is on the men: a courier killed on the road has the furs and the packet on his body.

**You**: at the road head on the bank below either gate the prompt reads *E Take the river road…* - **travel mode**: you march waypoint to waypoint while the clock runs at 6× for everyone (`GameCalendar.TravelBoost`); E or any step ends it. About 13 game hours York → Oxford House; take food and a capote. Volunteer to the Factor: *"Send me up to Oxford House with the outfit"* (standing 12) - ten pieces, a letter for Sutherland, the *Courier* task (stow them at the outpost chest), and you're **posted inland**: Sutherland gives your morning orders and his chest is yours; ask him to send you back to the Bay. `--spawn=outpost|road`, F1 *Oxford House* buttons (send the outfit now, teleports). Households: men now belong to a *post* (`HomeStores`), not just a company; each post's leader is `Household.Leader`.

## The Assiniboine and the nations (design doc 7.2, 13.1)

A second band (`BandLedger` is generic now: nation, name, camp, its own standing and debt with each company, `Sim.Bands`). The **Assiniboine** - Tchatka's people, with the new sheets (`AssiniboineFrames.tres`: walk, bow, musket) - winter upland near Oxford House (16,424) and come down the river road on 1 July to camp on the trail below York (22,118) for the trading season, back on 1 September. Their cache fills with **pemmican** from the buffalo country while they winter (14 a week), and the chief sells it at the posts (`FortLedger.BuyProvisions`: two pemmican for a piece of goods, only when the mess is under 60 days) - the plains provisioning of the design doc, feeding Oxford House. Each band has a **standing with the other** (65, allies of old): hunters of the two nations meeting on the same ground trade hard words (Chronicle, -2 each), it mends 0.3 a day, and under 25 the bands are **at odds** - their hunters treat each other as enemies in the bush. You can carry tobacco between the camps (*Speak for peace with…*, rapport 15, 3 goods, +15). Nakoda is its own tongue (`AssiniboineFluency`; an Assiniboine wife or a Cree wife interprets). Blood debts, grudges, restitution and marriage all work per band.

**The Nor'Westers on the river road** (design doc 10.3, 12.2): with tension ≥ 55 (or the council's sanction) Cameron sends two men to lie up a day's march out for the Company's inland packet (`AmbushGoal`, 30 hours): they fall on any Company man carrying papers or four furs, strip the body, and the partner reads what he shouldn't. Guards and couriers with muskets shoot back.

**Careers (design doc 3.6, 3.7)**: every company man has a **standing** with his master (+1.5 for a hunt reported, +0.5 for other work, -3 for a refusal). When Oxford House loses its master (the men carry on under the steadiest labourer, who gives the morning orders till one comes), the Factor - once a report or a search party tells him - **names the best clerk** (standing + trust + ambition) to the charge and sends him up the road; the runner-up remembers, and players in the running hear how they placed. An **ambitious clerk** who outranks you in nothing but envy has a quiet word with the Factor about the worst thing he's heard of you (a retold memory; -2 standing each time). Gossip tells you where the clerks stand. Women at the fire now **make pemmican** (8 dried meat → 6, when the cache holds 8+). Journeys (courier, search, ambush) are one-shot orders.

**Canoe freight (design doc 12)**: E at a canoe opens her - push off, **load** your goods and furs (300 lb; clothing, weapons and two meals stay on you), take everything out. A loaded canoe rides slower, an overloaded one much slower; what's in her stays in her when you step ashore, and is saved. The **frozen river is the winter road** (pace 1.25, and slippery).

**Rescue (design doc 9.1, 3.9)**: a post with one of its men held across the river sends two armed men to fetch him (urgency 72). If the house has the odds they refuse and tempers rise; otherwise he's handed over and walks home.

**Captivity (design doc 9.1)**: a blow from the other company that would kill you takes you **prisoner** instead - your goods and papers into their stores, you on the floor of their house for three days (HUD says so). Walk out of their yard and you're an escaped man (tension +4, the story travels); wait and they turn you loose with two days' pemmican. Or talk: **ransom** (12 trade goods) or **parole** - your word to carry no arms against them for four months. Break it in a fight and it's witnessed (standing -10, tension +8); confess it to your own Factor first (standing -6) or he finds out at ship time (-12). NPCs are taken the same way (85% when the beating would be fatal): marched to the other post, held three days, then walked home - nobody troubles a prisoner, and his own post counts him missing meanwhile.

**Wounds heal (design doc 17.5)**: a point an hour when fed, watered and warm, half on the move, nothing while starving or freezing. A woman of the post will **dress a wound** once a day (+25) if there's clean water.

**Skills (design doc 3.10)**: everyone has hunting, trapping, trade and woodcraft (0–1; Pack tab shows yours). Seeded by role (Cree hunters 0.5–0.8 hunting/trapping, clerks and chiefs trade 0.5–0.8) and learned by doing: a shot teaches hunting (+0.01) and steadies the aim (miss chance × (1 − 0.7·skill)); working a site (+0.004) and a trapline set with a practised hand (chance × 0.6–1.4, the line remembers who set it); each trade (+0.01), and at 0.5 you bargain 15% better both ways; the axe (+0.02), and at 0.5 a tree gives 6 wood in less time.

**The country wife (design doc 8.3, 8.4)**: a wife needs seeing. Twenty-five days apart and her people ask after you; forty-five and she **goes home** - the marriage is over, the band's standing with the Company drops 15 and her kin think less of you. Each spring (6 June) her father looks for his due: gifts worth 3 MB per year of marriage or he calls you a bad son-in-law (rapport −12, band insulted).

**Woodpiles** are a menu now (stack / take; not yours = a gift or a theft). Musket shots play the new firing frames (player, labourers, Assiniboine). The river road is drawn with **footprint tiles** (`SourceRoad`, pace 1.1).

## Ground, gear and noise (design doc 12, 16.1, 17.3)

The ground sets your pace (`TerrainPainter.SpeedFactorAt`): a beaten trail 1.05, sand 0.9, **snow 0.7** (1.0 on **snowshoes** - the Cree wear them; four in the stores), the **goose marsh 0.6** in summer, **ice** no slower but slippery (velocity carries). Clothing counts only when **worn**; take a capote off and it's just weight. **Sound** carries and shows as a ring: a musket shot 1500 px (prey bolts, predators think twice), a bow 200, running 420, walking 190, crouching 45 - animals hear you coming at 60% of that. An **axe** (you start with one; stores hold them) fells the nearest spruce for 4 firewood; there's no other way to cut wood.

**Day length** now comes from the latitude (York Factory, 57 N) and the date, then a thumb on the scale: the day is kept a little longer than the night (≥ 12.6 h) except in December–January that far north (≥ 9.5 h). A bed from 17:00 takes you to 07:00.

**The storeroom chest** (E inside your own warehouse / the Nor'Westers' house): stow goods and furs; **stow provisions** (geese, meat, fish - you keep two meals; standing for it); draw a musket (standing 15, one), traps, an axe, snowshoes, a blanket (standing 10), a kettle, a tumpline, four trade goods (clerk), a ration. **Other people's woodpiles** give wood too, and anyone who sees you remembers it.

**Talk** is in submenus now: *Talk…* (how things stand, about them, **rumours**, where the game is, London/the council), *Work and orders…*, *Trade*, *Give a gift*, *Family and marriage…*, *The companies…*; pressing matters (a letter to hand over, a debt) sit on top. Rapport from talk wears thin: the first question of the day warms, the fifth does nothing. The chief's **answer** to the Factor's letter is a paper in your pack: hand it to Auld to finish the task.

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

`--spawn=depot|warehouse|menshouse|wharf|berth|camp|caribou|geese|beaver|wolves|x,y` (e.g. `31,86` is the yard) · `--hour=H` · `--date=YYYY-MM-DD` · `--speed=SECONDS_PER_HOUR` · `--trace` (print everyone's state every game hour) · `--ship` (bring the ship in now) · `--wolves` · `--scarcity` · `--tension=N` · `--herdmoved` · `--follow` (nearest person follows you, then you're moved outside the gate) · `--scurvy` · `--load` · `--autosave=S` · `--autoload=S` · `--provoke` · `--talk` · `--blaze` · `--son` · `--killme=S` · `--talkto=Name` · `--pick=0,3,1` (press those dialogue options, 0-based, from eight seconds in) · `--talktrace` (print every screen of a conversation) · `--kit=BeaverPelt:8,Rum:2` · `--nodirector` · `--brawl` · `--provoke=N` · `--dumpbook=S` · `--quitafter=S` · `--ambition=RiseAtThePost|FoundAPost|MarryIn|PrivateTrade|DriveOffTheRival|KeepTheBandWhole`

## Barks and shared memory (design doc P.4, P.8)

`Agents/Barks.cs`. People say things out loud in passing - a bubble over the head for a few seconds, to whoever is beside them, player or NPC - and the listener may answer. What they say comes from what they have, in this order: **a memory shared with the listener** (both saw it firsthand, weight >= 1, a day or more old, not yet raised between them: "Do you remember, Peter? ..."; a wrong the listener did you comes out sour), **dead kin** ("Kimiwan should be here", a handful of times), **the body** (hungry, cold, fever, scurvy, hurt, drunk), **the post's books** (Factor, clerks, postmaster: "30 beaver in the warehouse and London wants 60, with 229 days to the ship" - one voice per household every few days), **what they want** (by role and temper). Raising a shared hardship warms both people +1.5 rapport. One speaker at a time in a group. Cree or Assiniboine speech reads as "(Cree - you catch a word or two)" below 0.3 fluency.

Every `Chronicle.Event` now records its **witnesses** (saved). `Chronicle.Shared` records things a household lives through together: a long **blizzard**, the **food running out** (at most once a fortnight), the **ship coming in**. The console prints each line as `[bark] Name: text`.

**Evenings at the fire** (`HearthGoal`, `Barks.HearthLine`): from 18:00 to about 21:30 the household drifts to its own hearth (the sociable nearly always; a band's open fire not in a blizzard), each to a seat of their own round it, and the talk comes three times as thick: a **toast** to the house's recent dead ("To John."; "We remember ..." in a camp), **stories** - something the teller knows that the fire hasn't heard tonight, which passes to everyone sitting there one retelling further from the truth, and into your journal if you're within earshot - and **songs** and tales by people (a paddling song, an Orkney air, Wisahkecahk and the ducks, a year-old London paper). An evening at the same fire warms everyone there to you a little. Talk to anyone at the fire: *Tell a story at the fire...* offers up to four things you saw yourself that someone there hasn't heard; they have it from you firsthand, and like you better for it.

**A death leaves a hole** (`Agents/Losses.cs`): every death in a household is recorded - his bunk, his day, his body. Housemates passing the **empty bunk** say so, and when a recruit takes it, someone remarks on that. Kin mourn aloud up to eight times; housemates who went through things with him (shared memories, rapport) for six weeks, sometimes with the memory itself. The evening after the death his **effects are gathered** off the body (whatever nobody looted; food to the stores) and a company post **sells them among the men** - talk to the master to *Bid at the sale of ...'s effects*, priced in pieces of goods, the money to his widow if he had one at the post, else the stores. At 21:00 what's left goes to whoever was closest to him; a band's family gives his things away the same way. Whoever has his capote or his knife says so now and then ("This was John's capote").

**Lies** (`Agents/Lies.cs`, design doc P.10): *Talk... > Tell them something... (it needn't be true)*, or at the fire *Make something up...* - pick a name (clerks of your post, the other company's men, the listener's house) and a deed: robbed the traps, struck someone, traded on his own account, was at the other house after dark, or *say it was him, not you* for your own latest misdeed. Believed, it hurts the accused like the truth (his master's standing, a band's opinion of his company). It's seen through by the accused, anyone named in it, whoever saw the real deed, whoever was with him within ~3 hours of the claimed time (half the time), a close friend of his (half the time), and quietly disbelieved by a suspicious man who dislikes you. Exposure: "That's a lie..." aloud, and "*you* have been telling lies about ..." goes round - your master hears it too (standing).

**NPC lies** (`Lies.Consider`): loyalty under 0.5, at most once a game day, never twice in five days about the same man, told face to face to someone within 200 px (bubble "(low) ..."). Motives in order: *cover* (his own misdeed of the last ten days, on the witness he likes least, to someone who didn't see it), *spite* (someone at rapport <= -25), *career* (clerks, labourers, voyageurs, postmasters with ambition > 0.55: the best-standing man of his rank within 5 below him or above, or a player of the house, to the master), *rivalry* (tension >= 50: the other company's men robbed your traps, to a band person). Within 260 px you overhear it (journal) and know who started it; a friend who warns you names the liar if the story was within two tellings of him. *Talk... > What are they saying about me?* lists up to three misdeeds with your name on them (not his own lies) - *deny it* (eyewitness: no; a real lie: believed if his regard for you + 10 >= his regard for the liar; a true story: a gamble on regard) and *...and say X started it* (he marks the liar down and remembers the liar lied about you). Dev args `--liar=Name` / `--liar=Name:spite`; `--converse=h1:*about-me>Talk>What are they saying>deny` waits for someone holding a story about you.

**Friends** (`Agents/Friends.cs`): rapport >= 20 and a bond >= 3 (shared firsthand memories, rapport/25, kin +3). A friend within 220 px shares food when you're under 30 hunger, takes you aside with what's being said about you (or who still holds a grudge), won't pass on your misdeeds (mostly), may refuse a lie about you, and comes in armed when you're set upon by someone who isn't also his friend. Works NPC to NPC too. Dev arg `--befriend=Name`.

**What matters now** (`Core/Focus.cs`): the panel top right - *Now* (prisoner, hurt, freezing, starving, parched, a grudge-holder within 900 px, a friend's warning in the last two days, a sale at your post, your task, under 10 days of food), *Aim* (your first live ambition's line), *Heard* (the most relevant of your last 12 journal lines).

Dev arg `--kill=Name` kills that person five seconds in (to watch a household take a loss).

**Children and succession** (`Agents/Heirs.cs`, design doc P.6). A country marriage bears children: a year or so after the wedding, if the two of them are still together, the wife bears a child at the post (headline, and the band's people think better of you), and the child grows year by year until at sixteen he "takes a man's work". **Death is a turn of the story, not a reset.** When the player dies the overlay reads out who takes his place - a grown son first, else a wife's brother if he married into the country, else a brother come out on the ship - with his age, his ties, and what he inherits. Pressing R hands the world over: the dead man goes into **your house** in the journal (`Forebear`: name, rank, years, how it ended, what he got done), blood debts held against his name follow the family at 0.6 of the debt, everyone's regard drops to 0.4 of what they thought of the man (a name is worth something, or costs something), the heir keeps the unfinished ambitions, the parole dies with the man, rank goes back to labourer, a quarter of the private pile survives, and a son or a wife's brother brings his own fluency and his hunting and trapping with him. All of it saves and loads.

Dev args `--son` (the player has a grown son at the post from second six) and `--killme=SECONDS` (the player dies then, and takes up his heir's place four seconds later) walk the whole handover.

Dev arg `--shot=SECONDS` saves the screen to `user://shot_N.png` every SECONDS (six shots) - handy when no window capture is possible.

Dev args for menus: `--snap=S1,S2` saves the screen to `user://snap_N.png` at those seconds in any scene, the main menu included; `--menu=settings|controls|multiplayer` opens the main menu on that page; `--settings` opens the Esc menu's settings 4 s into the world. Settings live in `user://settings.cfg` (`Core/Settings.cs`; `UI/SettingsPanel.cs` is the shared screen, `UI/SettingsRuntime.cs` the root node for F11, Ctrl+wheel zoom, FPS, focus pause and autosave; `Core/Keybinds.cs` keeps rebound keys in its [keys] section).

## Known gaps / next

- Art is placeholder everywhere new (trees, tipis, ship, women/factor recolours, item icons).
- Roofs are flat placeholder planes; the front wall still fades when you're inside.
- Player's own weapons have no swing animation; shots are a line and a flash.
- Delegation is a fixed playbook, not a planner; no message objects yet (orders take effect immediately, except London's, which wait for the ship).
- Balance numbers (need rates, site sizes, prices) are first guesses. Use `--trace` runs at `--speed=1` to watch a week in a few minutes.
