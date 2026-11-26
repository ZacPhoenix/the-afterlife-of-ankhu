# Realms voices (2026-11-21, machine session)

Model eleven_multilingual_v2, "Ankhu names" pronunciation dictionary, profiles in `profiles/`. Account voices picked by audition (`audition_new_speakers.py`, one line per candidate, median pitch and ASR check; clips in `tools/voice/out/realms_audition`). Not yet heard by Zac.

| Speaker | Voice id | Account voice | Why |
|---|---|---|---|
| anpu | HpaPE1DxbRg8OHTO1blt | Anubis | existing `anpu_voice_profile.json` |
| ankhu | prbpIeiA8upYT2mdCWEz | Ankhu | existing `voice_profile.json` |
| announcer | O4uxPTtvgxrA0QCLHI1J | Arkadius - Old Storyteller | lowest, slowest old male (F0 about 79 Hz). Light Scottish colour |
| guardian | fbIG6gEosVIM95R5qOna | W. Clint Oxley | intimate raspy baritone, close and dry |
| keeper | wzVFxVZCBIi6KBxNhQDI | Jonas - deep and cool | deepest candidate (F0 about 66 Hz), speed 0.8 |
| hori | XJ2fW4ybq7HouelYYGcL | Cherry Twinkle | child-pitched (about 414 Hz). A cartoon girl voice standing in for a four-year-old boy |
| neferet | EXAVITQu4vr4xnSDxMaL | Sarah | warm young woman |
| father | iP95p4xoKVk53GoZ742B | Chris - Charming, Down-to-Earth | warm natural male, distinct from Ankhu |

Known pronunciation gaps (no dictionary rule yet): "Wernes" comes out close to "fairness" (anpu.field.10), "Khetem" (ankhu.house.5) and "Heru" (hori.field.1) are approximate.

## Shabti Row (2026-11-21, machine session)

33 new lines from commit ffa45f3 (row dialogue, 17 sluice/shabti/plot spells, mother, midwife). Line files, profiles and rules are in `D:\TheAfterlifeofAnkhu\_staging\voice_row` (not copied over the tracked files here).

| Speaker | Voice id | Account voice | Why |
|---|---|---|---|
| mother | tbfu7H6JPPlpF9clW89V | Emma - Fierce, Intense & Eloquent | strong and bright (F0 about 338 Hz on the audition line), distinct from Neferet. Light Latin American colour |
| other (midwife) | aAsWcN5jdLdiYG7Hq0YL | Harriet - Mature British Actress | the only older female voice on the account |

New pronunciation dictionary "Ankhu names" fLDHn3gRtRFOvghOIENR version KU28f1C4uh829GTF8Io6 adds Wernes = Wer-ness, Khetem = Kheh-tem, Heru = Heh-roo, shabti = shab-tee. Only the row profiles use it so far. The earlier lines with Wernes, Khetem and Heru were not re-recorded. Speech-to-text still hears "shabti" inconsistently (Shepty, "chapter" on spell.wake.shabti1).
