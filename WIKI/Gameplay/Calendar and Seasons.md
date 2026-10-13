---
title: Calendar and Seasons
aliases: [Clock, Time, Seasons, Day and Night, Game Clock]
tags: [gameplay, system, time, seasons]
status: implemented
---

# Calendar and Seasons

**Status:** Implemented. Code: `Scripts/Core/GameCalendar.cs`; HUD in `Scripts/UI/CalendarHud.cs`.

Everything time-dependent (light, weather, the annual ship, the hunts, the freezing of the water) hangs off the game calendar. Dates are on **Hudson Bay terms**: York Factory is at 57 degrees north, so daylight swings from about nine and a half hours in December to nearly sixteen in June.

## The clock

| Value | Setting |
|---|---|
| Default start | 1 January 1811, 09:00 (a character made on the New Game page is dated to the ship, brigade or camp day instead; see [[Game Overview]]) |
| Seconds per game hour | **30** (a game day is 12 minutes of play; a 365-day year is about 73 hours at the base rate) |
| Fast-forward (hold `T`) | x40 |
| Waiting / sleeping skip speed | x400 |
| River road or map travel | at least x6 clock rate |
| Longest single frame | 0.25 game hours (keeps the simulation sane) |
| Host clock keys | `Space` stops/starts; `[` doubles and `]` halves seconds per hour (0.5 - 120 s) |

The calendar HUD (top of the screen) reads like `14 June 1811   07:30` and, beneath it, the season chapter and the sky, e.g. `Breakup - the year's trade is won here - overcast`. A `>>` marks fast-forward and `|| PAUSED` a stopped clock. Because the HUD compares the clock against an old 10 s/hour baseline, it also prints `(30 s/hour)` at the default speed.

### Uneven pacing

The design aim is that the clock is not flat: the trading season and the reckoning run close to live while deep winter nights go by in a breath. With `UnevenPacing` on (the default) the displayed speed is divided by a **pacing factor** (clamped 0.25 - 3; a factor above 1 slows the clock):

| Condition | Factor |
|---|---|
| Supply ship docked | x2.2 |
| Breakup season (May-June) | x1.5 |
| July and August | x1.3 |
| Night (before 05:00 or after 21:00) | x0.45 (x0.3 in winter) |
| Winter daytime | x0.65 |
| Blizzard or gale | x0.8 |
| One person near you | x1.2; two or more x1.5 |
| In a fight (attacked in the last 8 s) | x2.5 |

> [!note]
> The design document aims for a year in four to six hours of play. At the current 30 s/hour a year is far longer than that target, even allowing for the night and winter speed-ups. A comment in the code records "a day in twelve minutes of play (the user's pick, Oct 2026)".

## Seasons

The calendar has four seasons by month. The sea and the rivers are frozen only in **Winter**.

| Season | Months | Chapter title | What it means |
|---|---|---|---|
| **Winter** | November - April | "Winter - hold the men together" | Bay frozen; walk on the ice, travel by snowshoe and sled; canoes frozen in; the main trapping season (prime pelts). Water is melted from snow at a fire. |
| **Breakup** | May - June | "Breakup - the year's trade is won here" | Ice goes out, geese arrive (spring flight 5-30 May), rivers open, bands come to the post. |
| **Open water** | July - August | "The ship - the reckoning" | The supply ship can reach the bay; the Nor'Westers' brigade arrives. |
| **Freeze-up** | September - October | "Freeze-up - the postings are decided" | The ship must leave; last brigades in; autumn goose flight; the fall hunt. |

### Annual events

These fire once a year and appear on the HUD for eight seconds:

| Date | Event | HUD message |
|---|---|---|
| 5 June | Breakup | "The ice is going out" |
| 15 June | Summer bison hunt | "The summer hunt is out on the plains" |
| 12 July | Brigade | "The Nor'Westers' brigade is in from Fort William" |
| 20 August | Ship arrives | "The ship is in from England" |
| 15 September | Ship departs | "The ship has sailed" |
| 15 October | Fall bison hunt | "The fall hunt is out on the plains" |
| 10 November | Freeze-up | "The bay has frozen" |

Other dated rhythms: the **autumn goose flight** runs 20 August - 15 October and the spring flight 5 - 30 May ([[Hunting]]); berries ripen July-October ([[Gathering and Gardens]]); beaver pelts are **thin** from June to September and **prime** otherwise ([[Furs and Pelts]]); the band camp moves to the post on 5 June and back to the bush on 10 November (the notes say so; see [[Cree Camps]]); in early June a husband's father-in-law expects his yearly gift ([[Marriage and Country Wives]]).

The HUD also carries a reckoning counter such as `N days to the ship  -  packs 30 of 60`, counted down to 20 August (day 232 of the year) for Company men, or "the brigade" for Nor'Westers.

## Day length and light

Sunrise and sunset come from latitude and the date, then are nudged so days are never shorter than 12.6 hours (9.5 in December and January). Twilight lasts 1.5 hours each side. Approximate figures by month:

| Month | Day length (h) | Sunrise | Sunset |
|---|---|---|---|
| Jan | 9.5 | 07:40 | 17:05 |
| Feb-Mar | 12.6 | 06:00 | 18:35 |
| Apr | 13.6 | 05:30 | 19:05 |
| May | 14.9 | 04:50 | 19:45 |
| Jun | 15.7 | 04:25 | 20:10 |
| Jul | 15.4 | 04:35 | 20:00 |
| Aug | 14.2 | 05:10 | 19:25 |
| Sep-Nov | 12.6-12.7 | 06:00 | 18:35 |
| Dec | 9.5 | 07:40 | 17:05 |

(Computed from the formula in `GameCalendar.DayLengthHours` for the middle of each month.) Cold bites harder at night: see [[Needs and Survival]]. Weather rolls from the season: see [[Weather]].

## Command-line aids

`--hour=N`, `--date=YYYY-MM-DD` and `--speed=SECONDS` set the starting hour, date and seconds per game hour.

## See also

[[Weather]] - [[Sleeping and Resting]] - [[Travel and Wayfinding]] - [[Setting and Timeline]] - [[Controls]] - [[Hunting]] - [[Trapping]]
