"""
Copies the chosen CC0 recordings (OpenGameArt packs in Tools/downloads/sfx) into
Assets/_Game/Resources/SFX/<id>[_n].wav, with light processing where a sound needs to feel distant,
muffled, or bigger. The game picks a random variant (<id>, <id>_1, <id>_2 ...) each time.

    python Tools/install_sfx_packs.py
"""
import os
import shutil

import numpy as np
import soundfile as sf
from scipy import signal

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "downloads", "sfx")
OUT = os.path.join(HERE, "..", "Assets", "_Game", "Resources", "SFX")
A = "100-CC0-SFX_0"
B = "sfx_100_v2"
C = "80-CC0-creature-SFX_0"
H = os.path.join("horror_hit_soundpack_1", "Horror Hit Soundpack 1", "Sound", "Pitches")
rng = np.random.default_rng(7)


def load(rel):
    x, sr = sf.read(os.path.join(SRC, rel), always_2d=True)
    return x.mean(axis=1), sr


def save(name, x, sr):
    x = x / (np.max(np.abs(x)) + 1e-9) * 0.9
    sf.write(os.path.join(OUT, name + ".wav"), x.astype(np.float32), sr, subtype="PCM_16")


def lp(x, sr, f, o=4): return signal.sosfilt(signal.butter(o, f, "lp", fs=sr, output="sos"), x)
def hp(x, sr, f, o=2): return signal.sosfilt(signal.butter(o, f, "hp", fs=sr, output="sos"), x)


def reverb(x, sr, secs=1.5, mix=0.4, tone=3000):
    n = int(secs * sr)
    t = np.arange(n) / sr
    ir = lp(rng.normal(0, 1, n) * np.exp(-t / (secs / 5.0)), sr, tone, 2)
    ir[0] = 0
    y = np.concatenate([x, np.zeros(n)])
    wet = signal.fftconvolve(y, ir)[: len(y)]
    wet /= np.max(np.abs(wet)) + 1e-9
    return y / (np.max(np.abs(y)) + 1e-9) * (1 - mix) + wet * mix


def distant(x, sr):
    return reverb(lp(x, sr, 1500), sr, 2.5, 0.55, 1500)


def thicken(x, sr):
    """A little body for small door hits: low shelf + short room."""
    low = lp(x, sr, 220, 2) * 1.6
    return reverb(x + low, sr, 0.35, 0.12, 2500)


def install(name, rels, fx=None):
    for i, rel in enumerate(rels):
        x, sr = load(rel)
        if fx is not None:
            x = fx(x, sr)
        save(name if i == 0 else f"{name}_{i}", x, sr)
    print(f"  {name:<18} {len(rels)} variant(s)")


def main():
    os.makedirs(OUT, exist_ok=True)
    for f in os.listdir(OUT):
        if f.endswith(".wav") or f.endswith(".wav.meta"):
            os.remove(os.path.join(OUT, f))
    j = os.path.join
    install("knock_single", [j(B, "sfx100v2_wood_hit_01.ogg"), j(B, "sfx100v2_wood_hit_02.ogg"), j(A, "wooden_01.ogg"), j(A, "wooden_03.ogg")], thicken)
    install("knock_soft", [j(A, "wooden_02.ogg"), j(B, "sfx100v2_wood_hit_03.ogg")], thicken)
    install("knock_heavy", [j(A, "slam_03.ogg"), j(A, "door_close_03.ogg"), j(A, "door_close_04.ogg")], thicken)
    install("door_creak", [j(A, "door_01.ogg"), j(A, "door_02.ogg")])
    install("door_slam", [j(A, "door_close_04.ogg"), j(A, "slam_02.ogg")], thicken)
    install("doorknob_rattle", [j(B, "sfx100v2_lock_open_01.ogg"), j(A, "key_open_01.ogg")])
    install("deadbolt", [j(A, "key_open_02.ogg")])
    install("window_latch", [j(A, "switch_02.ogg")])
    install("window_tap", [j(A, "glass_02.ogg"), j(A, "glass_01.ogg"), j(B, "sfx100v2_glass_01.ogg")])
    install("glass_break", [j(A, "glass_05.ogg"), j(A, "glass_04.ogg")])
    install("footstep", [j(B, f"sfx100v2_footstep_wood_0{i}.ogg") for i in range(1, 5)])
    install("footstep_wet", [j(B, f"sfx100v2_footstep_wet_0{i}.ogg") for i in range(1, 4)])
    install("thunder", [j(B, "sfx100v2_thunder_01.ogg")])
    install("breath_close", [j(C, "breath.ogg")])
    install("creature_scream", [j(C, "scream_02.ogg"), j(C, "scream_01.ogg")], lambda x, sr: reverb(x, sr, 1.2, 0.25, 6000))
    install("creature_click", [j(C, f"bug_0{i}.ogg") for i in range(1, 5)], lambda x, sr: reverb(lp(x, sr, 5000), sr, 1.2, 0.35))
    install("creature_growl", [j(C, "monster_07.ogg"), j(C, "monster_04.ogg"), j(C, "monster_06.ogg")], lambda x, sr: reverb(x, sr, 1.5, 0.35, 2500))
    install("jumpscare", [j(H, "Very Bassy", f"Very Bassy {i}.wav") for i in range(1, 5)])
    install("sting_reveal", [j(H, "Mid", f"Mid {i}.wav") for i in (2, 5, 9, 13, 17)])
    install("sting_high", [j(H, "High", f"High {i}.wav") for i in (3, 8, 12, 16)])
    install("hit_bass", [j(H, "Bassy", f"Bassy {i}.wav") for i in (2, 5, 9)])
    install("dog_distant", [j(C, "barking_01.ogg"), j(C, "barking_02.ogg")], distant)
    install("howl_distant", [j(C, "howl.ogg")], distant)
    install("toilet_flush", [j(A, "toilet_01.ogg")], lambda x, sr: reverb(x, sr, 0.8, 0.2, 4000))
    install("dishes", [j(A, "dishes_01.ogg"), j(A, "dishes_03.ogg")])
    install("paper", [j(A, f"paper_0{i}.ogg") for i in range(1, 5)])
    install("flashlight", [j(A, "switch_01.ogg")])
    install("cough_outside", [j(C, "cough_01.ogg"), j(C, "cough_03.ogg")], lambda x, sr: reverb(lp(x, sr, 1400), sr, 0.9, 0.3, 1500))
    install("snore", [j(C, "snore.ogg")])
    install("tv_glitch", [j(A, "weird_02.ogg"), j(A, "weird_04.ogg"), j(A, "noise_02.ogg")])
    print("Done ->", os.path.abspath(OUT))


if __name__ == "__main__":
    main()
