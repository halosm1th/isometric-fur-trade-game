# York Factory test world — what's in it and how to drive it

*Written after the overnight build session (Sept 2026). Everything here is placeholder art and first-pass numbers; the point is that the loops run end to end so they can be watched and tuned.*

## Controls

| Key | Action |
|---|---|
| W A S D / arrows | walk |
| Shift | sprint (1.7× speed, ~2.4× hunger) |
| E | context action: talk to a person · pick up · loot a body / butcher a carcass · woodpile (stack or take firewood) · warehouse (stow goods/furs) · stores counter (draw a ration when short) · ship (shoulder cargo) · open/close door · look at a fire |
| 1–9 | use hotbar slot (eat / drink / feed a fire / ready a weapon / read clothing) |
| Mouse wheel + Enter | select and use hotbar slot |
| Left click | attack whoever is under the cursor with your best weapon (knife, tomahawk, bow, musket) |
| F | attack the nearest hostile |
| Q | drink (from water, or melt snow at a fire in winter) |
| Z | wait an hour; at night, sleep until 07:00 |
| T | hold for fast-forward (×40) |
| F1 | test console |
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

Talk to a father (E): "Ask for X's hand". Needs rapport ≥ 30 (gifts, talking) and ≥ 8 MB of gifts given to him. HBC rule: one wife. She moves into the trading room and keeps house.

## F1 test console

Calendar (speed, skip hours/days/months, set date, blizzard/clear/gale), Player (needs, invulnerable, give items), London & the post (pelt price now / next ship, quota, stores, +goods/+food/empty food), Cree band, Wildlife (site report, ×2, ÷2, force scarcity, wolf pack at the gate, starving caribou at the gate), Ship (summon, depart), People (everyone's goal and needs, teleports).

## Command-line dev args (after `--`)

`--spawn=depot|warehouse|menshouse|wharf|berth|camp|caribou|geese|beaver|wolves|x,y` (e.g. `31,86` is the yard) · `--hour=H` · `--date=YYYY-MM-DD` · `--speed=SECONDS_PER_HOUR` · `--trace` (print everyone's state every game hour) · `--ship` (bring the ship in now) · `--wolves` · `--scarcity`

## Known gaps / next

- Art is placeholder everywhere new (trees, tipis, ship, women/factor recolours, item icons).
- No roof rendering; the front wall fades when you're inside.
- Player's own weapons have no animation; NPC attack has none either.
- Sites are known to everyone (design doc wants them learned).
- Delegation is a fixed playbook, not a planner; no message objects yet (orders take effect immediately, except London's, which wait for the ship).
- Balance numbers (need rates, site sizes, prices) are first guesses. Use `--trace` runs at `--speed=1` to watch a week in a few minutes.
