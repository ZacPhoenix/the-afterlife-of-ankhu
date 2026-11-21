using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Ankhu.Realms.EditorTools
{
    /// <summary>
    /// A Windows build of the playable path: every enabled Build Settings scene up to and including the last Realm_ scene
    /// (title, film, walk, then the realms). Scenes after the realms are left out of this build only.
    ///   Unity -batchmode -projectPath . -executeMethod Ankhu.Realms.EditorTools.RealmBuild.BuildWindowsCli -logFile realms_build.log
    /// Output: Builds/Realms/AfterlifeOfAnkhu.exe
    /// </summary>
    public static class RealmBuild
    {
        public const string OutPath = "Builds/Realms/AfterlifeOfAnkhu.exe";

        public static void BuildWindowsCli()
        {
            try
            {
                RealmSceneBuilder.BuildAll();
                string[] enabled = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
                int last = Array.FindLastIndex(enabled, p => Path.GetFileName(p).StartsWith("Realm_"));
                string[] scenes = last >= 0 ? enabled.Take(last + 1).ToArray() : enabled;
                Debug.Log("[Realms] Building scenes:\n" + string.Join("\n", scenes));
                Directory.CreateDirectory(Path.GetDirectoryName(OutPath));
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = OutPath,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None,
                });
                Debug.Log($"[Realms] Build {report.summary.result}, {report.summary.totalErrors} errors, {report.summary.totalSize / (1024 * 1024)} MB");
                EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }
    }
}
