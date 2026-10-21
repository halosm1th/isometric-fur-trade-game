# Longer Goals

Last verified against the code: 2026-10-13

Status key (checked against the code and `WIKI/Development/Development Status.md`): **not built** unless noted.

1. Draw out timeline from say 1766-1821. *(Not built: the game runs 1811-1821; `Campaigns/historical.json` has 13 beats and no pre-1811 content.)*
1.1 If lewis and clark are killed America should go haywire and launch and whole army. *(Not built in the world; only an alternate campaign script exists, `Campaigns/Gemini and Clark.json`.)*
2. Make it so that characters age over time. *(Not built: no ageing or natural decline; heirs and succession on death are in.)*
3. character creator with different faces, statws, and when applying clothes differnt things come and ysing this system to generate ppl. *(Not built: the "Who you are" page picks a name, road in, background and traits, but there is no face or clothing customisation. Each nation has a fixed sprite sheet, wired in `Agents/Appearance.cs`.)*
4. Horenback riding and plains warfare betweena nd with indgeinous people. *(Partly built: horses, herds, riding and horse raids between nations are in (`Economy/Plains.cs`), and plains camps run the buffalo together. No war parties, mounted battle or truce yet; see `docs/specs/story-plains-years.md`.)*
5. Farming. *(Partly: camps, posts and the Missouri towns keep gardens and gather berries and corn as a weekly sum in `Economy/Gardens.cs`; the player cannot plant or tend a field.)*
6. crafting, and complex multi step manufacturing chains. *(Not built: no recipes or facility-gated crafting.)*
7. weather effects on screen. *(Not built on screen: weather is simulated regionally (six states, `Weather.At`) but shows only as a colour modulate; no snow, rain or fog particles.)*
8. expanded american parts. *(Not built beyond American posts on the map, the Fort Manuel Lisa post and the Missouri villages.)*
