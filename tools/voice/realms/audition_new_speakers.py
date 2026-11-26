"""Auditions account voices for the new Realms speakers: one line per candidate, then pitch and loudness numbers.
Run from the repo root: py -3 tools/voice/realms/audition_new_speakers.py. Output: tools/voice/out/realms_audition/."""
import base64, json, os, sys
import numpy as np
HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.dirname(HERE))
import ankhu_voice as av

OUT = os.path.join(os.path.dirname(HERE), "out", "realms_audition")
os.makedirs(OUT, exist_ok=True)
CANDIDATES = {
    "announcer": ["6sFKzaJr574YWVu4UuJF", "pqHfZKP75CvOlQylNhV4", "O4uxPTtvgxrA0QCLHI1J"],
    "guardian": ["N2lVS1w4EtoT3dr4eOWO", "fbIG6gEosVIM95R5qOna", "1BfrkuYXmEwp8AWqSLWk"],
    "keeper": ["lhsOe6KovF0XxcKEsHpN", "wzVFxVZCBIi6KBxNhQDI", "ljo9gAlSqKOvF6D8sOsX"],
    "hori": ["0luPAj5RsdhmnkZaiYcb", "XJ2fW4ybq7HouelYYGcL", "1jR8l3dNgd4OQs6kxgaF"],
    "neferet": ["EXAVITQu4vr4xnSDxMaL", "pFZP5JQG7iQjIQuC4Bku", "hpp4J3VqNfWAUOO0d1Us"],
    "father": ["JBFqnCBsd6RMkjVDRZzb", "nPczCjzI2devNBz1zQrb", "iP95p4xoKVk53GoZ742B"],
}
base = av.load_profile(os.path.join(os.path.dirname(HERE), "voice_profile.json"))


def f0_median(x, sr):
    fr, hop = int(0.04 * sr), int(0.01 * sr)
    vals = []
    for i in range(0, len(x) - fr, hop):
        s = x[i:i + fr]
        if np.sqrt(np.mean(s ** 2)) < 0.02:
            continue
        s = s - s.mean()
        ac = np.correlate(s, s, "full")[fr - 1:]
        lo, hi = int(sr / 500), int(sr / 50)
        k = lo + np.argmax(ac[lo:hi])
        if ac[k] > 0.4 * ac[0]:
            vals.append(sr / k)
    return float(np.median(vals)) if vals else 0.0


report = []
for speaker, ids in CANDIDATES.items():
    line = json.load(open(os.path.join(HERE, f"realms_{speaker}_lines.json"), encoding="utf-8"))["lines"][0]
    for vid in ids:
        path = os.path.join(OUT, f"{speaker}_{vid}.wav")
        if not os.path.exists(path):
            res = av.tts(vid, base, line["text"], seed=7)
            av.write_wav(path, base64.b64decode(res["audio_base64"]), res["_rate"])
        import wave
        with wave.open(path) as w:
            sr = w.getframerate(); x = np.frombuffer(w.readframes(w.getnframes()), np.int16) / 32768.0
        r = {"speaker": speaker, "voice_id": vid, "f0": round(f0_median(x, sr)), "secs": round(len(x) / sr, 2)}
        report.append(r); print(r, flush=True)
json.dump(report, open(os.path.join(OUT, "audition.json"), "w"), indent=1)
