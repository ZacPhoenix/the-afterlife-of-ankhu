# 07 Teach3_LingeringShadow (plays as Teach3_LingeringShadow_Painted): The Lingering Shadow

## 1. Summary

| Field | Value |
|---|---|
| Scene | `Teach3_LingeringShadow_Painted` in the chain. Greybox twin `Teach3_LingeringShadow` stays in Build Settings |
| Realm | Anpu's pavilion, teaching room 3 of 3 |
| Title shown | "The Lingering Shadow" |
| Play order | 7 of 21 |
| Previous scene | `Teach2_HeldStep_Painted` (6) |
| Next scene | `BorrowedDawn_Painted` (8) |
| Engine | Borrowed Dawn rules (`RuleProfile.Greybox`), `BorrowedDawnRulesAdapter`, `TeachingRoomDirector`, `FarewellDoor` |
| Room source | D:\TheAfterlifeofAnkhu\Packages\com.ankhu.borrowed-dawn\Runtime\TeachingRooms.cs, `TeachingRooms.LingeringShadow` (id `teach-3-lingering-shadow`) |
| Rules | D:\TheAfterlifeofAnkhu\Packages\com.ankhu.borrowed-dawn\Runtime\RoomRules.cs (`Bind` capacity and shadow checks, `Release`, `WakeBearer`, `SleepBearer`, `Flow`) and RoomState.cs derived `ShadowPresent`, `SunAtGate` |
| Exit glue | D:\TheAfterlifeofAnkhu\Assets\Ankhu\Bindings\TeachingRoomDirector.cs `FarewellThenLoad`, D:\TheAfterlifeofAnkhu\Assets\Ankhu\Bindings\FarewellDoor.cs |
| Solver report | D:\TheAfterlifeofAnkhu\dotnet\borrowed-dawn\TEACHING_ROOMS.md (28 states, 0 softlocks, shortest 4, 5 distinct shortest) |

## 2. Intent and story beat

Lesson: "bind shadow before wake bearer, and free a thread first because the voice holds only two." A shadow can outlast its owner if it is held, which is the Borrowed Dawn gate's hardest idea. Anpu: "The shadow stays though its owner has gone. You will need this again." For a man grieving his son and wife, the image of a shadow that stays after its owner leaves is the emotional point.

This is the end of the pavilion. After the flicker film `flick_f3_shadow` (F3, a shadow on a door), Anpu says Ankhu must face the next door alone and that he will wait on the other side, and the painted farewell doors close between them (Zac's pick 9, "A door closes between them").

## 3. Layout and zones

Full kit, **no lesson seal**. Ankhu starts on the floor. The start state already has the elevator and the bridge bound (two threads from "before"), water West, mirror in the socket, Bearer asleep in the beam.

| Zone | Location | Reachable |
|---|---|---|
| `floor` | Floor | Start. The whole lesson happens here: sun socket, Bearer, near shadow seal, far beam |
| `lift` | Lift | Physically high at start (bound). If the player releases the elevator with water East and then flows West, the lift comes down and he can step on, but `MoveTo` is not taught, so the rules refuse and teleport him back |
| `gallery` | Gallery | Only by the ladder, which is up and not taught |
| others | | no |

## 4. Elements

| Element | Rules field | Start | Words | Behaviour |
|---|---|---|---|---|
| Water | `Water` | West | `flow east`, `flow west` | West raises the raft and its light screen, which blocks the far beam |
| Elevator thread | Bindings Elevator | held | `release elevator` refused while water West (load-bearing) | |
| Bridge thread | Bindings Bridge | held | `release bridge` accepted (Ankhu is on the floor) | The free thread to give up |
| Sun mirror | `Mirror` | Socket | none | Lights the near beam onto the Bearer |
| Bearer | `BearerAwake` | false | `wake bearer|statue` (also `awaken`), `sleep bearer` | Awake, it walks into its alcove. Its shadow follows unless the shadow is bound |
| Shadow on the near seal | `ShadowPresent` = socket and (asleep or shadow bound) | true | `bind shadow` needs a free thread and a shadow present | |
| Sun at the far seal | `SunAtGate` = socket, Bearer awake, water East | false | | |
| Thread counter | `BindingCount` | 2 of 2 | | |

## 5. Words

Vocabulary: `Flow`, `Bind`, `Release`, `WakeBearer`, `SleepBearer`. **Not `MoveTo`**, so walking into another zone is refused.

| Situation | Outcome | Adapter | Message |
|---|---|---|---|
| `bind shadow` with two threads held | BindingCapacityFull | Rejected | "Both bindings are occupied. Find another way to support something, then release it." Nudge: "Your voice already holds two threads. Which one do you still need?" |
| `bind shadow` after waking the Bearer (shadow gone) with a free thread | NoShadowToBind | Rejected | "A shadow must fall on the seal before you can bind it." Nudge: "There is no shadow to hold. Perhaps its owner should rest again." |
| `release elevator` with water West | BindingBearsLoad | Rejected | "The elevator hangs on the binding alone..." Nudge: "That thread carries the platform. Give its weight to the water first." |
| Untaught (`go to lift`, `take mirror`, `strike bell`, E on the socket) | NotYetTaught | Rejected | "Those words have no power here yet." Nudge: "That word has no power here yet. Hold, release, wake, sleep, and move the water." |
| `wake bearer` twice | AlreadyInState | NoChange | "The Bearer is already in its alcove." |
| `release bridge` twice | NotBound | Rejected | "That target is not bound." |
| After the solve | PuzzleComplete | Rejected | "This chamber is already open." |

## 6. Canonical solution

| # | Action | Result | State after |
|---|---|---|---|
| 0 | start | title, Anpu intro 1 to 3 | West, Floor, Elevator and Bridge bound, Socket, asleep. ShadowPresent true, SunAtGate false |
| 1 | `release bridge` | "You release the bridge." | Bindings Elevator |
| 2 | `bind shadow` | "A binding thread holds the shadow." | Bindings Elevator, Shadow |
| 3 | `wake bearer` | "The Bearer walks into its alcove. Its shadow remains." | awake, ShadowPresent true |
| 4 | `flow east` | "Water returns east..." The raft screen drops and the sun reaches the far seal | SunAtGate true, goal holds, solved |
| 5 | automatic | solve line, film `flick_f3_shadow`, farewell plate, two farewell lines, doors close, fade to `BorrowedDawn_Painted` | |

## 7. Alternate valid orders

Five distinct shortest solutions (4 steps). The solver's: `flow east`, `release elevator`, `bind shadow`, `wake bearer`. Others: `release bridge`, `flow east`, `bind shadow`, `wake bearer`, and `flow east` at any point before or after steps 1 to 3, and releasing the elevator after the water is east instead of the bridge.

Fails that recover:
- `wake bearer` before binding: the shadow leaves with it. `sleep bearer` brings the shadow back.
- `bind shadow` first: capacity full.
- `release elevator` first: load-bearing refusal.

## 8. Failure, refusal and reset behaviour

Nudges once each for BindingCapacityFull, NoShadowToBind, BindingBearsLoad, NotYetTaught. Undo U, hold R reset, H hint tiers, F3 developer skip (F3 skips the film and farewell). Walking out of the floor zone is refused with the NotYetTaught nudge, see section 13.

## 9. Beats, memory films and codex

| Beat | What happens |
|---|---|
| Title | "The Lingering Shadow", 3 s |
| Intro | `dialogue.anpu.teach-3-lingering-shadow.intro.1..3` |
| Nudges | `...nudge.<Outcome>` |
| Solve | `...solve` "The shadow stays though its owner has gone. You will need this again." |
| Film | `flick_f3_shadow` |
| Farewell | `FarewellDoor.Open()` (painted plate fades up 1.6 s), Anpu `...farewell.1` "The next door is yours alone, Ankhu. I cannot walk through it with you.", `...farewell.2` "Go in alone, and come out to me. I will be waiting on the other side." During the last line the leaves close (`door.Close`, at least 3 s), 1.4 s hold |
| Exit | `SceneFader.FadeTo("BorrowedDawn_Painted", 2, 1)` |

No codex writes.

## 10. SFX and audio

Anpu intro, nudges and solve are recorded. **Farewell 1 and 2 have no recordings** (no `dialogue.anpu.teach-3-lingering-shadow.farewell.*` clip on disk), so they play silent. The closing doors have no SFX. No SFX for the water, threads, Bearer walking, shadow, the beam reaching the seal, footsteps or ambience.

## 11. Exit conditions

`SunAtGate && ShadowPresent`. Then solve line, `flick_f3_shadow`, farewell plate and lines, doors close, fade to `PlayOrder.BorrowedDawnScene()` = `BorrowedDawn_Painted`.

## 12. Acceptance criteria

- AC-1 [auto] Start state: water West, Floor, Elevator and Bridge bound, mirror in the socket, Bearer asleep, shadow present, sun not at the gate, not solved.
- AC-2 [auto] `bind shadow` with two threads held is refused with "Both bindings are occupied".
- AC-3 [auto] `release elevator` with water West is refused (load-bearing), `release bridge` is accepted.
- AC-4 [auto] Waking the Bearer with the shadow unbound removes the shadow. `bind shadow` is then refused with "A shadow must fall on the seal". Sleeping the Bearer restores it.
- AC-5 [auto] Binding the shadow, waking the Bearer and flowing east solves the room. Neither sun nor shadow alone solves it.
- AC-6 [auto] Untaught words (`go to lift`, `take mirror`) are Rejected with "Those words have no power here yet."
- AC-7 [auto] The room exits to `BorrowedDawn_Painted` after film `flick_f3_shadow` and the farewell.
- AC-8 [manual] The farewell plate appears, both lines are voiced, and the doors close as the last line ends.
- AC-9 [manual] The bound shadow visibly stays on the seal while the Bearer walks away.
- AC-10 [manual] Each interaction has an audible SFX.

## 13. Code vs intent disagreements

| # | Where | Severity | Finding | Proposed fix |
|---|---|---|---|---|
| 1 | Borrowed Dawn kit (no audio) | major | No SFX for any interaction or the farewell doors | As Teach1 finding 1, plus a door-closing clip in `FarewellDoor.Close` |
| 2 | D:\TheAfterlifeofAnkhu\Assets\Ankhu\Bindings\TeachingRoomDirector.cs:205-209 | minor | `FarewellLines` have no voice clips (`dialogue.anpu.teach-3-lingering-shadow.farewell.1/2` are absent), so Anpu's emotional parting is silent while every other teaching line is voiced | Record both lines with the Anpu ElevenLabs voice |
| 3 | D:\TheAfterlifeofAnkhu\Packages\com.ankhu.borrowed-dawn\Runtime\TeachingRooms.cs:71-72 with BorrowedDawnRulesInstaller.cs:146-162 | minor | `MoveTo` is not in the vocabulary, but the kit is unsealed. After `release elevator` (water East) and `flow west`, the lift comes down and Ankhu can step on. The installer offers the move, the rules say NotYetTaught, Ankhu is teleported back and Anpu says "That word has no power here yet..." about walking, which reads as a bug | Allow `MoveTo` in this room (the goal does not depend on location), or add a lesson seal around the floor |
| 4 | D:\TheAfterlifeofAnkhu\Packages\com.ankhu.borrowed-dawn\Runtime\RoomRules.cs:263-265 | minor | `bind shadow` checks capacity before shadow presence, so with two threads held and the Bearer awake the player hears "Both bindings are occupied" even though there is no shadow. The NoShadowToBind nudge only appears after freeing a thread. Acceptable, but the nudge for the real problem is delayed | Accept, or check `ShadowPresent` first for the Shadow target |
| 5 | D:\TheAfterlifeofAnkhu\Assets\Ankhu\Bindings\TeachingRoomDirector.cs:110-113 | minor | F3 developer skip in this room bypasses `FarewellThenLoad` (and the film), so a tester using F3 never sees the farewell | Route F3 through the same exit coroutine with short waits |
| 6 | D:\TheAfterlifeofAnkhu\docs\PLAY_ORDER.md row 7 | minor | Names `Teach3_LingeringShadow`, but the painted twin plays | Update the table |
| 7 | Floor E prompts (`install_mirror`) | minor | The mirror is already socketed. E at the socket is NotYetTaught and plays the nudge | Hide prompts the room does not allow |

No softlock (solver verified).

## 14. E2E script

Engine `bd`, grammar as described in 05_Teach1_Cistern_Painted.md section 14.

```e2e canonical
scene Teach3_LingeringShadow_Painted
engine bd
expect Water=West                        [AC-1]
expect Location=Floor                    [AC-1]
expect Bindings=Elevator, Bridge         [AC-1]
expect Mirror=Socket                     [AC-1]
expect BearerAwake=False                 [AC-1]
expect ShadowPresent=True                [AC-1]
expect SunAtGate=False                   [AC-1]
unsolved                                 [AC-1]
zone floor
say release bridge => Success            [AC-3]
expect Bindings=Elevator
say bind shadow => Success
say wake bearer => Success | Its shadow remains.
expect ShadowPresent=True
unsolved                                 [AC-5]
say flow east => Success                 [AC-5]
expect SunAtGate=True                    [AC-5]
solved                                   [AC-5]
film flick_f3_shadow                     [AC-7]
exit BorrowedDawn_Painted                [AC-7]
```

```e2e alt-1
# The solver's order: water first, then the elevator thread is safe to drop.
scene Teach3_LingeringShadow_Painted
engine bd
zone floor
say flow east => Success
unsolved
say release elevator => Success          [AC-3]
say bind the shadow => Success
say awaken statue => Success
solved
exit BorrowedDawn_Painted
```

```e2e fail-1
scene Teach3_LingeringShadow_Painted
engine bd
zone floor
say bind shadow => Rejected | Both bindings are occupied        [AC-2]
say release elevator => Rejected | hangs on the binding alone    [AC-3]
expect Bindings=Elevator, Bridge
say go to lift => Rejected | Those words have no power here yet.   [AC-6]
say take mirror => Rejected | Those words have no power here yet.  [AC-6]
say wake bearer => Success | Its shadow follows
expect ShadowPresent=False               [AC-4]
say wake bearer => NoChange | already in its alcove
say release bridge => Success
say bind shadow => Rejected | A shadow must fall on the seal    [AC-4]
say flow east => Success
expect SunAtGate=True
unsolved                                 [AC-5]
say sleep bearer => Success
expect ShadowPresent=True                [AC-4]
expect SunAtGate=False
unsolved                                 [AC-5]
say bind shadow => Success
say wake bearer => Success
solved
```
