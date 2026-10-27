"""
Procedural sound generator for "Don't Answer".

Generates every placeholder sound effect / loop the game uses into Assets/_Game/Audio.
Re-run any time:  python Tools/generate_audio.py
Swap any file for a real recording later - keep the same file name and Unity picks it up.
"""
import os
import wave

import numpy as np
from scipy import signal

SR = 44100
OUT = os.path.join(os.path.dirname(__file__), "..", "Assets", "_Game", "Audio")
rng = np.random.default_rng(1031)


# ---------------------------------------------------------------- helpers
def t_axis(seconds):
    return np.arange(int(seconds * SR)) / SR


def noise(seconds):
    return rng.uniform(-1.0, 1.0, int(seconds * SR))


def brown(seconds):
    w = rng.normal(0, 1, int(seconds * SR))
    b = np.cumsum(w)
    b = signal.sosfilt(signal.butter(1, 20, "hp", fs=SR, output="sos"), b)
    return b / (np.max(np.abs(b)) + 1e-9)


def lp(x, hz, order=2):
    return signal.sosfilt(signal.butter(order, hz, "lp", fs=SR, output="sos"), x)


def hp(x, hz, order=2):
    return signal.sosfilt(signal.butter(order, hz, "hp", fs=SR, output="sos"), x)


def bp(x, lo, hi, order=2):
    return signal.sosfilt(signal.butter(order, [lo, hi], "bp", fs=SR, output="sos"), x)


def mode(freq, decay, seconds, phase=0.0):
    """Exponentially decaying sine - one resonant mode of a struck object."""
    t = t_axis(seconds)
    return np.sin(2 * np.pi * freq * t + phase) * np.exp(-t / decay)


def resonate(x, freq, q):
    b, a = signal.iirpeak(freq, q, fs=SR)
    return signal.lfilter(b, a, x)


def env_ar(n, attack, release):
    e = np.ones(n)
    a = max(1, int(attack * SR))
    r = max(1, int(release * SR))
    e[:a] = np.linspace(0, 1, a)
    e[-r:] *= np.linspace(1, 0, r)
    return e


def place(dst, src, at_seconds, gain=1.0):
    i = int(at_seconds * SR)
    if i >= len(dst):
        return dst
    n = min(len(src), len(dst) - i)
    dst[i:i + n] += src[:n] * gain
    return dst


def reverb(x, seconds=1.6, mix=0.25, tone=4000):
    n = int(seconds * SR)
    t = np.arange(n) / SR
    ir = rng.normal(0, 1, n) * np.exp(-t / (seconds / 5.0))
    ir = lp(ir, tone)
    ir[0] = 0
    wet = signal.fftconvolve(x, ir)[: len(x)]
    wet /= np.max(np.abs(wet)) + 1e-9
    dry = x / (np.max(np.abs(x)) + 1e-9)
    return dry * (1 - mix) + wet * mix


def pad(x, seconds):
    return np.concatenate([x, np.zeros(int(seconds * SR))])


def normalize(x, peak_db=-1.0):
    peak = np.max(np.abs(x)) + 1e-9
    return x / peak * (10 ** (peak_db / 20.0))


def fade(x, fin=0.002, fout=0.01):
    x = x.copy()
    a, b = int(fin * SR), int(fout * SR)
    if a > 0:
        x[:a] *= np.linspace(0, 1, a)
    if b > 0:
        x[-b:] *= np.linspace(1, 0, b)
    return x


def make_loop(x, xfade=0.5):
    """Crossfade the tail into the head so the clip loops without a click."""
    n = int(xfade * SR)
    body = x[:-n].copy()
    tail = x[-n:]
    ramp = np.linspace(0, 1, n)
    body[:n] = body[:n] * ramp + tail * (1 - ramp)
    return body


def save(name, x, peak_db=-1.0, do_fade=True):
    x = np.nan_to_num(x)
    if do_fade:
        x = fade(x)
    x = normalize(x, peak_db)
    data = np.clip(x * 32767, -32768, 32767).astype(np.int16)
    path = os.path.join(OUT, name + ".wav")
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(data.tobytes())
    print(f"  {name}.wav  {len(x) / SR:5.2f}s")


def loop_freq(f, length):
    """Snap a frequency so it completes a whole number of cycles in `length` seconds."""
    return max(1, round(f * length)) / length


# ---------------------------------------------------------------- sounds
def knock():
    d = 0.35
    x = (mode(92, 0.09, d) * 1.0 + mode(176, 0.06, d, 0.4) * 0.7 +
         mode(318, 0.04, d, 1.1) * 0.45 + mode(540, 0.025, d, 2.0) * 0.3)
    click = lp(noise(0.02), 2500) * np.exp(-t_axis(0.02) / 0.004)
    x = place(x, click, 0, 0.8)
    return reverb(x, 0.5, 0.12, 2500)


def knock_heavy():
    d = 0.7
    x = (mode(64, 0.18, d) * 1.0 + mode(118, 0.12, d, 0.3) * 0.8 +
         mode(230, 0.07, d, 0.9) * 0.5 + mode(410, 0.04, d, 1.7) * 0.3)
    thud = lp(noise(0.05), 900) * np.exp(-t_axis(0.05) / 0.012)
    x = place(x, thud, 0, 1.2)
    rattle = hp(noise(0.35), 2500) * np.exp(-t_axis(0.35) / 0.08) * (rng.uniform(0, 1, int(0.35 * SR)) > 0.97)
    x = place(x, lp(rattle, 7000), 0.01, 0.6)
    x = np.tanh(x * 2.2)
    return reverb(x, 0.8, 0.15, 2200)


def door_creak():
    d = 1.8
    t = t_axis(d)
    rate = 32 + 22 * np.sin(2 * np.pi * 0.45 * t) + 8 * rng.normal(0, 1, len(t)).cumsum() / 4000
    phase = np.cumsum(rate / SR)
    pulses = np.diff(np.floor(phase), prepend=0) * (0.6 + 0.4 * rng.uniform(0, 1, len(t)))
    x = sum(resonate(pulses, f, q) * g for f, q, g in
            [(620, 18, 1.0), (1150, 22, 0.8), (1720, 25, 0.6), (2650, 30, 0.35)])
    x *= np.sin(np.pi * np.clip(t / d, 0, 1)) ** 0.7
    return reverb(x, 0.6, 0.15, 3500)


def door_slam():
    d = 1.4
    x = mode(55, 0.25, d) + mode(98, 0.16, d, 0.5) * 0.8 + mode(170, 0.1, d, 1.3) * 0.5
    x = place(x, lp(noise(0.08), 1500) * np.exp(-t_axis(0.08) / 0.02), 0, 1.5)
    latch = hp(noise(0.06), 3000) * np.exp(-t_axis(0.06) / 0.01)
    x = place(x, latch, 0.03, 0.5)
    return reverb(np.tanh(x * 1.8), 1.2, 0.2, 2000)


def door_rattle():
    d = 1.1
    x = np.zeros(int(d * SR))
    tcur = 0.0
    while tcur < d - 0.1:
        c = mode(rng.uniform(2200, 3400), 0.012, 0.06) + mode(rng.uniform(4200, 5600), 0.008, 0.06) * 0.5
        c += hp(noise(0.06), 3000) * np.exp(-t_axis(0.06) / 0.006) * 0.6
        x = place(x, c, tcur, rng.uniform(0.5, 1.0))
        tcur += rng.uniform(0.05, 0.13)
    x = place(x, mode(140, 0.05, 0.2) * 0.6, 0.0)
    return reverb(x, 0.4, 0.1)


def phone_buzz():
    d = 1.15
    t = t_axis(d)
    tone = np.tanh(np.sin(2 * np.pi * 172 * t) * 3) * (0.8 + 0.2 * np.sin(2 * np.pi * 31 * t))
    gate = ((t > 0.0) & (t < 0.42)) | ((t > 0.62) & (t < 1.05))
    gate = lp(gate.astype(float), 60)
    x = tone * gate
    x += hp(noise(d), 1500) * gate * 0.08   # table rattle
    return x


def phone_ping():
    d = 0.9
    x = mode(1318.5, 0.18, d) * 0.7 + mode(2637, 0.08, d) * 0.15
    x = place(x, mode(1760, 0.25, d) * 0.7 + mode(3520, 0.1, d) * 0.15, 0.11)
    return reverb(x, 0.6, 0.2)


def voice(f1, f2, f3, f0=128, d=0.11):
    t = t_axis(d)
    vib = f0 * (1 + 0.03 * np.sin(2 * np.pi * 6 * t))
    phase = np.cumsum(vib / SR)
    pulses = np.diff(np.floor(phase), prepend=0)
    src = lp(pulses, 3500, 1)
    x = resonate(src, f1, 6) * 1.0 + resonate(src, f2, 9) * 0.6 + resonate(src, f3, 12) * 0.3
    x += bp(noise(d), 1500, 5000) * 0.05   # breath
    return x * env_ar(len(t), 0.012, 0.045)


def scream():
    d = 1.9
    t = t_axis(d)
    x = np.zeros(len(t))
    for i, det in enumerate([1.0, 1.013, 0.986, 1.5, 2.02]):
        f = (560 + 380 * np.clip(t / 0.25, 0, 1) - 260 * np.clip((t - 1.1) / 0.8, 0, 1)) * det
        f *= 1 + 0.04 * np.sin(2 * np.pi * (11 + i) * t)
        ph = np.cumsum(f / SR)
        x += (2 * (ph % 1) - 1) * (0.9 if det < 1.2 else 0.45)
    x += bp(noise(d), 1200, 7000) * 1.4
    x = np.tanh(x * 3.0)
    x *= np.minimum(1, t / 0.015) * np.clip(1 - (t - 1.2) / 0.7, 0, 1)
    boom = mode(46, 0.35, d) * 2.0 + lp(noise(d), 200) * np.exp(-t / 0.2) * 1.5
    x = x * 0.75 + boom
    crush = np.round(x * 24) / 24
    return reverb(crush, 1.2, 0.18, 6000)


def heartbeat():
    period = 60.0 / 74
    x = np.zeros(int(period * SR))
    lub = mode(52, 0.07, 0.3) + mode(85, 0.04, 0.3) * 0.5
    dub = mode(64, 0.05, 0.25) * 0.7 + mode(102, 0.03, 0.25) * 0.4
    x = place(x, lp(lub, 300), 0.0)
    x = place(x, lp(dub, 300), 0.26)
    return x


def static_loop():
    d = 3.5
    x = bp(noise(d + 0.5), 500, 4500) * 0.55
    crackles = (rng.uniform(0, 1, len(x)) > 0.9985) * rng.uniform(-1, 1, len(x))
    x += hp(crackles, 2000) * 3
    x *= 0.8 + 0.2 * np.sin(2 * np.pi * 0.7 * t_axis(d + 0.5))
    return make_loop(x, 0.5)


def intercom_click():
    d = 0.35
    x = np.zeros(int(d * SR))
    x = place(x, mode(2900, 0.004, 0.05) + mode(1200, 0.01, 0.05) * 0.6, 0.0)
    burst = bp(noise(0.25), 600, 4000) * env_ar(int(0.25 * SR), 0.005, 0.15) * 0.35
    return place(x, burst, 0.03)


def clock_tick():
    d = 2.0
    x = np.zeros(int(d * SR))
    tick = place(mode(2600, 0.006, 0.05) + mode(5200, 0.003, 0.05) * 0.4, hp(noise(0.01), 3000), 0, 0.3)
    tock = place(mode(1900, 0.008, 0.05) + mode(3700, 0.004, 0.05) * 0.4, hp(noise(0.01), 2500), 0, 0.3)
    x = place(x, tick, 0.0)
    x = place(x, tock * 0.85, 1.0)
    return reverb(x, 0.4, 0.15)


def chime():
    d = 6.0
    f0 = 196.0
    parts = [(0.5, 3.5, 0.6), (1.0, 2.6, 1.0), (1.19, 1.8, 0.55), (1.56, 1.4, 0.4),
             (2.0, 1.2, 0.45), (2.51, 0.8, 0.25), (2.66, 0.7, 0.2), (3.01, 0.5, 0.15)]
    x = sum(mode(f0 * r, dec, d, rng.uniform(0, 6)) * g for r, dec, g in parts)
    x *= 1 + 0.15 * np.sin(2 * np.pi * 1.6 * t_axis(d))   # beating
    x = place(x, hp(noise(0.02), 1500) * np.exp(-t_axis(0.02) / 0.004), 0, 0.4)
    return reverb(x, 3.0, 0.3, 3000)


def footstep(variant):
    d = 0.32
    f = [1.0, 1.08, 0.93][variant]
    x = mode(88 * f, 0.05, d) + mode(150 * f, 0.03, d, 0.5) * 0.6 + mode(260 * f, 0.02, d, 1.0) * 0.3
    x = place(x, lp(noise(0.04), 1200) * np.exp(-t_axis(0.04) / 0.008), 0, 0.5)
    if variant == 2:   # creaky board
        x = place(x, door_creak()[: int(0.25 * SR)] * 0.25, 0.04)
    return reverb(x, 0.35, 0.08, 2000)


def light_buzz():
    d = 0.7
    t = t_axis(d)
    buzz = np.sign(np.sin(2 * np.pi * 120 * t)) * 0.25
    gate = (rng.uniform(0, 1, len(t)) > 0.9993).astype(float)
    gate = lp(np.convolve(gate, np.ones(900), "same"), 200)
    sparks = hp(noise(d), 2500) * gate * 2.0
    return (buzz * (0.3 + gate) + sparks) * np.exp(-t / 0.4)


def window_tap():
    d = 0.5
    x = mode(1250, 0.03, d) + mode(2930, 0.02, d, 0.5) * 0.6 + mode(4310, 0.012, d, 1.0) * 0.35
    x = place(x, hp(noise(0.01), 2000) * np.exp(-t_axis(0.01) / 0.002), 0, 0.6)
    return reverb(x, 0.5, 0.15)


def sting():
    d = 3.2
    t = t_axis(d)
    x = np.zeros(len(t))
    for f in [110, 116.5, 164.8, 174.6, 233.1, 246.9]:
        ph = np.cumsum(f * (1 + 0.004 * np.sin(2 * np.pi * rng.uniform(4, 6) * t)) / SR)
        x += 2 * (ph % 1) - 1
    x = lp(x, 1400)
    hi = np.sin(2 * np.pi * np.cumsum((1900 + 300 * t) / SR)) * 0.15
    x = x * 0.25 + hi
    swell = np.clip(t / 1.8, 0, 1) ** 2.2
    x *= swell * np.clip(1 - (t - 2.2) / 1.0, 0, 1)
    x = place(x, mode(41, 0.6, 1.5) * 1.2, 1.9)
    return reverb(x, 2.0, 0.3)


def breath_loop():
    d = 4.5
    t = t_axis(d + 0.6)
    x = bp(noise(d + 0.6), 350, 2400)
    env = np.clip(np.sin(2 * np.pi * t / 2.25), 0, 1) ** 1.5 * 0.9 + np.clip(-np.sin(2 * np.pi * t / 2.25), 0, 1) ** 2 * 0.5
    x = resonate(x, 900, 3) + x * 0.3
    return make_loop(x * env, 0.6)


def thunder():
    d = 6.0
    t = t_axis(d)
    x = brown(d) * np.exp(-t / 1.8) * np.minimum(1, t / 0.4)
    crack = lp(noise(d), 900) * np.exp(-t / 0.3) * (t > 0.05)
    x = lp(x, 160) * 2 + crack * 0.4
    x *= 1 + 0.5 * np.sin(2 * np.pi * 0.9 * t) * np.exp(-t / 2)
    return reverb(x, 2.5, 0.25, 800)


def rain_loop():
    d = 12.0
    x = lp(hp(noise(d + 1), 350), 5500) * 0.35
    x += lp(brown(d + 1), 300) * 0.25
    drops = (rng.uniform(0, 1, len(x)) > 0.9992).astype(float) * rng.uniform(0.3, 1, len(x))
    for f in (2400, 3600, 5100):
        x += resonate(drops, f, 12) * 0.25
    x *= 0.9 + 0.1 * np.sin(2 * np.pi * 0.11 * t_axis(d + 1))
    return make_loop(x, 1.0)


def drone_loop():
    L = 24.0
    t = t_axis(L)
    x = np.zeros(len(t))
    for f, g in [(41.2, 1.0), (61.7, 0.6), (82.4, 0.5), (87.3, 0.25), (123.5, 0.15)]:
        fl = loop_freq(f, L)
        lfo = 0.6 + 0.4 * np.sin(2 * np.pi * loop_freq(rng.uniform(0.04, 0.12), L) * t + rng.uniform(0, 6))
        x += np.sin(2 * np.pi * fl * t) * g * lfo
    wind = bp(np.tile(noise(L / 2), 2), 200, 900)
    wind_env = 0.5 + 0.5 * np.sin(2 * np.pi * (2 / L) * t)
    x += wind * wind_env * 0.25
    return x


def hum_loop():
    L = 4.0
    t = t_axis(L)
    x = (np.sin(2 * np.pi * 60 * t) + 0.5 * np.sin(2 * np.pi * 120 * t) +
         0.2 * np.sin(2 * np.pi * 180 * t) + 0.08 * np.sin(2 * np.pi * 240 * t))
    x += lp(np.tile(noise(L / 2), 2), 900) * 0.05
    return x


def birds():
    d = 7.0
    x = np.zeros(int(d * SR))
    tc = 0.3
    while tc < d - 0.6:
        n = rng.integers(2, 5)
        base = rng.uniform(2600, 4200)
        for k in range(n):
            cd = rng.uniform(0.05, 0.09)
            tt = t_axis(cd)
            f = base + rng.uniform(800, 1600) * (tt / cd) * rng.choice([1, -1])
            chirp = np.sin(2 * np.pi * np.cumsum(f / SR)) * np.sin(np.pi * tt / cd)
            x = place(x, chirp, tc + k * rng.uniform(0.09, 0.14), rng.uniform(0.4, 1.0))
        tc += rng.uniform(0.6, 1.5)
    return reverb(x, 1.0, 0.25)


def ui_click():
    return mode(3000, 0.004, 0.04) + mode(1500, 0.006, 0.04) * 0.4


def music_box_loop():
    bpm = 66
    beat = 60.0 / bpm
    # An original little lullaby in A minor (note, beats). 0 = rest.
    A4, B4, C5, D5, E5, F5, G4, Gs4, A3, E4, F4 = 440.0, 493.9, 523.3, 587.3, 659.3, 698.5, 392.0, 415.3, 220.0, 329.6, 349.2
    melody = [(E5, 1), (C5, 0.5), (A4, 0.5), (B4, 1), (Gs4, 1),
              (A4, 1), (C5, 0.5), (E5, 0.5), (F5, 1.5), (E5, 0.5),
              (D5, 1), (B4, 0.5), (Gs4, 0.5), (A4, 1), (E4, 1),
              (F4, 1), (Gs4, 0.5), (B4, 0.5), (A4, 2)]
    bass = [(A3, 4), (220.0 * 0.749, 4), (220.0 * 0.891, 4), (A3, 4)]  # A, F, G#, A-ish
    total = sum(b for _, b in melody) * beat
    L = total
    two = np.zeros(int(L * 2 * SR) + SR)

    def tine(f, d=2.5):
        tt = t_axis(d)
        detune = 1 + rng.uniform(-0.002, 0.002)
        return (np.sin(2 * np.pi * f * detune * tt) * np.exp(-tt / 0.9) +
                np.sin(2 * np.pi * f * 2.76 * tt) * np.exp(-tt / 0.18) * 0.35 +
                np.sin(2 * np.pi * f * 5.4 * tt) * np.exp(-tt / 0.06) * 0.2)

    for rep in range(2):
        tc = rep * L
        for f, b in melody:
            two = place(two, tine(f), tc, 0.8)
            tc += b * beat
        tc = rep * L
        for f, b in bass:
            two = place(two, tine(f, 3.5) * 0.6, tc, 0.5)
            tc += b * beat
    two = reverb(two, 2.5, 0.35, 5000)
    # take the second repetition so reverb tails wrap around seamlessly
    start = int(L * SR)
    return two[start:start + int(L * SR)]


def main():
    os.makedirs(OUT, exist_ok=True)
    print("Writing to", os.path.abspath(OUT))
    save("knock", knock())
    save("knock_heavy", knock_heavy())
    save("door_creak", door_creak(), -3)
    save("door_slam", door_slam())
    save("door_rattle", door_rattle(), -2)
    save("phone_buzz", phone_buzz(), -4)
    save("phone_ping", phone_ping(), -6)
    save("voice_a", voice(730, 1090, 2440), -3)
    save("voice_e", voice(530, 1840, 2480), -3)
    save("voice_o", voice(570, 840, 2410), -3)
    save("voice_u", voice(440, 1020, 2240), -3)
    save("scream", scream(), -0.5)
    save("heartbeat_loop", heartbeat(), -2, do_fade=False)
    save("static_loop", static_loop(), -8, do_fade=False)
    save("intercom_click", intercom_click(), -4)
    save("clock_tick_loop", clock_tick(), -10, do_fade=False)
    save("chime", chime(), -2)
    for i in range(3):
        save(f"footstep_{i}", footstep(i), -6)
    save("light_buzz", light_buzz(), -6)
    save("window_tap", window_tap(), -3)
    save("sting", sting(), -2)
    save("breath_loop", breath_loop(), -6, do_fade=False)
    save("thunder", thunder(), -2)
    save("rain_loop", rain_loop(), -6, do_fade=False)
    save("drone_loop", drone_loop(), -4, do_fade=False)
    save("hum_loop", hum_loop(), -14, do_fade=False)
    save("birds", birds(), -6)
    save("ui_click", ui_click(), -8)
    save("music_box_loop", music_box_loop(), -3, do_fade=False)
    print("Done.")


if __name__ == "__main__":
    main()
