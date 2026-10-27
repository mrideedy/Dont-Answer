"""
Flags voice lines that probably came out wrong (a model that rambled past the text, cut out early, or whispered
into silence), comparing each file's length and loudness with its text. Prints the IDs to re-roll:

    python Tools/audit_voices.py
    <venv python> Tools/voices_chatterbox.py --only <the IDs it prints>
"""
import csv
import os

import numpy as np
import soundfile as sf

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, ".."))
LINES = os.path.join(ROOT, "Assets", "_Game", "Data", "Lines.csv")
VOICE = os.path.join(ROOT, "Assets", "_Game", "Resources", "Voice")


def main():
    with open(LINES, newline="", encoding="utf-8") as f:
        lines = list(csv.DictReader(f))
    suspicious = []
    for line in lines:
        path = os.path.join(VOICE, line["id"] + ".wav")
        if not os.path.exists(path):
            suspicious.append((line["id"], "missing"))
            continue
        x, sr = sf.read(path, always_2d=True)
        x = x.mean(axis=1)
        secs = len(x) / sr
        words = max(1, len(line["text"].split()))
        pauses = line["text"].count("...") * 0.75 + sum(line["text"].count(c) for c in ".?!") * 0.45
        expected = words / 2.6 + pauses + 0.5          # ~2.6 words a second, plus the pauses we add
        rms = float(np.sqrt(np.mean(x ** 2)) + 1e-9)
        why = []
        if secs > expected * 2.4 + 2.0:
            why.append(f"long {secs:.1f}s vs ~{expected:.1f}s")
        if secs < expected * 0.35:
            why.append(f"short {secs:.1f}s vs ~{expected:.1f}s")
        if rms < 0.01:
            why.append(f"quiet rms {rms:.3f}")
        if why:
            suspicious.append((line["id"], ", ".join(why)))
    for lid, why in suspicious:
        print(f"  {lid:<14} {why}")
    print(f"{len(suspicious)} suspicious of {len(lines)}")
    if suspicious:
        print("re-roll: " + ",".join(lid for lid, _ in suspicious))


if __name__ == "__main__":
    main()
