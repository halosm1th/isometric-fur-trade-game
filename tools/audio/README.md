# Audio tools

- `process.py <repo>`: cuts the raw Freesound downloads in `assets/external/soundFX/` into the clips in `assets/audio/` (trims, footstep and chop slicing, loop crossfades, resampling).
- `synth.py <repo>`: generates the synthesised stand-ins (plank steps, rain, wolves, body fall, musket reload). Seeded, so reruns give the same files.
- `audiolib.py`: shared helpers (load any WAV, resample, slice by onsets, crossfade a loop).

Needs numpy and scipy. Run with `python -I tools/audio/<script>.py .` from the repo root, then `--headless --path . --import`.
