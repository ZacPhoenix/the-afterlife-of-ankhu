# 12 Wernes_B_Shabti: The Shabti With My Name

## 1. Summary

| Field | Value |
|---|---|
| Scene | `Wernes_B_Shabti` (D:\TheAfterlifeofAnkhu\Assets\Ankhu\Realms\Generated\Scenes\Wernes_B_Shabti.unity) |
| Realm | Wernes |
| Title | The Shabti With My Name |
| Play order | 12 |
| Previous | Wernes_Hub (after A) |
| Next | Wernes_Hub (after B) |
| Script | D:\TheAfterlifeofAnkhu\Assets\Ankhu\Realms\Core\Content\WernesPaintedScripts.cs, `BuildShabtiB()` (lines 227 to 387) |
| Builder | D:\TheAfterlifeofAnkhu\Assets\Ankhu\Realms\Editor\WernesPaintedBuilderB.cs (zones lines 275 to 278) |
| Design | wernes_build_spec_v2.md section 4 Room B, solver W4v3 in wernes_rooms_v2.py |

## 2. Intent and story beat

Three family plots share one canal. On the far bank a fine painted shabti with Ankhu's name in a cartouche on its chest has carried water since the day he was buried, and while it works everything stays manageable. To stop it Ankhu must read its name on the far bank, come back, and call it by name across the water as a worker is called in life: `hetep ankhu`. When the work stops, everything it held back comes at once (the surge). The beat is Memory A (`memA_river_sunset`): Hori on his shoulders at sunset, "He will not fall. You are holding me and I am holding him." The line that closes it, "It has been working under my name. So have I.", is the self-acceptance turn of Wernes.

## 3. Layout and zones

| Zone id | Centre x, size | What stands there | Objects reached |
|---|---|---|---|
| `vignette` | -13.4, 2.4 | painting of four workers resting and a fifth working | usable `read_vignette` |
| `plot1`, `plot2`, `plot3` | PlotX[0..2], 5.4 wide | each: a plot, a hand-wheeled sluice, a blank shabti | `sluiceN`, `shabtiN`, `plotN`, usables `shutN`, `drainN` |
| `bridge` | 9.2, 3.4 | footbridge over the canal, swings in the current | usables `cross_over`, `look_named` |
| `farbank` | 20.6, 7.0 | the painted named shabti and its chest | `chest`, usables `read_chest`, `cross_back` |

Ankhu spawns at x -15.6. Crossing is a RealmRide on the `side` view (forward state `far`). The named shabti, bridge, side, surge and split have no zones.

## 4. Elements

| Id | Name | Aliases | Initial | States | Words |
|---|---|---|---|---|---|
| `sluice1..3` | first/second/third sluice | sluice, gate, sluice gate, sluice N, Nth sluice, sluice Nth | shut | shut, open | open: shut to open, event `sluice_open`. turn (deed): open to shut "You wheel the gate down by hand." |
| `shabti1..3` | first/second/third shabti | shabti, worker, servant, shabti N, Nth shabti, blank shabti | asleep | asleep, carrying, poured | wake: asleep to carrying "It rises and takes up its jars.", event `blank_awake`. still with only its own sluice open: carrying to poured "It pours its last jar into the furrow, and sits.", event `poured`. still otherwise: carrying to asleep "It sets its jars down. There was no water to carry.", event `poured_dry`. bind: refused "It is held. It is not resting." (no event here) |
| `plot1..3` | first/second/third plot | plot, field, furrow, furrows, plot N, Nth plot | dry | dry, wet, drowned | drain (deed, own sluice shut): drowned to dry |
| `named` | shabti with your name | ankhu, painted shabti, named shabti, far shabti, fine shabti, my shabti, shabti ankhu, ankhu shabti | working | working, still | still (chest read, Ankhu near): working to still, also surge=pending, event `named_stilled`. Refusal "It does not hear you. It answers only to its name, called across the water." Refuses "still painted/named/far/fine shabti" with "It does not answer to that." and "bind painted shabti" with "It is held. It is not resting. It pulls against the thread and works on." |
| `chest` | chest of the painted shabti | chest, name, cartouche, inscription, writing | unread | unread, read | read (deed, Ankhu far): event `name_read`. Zone `farbank`. Refusal "From here it is only a glint." |
| `bridge` | footbridge | bridge, footbridge, planks, plank bridge | free | free, held | bind: free to held, event `bridge_held`. release: held to free |
| `side` | bank | none | near | near, far | cross (deed, bridge held): near to far (event `cross_far`) and back (event `cross_near`). Refusal "The bridge swings out over the water and back." |
| `surge` | surge | none | none | none, pending, hit, spent | tick and auto |
| `split` | split | none | no | no, yes | auto: any two sluices open gives yes and event `split` |

**World clock** after every action, refusal or wait: (1) every shabti in `poured` turns its plot dry to wet or wet to drowned and goes back to asleep. (2) If surge is pending, every open sluice drowns its plot and surge becomes hit (any drowned) or spent. (3) Fewer than two sluices open resets split to no. Then auto: surge hit becomes spent with event `surge_drowned`.

**Goal**: plot1, plot2, plot3 wet, named still, bridge free, side near.

Usables:

| Id | Zone | Prompt | Deed | Shown when |
|---|---|---|---|---|
| `shut1..3` | plotN | Wheel the gate shut | turn sluice N | sluiceN open |
| `drain1..3` | plotN | Break the bank and drain the plot | drain plot N | plotN drowned and sluiceN shut |
| `cross_over` | bridge | Cross the bridge | cross | side near and bridge held |
| `cross_back` | farbank | Cross back | cross | side far and bridge held |
| `read_chest` | farbank | Read the shabti's chest | read chest | chest unread |
| `look_named` | bridge | Look across at the painted shabti | none | chest unread and side near |
| `read_vignette` | vignette | Look at the painting | none | always |

## 5. Words

Known on entry (room Verbs and CodexOnEntry): open, wake, still, bind, release. Deeds: turn, drain, cross, read. Nothing is learned in this room. Seal is not known (spec: sluices are shut by hand because khetem is not known yet), so "seal sluice" or "shut sluice" (an alias of seal) is UnknownVerb.

| Situation | Judgement | Message |
|---|---|---|
| "still painted shabti" | Refused | "It does not answer to that." (ticks) |
| "hetep ankhu" before reading, or from the far bank | Refused | "It does not hear you. It answers only to its name, called across the water." (ticks) |
| "still shabti" on a sleeping blank shabti | Refused | "The word finds nothing to hold." (default refusal, ticks) |
| "open sluice" twice | NoChange | "The first sluice is already open." (ticks) |
| "hetep ankhu" again after it worked | NoChange | "The shabti with your name is already still." |
| "seal sluice" | UnknownVerb | "You know this word exists, but your mouth cannot shape it yet." |

"This time, nothing has changed." is not produced in this room (no name rules).

## 6. Canonical solution (bible order, named shabti stopped before the last plot)

| # | Zone | Action | State after | Beats |
|---|---|---|---|---|
| 0 | spawn | start | | `arrive` |
| 1 | plot1 | "open sluice" | sluice1 open | |
| 2 | plot1 | "wake shabti" | shabti1 carrying | |
| 3 | plot1 | "still shabti" | poured, tick: plot1 wet, shabti1 asleep | |
| 4 | plot1 | E Wheel the gate shut | sluice1 shut | |
| 5 to 8 | plot2 | same four steps | plot2 wet, sluice2 shut | |
| 9 | bridge | E Look across (optional) | | `look` |
| 10 | bridge | "bind bridge" | bridge held | |
| 11 | bridge | E Cross the bridge | side far | `first_cross` |
| 12 | farbank | E Read the shabti's chest | chest read | `read` |
| 13 | farbank | E Cross back | side near | |
| 14 | bridge | "release bridge" | bridge free | |
| 15 | any near | "hetep ankhu" | named still, tick: surge spent (no sluice open) | `named`: film `memA_river_sunset`, codex `memory.shoulders` |
| 16 to 18 | plot3 | "open sluice", "wake shabti", "still shabti" | plot3 wet, **solved** | `solved` |
| 19 | | solve beat | codex `lore.shabti_row`, Next | |

## 7. Alternate valid orders

- Solver W4v2 order: bind, cross, release is not allowed in the C# port before reading (crossing needs the bridge held, and coming back needs it held too). Valid alternative: bind bridge, cross, read, cross back, release bridge, hetep ankhu first, then water all three plots one at a time. This avoids the surge entirely because no sluice is open when the work stops.
- Plots can be watered in any order. The third sluice does not need shutting at the end (the goal does not check sluices).
- The named shabti can be stopped at any point once read, as long as every sluice is shut at that moment.
- 3,216 distinct shortest solutions, 14 ticks (solver W4v3).

## 8. Failure, refusal and reset behaviour

- Two sluices open: caption "The water divides and reaches neither." (once). Stilling a shabti then says "There was no water to carry." and the caption "No water reached its plot. One gate open, and only one." (once).
- Stilling a shabti twice on a wet plot drowns it.
- Stopping the named shabti with a sluice open: the surge drowns that plot, caption "When the work stops, everything it held back comes at once." (once). Recovery: wheel the gate shut, break the bank to drain (plot back to dry), water it again.
- Releasing the bridge while on the far bank strands Ankhu until he binds it again (allowed from anywhere, not a softlock).
- Idle 45 s: "One canal. Three plots. And one worker who never stops."
- 75 s hints in order: plot1 not wet, plot2 not wet, plot3 not wet, chest unread, named working, bridge held.
- H: only the nudge tier works (see B2). Solver: 2,592 states, 0 softlocks.

## 9. Beats, memory films and codex

| Beat | Trigger | Steps |
|---|---|---|
| `arrive` | Start | Freeze, Title "THE SHABTI WITH MY NAME", caption, Ankhu line, Free |
| `look` | Use `look_named` (repeatable) | Ankhu "Something is written on its chest..." |
| `vignette` | Use `read_vignette` (repeatable) | Ankhu line |
| `split` | Event `split` | caption |
| `dry_pour` | Event `poured_dry` | caption |
| `first_cross` | Event `cross_far` | Ankhu "The planks are wet..." |
| `read` | Event `name_read` | Freeze, 3 lines ("Painted on its chest, in a cartouche. Ankhu."), Free |
| `named` | Event `named_stilled` | Freeze, caption, Wait 0.8, **film `memA_river_sunset`** (D:\TheAfterlifeofAnkhu\Assets\StreamingAssets\Memories\memA_river_sunset.mp4), Write `memory.shoulders`, caption, Ankhu "It has been working under my name. So have I.", Free |
| `surge` | Event `surge_drowned` | caption |
| `murmur` | Idle 45 s | Ankhu line |
| `solved` | Solved | Freeze, caption, Write `lore.shabti_row`, Wait 1.5, Next |

## 10. SFX and audio

| Sound | State |
|---|---|
| Ambience walk_ambience.wav | assigned (no canal or water bed) |
| Spell cast, land, fail | **not assigned** |
| Ankhu spell voice | spell.open.sluice1..3, spell.wake.shabti1..3, spell.still.shabti1..3, word.open/wake/still exist. **No clip for "still named" ("hetep ankhu"), bind or release** |
| Sluice wheel, water running, pouring, plot greening, flood, drain, bridge swing, crossing, surge | **no SFX** |
| Dialogue voice | none recorded for `wernes_b` keys |

## 11. Exit conditions

Goal holds. `solved` ends with Next: `WernesProgress.MarkDone("Wernes_B_Shabti")`, then `ExitOf` to `Wernes_Hub`.

## 12. Acceptance criteria

- AC-1 [auto] With only its own sluice open, wake then still waters a plot (dry to wet) on that tick and the shabti sleeps again.
- AC-2 [auto] The hand deed "Wheel the gate shut" is offered only while that sluice is open, and shuts it.
- AC-3 [auto] Two sluices open fire `split`, and stilling a shabti then pours dry ("There was no water to carry.") and fires `dry_pour`.
- AC-4 [auto] "still painted shabti" is Refused with "It does not answer to that."
- AC-5 [auto] "hetep ankhu" before reading the chest is Refused with the "called across the water" message.
- AC-6 [auto] Crossing is refused while the bridge is free, and works while it is held, firing `first_cross`.
- AC-7 [auto] Reading the chest works only from the far bank and fires `read`.
- AC-8 [auto] "hetep ankhu" from the near bank after reading stills the named shabti, fires `named`, plays film `memA_river_sunset` and writes `memory.shoulders`.
- AC-9 [auto] Stopping the named shabti with a sluice open drowns that plot and fires `surge`. Shut, drain and re-water recovers it.
- AC-10 [auto] With all three plots wet, the named shabti still, the bridge free and Ankhu on the near bank, the room solves, writes `lore.shabti_row` and exits to `Wernes_Hub`.
- AC-11 [manual] The near bank shows the chest as an unreadable glint and the far bank shows the cartouche readable.
- AC-12 [manual] Ankhu visibly crosses the swinging bridge and back.
- AC-13 [manual] Every interaction has SFX (currently fails, B4).
- AC-14 [auto] Anpu is not in the room, he stayed at the Memory House (fixed 2026-11-26).

## 13. Code vs intent disagreements

| # | Severity | Where | Problem | Proposed fix |
|---|---|---|---|---|
| B1 | major | RealmRules.cs `Tokenize` line 557 drops digits. Aliases "sluice 1", "plot 2", "shabti 3" (WernesPaintedScripts.cs lines 245 to 249) can never match. Typing "open sluice 2" while standing at plot 1 becomes "open sluice" and silently opens **sluice 1** (resolved by zone). The usables only work because their zone picks the right one | Keep digits in Tokenize (or map "1" to "first"), and add a test that "open sluice 2" from plot1 is OutOfReach |
| B2 | major | RealmHintSolver.cs line 93 | First aliases "sluice", "shabti", "plot" are shared by three objects and Ambiguous from zone null. "cross bank" (Name of `side`) and "read chest of the painted shabti" do not parse. The solver finds no path, so H never gives direction or answer here | as A2 in 11 |
| B3 | minor | WernesPaintedScripts.cs line 300 | `bind shabti` here refuses without teaching anything, which is right, but the refusal for a sleeping blank shabti "still" falls to the default "The word finds nothing to hold." | Give still a refusal: "It is already resting." |
| B4 | major | Wernes_B_Shabti.unity | No spell or object SFX, no hetep-ankhu voice | Author and assign |
| B5 | minor | Spec v2 Room B "Memory A (60 s, playable)" | Code plays an 18 to 20 s film, not a playable memory. This follows Zac's later memory film rule (2026-11-25) | Update the spec |
| B6 | minor | Spec v2 Room B "On return: the north plot turns green" and hub H2 lines | The hub's `back_b` talk does not match H2, and the falcon line is used one room early in `back_a` (see H1 in 10) | Align with the spec |
| B7 | minor | RealmSceneBuilder.cs `Cast` | Anpu follows Ankhu here although he stayed at the hub | as A5 |

## 14. E2E script

```e2e canonical
scene Wernes_B_Shabti
beat arrive
at plot1
anpu absent [AC-14]
say open sluice => Success
expect sluice1=open
say wake shabti => Success
expect shabti1=carrying
say still shabti => Success | It pours its last jar into the furrow [AC-1]
expect plot1=wet [AC-1]
expect shabti1=asleep [AC-1]
use shut1 [AC-2]
expect sluice1=shut [AC-2]
at plot2
say open sluice => Success
expect sluice2=open
say wake shabti => Success
say still shabti => Success
expect plot2=wet [AC-1]
use shut2
expect sluice2=shut
at bridge
use look_named
beat look
say bind bridge => Success [AC-6]
expect bridge=held
use cross_over [AC-6]
expect side=far [AC-6]
beat first_cross [AC-6]
at farbank
use read_chest [AC-7]
expect chest=read [AC-7]
beat read [AC-7]
use cross_back
expect side=near
at bridge
say release bridge => Success
expect bridge=free
say hetep ankhu => Success [AC-8]
expect named=still [AC-8]
expect surge=spent [AC-8]
beat named [AC-8]
film memA_river_sunset [AC-8]
codexhas memory.shoulders [AC-8]
nobeat surge
at plot3
say open sluice => Success
say wake shabti => Success
say still shabti => Success
expect plot3=wet [AC-10]
solved [AC-10]
beat solved [AC-10]
codexhas lore.shabti_row [AC-10]
exit Wernes_Hub [AC-10]
```

```e2e alt-1
scene Wernes_B_Shabti
at bridge
say bind bridge => Success
use cross_over
at farbank
use read_chest
use cross_back
at bridge
say release bridge => Success
say hetep ankhu => Success
nobeat surge
at plot3
say open sluice => Success
say wake shabti => Success
say still shabti => Success
expect plot3=wet
use shut3
at plot1
say open sluice => Success
say wake shabti => Success
say still shabti => Success
use shut1
at plot2
say open sluice => Success
say wake shabti => Success
say still shabti => Success
solved
exit Wernes_Hub
```

```e2e fail-1
scene Wernes_B_Shabti
at plot1
say open sluice => Success
at plot2
say open sluice => Success
beat split [AC-3]
expect split=yes [AC-3]
say wake shabti => Success
say still shabti => Success | There was no water to carry. [AC-3]
beat dry_pour [AC-3]
expect plot2=dry [AC-3]
say still painted shabti => Refused | It does not answer to that. [AC-4]
say hetep ankhu => Refused | It answers only to its name, called across the water. [AC-5]
say seal sluice => UnknownVerb
at bridge
say cross => Refused | The bridge swings out over the water and back. [AC-6]
expect side=near [AC-6]
unsolved
```

```e2e fail-surge
scene Wernes_B_Shabti
at bridge
say bind bridge => Success
use cross_over
at farbank
use read_chest
use cross_back
at bridge
say release bridge => Success
at plot3
say open sluice => Success
say hetep ankhu => Success
expect named=still
expect plot3=drowned [AC-9]
expect surge=spent [AC-9]
beat surge [AC-9]
use shut3 [AC-9]
use drain3 [AC-9]
expect plot3=dry [AC-9]
say open sluice => Success
say wake shabti => Success
say still shabti => Success
expect plot3=wet [AC-9]
use shut3
at plot1
say open sluice => Success
say wake shabti => Success
say still shabti => Success
use shut1
at plot2
say open sluice => Success
say wake shabti => Success
say still shabti => Success
solved [AC-9]
exit Wernes_Hub
```
