# 05 Teach1_Cistern_Painted: The Cistern

## 1. Summary

| Field | Value |
|---|---|
| Scene | `Teach1_Cistern_Painted` (greybox twin `Teach1_Cistern`, still in Build Settings after the chain) |
| Realm | Anpu's pavilion (the God's Pavilion), teaching room 1 of 3 |
| Title shown | "The Cistern" (subtitle panel, 3 s) |
| Play order | 5 of 21 |
| Previous scene | `Realm_01_Arit1_Painted` (4) |
| Next scene | `Teach2_HeldStep_Painted` (6) |
| Engine | Borrowed Dawn rules package (`Ankhu.BorrowedDawn`, `RuleProfile.Greybox`) through `BorrowedDawnRulesAdapter`, with `TeachingRoomDirector` for Anpu |
| Room source | D:\TheAfterlifeofAnkhu\Packages\com.ankhu.borrowed-dawn\Runtime\TeachingRooms.cs, `TeachingRooms.Cistern` (room id `teach-1-cistern`) |
| Rules | D:\TheAfterlifeofAnkhu\Packages\com.ankhu.borrowed-dawn\Runtime\RoomRules.cs (`MoveGreybox`, `Flow`) and RoomDefinition.cs (`Apply`) |
| Unity glue | D:\TheAfterlifeofAnkhu\Assets\Ankhu\Bindings\BorrowedDawnRulesInstaller.cs, BorrowedDawnRulesAdapter.cs, TeachingRoomDirector.cs, MemoryFlicker.cs |
| Scene build | D:\TheAfterlifeofAnkhu\Assets\Ankhu\Editor\BorrowedDawnGreyboxBuilder.cs (spec: spawn `L.Spawn` on the floor, `SealGalleryEast = true`), painted by D:\TheAfterlifeofAnkhu\Assets\Ankhu\Editor\Environment\BorrowedDawnPainted.cs |
| Solver report | D:\TheAfterlifeofAnkhu\dotnet\borrowed-dawn\TEACHING_ROOMS.md (5 states, 0 softlocks, shortest 4) |

## 2. Intent and story beat

Lesson (from the room definition): "flow west and flow east move the water, and the water moves the lift." It is the first time Ankhu's voice changes the world by itself, and Anpu frames it: "Here, words are the only hands you have." On the solve: "You did not climb. You asked, and the water carried you."

Story: the room ends in the 8 s flicker film `flick_f1_river` (F1, the river), the first fragment of Ankhu's buried life surfacing as he learns. The three teaching rooms are Anpu's pavilion, where embalmers learn their craft, promised at the First Gate.

## 3. Layout and zones

Side-on Borrowed Dawn kit (one fixed painted screen). Ankhu walks with `PlayerController2D`. `PlayerZoneTracker` reports the zone, and the installer offers each zone change to the rules as `MoveTo`. A refused move teleports Ankhu back to the last accepted spot and posts the refusal.

| Zone id (`ZoneIds`) | Rules `Location` | What stands there | Reachable in this room |
|---|---|---|---|
| `floor` | Floor | Spawn, water selector, sun socket, Bearer, shadow seal, ladder foot | yes (start) |
| `lift` | Lift | The elevator platform in the shaft, low when water is west, high (gallery height) when east | yes |
| `gallery` | Gallery | Upper west walkway: inscription wall, ladder hatch | yes (the goal) |
| `landing` | Landing | Bell landing | no, the lesson seal blocks the walkway east of the gallery |
| `bridge`, `gate` | Bridge, Gate | Pendulum bridge and the great gate | no |

Floor to gallery is only possible by the ladder, which stays rolled up here (LowerLadder is not taught).

## 4. Elements

The full Borrowed Dawn kit is present, but only water and the lift take part.

| Element | Rules field | Start | Moved by | Behaviour |
|---|---|---|---|---|
| Water (east and west basins, selector) | `Water` | `East` | `flow west`, `flow east` | Water fills one basin only |
| Elevator platform | `ElevatorHigh` (derived: `Water == East` or elevator bound) | high | the water | Rides up to gallery height with water east, down to the floor with water west. Carries Ankhu |
| Raft and mirror | `RaftRaised` (derived: water west), `Mirror` | low, `Raft` | the water | Rises with water west. The mirror is not takeable (TakeMirror not taught) |
| Ladder hatch | `LadderLowered` | false | `lower ladder` (not taught) | Rolled up |
| Inscription, bell, Bearer, bridge, gate | | idle | not taught | Inert. Any E or word on them is NotYetTaught |
| Lesson seal | | | | Static barrier east of the gallery |

E interactions present in the kit (`InteractionIds`): `read_inscription`, `take_mirror`, `install_mirror`, `strike_bell`, `lower_ladder`. All map to commands outside this room's vocabulary.

## 5. Words

Vocabulary (`allows`): `Flow` and `MoveTo` only. Accepted spellings come from D:\TheAfterlifeofAnkhu\Packages\com.ankhu.borrowed-dawn\Runtime\SpellParser.cs: `flow west|w|east|e`, and movement words `go to|go|walk to|move to <floor|ground|lift|elevator|gallery|balcony|landing|bell landing|gate|threshold|bridge>`, `board lift`, `enter gallery`, `descend`.

| Situation | Library outcome | Adapter `CommandOutcome` | Message |
|---|---|---|---|
| A real verb not taught here ("bind elevator", "lower ladder", E on any interaction) | `NotYetTaught` | `Rejected` | "Those words have no power here yet." plus Anpu's nudge "That word has no power here yet. In this room only the water listens." (first time) |
| Water already where asked | `AlreadyInState` | `NoChange` | "The water is already in the east basin." |
| Unknown word | parse `UnknownVerb` | `NotUnderstood` | "The chamber does not answer to "x"." plus "Did you mean "flow"?" when one edit away |
| Missing or wrong argument | parse `MissingArgument` / `InvalidArgument` | `Incomplete` / `InvalidTarget` | "Flow what? It can act on: west, east." / ""flow" cannot act on "north". Try: west, east." |
| Anything after the solve | `PuzzleComplete` | `Rejected` | "This chamber is already open." |

The realm-room wording ("You know this word exists, but your mouth cannot shape it yet." and "This time, nothing has changed.") is not used in this engine. See section 13.

## 6. Canonical solution

Solver shortest, 4 steps: flow west, go to lift, flow east, go to gallery.

| # | Where | Action | Result | State after |
|---|---|---|---|---|
| 0 | floor | scene starts | title "The Cistern", then Anpu intro 1 to 3 (6 s each or the voice length) | Water East, Floor, lift high |
| 1 | floor | `flow west` | Success "Water pours west. The raft rises." The platform sinks to the floor | Water West, ElevatorHigh false |
| 2 | walk onto the platform | zone `lift` | move accepted | Location Lift |
| 3 | lift | `flow east` | Success "Water returns east. The elevator gains buoyant support." The platform carries Ankhu up | Water East, ElevatorHigh true |
| 4 | walk off at the top | zone `gallery` | move accepted, goal holds (`Location == Gallery`) | solved |
| 5 | | (automatic) | Anpu's solve line for max(5 s, voice + 0.6 s), then film `flick_f1_river`, then hard load of the next room | |

## 7. Alternate valid orders

The state graph has 5 states and one shortest solution. Variants:
- Typed movement replaces walking: `go to lift` after step 1, `go to gallery` after step 3. The installer then teleports Ankhu to the zone anchor.
- Extra round trips (flow east while on the floor, flow west again) are harmless.

Orders that fail:
- `go to lift` (or walking under it) with water east: Rejected `ElevatorNotHere` "The elevator is above you. Send the water west to lower it." with nudge "The platform rides on the water. Where must the water go for it to come down to you?"
- `go to gallery` from the floor: Rejected `LadderRaised` "The hatch above is closed. Its ladder is still rolled up in the gallery." with nudge "The ladder is rolled up above. There is another way to rise."
- `go to gallery` from the lift while low: Rejected `NoLandingAtThisHeight` with nudge "You stand between floors. Let the water choose your height."

## 8. Failure, refusal and reset behaviour

- Each refusal outcome that has a nudge (`ElevatorNotHere`, `NoLandingAtThisHeight`, `LadderRaised`, `NotYetTaught`) shows it once (7 s or the voice length). This includes refusals caused by walking, because the installer's `SyncLocation` runs through the same session.
- Undo (U) and reset (hold R) are provided by the session (`PuzzleSession.Undo`, `Reset`). Undo restores a location, and the installer then teleports Ankhu to that location's anchor.
- H: `HintTiers` gives the nudge (Anpu's last intro line), a direction from the solver path, and the next typed command.
- F3 is a developer skip to the next room.
- No softlock is possible (solver: 0 softlocks, 0 dead ends).

## 9. Beats, memory films and codex

| Beat | Trigger | What happens |
|---|---|---|
| Title | `TeachingRoomDirector.Start` | "The Cistern" for 3 s |
| Intro | after the title | Anpu lines keyed `dialogue.anpu.teach-1-cistern.intro.1..3` |
| Nudges | first refusal of each listed outcome | `dialogue.anpu.teach-1-cistern.nudge.<Outcome>` |
| Solve | first state change with the goal holding | `dialogue.anpu.teach-1-cistern.solve` "You did not climb. You asked, and the water carried you." |
| Film | after the solve line | `MemoryFlicker.PlayForRoom("teach-1-cistern")` plays StreamingAssets `Memories/flick_f1_river.mp4` through `Ankhu.Memories.MemoryFlashback` (120 s guard) |
| Exit | after the film | `SceneManager.LoadScene("Teach2_HeldStep_Painted")` |

No codex writes in this room.

## 10. SFX and audio

| Source | Status |
|---|---|
| Anpu voice: intro 1 to 3, four nudges, solve | recorded (Resources `AnpuDialogueVoice` library) |
| Ankhu speaking the spell (`BorrowedDawnSpellVoice`) | wired through the spell voice library |
| Film audio | inside `flick_f1_river.mp4` |
| Water pouring, basin filling, platform moving, raft rising, refusal sting, footsteps, ambience | **none**. No AudioSource or clip exists anywhere in the greybox kit, the painted pass or the bindings |

## 11. Exit conditions

`Location == Gallery` (rules state). The director then waits the solve time, plays `flick_f1_river`, and loads `PlayOrder.PaintedOf("Teach2_HeldStep")` = `Teach2_HeldStep_Painted`. After the solve, every command answers "This chamber is already open." and walking is no longer synced to the rules.

## 12. Acceptance criteria

- AC-1 [auto] The room starts with Water East, Ankhu on the Floor, the lift high, and is not solved.
- AC-2 [auto] Anpu shows the title and then a non-empty intro line on entry.
- AC-3 [auto] `flow west` is accepted and lowers the lift (ElevatorHigh false). `flow east` raises it again.
- AC-4 [auto] Moving onto the lift while it is high is refused with "The elevator is above you".
- AC-5 [auto] Moving to the gallery from the floor is refused with "The hatch above is closed".
- AC-6 [auto] Untaught verbs (`bind elevator`, `lower ladder`) are Rejected with "Those words have no power here yet." and change nothing.
- AC-7 [auto] Asking for water where it already is gives NoChange.
- AC-8 [auto] Reaching the gallery on the raised lift solves the room.
- AC-9 [auto] After the solve, commands answer "This chamber is already open."
- AC-10 [auto] The room exits to `Teach2_HeldStep_Painted` after the film `flick_f1_river`.
- AC-11 [manual] The platform visibly carries Ankhu up and down, and walking off at the gallery is smooth.
- AC-12 [manual] Water, the platform and the raft have audible SFX.

## 13. Code vs intent disagreements

| # | Where | Severity | Finding | Proposed fix |
|---|---|---|---|---|
| 1 | Greybox kit and BorrowedDawnPainted.cs (no AudioSource anywhere), D:\TheAfterlifeofAnkhu\Assets\Ankhu\Runtime\World\*.cs | major | No SFX at all for water, lift, raft, refusals, footsteps or ambience in any Borrowed Dawn kit room. Breaks the rule that every interaction and animation has authored SFX | Add an SFX component to `WaterColumnView`, `ElevatorPlatform`, `RaftView`, `BellView` and the rest, driven by `SnapshotListener`, with earshot takes. Add room ambience and footsteps to `PlayerController2D` |
| 2 | D:\TheAfterlifeofAnkhu\Packages\com.ankhu.borrowed-dawn\Runtime\RoomDefinition.cs:75 | minor | Untaught words say "Those words have no power here yet." while realm rooms say "You know this word exists, but your mouth cannot shape it yet.", and NoChange messages are specific ("The water is already in the east basin.") rather than "This time, nothing has changed." The two engines speak to the player differently | Decide one convention. The smallest change is to keep the specific NoChange lines (they teach) and change the NotYetTaught text to the realm wording |
| 3 | D:\TheAfterlifeofAnkhu\Assets\Ankhu\Bindings\TeachingRoomDirector.cs:246 | minor | Teach1 and Teach2 leave with a hard `SceneManager.LoadScene` cut straight after the film, while Teach3 and every realm room fade (`SceneFader.FadeTo`) | Use `SceneFader.FadeTo(next, 1.6f, 1.0f)` in `LoadNext` |
| 4 | D:\TheAfterlifeofAnkhu\docs\PLAY_ORDER.md rows 6 and 7 | minor | The table lists `Teach2_HeldStep` and `Teach3_LingeringShadow`, but the chain plays the painted twins `Teach2_HeldStep_Painted` and `Teach3_LingeringShadow_Painted` (both in Build Settings, `PlayOrder.PaintedOf`) | Update the table |
| 5 | D:\TheAfterlifeofAnkhu\Assets\Ankhu\Tests\PlayMode\GreyboxRulesConnectedTests.cs | minor | The only play-mode test loads the greybox scenes (`Teach1_Cistern` and so on), not the painted scenes the chain plays, so the painted copies are untested | Run the same checks against `PlayOrder.PaintedOf(...)` |
| 6 | D:\TheAfterlifeofAnkhu\Assets\Ankhu\Bindings\TeachingRoomDirector.cs:116-127 | minor | Pressing E on an inert interaction (inscription wall, ladder hatch) in the gallery is NotYetTaught and triggers the nudge "In this room only the water listens." The prompts should not be offered at all in this room | Hide interaction prompts whose command the room does not allow (`RoomDefinition.Allows`) |

No softlock (solver verified).

## 14. E2E script

Engine `bd` grammar, as in GUIDE_FORMAT.md, with these additions used below: `zone <ZoneId>` calls `BorrowedDawnRulesAdapter.SyncLocation(zone)` and expects null (move accepted). `zone <ZoneId> => <CommandOutcome> | <text>` expects that refusal. `say` calls `adapter.Speak(text, new PlayerContext(currentZone, false, false))`. `use <InteractionId> => <CommandOutcome>` calls `adapter.Interact`. `expect <Field>=<Value>` reads `adapter.Session.State.<Field>` (RoomState properties, including derived ones such as `ElevatorHigh`, `RaftRaised`, `SunAtGate`, `ShadowPresent`), compared with `ToString()`. `film <id>` asserts the room's film (`MemoryFlicker.FilmForRoom`). `solved` and `unsolved` read `adapter.IsSolved`.

```e2e canonical
scene Teach1_Cistern_Painted
engine bd
expect Location=Floor                        [AC-1]
expect Water=East                            [AC-1]
expect ElevatorHigh=True                     [AC-1]
unsolved                                     [AC-1]
zone floor
say flow west => Success                     [AC-3]
expect ElevatorHigh=False                    [AC-3]
zone lift
expect Location=Lift
say flow east => Success                     [AC-3]
expect ElevatorHigh=True                     [AC-3]
unsolved
zone gallery                                 [AC-8]
expect Location=Gallery
solved                                       [AC-8]
say flow west => Rejected | This chamber is already open.   [AC-9]
film flick_f1_river                          [AC-10]
exit Teach2_HeldStep_Painted                 [AC-10]
```

```e2e alt-1
# A false start: the water raises the empty lift, then comes back for Ankhu.
# Typed movement ("go to lift") is not a separate path: the adapter re-syncs the rules to where Ankhu
# physically stands before every utterance, so only walking moves him.
scene Teach1_Cistern_Painted
engine bd
zone floor
say flow west => Success
say flow east => Success
expect ElevatorHigh=True
zone lift => Rejected | The elevator is above you
say flow west => Success
zone lift
say flow e => Success
zone gallery
solved
exit Teach2_HeldStep_Painted
```

```e2e fail-1
scene Teach1_Cistern_Painted
engine bd
zone floor
zone lift => Rejected | The elevator is above you            [AC-4]
expect Location=Floor
say go to gallery => Rejected | The hatch above is closed     [AC-5]
say bind elevator => Rejected | Those words have no power here yet.   [AC-6]
say lower ladder => Rejected | Those words have no power here yet.    [AC-6]
use read_inscription => Rejected | Those words have no power here yet. [AC-6]
say flow east => NoChange | already in the east basin         [AC-7]
say flwo west => NotUnderstood | Did you mean
say flow north => InvalidTarget | Try: west, east
say flow => Incomplete | Flow what?
expect Water=East
unsolved
say flow west => Success
zone lift
say go to gallery => Rejected | There is no landing at this height
unsolved
```
