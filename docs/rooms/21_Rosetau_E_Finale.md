# 21 Rosetau_E_Finale: The Shield Brought Home

## 1. Summary

| Field | Value |
|---|---|
| Scene | `Rosetau_E_Finale` |
| Realm | Rosetau |
| Title | The Shield Brought Home |
| Play order | 21 (last built scene) |
| Previous | 20 `Rosetau_D_TwoWays` |
| Next | `Opening_Title` (end of built content). `Step.Next()` resolves through `PlayOrder.ExitOf`, which returns the title after the last chain entry |
| Script | D:\TheAfterlifeofAnkhu\Assets\Ankhu\Realms\Core\Content\RosetauScripts.cs, `BuildFinaleE()` (lines 652 to 804) |
| Scene builder | D:\TheAfterlifeofAnkhu\Assets\Ankhu\Realms\Editor\RosetauBuilder.cs, `BuildFinaleE()` (lines 713 to 810) |
| Scene file | D:\TheAfterlifeofAnkhu\Assets\Ankhu\Realms\Generated\Scenes\Rosetau_E_Finale.unity |
| Design | D:\TheAfterlifeofAnkhu\docs\ROSETAU_DESIGN.md, row Rosetau_E_Finale |

## 2. Intent and story beat

The stair of Rosetau comes out at the door of the house in Abdju on the day the shield came home. The shield leans on the doorpost, his young mother wails in the dust, Nakht's shade waits with nothing to go into. Anpu: "You are standing where he stood." The room is the Opening of the Mouth that Ankhu performed for strangers, performed at last for his father. `bind` the father is refused (no body). `bind` the shield with the two linen bands carried out of the Room of the Mourners (the same bands, the same knot, "to keep someone in, not to keep someone quiet"), and the shade enters it. Anpu comes off the stair for the first time in Rosetau and holds the wrapped form upright, as in the Papyrus of Hunefer vignette. `still` on the mother is refused for the last time. Ankhu does what the boy could not: he sits in the dust and keens with her ("Who taught you to wail like that? You sound like a goat." "You did."). Then `open` the mouth, which needs the form held and the lament answered, and `give` bread. The blank cenotaph stela by the door is cut with the couple at their offering table. Film `ros_whistle`: she learned his father's tune and played it badly to Hori every night. END OF ROSETAU.

## 3. Layout and zones

One camera `cam_street`, x about -9.5 to 9.5.

| Zone id | Centre x | Width | What stands there | Reaches |
|---|---|---|---|---|
| `door` | 2.0 | 4.2 | The house door, the shield on the doorpost (x 0.8), Nakht's shade (x 2.9), the stela (x 3.5), the mouth glow (x 1.6). `read_stela` usable | `shield`, `father`, `mouth` |
| `street` | 6.4 | 4.2 | The young widow in the dust (x 6.2) | `mother`, `lament` |

Ankhu spawns at x -4.8, outside both zones, near the stair. Anpu spawns on the stair at (-6.6, 0.96, 1.6) as an active `RealmFollower` (builder line 793). Mark `mark_hold` at (1.6, 0, 1.7) is saved in the scene. Bands and bread are reachable from anywhere. Saved scene zone ids (checked 2026-11-26): door, street plus one camera zone. They match.

## 4. Elements

### Room objects

| Id | Name | Key aliases | Initial | States | Zones |
|---|---|---|---|---|---|
| `shield` | shield | shield, cowhide shield, father's shield, his shield | leaning | leaning, wrapped | door |
| `bands` | linen bands | band, bands, linen, linen bands, wrapping, bandages | carried | carried, used | any |
| `father` | father | father, dad, nakht, shade, soldier, my father, him | waiting | waiting, housed, opened | door |
| `holder` | (internal, Anpu) | | stair | stair, holding | |
| `mother` | mother | mother, mum, mama, mut, woman, widow, my mother, her, wailing woman | keening | keening, answered | street |
| `lament` | lament | lament, keen, keening, wail, wailing, scream, screaming, grief, weeping, crying | unanswered | unanswered, answered | street |
| `mouth` | mouth | mouth, his mouth, father's mouth, lips | closed | closed, open | door |
| `bread` | bread | bread, loaf, offering, bread offering | carried | carried, given | any |

No world clock. `wait` is NotUnderstood here.

### Rules

| Verb | Target | From | To | Requires | Also | Event | Success | Refusal |
|---|---|---|---|---|---|---|---|---|
| bind | father | never | never | | | | | "There is nothing of him to wrap. They never brought him home." + `no_body` |
| bind | shield | leaning | wrapped | bands carried | bands used, father housed | `shield_wrapped` | "Tjes. Double turn, flat tuck. The two bands go round the shield, and round, and it stands up by itself." | default "The word finds nothing to hold." |
| bind | bands | carried | used | shield leaning | shield wrapped, father housed | `shield_wrapped` | same | "Bind them round what?" |
| auto | holder | | holding | father housed | | `anpu_holds` | | |
| still | mother / lament | never | never | | | | | "No. Not her. Not ever again." + `hush_last` |
| echo | mother | keening | answered | | lament answered | `lament_answered` | "Wesheb. You keen with her, in the street, out loud." | default |
| echo | lament | unanswered | answered | | mother answered | `lament_answered` | same | default |
| open | mouth | closed | open | holder holding, lament answered | father opened | `mouth_open` | "Wen. You touch the mouth of the wrapped form, and the mouth of the man." | "Not yet. In every Opening you ever served, someone held the dead man up and someone mourned him." + `not_ready` |
| give | bread | carried | given | mouth open | | `bread_given` | "Rdi. He takes the loaf." | "He can't eat. His mouth was never opened." + `cant_eat` |
| give | father | opened | opened | bread carried | bread given | `bread_given` | same | same |

Goal: `bread = given`.

### Usables

| Id | Zone | Prompt |
|---|---|---|
| `read_stela` | door | Read the stela |

## 5. Words

| Kind | Words |
|---|---|
| Known on entry | open, wake, still, bind, release, echo, give, remember, name |
| Codex on entry | word.bind, word.open, lore.opening_of_the_mouth, lore.abdju_stela |
| Learned in room | none |

Messages:

| Situation | Judgement | Message |
|---|---|---|
| `bind father` (in door) | Refused | "There is nothing of him to wrap..." then the `no_body` beat |
| `open mouth` before held and mourned | Refused | "Not yet. In every Opening you ever served..." then "There's an order to it." |
| `give bread` before the mouth is open | Refused | "He can't eat. His mouth was never opened." then the `cant_eat` beat |
| `still mother` (in street) | Refused | "No. Not her. Not ever again." then the `hush_last` beat |
| `echo mother` from `door` | OutOfReach | "The mother is too far. Come closer." |
| bare `bind` in `door` | Incomplete | "Bind what? Name the thing." (13.4) |
| bare `echo` in `street` | Incomplete | "Echo what? Name the thing." (13.4) |
| `bind shield` again | Refused today "The word finds nothing to hold." Intended NoChange "This time, nothing has changed." (13.3) |
| `bind bands` again | Refused today "Bind them round what?" Intended NoChange (13.3) |
| `wait` | NotUnderstood | "The words fall like sand." |

## 6. Canonical solution

Start: beat `arrive` (title, caption, Ankhu and Anpu exchange, "You are standing where he stood.").

| # | Zone | Input | Result | State after | Beats |
|---|---|---|---|---|---|
| 1 | door | E on `read_stela` | Use | | `read_stela` (offering formula) |
| 2 | door | `bind father` | Refused | | `no_body` |
| 3 | door | `bind shield` | Success | shield wrapped, bands used, father housed, holder holding | `wrapped`, `anpu` (Anpu walks to `mark_hold`) |
| 4 | street | `still mother` | Refused | | `hush_last` |
| 5 | street | `echo mother` | Success | mother answered, lament answered | `answered` |
| 6 | door | `open mouth` | Success | mouth open, father opened | `opened` ("Is there any bread?") |
| 7 | door | `give bread` | Success | bread given, solved | `solved`: captions, film `ros_whistle`, codex `memory.whistle`, Anpu and Ankhu lines, Title "END OF ROSETAU", Next |

## 7. Alternate valid orders

- Echo the mother (step 5) before binding the shield (step 3). Both must hold before the mouth opens.
- `bind bands`, `bind the shield with the bands`, `tjes shield` (tjes is an alias) instead of `bind shield`.
- `echo lament`, `echo her`, `echo widow` instead of `echo mother`.
- `give father`, `give bread to father`, `give loaf` instead of `give bread`.

Orders that fail: opening the mouth before both the hold and the lament, giving bread before the mouth.

## 8. Failure, refusal and reset behaviour

No reset, no clock. Failure beats: `no_body` (once), `hush_last`, `not_ready`, `cant_eat` (repeatable). Idle `murmur` after 45 s: "The bands, the god, the mourning, the mouth. I know this rite better than my own name." Hints:

- shield leaning: "No body ever came home. Only the shield. And you still have the bands from the mourners' room."
- lament unanswered: "She's screaming in the street, the way she did. You held your ears that day."
- held, answered, mouth closed: "He's held up and he's mourned. You know the rite. Open his mouth."
- mouth open: "Read the stela by the door. You know what the dead want."

## 9. Beats, memory films and codex

| Beat id | Trigger | Once | Steps |
|---|---|---|---|
| `arrive` | Start | yes | Freeze, Title, Caption, four lines, Free |
| `read_stela` | Use `read_stela` | no | Caption offering formula, Ankhu line |
| `no_body` | Event `no_body` | yes | Father and Ankhu lines |
| `wrapped` | Event `shield_wrapped` | yes | Freeze, caption, Ankhu line, Free |
| `anpu` | Event `anpu_holds` | yes | Freeze, caption, `Move Anpu mark_hold`, two lines, Free |
| `hush_last` | Event `hush_last` | no | Ankhu line |
| `answered` | Event `lament_answered` | yes | Freeze, caption, Ankhu, Mother ("You sound like a goat."), Ankhu ("You did."), Free |
| `not_ready` | Event `not_ready` | no | Ankhu line |
| `cant_eat` | Event `cant_eat` | no | Ankhu line |
| `opened` | Event `mouth_open` | yes | Freeze, caption, Father, Mother "Nakht?", Free |
| `murmur` | Idle 45 s | yes | Ankhu line |
| `solved` | Solved | yes | Freeze, two captions (stela cut), `flashback:ros_whistle`, Ankhu, Write `memory.whistle`, Anpu, Ankhu, Title "END OF ROSETAU" 4 s, Wait 1, Next |

Film present at D:\TheAfterlifeofAnkhu\Assets\StreamingAssets\Memories\ros_whistle.mp4. Codex `memory.whistle`, `lore.opening_of_the_mouth`, `lore.abdju_stela` exist.

## 10. SFX and audio

| Source | Status |
|---|---|
| Ambience walk_ambience.wav | assigned. No street or wailing bed |
| Spell cast, land, fail | null |
| The mother's wail, the keening together, the bands wrapping, the stela being cut, the bread | none |
| Spell voice | word.open exists. No word.bind, word.echo, word.give |
| Dialogue (Ankhu, Anpu, Father, Mother) | none recorded |

## 11. Exit conditions

Goal `bread = given`. The `solved` beat ends with `Step.Next()`. `PlayOrder.ExitOf("Rosetau_E_Finale")` finds no chain scene after it and returns `OpeningSettings.TitleScene` (`Opening_Title`). PLAY_ORDER.md row 21 says "Back to the title, end of built content", which matches.

## 12. Acceptance criteria

- AC-1 [auto] Loads with shield leaning, bands carried, father waiting, holder stair, mother keening, lament unanswered, mouth closed, bread carried, unsolved.
- AC-2 [auto] `arrive` fires on start. `read_stela` is offered in `door` and fires its beat.
- AC-3 [auto] `bind father` is Refused with "nothing of him to wrap" and fires `no_body`.
- AC-4 [auto] `open mouth` before the hold and the lament is Refused with "Not yet." and fires `not_ready`.
- AC-5 [auto] `give bread` before the mouth is open is Refused with "His mouth was never opened." and fires `cant_eat`.
- AC-6 [auto] `bind shield` wraps the shield, uses the bands, houses the father, sets Anpu holding, and fires `wrapped` and `anpu`.
- AC-7 [auto] `still mother` is Refused with "Not ever again." and fires `hush_last`.
- AC-8 [auto] `echo mother` from `street` answers the lament and fires `answered`. From `door` it is OutOfReach.
- AC-9 [auto] With the hold and the lament, `open mouth` opens it and fires `opened`.
- AC-10 [auto] `give bread` then solves, plays `ros_whistle`, writes `memory.whistle`, and returns to `Opening_Title`.
- AC-11 [auto] Binding the shield again answers "This time, nothing has changed."
- AC-12 [manual] Anpu stands on the stair until the shield is wrapped, then walks to the door and stays holding the wrapped form until the room ends.
- AC-13 [manual] The wail, the shared keen and the bread offering are audible. The stela visibly changes from blank to cut.

## 13. Code vs intent disagreements

| # | Severity | Where | Problem | Proposed fix |
|---|---|---|---|---|
| 13.1 | major | D:\TheAfterlifeofAnkhu\Assets\Ankhu\Realms\Editor\RosetauBuilder.cs:793, D:\TheAfterlifeofAnkhu\Assets\Ankhu\Realms\Runtime\Director\RealmFollower.cs:34 | Anpu is an active `RealmFollower` from the start, so he leaves the stair and walks behind Ankhu at once. The whole beat ("Anpu stands on the last step", "You came off the step.") is contradicted on screen | Spawn him with no leader (or a hold flag) and only give him the mark in the `anpu` beat |
| 13.2 | major | D:\TheAfterlifeofAnkhu\Assets\Ankhu\Realms\Runtime\Director\RealmFollower.cs:55 (the line that clears the mark on arrival) | After `Move("Anpu", "mark_hold")` he arrives, the mark is cleared, and he goes back to following Ankhu. He does not stay holding the form through the mouth opening and the bread | Keep the mark until `Release()` is called (add a `hold` flag to `GoTo`), and have the director not release it |
| 13.3 | minor | RosetauScripts.cs:688 and :691, RealmRules.cs:442 | A second `bind shield` says the default "The word finds nothing to hold.", and a second `bind bands` says "Bind them round what?". Both should say the word is spent | Spent-word fix (guide 17, 13.1) |
| 13.4 | minor | RealmRules.cs bare verb candidates | Bare `bind` at the door and bare `echo` in the street both return Incomplete because two twin rules apply | Collapse twin rules that share an Event when choosing a bare target |
| 13.5 | minor | RosetauScripts.cs:802 | Uses `Step.Next()` rather than `Step.End()` for the end of built content. Works (ExitOf returns the title) but `Next` marks `WernesProgress` done for a non-Wernes scene and fades with the room timing, not the slice ending timing | Use `Step.End()` while Rosetau is the last built content |
| 13.6 | minor | ROSETAU_DESIGN.md:76 | A 3D carrying pose is still a placeholder: Anpu stands behind the wrapped card | Known, listed in the design's open items |

## 14. E2E script

```e2e canonical
scene Rosetau_E_Finale
beat arrive [AC-2]
expect shield=leaning [AC-1]
expect bands=carried [AC-1]
expect father=waiting [AC-1]
expect holder=stair [AC-1]
expect mother=keening [AC-1]
expect lament=unanswered [AC-1]
expect mouth=closed [AC-1]
expect bread=carried [AC-1]
unsolved [AC-1]
at door
use read_stela [AC-2]
beat read_stela [AC-2]
say bind father => Refused | nothing of him to wrap [AC-3]
beat no_body [AC-3]
say open mouth => Refused | Not yet [AC-4]
beat not_ready [AC-4]
say give bread => Refused | never opened [AC-5]
beat cant_eat [AC-5]
say echo mother => OutOfReach [AC-8]
say bind shield => Success [AC-6]
expect shield=wrapped [AC-6]
expect bands=used [AC-6]
expect father=housed [AC-6]
expect holder=holding [AC-6]
beat wrapped [AC-6]
beat anpu [AC-6]
say bind shield => NoChange | This time, nothing has changed. [AC-11]
# fails today: Refused "The word finds nothing to hold.", see 13.3
say open mouth => Refused | Not yet [AC-4]
at street
say still mother => Refused | Not ever again [AC-7]
beat hush_last [AC-7]
say echo mother => Success [AC-8]
expect lament=answered [AC-8]
expect mother=answered [AC-8]
beat answered [AC-8]
at door
say open mouth => Success [AC-9]
expect mouth=open [AC-9]
expect father=opened [AC-9]
beat opened [AC-9]
say give bread => Success [AC-10]
expect bread=given [AC-10]
solved [AC-10]
beat solved [AC-10]
film ros_whistle [AC-10]
codexhas memory.whistle [AC-10]
exit Opening_Title [AC-10]
# The room leaves with Step.Next(), which resolves to the title scene. If 13.5 is fixed to Step.End(), change this to "exit title".
```

```e2e alt-1
# Mourn first, bind with the bands, feed through the father.
scene Rosetau_E_Finale
at street
say echo lament => Success
expect mother=answered
at door
say open mouth => Refused | Not yet
say bind bands => Success
expect shield=wrapped
expect holder=holding
say open mouth => Success
say give bread to father => Success
expect bread=given
solved
exit Opening_Title
```
