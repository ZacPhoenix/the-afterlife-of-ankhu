#!/usr/bin/env python3
"""Ankhu spell voice pipeline: ElevenLabs generation side.

Standard library only, so it runs on any machine with Python 3.9+ (Windows included).

    python tools/voice/ankhu_voice.py expand                      # list every take the library needs
    python tools/voice/ankhu_voice.py candidates                  # voice design previews + one account voice
    python tools/voice/ankhu_voice.py save-voice --generated-id ID --name "Ankhu"
    python tools/voice/ankhu_voice.py batch [--only PREFIX] [--force]
    python tools/voice/ankhu_voice.py asr DIR [--out FILE]        # speech-to-text round trip (Scribe)

The API key is read from the ELEVENLABS_API_KEY environment variable, or on Windows from the
user-level registry value of the same name (so a variable set with SetEnvironmentVariable(..., "User")
works without restarting the shell that launched this script). The key is never printed, logged or
written to disk. Raw output goes to tools/voice/out/ which git ignores. Post-processing (trim, loudness,
fades, import into Unity) is a separate step: process_voice.py.
"""
import argparse
import base64
import hashlib
import json
import os
import sys
import time
import urllib.error
import urllib.request
import uuid
import wave

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "out")
LINES = os.path.join(HERE, "voice_lines.json")
PROFILE = os.path.join(HERE, "voice_profile.json")
API = "https://api.elevenlabs.io"
SAMPLE_RATE = 44100

DEFAULT_PROFILE = {
    "voice_id": None,
    "voice_name": None,
    "model_id": "eleven_multilingual_v2",
    "language_code": "en",
    "voice_settings": {
        "stability": 0.6,
        "similarity_boost": 0.8,
        "style": 0.1,
        "use_speaker_boost": True,
        "speed": 0.92,
    },
}

# Same text for every candidate so the comparison is fair. It holds every phrase shape the game uses:
# a bare command, an article phrase, a vocative, a two-clause chain and the gate's Name.
CANDIDATE_TEXT = (
    "Flow west. Bind the shadow. Wake, bearer. "
    "Release the elevator, and echo the bell. "
    "He Who Returns the Morning."
)

_STUDIO = " Soft, unplaceable accent. Clean studio recording, completely dry, no reverb."
VOICE_DESCRIPTIONS = {
    "A_grave_baritone": (
        "A man in his mid thirties, a priest of ancient Egypt speaking words of power. Low, warm baritone, "
        "grave and intimate, close to the microphone, unhurried and deliberate. Quiet authority, never "
        "theatrical or booming." + _STUDIO
    ),
    "B_hushed_scribe": (
        "A soft-spoken man in his thirties, a temple scribe and priest, speaking reverently in a dim stone "
        "chamber. Light baritone, slightly breathy, almost hushed, very precise consonants, measured pace, "
        "a calm sorrow under the surface." + _STUDIO
    ),
    "C_resonant_chanter": (
        "A man in his late thirties, a lector priest trained to chant. Deep, resonant chest voice with a "
        "slightly rough, weathered texture. Slow, rhythmic, ceremonial cadence, each word weighed, solemn "
        "but human, not a narrator." + _STUDIO
    ),
    "D_earnest_seeker": (
        "A man in his early thirties, newly dead and finding his way, speaking ancient words he half "
        "remembers. Clear, warm mid-range voice, earnest and a little vulnerable, steady and sincere, "
        "gentle conviction rather than command." + _STUDIO
    ),
}

# ----------------------------------------------------------------------------------------------- key, http

def api_key():
    key = os.environ.get("ELEVENLABS_API_KEY")
    if not key and sys.platform == "win32":
        try:
            import winreg  # stdlib on Windows
            with winreg.OpenKey(winreg.HKEY_CURRENT_USER, "Environment") as reg:
                key, _ = winreg.QueryValueEx(reg, "ELEVENLABS_API_KEY")
        except OSError:
            key = None
    if not key:
        sys.exit("ELEVENLABS_API_KEY is not set (environment or HKCU\\Environment).")
    return key.strip()


def request(method, path, body=None, raw_body=None, content_type=None, accept="application/json", retries=4):
    headers = {"xi-api-key": api_key(), "Accept": accept}
    data = None
    if body is not None:
        data = json.dumps(body).encode("utf-8")
        headers["Content-Type"] = "application/json"
    elif raw_body is not None:
        data = raw_body
        headers["Content-Type"] = content_type
    delay = 2.0
    for attempt in range(retries + 1):
        req = urllib.request.Request(API + path, data=data, headers=headers, method=method)
        try:
            with urllib.request.urlopen(req, timeout=120) as resp:
                payload = resp.read()
                return json.loads(payload) if accept == "application/json" else payload
        except urllib.error.HTTPError as err:
            detail = err.read().decode("utf-8", "replace")[:600]
            if err.code in (429, 500, 502, 503, 504) and attempt < retries:
                time.sleep(delay)
                delay *= 2
                continue
            raise RuntimeError(f"{method} {path} -> HTTP {err.code}: {detail}") from None
        except urllib.error.URLError as err:
            if attempt < retries:
                time.sleep(delay)
                delay *= 2
                continue
            raise RuntimeError(f"{method} {path} -> {err.reason}") from None


def write_wav(path, pcm_bytes, rate=SAMPLE_RATE):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(rate)
        w.writeframes(pcm_bytes)


def load_profile():
    profile = json.loads(json.dumps(DEFAULT_PROFILE))
    if os.path.exists(PROFILE):
        with open(PROFILE, encoding="utf-8") as fh:
            saved = json.load(fh)
        profile.update({k: v for k, v in saved.items() if k != "voice_settings"})
        profile["voice_settings"].update(saved.get("voice_settings", {}))
    return profile


# ----------------------------------------------------------------------------------------------- lines

def _cap(text):
    return text[:1].upper() + text[1:]


def expand_lines(spec=None):
    """Every take the library needs: key, text, previous_text, next_text, seed."""
    if spec is None:
        with open(LINES, encoding="utf-8") as fh:
            spec = json.load(fh)
    chain = spec["chainExample"]
    takes = []

    def add(key, text, prev=None, nxt=None, variants=1):
        for v in range(variants):
            seed = int(hashlib.sha256(f"{key}#{v}".encode()).hexdigest()[:8], 16) % 4294967295
            takes.append({"key": key, "variant": v, "text": text, "previous_text": prev, "next_text": nxt, "seed": seed})

    neighbour = "Wake, bearer,"
    for c in spec["clauses"]:
        positions = c.get("positions", spec["clausePositions"])
        t = c["text"]
        n = spec["clauseVariants"]
        if c["category"] == "name":
            add(f"clause.{c['id']}.sole", t + ".", variants=n)
            continue
        for pos in positions:
            key = f"clause.{c['id']}.{pos}"
            if pos == "sole":
                add(key, _cap(t) + ".", variants=n)
            elif pos == "first":
                add(key, _cap(t) + ",", nxt=chain + ".", variants=n)
            elif pos == "medial":
                add(key, "and " + t + ",", prev=neighbour, nxt=chain + ".", variants=n)
            elif pos == "final":
                add(key, "and " + t + ".", prev=neighbour, variants=n)

    w = spec["words"]
    nv = spec["wordVariants"]
    for verb in w["verbs"]:
        add(f"word.{verb}.lead", _cap(verb), nxt="the bridge.", variants=nv)
    for noun in w["targets"] + w["modifiers"]:
        add(f"word.{noun}.lead", noun, prev="Bind the", nxt="and flow west.", variants=nv)
        add(f"word.{noun}.final", noun + ".", prev="Bind the", variants=nv)
    add("word.the.lead", "the", prev="Bind", nxt="bridge.", variants=nv)
    add("word.and.lead", "and", prev="Flow west,", nxt="bind the bridge.", variants=nv)
    return takes


def take_hash(take, profile):
    blob = json.dumps({"t": take, "v": profile["voice_id"], "m": profile["model_id"], "s": profile["voice_settings"]}, sort_keys=True)
    return hashlib.sha256(blob.encode()).hexdigest()[:16]


# ----------------------------------------------------------------------------------------------- commands

def cmd_expand(args):
    takes = expand_lines()
    chars = sum(len(t["text"]) for t in takes)
    keys = sorted({t["key"] for t in takes})
    print(f"{len(takes)} takes, {len(keys)} keys, {chars} characters of spoken text")
    if args.verbose:
        for t in takes:
            print(f"  {t['key']}#{t['variant']}: {t['text']!r}  prev={t['previous_text']!r} next={t['next_text']!r}")


def tts(voice_id, profile, text, prev=None, nxt=None, seed=None):
    body = {
        "text": text,
        "model_id": profile["model_id"],
        "voice_settings": profile["voice_settings"],
    }
    if profile.get("language_code") and profile["model_id"] in ("eleven_turbo_v2_5", "eleven_flash_v2_5"):
        body["language_code"] = profile["language_code"]
    if prev:
        body["previous_text"] = prev
    if nxt:
        body["next_text"] = nxt
    if seed is not None:
        body["seed"] = seed
    return request("POST", f"/v1/text-to-speech/{voice_id}/with-timestamps?output_format=pcm_{SAMPLE_RATE}", body)


def cmd_candidates(args):
    out = os.path.join(OUT, "candidates")
    os.makedirs(out, exist_ok=True)
    profile = load_profile()
    report = {"text": CANDIDATE_TEXT, "designed": [], "account": []}

    for label, description in VOICE_DESCRIPTIONS.items():
        body = {"voice_description": description, "text": CANDIDATE_TEXT, "model_id": "eleven_multilingual_ttv_v2", "seed": 7}
        try:
            result = request("POST", "/v1/text-to-voice/design", body)
        except RuntimeError as err:
            print(f"design endpoint failed ({err}); trying create-previews", file=sys.stderr)
            body = {"voice_description": description, "text": CANDIDATE_TEXT}
            result = request("POST", "/v1/text-to-voice/create-previews", body)
        for i, preview in enumerate(result.get("previews", [])):
            name = label if i == 0 else f"{label}_alt{i}"
            path = os.path.join(out, name + ".mp3")
            with open(path, "wb") as fh:
                fh.write(base64.b64decode(preview["audio_base_64"]))
            report["designed"].append({
                "name": name, "file": os.path.basename(path), "description": description,
                "generated_voice_id": preview.get("generated_voice_id"), "duration_secs": preview.get("duration_secs"),
            })
            print(f"wrote {path}")

    # One voice already in the account (premade or owned), chosen by its labels, for a baseline.
    voices = request("GET", "/v2/voices?page_size=100").get("voices", [])

    def score(v):
        labels = {k: str(val).lower() for k, val in (v.get("labels") or {}).items()}
        s = 0
        s += 3 if labels.get("gender") == "male" else -10
        s += 2 if labels.get("age") in ("middle aged", "middle_aged", "young") else 0
        desc = " ".join(labels.values()) + " " + str(v.get("description") or "").lower()
        s += sum(1 for w in ("deep", "calm", "warm", "narrat", "grave", "soft", "story") if w in desc)
        s -= 3 if any(w in desc for w in ("energetic", "excited", "upbeat", "child")) else 0
        return s

    ranked = sorted(voices, key=score, reverse=True)[: args.account_voices]
    for v in ranked:
        res = tts(v["voice_id"], profile, CANDIDATE_TEXT, seed=7)
        name = "C_account_" + "".join(ch for ch in v.get("name", "voice") if ch.isalnum())
        path = os.path.join(out, name + ".wav")
        write_wav(path, base64.b64decode(res["audio_base64"]))
        report["account"].append({"name": name, "file": os.path.basename(path), "voice_id": v["voice_id"],
                                  "voice_name": v.get("name"), "labels": v.get("labels"), "category": v.get("category")})
        print(f"wrote {path}")

    with open(os.path.join(out, "candidates.json"), "w", encoding="utf-8") as fh:
        json.dump(report, fh, indent=2)
    print("candidates.json written")


def cmd_save_voice(args):
    description = args.description or VOICE_DESCRIPTIONS.get(args.label or "", "Ankhu, priest of ancient Egypt")
    res = request("POST", "/v1/text-to-voice", {
        "voice_name": args.name, "voice_description": description, "generated_voice_id": args.generated_id})
    profile = load_profile()
    profile["voice_id"] = res["voice_id"]
    profile["voice_name"] = args.name
    with open(PROFILE, "w", encoding="utf-8") as fh:
        json.dump(profile, fh, indent=2)
        fh.write("\n")
    print(f"saved voice {args.name} as {res['voice_id']} in voice_profile.json")


def cmd_use_voice(args):
    profile = load_profile()
    profile["voice_id"] = args.voice_id
    profile["voice_name"] = args.name
    with open(PROFILE, "w", encoding="utf-8") as fh:
        json.dump(profile, fh, indent=2)
        fh.write("\n")
    print(f"voice_profile.json now uses {args.voice_id}")


def cmd_batch(args):
    profile = load_profile()
    if not profile["voice_id"]:
        sys.exit("No voice_id in voice_profile.json. Run save-voice or use-voice first.")
    raw = os.path.join(OUT, "raw")
    os.makedirs(raw, exist_ok=True)
    takes = [t for t in expand_lines() if not args.only or t["key"].startswith(args.only)]
    done = skipped = 0
    chars = 0
    for t in takes:
        stem = f"{t['key']}#{t['variant']}"
        wav_path = os.path.join(raw, stem + ".wav")
        meta_path = os.path.join(raw, stem + ".json")
        h = take_hash(t, profile)
        if not args.force and os.path.exists(wav_path) and os.path.exists(meta_path):
            with open(meta_path, encoding="utf-8") as fh:
                if json.load(fh).get("hash") == h:
                    skipped += 1
                    continue
        res = tts(profile["voice_id"], profile, t["text"], t["previous_text"], t["next_text"], t["seed"])
        write_wav(wav_path, base64.b64decode(res["audio_base64"]))
        meta = dict(t, hash=h, voice_id=profile["voice_id"], model_id=profile["model_id"],
                    voice_settings=profile["voice_settings"], alignment=res.get("alignment"),
                    normalized_alignment=res.get("normalized_alignment"))
        with open(meta_path, "w", encoding="utf-8") as fh:
            json.dump(meta, fh, indent=1)
        done += 1
        chars += len(t["text"])
        print(f"[{done + skipped}/{len(takes)}] {stem}")
    print(f"generated {done}, unchanged {skipped}, {chars} characters billed")


def _multipart(fields, file_field, filename, file_bytes, mime):
    boundary = uuid.uuid4().hex
    parts = []
    for k, v in fields.items():
        parts.append(f"--{boundary}\r\nContent-Disposition: form-data; name=\"{k}\"\r\n\r\n{v}\r\n".encode())
    parts.append(f"--{boundary}\r\nContent-Disposition: form-data; name=\"{file_field}\"; filename=\"{filename}\"\r\n"
                 f"Content-Type: {mime}\r\n\r\n".encode() + file_bytes + b"\r\n")
    parts.append(f"--{boundary}--\r\n".encode())
    return b"".join(parts), f"multipart/form-data; boundary={boundary}"


def cmd_asr(args):
    results = {}
    files = sorted(f for f in os.listdir(args.dir) if f.lower().endswith((".wav", ".mp3")))
    for i, f in enumerate(files, 1):
        with open(os.path.join(args.dir, f), "rb") as fh:
            data = fh.read()
        body, ctype = _multipart({"model_id": "scribe_v1", "language_code": "en", "tag_audio_events": "false"},
                                 "file", f, data, "audio/wav" if f.lower().endswith(".wav") else "audio/mpeg")
        res = request("POST", "/v1/speech-to-text", raw_body=body, content_type=ctype)
        results[f] = res.get("text", "")
        print(f"[{i}/{len(files)}] {f}: {results[f]!r}")
    out = args.out or os.path.join(args.dir, "asr.json")
    with open(out, "w", encoding="utf-8") as fh:
        json.dump(results, fh, indent=1)
    print(f"wrote {out}")


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = p.add_subparsers(dest="cmd", required=True)
    e = sub.add_parser("expand"); e.add_argument("-v", "--verbose", action="store_true"); e.set_defaults(fn=cmd_expand)
    c = sub.add_parser("candidates"); c.add_argument("--account-voices", type=int, default=1); c.set_defaults(fn=cmd_candidates)
    s = sub.add_parser("save-voice"); s.add_argument("--generated-id", required=True); s.add_argument("--name", default="Ankhu")
    s.add_argument("--label"); s.add_argument("--description"); s.set_defaults(fn=cmd_save_voice)
    u = sub.add_parser("use-voice"); u.add_argument("--voice-id", required=True); u.add_argument("--name"); u.set_defaults(fn=cmd_use_voice)
    b = sub.add_parser("batch"); b.add_argument("--only"); b.add_argument("--force", action="store_true"); b.set_defaults(fn=cmd_batch)
    a = sub.add_parser("asr"); a.add_argument("dir"); a.add_argument("--out"); a.set_defaults(fn=cmd_asr)
    args = p.parse_args()
    args.fn(args)


if __name__ == "__main__":
    main()
