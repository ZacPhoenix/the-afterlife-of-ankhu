using System;
using System.IO;
using System.Linq;
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

        [UnityEditor.MenuItem("Ankhu/Realms/Capture Realm Cameras")]
        public static void CaptureAll()
        {
            Directory.CreateDirectory(OutDir);
            foreach (var script in WernesScripts.All())
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

        private static void Render(Camera cam, string file, int w, int h)
        {
            bool was = cam.enabled;
            cam.enabled = true;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            RenderTexture prev = cam.targetTexture;
            cam.targetTexture = rt;
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
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(tex);
        }
    }
}
