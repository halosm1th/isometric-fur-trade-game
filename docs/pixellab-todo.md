# New character sheets still to draw with PixelLab

The character-variety branch (`claude/more-characters-bd3cae`) gives the existing
sheets dyed garments and grey hair. The roles below need new PixelLab art.
`Appearance` already uses each sheet once it is installed.

Run these from `tools/sprites/pixellab`, with `PIXELLAB_API_KEY` set in your shell
(never in a file). Each `make` uses about 45 generations. After installing, run
`godot --headless --path . --import`.

## 1. Métis (no new reference art needed)

```
python pixellab.py outfit metis_ref capote_ref.png ../../../assets/sprites/voyageur/idle_front.png ../../../assets/sprites/voyageur/idle_side.png ../../../assets/sprites/voyageur/idle_back.png
python roles.py seed metis out/metis_ref_0.png out/metis_ref_1.png out/metis_ref_2.png
python roles.py make metis
python roles.py check metis
python roles.py install metis
```

## 2. Roles that need three reference idles drawn first

`cree_boy`, `cree_girl`, `orkneyman` (bare-headed), `cree_woman_cloth` and
`ship_captain` each need three reference idles: front, three-quarter side and back,
180x170, with a transparent background. Then:

```
python roles.py seed ROLE FRONT.png SIDE.png BACK.png
python roles.py make ROLE
python roles.py check ROLE
python roles.py install ROLE
```

Dyed garments already cover elders, except voyageurs, clerks, sailors and the Ojibwe.
