# 19 Rosetau_C_Mother: The Mother's Room

## 1. Summary

| Field | Value |
|---|---|
| Scene | `Rosetau_C_Mother` |
| Realm | Rosetau |
| Title | The Mother's Room |
| Play order | 19 |
| Previous | 18 `Rosetau_B_Mourners` |
| Next | 20 `Rosetau_D_TwoWays` |
| Script | D:\TheAfterlifeofAnkhu\Assets\Ankhu\Realms\Core\Content\RosetauScripts.cs, `BuildMotherC()` (lines 373 to 477) |
| Scene builder | D:\TheAfterlifeofAnkhu\Assets\Ankhu\Realms\Editor\RosetauBuilder.cs, `BuildMotherC()` (lines 497 to 622) |
| Scene file | D:\TheAfterlifeofAnkhu\Assets\Ankhu\Realms\Generated\Scenes\Rosetau_C_Mother.unity |
| Design | D:\TheAfterlifeofAnkhu\docs\ROSETAU_DESIGN.md, row Rosetau_C_Mother |

## 2. Intent and story beat

The one-room mud-brick house at Abdju, the year after his father died. His mother sits at the loom with her face in her hands. The boy used to walk out so he would not hear it. `still` is refused ("No. Not again."). Nothing in the room moves while she weeps alone. When Ankhu `echo`es her, weeping out loud with her, she lifts her head: "There you are." His realization: "It was the same grief... Hers was mine with the door open." Then the house comes back to work: `wake` the loom and the quern, and the lamp catches by itself. `remember` her with the lamp lit, and the film `ros_old_mother` shows her old in his and Neferet's house, the loudest woman in the street, dying with thread in her lap, washed and wrapped by her son, "I took my time." A red light under the back door leads to the Two Ways.

## 3. Layout and zones

One camera `cam_house`, x about -8.5 to 8.5.

| Zone id | Centre x | Width | What stands there | Reaches |
|---|---|---|---|---|
| `shelf` | -5.6 | 2.0 | Shelf with the faience of the young Heru and the clay falcon. Ankhu spawns inside it (x -6.8) | `look_bread` usable |
| `loom` | -1.6 | 3.0 | The loom, the mother seated at it | `loom` |
| `quern` | 3.0 | 2.6 | Saddle quern | `quern` |

Mother, weeping and lamp are reachable from anywhere. Saved scene zone ids (checked 2026-11-26): loom, shelf, quern, plus one camera zone. They match. Anpu is deactivated in this scene (builder line 602).

## 4. Elements

### Room objects

| Id | Name | Key aliases | Initial | States | Zones |
|---|---|---|---|---|---|
| `mother` | mother | mother, mum, mama, mut, woman, her, my mother, weeping woman, weaver | weeping | weeping, listening, remembered | any |
| `weeping` | weeping | weeping, crying, grief, sobbing, tears, lament, voice | unanswered | unanswered, answered | any |
| `loom` | loom | loom, weaving, warp, shuttle, cloth, thread | still | still, working | loom |
| `quern` | quern | quern, grindstone, millstone, grain, stone | still | still, grinding | quern |
| `lamp` | lamp | lamp, light, oil lamp, wick | dark | dark, lit | any |

No world clock in this room. `wait` is not a command here (NotUnderstood). This contradicts the script header comment that every Rosetau utterance is a tick, but it is harmless.

### Rules

| Verb | Target | From | To | Requires | Also | Event | Success | Refusal |
|---|---|---|---|---|---|---|---|---|
| still | mother / weeping | never | never | | | | | "No. Not again." + `hush_again` |
| echo | weeping | unanswered | answered | | mother listening | `answered` | "Wesheb. You weep with her, out loud. She lifts her head." | default |
| echo | mother | weeping | listening | | weeping answered | `answered` | same | default |
| wake | loom | still | working | weeping answered | | `loom_on` | "Res. The heddle lifts. The shuttle runs across and back." | "Her hands are over her face. The loom does not move while she weeps alone." + `not_yet` |
| wake | quern | still | grinding | weeping answered | | `quern_on` | "Res. The rubbing stone rocks on the saddle. Flour gathers at the lip." | "The grain lies where she left it. Nothing in this room moves while she weeps alone." + `not_yet` |
| wake | lamp | never | never | | | | | "It is her lamp. She lights it when the work is going." |
| auto | lamp | | lit | loom working, quern grinding | | `lamp_lit` | | |
| remember | mother | listening | remembered | lamp lit | | `remembered` | "Sekha. You remember her." | "Not like this. Not in the dark with her face in her hands." |

Goal: `mother = remembered`.

### Usables

| Id | Zone | Prompt | When |
|---|---|---|---|
| `look_bread` | shelf | Look at the shelf | always |

## 5. Words

| Kind | Words |
|---|---|
| Known on entry | open, wake, still, release, echo, give, remember |
| Codex on entry | word.wake, word.echo, lore.mothers_house |
| Learned in room | none. `remember` is accepted but never taught or written to the codex (13.1) |

Messages:

| Situation | Judgement | Message |
|---|---|---|
| `still mother` | Refused | "No. Not again." then "I did that for a year. I'm not doing it again." |
| `wake loom` before echo | Refused | "Her hands are over her face..." then once "Nothing moves while she's like this. It didn't then either." |
| `wake loom` from outside `loom` | OutOfReach | "The loom is too far. Come closer." |
| `wake lamp` | Refused | "It is her lamp. She lights it when the work is going." |
| `remember mother` before the lamp | Refused | "Not like this. Not in the dark with her face in her hands." |
| bare `echo` | Incomplete | "Echo what? Name the thing." (13.3) |
| `echo her` after answered | NoChange | "The mother is already listening." (intended AlreadySpoken, guide 17 13.1) |
| `bind`, `seal`, `name` | UnknownVerb | "You know this word exists, but your mouth cannot shape it yet." |
| `wait` | NotUnderstood | "The words fall like sand. They are not words of power." |

## 6. Canonical solution

Start: beat `arrive` (title, caption, two Ankhu lines).

| # | Zone | Input | Result | State after | Beats |
|---|---|---|---|---|---|
| 1 | shelf | E on `look_bread` | Use | | `shelf` |
| 2 | shelf | `still her` | Refused | unchanged | `hush_again` |
| 3 | shelf | `echo mother` | Success | mother listening, weeping answered | `answered` ("There you are.") |
| 4 | loom | `wake loom` | Success | loom working | `loom` |
| 5 | quern | `wake quern` | Success | quern grinding, lamp lit | `quern`, `lamp` |
| 6 | quern | `remember mother` | Success | mother remembered, solved | `solved`: film `ros_old_mother`, codex `memory.old_mother`, red light caption, Next |

## 7. Alternate valid orders

- `echo weeping`, `echo her`, `echo crying` all work in place of step 3.
- Quern before loom. The lamp catches on whichever is second.
- `remember her`, `remember my mother` work.

Orders that fail: waking either machine before the echo, remembering before the lamp catches.

## 8. Failure, refusal and reset behaviour

No reset and no clock, so nothing can be undone by the room. Failure beats: `hush_again` (repeatable), `not_yet` (once). Idle `murmur` after 45 s: "I used to walk out of this room. That's all I ever did with it." Hints:

- weeping unanswered: "In the last room, the lamps lit for a lament that was answered."
- answered, lamp dark: "Her loom and her quern. She was never still in her life."
- lamp lit: "Remember her, the way she was."

## 9. Beats, memory films and codex

| Beat id | Trigger | Once | Steps |
|---|---|---|---|
| `arrive` | Start | yes | Freeze, Title, Caption, two Ankhu lines, Free |
| `shelf` | Use `look_bread` | no | Caption faience and clay falcon, "He brought it back." |
| `hush_again` | Event `hush_again` | no | Ankhu "I did that for a year. I'm not doing it again." |
| `not_yet` | Event `not_yet` | yes | Ankhu line |
| `answered` | Event `answered` | yes | Freeze, caption, Ankhu realization, Mother "There you are.", Free |
| `loom` | Event `loom_on` | yes | Mother "Hand me the thread, then, if you're staying." |
| `quern` | Event `quern_on` | yes | Ankhu line |
| `lamp` | Event `lamp_lit` | yes | Caption "The lamp catches by itself." |
| `murmur` | Idle 45 s | yes | Ankhu line |
| `solved` | Solved | yes | Freeze, `flashback:ros_old_mother`, two Ankhu lines, Write `memory.old_mother`, Caption red light, Wait 1.5, Next |

Film present at D:\TheAfterlifeofAnkhu\Assets\StreamingAssets\Memories\ros_old_mother.mp4. Codex `memory.old_mother`, `lore.mothers_house` exist.

## 10. SFX and audio

| Source | Status |
|---|---|
| Ambience walk_ambience.wav | assigned |
| Spell cast, land, fail | null |
| Weeping, loom shuttle, quern grinding, lamp catching | none (views are visual only) |
| Spell voice | word.wake exists. No word.echo, word.remember |
| Dialogue (Ankhu and Mother) | none recorded |

## 11. Exit conditions

Goal `mother = remembered`. Next to `PlayOrder.ExitOf("Rosetau_C_Mother")` = `Rosetau_D_TwoWays`.

## 12. Acceptance criteria

- AC-1 [auto] Loads with mother weeping, weeping unanswered, loom still, quern still, lamp dark, unsolved.
- AC-2 [auto] `arrive` fires on start. `look_bread` is offered in `shelf` and fires `shelf`.
- AC-3 [auto] `still` on the mother is Refused with "No. Not again." and fires `hush_again` every time.
- AC-4 [auto] `wake loom` before the echo is Refused and fires `not_yet`, state unchanged.
- AC-5 [auto] `echo mother` sets mother listening and weeping answered and fires `answered`.
- AC-6 [auto] After the echo, `wake loom` (in `loom`) and `wake quern` (in `quern`) start them, and the lamp lights by itself when both run, firing `lamp`.
- AC-7 [auto] `remember mother` before the lamp is lit is Refused with "Not like this."
- AC-8 [auto] `remember mother` with the lamp lit solves, plays `ros_old_mother`, writes `memory.old_mother`, exits to `Rosetau_D_TwoWays`.
- AC-9 [auto] The codex holds `word.remember` before the player needs it (written when the lamp catches, fix 2026-11-26).
- AC-10 [auto] Bare `echo` resolves to the mother (no "Echo what?").
- AC-11 [manual] Weeping, shuttle, quern and the lamp catching are audible. The mother's card changes from face in hands to looking up to the remembered pose.

## 13. Code vs intent disagreements

| # | Severity | Where | Problem | Proposed fix |
|---|---|---|---|---|
| 13.1 | major | D:\TheAfterlifeofAnkhu\Assets\Ankhu\Realms\Core\Content\CodexContent.cs (no `word.remember`), RosetauScripts.cs:388 | `remember` is the winning word, but no room teaches it, `word.remember` is not in the codex catalog, and it never appears on the roll or in the bubble's known words. The only pointer is the last hint. ROSETAU_DESIGN.md says the player already owns it from Wernes. They do not | Add `word.remember` to CodexContent and teach it with `Step.Learn("remember")` in the `lamp` beat (or in Wernes where the design says) |
| 13.2 | minor | RosetauScripts.cs header (line 9) vs `BuildMotherC` | Header says every Rosetau utterance is a tick. Room C has no Tick, so `wait` is "not words of power" here while it worked in A and B | Accept, or treat `wait` as a no-op success in all rooms |
| 13.3 | minor | D:\TheAfterlifeofAnkhu\Assets\Ankhu\Realms\Core\RealmRules.cs:401 (bare verb candidates) | Bare `echo` returns Incomplete because both the `weeping` and `mother` rules apply. They are the same deed with the same event | Pick the first candidate when all candidates share an Event, or drop one rule and alias the other |
| 13.4 | minor | builder | Ankhu spawns inside `shelf`, so the shelf prompt is the first thing on screen while `arrive` plays | Spawn at x -7.4 or narrow the zone |
| 13.5 | minor | RealmRules.cs:445 | NoChange text "The mother is already listening." for a spent echo | AlreadySpoken message (guide 17, 13.1) |

## 14. E2E script

```e2e canonical
scene Rosetau_C_Mother
beat arrive [AC-2]
expect mother=weeping [AC-1]
expect weeping=unanswered [AC-1]
expect loom=still [AC-1]
expect quern=still [AC-1]
expect lamp=dark [AC-1]
unsolved [AC-1]
at shelf
use look_bread [AC-2]
beat shelf [AC-2]
say still her => Refused | No. Not again. [AC-3]
beat hush_again [AC-3]
say echo => Success [AC-10]
# fixed 2026-11-26, this used to fail: Incomplete "Echo what? Name the thing.", see 13.3
expect mother=listening [AC-5]
expect weeping=answered [AC-5]
beat answered [AC-5]
at loom
say wake loom => Success [AC-6]
expect loom=working [AC-6]
say remember mother => Refused | Not like this [AC-7]
at quern
say wake quern => Success [AC-6]
expect quern=grinding [AC-6]
expect lamp=lit [AC-6]
beat lamp [AC-6]
codexhas word.remember [AC-9]
verb remember [AC-9]
say remember mother => Success [AC-8]
expect mother=remembered [AC-8]
solved [AC-8]
film ros_old_mother [AC-8]
codexhas memory.old_mother [AC-8]
exit Rosetau_D_TwoWays [AC-8]
```

```e2e fail-1
# Nothing moves while she weeps alone. Quern first, explicit echo target.
scene Rosetau_C_Mother
at loom
say wake loom => Refused | weeps alone [AC-4]
beat not_yet [AC-4]
expect loom=still [AC-4]
say wake lamp => Refused | her lamp
say remember her => Refused | Not like this [AC-7]
say still mother => Refused | No. Not again. [AC-3]
say echo weeping => Success [AC-5]
at quern
say wake quern => Success
expect lamp=dark
at loom
say wake loom => Success
expect lamp=lit
say remember her => Success
solved
exit Rosetau_D_TwoWays
```
