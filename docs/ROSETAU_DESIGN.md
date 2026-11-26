# Rosetau, design notes

Built 2026-11-25 overnight. Rosetau is the realm after Wernes. Wernes was about Ankhu's wife and son. Rosetau is
about his mother. The canon: his father was a soldier whose death left the family poor. Ankhu trained as a
priest to support his mother. She later lived with him and Neferet and died old. The gods here help Ankhu, they
do not judge him.

## What Ankhu learns

As a boy he hushed his mother's grief, every night, with his hand over her mouth, because the neighbours would
hear. He thought he was helping. Rosetau gives grief its voice back, one room at a time. First a god's laugh he
can only hold by answering it. Then two goddesses whose mourning is bound with his own knot. Last his mother,
weeping alone, who only moves again when someone answers her. Then his father, who never came home: a soldier's
shade with no face on the Two Ways, and a shield on a doorpost that was all the army sent back. The realm ends on
the day the shield came home, with Ankhu doing the one thing the boy could not, keening out loud in the street,
and then giving his father the rite he gave strangers for a living.

The verbs are the ones the player already owns from Wernes (`echo`/wesheb, `release`/weha, `wake`/res,
`remember`/sekha, `open`/wen, `give`/rdi, `still`/hetep, `name`/ren, `bind`/tjes). Rosetau teaches no new spell. It turns `echo` from a trick
into the point: to answer someone. `still` is refused on every mourner, and each refusal is the hint. Room D is the one place `still` is welcome, on
the fire, which buys about two utterances of quiet. That turns the hush into a tool you use on the right thing.

## Rooms in play order

| Scene | Room | Puzzle | Solve gives |
|---|---|---|---|
| Rosetau_A_Laugh | The passage under Abdju. A stair down into a basalt hall with three low doorways | A child's laugh bounces off all three doorways and fades. `echo` holds it, and while held it answers from one doorway only, which lights. `open` the lit doorway while standing at it. Three rounds, middle then left then right. A dark doorway is only stone. Then the young Heru stands in the last doorway, finger at his lips. `give falcon` (Hori's clay falcon, carried from Wernes) | Painted memory pm_news, codex memory.news |
| Rosetau_B_Mourners | Ausar on the lion bier. Auset kneels at his head, Nebet-Het at his feet, mouths bound with linen in Ankhu's knot | `still` on a mourner is refused and plays pm_hush. `release` each band at her side and she keens. `echo` answers the two of them, and while both keen and are answered one lamp lights each tick. A lone keener runs out of breath after two ticks. Four lamps open the sealed door | pm_hush and pm_leaving, codex memory.hush and memory.leaving |
| Rosetau_C_Mother | The one-room house at Abdju. She weeps at her loom | `still` is refused. Loom, quern and lamp will not move while she weeps alone. `echo` her and she lifts her head. `wake` the loom and the quern, and once both work the lamp catches by itself. `remember` her with the lamp lit | pm_old_mother, codex memory.old_mother |
| Rosetau_D_TwoWays | The Book of Two Ways: a black road and a blue road with a channel of fire between. Three faceless soldier shades keep a watch on the far bank. A garrison roster hangs by the stair | The fire drowns every voice. `still` the fire and it stays low for about two utterances. While it is low, `echo` a shade and he calls back his town. Then `name` him at his post, matching what he carries (shield, empty strap, bow) to the roster: Paser of Iunu, Nakht of Abdju, Huy of Abdju. A wrong name turns no one. The middle man with the empty strap is Nakht, Ankhu's father, whose shield was carried home | pm_father, codex memory.father, lore.two_ways |
| Rosetau_E_Finale | The doorway of the house at Abdju on the day the shield came home. The young mother keens in the street. Nakht's shade waits by the door | `bind` the father is refused, there is no body. `bind` the shield with the two linen bands from Room B, and the shade goes into it. Anpu comes off the stair for the first time and holds the wrapped form upright (Hunefer's Opening of the Mouth vignette). `still` on the mother is refused for the last time. `echo` her. Then `open` the mouth, which needs the form held and the lament answered. `give` bread. The blank stela by the door is cut | pm_whistle, codex memory.whistle, then END OF ROSETAU and the title (end of built content) |

The Wernes hub's door, once all four Wernes rooms are done, now opens onto Rosetau A instead of the title.

## Words and glyphs

Every new noun has a gloss in `Assets/Ankhu/Realms/Core/Lexicon.cs` with a source tag ([F] Faulkner, Concise
Dictionary of Middle Egyptian; [PH] project house rule; Wiktionary where marked), repeated in
`RosetauScripts.cs`: laugh sbt, fire, soldier, shield, father, mouth, bread and the three names Paser, Nakht and Huy, child hrd, falcon bjk, linen mnht, lament j3kb, lamp hbs, loom sht (weave,
marked verify), quern nd, mother mwt, Auset 3st, Nebet-Het nbt-hwt, door ꜥ3. Names are authentic Egyptian
forms (Auset, Nebet-Het, Ausar, Heru, Anpu, Abdju, Rosetau).

## Art

Painted 2.5D per `docs/PAINTED_ROOM_SPEC.md`. Scenes are built by `Assets/Ankhu/Realms/Editor/RosetauBuilder.cs`
(Ankhu > Realms > Build Rosetau, or `-executeMethod Ankhu.Realms.EditorTools.RealmSceneBuilder.BuildRosetauCli`).
Greybox plates for paint-over: `art_gen/rosetau/plates`. Backdrops: `Assets/Ankhu/Realms/Art/Rosetau/<scene>_<cam>.png`.
Cards: `Assets/Ankhu/Realms/Art/Rosetau/Sprites`. Memory stills are in the living papyrus vignette style in
`Assets/Ankhu/Realms/Art/Memories/Resources/RealmMemories/pm_*.png`.

## Voice

Ankhu uses contractions, Anpu does not. No maxims. Captions say only what the camera sees. The mother's one
line is ordinary and funny, because she was.

## Sources

- Rosetau as the necropolis passage and Book of the Dead spell 117 (the way of Rosetau): Faulkner, The Ancient
  Egyptian Book of the Dead (1985).
- Auset and Nebet-Het as the two kites mourning Ausar at the bier: the Lamentations of Isis and Nephthys,
  Papyrus Bremner-Rhind (Faulkner 1936).
- The child Heru with finger at lips (Harpocrates): standard iconography, Late Period bronzes and faience.
- Household loom (horizontal and upright), saddle quern and lamp: Szpakowska, Daily Life in Ancient Egypt (2008).
- The Book of Two Ways, water road and land road with fire between: Lesko, The Ancient Egyptian Book of Two Ways
  (1972), Coffin Texts spells 1029 to 1130. The floor text in Room D is Coffin Texts spell 1130, after Lichtheim,
  Ancient Egyptian Literature I (1973), p. 131.
- Anpu holding the mummy upright while the women mourn, the Opening of the Mouth: Papyrus of Hunefer, British
  Museum EA 9901/5.
- The offering formula on the stela in Room E: Gardiner, Egyptian Grammar, 3rd ed. (1957), p. 170.
- Cenotaph stelae at Abdju for people buried elsewhere: the Abydos North Offering Chapels, Simpson, The Terrace of
  the Great God at Abydos (1974).
- The soldiers' names Paser, Nakht and Huy: Ranke, Die altägyptischen Personennamen (1935).

## What needs a person

- A 3D Anpu pose holding the wrapped shield upright in Room E. For now Anpu walks to the mark and stands behind
  the wrapped shield card.
- Voice takes for every new Rosetau line (rosetau_a to rosetau_e).
