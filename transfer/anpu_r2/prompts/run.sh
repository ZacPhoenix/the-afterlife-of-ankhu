#!/bin/bash
# usage: run.sh <n> <outname> <ref>...
S="C:/Users/zaclu/AppData/Local/Temp/claude/4402bc9105b314c0/1f1ab50e-2601-4452-b4cd-d9941afe5267/scratchpad"
n=$1; name=$2; shift 2
size=1536x1024; [ "$n" = 2 ] && size=1024x1536
refs=(); for r in "$@"; do refs+=(--ref "$r"); done
OUT=D:/TheAfterlifeofAnkhu/art_gen/anpu/out2/$name
for a in 1 2; do
  t0=$(date +%s)
  if ~/.claude/skills/gpt-image-bridge/bin/gpt-image-2 "$(cat $S/p$n.txt)" "$OUT" --size $size "${refs[@]}" > "$S/log$n.$a.txt" 2>&1; then
    echo "$name attempt=$a ok $(( $(date +%s)-t0 ))s" | tee -a "$S/results.txt"; exit 0
  fi
  echo "$name attempt=$a FAILED $(( $(date +%s)-t0 ))s" | tee -a "$S/results.txt"
done
exit 1
