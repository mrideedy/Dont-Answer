# Assets: status

Nothing more is needed from you. Everything in the game is either our own work or a free asset with a license that allows it in a jam game. The full list is in `JAM_SUBMISSION.md` under *Credits*.

| Area | Status |
|---|---|
| Characters | 9 rigged Quaternius models (CC0) with shared walk, idle, knock, death and other clips. Leo's bedsheet ghost is built from primitives on purpose. |
| Basement creature | Mutant model from your own library, posed and lit in `SceneBuilder.BuildMutant`. |
| Voices | All 228 lines are acted by Chatterbox (Resemble AI, MIT) on your GPU, in character voices designed with Kokoro (Apache-2.0). Each line's acting note sets its emotion and pacing, punctuation gets real pauses, and then it's treated for how you hear it: through the door, over the intercom, on the phone, on TV, as a whisper or as a mimic. The Kokoro versions are kept in `Tools/voice_backup_kokoro/`. |
| Sound effects | About 70 real recordings from four CC0 OpenGameArt packs, with variants that play at random. The synthesized fallbacks cover anything else. |
| Fonts | Special Elite (story text) and VT323 (clock and phone), both from Google Fonts. |

## Optional upgrades, if there's time after the jam

- **Better voice acting.** `Tools/elevenlabs/generate.py` can regenerate every line with ElevenLabs. Set your own `ELEVENLABS_API_KEY` as an environment variable (never paste it into chat), fill in `Tools/elevenlabs/voices.json`, and run `python Tools/elevenlabs/generate.py voices`. The files land in `Assets/_Game/Resources/Voice/` with the same IDs, so nothing else changes.
- **Re-generating the voices.** `C:/Users/<you>/dont-answer-tts/venv/Scripts/python.exe Tools/voices_chatterbox.py --force` (or `--only ID1,ID2` for a few lines). The reference voices come from `python Tools/voices_kokoro.py --refs`.
