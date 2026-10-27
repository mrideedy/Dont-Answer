"""
Re-acts every line in Assets/_Game/Data/Lines.csv with Chatterbox (Resemble AI, MIT): real emotion and pacing,
in each character's own voice (cloned from Tools/voice_refs/<voice>.wav, made by voices_kokoro.py --refs).

Each line's acting note ("direction" column) sets how hard it's played (Chatterbox's emotion exaggeration),
how fast (cfg weight) and how loose (temperature). Punctuation is honoured with real silences: the line is
acted phrase by phrase and joined with pauses (full stop, question, exclamation, ellipsis, dash), because the
model on its own reads "..." as a comma. Then the same sound design as before (voice_fx.py) is applied.

    <venv python> Tools/voices_chatterbox.py              # everything not done yet
    <venv python> Tools/voices_chatterbox.py --force      # redo everything
    <venv python> Tools/voices_chatterbox.py --only VOSS_P01,ANA_K1

The venv is C:/Users/<you>/dont-answer-tts/venv (Python 3.11, torch 2.6 CUDA, chatterbox-tts 0.1.7).
Before the first run, the old Kokoro voices are copied to Tools/voice_backup_kokoro/.
"""
import csv
import hashlib
import os
import re
import shutil
import sys
import time

import numpy as np
import soundfile as sf

from voice_fx import SR, apply_treatment, mimic_touch, norm, pad, silence, speakable, treatment, trim, unison, vibrato

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, ".."))
LINES = os.path.join(ROOT, "Assets", "_Game", "Data", "Lines.csv")
OUT = os.path.join(ROOT, "Assets", "_Game", "Resources", "Voice")
REFS = os.path.join(HERE, "voice_refs")
BACKUP = os.path.join(HERE, "voice_backup_kokoro")
DONE_MARK = os.path.join(HERE, "voice_backup_kokoro", "_chatterbox_done.txt")

# Pauses that make punctuation mean something (seconds, before a little jitter).
PAUSE = {".": 0.42, "?": 0.5, "!": 0.32, "...": 0.75, "--": 0.18, ",": 0.0}


class Acting:
    def __init__(self, exaggeration=0.55, cfg=0.45, temperature=0.8):
        self.exaggeration, self.cfg, self.temperature = exaggeration, cfg, temperature

    def __repr__(self):
        return f"ex{self.exaggeration:.2f} cfg{self.cfg:.2f} t{self.temperature:.2f}"


# Acting notes -> delivery. First match wins, strongest first.
RULES = [
    (r"scream|shriek|howl", Acting(1.3, 0.55, 0.9)),
    (r"panic|terrified|desperate|sob|crying|tearful|begging|cut off", Acting(1.05, 0.5, 0.85)),
    (r"angry|furious|snarl|yell|shout|hiss|rage", Acting(0.95, 0.5, 0.85)),
    (r"scared|frightened|nervous|shaking|chattering|shiver|uneasy|unsettled|out of breath|breathless|hurt", Acting(0.8, 0.42, 0.85)),
    (r"excited|delight|giggl|laugh|cheerful|sing-song", Acting(0.85, 0.45, 0.85)),
    (r"urgent|insistent|impatient|hurry|exasperat", Acting(0.75, 0.55, 0.8)),
    (r"flat|monotone|cold|mechanical|beat too slow|no warmth|even rhythm|unison|recording of a person", Acting(0.28, 0.3, 0.6)),
    (r"whisper", Acting(0.45, 0.35, 0.75)),
    (r"weary|tired|slow|measured|grave|firm|intimate|dreamy|sleepy|quiet|small voice|low", Acting(0.6, 0.3, 0.8)),
    (r"warm|sincere|gentle|proud|relieved|fond|sweet|friendly|hopeful|earnest|stunned", Acting(0.62, 0.4, 0.8)),
]


def acting_for(line, previous):
    d = line["direction"].lower()
    if d.startswith("same") and previous is not None:
        a = Acting(previous.exaggeration, previous.cfg, previous.temperature)
        if "whisper" in d:
            a.exaggeration = min(a.exaggeration, 0.45)
        if "slow" in d:
            a.cfg = 0.3
        return a
    a = Acting()
    for pattern, preset in RULES:
        if re.search(pattern, d):
            a = Acting(preset.exaggeration, preset.cfg, preset.temperature)
            break
    if re.search(r"very slow|slow on|a beat too slow", d):
        a.cfg = min(a.cfg, 0.3)
    if line["text"].count("!") >= 2 and a.exaggeration < 0.8:
        a.exaggeration = 0.85
    if line["variant"] == "mimic" and not line["id"].endswith("_D"):
        # Almost right. A little too even, a little too slow: wearing someone's voice, not using it.
        a.exaggeration = min(a.exaggeration, 0.45)
        a.cfg = min(a.cfg, 0.35)
        a.temperature = min(a.temperature, 0.7)
    if line["voice"] == "kids":
        a.exaggeration, a.cfg = 0.3, 0.3
    return a


def phrases(text):
    """Splits a line at the punctuation that should breathe: returns [(phrase, pause_after)]."""
    t = speakable(text).replace("…", "...")
    tokens = re.split(r"(\.\.\.|--|[.!?]+)", t)
    out = []
    buf = ""
    for tok in tokens:
        if tok is None or tok == "":
            continue
        if re.fullmatch(r"\.\.\.|--|[.!?]+", tok):
            mark = "..." if tok == "..." else "--" if tok == "--" else tok[-1]
            spoken = buf.strip()
            if spoken:
                # Hesitations and cut-offs read as unfinished: keep them open with a comma.
                ending = "," if mark in ("...", "--") else tok
                out.append([spoken + ending, PAUSE[mark]])
            elif out:
                out[-1][1] = max(out[-1][1], PAUSE[mark])
            buf = ""
        else:
            buf += tok
    if buf.strip():
        out.append([buf.strip() + ".", 0.0])
    # Very short fragments make the model mumble; fold them into the next phrase.
    merged = []
    for p, pause in out:
        if merged and len(merged[-1][0].split()) < 2:
            merged[-1][0] = merged[-1][0] + " " + p
            merged[-1][1] = pause
        else:
            merged.append([p, pause])
    if merged:
        merged[-1][1] = 0.0
    return [(p, s) for p, s in merged]


def seed_for(line_id, attempt):
    return int(hashlib.md5(f"{line_id}:{attempt}".encode()).hexdigest()[:8], 16)


TOKEN_CAP = [1000]   # read by the patched t3.inference below


def cap_runaways(model):
    """Chatterbox sometimes never says stop and samples to 1000 tokens (40 s of mumbling, two minutes of GPU).
    Cap every take at a little over twice its expected length; a capped take fails the length check and is re-rolled."""
    original = model.t3.inference

    def capped(*args, **kwargs):
        kwargs["max_new_tokens"] = min(kwargs.get("max_new_tokens", 1000), TOKEN_CAP[0])
        return original(*args, **kwargs)

    model.t3.inference = capped


def act(model, torch, line, acting, conds):
    pieces = []
    for phrase, pause in phrases(line["text"]):
        expected = max(0.6, len(phrase) / 14.0)
        TOKEN_CAP[0] = int(expected * 25 * 2.4 + 40)   # speech tokens run at 25 per second
        best = None
        for attempt in range(3):
            torch.manual_seed(seed_for(line["id"] + phrase, attempt))
            model.conds = conds
            wav = model.generate(phrase, exaggeration=acting.exaggeration, cfg_weight=acting.cfg, temperature=acting.temperature)
            x = trim(wav.squeeze(0).cpu().numpy().astype(np.float64))
            secs = len(x) / SR
            ok = 0.35 * expected < secs < 2.6 * expected
            if best is None or ok:
                best = x
            if ok:
                break
        pieces.append(best)
        if pause > 0:
            pieces.append(silence(pause * np.random.uniform(0.9, 1.15)))
    return np.concatenate(pieces)


def finish(x, line):
    if line["voice"] == "kids":
        x = unison(x, (1.0, 1.07, 0.94), (0.0, 0.014, 0.024))
    if line["voice"] == "hale":
        x = vibrato(x, 5.2, 0.0018)
    if line["variant"] == "mimic":
        x = mimic_touch(x)
    kind = treatment(line["id"], line["variant"])
    return norm(apply_treatment(pad(x), kind), 0.89), kind


def backup_kokoro():
    if os.path.exists(BACKUP) and os.listdir(BACKUP):
        return
    os.makedirs(BACKUP, exist_ok=True)
    n = 0
    for f in os.listdir(OUT):
        if f.endswith(".wav"):
            shutil.copy2(os.path.join(OUT, f), os.path.join(BACKUP, f))
            n += 1
    print(f"Backed up {n} Kokoro lines to {BACKUP}")


def main():
    args = sys.argv[1:]
    force = "--force" in args
    only = None
    if "--only" in args:
        only = set(a.strip() for a in args[args.index("--only") + 1].split(","))
    with open(LINES, newline="", encoding="utf-8") as f:
        lines = list(csv.DictReader(f))
    backup_kokoro()
    done_ids = set()
    if os.path.exists(DONE_MARK) and not force:
        done_ids = set(open(DONE_MARK, encoding="utf-8").read().split())

    import torch
    from chatterbox.tts import ChatterboxTTS
    device = "cuda" if torch.cuda.is_available() else "cpu"
    print(f"Loading Chatterbox on {device}...", flush=True)
    model = ChatterboxTTS.from_pretrained(device=device)
    cap_runaways(model)

    conds = {}
    for key in sorted({l["voice"] for l in lines}):
        ref = os.path.join(REFS, key + ".wav")
        if not os.path.exists(ref):
            print(f"  missing reference for {key}; run voices_kokoro.py --refs")
            continue
        model.prepare_conditionals(ref, exaggeration=0.5)
        conds[key] = model.conds

    t0 = time.time()
    previous = {}
    count = 0
    for i, line in enumerate(lines):
        lid = line["id"]
        acting = acting_for(line, previous.get(line["voice"]))
        previous[line["voice"]] = acting
        if only and lid not in only:
            continue
        if lid in done_ids and not only:
            continue
        if line["voice"] not in conds:
            continue
        try:
            x = act(model, torch, line, acting, conds[line["voice"]])
            x, kind = finish(x, line)
        except Exception as e:  # keep going; report at the end
            print(f"  FAILED {lid}: {e}", flush=True)
            continue
        sf.write(os.path.join(OUT, lid + ".wav"), x.astype(np.float32), SR, subtype="PCM_16")
        with open(DONE_MARK, "a", encoding="utf-8") as f:
            f.write(lid + "\n")
        count += 1
        print(f"  [{i + 1:3}/{len(lines)}] {lid:<14} {kind:<10} {len(x) / SR:5.1f}s  {acting}  {line['text'][:44]}", flush=True)
    print(f"Done. {count} lines in {(time.time() - t0) / 60:.1f} min")


if __name__ == "__main__":
    main()
