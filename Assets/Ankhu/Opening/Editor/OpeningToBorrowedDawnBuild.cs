using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Ankhu.Opening.EditorTools
{
    /// <summary>
    /// Builds title, cinematic, walk and the first gate into Borrowed Dawn to its own folder, so it never
    /// overwrites Builds/OpeningSlice. Uses the Build Settings that OpeningSceneBuilder.BuildScenes leaves.
    /// </summary>
    public static class OpeningToBorrowedDawnBuild
    {
        public const string BuildDir = "Builds/OpeningToBorrowedDawn";
        private const string Greybox = "Assets/Ankhu/Generated/Scenes/BorrowedDawn_Greybox.unity";

        [UnityEditor.MenuItem("Ankhu/Opening/Build Opening To Borrowed Dawn")]
        public static void Build()
        {
            OpeningSceneBuilder.BuildScenes();
            var list = EditorBuildSettings.scenes.ToList();
            int i = list.FindIndex(s => s.path == Greybox);
            if (i < 0) list.Add(new EditorBuildSettingsScene(Greybox, true));
            else if (!list[i].enabled) list[i] = new EditorBuildSettingsScene(Greybox, true);
            EditorBuildSettings.scenes = list.ToArray();

            Directory.CreateDirectory(BuildDir);
            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled && File.Exists(s.path)).Select(s => s.path).ToArray();
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(BuildDir, OpeningSceneBuilder.ExeName),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Player build " + report.summary.result + ", " + report.summary.totalErrors + " errors.");
            Debug.Log("[OpeningToBD] Built " + Path.GetFullPath(options.locationPathName) + " scenes: " + string.Join(", ", scenes.Select(Path.GetFileNameWithoutExtension)));
        }

        public static void BuildCli()
        {
            try { Build(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
    }
}
