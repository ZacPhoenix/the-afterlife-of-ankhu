# Painted Room Spec (binding)

Status: binding for every agent and every thread that builds, changes or dresses a puzzle room in The Afterlife of Ankhu. Written 2026-11-24 from Zac's direction and from what actually worked in the Borrowed Dawn painted room.

Zac's rule, in his words: "I don't want any puzzle rooms themselves to be 3D object based. I want them to use the authored png 2.5D look of the first puzzle room. For all objects, animations, environment, etc. 3D elements are to help structure the space and for Ankhu to walk around, interact with things, etc but everything else should be beautiful painted shaders and flipbooks."

The reference room is Borrowed Dawn painted (`Assets/Ankhu/Generated/Scenes/BorrowedDawn_Painted.unity`, built by `Assets/Ankhu/Editor/Environment/BorrowedDawnPainted.cs`). The visual target is `D:\TheAfterlifeofAnkhu\art_gen\borrowed_dawn\painted\target\master_target.png`. When this spec and a habit disagree, this spec wins. When this spec and Zac disagree, Zac wins and this file gets updated.

## 1. The one rule

Every pixel the player sees in a puzzle room, except the cast, is authored paint: a PNG painted in the house style and drawn by a painted shader. Geometry exists only to structure the space and drive play, and it is never what the camera sees.

The cast means Ankhu, Anpu and any character with an Animator. They stay 3D and are graded into the paint by `PaintedActorGrade`.

## 2. What 3D is for (invisible)

These are allowed and expected. None of them may render to a player camera.

- Colliders, the walk lane, floors Ankhu stands on, steps and ledges.
- Blockers, trigger zones, interaction volumes and `RealmObjectView` pose pivots.
- Transforms that a pose moves, rotates or scales. The painted card rides on the transform, while the grey mesh under it is disabled.
- Cameras, camera rails and `BackdropFit`.
- Shadow catchers (`shadow_catcher.mat`, which draws only the cast's shadow).
- Lights. They exist only to light the cast. Painted materials ignore them or take at most `_LightGain` 0.12.

The greybox build keeps its grey primitives so designers can block out and test. The painted build disables every primitive renderer, moves the static set to the hidden paint layer (29) and keeps the colliders. A grey primitive that is visible in a painted build is a bug.

## 3. What may be seen (the allowed list)

| Kind | Use it for | Shader / material | Where the art lives |
|---|---|---|---|
| Backdrop painting | The whole static room per camera: walls, floor, sky, static props | URP Unlit on a `Painting` quad child of the camera, `BackdropFit`, 300 m, queue Background+10 | `Assets/Ankhu/Realms/Art/<Realm>/<Scene>_cam_<cam>.png` |
| Painted layers | Parallax depth (far, mid, cut groups) when the camera moves a lot | `Ankhu/PaintedLit`, `_LightGain` 0.12 | `Assets/Ankhu/Painted/Art/L_*.png` |
| Painted card | Any object that moves, toggles or sits in front of the painting | `card_<sprite>.mat`, URP Unlit transparent, ZWrite off, or `Ankhu/PaintedLit` | `Assets/Ankhu/Realms/Art/<Realm>/Sprites/<sprite>.png` |
| Flipbook card | Anything that animates its shape: state changes, cloth, birds, flames, glyphs waking | `Ankhu/PaintedFlipbook` (`flip_<name>.mat`), `FlipbookPlay` for one-shots | `.../Sprites/flip_<name>.png` |
| Painted ambient | Motion that stays inside a painted shape: fire, glints, mist, motes, glow | `Ankhu/PaintedAnim` (scrolling sheet plus static mask) | `.../Sprites/anim_<name>.png` and `anim_<name>_m.png` |
| Painted water | Bodies of water, channels, pours | `Ankhu/PaintedWater` with a painted `_Flipbook` sheet | `water_<name>.png` |
| Painted FX | Additive light marks: sound rings, threads, beams, eye glints | URP Unlit additive or `Ankhu/PaintedBeam`, material `fx_<name>` | `fx_<name>.png` |
| Particles | Only with a painted sprite texture from an Art folder, never a soft dot or default particle | Particles/Unlit additive, material `fx_<name>` | `fx_<name>.png` |

## 4. Forbidden (the audit fails on any of these)

- A visible renderer with no texture, whatever its shader (flat-colour cubes, spheres, cylinders, planes and sheens).
- A visible renderer using URP Lit or Simple Lit. Lit surfaces show the grey primitive's 3D shading, not paint.
- Emissive primitives used as glows (sphere beads, glowing boxes, emissive glyph plates). Paint the glow and play it on a card or FX quad.
- Procedural or default particle textures (`soft_dot`, Default-Particle).
- Untextured LineRenderers. They fall back to white wires.
- Scene fog with a painted backdrop. Exp2 fog turns a 300 m painting into solid colour, so `ApplyPaint` sets `RenderSettings.fog = false`.
- Animating a visible shape by scaling, stretching or rotating primitives. Animate the paint with a flipbook, or move a card as a whole.
- Placeholder art left in a build Zac sees. Paint it or hide it.

## 5. The pipeline, in order

1. **Greybox the play.** Build the room with grey primitives, colliders, poses and the solver. Prove it plays. This is the only stage where primitives may show.
2. **Plan the paint.** For each camera list three things: what the backdrop paints (the static set), what must be a card (anything a pose moves, shows or hides, or that sits in front of Ankhu), and what needs motion (ambient loops and state transitions). Write it in the room's build spec.
3. **Capture plates.** `RealmSceneBuilder.CaptureWernesPlatesCli` (or the room's equivalent) renders each camera with only the static set at 1536x1024, poses at their first state. Output goes to `art_gen\<realm>\plates\`.
4. **Paint the backdrops.** Use Codex through `~/.claude/skills/gpt-image-bridge/bin/gpt-image-2` with `--size 1536x1024 --ref <plate> --ref D:/TheAfterlifeofAnkhu/art_gen/borrowed_dawn/painted/target/master_target.png`. The prompt keeps the composition pixel-aligned to the plate and asks for the Borrowed Dawn painted style. Run one Codex call at a time, with a pause between calls, and tell the coordinator when you take and release the Codex slot. Save to `art_gen\<realm>\painted\`, then copy into the Art folder.
5. **Paint the cards and flipbooks.** Each live object is painted on flat magenta (#FF00FF), front-on in the same camera angle and light as its backdrop, using its plate crop and the backdrop as refs. Key the magenta out (`art_gen\<realm>\sprites\keyed`) and check the fringe at 2x. Flipbook sheets are painted as one image so the frames match (section 7).
6. **Build.** The painted builder installs the paintings, runs `Cardify` (or the room's card pass) to swap every live primitive for its card, moves the static set to the paint layer and turns fog off.
7. **Audit.** Run `PaintedRoomAudit` (section 9). Zero violations, or the room is not done.
8. **Capture and look.** Run state captures for every pose state (`RealmCapture.CaptureWernesPaintedCli` or the room's own). Read every image. Then do a player-build check, because some bugs only show in a player (see `painted-backdrop-pitfalls` in project memory).

## 6. Cards

- One card per live object. The grey primitive keeps its transform, poses and collider, and only its renderer is disabled.
- A card faces the camera (`CardFacing`) unless it must turn in its own plane, like a door leaf on a hinge or a slab standing ajar. Such cards are built in the object's plane.
- Size a card from the art: the sprite's aspect, and the object's world height (`hWorld`) against its painted height (`hArt`).
- Materials are alpha-blended with ZWrite off. Alpha clip with depth write drew pale boxes in the soft mist, so do not use it.
- Text, glyphs and hieroglyphs must read left to right as painted. Check every card and plate for mirroring in a capture. Glyph planes use euler zero.
- Grade a card to sit in its painting (`_BaseColor` about 0.93, 0.90, 0.86, or `sat` and `gain` in props.json). A card should not be brighter or more saturated than the paint around it.
- States: each pose state that changes the look gets its own card or flipbook range. Poses show and hide cards, never primitives.

## 7. Flipbooks

- Shader: `Ankhu/PaintedFlipbook`. Properties: `_Cols`, `_Rows`, `_FrameStart`, `_FrameCount`, `_Fps`, `_Loop`, `_Blend` (cross-fade, 1 for soft paint), `_Tint`, `_Gain`, `_PulseAmp`, `_Flicker`, `_Sway`, `_SwaySpeed`, plus the blend enums (alpha 5/10, additive 5/1 or 1/1).
- Sheet layout: a grid with row 0 at the top, frames read left to right. Use 4x4 (16 frames) or 4x2 (8). Each cell is at least 256 px tall for props and 384 px for anything near Ankhu. Keep a 4 px empty margin inside each cell so frames never bleed.
- Every frame shares one pivot: bottom centre for standing things, top centre for hanging things.
- Paint the whole sheet in one Codex call with the backdrop as a ref, so light and palette match across frames. If frames drift, repaint the sheet. Never mix frames from two calls.
- Loops (fire, cloth, reeds, water, a bird breathing) run at 6 to 10 fps with `_Blend` 1. Every loop gets a phase from its world position, so nothing pulses in lockstep.
- Transitions (a lid opening, a glyph waking, a brick cracking) are one-shots: `_Loop` 0, with `FlipbookPlay` on the card so the clip starts when the pose shows it and holds its last frame. One state can share a sheet with another through `_FrameStart` and `_FrameCount`.
- Cheap motion goes first. Sway on a static card (`_Sway`) covers hanging cloth and reeds, and `_Flicker` covers lamps, before a full flipbook is needed.

## 8. Animation and life

Zac asked for lots of animation. A finished room has all of the following.

- **Ambient loops in every shot:** lamp and brazier flames (flipbook or PaintedAnim with flicker), drifting motes in the light (PaintedAnim, weighted by light), and at least one moving surface such as water, cloth, reeds or smoke.
- **A painted transition for every state change** the player causes, so nothing pops. Use a one-shot flipbook, a card cross-fade, or a card sliding or rotating as a whole.
- **Feedback FX in paint:** rings, threads, glints and glows from painted `fx_` textures, additive, never emissive primitives.
- **The cast seated in the paint:** `PaintedActorGrade` on Ankhu and Anpu, a contact shadow at queue 3050, and shadow catchers on the walk lane.

## 9. Audit (required before calling a room done)

Tool: `Assets/Ankhu/Realms/Editor/PaintedRoomAudit.cs`.

```
"C:\Program Files\Unity\Hub\Editor\6000.6.3f1\Editor\Unity.exe" -batchmode -quit -projectPath D:\TheAfterlifeofAnkhu -executeMethod Ankhu.Realms.EditorTools.PaintedRoomAudit.AuditWernesCli -logFile <log>
```

It opens each painted room and checks every renderer that a player camera can see, including those that only a pose shows. Each renderer is classed as painting, card, painted FX, cast, catcher or violation. Its report goes to `D:\TheAfterlifeofAnkhu\art_gen\realms\audit\painted_audit.md`. A room passes only with zero violations. New realms add their scenes to the audit by calling `PaintedRoomAudit.AuditOpenScene`.

**Glyph coverage (Zac, 2026-11-24).** Every spell word the room accepts, and every target or noun the player can type after one, needs a hieroglyph gloss (in `Lexicon.cs` or the object's `Gloss`), and every sign in it must ship as `<code>.png` and `<code>_glow.png` in `Assets/Ankhu/Realms/Art/Glyphs/Resources/RealmGlyphs`. A sign with no texture is dropped silently in game. New signs go into `CODES` in `Assets/Ankhu/Realms/Art/Glyphs/_tools/make_glyphs.py`, then run it. Check:

```
"C:\Program Files\Unity\Hub\Editor\6000.6.3f1\Editor\Unity.exe" -batchmode -quit -projectPath D:\TheAfterlifeofAnkhu -executeMethod Ankhu.Realms.EditorTools.GlyphCoverageCheck.CheckCli -logFile <log>
```

It reads every Wernes and Realm room script, the Borrowed Dawn and teaching parser and the whole lexicon, and writes `D:\TheAfterlifeofAnkhu\art_gen\realms\audit\glyph_coverage_check.md`. It must report PASS. Deeds and movement carry no glyphs and are skipped.

## 10. Naming and locations

| What | Path |
|---|---|
| This spec | `D:\TheAfterlifeofAnkhu\docs\PAINTED_ROOM_SPEC.md` |
| Plates (unpainted renders) | `D:\TheAfterlifeofAnkhu\art_gen\<realm>\plates\<Scene>_cam_<cam>.png` |
| Codex output, prompts, logs | `D:\TheAfterlifeofAnkhu\art_gen\<realm>\painted\` and `...\sprites\raw\` |
| Keyed sprites | `D:\TheAfterlifeofAnkhu\art_gen\<realm>\sprites\keyed\` |
| Backdrops in Unity | `Assets/Ankhu/Realms/Art/<Realm>/<Scene>_cam_<cam>.png` |
| Cards, flipbooks, anim sheets in Unity | `Assets/Ankhu/Realms/Art/<Realm>/Sprites/` with names `<thing>.png`, `flip_<thing>_<state>.png`, `anim_<thing>.png` and `anim_<thing>_m.png`, `fx_<thing>.png` |
| Generated materials | `Assets/Ankhu/Realms/Art/<Realm>/Materials/` with prefixes `painting_`, `card_`, `flip_`, `anim_`, `fx_` |
| Painted shaders | `Assets/Ankhu/Painted/Shaders/` (PaintedLit, PaintedAnim, PaintedFlipbook, PaintedWater, PaintedBeam) |
| Painted runtime | `Assets/Ankhu/Runtime/Painted/` (PaintedActorGrade, FlipbookPlay, PaintedGateSink, PaintedSunGate) |
| Audit reports | `D:\TheAfterlifeofAnkhu\art_gen\realms\audit\` |

Texture import: painted art uses Trilinear filtering, mip bias -0.5 and aniso 4 (r04 found zoomed views sampling a blurry mip). Backdrops use no mips, 2048 max, uncompressed and clamp. Cards and flipbooks use alpha is transparency, clamp, NPOT none and CompressedHQ.

## 11. Known pitfalls (each one cost a night)

- The static-set sort once caught the new `Painting` quads and hid them on layer 29. Skip renderers named `Painting` whose parent has a Camera.
- Exp2 fog at 300 m hid the backdrop completely. Turn fog off.
- Alpha-clipped cards with depth write drew pale boxes in the mist. Use alpha blend with ZWrite off.
- Far layers that are a full copy of the master doubled every pillar under parallax. Inpaint solids out of the far layer where the mid layer is opaque.
- Glyph plates built with a 180 degree yaw read mirrored. Use euler zero and check in a capture.
- Emissive beads read as white blobs over paint. Paint the glow instead.
- Live copies of ambient sway and water sat over the painting as floating shapes. Ambient motion belongs to the paint (flipbook or PaintedAnim), not to live geometry.
- Batch builds pass silently while a player shows a broken frame. Always look at a player capture.

## 12. Done checklist

A room is done only when every box is true. Paste this list into the thread's final report with each box ticked or explained.

- [ ] The greybox plays and solves (solver or play-through), and the colliders and walk lane are unchanged by painting.
- [ ] Every camera has a painted backdrop in the house style, pixel-aligned to its plate.
- [ ] Every live object is a painted card or flipbook. No grey primitive is visible in any pose state.
- [ ] Every state change has a painted transition, and every shot has ambient motion (flame, motes, one moving surface).
- [ ] The cast is graded, has a contact shadow, and casts shadows on catchers only.
- [ ] Glyphs and text read correctly, with no mirroring, and cards sit in the paint's light and palette.
- [ ] `PaintedRoomAudit` reports 0 violations for the room, and the report path is in the thread.
- [ ] `GlyphCoverageCheck` reports PASS, so every word the room accepts has its signs and textures.
- [ ] State captures for every pose state were read by eye, and a player-build capture was checked.
- [ ] No greybox, Teach, Realm_0x or BorrowedDawn_Greybox scene was overwritten.

## No-stretch rule (Zac, 2026-11-26)

Stretching images is banned. Every image is authored at the aspect it is shown at and is never resampled to another aspect by the importer, a quad, a sprite or a UI rect. The aim is also to author at the pixel size it is shown at, so nothing is upscaled.

- **Import.** Any non power of two image under `Assets/Ankhu` or `Assets/Art` that is not a sprite imports with Non Power of 2 = None. `NoStretchImportRule` (in `Assets/Ankhu/Realms/Editor/NoStretchCheck.cs`) enforces this on every import. The default (ToNearest) turned the 1536x1024 Wernes and Rosetau paintings into 2048x1024 and made every moon oval.
- **Sizing.** Size anything from the image's source size with `NoStretchCheck.SourceAspect(tex)`, never from `tex.width / tex.height`, which is the imported size.
- **Backdrops.** `BackdropFit.imageAspect` equals the painting's source aspect. The fit covers the screen by scaling both axes together.
- **Cards and flipbooks.** A textured quad's world scale (with tiling and the flipbook grid) matches its frame's aspect. Sprite cards keep a uniform scale.
- **UI.** Full-screen stills cover the screen with an AspectRatioFitter (EnvelopeParent) at the still's own aspect, never a stretched RawImage.
- **Films.** Every film is 16:9, the aspect every player fits to.
- **Exempt on purpose.** The farewell door leaves squash on x at runtime to fake their swing on the hinge.

**Check:** Ankhu > Painted > Check No Stretch, or in batch
`Unity.exe -batchmode -projectPath D:\TheAfterlifeofAnkhu -executeMethod Ankhu.Realms.EditorTools.NoStretchCheck.CheckCli -logFile <log>`.
It opens every play-order scene and exits 1 on any aspect mismatch over 1%. Resolution debt (an image shown larger than it was painted at 1080p) is listed as a note, and `-strictRes` makes it fail too. `FixCli` repairs imports and backdrop aspects that need no new art. Report: `D:\TheAfterlifeofAnkhu\art_gen\realms\audit\no_stretch_check.md`.

## Complete backgrounds, separate objects (Zac, 2026-11-26)

Zac's rule, in his words: "No attempts to 'cut out' things from backgrounds - backgrounds must be complete and objects must be authored separately with transparent backgrounds and placed on top."

- **Backdrops are complete and empty.** A backdrop painting is the whole environment for its camera (sky, ground, walls, water, far scenery) with nothing missing behind where an object will sit. It paints no puzzle objects, props, cards, characters or FX. Ground continues under every object, so moving or hiding a card never shows a hole.
- **No cutting out.** Never mask, key, inpaint or crop an object out of a backdrop to make a card, and never paint a hole or a blank patch in a backdrop for a card to cover.
- **Objects are authored on their own.** Every object is painted separately (on flat magenta, then keyed, per section 5 step 5, or with real transparency), saved as a PNG with alpha, and placed on top as a card or flipbook.
- **Prompts.** A backdrop prompt names the room's objects only to say they are absent. Save each prompt next to its output.
- This overrides "static props" in the backdrop row of section 3: a static prop that is part of the architecture (a wall, a pillar, a cliff) stays in the backdrop, while anything that reads as an object placed in the room is its own card.
- **Clarification (Zac, 2026-11-26, "That's ok").** Cards painted on their own on flat magenta and colour-keyed (Wernes, Rosetau) follow this rule and stay. What breaks it is any asset masked out of a master painting, such as the Borrowed Dawn and cistern `L_*` layers.
