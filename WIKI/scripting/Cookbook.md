---
title: "Cookbook"
aliases: [Cookbook]
tags: [scripting, campaign, tool]
status: reference
---

<!-- Copied from docs/wiki/Cookbook.md by tools/docs/sync_scripting_wiki.py: edit that, not this. -->

# Cookbook

Patterns for the things campaigns most often need, each a few lines you can copy and change. Every snippet on this page compiles; the complete campaign at the end is checked by the tests. The *why* behind each rule is in [[Beats-and-the-Lifecycle|Beats and the lifecycle]], [[State-Flags-and-Variables|State, flags and variables]] and [[Pitfalls|Pitfalls]].

| I want | Recipe |
|---|---|
| something to happen on a day, whatever anyone does | [[Cookbook#A fixed fact|A fixed fact]] |
| history the player or the world can prevent | [[Cookbook#An avertable event|An avertable event]] |
| history the world can bring about itself | [[Cookbook#An event the world can cause|An event the world can cause]] |
| the world to grow tense before something happens | [[Cookbook#Pressure before a fixed day|Pressure before a fixed day]] |
| the story to fork | [[Cookbook#Two roads|Two roads]] |
| something to happen only once the news has arrived | [[Cookbook#Waiting for the letter|Waiting for the letter]] |
| the same thing every year | [[Cookbook#Once a year|Once a year]] |
| trouble every few days for a season | [[Cookbook#A run of troubles|A run of troubles]] |
| a thing that may happen, at some rate | [[Cookbook#Dice: a rate or a single throw|Dice: a rate or a single throw]] |
| one of several outcomes | [[Cookbook#Dice: one of several|Dice: one of several]] |
| a consequence later | [[Cookbook#Later consequences|Later consequences]] |
| a running opinion that leads to a reckoning | [[Cookbook#Counters and a reckoning|Counters and a reckoning]] |
| to react to the boats | [[Cookbook#Late boats|Late boats]], [[Cookbook#Standing a run down for good|Standing a run down for good]] |
| a post to run short | [[Cookbook#Shortages|Shortages]] |
| the world to answer what the player did | [[Cookbook#Reputation|Reputation]] |
| different words for each company | [[Cookbook#Speaking to each side|Speaking to each side]] |
| a post founded as part of the story | [[Cookbook#Founding a post|Founding a post]] |
| the player to feel something coming | [[Cookbook#Rumours that sharpen|Rumours that sharpen]] |
| the world to begin later than 1811 | [[Cookbook#A later start|A later start]] |
| something asked of the player | [[Cookbook#A first quest|A first quest]] |
| every player to be one kind of person, somewhere of my choosing | [[Cookbook#A soldier who goes over|A soldier who goes over]] |
| the player to change sides, or desert | [[Cookbook#A soldier who goes over|A soldier who goes over]] |
| an end | [[Cookbook#An ending|An ending]] |

## A fixed fact

A date and an `OnActivate`, nothing else: it happens on its day at every adherence. Use it for what the world cannot change (a war declared in Europe, a grant signed in London).

```c
beat Blockade(title: "The Continental Blockade holds the fur market down", date: date(1811, 6, 1)) {
    OnActivate() {
        world.setLondon(0.85, 0.85);
        flags.set("blockade");
    }
}
```

Dated before the player's [[World-Reference#When the game starts|first day]] (as this one is, on every starting path), a beat like this is [[State-Flags-and-Variables#History before the start|history]]: its flag is raised but the London change is not made. If later beats depend on its effect, let them test its flag.

## An avertable event

A beat the story expects, but which a calm world or the player can stop. `CancelWhen` is asked first, every day, at every adherence: once it holds, the beat cannot happen, and `OnCancel` says what happened instead. Raise a flag there, because a cancelled beat raises none.

```c
beat MassacreAtTheForks(
    id: "massacre-at-the-forks",
    title: "Blood at the Forks",
    date: date(1816, 6, 19),
    window: days(45),
    rumour: "Riders are gathering on the Assiniboine."
) {
    OnApproach() { world.raiseTension(15, "riders gather on the plains"); }
    CancelWhen() => world.date >= date(1816, 5, 1) && (world.tension < 30 || player.did("truce"));
    OnCancel() { flags.set("forks_averted"); say("The riders have gone home, and the colony breathes."); }
    OnActivate() {
        world.raiseTension(25, "blood at the Forks");
        flags.set("governor_killed");
    }
}
```

Note the date in `CancelWhen`: without it, a quiet week in 1812 would cancel an 1816 beat. The `window` makes it an *event*: at Firm and Strict it is forced if the world has not cancelled it; at Steady it is only pressed, and it needs a `ResolveWhen` to ever happen (next recipe) or it waits for ever.

## An event the world can cause

The world is given the chance to do it: if `ResolveWhen` holds, the beat is over by the world's doing and `OnResolve` runs instead of `OnActivate`. Guard it with a date so that it cannot resolve years early.

```c
beat PemmicanWar(
    id: "pemmican-war",
    title: "The pemmican war breaks out",
    date: date(1814, 6, 1),
    window: days(60),
    lead: days(120),
    rumour: "They say the colony means to stop the pemmican leaving the Red River."
) {
    OnApproach() { world.raiseTension(10, "the pemmican question"); }
    ResolveWhen() => world.date >= date(1814, 3, 1) && world.tension >= 70;
    OnResolve() { flags.set("pemmican_war"); say("It has come to blows over the pemmican."); }
    OnActivate() { flags.set("pemmican_war"); world.raiseTension(20, "the Proclamation enforced"); }
}
```

Both ends raise the same flag, so what follows needs only `["pemmican_war"]`, whichever way it came.

## Pressure before a fixed day

To press on the world **and** make sure the beat happens, write `kind: Kind.Fixed`; otherwise `OnApproach` would turn it into an event that Steady never forces.

```c
beat UnionOfTheCompanies(
    id: "union",
    title: "The companies unite",
    date: date(1821, 3, 26),
    lead: days(365),
    kind: Kind.Fixed,
    rumour: "There is talk in London and Montreal of a union of the two companies."
) {
    OnApproach() { world.setLondon(0.9, 1); }
    OnActivate() { world.raiseTension(-70, "the union"); flags.set("union"); }
}
```

## Two roads

A fork is two beats that wait on different flags. Make sure exactly one of the flags can be raised: here a single beat raises one or the other.

```c
beat TheChoice(id: "the-choice", title: "The partners decide", date: date(1815, 6, 1)) {
    OnActivate() {
        if (player.did("truce") || world.tension < 40) { flags.set("road_peace"); }
        else { flags.set("road_war"); }
    }
}

beat PeaceRoad(title: "A bargain is struck") ["road_peace"] {
    OnActivate() { world.raiseTension(-20, "a bargain"); say("The colony and the partners have struck a bargain over the pemmican."); }
}

beat WarRoad(title: "The colony is dispersed") ["road_war"] {
    OnActivate() { world.raiseTension(15, "the colony dispersed"); shout("The settlers have been driven from the Forks."); }
}
```

A fork can also come from `CancelWhen` (the avertable event above leads to `forks_averted` or `governor_killed`), or from two beats with opposite `AwakenWhen`s. For long branches, put a requires on the first beat of each road and let the rest of each road require the beat before it.

## Waiting for the letter

`news(...)` sends a letter; reading it raises `heard:<beat id>`. A beat that requires that flag happens when the news has **arrived**, wherever and whenever that turns out to be.

```c
beat GrantSigned(id: "grant-signed", title: "Selkirk's grant is signed", date: date(1811, 9, 1)) {
    OnActivate() {
        flags.set("grant");
        news(from: "the Governor and Committee, London", to: Person.Factor,
             subject: "Lord Selkirk's grant",
             text: "Settlers will come out by the Bay. Give them every assistance.", days: 70);
    }
}

beat TheFactorPrepares(title: "The Factor makes room for the settlers") ["heard:grant-signed"] {
    OnActivate() { world.addStock("York Factory", Item.Firewood, 20); say("The Factor has the men cutting wood for the settlers.", Company.HBC); }
}
```

The letter goes to whoever holds the role when it arrives; if nobody does, it waits (see [[World-Reference#Letters: who can be written to|Letters]]). In the simulator letters are never delivered, so raise `heard:grant-signed` with a scenario event to test the second beat.

## Once a year

Two ways. A `foreach` writes a beat per year: each has its own id, title and text, and they are history independently.

```c
foreach (int year in 1812..1820) {
    beat YearlyShip(id: "ship-" + year, title: "The ship comes in, " + year, date: date(year, 8, 20)) {
        OnActivate() { say("The ship is in the roads: the " + year + " outfit is landing.", Company.HBC); }
    }
}
```

Or one beat with `repeat`, which is shorter but the same every year. A repeating beat is never history: date its **first** time on or after the game's start, because one dated earlier [[Pitfalls#A repeating beat dated in the past catches up|catches up]], once a day, on the first days of the game:

```c
beat Freshet(title: "The spring freshet", date: date(1812, 5, 15), repeat: days(365)) {
    OnActivate() { brigade("the Montreal canoes").delay(3); }
}
```

## A run of troubles

A loop with date arithmetic writes a beat for every week of a season. Each must have its own id. The `?:` and `%` are worked out when the script compiles.

```c
foreach (int week in 0..10) {
    beat WeeklyTrouble(
        id: "trouble-" + week,
        title: "Trouble at the woodpile, week " + (week + 1),
        date: date(1811, 9, 1) + days(7 * week),
        rumour: week % 2 == 0 ? "Somebody has been at the woodpile in the night." : "The dogs have been barking at nothing."
    ) {
        OnActivate() {
            if (chance(50)) { world.addStock("York Factory", Item.Firewood, -5); }
        }
    }
}
```

## Dice: a rate or a single throw

`chance(n)` in a **condition** is a rate: n percent each day. In an **effect** it is one throw.

```c
// a rate: on each summer day of open hostility, one chance in twenty
beat Skirmish(title: "A skirmish on the river", repeat: days(30)) {
    AwakenWhen() => world.inMonths(6, 9) && world.tension >= 70 && chance(5);
    OnActivate() { world.raiseTension(4, "a skirmish"); say("Shots were exchanged on the river."); }
}

// a single throw, made on one day and remembered as a flag
beat BadIceThisYear(id: "bad-ice-1813", title: "The ice will be bad this year", date: date(1813, 10, 1)) {
    OnActivate() { if (chance(30)) { flags.set("bad_ice_1813"); } }
}
```

Put `chance` last in a condition, so the dice only roll on days the rest holds. To see how often a beat really fires, run the simulator with `--runs 200`.

## Dice: one of several

`random` does exactly one of its options, by weight (the weights need not add up to 100).

```c
beat Ambush(title: "The brigade is set upon", date: date(1818, 7, 1)) {
    OnActivate() {
        random(
            option(40, () => brigade("the Saskatchewan brigade").delay(6)),
            option(25, () => brigade("the Saskatchewan brigade").turnBack()),
            option(10, () => brigade("the Saskatchewan brigade").lose()),
            option(25, () => say("Shots were heard at the carrying place, but nothing came of it."))
        );
    }
}
```

(Remember the brigade effects act on the crossing on the river *that day*: on 1 July the Saskatchewan brigade may already be in. Pair it with `AwakenWhen() => brigade("...").isOut && !brigade("...").isIn` to be sure.)

## Later consequences

`after` runs effects some days later (saved with the game); `trigger` makes another beat happen at the end of today, whatever its date and conditions.

```c
beat FortSeized(id: "fort-seized", title: "Fort William is seized", date: date(1816, 8, 13)) {
    OnActivate() {
        world.addStock("Fort William", Item.TradeGoods, -60);
        after(days(60), () => say("Fort William has been given back to the partners."));
        after(days(300), () => trigger(TheTrials));
    }
}

beat TheTrials(id: "the-trials", title: "The trials in Upper Canada") {
    // no date and nothing to wake on: it only happens when triggered
    AwakenWhen() => false;
    OnActivate() { say("The men taken at Fort William have been tried, and acquitted."); }
}
```

`AwakenWhen() => false` keeps a beat that is only ever triggered from happening on its own (and is clearer than leaving it out, which would make a dateless beat happen on the first day).

## Counters and a reckoning

Variables keep a running score that later beats read. Reset them when the reckoning comes.

```c
int hardYears;

foreach (int year in 1812..1820) {
    beat Returns(id: "returns-" + year, title: "The returns of " + year, date: date(year, 9, 1)) {
        OnActivate() {
            if (world.furs("York Factory") < 300) { hardYears++; }
        }
    }
}

beat CommitteeReckons(title: "The Committee loses patience", repeat: days(365)) {
    AwakenWhen() => hardYears >= 3;
    OnActivate() {
        news(from: "the Governor and Committee, London", to: Person.Factor, subject: "the returns",
             text: "Three poor years. The Committee expects better.", days: 300);
        hardYears = 0;
    }
}
```

## Late boats

The full lifecycle on one repeating beat: worry when the boats are late, escalate if they stay late, stand down if they come in.

```c
beat YorkBoatsLate(title: "The York boats are overdue", repeat: days(300)) {
    AwakenWhen() => world.inMonths(7, 9) && brigade("the York boats").isLate;
    OnAwaken() { say("The York boats are overdue.", Company.HBC); }
    ActivateWhen() => brigade("the York boats").daysLate >= 10;
    OnActivate() { shout("The York boats are ten days overdue.", Company.HBC); }
    ResolveWhen() => brigade("the York boats").isIn;
    OnResolve() { say("The York boats are in at last.", Company.HBC); }
}
```

`repeat: days(300)` makes it a once-a-season beat: after it happens or resolves, it sleeps until next summer.

## Standing a run down for good

`cancel()` stands down only the crossing on the river; the run's next leg still sails. To end a run, cancel every crossing as it sets out:

```c
beat CanoesStandDown(title: "The Montreal canoes are stood down", repeat: days(1)) ["union"] {
    AwakenWhen() => brigade("the Montreal canoes").isOut && !brigade("the Montreal canoes").isIn;
    OnActivate() { brigade("the Montreal canoes").cancel(); }
}
```

## Shortages

Guard any inland post with `hasPost`: a post that does not stand answers nothing.

```c
beat NorwayHouseHungry(title: "Norway House is short of pemmican", repeat: days(60)) {
    AwakenWhen() => world.hasPost("Norway House") && world.stock("Norway House", Item.Pemmican) < 10;
    OnActivate() {
        world.addStock("York Factory", Item.Pemmican, -10);
        world.addStock("Norway House", Item.Pemmican, 10);
        say("Pemmican has gone up from the Factory to Norway House.", Company.HBC);
    }
}
```

## Reputation

Deeds are what the player did. Count them, and let the world remember.

```c
beat KnownForTruces(title: "The country knows you for a peacemaker") {
    AwakenWhen() => player.deeds("truce") >= 2;
    OnActivate() { world.raiseTension(-5, "a peacemaker's word"); say("Men of both companies say you can be trusted to talk."); }
}

beat KnownForRaids(title: "Your name is a threat on the river") {
    AwakenWhen() => player.did("raid") && world.tension >= 40;
    OnActivate() { world.raiseTension(5, "the raider's name"); }
}
```

See [[Player-Deeds-and-Rumours|Player deeds and rumours]] for what each deed is.

## Speaking to each side

`if` on `world.playerFaction` picks the words when the effect runs; `say(..., Company.X)` picks who hears them. In multiplayer both may be playing, so the second is usually better:

```c
beat NewsOfTheMerger(title: "News of the union reaches the country", date: date(1821, 7, 1)) {
    OnActivate() {
        say("The Company has swallowed the Nor'Westers. The Committee's men are to keep their places.", Company.HBC);
        say("The partners have sold out to the Bay. Half the posts are to close.", Company.NWC);
        if (!(world.playerFaction == Company.HBC) && !(world.playerFaction == Company.NWC)) {
            say("The traders say the two companies are one now. There will be one price for furs.");
        }
    }
}
```

## Founding a post

With `postsByFoundingYear: true` posts appear by themselves on 1 January of their founding year. Without it, a beat founds them, which lets the story decide when (or whether) a post is built:

```c
beat FortDouglasBuilt(title: "The colony builds Fort Douglas", date: date(1815, 8, 1)) {
    AwakenWhen() => !flags.has("colony_dispersed");
    OnActivate() { world.foundPost("Fort Douglas"); }
}
```

`foundPost` is replayed for beats that are history, so a post founded in the past by the script stands from the first day.

## Rumours that sharpen

A rumour shows while its beat is awake and (for a dated beat) within its `lead`. Use two beats to make the talk grow more urgent as the day nears: a vague pressure beat early, and the real beat's own rumour close to the day.

```c
beat Whispers(
    title: "Whispers of trouble",
    date: date(1816, 5, 28),
    lead: days(120),
    rumour: "There is ill feeling between the colony and the Métis."
) {
    OnApproach() { world.raiseTension(5, "ill feeling"); }
}

beat TheBattle(
    title: "The battle on the plains",
    date: date(1816, 6, 19),
    lead: days(21),
    kind: Kind.Fixed,
    rumour: "Riders were seen at Frog Plain this morning, painted."
) {
    OnApproach() { world.raiseTension(10, "riders at Frog Plain"); }
    OnActivate() { flags.set("battle"); }
}
```

`Whispers` has no `OnActivate`, so it is a *pressure* beat: from 120 days before its date its rumour is told and its pressure runs, and on its date it is done and its rumour goes, the day before `TheBattle`'s own rumour (21 days of lead) begins.

## A later start

The header's `start` begins the world on a day of your choosing, on every starting path; `startYear` only moves the year. Everything dated before the first day is history (its flags hold), so write the years you skip as beats with flags, and test later beats against them:

```c
// in the header: campaign AfterTheMerger(title: "After the merger", start: date(1821, 8, 20));
beat UnionSigned(id: "union-signed", title: "The companies unite", date: date(1821, 3, 26)) {
    OnActivate() { flags.set("union"); world.raiseTension(-70, "the union"); }   // history: only the flag is kept
}

beat FirstCouncil(title: "Simpson's council at York", date: date(1821, 9, 1)) ["union"] {
    OnActivate() { say("The chief factors of both old companies sit down together at York Factory."); }
}
```

See [[World-Reference#A campaign's own start|a campaign's own start]] for what changes with the year and what does not.

## A first quest

Beats happen to the world; a [[Quests|quest]] asks something of the player. The smallest useful one: speak to someone, do something, come back.

```c
questline Errands(title: "Errands at the Factory", forCompany: Company.HBC) {
    quest Wood(title: "Wood for the Factory", summary: "The Factor wants wood cut before the snow.") {
        talk Asked(who: Person.Factor, text: "Ask Mr. Auld what is wanted") {
            Says() => "\"Wood, {player}. Ten loads, before the snow comes.\"";
        }
        task Cut(task: Task.Firewood, count: 10, text: "Bring in ten loads of firewood");
        talk Done(who: Person.Factor, text: "Tell Mr. Auld it is done") {
            Says() => "\"Good. I'll remember it.\"";
        }
        OnComplete() { player.addStanding(3); }
    }
}
```

A beat can then wait on it with `AwakenWhen() => Wood.done;`.

## A soldier who goes over

`startPath` brings every player in by one road and `startAt` sets them down somewhere: here, a private of the garrison at Niagara on the King's birthday, 1812. `player.desert(...)` takes him over the river; a questline `forCompany` the side he is on now picks up the story, and the garrison he left will take him back if it can (the flag `deserter:taken`). See [[World-Reference#Who the players are, and where they begin|Who the players are]] and [[Effects-in-the-Game#Deserters|Deserters]].

```c
campaign OverTheRiver(
    title: "Over the river",
    description: "A private of the garrison at Niagara in the summer of 1812, and a boat across on a dark night.",
    extends: "historical",
    start: date(1812, 6, 4),
    startPath: Path.Crown,
    startAt: "Niagara"
);

questline Ranks(title: "In the ranks", forCompany: Company.Crown) {
    quest Muster(title: "The war comes", summary: "The Americans have declared war. The garrison stands to.") {
        StartWhen() => world.date >= date(1812, 6, 26);
        talk Orders(who: "officer@Fort George", text: "Report to the officer at Fort George") {
            Says() => "\"You will stand your watch on the bank at Queenston, and you will keep away from the boats.\"";
            choice Obey(label: "\"Sir.\"", reply: "He nods you out.");
            choice Look(label: "Say nothing, and look at the river.", reply: "He watches you a moment too long.") { flags.set("thinking_of_it"); }
        }
        reach Bank(place: "Queenston", text: "Stand your watch on the bank at Queenston");
    }
    quest TheBoat(title: "The boat", summary: "A boat goes over from below the town on dark nights.") [Muster] {
        StartWhen() => flags.has("start:knows-the-boatman") || flags.has("thinking_of_it");
        wait Dark(days: 3, text: "Wait for a night with no moon");
        OnComplete() { player.desert(Company.American); }
    }
}

questline Across(title: "Across the river", forCompany: Company.American) {
    quest LieLow(title: "Lie low", summary: "You are a deserter, and the garrison across the water knows your face.") {
        wait Low(days: 14, text: "Keep away from the river for a fortnight");
        reach Fort(place: "Fort Niagara", text: "Go up to the Americans' fort");
        talk Enlist(who: "officer@Fort Niagara", text: "Offer yourself to the officer at Fort Niagara") {
            Says() => "\"A redcoat? We'll see what you're worth. There's land in it, if you live.\"";
        }
    }
}

// Taken back across the river: the garrison has put him in the ranks again.
beat Retaken(title: "Brought back") ["deserter:taken"] {
    OnActivate() { player.note("The whole garrison watched the flogging. Nobody in your mess will look at you."); }
}

// With the peace, the story forgives a deserter still at large.
beat WithThePeace(title: "A pardon with the peace", date: date(1814, 12, 24)) {
    ActivateWhen() => player.deserted;
    OnActivate() { player.pardon(); say("Word comes with the peace: deserters who come in will not be punished."); }
}
```

Things to know:

- `startPath: Path.Crown` fixes the road on the character page; the player still picks a background on it. Without `startAt` the soldier is York's; with it, he belongs to the Crown garrison of that town or fort, or the nearest one that stands.
- After `player.desert(Company.American)` the `Ranks` questline is no longer offered to him (it is `forCompany: Company.Crown`) and `Across` is. Leave `to` out (`player.desert()`) for a man who goes over to nobody, and write his story `forCompany: Company.Free`.
- `WithThePeace` waits on its day until its `ActivateWhen` holds, so a man who deserts after it is pardoned the day he does. Give it a `CancelWhen` if the pardon should have a last day.

## An ending

A campaign does not end by itself; the world goes on. Mark the end with a beat that sums up what happened, from flags and counters.

```c
int ambushes;   // counted by the beats that ambush the brigades: ambushes++;

beat Epilogue(id: "epilogue", title: "The end of the war years", date: date(1821, 6, 1)) {
    OnActivate() {
        if (flags.has("governor_killed")) { say("They will talk of the Forks for a hundred years."); }
        else { say("The war years passed, and the Forks were never stained."); }
        if (ambushes >= 3) { say("The brigade roads were never safe, and the boatmen do not forget."); }
        flags.set("campaign_over");
    }
}
```

## A complete campaign

Here the recipes are put together: a short campaign about one hard winter at the Bay, eight beats. Copy it into the [[Web-Editor|editor]] and play it forward against different worlds: with the default world the raid on the stores never happens (Steady adherence only presses it, and spring cancels it); at **Strict**, with a scenario where `tension` starts at 60, it is forced in March and London's letter follows. In the default simulator world York Factory's stock is 0 (no scenario lists it), so the hungry spells come every three weeks all winter: list `"York Factory|Pemmican": 100` under `stocks` and they stop.

```c
campaign HardWinter(
    title: "The Hard Winter, 1811-1813",
    description: "A short campaign: a bad first winter at the Bay, a rival that smells weakness, and a spring the player can turn.",
    seed: 1811
);

const string london = "the Governor and Committee, London";
const int restless = 45;

int hungerSpells;

void toTheFactor(string subject, string text, int days) {
    news(from: london, to: Person.Factor, subject: subject, text: text, days: days);
}

bool atOdds() => world.tension >= restless && !player.did("truce");

// 1. A fixed fact: it happens on its day, whatever anyone does.
beat ShortShip(
    id: "short-ship",
    title: "The ship brings half an outfit",
    date: date(1811, 8, 25)
) {
    OnActivate() {
        world.addStock("York Factory", Item.TradeGoods, -30);
        world.setLondon(0.9, 0.9);
        say("The ship has brought half what was ordered. The Factor counts the bales twice.", Company.HBC);
        flags.set("short_outfit");
    }
}

// 2. A rate: each week the pemmican is low, a hungry spell may come.
beat HungrySpell(
    title: "A hungry spell at the Factory",
    repeat: days(21),
    rumour: "The men say the pemmican will not last the winter."
) ["short_outfit"] {
    AwakenWhen() => world.inMonths(11, 3) && world.stock("York Factory", Item.Pemmican) < 40 && chance(15);
    OnActivate() {
        hungerSpells++;
        world.addStock("York Factory", Item.DriedGoose, -10);
        say("Hunger at the Factory: that is {var:hungerSpells} spells since the short ship.", Company.HBC);
    }
}

// 3. An event the player can prevent, with a rumour while it is coming.
beat RaidOnTheStores(
    id: "raid-on-the-stores",
    title: "The Nor'Westers raid the Factory's stores",
    date: date(1812, 2, 15),
    window: days(30),
    lead: days(45),
    rumour: "Armed men have been seen on the river below the Factory at night."
) ["short_outfit"] {
    OnApproach() { world.raiseTension(15, "the Nor'Westers smell weakness"); }
    ResolveWhen() => world.date >= date(1812, 1, 1) && world.tension >= 80;
    CancelWhen() => player.did("truce") || world.date >= date(1812, 5, 1);
    OnActivate() {
        world.addStock("York Factory", Item.TradeGoods, -15);
        world.addStock("York Factory", Item.Powder, -5);
        shout("The Nor'Westers have broken into the Factory's stores.", Company.HBC);
        flags.set("stores_raided");
    }
    OnResolve() { flags.set("stores_raided"); }
    OnCancel() {
        if (player.did("truce")) { say("The truce holds: nobody comes for the stores this winter."); }
        flags.set("raid_averted");
    }
}

// 4. Two roads: what London hears depends on what happened.
beat LondonHearsOfTheRaid(title: "London hears of the raid") ["stores_raided"] {
    OnActivate() {
        toTheFactor("the winter's losses",
            "The Committee has learnt with displeasure of the loss of the stores. " +
            "You will answer for it in your next returns.", 300);
        world.setLondon(1, 0.85);
    }
}

beat LondonHearsOfThePeace(title: "London hears the winter was kept") ["raid_averted"] {
    OnActivate() {
        toTheFactor("the winter", "The Committee is pleased that the peace was kept at the Bay.", 300);
    }
}

// 5. Waiting for news to arrive, not merely to be sent.
beat TheFactorAnswers(title: "The Factor reads the Committee's letter") {
    AwakenWhen() => flags.has("heard:LondonHearsOfTheRaid") || flags.has("heard:LondonHearsOfThePeace");
    OnActivate() { say("The Factor has read London's letter, and has gone quiet.", Company.HBC); }
}

// 6. The boats: escalate if they are late, stand down if they come in.
beat BoatsOverdue(title: "The York boats are overdue", repeat: days(300)) {
    AwakenWhen() => world.inMonths(7, 9) && brigade("the York boats").isLate;
    OnAwaken() { say("The York boats are overdue.", Company.HBC); }
    ActivateWhen() => brigade("the York boats").daysLate >= 10;
    OnActivate() {
        shout("The York boats are ten days overdue: the Factor sends a canoe to look for them.", Company.HBC);
        if (atOdds()) { world.raiseTension(5, "the boats are overdue and the Nor'Westers are blamed"); }
    }
    ResolveWhen() => brigade("the York boats").isIn;
    OnResolve() { say("The York boats are in at last.", Company.HBC); }
}

// 7. An ending.
beat SpringReckoning(
    id: "spring-reckoning",
    title: "The winter is over",
    date: date(1813, 6, 1)
) {
    OnActivate() {
        if (hungerSpells >= 3) {
            say("A hard winter, and it will be remembered.");
        } else {
            say("The winter is over, and the Factory came through it.");
        }
    }
}
```

What each part shows:

1. `ShortShip`: a fixed fact that changes stock and London, and raises the flag the rest wait on.
2. `HungrySpell`: a rate (`chance` in a condition) on a repeating beat, with a counter put into text with `{var:...}`.
3. `RaidOnTheStores`: an event that is pressed (`lead`, `OnApproach`), can be done by the world (a guarded `ResolveWhen`), forced (`window`), or averted (`CancelWhen`, with a flag in `OnCancel`).
4. `LondonHearsOfTheRaid` and `LondonHearsOfThePeace`: two roads, each waiting on one flag.
5. `TheFactorAnswers`: waiting for a letter to be read (`heard:`).
6. `BoatsOverdue`: the full lifecycle on a repeating beat.
7. `SpringReckoning`: an ending that reads a counter.
