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
| F1 | test console (host only) |
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

## Trapping (design doc 16.1, 16.3)

Beaver are trapped, not hunted. Steel traps are items (the post has 10, the band 8, the ship brings 12). Use a trap from the hotbar while standing at a beaver lodge to set your line (up to 6 at once); traps catch overnight against how thick the lodge is; E at the lodge lifts the catch (pelt + meat per beaver) and leaves the traps. Cree hunters and, in winter, the Factor's men run their own lines. Anyone checking a lodge with other parties' traps may rob them (the less loyal, the likelier); the owner finds the traps robbed, and witnesses remember who did it.

## Memory and rumour (design doc 10.2, 10.4, 11.2)

Events (deaths, blows struck, trades, traplines set or robbed, letters carried) are remembered firsthand by everyone within ~650 px, and passed on when people stand near each other — each retelling one hop less direct, fading after four. "Ask about the others" now draws on what that person actually knows, hedged by how they know it ("I saw it myself" / "I was told" / "They say"). Hearing that someone robbed traps or struck a friend lowers their opinion of the culprit.

## Credit (design doc 13.3)

A band in good standing (> 40) that comes to the post hungry without goods is advanced food on credit; the debt is settled first out of the next furs they bring. `BandLedger.DebtMb`.

## Letters (design doc 10.1)

One task the Factor may hand you: carry his letter to the chief. It takes effect when the chief reads it — the band is invited to trade (they come with what furs they have), standing +6.

## Food chain

Fresh meat spoils (~12% a day in the cache, half that in winter). Women at the fire dry it: 3 fresh → 2 dried, which keeps. Rations come out of the plainest food first.

## F1 test console

Calendar (speed, skip hours/days/months, set date, blizzard/clear/gale), Player (needs, invulnerable, give items), London & the post (pelt price now / next ship, quota, stores, +goods/+food/empty food), Cree band, Wildlife (site report, ×2, ÷2, force scarcity, wolf pack at the gate, starving caribou at the gate), Ship (summon, depart), People (everyone's goal and needs, teleports).

## Command-line dev args (after `--`)

`--spawn=depot|warehouse|menshouse|wharf|berth|camp|caribou|geese|beaver|wolves|x,y` (e.g. `31,86` is the yard) · `--hour=H` · `--date=YYYY-MM-DD` · `--speed=SECONDS_PER_HOUR` · `--trace` (print everyone's state every game hour) · `--ship` (bring the ship in now) · `--wolves` · `--scarcity`

## Known gaps / next

- Art is placeholder everywhere new (trees, tipis, ship, women/factor recolours, item icons).
- Roofs are flat placeholder planes; the front wall still fades when you're inside.
- Player's own weapons have no swing animation; shots are a line and a flash.
- Delegation is a fixed playbook, not a planner; no message objects yet (orders take effect immediately, except London's, which wait for the ship).
- Balance numbers (need rates, site sizes, prices) are first guesses. Use `--trace` runs at `--speed=1` to watch a week in a few minutes.
