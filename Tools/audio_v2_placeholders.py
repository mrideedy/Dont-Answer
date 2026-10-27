"""
Placeholder versions of every sound the game asks for by name (Resources/SFXFallback/<id>.wav).
Real recordings in Resources/SFX/<id>.mp3 (e.g. from ElevenLabs) automatically replace these.

    python Tools/audio_v2_placeholders.py
"""
import os
import sys

sys.path.insert(0, os.path.dirname(__file__))
import generate_audio as ga  # noqa: E402
from generate_audio import (np, SR, t_axis, noise, brown, lp, hp, bp, mode, resonate, place, reverb,  # noqa: E402
                            loop_freq, knock, knock_heavy, door_creak, door_slam, door_rattle, window_tap,
                            footstep, breath_loop, phone_buzz, intercom_click, thunder, rain_loop, scream,
                            heartbeat, drone_loop, sting, chime, birds, music_box_loop, static_loop, clock_tick,
                            light_buzz, ui_click, voice, rng)


def landline_ring():
    d = 4.0
    t = t_axis(d)
    bell = np.sin(2 * np.pi * 1180 * t) * 0.6 + np.sin(2 * np.pi * 1420 * t) * 0.5
    clapper = (np.sin(2 * np.pi * 20 * t) > 0).astype(float)
    gate = lp(((t % 3.0) < 1.6).astype(float), 40)
    return bell * (0.6 + 0.4 * clapper) * gate


def child_giggle():
    x = np.zeros(int(1.6 * SR))
    tc = 0.05
    for i in range(7):
        g = voice(800, 1500, 3000, f0=330 + rng.uniform(-30, 60) + i * 8, d=0.09)
        x = place(x, g, tc, 0.9 - i * 0.08)
        tc += rng.uniform(0.11, 0.16)
    return reverb(x, 1.6, 0.4, 5000)


def creature_click():
    d = 2.2
    x = np.zeros(int(d * SR))
    tc = 0.0
    while tc < d - 0.2:
        c = place(mode(rng.uniform(300, 700), 0.01, 0.03) * 0.6, hp(noise(0.02), 1500) * np.exp(-t_axis(0.02) / 0.003), 0, 1)
        x = place(x, c, tc, rng.uniform(0.5, 1.0))
        tc += rng.uniform(0.03, 0.09) if rng.uniform() < 0.8 else rng.uniform(0.2, 0.4)
    growl = lp(brown(d), 180) * (0.5 + 0.5 * np.sin(2 * np.pi * 7 * t_axis(d))) * 0.6
    return reverb(x + growl, 1.2, 0.3, 3000)


def bone_crack():
    x = np.zeros(int(1.8 * SR))
    tc = 0.0
    for _ in range(9):
        pop = place(mode(rng.uniform(150, 400), 0.02, 0.04) * 0.5,
                    hp(noise(0.03), 900) * np.exp(-t_axis(0.03) / rng.uniform(0.004, 0.01)), 0, 1)
        x = place(x, np.tanh(pop * 3), tc, rng.uniform(0.4, 1.0))
        tc += rng.uniform(0.08, 0.25)
    return reverb(x, 0.4, 0.1)


def drag():
    d = 4.0
    t = t_axis(d)
    x = bp(noise(d), 120, 1500) * (0.6 + 0.4 * np.abs(np.sin(2 * np.pi * 0.7 * t)))
    x = place(x, door_creak()[: int(1.0 * SR)] * 0.4, 1.1)
    return x * np.minimum(1, t / 0.2) * np.clip((d - t) / 0.5, 0, 1)


def breaker(off):
    d = 2.0
    t = t_axis(d)
    x = np.zeros(len(t))
    x = place(x, mode(70, 0.08, 0.5) + mode(140, 0.05, 0.5) * 0.6, 0, 1)
    x = place(x, lp(noise(0.05), 2000), 0, 1)
    hum = np.sin(2 * np.pi * 120 * t) * 0.3 + np.sign(np.sin(2 * np.pi * 60 * t)) * 0.08
    env = np.exp(-t / 0.4) if off else np.clip((t - 0.2) / 0.6, 0, 1) * np.exp(-np.maximum(0, t - 1.2) / 0.5)
    return x + hum * env


def basement_tone():
    L = 10.0
    t = t_axis(L)
    x = np.sin(2 * np.pi * loop_freq(50, L) * t) * 0.3 + np.sin(2 * np.pi * loop_freq(100, L) * t) * 0.12
    x += lp(np.tile(noise(L / 2), 2), 300) * 0.25
    for _ in range(6):
        x = place(x, mode(rng.uniform(1600, 2600), 0.03, 0.2), rng.uniform(0, L - 0.3), rng.uniform(0.3, 0.7))
    return x


def house_tone():
    L = 10.0
    t = t_axis(L)
    x = lp(np.tile(noise(L / 2), 2), 400) * 0.25 + np.sin(2 * np.pi * loop_freq(60, L) * t) * 0.03
    x += bp(np.tile(noise(L / 2), 2), 300, 900) * (0.5 + 0.5 * np.sin(2 * np.pi * (1 / L) * t)) * 0.15
    return x


def wind_howl():
    d = 6.0
    t = t_axis(d)
    x = np.zeros(len(t))
    for f in (420, 610, 830):
        x += resonate(bp(noise(d), f * 0.7, f * 1.3), f, 8) * 0.5
    return x * np.sin(np.pi * t / d) ** 1.2


def whisper_swirl():
    d = 4.0
    t = t_axis(d)
    x = np.zeros(len(t))
    for k in range(5):
        x += bp(noise(d), 1800, 6000) * np.clip(np.sin(2 * np.pi * rng.uniform(1.5, 3) * t + k), 0, 1) ** 3 * rng.uniform(0.3, 0.7)
    return reverb(x * np.sin(np.pi * t / d), 1.5, 0.4, 7000)


def clunk(f=900, d=0.4):
    x = mode(f, 0.01, d) + mode(f * 2.3, 0.006, d) * 0.5
    return place(x, hp(noise(0.02), 2000) * np.exp(-t_axis(0.02) / 0.004), 0, 0.6)


def scratch(d=3.0, hi=3000):
    t = t_axis(d)
    return bp(noise(d), hi * 0.4, hi) * (np.abs(np.sin(2 * np.pi * 2.3 * t)) ** 2) * np.sin(np.pi * t / d)


def steps(n, gap):
    x = np.zeros(int((n * gap + 0.6) * SR))
    for i in range(n):
        x = place(x, footstep(i % 3), i * gap + rng.uniform(0, 0.05), rng.uniform(0.7, 1.0))
    return x


def knocks(times, heavy=False):
    x = np.zeros(int((times[-1] + 0.8) * SR))
    k = knock_heavy() if heavy else knock()
    for tt in times:
        x = place(x, k, tt, rng.uniform(0.85, 1.0))
    return x


def car_pass():
    d = 6.0
    t = t_axis(d)
    env = np.exp(-((t - 3.0) / 1.2) ** 2)
    return lp(brown(d), 400) * env + bp(noise(d), 800, 3000) * env * 0.2


def dog_distant():
    x = np.zeros(int(4.0 * SR))
    for i in range(5):
        x = place(x, lp(voice(600, 1200, 2400, f0=rng.uniform(260, 320), d=0.16) * 1.5, 1800), 0.2 + i * 0.45, 1.0 - i * 0.15)
    return reverb(x, 2.0, 0.5, 2000)


def faucet_drip():
    x = np.zeros(int(6.0 * SR))
    for k in range(5):
        x = place(x, mode(rng.uniform(1400, 2000), 0.02, 0.15) + mode(rng.uniform(600, 900), 0.03, 0.15) * 0.4, k * 1.2 + 0.1, 0.7)
    return x


def chair_scrape():
    d = 1.6
    t = t_axis(d)
    rate = 60 + 30 * np.sin(2 * np.pi * 1.3 * t)
    pulses = np.diff(np.floor(np.cumsum(rate / SR)), prepend=0)
    x = resonate(pulses, 450, 10) + resonate(pulses, 900, 14) * 0.6 + resonate(pulses, 1700, 18) * 0.3
    return reverb(x * np.sin(np.pi * t / d), 0.6, 0.2)


def glass_break():
    x = np.zeros(int(1.6 * SR))
    for k in range(18):
        x = place(x, mode(rng.uniform(2500, 7000), rng.uniform(0.01, 0.05), 0.3) * rng.uniform(0.3, 1.0),
                  rng.uniform(0, 0.25) + (0 if k < 6 else rng.uniform(0.1, 0.6)))
    x = place(x, lp(noise(0.05), 3000) * np.exp(-t_axis(0.05) / 0.01), 0, 1.0)
    return reverb(x, 0.6, 0.2)


def sheet_rustle():
    d = 2.5
    t = t_axis(d)
    return bp(noise(d), 1500, 7000) * (np.abs(np.sin(2 * np.pi * 1.4 * t)) ** 3) * np.sin(np.pi * t / d)


def tv_on():
    d = 2.0
    t = t_axis(d)
    x = np.sin(2 * np.pi * 15600 * t) * 0.05 + bp(noise(d), 500, 6000) * np.minimum(1, t / 0.1) * 0.6
    return place(x, mode(90, 0.05, 0.3), 0, 0.8)


def jumpscare():
    d = 2.0
    t = t_axis(d)
    stab = np.zeros(len(t))
    for f in (311, 330, 466, 622, 659, 932):
        stab += 2 * (np.cumsum(f * (1 + 0.01 * np.sin(2 * np.pi * 9 * t)) / SR) % 1) - 1
    stab = np.tanh(stab * 0.8) * np.exp(-t / 0.5)
    boom = mode(38, 0.5, d) * 2.5 + lp(noise(d), 150) * np.exp(-t / 0.3) * 1.5
    return reverb(stab * 0.6 + boom, 1.5, 0.25)


def walkie_static():
    d = 1.6
    t = t_axis(d)
    x = bp(noise(d), 400, 3500) * np.minimum(1, t / 0.02)
    x = place(x, np.sin(2 * np.pi * 1000 * t_axis(0.08)) * 0.4, 0, 1)
    return x * np.clip((d - t) / 0.2, 0, 1)


def main():
    out = os.path.join(os.path.dirname(__file__), "..", "Assets", "_Game", "Resources", "SFXFallback")
    os.makedirs(out, exist_ok=True)
    ga.OUT = out
    s = ga.save
    print("Writing to", os.path.abspath(out))
    s("knock_single", knock())
    s("knock_soft", knock() * 0.6, -6)
    s("knock_firm", knocks([0.0, 0.24, 0.47]))
    s("knock_pound", knocks([0.0, 0.17, 0.33, 0.52, 0.68, 0.86], heavy=True))
    s("knock_heavy", knock_heavy())
    s("knock_secret", knocks([0.0, 0.16, 0.75, 1.35, 1.51]))
    s("door_creak", door_creak(), -3)
    s("door_slam", door_slam())
    s("doorknob_rattle", door_rattle(), -2)
    s("deadbolt", clunk(700, 0.5), -2)
    s("window_latch", clunk(1500, 0.3), -4)
    s("window_tap", window_tap(), -3)
    s("window_scratch", scratch(3.0, 6000), -3)
    s("wall_scratch", lp(scratch(4.0, 2500), 1800), -3)
    s("footsteps_room", steps(6, 0.6), -3)
    s("footsteps_ceiling", lp(steps(8, 0.55), 500), -2)
    s("porch_steps", steps(5, 0.5), -4)
    s("breath_door", breath_loop(), -6, do_fade=False)
    s("whisper_swirl", whisper_swirl(), -4)
    s("mail_slot", clunk(1100, 0.35), -4)
    s("landline_ring", landline_ring(), -6, do_fade=False)
    s("phone_buzz", phone_buzz(), -4)
    s("intercom", intercom_click(), -4)
    s("tv_on", tv_on(), -6)
    s("thunder", thunder(), -2)
    s("rain_window", rain_loop(), -6, do_fade=False)
    s("house_tone", house_tone(), -10, do_fade=False)
    s("wind_howl", wind_howl(), -4)
    s("breaker_off", breaker(True), -3)
    s("breaker_on", breaker(False), -3)
    s("basement_tone", basement_tone(), -8, do_fade=False)
    s("creature_click", creature_click(), -2)
    s("creature_scream", scream(), -0.5)
    s("jumpscare", jumpscare(), -0.5)
    s("drag", drag(), -3)
    s("bone_crack", bone_crack(), -2)
    s("heartbeat", heartbeat(), -2, do_fade=False)
    s("drone_dread", drone_loop(), -4, do_fade=False)
    s("sting_reveal", sting(), -2)
    s("child_giggle", child_giggle(), -3)
    s("church_bell", chime(), -2)
    s("dawn_birds", birds(), -6)
    s("car_pass", car_pass(), -6)
    s("dog_distant", dog_distant(), -8)
    s("faucet_drip", faucet_drip(), -8, do_fade=False)
    s("chair_scrape", chair_scrape(), -3)
    s("glass_break", glass_break(), -2)
    s("flashlight", clunk(2400, 0.15), -8)
    s("sheet_rustle", sheet_rustle(), -6)
    s("walkie_static", walkie_static(), -6)
    s("music_box", music_box_loop(), -3, do_fade=False)
    s("static_loop", static_loop(), -8, do_fade=False)
    s("clock_tick", clock_tick(), -10, do_fade=False)
    s("light_buzz", light_buzz(), -6)
    s("ui_click", ui_click(), -8)
    for i, (a, b, c) in enumerate([(730, 1090, 2440), (530, 1840, 2480), (570, 840, 2410), (440, 1020, 2240)]):
        s(f"voice_{i}", voice(a, b, c), -3)
    for i in range(3):
        s(f"footstep_{i}", footstep(i), -6)
    print("Done.")


if __name__ == "__main__":
    main()
