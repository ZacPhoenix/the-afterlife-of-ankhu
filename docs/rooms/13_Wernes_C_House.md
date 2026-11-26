# 13 Wernes_C_House: The House With One Door

## 1. Summary

| Field | Value |
|---|---|
| Scene | `Wernes_C_House` (D:\TheAfterlifeofAnkhu\Assets\Ankhu\Realms\Generated\Scenes\Wernes_C_House.unity) |
| Realm | Wernes |
| Title | The House With One Door |
| Play order | 13 |
| Previous | Wernes_Hub (after B) |
| Next | Wernes_Hub (after C) |
| Script | D:\TheAfterlifeofAnkhu\Assets\Ankhu\Realms\Core\Content\WernesPaintedScripts.cs, `BuildHouseC()` (lines 402 to 538) |
| Builder | D:\TheAfterlifeofAnkhu\Assets\Ankhu\Realms\Editor\WernesPaintedBuilderC.cs (zones lines 222 to 227) |
| Design | wernes_build_spec_v2.md section 4 Room C, solver W2v2 |

## 2. Intent and story beat

The father's room. A one-room house whose plank door the wind swings open and shut. The upper channel is jammed open, so water reaches the seedlings only through the door, and every word Ankhu knows moves water on and away. Nothing he knows shuts anything for good. The false door on the back wall holds the word: walking into it plays Memory B (`memB_door_dawn`), his father kneeling in a soldier's kilt, "I will bring you something from the north", the door shutting with the bolt on the outside. From that he learns *khetem* (seal), "from the wrong side of a door". Sealing the door from inside, while it is shut, keeps the seedlings alive. Closing line: "A door that shuts can keep something alive." The sapling in the father's plot puts out one leaf.

## 3. Layout and zones

Inside the house (x 0 to 12), then the yard to the right of the door at `DoorX`.

| Zone id | Centre x, size, priority | What stands there |
|---|---|---|
| `house` | 6.0, 12 wide, priority 0 | the whole room (low priority, the others override it) |
| `falsedoor` | 3.2, 3.2, priority 2 | the false door on the back wall: usables `enter_false`, `look_false`, `read_vignette` |
| `threshold` | DoorX - 1.2, 2.4, priority 2 | inside the front door: usable `go_out` |
| `yard` | DoorX + 1.6, 3.2, priority 2 | outside the front door: usable `go_in` |
| `frontplot` | 17.0, 5.6 | the seedling plot and the lower sluice |
| `back` | 24.5, 8.0 | the father's plot, the sapling, the ditch sluice: usable `look_sapling` |

Ankhu spawns inside at x 6.2. Only the ride passes the doorway (blockers line the house). No object has zones: everything answers from anywhere, gated by the `side` state instead.

## 4. Elements

| Id | Name | Aliases | Initial | States | Words |
|---|---|---|---|---|---|
| `door` | door | door, front door, house door, plank door, doorway | shut | shut, open | bind (mode free): mode=held, event `door_held`. release (mode held): mode=free. seal (door shut, mode free or held, Ankhu in, memory seen): mode=sealed, event `door_sealed`, "Khetem. The bolt slides home from the inside...". open (mode sealed, Ankhu in): door open, mode free, "The bolt draws back..." Refusal of open while not sealed: "It opens well enough on its own." |
| `mode` | door's binding | none | free | free, held, sealed | via the door |
| `lower` | lower sluice | lower, lower sluice, lower gate, sluice, gate | shut | shut, open | open (Ankhu out): event `sluice_open`. seal (open, Ankhu out, memory seen): shut, event `sluice_sealed`. Refusal of both while in: "The sluices are out in the yard." |
| `ditch` | ditch sluice | ditch, ditch sluice, ditch gate, drain | shut | shut, open | as lower |
| `front` | seedling plot | front plot, seedlings, seedling plot, front field | 0 | 0 dry, 1 wet, 2 flooded, 3 drowned | tick only |
| `back` | father's plot | back plot, sapling, back field, old plot, father's plot | 0 | 0..3 | tick only |
| `side` | where Ankhu stands | none | in | in, out | cross (deed, door open): in to out (event `go_out`) and out to in (event `go_in`). Refusal "The door is shut. Wait for the wind." |
| `fdoor` | false door | false door | dark | dark, lit, warm | tick only |
| `memory` | memory | none (id) | unseen | unseen, seen | enter (deed, fdoor lit or warm, Ankhu in): event `memory_b`. Refusal "Painted stone. It does not open for you yet." |
| `held`, `drown` | notes | none | none | | auto only |

**World clock** after every action, refusal, NoChange and wait, in this order:
1. If mode is free, the door flips open or shut.
2. If the door is now open, front +1 (max 3).
3. If lower is open and front > 0: front -1, back +1 (max 3).
4. If ditch is open: back -1 (min 0).
5. False door: warm if front or back >= 2. Otherwise lit if it was already lit or warm, or if a sluice is open. Otherwise stays dark.

Auto: front 3 gives drown=front and event `front_drowned` (each time it reaches 3 again). Both plots 1, both sluices shut, mode held and door shut gives event `held_not_shut`.

**Goal**: front=1, back=1, mode=sealed, lower=shut, ditch=shut.

Usables:

| Id | Zone | Prompt | Deed | Shown when |
|---|---|---|---|---|
| `go_out` | threshold | Step out through the door | cross | side in, door open |
| `go_in` | yard | Step in through the door | cross | side out, door open |
| `enter_false` | falsedoor | Walk into the false door | enter memory | memory unseen, fdoor lit or warm |
| `look_false` | falsedoor | Look at the false door | none | fdoor dark |
| `read_vignette` | falsedoor | Look at the painting over the door | none | fdoor lit or warm |
| `look_sapling` | back | Look at the sapling | none | always |

## 5. Words

Known on entry: open, wake, still, bind, release (CodexOnEntry also gives `lore.house`). Deeds: cross, enter. Learned: **seal** (*khetem*), in the `memory` beat. Wake and still have no rules here ("Nothing here answers that word.", no tick).

| Situation | Judgement | Message |
|---|---|---|
| "seal door" before the memory | UnknownVerb | "You know this word exists, but your mouth cannot shape it yet." (no tick). Spec wants "Your mouth cannot shape it yet." (close enough) |
| "open door" while not sealed | Refused | "It opens well enough on its own." (ticks) |
| "open ditch" from inside | Refused | "The sluices are out in the yard." (ticks) |
| "enter memory" while the false door is dark | Refused | "Painted stone. It does not open for you yet." (ticks) |
| "bind door" while held or sealed | Refused | "It is already held, or sealed." (ticks) |
| "seal door" while open | Refused | "It must be shut to be sealed, and sealed from inside." (ticks) |
| "open ditch" twice | NoChange | "The ditch sluice is already open." (ticks) |

Remember that every Refused and NoChange answer is a tick: the door swings and water moves. "This time, nothing has changed." is not produced here.

## 6. Canonical solution (solver W2v2 order, traced against the C# tick)

| # | Zone | Action | door, mode | front, back | Other | Beats |
|---|---|---|---|---|---|---|
| 0 | house | start | shut, free | 0, 0 | fdoor dark | `arrive` |
| 1 | falsedoor | E Look at the false door (optional, no tick) | | | | `look_false` |
| 2 | falsedoor | "wait" | open, free | 1, 0 | | |
| 3 | falsedoor | "bind door" | open, held | 2, 0 | fdoor warm | |
| 4 | falsedoor | E Walk into the false door | open, held | 3, 0 | memory seen, seal learned | `memory` (film `memB_door_dawn`, codex `memory.door_dawn`), `drowned` |
| 5 | threshold | E Step out | open, held | 3, 0 | side out | `first_out` |
| 6 | yard | "open ditch" | open, held | 3, 0 | ditch open | `lit` |
| 7 | yard | "open lower" | open, held | 2, 0 | lower open | |
| 8 | yard | "release door" | shut, free | 1, 0 | | |
| 9 | yard | "wait" | open, free | 1, 0 | | |
| 10 | yard | "seal ditch" | shut, free | 0, 1 | ditch shut | |
| 11 | yard | "seal lower" | open, free | 1, 1 | lower shut | |
| 12 | yard | E Step in | shut, free | 1, 1 | side in | |
| 13 | threshold | "seal door" | shut, sealed | 1, 1 | **solved** | `solved` (film `sting_khetem_seedlings`, codex `lore.one_door`) |

Why it works: crossing back in on an open swing ends the tick with the door shut and no water added (the flip happens before the inflow), so the door can be sealed at once with front 1.

## 7. Alternate valid orders

The solver finds only 3 distinct shortest solutions (12 ticks). All share the shape: let the door bring water and light the false door, hold the door, take the memory, go out, use the sluices to move the excess to the back plot and drain it, seal both sluices on the right ticks, cross in on an open swing, seal the door. Longer orders are fine. Examples:

- Light the false door by opening a sluice instead of flooding: hold the door open, go out, "open ditch" (fdoor lit), come back in, enter the false door.
- Seal the door early (any time it is shut, Ankhu is inside and the memory is seen), then "open door" from inside to unseal it if the plots are not right yet.

Orders that fail without a softlock (solver: 411 states, 0 softlocks): sealing with the plots wrong (open it again), drowning the front plot (move water on with the lower sluice), flooding the back plot (open the ditch).

## 8. Failure, refusal and reset behaviour

- Front plot at 3: caption "The seedlings are under water. Water still comes through the door." (each time).
- Plots right but the door only held: caption "The plots are right. The door is only held. Held is not shut." (each time).
- First sluice opened: caption "Behind you, inside the house, the false door warms." (once).
- Idle 45 s: "Everything here opens. Nothing shuts. Except that door."
- 75 s hints in order: memory unseen with fdoor dark, memory unseen, front 2 or 3, back 2 or 3, mode held, lower open, ditch open.
- H: nudge only (see C2).
- The world does not move on its own. The door swings only when the player acts or types "wait", "watch" or "rest" (see C1).

## 9. Beats, memory films and codex

| Beat | Trigger | Steps |
|---|---|---|
| `arrive` | Start | Freeze, Title "THE HOUSE WITH ONE DOOR", caption "A plank door bangs open in the wind...", Ankhu line, Free |
| `look_false` | Use `look_false` (repeatable) | Ankhu reads BD 92 on the lintel |
| `vignette` | Use `read_vignette` (repeatable) | Ankhu line |
| `sapling` | Use `look_sapling` (repeatable) | Ankhu line |
| `first_out` | Event `go_out` | Ankhu "The upper gate is jammed open..." |
| `lit` | Event `sluice_open` | caption |
| `memory` | Event `memory_b` | Freeze, caption, **film `memB_door_dawn`** (D:\TheAfterlifeofAnkhu\Assets\StreamingAssets\Memories\memB_door_dawn.mp4), 2 Ankhu lines, Learn seal (codex word.seal), Write `memory.door_dawn`, Free |
| `held` | Event `held_not_shut` (repeatable) | caption |
| `drowned` | Event `front_drowned` (repeatable) | caption |
| `murmur` | Idle 45 s | Ankhu line |
| `solved` | Solved | Freeze, caption, Ankhu "A door that shuts can keep something alive.", **film `sting_khetem_seedlings`**, Write `lore.one_door`, Wait 1.5, Next |

## 10. SFX and audio

| Sound | State |
|---|---|
| Ambience walk_ambience.wav | assigned (no wind bed, though the room is about wind) |
| Spell cast, land, fail | **not assigned** |
| Voice | spell.open.lower, spell.seal.lower, word.open, word.seal exist. **No ditch clips** (falls back to word.open, word.seal), no bind or release clips |
| Door banging, bolt, thread, sluices, water, false door warming, walking through the false door, seal glyph | **no SFX** |
| Dialogue voice | none recorded for `wernes_c` keys |

## 11. Exit conditions

Goal holds. `solved` ends with Next: `WernesProgress.MarkDone("Wernes_C_House")`, then `ExitOf` to `Wernes_Hub`.

## 12. Acceptance criteria

- AC-1 [auto] Each tick with the door free flips it, and an open door adds one to the front plot.
- AC-2 [auto] "bind door" holds the door in its current position, and water keeps coming in while it is held open.
- AC-3 [auto] The false door lights or warms only after a plot floods or a sluice opens, and walking into it then plays Memory B, teaches seal and writes `memory.door_dawn`.
- AC-4 [auto] Walking into a dark false door is Refused with "Painted stone. It does not open for you yet."
- AC-5 [auto] "seal" is UnknownVerb before the memory.
- AC-6 [auto] Crossing the doorway is possible only while the door is open, and sluice words work only from the yard.
- AC-7 [auto] The lower sluice moves one step of water from the front plot to the back plot per tick, and the ditch drains one step per tick.
- AC-8 [auto] The canonical 13 steps end with front 1, back 1, both sluices shut, the door sealed, the room solved, film `sting_khetem_seedlings`, codex `lore.one_door`, exit `Wernes_Hub`.
- AC-9 [auto] A sealed door can be unsealed with "open door" from inside.
- AC-10 [auto] Every Refused answer costs a tick (the door swings).
- AC-11 [manual] The door visibly swings on each tick and shivers while free, the thread shows while held, the gold bolt and seal glyph show while sealed.
- AC-12 [manual] Every interaction has SFX (currently fails, C4).
- AC-13 [auto] Anpu is not in the room, he stayed at the Memory House (fixed 2026-11-26).

## 13. Code vs intent disagreements

| # | Severity | Where | Problem | Proposed fix |
|---|---|---|---|---|
| C1 | major | RealmSceneDirector.cs (no clock), WernesPaintedScripts.cs line 501 | The arrive caption says the door "bangs open in the wind, and bangs shut, and bangs open", but the director never advances the clock on its own (`RealmRoom.Advance` is never called). The door moves only when the player acts, and the only time word, "wait" (or "watch", "rest"), is never taught or shown anywhere in Wernes. A player who stands and watches sees a still door | Either tick on a timer while idle (call `room.Advance()` every few seconds when the room has a Tick and nothing is busy), or teach "wait" in the arrive beat and the legend |
| C2 | major | RealmHintSolver.cs line 93 | `side` and `memory` have no aliases, so the solver tries "cross where ankhu stands", which does not parse (`MatchObjects` only knows aliases and the id). No path, so H gives only the nudge | as A2 in 11 |
| C3 | minor | WernesPaintedScripts.cs line 452 | "open door" while sealed succeeds with "The bolt draws back. The wind has the door again." but the same tick flips the door shut again (mode becomes free before the tick). The door is shown opening and closing in one beat | Apply the unseal with To = "shut" and let the next tick open it, or skip the flip on the unseal tick |
| C4 | major | Wernes_C_House.unity | No spell or object SFX (door bang, bolt, sluices, water, wind) | Author and assign |
| C5 | minor | Spec v2 Room C Goal "nothing held" | Code goal is mode=sealed, which implies nothing held. Same meaning | none |
| C6 | minor | Spec v2 failure line "Your mouth cannot shape it yet." | Code shows the generic UnknownVerb line "You know this word exists, but your mouth cannot shape it yet." | Accept |
| C7 | minor | RealmSceneBuilder.cs `Cast` | Anpu follows Ankhu inside his father's house although he stayed at the hub | as A5 |

## 14. E2E script

```e2e canonical
scene Wernes_C_House
beat arrive
expect door=shut
expect fdoor=dark
say seal door => UnknownVerb [AC-5]
expect door=shut [AC-5]
at falsedoor
anpu absent [AC-13]
use look_false
beat look_false
say wait => Success [AC-1]
expect door=open [AC-1]
expect front=1 [AC-1]
expect fdoor=dark
say bind door => Success [AC-2]
expect mode=held [AC-2]
expect door=open [AC-2]
expect front=2 [AC-2]
expect fdoor=warm [AC-3]
use enter_false [AC-3]
expect memory=seen [AC-3]
beat memory [AC-3]
film memB_door_dawn [AC-3]
verb seal [AC-3]
codexhas memory.door_dawn [AC-3]
expect front=3
beat drowned
at threshold
use go_out [AC-6]
expect side=out [AC-6]
beat first_out
at yard
say open ditch => Success [AC-6]
expect ditch=open
beat lit
expect front=3
expect back=0
say open lower => Success
expect lower=open
expect front=2 [AC-7]
expect back=0 [AC-7]
say release door => Success
expect mode=free
expect door=shut
expect front=1
expect back=0
say wait => Success
expect door=open
expect front=1
expect back=0
say seal ditch => Success
expect ditch=shut
expect door=shut
expect front=0 [AC-7]
expect back=1 [AC-7]
say seal lower => Success
expect lower=shut
expect door=open
expect front=1
expect back=1
use go_in [AC-6]
expect side=in
expect door=shut
at threshold
say seal door => Success [AC-8]
expect mode=sealed [AC-8]
solved [AC-8]
beat solved [AC-8]
film sting_khetem_seedlings [AC-8]
codexhas lore.one_door [AC-8]
exit Wernes_Hub [AC-8]
```

```e2e fail-1
scene Wernes_C_House
at falsedoor
say enter memory => Refused | Painted stone. It does not open for you yet. [AC-4]
expect memory=unseen [AC-4]
expect door=open [AC-10]
expect front=1 [AC-10]
say open door => Refused | It opens well enough on its own. [AC-10]
expect door=shut [AC-10]
expect front=1
say open ditch => Refused | The sluices are out in the yard. [AC-6]
expect ditch=shut [AC-6]
expect door=open
expect front=2
expect fdoor=warm
use enter_false
expect memory=seen
expect door=shut
expect front=2
say seal door => Success [AC-9]
expect mode=sealed [AC-9]
unsolved [AC-9]
say open door => Success | The bolt draws back. [AC-9]
expect mode=free [AC-9]
expect door=shut
expect front=2
unsolved
```
