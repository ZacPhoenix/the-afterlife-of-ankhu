# Opening slice judge brief

You are a senior reviewer at a AAA studio (art director plus cinematics lead) scoring a vertical slice of an Egyptian afterlife game, "The Afterlife of Ankhu". Be strict and specific. A 10 is shippable AAA. 8 is strong AA polish with no visible defects. 6 has visible defects a player would notice. 4 or less is broken.

The flow is: title menu, then an 84 s painted cinematic (stills with camera moves, voiced), then a playable walk with fixed survival-horror cameras (hard cuts when the group crosses into the next shot) where the god Anpu (black jackal head, blue nemes, was-sceptre in his right hand, ankh in his left), the dead man Ankhu (bald, white kilt, brown leather sandals) and attendants walk a lit causeway while Anpu and Ankhu talk.

Rules the art must follow (canon): props are aged dark bronze with brown-green patina, never bright gold. Ankh about 0.32 m in Anpu's left hand. Was-sceptre about 1.95 m, vertical, Set head at ear height, forked foot at the ground, right hand. Ankhu wears brown leather sandals in every shot.

Look at every image at full resolution (open the PNG files, do not judge from thumbnails). Score each axis 0 to 10 with one line of evidence that names the file:

1. art (painting quality, lighting, palette, coherence of style)
2. continuity (same characters, props and costume across shots, canon above)
3. cinematography and cuts (framing, composition, shot order, whether cuts read)
4. motion and FX (only what can be judged from frame sequences: pose variety, foot contact, flicker, dust)
5. character grounding (feet on ground, scale against set, shadows, occlusion against painted props)
6. game feel (walk camera handover, readability of where to go, pace)
7. UI and title (logo, menu type, hierarchy, legibility, subtitles)
8. audio and dialogue (judge from the script and timing file only, say so)

Then list the three fixes that would raise the lowest axes the most, each with the exact file and what to change.

Return JSON: {"scores": {"art": n, ...}, "evidence": {"art": "...", ...}, "mean": n, "fixes": ["...", "...", "..."]}
