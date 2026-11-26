# 14 Wernes_D_Bricks: The Birth Bricks

## 1. Summary

| Field | Value |
|---|---|
| Scene | `Wernes_D_Bricks` (D:\TheAfterlifeofAnkhu\Assets\Ankhu\Realms\Generated\Scenes\Wernes_D_Bricks.unity) |
| Realm | Wernes |
| Title | The Birth Bricks |
| Play order | 14 |
| Previous | Wernes_Hub (after C) |
| Next | Wernes_Hub (after D, the final return, play order 15) |
| Script | D:\TheAfterlifeofAnkhu\Assets\Ankhu\Realms\Core\Content\WernesPaintedScripts.cs, `BuildBricksD()` (lines 557 to 774) |
| Builder | D:\TheAfterlifeofAnkhu\Assets\Ankhu\Realms\Editor\WernesPaintedBuilderD.cs (zones lines 329 to 336) |
| Design | Zac 2026-11-24 pick 4 (comment at line 548). Earlier solver W3v2 is a different variant (running chick, still) |

## 2. Intent and story beat

The mother's room in Wernes, about his birth and his name. Four birth bricks of Meskhenet have been scraped bare. The four sound-signs of the cartouche (S34 ankh, N35 water, Aa1 placenta, G43 quail chick) are scattered, each won back with one word he already has: wake the goddess, flow the basin, open the midwife's basket, release the chick. Laid in reading order the bricks wake and his mother's cry, "He lives!", sounds. While it sounds the seated man (A1, the determinative that says the name belongs to a man) appears on the birthing mat, but the cry fades within two ticks and takes the sign with it. Only *wesheb* (echo, answer) holds her voice long enough to set the man at the end of the name. The painted memory `mem_naming` follows: "Ankhu. He lives. Say it so the gods hear." and Ankhu: "She said it loud enough for the gods. I have said it quietly ever since." That line is why the hub door needs his name said aloud.

## 3. Layout and zones

| Zone id | Centre x, size | What stands there |
|---|---|---|
| `goddess` | 1.8, 2.6 | Meskhenet painted on the wall, the ankh drops here: `look_goddess`, `take_ankh` |
| `basin` | 4.2, 1.8 | the midwife's basin: `look_basin`, `take_water` |
| `brick1`..`brick4` | 6.4, 7.6, 8.8, 10.0, each 1.15 | the four bricks: `placeN`, `swapN`, `liftN` |
| `seat` | 11.2, 1.15 | the empty cartouche end: `place_man`, `look_cartouche` |
| `mat` | 13.2, 1.9 | the birthing mat: `take_man` |
| `basket` | 14.6, 0.9 | the midwife's basket: `look_basket`, `take_placenta` |
| `snare` | 15.9, 1.6 | the quail chick in its snare: `look_snare`, `take_quail` |

Ankhu spawns at x 2.6 (inside `goddess`). Bricks are zoned, everything else answers from anywhere.

## 4. Elements

| Id | Name | Aliases | Initial | States | Words |
|---|---|---|---|---|---|
| `brick1..4` | first..fourth birth brick | brick, birth brick, brick N, Nth brick, brick Nth, Nth birth brick | bare | bare, ankh, water, placenta, quail | place (deed, a sign in hand): bare to that sign, sign laid. Swap (deed, sign in hand, brick holds another, bricks dull or spent). lift (deed, hands empty, bricks dull or spent): sign back to hand |
| `bricks` | the bricks | bricks, birth bricks, all the bricks, the bricks | dull | dull, awake, spent | auto: four signs in reading order and dull gives awake, cry sounding, man on mat, event `bricks_wake`. wake (spent, in order): awake again, event `bricks_rewake`, "Res. The bricks wake again, and the cry comes again." |
| `goddess` | Meskhenet | meskhenet, goddess, painted goddess, woman, painted woman, her | asleep | asleep, awake | wake: ankh loose, event `goddess_wakes`, "Res. The painted goddess opens her eyes..." |
| `basin` | basin | basin, dry basin, bowl, water basin | dry | dry, full | flow: water loose, event `basin_flows` |
| `basket` | midwife's basket | basket, midwife's basket, midwife basket, lid | shut | shut, open | open: placenta loose, event `basket_open` |
| `quail` | quail chick | quail, chick, quail chick, bird, snare, reed snare | snared | snared, loose, hand, laid | release: loose, event `quail_free`. take as a sign |
| `ankh`, `water`, `placenta` | ankh, water sign, round sign | ankh, ankh sign, life / water sign, ripple, water ripple, wavy line / round sign, placenta, sieve, kh, round | hidden | hidden, loose, hand, laid | take (deed, hands empty): loose to hand, event `sign_taken` |
| `man` | seated man | man, seated man, little man, sign on the mat | unseen | unseen, mat, hand, laid | take (deed, on mat, hands empty): event `man_taken` |
| `seat` | end of the cartouche | cartouche, end of the name, end, seat | bare | bare, man | place (deed, man in hand): seat=man, man laid, cry answered, event `named`. Zone `seat` |
| `cry` | cry | cry, voice, her voice, shout, wail, sound | silent | silent, sounding, fading, held, answered | echo (sounding or fading): held, event `cry_held`, "Wesheb. You answer the cry..." |
| `hand` | your hands | none | none | none, ankh, water, placenta, quail, man | |
| `misread`, `fade` | notes | none | no, none | | auto only: misread yes (all four laid, one out of place) with event `misread`. fade yes when bricks spent with event `cry_faded` |

**World clock** after every action, refusal, NoChange and wait: a sounding cry becomes fading. A fading cry becomes silent, the bricks become spent, the man vanishes from the mat or the hand, and the hand empties if it held him. A held cry never fades.

Consequence: the tick that lays the fourth sign already turns the cry to fading. The player has exactly one action left, and only "echo cry" keeps the man.

**Goal**: seat=man.

## 5. Words

Known on entry (Verbs, CodexOnEntry `word.open`, `word.wake`, `word.release`, `word.flow`, `word.echo`, `name.ankhu`, `lore.birth_bricks`): open, wake, still, bind, release, seal, flow, echo. Deeds: take, place, lift. Nothing is learned. Still, bind and seal have no rules here ("Nothing here answers that word.").

| Situation | Judgement | Message |
|---|---|---|
| "wake goddess" twice | NoChange | "The Meskhenet is already awake." (the rule's Refusal "She is awake..." never shows) |
| "flow basin" twice | NoChange | "The basin is already full." |
| "echo cry" with the room quiet | Refused | "There is nothing to answer. The room is quiet." (ticks) |
| "echo cry" while held | NoChange | "The cry is already held." |
| "wake bricks" not in order or not spent | Refused | "The bricks wake only for a name, read in its order." |
| "take ..." with full hands | Refused | "Your hands are full, or it is not yours to lift yet. One sign at a time." |
| "still goddess" | InvalidTarget | "Nothing here answers that word." |

"This time, nothing has changed." is not produced in this room.

## 6. Canonical solution

| # | Zone | Action | State after | Beats |
|---|---|---|---|---|
| 0 | goddess | start | | `arrive` |
| 1 | goddess | "wake goddess" | goddess awake, ankh loose | |
| 2 | goddess | E Pick up the ankh | hand ankh | `first_sign` |
| 3 | brick1 | E Lay the sign | brick1 ankh | |
| 4 | basin | "flow basin", E Lift the ripple | hand water | |
| 5 | brick2 | E Lay the sign | brick2 water | |
| 6 | basket | "open basket", E Take the round sign | hand placenta | |
| 7 | brick3 | E Lay the sign | brick3 placenta | |
| 8 | snare | "release quail", E Pick up the quail chick | hand quail | |
| 9 | brick4 | E Lay the sign | brick4 quail, bricks awake, man on mat, tick: cry fading | `wake` |
| 10 | any | "echo cry" | cry held | `held` |
| 11 | mat | E Take the seated man | hand man | `man` |
| 12 | seat | E Set him at the end of the name | seat man, **solved** | `solved` (painted memory `mem_naming`, codex `memory.naming`) |

## 7. Alternate valid orders

- The four signs can be won and laid in any order and on any bricks. Only the final arrangement matters: ankh, water, placenta, quail on bricks 1 to 4.
- A wrong arrangement fires `misread` ("The ankh first, because it is the strongest sign. Mother taught me that."). Fix it with lift and swap (each needs the bricks dull or spent).
- If the cry fades before "echo cry": the bricks go spent and the man vanishes (`faded`). Either "wake bricks" (rewake) or lift one sign and lay it again, then echo at once.
- "echo cry" while sounding or fading both work.

## 8. Failure, refusal and reset behaviour

- Misread: caption "The bricks stay dull. The signs say something, but not a name." plus Ankhu's line (each time).
- Cry faded: caption "The cry thins to nothing. The sign on the mat goes with it." plus "Gone before I could reach it. It has to stay." (each time).
- Taking the man without echo loses him on that same tick.
- Idle 45 s: "Four bricks, four bare places. A name is written in an order."
- 75 s hints in order: goddess asleep, basin dry, basket shut, quail snared, misread, bricks spent, cry sounding or fading, man on mat with cry held, man in hand.
- H: nudge only (see D2).

## 9. Beats, memory films and codex

| Beat | Trigger | Steps |
|---|---|---|
| `arrive` | Start | Freeze, Title "THE BIRTH BRICKS", caption, 2 Ankhu lines, Free |
| `look_goddess`, `look_basin`, `look_basket`, `look_snare`, `look_cartouche` | Use (repeatable) | one Ankhu line each |
| `first_sign` | Event `sign_taken` (once) | "It is one of mine. One of the signs of my name." |
| `misread` | Event `misread` (repeatable) | caption, Ankhu line |
| `wake` | Event `bricks_wake` (once) | Freeze, caption, Mother "He lives!", caption, Ankhu "That was her voice.", Free |
| `rewake` | Event `bricks_rewake` (repeatable) | Mother "He lives!" |
| `faded` | Event `cry_faded` (repeatable) | caption, Ankhu line |
| `held` | Event `cry_held` (once) | Ankhu "Wesheb. I am holding her voice in the room.", Mother "He lives! He lives!" |
| `man` | Event `man_taken` (once) | "The man on the mat is him." |
| `murmur` | Idle 45 s | Ankhu line |
| `solved` | Solved | Freeze, caption, 3 **painted memory stills `mem_naming`** (D:\TheAfterlifeofAnkhu\Assets\Ankhu\Realms\Art\Memories\Resources\RealmMemories\mem_naming.png), Ankhu line, Write `memory.naming`, Write `lore.birth_bricks`, Wait 1.5, Next |

No memory film here. PLAY_ORDER.md row 14 matches (painted memory mem_naming).

## 10. SFX and audio

| Sound | State |
|---|---|
| Ambience walk_ambience.wav | assigned |
| Spell cast, land, fail | **not assigned** |
| Mother's cry "He lives!" | dialogue line only, **no voice clip**, though the whole puzzle is about hearing and holding her voice |
| Voice | word.open, word.wake exist. **No word.flow, word.echo, word.release clips** |
| Ankh ringing on the floor (it is in the success text), water welling, knot giving, snare falling, signs settling into clay, bricks waking, man appearing | **no SFX** |

## 11. Exit conditions

Goal `seat=man`. `solved` ends with Next: `WernesProgress.MarkDone("Wernes_D_Bricks")` (D done, last D), then `ExitOf` to `Wernes_Hub`, which plays `back_d` and opens on `Wernes_Finale`.

## 12. Acceptance criteria

- AC-1 [auto] Each of the four words frees its sign: wake goddess (ankh), flow basin (water), open basket (placenta), release quail (quail).
- AC-2 [auto] Only one sign can be held at a time.
- AC-3 [auto] Laying the four signs in reading order wakes the bricks, fires `wake`, puts the man on the mat, and the cry is already fading after that tick.
- AC-4 [auto] "echo cry" holds the cry and fires `held`, and the man can then be taken and set on the seat, which solves the room.
- AC-5 [auto] Without echo the cry fades on the next tick, the bricks go spent, the man vanishes and `faded` fires. "wake bricks" brings the cry back (`rewake`).
- AC-6 [auto] A wrong arrangement fires `misread` and does not wake the bricks. Lift and swap fix it.
- AC-7 [auto] The solve shows `mem_naming`, writes `memory.naming` and exits to `Wernes_Hub` with D marked done.
- AC-8 [manual] The four signs are readable as glyphs on the bricks, and the seated man reads as A1.
- AC-9 [manual] The mother's cry is heard (currently text only, D4).

## 13. Code vs intent disagreements

| # | Severity | Where | Problem | Proposed fix |
|---|---|---|---|---|
| D1 | major | WernesPaintedScripts.cs line 698 and RealmSceneDirector.cs line 241 | `lore.birth_bricks` is in `CodexOnEntry`, and the director calls `CodexStore.EnsureEntries(script.CodexOnEntry)` on every load, not only on a dev skip. So the entry is already in the codex when the room starts, and the solve's `Step.Write("lore.birth_bricks")` (line 770) is a silent no-op: the keystone page is never presented. PLAY_ORDER.md lists it as a codex write of this room. (Room C has the same pattern with `lore.house`, which is harmless there.) | Remove `lore.birth_bricks` from CodexOnEntry, or make EnsureEntries run only when the scene was loaded directly |
| D2 | major | RealmHintSolver.cs line 93 | The first alias "brick" is shared by four bricks (Ambiguous with zone null), so no path is found and H gives only the nudge | as A2 in 11 |
| D3 | major | RealmRules.cs `Tokenize` | As B1: "place brick 3" becomes "place brick" and is resolved by the zone, so typing it at brick 1 lays the sign on brick 1 | as B1 |
| D4 | major | Wernes_D_Bricks.unity, RealmVoice | No SFX anywhere, and the mother's "He lives!" has no voice although holding her voice is the mechanic | Record the cry (and echo layer), author SFX |
| D5 | minor | WernesPaintedScripts.cs line 698 | `word.flow` and `word.echo` are granted on entry. Echo is not taught anywhere before this room in the Wernes code (flow comes from the teaching rooms). If Borrowed Dawn does not teach echo, the player meets a word here that the codex simply hands over | Confirm which earlier room teaches echo, or add a teaching beat |
| D6 | minor | WernesPaintedScripts.cs lines 607, 611, 614, 617 | The custom refusals ("She is awake. She is watching the bricks.", "It is full.", "It is open.", "It is free already.") never show: the engine's NoChange "The X is already Y." wins. "The Meskhenet is already awake." reads badly | Give NoChange a per-object phrasing, or drop the dead text |
| D7 | minor | Memory note 2026-11-24 (Bible approach) says "Mother memories stay in Rosetau (birth bricks removed)" | Code and PLAY_ORDER.md have room D with a mother memory, per Zac's later pick 4. Spec v2 section 2 has no room D | Update the spec |
| D8 | minor | RealmSceneBuilder.cs `Cast` | Anpu follows Ankhu here | as A5 |

## 14. E2E script

```e2e canonical
scene Wernes_D_Bricks
beat arrive
at goddess
use look_goddess
beat look_goddess
say wake goddess => Success [AC-1]
expect goddess=awake [AC-1]
expect ankh=loose [AC-1]
use take_ankh
expect hand=ankh
beat first_sign
at brick1
use place1
expect brick1=ankh
expect hand=none
at basin
say flow basin => Success [AC-1]
expect water=loose [AC-1]
use take_water
expect hand=water
at brick2
use place2
expect brick2=water
at basket
say open basket => Success [AC-1]
expect placenta=loose [AC-1]
use take_placenta
at brick3
use place3
expect brick3=placenta
at snare
say release quail => Success [AC-1]
expect quail=loose [AC-1]
use take_quail
expect hand=quail
at brick4
use place4
expect brick4=quail
expect bricks=awake [AC-3]
expect man=mat [AC-3]
expect cry=fading [AC-3]
beat wake [AC-3]
say echo cry => Success [AC-4]
expect cry=held [AC-4]
beat held [AC-4]
at mat
use take_man [AC-4]
expect hand=man
beat man
at seat
use place_man [AC-4]
expect seat=man [AC-4]
solved [AC-4]
beat solved [AC-7]
codexhas memory.naming [AC-7]
exit Wernes_Hub [AC-7]
```

```e2e alt-fade
scene Wernes_D_Bricks
at goddess
say wake goddess => Success
use take_ankh
at brick1
use place1
say flow basin => Success
at basin
use take_water
say take ankh => Refused | Your hands are full [AC-2]
at brick2
use place2
say open basket => Success
at basket
use take_placenta
at brick3
use place3
say release quail => Success
at snare
use take_quail
at brick4
use place4
expect cry=fading
say wait => Success [AC-5]
expect cry=silent [AC-5]
expect bricks=spent [AC-5]
expect man=unseen [AC-5]
beat faded [AC-5]
say echo cry => Refused | There is nothing to answer. [AC-5]
say wake bricks => Success [AC-5]
beat rewake [AC-5]
expect cry=fading [AC-5]
expect man=mat [AC-5]
say echo cry => Success
at mat
use take_man
at seat
use place_man
solved
exit Wernes_Hub
```

```e2e alt-misread
scene Wernes_D_Bricks
at goddess
say wake goddess => Success
use take_ankh
at brick2
use place2
expect brick2=ankh
say flow basin => Success
at basin
use take_water
at brick1
use place1
expect brick1=water
say open basket => Success
at basket
use take_placenta
at brick3
use place3
say release quail => Success
at snare
use take_quail
at brick4
use place4
expect misread=yes [AC-6]
beat misread [AC-6]
expect bricks=dull [AC-6]
at brick1
use lift1 [AC-6]
expect hand=water
expect brick1=bare
at brick2
use swap2 [AC-6]
expect brick2=water
expect hand=ankh
at brick1
use place1
expect brick1=ankh
expect bricks=awake [AC-6]
beat wake
say echo cry => Success
at mat
use take_man
at seat
use place_man
solved
exit Wernes_Hub
```
