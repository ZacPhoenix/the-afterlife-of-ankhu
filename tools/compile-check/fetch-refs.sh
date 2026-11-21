#!/usr/bin/env bash
# Downloads the Unity reference assemblies used by compile-check into ./refs (idempotent).
#
# Sources (nuget.org; download.unity3d.com / packages.unity.com are blocked from this container):
#   refs/unity6  <- OnTogether.GameLibs 3.0.0 (lib/netstandard2.1): genuine, unmodified Unity 6 (6000.0.x)
#                   player assemblies shipped by a Unity 6 game: UnityEngine.*Module, UnityEngine.UI (uGUI),
#                   Unity.InputSystem 1.14.2, Unity.TextMeshPro, Unity.Mathematics. Original member visibility
#                   (NOT publicized), real method bodies (not reference-stripped).
#   refs/editor  <- Unity3D.SDK 2021.1.14.1: UnityEditor.dll from Unity 2021.1.14f1 (no Unity 6 UnityEditor
#                   is reachable). Editor-API version skew is possible; see compile-check.sh output.
# These DLLs are Unity Technologies' property; use them for local compile checking only, never redistribute.
set -euo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REFS="$HERE/refs"
CACHE="$HERE/.cache"
mkdir -p "$CACHE" "$REFS/unity6" "$REFS/editor"

fetch() { # id version sha256
  local id="$1" ver="$2" sha="$3" f="$CACHE/$1.$2.nupkg"
  if [[ ! -f "$f" ]] || ! echo "$sha  $f" | sha256sum -c --quiet - 2>/dev/null; then
    curl -fsSL --retry 3 -m 600 "https://api.nuget.org/v3-flatcontainer/$id/$ver/$id.$ver.nupkg" -o "$f.tmp"
    echo "$sha  $f.tmp" | sha256sum -c --quiet - || { echo "sha256 mismatch for $id $ver" >&2; exit 1; }
    mv "$f.tmp" "$f"
  fi
}

if [[ -f "$REFS/.complete" ]]; then exit 0; fi

fetch ontogether.gamelibs 3.0.0 5ee0703a6421abb7dd47f4ebe1d39781d6f63460810962eed2d543ddc4fa3a95 &
fetch unity3d.sdk 2021.1.14.1 c8c455eaf62dfc915fe316cf5fede8530ec56dfcfcf05df1e74b80ca23b61b62 &
wait

rm -f "$REFS"/unity6/*.dll "$REFS"/editor/*.dll
unzip -qoj "$CACHE/ontogether.gamelibs.3.0.0.nupkg" \
  'lib/netstandard2.1/UnityEngine.dll' 'lib/netstandard2.1/UnityEngine.*Module.dll' \
  'lib/netstandard2.1/UnityEngine.UI.dll' 'lib/netstandard2.1/Unity.InputSystem.dll' \
  'lib/netstandard2.1/Unity.InputSystem.ForUI.dll' 'lib/netstandard2.1/Unity.TextMeshPro.dll' \
  'lib/netstandard2.1/Unity.Mathematics.dll' -d "$REFS/unity6"
unzip -qoj "$CACHE/unity3d.sdk.2021.1.14.1.nupkg" 'lib/UnityEditor.dll' -d "$REFS/editor"
touch "$REFS/.complete"
echo "refs ready: $(ls "$REFS/unity6" | wc -l) Unity 6 player DLLs, $(ls "$REFS/editor" | wc -l) editor DLL"
