using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Ankhu.Realms.EditorTools
{
    /// <summary>
    /// A one-scene Windows player of Wernes_A_Field for the built-player flashback check. Writes only to the
    /// folder given by -ankhuCheckOut (never to Builds/). -ankhuCheckScene &lt;path&gt; picks another scene. Run the exe with -ankhuFlashbackCheck &lt;folder&gt;.
    ///   Unity -batchmode -quit -projectPath . -executeMethod Ankhu.Realms.EditorTools.FlashbackCheckBuild.BuildCli -ankhuCheckOut &lt;dir&gt;
    /// </summary>
    public static class FlashbackCheckBuild
    {
        public const string Scene = "Assets/Ankhu/Realms/Generated/Scenes/Wernes_A_Field.unity";

        public static void BuildCli()
        {
            try
            {
                string outDir = null, scene = Scene;
                string[] args = System.Environment.GetCommandLineArgs();
                for (int i = 0; i < args.Length - 1; i++)
                {
                    if (args[i] == "-ankhuCheckOut") outDir = args[i + 1];
                    if (args[i] == "-ankhuCheckScene") scene = args[i + 1];
                }
                if (string.IsNullOrEmpty(outDir)) throw new ArgumentException("-ankhuCheckOut <dir> is required");
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { scene },
                    locationPathName = System.IO.Path.Combine(outDir, "FlashbackCheck.exe"),
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None,
                });
                Debug.Log($"[FlashbackCheck] Build {report.summary.result}, {report.summary.totalErrors} errors");
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
