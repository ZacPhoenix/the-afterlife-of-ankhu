#!/usr/bin/env bash
# Compile-check the Ankhu Unity 6 C# code without a Unity editor.
#
#   tools/compile-check/compile-check.sh            # full check: asmdef mirrors + engine-free NUnit run
#   tools/compile-check/compile-check.sh --no-tests # compile only
#
# Builds one SDK-style project per asmdef (Ankhu.Integration, Ankhu.Runtime [Editor + Player defines],
# Ankhu.Editor, Ankhu.Tests.EditMode) against genuine Unity 6 player assemblies (see fetch-refs.sh), then
# compiles the engine-free files into Ankhu.Tests.Pure (.NET 8) and runs its NUnit tests with dotnet test.
# selftest/ is a canary that uses Unity 6-only APIs; an error there means the reference set is broken.
# Never writes under Assets/: all outputs go to tools/compile-check/.build.
# Exit status: 0 = no compile errors and no failing pure tests, 1 otherwise.
set -uo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ASSETS="$(cd "${ANKHU_ASSETS:-$HERE/../../Assets/Ankhu}" && pwd)"   # ANKHU_ASSETS overrides (for testing the tool)
LOGS="$HERE/.build/logs"
RUN_TESTS=1
[[ "${1:-}" == "--no-tests" ]] && RUN_TESTS=0
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 MSBUILDDISABLENODEREUSE=0
command -v dotnet >/dev/null || { echo "dotnet not found (apt-get install -y dotnet-sdk-8.0)" >&2; exit 2; }

cd "$HERE"
mkdir -p "$LOGS"; rm -f "$LOGS"/*.log
"$HERE/fetch-refs.sh" || { echo "could not fetch Unity reference assemblies" >&2; exit 2; }

# asmdef boundary sanity: these projects assume exactly these asmdefs.
known="Bindings/Ankhu.Bindings.asmdef Editor/Ankhu.Editor.asmdef Runtime/Ankhu.Runtime.asmdef Runtime/Integration/Ankhu.Integration.asmdef"
while IFS= read -r a; do
  rel="${a#$ASSETS/}"
  case " $known " in *" $rel "*) continue;; esac
  [[ "$rel" == Tests/EditMode/*.asmdef ]] && continue
  echo "WARNING: unmapped asmdef $rel - compile-check project boundaries may not match Unity's"
done < <(find "$ASSETS" -name '*.asmdef' | sort)
[[ -n "$(find "$ASSETS/Tests/EditMode" -name '*.asmdef' 2>/dev/null)" ]] || echo "note: no Tests/EditMode asmdef yet; compiling Tests/EditMode/**/*.cs anyway"

B="-nologo -v:q -clp:NoSummary;ErrorsOnly -m:4 -p:WarningLevel=4 -p:AnkhuRoot=$ASSETS/"
echo "restoring..."
dotnet restore CheckAll.proj -m:4 -v:q -nologo >"$LOGS/restore.txt" 2>&1 \
  && dotnet restore Ankhu.Tests.Pure/Ankhu.Tests.Pure.csproj -v:q -nologo >>"$LOGS/restore.txt" 2>&1 \
  || { cat "$LOGS/restore.txt"; echo "restore failed (nuget.org reachable?)" >&2; exit 2; }

echo "building Unity-editor and player variants in parallel..."
dotnet build CheckAll.proj --no-restore $B \
  "-flp1:logfile=$LOGS/editor.err.log;errorsonly" "-flp2:logfile=$LOGS/editor.warn.log;warningsonly" >/dev/null 2>&1 &
dotnet build Ankhu.Runtime/Ankhu.Runtime.csproj --no-restore $B -p:Variant=Player \
  "-flp1:logfile=$LOGS/player.err.log;errorsonly" "-flp2:logfile=$LOGS/player.warn.log;warningsonly" >/dev/null 2>&1 &
wait

echo "=== Unity asmdef compile (refs: Unity 6000.0 player DLLs + UnityEditor 2021.1.14f1) ==="
python3 -I "$HERE/summarize.py" "$LOGS/editor.err.log" "Player=$LOGS/player.err.log" \
  "$LOGS/editor.warn.log" "Player=$LOGS/player.warn.log"
status=$?

if (( RUN_TESTS )); then
  echo
  echo "=== engine-free NUnit tests (Ankhu.Tests.Pure, .NET 8) ==="
  excl="$LOGS/pure.excluded"; : >"$excl"
  ok=0
  for pass in 1 2 3 4 5 6 7 8; do
    ntests=$(python3 -I "$HERE/select_pure.py" "$ASSETS" "$HERE/Ankhu.Tests.Pure/PureFiles.g.props" "$excl")
    if dotnet build Ankhu.Tests.Pure/Ankhu.Tests.Pure.csproj --no-restore $B \
         "-flp1:logfile=$LOGS/pure.err.log;errorsonly" >/dev/null 2>&1; then ok=1; break; fi
    # Drop files that failed (they lean on Unity-bound code) and retry.
    bad=$(grep -oE '^/[^(]+\.cs' "$LOGS/pure.err.log" | sort -u | grep -v '/Runtime/Integration/' || true)
    [[ -z "$bad" ]] && break
    echo "$bad" >>"$excl"
  done
  if [[ -s "$excl" ]]; then
    echo "excluded (depend on Unity-bound code): $(sed "s|$ASSETS/||" "$excl" | sort -u | tr '\n' ' ')"
  fi
  if (( ! ok )); then
    echo "pure harness did not compile (Integration errors are reported above):"
    sed "s|$ASSETS/||" "$LOGS/pure.err.log" | sort -u | head -20
  elif [[ "$ntests" == "0" ]]; then
    echo "no engine-free test files with [Test] found under Tests/EditMode; nothing to run"
  else
    echo "$ntests engine-free test file(s) selected"
    dotnet test Ankhu.Tests.Pure/Ankhu.Tests.Pure.csproj --no-build -nologo -v:q \
      --logger "console;verbosity=normal" 2>&1 | grep -E '^\s*(Passed|Failed|Skipped|Total|Error Message|Stack Trace|  at |Failed!|Passed!|Expected|But was)' | head -80
    (( PIPESTATUS[0] == 0 )) || status=1
  fi
fi
exit $status
