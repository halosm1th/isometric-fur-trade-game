# Character art still to draw with PixelLab

Done: the Métis sheet (42bb3235); the Orkneyman, the Cree woman in a stroud dress,
Cree boys and girls, and the ship's captain (f2e4c471); the women of the plains,
woodlands, Dene and Plateau (2e03399f).

Run from `tools/sprites/pixellab`, with `PIXELLAB_API_KEY` set in your shell
(never in a file). `python pixellab.py balance` shows the generations left.

## Actions not drawn yet

Walking with a load, running, sneaking and writing. Each one needs a new doll motion
in `tools/sprites/doll/render.py` (skeleton mode, merged into skeletons.json) and an
entry in `roles.py` ACTIONS. After that, every role costs about 3 generations per
direction per action.
