# Opening slice: overnight plan (2026-11-21, Zac asleep until about 14:00 UTC)

Zac's words: "I want to wake up to the introduction menu in the game, to then the intro cinematic, to then a walking sequence (playable) of Anpu, Ankhu and the attendants walking and Anpu and Ankhu talking together. Do it Resident Evil style so the cameras are all fixed and when they reach the edge and walk off, the camera moves to the next shot and continues." "Hill climb all night on quality, polish this over and over until it feels like a AAA studio cinematic and video game." "Use judges to score and keep going higher and higher, aim for 8/10."

Order: finish the consistency pass first (art_gen\cinematic\consistency). Overnight, keep Zac's pick when it passes the judge, otherwise use the judge's best reroll, and log each substitution.

## Flow
TitleMenu -> Begin Journey -> Opening (Spell 25 cinematic 0:00 to 1:24.5) -> on the DUAT drum hit at 1:24.5 the 2.39 letterbox bars retract -> Walk_Duat (playable) -> slow fade -> back to the title.
Continue loads Walk_Duat when a save key exists. Settings: master and music volume, subtitles, reduced motion, plus an entry to the Borrowed Dawn greybox. Hold any key or button for 1 s to skip the cinematic. New scenes go first in Build Settings, the existing scenes stay after them. Windows build at D:\TheAfterlifeofAnkhu\Builds\OpeningSlice\ each time a phase lands.

## Phases (each ends with a judge score, a LOG.md entry and a LOCAL git commit, never pushed until the end-of-night backup rule below)
- P1 Scaffold: scenes, flow, settings, skip, save key. Reuse the Ankhu, Anpu and Guardian prefabs, DialogueVoicePlayer and subtitles, PlayModeRecompileGuard. Unity 6000.6.3f1, driven in batch mode through editor builder scripts so the scenes are reproducible.
- P2 Title menu: separate layers from options\out\TITLE_night_optC.png (sky and landscape through the portal, interior and columns, braziers, statues, Ankhu in the doorway). Subtle parallax on mouse or stick, brazier flicker with matched light, dust in the portal light. Gold logo from C:\Users\zaclu\Downloads\Gold The Afterlife of Ankhu Logo.png with the winged sun disc kept slightly visible. Cinzel and Cormorant Garamond, gold hover, keyboard and controller navigation, quiet ambient loop.
- P3 Cinematic: every picked shot as layers (bg plate, keyed figure RGBA matched to the painting, optional mid haze) with procedural FX, as Act 1 v2. Timing from the script doc's final timeline (memories one bar of 3.2 s from 0:14.1, multi-take shots split their slot in Zac's order, E1 0:49.5, E4 signs 0:55.5 to 0:58.9, Anpu emerges 1:02.25, bow 1:08.5, Ankhu line 1:09.3, ankh 1:11.7, "Rise, Ankhu." 1:13.0, rise 1:16, walk 1:18.1, DUAT card and handoff 1:24.5). Synced to the DSP clock. Ripple dissolves between memories, subtitles, slow filmic motion.
  Implementation choice: the painted cinematic is rendered offline by the Python layer renderer (the Act 1 v2 path) to a lossless-quality video and played in Unity with the music on the DSP clock, so the paint keeps its quality. Subtitles and the letterbox handoff run in Unity.
- P4 Walk_Duat: 5 to 7 fixed cameras from the temple exit, past the pool, down the lamp-lit road into the hills. Per camera: greybox, render from that camera, Codex paints over the render so the paint's perspective matches the 3D ground. Invisible colliders and walkable path. Ankhu is the player (camera-relative controls held through a cut until the stick is released). Anpu and the two attendants escort, match pace, wait if he lags, keep the bible formation. Edge triggers cut cameras. Contact shadows and rim light. Dialogue zones with voices from tools\voice (key from the env var only), lines marked DRAFT for Zac's review:
  - Cam1 temple exit: silence, footsteps.
  - Cam2: ANKHU "I served your house for forty years. I wore your face over the dead." ANPU "I know. I was there each time you lifted the mask."
  - Cam3: ANKHU "Then you know what I was before that." ANPU "A son. A father. A husband. A man who stopped speaking."
  - Cam4: ANKHU "Will I see them?" ANPU "The Duat keeps what the living lose. But the road asks something first."
  - Cam5: ANKHU "What does it ask?" ANPU "Your name, at every gate. Hold it as you held it in the water."
  - Cam6 vista: ANPU "Walk with me, Ankhu. The night is long, and the gates are many." Then a slow fade back to the title.
- P5 Hill climb until about 13:30 UTC: stills at key moments plus a full-flow MP4. Two fresh independent judges score 1 to 10 on: (1) art quality and painterly consistency, (2) continuity against the bible, (3) cinematography and cuts on the music, (4) motion and FX, (5) character grounding in the walk, (6) game feel, (7) UI and title, (8) audio and dialogue sync and mix. Overall is the mean, no axis below 6, target 8. Fix the lowest first, keep a round only if it scores higher.

## Rules
- Before every Unity launch check D:\TheAfterlifeofAnkhu\Temp\UnityLockfile. If Unity is open elsewhere, wait and retry. Never recompile in Play mode.
- Work-in-progress scripts stay outside Assets (in D:\TheAfterlifeofAnkhu\_staging\opening) until they pass tools\compile-check, then move in.
- If the coordinator asks to release Unity for the Borrowed Dawn art thread, close Unity at the next checkpoint and do non-Unity work.
- Keys only in env vars. Absolute paths in reports. Honest about what was run.
- Progress note at each phase end and about hourly.
- About 13:30 UTC: write MORNING_REPORT.md here. End of night: push main to origin only if it needs no typed approval, otherwise skip and say so.

## Hill-climb backlog (added 08:05 UTC from coordinator notes)
1. M2 glyphs: blank papyrus in bg, draw signs as own layer upright from the writer's seat, ink brush texture, write on stroke by stroke. Judge question: "Are the signs upright from the writer's viewpoint?"
2. S15 r2c: Ankhu barefoot, bible says brown leather sandals. Repaint feet or reroll.
3. S16 at 6/10 (handoff shot): reroll toward bible and side-on game plane, aim 8+. HIGH priority.
4. S12/S14 linen panel behind Anpu's legs (check vs Anpu ref), M10 age echoes (one man across time, low-opacity ghosts or slow dissolve), S13 ankh size (0.32 m, forearm length), re-judge M12 full size.
5. Judges score at full resolution from now on.
6. Ankhu wears brown leather sandals in EVERY shot (Zac's decision, bible updated 08:10 UTC). Judge feet in every Ankhu shot, fix barefoot ones (S15 r2c known, S16 r1c judged barefoot) in figure layer or reroll.
7. Walk dialogue cam3 Anpu: "A son. A father. A husband. A man who stopped speaking." Re-voice. List in MORNING_REPORT under dialogue for Zac's review. No reunion promise beyond "The Duat keeps what the living lose".
