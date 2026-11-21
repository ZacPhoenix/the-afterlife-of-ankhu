#!/usr/bin/env bash
# Differential fuzz: prototype JS logic (node) vs Ankhu.BorrowedDawn (C#).
# Usage: tools/DiffFuzz/run.sh [actions_per_shard=250000] [shards=4] [base_seed=1]
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
NODE="${NODE:-node}"
PER="${1:-250000}"; SHARDS="${2:-4}"; SEED="${3:-1}"
OUT="${DIFFFUZZ_OUT:-$(mktemp -d)}"
mkdir -p "$OUT"
echo "data dir: $OUT"
if [[ -n "${PROTOTYPE_HTML:-}" ]]; then "$NODE" "$HERE/js_reference.js" --verify-source "$PROTOTYPE_HTML"; fi
"$NODE" "$HERE/js_reference.js" bfs "$OUT/bfs.jsonl"
pids=(); fuzzargs=()
for ((i=0;i<SHARDS;i++)); do
  "$NODE" "$HERE/js_reference.js" fuzz $((SEED+i)) "$PER" "$OUT/fuzz_$i.jsonl" "$OUT/bfs.jsonl" & pids+=($!)
  fuzzargs+=(--fuzz "$OUT/fuzz_$i.jsonl")
done
for p in "${pids[@]}"; do wait "$p"; done
dotnet build "$HERE/DiffFuzz.csproj" -c Release -nologo -v q
dotnet "$HERE/bin/Release/net8.0/DiffFuzz.dll" --bfs "$OUT/bfs.jsonl" "${fuzzargs[@]}"
