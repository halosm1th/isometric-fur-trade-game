# Longer Goals

Last verified against the code: 2026-10-21 (see `docs/audit.md`)

Status key (checked against the code and `WIKI/Development/Development Status.md`): **not built** unless noted.

1. Draw out timeline from say 1766-1821. *(Not built: the game runs 1811-1821; `Campaigns/historical.json` has no pre-1811 content. Seven other campaigns ship in `Campaigns/`, including the American road and "The Plains Ablaze (1801-1821)".)*
1.1 If lewis and clark are killed America should go haywire and launch and whole army. *(Not built in the world; only an alternate campaign script exists, `Campaigns/Gemini and Clark.json`.)*
2. Make it so that characters age over time. *(Partly built: everyone is a year older each New Year, NPCs past 45 slow a little, children are born and grow into the post at 16 (`World/Population.cs`). The player does not decline and nobody dies of age.)*
3. character creator with different faces, statws, and when applying clothes differnt things come and ysing this system to generate ppl. *(Not built: the "Who you are" page picks a name, road in, background, traits and an ambition, but there is no face, body or clothing customisation. Each nation has a fixed sprite sheet, wired in `Agents/Appearance.cs`. Plan in the memory note "character system plan".)*
4. Horenback riding and plains warfare betweena nd with indgeinous people. *(Mostly built: horses, herds, riding, horse raids (`Economy/Plains.cs`), the communal buffalo run (`Ecology/BuffaloRun.cs`), mounted war parties with morale and retreat, and truces (`Economy/War.cs`). Missing: a battle larger than one party, truces as Accords, and the player leading a party.)*
5. Farming. *(Partly: camps, posts and the Missouri towns keep gardens and gather berries and corn as a weekly sum in `Economy/Gardens.cs`; the player cannot plant or tend a field.)*
6. crafting, and complex multi step manufacturing chains. *(Not built: no recipes or facility-gated crafting. NPC households dry meat and make pemmican (`MakePemmicanGoal`) and brew spruce beer; the player cannot.)*
7. weather effects on screen. *(Built: `UI/WeatherOverlay.cs` draws snow, rain, whiteout, river fog and the aurora over the regional weather.)*
8. expanded american parts. *(Partly: `Faction.American`, the eight American posts with their masters and men, the bands' regard for the Americans and an American story road. No Astor or St. Louis background nodes.)*
