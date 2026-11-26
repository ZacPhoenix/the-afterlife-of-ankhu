using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ankhu.Voice;
using UnityEditor;
using UnityEngine;

namespace Ankhu.EditorTools
{
    /// <summary>
    /// Builds the keyed voice libraries from the manifests tools/voice/process_voice.py writes beside the clips:
    /// Ankhu's spells (Assets/Ankhu/Audio/Voice), Anpu's dialogue (Assets/Ankhu/Audio/Dialogue/Anpu) and
    /// Ankhu's spoken dialogue lines (Assets/Ankhu/Audio/Dialogue/Ankhu).
    /// Runs by itself whenever a manifest or a clip is imported, and from the menu Ankhu &gt; Rebuild Spell Voice
    /// Library. Also fixes the import settings of every voice clip: mono, decompressed on load (no decode
    /// latency at cast time), Vorbis on disk.
    /// </summary>
    public sealed class SpellVoiceLibraryBuilder : AssetPostprocessor
    {
        public const string VoiceRoot = "Assets/Ankhu/Audio/Voice";
        public const string ClipsRoot = VoiceRoot + "/Clips/";
        public const string ManifestPath = VoiceRoot + "/voice_manifest.json";
        public const string LibraryFolder = "Assets/Ankhu/Generated/Resources";
        public const string LibraryPath = LibraryFolder + "/" + SpellVoiceLibrary.ResourcePath + ".asset";
        public const string AnpuRoot = "Assets/Ankhu/Audio/Dialogue/Anpu";
        public const string AnkhuDialogueRoot = "Assets/Ankhu/Audio/Dialogue/Ankhu";

        /// <summary>Every library this builder maintains: clip folder root and the Resources name of its asset.</summary>
        private static readonly (string root, string resource)[] Libraries =
        {
            (VoiceRoot, SpellVoiceLibrary.ResourcePath),
            (AnpuRoot, "AnpuDialogueVoice"),
            (AnkhuDialogueRoot, "AnkhuDialogueVoice"),
        };

        private static bool IsVoiceClip(string path) =>
            Libraries.Any(l => path.StartsWith(l.root + "/Clips/", StringComparison.Ordinal));

        private static bool IsVoiceAsset(string path) =>
            Libraries.Any(l => path == l.root + "/voice_manifest.json" || path.StartsWith(l.root + "/Clips/", StringComparison.Ordinal));

        private static bool rebuildQueued;

        [Serializable]
        private sealed class ManifestTiming
        {
            public float clauseGap = SpellVoiceTiming.Default.ClauseGap;
            public float wordGap = SpellVoiceTiming.Default.WordGap;
        }

        [Serializable]
        private sealed class ManifestEntry
        {
            public string key;
            public int variant;
            public string file;
        }

        [Serializable]
        private sealed class Manifest
        {
            public int version;
            public float targetAsl = -20f;
            public ManifestTiming timing = new ManifestTiming();
            public ManifestEntry[] entries = Array.Empty<ManifestEntry>();
        }

        private void OnPreprocessAudio()
        {
            if (!IsVoiceClip(assetPath))
            {
                return;
            }
            var importer = (AudioImporter)assetImporter;
            importer.forceToMono = true;
            importer.loadInBackground = false;
            AudioImporterSampleSettings s = importer.defaultSampleSettings;
            s.loadType = AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = 0.8f;
            s.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            importer.defaultSampleSettings = s;
        }

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            bool touched = imported.Concat(deleted).Concat(moved).Any(IsVoiceAsset);
            if (touched && !rebuildQueued)
            {
                rebuildQueued = true;
                EditorApplication.delayCall += () =>
                {
                    rebuildQueued = false;
                    RebuildAll(false);
                };
            }
        }

        [MenuItem("Ankhu/Rebuild Spell Voice Library")]
        public static void RebuildFromMenu() => RebuildAll(true);

        /// <summary>Batch mode: Unity -batchmode -projectPath . -executeMethod Ankhu.EditorTools.SpellVoiceLibraryBuilder.BuildFromCommandLine -quit</summary>
        public static void BuildFromCommandLine()
        {
            AssetDatabase.Refresh();
            if (RebuildAll(true) == 0)
            {
                EditorApplication.Exit(1);
            }
        }

        /// <summary>Rebuilds every library whose manifest exists. Returns how many were built.</summary>
        public static int RebuildAll(bool verbose)
        {
            int built = 0;
            foreach ((string root, string resource) in Libraries)
            {
                if (Rebuild(root, resource, verbose) != null)
                {
                    built++;
                }
            }
            return built;
        }

        public static SpellVoiceLibrary Rebuild(bool verbose) => Rebuild(VoiceRoot, SpellVoiceLibrary.ResourcePath, verbose);

        public static SpellVoiceLibrary Rebuild(string root, string resource, bool verbose)
        {
            string manifestPath = root + "/voice_manifest.json";
            string libraryPath = LibraryFolder + "/" + resource + ".asset";
            if (!File.Exists(manifestPath))
            {
                if (verbose && root == VoiceRoot)
                {
                    Debug.LogWarning("No voice manifest at " + manifestPath + ". Run tools/voice/process_voice.py first.");
                }
                return null;
            }

            Manifest manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(manifestPath));
            var missing = new List<string>();
            var entries = new List<SpellVoiceLibrary.Entry>();
            foreach (IGrouping<string, ManifestEntry> group in manifest.entries.Where(e => !string.IsNullOrEmpty(e.key)).GroupBy(e => e.key).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                var clips = new List<AudioClip>();
                foreach (ManifestEntry e in group.OrderBy(e => e.variant))
                {
                    string path = root + "/" + e.file;
                    AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                    if (clip == null)
                    {
                        missing.Add(path);
                        continue;
                    }
                    clips.Add(clip);
                }
                if (clips.Count > 0)
                {
                    entries.Add(new SpellVoiceLibrary.Entry { key = group.Key, variants = clips.ToArray() });
                }
            }

            EnsureFolder(LibraryFolder);
            var library = AssetDatabase.LoadAssetAtPath<SpellVoiceLibrary>(libraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<SpellVoiceLibrary>();
                AssetDatabase.CreateAsset(library, libraryPath);
            }
            var timing = new SpellVoiceTiming { ClauseGap = manifest.timing.clauseGap, WordGap = manifest.timing.wordGap };
            library.SetContents(entries, timing, manifest.targetAsl);
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();

            if (missing.Count > 0)
            {
                Debug.LogWarning("Voice library " + resource + ": " + missing.Count + " clips listed in the manifest were not found, first: " + missing[0]);
            }
            if (verbose || missing.Count > 0)
            {
                Debug.Log("Voice library " + resource + " rebuilt: " + entries.Count + " keys, " + entries.Sum(e => e.variants.Length) + " takes -> " + libraryPath);
            }
            return library;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent))
            {
                EnsureFolder(parent);
            }
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
