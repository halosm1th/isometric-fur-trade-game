import numpy as np
from scipy.io import wavfile
from scipy.signal import resample_poly
from math import gcd

def load(path):
    sr, d = wavfile.read(path)
    x = np.asarray(d)
    if x.dtype == np.int16: x = x.astype(np.float32) / 32768
    elif x.dtype == np.int32: x = x.astype(np.float32) / 2**31
    elif x.dtype == np.uint8: x = (x.astype(np.float32) - 128) / 128
    else: x = x.astype(np.float32)
    if x.ndim == 1: x = x[:, None]
    return sr, x

def resample(x, sr, to):
    if sr == to: return x
    g = gcd(sr, to)
    return resample_poly(x, to // g, sr // g, axis=0).astype(np.float32)

def mono(x): return x.mean(axis=1, keepdims=True)

def env(x, sr, hop_s=0.01):
    m = np.abs(mono(x)[:, 0]); hop = max(1, int(sr * hop_s)); n = len(m) // hop
    return np.sqrt((m[: n * hop].reshape(n, hop) ** 2).mean(1))

def onsets(x, sr, thresh_ratio=4.0, min_gap=0.25, floor=None):
    e = env(x, sr); bg = np.median(e) + 1e-6
    t = floor if floor else bg * thresh_ratio
    out = []; last = -1e9
    for i, v in enumerate(e):
        if v > t and (i * 0.01 - last) >= min_gap and (i == 0 or e[i - 1] <= t):
            out.append(i * 0.01); last = i * 0.01
    return out, e

def fade(x, sr, fin=0.005, fout=0.05):
    x = x.copy(); n_in = int(sr * fin); n_out = int(sr * fout)
    if n_in: x[:n_in] *= np.linspace(0, 1, n_in)[:, None]
    if n_out: x[-n_out:] *= np.linspace(1, 0, n_out)[:, None]
    return x

def normalize(x, peak=0.9):
    p = np.abs(x).max()
    return x * (peak / p) if p > 0 else x

def loopify(x, sr, xfade=2.0):
    """Crossfade the tail into the head so the clip loops without a seam."""
    n = int(sr * xfade)
    head, body, tail = x[:n], x[n:-n] if n else x, x[-n:]
    ramp = np.linspace(0, 1, n)[:, None]
    # equal-power
    mixed = tail * np.cos(ramp * np.pi / 2) + head * np.sin(ramp * np.pi / 2)
    return np.concatenate([body, mixed])  # ends on head content, starts after head: seamless

def save(path, x, sr):
    y = np.clip(x, -1, 1)
    wavfile.write(path, sr, (y * 32767).astype(np.int16) if y.shape[1] > 1 else (y[:, 0] * 32767).astype(np.int16))
