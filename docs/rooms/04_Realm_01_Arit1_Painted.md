# 04 Realm_01_Arit1_Painted: The First Gate

## 1. Summary

| Field | Value |
|---|---|
| Scene | `Realm_01_Arit1_Painted` (greybox twin `Realm_01_Arit1` kept, not in the chain) |
| Realm | The First Gate (Arit 1) |
| Title card | "THE FIRST GATE / Spell for knowing the gates" |
| Play order | 4 of 21 |
| Previous scene | `Opening_WalkDuat` (3) |
| Next scene | `Teach1_Cistern_Painted` (5) |
| Engine | RealmSceneDirector + RealmRoom (`Ankhu.Realms.Core`) |
| Script source | D:\TheAfterlifeofAnkhu\Assets\Ankhu\Realms\Core\Content\WernesScripts.cs, `WernesScripts.BuildGate()` (lines 55 to 170) |
| Scene builder | D:\TheAfterlifeofAnkhu\Assets\Ankhu\Realms\Editor\FirstGatePaintedBuilder.cs, `BuildFirstGate()`. It sets `director.sceneName = "Realm_01_Arit1"`, so the painted scene runs the greybox script by name |
| Exit code | D:\TheAfterlifeofAnkhu\Assets\Ankhu\Realms\Runtime\Director\RealmSceneDirector.cs `NextSceneName()` then D:\TheAfterlifeofAnkhu\Assets\Ankhu\Opening\Runtime\PlayOrder.cs `ExitOf()` |

## 2. Intent and story beat

Book of the Dead spells 144 and 147 ("knowing the gates"): each gate has a keeper, a guardian and an announcer, and the dead pass by knowing their names. This is the first time Ankhu uses the one verb he has, **name**, and the room teaches the core contract of the whole game: knowledge, not force, opens the Duat. A gate is "opened by being known".

Story beats:
- Anpu gives Ankhu the **empty codex** (the papyrus roll he will write his own road into). "So is every road, before it is walked."
- The guardian repeats what Anpu said on the road ("A son. A father. A husband. A man who stopped speaking."), the first hint that the gods already know Ankhu's unfinished story.
- On the solve the announcer names him as "Ankhu, who wore the face of Anpu over the dead", the first public acknowledgement of his life as a funerary priest. Anpu then leads him down into his pavilion (the teaching rooms).

## 3. Layout and zones

Painted court in a cavern, one fixed camera `cam_court` (slow 90 s push from FOV 42 to 38). Ankhu spawns at (0, 0, -8.6) facing the pylon, Anpu at (-1.3, 0, -9.0). A water channel crosses the court at z = 6.2 with a bridge gap at |x| < 1.5. The pylon gate sits at z = 9. Blockers fence the court (x = ±7.6, z = -9.6) and the avenue beyond (x = ±2.2).

| Zone id | Centre | Size | Priority | What stands there |
|---|---|---|---|---|
| `court` | (0, 1, -0.6) | 16 x 4 x 13 | 1 | The whole court. Starts one step in front of the spawn so the announcer calls as Ankhu walks in |
| `plinth` | (4.2, 1, 2.9) | 3 x 4 x 2.4 | 2 | In front of the guardian's plinth (right, back). Usable `guardian_plinth` |
| `fallen` | (4.6, 1, -2.4) | 3.6 x 4 x 3.4 | 2 | Around the keeper's bier (right, front). Usable `keeper_figure` |
| `beyond` | (0, 1, 13) | 4 x 4 x 6 | 3 | The avenue behind the gate. Only reachable when the gate's `DoorBar` collider is hidden (state `open`) |
| (none) | spawn area z < -7.1 and the strip 5.9 < z < 10 | | | No zone. Words still work here because no object has zones |

Reach: no room object lists zones, so every name reaches from anywhere, including outside every zone. Usables need their zone.

## 4. Elements

### Room objects

| Id | Name | Aliases | Initial | States | Words that move it | Success | Refusal |
|---|---|---|---|---|---|---|---|
| `announcer` | announcer | announcer, herald, caller | `unknown` | unknown, known, answered | name "sad of voice" / "sad-of-voice" / "of voice": known to answered, event `announcer_answered`. Set to known by beat `gate_calls` | (empty, the beat `a` speaks) | "You have not learned that name. A name you do not know is only noise here." |
| `guardian` | guardian | guardian, watcher | `unknown` | unknown, known, answered | name "eavesdropper" / "eaves-dropper" / "eaves dropper": known to answered, event `guardian_answered`. Set to known by beat `read_plinth` | (empty, beat `g`) | same as above |
| `keeper` | keeper | keeper | `unknown` | unknown, known, answered | name "face-downward" / "face downward" / "numerous of shapes" / "face-downward numerous of shapes" / "face downward numerous of shapes": known to answered, event `keeper_answered`. Set to known by beat `look_fallen` | (empty, beat `k`) | same as above |
| `gate` | gate | gate, arit, door, doors | `shut` | shut, open | auto rule: shut to open when all three are `answered`, event `gate_open`. name "ankhu": From `never`, so it never applies | | "The gate is still listening. It wants the names of those who keep it." |

Views: each figure has poses `unknown`/`known` (dormant card) and `answered` (dormant fades out, awake card and warm glow fade in, 1.8 to 2.2 s). The gate has poses `shut` (leaves closed, `DoorBar` collider on, beyond light 0) and `open` (leaves swing ±78 degrees over 3.5 s, shaft glow, `DoorBar` hidden, beyond light 4.5).

### Usables

| Id | Zone | Prompt | Deed | When |
|---|---|---|---|---|
| `guardian_plinth` | `plinth` | Read the plinth | none | always |
| `keeper_figure` | `fallen` | Look at the fallen figure | none | always |

## 5. Words

| Verb | Known on entry | What it does here |
|---|---|---|
| `name` | Yes (`Verbs = { "name" }`), although Anpu only teaches it in beat `gate_calls`, which also writes `word.name` | Speaks a keeper's name. Typed as the bare name, or "I am X", "my name is X", "say X", "speak X", "name X" |

Every other verb ("open gate", "wake", "still") is outside the room's verb set.

| Situation | Judgement | Message |
|---|---|---|
| A verb Ankhu has not learned ("open the gate") | `UnknownVerb` | "You know this word exists, but your mouth cannot shape it yet." |
| A keeper's name before its clue was found (object still `unknown`) | `Refused` | "You have not learned that name. A name you do not know is only noise here." (first one also fires beat `refused`) |
| A keeper's name a second time (object `answered`) | `NoChange` | "This time, nothing has changed." (`RealmRoom.AlreadySpoken`) |
| "Ankhu" / "I am Ankhu" while the gate is shut | `Refused` | "The gate is still listening. It wants the names of those who keep it." |
| "Ankhu" after the gate opened | `NoChange` | "This time, nothing has changed." |
| A known name with no rule here ("Anpu", "Hori") | `InvalidTarget` | "The name echoes and is not answered." |
| Gibberish | `NotUnderstood` | "The words fall like sand. They are not words of power." |

## 6. Canonical solution

Zac 2026-11-26 playtest: Face-downward lies nearest the way in, so he is named first, and that first name is a guided spell. The plinth only opens once he has answered, and Anpu only names the announcer once the guardian has. The order is now fixed.

| # | Where | Action | Expected result | State after | Beats |
|---|---|---|---|---|---|
| 0 | spawn | scene starts | frozen, title card, Anpu speaks, codex gift plays (the roll appears only now), two codex writes | all unknown, gate shut | `arrive` (codex `lore.codex`, `lore.gate_one`) |
| 1 | walk into `court` | walk | frozen, announcer calls "Who comes?" with talk rings by his head, Anpu sends Ankhu to the figure lying on its face (anpu12, unvoiced) | unchanged | `gate_calls` |
| 2 | `fallen` | press E on `keeper_figure` | frozen, Ankhu and Anpu name it, Anpu says "Try his", then the guided spell: the bubble shows ENTER, then grey letters FACE-DOWNWARD to type over, then ENTER to speak | keeper = known | `look_fallen` (codex `clue.keeper_name`, `word.name`, cue `teach_speak:Face-downward`) |
| 3 | anywhere | type `face-downward` | Success, figure wakes, Anpu sends Ankhu to the plinth (anpu13, unvoiced) | keeper = answered | `k` |
| 4 | `plinth` | press E on `guardian_plinth` (usable only now) | frozen, Ankhu reads the name, the guardian repeats Anpu's words | guardian = known, flag `plinth_read` | `read_plinth`, `plinth_flag` (codex `clue.guardian_name`) |
| 5 | anywhere | type `eavesdropper` | Success, then Anpu names the announcer | guardian = answered, announcer = known | `g` (codex `clue.announcer_name`) |
| 6 | anywhere | type `sad of voice` | Success, auto rule opens the gate | announcer = answered, gate = open, room solved | `a`, then `opened` |
| 7 | walk through the gate into `beyond` | walk | freeze, fade | | `through` (NextScene) to `Teach1_Cistern_Painted` |

## 7. Alternate valid orders

- The three names now come in one order: keeper, guardian, announcer. Each clue unlocks only after the previous name is answered.
- The bare-name and prefixed forms are interchangeable: "say eavesdropper", "I am...", "face downward numerous of shapes".
- Step 1 cannot be skipped: every route from the spawn to the bier crosses `court`.

Orders that fail: speaking any name before its clue (Refused, no state change). Pressing E at the plinth before Face-downward has answered (no prompt). Walking to `beyond` before the gate opens (the `DoorBar` collider blocks it).

## 8. Failure, refusal and reset behaviour

- First `Refused` of the room fires beat `refused`: Anpu says "A gate is not opened by force. It is opened by being known." Once only.
- No world clock, no undo in the director. Refusals change nothing.
- Idle: no Idle beats. After 75 s without a success the director shows `room.Hint()` for 8 s. H shows the three sealed tiers from `RealmHintSolver`. Because story beats (not rules) move the figures from unknown to known, the solver finds no path while a clue is missing and returns only the nudge.
- Authored hints (first match wins): guardian unknown, then keeper unknown, then announcer known, guardian known, keeper known.
- No reset is needed: the room cannot softlock.

## 9. Beats, memory films and codex

| Beat | Trigger | Flags | Steps |
|---|---|---|---|
| `arrive` | Start | | Freeze, Title (4.5 s), Anpu 1, Ankhu 1, Anpu 2, Cue `anpu_offers_codex` (CodexGift.Play, roll floats from Anpu to Ankhu), Anpu 3, Ankhu 2, Anpu 4, Write `lore.codex` (presented on the roll), Write `lore.gate_one`, Free |
| `gate_calls` | EnterZone `court` | | Freeze, Cue `announcer_calls`, Announcer 1, Anpu 12 (unvoiced), Free |
| `look_fallen` | Use `keeper_figure` | | Freeze, Ankhu 6, Anpu 8, Set keeper=known, Write `clue.keeper_name`, Write `word.name`, Anpu 6, Cue `teach_speak:Face-downward` (guided first spell), Free |
| `read_plinth` | Use `guardian_plinth` (only when keeper answered) | | Freeze, Ankhu 3, Set guardian=known, Write `clue.guardian_name`, Cue `guardian_repeats`, Guardian 1, Ankhu 4, Anpu 7, Free |
| `read_plinth_again` | Use `guardian_plinth` (repeatable) | requires `plinth_read` | Ankhu 5 ("Eavesdropper.") |
| `plinth_flag` | Use `guardian_plinth` | | Flag `plinth_read` |
| `k` | Event `keeper_answered` | | Wait 0.6, Keeper 1, Anpu 13 (unvoiced) |
| `g` | Event `guardian_answered` | | Wait 0.6, Guardian 2, Anpu 5, Set announcer=known, Write `clue.announcer_name` |
| `a` | Event `announcer_answered` | | Wait 0.6, Announcer 2 |
| `refused` | Failure `Refused` | | Anpu 9 |
| `opened` | Solved | | Freeze, Cue `gate_opens`, Wait 2.6, Announcer 3, Write `lore.known_at_gate`, Anpu 10, Anpu 11, Flag `open`, Free |
| `through` | EnterZone `beyond` | requires `open` | Freeze, NextScene |

Memory films: none in this room. Codex entries written: `lore.codex`, `lore.gate_one`, `clue.announcer_name`, `word.name`, `clue.guardian_name`, `clue.keeper_name`, `lore.known_at_gate`.

## 10. SFX and audio

| Source | Asset | Status |
|---|---|---|
| Ambience | D:\TheAfterlifeofAnkhu\Assets\Ankhu\Opening\Audio\walk_ambience.wav (RealmKit.Ambience) | wired |
| Footsteps | RealmKit.Footsteps via WalkPlayer | wired |
| Codex gift | Resources `RealmCodex/sfx_gift` (CodexGift) | wired |
| Codex roll open, close, page | Resources `Sfx/roll/*` (CodexRollSfx) | wired |
| Voice | Resources `RealmVoice/dialogue.<speaker>.arit1.<n>`: anpu 1 to 10, ankhu 1 to 6, announcer 1 to 3, guardian 1 to 2, keeper 1 | `dialogue.anpu.arit1.11` is missing (see 13) |
| Ankhu speaking a name | `RealmVoice/name.<translit>` via `RealmVoice.Find` | not checked here |
| Spell cast, land, fail | `RealmSceneDirector.spellCastSfx`, `spellLandSfx`, `spellFailSfx` | **null in the saved scene** (no builder assigns them) |
| Figure wakes (answered pose), gate leaves swing, beyond light, E use, hint glint | none | **no authored SFX** |

## 11. Exit conditions

Gate `open` (all three keepers `answered`), the `opened` beat has finished (flag `open` set), and Ankhu walks into `beyond`. Beat `through` runs `NextScene`: `WernesProgress.MarkDone` (harmless here), then `SceneFader.FadeTo(NextSceneName())`. Because the active scene is `Realm_01_Arit1_Painted` (not `OpeningSettings.FirstGateScene`), the first branch of `NextSceneName` is skipped and `PlayOrder.ExitOf` returns `Teach1_Cistern_Painted`.

## 12. Acceptance criteria

- AC-1 [auto] On load the `arrive` beat fires and writes `lore.codex` and `lore.gate_one`.
- AC-2 [manual] The codex gift plays: the roll forms over Anpu's palm and floats to Ankhu, with `sfx_gift` audible.
- AC-3 [auto] Entering `court` fires `gate_calls` once. `look_fallen` writes `word.name`, and answering the guardian sets announcer to `known` and writes `clue.announcer_name`.
- AC-4 [auto] Speaking a keeper's name before its clue is Refused with "You have not learned that name" and changes nothing. The first such refusal fires beat `refused`.
- AC-5 [auto] Speaking a verb other than name ("open gate") is UnknownVerb with "your mouth cannot shape it yet".
- AC-6 [auto] E on `guardian_plinth` in zone `plinth` sets guardian to `known`. A second E fires `read_plinth_again`.
- AC-7 [auto] E on `keeper_figure` in zone `fallen` sets keeper to `known`.
- AC-8 [auto] Each correct name moves its figure to `answered` and fires its answer beat (`a`, `g`, `k`).
- AC-9 [auto] Repeating an answered name is NoChange with "This time, nothing has changed."
- AC-10 [auto] The gate opens exactly when the third keeper is answered, in any order, and the room reports solved.
- AC-11 [auto] The `opened` beat fires, writes `lore.known_at_gate` and sets flag `open`.
- AC-12 [auto] Walking into `beyond` after the solve leaves the room for `Teach1_Cistern_Painted`.
- AC-13 [manual] The gate leaves swing open and the beyond light comes up. Ankhu cannot walk through the gate before it opens.
- AC-14 [manual] Every line is voiced, including Anpu's last line.
- AC-15 [manual] Casting, landing and refusing a word, a figure waking and the gate opening each have an audible SFX.
- AC-16 [auto] Saying "Ankhu" once the gate is open is refused with "The gate already stands open", not with "The gate is still listening" (fixed 2026-11-26).
- AC-17 [auto] Anpu walks with Ankhu through the First Gate (he leads him down to his pavilion).

## 13. Code vs intent disagreements

| # | Where | Severity | Finding | Proposed fix |
|---|---|---|---|---|
| 1 | D:\TheAfterlifeofAnkhu\Assets\Ankhu\Realms\Runtime\Director\RealmSceneDirector.cs:58-60, D:\TheAfterlifeofAnkhu\Assets\Ankhu\Realms\Editor\RealmSceneBuilder.cs:75-90 | major | `spellCastSfx`, `spellLandSfx` and `spellFailSfx` are never assigned by any builder (the saved scene has `spellCastSfx: {fileID: 0}`). Every spell in every realm room is silent apart from the voice. Breaks the rule that every interaction has authored SFX | Author three earshot takes and assign them in `RealmSceneBuilder.Begin` (shared by every realm builder), or load them from Resources in `RealmSceneDirector.Awake` when null |
| 2 | FirstGatePaintedBuilder.cs (views) | major | No SFX for figures waking, the gate opening (3.5 s swing), or E use | Add a per-state clip to `RealmObjectView` poses (for example `pose.sfx`) and author gate and wake sounds |
| 3 | WernesScripts.cs:157-166 | minor | `opened` speaks two Anpu lines keyed `dialogue.anpu.arit1.10` and `.11`. Only 1 to 10 were recorded (D:\TheAfterlifeofAnkhu\tools\voice\out\raw_realms_anpu has no arit1.11), so "But I can teach you how to walk them. Below this gate is my pavilion..." plays unvoiced | Record `dialogue.anpu.arit1.11` with the ElevenLabs Anpu voice |
| 4 | WernesScripts.cs:62 and 115-124 | minor | `name` is in the verb set from the start, but Anpu teaches it in `gate_calls` (which writes `word.name`). Before the court beat a name gets "You have not learned that name" rather than the not-yet-learned verb message. Harmless because the court cannot be bypassed, but the codex and the rules disagree about when the verb is learned | Either start with an empty verb set and add `Step.Learn("name")` to `gate_calls`, or leave as is and accept it |
| 5 | WernesScripts.cs | fixed 2026-11-26 | Hint priority: "guardian unknown" is listed first, so after Anpu says "Try his" the H nudge still sends Ankhu to the plinth instead of telling him to speak Sad of Voice | Move the "announcer known" hint to the top |
| 6 | WernesScripts.cs:98-99 | minor | Both usables stay prompting after use. `keeper_figure` has no repeat beat, so a second E shows a prompt and does nothing | Add `When = { keeper unknown }` to the usable, or a repeat line like `read_plinth_again` |
| 7 | FirstGatePaintedBuilder.cs:310 vs director | minor | Cues `announcer_calls`, `guardian_repeats` and `gate_opens` have no CueHook in this scene, so they do nothing. The gate's motion comes only from its state pose, which is fine, but the announcer and guardian get no animation on their lines | Add CueHooks (a glow pulse on the speaking card) or drop the cues |
| 8 | RealmSceneDirector.cs:748-760 | minor | `NextSceneName`'s special case for `OpeningSettings.FirstGateScene` never matches the painted scene. The result is still right because `PlayOrder.ExitOf` returns the painted Teach1. Dead code | Remove the special case or compare with `PlayOrder.IndexOf` |

No softlock: every state reaches the goal.

## 14. E2E script

```e2e canonical
scene Realm_01_Arit1_Painted
beat arrive                                   [AC-1]
codexhas lore.codex                           [AC-1]
codexhas lore.gate_one                        [AC-1]
expect announcer=unknown
expect gate=shut
at court
anpu follows [AC-17]
beat gate_calls                               [AC-3]
expect announcer=unknown
at fallen
use keeper_figure                             [AC-7]
beat look_fallen                              [AC-7]
expect keeper=known                           [AC-7]
codexhas word.name                            [AC-3]
codexhas clue.keeper_name
verb name
say face-downward => Success                  [AC-8]
expect keeper=answered
beat k                                        [AC-8]
unsolved
at plinth
use guardian_plinth                           [AC-6]
beat read_plinth                              [AC-6]
flag plinth_read
expect guardian=known                         [AC-6]
say eavesdropper => Success                   [AC-8]
expect guardian=answered
beat g                                        [AC-8]
expect announcer=known                        [AC-3]
codexhas clue.announcer_name                  [AC-3]
unsolved                                      [AC-10]
say sad of voice => Success                   [AC-8]
expect announcer=answered                     [AC-8]
beat a                                        [AC-8]
expect gate=open                              [AC-10]
solved                                        [AC-10]
beat opened                                   [AC-11]
codexhas lore.known_at_gate                   [AC-11]
flag open                                     [AC-11]
at beyond
beat through                                  [AC-12]
exit Teach1_Cistern_Painted                   [AC-12]
```

```e2e alt-1
# Prefixed name forms, and a second read of the plinth.
scene Realm_01_Arit1_Painted
at court
beat gate_calls
at fallen
use keeper_figure
say face downward numerous of shapes => Success     [AC-8]
expect keeper=answered
at plinth
use guardian_plinth
say say eaves dropper => Success                    [AC-8]
expect guardian=answered
use guardian_plinth                                 [AC-6]
beat read_plinth_again                              [AC-6]
unsolved                                            [AC-10]
say sad-of-voice => Success                         [AC-10]
expect gate=open                                    [AC-10]
solved
beat opened
at beyond
exit Teach1_Cistern_Painted
```

```e2e fail-1
# Refusals, not-learned and already-spoken messages.
scene Realm_01_Arit1_Painted
say eavesdropper => Refused | You have not learned that name      [AC-4]
beat refused                                                      [AC-4]
expect guardian=unknown                                           [AC-4]
say open the gate => UnknownVerb | your mouth cannot shape it yet [AC-5]
say ankhu => Refused | The gate is still listening
say anpu => InvalidTarget | The name echoes
say blorp => NotUnderstood | not words of power
at court
say sad of voice => Refused | You have not learned that name      [AC-4]
at fallen
use keeper_figure
say face-downward => Success
say face-downward => NoChange | This time, nothing has changed.   [AC-9]
at plinth
say eavesdropper => Refused | You have not learned that name      [AC-4]
use guardian_plinth
say eavesdropper => Success
say sad of voice => Success
solved
say ankhu => Refused | already stands open            [AC-16]
nobeat through
```
