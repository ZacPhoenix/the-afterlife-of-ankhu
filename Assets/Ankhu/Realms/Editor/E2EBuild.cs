using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Ankhu.Realms.EditorTools
{
    /// <summary>
    /// Zac 2026-11-25: a Windows player of the whole play order, title first, for an end to end playtest.
    /// Applies the PlayOrder Build Settings order, then builds every enabled scene that exists on disk.
    ///   Unity -batchmode -projectPath D:\TheAfterlifeofAnkhu -executeMethod Ankhu.Realms.EditorTools.E2EBuild.BuildCli -e2eOut D:\TheAfterlifeofAnkhu\Builds\E2E_2026-11-25 -logFile e2e_build.log
    /// </summary>
    public static class E2EBuild
    {
        public const string ExeName = "AfterlifeOfAnkhu.exe";

        public static void BuildCli()
        {
            int code = 1;
            try
            {
                string outDir = Arg("-e2eOut") ?? Path.GetFullPath("Builds/E2E_" + DateTime.Now.ToString("yyyy-MM-dd"));
                PlayOrderCheck.ApplyBuildOrder();
                string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled && File.Exists(s.path)).Select(s => s.path).ToArray();
                Debug.Log("[E2E] Building " + scenes.Length + " scenes into " + outDir + ":\n" + string.Join("\n", scenes));
                if (!scenes[0].EndsWith("Opening_Title.unity")) throw new Exception("First scene is not the title: " + scenes[0]);
                Directory.CreateDirectory(outDir);
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = Path.Combine(outDir, ExeName),
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None,
                });
                Debug.Log($"[E2E] Build {report.summary.result}, {report.summary.totalErrors} errors, {report.summary.totalSize / (1024 * 1024)} MB, {report.summary.totalTime}");
                code = report.summary.result == BuildResult.Succeeded ? 0 : 1;
            }
            catch (Exception e) { Debug.LogException(e); }
            EditorApplication.Exit(code);
        }

        private static string Arg(string name)
        {
            string[] a = System.Environment.GetCommandLineArgs();
            int i = Array.IndexOf(a, name);
            return i >= 0 && i + 1 < a.Length ? a[i + 1] : null;
        }
    }
}
