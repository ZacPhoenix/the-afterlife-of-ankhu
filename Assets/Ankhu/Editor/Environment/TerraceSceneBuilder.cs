using System;
using Ankhu.CameraRig;
using Ankhu.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ankhu.EditorTools.Environment
{
    /// <summary>
    /// Builds a walkable test scene for the awakening terrace plates: the five-layer parallax rig, a
    /// ground collider on the painted walk line, side walls, warm key light, Ankhu with his speech
    /// bubble, and an orthographic camera at the plates' native framing. No puzzle rules run here.
    /// The traversal test adapter answers speech so the bubble can be tried.
    /// </summary>
    public static class TerraceSceneBuilder
    {
        public const string ScenePath = BorrowedDawnGreyboxBuilder.ScenesFolder + "/Terrace_Parallax.unity";

        /// <summary>Feet at y = 0 so the controller's kill plane and step logic behave as in the greybox.</summary>
        public const float WalkLineY = 0f;
        public const float LevelMinX = -16f;
        public const float LevelMaxX = 16f;
        public static readonly Vector2 Spawn = new Vector2(-12f, WalkLineY);

        [MenuItem("Ankhu/Environment/Build Terrace Test Scene")]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }
            Build();
            Debug.Log("Terrace test scene built at " + ScenePath);
        }

        public static void BuildFromCommandLine()
        {
            try
            {
                Build();
                Debug.Log("Terrace test scene built at " + ScenePath);
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        public static void Build()
        {
            Debug.Log(UniversalPipelineSetup.EnsureUniversalPipeline());
            if (!AssetDatabase.IsValidFolder(BorrowedDawnGreyboxBuilder.ScenesFolder))
            {
                throw new InvalidOperationException("Run Ankhu > Build Borrowed Dawn Greybox once first, it creates " + BorrowedDawnGreyboxBuilder.ScenesFolder);
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            TerracePlateImporter.BuildRig(WalkLineY);

            var ground = new GameObject("Ground");
            var groundBox = ground.AddComponent<BoxCollider2D>();
            groundBox.size = new Vector2(LevelMaxX - LevelMinX, 2f);
            ground.transform.position = new Vector3((LevelMinX + LevelMaxX) * 0.5f, WalkLineY - 1f, 0f);

            for (int side = 0; side < 2; side++)
            {
                var wall = new GameObject(side == 0 ? "Wall_West" : "Wall_East");
                var box = wall.AddComponent<BoxCollider2D>();
                box.size = new Vector2(1f, 20f);
                float x = side == 0 ? LevelMinX - 0.5f : LevelMaxX + 0.5f;
                wall.transform.position = new Vector3(x, WalkLineY + 9f, 0f);
            }

            var sunGo = new GameObject("Key Light");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.82f, 0.62f);
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.Soft;
            sunGo.transform.rotation = Quaternion.Euler(35f, 40f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.16f, 0.19f, 0.30f);

            new GameObject("RoomRulesHost").AddComponent<RoomRulesHost>();

            var cameraGo = new GameObject("Main Camera");
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.04f, 0.05f, 0.10f);
            camera.orthographic = true;
            float halfHeight = TerracePlateImporter.FrameHeight * 0.5f;
            camera.orthographicSize = halfHeight;
            cameraGo.transform.position = new Vector3(Spawn.x, TerracePlateImporter.CameraYForWalkLine(WalkLineY), -10f);

            Ankhu.Player.PlayerRigFactory.Create(Spawn, camera);
            CameraFollow2D follow = cameraGo.GetComponent<CameraFollow2D>();
            if (follow != null)
            {
                follow.SetFraming(halfHeight, TerracePlateImporter.CameraYForWalkLine(WalkLineY), LevelMinX, LevelMaxX);
                follow.SetProjection(CameraFollow2D.Projection.Orthographic);
                follow.SnapToTarget();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new InvalidOperationException("Could not save scene at " + ScenePath);
            }
            AddToBuildSettings(ScenePath);
            AssetDatabase.SaveAssets();
        }

        private static void AddToBuildSettings(string path)
        {
            var scenes = EditorBuildSettings.scenes;
            foreach (EditorBuildSettingsScene s in scenes)
            {
                if (s.path == path)
                {
                    return;
                }
            }
            var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(scenes) { new EditorBuildSettingsScene(path, true) };
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
