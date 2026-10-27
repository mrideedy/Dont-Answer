# Don't Answer — GameDev.tv Halloween Jam 2026 (theme: UNINVITED)

Submissions close **October 13, 2026, 2:30 AM** (the time shown on the itch.io jam page).

## itch.io page text (copy and paste)

**DON'T ANSWER**
*Some of the knocks tonight won't be people.*

One year ago, on Halloween night, your little brother Leo knocked on your bedroom door and asked you to take him trick-or-treating. You told him to go away. He was last seen on the porch of 13 Elm Street, and he was never found.

Tonight you've rented that apartment. You're going to wait for him.

The old man next door left you a list of rules. The first one matters most:

> **Nothing comes in unless you let it in. An open door or window IS an invitation.**

From midnight until sunrise, people knock. There's a pizza driver, your best friend soaked from the storm, a police officer, a man bleeding on the porch, and your father. Some of them are real people who need you. Some are **the Uninvited**: things that wear the faces of people you know, and never quite get them right.

Survive until sunrise. Leave three of the living outside, or let the Uninvited in five times, and the night is lost.

Look through the peephole. Check for a shadow under the porch light and for breath in the cold. Ask them what only the real one would know. Lock the back door and latch the windows. And don't let fear make you cruel, because the people you leave outside don't stay outside forever.

- A full night of visitors whose roles change every time you play. Every one of them has tells.
- The rules change at 2 AM and 4 AM, because they're learning.
- Things you refuse come back as something else. The things you send away don't leave: they try the back door, throw every window open, press their faces to the glass, and walk the street. Open your door while one of them is standing in your front yard, and it comes running.
- The night gets worse by the hour. They work the window latches, slide the back door's bolt and turn the front deadbolt from outside, and you have to get there and force them shut. They reach through the mail slot, and they come two at a time. When one of them is pounding on your door, stay away from the peephole: if it catches your eye, that's an invitation too.
- The people you let in can be fooled. Something cries like a lost child at the back door, and one of your guests gets up to open it. Stop them.
- A power cut, a dark basement, and a note on the fuse box that says DON'T LOOK BACK.
- Four endings. Only one of them brings Leo home.

**Controls:** WASD to move · Mouse to look · E or Left Click to interact (hold it to force a bolt, a latch or the deadbolt shut) · F for your light · TAB for your phone · N for the rules · Esc to pause.
On the intercom, press 1 to 6 to choose a line, and Space or Click to skip dialogue.

Best played with headphones, at night, in fullscreen.

## Submission (done October 11, 2026)

- Game page: https://mrideedy.itch.io/dont-answer (itch.io project 5138524), Public.
- Jam entry: https://itch.io/jam/gamedevtv-halloween-jam-2026/rate/5138524
- Kind of project **HTML**, `Builds/DontAnswer-WebGL.zip` played in the browser, embedded at **1280 × 720** with itch.io's **Fullscreen button** on. The build uses its own page template (`Assets/WebGLTemplates/DontAnswer`, set by the WebGL builder): the game fills whatever frame it's given, so itch.io's fullscreen button fills the whole screen.
- Pricing **$0 or donate** (suggested $2). The jam has no pricing rules.
- Genre **Survival** (itch.io has no Horror genre). Tags: horror, psychological-horror, first-person, halloween, unity, short, atmospheric, singleplayer.
- AI disclosure: **Code, Sounds, Text & Dialog**.
- Cover `Captures/cover-630x500.png`; 5 screenshots from `Captures/` (itch.io's limit is 3 MB per screenshot, hence `leo-in-his-tuxedo.jpg`).
- To update the game after the deadline: bug fixes only, keep the original build up, and label updates clearly (jam rule). Upload a new zip on the project's edit page.

## Credits (the jam requires listing everything you didn't make)

| What | Source | License |
|---|---|---|
| Character models and animations (Marco, Ana, Officer Daniels, Tom, Eli, Dad, Mr. Voss, Mrs. Hale, Leo and the trick-or-treaters) | **Quaternius**, Ultimate Modular Men & Women, quaternius.com (via poly.pizza) | CC0 |
| All voice acting (261 lines) | Performed with **Chatterbox** text-to-speech (Resemble AI), cloning character voices designed with **Kokoro-82M** (hexgrad), then processed in `Tools/voices_chatterbox.py` | MIT (Chatterbox), Apache-2.0 (Kokoro) |
| Sound effects: doors, knocks, footsteps, glass, thunder, household | **rubberduck**, "100 CC0 SFX" and "100 CC0 SFX #2", opengameart.org | CC0 |
| Sound effects: creatures, breathing, screams, dogs, coughs | **rubberduck**, "80 CC0 creature SFX", opengameart.org | CC0 |
| Stingers and jump-scare hits | "Horror Hit Soundpack 1", opengameart.org | CC0 |
| Typewriter font | **Special Elite** by Astigmatic, Google Fonts | Apache-2.0 |
| Digital clock font | **VT323** by Peter Hull, Google Fonts | SIL OFL 1.1 |
| Music box, house tones, drones, the death stinger and placeholder sounds | Synthesized by `Tools/generate_audio.py`, `Tools/audio_v2_placeholders.py` and `Tools/make_death_flash.py` (which layers in a slice of a Horror Hit clip) | Own work + CC0 |
| House, props, lighting, effects and textures | Built in code by `SceneBuilder.cs` (procedural textures) | Own work |

## How the project is organised

| Path | What it is |
|---|---|
| `Assets/_Game/Data/Lines.csv` | Every spoken line: id, speaker, voice, human or mimic variant, text and acting note. |
| `Assets/_Game/Scripts/Story/StoryScript.cs` | The night itself: who visits and when, their questions, tells, consequences, phone messages, events, endings. |
| `Assets/_Game/Scripts/Story/NightPlan.cs` | Per-playthrough randomness: which visitors are real, which tells show, how the porch light behaves. |
| `Assets/_Game/Scripts/Core/GameDirector*.cs` | The night loop, visits, events, cinematics, deaths, checkpoints and endings. |
| `Assets/_Game/Scripts/Visitors/` | `VisitorAvatar` (behaviour), `RiggedBody` (animated characters), `Casting` (who plays whom), `VisitorBody` (a primitive body for any role without a model). |
| `Assets/_Game/Scripts/Editor/SceneBuilder.cs` | *Don't Answer → Build Game Scene* regenerates `Main.unity`: the house, lights, post-processing and every reference. Manual scene edits are lost on rebuild. |
| `Assets/_Game/Scripts/Editor/CharacterBuilder.cs` | Converts the Quaternius models into prefabs and builds `Data/Characters.asset`. The scene builder runs it for you. |
| `Assets/_Game/Resources/Voice`, `SFX`, `SFXFallback` | Voice lines, real sound effects (with `_1`, `_2`… variants picked at random), and synthesized fallbacks. |
| `Tools/` | `voices_chatterbox.py` (re-acts every line with emotion, see its header for the venv), `voices_kokoro.py` (the reference voices and a no-GPU fallback), `voice_fx.py` (door, intercom, phone and whisper effects), `install_sfx_packs.py` (re-installs the sound packs from `Tools/downloads`), and the audio synthesizers. |

### Debugging
On the **Game** object's `GameDirector`:
- *Skip Intro* and *Skip Prologue* jump straight into the night.
- *Debug Start Minute* starts the clock at a given minute after midnight (for example, 34 starts just before the trick-or-treaters).
- *Debug Seed* fixes the night's randomness, so a run can be repeated.

## Building for the web

The quick way: *Don't Answer → Build WebGL (itch.io)* does all of this and writes `Builds/DontAnswer-WebGL.zip` (about 8 minutes). By hand:

1. Open *File → Build Profiles*, select **Web**, and click **Switch Platform**.
2. In *Player Settings → Publishing Settings*, set **Compression Format** to **Gzip** and turn **Decompression Fallback** on (itch.io needs both).
3. Build to `Builds/WebGL`. Zip the *contents* of that folder and upload the zip to itch.io.
