"""
Generates every voice line in Assets/_Game/Data/Lines.csv with Kokoro TTS (offline, Apache-2.0),
then sound-designs each one for how the player hears it (see voice_fx.py).

    python Tools/voices_kokoro.py            # generate missing lines
    python Tools/voices_kokoro.py --force    # regenerate everything
    python Tools/voices_kokoro.py --only ANA_K1,LEOR_A2
    python Tools/voices_kokoro.py --refs     # write clean reference clips for voices_chatterbox.py

Writes Assets/_Game/Resources/Voice/<ID>.wav. These are the fallback voices; voices_chatterbox.py
re-acts every line with real emotion using these voices as its reference timbres.
"""
import csv
import os
import re
import sys
import time

import numpy as np
import soundfile as sf
from scipy import signal

from voice_fx import SR, apply_treatment, mimic_touch, norm, pad, repitch, speakable, treatment, unison, vibrato

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, ".."))
LINES = os.path.join(ROOT, "Assets", "_Game", "Data", "Lines.csv")
OUT = os.path.join(ROOT, "Assets", "_Game", "Resources", "Voice")
REFS = os.path.join(HERE, "voice_refs")
MODEL = os.path.join(HERE, "kokoro", "kokoro-v1.0.fp16.onnx")
VOICES = os.path.join(HERE, "kokoro", "voices-v1.0.bin")

# voice key -> (kokoro voice, speed, lang, pitch factor applied afterwards)
CAST = {
    "voss":    ("bm_george", 0.86, "en-gb", 0.94),
    "leo":     ("af_sky", 0.98, "en-us", 1.13),
    "mom":     ("af_sarah", 0.9, "en-us", 0.98),
    "anchor":  ("am_michael", 0.95, "en-us", 1.0),
    "marco":   ("am_puck", 1.02, "en-us", 1.0),
    "kids":    ("af_sky", 0.92, "en-us", 1.22),
    "ana":     ("af_heart", 1.0, "en-us", 1.0),
    "officer": ("am_adam", 0.92, "en-us", 0.95),
    "tom":     ("am_eric", 1.0, "en-us", 1.02),
    "eli":     ("am_fenrir", 1.06, "en-us", 1.0),
    "dad":     ("am_onyx", 0.9, "en-us", 0.96),
    "hale":    ("af_nicole", 0.84, "en-us", 1.03),
}

# What each character says in their reference clip: ordinary, in character, about ten seconds.
REF_TEXT = {
    "voss": "I've lived on Elm Street for forty years. I know every board on this porch, and I know what walks on it after dark.",
    "leo": "When I grow up I'm going to be a lighthouse keeper. You can visit me, and we can watch the ships come in. Okay?",
    "mom": "Hi sweetheart, it's Mom. I just wanted to hear your voice. Call me back when you get this, okay? I love you.",
    "anchor": "Good evening, and welcome back. Tonight, one year after he disappeared, Hollow Creek remembers Leo Hollis.",
    "marco": "Hey, pizza delivery for Elm Street! Sorry it took a while, the roads are a total mess out there tonight.",
    "kids": "Trick or treat! We came all the way from the end of the street, and it's really cold out here.",
    "ana": "Hey, it's me. My car died on Route 9 and I walked the rest of the way. I'm soaked, but I'm fine. Really.",
    "officer": "Evening. Hollow Creek police. We've had some reports on this street tonight, so I'm checking every house.",
    "tom": "Dude, the party next door is insane. I lost my phone, my keys, and I'm pretty sure my left shoe.",
    "eli": "I was walking home from the station when something came out of the trees. I didn't see it. I just ran.",
    "dad": "I drove over as soon as your mother told me. I know I haven't been there. I'm here now. Let me in.",
    "hale": "Oh, hello, dear. I'm looking for my cat. She always comes to this porch when it rains. Have you seen her?",
}


def kokoro():
    import onnxruntime as ort
    from kokoro_onnx import Kokoro
    # Lean on memory: no arena pooling, two threads. (The PC may be running other heavy apps.)
    so = ort.SessionOptions()
    so.enable_cpu_mem_arena = False
    so.enable_mem_pattern = False
    so.intra_op_num_threads = 2
    sess = ort.InferenceSession(MODEL, sess_options=so, providers=["CPUExecutionProvider"])
    return Kokoro.from_session(sess, VOICES)


def speak(k, text, voice_key, speed_scale=1.0):
    voice, speed, lang, pitch = CAST.get(voice_key, ("am_adam", 1.0, "en-us", 1.0))
    parts = [p for p in re.split(r"(?<=[.!?])\s+", text) if p.strip()] if len(text) > 70 else [text]
    chunks = []
    for part in parts:
        samples, sr = k.create(part, voice=voice, speed=speed * speed_scale, lang=lang)
        x = np.asarray(samples, dtype=np.float64)
        if sr != SR:
            x = signal.resample(x, int(len(x) * SR / sr))
        chunks.append(x)
        chunks.append(np.zeros(int(0.18 * SR)))
    x = np.concatenate(chunks[:-1]) if len(chunks) > 1 else chunks[0]
    return repitch(x, pitch)


def render(k, line):
    text = speakable(line["text"])
    angry = line["id"].endswith("_AN") or text.count("!") >= 2
    x = speak(k, text, line["voice"], 1.08 if angry else 1.0)
    if line["voice"] == "kids":
        x = unison(x, (1.0, 1.07, 0.94), (0.0, 0.014, 0.024))
    if line["voice"] == "hale":
        x = vibrato(x, 5.2, 0.0022)
    if angry:
        x = np.tanh(x / (np.max(np.abs(x)) + 1e-9) * 2.4)
    if line["variant"] == "mimic":
        x = mimic_touch(x)
    kind = treatment(line["id"], line["variant"])
    return norm(apply_treatment(pad(x), kind), 0.89), kind


def write_refs(k):
    os.makedirs(REFS, exist_ok=True)
    for key, text in REF_TEXT.items():
        x = norm(speak(k, text, key), 0.9)
        sf.write(os.path.join(REFS, key + ".wav"), x.astype(np.float32), SR, subtype="PCM_16")
        print(f"  ref {key:<8} {len(x) / SR:4.1f}s")


def main():
    args = sys.argv[1:]
    k = kokoro()
    if "--refs" in args:
        write_refs(k)
        return
    force = "--force" in args
    only = None
    if "--only" in args:
        only = set(a.strip() for a in args[args.index("--only") + 1].split(","))
    os.makedirs(OUT, exist_ok=True)
    with open(LINES, newline="", encoding="utf-8") as f:
        lines = list(csv.DictReader(f))
    import gc
    t0 = time.time()
    done = 0
    for i, line in enumerate(lines):
        lid = line["id"]
        if only and lid not in only:
            continue
        out = os.path.join(OUT, lid + ".wav")
        if os.path.exists(out) and not force:
            continue
        try:
            x, kind = render(k, line)
        except Exception as e:  # keep going; report at the end
            print(f"  FAILED {lid}: {e}")
            continue
        sf.write(out, x.astype(np.float32), SR, subtype="PCM_16")
        done += 1
        secs = len(x) / SR
        del x
        gc.collect()
        print(f"  [{i + 1:3}/{len(lines)}] {lid:<14} {kind:<10} {secs:5.1f}s  {line['text'][:50]}", flush=True)
    print(f"Done. {done} lines in {time.time() - t0:.0f}s")


if __name__ == "__main__":
    main()
