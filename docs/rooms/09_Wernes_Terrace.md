# 09 Wernes_Terrace: The Terrace Above Wernes

## 1. Summary

| Field | Value |
|---|---|
| Scene | `Wernes_Terrace` (D:\TheAfterlifeofAnkhu\Assets\Ankhu\Realms\Generated\Scenes\Wernes_Terrace.unity) |
| Realm | Wernes |
| Title | The Terrace Above Wernes |
| Play order | 9 |
| Previous | 8 BorrowedDawn_Painted (BorrowedDawnExit) |
| Next | 10 Wernes_Hub (first visit) |
| Script | D:\TheAfterlifeofAnkhu\Assets\Ankhu\Realms\Core\Content\WernesPaintedScriptsHub.cs, `WernesPaintedScripts.BuildTerrace()` (lines 23 to 83) |
| Builder | D:\TheAfterlifeofAnkhu\Assets\Ankhu\Realms\Editor\WernesPaintedBuilderHub.cs (terrace part, lines 40 to 125) |
| Runner | `RealmSceneDirector` (D:\TheAfterlifeofAnkhu\Assets\Ankhu\Realms\Runtime\Director\RealmSceneDirector.cs) |

## 2. Intent and story beat

The reunion with Anpu after Borrowed Dawn, where Ankhu has to face the son he only glimpsed in Memory 0. Anpu asks for the name Ankhu said in the doorway and Ankhu speaks it aloud for the first time in the afterlife: **Hori**. It is a one-word puzzle on purpose. The beat is "You have a son." / "Had. I had a son." and the first sight of the grey figures who forgot their names (the lost souls of the story vision). Speaking the name writes `name.hori` in the codex.

Side effect that matters for the whole realm: the director calls `WernesProgress.Reset()` when this scene starts (RealmSceneDirector.cs line 238), so every Wernes room starts undone.

## 3. Layout and zones

Side-on terrace, x 0 to 13.4, one lane. Two fixed cameras (`cam_terrace` wide, `cam_edge` at the parapet).

| Zone id | Where | Purpose |
|---|---|---|
| `edge` | x 8.8 to 12.0 (centre 10.4, size 3.2) | Anpu stands here (x 10.8). Entering it starts the reunion talk |

Ankhu spawns at x 1.4 (outside every zone). Anpu starts at x 10.8 and follows Ankhu (RealmFollower). Mark `mark_steps` at x 13.1 is where Anpu walks after the solve.

Reach: no object has zones, so the name can be spoken from anywhere.

## 4. Elements

| Id | Name | Aliases | Initial | States | Words | Notes |
|---|---|---|---|---|---|---|
| `son` | son | none | `unnamed` | unnamed, named | `name` = "hori", from unnamed to named, requires `asked=yes`, event `hori` | Success: "The name goes out over the fields and something down there turns toward it." Refusal: "Not yet. Go to Anpu first." |
| `asked` | asked | none | `no` | no, yes | none | Set to `yes` by the `reunion` beat (Step.Set) |
| `ankhu` | Ankhu | none | `terrace` | terrace | none | Unused marker |

Usables: none. Views: none (the scene has no RealmObjectView, nothing changes on screen except Anpu and the lines).

## 5. Words

| Verb | Known on entry | What it does here |
|---|---|---|
| `name` | yes (room Verbs) | Speaking "Hori" (or "ren Hori", "say Hori", "my name is Hori", "I am Hori") names the son |

No verb is learned here. Messages:

| Situation | Judgement | Message |
|---|---|---|
| "Hori" before reaching Anpu | Refused | "Not yet. Go to Anpu first." |
| "Hori" again after it was accepted | NoChange | "This time, nothing has changed." (AlreadySpent: target sits in `named`, the rule's From does not contain To) |
| Any other known name ("Ankhu", "Anpu", "Neferet") | InvalidTarget | "The name echoes and is not answered." |
| A known verb ("open", "bind") | UnknownVerb | "You know this word exists, but your mouth cannot shape it yet." |
| Not a word of power ("hello") | NotUnderstood | "The words fall like sand. They are not words of power." |

In practice the NoChange case cannot be reached in play, because the solve freezes Ankhu and leaves the scene.

## 6. Canonical solution

| # | Where | Action | Result | State after | Beats |
|---|---|---|---|---|---|
| 0 | spawn | scene starts | Title "THE TERRACE ABOVE WERNES", caption, then free | asked=no | `arrive` |
| 1 | `edge` | walk to Anpu | Ankhu frozen, dialogue, Anpu: "Say it again." | asked=yes | `reunion` |
| 2 | `edge` | type "Hori" | Success | son=named, solved | `solved` |
| 3 | | solve beat | Anpu "You have a son." talk, codex writes `name.hori`, Anpu walks to `mark_steps`, grey figures caption, "They forgot." | | `solved` |
| 4 | | Step.Next | fade to `Wernes_Hub` | | |

## 7. Alternate valid orders

- Any accepted phrasing of the name: "ren Hori", "say Hori", "my name is Hori", "I am called Hori". `Tokenize` lowercases, so "HORI" works.
- Speaking from anywhere once `asked=yes` (the son has no zone).

Orders that fail: saying the name before the reunion (Refused, no change). There is no softlock: the reunion beat fires on the first entry to `edge` and nothing else can change `asked`.

## 8. Failure, refusal and reset behaviour

- Before the reunion: "Not yet. Go to Anpu first." No world clock, so nothing moves on a refusal.
- Idle: `murmur` after 40 s with no success while unsolved, once: Ankhu "A small name. I used to say it a hundred times a day."
- 75 s with no success: the room hint, which is "Anpu is waiting at the edge of the terrace." while asked=no, and "Anpu asked for the name you said in the doorway. Speak it." after.
- H key: hint tiers from `RealmHintSolver`. The solver plays `hori` from the current state. Before the reunion `asked=no` and no rule can change it (it is set by a beat), so H gives only the nudge. After the reunion H gives nudge, "The room is waiting for a name.", "Say: hori."
- No undo or reset in this scene.

## 9. Beats, memory films and codex

| Beat | Trigger | Flags | Steps |
|---|---|---|---|
| `arrive` | Start | none | Freeze, Title "THE TERRACE ABOVE WERNES" 4 s, Caption "Steps cut into the cliff go down toward green fields. Anpu is waiting at the edge.", Free |
| `reunion` | EnterZone `edge` (once) | none | Freeze, Set asked=yes, Ankhu "You said you would be here.", Anpu "I am always where I say.", Ankhu "In there, when the light came through...", Anpu "Say it again.", Free |
| `murmur` | Idle 40 s | none | Ankhu "A small name. I used to say it a hundred times a day." |
| `solved` | Solved | none | Freeze, 5 lines (Anpu "You have a son." ... "Then the field has waited a long time. Come."), Write `name.hori`, Move Anpu to `mark_steps`, Wait 1.2, Caption grey figures, Ankhu "Who are they?", Anpu "They forgot.", Wait 1.0, Next |

No memory film. Codex: `name.hori` (a keystone page in CodexRollContent, so it is presented on the open roll with the writing hand).

## 10. SFX and audio

| Sound | Source | State |
|---|---|---|
| Ambience | `RealmKit.Ambience` = D:\TheAfterlifeofAnkhu\Assets\Ankhu\Opening\Audio\walk_ambience.wav (same loop as every realm scene) | assigned |
| Spell cast, land, fail | `RealmSceneDirector.spellCastSfx / spellLandSfx / spellFailSfx` | **not assigned** (fileID 0 in the saved scene). Speaking the name has no cast or land sound |
| Ankhu's voice for the name | `RealmVoice.Find("name.hori")` | **missing**: D:\TheAfterlifeofAnkhu\Assets\Ankhu\Realms\Audio\Resources\RealmVoice\ has name.ankhu but no name.hori, so the climax line is silent |
| Dialogue voice | keys `dialogue.ankhu.wernes_terrace.1..`, `dialogue.anpu.wernes_terrace.1..` | **none recorded**. All lines hold on reading time |
| Footsteps | `RealmKit.Footsteps()` on WalkPlayer | assigned |
| Codex write | CodexRollSfx during the `name.hori` presentation | present (codex system) |

## 11. Exit conditions

The goal `son=named` must hold. The `solved` beat ends with `Step.Next()`, which calls `WernesProgress.MarkDone("Wernes_Terrace")` (no-op, not a Wernes room) and fades to `RealmSceneDirector.NextSceneName()` = `PlayOrder.ExitOf("Wernes_Terrace")` = the next built chain scene, `Wernes_Hub`.

## 12. Acceptance criteria

- AC-1 [auto] On load the `arrive` beat fires and Ankhu is frozen until it ends.
- AC-2 [auto] Saying "Hori" before reaching Anpu is Refused with "Not yet. Go to Anpu first." and `son` stays `unnamed`.
- AC-3 [auto] Entering zone `edge` fires `reunion` once and sets `asked=yes`.
- AC-4 [auto] Saying "Hori" after the reunion is a Success, sets `son=named` and solves the room.
- AC-5 [auto] A different name ("Ankhu") is InvalidTarget with "The name echoes and is not answered." and nothing changes.
- AC-6 [auto] The `solved` beat fires once and the codex holds `name.hori` afterwards.
- AC-7 [auto] The room exits to `Wernes_Hub`.
- AC-8 [manual] Starting this scene clears Wernes progress (PlayerPrefs `ankhu.wernes.done` and `ankhu.wernes.last` are empty), so the hub that follows plays its first-visit beat. (Checked indirectly by the chain run: the hub after the terrace must fire `first`.)
- AC-9 [manual] Anpu stands at the parapet on entry and walks toward the steps (x 13.1) after the solve.
- AC-10 [manual] The glyph flight for "Hori" shows the cartouche signs G5 D21 M17 and is readable.
- AC-11 [manual] Ankhu's voice speaks "Hori" (currently fails, see section 13).
- AC-12 [auto] Anpu waits at the terrace edge and does not walk after Ankhu (fixed 2026-11-26, he used to follow him).

## 13. Code vs intent disagreements

| # | Severity | Where | Problem | Proposed fix |
|---|---|---|---|---|
| T1 | major | D:\TheAfterlifeofAnkhu\Assets\Ankhu\Realms\Audio\Resources\RealmVoice\ | No `name.hori.wav`. The director looks up `name.` + transliteration (RealmSceneDirector.cs line 422), so the most important word in the scene is silent | Record `name.hori` with the A_grave_baritone voice (tools/voice) |
| T2 | major | Wernes_Terrace.unity, RealmSceneBuilder.cs `Begin` (line 75) | `spellCastSfx`, `spellLandSfx`, `spellFailSfx` are never assigned by any builder. Every spell in this scene (and every Wernes scene) is silent apart from voice. Breaks Zac's "every interaction needs authored SFX" rule | Author three clips with earshot and assign them in `RealmSceneBuilder.Begin` so every realm scene gets them |
| T3 | minor | WernesPaintedScriptsHub.cs line 58 onward | No dialogue line is voiced (no `dialogue.*.wernes_terrace.*` clips) | Voice pass |
| T4 | minor | Spec v2 section 2 row 9 calls this "WK5, 3D walk, 5 cameras" | Built as a painted single-lane scene with 2 cameras and a one-word puzzle. PLAY_ORDER.md matches the code | Update the spec, or accept |
| T5 | minor | RealmHintSolver.cs line 32 | Before the reunion, H only shows the nudge (asked is set by a beat, which the solver does not model). Acceptable here because the nudge says where to go | none |

## 14. E2E script

```e2e canonical
scene Wernes_Terrace
beat arrive [AC-1]
expect asked=no
say Hori => Refused | Not yet. Go to Anpu first. [AC-2]
expect son=unnamed [AC-2]
unsolved
at edge
anpu waits [AC-12]
beat reunion [AC-3]
expect asked=yes [AC-3]
say Hori => Success [AC-4]
expect son=named [AC-4]
solved [AC-4]
beat solved [AC-6]
codexhas name.hori [AC-6]
exit Wernes_Hub [AC-7]
```

```e2e alt-1
scene Wernes_Terrace
at edge
beat reunion
say ankhu => InvalidTarget | The name echoes and is not answered. [AC-5]
expect son=unnamed [AC-5]
say ren hori => Success [AC-4]
solved
exit Wernes_Hub
```

```e2e fail-1
scene Wernes_Terrace
say open => UnknownVerb | your mouth cannot shape it yet
say hello there => NotUnderstood
say my name is hori => Refused | Not yet. Go to Anpu first. [AC-2]
nobeat reunion
unsolved
```
