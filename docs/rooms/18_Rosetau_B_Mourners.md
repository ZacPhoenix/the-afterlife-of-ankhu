# 18 Rosetau_B_Mourners: The Room of the Mourners

## 1. Summary

| Field | Value |
|---|---|
| Scene | `Rosetau_B_Mourners` |
| Realm | Rosetau |
| Title | The Room of the Mourners |
| Play order | 18 |
| Previous | 17 `Rosetau_A_Laugh` |
| Next | 19 `Rosetau_C_Mother` |
| Script | D:\TheAfterlifeofAnkhu\Assets\Ankhu\Realms\Core\Content\RosetauScripts.cs, `BuildMournersB()` (lines 239 to 371) |
| Scene builder | D:\TheAfterlifeofAnkhu\Assets\Ankhu\Realms\Editor\RosetauBuilder.cs, `BuildMournersB()` (lines 369 to 495) |
| Scene file | D:\TheAfterlifeofAnkhu\Assets\Ankhu\Realms\Generated\Scenes\Rosetau_B_Mourners.unity |
| Design | D:\TheAfterlifeofAnkhu\docs\ROSETAU_DESIGN.md, row Rosetau_B_Mourners |

## 2. Intent and story beat

Ausar lies wrapped on a lion bier. Auset at his head and Nebet-Het at his feet are gagged with linen tied in Ankhu's own embalmer's knot. The room confronts the boy who hushed his mother every night: `still` on a mourner is refused ("She is not loud for you. She is loud for him.") and plays the film `ros_hush`. The fix is the reverse of hushing: `release` each band, then `echo` to answer the two voices, as two priestesses sang at a burial. While both keen and are answered the lamps catch one by one. A lone keener runs out of breath. On the solve Ankhu folds the two bands into his belt (they are used in Room E) and the film `ros_leaving` tells how he left his mother for the temple at Abdju: "It was also somewhere quiet."

## 3. Layout and zones

One fixed camera `cam_hall` covering x -10 to 10.

| Zone id | Centre x | Width | What stands there | Reaches |
|---|---|---|---|---|
| `wall` | -6.8 | 2.6 | Wall text, the Lamentations refrain | `read_wall` usable |
| `head` | -2.5 | 2.6 | Auset at the bier's head | `auset` |
| `feet` | 2.5 | 2.6 | Nebet-Het at the bier's feet | `nebethet` |
| `door` | 8.0 | 2.6 | Sealed far door | `door` |

Ankhu spawns at x -8.4 (outside every zone, left of `wall`). Lament, lamps and band are reachable from anywhere. Saved scene zone ids (checked 2026-11-26): door, feet, wall, head, plus one camera zone. They match.

Anpu is spawned at x -9.2 as an active `RealmFollower` (builder line 478). See 13.2.

## 4. Elements

### Room objects

| Id | Name | Key aliases | Initial | States | Zones |
|---|---|---|---|---|---|
| `auset` | Auset | auset, isis, aset, mourner, first mourner, woman, sister, kite, her, mourner at the head | bound | bound, keening, quiet | head |
| `nebethet` | Nebet-Het | nebet-het, nebet het, nebethet, nephthys, mourner, second mourner, woman, sister, kite, her, mourner at the feet | bound | bound, keening, quiet | feet |
| `band` | linen band | band, bands, linen, gag, knot, wrapping, bandage | tied | tied | any |
| `lament` | lament | lament, keen, keening, mourning, wail, wailing, voices, song, cry | unanswered | unanswered, answered | any |
| `lamps` | lamps | lamp, lamps, light | 0 | 0 to 4 | any |
| `door` | door | door, far door, doorway, seal | sealed | sealed, open | door |
| `breath_a`, `breath_b` | (internal) | | 0 | 0 to 2 | |
| `full` | (internal) | | no | no, yes | |

### World clock (`room.Tick`, lines 274 to 291)

- Both keening: both breaths reset to 0. If lament is answered and lamps < 4, lamps +1.
- Otherwise each keener alone gains one breath. At 3 she falls `quiet`, breath 0, and the lament reverts to `unanswered`.

So a lone keener falls quiet on the third tick counting the tick of her own release, which is two further ticks.

### Rules

| Verb | Target | From | To | Requires | Event | Success | Refusal |
|---|---|---|---|---|---|---|---|
| release | auset / nebethet | bound | keening | | `released` | "Weha. The knot gives. She draws a long breath and keens." | default |
| release | auset / nebethet | quiet | keening | | `rekeen` | "She lifts her head and keens again." | default |
| still | auset / nebethet | never | never | | | | "She is not loud for you. She is loud for him." + `hush_refused` |
| wake | auset / nebethet | quiet | keening | | `rekeen` | "Res. She lifts her head and keens again." | "She is not asleep. She is grieving." |
| release | band | never | never | | | | "Two bands, two women. Stand by the one you mean and name her." |
| echo | lament | unanswered | answered | auset keening, nebethet keening | `lament_answered` | "Wesheb. You answer them, and the two voices answer each other over the bier." | "One voice alone. Her sister has not answered her yet." |
| auto | full | | yes | lamps 4 | `lamps_full` | | |
| open | door | sealed | open | lamps 4 | `door_open` | "Wen. The seal on the door breaks clean." | "The door is sealed, and the room is dark. Not yet." |

Goal: `door = open`.

### Usables

| Id | Zone | Prompt | When |
|---|---|---|---|
| `read_wall` | wall | Read the wall | always |

## 5. Words

| Kind | Words |
|---|---|
| Known on entry | open, wake, still, release, echo, give |
| Codex on entry | word.release, word.echo, word.still |
| Learned in room | none |
| Room clock | wait, watch, rest |

Messages:

| Situation | Judgement | Message | Ticks |
|---|---|---|---|
| A verb not in the set (`bind`, `seal`, `remember`) | UnknownVerb | "You know this word exists, but your mouth cannot shape it yet." | no |
| `release auset` from `feet` | OutOfReach | "The Auset is too far. Come closer." | no |
| `release mourner` from outside head and feet | OutOfReach | "No mourner is close enough. Walk to the one you mean." | no |
| `release auset` while keening | NoChange | "The Auset is already keening." | yes |
| `still auset` | Refused | "She is not loud for you. She is loud for him." first time plays `ros_hush` | yes |
| `echo` with fewer than two keening | Refused | "One voice alone. Her sister has not answered her yet." | yes |
| `echo` when already answered | NoChange | "The lament is already answered." | yes |
| `open door` before four lamps | Refused | "The door is sealed, and the room is dark. Not yet." | yes |
| `open door` once open | NoChange | "The door is already open." (intended "This time, nothing has changed.", see 13.1 in guide 17) | yes |
| `echo auset` | InvalidTarget | "The Auset does not answer to "echo"." (13.5) | no |

## 6. Canonical solution

Start: beat `arrive` (title, caption, two Ankhu lines about the knot).

| # | Zone | Input | Result | State after | Beats |
|---|---|---|---|---|---|
| 1 | wall | E on `read_wall` | Use | | `read_wall` |
| 2 | head | `still auset` | Refused | unchanged | `hush` (film `ros_hush`, codex `memory.hush`) |
| 3 | head | `release auset` | Success | auset keening, breath_a 1 | `released` |
| 4 | feet | `release nebethet` | Success | both keening, breaths 0 | |
| 5 | feet | `echo lament` | Success | lament answered, lamps 1 | `answered` |
| 6 | feet | `wait` | Success | lamps 2 | |
| 7 | feet | `wait` | Success | lamps 3 | |
| 8 | feet | `wait` | Success | lamps 4, full yes | `full` |
| 9 | door | `open door` | Success | door open, solved | `solved`: film `ros_leaving`, codex `memory.leaving`, Next |

Step 2 is optional for the goal but is the room's main memory. See 13.3.

## 7. Alternate valid orders

- Release Nebet-Het first, then Auset.
- `wake` instead of `release` on a mourner who has fallen quiet.
- Any ticking action instead of `wait` grows the lamps (for example `open door` at the door is refused but ticks).
- A lone keener who falls quiet can be released again. The lament reverts to unanswered when she does, so echo again once both keen.
- `hush` can be triggered on either mourner, at any time, as long as Ankhu stands in her zone.

Orders that fail: echo before both keen. Releasing one, then spending two ticks elsewhere before releasing the other.

## 8. Failure, refusal and reset behaviour

- No reset. Lone keeners decay to `quiet` and can be restarted.
- Failure beats: `hush` (once, on `hush_refused`). `quiet` fires once on the first `rekeen`: "She can't keep it up alone. Nobody can."
- Idle `murmur` after 45 s (once): "Two women, two bands. And nobody answering them."
- Hints from `room.Hint()`:
  - both bound: "Their mouths are bound with linen. You tied knots like that every day."
  - lament unanswered, lamps 0 to 3: "Both must keen at once, and someone must answer them."
  - answered, lamps 0 to 3: "Let them keen. The lamps are catching."
  - lamps 4: "The door at the far end."

## 9. Beats, memory films and codex

| Beat id | Trigger | Once | Steps |
|---|---|---|---|
| `arrive` | Start | yes | Freeze, Title, Caption, two Ankhu lines, Free |
| `read_wall` | Use `read_wall` | no | Caption "As it is written: "Come to your house, come to your house."", Ankhu line |
| `hush` | Event `hush_refused` | yes | Freeze, caption, `flashback:ros_hush`, Ankhu line, Write `memory.hush`, Free |
| `released` | Event `released` | yes | Caption, band falls, she keens |
| `quiet` | Event `rekeen` | yes | Ankhu "She can't keep it up alone. Nobody can." |
| `answered` | Event `lament_answered` | yes | Caption two voices, Ankhu line |
| `full` | Event `lamps_full` | yes | Caption four lamps, Ankhu line |
| `murmur` | Idle 45 s | yes | Ankhu line |
| `solved` | Solved | yes | Freeze, caption (he folds the bands into his belt), `flashback:ros_leaving`, two Ankhu lines, Write `memory.leaving`, Wait 1.5, Next |

Films: D:\TheAfterlifeofAnkhu\Assets\StreamingAssets\Memories\ros_hush.mp4 and ros_leaving.mp4 (both present). Codex entries `memory.hush`, `memory.leaving` exist.

## 10. SFX and audio

| Source | Status |
|---|---|
| Ambience walk_ambience.wav | assigned |
| Spell cast, land, fail | null in the saved scene (see 13.5 in guide 17) |
| Keening (the room's core sound) | none. Only the `Voice` streak glow |
| Knot release, lamp catching, door seal breaking | none |
| Spell voice | `word.still` exists. No word.release, word.echo, word.open is generic |
| Dialogue | no rosetau_b clips recorded |

## 11. Exit conditions

Goal `door = open`, which needs lamps 4. `solved` beat ends with Next to `PlayOrder.ExitOf("Rosetau_B_Mourners")` = `Rosetau_C_Mother`.

## 12. Acceptance criteria

- AC-1 [auto] Loads with both mourners bound, lament unanswered, lamps 0, door sealed, unsolved.
- AC-2 [auto] `arrive` fires on start. `read_wall` is offered in `wall` and fires its beat.
- AC-3 [auto] `still` on a mourner is Refused with "She is not loud for you." and the first one plays film `ros_hush` and writes `memory.hush`.
- AC-4 [auto] `release` at a mourner's side sets her keening and fires `released` once.
- AC-5 [auto] `echo` with only one keener is Refused with "One voice alone."
- AC-6 [auto] With both keening, `echo` answers the lament and lights the first lamp.
- AC-7 [auto] While both keen and are answered, each tick lights one more lamp, up to 4, and the fourth fires `full`.
- AC-8 [auto] A lone keener falls quiet after two ticks beyond her release tick, and `release` or `wake` restarts her (firing `quiet` once).
- AC-9 [auto] `open door` before four lamps is Refused with "Not yet." and from outside `door` is OutOfReach.
- AC-10 [auto] `open door` with four lamps solves the room, plays `ros_leaving`, writes `memory.leaving` and exits to `Rosetau_C_Mother`.
- AC-11 [auto] A word already spent (release on a mourner who already keens) answers "This time, nothing has changed."
- AC-12 [manual] Anpu is not in this room, or stays at the threshold, matching his line in Room A.
- AC-13 [manual] Each mourner's keen is audible, the lamps visibly catch one per tick, the door breaks open with a sound.
- AC-14 [manual] The bound, keening and quiet cards read clearly at gameplay scale.

## 13. Code vs intent disagreements

| # | Severity | Where | Problem | Proposed fix |
|---|---|---|---|---|
| 13.1 | major | D:\TheAfterlifeofAnkhu\Assets\Ankhu\Realms\Runtime\Director\RealmSceneDirector.cs (no `room.Advance()`) | Design: "one lamp lights each tick". The hint "Let them keen. The lamps are catching." invites waiting, but standing still lights nothing. The player must type `wait` three times, which no screen text teaches | Real-time advance in Tick rooms while idle, or teach `wait` (caption in `answered` beat: "Wait with them.") |
| 13.2 | major | D:\TheAfterlifeofAnkhu\Assets\Ankhu\Realms\Editor\RosetauBuilder.cs:478 | Anpu is active and following Ankhu here, right after saying in Room A he would wait on the step. C and D deactivate him | `ctx.anpu.gameObject.SetActive(false)` in `BuildMournersB`, as in C and D |
| 13.3 | minor | RosetauScripts.cs beat `hush` | The room's core memory (`ros_hush`, the boy hushing his mother) only plays if the player tries `still`. A player who reads the hints goes straight to release and never sees it, and Room C's "I did that for a year" then lands without setup | Fire `hush` from the first `release` too if not yet fired (`ForbidsFlags` guard), or make the first hint "You want to quiet them. Try it." |
| 13.4 | minor | RosetauScripts.cs:347 | `released` is once, so the second mourner's release has no caption | Set `Once = false` or add a second-release caption |
| 13.5 | minor | RosetauScripts.cs lament aliases | `echo auset`, `echo nebet-het`, `echo them`, `echo mourners` are natural phrasings and fail (InvalidTarget or "There is no "mourners" here") | Add echo rules on auset and nebethet that forward to the lament, and aliases "mourners", "them", "women", "sisters" |
| 13.6 | minor | RosetauScripts.cs:310 | "One voice alone" is shown even when nobody keens | Split into two rules: none keening ("Nobody is keening yet.") and one keening |
| 13.7 | minor | D:\TheAfterlifeofAnkhu\Assets\Ankhu\Realms\Core\RealmRules.cs:445 and OutOfReach line 437 | Messages read "The Auset is too far", "The Nebet-Het is already keening" | Drop the article for objects whose Name is capitalised |
| 13.8 | minor | Tick | A keener falling quiet has no event, caption or sound, so the player does not learn why the lamps stopped | Raise an event `fell_quiet` in Tick (via auto rule on breath) with a caption |
| 13.9 | minor | D:\TheAfterlifeofAnkhu\Assets\Ankhu\Realms\Core\Lexicon.cs:122 | Success text and codex say "Weha" but the alias is `wekha`. Typing `weha` is NotUnderstood | Add `weha` alias |

## 14. E2E script

```e2e canonical
scene Rosetau_B_Mourners
beat arrive [AC-2]
expect auset=bound [AC-1]
expect nebethet=bound [AC-1]
expect lament=unanswered [AC-1]
expect lamps=0 [AC-1]
expect door=sealed [AC-1]
unsolved [AC-1]
at wall
use read_wall [AC-2]
beat read_wall [AC-2]
at head
say still auset => Refused | not loud for you [AC-3]
beat hush [AC-3]
film ros_hush [AC-3]
codexhas memory.hush [AC-3]
say release auset => Success [AC-4]
expect auset=keening [AC-4]
beat released [AC-4]
say echo => Refused | One voice alone [AC-5]
at feet
say release nebethet => Success [AC-4]
expect nebethet=keening
expect auset=keening
say echo lament => Success [AC-6]
expect lament=answered [AC-6]
expect lamps=1 [AC-6]
beat answered [AC-6]
wait
expect lamps=2 [AC-7]
wait
expect lamps=3 [AC-7]
wait
expect lamps=4 [AC-7]
beat full [AC-7]
say open door => OutOfReach [AC-9]
at door
say open door => Success [AC-10]
expect door=open [AC-10]
solved [AC-10]
beat solved [AC-10]
film ros_leaving [AC-10]
codexhas memory.leaving [AC-10]
exit Rosetau_C_Mother [AC-10]
```

```e2e fail-1
# A lone keener runs out of breath, is restarted, and the door refuses early.
scene Rosetau_B_Mourners
at head
say release auset => Success
expect breath_a=1
wait
expect auset=keening
wait
expect auset=quiet [AC-8]
expect lament=unanswered [AC-8]
say release auset => Success [AC-8]
beat quiet [AC-8]
expect auset=keening
at feet
say wake nebethet => Refused | not asleep
say release nebethet => Success
say release nebethet => NoChange | This time, nothing has changed. [AC-11]
# fixed 2026-11-26, this used to fail: message is "The Nebet-Het is already keening." See guide 17, 13.1
expect lamps=0
say echo => Success
expect lamps=1
at door
say open door => Refused | Not yet [AC-9]
expect lamps=2
say open door => Refused | Not yet
expect lamps=3
say open door => Refused | Not yet
expect lamps=4
say open door => Success
solved
exit Rosetau_C_Mother
```
