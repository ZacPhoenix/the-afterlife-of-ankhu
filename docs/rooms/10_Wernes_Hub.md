# 10 Wernes_Hub: The Memory House (and 15, the final return)

## 1. Summary

| Field | Value |
|---|---|
| Scene | `Wernes_Hub` (D:\TheAfterlifeofAnkhu\Assets\Ankhu\Realms\Generated\Scenes\Wernes_Hub.unity) |
| Realm | Wernes |
| Title | The Memory House |
| Play order | 10, and again between every Wernes room, and 15 (all four done) |
| Previous | 9 Wernes_Terrace on the first visit, then each of Wernes_A_Field, Wernes_B_Shabti, Wernes_C_House, Wernes_D_Bricks |
| Next | the first Wernes room not done (A, B, C, D in that order), or 16 Wernes_Finale once all four are done |
| Script | D:\TheAfterlifeofAnkhu\Assets\Ankhu\Realms\Core\Content\WernesPaintedScriptsHub.cs, `WernesPaintedScripts.BuildHub()` (lines 93 to 197) |
| Progress store | D:\TheAfterlifeofAnkhu\Assets\Ankhu\Opening\Runtime\WernesProgress.cs (PlayerPrefs `ankhu.wernes.done`, `ankhu.wernes.last`) |
| Routing | D:\TheAfterlifeofAnkhu\Assets\Ankhu\Opening\Runtime\PlayOrder.cs `ExitOf` (lines 107 to 117) |
| Builder | D:\TheAfterlifeofAnkhu\Assets\Ankhu\Realms\Editor\WernesPaintedBuilderHub.cs (hub part, lines 127 to 240) |

## 2. Intent and story beat

Ankhu's house in Wernes, built for him at his burial and never lived in. Its false door (Book of the Dead 92 on the lintel: "Wide-open or tight-shut and asleep, open wide for the soul therein") opens only for its owner, so the one puzzle is to say his own name, **Ankhu**. Each time he says it the door opens onto the next memory room he has not finished. Four panels beside the door light as rooms are done (falcon over water, a shabti, a door, the birth goddess), and Anpu says something new on each return. The arc is self-acceptance in small steps: a man who said his name quietly all his life learns to say it so the door hears.

## 3. Layout and zones

One room, x 0.2 to 11.9, side-on, two cameras (`cam_house` wide, `cam_door` close).

| Zone id | Where | Purpose |
|---|---|---|
| `door` | x 4.7 to 7.3 (centre 6.0, size 2.6), z -1.6 to 2.8 | The false door. The read usable is here, and the door object answers only from here |

Ankhu spawns at x 1.2. Anpu spawns at x 7.4 by the door, then follows Ankhu (RealmFollower). Mark `mark_door` (x 7.2) exists but no step uses it. Panels A to D hang at x 3.5, 4.6, 7.4, 8.5.

## 4. Elements

| Id | Name | Aliases | Initial | States | Words | Notes |
|---|---|---|---|---|---|---|
| `fdoor` | false door | false door, door, ka door | `shut` | shut, open | `name` = "ankhu", shut to open, event `door_opens`, zone `door` | Success "The painted door gives way like water. Beyond it, light on a field." Refusal "It is already open." (unreachable, see H4) |
| `panel_a` .. `panel_d` | panel A..D | none | `dark` | dark, lit | none | Set to `lit` by the `panel_X` Start beats when `progress.done.X` |
| `false_door_panels` | lit panels | none | `0` | 0..4 | none | Set by `count_N` beats. No view |
| `ankhu` | Ankhu | none | `house` | house | none | marker |

Usables:

| Id | Zone | Prompt | Deed | When |
|---|---|---|---|---|
| `read_door` | `door` | Read the false door | none | always |

Views: `fdoor` (shut, open poses with glow), `panel_a..d` (dark, lit with light).

## 5. Words

| Verb | Known on entry | What it does |
|---|---|---|
| `name` | yes | "Ankhu" (also "ren Ankhu", "I am Ankhu", "my name is Ankhu") opens the false door |

| Situation | Judgement | Message |
|---|---|---|
| "Ankhu" in zone `door` or outside every zone | Success | "The painted door gives way like water..." |
| Another name ("Hori", "Anpu") | InvalidTarget | "The name echoes and is not answered." |
| "Ankhu" again after the door opened | NoChange | "This time, nothing has changed." (cannot be reached in play: the solve freezes and leaves) |
| Any verb ("open door") | UnknownVerb | "You know this word exists, but your mouth cannot shape it yet." |

## 6. Canonical solution (every visit)

| # | Where | Action | Result | Beats |
|---|---|---|---|---|
| 0 | spawn | scene starts | panel and count beats set state, then the visit talk | `panel_X`, `count_N`, then `first` or `back_X` |
| 1 | `door` | E: Read the false door | caption of BD 92 and Ankhu's reading | `read_door` (repeatable) |
| 2 | `door` | type "Ankhu" | Success, fdoor=open, solved | `solved` or `solved_all` |
| 3 | | Next | fade to the next room | |

### Visit states

`WernesProgress.Flags()` gives the director `progress.done.X` for each finished room, `progress.back.X` for the last finished room, `progress.count.N`, and `progress.all` when all four are done.

| Visit | Progress before | Start beats | Talk | Solve beat | Exit |
|---|---|---|---|---|---|
| 10 first | none (terrace reset) | `count_N` none (count.0 has no beat) | `first`: Title "THE MEMORY HOUSE", "This is your house in Wernes...", "Read what is written on it." | `solved` | Wernes_A_Field |
| after A | done A, back A, count 1 | `panel_A`, `count_1` | `back_a`: "He said the falcon would not fall because I was holding him." | `solved` | Wernes_B_Shabti |
| after B | done A B, back B, count 2 | `panel_A`, `panel_B`, `count_2` | `back_b`: "The shabti worked for me all those years..." | `solved` | Wernes_C_House |
| after C | done A B C, back C, count 3 | panels A B C, `count_3` | `back_c`: "He said he would bring me something from the north." | `solved` | Wernes_D_Bricks |
| 15 after D | done A B C D, back D, count 4, all | panels A to D, `count_4` | `back_d`: "She was the loudest person in our street..." then "There is one more field with your name on it." | `solved_all`: door opens on the terrace steps, "One of them looked up." | Wernes_Finale |

## 7. Alternate valid orders

- Reading the door is optional. Saying the name straight away works.
- Any accepted name phrasing.
- Room order is fixed by `WernesProgress.NextRoom()` (A, B, C, D). There is no player choice of room.

## 8. Failure, refusal and reset behaviour

- Wrong name: InvalidTarget, nothing changes. No world clock.
- Idle 45 s: `murmur`, Anpu "It is your door. Tell it so." (once per visit).
- 75 s idle: hint "Read the false door (E). It was made for one person only."
- H: nudge, "The room is waiting for a name.", "Say: ankhu."
- Progress is reset only by the terrace. Each room marks itself done on its own exit.

## 9. Beats, memory films and codex

| Beat | Trigger | Requires | Forbids | Steps |
|---|---|---|---|---|
| `panel_A`..`panel_D` | Start | `progress.done.X` | | Set panel_x=lit |
| `count_1`..`count_4` | Start | `progress.count.N` | | Set false_door_panels=N |
| `first` | Start | `progress.count.0` | | Freeze, Title, caption, 3 lines, Free |
| `back_a` | Start | `progress.back.A` | | Freeze, caption, 4 lines, Free |
| `back_b` | Start | `progress.back.B` | | Freeze, caption, 2 lines, Free |
| `back_c` | Start | `progress.back.C` | | Freeze, caption, 3 lines, Free |
| `back_d` | Start | `progress.back.D` | | Freeze, caption, 6 lines with a wait, Free |
| `read_door` | Use `read_door` (repeatable) | | | Caption BD 92, Ankhu line |
| `murmur` | Idle 45 s | | | Anpu line |
| `solved` | Solved | | `progress.all` | Freeze, caption, Anpu "Go on. I will be here.", Wait 1, Next |
| `solved_all` | Solved | `progress.all` | | Freeze, caption, 2 lines, Wait 1.5, Next |

No memory films and no codex writes in the hub.

## 10. SFX and audio

| Sound | State |
|---|---|
| Ambience walk_ambience.wav | assigned |
| Spell cast, land, fail | **not assigned** (fileID 0) |
| Ankhu's voice for the name | D:\TheAfterlifeofAnkhu\Assets\Ankhu\Realms\Audio\Resources\RealmVoice\name.ankhu.wav exists |
| Door opening, panel lighting | **no SFX** (RealmObjectView has no audio, no cue plays a sound) |
| Dialogue voice | none recorded for `wernes_hub` keys |

## 11. Exit conditions

Goal `fdoor=open`. `solved` (or `solved_all`) ends with `Step.Next()`. `WernesProgress.MarkDone("Wernes_Hub")` is a no-op. The target is `PlayOrder.ExitOf("Wernes_Hub")`: the first room in A, B, C, D not done and in Build Settings, else `NextBuiltAfter(Wernes_D_Bricks)` = `Wernes_Finale`.

## 12. Acceptance criteria

- AC-1 [auto] First visit (no progress): `first` fires, no `back_X` fires, every panel is `dark`, `false_door_panels=0`.
- AC-2 [auto] Using `read_door` in zone `door` fires the `read_door` beat, and it can fire again.
- AC-3 [auto] Saying "Ankhu" opens the false door (fdoor=open) and solves the room.
- AC-4 [auto] A wrong name is InvalidTarget and the door stays shut.
- AC-5 [auto] First visit exits to `Wernes_A_Field`.
- AC-6 [auto] After A: `panel_a=lit`, others dark, count 1, `back_a` fires, `first` does not, exit `Wernes_B_Shabti`.
- AC-7 [auto] After A B: panels a b lit, count 2, `back_b` fires, exit `Wernes_C_House`.
- AC-8 [auto] After A B C: panels a b c lit, count 3, `back_c` fires, exit `Wernes_D_Bricks`.
- AC-9 [auto] After A B C D: all panels lit, count 4, `back_d` fires, `solved_all` fires and `solved` does not, exit `Wernes_Finale`.
- AC-10 [manual] Panels already earned appear lit on entry, and the newest one lights visibly.
- AC-11 [manual] Anpu waits by the false door (PLAY_ORDER.md row 10). Currently he follows Ankhu (see H3).
- AC-12 [manual] The door opening reads as a glowing painted door giving way, with sound.

The harness should treat the last letter of a `progress` line as the room just finished (`progress.back.X`), and no `progress` line as a cleared store.

## 13. Code vs intent disagreements

| # | Severity | Where | Problem | Proposed fix |
|---|---|---|---|---|
| H1 | major | WernesPaintedScriptsHub.cs line 144 (`back_a`) | After room A, Ankhu says "He said the falcon would not fall because I was holding him." That line is the content of Memory A, which plays in room B (`memA_river_sunset`, WernesPaintedScripts.cs line 371). Room A only shows the tease. Spec v2 H1 has "There was a man in the water with a child on his shoulders. I could not see the man's face." for this return, and gives the "would not fall" exchange to H2 after B | Swap in the spec's H1 lines for `back_a`, and move the falcon line into `back_b` |
| H2 | major | RealmRules.cs `Reaches` line 515 | `zone == null` counts as in reach. The hub has one zone, so "Ankhu" opens the door from anywhere in the room, not only at the door. The zone on `fdoor` does nothing, and the OutOfReach message can never show | Treat a null zone as out of reach for objects that have zones (and check other rooms for gaps between zones), or drop the zone from the door |
| H3 | minor | WernesPaintedBuilderHub.cs line 228 and RealmSceneBuilder.cs `Cast` line 109 | PLAY_ORDER.md says Anpu waits by the false door. In the built scene he is a RealmFollower that trails Ankhu, and `mark_door` is never used | Add `Step.Move("Anpu", "mark_door")` to every Start talk beat, or a follower flag to stay put |
| H4 | minor | WernesPaintedScriptsHub.cs line 114 | Refusal "It is already open." can never show: a second "Ankhu" is AlreadySpent and says "This time, nothing has changed." | Drop the dead text |
| H5 | minor | WernesProgress.cs line 44 | `LastDone` returns null under `Simulate`, so PlayOrderCheck never sees `progress.back.X` and cannot prove the return talk fires | Simulate the last letter as well |
| H6 | minor | Spec v2 section 3 | Spec has three rooms, three panels, the barque passing the house at H0, H2, H3 and different dialogue. Code has four rooms (D added by Zac's pick 4) and no barque in the hub (it moved to the finale) | Update the spec to match |
| H7 | major | Wernes_Hub.unity | Spell cast, land and fail SFX unassigned, no door or panel SFX | as T2 in 09 |
| H8 | minor | WernesPaintedScriptsHub.cs line 124 | Panel beats use animated SetState every visit, so earned panels animate on from dark each time instead of starting lit | Set earned panels with `SetState(s, true)` (instant) and animate only the `back.X` panel |

## 14. E2E script

```e2e canonical
scene Wernes_Hub
beat first [AC-1]
nobeat back_a [AC-1]
expect panel_a=dark [AC-1]
expect panel_d=dark [AC-1]
expect false_door_panels=0 [AC-1]
at door
use read_door [AC-2]
beat read_door [AC-2]
use read_door [AC-2]
say Ankhu => Success [AC-3]
expect fdoor=open [AC-3]
solved [AC-3]
beat solved
nobeat solved_all
exit Wernes_A_Field [AC-5]
```

```e2e fail-1
scene Wernes_Hub
at door
say Hori => InvalidTarget | The name echoes and is not answered. [AC-4]
expect fdoor=shut [AC-4]
say open door => UnknownVerb [AC-4]
unsolved
```

```e2e after-a
scene Wernes_Hub
progress A
beat panel_A [AC-6]
beat count_1 [AC-6]
beat back_a [AC-6]
nobeat first [AC-6]
expect panel_a=lit [AC-6]
expect panel_b=dark [AC-6]
expect false_door_panels=1 [AC-6]
at door
say Ankhu => Success
beat solved
exit Wernes_B_Shabti [AC-6]
```

```e2e after-b
scene Wernes_Hub
progress A B
beat back_b [AC-7]
nobeat back_a [AC-7]
expect panel_a=lit [AC-7]
expect panel_b=lit [AC-7]
expect panel_c=dark [AC-7]
expect false_door_panels=2 [AC-7]
at door
say Ankhu => Success
exit Wernes_C_House [AC-7]
```

```e2e after-c
scene Wernes_Hub
progress A B C
beat back_c [AC-8]
expect panel_c=lit [AC-8]
expect panel_d=dark [AC-8]
expect false_door_panels=3 [AC-8]
at door
say Ankhu => Success
exit Wernes_D_Bricks [AC-8]
```

```e2e after-d
scene Wernes_Hub
progress A B C D
beat back_d [AC-9]
flag progress.all [AC-9]
expect panel_a=lit [AC-9]
expect panel_d=lit [AC-9]
expect false_door_panels=4 [AC-9]
at door
say Ankhu => Success
beat solved_all [AC-9]
nobeat solved [AC-9]
exit Wernes_Finale [AC-9]
```
