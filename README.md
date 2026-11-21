# The Afterlife of Ankhu

Narrative puzzle adventure through a fictional Egyptian afterlife, built in Unity. This repository is the Unity project root.

## Layout

| Path | Owner | What it is |
|---|---|---|
| `Assets/Ankhu/Runtime/Integration/` | greybox | Engine-free adapter contract (`IRoomRulesAdapter`, `RoomSnapshot`, `PlayerContext`) |
| `Assets/Ankhu/Runtime/` | greybox | Player, speech bubble, camera, world presentation, traversal test adapter |
| `Assets/Ankhu/Bindings/` | greybox | Binds the rules library (`RuleProfile.Greybox`) to the adapter contract and keeps the rules location in step with where Ankhu walks |
| `Assets/Ankhu/Editor/` | greybox | Code-generated scene and prefab builder |
| `Assets/Ankhu/Generated/` | generated | Output of the builder. Rebuild instead of hand-editing |
| `Packages/com.ankhu.borrowed-dawn/` | rules engine | Pure C# rules and parser (`Ankhu.BorrowedDawn`, no engine references) |
| `dotnet/borrowed-dawn/` | rules engine | .NET build, NUnit tests, solver, outside the Unity tree |
| `tools/compile-check/` | greybox | Offline compile check against Unity reference DLLs (fetched, never committed) |
| `docs/` | all | Design notes, starting with `GREYBOX_DESIGN.md` |

## Defaults (reversible)

Unity 6 LTS, URP with the Universal (3D) renderer, Input System package, uGUI. Gameplay physics is Physics2D in the XY plane, which locks movement to one plane, while visuals are 3D so a 3D Ankhu (for example from Meshy) and 2D or 3D scenery can mix. Nothing pins an editor patch version.

## Build the Borrowed Dawn greybox

1. Open this folder in Unity Hub with a Unity 6 editor. Unity resolves `Packages/manifest.json`.
2. Run the menu **Ankhu > Build Borrowed Dawn Greybox**. It assigns a URP pipeline asset if none is set, then writes materials, prefabs and the scene under `Assets/Ankhu/Generated/`.
3. Open `Assets/Ankhu/Generated/Scenes/BorrowedDawn_Greybox.unity` and press Play.

Batch mode: `Unity -batchmode -projectPath . -executeMethod Ankhu.EditorTools.BorrowedDawnGreyboxBuilder.BuildFromCommandLine -logFile build.log`

## Controls

| Key | Action |
|---|---|
| A D or arrows | Walk |
| W S or arrows | Climb ladders |
| Enter | Open the speech bubble, type, Enter to speak, Escape to cancel |
| E | Use what Ankhu faces |
| Q | Inspect |
| U, H, hold R | Undo, hint, reset |
| F1 | Debug state panel (toggle any snapshot field while no rules library is installed) |
| F2 | Switch camera between perspective and orthographic |

## Offline compile check

`tools/compile-check/compile-check.sh` (Linux, .NET 8) downloads Unity 6 player reference DLLs from nuget.org, compiles every asmdef, and runs the engine-free NUnit tests. It cannot catch scene, serialization or runtime problems. A real editor run is the authority.
