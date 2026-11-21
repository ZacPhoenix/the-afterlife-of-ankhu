# Ankhu's spoken voice

When a spell succeeds, Ankhu says it aloud. Every line is pre-recorded with ElevenLabs and ships as audio in the game. Nothing calls ElevenLabs at runtime, so there is no latency, no per-play cost and no API key in a build.

## The voice

| Item | Value |
|---|---|
| Character | Ankhu, a priest in his thirties |
| Chosen candidate | `A_grave_baritone` (Zac, 2026-11-21) |
| Voice | Saved as "Ankhu" in Zac's ElevenLabs account. The ID, model and settings are in `voice_profile.json` |
| Model | `eleven_multilingual_v2`, pinned, with a fixed seed per take |
| Description used to design it | `VOICE_DESCRIPTIONS["A_grave_baritone"]` in `ankhu_voice.py` |
| Reference | `reference/ankhu_reference_preview.mp3`, the original preview Zac picked |
| Delivery | Dry and close. Room reverb belongs on the Unity mixer, per room |

If the account voice is ever lost, redesign it from the same description and compare it against the reference clip. If ElevenLabs retires the pinned model, regenerate the whole library in one pass (`batch --force`) so old and new takes match.

## How the library is built

1. `voice_lines.json` lists every phrase and word. Each phrase is recorded in four chain positions: `sole` ("Flow west."), `first` ("Flow west,"), `medial` ("and flow west,") and `final` ("and flow west."). Each has two takes, so a repeated spell never plays the same recording twice in a row. There is also a word library (`word.<w>.lead` and `word.<w>.final`) for phrases that have no recording yet.
2. `ankhu_voice.py batch` generates the takes it doesn't have yet. It uses ElevenLabs `previous_text` and `next_text` so each position gets the right intonation. Raw output lands in `out/` (git ignores it).
3. `process_voice.py out/raw` trims, high-passes, levels and fades every take, then writes `Assets/Ankhu/Audio/Voice/Clips/*.wav` and `voice_manifest.json`. Levels: active speech level -20 dB (K-weighted), true peak at most -1 dBTP. With `--previews DIR` it also renders stitched chains and measures the silence at each join.
4. Unity rebuilds `Assets/Ankhu/Generated/Resources/AnkhuSpellVoice.asset` automatically when the manifest changes. To rebuild by hand, use Ankhu > Rebuild Spell Voice Library.

## Adding words for a new room

1. Add the clause (or the word) to `voice_lines.json`.
2. Map the room's commands to phrase ids in its resolver (for Borrowed Dawn, `Assets/Ankhu/Bindings/BorrowedDawnSpellVoice.cs`). `EveryVoicedCommandHasALineInTheVoiceCatalog` fails if a command has no line.
3. On a machine that can reach api.elevenlabs.io (Zac's PC), run `py -3 tools/voice/ankhu_voice.py batch`. Only new or changed takes are billed.
4. `python3 -I tools/voice/process_voice.py tools/voice/out/raw`, then commit the clips and the manifest.
5. Optional check: `py -3 tools/voice/ankhu_voice.py asr Assets/Ankhu/Audio/Voice/Clips` runs every clip through ElevenLabs speech-to-text.

## The key

The scripts read `ELEVENLABS_API_KEY` from the environment, or on Windows from the user-level registry value of the same name. Set it once in PowerShell. Read-Host keeps the key out of shell history:

```powershell
$k = Read-Host "ElevenLabs key"; [Environment]::SetEnvironmentVariable("ELEVENLABS_API_KEY", $k, "User")
```

Never put the key in a file in this repo.

## Plan limits

`pcm_44100` needs the ElevenLabs Pro tier. On lower plans the script falls back to `pcm_24000`, and `process_voice.py` resamples to 44.1 kHz. Speech carries almost nothing above 12 kHz, so the loss is negligible.
