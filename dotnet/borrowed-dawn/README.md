# Ankhu.BorrowedDawn: headless rules engine (Milestone A)

Pure C# implementation of the Borrowed Dawn room from `prototype/borrowed-dawn.html`. No Unity dependency. Targets `netstandard2.1` with C# 9, which Unity 2021.2 and later compile directly.

## Layout

| Path (repo root) | Contents |
|---|---|
| `Packages/com.ankhu.borrowed-dawn/Runtime/` | The library, as an embedded Unity package. The asmdef `Ankhu.BorrowedDawn` sets `noEngineReferences: true`. Unity code references that asmdef. |
| `dotnet/borrowed-dawn/src/` | SDK-style project that compiles the package's Runtime sources outside Unity, so `bin/` and `obj/` never land in the Unity tree. |
| `dotnet/borrowed-dawn/tests/` | NUnit 3 tests (net8.0). |
| `dotnet/borrowed-dawn/tools/Analyze/` | Breadth-first search over every reachable state. Writes `ANALYSIS.md` and `ANALYSIS_GREYBOX.md`. |
| `dotnet/borrowed-dawn/tools/DiffFuzz/` | Differential harness that runs the original prototype JavaScript logic under Node and compares it with the C# rules (Prototype profile). |
| `dotnet/borrowed-dawn/ANALYSIS*.md` | Generated solver reports, one per rules profile. |
| `dotnet/borrowed-dawn/DESIGN_FINDINGS.md` | Puzzle design risks and recommendations drawn from the analysis. |

## Build and run

Requires the .NET 8 SDK, and Node 18 or later for the differential harness. From `dotnet/borrowed-dawn/`:

```bash
dotnet test BorrowedDawn.sln -c Release
dotnet run -c Release --project tools/Analyze -- ANALYSIS.md
dotnet run -c Release --project tools/Analyze -- --profile greybox ANALYSIS_GREYBOX.md
tools/DiffFuzz/run.sh            # optional: PROTOTYPE_HTML=path/to/borrowed-dawn.html also verifies the copied JS lines
```

## Public API

```csharp
var session = new PuzzleSession(RuleProfile.Greybox); // starts at RoomState.Initial
session.StateChanged += (before, after) => { };  // fires on accept, undo, reset, load
session.CommandResolved += result => { };        // fires on every command attempt

SubmitResult r = session.Submit("Bind the elevator!");  // parse + execute
r.Accepted; r.Message; r.Parse.Status; r.Execution?.Outcome;

session.Execute(Command.Flow(WaterSide.West));    // bypass the parser (buttons, walking triggers)
session.Undo();  session.Reset();  session.Load(RoomState.Unpack(saved));
int saved = session.State.Pack();                 // 15-bit save key, unique per state
```

| Type | Role |
|---|---|
| `RoomState` | Immutable logical state plus derived conditions: `ElevatorHigh`, `ShadowPresent`, `SunAtGate`, `FarBeamBlocked`, `RaftRaised`, `BridgeHeld`, `AllGateConditions`. Presentation reads these and nothing else. |
| `Command` | Parser-independent intent built from static factories. `ToString()` gives a canonical spoken form that parses back to an equal command. |
| `RoomRules.Apply(state, command)` | Pure transition. Returns `CommandResult` with a stable `Outcome` code, a message, and the next state. A rejected command returns the input state untouched. |
| `SpellParser.Parse(text)` | Deterministic, state-free parser with explicit alias tables. Returns `ParseStatus` plus player feedback that never leaks the gate Name. |
| `PuzzleSession` | Current state, bounded undo (100 by default, matching the prototype), reset, load, events. |
| `Analysis.StateSpace` | Exhaustive reachable-state graph, softlock detection, shortest solutions, constrained solves for design questions. |

## Rules profiles

| Profile | Use |
|---|---|
| `RuleProfile.Prototype` | Faithful port of `borrowed-dawn.html`. Default for `RoomRules.Apply(state, command)` and `new PuzzleSession()`. Verified against the original JavaScript by `tools/DiffFuzz`. |
| `RuleProfile.Greybox` | Chosen by Zac for the Unity slice. Differences listed below. Pass it explicitly. |

Greybox differences from Prototype:

| Rule | Greybox |
|---|---|
| Floor and gallery | Connected only by the ladder hatch. `lower ladder` from the gallery, after which it is two-way and stays down. No free descent. |
| Lift and gallery | Connected both ways while the elevator is high. |
| Taking the mirror | Only from the raised lift (`Location.Lift`) with water west. |
| Gallery to landing | Always open. The walkway no longer needs the elevator high. |
| Bridge | `Location.Bridge` is a zone between landing and gate. Entering or leaving it needs the bridge bound. `release bridge` is refused on the bridge or at the gate. |
| Elevator binding | Load-bearing: `release elevator` is refused while water is west (`Outcome.BindingBearsLoad`). |
| Unread Name | Refused with the same text as nonsense, so the reply does not confirm a guess. The `Outcome` is still `NameUnknown` for logging. |

## Rules (ported from the prototype)

Initial state: water east, Ankhu on the floor, no bindings, mirror on the raft, Bearer asleep, Name unread, bell cracked and silent.

| Derived condition | Definition |
|---|---|
| Elevator high | water east OR elevator bound |
| Shadow present | mirror in socket AND (Bearer asleep OR shadow bound) |
| Sun at gate | mirror in socket AND Bearer awake AND water east |
| Gate opens | at gate AND Name discovered AND sun AND shadow AND echo |

At most two bindings are active. Echo uses no binding.

### Deliberate differences from the prototype

1. **Explicit adjacency.** The prototype's `move()` trusts its buttons to offer only neighbours. `RoomRules` rejects non-adjacent moves (`NotAdjacent`) and moves to the current location (`AlreadyInState`).
2. **Second bell strike rejected.** The prototype accepts `strike bell` repeatedly as a no-op that still consumes an undo slot. Here it returns `AlreadyInState` so every accepted command changes state. Presentation can still play the bell sound on that outcome.
3. **Message wording for binding the elevator.** The prototype says to reach the gallery, but it also accepts the binding while riding the raised lift. The message now says so. The rule itself is unchanged.

### Parser normalization

Unicode NFKC, invariant lower case, every non letter or digit becomes a space, whitespace collapses, and the articles `the`, `a`, `an` are dropped. Matching is exact on tokens. There is no fuzzy matching.

| Canonical | Accepted spellings |
|---|---|
| `flow west/east` | direction `w`, `e` |
| `bind X`, `release X` | `unbind`. Targets: `elevator`/`lift`, `shadow`, `bridge`/`pendulum` |
| `wake bearer`, `sleep bearer` | `awaken`. Object `statue` |
| `recall/strike/echo bell` | `ring bell`. Object `resonator` |
| `take mirror` | `carry`, `grab`, `lift`, `pick up`, `collect`. Object `sun mirror` |
| `install mirror` | `place`, `put`, `set`, `insert`, `mount`. Optional `in`/`into`/`on`/`onto` `[sun] socket` after the mirror |
| `read inscription` | objects `inscriptions`, `gallery` |
| `go to X` | `go X`, `walk to X`, `move to X`. Places: `floor`/`ground`, `elevator`/`lift`, `gallery`/`balcony`, `landing`/`bell landing`, `gate`/`threshold` |
| `board elevator`, `enter gallery`, `descend [to floor]`, `cross bridge` | movement shortcuts |
| Gate Name | `he who returns the morning`, optionally prefixed by `speak` or `say`. Accepted by the parser always, but the rules reject it until the inscription has been read. |

Failure statuses: `Empty`, `UnknownVerb`, `MissingArgument`, `InvalidArgument`, `TrailingWords`. A bare verb (`carry`) gives `MissingArgument` with feedback such as "Carry what? It can act on: mirror." An unknown word gets neutral feedback, plus "Did you mean ...?" naming one verb when the word is one edit away (two for words over four letters, adjacent swaps count as one). Feedback never lists targets or steps for unknown words.

## Unity integration notes

- Keep one `PuzzleSession` per room in a MonoBehaviour adapter. Subscribe presentation components to `StateChanged` and derive every visual (water level, lift height, beam, shadow, bridge angle, bell) from `RoomState`, so undo, reset and load restore visuals for free.
- Walking replaces `MoveTo`: fire `Command.MoveTo` when Ankhu enters a location trigger, and gate the physical path on the same rule result so the collider and the logic cannot disagree.
- The prototype applies state instantly and animates for about 1.2 seconds. Decide input locking in the adapter, not here.
- Proximity rules for spells belong in the adapter as a filter before `Execute`. `ANALYSIS.md` shows which proximity profiles keep the room solvable.
