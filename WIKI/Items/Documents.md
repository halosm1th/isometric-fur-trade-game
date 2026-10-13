---
title: Documents
aliases: [Letters, Cut Items]
tags: [item, index, cut-content]
status: planned
---

# Documents

**Status:** Planned. Documents are not items yet; eight finished icons are waiting on code.

Letters, dispatches, ledgers and journals exist in the game as **systems** (the message system in `Scripts/Core/Message.cs`, the accounts that sail with the ship, the player's journal panel), not as objects you can carry or drop. Design doc 12.5 says every item should be a physical object, 'a map, a letter', so the art exists but the `ItemId`s do not.

## Cut content: unused icons

These PNGs sit in `assets/sprites/items/` and are imported but no `ItemId` points at them (checked against `Items.cs`; `docs/asset-inventory.md` lists seven of them and counts `pemmican_bag` as borrowed by Corn, which is stale since Corn now has its own `corn.png`).

| Icon | Image | Likely use |
|---|---|---|
| `letter.png` | ![[item-letter.png|64]] | Mail between posts and ships; the message system already moves letters as data. |
| `dispatch.png` | ![[item-dispatch.png|64]] | The Committee's packet, the Council's mandate; inert until opened. |
| `ledger.png` | ![[item-ledger.png|64]] | The post accounts and the Made Beaver tallies. |
| `journal.png` | ![[item-journal.png|64]] | The player's journal panel. |
| `fur_pack.png` | ![[item-fur_pack.png|64]] | A baled pack of furs (a pack was about 90 lb): the unit sent to London. |
| `medicine_bag.png` | ![[item-medicine_bag.png|64]] | Treating wounds (design doc 17.5). |
| `bread_plate.png` | ![[item-bread_plate.png|64]] | A served meal; overlaps with the loaf of bread. |
| `pemmican_bag.png` | ![[item-pemmican_bag.png|64]] | A 90 lb bag of pemmican, the real trade unit. |

## What the message system does today

- A **directive** from the Committee (price, quota, year) rides the ship as a packet, is carried ashore by the bosun and does nothing until the Factor opens it. If a Nor'Wester reads it first, the NWC sets its own price to 115 percent of London's and tension rises by 6.
- **Accounts** (returns against quota) sail home with the ship and are read in London 75 days later; reading them runs [[London Committee Orders]].
- A **mandate** from the Fort William council comes north with the July brigade for the wintering partner; see [[NWC Ledger]].
- Other kinds exist for letters to chiefs, rumours, replies, news and outpost reports. See [[Information and Messages]].

## See also

- [[Items]]
- [[Asset Inventory]]
- [[Information and Messages]]
