# Anpu concept round 4 (attendant spirits): generation report

I ran these on Zac's PC on 2026-11-20, from 20:05 to 20:07 local time. They went through Codex CLI and the gpt-image-bridge wrapper, logged in with ChatGPT, and no API key was used. All four ran in parallel and all four succeeded on the first attempt, with no retries.

The brief is in `../in/BRIEF_4.md`, which holds the exact prompts. The same prompts are in `prompts/p1.txt` to `p4.txt`, and `prompts/run.sh` is the runner.

Every image got the same references: out/emissaries_family.png, out2/obsidian_guardians_v2.png and in/ref_ankhu_sheet.png.

## Files

| File | Pixels | Bytes | Time |
|---|---|---|---|
| attendants_roster_front.png | 1536 x 1024 | 1,587,904 | 85 s |
| attendants_roster_side.png | 1536 x 1024 | 1,542,021 | 92 s |
| attendants_poses.png | 1536 x 1024 | 1,808,111 | 89 s |
| props_attendants.png | 1536 x 1024 | 1,470,102 | 56 s |

## Checked against the prompts (by eye, my reading)

All four images have a flat grey background with no text and no watermark.

**1. Roster front: good.**
- **What matches:** the six figures are in the right order, in A-pose on one baseline, with empty hands, collars, kilts above the knee and bare feet.
  - Sab has charcoal fur and a red neck band.
  - The crocodile Warden is blue-green, with an ochre kilt and leather wristguards.
  - The Ram Herald is red-orange, with tight curled horns and a red sash belt.
  - The Serpent Sentinel is jet-black, with a rigid hood that has a blue sheen.
  - Hraf-haf shows the back of his shaved head from the front.
  - The Obsidian Torch-Priest is glassy black, with white eyes and round human ears.
- **Miss:** the shared body is more muscular than "lean", with visible abs on most figures.
- **Miss:** Sab's ears are not much shorter than Anpu's.

**2. Roster side: half right.**
- **What matches:** all six are in order on one baseline. Five face LEFT. For Hraf-haf, the chest and toes point left and the face points right, so his backwards head reads correctly. I zoomed in to check.
- **Main miss:** the bodies of the first four figures are warm ochre-brown human skin, copied from the emissaries reference. They don't keep their front-view colours (charcoal, blue-green, red-orange and jet-black). Only Hraf-haf and the Obsidian Priest keep their skin.
- **Miss:** the bodies are muscular here too.

**3. Poses: good.**
- **What matches:**
  - (i) The crocodile Warden squats on a reed mat with two curved flint knives upright on his knees.
  - (ii) Sab kneels in the henu pose, with one fist to his chest and the other arm raised.
  - (iii) Two attendants stand at attention on either side of a plain stone doorway.
  - (iv) The Obsidian Priest holds a torch high, with a small soft gold flame.
- **Miss:** in (ii), Sab's red band is at the waist, not the neck, and the raised hand is open, not a fist.
- **Miss:** in (i), the kilt reads more like a loincloth.
- **Miss:** in (iii), the two attendants are the pale-sand emissaries from round 1 rather than roster figures. The prompt didn't say which ones, so this isn't wrong.

**4. Props: good.** It has, each in front and side view:
- the pair of curved flint knives
- a straight bronze knife
- an unlit linen torch
- a gold-over-black ankh
- a reed mat, shown from above and from the side

The grips are leather-wrapped with pale caps, and the shapes are solid.

## Fixes for a later round
- Side roster: regenerate with the front roster as the reference and the rule "same skin colour as the front view". I'd drop the emissaries reference for this one, since its brown skin leaks through.
- Leaner shared body: add "slim, no visible abs" and use the regal Anpu body as a reference instead.

## v2 redo (20:12 to 20:16 local, same machine and method)

Both images succeeded on the first attempt, with no retries. I ran them one after the other, because the side view needed the finished front v2 as its reference. The exact prompts are in `prompts/p5.txt` and `p6.txt`.

| File | Pixels | Bytes | Time | References |
|---|---|---|---|---|
| attendants_roster_front_v2.png | 1536 x 1024 | 1,471,823 | 94 s | attendants_roster_front.png only |
| attendants_roster_side_v2.png | 1536 x 1024 | 1,427,306 | 107 s | attendants_roster_front_v2.png, attendants_roster_front.png, in/ref_ankhu_sheet.png (no emissaries reference) |

**Front v2: good.**
- **What matches:** this time the one change was the body. All six are now slim with soft musculature and no visible abs. The heads, skin colours, kilts, collars, wristguards, red neck band and sash belt, order, scale and baseline all match v1.
- **Note:** the body is now very thin, close to wiry, with long thin arms. It may be thinner than "temple statue". For a middle ground, ask for "slender but with some shoulder width".

**Side v2: fixed.**
- **What matches:**
  - Every figure keeps its front-view colour from head to toe: charcoal, blue-green, red-orange, jet-black, blue-green and glassy obsidian.
  - No brown human skin is left.
  - All six bodies face LEFT, with chest and toes pointing left.
  - The five faces point left, and Hraf-haf's face points right, as asked.
  - The slim bodies match front v2.
  - Same order, scale and baseline, with the neck band, wristguard and sash belt all present.
- **No misses found by eye.**
