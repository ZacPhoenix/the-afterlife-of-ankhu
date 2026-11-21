# Anpu concept round 2: generation report

I ran these on Zac's PC on 2026-11-20 from 19:27 local time, through Codex CLI 0.157.1 logged in with ChatGPT. No API key was used. I generated each image once, and all five succeeded on the first attempt, so no retries were needed. Images 1, 3, 4 and 5 ran in parallel. Image 2 started after image 1 finished and used image 1 as an extra reference.

## Files

| File | Pixels | Bytes | Time |
|---|---|---|---|
| anpu_final_turnaround.png | 1536 x 1024 | 1,541,640 | 80 s |
| anpu_meshy_front.png | 1024 x 1536 | 1,412,881 | 68 s |
| anpu_head_expressions.png | 1536 x 1024 | 1,627,231 | 92 s |
| props_was_sceptre_v2.png | 1536 x 1024 | 1,293,529 | 62 s |
| obsidian_guardians_v2.png | 1536 x 1024 | 1,538,932 | 100 s |

The gpt-image-bridge wrapper copies each PNG straight from Codex's output with `cp`. Nothing was re-encoded. The Codex session logs show the agent model as `gpt-6-astra` at reasoning effort `ultra`. Codex does not report its image backend, so I can't name the image model.

## Commands

`prompts/run.sh <n> <file> <refs...>` calls the following once, and again only if the first attempt fails:

```
~/.claude/skills/gpt-image-bridge/bin/gpt-image-2 "$(cat p<n>.txt)" D:/TheAfterlifeofAnkhu/art_gen/anpu/out2/<file> --size <1536x1024 | 1024x1536 for image 2> --ref <each ref>
```

The wrapper then runs `codex exec --json --skip-git-repo-check -s workspace-write -C <private temp dir> "<wrapped prompt>" --image=<ref>...`.

These are the references each image got:
- Images 1 and 3: out/anpu_A_divine_booth.png, out/anpu_B_upon_his_mountain.png, in/ref_ankhu_sheet.png
- Image 2: the same three, plus out2/anpu_final_turnaround.png
- Image 4: in/ref_ankhu_sheet.png
- Image 5: out/obsidian_guardians.png, in/ref_ankhu_sheet.png

The exact prompt texts are in `prompts/p1.txt` to `p5.txt`. Each prompt has the same parts:
1. A line saying what each reference is for.
2. The "Anpu final design" block from BRIEF_2 (images 1 to 3 only).
3. The image's own text from BRIEF_2.
4. The BRIEF.md suffix (STYLE, LAYOUT, POSE, CONSTRUCTION, BANNED), appended verbatim.

Some images also got a one-line override after the suffix, where BRIEF_2 overrides it:
- Image 2: one figure only, at 85 percent of the height.
- Image 3: head studies instead of full body, and only study (a) has its mouth closed.
- Image 4: no pose, since no characters appear, and the crook is requested even though BANNED lists it.

Image 5's prompt also restates the round-1 costume descriptions so the three figures stay the same.

## Checked against BRIEF_2 (by eye, my reading)

All five images have a flat grey background, no text and no watermark, and every figure has empty hands.

**1. Final turnaround: good, with one main miss.** It has four views (front, left side facing left, back, three-quarter facing left) on one baseline. The ears are gilded, with gold eye rims and gold brows. The wig is rigid with gold bands. The body is lean with no abs. It also has the collar, both armbands, the wristbands and silver claws. The kilt has a front panel, a white belt and a gold buckle, and it flares above the knee with a gap. The feet are bare.
- **Miss:** the wig is still not short enough. Only the back view shows bare neck between the wig and the collar. In the front and side views the lappets and back panel reach down to the collar, with no visible gap.
- **Miss:** the eyes read as amber or gold irises, not white calcite with black pupils.

**2. Meshy front: good.** This is the same character as image 1, with the same misses.
- **Miss:** the wig lappets touch the collar.
- **Miss:** the eyes are amber, not white calcite.
- **Miss:** the figure fills about 95 percent of the height, not 85. The top and bottom margins are thin, with the ear tips and soles close to the edges.

**3. Head expressions: mostly good.** It has four left profiles of the same size. (a) is neutral. (b) has the jaw open with a dark interior and tongue and no teeth. (c) has the ears folded flat back over the wig. The gilded ears, gold rims and brows, and the wig ending above the collar all match. The eyes here are white with dark pupils.
- **Miss:** (d) "alert" barely differs from (a). The ears are slightly taller, but they are not pricked forward.

**4. Was-sceptre v2: partial.** The shaft is black with gold bands and has a gold two-pronged fork. The smaller crook is in the same materials.
- **Miss:** the side-view head reads more like an ibis (Thoth) beak than the Set animal. The snout is long and curved, but it looks beak-like.
- **Miss:** the head sits on top of the shaft, not angled like a hooked handle.
- **Miss:** in the front view the ears are tall paired slats, not short and squared.
- **Miss:** the crook is shown in only one view.

**5. Obsidian guardians v2: improved material, but new misses.** The obsidian is now clearly glassy, with sharp speculars along the limbs and skulls. The bodies are leaner and calmer, with no abs. The costumes match round 1: the Priest's collar, the Guardian's solid gold nemes and gold kilt, and the Lector's sash and blue brow band.
- **Miss:** the side views face RIGHT, not left. This repeats the round-1 miss.
- **Miss:** all six figures have pointed, elf-like ears. The brief says human faces with no jackal features.
- **Miss:** the eyes are amber or gold, not white calcite with black pupils and gold rims.
- **Miss:** the deep-blue glints are barely visible.

Possible fixes for round 3:
- Ask for a still shorter wig, described as "wig hem at jaw level".
- Mirror image 5 horizontally in post rather than re-prompting. Its views are symmetric, so the flip would be clean.
- Name the Set animal with "not a bird, not a beak" for the sceptre.
- Add "round human ears" to the guardians prompt.
