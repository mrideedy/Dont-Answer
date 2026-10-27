"""
Builds the death-flash stinger: the one short, sharp sound you hear in the split second you see its face.

    python Tools/make_death_flash.py

Writes Assets/_Game/Resources/SFX/death_flash.wav, death_flash_1.wav and death_flash_2.wav (48 kHz mono).
Each one is under a second: an instant noise slam and a low thump, a shrieking cluster of detuned saws a
semitone apart (the "violin stab"), and the loudest slice of one of the Horror Hit jump-scare clips for grit.
No long tail - it's over almost as soon as it starts.
"""
import os

import numpy as np
import soundfile as sf

SR = 48000
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SFX = os.path.join(ROOT, "Assets", "_Game", "Resources", "SFX")


def load_slice(name, start, n):
    x, sr = sf.read(os.path.join(SFX, name))
    if x.ndim > 1:
        x = x.mean(axis=1)
    if sr != SR:
        x = np.interp(np.arange(0, len(x), sr / SR), np.arange(len(x)), x)
    s = int(start * SR)
    seg = x[s:s + n]
    return np.pad(seg, (0, n - len(seg)))


def stinger(seed, base, cluster, source, source_at, length=0.75):
    rng = np.random.default_rng(seed)
    n = int(SR * length)
    t = np.arange(n) / SR
    attack = lambda a: np.minimum(1.0, t / a)
    out = np.zeros(n)

    # The slam: bright noise, gone in a few hundredths of a second.
    noise = rng.standard_normal(n)
    bright = np.concatenate([[0.0], np.diff(noise)])
    out += 0.75 * bright * attack(0.0008) * np.exp(-t / 0.03)

    # The thump under it: a sine falling from 120 Hz to 40.
    f = 40 + 80 * np.exp(-t / 0.05)
    out += 0.95 * np.sin(2 * np.pi * np.cumsum(f) / SR) * attack(0.002) * np.exp(-t / 0.16)

    # The shriek: detuned saws a semitone apart, wobbling and climbing.
    shriek = np.zeros(n)
    for r in cluster:
        f0 = base * r * (1 + rng.uniform(-0.007, 0.007))
        vib = 1 + 0.013 * np.sin(2 * np.pi * rng.uniform(7, 12) * t + rng.uniform(0, 6.3))
        climb = 1 + 0.07 * (1 - np.exp(-t / 0.22))
        phase = np.cumsum(f0 * vib * climb) / SR
        shriek += (2 * (phase % 1.0) - 1) * rng.uniform(0.6, 1.0)
    shriek /= len(cluster)
    shriek += 0.12 * noise * np.exp(-t / 0.25)
    shape = attack(0.003) * (0.6 * np.exp(-t / 0.08) + 0.4 * np.exp(-t / 0.28))
    out += 0.8 * np.tanh(2.4 * shriek) * shape

    # Grit: the loudest moment of a real jump-scare hit.
    seg = load_slice(source, source_at, n)
    seg *= attack(0.002) * np.exp(-t / 0.2)
    out += 0.55 * seg / (np.abs(seg).max() + 1e-9)

    out = np.tanh(1.7 * out)
    tail = int(0.3 * SR)
    out[-tail:] *= np.linspace(1.0, 0.0, tail) ** 2
    return out * (0.8 / np.abs(out).max())


def main():
    variants = [
        ("death_flash.wav", stinger(11, 1180.0, [1.0, 1.059, 1.122, 1.414, 1.498, 2.06], "jumpscare.wav", 0.30)),
        ("death_flash_1.wav", stinger(23, 1320.0, [1.0, 1.059, 1.189, 1.335, 1.414, 1.888], "jumpscare_2.wav", 0.42)),
        ("death_flash_2.wav", stinger(37, 990.0, [1.0, 1.06, 1.12, 1.5, 1.59, 2.12], "jumpscare_3.wav", 0.33)),
    ]
    for name, data in variants:
        path = os.path.join(SFX, name)
        sf.write(path, data.astype(np.float32), SR, subtype="PCM_16")
        print("wrote", os.path.relpath(path, ROOT), f"{len(data) / SR:.2f}s")


if __name__ == "__main__":
    main()
