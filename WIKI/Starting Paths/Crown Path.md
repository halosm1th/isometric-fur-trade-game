---
title: Crown Path
aliases: [Soldier Start, Garrison Start, Redcoat Start, King's Service]
tags: [starting-paths, crown, soldier, prologue, war-of-1812]
status: implemented
---

# Crown Path

**Status:** Implemented. A private soldier of one of His Majesty's garrisons on the Lakes frontier. A campaign can make it every player's road with `startPath: Path.Crown` (see the scripting [[World-Reference|World reference]]).

| | |
|---|---|
| **Road in** | On the strength of a garrison of Upper Canada |
| **Start date** | 4 June (the King's birthday), 07:00, unless the campaign gives its own `start` |
| **Place** | The parade inside the garrison's fort: Fort York at York by default, or the Crown garrison a campaign's `startAt` names (or the nearest one that stands) |
| **Faction / home post** | The Crown (`Faction.Crown`); no company post and no company books |
| **Backgrounds** | Private of the 41st Foot, Royal Newfoundland Fencible, Militia volunteer |
| **Sprite** | The garrisons' men's sheet (`LabourerFrames`) |
| **Rank / standing** | private, 20 |

Blurb: *You are on the strength of a garrison of the Lakes frontier - Fort York, Fort George at Niagara, Fort Malden at Amherstburg - a shilling a day less stoppages, drill on the parade and the Americans across the water. The Crown feeds you, clothes you and flogs you; the King's birthday is a holiday and a feu de joie. A man who goes over the river is a deserter, and the garrison will want him back.*

## Kit

The pack is cleared and replaced with the King's: a musket, 2 powder, a capote, a knife, 4 bread, 2 water, a flint and 24 coin (two shillings). The background adds to it.

## Backgrounds

- *Private of the 41st Foot* - Hunting 0.25, Woodcraft 0.2, one more powder. The regiment had been in the Canadas since 1799.
- *Royal Newfoundland Fencible* - Woodcraft 0.35, a fishing line and a tumpline. The Fencibles served as often in the Provincial Marine's schooners as on the parade.
- *Militia volunteer* - Hunting 0.35, Trade 0.2, an axe and 2 more bread. A farmer's son in the township's flank company.

## Which garrison

The garrison is the town (or fort) the campaign's `startAt` names, if it stands with a garrison of the Crown; otherwise the nearest Crown garrison to that place; otherwise York's. In 1811 the Crown holds Fort York (York), Fort George (Niagara), Fort Malden (Amherstburg) and Fort St. Joseph (St. Joseph Island). Detroit, Fort Niagara and Michilimackinac are American, and Queenston has no fort.

## The prologue: "The King's birthday"

Opening line: *Fort York, York. You are a private soldier of His Majesty's garrison.* The garrison is drawn up on the parade in its best coats for the King's birthday (or "the general muster" on any other day). Two privates of the garrison stand beside you in the rear rank and share a memory with you ("have shared a mess in the barracks since the winter", +12 regard each). The sergeant is the garrison's first man named *Sergeant*; the officer is the garrison's commanding officer.

### Stand easy with your mates
They grumble about the stoppages, and one nods at the American shore: the Americans give a recruit a hundred and sixty acres.
- **"Hold your tongues, the pair of you."** +4 regard from both.
- **"And how would a man get over there?"** They remember you asked (a memory they may pass on), and a journal note.

### Listen to the sergeant
The inspection after the firing, the triangle for any man drunk, and the last man who tried for the other shore, "brought back in irons and flogged till the drummers were tired". A journal note.

### See who is watching you
A man in a farmer's coat by the gate: a boat goes over from below the town on dark nights, two dollars and a coat that isn't red, ask at the landing for Abner.
- **Remember the name:** raises the campaign flag `start:knows-the-boatman` and a journal note.
- **Tell the sergeant:** standing +3 and a journal note.
- **Forget it:** nothing.

### Fall in for the feu de joie
The line fires by files, three times, and three cheers for the King. You fall out on the parade (or, if the campaign's `startAt` is somewhere other than your garrison, you are sent there).

## Desertion

A campaign can take a soldier (or anyone) over to the other side with `player.desert(...)`. From then on:

- he is on the side he went over to (or nobody's), and quests for his old side are no longer offered;
- everyone of the side he left thinks 30 worse of him;
- the **soldiers and officers of that side's garrisons** go for him on sight, and one whose blow would kill him takes him alive instead: he is marched back to that garrison, tried and flogged, and put back on its strength (the flag `deserter:taken`);
- a campaign's `player.pardon()` ends it.

See [[Effects-in-the-Game#Deserters|Deserters]] in the scripting wiki.

## See also
[[Starting Paths]] | [[HBC Path]] | [[NWC Path]] | [[Player Character]] | [[Standing]]
