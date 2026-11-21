using System;
using System.Collections.Generic;
using System.IO;
using Ankhu.EditorTools.Environment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using L = Ankhu.Layout.BorrowedDawnLayout;

namespace Ankhu.EditorTools
{
    /// <summary>
    /// Renders side-on orthographic frames of the generated scenes to PNG, so environment art can be
    /// laid out against real geometry. Frames are 1920x1080 and written to docs/captures/ at the
    /// project root. Batch mode needs graphics, so do not pass -nographics:
    /// Unity -batchmode -projectPath . -executeMethod Ankhu.EditorTools.FrameCapture.CaptureFromCommandLine
    /// </summary>
    public static class FrameCapture
    {
        public const int Width = 1920;
        public const int Height = 1080;
        public const string OutputFolder = "docs/captures";

        [MenuItem("Ankhu/Capture/Side-On Frames")]
        public static void CaptureFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }
            List<string> written = CaptureAll();
            Debug.Log("Captured " + written.Count + " frames into " + Path.GetFullPath(OutputFolder));
        }

        public static void CaptureFromCommandLine()
        {
            try
            {
                List<string> written = CaptureAll();
                foreach (string path in written)
                {
                    Debug.Log("Captured " + path);
                }
                EditorApplication.Exit(written.Count > 0 ? 0 : 1);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        public static List<string> CaptureAll()
        {
            Directory.CreateDirectory(OutputFolder);
            var written = new List<string>();

            if (File.Exists(BorrowedDawnGreyboxBuilder.ScenePath))
            {
                EditorSceneManager.OpenScene(BorrowedDawnGreyboxBuilder.ScenePath, OpenSceneMode.Single);
                float half = L.CameraOrthoSize * Width / (float)Height;
                float west = L.RoomMinX + half;
                float east = L.RoomMaxX - half;
                written.Add(Capture("borrowed_dawn_west", west, L.CameraFixedY, L.CameraOrthoSize));
                written.Add(Capture("borrowed_dawn_centre", (west + east) * 0.5f, L.CameraFixedY, L.CameraOrthoSize));
                written.Add(Capture("borrowed_dawn_east", east, L.CameraFixedY, L.CameraOrthoSize));
                float wholeHalfHeight = (L.RoomMaxX - L.RoomMinX) * 0.5f * Height / Width;
                written.Add(Capture("borrowed_dawn_whole_room", (L.RoomMinX + L.RoomMaxX) * 0.5f,
                    (L.BasinBottomY + L.CeilingY) * 0.5f + 0.5f, Mathf.Max(wholeHalfHeight, (L.CeilingY - L.BasinBottomY) * 0.5f + 1f)));
            }
            else
            {
                Debug.LogWarning("Greybox scene not found. Run Ankhu > Build Borrowed Dawn Greybox first.");
            }

            if (File.Exists(TerraceSceneBuilder.ScenePath))
            {
                EditorSceneManager.OpenScene(TerraceSceneBuilder.ScenePath, OpenSceneMode.Single);
                float cy = TerracePlateImporter.CameraYForWalkLine(TerraceSceneBuilder.WalkLineY);
                written.Add(Capture("terrace_spawn", TerraceSceneBuilder.Spawn.x + 7.4f, cy, TerracePlateImporter.FrameHeight * 0.5f));
            }
            return written;
        }

        /// <summary>Renders the scene's main camera, forced orthographic and side-on, at the given framing.</summary>
        private static string Capture(string name, float x, float y, float orthoSize)
        {
            Camera cam = Camera.main != null ? Camera.main : UnityEngine.Object.FindAnyObjectByType<Camera>();
            if (cam == null)
            {
                throw new InvalidOperationException("No camera in the open scene.");
            }

            Vector3 position = cam.transform.position;
            Quaternion rotation = cam.transform.rotation;
            bool orthographic = cam.orthographic;
            float size = cam.orthographicSize;
            float far = cam.farClipPlane;
            RenderTexture previousTarget = cam.targetTexture;

            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var image = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            RenderTexture previousActive = RenderTexture.active;
            try
            {
                cam.transform.SetPositionAndRotation(new Vector3(x, y, -30f), Quaternion.identity);
                cam.orthographic = true;
                cam.orthographicSize = orthoSize;
                cam.farClipPlane = 200f;
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                image.Apply();
                string path = Path.Combine(OutputFolder, name + ".png");
                File.WriteAllBytes(path, image.EncodeToPNG());
                return path;
            }
            finally
            {
                RenderTexture.active = previousActive;
                cam.targetTexture = previousTarget;
                cam.transform.SetPositionAndRotation(position, rotation);
                cam.orthographic = orthographic;
                cam.orthographicSize = size;
                cam.farClipPlane = far;
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
                UnityEngine.Object.DestroyImmediate(image);
            }
        }
    }
}
