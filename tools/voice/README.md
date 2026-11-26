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

The saved account voice is the master. Voice Design gives a different voice on every run, so the description can't bring back the same voice. Don't delete "Ankhu" in ElevenLabs, and keep a free custom-voice slot on the plan. If it's lost anyway, the committed clips are the canon: design a replacement from the description, match it by ear against the reference clip, and regenerate everything. If ElevenLabs retires the pinned model, regenerate the whole library in one pass (`batch --force`) so old and new takes match.

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

## Names and pronunciation

Names ElevenLabs gets wrong are fixed with a pronunciation dictionary, not by respelling the line text, so subtitles and line files keep the real spelling.

1. Add a rule to `pronunciation.json`. Rules are case sensitive, so add one per spelling (`Ankhu`, `ANKHU`, `Ankhu's`). An `alias` rule (`{"type": "alias", "string_to_replace": "Ankhu", "alias": "..."}`) works on every model. A `phoneme` rule (`"phoneme": "ˈɑn.xu", "alphabet": "ipa"`) only works on `eleven_flash_v2`, `eleven_turbo_v2` and `eleven_monolingual_v1`.
2. Upload it and point the profiles at it: `py -3 tools/voice/ankhu_voice.py pron-dict --profile tools/voice/anpu_voice_profile.json --profile tools/voice/voice_profile.json`. This writes `pronunciation_dictionary_locators` into each profile, replacing any earlier version of the same dictionary. Every `batch` and `takes` request then sends it.
3. Re-record the lines that contain the name (`batch --only <key> --force`, or `takes` to audition seeds first). A changed dictionary changes the take hash, so `batch` also picks those up on its own.

To audition a rule set without touching the profiles: `takes ... --rules some_rules.json [--model eleven_flash_v2] --stem "{key}_s{seed}" --mp3`.

Ankhu's "kh" is a guttural /x/ (as in Bach), not a plain k (Zac, 2026-11-21). Zac picked "Ahn-khoo" on 2026-11-21 (take 4 of `C:\Users\zaclu\Downloads\AnkhuVoice\kh_audition_new`, eleven_multilingual_v2, seed 2). `pronunciation.json` now holds alias rules for `Ankhu`, `ANKHU`, `Ankhu's` and `ankhu`, uploaded as dictionary "Ankhu names" (jkUtFYXxMWcETpw5TQoE, version CndSfnesDXf7HEkQWiH8) and set in both `anpu_voice_profile.json` and `voice_profile.json`. The other rule sets tried are in `reference/kh_audition/`.

`pron-dict` also stores the strings each dictionary replaces (`replaces`) in the locator. The take hash only counts a dictionary when the take text contains one of those strings, so adding a name rule only marks the lines that say the name as stale. Scribe still writes the name as "Anku", which is expected for an alias it has never seen.

Ankhu's own spoken lines (not spells) live in `ankhu_dialogue_lines.json` and go to `Assets/Ankhu/Audio/Dialogue/Ankhu` (default 70 Hz high-pass), which SpellVoiceLibraryBuilder builds into `Resources/AnkhuDialogueVoice`.

## The key

The scripts read `ELEVENLABS_API_KEY` from the environment, or on Windows from the user-level registry value of the same name. Set it once in PowerShell. Read-Host keeps the key out of shell history:

```powershell
$k = Read-Host "ElevenLabs key"; [Environment]::SetEnvironmentVariable("ELEVENLABS_API_KEY", $k, "User")
```

Never put the key in a file in this repo.

## Plan limits

`pcm_44100` needs the ElevenLabs Pro tier. On lower plans the script falls back to `pcm_24000`, and `process_voice.py` resamples to 44.1 kHz. Speech carries almost nothing above 12 kHz, so the loss is negligible.

## Anpu (Anubis) dialogue

Anpu's lines use the same pipeline with a second voice profile and their own clip folder.

| Item | Value |
|---|---|
| Line list | `anpu_lines.json`, exported from `Ankhu.BorrowedDawn.TeachingRooms` plus "Rise, Ankhu." for the Spell 25 cinematic. `AnpuDialogueLinesTests` fails when the rooms' text and this file drift apart |
| Keys | `dialogue.anpu.<roomId>.intro.<n>`, `.nudge.<Outcome>` and `.solve`, as raised by `TeachingRoomDirector.AnpuLineStarted`, plus `dialogue.anpu.spell25.rise` |
| Voice profile | `anpu_voice_profile.json`. Since 2026-11-21 Anpu is HpaPE1DxbRg8OHTO1blt ("Anubis" in Zac's ElevenLabs account), eleven_multilingual_v2, speed 0.88. The earlier Voice Design voice (Q1, gQf5qGSlYoX99rlEEoSY) is kept in `reference/anpu_q1_voice_profile.json` |
| Clips | `Assets/Ankhu/Audio/Dialogue/Anpu/Clips`, library asset `Resources/AnpuDialogueVoice` |
| Playback | `AnpuVoiceBootstrap` adds a `DialogueVoicePlayer` to teaching-room scenes. The director holds each subtitle at least as long as its recording |

To re-record (for a Voice Design id, run `save-voice --generated-id <id> --name "Anpu" --profile tools/voice/anpu_voice_profile.json --speed 0.88` first. For a voice already in the account, edit `voice_id` in the profile):

```
py -3 tools/voice/ankhu_voice.py batch --lines tools/voice/anpu_lines.json --profile tools/voice/anpu_voice_profile.json --out tools/voice/out/raw_anpu
py -3 tools/voice/process_voice.py tools/voice/out/raw_anpu --assets Assets/Ankhu/Audio/Dialogue/Anpu --highpass 45 --gate-pauses 52
```

Why those flags: this voice's median pitch is about 63 Hz, so the default 70 Hz high-pass would cut his fundamental. 45 Hz is used instead. Raw takes carry a breath and room floor between words (mostly 120 Hz to 4 kHz, not low hum) at a median 35 dB under the speech. Zac wants a natural voice with no texture, so the pause gate pulls the gaps to at least 40 dB under (41 to 50 dB after processing). A low shelf does not help with this voice and is left off. Run `process_voice.py` without `-I`, since it needs numpy from the user site-packages.

Voice Design previews sometimes invent sound: a low boom under the voice, or hiss between words. The alternates (`_alt1`, `_alt2`) are especially prone to it. Before anyone auditions a preview, check that less than about -6 dB of its energy sits below 80 Hz, and that pauses sit at least 40 dB under the voice.
