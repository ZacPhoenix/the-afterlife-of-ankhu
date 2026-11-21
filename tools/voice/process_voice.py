#!/usr/bin/env python3
"""Ankhu spell voice pipeline: post-processing and measurement.

    python3 -I tools/voice/process_voice.py RAW_DIR [--assets Assets/Ankhu/Audio/Voice] [--previews DIR]

Needs numpy, scipy and pyloudnorm (pip install numpy scipy pyloudnorm). Reads the raw takes written by
ankhu_voice.py batch (WAV plus JSON with alignment) and writes, for each take:

1. High-pass at 70 Hz (rumble and DC), 2nd-order Butterworth, zero phase.
2. Trim to the speech: 10 ms RMS envelope, a frame is speech when it is within 40 dB of the loudest
   frame and above -60 dBFS. Keeps PRE_ROLL before the first speech frame and TAIL after the last,
   and never cuts before the end of the last aligned character.
3. Level: active speech level (ASL, the ITU-T P.56 idea on a K-weighted signal) set to TARGET_ASL.
   Integrated LUFS (BS.1770) is unreliable for clips under 400 ms, so ASL is what is matched, and
   integrated loudness is reported for the clips long enough to measure it.
4. True peak checked at 4x oversampling and pulled under CEILING_DBTP if needed.
5. Raised-cosine fades (FADE_IN, FADE_OUT), TPDF dither to 16-bit, 44.1 kHz mono WAV.

It also writes voice_manifest.json (the Unity library builder reads it) and, with --previews, renders
stitched chains with the same timing rules as SpellVoicePlanner.cs and measures the audible gaps.
"""
import argparse
import json
import os
import re
import sys
import wave

import numpy as np
import pyloudnorm
from pyloudnorm.iirfilter import IIRfilter
from scipy.signal import butter, resample_poly, sosfiltfilt

RATE = 44100
TARGET_ASL = -20.0      # dB, K-weighted active speech level
CEILING_DBTP = -1.0     # dBTP
PRE_ROLL = 0.025        # s kept before the first speech frame
TAIL = 0.110            # s kept after the last speech frame (natural decay of the final consonant)
FADE_IN = 0.004
FADE_OUT = 0.030
GATE_REL_DB = 40.0
GATE_ABS_DBFS = -60.0
ASL_REL_DB = 25.0

# Mirror of SpellVoicePlanner timing (Assets/Ankhu/Runtime/Voice/SpellVoiceTiming.cs). Keep in step.
CLAUSE_GAP = 0.12       # s of silence added between clause clips (each clip already ends in TAIL)
WORD_GAP = 0.0          # s between stitched word clips (negative values overlap)


# ----------------------------------------------------------------------------------------------- io

def read_wav(path):
    with wave.open(path, "rb") as w:
        if w.getsampwidth() != 2:
            raise ValueError(f"{path}: expected 16-bit PCM")
        rate = w.getframerate()
        ch = w.getnchannels()
        x = np.frombuffer(w.readframes(w.getnframes()), dtype="<i2").astype(np.float64) / 32768.0
    if ch > 1:
        x = x.reshape(-1, ch).mean(axis=1)
    if rate != RATE:
        x = resample_poly(x, RATE, rate)
    return x


def write_wav(path, x, rng):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    lsb = 1.0 / 32768.0
    dither = (rng.random(len(x)) - rng.random(len(x))) * lsb
    q = np.clip(np.round((x + dither) * 32767.0), -32768, 32767).astype("<i2")
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(q.tobytes())


# ----------------------------------------------------------------------------------------------- dsp

_HP = butter(2, 70.0, btype="highpass", fs=RATE, output="sos")


def k_weight(x):
    stage1 = IIRfilter(4.0, 1 / np.sqrt(2), 1500.0, RATE, "high_shelf")
    stage2 = IIRfilter(0.0, 0.5, 38.0, RATE, "high_pass")
    return stage2.apply_filter(stage1.apply_filter(x))


def frame_db(x, frame=0.010):
    n = int(RATE * frame)
    if len(x) < n:
        return np.array([20 * np.log10(np.sqrt(np.mean(x ** 2)) + 1e-12)]), n
    m = len(x) // n
    rms = np.sqrt(np.mean(x[: m * n].reshape(m, n) ** 2, axis=1))
    return 20 * np.log10(rms + 1e-12), n


def speech_frames(x):
    db, n = frame_db(x)
    thr = max(db.max() - GATE_REL_DB, GATE_ABS_DBFS)
    return db > thr, n


def active_speech_level(x):
    """K-weighted mean power over frames within ASL_REL_DB of the loudest frame, in dB (LUFS-like)."""
    k = k_weight(x)
    n = int(RATE * 0.010)
    m = max(1, len(k) // n)
    ms = np.mean(k[: m * n].reshape(m, n) ** 2, axis=1) if len(k) >= n else np.array([np.mean(k ** 2)])
    db = 10 * np.log10(ms + 1e-20)
    active = ms[db > db.max() - ASL_REL_DB]
    return -0.691 + 10 * np.log10(np.mean(active) + 1e-20)


def true_peak_db(x):
    up = resample_poly(x, 4, 1)
    return 20 * np.log10(np.max(np.abs(up)) + 1e-12)


def integrated_lufs(x):
    if len(x) < int(0.45 * RATE):
        return None
    v = pyloudnorm.Meter(RATE).integrated_loudness(x)
    return None if not np.isfinite(v) else float(v)


def fade(x):
    x = x.copy()
    fi = min(int(FADE_IN * RATE), len(x) // 2)
    fo = min(int(FADE_OUT * RATE), len(x) // 2)
    if fi:
        x[:fi] *= 0.5 - 0.5 * np.cos(np.linspace(0, np.pi, fi))
    if fo:
        x[-fo:] *= 0.5 + 0.5 * np.cos(np.linspace(0, np.pi, fo))
    return x


def last_aligned_end(meta):
    al = (meta or {}).get("alignment") or {}
    ends = al.get("character_end_times_seconds") or []
    chars = al.get("characters") or []
    for c, e in zip(reversed(chars), reversed(ends)):
        if c.strip() and c not in ".,;:!?":
            return float(e)
    return None


def process_take(x, meta):
    x = sosfiltfilt(_HP, x)
    active, n = speech_frames(x)
    idx = np.flatnonzero(active)
    if len(idx) == 0:
        raise ValueError("no speech found")
    start = max(0, idx[0] * n - int(PRE_ROLL * RATE))
    end = min(len(x), (idx[-1] + 1) * n + int(TAIL * RATE))
    aligned_end = last_aligned_end(meta)
    if aligned_end is not None:
        end = max(end, min(len(x), int((aligned_end + 0.04) * RATE)))
    y = x[start:end]
    asl_before = active_speech_level(y)
    y = y * 10 ** ((TARGET_ASL - asl_before) / 20)
    tp = true_peak_db(y)
    if tp > CEILING_DBTP:
        y = y * 10 ** ((CEILING_DBTP - tp) / 20)
    y = fade(y)
    return y, start / RATE


# ----------------------------------------------------------------------------------------------- planner mirror

def plan(clause_ids, available):
    """Same order of preference as SpellVoicePlanner.Plan. Returns [(key, gap_before_seconds)]."""
    count = len(clause_ids)
    cues = []
    for i, cid in enumerate(clause_ids):
        pos = "sole" if count == 1 else "first" if i == 0 else "final" if i == count - 1 else "medial"
        gap = 0.0 if i == 0 else CLAUSE_GAP
        for key in (f"clause.{cid}.{pos}", ):
            if key in available:
                cues.append((key, gap))
                break
        else:
            if f"clause.{cid}.sole" in available:
                if i > 0 and "word.and.lead" in available:
                    cues.append(("word.and.lead", gap))
                    gap = WORD_GAP
                cues.append((f"clause.{cid}.sole", gap))
            else:
                raise KeyError(f"no clip for clause {cid}")
    return cues


def render(cues, clips):
    """Returns (audio, start sample of every cue after the first)."""
    out = np.zeros(0)
    joins = []
    for i, (key, gap) in enumerate(cues):
        clip = clips[key]
        pad = int(round(gap * RATE))
        if i > 0:
            joins.append(len(out) + pad)
        if pad >= 0:
            out = np.concatenate([out, np.zeros(pad), clip])
        else:
            ov = min(-pad, len(out), len(clip))
            mixed = out.copy()
            mixed[len(out) - ov:] += clip[:ov]
            out = np.concatenate([mixed, clip[ov:]])
    return out, joins


def join_gaps(x, joins):
    """Silence (frames below the speech gate) around each join point, in seconds."""
    active, n = speech_frames(x)
    gaps = []
    for j in joins:
        f = min(len(active) - 1, j // n)
        lo = f
        while lo > 0 and not active[lo - 1]:
            lo -= 1
        hi = f
        while hi < len(active) and not active[hi]:
            hi += 1
        gaps.append((hi - lo) * n / RATE)
    return gaps


# ----------------------------------------------------------------------------------------------- main

STEM = re.compile(r"^(?P<key>[a-z0-9_.]+)#(?P<v>\d+)$")


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("raw")
    p.add_argument("--assets", default=os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "Ankhu", "Audio", "Voice"))
    p.add_argument("--previews")
    p.add_argument("--seed", type=int, default=1)
    a = p.parse_args()

    rng = np.random.default_rng(a.seed)
    clips_dir = os.path.join(a.assets, "Clips")
    entries, clips, report = [], {}, []
    for f in sorted(os.listdir(a.raw)):
        if not f.endswith(".wav"):
            continue
        m = STEM.match(f[:-4])
        if not m:
            continue
        meta_path = os.path.join(a.raw, f[:-4] + ".json")
        meta = json.load(open(meta_path, encoding="utf-8")) if os.path.exists(meta_path) else {}
        x = read_wav(os.path.join(a.raw, f))
        y, trim_start = process_take(x, meta)
        key, v = m.group("key"), int(m.group("v"))
        name = f"{key}__{v}.wav"
        write_wav(os.path.join(clips_dir, name), y, rng)
        stats = {
            "asl": round(active_speech_level(y), 2),
            "lufs": None if integrated_lufs(y) is None else round(integrated_lufs(y), 2),
            "truePeak": round(true_peak_db(y), 2),
            "duration": round(len(y) / RATE, 4),
            "rawDuration": round(len(x) / RATE, 4),
        }
        al = meta.get("alignment") or {}
        entry = {"key": key, "variant": v, "file": "Clips/" + name, "text": meta.get("text"), **stats,
                 "trimStart": round(trim_start, 4)}
        if al.get("characters"):
            entry["alignment"] = {
                "characters": "".join(al["characters"]),
                "start": [round(s - trim_start, 3) for s in al["character_start_times_seconds"]],
                "end": [round(e - trim_start, 3) for e in al["character_end_times_seconds"]],
            }
        entries.append(entry)
        if v == 0:
            clips[key] = y
        report.append((key, v, stats))

    manifest = {"version": 1, "sampleRate": RATE, "targetAsl": TARGET_ASL, "ceilingDbtp": CEILING_DBTP,
                "timing": {"clauseGap": CLAUSE_GAP, "wordGap": WORD_GAP}, "entries": entries}
    with open(os.path.join(a.assets, "voice_manifest.json"), "w", encoding="utf-8") as fh:
        json.dump(manifest, fh, indent=1)
        fh.write("\n")

    asl = np.array([s["asl"] for _, _, s in report])
    tps = np.array([s["truePeak"] for _, _, s in report])
    lufs = np.array([s["lufs"] for _, _, s in report if s["lufs"] is not None])
    summary = {
        "takes": len(report),
        "asl_min": float(asl.min()), "asl_max": float(asl.max()),
        "truePeak_max": float(tps.max()),
        "lufs_measurable": int(len(lufs)),
        "lufs_min": float(lufs.min()) if len(lufs) else None, "lufs_max": float(lufs.max()) if len(lufs) else None,
        "duration_total": round(sum(s["duration"] for _, _, s in report), 2),
    }

    if a.previews:
        os.makedirs(a.previews, exist_ok=True)
        chains = [["bind_elevator"], ["name_gate"], ["release_elevator", "flow_east"],
                  ["wake_bearer", "recall_bell", "echo_bell"], ["release_shadow", "bind_bridge"]]
        gap_report = {}
        for chain in chains:
            try:
                cues = plan(chain, clips)
            except KeyError as err:
                gap_report["+".join(chain)] = str(err)
                continue
            y, joins = render(cues, clips)
            name = "+".join(chain)
            write_wav(os.path.join(a.previews, name + ".wav"), y, rng)
            gap_report[name] = {"cues": [k for k, _ in cues], "duration": round(len(y) / RATE, 3),
                                "joinGapsMs": [round(g * 1000) for g in join_gaps(y, joins)]}
        summary["previews"] = gap_report

    json.dump(summary, sys.stdout, indent=1)
    print()


if __name__ == "__main__":
    main()
