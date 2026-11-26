# Borrowed Dawn art pass: paused state (2026-11-21)

Paused at Zac's request. Nothing in Unity has been built or compiled for this pass yet.

## Done
- Round 1 options (12 sets, 38 Codex images) and Zac's picks. Picker: https://claude.ai/artifact/PPyAFTTMdCeAhdGseoBY39
- Picks: lapis-and-gold temple, sandstone and lapis plaster, night blue with one dawn shaft, dark celestial water, bronze sun-disc mirror on papyrus raft, basalt and gold Bearer, slate stele seal, gilded and lapis machinery, dark bronze bell in a stone pylon, cedar plank bridge on bronze chains, winged sun-disc gate. Binding colour violet, with cyan to be tested.
- Zac: this is Anpu's final test room for Ankhu, so lean into the Anubis theme.
- 15 production textures made with Codex (art_gen/borrowed_dawn/tex). Processed into BaseColor, Normal and MaskMap PNGs and copied into Assets/Art/Environments/BorrowedDawn/Textures (44 files, not yet imported or committed).

## In progress (not verified)
- Dresser editor script draft: /mnt/project-files/art/environments/borrowed_dawn/unity/Editor/BorrowedDawnDresser.cs. NOT in the repo yet and never compiled. Target path Assets/Ankhu/Editor/Environment/. It runs the greybox builder and then dresses all four scenes (menu Ankhu > Build Borrowed Dawn Dressed).
- Blender modular kit: 16 FBX files exported but the agent was stopped during QA, so their dimensions and axes are unchecked. Location: /mnt/project-files/art/environments/borrowed_dawn/kit/wip_unverified/ (build_kit.py rebuilds them).

## Next steps
1. Re-run the kit QA (re-import each FBX, check sizes, axes, UVs, slot names), then copy into Assets/Art/Environments/BorrowedDawn/Kit.
2. Add `[SerializeField] Color threadColor = GreyboxPalette.Violet` to BindingIndicator and use it in Update (one-line change, tell the greybox thread).
3. Copy the dresser into the repo when Unity is not in Play mode, compile, run it, fix errors.
4. Capture frames (Ankhu > Capture > Side-On Frames), run env_metrics (HF < 2%, speckle < 400/MP, squint walk line > 8), measure Ankhu contrast, compare violet vs cyan.
5. Open: Zac's decision on Meshy vs Blender for the Bearer and jackal guardians. Gate door icons (sun, figure, bell) still to make.
