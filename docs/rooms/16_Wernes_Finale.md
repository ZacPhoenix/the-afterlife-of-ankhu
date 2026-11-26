# 16 Wernes_Finale: The Barque of Ra

## 1. Summary

| Field | Value |
|---|---|
| Scene | `Wernes_Finale` (D:\TheAfterlifeofAnkhu\Assets\Ankhu\Realms\Generated\Scenes\Wernes_Finale.unity) |
| Realm | Wernes |
| Title | The Barque of Ra |
| Play order | 16 |
| Previous | 15 Wernes_Hub with all four rooms done (`solved_all`) |
| Next | 17 Rosetau_A_Laugh |
| Script | D:\TheAfterlifeofAnkhu\Assets\Ankhu\Realms\Core\Content\WernesPaintedScriptsFinale.cs, `BuildFinale()` (lines 215 to 258) |
| Builder | D:\TheAfterlifeofAnkhu\Assets\Ankhu\Realms\Editor\WernesPaintedBuilderFinale.cs (cue hooks lines 216 to 218) |

## 2. Intent and story beat

Not a puzzle: a frozen painted cinematic that closes the first realm. From the terrace parapet at dusk Ankhu and Anpu watch Ra's night barque glide down the valley river, the sun disc under the coil of Mehen (the first hour of the Amduat), while the shadow of Apep slides under the water the other way and opens its eye. Anpu: "It waits under every hour of the night for the boat to fail. It never has. Not yet." and "The light is going down to Rosetau. So are we." It plants the threat of Apep (the later rage-and-surrender descent in the story vision) and hands the player on to Rosetau.

## 3. Layout and zones

No zones, no usables, no puzzle objects. Ankhu at x -0.75 and Anpu at x 0.85 stand at the parapet facing the river (yaw 0). The barque, the Apep shadow and the eye are hidden objects that cue hooks switch on.

## 4. Elements

| Id | Name | Initial | Notes |
|---|---|---|---|
| `ankhu` | Ankhu | terrace | marker only |

The room has no Verbs, no rules, no Goal and no Tick. Cue hooks: `barque_rise` activates the barque, `apep_pass` activates the Apep shadow, `apep_eye` activates the eye.

## 5. Words

None. Ankhu is frozen from the first step to the scene change, so the speech bubble never opens (`presenter.InputAllowed` is false while frozen). If text did reach the rules, every verb would be UnknownVerb ("You know this word exists, but your mouth cannot shape it yet.") and names InvalidTarget.

## 6. Canonical solution

Watch. Lines can be advanced early with Space, E or the gamepad A button (after 0.5 s each).

| # | Step |
|---|---|
| 1 | Freeze, Wait 1.5 |
| 2 | Caption "The terrace again. The fields have gone dark. Far down the river, the horizon is burning." |
| 3 | Ankhu "The sun went down hours ago." Anpu "It went down. Now it comes through here." |
| 4 | Cue `barque_rise`, Wait 4 |
| 5 | Title "THE BARQUE OF RA" 5 s, caption about the boat and Mehen |
| 6 | Ankhu "Nobody is rowing it." Anpu on the twelve hours |
| 7 | Wait 2, Cue `apep_pass`, Wait 3, caption "Under the water something longer than the boat is moving the other way." |
| 8 | Ankhu "Anpu. In the river." Anpu "Do not look at it." |
| 9 | Cue `apep_eye`, Wait 2.5, Ankhu "It opened its eye.", Anpu on Apep |
| 10 | Wait 2, Ankhu "And if it does?", Anpu "Then no one wakes... So are we." |
| 11 | Wait 2, caption "The barque passes into the hills...", Wait 2, Next |

## 7. Alternate valid orders

None. Skipping lines shortens the waits between lines but not the fixed Wait and Title steps (about 26 s of fixed time).

## 8. Failure, refusal and reset behaviour

None. No idle beats. The 75 s hint and the H key run only while not frozen, so they never show.

## 9. Beats, memory films and codex

| Beat | Trigger | Steps |
|---|---|---|
| `barque` | Start | the whole sequence above, ending in Next |

No memory film, no codex write.

## 10. SFX and audio

| Sound | State |
|---|---|
| Ambience walk_ambience.wav | assigned (the same walk loop, no river or night bed) |
| Barque rising and gliding, the light, Apep passing under the water, the eye opening | **no SFX and no music**. The cue hooks only activate objects |
| Dialogue voice | none recorded for `wernes_finale` keys |

## 11. Exit conditions

The Start beat's last step is Next. `WernesProgress.MarkDone("Wernes_Finale")` is a no-op. `ExitOf("Wernes_Finale")` = next built chain scene = `Rosetau_A_Laugh`.

## 12. Acceptance criteria

- AC-1 [auto] On load the `barque` beat fires and Ankhu is frozen for its whole length.
- AC-2 [auto] The room never reports solved (no goal).
- AC-3 [auto] The scene exits to `Rosetau_A_Laugh` without player input.
- AC-4 [manual] The barque appears on `barque_rise`, the Apep shadow on `apep_pass`, the eye on `apep_eye`, each readable at gameplay scale.
- AC-5 [manual] The sequence has music and SFX for the barque, Apep and the eye (currently fails, F1).
- AC-6 [manual] Total length is under about 90 s, and Space advances lines.

## 13. Code vs intent disagreements

| # | Severity | Where | Problem | Proposed fix |
|---|---|---|---|---|
| F1 | major | WernesPaintedBuilderFinale.cs lines 216 to 218, Wernes_Finale.unity | The realm finale has no music and no SFX: the cues only activate objects, and the scene plays the generic walk ambience | Add cue SFX (barque swell, water displacement, eye opening) and a music cue, played from the cue hooks |
| F2 | minor | WernesPaintedScriptsFinale.cs line 229 | No `Step.Free()` and no input at all. That is by design (frozen cinematic), but it means a player cannot open the codex or skip the whole scene | Accept, or add a hold-to-skip |
| F3 | minor | PlayOrder.cs line 37 comment and PLAY_ORDER.md row 15 | The hub's all-done exit is routed through `NextBuiltAfter(IndexOf("Wernes_D_Bricks"))`. If `Wernes_Finale` is missing from Build Settings the hub silently skips to Rosetau. PlayOrderCheck covers this only with all rooms simulated | Keep the finale in the play order check with `progress A B C D` |
| F4 | minor | RealmSceneDirector.cs line 286 | `room.Solved` is false forever (empty goal), which keeps the idle and hint code armed. Harmless while frozen | none |

## 14. E2E script

```e2e canonical
scene Wernes_Finale
beat barque [AC-1]
unsolved [AC-2]
exit Rosetau_A_Laugh [AC-3]
```

```e2e alt-1
scene Wernes_Finale
nobeat solved [AC-2]
beat barque
unsolved [AC-2]
exit Rosetau_A_Laugh [AC-3]
```
