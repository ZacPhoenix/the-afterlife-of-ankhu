# 11 Wernes_A_Field: The Field That Waits

## 1. Summary

| Field | Value |
|---|---|
| Scene | `Wernes_A_Field` (D:\TheAfterlifeofAnkhu\Assets\Ankhu\Realms\Generated\Scenes\Wernes_A_Field.unity) |
| Realm | Wernes |
| Title | The Field That Waits |
| Play order | 11 |
| Previous | 10 Wernes_Hub (first visit) |
| Next | Wernes_Hub (after A) |
| Script | D:\TheAfterlifeofAnkhu\Assets\Ankhu\Realms\Core\Content\WernesPaintedScripts.cs, `BuildFieldA()` (lines 60 to 212) |
| Builder | D:\TheAfterlifeofAnkhu\Assets\Ankhu\Realms\Editor\WernesPaintedBuilder.cs (zones lines 277 to 283) |
| Design | /mnt/project-files/design/next_area/wernes_build_spec_v2.md section 4, Room A. Solver W1v2 in /mnt/project-files/design/next_area/solver/wernes_rooms_v2.py |

## 2. Intent and story beat

Ankhu recovers three words of his own priesthood: *wen* (open), *res* (wake) and *hetep* (be at peace). A basin can only show a picture when everyone has stopped working and nothing is being held. The shaduf beam that swings while the work goes on is the only way up to the ledge where Hori's clay falcon was "put up high, out of reach, for safekeeping". The lesson is that holding is not resting. On the solve the still water shows a low sun and a man with a child on his shoulders whose face will not resolve (the `tease_basin` film), and Ankhu keeps the falcon. It sets up Memory A in room B.

## 3. Layout and zones

Side-on, left to right. Two shabti and the shaduf on the ground lane, the ledge high on the right.

| Zone id | Centre x, size | What stands there | Objects reached |
|---|---|---|---|
| `canal` | -11.6, 4.2 wide | sluice gate on the canal (spawn at x -13.2) | `sluice` |
| `trough` | -7.4, 4.2 | shabti A at the trough | `shabti_a`, usable `examine_shabti` |
| `vignette` | -3.7, 2.6 | wall painting | usable `read_vignette` |
| `basin` | 0.2, 4.6 | terrace basin, perch on its rim, lotus | `perch`, usable `place_falcon` |
| `beamfoot` | 3.6, 2.2 | the shaduf footplate | usable `ride_up` |
| `shaduf` | 7.6, 5.8 | shabti B working the shaduf | `shabti_b` |
| `ledge` | 13.75 x, 5.6 y, 5.5 wide | high ledge with the falcon | usables `take_falcon`, `ride_down` |

Objects without zones (beam, basin, falcon, lotus) answer from anywhere. Riding the beam is a RealmRide on the `ankhu` view that carries Ankhu between the footplate and the ledge.

## 4. Elements

| Id | Name | Aliases | Initial | States | Words (verb: from to to, requires, also) |
|---|---|---|---|---|---|
| `sluice` | sluice | sluice, sluice gate, gate, canal gate, canal | shut | shut, open | open: shut to open, event `sluice_open`. seal: open to never (unreachable, seal is never known here) |
| `shabti_a` | shabti at the trough | shabti, shabti a, first shabti, trough shabti, worker, servant | asleep | asleep, carrying | wake: asleep to carrying ("It shoulders the yoke and goes down to the water."). still: carrying to asleep. bind: refused with "It is held. It is not resting." and event `learn_hetep` |
| `shabti_b` | shabti at the shaduf | as above, plus shabti b, second shabti, shaduf shabti | asleep | asleep, carrying | wake (sluice shut): "It lifts an empty bucket, carefully." wake (otherwise): "It takes the rope. The beam begins to swing." still and bind as A |
| `beam` | shaduf beam | beam, shaduf, arm, shaduf beam, pole, sweep | down | up, down | bind (up, hold free): hold=bound, "A cyan thread holds the beam at the top...", event `beam_bound_up`. bind (down): hold=bound, "Held, at the bottom." release (hold bound): hold=free, "The thread lets go." |
| `hold` | binding | none | free | free, bound | set by bind and release on the beam |
| `basin` | basin | basin, pool, water, terrace basin | 0 | 0, 1, 2 | filled by the tick only |
| `falcon` | clay falcon | falcon, clay falcon, toy, bird | ledge | ledge, hand, perch, kept | take (deed, Ankhu on ledge): ledge to hand, event `falcon_taken`. Set to `kept` by the solve beat |
| `perch` | perch | perch | empty | empty, falcon | place (deed, falcon in hand): empty to falcon, also falcon=perch, event `falcon_placed`. Zone `basin` |
| `ankhu` | Ankhu | none | ground | ground, ledge | ride (deed): ground to ledge or back, requires hold=bound and beam=up |
| `settle` | settle | none | none | none, carrying, shadow, still | auto only (see ticks) |
| `lotus` | lotus | lotus, bud | shut | shut, open | set open by the solve beat |

**World clock** (`room.Tick`, runs after every accepted spell, every deed, every "wait", and after every Refused or NoChange spell):
1. If shabti B carries and the beam is not held, the beam flips up or down.
2. If the sluice is open and both shabti carry and the basin is below 2, the basin rises by one.

**Auto rules** (settle after every change): basin 2 and A or B carrying gives settle=carrying and event `full_but_working`. Basin 2, both asleep and hold bound gives settle=shadow and event `full_but_shadow`. Basin 2, both asleep and hold free gives settle=still.

**Goal**: basin=2, shabti_a=asleep, shabti_b=asleep, falcon=perch, hold=free.

Usables:

| Id | Zone | Prompt | Deed | Shown when |
|---|---|---|---|---|
| `examine_shabti` | trough | Look at the sleeping shabti | none | shabti_a asleep |
| `ride_up` | beamfoot | Step onto the footplate | ride | ankhu ground, hold bound, beam up |
| `take_falcon` | ledge | Take the clay falcon | take falcon | falcon ledge |
| `ride_down` | ledge | Step back onto the footplate | ride | ankhu ledge, hold bound, beam up |
| `place_falcon` | basin | Set the falcon on the perch | place perch | falcon hand |
| `read_vignette` | vignette | Look at the painting | none | always |

## 5. Words

| Verb | Source | Here |
|---|---|---|
| bind, release | known on entry (room Verbs, codex word.bind, word.release) | hold the beam, or try to hold a shabti (teaches hetep) |
| ride, take, place | deeds (E), no glyphs | ride the beam, take the falcon, place it |
| open (*wen*) | learned in the `arrive` beat (Anpu: "Wen. Open.") | the sluice |
| wake (*res*) | learned in the `examine` beat (E on the sleeping shabti A) | wake a shabti |
| still (*hetep*) | learned in the `hetep` beat, raised by the refusal of "bind shabti" | still a shabti |
| seal | never learned here | "seal sluice" is UnknownVerb |

| Situation | Judgement | Message |
|---|---|---|
| "wake shabti" before examining | UnknownVerb | "You know this word exists, but your mouth cannot shape it yet." (no tick) |
| "still shabti" before the bind refusal | UnknownVerb | same (no tick) |
| "open sluice" twice | NoChange | "The sluice is already open." (ticks) |
| "wake shabti" on a carrying shabti | NoChange | "The shabti at the trough is already carrying." (ticks) |
| "still shabti" on a sleeping shabti | NoChange | "The shabti at the trough is already asleep." (ticks) |
| "bind beam" while held | Refused | "It is already held." (ticks) |
| "shabti" said outside trough and shaduf zones | OutOfReach | "No shabti is close enough. Walk to the one you mean." (no tick). Exception: outside every zone both are "near" and it is Ambiguous: "Which shabti? Several hear you. Stand beside the one you mean." |

"This time, nothing has changed." never appears in this room: it is only produced for name rules (`RealmRoom.Finish` is reached for non-name verbs only when the rule applies). A spent non-name word gets "The X is already Y." instead.

## 6. Canonical solution

| # | Zone | Action | Result | State after | Beats |
|---|---|---|---|---|---|
| 0 | canal | scene starts | Title, drip caption, Anpu "Wen. Open.", learns open | | `arrive` |
| 1 | trough | E Look at the sleeping shabti | learns wake | | `examine` |
| 2 | trough | "bind shabti" | Refused, "It is held. It is not resting." | learns still | `hetep` |
| 3 | shaduf | "wake shabti" | B carries. Tick: beam swings up | shabti_b=carrying, beam=up | |
| 4 | shaduf | "bind beam" | held at the top | hold=bound, beam=up | |
| 5 | beamfoot | E Step onto the footplate | ride up | ankhu=ledge | `ride_first` |
| 6 | ledge | E Take the clay falcon | | falcon=hand | `falcon` |
| 7 | ledge | E Step back onto the footplate | ride down | ankhu=ground | |
| 8 | canal | "open sluice" | water in the channel | sluice=open | |
| 9 | trough | "wake shabti" | A carries. Tick: basin 1 | shabti_a=carrying, basin=1 | |
| 10 | any | "wait" | Tick: basin 2 | basin=2, settle=carrying | `full_working` |
| 11 | basin | E Set the falcon on the perch | | falcon=perch, perch=falcon | |
| 12 | trough | "still shabti" | A sits | shabti_a=asleep | |
| 13 | shaduf | "still shabti" | B sits | shabti_b=asleep, settle=shadow | `full_shadow` |
| 14 | any | "release beam" | thread lets go | hold=free, settle=still, **solved** | `solved` |
| 15 | | solve beat | lotus opens, film `tease_basin`, "I looked at the child.", falcon kept, codex `lore.field_waits`, Next | | |

## 7. Alternate valid orders

- Solver order (W1v2): open sluice, wake A, wake B, bind beam on the up tick, still A, still B, ride up, take falcon, ride down, release beam, place falcon. Valid with the C# port (traced, see E2E `alt-1`). The basin fills while the beam is bound because the fill rule does not care about the beam.
- The falcon can be fetched at any point once B swings the beam, before or after the basin is full.
- "bind shabti" can be tried at any time (before or after examining, on A or B, asleep or carrying): every version refuses and teaches hetep.
- Riding needs the beam held at the top. If it was bound at the bottom: release, let one tick pass (any word or "wait" while B carries flips it up), bind again.

There are 1,176 distinct shortest solutions in the solver, 11 ticks long.

## 8. Failure, refusal and reset behaviour

- Every Refused or NoChange spell, every deed and every "wait" costs one tick: the beam swings and the basin may fill. UnknownVerb, NotUnderstood, InvalidTarget, OutOfReach, Incomplete and Ambiguous cost nothing.
- Bound at the bottom: "Held, at the bottom." and riding is refused with "The beam will not carry you unless it is held, and at the top."
- Full but still working: caption "The water is full and cannot settle." (once).
- Full and still but held: caption "A shadow lies across the water." (once).
- Idle 45 s: Ankhu "It goes up and down whether I watch or not. I only need it up once."
- 75 s: first matching hint, in order: sluice shut, basin 0 or 1, falcon on ledge, falcon in hand, basin 2.
- H: see A2 in section 13. The answer tier is mostly missing in this room.
- No softlocks (solver: 360 states, 0 softlocks). There is no undo key in the director.

## 9. Beats, memory films and codex

| Beat | Trigger | Steps |
|---|---|---|
| `arrive` | Start | Freeze, Title "THE FIELD THAT WAITS", caption, Anpu "Wen. Open.", Learn open (codex word.open), Ankhu line, Free |
| `examine` | Use `examine_shabti` | Freeze, 2 Ankhu lines, Learn wake (codex word.wake), Free |
| `hetep` | Event `learn_hetep` | Ankhu "Held is not resting. There is a word for rest. Hetep. Be at peace.", Learn still (codex word.still) |
| `vignette` | Use `read_vignette` (repeatable) | Ankhu line |
| `full_working` | Event `full_but_working` | caption |
| `full_shadow` | Event `full_but_shadow` | caption |
| `ride_first` | Event `ride_up` | Ankhu "...The plate is worn in the shape of a foot." |
| `falcon` | Event `falcon_taken` | Ankhu "Put up high, out of reach. For safekeeping." |
| `murmur` | Idle 45 s | Ankhu line |
| `solved` | Solved | Freeze, caption, Set lotus=open, **film `tease_basin`** (D:\TheAfterlifeofAnkhu\Assets\StreamingAssets\Memories\tease_basin.mp4, painted first in the codex page), Wait 1, "I looked at the child.", Set falcon=kept, caption, Write `lore.field_waits`, Wait 1.5, Next |

## 10. SFX and audio

| Sound | State |
|---|---|
| Ambience walk_ambience.wav | assigned (generic, not a field or water bed) |
| Spell cast, land, fail | **not assigned** |
| Ankhu spell voice | word.open, word.wake, word.still fall back for any target. spell.open.sluice exists. **No word.bind or word.release clip**, so bind and release are silent |
| Sluice lifting, water running, basin filling, beam swinging, thread binding, riding, taking and placing the falcon, lotus opening | **no SFX** (views and RealmRide play no audio, and deeds bypass the cast sound) |
| Film | tease_basin film carries the "A New Memory" score per the memory film rule |
| Dialogue voice | none recorded for `wernes_a` keys |

## 11. Exit conditions

Goal holds (basin 2, both asleep, falcon on perch, beam free). The `solved` beat ends with Next: `WernesProgress.MarkDone("Wernes_A_Field")` stores A as done and last, then `ExitOf` sends Ankhu to `Wernes_Hub`.

## 12. Acceptance criteria

- AC-1 [auto] On load `arrive` fires and `open` is known.
- AC-2 [auto] "wake shabti" before examining is UnknownVerb and changes nothing.
- AC-3 [auto] E on the sleeping shabti teaches wake.
- AC-4 [auto] "bind shabti" at the trough is Refused with "It is held. It is not resting." and teaches still (beat `hetep`).
- AC-5 [auto] Waking shabti B makes the beam swing up on that tick.
- AC-6 [auto] "bind beam" while up sets hold=bound and the beam stays up across later ticks.
- AC-7 [auto] Riding up needs hold bound and beam up, carries Ankhu to the ledge and fires `ride_first`.
- AC-8 [auto] The falcon can be taken only from the ledge, and Ankhu can ride back down.
- AC-9 [auto] With the sluice open and both shabti carrying, the basin rises one step per tick to 2, and `full_working` fires.
- AC-10 [auto] Placing the falcon on the perch sets falcon=perch.
- AC-11 [auto] With both stilled and the beam still held, settle=shadow and `full_shadow` fires, and the room is not solved.
- AC-12 [auto] Releasing the beam solves the room, the `solved` beat fires, film `tease_basin` plays and the codex holds `lore.field_waits`.
- AC-13 [auto] The room exits to `Wernes_Hub` and A is marked done.
- AC-14 [auto] Binding the beam at the bottom says "Held, at the bottom." and riding is refused with the top-only message.
- AC-15 [manual] Ankhu visibly rides the beam to the ledge and back, the falcon is in his hand and then on the rim.
- AC-16 [manual] The basin water rises in two steps, and the still water shows the sun, then the shadow bar while held.
- AC-17 [manual] Every interaction has authored SFX (currently fails, see A4).

## 13. Code vs intent disagreements

| # | Severity | Where | Problem | Proposed fix |
|---|---|---|---|---|
| A1 | major | WernesPaintedScripts.cs lines 110 to 111, 180 to 182 | *hetep* is learned only by trying "bind shabti". Nothing in the room suggests binding a shabti: no hint, no murmur, no clue line. A player who never tries it reaches basin 2 with both shabti carrying and no word to stop them. The spec calls this "the failure layer", but every failure line the player can see points elsewhere. The 75 s hint at basin 2 says "The water needs everyone to rest." without saying how | Add a hint for basin=2 with a shabti carrying and `still` unknown ("You cannot make it rest by holding it. Try."), or teach hetep from the `full_working` caption |
| A2 | major | RealmHintSolver.cs line 64 and 93 | The H key solver speaks every candidate with zone null and uses each object's first alias. Both shabti have first alias "shabti", which is Ambiguous from no zone, so no path exists and H gives only the nudge for the whole room, never "direction" or "answer". (Same for rooms B, C, D, see their guides.) | Make candidates use the object id (MatchObjects already accepts the id), and resolve reach by passing each object's first zone |
| A3 | minor | WernesPaintedScripts.cs line 97 | The `seal sluice` rule and its refusal "Your mouth cannot shape it yet." are unreachable (seal is never in Verbs, so UnknownVerb wins) | Drop it, or keep it as documentation |
| A4 | major | Wernes_A_Field.unity | No spell cast, land or fail SFX, no object SFX for sluice, water, beam, binding, ride, falcon, lotus. word.bind and word.release voice clips missing | Author with earshot, assign in the builder, add state SFX to RealmObjectView |
| A5 | minor | RealmSceneBuilder.cs `Cast` | Anpu is cast in this room and follows Ankhu, though the hub has him say "Go on. I will be here." and the spec has Anpu's voice only as "Wen. Open." from the hub | Do not cast Anpu in rooms A to D, or make him a voice only |
| A6 | minor | Spec v2 Room A "Hint ladder" | Spec has a three-step ladder ending in the full solution. Code uses the generic RealmHintSolver (which fails here, A2) | as A2 |
| A7 | minor | WernesPaintedScripts.cs line 155 | `examine_shabti` only shows while shabti A is asleep. If the player wakes nothing first this is fine, but its prompt "Look at the sleeping shabti" is the only way to learn wake, and there is no similar prompt on shabti B | Add the same usable on shabti B at the shaduf |

## 14. E2E script

```e2e canonical
scene Wernes_A_Field
beat arrive [AC-1]
verb open [AC-1]
say wake shabti => UnknownVerb | your mouth cannot shape it yet [AC-2]
expect shabti_a=asleep [AC-2]
at trough
use examine_shabti [AC-3]
beat examine [AC-3]
verb wake [AC-3]
say bind shabti => Refused | It is held. It is not resting. [AC-4]
beat hetep [AC-4]
verb still [AC-4]
at shaduf
say wake shabti => Success [AC-5]
expect shabti_b=carrying [AC-5]
expect beam=up [AC-5]
say bind beam => Success [AC-6]
expect hold=bound [AC-6]
expect beam=up [AC-6]
at beamfoot
use ride_up [AC-7]
expect ankhu=ledge [AC-7]
beat ride_first [AC-7]
at ledge
use take_falcon [AC-8]
expect falcon=hand [AC-8]
beat falcon [AC-8]
use ride_down [AC-8]
expect ankhu=ground [AC-8]
expect beam=up [AC-6]
at canal
say open sluice => Success
expect sluice=open
at trough
say wake shabti => Success [AC-9]
expect shabti_a=carrying [AC-9]
expect basin=1 [AC-9]
say wait => Success [AC-9]
expect basin=2 [AC-9]
expect settle=carrying [AC-9]
beat full_working [AC-9]
at basin
use place_falcon [AC-10]
expect falcon=perch [AC-10]
expect perch=falcon [AC-10]
at trough
say still shabti => Success
expect shabti_a=asleep
at shaduf
say still shabti => Success
expect shabti_b=asleep
expect settle=shadow [AC-11]
beat full_shadow [AC-11]
unsolved [AC-11]
say release beam => Success [AC-12]
expect hold=free [AC-12]
expect settle=still [AC-12]
beat solved [AC-12]  # the solved beat then sets falcon=kept, so the goal no longer reads true afterwards
beat solved [AC-12]
film tease_basin [AC-12]
expect lotus=open [AC-12]
codexhas lore.field_waits [AC-12]
exit Wernes_Hub [AC-13]
```

```e2e alt-1
scene Wernes_A_Field
at trough
use examine_shabti
say bind shabti => Refused | It is held. It is not resting.
verb still
at canal
say open sluice => Success
at trough
say wake shabti => Success
expect shabti_a=carrying
expect basin=0
at shaduf
say wake shabti => Success | The beam begins to swing.
expect beam=up
expect basin=1
say bind beam => Success
expect hold=bound
expect basin=2
beat full_working
at trough
say still shabti => Success
at shaduf
say still shabti => Success
beat full_shadow
at beamfoot
use ride_up
at ledge
use take_falcon
use ride_down
say release beam => Success
expect settle=still
unsolved
at basin
use place_falcon
beat solved  # the solved beat then sets falcon=kept, so the goal no longer reads true afterwards
exit Wernes_Hub
```

```e2e fail-1
scene Wernes_A_Field
at trough
use examine_shabti
at shaduf
say wake shabti => Success | It lifts an empty bucket, carefully.
expect beam=up
say wait => Success
expect beam=down
say bind beam => Success | Held, at the bottom. [AC-14]
expect hold=bound [AC-14]
expect beam=down [AC-14]
at beamfoot
say ride => Refused | The beam will not carry you unless it is held, and at the top. [AC-14]
expect ankhu=ground [AC-14]
say bind beam => Refused | It is already held.
say release beam => Success
expect hold=free
expect beam=up
say bind beam => Success
expect beam=up
use ride_up
expect ankhu=ledge
say still shabti => UnknownVerb
say seal sluice => UnknownVerb
unsolved
```
