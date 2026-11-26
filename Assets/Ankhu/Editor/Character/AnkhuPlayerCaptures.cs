using System;
using System.IO;
using Ankhu.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Ankhu.EditorTools.Character
{
    /// <summary>
    /// Renders 1920x1080 stills of the Ankhu player model to Captures/ankhu_player at the project root,
    /// in Borrowed Dawn and the second teaching room (the first shares Borrowed Dawn's geometry and spawn): idle and mid-walk from the gameplay camera, a
    /// mid-walk side view, and a face close-up. Poses are sampled in AnimationMode, so no Play mode.
    ///
    /// Batch (needs graphics, so no -nographics):
    /// Unity -batchmode -quit -projectPath . -executeMethod Ankhu.EditorTools.Character.AnkhuPlayerCaptures.CaptureFromCommandLine -logFile capture.log
    /// </summary>
    public static class AnkhuPlayerCaptures
    {
        public const int Width = 1920;
        public const int Height = 1080;

        public static string OutputFolder => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Captures", "ankhu_player"));

        [MenuItem("Ankhu/Character/Capture Ankhu Player Stills")]
        public static void CaptureFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Leave Play mode before capturing.");
                return;
            }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                CaptureAll();
            }
        }

        public static void CaptureFromCommandLine()
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

        public static void CaptureAll()
        {
            Directory.CreateDirectory(OutputFolder);
            Capture(BorrowedDawnGreyboxBuilder.ScenePath, "borrowed_dawn");
            Capture(BorrowedDawnGreyboxBuilder.Sequence[1].ScenePath, "teach2");
        }

        public static void Capture(string scenePath, string prefix)
        {
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            PlayerController2D player = Object.FindAnyObjectByType<PlayerController2D>();
            if (player == null)
            {
                throw new InvalidOperationException("No player in " + scenePath);
            }
            AnkhuVisual visual = player.GetComponentInChildren<AnkhuVisual>(true);
            visual.Rebuild();
            // Facing right, as at spawn in play.
            visual.transform.localRotation = Quaternion.Euler(0f, visual.BaseYaw + 90f, 0f);
            Animator animator = visual.GetComponentInChildren<Animator>(true);
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                throw new InvalidOperationException("The player in " + scenePath + " has no animated Ankhu model. Run the character setup first.");
            }
            Camera game = Camera.main != null ? Camera.main : Object.FindAnyObjectByType<Camera>();

            AnimationClip idle = LoadClip(AnkhuCharacterSetup.IdleClip);
            AnimationClip walk = LoadClip(AnkhuCharacterSetup.WalkClip);
            Vector3 feet = player.transform.position;

            var closeGo = new GameObject("AnkhuCaptureCamera") { hideFlags = HideFlags.HideAndDontSave };
            Camera close = closeGo.AddComponent<Camera>();
            AnimationMode.StartAnimationMode();
            try
            {
                close.CopyFrom(game);
                close.orthographic = false;
                close.fieldOfView = 30f;
                close.nearClipPlane = 0.05f;
                close.farClipPlane = 200f;

                AnkhuCharacterSetup.SamplePose(animator.gameObject, idle, idle.length * 0.4f);
                Bounds body = AnkhuCharacterSetup.RendererBounds(animator.gameObject);
                Debug.Log(string.Format("[AnkhuCapture] {0}: player feet {1}, posed model bounds min {2} max {3}, height {4:0.000} m, visual yaw {5}",
                    prefix, feet.ToString("F3"), body.min.ToString("F3"), body.max.ToString("F3"), body.size.y, visual.transform.eulerAngles.y));
                Transform toes = animator.GetBoneTransform(HumanBodyBones.LeftToes);
                Transform foot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
                Debug.Log(string.Format("[AnkhuCapture] {0}: above the collider bottom, left toes {1:0.000} m, left ankle {2:0.000} m, head bone {3:0.000} m, model scale {4:0.000}",
                    prefix, toes != null ? toes.position.y - feet.y : float.NaN, foot.position.y - feet.y, head.position.y - feet.y, animator.transform.lossyScale.y));
                Vector2 span = AnkhuCharacterSetup.BakedHeightSpan(animator.gameObject);
                Debug.Log(string.Format("[AnkhuCapture] {0}: idle mesh above the collider bottom, lowest vertex {1:0.000} m, crown {2:0.000} m",
                    prefix, span.x - feet.y, span.y - feet.y));
                GameObject proxy = BakedProxy(animator.gameObject);
                Write(game, prefix + "_idle_game_camera.png");
                Frame(close, feet + new Vector3(0f, 1.5f, 0f), new Vector3(0.35f, -0.05f, -1f), 2.4f);
                Write(close, prefix + "_closeup.png");
                RemoveProxy(animator.gameObject, proxy);

                // Mid-stride: the sample with the widest gap between the feet, not the passing pose.
                Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                Transform rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
                float strideTime = 0f;
                float widest = -1f;
                for (int i = 0; i < 40; i++)
                {
                    float time = walk.length * i / 40f;
                    AnkhuCharacterSetup.SamplePose(animator.gameObject, walk, time);
                    Vector3 gap = leftFoot.position - rightFoot.position;
                    float spread = new Vector2(gap.x, gap.z).magnitude;
                    if (spread > widest)
                    {
                        widest = spread;
                        strideTime = time;
                    }
                }
                AnkhuCharacterSetup.SamplePose(animator.gameObject, walk, strideTime);
                Debug.Log(string.Format("[AnkhuCapture] {0}: mid-walk at {1:0.00} s of {2:0.00} s, feet {3:0.00} m apart",
                    prefix, strideTime, walk.length, widest));
                Vector2 walkSpan = AnkhuCharacterSetup.BakedHeightSpan(animator.gameObject);
                Debug.Log(string.Format("[AnkhuCapture] {0}: mid-walk mesh above the collider bottom, lowest vertex {1:0.000} m, crown {2:0.000} m",
                    prefix, walkSpan.x - feet.y, walkSpan.y - feet.y));
                proxy = BakedProxy(animator.gameObject);
                Write(game, prefix + "_midwalk_game_camera.png");
                Frame(close, feet + new Vector3(0f, 0.95f, 0f), new Vector3(0f, 0f, -1f), 5.5f);
                Write(close, prefix + "_midwalk_side.png");
                RemoveProxy(animator.gameObject, proxy);
            }
            finally
            {
                AnimationMode.StopAnimationMode();
                Object.DestroyImmediate(closeGo);
            }
        }

        private static AnimationClip LoadClip(string name)
        {
            string path = AnkhuCharacterImport.AnimationFolder + "/" + name + ".fbx";
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (o is AnimationClip c && !c.name.StartsWith("__preview__"))
                {
                    return c;
                }
            }
            throw new InvalidOperationException("No clip in " + path);
        }

        /// <summary>
        /// Skinned meshes are not re-skinned between an AnimationMode sample and Camera.Render in batch
        /// mode, so stills would show a stale pose. Bake each skinned mesh in the sampled pose into a
        /// plain mesh with the same materials and hide the skinned renderers while rendering.
        /// </summary>
        private static GameObject BakedProxy(GameObject model)
        {
            var proxy = new GameObject("AnkhuCaptureProxy") { hideFlags = HideFlags.HideAndDontSave };
            foreach (SkinnedMeshRenderer smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = new Mesh { hideFlags = HideFlags.HideAndDontSave };
                smr.BakeMesh(mesh, true);
                var part = new GameObject(smr.name) { hideFlags = HideFlags.HideAndDontSave };
                part.transform.SetParent(proxy.transform, false);
                part.transform.SetPositionAndRotation(smr.transform.position, smr.transform.rotation);
                part.AddComponent<MeshFilter>().sharedMesh = mesh;
                part.AddComponent<MeshRenderer>().sharedMaterials = smr.sharedMaterials;
                smr.enabled = false;
            }
            return proxy;
        }

        private static void RemoveProxy(GameObject model, GameObject proxy)
        {
            foreach (MeshFilter mf in proxy.GetComponentsInChildren<MeshFilter>(true))
            {
                Object.DestroyImmediate(mf.sharedMesh);
            }
            Object.DestroyImmediate(proxy);
            foreach (SkinnedMeshRenderer smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                smr.enabled = true;
            }
        }

        private static void Frame(Camera cam, Vector3 target, Vector3 fromDirection, float distance)
        {
            cam.transform.position = target + fromDirection.normalized * distance;
            cam.transform.LookAt(target);
        }

        private static void Write(Camera cam, string fileName)
        {
            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            RenderTexture previousTarget = cam.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            try
            {
                cam.targetTexture = rt;
                cam.Render();
                cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                tex.Apply();
                string path = Path.Combine(OutputFolder, fileName);
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Debug.Log("[AnkhuCapture] Wrote " + path);
            }
            finally
            {
                cam.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                Object.DestroyImmediate(tex);
                rt.Release();
                Object.DestroyImmediate(rt);
            }
        }
    }
}
