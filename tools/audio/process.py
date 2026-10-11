"""Cut the raw Freesound downloads in assets/external/soundFX into game clips
under assets/audio/. Run from the repo root:  python -I process.py <repo>"""
import sys, os, shutil, warnings
warnings.filterwarnings("ignore")
sys.path.insert(0, os.path.dirname(__file__))
from audiolib import *

repo = sys.argv[1]
SRC = os.path.join(repo, "assets/external/soundFX")
OUT = os.path.join(repo, "assets/audio")
for d in ("sfx", "ambient", "ui", "music"):
    os.makedirs(os.path.join(OUT, d), exist_ok=True)

def src(name): return load(os.path.join(SRC, name))
def cut(x, sr, a, b): return x[int(a * sr): int(b * sr)]
def out(sub, name, x, sr): save(os.path.join(OUT, sub, name), x, sr); print(sub, name, f"{len(x)/sr:.2f}s", x.shape[1], "ch")

SFX_SR = 44100

def oneshot(sub, name, x, sr, a=None, b=None, peak=0.9, to_mono=True, fout=0.05):
    if a is not None: x = cut(x, sr, a, b)
    if to_mono: x = mono(x)
    x = resample(x, sr, SFX_SR)
    out(sub, name, fade(normalize(x, peak), SFX_SR, 0.003, fout), SFX_SR)

def steps(prefix, x, sr, starts, length, peak, take=5):
    for i, t in enumerate(starts[:take]):
        oneshot("sfx", f"{prefix}_{i+1:02d}.wav", x, sr, max(0, t - 0.02), t - 0.02 + length, peak, fout=0.08)

def loop(name, x, sr, a, b, to_sr, peak, xf=2.0, stereo=True):
    x = cut(x, sr, a, b)
    if not stereo: x = mono(x)
    x = resample(x, sr, to_sr)
    out("ambient", name, loopify(normalize(x, peak), to_sr, xf), to_sr)

# ---- footsteps -------------------------------------------------------------
sr, x = src("341923__seth-m__snow-footsteps.wav")
on, _ = onsets(x, sr)
steps("step_snow", x, sr, on[2::3], 0.5, 0.6)          # spread across the take
sr, x = src("33891__jrwentertainment__snow-footsteps/614137__jrwentertainment__snow-footsteps-crunch-1-mono.wav")
steps("step_snow_crust", x, sr, [5.32, 7.56, 9.57, 12.98, 14.69], 0.55, 0.6)
sr, x = src("543775__walthamstow_walker__walking-through-thick-wet-mud.wav")
steps("step_mud", x, sr, [7.02, 10.7, 21.77, 26.25, 34.59], 0.7, 0.55)

# ---- work ------------------------------------------------------------------
sr, x = src("847818__elschorscho__chopping-wood_medium-459.wav")
steps("axe_chop", x, sr, [0.03, 0.61, 1.37, 2.33, 3.9], 0.45, 0.85)
sr, x = src("670300__brunoauzet__tree-cut-down.wav")
oneshot("sfx", "tree_fall.wav", x, sr, 0.4, 13.0, 0.9, fout=1.0)

# ---- weapons and events ----------------------------------------------------
sr, x = src("244345__willlewis__musket-explosion.wav")
oneshot("sfx", "musket_fire.wav", x, sr, 0.0, 1.9, 0.95, fout=0.4)
sr, x = src("565794__stomachache__distant-cannon-fire-simulated.wav")
oneshot("sfx", "cannon_distant.wav", x, sr, 0.0, 3.8, 0.95, fout=0.8)
sr, x = src("724854__tsp-talk__cast_iron_bell_ringing_mono_md441u_192khz_32bit-float_240224_001_tr4.wav")
oneshot("sfx", "fort_bell_work.wav", x, sr, 13.6, 23.5, 0.85, fout=1.5)
oneshot("sfx", "fort_bell_alarm.wav", x, sr, 73.2, 86.0, 0.9, fout=1.5)
sr, x = src("737896__vrymaa__ice-lake-fractures.wav")
oneshot("sfx", "ice_crack_01.wav", x, sr, 7.8, 12.2, 0.9, fout=0.6)
oneshot("sfx", "ice_crack_02.wav", x, sr, 22.7, 26.5, 0.9, fout=0.6)
oneshot("sfx", "ice_crack_03.wav", x, sr, 39.2, 46.5, 0.9, fout=0.8)
sr, x = src("506665__rudmer_rotteveel__wood-creak-single-v10.wav")
oneshot("sfx", "wood_creak.wav", x, sr, 0.0, 1.2, 0.6, fout=0.2)
sr, x = src("348247__newagesoup__dogsled-passes-by-l-r-panning.wav")
x = resample(normalize(cut(x, sr, 0.0, 10.5), 0.8), sr, SFX_SR)
out("sfx", "dogsled_pass.wav", fade(x, SFX_SR, 0.01, 1.5), SFX_SR)   # stereo: the pan is the point

# ---- ambient loops ---------------------------------------------------------
AMB_SR = 32000
sr, x = src("505999__dblover__howling-winter-storm-ambient-sounds.wav")
loop("amb_winter_wind.wav", x, sr, 31.0, 76.0, AMB_SR, 0.55)
loop("amb_blizzard.wav", x, sr, 52.0, 102.0, AMB_SR, 0.9)
sr, x = src("869104__bassimat__mountain-stream-flowing-past-from-the-riverbank-boscoverde-dolomites-torrente-pettorina.wav")
loop("amb_river.wav", x, sr, 20.0, 65.0, AMB_SR, 0.6)
sr, x = src("462440__hectorsoto19__mosquito_01.wav")
loop("amb_mosquitoes.wav", x, sr, 0.3, 9.8, AMB_SR, 0.5, xf=1.0)
sr, x = src("503208__danjocross__paddling-a-kayak.wav")
loop("canoe_paddling_loop.wav", x, sr, 72.0, 104.0, AMB_SR, 0.8, xf=1.5)
sr, x = src("410404__alexanderche__fire-crackling-fireplace-bbq.wav")
loop("fire_hearth_loop.wav", x, sr, 0.0, 16.6, SFX_SR, 0.8, xf=1.5, stereo=False)

# ---- compressed files: copy as they are ----------------------------------
for s, d in [
    ("805297__jahjahjahjah__goose-honk.mp3", "sfx/goose_honk.mp3"),
    ("683434__saha213131__door-open.mp3", "sfx/door_open.mp3"),
    ("538024__tairblenn__flint-and-steel.mp3", "sfx/flint_strike.mp3"),
    ("275466__adharca__raven-call.ogg", "sfx/raven_croak.ogg"),
    ("39339__stackpool__loonsnippets.ogg", "sfx/loon_call.ogg"),
    ("737005__redagee__page-turn-2.ogg", "ui/ui_page_turn.ogg"),
    ("856167__brktkrgll__ink-quill-writing-on-parchment-asmr.mp3", "ui/ui_quill_write.mp3"),
]:
    shutil.copyfile(os.path.join(SRC, s), os.path.join(OUT, d)); print("copy", d)

# The ship recording is 24 MB of MP3. MP3 is a run of independent frames,
# so a byte range starting on a frame header is itself a valid file.
def mp3_slice(path, dst, start_frac, seconds):
    data = open(path, "rb").read()
    rates = [0, 32, 40, 48, 56, 64, 80, 96, 112, 128, 160, 192, 224, 256, 320, 0]
    i = int(len(data) * start_frac)
    while not (data[i] == 0xFF and (data[i + 1] & 0xE0) == 0xE0 and (data[i+2] >> 4) not in (0, 15)): i += 1
    kbps = rates[data[i + 2] >> 4]
    n = kbps * 1000 // 8 * seconds
    open(dst, "wb").write(data[i: i + n]); print("mp3 slice", dst, kbps, "kbps", n // 1024, "KB")
mp3_slice(os.path.join(SRC, "195193__pimfeijen__largewoodenship.mp3"), os.path.join(OUT, "ambient/ship_creak_loop.mp3"), 0.3, 60)
