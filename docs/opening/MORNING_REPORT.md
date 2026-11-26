# Morning report: the opening slice

You asked to wake up to a playable opening: the title menu, then the intro film, then a walk with Anpu, Ankhu and the attendants talking under fixed cameras that cut as the group walks out of shot. That flow is built and runs end to end.

Play it here: D:\TheAfterlifeofAnkhu\Builds\OpeningSlice\AfterlifeOfAnkhu.exe

Press Begin Journey on the title. The film plays, then you walk Ankhu up the causeway to the gate with the stick or WASD. Anpu walks beside you and the two attendants walk ahead as heralds. Six fixed cameras cut as you move, and holding forward keeps you walking the same way through a cut. F9 on the title opens Borrowed Dawn, which is no longer on the menu.

That build is build 51, made at about 12:10 UTC from the round 23 state, the best-scoring one. An autopilot capture of it (D:\TheAfterlifeofAnkhu\art_gen\flow\run39) played the whole flow from title to end card and back. The build holds the opening scenes only. No Realm_* scenes exist yet (D:\TheAfterlifeofAnkhu\Assets\Ankhu\Realms has only Art and Audio), so none are in it.

## Where the quality landed

The judges never reached 8. The best round, round 23, scored 6.72 on the two-judge mean, up from 5.78 in round 1. A same-day rescore showed the judges drift by about 0.7 between sittings. Rescored on the same morning, the round 12 build I thought was best fell from 6.44 to 5.75. So the climb is real but smaller than the raw numbers suggest.

Two judges (a cinematics director and a player-experience reviewer) scored every round on eight axes, out of 10. Every round's per-axis scores are in D:\TheAfterlifeofAnkhu\docs\opening\LOG.md and the judges' own files are in D:\TheAfterlifeofAnkhu\docs\opening\judges.

| Round | What changed | Judge A | Judge B | Mean |
|---|---|---|---|---|
| 1 | First full flow | 5.75 | 5.8 | 5.78 |
| 2 | Shadow catcher, Borrowed Dawn moved to F9 | 5.8 | 5.4 | 5.6 |
| 3 | Torch fill, lighter escorts, menu moved | 5.5 | 5.8 | 5.65 |
| 4 | Path end on the paving, amber rim light | 5.9 | 5.8 | 5.85 |
| 5 | Brown attendant skin | 5.75 | 5.9 | 5.83 |
| 6 | Night grade on the cast | 5.6 | 5.6 | 5.6 |
| 7 | Paint filter (rejected), formation | 5.4 | 5.6 | 5.5 |
| 8 | Anpu's gold trim repainted bronze | 5.7 | 5.94 | 5.82 |
| 9 | Later cut to the high shot | 5.9 | 6.1 | 6.0 |
| 10 | No canned speaking gesture, narrower lane | 6.3 | 6.25 | 6.28 |
| 11 | Cam4 aim, attendants further back | 6.5 | 5.9 | 6.2 |
| 12 | Escort feet locked to ground speed | 6.38 | 6.5 | 6.44 |
| 13 | Depth matte for the cam3 brazier | 6.19 | 6.5 | 6.35 |
| 14 | Flat dust, darker attendant skin | 6.69 | 6.0 | 6.34 |
| 15 | Sceptre foot planted on the road | 6.4 | 6.19 | 6.3 |
| 16 | Cam6 brazier matte, no hop, heralds ahead | 5.9 | 6.4 | 6.15 |
| 17 | Earlier cut to cam3 | 6.19 | 6.3 | 6.25 |
| 12 again | Round 12's frames rescored | 5.9 | 5.6 | 5.75 |
| 18 | Lens zoom on the gate shots | 6.4 | 6.6 | 6.5 |
| 19 | Painted character shader | 6.7 | 6.4 | 6.55 |
| 20 | Herald ramp, gate end marks | 6.25 | 6.6 | 6.43 |
| 19 again | Round 19's frames rescored | 6.25 | 6.69 | 6.47 |
| 21 | End marks follow Ankhu, end card | 6.1 | 6.3 | 6.2 |
| 22 | Attendants by the sphinxes, end push-in | 6.5 | 6.56 | 6.53 |
| 23 | Opening-only Ankhu skin, warm brown instead of pink | 6.69 | 6.75 | 6.72 |

The axes that held the score down all night were continuity (film to 3D, 5 to 6), motion (5 to 6) and grounding (4 to 6, reaching 6 from both judges only in round 23). Art and the title UI sit at 7.5 to 8.

## What is capping the score

Every round, both judges put the same two things first, and neither can be fixed from code.

**1. The 3D cast does not match the painted film.** The film has painted figures with gold-and-blue costumes, glossy-eyed attendants and a different Ankhu body. The walk uses the 3D models, which read as game models pasted over the paintings. Lighting, shadows, depth mattes, skin regrades, a bronze Anpu texture, a paint filter and a painted character shader all helped a little. None of them closes the gap.
Recommendation: commission one character pass that makes the 3D models the source of truth. Repaint the 3D albedo and costumes toward the film's design (attendant collars and kilt bands, Anpu's corselet), and reroll the film shots that drift from the models. Better still, render the film's character shots from the 3D models and paint over them, so both sides share one cast.

**2. The walk animation is stiff.** All four figures share one walk clip. Even with feet locked to ground speed and per-character phase, the judges read gliding, scissoring legs and no secondary motion.
Recommendation: get a proper walk set: a solemn processional walk for Anpu that holds the sceptre, a separate attendant walk, starts and stops, and a turn. Motion capture or a hand-keyed set would lift motion, grounding and continuity together.

**3. The gold trim in the film.** The judges keep asking for the film's bright gold on Anpu's props and trim. Your rule was aged dark bronze, never bright gold. I followed it and did not follow the judges. The props are bronze in the 3D walk and in the rerolled film shots, and Anpu's 3D gold trim was repainted bronze for the opening. Some film frames still show bright gold costume trim (not props), which is the continuity conflict.
Recommendation: decide whether costume trim (collar, kilt bands) may stay gold while props stay bronze. If not, the film shots with gold trim need a reroll.

## For you to review

**The walk dialogue.** The voiced lines from earlier are unchanged, and nothing was re-voiced. Anpu has five lines and Ankhu four, so Anpu's last line at the gate has no reply. Both judges noticed.

| Shot | Ankhu | Anpu |
|---|---|---|
| cam2 | I served your house for forty years. I wore your face over the dead. | I know. I was there each time you lifted the mask. |
| cam3 | Then you know what I was before that. | A son. A father. A husband. A man who stopped speaking. |
| cam4 | Will I see them? | The Duat keeps what the living lose. But the road asks something first. |
| cam5 | What does it ask? | Your name, at every gate. Hold it as you held it in the water. |
| cam6 | (none) | Walk with me, Ankhu. The night is long, and the gates are many. |

Please check Anpu's cam3 line, "A son. A father. A husband. A man who stopped speaking." It was flagged for your review because it states Ankhu's backstory outright.

**Names.** Only canon names appear: Heru, Auset, Ausar, Het-Heru, Djehuty, Anpu, Ankhu's son Hori and his wife Neferet.

**Props.** The ankh is in Anpu's left hand (about 0.32 m) and the was-sceptre in his right (about 1.91 m measured, vertical, Set head at ear height, forked foot planted on the road). Both are aged dark bronze. The judges repeatedly asked for the film's bright gold, and I did not follow them, per your rule.

**Sandals.** Ankhu wears sandals in every walk shot.

## What changed overnight

- **Flow.** Title menu, then the film, then the playable walk, then a fade at the gate. Borrowed Dawn moved off the menu to F9.
- **Fixed cameras.** Six painted shots, each with its own painting made from that exact camera. A shot cuts when the group crosses a set distance along the road. Holding forward keeps Ankhu walking the same way through a cut.
- **Grounding.** An invisible shadow catcher on the road takes the moon shadow. Depth mattes (cut-outs of the painting on cards) let the cast walk behind the painted braziers in cam3 and cam6. Footfall dust lies flat on the road. An earlier version read as floating white orbs and was fixed.
- **The cast.** Anpu got an opening-only body texture with his gold trim repainted aged bronze. The attendant skin was repainted warm brown, then darkened toward the film's near-black. Ankhu got his own opening-only texture with the skin deepened from pink to warm brown and the linen left white, which lifted grounding to 6 from both judges. A night grade sits on all character textures. The sceptre's foot is planted on the road as he walks.
- **Motion.** The escorts' feet are locked to ground speed, so they no longer skate, and each has its own walk phase so they don't step like clones. Ankhu's canned speaking gesture was removed. A hop at the start of the walk was fixed.
- **Staging.** The attendants walk behind through the cam2 look-back, then move ahead as heralds by cam4. At the gate the attendants stop by the sphinx bases on either side, keeping a clear gap from wherever Ankhu actually stands, and Anpu stops beside him at the threshold. Earlier versions parked an attendant in front of Ankhu or on top of a brazier, and both were fixed.
- **Ending.** At the gate the camera pushes in slowly, the letterbox closes in, and an end card reads "The First Gate" over "The Afterlife of Ankhu" before the fade back to the title.
- **Cameras.** The cut to cam3 comes earlier, so Ankhu never gets too close to the cam2 lens. Cam5 and cam6 zoom 1.35x and 1.25x into their paintings, so the group reads at twice the size at the gate.
- **Painted character shader.** The cast is shaded like the paintings: soft banded light, a flat ambient taken from the painting, no shine, a thin moonlit edge, and white linen that rolls off to cream instead of clipping.

## Tried and dropped

- A Kuwahara paint filter over the walk shots (round 7). It smeared the paintings and scored lower, so it is off. The shader is still in the project, unused.
- A tilt on cam4 that turned out to hit the wrong camera. Caught in the frames, reverted and never judged.
- An early version of the herald ramp turned the cast salmon pink. I caught it in the frames before judging and fixed it.
- Two tries at Anpu's black legs (round 24): more lift and rim in the shader, then a charcoal floor in his texture. Neither changed the frames enough to beat judge noise, so both were reverted unjudged and round 23 is what ships.
- One judge's round 13 result hit an image limit and skipped the film. It was rerun, and the invalid file is kept as judge_round13_A_invalid.json.

## Known limits

- The film-to-3D cast mismatch and the shared walk clip, described above.
- Subtitles sometimes run across a cut onto the next shot, and a few shots hold a line over an empty frame (024-025).
- In the M2 hieroglyph write-on, the pen tip runs ahead of the ink in places.
- Anpu's face touches the letterbox in some film frames (014-017). The film was not reworked tonight.

## What was run and what was not

- Every round was built as a Windows player and played by an autopilot capture (49 to 51 frames per run), and the judges scored those frames.
- The judges never heard the audio. They scored it from the scripts and the subtitles.
- Gamepad navigation of the menu is untested. Keyboard and the autopilot were the only inputs used.
- I did not play the build by hand. The autopilot walks the full road at a steady pace, so stopping, turning back and standing still in a shot were not checked tonight.
- The work is committed locally on feat/ankhu-player-model but is not on GitHub. The push to claude/project-thread-mmvgrd was refused because D:\TheAfterlifeofAnkhu\Assets\Ankhu\Opening\Video\Opening.mp4 (301 MB) is in the history, over GitHub's 100 MB limit. Moving it to Git LFS or out of the history needs your decision, so I left it.
