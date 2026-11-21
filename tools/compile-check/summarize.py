#!/usr/bin/env python3
"""Summarise MSBuild diagnostics (from -flp logs) grouped by source file.

usage: summarize.py [LABEL=]LOG [[LABEL=]LOG...]
A diagnostic that appears only in logs with a label is annotated with those labels (e.g. Player).
Prints errors then warnings, deduplicated across build variants, grouped by file with paths
relative to the repo root. Exit status 1 if any error was found.
"""
import os
import re
import sys
from collections import OrderedDict

REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
LINE = re.compile(
    r"^(?P<file>[^(\n]+?)\((?P<line>\d+),(?P<col>\d+)\): (?P<sev>error|warning) (?P<code>[A-Z]+\d+): "
    r"(?P<msg>.*?)(?: \[(?P<proj>[^\]]+)\])?$"
)
GENERIC = re.compile(r"^(?P<file>.*?): (?P<sev>error|warning) (?P<code>[A-Z]+\d+): (?P<msg>.*?)(?: \[(?P<proj>[^\]]+)\])?$")


def tag(d):
    """Classify diagnostics whose cause is the reference set, not the Ankhu code."""
    m, f, code = d["msg"], d["file"], d["code"]
    if "TestTools" in m or "UnityTest" in m or "LogAssert" in m:
        return "missing-ref: Unity Test Framework (UnityEngine.TestRunner) is not in the reference set"
    if code in ("CS0117", "CS0234", "CS0246", "CS1061", "CS0103", "CS7036", "CS1501") and (
        "UnityEditor" in m or "/Editor/" in f
    ):
        return "possible version skew: UnityEditor reference is 2021.1.14f1, not Unity 6"
    return ""


def main(paths):
    diags = OrderedDict()
    for spec in paths:
        label, _, p = spec.rpartition("=") if "=" in spec else ("", "", spec)
        if not os.path.exists(p):
            continue
        for raw in open(p, encoding="utf-8", errors="replace"):
            raw = re.sub(r"^\s*\d+>", "", raw.strip())
            m = LINE.match(raw) or GENERIC.match(raw)
            if not m:
                continue
            d = m.groupdict()
            d.setdefault("line", "")
            d.setdefault("col", "")
            f = d["file"]
            if os.path.isabs(f):
                f = os.path.relpath(f, REPO)
            d["file"] = f
            d["proj"] = os.path.basename(d.get("proj") or "").replace(".csproj", "")
            key = (d["sev"], f, d["line"], d["col"], d["code"], d["msg"])
            if key in diags:
                if d["proj"] and d["proj"] not in diags[key]["projs"]:
                    diags[key]["projs"].append(d["proj"])
                diags[key]["labels"].add(label)
            else:
                d["projs"] = [d["proj"]] if d["proj"] else []
                d["labels"] = {label}
                diags[key] = d

    errors = [d for d in diags.values() if d["sev"] == "error"]
    warnings = [d for d in diags.values() if d["sev"] == "warning"]

    def dump(title, items):
        print(f"\n{title}: {len(items)}")
        byfile = OrderedDict()
        for d in items:
            byfile.setdefault(d["file"], []).append(d)
        for f, ds in sorted(byfile.items()):
            print(f"  {f}  ({len(ds)})")
            for d in sorted(ds, key=lambda x: (int(x["line"] or 0), int(x["col"] or 0))):
                loc = f"{d['line']}:{d['col']}" if d["line"] else "-"
                t = tag(d)
                t = f"   <-- {t}" if t else ""
                if "" not in d["labels"]:
                    t += "   [" + "/".join(sorted(d["labels"])) + " build only]"
                print(f"    {loc:>8} {d['code']} {d['msg']}{t}")

    dump("ERRORS", errors)
    dump("WARNINGS", warnings)
    skew = [d for d in errors if tag(d)]
    if skew:
        print(f"\n  ({len(skew)} of the errors are tagged as reference-set artefacts, not necessarily real)")
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
