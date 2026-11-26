# Play order

This is the whole game as a player meets it today, first scene to last. The list in code is
`Assets/Ankhu/Opening/Runtime/PlayOrder.cs`, and every exit follows it. Build Settings order no longer
decides where a room leads (it only decides which scene a built player opens first, which must be the title).

To prove it is all wired, run **Ankhu > Play Order > Check Play Order**, or in batch:

    Unity.exe -batchmode -projectPath D:\TheAfterlifeofAnkhu -executeMethod Ankhu.Realms.EditorTools.PlayOrderCheck.CheckCli -logFile <log>

It opens each scene without saving, asks the component that owns the exit where it goes, compares that with
the next scene here, and checks every film, painted memory still and codex write the scene plays. The report
lands in `D:\TheAfterlifeofAnkhu\art_gen\realms\audit\play_order_check.md` and the exit code is 1 when any
link is broken. **Ankhu > Play Order > Apply Build Settings Order** puts Build Settings in this order.

## The chain

| # | Scene | What happens | How it leaves | Status |
|---|---|---|---|---|
| 1 | Opening_Title | Title menu. New game, or Continue once the walk was reached | TitleMenu fades to the cinematic (Continue skips to the walk) | wired |
| 2 | Opening_Cinematic | Opening film | OpeningCinematic fades to the walk | wired |
| 3 | Opening_WalkDuat | Anpu escorts Ankhu through the Duat | WalkSequence, `OpeningSettings.SceneAfterWalk()` | wired |
| 4 | Realm_01_Arit1_Painted (greybox Realm_01_Arit1 kept) | The First Gate, painted court in the cavern. Sad of Voice, the Eavesdropper and Face-downward wake when named. Anpu gives the empty codex | RealmSceneDirector script Next step | wired |
| 5 | Teach1_Cistern_Painted (greybox Teach1_Cistern kept) | Teaching room 1, the night well, one fixed screen, then film flick_f1_river | TeachingRoomDirector | wired |
| 6 | Teach2_HeldStep | Teaching room 2, then film flick_f2_hands | TeachingRoomDirector | wired |
| 7 | Teach3_LingeringShadow | Teaching room 3, film flick_f3_shadow, Anpu's farewell and the closing door | TeachingRoomDirector into Borrowed Dawn | wired |
| 8 | BorrowedDawn_Painted | Borrowed Dawn alone. On the solve, film mem0_dawn_name (Memory 0) and Anpu's exit lines | BorrowedDawnExit, added at runtime | wired |
| 9 | Wernes_Terrace | Reunion with Anpu at the terrace edge. He asks for the name from the doorway and Ankhu says "Hori". Anpu leads him down the steps past the grey figures | RealmSceneDirector | wired |
| 10 | Wernes_Hub | The Memory House. Anpu waits by the false door. Saying "Ankhu" opens it onto the next room not yet done. Four panels light as rooms finish and Anpu says something new each return | RealmSceneDirector, `WernesProgress.NextRoom()` | wired |
| 11 | Wernes_A_Field | Field. Flashback tease_basin | Back to the hub | wired |
| 12 | Wernes_B_Shabti | Shabti. Flashback memA_river_sunset | Back to the hub | wired |
| 13 | Wernes_C_House | House. Flashbacks memB_door_dawn and sting_khetem_seedlings | Back to the hub | wired |
| 14 | Wernes_D_Bricks | Birth bricks. Painted memory mem_naming, codex memory.naming and lore.birth_bricks | Back to the hub | wired |
| 15 | Wernes_Hub (all four done) | The door opens one last time, onto the terrace at dusk | Wernes_Finale | wired |
| 16 | Wernes_Finale | Finale of the first realm. From the terrace parapet Ankhu and Anpu watch Ra's night barque glide down the valley river, a colossal sun disc under Mehen's arch, while Apep's shadow passes under the water and its eye opens. A frozen painted cinematic | RealmSceneDirector script Next step, to Rosetau_A_Laugh | wired |
| 17 | Rosetau_A_Laugh | The passage under Abdju. A child's laugh from three dark doorways. Echo holds it so one doorway lights, three rounds, then the young Heru stands in the last and Ankhu gives him Hori's clay falcon | RealmSceneDirector script Next step | wired |
| 18 | Rosetau_B_Mourners | Ausar on the lion bier. Auset and Nebet-Het kneel with their mouths bound. Release frees each lament, echo answers both, four lamps light and the sealed door opens | RealmSceneDirector script Next step | wired |
| 19 | Rosetau_C_Mother | The mother's room at Abdju. She weeps at the loom. Echo answers her, wake the loom and the quern, the lamp catches, remember her and the memory plays | RealmSceneDirector script Next step | wired |
| 20 | Rosetau_D_TwoWays | The Book of Two Ways. A channel of fire and three faceless soldier shades. Still the fire for a few breaths, echo each shade, read the roster and name each man at his post. The middle one is Ankhu's father, Nakht | RealmSceneDirector script Next step | wired |
| 21 | Rosetau_E_Finale | The shield brought home, the door at Abdju. Bind the shield in the mourners' bands, Anpu holds it upright, echo answers the young mother, open the mouth, give bread | Back to the title, end of built content | wired (ends) |

The hub sits between every room: Hub, A, Hub, B, Hub, C, Hub, D, Hub. Progress lives in PlayerPrefs
(`WernesProgress`, reset when the terrace starts), and the check report simulates each return.

The codex writes and painted stills of every realm scene are listed per scene in the check report.

## Not built yet (in play order)

These beats are in the story but have no scene, so the chain steps over them. Each is the next thing to
build at that point, and the chain gets one new line when it is.

1. **After Rosetau.** The shield brought home returns to the title. Whatever follows Rosetau goes here. Design notes are in `docs/ROSETAU_DESIGN.md`.

## Notes

- Borrowed Dawn plays the painted scene when `BorrowedDawn_Painted` is in Build Settings, otherwise the greybox.
  The painted scene is a saved copy of the greybox with paint over it, so the greybox's puzzle wiring comes along.
  If the greybox room is rebuilt, rebuild the painted scene too (BorrowedDawnPainted.BuildPaintedCli).
- `Realm_02_Wernes_Field`, `Realm_03_Wernes_House` and `Realm_04_Wernes_Row` are the older 3D Wernes rooms. The
  painted A to D rooms replaced them and nothing leads to them. They stay in Build Settings after the chain.
- Title F9 is a developer jump to the greybox Borrowed Dawn.
