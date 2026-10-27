"""
ElevenLabs batch generator for "Don't Answer".

  python Tools/elevenlabs/generate.py script            # writes VOICE_SCRIPT.md (for generating by hand)
  python Tools/elevenlabs/generate.py estimate          # how many characters / credits you'll need
  python Tools/elevenlabs/generate.py voices            # generates every voice line that has a voice_id
  python Tools/elevenlabs/generate.py sfx               # generates every sound effect
  python Tools/elevenlabs/generate.py voices --only ANA_K1,ANA_K2 --force

Needs your API key in the ELEVENLABS_API_KEY environment variable (set it yourself, e.g.
`setx ELEVENLABS_API_KEY "..."` then open a new terminal). The key is only read from the
environment and is never printed or written anywhere.

Files go to Assets/_Game/Resources/Voice/<ID>.mp3 and Assets/_Game/Resources/SFX/<id>.mp3.
Existing files are skipped unless --force is given, so you can re-run safely.
"""
import csv
import json
import os
import sys
import time
import urllib.error
import urllib.request

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
VOICE_OUT = os.path.join(ROOT, "Assets", "_Game", "Resources", "Voice")
SFX_OUT = os.path.join(ROOT, "Assets", "_Game", "Resources", "SFX")
API = "https://api.elevenlabs.io/v1"


def load_lines():
    with open(os.path.join(ROOT, "Assets", "_Game", "Data", "Lines.csv"), newline="", encoding="utf-8") as f:
        return list(csv.DictReader(f))


def load_sfx():
    with open(os.path.join(HERE, "sfx.csv"), newline="", encoding="utf-8") as f:
        return list(csv.DictReader(f))


def load_voices():
    with open(os.path.join(HERE, "voices.json"), encoding="utf-8") as f:
        return json.load(f)


def api_key():
    key = os.environ.get("ELEVENLABS_API_KEY", "").strip()
    if not key:
        sys.exit("ELEVENLABS_API_KEY is not set. Set it in your environment first (see the top of this file).")
    return key


def post(url, payload, key, retries=4):
    body = json.dumps(payload).encode("utf-8")
    for attempt in range(retries):
        req = urllib.request.Request(url, data=body, method="POST", headers={
            "xi-api-key": key, "Content-Type": "application/json", "Accept": "audio/mpeg"})
        try:
            with urllib.request.urlopen(req, timeout=120) as res:
                return res.read()
        except urllib.error.HTTPError as e:
            detail = e.read().decode("utf-8", "replace")[:300]
            if e.code == 429 and attempt < retries - 1:
                wait = 5 * (attempt + 1)
                print(f"    rate limited, waiting {wait}s...")
                time.sleep(wait)
                continue
            raise RuntimeError(f"HTTP {e.code}: {detail}") from None
    raise RuntimeError("gave up after retries")


def parse_args():
    args = sys.argv[1:]
    cmd = args[0] if args else "help"
    only = None
    if "--only" in args:
        only = set(x.strip() for x in args[args.index("--only") + 1].split(",") if x.strip())
    return cmd, only, "--force" in args


def cmd_voices(only, force):
    key = api_key()
    cfg = load_voices()
    os.makedirs(VOICE_OUT, exist_ok=True)
    made = skipped = missing = 0
    for line in load_lines():
        lid = line["id"]
        if only and lid not in only:
            continue
        out = os.path.join(VOICE_OUT, lid + ".mp3")
        if os.path.exists(out) and not force:
            skipped += 1
            continue
        voice = cfg["voices"].get(line["voice"], {})
        if not voice.get("voice_id"):
            missing += 1
            continue
        settings = cfg["settings"].get(line["variant"], cfg["settings"]["human"])
        print(f"  {lid:<14} {line['speaker']:<20} {line['text'][:60]}")
        try:
            audio = post(f"{API}/text-to-speech/{voice['voice_id']}?output_format=mp3_44100_128",
                         {"text": line["text"], "model_id": cfg.get("model_id", "eleven_multilingual_v2"),
                          "voice_settings": settings}, key)
        except RuntimeError as e:
            print(f"    FAILED: {e}")
            continue
        with open(out, "wb") as f:
            f.write(audio)
        made += 1
        time.sleep(0.4)
    print(f"Done. generated={made} skipped(existing)={skipped} skipped(no voice_id)={missing}")


def cmd_sfx(only, force):
    key = api_key()
    os.makedirs(SFX_OUT, exist_ok=True)
    made = 0
    for s in load_sfx():
        sid = s["id"]
        if only and sid not in only:
            continue
        out = os.path.join(SFX_OUT, sid + ".mp3")
        if os.path.exists(out) and not force:
            continue
        payload = {"text": s["prompt"], "duration_seconds": float(s["seconds"]), "prompt_influence": 0.45}
        if s["loop"] == "yes":
            payload["loop"] = True
        print(f"  {sid:<16} {s['prompt'][:70]}")
        try:
            audio = post(f"{API}/sound-generation", payload, key)
        except RuntimeError as e:
            if "loop" in payload:  # older API versions don't know 'loop'
                payload.pop("loop")
                try:
                    audio = post(f"{API}/sound-generation", payload, key)
                except RuntimeError as e2:
                    print(f"    FAILED: {e2}")
                    continue
            else:
                print(f"    FAILED: {e}")
                continue
        with open(out, "wb") as f:
            f.write(audio)
        made += 1
        time.sleep(0.4)
    print(f"Done. generated={made}")


def cmd_estimate():
    lines = load_lines()
    chars = sum(len(l["text"]) for l in lines)
    sfx = load_sfx()
    secs = sum(float(s["seconds"]) for s in sfx)
    print(f"Voice lines: {len(lines)}  characters: {chars}")
    print(f"  eleven_multilingual_v2 ~= {chars} credits, eleven_flash_v2_5 ~= {chars // 2} credits")
    print(f"Sound effects: {len(sfx)}  total seconds: {secs:.0f}")
    print("  (sound effect pricing changes; check your ElevenLabs plan - expect a few thousand credits)")


def cmd_script():
    cfg = load_voices()
    lines = load_lines()
    order = []
    for l in lines:
        if l["voice"] not in order:
            order.append(l["voice"])
    out = ["# Don't Answer - voice script", "",
           "Generate each line with the character's voice and save it as **`<ID>.mp3`** "
           "into `Assets/_Game/Resources/Voice/`. The file name must match the ID exactly.", "",
           "**Settings:** normal lines use Stability ~45%, Similarity ~80%, Style ~25%. "
           "Lines marked **MIMIC** use the *same voice* with Stability ~10% and Style 0%, which makes the delivery uncanny. "
           "The game adds the rest of the creepiness. Text like `--` means the line cuts off abruptly.", ""]
    for v in order:
        info = cfg["voices"].get(v, {})
        out.append(f"## {v.upper()}")
        out.append(f"*Voice: {info.get('description', '')}*")
        out.append("")
        out.append("| ID | Type | Line | Direction |")
        out.append("|---|---|---|---|")
        for l in lines:
            if l["voice"] != v:
                continue
            kind = {"mimic": "**MIMIC**", "memory": "memory", "human": ""}[l["variant"]]
            text = l["text"].replace("|", "/")
            out.append(f"| `{l['id']}` | {kind} | {text} | {l['direction']} |")
        out.append("")
    out.append("## Sound effects (ElevenLabs > Sound Effects)")
    out.append("Save each as **`<id>.mp3`** into `Assets/_Game/Resources/SFX/`. Tick *Loop* where it says loop.")
    out.append("")
    out.append("| id | Seconds | Loop | Prompt |")
    out.append("|---|---|---|---|")
    for s in load_sfx():
        out.append(f"| `{s['id']}` | {s['seconds']} | {s['loop']} | {s['prompt']} |")
    path = os.path.join(ROOT, "VOICE_SCRIPT.md")
    with open(path, "w", encoding="utf-8") as f:
        f.write("\n".join(out) + "\n")
    print("Wrote", path)


if __name__ == "__main__":
    cmd, only, force = parse_args()
    if cmd == "voices":
        cmd_voices(only, force)
    elif cmd == "sfx":
        cmd_sfx(only, force)
    elif cmd == "estimate":
        cmd_estimate()
    elif cmd == "script":
        cmd_script()
    else:
        print(__doc__)
