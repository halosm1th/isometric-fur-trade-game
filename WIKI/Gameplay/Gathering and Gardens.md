---
title: Gathering and Gardens
aliases: [Berries, Berry Picking, Spruce Beer, Missouri Gardens, Felling Trees, Firewood]
tags: [gameplay, system, gathering, food, scurvy]
status: partial
---

# Gathering and Gardens

**Status:** Partial. Berry picking, felling trees and the Missouri gardens' trade are implemented. The player cannot farm: farming and crafting are on the project's "longer goals" list. Code: `Scripts/World/StructurePainter.cs` (berry bushes, trees), `Scripts/Economy/Gardens.cs`, `PlayerController.UseSlotServer` (axe).

![[tile-saskatoon_bush.png|80]] ![[tile-cranberry_patch.png|80]] ![[tile-rosehip_bush.png|80]] ![[item-berries.png|56]] ![[tile-garden_plot.png|80]]

## Berries

Dried meat and pemmican do not answer the scurvy; **berries** and **spruce beer** do ([[Health, Wounds and Illness]]). Three shrubs are scattered through the bush as flora, like trees:

| Bush | Where it grows | In fruit | Yield per pick |
|---|---|---|---|
| **Saskatoon** | the Shield, the parkland, the mountains | July - August | 4 berries |
| **Cranberry patch** | muskeg and tundra | August - October | 5 berries |
| **Rosehip** | plains and parkland | July - February (hangs on the bush all winter) | 3 berries |

Stand at a bush in fruit and the prompt reads **"E  Pick the saskatoon bush"**. You strip it, get the berries (a 1.2 s stoop), and the bush shows picked over for **30 days**. January - March the non-rosehip bushes are a mound of snow. Each berry feeds 10 Food, weighs 0.6 lb per berry unit, and **resets scurvy** ("berries at last"). Pemmican pounded with berries comes out a quarter larger when NPCs make it.

Camps and posts gather automatically through **July, August and September**: bands add 3-4 berries per mouth, posts 2 per mouth to their stores.

## Spruce beer

Out of berry season, a post with a **kettle and firewood** brews **spruce beer** when its men have gone a fortnight without anything fresh ("spruce beer brewed against the scurvy"). A tankard feeds 4, quenches 40, warms 3, and answers scurvy the way berries do. See [[Food and Drink]].

## Felling trees and firewood

Your **axe** (you start with one) fells the nearest spruce within 150 px when you use it. A tree gives **4 firewood** (6 once Woodcraft is 0.5 or more), takes 1.2 s (0.8 s with Woodcraft 0.5) and trains Woodcraft by 0.02. Firewood (5 lb each) is the fuel for hearths and campfires: 3 hours each. Felled trees stay cut (they are saved). If no tree is near: "No tree within reach of the axe. The bush starts west and south of the post." See [[Fire and Warmth]] and [[Skills]]. Stacking firewood on the post's woodpile is a standard morning order ([[Daily Orders]]).

## The gardens of the Missouri

The Mandan, Hidatsa and Arikara are **farming towns in earth lodges**, not hunting bands, and the great market of the plains ([[Missouri Villages]]). The game models their trade:

- **Harvest.** In September and October each town adds **30 corn per mouth** to its stores ("the corn is in, and the lodges are full of it"). In July and August green corn and squash add 3 berries per mouth, keeping them clear of scurvy.
- **The fair.** June to September, any camp within about a fortnight's travel brings dried meat and takes corn at **two for one**.
- **The northern leg.** The [[Assiniboine]] are the middlemen between the Missouri towns and the Bay: they buy up to 40 corn a week, paying in pemmican, and the posts buy it from them. You see "<band> are back from the Missouri towns with N of corn."

Corn (food 32, 1 lb, 0.25 MB) is food and trade goods, **not** a cure for scurvy. See [[Trade Goods]] and [[Economy]].

![[tile-corn_scaffold.png|80]] ![[tile-garden_harvested.png|80]] ![[tile-earth_lodge.png|80]]

## Tips

> [!tip]
> Pick berries in July and August when the bushes are loaded; stow a handful (they weigh little) for the winter, and eat one if your HUD shows scurvy.

> [!tip]
> Always carry firewood and flint. A fire in the open needs both ([[Fire and Warmth]]).

## See also

[[Hunting]] - [[Fishing]] - [[Fire and Warmth]] - [[Food and Drink]] - [[Health, Wounds and Illness]] - [[Terrain and Biomes]]
