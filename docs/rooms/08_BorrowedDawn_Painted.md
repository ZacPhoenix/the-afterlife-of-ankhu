# 08 BorrowedDawn_Painted: The Borrowed Dawn

## 1. Summary

| Field | Value |
|---|---|
| Scene | `BorrowedDawn_Painted` (`PlayOrder.BorrowedDawnScene()` picks it when in Build Settings, else `BorrowedDawn_Greybox`) |
| Realm | The first true challenge, walked alone |
| Play order | 8 of 21 (chain slot `@BorrowedDawn`) |
| Previous scene | `Teach3_LingeringShadow_Painted` (7) |
| Next scene | `Wernes_Terrace` (9) |
| Engine | Borrowed Dawn rules, `RoomDefinition.BorrowedDawn(RuleProfile.Greybox)` (id `borrowed-dawn`, every command allowed, goal `Won`) via `BorrowedDawnRulesAdapter`. No `TeachingRoomDirector`. `BorrowedDawnExit` is added at runtime |
| Rules | D:\TheAfterlifeofAnkhu\Packages\com.ankhu.borrowed-dawn\Runtime\RoomRules.cs, RoomState.cs, SpellParser.cs, RoomDefinition.cs |
| Unity glue | D:\TheAfterlifeofAnkhu\Assets\Ankhu\Bindings\BorrowedDawnRulesInstaller.cs, BorrowedDawnRulesAdapter.cs, BorrowedDawnExit.cs, MemoryFlicker.cs |
| Scene build | Greybox from D:\TheAfterlifeofAnkhu\Assets\Ankhu\Editor\BorrowedDawnGreyboxBuilder.cs, painted copy by D:\TheAfterlifeofAnkhu\Assets\Ankhu\Editor\Environment\BorrowedDawnPainted.cs (`BuildPaintedCli`). Rebuilding the greybox requires rebuilding the painted scene |
| Solver | D:\TheAfterlifeofAnkhu\dotnet\borrowed-dawn\ANALYSIS_GREYBOX.md: shortest 23, 107,254 distinct shortest, 0 softlocks, authored solution 24 steps replays to won |

## 2. Intent and story beat

Every mechanic of the pavilion at once: water and the lift, a load-bearing binding, the lingering shadow, the two-thread limit, a remembered bell and echo, and a Name discovered on a wall. The gate is "He Who Returns the Morning" (framed by Book of the Dead spell 92, the spell for opening the tomb so the ba and the shadow may go out by day). To open it, sun, shadow and sound must all be present, and Ankhu must speak its name from the threshold.

Ankhu walks it **alone** (Anpu said goodbye at the closing doors). On the solve the film `mem0_dawn_name` (Memory 0) plays: the dawn Hori was born, Neferet on the mat, Ankhu lifting the child into the sun and saying "Hori". Then Anpu, waiting at the exit, greets him ("You went in alone, and you came out to me.") and leads him down to Wernes.

Playtest (Zac 2026-11-21): solved with hints, liked, too tough for onboarding. The teaching rooms exist because of that.

## 3. Layout and zones

Side-on two-tier chamber. Ground tier west to east: water selector, sun socket, Bearer on its pedestal in the beam, near shadow seal. The elevator shaft rises between floor and gallery. Upper tier: gallery (inscription wall, ladder hatch) and, joined by an open walkway (Greybox), the bell landing. A pendulum bridge spans from the landing to the gate ledge.

| Zone | Location | Contents | Reach rules (Greybox) |
|---|---|---|---|
| `floor` | Floor | socket, Bearer, near seal, selector, ladder foot. Spawn | to Lift only when the lift is low (water West, unbound). To Gallery only by the lowered ladder |
| `lift` | Lift | the platform. The raft and mirror are reached from here | to Gallery only when high, to Floor only when low |
| `gallery` | Gallery | inscription wall, ladder hatch | to Landing always (open walkway), to Lift when high, to Floor by ladder |
| `landing` | Landing | the bell | to Bridge or Gate only when the bridge is bound |
| `bridge` | Bridge | pendulum span | only while bound |
| `gate` | Gate | threshold | only while the bridge is bound |

## 4. Elements

| Element | Rules field | Start | Accepted by | Notes |
|---|---|---|---|---|
| Water | `Water` | East | `flow west|w|east|e` | |
| Elevator | `ElevatorHigh` (East or bound) | high | water, `bind/release elevator|lift` | Release refused while water West (load-bearing) |
| Raft and light screen | `RaftRaised`, `LightScreenRaised` (water West) | low | water | Raised screen blocks the far beam |
| Sun mirror | `Mirror` Raft, Carried, Socket | Raft | `take|pick up|carry|grab|lift|collect mirror` from the Lift with water West and the lift high. `install|place|put|set|insert|mount mirror [in socket]` on the Floor while carried. E `take_mirror`, `install_mirror` | |
| Bearer | `BearerAwake` | false | `wake|awaken bearer|statue`, `sleep bearer` | |
| Shadow on the near seal | `ShadowPresent` = socket and (asleep or shadow bound) | false | `bind shadow` | |
| Sun at the gate | `SunAtGate` = socket, awake, East | false | | |
| Ladder hatch | `LadderLowered` | false | `lower ladder|hatch` in the Gallery, E `lower_ladder` | Stays down |
| Inscription | `NameDiscovered` | false | `read inscription|inscriptions|gallery` in the Gallery, E `read_inscription` | Reveals the Name |
| Bell | `BellRepaired`, `BellStruck`, `EchoActive` | false | `recall bell` (Landing), `strike|ring bell` (Landing, repaired), E `strike_bell`, `echo bell` (anywhere, once struck) | Echo uses no thread |
| Pendulum bridge | Bindings Bridge | swinging | `bind bridge|pendulum` from the Landing, `cross bridge`. Release refused while standing on the bridge or gate | |
| Threads | `Bindings`, capacity 2 | none | | |
| Gate | `Won` | shut | the Name ("he who returns the morning", optionally "speak" or "say" first) at the Gate with sun, shadow and echo present | |

## 5. Words

All commands are allowed (no NotYetTaught here).

| Situation | Outcome | Adapter | Message |
|---|---|---|---|
| Already in that state | AlreadyInState | NoChange | specific, for example "The bell is already whole.", "The ladder already reaches the floor.", re-reading gives the Name again |
| Thread already held / not bound | BindingAlreadyHeld / NotBound | Rejected | "That binding is already held." / "That target is not bound." |
| Speaking the Name before reading it | NameUnknown | Rejected | "The chamber does not answer to "he"." (deliberately the same as an unknown word, so a guessed Name is never confirmed) |
| Name at the gate but sun, shadow or echo missing | GateConditionsUnmet | Rejected | "The gate hears you, but its sun, shadow, and sound must all be present." |
| Name away from the gate | WrongLocation | Rejected | "Speak the gate's name from its threshold." |
| Unknown word | parse | NotUnderstood | "The chamber does not answer to "x"." (+ one suggestion) |
| After the win | PuzzleComplete | Rejected | "The morning has returned. Restart to play again." |

The realm-room phrases ("your mouth cannot shape it yet", "This time, nothing has changed.") are not used.

## 6. Canonical solution

A 23-step shortest solution (accepted commands, including moves). Walking into a zone is a move.

| # | Zone | Action | Result |
|---|---|---|---|
| 1 | floor | `flow west` | lift sinks, raft rises |
| 2 | to lift | walk onto the platform | |
| 3 | lift | `flow east` | lift rises to gallery height |
| 4 | lift | `bind elevator` | thread 1 of 2 |
| 5 | lift | `flow west` | raft rises, lift held high |
| 6 | lift | `take mirror` | mirror Carried |
| 7 | to gallery | walk off | |
| 8 | gallery | `lower ladder` | ladder down |
| 9 | gallery | `read inscription` | Name discovered |
| 10 | to floor | climb down | |
| 11 | floor | `install mirror` | mirror in the socket, Bearer casts a shadow on the seal |
| 12 | floor | `bind shadow` | thread 2 of 2 |
| 13 | floor | `wake bearer` | "Its shadow remains." |
| 14 | floor | `flow east` | sun reaches the gate (screen down) |
| 15 | floor | `release elevator` | now safe, water East. Thread 1 of 2 |
| 16 | to gallery | climb the ladder | |
| 17 | to landing | walk east | |
| 18 | landing | `recall bell` | bell whole |
| 19 | landing | `strike bell` | note held |
| 20 | landing | `echo bell` | echo active |
| 21 | landing | `bind bridge` | thread 2 of 2, bridge level |
| 22 | to gate | cross | |
| 23 | gate | `he who returns the morning` | Won, gate opens |
| 24 | | automatic | 2.5 s, film `mem0_dawn_name`, four exit lines (Anpu, Ankhu, Anpu, Anpu, 6 s each), fade to `Wernes_Terrace` |

## 7. Alternate valid orders

107,254 distinct shortest solutions. The fixed dependencies are:
- The mirror needs the lift bound high with water West, so the elevator binding comes first and is released only after the water returns East (load-bearing).
- The mirror reaches the floor only by the ladder, so `lower ladder` precedes `install mirror`.
- `bind shadow` must precede `wake bearer` (or sleep, bind, wake again).
- Only two threads: the shadow and the bridge must be held at the end, so the elevator thread must be released before `bind bridge`.
- `read inscription` can happen any time Ankhu is in the gallery. `recall`, `strike`, `echo` can happen any time after the landing is reached, in that order. `echo bell` can be spoken from anywhere after the strike.
- E interactions replace typed `take mirror`, `install mirror`, `lower ladder`, `read inscription`, `strike bell`.
- The bridge can be crossed in two hops (`bridge` then `gate` zones).

## 8. Failure, refusal and reset behaviour

No Anpu nudges (he is absent). H gives three tiers from a breadth-first search of the rules (nudge lists what the gate still lacks: sun, shadow, sound, a name). If no path remains, H says "Undo with U, or hold R to begin again." The solver reports 0 softlocks, so that branch should not occur. Undo U and reset R act on the session, and the installer moves Ankhu to the restored zone's anchor. A refused walk teleports him back to the last accepted spot.

## 9. Beats, memory films and codex

| Step | What happens |
|---|---|
| On load | `BorrowedDawnExit.Install` adds the exit component to the installer's object (not a teaching room) |
| Gate opens (`GateOpen`) | wait 2.5 s (realtime) |
| Memory 0 | `MemoryFlicker.PlayForRoom("borrowed-dawn")` plays `mem0_dawn_name.mp4` via MemoryFlashback. Fallback painted still `memory0_dawn` with captions if the film or player is missing |
| Exit lines | Anpu "You went in alone, and you came out to me...", Ankhu "There was a dawn in there...", Anpu "Then you remember more than you did at the gate...", Anpu "Come. Below us lies Wernes..." Keys `dialogue.anpu.borrowed-dawn.exit.1..3` |
| Leave | `SceneFader.FadeTo(PlayOrder.ExitOf("BorrowedDawn_Painted"))` = `Wernes_Terrace`, 2.5 s fade |

No codex writes (the codex belongs to the realm engine).

## 10. SFX and audio

Ankhu's spell voice (`BorrowedDawnSpellVoice`) and the film audio. **The three Anpu exit lines have no recordings**. **No SFX anywhere**: water, lift, raft, mirror, ladder unrolling, inscription glowing, bell recall, the bell strike itself, the echo, the bridge locking, the gate opening, footsteps, ambience. A bell puzzle with a silent bell is the most visible gap in the game.

## 11. Exit conditions

`Won` (the Name spoken at the Gate with `SunAtGate`, `ShadowPresent`, `EchoActive`, `NameDiscovered`). Then Memory 0 and the exit lines, then `Wernes_Terrace`.

## 12. Acceptance criteria

- AC-1 [auto] Start state: water East, Floor, nothing bound, mirror on the raft, ladder up, nothing read, bell broken, not won.
- AC-2 [auto] The 23-step canonical solution is accepted step by step with the expected state after each step, and ends Won.
- AC-3 [auto] The elevator binding cannot be released while the water is West.
- AC-4 [auto] The mirror can only be taken from the raised, held lift with water West, and only installed on the floor while carried.
- AC-5 [auto] Speaking the Name before reading the inscription is refused without confirming the Name ("does not answer to").
- AC-6 [auto] Speaking the Name away from the threshold is refused.
- AC-7 [auto] Walking onto the lift while it is high, or off it at a height with no landing, is refused.
- AC-8 [auto] After the win the scene plays `mem0_dawn_name` and leaves for `Wernes_Terrace`.
- AC-9 [manual] Every state change is visible: lift, raft, mirror, ladder, Bearer walk, shadow, beam at the gate, bell gold, bridge level, gate opening.
- AC-10 [manual] The bell strike and echo are audible, and every other interaction has SFX.
- AC-11 [manual] Anpu's exit lines are voiced.

## 13. Code vs intent disagreements

| # | Where | Severity | Finding | Proposed fix |
|---|---|---|---|---|
| 1 | Borrowed Dawn kit views (D:\TheAfterlifeofAnkhu\Assets\Ankhu\Runtime\World\BellView.cs and the rest), BorrowedDawnPainted.cs | major | No audio at all. The bell, strike and echo are silent, though the puzzle is about a remembered sound | Author earshot SFX per view (bell strike, echo loop, water, lift, ladder, gate) and drive them from `SnapshotListener` |
| 2 | D:\TheAfterlifeofAnkhu\Assets\Ankhu\Bindings\BorrowedDawnExit.cs:25-31 | minor | Anpu's three exit lines (the reunion) have no recordings (`dialogue.anpu.borrowed-dawn.exit.*` absent), and Ankhu's line has no key at all | Record them. Give Ankhu's line a key and voice it with the Ankhu voice |
| 3 | D:\TheAfterlifeofAnkhu\Packages\com.ankhu.borrowed-dawn\Runtime\RoomRules.cs:140 and PuzzleSession.cs:128 | minor | After the win every command says "The morning has returned. Restart to play again." and hold R still resets the room while the exit film and lines are playing (the gate closes behind Anpu's greeting). In the chain there is no "restart" | Block undo and reset in `BorrowedDawnRulesInstaller` once `GateOpen`, and change the message to a quiet "The gate stands open." |
| 4 | D:\TheAfterlifeofAnkhu\Assets\Ankhu\Bindings\BorrowedDawnRulesAdapter.cs:342-360 | minor | `BindingAlreadyHeld` and `NotBound` map to Rejected while `AlreadyInState` maps to NoChange. Saying "bind bridge" twice is a refusal, not "nothing has changed" | Map both to NoChange |
| 5 | D:\TheAfterlifeofAnkhu\Assets\Ankhu\Bindings\BorrowedDawnRulesAdapter.cs:81-86 | minor | `Speak` first moves the rules to the reported zone. While riding the lift between heights the tracker reports no zone, and any spell typed then is refused with "Ankhu is somewhere the chamber does not recognise." | Fall back to the rules location when the zone is null |
| 6 | Design (DESIGN_FINDINGS.md) | design | The two-thread cap only bites suboptimal play, and the bell trio (recall, strike, echo) is a linear chore. Zac found the room too hard for onboarding | Already addressed by the teaching rooms. Consider folding recall and strike into one E deed |
| 7 | D:\TheAfterlifeofAnkhu\Assets\Ankhu\Tests\PlayMode\GreyboxRulesConnectedTests.cs | minor | Play-mode coverage loads `BorrowedDawn_Greybox`, not `BorrowedDawn_Painted`, and never plays to the win | The E2E blocks below |

No softlock (solver verified for the Greybox profile).

## 14. E2E script

Engine `bd`, grammar as described in 05_Teach1_Cistern_Painted.md section 14.

```e2e canonical
scene BorrowedDawn_Painted
engine bd
expect Water=East                 [AC-1]
expect Location=Floor             [AC-1]
expect Bindings=None              [AC-1]
expect Mirror=Raft                [AC-1]
expect LadderLowered=False        [AC-1]
expect NameDiscovered=False       [AC-1]
expect BellRepaired=False         [AC-1]
expect Won=False                  [AC-1]
zone floor
say flow west => Success          [AC-2]
expect ElevatorHigh=False
zone lift                         [AC-2]
say flow east => Success          [AC-2]
expect ElevatorHigh=True
say bind elevator => Success      [AC-2]
say flow west => Success          [AC-2]
expect ElevatorHigh=True
expect RaftRaised=True
say take mirror => Success        [AC-4]
expect Mirror=Carried
zone gallery                      [AC-2]
say lower ladder => Success       [AC-2]
expect LadderLowered=True
say read inscription => Success   [AC-2]
expect NameDiscovered=True
zone floor                        [AC-2]
say install mirror => Success     [AC-4]
expect Mirror=Socket
expect ShadowPresent=True
say bind shadow => Success        [AC-2]
expect Bindings=Elevator, Shadow
say wake bearer => Success | Its shadow remains.
expect BearerAwake=True
expect ShadowPresent=True
say flow east => Success          [AC-2]
expect SunAtGate=True
say release elevator => Success   [AC-3]
expect Bindings=Shadow
zone gallery
zone landing
say recall bell => Success
say strike bell => Success
say echo bell => Success
expect EchoActive=True
say bind bridge => Success
expect Bindings=Shadow, Bridge
zone gate
say he who returns the morning => Success   [AC-2]
expect Won=True                   [AC-2]
solved                            [AC-2]
film mem0_dawn_name               [AC-8]
exit Wernes_Terrace               [AC-8]
```

```e2e alt-1
# Read and lower first, E deeds instead of words, two-hop crossing, prefixed Name.
scene BorrowedDawn_Painted
engine bd
zone floor
say flow w => Success
zone lift
say flow e => Success
zone gallery
use lower_ladder => Success
use read_inscription => Success
zone lift
say bind lift => Success
say flow west => Success
use take_mirror => Success
zone gallery
zone floor
use install_mirror => Success
say bind shadow => Success
say awaken bearer => Success
say flow east => Success
say unbind elevator => Success
zone gallery
zone landing
say recall bell => Success
use strike_bell => Success
say echo the bell => Success
say bind pendulum => Success
zone bridge
zone gate
say speak he who returns the morning => Success
solved
exit Wernes_Terrace
```

```e2e fail-1
scene BorrowedDawn_Painted
engine bd
zone floor
say he who returns the morning => Rejected | from its threshold      [AC-6]
zone lift => Rejected | The elevator is above you                    [AC-7]
say take mirror => Rejected | The raft is only within reach
say install mirror => Rejected | Carry the mirror
say strike bell => Rejected | repair the bell
say echo bell => Rejected | no clear sound
say read inscription => Rejected | Reach the upper gallery
say release elevator => Rejected | not bound
say bind shadow => Rejected | A shadow must fall on the seal
say bind bridge => Rejected | Reach the bell landing
say bind elevator => Rejected | Stand on the raised elevator
say flow east => NoChange | already in the east basin
say flwo west => NotUnderstood | Did you mean
say flow west => Success
zone lift
say flow east => Success
say bind elevator => Success
say flow west => Success
say release elevator => Rejected | hangs on the binding alone         [AC-3]
expect Bindings=Elevator                                             [AC-3]
say take mirror => Success                                           [AC-4]
say take mirror => Rejected | already collected
zone floor => Rejected | There is no landing at this height          [AC-7]
expect Won=False
```

```e2e fail-2
# The Name is withheld until it is read, and the gate checks every condition.
scene BorrowedDawn_Painted
engine bd
zone floor
say flow west => Success
zone lift
say flow east => Success
say bind elevator => Success
say flow west => Success
say take mirror => Success
zone gallery
say lower ladder => Success
zone floor
say install mirror => Success
say bind shadow => Success
say wake bearer => Success
say flow east => Success
say release elevator => Success
zone gallery
zone landing
say recall bell => Success
say strike bell => Success
say bind bridge => Success
zone gate
say he who returns the morning => Rejected | does not answer to       [AC-5]
expect Won=False
zone landing
zone gallery
say read inscription => Success
zone landing
zone gate
say he who returns the morning => Rejected | sun, shadow, and sound must all be present
say echo bell => Success
say he who returns the morning => Success
solved
```
