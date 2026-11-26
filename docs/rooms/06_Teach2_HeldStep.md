# 06 Teach2_HeldStep (plays as Teach2_HeldStep_Painted): The Held Step

## 1. Summary

| Field | Value |
|---|---|
| Scene | `Teach2_HeldStep_Painted` in the chain (`PlayOrder.PaintedOf("Teach2_HeldStep")`). Greybox twin `Teach2_HeldStep` stays in Build Settings |
| Realm | Anpu's pavilion, teaching room 2 of 3 |
| Title shown | "The Held Step" |
| Play order | 6 of 21 |
| Previous scene | `Teach1_Cistern_Painted` (5) |
| Next scene | `Teach3_LingeringShadow_Painted` (7) |
| Engine | Borrowed Dawn rules (`RuleProfile.Greybox`) via `BorrowedDawnRulesAdapter`, Anpu via `TeachingRoomDirector` |
| Room source | D:\TheAfterlifeofAnkhu\Packages\com.ankhu.borrowed-dawn\Runtime\TeachingRooms.cs, `TeachingRooms.HeldStep` (id `teach-2-held-step`) |
| Rules | D:\TheAfterlifeofAnkhu\Packages\com.ankhu.borrowed-dawn\Runtime\RoomRules.cs (`Bind`, `Release` with the load-bearing check, `TakeMirror` Greybox branch, `MoveGreybox`) |
| Scene build | D:\TheAfterlifeofAnkhu\Assets\Ankhu\Editor\BorrowedDawnGreyboxBuilder.cs (spawn on the raised platform at `(ShaftMid, PlatformHighY)`, `SealGalleryEast = true`) |
| Solver report | D:\TheAfterlifeofAnkhu\dotnet\borrowed-dawn\TEACHING_ROOMS.md (28 states, 0 softlocks, shortest 6, 3 distinct shortest) |

## 2. Intent and story beat

Lesson: "bind holds a thing after its support leaves. release lets it go, but not while the binding bears the whole weight." This is the game's load-bearing rule (Zac 2026-11-20): the elevator binding cannot be released while the water is west.

Anpu frames holding and letting go as a moral act: "You held only as long as you needed to. Remember the feeling of letting go." This foreshadows the story of a man who held his grief too long. The room ends in the flicker film `flick_f2_hands` (F2, small hands), a glimpse of Hori.

## 3. Layout and zones

Same Borrowed Dawn kit as Teach1, gallery sealed to the east. Ankhu starts **on the raised platform** (rules `Location.Lift`, water East).

| Zone | Location | Notes |
|---|---|---|
| `lift` | Lift | Start. The raft with the mirror is reached from here |
| `gallery` | Gallery | The goal. Reached from the lift only while the lift is high |
| `floor` | Floor | Reachable only by riding the lift down (water west, unbound). Floor to gallery needs the ladder, which is not taught |
| `landing`, `bridge`, `gate` | | Sealed |

## 4. Elements

| Element | Rules field | Start | Words | Behaviour |
|---|---|---|---|---|
| Water | `Water` | East | `flow west`, `flow east` | |
| Elevator platform | `ElevatorHigh` = water east or elevator bound | high | the water, `bind elevator`, `release elevator` | A bound platform stays high when the water leaves |
| Elevator binding | `Bindings` has Elevator | none | `bind elevator|lift` needs ElevatorHigh and Ankhu on Lift, Gallery or Landing. `release elevator` refused while water is West (`BindingBearsLoad`) | Thread visual (`BindingIndicator`) |
| Raft and mirror | `RaftRaised` = water west, `Mirror` | low, `Raft` | `take mirror` (also E `take_mirror`): needs Location Lift, Mirror on Raft, water West and ElevatorHigh | Mirror becomes `Carried` |
| Bindings counter | `BindingCount` / capacity 2 | 0 | | Only the elevator can be bound here |

## 5. Words

Vocabulary: `Flow`, `MoveTo`, `TakeMirror`, and `Bind`/`Release` with target Elevator only.

| Situation | Library outcome | Adapter outcome | Message |
|---|---|---|---|
| Untaught verb or target (`bind shadow`, `wake bearer`, `lower ladder`, E on the inscription or hatch) | NotYetTaught | Rejected | "Those words have no power here yet." Nudge: "That word has no power here yet. Hold, release, and move the water." |
| Already in that state (`flow east` at start, `bind elevator` twice) | AlreadyInState / BindingAlreadyHeld | NoChange / Rejected | "The water is already in the east basin." / "That binding is already held." |
| `release elevator` with water West | BindingBearsLoad | Rejected | "The elevator hangs on the binding alone. Let the water carry it before you let go." Nudge: "That thread carries the whole platform now. Give its weight back to the water first." |
| `take mirror` with water East, or lift low | MirrorNotReachable | Rejected | "The raft must be raised and the elevator held high beside it." Nudge: "The raft rises only with the water in the west. Can your platform stay high without the water?" |
| `bind elevator` while the lift is low | ElevatorNotReachable | Rejected | "Stand on the raised elevator or the upper walkway before anchoring it." Nudge: "You can only hold the platform while you stand on it, high in the shaft." |
| `take mirror` a second time | MirrorAlreadyCollected | Rejected | "You have already collected the mirror." |
| After the solve | PuzzleComplete | Rejected | "This chamber is already open." |

## 6. Canonical solution

Solver shortest (6): bind elevator, flow west, take mirror, flow east, release elevator, go to gallery.

| # | Where | Action | Result | State after |
|---|---|---|---|---|
| 0 | lift (high) | start | title, Anpu intro 1 to 3 | East, Lift, none bound, Raft |
| 1 | lift | `bind elevator` | "A binding thread holds the elevator." | Bindings Elevator |
| 2 | lift | `flow west` | "Water pours west. The raft rises." The platform stays high | West, RaftRaised, ElevatorHigh (bound) |
| 3 | lift | `take mirror` (or E) | "You lift the sun-mirror from the raft..." | Mirror Carried |
| 4 | lift | `flow east` | "Water returns east..." The water takes the platform's weight | East |
| 5 | lift | `release elevator` | "You release the elevator." | Bindings None |
| 6 | walk to the gallery | zone `gallery` | goal holds (Carried, Gallery, None) | solved |
| 7 | | automatic | solve line, film `flick_f2_hands`, hard load of `Teach3_LingeringShadow_Painted` | |

## 7. Alternate valid orders

- Walk to the gallery after step 4 (still bound), then `release elevator` there. The goal holds on the release.
- Walk to the gallery after step 3 with water West and the thread holding, then `flow east`, then `release elevator`.
- Typed `go to gallery` instead of walking.

Orders that fail and recover (no softlocks):
- `flow west` first, unbound: the platform sinks with Ankhu on it. `take mirror` is then refused (lift low). `flow east` brings him back up.
- `release elevator` before `flow east`: refused by the load-bearing rule.
- Walking to the gallery without the mirror: not solved. Ankhu can walk back onto the high lift and continue.

## 8. Failure, refusal and reset behaviour

Nudges (once each): MirrorNotReachable, BindingBearsLoad, ElevatorNotReachable, NotYetTaught. Undo U and hold R reset through `PuzzleSession`. H gives three tiers from the solver. F3 skips (developer). Walking refusals (stepping off the low platform onto the floor zone is fine, stepping into a zone the rules refuse) teleport Ankhu back.

## 9. Beats, memory films and codex

| Beat | What happens |
|---|---|
| Title | "The Held Step", 3 s |
| Intro | `dialogue.anpu.teach-2-held-step.intro.1..3` |
| Nudges | `...nudge.<Outcome>` for the four outcomes above |
| Solve | `...solve` "You held only as long as you needed to. Remember the feeling of letting go." |
| Film | `flick_f2_hands` (StreamingAssets `Memories/flick_f2_hands.mp4`) |
| Exit | `SceneManager.LoadScene("Teach3_LingeringShadow_Painted")` |

No codex writes.

## 10. SFX and audio

Anpu intro, nudges and solve are recorded. Ankhu's spell voice through `BorrowedDawnSpellVoice`. Film audio in the mp4. **No SFX** for the water, the platform, the binding thread forming or breaking, the raft, taking the mirror, refusals, footsteps or ambience.

## 11. Exit conditions

`Mirror == Carried`, `Location == Gallery`, `Bindings == None`. Then solve line, `flick_f2_hands`, and `Teach3_LingeringShadow_Painted`.

## 12. Acceptance criteria

- AC-1 [auto] The room starts on the raised lift with water East, nothing bound, the mirror on the raft, not solved.
- AC-2 [auto] `bind elevator` on the raised lift is accepted and the platform stays high after `flow west`.
- AC-3 [auto] `take mirror` is refused unless the water is West and the lift is held high, and accepted when both hold.
- AC-4 [auto] `release elevator` is refused while the water is West (load-bearing rule) and accepted once it is East.
- AC-5 [auto] Untaught words (`wake bearer`, `bind shadow`) are Rejected with "Those words have no power here yet."
- AC-6 [auto] The room is solved only with the mirror carried, Ankhu in the gallery and no binding held.
- AC-7 [auto] The room exits to `Teach3_LingeringShadow_Painted` after film `flick_f2_hands`.
- AC-8 [manual] The binding thread is visible while held and disappears on release. The raft visibly rises with the mirror.
- AC-9 [manual] Each interaction has an audible SFX.

## 13. Code vs intent disagreements

| # | Where | Severity | Finding | Proposed fix |
|---|---|---|---|---|
| 1 | Borrowed Dawn kit views and bindings (no audio anywhere) | major | No SFX for any interaction or animation | As Teach1 finding 1 |
| 2 | D:\TheAfterlifeofAnkhu\Packages\com.ankhu.borrowed-dawn\Runtime\TeachingRooms.cs:43-46 vs the kit | minor | The gallery still offers E prompts for the inscription and the ladder hatch. Both are NotYetTaught, and the first press plays the nudge "That word has no power here yet. Hold, release, and move the water." while Ankhu is trying to finish | Hide prompts whose command the room does not allow |
| 3 | D:\TheAfterlifeofAnkhu\Packages\com.ankhu.borrowed-dawn\Runtime\RoomRules.cs:263 | minor | `bind elevator` a second time is Rejected (`BindingAlreadyHeld`) rather than NoChange, unlike `flow east` twice (AlreadyInState to NoChange). Same situation, two judgements | Map `BindingAlreadyHeld` and `NotBound` to `NoChange` in `BorrowedDawnRulesAdapter.FromExecution` |
| 4 | D:\TheAfterlifeofAnkhu\Assets\Ankhu\Bindings\TeachingRoomDirector.cs:246 | minor | Hard cut to the next scene after the film | Fade, as Teach1 finding 3 |
| 5 | D:\TheAfterlifeofAnkhu\docs\PLAY_ORDER.md row 6 | minor | Names `Teach2_HeldStep`, but the painted twin plays | Update the table |
| 6 | Wording | minor | Same two-engine wording split as Teach1 finding 2 | |

No softlock (solver verified).

## 14. E2E script

Engine `bd`, grammar as described in 05_Teach1_Cistern_Painted.md section 14.

```e2e canonical
scene Teach2_HeldStep_Painted
engine bd
expect Location=Lift                     [AC-1]
expect Water=East                        [AC-1]
expect Bindings=None                     [AC-1]
expect Mirror=Raft                       [AC-1]
unsolved                                 [AC-1]
zone lift
say bind elevator => Success             [AC-2]
say flow west => Success                 [AC-2]
expect ElevatorHigh=True                 [AC-2]
expect RaftRaised=True
say take mirror => Success               [AC-3]
expect Mirror=Carried                    [AC-3]
say flow east => Success
say release elevator => Success          [AC-4]
expect Bindings=None
unsolved                                 [AC-6]
zone gallery                             [AC-6]
solved                                   [AC-6]
film flick_f2_hands                      [AC-7]
exit Teach3_LingeringShadow_Painted      [AC-7]
```

```e2e alt-1
# Carry the mirror to the gallery still bound, then let go there. E for the mirror.
scene Teach2_HeldStep_Painted
engine bd
zone lift
say bind lift => Success
say flow w => Success
use take_mirror => Success
zone gallery
unsolved                                 [AC-6]
say release elevator => Rejected | hangs on the binding alone   [AC-4]
say flow east => Success
say unbind elevator => Success           [AC-4]
solved                                   [AC-6]
exit Teach3_LingeringShadow_Painted
```

```e2e fail-1
scene Teach2_HeldStep_Painted
engine bd
zone lift
say take mirror => Rejected | The raft must be raised        [AC-3]
say flow east => NoChange | already in the east basin
say wake bearer => Rejected | Those words have no power here yet.   [AC-5]
say bind shadow => Rejected | Those words have no power here yet.   [AC-5]
say flow west => Success
expect ElevatorHigh=False
say take mirror => Rejected | The raft must be raised        [AC-3]
say bind elevator => Rejected | Stand on the raised elevator
say flow east => Success
say bind elevator => Success
say flow west => Success
say release elevator => Rejected | hangs on the binding alone  [AC-4]
expect Bindings=Elevator                 [AC-4]
zone gallery
unsolved                                 [AC-6]
```
