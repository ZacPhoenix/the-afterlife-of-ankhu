# Realms: the playable game after the opening walk

Everything that plays after `Opening_WalkDuat` lives here. The story source is the full-game script v1 (`/mnt/project-files/design/story/ankhu_full_game_script_v1.md`, sections 3.0, 3.1 and 6.2). The story bible and spell compendium are linked from project memory.

## What plays

| Scene | Room | Words | What it means |
|---|---|---|---|
| `Realm_01_Arit1` | The First Gate (Spell 144). Learn the three keepers' names (heard, read, looked at) and speak them | name | A gate opens for one who is known. The keeper lies on its face, as Ankhu lay at the pool |
| `Realm_02_Wernes_Field` | The Field That Waits. Open the sluice, wake the shabti, still the shabti. Memory A: Hori on his shoulders | open, wake, still | A man who let work do his living has to tell the worker to stop before he can remember |
| `Realm_03_Wernes_House` | The House With One Door. Seal cannot be spoken until the false door gives Memory B (his father leaving) | seal (learned) | The word he feared since his father's door saves something small |

Anpu gives Ankhu the codex at the First Gate. Each scene ends by loading the next `Realm_*` scene in Build Settings. The last one returns to the title.

## Layout

| Path | What |
|---|---|
| `Core/` | Engine-free C# (`Ankhu.Realms.Core`, no UnityEngine): rules (`RealmRules.cs`), words and their hieroglyphs (`Lexicon.cs`), codex data (`Codex.cs`, `Content/CodexContent.cs`), scene scripts and beats (`SceneScript.cs`, `Content/WernesScripts.cs`) |
| `Runtime/Director/` | `RealmSceneDirector` runs a scene: zones, camera cuts, spells, beats, hints. `RealmHud` draws dialogue, titles, memories, prompts. `RealmObjectView` poses objects per state |
| `Runtime/Magic/` | The words-of-power layer: speech bubble over Ankhu's head, English turning into hieroglyphs that fly to their target, gold light-up (`SpellPresenter`, `GoldMagic`, `GlyphAtlas`). See `README_MAGIC.md` |
| `Runtime/Codex/` | Anpu's codex: the papyrus book, toasts, saving. See `README_CODEX.md` |
| `Editor/` | `RealmSceneBuilder` builds the scenes from code. `RealmKit` holds materials and architecture pieces. `RealmCapture` renders every camera to PNG |
| `Art/` | Glyph sprites (from Noto Sans Egyptian Hieroglyphs, OFL), codex textures, fonts (Cinzel, Cormorant Garamond) |
| `Generated/` | Builder output. Rebuild rather than hand-edit |

## Commands

Build the scenes and add them to Build Settings after the walk:

```
Unity -batchmode -projectPath D:\TheAfterlifeofAnkhu -executeMethod Ankhu.Realms.EditorTools.RealmSceneBuilder.BuildAllCli -logFile realms_build.log
```

Build and capture every camera (start and end states) to `art_gen/realms/captures`:

```
Unity -batchmode -projectPath D:\TheAfterlifeofAnkhu -executeMethod Ankhu.Realms.EditorTools.RealmCapture.CaptureAllCli -logFile realms_capture.log
```

Menu: Ankhu > Realms > Build Wernes Scenes, and Ankhu > Realms > Capture Realm Cameras.

## Controls

| Key | Action |
|---|---|
| WASD or stick | Walk (camera-relative, held direction survives cuts) |
| Enter | Open the speech bubble, type, Enter to speak, Esc to cancel |
| E or gamepad A | Use (read, look, walk through) |
| Tab or C | The codex |
| H or gamepad Y | Hint |
| Space | Skip a line |

## Writing a room

1. Add a `SceneScript` in `Core/Content` with a `RoomDef` (objects, rules, goal, hints), beats (dialogue, codex writes, memories, cues) and usables.
2. Add any codex entries to `CodexContent.cs` and any new words to `Lexicon.cs`.
3. Add a `BuildX` method in `RealmSceneBuilder` that lays out geometry, a `RealmObjectView` pose per object state, zones and fixed cameras.
4. Prove the room solvable with the solver (`/home/user/realms-check/solver` in the cloud session, a breadth-first search over every utterance).
5. Export voice lines (`Solver.dll export tools/voice/realms`) and record them (see `tools/voice/README.md`). Clips go to `Assets/Ankhu/Realms/Audio/Resources/RealmVoice/<key>.wav`.

Rules can carry `Requires` conditions, `Also` side effects and `auto` consequences (derived states). A verb outside the room's verb set answers "You know this word exists, but your mouth cannot shape it yet", which is how a memory can teach a word mid-room (`Step.Learn`).

## Honest status

Written and compile-checked offline against Unity 6 reference assemblies. The first real Unity run is the authority. Voice clips and memory paintings are optional at runtime: a missing clip shows its subtitle for a reading time, a missing painting shows a dark sepia frame.
