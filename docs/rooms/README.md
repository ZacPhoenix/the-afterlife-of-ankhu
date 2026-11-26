# Puzzle room master guides

One guide per room in play order (D:\TheAfterlifeofAnkhu\docs\PLAY_ORDER.md). Each guide covers the puzzle's intent and story beat, zones, every element and its states, the words that act on it, the canonical solution, alternate and failing orders, refusal and reset behaviour, beats, memory films and codex writes, SFX, exit conditions, numbered acceptance criteria, and every place where the code and the intent disagree. The last section of each guide holds machine-readable E2E blocks that the tests below play. The format is in D:\TheAfterlifeofAnkhu\docs\rooms\GUIDE_FORMAT.md.

## Rooms

| # | Scene | Guide | Acceptance criteria (auto / total) |
|---|---|---|---|
| 4 | Realm_01_Arit1_Painted, The First Gate | D:\TheAfterlifeofAnkhu\docs\rooms\04_Realm_01_Arit1_Painted.md | 13 / 16 |
| 5 | Teach1_Cistern_Painted, The Cistern | D:\TheAfterlifeofAnkhu\docs\rooms\05_Teach1_Cistern_Painted.md | 10 / 12 |
| 6 | Teach2_HeldStep, The Held Step | D:\TheAfterlifeofAnkhu\docs\rooms\06_Teach2_HeldStep.md | 7 / 9 |
| 7 | Teach3_LingeringShadow, The Lingering Shadow | D:\TheAfterlifeofAnkhu\docs\rooms\07_Teach3_LingeringShadow.md | 7 / 10 |
| 8 | BorrowedDawn_Painted, The Borrowed Dawn | D:\TheAfterlifeofAnkhu\docs\rooms\08_BorrowedDawn_Painted.md | 8 / 11 |
| 9 | Wernes_Terrace, The Terrace Above Wernes | D:\TheAfterlifeofAnkhu\docs\rooms\09_Wernes_Terrace.md | 7 / 11 |
| 10, 15 | Wernes_Hub, The Memory House (every return, and the final one) | D:\TheAfterlifeofAnkhu\docs\rooms\10_Wernes_Hub.md | 9 / 12 |
| 11 | Wernes_A_Field, The Field That Waits | D:\TheAfterlifeofAnkhu\docs\rooms\11_Wernes_A_Field.md | 14 / 17 |
| 12 | Wernes_B_Shabti, The Shabti With My Name | D:\TheAfterlifeofAnkhu\docs\rooms\12_Wernes_B_Shabti.md | 10 / 13 |
| 13 | Wernes_C_House, The House With One Door | D:\TheAfterlifeofAnkhu\docs\rooms\13_Wernes_C_House.md | 10 / 12 |
| 14 | Wernes_D_Bricks, The Birth Bricks | D:\TheAfterlifeofAnkhu\docs\rooms\14_Wernes_D_Bricks.md | 7 / 9 |
| 16 | Wernes_Finale, The Barque of Ra | D:\TheAfterlifeofAnkhu\docs\rooms\16_Wernes_Finale.md | 3 / 6 |
| 17 | Rosetau_A_Laugh, The Laugh in the Dark | D:\TheAfterlifeofAnkhu\docs\rooms\17_Rosetau_A_Laugh.md | 14 / 18 |
| 18 | Rosetau_B_Mourners, The Room of the Mourners | D:\TheAfterlifeofAnkhu\docs\rooms\18_Rosetau_B_Mourners.md | 11 / 14 |
| 19 | Rosetau_C_Mother, The Mother's Room | D:\TheAfterlifeofAnkhu\docs\rooms\19_Rosetau_C_Mother.md | 10 / 11 |
| 20 | Rosetau_D_TwoWays, The Two Ways | D:\TheAfterlifeofAnkhu\docs\rooms\20_Rosetau_D_TwoWays.md | 11 / 13 |
| 21 | Rosetau_E_Finale, The Shield Brought Home | D:\TheAfterlifeofAnkhu\docs\rooms\21_Rosetau_E_Finale.md | 11 / 14 |

Title, cinematic and the Duat walk (1 to 3) have no puzzle and no guide. The chain test starts at the First Gate.

## The tests

Code: D:\TheAfterlifeofAnkhu\Assets\Ankhu\Tests\RoomGuides (test-only assemblies, never in a player build).

| Test | What it drives | Command |
|---|---|---|
| Rules simulation (EditMode) | Every e2e block through the real RealmRoom rules and a line-for-line copy of the director's beat logic (Once, flags, learns, set states, films, codex, exits). Teaching rooms and Borrowed Dawn run through BorrowedDawnRulesAdapter. Seconds. | `Unity.exe -batchmode -projectPath D:\TheAfterlifeofAnkhu -runTests -testPlatform EditMode -assemblyNames Ankhu.Tests.RoomGuides.Editor -testResults <xml>` |
| Per-room play mode | Loads each scene and plays the block through the real director, speech bubble (InsertWord and Submit), E-uses, beats, codex roll, memory films, rides and the scene exit. | `... -runTests -testPlatform PlayMode -assemblyNames Ankhu.Tests.RoomGuides -testFilter "canonical\|after-"` |
| Whole game chain | The canonical blocks back to back from the First Gate to the shield brought home, each starting in the scene the previous exit loaded, with codex and Wernes progress carried over. | `... -testFilter "WholeGameInPlayOrder"` |

Reports land in D:\TheAfterlifeofAnkhu\art_gen\realms\audit\room_guides_sim.md, room_guides_play.md and room_guides_chain.md, with every failing line, what the game answered, and a trace.

What the tests do not cover: how anything looks or sounds, walking between zones (Ankhu is placed inside each zone), hint tiers on H, idle beats, and the teaching rooms' speech bubble (those rooms are driven through their rules adapter).

## Fixed on 2026-11-26

| Room | Bug | Fix |
|---|---|---|
| All realm rooms | A word already spoken to effect answered "The X is already Y.", or the wrong refusal, instead of "This time, nothing has changed." (reopening a doorway in Rosetau A fired the wrong-doorway beat) | RealmRules.cs remembers which words worked and answers AlreadySpoken |
| All realm rooms | Standing in no zone reached every zoned object (the hub's false door answered "Ankhu" from anywhere) | RealmRules.cs Reaches |
| All realm rooms | Digits were stripped from typed words, so "open sluice 2" opened sluice 1 | RealmRules.cs Tokenize keeps digits |
| All realm rooms | The H-key hint solver spoke with no zone, so shared names came back Ambiguous and it never got past the nudge | RealmHintSolver.cs speaks from the target's zone |
| First Gate | Saying "Ankhu" after the gate opened answered "The gate is still listening" | Second rule for the open gate, name rules now pick the one whose conditions hold |
| Wernes B | The chest could never be read while the bridge was held (two prompts on the far bank, Cross back always won) | Usable order in WernesPaintedScripts.cs |
| Wernes D | lore.birth_bricks was written on entry, so the solve's keystone page never showed | Removed from CodexOnEntry |
| Rosetau A, C | give and remember had no codex entry, so learning them wrote nothing, and remember (the winning word in C) was never taught | word.give and word.remember in CodexContent.cs, remember taught when the lamp catches |
| Rosetau C | A bare "echo" answered "Echo what?" because two rules were the same deed | Bare verbs pick the first when every candidate raises the same event |
| Rosetau D | A wrong name answered "This time, nothing has changed." after a right one, and the wrong-name beat never played | Standing refusals are never "spent" |
| Rosetau D | The first still of the fire captioned twice | fire_low_again waits for the first beat's flag |

Backups of the files before the change: D:\TheAfterlifeofAnkhu\_staging\room_guides\backup_core.

## Still open (needs a decision or its own job)

1. **SFX.** Spell cast, land and fail clips are unassigned in every realm scene, no object state change has a sound, and the teaching rooms and Borrowed Dawn have no audio besides voice. There are no spell SFX clips in the project yet. This breaks the rule that every interaction needs authored SFX. Best done as its own job with earshot.
2. **The world only moves when Ankhu acts.** The director never advances the clock on its own, so the swinging door (Wernes C), the laugh (Rosetau A), the lamps (Rosetau B) and the fire (Rosetau D) stand still until the player types. "wait" works but is never taught. Choose between a real-time tick while idle or teaching "wait".
3. **Wernes A** teaches hetep only if the player happens to try "bind shabti". Nothing points there.
4. **Hub return lines.** back_a talks about the falcon not falling, which is Memory A, and Memory A plays in room B.
5. **Anpu follows Ankhu** in rooms where he says he waits (Wernes A to D, the hub, Rosetau A, B and E). In Rosetau E he walks off the shield.
6. **Missing voice clips**: Wernes and Rosetau dialogue, name.hori (the terrace climax is silent), word.bind, release, flow, echo, the Teach3 farewell, the Borrowed Dawn exit lines.
7. **Borrowed Dawn engine wording.** Teaching rooms say "Those words have no power here yet." and their already-done messages are room specific, not "This time, nothing has changed." After the win, commands answer "Restart to play again." and Hold R still resets during the exit.
8. **Small ones** are listed per room in section 13 of each guide.
