"""Synthesised stand-ins for sounds nobody has recorded yet. Usage:
python -I synth.py <repo>.  Deterministic (seeded) so reruns give the same files."""
import sys, os, warnings
warnings.filterwarnings("ignore")
sys.path.insert(0, os.path.dirname(__file__))
import numpy as np
from scipy.signal import butter, sosfilt, fftconvolve
from audiolib import save, normalize, fade, loopify

repo = sys.argv[1]
SFX = os.path.join(repo, "assets/audio/sfx"); AMB = os.path.join(repo, "assets/audio/ambient")
SR = 44100
rng = np.random.default_rng(1811)

def bp(x, lo, hi, sr=SR, order=2):
    return sosfilt(butter(order, [lo, hi], btype="band", fs=sr, output="sos"), x, axis=0)
def lp(x, f, sr=SR, order=2):
    return sosfilt(butter(order, f, btype="low", fs=sr, output="sos"), x, axis=0)
def hp(x, f, sr=SR, order=2):
    return sosfilt(butter(order, f, btype="high", fs=sr, output="sos"), x, axis=0)
def t(sec, sr=SR): return np.arange(int(sec * sr)) / sr
def decay(n, tau, sr=SR): return np.exp(-np.arange(n) / (tau * sr))
def col(x): return x[:, None] if x.ndim == 1 else x
def reverb(x, secs=1.2, wet=0.3, sr=SR, bright=3000):
    ir = rng.standard_normal(int(secs * sr)) * decay(int(secs * sr), secs / 6, sr)
    ir = lp(ir, bright, sr)
    y = fftconvolve(x, ir)[: len(x) + int(secs * sr)]
    y = y / (np.abs(y).max() + 1e-9) * np.abs(x).max()
    out = np.zeros(len(y)); out[: len(x)] += x * (1 - wet); out += y * wet
    return out
def write(dirn, name, x, sr=SR, peak=0.85, fin=0.002, fout=0.05):
    x = col(np.asarray(x, dtype=np.float32))
    save(os.path.join(dirn, name), fade(normalize(x, peak), sr, fin, fout), sr)
    print(name, f"{len(x)/sr:.2f}s")

# ---- footsteps on wooden planks -------------------------------------------
for i in range(5):
    n = int(0.42 * SR); x = np.zeros(n)
    # the board's body: a couple of low, quickly damped modes
    for f, a, tau in [(rng.uniform(95, 140), 1.0, 0.05), (rng.uniform(210, 290), 0.6, 0.035), (rng.uniform(420, 560), 0.25, 0.02)]:
        x += a * np.sin(2 * np.pi * f * t(0.42)) * decay(n, tau)
    # the heel's knock
    knock = bp(rng.standard_normal(n), 700, 2600) * decay(n, 0.012)
    x += 0.9 * knock
    # a second, softer contact (toe) 60-90 ms later
    off = int(rng.uniform(0.06, 0.09) * SR)
    x[off:] += 0.35 * (bp(rng.standard_normal(n - off), 500, 1800) * decay(n - off, 0.01))
    # sometimes the board creaks
    if i in (1, 3):
        tt = t(0.22); f0 = np.linspace(rng.uniform(280, 340), rng.uniform(420, 520), len(tt))
        creak = np.sin(2 * np.pi * np.cumsum(f0) / SR) * (0.5 + 0.5 * np.sign(np.sin(2 * np.pi * 38 * tt)))
        creak = bp(creak, 250, 2500) * np.hanning(len(tt)) * 0.18
        s0 = int(0.1 * SR); x[s0: s0 + len(creak)] += creak
    write(SFX, f"step_planks_{i+1:02d}.wav", lp(x, 6000), peak=0.6, fout=0.1)

# ---- rain (stereo loop) ------------------------------------------------------
dur = 46.0; n = int(dur * SR)
bed = np.stack([bp(rng.standard_normal(n), 500, 7000) for _ in range(2)], 1)
swell = 0.8 + 0.2 * np.sin(2 * np.pi * t(dur) / 11.0)[:, None]
bed *= swell * 0.25
drops = np.zeros((n, 2))
for _ in range(int(dur * 260)):
    p = rng.integers(0, n - 2000); L = rng.integers(200, 900)
    f = rng.uniform(1200, 6500); a = rng.uniform(0.05, 0.6) ** 2
    d = np.sin(2 * np.pi * f * np.arange(L) / SR) * decay(L, rng.uniform(0.002, 0.008))
    pan = rng.uniform(0, 1)
    drops[p: p + L, 0] += d * a * (1 - pan); drops[p: p + L, 1] += d * a * pan
# heavier drips off eaves now and then
for _ in range(int(dur * 3)):
    p = rng.integers(0, n - 6000); L = 4000
    d = lp(rng.standard_normal(L), 900) * decay(L, 0.03) * rng.uniform(0.3, 0.8)
    pan = rng.uniform(0, 1); drops[p: p + L, 0] += d * (1 - pan); drops[p: p + L, 1] += d * pan
rain = bed + drops
rain = loopify(normalize(rain.astype(np.float32), 0.6), SR, 2.0)
save(os.path.join(AMB, "amb_rain.wav"), rain, SR); print("amb_rain.wav", f"{len(rain)/SR:.1f}s")

# ---- wolves ---------------------------------------------------------------
def howl(seconds, f_lo, f_hi, seed):
    r = np.random.default_rng(seed); tt = t(seconds); n = len(tt)
    rise, fall = 0.18 * seconds, 0.22 * seconds
    f0 = np.full(n, float(f_hi))
    a = tt < rise; f0[a] = f_lo + (f_hi - f_lo) * np.sin(np.pi / 2 * tt[a] / rise)
    z = tt > seconds - fall; f0[z] = f_hi - (f_hi - f_lo * 0.8) * ((tt[z] - (seconds - fall)) / fall) ** 1.5
    f0 *= 1 + 0.012 * np.sin(2 * np.pi * r.uniform(4, 6) * tt) + 0.02 * np.sin(2 * np.pi * 0.4 * tt)
    ph = 2 * np.pi * np.cumsum(f0) / SR
    x = np.sin(ph) + 0.32 * np.sin(2 * ph) + 0.1 * np.sin(3 * ph) + 0.04 * np.sin(4 * ph)
    breath = bp(r.standard_normal(n), 600, 3000) * 0.05
    env = np.minimum(1, tt / 0.35) * np.minimum(1, (seconds - tt) / 0.6)
    env *= 0.85 + 0.15 * np.sin(2 * np.pi * 0.7 * tt)
    return (x + breath) * np.clip(env, 0, 1)

for i, (secs, lo, hi) in enumerate([(4.6, 330, 520), (5.4, 300, 470), (3.8, 380, 580)]):
    x = reverb(howl(secs, lo, hi, 40 + i), 2.0, 0.45, bright=2500)
    write(SFX, f"wolf_howl_{i+1:02d}.wav", lp(x, 5000), peak=0.8, fin=0.05, fout=0.8)

# a pack: several voices, staggered, different pitches
pack = np.zeros(int(9.5 * SR))
for k, (start, secs, lo, hi) in enumerate([(0.0, 5.0, 320, 500), (0.9, 5.6, 360, 560), (2.1, 4.8, 290, 450), (3.0, 5.4, 410, 610), (3.6, 4.0, 340, 520)]):
    v = howl(secs, lo, hi, 70 + k) * (0.9 - 0.12 * k)
    s0 = int(start * SR); pack[s0: s0 + len(v)] += v
write(SFX, "wolf_pack_howl.wav", lp(reverb(pack, 2.4, 0.5, bright=2200), 4500), peak=0.8, fin=0.1, fout=1.2)

def growl(seconds, seed, snap=False):
    r = np.random.default_rng(seed); tt = t(seconds); n = len(tt)
    rate = 26 + 6 * np.sin(2 * np.pi * 0.8 * tt)
    pulses = (0.5 + 0.5 * np.sin(2 * np.pi * np.cumsum(rate) / SR)) ** 3
    src = r.standard_normal(n) * pulses
    x = bp(src, 90, 260) * 1.0 + bp(src, 500, 1100) * 0.5 + bp(src, 1800, 2600) * 0.12
    env = np.minimum(1, tt / 0.25) * np.minimum(1, (seconds - tt) / 0.3) * (0.7 + 0.3 * tt / seconds)
    x = x * env
    if snap:  # the jaws closing
        s0 = int((seconds - 0.25) * SR); L = int(0.06 * SR)
        x[s0: s0 + L] += bp(r.standard_normal(L), 1500, 5000) * decay(L, 0.01) * 3
    return x
write(SFX, "wolf_growl_01.wav", growl(1.8, 5), peak=0.8, fout=0.2)
write(SFX, "wolf_growl_02.wav", growl(1.3, 6), peak=0.8, fout=0.2)
write(SFX, "wolf_bite.wav", growl(0.7, 7, snap=True), peak=0.85, fout=0.1)

# ---- a body falling on packed snow or earth --------------------------------
for i in range(2):
    n = int(0.9 * SR); x = np.zeros(n)
    x += np.sin(2 * np.pi * rng.uniform(55, 75) * t(0.9)) * decay(n, 0.09)
    x += lp(rng.standard_normal(n), 500) * decay(n, 0.06) * 1.2
    cloth = hp(rng.standard_normal(n), 2500) * decay(n, 0.12) * 0.25
    x += cloth
    s0 = int(rng.uniform(0.12, 0.2) * SR)  # the second contact: shoulders, then the rest
    x[s0:] += (lp(rng.standard_normal(n - s0), 400) * decay(n - s0, 0.05) * 0.8)
    write(SFX, f"body_fall_{i+1:02d}.wav", lp(x, 5000), peak=0.8, fout=0.3)

# ---- reloading a flintlock --------------------------------------------------
n = int(4.2 * SR); x = np.zeros(n)
def click(at, f, amp):
    L = int(0.04 * SR); s0 = int(at * SR)
    c = bp(rng.standard_normal(L), 1500, 7000) * decay(L, 0.004) + np.sin(2 * np.pi * f * np.arange(L) / SR) * decay(L, 0.01) * 0.6
    x[s0: s0 + L] += c * amp
def scrape(at, secs, amp):
    L = int(secs * SR); s0 = int(at * SR)
    s = bp(rng.standard_normal(L), 2200, 6500) * np.hanning(L)
    ring = np.sin(2 * np.pi * 3150 * np.arange(L) / SR) * np.hanning(L) * 0.15
    x[s0: s0 + L] += (s + ring) * amp
pour = bp(rng.standard_normal(int(0.5 * SR)), 3000, 9000) * np.hanning(int(0.5 * SR)) * 0.25
x[int(0.2 * SR): int(0.2 * SR) + len(pour)] += pour            # powder down the barrel
click(0.85, 2200, 0.6)                                          # ball dropped in
for k, at in enumerate([1.2, 1.75, 2.3]): scrape(at, 0.38, 0.8 - 0.1 * k)   # ramrod, three strokes
scrape(2.85, 0.25, 0.5)                                         # rod back in its pipes
click(3.35, 2600, 0.8)                                          # pan primed, frizzen shut
click(3.75, 1900, 1.0)                                          # hammer cocked
write(SFX, "musket_reload.wav", x, peak=0.7, fout=0.1)
