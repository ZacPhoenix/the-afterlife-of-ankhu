using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Ankhu.EditorTools
{
    /// <summary>
    /// Player build of the current Build Settings (title, film, walk and the Borrowed Dawn gate, as the opening
    /// build leaves them) into Builds/OpeningToBorrowedDawn_v2, so a build Zac is playing is never overwritten.
    /// BorrowedDawnDressOnBuild re-dresses the room before the build starts.
    /// -executeMethod Ankhu.EditorTools.BorrowedDawnV2Build.BuildCli
    /// </summary>
    public static class BorrowedDawnV2Build
    {
        public const string BuildDir = "Builds/OpeningToBorrowedDawn_v2";
        private const string ExeName = "AfterlifeOfAnkhu.exe";
        private const string Greybox = "Assets/Ankhu/Generated/Scenes/BorrowedDawn_Greybox.unity";

        public static void Build()
        {
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
                locationPathName = Path.Combine(BuildDir, ExeName),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Player build " + report.summary.result + ", " + report.summary.totalErrors + " errors.");
            Debug.Log("[BDv2] Built " + Path.GetFullPath(options.locationPathName) + " scenes: " + string.Join(", ", scenes.Select(Path.GetFileNameWithoutExtension)));
        }

        public static void BuildCli()
        {
            try { Build(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
    }
}
