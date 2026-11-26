# 20 Rosetau_D_TwoWays: The Two Ways

## 1. Summary

| Field | Value |
|---|---|
| Scene | `Rosetau_D_TwoWays` |
| Realm | Rosetau |
| Title | The Two Ways |
| Play order | 20 |
| Previous | 19 `Rosetau_C_Mother` |
| Next | 21 `Rosetau_E_Finale` |
| Script | D:\TheAfterlifeofAnkhu\Assets\Ankhu\Realms\Core\Content\RosetauScripts.cs, `BuildTwoWaysD()` (lines 479 to 650) |
| Scene builder | D:\TheAfterlifeofAnkhu\Assets\Ankhu\Realms\Editor\RosetauBuilder.cs, `BuildTwoWaysD()` (lines 624 to 711) |
| Scene file | D:\TheAfterlifeofAnkhu\Assets\Ankhu\Realms\Generated\Scenes\Rosetau_D_TwoWays.unity |
| Design | D:\TheAfterlifeofAnkhu\docs\ROSETAU_DESIGN.md, row Rosetau_D_TwoWays |

## 2. Intent and story beat

The Book of Two Ways: a black water road and a blue land road with a channel of fire between. Three faceless soldier shades keep a watch nobody relieved. This is the one place in Rosetau where `still` is welcome, on the fire, which turns the boy's hush into a tool used on the right thing. While the fire is low Ankhu can `echo` a shade, who calls back his town, and then `name` him at his post by matching what he carries to the roster: Paser of Iunu (shield), Nakht of Abdju (empty shield strap, because his shield was carried home), Huy of Abdju (bow). The middle shade whistles the four falling notes of the reed whistle Ankhu lost. Named, Nakht has a face: "Ankhu? You've gone grey." The solve plays `ros_father` and Nakht asks: "Then bring me home on that." The deduction (roster, carried object, post) is the puzzle. The fire window is the pressure.

## 3. Layout and zones

One camera `cam_room`, x about -9.5 to 9.5. The walk lane is the near bank (z -2.3 to 0.95). The shades stand on the far bank at z 2.1.

| Zone id | Centre x | Width | What stands there | Reaches |
|---|---|---|---|---|
| `roster` | -6.4 | 2.0 | Garrison roster on a peg by the stair. Ankhu spawns inside it (x -7.2) | `read_roster` usable |
| `floor` | -3.4 | 2.2 | Coffin Texts spell 1130 on the floor | `read_floor` usable |
| `post1` | 0.0 | 2.4 | Across from the shade with the shield (Paser) | `shade_paser` |
| `post2` | 3.0 | 2.4 | Across from the shade with the empty strap (Nakht) | `shade_nakht` |
| `post3` | 6.0 | 2.4 | Across from the shade with the bow (Huy) | `shade_huy` |

The fire is reachable from anywhere. Saved scene zone ids (checked 2026-11-26): post1, post2, floor, post3, roster plus one camera zone. They match. Anpu is deactivated (builder line 692).

## 4. Elements

### Room objects

| Id | Name | Key aliases | Initial | States | Zones |
|---|---|---|---|---|---|
| `fire` | fire | fire, flame, flames, channel, channel of fire, lake of fire, red channel, blaze, burning | roaring | roaring, low | any |
| `fclock` | (internal) | | 0 | 0 to 2 | |
| `watch` | (internal) | | kept | kept, relieved | |
| `shade_paser` | soldier | shade, soldier, man, guard, watchman, shadow, ghost, faceless man, left shade, left soldier, left man, shield, man with the shield, soldier with the shield, shield bearer, shield-bearer | faceless | faceless, heard, named | post1 |
| `shade_nakht` | soldier | common aliases plus middle shade, middle soldier, middle man, strap, empty, man with the empty strap, empty-handed, empty handed, soldier with no shield, man with no shield | faceless | faceless, heard, named | post2 |
| `shade_huy` | soldier | common aliases plus right shade, right soldier, right man, bow, archer, bowman, man with the bow, soldier with the bow | faceless | faceless, heard, named | post3 |

### World clock (lines 522 to 528)

When the fire is low, each tick adds 1 to `fclock`. At 3 it roars again and `fclock` resets. `still` sets fclock 0 and then its own tick makes it 1. So after `still fire` exactly two more ticking actions happen while the fire is low (for example `echo` then the name). Walking, reading and words that are not understood do not tick.

### Rules

| Verb | Target | From | To | Requires | Event | Success | Refusal |
|---|---|---|---|---|---|---|---|
| still | fire | roaring | low (also fclock 0) | | `fire_low` | "Hetep. The fire sinks to a red crawl along the channel." | "It is already low. Be quick." (never shown, 13.6) |
| echo | each shade | faceless | heard | fire low | `heard_iunu` / `heard_abdju` / `heard_abdju2` | "Wesheb. You call across the channel, and he calls back." | "The fire roars between you. Your voice does not carry." |
| echo | each shade | heard | heard | fire low | same | "Wesheb. He calls back again." | same |
| still | each shade | never | never | | | | "He has been still long enough." |
| name (wrong post) | each other shade | never | never | | (refusal) `wrong_name` | | "Nobody turns to that name." |
| name paser | shade_paser | heard | named | fire low | `paser_named` | "Ren. Paser." | "The fire takes the name. Quiet it, and let him answer you first." |
| name nakht | shade_nakht | heard | named | fire low | `nakht_named` | "Ren. Nakht." | same |
| name huy | shade_huy | heard | named | fire low | `huy_named` | "Ren. Huy." | same |
| auto | watch | | relieved | all three named | `relieved` | | |

For each name the wrong-post rules come first in the list and the right-post rule last. `Judge` picks the first rule with that name that reaches the current zone, so standing at a post decides which rule hears it.

Goal: `watch = relieved`.

### Usables

| Id | Zone | Prompt |
|---|---|---|
| `read_roster` | roster | Read the roster |
| `read_floor` | floor | Read the floor |

## 5. Words

| Kind | Words |
|---|---|
| Known on entry | open, wake, still, release, echo, give, remember, name |
| Codex on entry | word.still, word.echo, word.name, lore.two_ways |
| Names | paser, nakht, huy (typed alone, or "ren nakht", "name nakht", "i am ...") |
| Room clock | wait, watch, rest |

Messages:

| Situation | Judgement | Message | Ticks |
|---|---|---|---|
| `echo` with the fire roaring | Refused | "The fire roars between you. Your voice does not carry." | yes |
| A right name before the shade has answered | Refused | "The fire takes the name. Quiet it, and let him answer you first." | yes |
| A wrong name at a post whose shade is still faceless | Refused | "Nobody turns to that name." then "Not him. Look at what he's carrying." | yes |
| A wrong name at a post whose shade is heard or named | NoChange today "This time, nothing has changed." (bug 13.1). Intended Refused "Nobody turns to that name." | | yes |
| A right name again after he is named | NoChange | "This time, nothing has changed." (correct) | yes |
| A name from roster, floor or no post | OutOfReach | "Too far. Come closer." | no |
| `still fire` while low | NoChange | "The fire is already low." and it costs a tick of the window | yes |
| `bind`, `seal` | UnknownVerb | "your mouth cannot shape it yet" | no |

## 6. Canonical solution

Start: beat `arrive` (title, caption, two Ankhu lines).

| # | Zone | Input | Result | State after | Beats |
|---|---|---|---|---|---|
| 1 | roster | E on `read_roster` | Use | | `read_roster` (Nakht of Abdju, shield-bearer. Paser of Iunu, shield-bearer. Huy of Abdju, archer.) |
| 2 | floor | E on `read_floor` | Use | | `read_floor` |
| 3 | post1 | `still fire` | Success | fire low, fclock 1 | `fire_low` (and `fire_low_again`, bug 13.2) |
| 4 | post1 | `echo` | Success | paser heard, fclock 2 | `heard_paser` ("Iunu! Iunu!") |
| 5 | post1 | `paser` | Success | paser named, fire roaring | `paser` |
| 6 | post2 | `still fire` | Success | fire low | `fire_low_again` |
| 7 | post2 | `echo` | Success | nakht heard | `heard_nakht` (whistle) |
| 8 | post2 | `nakht` | Success | nakht named, fire roaring | `nakht` ("You've gone grey.") |
| 9 | post3 | `still fire` | Success | fire low | |
| 10 | post3 | `echo` | Success | huy heard | `heard_huy` |
| 11 | post3 | `huy` | Success | huy named, watch relieved, solved | `huy`, `solved`: film `ros_father`, codex `memory.father`, Next |

## 7. Alternate valid orders

- The three men in any order. The window and steps are the same at each post.
- `echo shade`, `echo soldier`, `echo middle`, `echo archer` at the right post all work. `ren nakht`, `name nakht`, `nakht` all work.
- Re-echo a heard shade ("He calls back again.") to replay his town line.
- A window holds two ticking actions. Echo and name must both be spoken at the same man's post, so the natural rhythm is one man per window. A man heard in an earlier window can be named with the first action of a later one.

Orders that fail: naming before the echo, echoing or naming with the fire roaring, a third action after `still` (the fire is back up), a wrong name at a post.

## 8. Failure, refusal and reset behaviour

No reset. The fire returns on its own. Failure beats: `wrong` on `wrong_name` (repeatable). Idle `murmur` after 45 s: "I was six. I don't even know which one is his face." Hints:

- fire roaring, all faceless: "Nobody can hear anything over that fire. You've quieted worse."
- fire low, Paser and Nakht faceless or heard: "The fire is low. Call to them while it lasts."
- otherwise while the watch is kept: "The roster by the stair has three names, their towns and what they carried. Match each man to his line, stand at his post and say it."

## 9. Beats, memory films and codex

| Beat id | Trigger | Once | Steps |
|---|---|---|---|
| `arrive` | Start | yes | Freeze, Title, Caption, two Ankhu lines, Free |
| `read_roster` | Use `read_roster` | no | Caption roster text, two Ankhu lines |
| `read_floor` | Use `read_floor` | no | Caption Coffin Texts 1130, Ankhu line |
| `fire_low` | Event `fire_low` | yes | Caption "The fire sinks. The three faceless men lift their heads.", Ankhu line |
| `fire_low_again` | Event `fire_low` | no | Caption "The fire sinks again." |
| `heard_paser` / `heard_nakht` / `heard_huy` | Events `heard_iunu` / `heard_abdju` / `heard_abdju2` | no | Caption and the town called back. Nakht also whistles |
| `wrong` | Event `wrong_name` | no | Ankhu "Not him. Look at what he's carrying." |
| `paser`, `huy` | Events `paser_named`, `huy_named` | yes | Caption, Ankhu line |
| `nakht` | Event `nakht_named` | yes | Freeze, caption, Father "Ankhu? You've gone grey.", two more lines, Free |
| `murmur` | Idle 45 s | yes | Ankhu line |
| `solved` | Solved | yes | Freeze, caption, `flashback:ros_father`, four lines, Write `memory.father`, Wait 1.5, Next |

Film present at D:\TheAfterlifeofAnkhu\Assets\StreamingAssets\Memories\ros_father.mp4. Codex `memory.father`, `lore.two_ways` exist.

## 10. SFX and audio

| Source | Status |
|---|---|
| Ambience walk_ambience.wav | assigned. No fire roar bed |
| Spell cast, land, fail | null |
| Fire roaring and sinking, shades calling back, Nakht's four-note whistle | none. The whistle is the emotional key of the room and exists only as a caption |
| Spell voice | word.still, word.name exist. No word.echo, no name.paser, name.nakht, name.huy |
| Dialogue (Ankhu, Father, Other) | none recorded |

## 11. Exit conditions

Goal `watch = relieved` (all three named). Next to `PlayOrder.ExitOf("Rosetau_D_TwoWays")` = `Rosetau_E_Finale`.

## 12. Acceptance criteria

- AC-1 [auto] Loads with fire roaring, all three shades faceless, watch kept, unsolved.
- AC-2 [auto] `arrive` fires on start. `read_roster` is offered in `roster`, `read_floor` in `floor`, each fires its beat.
- AC-3 [auto] `still fire` lowers the fire and fires `fire_low`. The fire stays low for exactly two further ticking actions and then roars.
- AC-4 [auto] `echo` with the fire roaring is Refused with "Your voice does not carry."
- AC-5 [auto] With the fire low, `echo` at a post makes that shade heard and fires his town beat.
- AC-6 [auto] The right name at the right post, with the shade heard and the fire low, names him and fires his beat.
- AC-7 [auto] A right name before the echo is Refused with "Quiet it, and let him answer you first."
- AC-8 [auto] A wrong name at a post is Refused with "Nobody turns to that name." and fires `wrong`, whether that post's shade is faceless, heard or named.
- AC-9 [auto] A right name spoken again after the man is named answers "This time, nothing has changed."
- AC-10 [auto] The first `still fire` shows "The fire sinks." only, not also "The fire sinks again."
- AC-11 [auto] All three named relieves the watch, solves, plays `ros_father`, writes `memory.father`, exits to `Rosetau_E_Finale`.
- AC-12 [manual] The fire roars audibly and drops when stilled. Each shade's call and Nakht's whistle are audible.
- AC-13 [manual] Faceless, heard (cold light) and named (face, near bank) cards read clearly.

## 13. Code vs intent disagreements

| # | Severity | Where | Problem | Proposed fix |
|---|---|---|---|---|
| 13.1 | major | D:\TheAfterlifeofAnkhu\Assets\Ankhu\Realms\Core\RealmRules.cs:497 (`AlreadySpent`) with RosetauScripts.cs:557 | The wrong-name rules use the sentinel From `never`. `AlreadySpent` treats any name rule whose From excludes the object's initial state as "needs learning first", and once the shade at that post is heard or named it reports NoChange "This time, nothing has changed." So the most common mistake (echo Paser, then say "nakht" at his post) does not say "Nobody turns to that name." and the `wrong` beat ("Look at what he's carrying") never plays. It also costs a tick of the fire window | In `AlreadySpent`, return false for sentinel rules (`From.Length == 1 && From[0] == To`, or To == "never") |
| 13.2 | minor | RosetauScripts.cs:604 | `fire_low` (once) and `fire_low_again` (repeatable) share the key `fire_low`, so the first still shows both captions: "The fire sinks." then "The fire sinks again." | Add `ForbidsFlags` to `fire_low_again` for a flag set by `fire_low` (Step.Flag("fire_seen")) |
| 13.3 | minor | RealmRules.cs:442 | `echo` at a named shade with the fire low says "The fire roars between you." because the NoChange check compares against the first rule's To (heard) | Spent-word fix from guide 17, 13.1, plus an echo rule From named To named with a short caption |
| 13.4 | major | D:\TheAfterlifeofAnkhu\Assets\Ankhu\Realms\Runtime\Director\RealmSceneDirector.cs | The fire window is measured in typed actions, not seconds. A refused name or a mistyped target that is Refused eats the window, but a typo that is InvalidTarget does not. The player is never told the window is counted in words | Show the fclock as the fire's visual (low, then flickering up) and a caption on the last safe tick ("The fire is rising."), via an auto rule at fclock 2 |
| 13.5 | minor | builder | Ankhu spawns inside `roster` | Spawn at x -8.0 |
| 13.6 | minor | RosetauScripts.cs still fire rule | Its Refusal "It is already low. Be quick." is never shown, NoChange "The fire is already low." wins | Accept or reorder |
| 13.7 | minor | D:\TheAfterlifeofAnkhu\Assets\Ankhu\Realms\Core\Lexicon.cs:112 | "name" itself is not a verb alias. "name nakht" works only through the `NameIn` prefix list, so "name the middle man nakht" fails | Accept |

## 14. E2E script

```e2e canonical
scene Rosetau_D_TwoWays
beat arrive [AC-2]
expect fire=roaring [AC-1]
expect shade_paser=faceless [AC-1]
expect shade_nakht=faceless [AC-1]
expect shade_huy=faceless [AC-1]
expect watch=kept [AC-1]
unsolved [AC-1]
at roster
use read_roster [AC-2]
beat read_roster [AC-2]
at floor
use read_floor [AC-2]
beat read_floor [AC-2]
at post1
say echo => Refused | does not carry [AC-4]
say still fire => Success [AC-3]
expect fire=low [AC-3]
beat fire_low [AC-3]
nobeat fire_low_again [AC-10]
# fixed 2026-11-26, this used to fail: fire_low_again fires with the first still, see 13.2
say echo => Success [AC-5]
expect shade_paser=heard [AC-5]
beat heard_paser [AC-5]
expect fire=low [AC-3]
say paser => Success [AC-6]
expect shade_paser=named [AC-6]
beat paser [AC-6]
expect fire=roaring [AC-3]
at post2
say still fire => Success
say echo => Success
beat heard_nakht [AC-5]
say nakht => Success [AC-6]
beat nakht [AC-6]
at post3
say still fire => Success
say echo => Success
beat heard_huy [AC-5]
say huy => Success [AC-6]
expect watch=relieved [AC-11]
solved [AC-11]
beat solved [AC-11]
film ros_father [AC-11]
codexhas memory.father [AC-11]
exit Rosetau_E_Finale [AC-11]
```

```e2e fail-1
# Names before echo, wrong names, and the window closing.
scene Rosetau_D_TwoWays
at post1
say paser => Refused | Quiet it [AC-7]
say still soldier => Refused | still long enough
say still fire => Success
expect fire=low
say nakht => Refused | Nobody turns to that name. [AC-8]
beat wrong [AC-8]
say echo => Success
expect shade_paser=heard
expect fire=roaring [AC-3]
say nakht => Refused | Nobody turns to that name. [AC-8]
# fixed 2026-11-26, this used to fail: NoChange "This time, nothing has changed.", see 13.1
say paser => Refused | Quiet it [AC-7]
say still fire => Success
say paser => Success
expect shade_paser=named
say paser => NoChange | This time, nothing has changed. [AC-9]
expect fire=roaring
at roster
say huy => OutOfReach
```
