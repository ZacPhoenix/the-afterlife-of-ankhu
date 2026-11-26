# 17 Rosetau_A_Laugh: The Laugh in the Dark

## 1. Summary

| Field | Value |
|---|---|
| Scene | `Rosetau_A_Laugh` |
| Realm | Rosetau |
| Title | The Laugh in the Dark |
| Play order | 17 |
| Previous | 16 `Wernes_Finale` |
| Next | 18 `Rosetau_B_Mourners` (via `PlayOrder.ExitOf`, `Step.Next()`) |
| Script | D:\TheAfterlifeofAnkhu\Assets\Ankhu\Realms\Core\Content\RosetauScripts.cs, `RosetauScripts.BuildLaughA()` (lines 82 to 237) |
| Scene builder | D:\TheAfterlifeofAnkhu\Assets\Ankhu\Realms\Editor\RosetauBuilder.cs, `BuildLaughA()` (lines 256 to 367) |
| Scene file | D:\TheAfterlifeofAnkhu\Assets\Ankhu\Realms\Generated\Scenes\Rosetau_A_Laugh.unity |
| Rules engine | D:\TheAfterlifeofAnkhu\Assets\Ankhu\Realms\Core\RealmRules.cs (`RealmRoom`), run by D:\TheAfterlifeofAnkhu\Assets\Ankhu\Realms\Runtime\Director\RealmSceneDirector.cs |
| Design | D:\TheAfterlifeofAnkhu\docs\ROSETAU_DESIGN.md (table row Rosetau_A_Laugh) |

## 2. Intent and story beat

Rosetau is the mother's realm. This room turns `echo` (wesheb) from a trick into its point: to answer someone. A child's laugh comes, bounces off three doorways and fades. Only when Ankhu answers it does it stay, and while held it sounds from one doorway only. Three rounds (middle, left, right) open three small chambers, each holding a child's object that slides from Hori's childhood into Ankhu's own: Hori's cedar top, Ankhu's own knucklebones and lost reed whistle, and finally Ankhu's tiny papyrus sandal that his mother kept. The child who appears is the young Heru (Harpocrates, finger at his lips), the faience over his mother's bread oven that "never worked on her". Ankhu gives him Hori's clay falcon. The solve plays the film `ros_news`: the day the garrison brought his father's shield home, his mother screaming in the street, the boy holding his ears, and the confession that he hated her for it. Anpu stops on the stair: "These rooms are your mother's. I was never a guest in her house."

## 3. Layout and zones

Side-on, x across the screen. Two fixed cameras: `cam_stair` (live for x about -17.25 to -0.75) and `cam_mouths` (x about -5 to 11.5).

| Zone id | Centre x | Width | What stands there | Reaches |
|---|---|---|---|---|
| `entry` | -10.4 | 3.6 | Foot of the descending stair, lintel with Book of the Dead spell 117 title. Ankhu spawns here (x -11.2) | `read_lintel` usable. Laugh, child and falcon are reachable from anywhere |
| `mouth1` | -3.0 | 3.0 | Left doorway | `mouth1` only |
| `mouth2` | 2.5 | 3.0 | Middle doorway | `mouth2` only |
| `mouth3` | 8.0 | 3.0 | Right doorway | `mouth3` only |

Zone ids in the saved scene (checked on disk 2026-11-26): `entry`, `mouth1`, `mouth2`, `mouth3` plus two camera zones. They match the script.

Anpu is spawned on the stair at x -13.0 as an active `RealmFollower` (builder line 342). See section 13.

## 4. Elements

### Room objects

| Id | Name | Aliases | Initial | States | Zones |
|---|---|---|---|---|---|
| `laugh` | laugh | laugh, laughter, laughing, giggle, voice, sound, child's laugh | silent | silent, sounding, fading, held | any |
| `mouth1` | left doorway | door, doorway, mouth, passage, opening, left door, left doorway, left mouth, left passage | dark | dark, lit, open | mouth1 |
| `mouth2` | middle doorway | same pattern with "middle" | dark | dark, lit, open | mouth2 |
| `mouth3` | right doorway | same pattern with "right" | dark | dark, lit, open | mouth3 |
| `round` | (internal) | none | 1 | 1, 2, 3, 4 | any |
| `clock` | (internal) | none | 1 | 0, 1 | any |
| `heard` | (internal) | none | no | no, yes | any |
| `child` | child | child, boy, heru, young heru, heru the child, hori, son, kid | hidden | hidden, seen, holding | any |
| `falcon` | clay falcon | falcon, clay falcon, toy, bird, hori's falcon | hand | hand, given | any |

### World clock (`room.Tick`, lines 116 to 134)

Every parsed utterance that is accepted, refused or "already" (NoChange), every `wait`, and every deed is one tick. Words that are not understood, unknown, out of reach, ambiguous or incomplete do not tick. There is no real-time tick: the director never calls `RealmRoom.Advance()`.

| Laugh state | On tick |
|---|---|
| silent | clock +1. At 2 the laugh becomes `sounding` and clock resets to 0. Nothing happens once round is 4 |
| sounding | becomes `fading` |
| fading | becomes `silent`, clock 0 |
| held | stays held (no decay) |

After the laugh step, every doorway not `open` is recomputed: the true doorway for the current round is `lit` while the laugh is `held`, every other non-open doorway is `dark`. True doorway: round 1 middle (`mouth2`), round 2 left (`mouth1`), round 3 right (`mouth3`).

### Rules

| Verb | Target | From | To | Requires | Also | Event | Success | Refusal |
|---|---|---|---|---|---|---|---|---|
| auto | heard | any | yes | laugh sounding | | `first_laugh` | | |
| echo | laugh | sounding, fading | held | | | `laugh_held` | "Wesheb. You answer the laugh, and it answers back from one doorway only." | "There is nothing to answer yet. Wait for it." |
| open | mouth2 | lit | open | round 1 | round 2, laugh silent, clock 0 | `found_1` | "Wen. The doorway opens on a small dry chamber." | "Only stone. The doorway ends a step in." + RefusalEvent `wrong_mouth` |
| open | mouth1 | lit | open | round 2 | round 3, laugh silent, clock 0 | `found_2` | same | same |
| open | mouth3 | lit | open | round 3 | round 4, laugh silent, clock 0 | `found_3` | same | same |
| auto | child | any | seen | round 4, child hidden | | `child_seen` | | |
| give | falcon | hand | given | child seen | child holding | `falcon_given` | "Rdi. He takes the falcon in both hands." | "There is no one here to give it to." |
| give | child | seen | holding | falcon hand | falcon given | `falcon_given` | same | "Not yet. Find him first." |

Goal: `falcon = given`.

### Usables

| Id | Zone | Prompt | Deed | When |
|---|---|---|---|---|
| `read_lintel` | entry | Read the lintel | none | always |

## 5. Words

| Kind | Words |
|---|---|
| Known on entry (`room.Verbs`) | open, wake, still, release, echo |
| Codex on entry (`CodexOnEntry`) | word.echo, word.open, lore.rosetau |
| Learned in room | `give`, by `Step.Learn("give")` in beat `child` (fires on event `child_seen`) |
| Names | none (the room has no `name` verb) |
| Room clock words | `wait`, `watch`, `rest` (one or two tokens) advance one tick, no glyphs |

What each verb does here: `echo` holds the laugh, `open` opens the lit doorway you stand at, `give` gives the falcon. `wake`, `still` and `release` have no rules here and return InvalidTarget "Nothing here answers that word." with no tick.

Messages:

| Situation | Judgement | Message | Ticks |
|---|---|---|---|
| `give falcon` before the child appears (give not learned) | UnknownVerb | "You know this word exists, but your mouth cannot shape it yet." | no |
| `echo` with the laugh silent | Refused | "There is nothing to answer yet. Wait for it." | yes |
| `echo` with the laugh already held | NoChange | "The laugh is already held." | yes |
| `open` a dark doorway | Refused | "Only stone. The doorway ends a step in." then Ankhu: "Wrong one. It's just a niche." | yes |
| `open` a doorway already opened | Refused today (bug 13.1). Intended NoChange "This time, nothing has changed." | | yes |
| `give falcon` again after it is given | NoChange | "The clay falcon is already given." | yes |
| `hori` typed alone | UnknownVerb | "You know this word exists, but your mouth cannot shape it yet." (bug 13.7) | no |

## 6. Canonical solution

Start: Ankhu in `entry`. Beat `arrive` plays (titles ROSETAU and THE LAUGH IN THE DARK, Anpu's four lines), player frozen until it ends.

| # | Zone | Input | Result | State after | Beats |
|---|---|---|---|---|---|
| 1 | entry | `wait` | Success (tick) | laugh sounding, heard yes | `first_laugh` |
| 2 | entry | `echo laugh` | Success | laugh held, mouth2 lit | `held` |
| 3 | mouth2 | `open middle doorway` (or bare `open`) | Success | mouth2 open, round 2, laugh silent, clock 1 | `found1` (spinning top) |
| 4 | mouth2 | `wait` | Success | laugh sounding | |
| 5 | mouth2 | `echo` | Success | laugh held, mouth1 lit | `held` |
| 6 | mouth1 | `open left doorway` | Success | mouth1 open, round 3 | `found2` (knucklebones and whistle) |
| 7 | mouth1 | `wait` | Success | laugh sounding | |
| 8 | mouth1 | `echo` | Success | laugh held, mouth3 lit | `held` |
| 9 | mouth3 | `open right doorway` | Success | mouth3 open, round 4, child seen | `found3` (sandal), `child` (learns give) |
| 10 | mouth3 | `give falcon` | Success | falcon given, child holding, solved | `solved`: film `ros_news`, codex `memory.news`, Next |

Exit to `Rosetau_B_Mourners`.

## 7. Alternate valid orders

- Use any ticking utterance instead of `wait`. A refused `echo` on silence ticks the clock, so `echo`, `echo` from the start sounds the laugh then holds it.
- Echo during `fading` instead of `sounding` (one extra wait first).
- If the laugh fades fully, two more ticks bring it back.
- Opening the true doorway is order-locked: middle, left, right. There is no other valid order.
- `give child`, `give heru`, `give boy`, `give falcon to heru` all solve (the falcon alias wins on length, or the child rule fires with falcon in hand).

Orders that fail: opening any doorway that is not lit (Refused, `wrong_mouth`). Opening a lit doorway from another zone (OutOfReach "No doorway is close enough. Walk to the one you mean." when the phrase is generic, or "The middle doorway is too far. Come closer." when named).

## 8. Failure, refusal and reset behaviour

- No reset. The held laugh never decays, so walking to the doorway is safe.
- Wrong doorway: Refused, beat `wrong` (repeatable): "Wrong one. It's just a niche."
- Idle: beat `murmur` after 45 s with no successful spell while unsolved (once): "It comes, and it goes. I only need it to stay once."
- After 75 s with no success the director shows `room.Hint()`:
  - laugh silent, round 1 to 3: "Wait. The laugh comes again."
  - laugh sounding or fading: "Answer it, so it stays, and listen for where it answers from."
  - laugh held: "One doorway is lit. Stand at it and open it."
  - child seen: "You are still carrying the clay falcon from the field."
- H gives `RealmHintSolver.Tiers(room)`.
- Undo exists in `RealmRoom` but no input calls it in this scene.

## 9. Beats, memory films and codex

| Beat id | Trigger | Once | Steps |
|---|---|---|---|
| `arrive` | Start | yes | Freeze, Title "ROSETAU" 3 s, Title "THE LAUGH IN THE DARK" 3.5 s, Caption, Anpu x3 and Ankhu x2 lines, Free |
| `read_lintel` | Use `read_lintel` | no | Caption "As it is written: "Spell for taking the way to Rosetau."", Ankhu line |
| `first_laugh` | Event `first_laugh` | yes | Freeze, captions, "Hori? Hori, it's me.", tomb-shaft line, Free |
| `held` | Event `laugh_held` | no | Caption "The laugh hangs in the air. Light gathers in one doorway." |
| `wrong` | Event `wrong_mouth` | no | Ankhu "Wrong one. It's just a niche." |
| `found1` | Event `found_1` | yes | Caption spinning top, Ankhu line about Hori's cedar top |
| `found2` | Event `found_2` | yes | Caption knucklebones and reed whistle, "Those are mine" |
| `found3` | Event `found_3` | yes | Caption papyrus sandal, "She kept my sandal." |
| `child` | Event `child_seen` | yes | Freeze, caption young Heru, "Hori.", cobra caption, faience line, "It never worked on her.", Learn give (writes `word.give`), "Rdi. It should go to a falcon.", Free |
| `murmur` | Idle 45 s | yes | Ankhu line |
| `solved` | Solved | yes | Freeze, caption, `flashback:ros_news`, two Ankhu lines, Write `memory.news`, Wait 1.5, Next |

Film: D:\TheAfterlifeofAnkhu\Assets\StreamingAssets\Memories\ros_news.mp4 (present, 18.1 MB). Codex: `memory.news` and `lore.rosetau` exist in the catalog. `word.give` does not (bug 13.3).

## 10. SFX and audio

| Source | What | Status |
|---|---|---|
| Ambience | `RealmKit.Ambience` = D:\TheAfterlifeofAnkhu\Assets\Ankhu\Opening\Audio\walk_ambience.wav, shared with every realm room | assigned |
| Footsteps | `RealmKit.Footsteps()` via `WalkPlayer` | assigned by builder |
| Spell cast, land, fail | `RealmSceneDirector.spellCastSfx`, `spellLandSfx`, `spellFailSfx` | null in the saved scene (bug 13.5) |
| Spell voice | `RealmVoice.FindSpell(verb, target)` then `word.<verb>`. Only word.open exists among this room's verbs. No word.echo, word.give | partial |
| Dialogue voice | keys `dialogue.<speaker>.rosetau_a.<n>` | none recorded (0 Rosetau clips in Resources/RealmVoice). Design lists it as to-do |
| The laugh itself | none. `RealmObjectView` has no audio. The puzzle is about a sound, and the sound is only a caption and a glow (bug 13.4) | missing |
| Doorway open, child appears, falcon given | none | missing |
| Codex write | `CodexRollSfx` | assigned at runtime |
| Film | music inside ros_news.mp4 | present |

## 11. Exit conditions

Goal `falcon = given`. The `solved` beat ends with `Step.Next()`: `WernesProgress.MarkDone("Rosetau_A_Laugh")` (harmless, not a Wernes room) and fade to `RealmSceneDirector.NextSceneName()` = `PlayOrder.ExitOf("Rosetau_A_Laugh")` = `Rosetau_B_Mourners` when it is in Build Settings (it is, enabled, EditorBuildSettings line 57).

## 12. Acceptance criteria

- AC-1 [auto] The room loads with laugh silent, all three doorways dark, round 1, child hidden, falcon in hand, and the room unsolved.
- AC-2 [auto] Beat `arrive` fires on start.
- AC-3 [auto] One `wait` from the start makes the laugh sound and fires `first_laugh` exactly once.
- AC-4 [auto] `echo` while the laugh sounds or fades holds it, and the true doorway for the round (middle, then left, then right) is lit while the other non-open doorways stay dark.
- AC-5 [auto] `echo` on silence is Refused with "There is nothing to answer yet." and still advances the clock.
- AC-6 [auto] `open` on the lit doorway while standing in its zone opens it, advances the round and fires the matching `found_N` beat.
- AC-7 [auto] `open` on a dark doorway is Refused with "Only stone." and fires beat `wrong`.
- AC-8 [auto] `open` on a doorway already opened is NoChange with "This time, nothing has changed." and does not fire `wrong`.
- AC-9 [auto] `give` before the child appears is UnknownVerb with "your mouth cannot shape it yet."
- AC-10 [auto] Opening the third doorway sets child seen, fires beat `child`, and teaches `give`.
- AC-11 [auto] `give falcon` then solves the room: falcon given, child holding.
- AC-12 [auto] The solve plays film `ros_news`, writes codex `memory.news`, and exits to `Rosetau_B_Mourners`.
- AC-13 [auto] `read_lintel` is offered in `entry` and fires its beat each time it is used.
- AC-14 [auto] Codex holds `word.give` after beat `child`.
- AC-15 [manual] Anpu stays on the stair for the whole room, as his line says.
- AC-16 [manual] The laugh is audible when it sounds, quieter when it fades, and sustained while held. Each doorway lights visibly, opens with a sound, and the chamber object is visible.
- AC-17 [manual] Spell cast, land and fail each have an audible SFX.
- AC-18 [manual] The young Heru card shows finger at lips when seen, holds the falcon when given.
- AC-19 [auto] Anpu stays on the step, where he says "I will wait on this step." (fixed 2026-11-26, he used to follow Ankhu).

## 13. Code vs intent disagreements

| # | Severity | Where | Problem | Proposed fix |
|---|---|---|---|---|
| 13.1 | major | D:\TheAfterlifeofAnkhu\Assets\Ankhu\Realms\Core\RealmRules.cs:442 with D:\TheAfterlifeofAnkhu\Assets\Ankhu\Realms\Core\Content\RosetauScripts.cs:150 | `open` on a doorway already opened returns Refused "Only stone. The doorway ends a step in." and fires `wrong_mouth` ("Wrong one. It's just a niche."), because the NoChange check also demands `Requires` (round N), which no longer holds. Breaks Zac's 2026-11-25 rule that a spent word says "This time, nothing has changed." It also costs a tick | In `Judge`, when the target already sits in `reachable.To` and `From` excludes `To`, return NoChange with `AlreadySpoken`, regardless of `Requires`. Use `AlreadySpoken` as the message for every NoChange |
| 13.2 | major | D:\TheAfterlifeofAnkhu\Assets\Ankhu\Realms\Editor\RosetauBuilder.cs:342, D:\TheAfterlifeofAnkhu\Assets\Ankhu\Realms\Runtime\Director\RealmFollower.cs:34 | Anpu says "I will wait on this step", but he is spawned as an active `RealmFollower` that follows Ankhu everywhere. Rooms C and D deactivate him. A leaves him following | Add a `Stay` mode to `RealmFollower` (leader null or a hold flag) and set it in A, or give him a mark and do not clear it on arrival. At minimum `ctx.anpu.enabled = false` after placing him on the step |
| 13.3 | major | D:\TheAfterlifeofAnkhu\Assets\Ankhu\Realms\Core\Content\CodexContent.cs (no entry) | `Step.Learn("give")` writes `word.give`, which is not in the codex catalog. `CodexStore.Write` returns false silently, so `give` never appears on the roll or in the bubble's known words, though the rules accept it. ROSETAU_DESIGN.md claims give is owned from Wernes, but no Wernes room teaches it | Add `Word("word.give", "Give", "rdi", "D21 D37", ...)` to CodexContent |
| 13.4 | major | D:\TheAfterlifeofAnkhu\Assets\Ankhu\Realms\Runtime\Director\RealmObjectView.cs (no audio), builder `BuildLaughA` | The puzzle is a sound puzzle (where does the laugh answer from) but there is no laugh audio at all, only glows and captions | Add an AudioSource per pose to `RealmObjectView` (clip per state) and author laugh sounding, fading and held loops, positional on the lit doorway |
| 13.5 | major | D:\TheAfterlifeofAnkhu\Assets\Ankhu\Realms\Editor\RealmSceneBuilder.cs:75 (`Begin`) | `spellCastSfx`, `spellLandSfx`, `spellFailSfx` are never assigned for any realm scene (all five Rosetau scenes have fileID 0). Zac's rule: every interaction needs authored SFX | Author three clips and assign them in `Begin`, or have the director load them from Resources when null |
| 13.6 | major | D:\TheAfterlifeofAnkhu\Assets\Ankhu\Realms\Runtime\Director\RealmSceneDirector.cs (no `room.Advance()` call) | Design says the laugh "comes every few ticks". With no real-time clock, the laugh never comes unless the player types something. A player who stands and listens hears nothing, and the 45 s murmur ("It comes, and it goes") is false. `wait` is not taught anywhere on screen | Either have the director call `room.Advance()` every few seconds in rooms with a Tick while input is idle, or teach `wait` explicitly (first beat caption and the H tier 1 hint) |
| 13.7 | minor | D:\TheAfterlifeofAnkhu\Assets\Ankhu\Realms\Core\RealmRules.cs:308 | Ankhu calls "Hori?" in the first beat. Typing `hori` alone returns UnknownVerb "your mouth cannot shape it yet", because `name` is not a verb here | Add a `name` rule with NameValue hori that answers with a caption ("The laugh does not answer to it.") or route a lone known name to InvalidTarget "The name echoes and is not answered." |
| 13.8 | minor | RosetauScripts.cs:108 and D:\TheAfterlifeofAnkhu\Assets\Ankhu\Realms\Core\Content\WernesPaintedScripts.cs:69 | Ankhu says "I've carried this since the field", but Wernes A's goal puts the clay falcon on the perch. There is no inventory carry-over | Accept as story licence, or have Wernes A's solved beat say he picks it back up |
| 13.9 | minor | RealmRules.cs Judge bare-verb path | Bare `give` after the child appears returns Incomplete "Give what? Name the thing." because both give rules apply | Prefer the rule whose target is a carried item, or dedupe rules with the same Event |
| 13.10 | minor | D:\TheAfterlifeofAnkhu\docs\ROSETAU_DESIGN.md:27 | Design says "Painted memory pm_news". Script plays the film `ros_news` (which exists) | Update the design table |
| 13.11 | minor | D:\TheAfterlifeofAnkhu\Assets\Ankhu\Realms\Core\Lexicon.cs:121 | The success line says "Wesheb" and "Rdi", but typing `wesheb` or `rdi` is NotUnderstood. Only English verbs (and wen, res, hetep, tjes, wekha, ren) are aliases | Add `wesheb`, `rdi`, `sekha`, `weha` aliases |

## 14. E2E script

```e2e canonical
scene Rosetau_A_Laugh
at entry
anpu waits [AC-19]
beat arrive [AC-2]
expect laugh=silent [AC-1]
expect mouth1=dark [AC-1]
expect mouth2=dark [AC-1]
expect mouth3=dark [AC-1]
expect child=hidden [AC-1]
expect falcon=hand [AC-1]
unsolved [AC-1]
use read_lintel [AC-13]
beat read_lintel [AC-13]
say give falcon => UnknownVerb | cannot shape it yet [AC-9]
wait
expect laugh=sounding [AC-3]
beat first_laugh [AC-3]
say echo laugh => Success [AC-4]
expect laugh=held [AC-4]
expect mouth2=lit [AC-4]
expect mouth1=dark [AC-4]
expect mouth3=dark [AC-4]
at mouth2
say open middle doorway => Success [AC-6]
expect mouth2=open [AC-6]
expect round=2 [AC-6]
beat found1 [AC-6]
wait
expect laugh=sounding
say echo => Success [AC-4]
expect mouth1=lit [AC-4]
at mouth1
say open left doorway => Success [AC-6]
expect round=3 [AC-6]
beat found2 [AC-6]
wait
say echo => Success [AC-4]
expect mouth3=lit [AC-4]
at mouth3
say open right doorway => Success [AC-6]
beat found3 [AC-6]
expect child=seen [AC-10]
beat child [AC-10]
verb give [AC-10]
codexhas word.give [AC-14]
# fixed 2026-11-26, this used to fail: no word.give in the codex catalog, see 13.3
say give falcon => Success [AC-11]
expect falcon=given [AC-11]
expect child=holding [AC-11]
solved [AC-11]
beat solved [AC-12]
film ros_news [AC-12]
codexhas memory.news [AC-12]
exit Rosetau_B_Mourners [AC-12]
```

```e2e alt-1
# Refused echo on silence ticks the clock, so two echoes sound and hold the laugh. Echo while fading also holds.
scene Rosetau_A_Laugh
at entry
say echo => Refused | nothing to answer yet [AC-5]
expect laugh=sounding [AC-5]
beat first_laugh [AC-3]
wait
expect laugh=fading
say echo laugh => Success [AC-4]
expect laugh=held
expect mouth2=lit
at mouth2
say open => Success
expect round=2
```

```e2e fail-1
# Wrong doorway, then reopening an opened doorway.
scene Rosetau_A_Laugh
at entry
wait
say echo => Success
at mouth1
say open => Refused | Only stone [AC-7]
beat wrong [AC-7]
expect mouth1=dark
expect laugh=held
at mouth2
say open => Success
expect mouth2=open
nobeat found2
say open => NoChange | This time, nothing has changed. [AC-8]
# fixed 2026-11-26, this used to fail: see 13.1 (returns Refused "Only stone" and fires wrong again)
expect round=2
```
