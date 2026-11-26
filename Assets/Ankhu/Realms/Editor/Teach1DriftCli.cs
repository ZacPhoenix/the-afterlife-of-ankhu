using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Ankhu.Realms.EditorTools
{
    /// <summary>
    /// After the Teach1 identity repaint (2026-11-26): runs the no-stretch check (no fix) and recaptures
    /// Teach1_Cistern_Painted and BorrowedDawn_Painted from every camera, with nothing saved to the scene.
    /// Args: -capOut &lt;abs dir&gt;. Logs "[Teach1Drift] CheckCli exit N" and exits with that code.
    /// </summary>
    public static class Teach1DriftCli
    {
        private static readonly string[] Scenes = { "Assets/Ankhu/Generated/Scenes/Teach1_Cistern_Painted.unity", "Assets/Ankhu/Generated/Scenes/BorrowedDawn_Painted.unity" };

        public static void Run()
        {
            int code;
            try
            {
                string[] args = System.Environment.GetCommandLineArgs();
                string outDir = args[Array.IndexOf(args, "-capOut") + 1];
                Directory.CreateDirectory(outDir);
                AssetDatabase.Refresh();
                int bad = NoStretchCheck.Run(false, false, out string report);
                code = bad == 0 ? 0 : 1;
                Debug.Log("[Teach1Drift] CheckCli exit " + code + ", failing rows " + bad);
                File.WriteAllText(Path.Combine(outDir, "check_report.md"), report);
                foreach (string sp in Scenes)
                {
                    EditorSceneManager.OpenScene(sp, OpenSceneMode.Single);
                    foreach (var f in Object.FindObjectsByType<Ankhu.CameraRig.CameraFollow2D>(FindObjectsSortMode.None))
                    {
                        var c = f.GetComponent<Camera>();
                        if (c != null) c.aspect = 16f / 9f;
                        f.SnapToTarget();
                    }
                    foreach (Camera cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                        RealmCapture.RenderTo(cam, Path.Combine(outDir, Path.GetFileNameWithoutExtension(sp) + "_" + cam.name + ".png"), 1920, 1080);
                }
                Debug.Log("[Teach1Drift] captured");
            }
            catch (Exception e) { Debug.LogException(e); code = 99; }
            EditorApplication.Exit(code);
        }
    }
}
