# Voice work: paused 2026-11-21

## Done
- Ankhu's spell voice is live in Borrowed Dawn. The voice is A_grave_baritone, saved in ElevenLabs as "Ankhu" (prbpIeiA8upYT2mdCWEz). There are 163 takes in Assets/Ankhu/Audio/Voice (commit 726a2ed), and 94 of 94 EditMode tests pass.
- Pipeline and how to add words: tools/voice/README.md.
- Anubis voice round 2: four designs with unusual accents (Q1 rolling ancient, Q2 Nile south, Q3 liturgical, Q4 stone whisper). The six clean previews are in C:\Users\zaclu\Downloads\AnkhuVoice\anubis_round2, and their ids and checks are in tools/voice/reference/anpu_round2_*.json. Suggested first listens: Q2_nile_south and Q4_stone_whisper.
- Anpu's voice wiring is in place. anpu_lines.json holds 25 lines exported from TeachingRooms plus the Spell 25 line. AnpuVoiceBootstrap plays them on AnpuLineStarted, and each subtitle holds for the length of its recording. It stays silent until the clips exist.

## Next
1. Done 2026-11-21: Zac picked HpaPE1DxbRg8OHTO1blt ("Anubis") as Anpu's voice, replacing Q1_rolling_ancient (profile kept in tools/voice/reference/anpu_q1_voice_profile.json).
2. Done 2026-11-21: all 25 Anpu lines re-recorded with the new voice and processed with `--highpass 45 --gate-pauses 52` (median pitch about 63 Hz, gaps pulled from about 35 dB to at least 40 dB under the speech). Scribe heard every line correctly with no audio events. Resources/AnpuDialogueVoice rebuilds when Unity imports the clips.
2a. Done 2026-11-21: Zac picked "Ahn-khoo" (alias dictionary jkUtFYXxMWcETpw5TQoE). dialogue.anpu.spell25.rise is his picked take spliced to a 1.0 s pause after "Rise". teach-1-cistern.intro.1 re-recorded with the dictionary. Ankhu's cinematic line (dialogue.ankhu.spell25.anpu) is now "My Lord... Anpu." (Zac, 2026-11-21: Ankhu at 1:26, Anpu's Rise at 1:32). Zac picked seed 5 of C:\Users\zaclu\Downloads\AnkhuVoice\ankhu_mylord_takes as heard, no splice (2.31 s, in Assets/Ankhu/Audio/Dialogue/Ankhu). A 0.8 s pause version is kept outside the game as mylord_s5_spliced08.wav in that folder. Nothing plays it yet.
2b. The Rise subtitle text is "Rise, Ankhu." (was "Rise.... Ankhu...."). The recording is intentionally kept: it is still Zac's spliced pick with the 1.0 s pause. Its raw meta in out/raw_anpu was re-stamped with the new text's hash, so `batch` skips it. Do not run `batch --only dialogue.anpu.spell25.rise --force`, which would replace the splice with a plain take.
3. Zac's ear check of Ankhu's "wake, bearer" and "bind ..." takes. Speech-to-text often heard "better" and "find". Regenerate any he finds unclear with `batch --only <key> --force`.
4. Optional: add the rumble and noise-floor check to the candidates step permanently.

## Rules
- No push approvals asked of Zac. Work goes straight into this local project.
- The ElevenLabs key lives only in Zac's user environment variable ELEVENLABS_API_KEY.
