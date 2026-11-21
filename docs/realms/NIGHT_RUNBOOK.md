# Realms night runbook (for the machine session on Zac's PC)

Project: D:\TheAfterlifeofAnkhu. The story thread writes code in the cloud and ships it on branch `claude/project-thread-1elknp`. Only these paths come from that branch: `Assets/Ankhu/Realms/**`, `tools/voice/realms/**`, `docs/realms/**`. Never overwrite anything else in the working tree. Another session (the opening cinematic) is using Unity on this machine too.

## Rules

- Unity lock: `D:\TheAfterlifeofAnkhu\Temp\UnityLockfile`. If it exists and Unity.exe is running, wait 10 minutes and retry. Run short batch jobs only (under 20 minutes). Never leave an editor open. Never recompile while Unity is in Play mode.
- Do not launch Unity between 13:10 and 13:35 UTC (the cinematic thread's Windows build).
- Keys: ELEVENLABS_API_KEY and MESHY_API_KEY are User env vars. Never print, log or write their values.
- Do not commit or push. The story thread handles git from the cloud.
- Report back with absolute Windows paths, what ran, and what did not.

## 1. Sync the code

```
cd D:\TheAfterlifeofAnkhu
git fetch origin claude/project-thread-1elknp
git checkout FETCH_HEAD -- Assets/Ankhu/Realms tools/voice/realms docs/realms
git reset -q
```

(The reset unstages those paths again so Zac's index stays as it was.)

## 2. Build and capture

Find the editor: `C:\Program Files\Unity\Hub\Editor\6000.6.3f1\Editor\Unity.exe` (or `Get-ChildItem "C:\Program Files\Unity\Hub\Editor"`).

```
& $unity -batchmode -projectPath D:\TheAfterlifeofAnkhu -executeMethod Ankhu.Realms.EditorTools.RealmCapture.CaptureAllCli -logFile D:\TheAfterlifeofAnkhu\Logs\realms_capture.log
```

Report: exit code, every `error CS` line and every `[Realms]` line from the log, every exception with its first 15 stack lines, and the list of PNGs in `art_gen\realms\captures`. Send the story thread the PNGs: copy them to `D:\TheAfterlifeofAnkhu\_staging\realms_captures\` (the story thread can read that folder).

## 3. Voices (ElevenLabs, on this PC)

Line files are in `tools\voice\realms\`. Use the existing pipeline (`tools\voice\README.md`).

- Anpu lines: `realms_anpu_lines.json` with `anpu_voice_profile.json`.
- Ankhu lines and spells: `realms_ankhu_lines.json`, `realms_ankhu_spells.json` with `voice_profile.json`.
- New speakers (announcer, guardian, keeper, hori, neferet, father): make a profile per speaker under `tools\voice\realms\profiles\` from a suitable voice already in Zac's ElevenLabs account or the shared library (announcer: low mournful older male; guardian: dry close whisper; keeper: very deep slow male; hori: young child, if no child voice is allowed use a light soft voice and keep it short; neferet: warm young woman; father: warm male in his thirties). Use eleven_multilingual_v2 and the same pronunciation dictionary locator as `voice_profile.json`. Note which voice ids you chose in `tools\voice\realms\VOICES.md`.

```
py -3 tools/voice/ankhu_voice.py batch --lines tools/voice/realms/realms_anpu_lines.json --profile tools/voice/anpu_voice_profile.json --out tools/voice/out/raw_realms_anpu
py -3 tools/voice/process_voice.py tools/voice/out/raw_realms_anpu --assets Assets/Ankhu/Realms/Audio/_processed/anpu --highpass 45 --gate-pauses 52
```

Repeat per speaker. Then copy each processed clip `<key>__0.wav` to `Assets\Ankhu\Realms\Audio\Resources\RealmVoice\<key>.wav` (strip the `__0` take suffix, keep take 0). The game loads `Resources/RealmVoice/<key>`.

## 4. Memory paintings

Two paintings, 1920x1080, painted, warm, a little soft at the edges like memory. Use the image tool the cinematic thread uses on this PC (Codex/OpenAI image gen). Save PNGs as `Assets\Ankhu\Realms\Art\Memories\Resources\RealmMemories\<name>.png`.

- `mem_shoulders`: Ancient Egyptian riverbank at sunset, the Nile gold. A man about thirty, shaven head, white linen kilt, walks away from us along the bank with a small boy (about four, side-lock of youth) on his shoulders. The boy holds a small clay falcon up against the low sun. Far behind on the bank, small, a young woman in a white sheath dress with one hand raised, calling. Reeds, palms, a moored papyrus skiff. Painterly, warm, nostalgic, soft vignette.
- `mem_door_dawn`: Interior of a poor one-room mudbrick house in ancient Egypt at dawn. Seen from a child's eye height. A soldier in a kilt with a leather breast guard and a spear stands in the open doorway against blue dawn light, half turned back. The doorframe has a wooden bolt on the outside. In the deep shadow at the back of the room a woman sits, barely visible. Painterly, cool dawn blue against warm lamplight, quiet, sad.

Match the look of the walk paintings (`Assets\Ankhu\Opening\Art\Walk`).

## 5. Rebuild and capture again

Run step 2 again so voices and paintings are imported. Report.
