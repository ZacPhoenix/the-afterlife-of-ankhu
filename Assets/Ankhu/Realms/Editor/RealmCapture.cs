using System;
using System.IO;
using System.Linq;
using Ankhu.Realms.Core;
using Ankhu.Realms.Core.Content;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ankhu.Realms.EditorTools
{
    /// <summary>
    /// Renders every fixed camera of every realm scene to PNG (1920x1080), once in each object's first state and
    /// once with every object in its last pose, so layouts and lighting can be judged without pressing Play.
    ///   Unity -batchmode -projectPath . -executeMethod Ankhu.Realms.EditorTools.RealmCapture.CaptureAllCli -logFile realms_capture.log
    /// Output: art_gen/realms/captures/&lt;scene&gt;_&lt;camera&gt;_&lt;phase&gt;.png
    /// </summary>
    public static class RealmCapture
    {
        public const string OutDir = "art_gen/realms/captures";

        public static void CaptureAllCli()
        {
            try
            {
                RealmSceneBuilder.BuildAll();
                CaptureAll();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        public static void CaptureOnlyCli()
        {
            try
            {
                CaptureAll();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        public static void CaptureWernesPaintedCli()
        {
            try
            {
                CaptureScripts(WernesPaintedScripts.All());
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>Renders every camera of one scene as saved: -executeMethod ...RealmCapture.CaptureSceneCli -capScene Assets/.../X.unity</summary>
        public static void CaptureSceneCli()
        {
            try
            {
                string[] a = System.Environment.GetCommandLineArgs();
                int i = Array.IndexOf(a, "-capScene");
                if (i < 0 || i + 1 >= a.Length) throw new ArgumentException("-capScene <path> required");
                string name = Path.GetFileNameWithoutExtension(a[i + 1]);
                EditorSceneManager.OpenScene(a[i + 1], OpenSceneMode.Single);
                Directory.CreateDirectory(OutDir);
                // Batch mode has no 16:9 window, so a follow camera clamps with the wrong aspect. Snap it at 16:9.
                foreach (var f in UnityEngine.Object.FindObjectsByType<Ankhu.CameraRig.CameraFollow2D>(FindObjectsSortMode.None))
                {
                    var c = f.GetComponent<Camera>();
                    if (c != null) c.aspect = 16f / 9f;
                    f.SnapToTarget();
                }
                foreach (Camera cam in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                    Render(cam, Path.Combine(OutDir, $"{name}_{cam.name}.png"), 1920, 1080);
                // -capBoth: also render each follow camera in both projections (what F2 flips), as _persp and _ortho.
                if (Array.IndexOf(a, "-capBoth") >= 0)
                    foreach (var f in UnityEngine.Object.FindObjectsByType<Ankhu.CameraRig.CameraFollow2D>(FindObjectsSortMode.None))
                    {
                        var c = f.GetComponent<Camera>();
                        if (c == null) continue;
                        var was = f.Mode;
                        foreach (var m in new[] { Ankhu.CameraRig.CameraFollow2D.Projection.Perspective, Ankhu.CameraRig.CameraFollow2D.Projection.Orthographic })
                        {
                            f.SetProjection(m);
                            f.SnapToTarget();
                            Render(c, Path.Combine(OutDir, $"{name}_{c.name}_{(m == Ankhu.CameraRig.CameraFollow2D.Projection.Perspective ? "persp" : "ortho")}.png"), 1920, 1080);
                        }
                        f.SetProjection(was);
                    }
                Debug.Log("[Realms] Captured " + name);
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        [UnityEditor.MenuItem("Ankhu/Realms/Capture Realm Cameras")]
        public static void CaptureAll() => CaptureScripts(WernesScripts.All());

        public static void CaptureScripts(System.Collections.Generic.IEnumerable<SceneScript> scripts)
        {
            Directory.CreateDirectory(OutDir);
            foreach (var script in scripts)
            {
                string path = RealmKit.SceneDir + "/" + script.SceneName + ".unity";
                if (!File.Exists(path)) continue;
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var views = UnityEngine.Object.FindObjectsByType<RealmObjectView>(FindObjectsSortMode.None);
                foreach (string phase in new[] { "start", "end" })
                {
                    foreach (RealmObjectView v in views)
                    {
                        if (v.poses.Count == 0) continue;
                        var pose = phase == "start" ? v.poses[0] : v.poses[v.poses.Count - 1];
                        SnapPose(pose);
                    }
                    var cams = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).OrderBy(c => c.transform.GetSiblingIndex()).ToArray();
                    foreach (Camera cam in cams)
                    {
                        string file = Path.Combine(OutDir, $"{script.SceneName}_{cam.name}_{phase}.png");
                        Render(cam, file, 1920, 1080);
                    }
                }
                Debug.Log("[Realms] Captured " + script.SceneName);
            }
        }

        private static void SnapPose(RealmObjectView.StatePose pose)
        {
            foreach (var pp in pose.parts)
            {
                if (pp.part == null) continue;
                pp.part.localPosition = pp.localPosition;
                pp.part.localRotation = Quaternion.Euler(pp.localEuler);
                pp.part.localScale = pp.localScale;
            }
            foreach (GameObject g in pose.show) if (g != null) g.SetActive(true);
            foreach (GameObject g in pose.hide) if (g != null) g.SetActive(false);
            for (int i = 0; i < pose.lights.Count && i < pose.lightIntensities.Count; i++)
                if (pose.lights[i] != null) pose.lights[i].intensity = pose.lightIntensities[i];
        }

        /// <summary>Public entry for other batch tools (RepaintBatchCli).</summary>
        public static void RenderTo(Camera cam, string file, int w, int h) => Render(cam, file, w, h);

        private static void Render(Camera cam, string file, int w, int h)
        {
            bool was = cam.enabled;
            cam.enabled = true;
            // Each painted camera carries its own backdrop quad 300 m out. Show only this camera's, as the game does.
            var others = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)
                .Where(c => c != cam).SelectMany(c => c.transform.Cast<Transform>())
                .Where(t => t.gameObject.activeSelf && (t.name == "Painting" || t.name.StartsWith("Backdrop"))).ToList();
            foreach (Transform t in others) t.gameObject.SetActive(false);
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            RenderTexture prev = cam.targetTexture;
            cam.targetTexture = rt;
            // BackdropFit only sizes its quad in LateUpdate, which batch mode never runs. Fit every backdrop now, at 16:9.
            foreach (var bf in UnityEngine.Object.FindObjectsByType<Ankhu.Opening.BackdropFit>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Ankhu.Realms.EditorTools.NoStretchCheck.FitBackdrop(bf);
            cam.Render();
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            File.WriteAllBytes(file, tex.EncodeToPNG());
            RenderTexture.active = null;
            cam.targetTexture = prev;
            cam.enabled = was;
            foreach (Transform t in others) t.gameObject.SetActive(true);
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(tex);
        }
    }
}
