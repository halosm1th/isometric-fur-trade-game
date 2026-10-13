# Documentation map

Four kinds of document live in this repo. Put new writing in the right one.

| Where | What it is | Audience | Rule |
|---|---|---|---|
| `WIKI/` | The player- and contributor-facing wiki (Obsidian-style, `[[links]]`, 239+ pages) | Anyone | A page says **Implemented** only if the code runs it. Every page has `title` and `status` frontmatter |
| `docs/fur-trade-game-design.md` | The design document: intent, principles, systems by section (§1-21) | Designers | Describes what the game *should* be. Implementation notes are added in place |
| `docs/specs/` | **Specs for things not built yet**: a story arc, a system, a tool | Whoever builds it | Written to be handed over: scope, phases, acceptance tests, open questions |
| `docs/*.md` (others) | Working lists: `longer_goals`, `world-content-needed`, `asset-*`, `audio-prompts` | Owner / artists | Each starts with "Last verified against the code: date" |
| `tools/*/README.md` | How to use each tool in five minutes | Anyone | Copy `tools/CampaignTool/README.md` |

## Start here

- **What is the game?** `WIKI/Game/Game Overview.md`, then `WIKI/Game/Design Principles.md`.
- **What works today?** `WIKI/Development/Development Status.md`, then `WIKI/Development/Roadmap.md`.
- **How is it built?** `WIKI/Development/Architecture.md`.
- **How do I write a campaign?** `tools/CampaignTool/README.md` and `docs/wiki/Home.md`.
- **What is next?** `docs/specs/`.

## Specs

| Spec | Summary |
|---|---|
| [story-plains-years.md](specs/story-plains-years.md) | HBC hand -> Auld -> posted inland -> friends on the plains -> opposition -> hunt -> intertribal war -> 1821. Act-by-act, with the engine work ranked |
| [global-trade.md](specs/global-trade.md) | A world fur market, money, a distance gradient and a shock calendar, with phases and acceptance tests |
| [tools.md](specs/tools.md) | Tools T0-T8: wiki checker (built), market sim, economy dashboard, scenario seeds, soak harness and more |

## Keeping the docs true

```
python3 tools/docs/check_wiki.py            # broken links, missing images, frontmatter, orphans
python3 tools/docs/check_wiki.py --strict   # warnings also fail
```

Conventions:

- **Status vocabulary** (frontmatter `status:`): `implemented`, `partial`, `planned`, `reference`, `index`, `stub`.
- **Dates and commits.** When a page states a fact about the code, name the file; when it is a snapshot, give the date.
- **Unsourced history** is marked *(check)* until it has a citation (see tool T8 in `docs/specs/tools.md`).
- **Status drift is the main risk.** Example found on 2026-10-13 and fixed: `Development Status.md` said horses were "planned, nothing exists" after `Economy/Plains.cs` had landed. When you merge a system, grep the wiki for its name.

## Known gaps in the wiki (2026-10-13)

No pages yet for systems that shipped recently: **Horses and herds**, **Horse raids and feuds**, **Missouri trade** (horse fair, winter corn, the December expedition), **regional weather and ground frost**, **the web campaign editor**. Each should be a short page linked from `Economy` or `Gameplay`.
