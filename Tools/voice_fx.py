"""
Sound design shared by the voice generators (voices_kokoro.py, voices_chatterbox.py): how each line is heard
in the game - through the front door, over the intercom, on the phone, on TV, as a memory, whispered, as a
mimic - plus the text clean-up both models need.
"""
import re

import numpy as np
from scipy import signal

SR = 24000
rng = np.random.default_rng(13)


# ------------------------------------------------------------------ dsp helpers
def sos(kind, f, order=4):
    return signal.butter(order, f, kind, fs=SR, output="sos")


def lp(x, f, o=4): return signal.sosfilt(sos("lp", f, o), x)
def hp(x, f, o=4): return signal.sosfilt(sos("hp", f, o), x)
def bp(x, lo, hi, o=4): return signal.sosfilt(sos("bp", [lo, hi], o), x)


def repitch(x, factor):
    if abs(factor - 1.0) < 1e-3:
        return x
    n = max(1, int(len(x) / factor))
    return signal.resample(x, n)


def pad(x, before=0.05, after=0.35):
    return np.concatenate([np.zeros(int(before * SR)), x, np.zeros(int(after * SR))])


def silence(secs):
    return np.zeros(int(secs * SR))


def trim(x, threshold_db=-42.0, keep=0.04):
    """Cuts leading/trailing silence (models often breathe or hiss at the ends), keeping a short tail."""
    if len(x) == 0:
        return x
    env = np.abs(x) / (np.max(np.abs(x)) + 1e-9)
    loud = np.where(env > 10 ** (threshold_db / 20.0))[0]
    if len(loud) == 0:
        return x
    k = int(keep * SR)
    return x[max(0, loud[0] - k): min(len(x), loud[-1] + k)]


def reverb(x, secs=1.2, mix=0.25, tone=4000):
    n = int(secs * SR)
    t = np.arange(n) / SR
    ir = rng.normal(0, 1, n) * np.exp(-t / (secs / 5.0))
    ir = lp(ir, tone, 2)
    ir[0] = 0
    wet = signal.fftconvolve(x, ir)[: len(x)]
    wet /= np.max(np.abs(wet)) + 1e-9
    dry = x / (np.max(np.abs(x)) + 1e-9)
    return dry * (1 - mix) + wet * mix


def pre_echo(x, secs=1.0, mix=0.35):
    """Reverse reverb: the sound swells in *before* the words. Classic 'something is wrong' trick."""
    r = reverb(x[::-1], secs, 1.0, 5000)[::-1]
    return x * (1 - mix) + r * mix


def vibrato(x, rate=5.5, depth=0.0035):
    n = len(x)
    t = np.arange(n) / SR
    delay = depth * SR * (1 + np.sin(2 * np.pi * rate * t)) * 0.5
    idx = np.clip(np.arange(n) - delay, 0, n - 1)
    return np.interp(idx, np.arange(n), x)


def whisperize(x, amount=1.0):
    """Keep the spectral envelope of the voice, replace the pitch with breath: a whisper."""
    f, tt, Z = signal.stft(x, fs=SR, nperseg=512)
    mag = np.abs(Z)
    phase = np.exp(1j * rng.uniform(0, 2 * np.pi, Z.shape))
    _, w = signal.istft(mag * phase, fs=SR, nperseg=512)
    w = np.pad(w, (0, max(0, len(x) - len(w))))[: len(x)]
    w = hp(w, 900, 2)
    w /= np.max(np.abs(w)) + 1e-9
    x2 = x / (np.max(np.abs(x)) + 1e-9)
    return x2 * (1 - amount) + w * amount


def noise_bed(n, lo, hi, level):
    return bp(rng.uniform(-1, 1, n), lo, hi, 2) * level


def crackle(n, density=0.0015, level=0.25):
    c = (rng.uniform(0, 1, n) < density) * rng.uniform(-1, 1, n)
    return hp(c, 2000, 2) * level


def norm(x, peak=0.89):
    return x / (np.max(np.abs(x)) + 1e-9) * peak


# ------------------------------------------------------------------ treatments
def through_door(x):
    x = lp(x, 1100, 4)
    x = reverb(x, 0.6, 0.22, 1500)
    return norm(x, 0.8)


def intercom(x):
    x = bp(x, 320, 3300, 4)
    x = np.tanh(x / (np.max(np.abs(x)) + 1e-9) * 2.2)
    x = x + noise_bed(len(x), 600, 4000, 0.025) + crackle(len(x), 0.0008, 0.2)
    return norm(x, 0.85)


def phone_line(x):
    x = bp(x, 380, 3000, 4)
    x = np.tanh(x / (np.max(np.abs(x)) + 1e-9) * 1.6)
    x = x + noise_bed(len(x), 300, 3000, 0.02)
    return norm(x, 0.82)


def tv_speaker(x):
    x = bp(x, 200, 5500, 3)
    x = reverb(x, 0.5, 0.18, 3000)
    x = x + noise_bed(len(x), 1000, 6000, 0.015) + crackle(len(x), 0.0006, 0.15)
    return norm(x, 0.85)


def memory(x):
    x = lp(x, 6000, 2)
    x = vibrato(x, 0.6, 0.004)
    x = reverb(x, 1.8, 0.35, 5000)
    return norm(x, 0.85)


def walkie(x):
    x = bp(x, 500, 2800, 4)
    x = np.tanh(x / (np.max(np.abs(x)) + 1e-9) * 3.0)
    x = x + noise_bed(len(x), 800, 5000, 0.05)
    sq = noise_bed(int(0.18 * SR), 800, 6000, 0.6)
    return norm(np.concatenate([sq, x, sq]), 0.85)


def voicemail(x):
    x = phone_line(x)
    rain = noise_bed(len(x), 300, 6000, 0.06)
    return norm(x + rain, 0.82)


def room(x):
    return norm(reverb(x, 0.7, 0.15, 5000), 0.85)


def outdoors(x):
    return norm(reverb(x, 1.6, 0.18, 4000), 0.85)


def distant(x):
    x = lp(x, 1800, 2)
    x = reverb(x, 2.5, 0.55, 2000)
    return norm(x, 0.7)


def whispered(x, wet=0.85):
    x = whisperize(x, wet)
    x = reverb(x, 1.0, 0.25, 7000)
    return norm(x, 0.8)


def death(x):
    w = whisperize(x, 1.0)
    x = norm(x * 0.5 + w * 0.7, 0.88)
    return reverb(x, 0.6, 0.12, 6000)


def mimic_touch(x):
    """Subtle wrongness baked into every mimic line (the game adds an octave-down under-voice)."""
    x = pre_echo(x, 0.9, 0.3)
    x = vibrato(x, 0.35, 0.0025)
    return x


def unison(x, factors=(1.0, 1.06, 0.95), offsets=(0.0, 0.012, 0.021)):
    outs = []
    for f, o in zip(factors, offsets):
        y = repitch(x, f)
        y = np.concatenate([np.zeros(int(o * SR)), y])
        outs.append(y)
    n = max(len(y) for y in outs)
    mix = np.zeros(n)
    for y in outs:
        mix[: len(y)] += y
    return mix


TREATMENTS = {
    "door": through_door, "intercom": intercom, "phone": phone_line, "tv": tv_speaker, "memory": memory,
    "door_memory": lambda x: memory(through_door(x)), "walkie": walkie, "voicemail": voicemail, "room": room,
    "outdoors": outdoors, "distant": distant, "whisper": lambda x: whispered(x, 0.9), "death": death,
}


def apply_treatment(x, kind):
    return TREATMENTS.get(kind, intercom)(x)


# ------------------------------------------------------------------ text prep
def speakable(text):
    t = text.replace("--", "...")
    # ALL-CAPS words are shouted, not spelled out.
    t = re.sub(r"\b([A-Z]{2,})\b", lambda m: m.group(1).capitalize() if m.group(1) not in ("TV", "PD") else m.group(1), t)
    t = t.replace("Banana-Ana", "Banana Ana").replace("Two-B", "Two B")
    return t


def treatment(line_id, variant):
    """How the player hears a line: decided by its id."""
    i = line_id
    if i.startswith("REPL_") or i.startswith("SLOT_") or i.endswith("_PEEP") or i in ("HOME_03", "MASKS_A3"):
        return "whisper"
    if i.endswith("_D"):
        return "death"
    if i.startswith("MEM_RADIO"):
        return "walkie"
    if i.startswith("MEM_VOICEMAIL"):
        return "voicemail"
    if i.startswith("MEM_ASK") or i.startswith("MEM_OKAY"):
        return "door_memory"
    if i.startswith("MEM_"):
        return "memory"
    if i.startswith("HOME_") or i.startswith("MOM_"):
        return "phone"
    if i.startswith("NEWS_"):
        return "tv"
    if i.startswith("VOSS_P"):
        return "outdoors"
    if i.startswith("VOSS_E"):
        return "door"
    if re.search(r"_TK$", i):
        return "distant"
    if "_IN" in i or i in ("LEOR_E1",):
        return "room"
    if re.search(r"_K\d$|_L$|_W\d$|_WIN$", i):
        return "door"
    return "intercom"
