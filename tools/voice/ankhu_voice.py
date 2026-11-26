#!/usr/bin/env python3
"""Ankhu spell voice pipeline: ElevenLabs generation side.

Standard library only, so it runs on any machine with Python 3.9+ (Windows included).

    python tools/voice/ankhu_voice.py expand                      # list every take the library needs
    python tools/voice/ankhu_voice.py candidates [--set anpu]     # voice design previews + one account voice
    python tools/voice/ankhu_voice.py save-voice --generated-id ID --name "Ankhu"
    python tools/voice/ankhu_voice.py batch [--only PREFIX] [--force]
    python tools/voice/ankhu_voice.py batch --lines anpu_lines.json --profile anpu_voice_profile.json --out out/raw_anpu
    python tools/voice/ankhu_voice.py takes --key KEY --text TEXT --seeds 1,2,3 --profile P --out DIR
    python tools/voice/ankhu_voice.py asr DIR [--out FILE]      # speech-to-text round trip (Scribe)
    python tools/voice/ankhu_voice.py pron-dict [--rules pronunciation.json] --profile P [--profile P2]
    python tools/voice/ankhu_voice.py takes ... --model eleven_flash_v2 --rules R.json --mp3   # audition variants

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
PRONUNCIATION = os.path.join(HERE, "pronunciation.json")
PRON_CACHE = os.path.join(OUT, "pron_dicts.json")  # rules hash -> dictionary locator, so reruns reuse one dictionary
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

# Anpu (Anubis). Test text is his real draft dialogue: the Spell 25 cinematic line and the first
# teaching-room nudge (borrowed-dawn TEACHING_ROOMS.md).
ANPU_TEXT = (
    "Rise... Ankhu. "
    "Your voice has come back to you, Ankhu. Here, words are the only hands you have. "
    "Water listens to a direction."
)
ANPU_DESCRIPTIONS = {
    "P1_gravel_colossus": (
        "An ancient god of the dead, guardian of the threshold. Extremely deep bass voice with enormous "
        "gravitas, gravelly and husky, slow and hushed, every word heavy and final. Epic but restrained, "
        "never shouting." + _STUDIO
    ),
    "P2_whispering_guide": (
        "A vast, ancient jackal-headed god guiding a newly dead soul. Very deep, husky, half-whispered "
        "bass, close and intimate, breath audible in the low gravel. Slow and patient, immense calm "
        "authority, kind beneath the weight." + _STUDIO
    ),
    "P3_cavern_herald": (
        "A towering primordial god speaking from deep in the chest. Huge, resonant, gravelly basso "
        "profundo, slow, deliberate, hushed but monumental, like stone moving. Epic gravitas, solemn, "
        "timeless." + _STUDIO
    ),
    "P4_weathered_eternal": (
        "An eternal god who has walked the dead for ten thousand years. Deep, weathered, rasping, husky "
        "low voice, very slow with long pauses, hushed and grave, tired but unshakable, enormous "
        "presence without volume." + _STUDIO
    ),
}

# Round 2 (Zac, 2026-11-21): unusual accent, and no invented ambience. Voice Design tends to add room
# tone, breaths or music when the description sounds cinematic, so these ask for a bare voice only.
_CLEAN = (" Voice only: no background sound, no music, no sound effects, no echo, no room ambience."
          " Clean studio recording, completely dry.")
ANPU2_DESCRIPTIONS = {
    "Q1_rolling_ancient": (
        "An ancient god of the dead. Very deep, gravelly, husky bass, slow and hushed, enormous gravitas. "
        "An unusual old-world accent that belongs to no living country: rolled r's, long open vowels, "
        "consonants pressed forward, a faint lilt at the ends of phrases." + _CLEAN
    ),
    "Q2_nile_south": (
        "A towering jackal-headed god guiding the dead. Deep, husky, gravelly bass, slow, hushed and "
        "grave, immense calm authority. A distinctive accent with a soft Nubian and Sudanese colour: "
        "warm rounded vowels, gently tapped r's, unhurried musical rhythm." + _CLEAN
    ),
    "Q3_liturgical": (
        "An eternal god speaking in the cadence of a Coptic liturgical chanter. Very deep, resonant, "
        "gravelly and husky, slow, hushed, every word weighed. An unusual accent with deep guttural "
        "consonants, stretched vowels and a ceremonial sing-song rise and fall." + _CLEAN
    ),
    "Q4_stone_whisper": (
        "A vast primordial god, half whispering. Deep, rasping, husky bass with heavy gravel, very slow, "
        "hushed, enormous presence without volume. A strange, hard-to-place accent: clipped consonants, "
        "dark rounded vowels, slight Eastern Mediterranean colour, foreign to modern English." + _CLEAN
    ),
}

CANDIDATE_SETS = {
    "ankhu": (CANDIDATE_TEXT, VOICE_DESCRIPTIONS),
    "anpu": (ANPU_TEXT, ANPU_DESCRIPTIONS),
    "anpu2": (ANPU_TEXT, ANPU2_DESCRIPTIONS),
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


def load_profile(path=None):
    path = path or PROFILE
    profile = json.loads(json.dumps(DEFAULT_PROFILE))
    if os.path.exists(path):
        with open(path, encoding="utf-8") as fh:
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
    parts = {"t": take, "v": profile["voice_id"], "m": profile["model_id"], "s": profile["voice_settings"]}
    # A dictionary only changes takes whose text contains one of its strings, so adding a name rule does not
    # mark every other take as stale. Locators without "replaces" (older profiles) always count.
    text = take.get("text") or ""
    locs = [l for l in profile.get("pronunciation_dictionary_locators") or []
            if "replaces" not in l or any(r in text for r in l["replaces"])]
    if locs:
        parts["p"] = api_locators({"pronunciation_dictionary_locators": locs})
    blob = json.dumps(parts, sort_keys=True)
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


_ALLOWED_RATE = None


def api_locators(profile):
    """The profile's pronunciation dictionaries in the shape the TTS API takes (it allows at most 3)."""
    locs = profile.get("pronunciation_dictionary_locators") or []
    return [{"pronunciation_dictionary_id": l["pronunciation_dictionary_id"], "version_id": l["version_id"]} for l in locs][:3]


def tts(voice_id, profile, text, prev=None, nxt=None, seed=None):
    body = {
        "text": text,
        "model_id": profile["model_id"],
        "voice_settings": profile["voice_settings"],
    }
    if api_locators(profile):
        body["pronunciation_dictionary_locators"] = api_locators(profile)
    if profile.get("language_code") and profile["model_id"] in ("eleven_turbo_v2_5", "eleven_flash_v2_5"):
        body["language_code"] = profile["language_code"]
    if prev:
        body["previous_text"] = prev
    if nxt:
        body["next_text"] = nxt
    if seed is not None:
        body["seed"] = seed
    # pcm_44100 needs the Pro tier. Fall back to pcm_24000 on lower plans and report the rate used.
    global _ALLOWED_RATE
    for rate in ((_ALLOWED_RATE,) if _ALLOWED_RATE else (SAMPLE_RATE, 24000)):
        try:
            try:
                res = request("POST", f"/v1/text-to-speech/{voice_id}/with-timestamps?output_format=pcm_{rate}", body)
            except RuntimeError as err:
                if "output_format_not_allowed" in str(err) or profile["model_id"] != "eleven_v3":
                    raise
                # Some models (eleven_v3) may refuse the timestamps endpoint. Plain TTS still returns the audio.
                pcm = request("POST", f"/v1/text-to-speech/{voice_id}?output_format=pcm_{rate}", body, accept="audio/pcm")
                res = {"audio_base64": base64.b64encode(pcm).decode("ascii")}
            res["_rate"] = rate
            _ALLOWED_RATE = rate
            return res
        except RuntimeError as err:
            if "output_format_not_allowed" not in str(err) or rate == 24000:
                raise
            print(f"pcm_{rate} not allowed on this plan, falling back to pcm_24000", file=sys.stderr)


def cmd_candidates(args):
    text, descriptions = CANDIDATE_SETS[args.set]
    out = os.path.join(OUT, "candidates" if args.set == "ankhu" else "candidates_" + args.set)
    os.makedirs(out, exist_ok=True)
    profile = load_profile()
    report = {"text": text, "designed": [], "account": []}
    json_path = os.path.join(out, "candidates.json")

    for label, description in descriptions.items():
        body = {"voice_description": description, "text": text, "model_id": "eleven_multilingual_ttv_v2", "seed": 7}
        try:
            result = request("POST", "/v1/text-to-voice/design", body)
        except RuntimeError as err:
            print(f"design endpoint failed ({err}); trying create-previews", file=sys.stderr)
            body = {"voice_description": description, "text": text}
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
        with open(json_path, "w", encoding="utf-8") as fh:  # save ids as we go, so a later crash loses nothing
            json.dump(report, fh, indent=2)

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

    ranked = sorted(voices, key=score, reverse=True)[: args.account_voices] if args.account_voices else []
    for v in ranked:
        res = tts(v["voice_id"], profile, text, seed=7)
        name = "C_account_" + "".join(ch for ch in v.get("name", "voice") if ch.isalnum())
        path = os.path.join(out, name + ".wav")
        write_wav(path, base64.b64decode(res["audio_base64"]), res["_rate"])
        report["account"].append({"name": name, "file": os.path.basename(path), "voice_id": v["voice_id"],
                                  "voice_name": v.get("name"), "labels": v.get("labels"), "category": v.get("category"), "sample_rate": res["_rate"]})
        print(f"wrote {path}")

    with open(json_path, "w", encoding="utf-8") as fh:
        json.dump(report, fh, indent=2)
    print("candidates.json written")


def cmd_save_voice(args):
    all_desc = dict(VOICE_DESCRIPTIONS, **ANPU_DESCRIPTIONS, **ANPU2_DESCRIPTIONS)
    description = args.description or all_desc.get(args.label or "", args.name)
    res = request("POST", "/v1/text-to-voice", {
        "voice_name": args.name, "voice_description": description, "generated_voice_id": args.generated_id})
    path = args.profile or PROFILE
    profile = load_profile(path)
    profile["voice_id"] = res["voice_id"]
    profile["voice_name"] = args.name
    profile["design_label"] = args.label
    profile["generated_voice_id"] = args.generated_id
    profile["voice_description"] = description
    if args.speed is not None:
        profile["voice_settings"]["speed"] = args.speed
    if args.stability is not None:
        profile["voice_settings"]["stability"] = args.stability
    with open(path, "w", encoding="utf-8") as fh:
        json.dump(profile, fh, indent=2)
        fh.write("\n")
    print(f"saved voice {args.name} as {res['voice_id']} in {os.path.basename(path)}")


def cmd_use_voice(args):
    profile = load_profile()
    profile["voice_id"] = args.voice_id
    profile["voice_name"] = args.name
    with open(PROFILE, "w", encoding="utf-8") as fh:
        json.dump(profile, fh, indent=2)
        fh.write("\n")
    print(f"voice_profile.json now uses {args.voice_id}")


def expand_dialogue(path):
    """One take per dialogue line. A line's previous_text is the line spoken just before it in the same
    scene (intro n uses intro n-1), so a run of lines keeps one continuous delivery."""
    with open(path, encoding="utf-8") as fh:
        spec = json.load(fh)
    takes, previous = [], {}
    for line in spec["lines"]:
        key = line["key"]
        scene = key.rsplit(".intro.", 1)[0] if ".intro." in key else None
        seed = int(hashlib.sha256(f"{key}#0".encode()).hexdigest()[:8], 16) % 4294967295
        takes.append({"key": key, "variant": 0, "text": line["text"],
                      "previous_text": previous.get(scene) if scene else None, "next_text": None, "seed": seed})
        if scene:
            previous[scene] = line["text"]
    return takes


def cmd_batch(args):
    profile_path = args.profile or PROFILE
    profile = load_profile(profile_path)
    if not profile["voice_id"]:
        sys.exit(f"No voice_id in {os.path.basename(profile_path)}. Run save-voice or use-voice first.")
    raw = args.out or os.path.join(OUT, "raw")
    os.makedirs(raw, exist_ok=True)
    source = expand_dialogue(args.lines) if args.lines else expand_lines()
    takes = [t for t in source if not args.only or t["key"].startswith(args.only)]
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
        write_wav(wav_path, base64.b64decode(res["audio_base64"]), res["_rate"])
        meta = dict(t, hash=h, voice_id=profile["voice_id"], model_id=profile["model_id"],
                    voice_settings=profile["voice_settings"], alignment=res.get("alignment"),
                    normalized_alignment=res.get("normalized_alignment"))
        with open(meta_path, "w", encoding="utf-8") as fh:
            json.dump(meta, fh, indent=1)
        done += 1
        chars += len(t["text"])
        print(f"[{done + skipped}/{len(takes)}] {stem}")
    print(f"generated {done}, unchanged {skipped}, {chars} characters billed")


def cmd_takes(args):
    """Alternative takes of one line, one per seed, for auditioning. Output stems are KEY#sSEED."""
    profile_path = args.profile or PROFILE
    profile = load_profile(profile_path)
    if not profile["voice_id"]:
        sys.exit(f"No voice_id in {os.path.basename(profile_path)}.")
    if args.model:
        profile["model_id"] = args.model
    if args.stability is not None:
        profile["voice_settings"]["stability"] = args.stability
    if args.rules:  # audition a rule set without touching the profile on disk
        profile["pronunciation_dictionary_locators"] = [ensure_dictionary(args.rules)]
    elif args.no_dict:
        profile["pronunciation_dictionary_locators"] = []
    os.makedirs(args.out, exist_ok=True)
    seeds = [int(s) for s in args.seeds.split(",") if s.strip()]
    for i, seed in enumerate(seeds, 1):
        t = {"key": args.key, "variant": 0, "text": args.text, "previous_text": None, "next_text": None, "seed": seed}
        res = tts(profile["voice_id"], profile, args.text, seed=seed)
        stem = (args.stem or "{key}#s{seed}").format(key=args.key, seed=seed)
        wav_path = os.path.join(args.out, stem + ".wav")
        write_wav(wav_path, base64.b64decode(res["audio_base64"]), res["_rate"])
        if args.mp3:
            import subprocess
            subprocess.run(["ffmpeg", "-loglevel", "error", "-y", "-i", wav_path, "-b:a", "192k",
                            os.path.join(args.out, stem + ".mp3")], check=True)
        meta = dict(t, hash=take_hash(t, profile), voice_id=profile["voice_id"], model_id=profile["model_id"],
                    voice_settings=profile["voice_settings"], rules=args.rules,
                    pronunciation_dictionary_locators=api_locators(profile), alignment=res.get("alignment"),
                    normalized_alignment=res.get("normalized_alignment"))
        with open(os.path.join(args.out, stem + ".json"), "w", encoding="utf-8") as fh:
            json.dump(meta, fh, indent=1)
        print(f"[{i}/{len(seeds)}] {stem}")
    print(f"{len(seeds)} takes, {len(args.text) * len(seeds)} characters billed")


def _rules_spec(path):
    with open(path, encoding="utf-8") as fh:
        spec = json.load(fh)
    rules = spec.get("rules") or []
    if not rules:
        sys.exit(f"{os.path.basename(path)} has no rules yet.")
    return spec.get("name", "Ankhu names"), rules


def ensure_dictionary(path, name=None):
    """Create (or reuse, if these exact rules were uploaded before) a pronunciation dictionary.
    Returns {name, pronunciation_dictionary_id, version_id}. Alias rules work on every model.
    Phoneme rules only work on eleven_flash_v2, eleven_turbo_v2 and eleven_monolingual_v1."""
    dict_name, rules = _rules_spec(path)
    dict_name = name or dict_name
    digest = hashlib.sha256(json.dumps({"n": dict_name, "r": rules}, sort_keys=True).encode()).hexdigest()[:16]
    cache = {}
    if os.path.exists(PRON_CACHE):
        with open(PRON_CACHE, encoding="utf-8") as fh:
            cache = json.load(fh)
    if digest in cache:
        return cache[digest]
    res = request("POST", "/v1/pronunciation-dictionaries/add-from-rules", {"name": dict_name, "rules": rules})
    loc = {"name": dict_name, "pronunciation_dictionary_id": res["id"], "version_id": res["version_id"]}
    cache[digest] = loc
    os.makedirs(OUT, exist_ok=True)
    with open(PRON_CACHE, "w", encoding="utf-8") as fh:
        json.dump(cache, fh, indent=1)
    print(f"dictionary {dict_name!r}: {loc['pronunciation_dictionary_id']} version {loc['version_id']}")
    return loc


def cmd_pron_dict(args):
    """Upload the rules and point each profile at the new dictionary version. A profile's existing locator
    with the same dictionary name is replaced, so a rerun after editing the rules never stacks duplicates."""
    loc = dict(ensure_dictionary(args.rules or PRONUNCIATION, args.name))
    loc["replaces"] = sorted({r["string_to_replace"] for r in _rules_spec(args.rules or PRONUNCIATION)[1]})
    for path in args.profile:
        with open(path, encoding="utf-8") as fh:
            profile = json.load(fh)
        locs = [l for l in profile.get("pronunciation_dictionary_locators") or [] if l.get("name") != loc["name"]]
        profile["pronunciation_dictionary_locators"] = locs + [loc]
        with open(path, "w", encoding="utf-8") as fh:
            json.dump(profile, fh, indent=2)
            fh.write("\n")
        print(f"{os.path.basename(path)} now uses {loc['pronunciation_dictionary_id']} / {loc['version_id']}")


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
        body, ctype = _multipart({"model_id": "scribe_v1", "language_code": "en",
                                  "tag_audio_events": "true" if args.events else "false"},
                                 "file", f, data, "audio/wav" if f.lower().endswith(".wav") else "audio/mpeg")
        res = request("POST", "/v1/speech-to-text", raw_body=body, content_type=ctype)
        results[f] = res.get("text", "")
        events = [w.get("text") for w in res.get("words", []) if w.get("type") == "audio_event"]
        if args.events:
            results[f] = {"text": results[f], "audio_events": events}
        print(f"[{i}/{len(files)}] {f}: {res.get('text', '')!r}" + (f"  EVENTS: {events}" if events else ""))
    out = args.out or os.path.join(args.dir, "asr.json")
    with open(out, "w", encoding="utf-8") as fh:
        json.dump(results, fh, indent=1)
    print(f"wrote {out}")


def main():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = p.add_subparsers(dest="cmd", required=True)
    e = sub.add_parser("expand"); e.add_argument("-v", "--verbose", action="store_true"); e.set_defaults(fn=cmd_expand)
    c = sub.add_parser("candidates"); c.add_argument("--account-voices", type=int, default=1)
    c.add_argument("--set", choices=sorted(CANDIDATE_SETS), default="ankhu"); c.set_defaults(fn=cmd_candidates)
    s = sub.add_parser("save-voice"); s.add_argument("--generated-id", required=True); s.add_argument("--name", default="Ankhu")
    s.add_argument("--label"); s.add_argument("--description"); s.add_argument("--profile")
    s.add_argument("--speed", type=float); s.add_argument("--stability", type=float)
    s.set_defaults(fn=cmd_save_voice)
    u = sub.add_parser("use-voice"); u.add_argument("--voice-id", required=True); u.add_argument("--name"); u.set_defaults(fn=cmd_use_voice)
    b = sub.add_parser("batch"); b.add_argument("--only"); b.add_argument("--force", action="store_true")
    b.add_argument("--lines", help="dialogue line file (for example anpu_lines.json) instead of the spell catalog")
    b.add_argument("--profile", help="voice profile json (default voice_profile.json)")
    b.add_argument("--out", help="raw output folder (default out/raw)"); b.set_defaults(fn=cmd_batch)
    k = sub.add_parser("takes", help="several seeds of one line, for auditioning")
    k.add_argument("--key", required=True); k.add_argument("--text", required=True)
    k.add_argument("--seeds", default="1,2,3,4,5,6"); k.add_argument("--profile"); k.add_argument("--out", required=True)
    k.add_argument("--model", help="override the profile's model_id (for example eleven_flash_v2 for phoneme rules)")
    k.add_argument("--stability", type=float, help="override stability (eleven_v3 takes only 0.0, 0.5 or 1.0)")
    k.add_argument("--rules", help="pronunciation rules json to use for these takes instead of the profile's dictionaries")
    k.add_argument("--no-dict", action="store_true", help="ignore the profile's pronunciation dictionaries")
    k.add_argument("--stem", help="output name template, default '{key}#s{seed}'")
    k.add_argument("--mp3", action="store_true", help="also write an mp3 next to each wav (needs ffmpeg)")
    k.set_defaults(fn=cmd_takes)
    d = sub.add_parser("pron-dict", help="upload pronunciation rules and attach the dictionary to profiles")
    d.add_argument("--rules", help="rules json (default pronunciation.json)"); d.add_argument("--name")
    d.add_argument("--profile", action="append", required=True); d.set_defaults(fn=cmd_pron_dict)
    a = sub.add_parser("asr"); a.add_argument("dir"); a.add_argument("--out")
    a.add_argument("--events", action="store_true", help="ask Scribe to tag non-speech audio (music, breathing, ...)"); a.set_defaults(fn=cmd_asr)
    args = p.parse_args()
    args.fn(args)


if __name__ == "__main__":
    main()
