using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ankhu.Realms.EditorTools
{
    /// <summary>
    /// The native backdrop repaint batch in one Unity launch per phase.
    /// -repaintPhase before: capture the 12 painted Wernes and Rosetau scenes as they are.
    /// -repaintPhase after: add flame cards in place in all 12 saved scenes, run the NoStretchCheck fix and check, capture.
    /// -capOut &lt;abs dir&gt; is where the 1920x1080 captures go, one per camera.
    /// -scenes A,B,... limits both phases to those scene names (default: all 12).
    /// FixCli result is logged as "[RepaintBatch] FixCli exit N" (0 pass, 1 fail, 99 error).
    /// </summary>
    public static class RepaintBatchCli
    {
        public static readonly string[] Scenes =
        {
            "Wernes_A_Field", "Wernes_B_Shabti", "Wernes_C_House", "Wernes_D_Bricks", "Wernes_Terrace", "Wernes_Hub", "Wernes_Finale",
            "Rosetau_A_Laugh", "Rosetau_B_Mourners", "Rosetau_C_Mother", "Rosetau_D_TwoWays", "Rosetau_E_Finale",
        };

        public static void Run()
        {
            try
            {
                string[] a = System.Environment.GetCommandLineArgs();
                string phase = Arg(a, "-repaintPhase") ?? throw new ArgumentException("-repaintPhase before|after required");
                string outDir = Arg(a, "-capOut") ?? throw new ArgumentException("-capOut <dir> required");
                Directory.CreateDirectory(outDir);
                string only = Arg(a, "-scenes");
                string[] scenes = only == null ? Scenes : only.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                if (phase == "after")
                {
                    // No builder rebuild: other work has edited these saved scenes. Flame cards go in place.
                    var paths = new System.Collections.Generic.List<string>();
                    foreach (string s in scenes) paths.Add(RealmKit.SceneDir + "/" + s + ".unity");
                    foreach (var kv in RealmSceneBuilder.FlameCardsInPlace(paths.ToArray()))
                        Debug.Log("[RepaintBatch] flame cards " + Path.GetFileNameWithoutExtension(kv.Key) + " " + kv.Value);
                    int code;
                    try
                    {
                        NoStretchCheck.Run(true, false, out _);
                        int bad = NoStretchCheck.Run(false, false, out string report);
                        Debug.Log(report);
                        code = bad == 0 ? 0 : 1;
                    }
                    catch (Exception e) { Debug.LogException(e); code = 99; }
                    Debug.Log("[RepaintBatch] FixCli exit " + code);
                }
                foreach (string s in scenes)
                {
                    string path = RealmKit.SceneDir + "/" + s + ".unity";
                    if (!File.Exists(path)) { Debug.LogWarning("[RepaintBatch] missing " + path); continue; }
                    EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    foreach (var f in UnityEngine.Object.FindObjectsByType<Ankhu.CameraRig.CameraFollow2D>(FindObjectsSortMode.None))
                    {
                        var c = f.GetComponent<Camera>();
                        if (c != null) c.aspect = 16f / 9f;
                        f.SnapToTarget();
                    }
                    foreach (Camera cam in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                        RealmCapture.RenderTo(cam, Path.Combine(outDir, $"{s}_{cam.name}.png"), 1920, 1080);
                    Debug.Log("[RepaintBatch] captured " + s);
                }
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        private static string Arg(string[] a, string key)
        {
            int i = Array.IndexOf(a, key);
            return i >= 0 && i + 1 < a.Length ? a[i + 1] : null;
        }
    }
}
