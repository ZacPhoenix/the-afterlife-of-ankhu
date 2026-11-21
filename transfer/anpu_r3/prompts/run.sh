#!/bin/bash
# usage: run.sh <n> <outname> <size> <ref>...
P=D:/TheAfterlifeofAnkhu/art_gen/anpu/out3/prompts
n=$1; name=$2; size=$3; shift 3
refs=(); for r in "$@"; do refs+=(--ref "$r"); done
OUT=D:/TheAfterlifeofAnkhu/art_gen/anpu/out3/$name
for a in 1 2; do
  t0=$(date +%s)
  if ~/.claude/skills/gpt-image-bridge/bin/gpt-image-2 "$(cat $P/p$n.txt)" "$OUT" --size $size "${refs[@]}" > "$P/log$n.$a.txt" 2>&1; then
    echo "$name attempt=$a ok $(( $(date +%s)-t0 ))s" | tee -a "$P/results.txt"; exit 0
  fi
  echo "$name attempt=$a FAILED $(( $(date +%s)-t0 ))s" | tee -a "$P/results.txt"
done
exit 1
