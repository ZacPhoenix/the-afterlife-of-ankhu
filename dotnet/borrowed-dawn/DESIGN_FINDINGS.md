# Borrowed Dawn: puzzle design findings

Source: exhaustive breadth-first search over the ported rules (`tools/Analyze`, full output in `ANALYSIS.md` for the prototype rules and `ANALYSIS_GREYBOX.md` for the Greybox profile). Every number below was produced by running that tool. Undo and reset are excluded from the search.

## Headline numbers

| Measure | Value | Reading |
|---|---|---|
| Reachable states | 2,145 | Small enough to verify exhaustively on every rules change |
| Softlocks | 0 | No sequence of accepted commands makes the gate unreachable |
| Dead ends | 0 | Every non-won state accepts at least one command |
| Shortest solution | 23 | Two fewer than the 25-step help solution |
| Distinct shortest solutions | 33,306 | High ordering freedom |
| Worst-case recovery | 25 | From the messiest reachable state, the room is never more than 25 commands from done |
| Elevator binding bypassable | No | `take mirror` needs water west AND elevator high, which only a binding provides |

## Greybox profile (chosen by Zac on 20 November 2026)

The Unity greybox uses `RuleProfile.Greybox`: ladder hatch between gallery and floor, mirror taken from the raised lift, open gallery to landing walkway, a physical bridge zone, and the load-bearing rule from finding 1 (the elevator binding cannot be released while water is west).

| Measure | Prototype | Greybox |
|---|---|---|
| Reachable states | 2,145 | 4,514 |
| Softlocks | 0 | 0 |
| Dead ends | 0 | 0 |
| Shortest solution | 23 | 23 |
| Authored solution | 25 | 24 |
| Distinct shortest solutions | 33,306 | 107,254 |
| Worst-case recovery | 25 | 24 |
| Elevator binding bypassable | No | No |
| Elevator released with water west | Allowed | Refused |

What the load-bearing rule fixed: every solution now has to send water east to carry the elevator before letting go of the binding, which is the insight the prototype's help text describes. What it did not fix: the two-binding cap still only bites a player who keeps the elevator bound while also binding the shadow. A solution exists that never holds two bindings except shadow plus bridge. If the cap should be a forced lesson, it needs a third demand on bindings mid-puzzle, for example the bell options in finding 2. Note that echo cannot simply hold a binding until the end, because shadow plus bridge already fill both slots at the gate. Re-run the solver on any such change.

## Findings on the prototype rules, ranked by design risk

### 1. The binding cap barely bites (high)

The intended insight is "two bindings are not enough, so let water physically support the elevator, then release it." The solver shows that insight is unnecessary. The elevator can be released the moment the mirror is taken, with water still west, because two things make dropping the elevator harmless:

- `release` works from anywhere and has no load check.
- The gallery-to-floor descent is always open, so a falling elevator never strands Ankhu upstairs.

A full solution exists that never holds two bindings at once except the final shadow plus bridge pair. So the cap of 2 only punishes a player who forgets to release, and the fix (release) is free.

**Recommendation.** Add one rule: *a binding cannot be released while it bears load* (elevator bound AND water west). The solver confirms the room stays solvable under that rule and its shortest solution becomes exactly 25, which matches the authored help solution. That single rule turns the "flow east to support, then release" step from optional into the puzzle's real second insight. Alternative: make the gallery descent depend on the elevator, which forces the same lesson through traversal rather than a refusal message.

### 2. The bell is a linear three-step chore (medium)

`recall bell`, `strike bell`, `echo bell` always occur back to back at the landing, interact with no other system, and cost no binding. Like the Name, it can never be lost once gained, while sun and shadow are fragile and must be balanced. It adds three typed steps and zero decisions.

**Recommendation.** Give the echo a cost or a conflict. Options: echo occupies a binding slot (which makes the cap matter even more alongside finding 1), echo decays when water flows (sound carried by water), or the bell only rings true while the shadow is absent. Re-run `tools/Analyze` after any change to confirm solvability and length.

### 3. Most spells are location-independent (medium, port risk)

`flow`, `bind shadow`, `release` (any target), `wake`/`sleep bearer` and `echo bell` are accepted from every location in the prototype. In a point-and-click room that reads fine. In a walking game it reads as remote control and weakens spatial reasoning.

The solver tested four progressively stricter proximity profiles. All remain solvable at length 23:

| Profile | Restriction added | Solvable | Shortest |
|---|---|---|---|
| A | wake, sleep, bind shadow only from the floor | Yes | 23 |
| B | A plus echo only from landing or gate | Yes | 23 |
| C | B plus flow only from the floor or while riding the lift | Yes | 23 |
| D | C plus release elevator only from an upper location | Yes | 23 |

**Recommendation.** Adopt profile C or D in the walking port. It costs zero solution length and makes position meaningful. Keep `flow` available while riding the lift, as the handoff notes already require.

### 4. Discovery of the Name is a single read (low)

The rules correctly refuse the Name until `read inscription` succeeds, so guessing the string does nothing. But discovery is one command in a room Ankhu must pass through anyway. The relief clue ("a body departing while its shadow remains") is stronger than the Name gate itself.

**Recommendation.** Fine for an introductory room. In later realms, require assembling Name fragments from more than one place, which the codex can record.

### 5. High ordering freedom means hints must be state-driven (low)

33,306 shortest solutions exist, mostly from interleaving the inscription, the bell trio, the bridge binding and the shadow binding. That is good for player agency. A linear hint script would misfire. The prototype's `hint()` is already state-driven. Port it as a function of `RoomState`, and let the solver validate it: for every reachable state, the hinted command should be accepted and should reduce `DistanceToGoal`.

### 6. Message and rule disagree on binding the elevator (fixed)

The prototype's refusal said "reach the upper gallery", yet the rule also accepts the binding while riding the raised lift, and the shortest solutions use that route. The port keeps the rule and corrects the message.

## Confirmed working as intended

- Every gate prerequisite is independently enforced (see tests).
- Waking the Bearer before binding the shadow loses the shadow, `bind shadow` is refused, and `sleep bearer` restores it. The room stays solvable.
- Releasing the bridge while standing at the gate is refused, so Ankhu cannot strand himself.
- The won state is unique: water east, gate, shadow plus bridge bound, Bearer awake, mirror installed, Name read, bell repaired, struck and echoing.
