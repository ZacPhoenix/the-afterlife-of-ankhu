# Anpu concept round 3 (regal, from direction A): generation report

I ran these on Zac's PC on 2026-11-20, from 20:02 to 20:05 local time. They went through Codex CLI and the gpt-image-bridge wrapper, logged in with ChatGPT, and no API key was used. Images 1 to 3 ran in parallel. Image 4 started after image 1 and used it as its first reference. All four succeeded on the first attempt, with no retries.

The brief is in `../in/BRIEF_3.md`. The exact prompts sent are in `prompts/p1.txt` to `p4.txt`, and `prompts/run.sh` is the runner.

## Files

| File | Pixels | Bytes | Time |
|---|---|---|---|
| anpu_regal_turnaround.png | 1536 x 1024 | 1,698,746 | 91 s |
| anpu_regal_judgment.png | 1536 x 1024 | 1,791,623 | 93 s |
| anpu_regal_details.png | 1536 x 1024 | 2,192,629 | 84 s |
| anpu_regal_meshy_front.png | 1024 x 1536 | 1,531,114 | 85 s |

The wrapper copies each PNG straight from Codex's output. Nothing was re-encoded.

These are the references each image got:
- Images 1 to 3: out2/anpu_final_turnaround.png, out/anpu_A_divine_booth.png, in/ref_ankhu_sheet.png.
- Image 4: out3/anpu_regal_turnaround.png first, then the same three.

## Checked against the prompts (by eye, my reading)

All four images have a flat grey background, no text and no watermark. Every figure has empty hands and no capes, chains or anything dangling. All ornament reads as rigid relief.

**1. Regal turnaround: good.**
- **What matches:** it has four views on one baseline. The side and three-quarter views face LEFT, the first time a round got this right. It also has:
  - a diadem with a gold disc at the brow and no uraeus
  - gilded inner ears
  - falcon-head collar terminals
  - a feathered corselet from chest to waist
  - a jackal-on-shrine pectoral
  - inlaid armlets and wrist cuffs
  - a chevron belt and an inlaid front apron
  - a gold-hemmed kilt with a gap to the thighs
  - anklets and gold-tipped claws
- **Miss:** the eyes are amber with gold rims, not white calcite with black pupils.
- **Miss:** the wig lappets still reach the collar in the front views, with no gap of black neck.
- **Miss:** the wig bands are plain gold, with no visible lapis inlay.

**2. Judgment: good, the most decorated.**
- **What matches:** the wig has a full gold-and-lapis lattice, and there's a gold disc on the diadem. The multi-row collar reaches the shoulders. There's a winged pectoral across the corselet and gold sheath bands on the forearms. Side views face left. The figure is still slender.
- **Miss:** the eyes are amber again.
- **Miss:** the lappets touch the collar.
- **Miss:** the "longer royal kilt" is the same length as in image 1. That's still within "above the knee", so it's not wrong, but it doesn't read as a different kilt.

**3. Details sheet: good.** It has all five studies: diadem and wig in profile, pectoral, collar with falcon terminals, an armlet and a cuff, and the belt with the apron. They're clean and isolated with no text. Here the eye IS white with a dark pupil, which can be the reference for the eye fix.

**4. Meshy front: good likeness, wrong margin.** It's the same character as image 1 in every detail.
- **Miss:** the figure fills about 95 percent of the height, not 85, the same miss as round 2. Ear tips and soles are close to the edges.
- **Miss:** the eyes are amber.

## Fixes for a later round
- Eyes: paint white calcite eyes in post, or use the details sheet's head study as a reference for the eyes.
- Meshy margin: pad the canvas in post (add grey around it), which needs no regeneration.
- Wig gap: this has failed three rounds by prompting. Fix it on the 3D model instead.
