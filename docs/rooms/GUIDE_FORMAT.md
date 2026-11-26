# Room guide format

One Markdown file per room, named `NN_<SceneName>.md` (NN = play order number from PLAY_ORDER.md).
Sections, in this order:

1. **Summary**: scene name, realm, title, play-order number, previous and next scene, script source (file and class/method).
2. **Intent and story beat**: what the puzzle teaches or means, which memory or realization it unlocks.
3. **Layout and zones**: every zone id, what stands there, which zones reach which objects.
4. **Elements**: a table per room object: id, name, aliases, initial state, all states, which words move it (verb, from, to, requires, also), success text, refusal text. Plus usables (id, zone, prompt, deed, when).
5. **Words**: verbs known on entry, verbs learned in-room (and by what beat), names accepted. For each verb: what it does here. Message the player sees for: a word not yet learned ("You know this word exists, but your mouth cannot shape it yet."), a name or rule not yet available (the rule's Refusal), and a word already spent ("This time, nothing has changed."). Say which judgement the engine returns in each case.
6. **Canonical solution**: numbered steps (zone, typed text or E-use, expected result, state after, beats that fire).
7. **Alternate valid orders**: every other order the rules allow, and orders that fail.
8. **Failure, refusal and reset behaviour**: what refusals say, failure beats, idle hints, H hint tiers, undo or reset.
9. **Beats, memory films and codex**: every beat id, trigger, flags required/forbidden, steps (lines, films `flashback:<id>`, codex writes, learns, set states, next scene).
10. **SFX and audio**: ambience, cast/land/fail SFX, per-interaction SFX the director or views play, voice keys. Note any interaction with no authored SFX (Zac rule: every interaction and animation needs authored SFX).
11. **Exit conditions**: what must hold to leave, which step leaves, and where it goes.
12. **Acceptance criteria**: numbered `AC-1`, `AC-2`, ... Each is one observable, testable statement. Mark each as `[auto]` (checked by the E2E block) or `[manual]` (needs eyes or ears, e.g. animation reads, SFX audible).
13. **Code vs intent disagreements**: every place the code, the scene builder, the design doc and PLAY_ORDER.md disagree, or a likely bug. Each with file:line, severity (blocker / major / minor), and a proposed fix.
14. **E2E script**: one or more fenced blocks, parsed by the test harness.

## E2E block grammar (RealmSceneDirector rooms)

Fence: three backticks then `e2e <name>` (name: `canonical`, `alt-1`, `fail-1`...). One command per line. `#` starts a comment. Trailing `[AC-n]` tags link a line to acceptance criteria.

```
scene <SceneName>                 first line. Loads the scene's script (sim) or the scene (play mode)
progress <A|B|C|D> ...            Wernes rooms already done before entry (PlayerPrefs WernesProgress)
codex <entryId> ...               codex entries held on entry, beyond the script's CodexOnEntry
at <zoneId>                       Ankhu walks into the zone (EnterZone beats fire, first entry only)
say <utterance> => <Judgement>    type and submit. Judgement: Success NoChange NotUnderstood UnknownVerb Incomplete InvalidTarget OutOfReach Refused Ambiguous
say <utterance> => <J> | <text>   also assert the message contains <text>
use <usableId>                    press E on the usable (asserts its prompt is offered in the current zone)
wait                              one tick (rooms with a world clock)
expect <objectId>=<state>         assert room state
beat <beatId>                     assert the beat has fired
nobeat <beatId>                   assert the beat has not fired
flag <name>                       assert the flag is set
verb <verb>                       assert the verb is known
film <id>                         assert flashback <id> has been played
codexhas <entryId>                assert the codex holds the entry
solved | unsolved                 assert the room goal
exit <SceneName|title>            assert the room left (NextScene or EndSlice ran) and where to
```

Teaching rooms and Borrowed Dawn run on the Borrowed Dawn package. Their block uses `engine bd` as the second line and the commands `zone <ZoneId>`, `say <text> => <CommandOutcome>`, `expect <field>=<value>` (fields from RoomState), `solved`, `exit <SceneName>`.
