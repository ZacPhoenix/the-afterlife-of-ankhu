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
    /// Builds Ankhu's <see cref="SpellVoiceLibrary"/> from Assets/Ankhu/Audio/Voice/voice_manifest.json, which
    /// tools/voice/process_voice.py writes together with the clips. Runs by itself whenever the manifest or a
    /// clip is imported, and from the menu Ankhu &gt; Rebuild Spell Voice Library. Also fixes the import settings
    /// of every voice clip: mono, decompressed on load (no decode latency at cast time), Vorbis on disk.
    /// </summary>
    public sealed class SpellVoiceLibraryBuilder : AssetPostprocessor
    {
        public const string VoiceRoot = "Assets/Ankhu/Audio/Voice";
        public const string ClipsRoot = VoiceRoot + "/Clips/";
        public const string ManifestPath = VoiceRoot + "/voice_manifest.json";
        public const string LibraryFolder = "Assets/Ankhu/Generated/Resources";
        public const string LibraryPath = LibraryFolder + "/" + SpellVoiceLibrary.ResourcePath + ".asset";

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
            if (!assetPath.StartsWith(ClipsRoot, StringComparison.Ordinal))
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
            bool touched = imported.Concat(deleted).Concat(moved).Any(p =>
                p == ManifestPath || p.StartsWith(ClipsRoot, StringComparison.Ordinal));
            if (touched && !rebuildQueued)
            {
                rebuildQueued = true;
                EditorApplication.delayCall += () =>
                {
                    rebuildQueued = false;
                    Rebuild(false);
                };
            }
        }

        [MenuItem("Ankhu/Rebuild Spell Voice Library")]
        public static void RebuildFromMenu() => Rebuild(true);

        /// <summary>Batch mode: Unity -batchmode -projectPath . -executeMethod Ankhu.EditorTools.SpellVoiceLibraryBuilder.BuildFromCommandLine -quit</summary>
        public static void BuildFromCommandLine()
        {
            AssetDatabase.Refresh();
            if (Rebuild(true) == null)
            {
                EditorApplication.Exit(1);
            }
        }

        public static SpellVoiceLibrary Rebuild(bool verbose)
        {
            if (!File.Exists(ManifestPath))
            {
                if (verbose)
                {
                    Debug.LogWarning("No voice manifest at " + ManifestPath + ". Run tools/voice/process_voice.py first.");
                }
                return null;
            }

            Manifest manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath));
            var missing = new List<string>();
            var entries = new List<SpellVoiceLibrary.Entry>();
            foreach (IGrouping<string, ManifestEntry> group in manifest.entries.Where(e => !string.IsNullOrEmpty(e.key)).GroupBy(e => e.key).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                var clips = new List<AudioClip>();
                foreach (ManifestEntry e in group.OrderBy(e => e.variant))
                {
                    string path = VoiceRoot + "/" + e.file;
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
            var library = AssetDatabase.LoadAssetAtPath<SpellVoiceLibrary>(LibraryPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<SpellVoiceLibrary>();
                AssetDatabase.CreateAsset(library, LibraryPath);
            }
            var timing = new SpellVoiceTiming { ClauseGap = manifest.timing.clauseGap, WordGap = manifest.timing.wordGap };
            library.SetContents(entries, timing, manifest.targetAsl);
            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();

            if (missing.Count > 0)
            {
                Debug.LogWarning("Spell voice library: " + missing.Count + " clips listed in the manifest were not found, first: " + missing[0]);
            }
            if (verbose || missing.Count > 0)
            {
                Debug.Log("Spell voice library rebuilt: " + entries.Count + " keys, " + entries.Sum(e => e.variants.Length) + " takes -> " + LibraryPath);
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
