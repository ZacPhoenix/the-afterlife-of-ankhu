using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

namespace Ankhu.Opening.EditorTools
{
    /// <summary>
    /// Builds the opening slice: Opening_Title, Opening_Cinematic and Opening_WalkDuat, the Anpu escort
    /// controller, Build Settings (new scenes first) and the Windows player in Builds/OpeningSlice.
    /// Every art input is optional: a missing painting falls back to the greybox, a missing video or clip is
    /// left empty, so the slice always builds and gets better as art lands.
    /// Command line (graphics needed for captures, so no -nographics):
    ///   -executeMethod Ankhu.Opening.EditorTools.OpeningSceneBuilder.BuildAllCli
    ///   -executeMethod Ankhu.Opening.EditorTools.OpeningSceneBuilder.CaptureWalkGreyboxCli
    /// </summary>
    public static class OpeningSceneBuilder
    {
        public const string Root = "Assets/Ankhu/Opening";
        public const string SceneDir = "Assets/Ankhu/Generated/Scenes/Opening";
        public const string TitlePath = SceneDir + "/" + OpeningSettings.TitleScene + ".unity";
        public const string CinematicPath = SceneDir + "/" + OpeningSettings.CinematicScene + ".unity";
        public const string WalkScenePath = SceneDir + "/" + OpeningSettings.WalkScene + ".unity";
        public const string GeneratedDir = Root + "/Generated";
        public const string TitleArtDir = Root + "/Art/Title";
        public const string WalkArtDir = Root + "/Art/Walk";
        public const string LogoPath = Root + "/Art/Logo.png";
        public const string VideoPath = Root + "/Video/Opening.mp4";
        public const string MusicPath = Root + "/Audio/NewIntro.wav";
        public const string TitleAmbientPath = Root + "/Audio/title_ambience.wav";
        public const string WalkAmbiencePath = Root + "/Audio/walk_ambience.wav";
        public const string FootstepDir = Root + "/Audio/Footsteps";
        public const string AnpuEscortControllerPath = GeneratedDir + "/Anpu_Escort.controller";
        public const string BuildDir = "Builds/OpeningSlice";
        public const string ExeName = "AfterlifeOfAnkhu.exe";
        public const string GreyboxCaptureDir = "art_gen/walk/greybox";

        private const string AnkhuPrefab = "Assets/Characters/Ankhu/Ankhu.prefab";
        private const string AnpuPrefab = "Assets/Characters/Anpu/Anpu.prefab";
        private const string GuardianPrefab = "Assets/Characters/Guardian/Guardian.prefab";
        private const string AnkhuController = "Assets/Characters/Ankhu/Ankhu.controller";
        private const string AnpuController = "Assets/Characters/Anpu/Anpu.controller";
        private const string AnkhuSpell25Line = "Assets/Ankhu/Audio/Dialogue/Ankhu/Clips/dialogue.ankhu.spell25.anpu__0.wav";
        private const string AnpuSpell25Rise = "Assets/Ankhu/Audio/Dialogue/Anpu/Clips/dialogue.anpu.spell25.rise__0.wav";
        private const string WalkAnkhuClips = "Assets/Ankhu/Audio/Dialogue/WalkAnkhu/Clips";
        private const string WalkAnpuClips = "Assets/Ankhu/Audio/Dialogue/WalkAnpu/Clips";

        /// <summary>Layer for greybox geometry. Hidden from a camera once that camera has its painting.</summary>
        public const int GreyboxLayer = 29;
        // Paintings are made at 3:2 (the image model's landscape size). The capture keeps the 16:9 game camera's
        // horizontal field of view and adds height, so BackdropFit's cover scaling puts the painting back exactly
        // on the 3D ground at 16:9.
        public const int CaptureWidth = 1536;
        public const int CaptureHeight = 1024;
        public const float GameAspect = 16f / 9f;
        private const float BackdropDistance = 300f;

        // ---- Walk layout (flat, y = 0). See docs/opening/OVERNIGHT_PLAN.md. ----
        public static readonly Vector3[] PathPoints =
        {
            new Vector3(0f, 0f, -2f), new Vector3(0f, 0f, 10f), new Vector3(2f, 0f, 18f), new Vector3(10f, 0f, 24f),
            new Vector3(22f, 0f, 26f), new Vector3(30f, 0f, 34f), new Vector3(34f, 0f, 46f), new Vector3(35.3f, 0f, 46.8f),
        };

        public struct CamSpec
        {
            public string name;
            public Vector3 position;
            public Vector3 lookAt;
            public float fov;
            public float startS;
            // Lens zoom into the painting (1 = as painted). The camera keeps its pose, so a narrower field of view
            // crops the painting about its centre and stays exactly on the 3D ground.
            public float zoom;
        }

        public static readonly CamSpec[] Cameras =
        {
            new CamSpec { name = "cam1_temple_exit", position = new Vector3(-1.8f, 4.2f, -3f), lookAt = new Vector3(1f, 1f, 6f), fov = 42f, startS = 0f },
            new CamSpec { name = "cam2_look_back", position = new Vector3(1.5f, 1.0f, 17f), lookAt = new Vector3(0f, 1.3f, 6f), fov = 38f, startS = 9f },
            new CamSpec { name = "cam3_wide_side", position = new Vector3(14f, 3f, 16f), lookAt = new Vector3(5f, 1f, 22f), fov = 50f, startS = 14f },
            new CamSpec { name = "cam4_follow_bend", position = new Vector3(6f, 2.5f, 20f), lookAt = new Vector3(24f, 1f, 27f), fov = 36f, startS = 32f },
            new CamSpec { name = "cam5_high", position = new Vector3(20f, 9f, 42f), lookAt = new Vector3(30f, -0.6f, 36f), fov = 44f, startS = 52f, zoom = 1.35f },
            new CamSpec { name = "cam6_vista", position = new Vector3(27f, 2.2f, 36f), lookAt = new Vector3(44f, 1.5f, 54f), fov = 46f, startS = 61f, zoom = 1.25f },
        };

        private const float RoadHalfWidth = 2.2f;

        // ------------------------------------------------------------------------------------------------
        // Entry points
        // ------------------------------------------------------------------------------------------------

        [UnityEditor.MenuItem("Ankhu/Opening/Build Scenes")]
        public static void BuildScenesMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildScenes();
        }

        [UnityEditor.MenuItem("Ankhu/Opening/Build Scenes And Player")]
        public static void BuildAllMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildScenes();
            BuildPlayer();
        }

        [UnityEditor.MenuItem("Ankhu/Opening/Capture Walk Greybox")]
        public static void CaptureMenu() { CaptureWalkGreybox(); }

        public static void BuildScenesCli() { RunCli(BuildScenes); }
        public static void BuildAllCli() { RunCli(() => { BuildScenes(); BuildPlayer(); }); }
        public static void BuildPlayerCli() { RunCli(BuildPlayer); }
        public static void CaptureWalkGreyboxCli() { RunCli(() => { BuildScenes(); CaptureWalkGreybox(); }); }

        private static void RunCli(Action a)
        {
            try
            {
                a();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        public static void BuildScenes()
        {
            EnsureFolder(SceneDir);
            EnsureFolder(GeneratedDir);
            EnsureFolder(TitleArtDir);
            EnsureFolder(WalkArtDir);
            EnsureFolder(Root + "/Video");
            EnsureFolder(FootstepDir);
            ConfigureImports();
            BuildAnpuEscortController();
            BuildTitle();
            BuildCinematic();
            BuildWalk();
            SetBuildSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[Opening] Scenes built.");
        }

        // ------------------------------------------------------------------------------------------------
        // Imports
        // ------------------------------------------------------------------------------------------------

        private static void ConfigureImports()
        {
            AssetDatabase.Refresh();
            var textures = new List<string>();
            textures.AddRange(PngsIn(TitleArtDir));
            textures.AddRange(PngsIn(WalkArtDir));
            textures.AddRange(PngsIn(WalkArtDir + "/Foreground"));
            if (File.Exists(LogoPath)) textures.Add(LogoPath);
            foreach (string extra in new[] { Root + "/Art/Logo_Pool.png", Root + "/Art/Title_WingedSunEmblem.png", Root + "/Art/Title_AnkhDivider.png", Root + "/Art/DustPuff.png" })
                if (File.Exists(extra)) textures.Add(extra);
            foreach (string path in textures)
            {
                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null) continue;
                bool dirty = ti.textureType != TextureImporterType.Default || ti.mipmapEnabled || ti.maxTextureSize != 4096
                             || ti.npotScale != TextureImporterNPOTScale.None || ti.textureCompression != TextureImporterCompression.CompressedHQ;
                if (!dirty) continue;
                ti.textureType = TextureImporterType.Default;
                ti.mipmapEnabled = false;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.npotScale = TextureImporterNPOTScale.None;
                ti.maxTextureSize = 4096;
                ti.alphaIsTransparency = true;
                ti.textureCompression = TextureImporterCompression.CompressedHQ;
                ti.SaveAndReimport();
            }
            foreach (string path in new[] { MusicPath, TitleAmbientPath, WalkAmbiencePath })
            {
                var ai = AssetImporter.GetAtPath(path) as AudioImporter;
                if (ai == null) continue;
                AudioImporterSampleSettings s = ai.defaultSampleSettings;
                // Long music and loops stream, and the music must not be resampled or the DSP sync drifts.
                if (s.loadType == AudioClipLoadType.Streaming && s.compressionFormat == AudioCompressionFormat.Vorbis) continue;
                s.loadType = AudioClipLoadType.Streaming;
                s.compressionFormat = AudioCompressionFormat.Vorbis;
                s.quality = 0.85f;
                s.preloadAudioData = true;
                ai.defaultSampleSettings = s;
                ai.SaveAndReimport();
            }
        }

        private static IEnumerable<string> PngsIn(string dir)
        {
            if (!Directory.Exists(dir)) return Enumerable.Empty<string>();
            return Directory.GetFiles(dir, "*.png").Select(p => p.Replace('\\', '/')).OrderBy(p => p, StringComparer.Ordinal);
        }

        // ------------------------------------------------------------------------------------------------
        // Anpu escort controller: Ankhu's locomotion clips on Anpu's avatar, plus his staff-holding arm.
        // ------------------------------------------------------------------------------------------------

        public static AnimatorController BuildAnpuEscortController()
        {
            var ankhu = AssetDatabase.LoadAssetAtPath<AnimatorController>(AnkhuController);
            var anpu = AssetDatabase.LoadAssetAtPath<AnimatorController>(AnpuController);
            if (ankhu == null) throw new InvalidOperationException("Missing " + AnkhuController);

            BlendTree source = ankhu.layers[0].stateMachine.states
                .Select(s => s.state).Where(s => s.name == "Locomotion").Select(s => s.motion as BlendTree).FirstOrDefault();
            if (source == null) throw new InvalidOperationException("Ankhu.controller has no Locomotion blend tree.");

            AssetDatabase.DeleteAsset(AnpuEscortControllerPath);
            AnimatorController ac = AnimatorController.CreateAnimatorControllerAtPath(AnpuEscortControllerPath);
            ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ac.AddParameter(new AnimatorControllerParameter { name = "Grounded", type = AnimatorControllerParameterType.Bool, defaultBool = true });
            ac.AddParameter("Speak", AnimatorControllerParameterType.Trigger);

            AnimatorState loco = ac.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
            loco.iKOnFeet = true; // Same fix as Ankhu: without Foot IK the retarget rolls the left foot at heel strike.
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            foreach (ChildMotion c in source.children) tree.AddChild(c.motion, c.threshold);
            ChildMotion[] copy = tree.children;
            for (int i = 0; i < copy.Length; i++) copy[i].timeScale = source.children[i].timeScale;
            tree.children = copy;
            ac.layers[0].stateMachine.defaultState = loco;
            { var ls = ac.layers; ls[0].iKPass = true; ac.layers = ls; } // For FootFloorClamp.

            AnimatorControllerLayer hold = anpu != null ? anpu.layers.FirstOrDefault(l => l.name == "HoldStaff") : null;
            Motion holdMotion = hold != null && hold.stateMachine.defaultState != null ? hold.stateMachine.defaultState.motion : null;
            if (holdMotion != null)
            {
                ac.AddLayer("HoldStaff");
                AnimatorControllerLayer[] layers = ac.layers;
                layers[1].defaultWeight = 1f;
                layers[1].avatarMask = hold.avatarMask;
                layers[1].blendingMode = AnimatorLayerBlendingMode.Override;
                ac.layers = layers;
                AnimatorState h = ac.layers[1].stateMachine.AddState("Hold");
                h.motion = holdMotion;
                ac.layers[1].stateMachine.defaultState = h;
            }
            else
            {
                Debug.LogWarning("[Opening] Anpu.controller has no HoldStaff layer, escort walks without the staff pose.");
            }
            EditorUtility.SetDirty(ac);
            AssetDatabase.SaveAssets();
            return ac;
        }

        // ------------------------------------------------------------------------------------------------
        // Title
        // ------------------------------------------------------------------------------------------------

        private static void BuildTitle()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Camera cam = MakeUiCamera();
            cam.gameObject.AddComponent<AudioListener>();

            var go = new GameObject("TitleMenu");
            var menu = go.AddComponent<TitleMenu>();
            var so = new SerializedObject(menu);
            SerializedProperty layers = so.FindProperty("layers");
            string[] files = PngsIn(TitleArtDir).ToArray();
            layers.arraySize = files.Length;
            for (int i = 0; i < files.Length; i++)
            {
                // Names like 03_columns_d70_f00.png or 05_brazier_glow_d70_f60_add.png.
                string n = Path.GetFileNameWithoutExtension(files[i]).ToLowerInvariant();
                SerializedProperty e = layers.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("texture").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(files[i]);
                e.FindPropertyRelative("depth").floatValue = Token(n, 'd', files.Length > 1 ? i / (float)(files.Length - 1) : 0.5f);
                e.FindPropertyRelative("flicker").floatValue = Token(n, 'f', 0f);
                e.FindPropertyRelative("additive").boolValue = n.Contains("_add");
            }
            so.FindProperty("logo").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(LogoPath);
            so.FindProperty("logoPool").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Art/Logo_Pool.png");
            so.FindProperty("wingedSun").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Art/Title_WingedSunEmblem.png");
            so.FindProperty("ankhDivider").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Art/Title_AnkhDivider.png");
            so.FindProperty("ambient").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(TitleAmbientPath);
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, TitlePath);
            Debug.Log("[Opening] Title: " + files.Length + " layers, logo " + (File.Exists(LogoPath) ? "found" : "missing"));
        }

        /// <summary>Reads a token like d70 (0.70) from a layer file name.</summary>
        private static float Token(string name, char key, float fallback)
        {
            foreach (string part in name.Split('_'))
            {
                if (part.Length >= 2 && part[0] == key && int.TryParse(part.Substring(1), out int v)) return v / 100f;
            }
            return fallback;
        }

        private static Camera MakeUiCamera()
        {
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.cullingMask = 0;
            return cam;
        }

        // ------------------------------------------------------------------------------------------------
        // Cinematic
        // ------------------------------------------------------------------------------------------------

        private static void BuildCinematic()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Camera cam = MakeUiCamera();
            cam.gameObject.AddComponent<AudioListener>();

            var go = new GameObject("OpeningCinematic");
            var cine = go.AddComponent<OpeningCinematic>();
            var so = new SerializedObject(cine);
            so.FindProperty("video").objectReferenceValue = AssetDatabase.LoadAssetAtPath<VideoClip>(VideoPath);
            so.FindProperty("music").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(MusicPath);
            so.FindProperty("musicStart").floatValue = 0f;
            so.FindProperty("handoff").floatValue = 129.67f;

            var cues = new List<(float time, string clip, string text)>
            {
                (110.875f, AnkhuSpell25Line, "My Lord... Anpu."),
                (116.584f, AnpuSpell25Rise, "Rise, Ankhu."),
            };
            SerializedProperty arr = so.FindProperty("cues");
            arr.arraySize = cues.Count;
            for (int i = 0; i < cues.Count; i++)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(cues[i].clip);
                SerializedProperty e = arr.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("time").floatValue = cues[i].time;
                e.FindPropertyRelative("voice").objectReferenceValue = clip;
                e.FindPropertyRelative("subtitle").stringValue = cues[i].text;
                e.FindPropertyRelative("hold").floatValue = (clip != null ? clip.length : 2f) + 0.6f;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.SaveScene(scene, CinematicPath);
            Debug.Log("[Opening] Cinematic: video " + (File.Exists(VideoPath) ? "found" : "missing") + ", music " + (File.Exists(MusicPath) ? "found" : "missing"));
        }

        // ------------------------------------------------------------------------------------------------
        // Walk_Duat
        // ------------------------------------------------------------------------------------------------

        [Serializable]
        private class LineFile { public LineEntry[] lines; }

        [Serializable]
        private class LineEntry { public string key; public string text; }

        // A second renderer, used only by the walk cameras, that runs the Kuwahara paint filter over the finished frame.
        // The default renderer (index 0) and every other scene are untouched. Returns the renderer's index, or -1.
        private const bool PaintFilterOn = false;
        private const string PaintRendererPath = GeneratedDir + "/Opening_Paint_Renderer.asset";

        private static int EnsurePaintRenderer()
        {
            var pipeline = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline == null) return -1;
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(PaintRendererPath);
            if (data == null)
            {
                var source = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Ankhu/Settings/Ankhu_URP_Renderer.asset");
                if (source == null || !AssetDatabase.CopyAsset("Assets/Ankhu/Settings/Ankhu_URP_Renderer.asset", PaintRendererPath)) return -1;
                data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(PaintRendererPath);
            }
            Shader shader = Shader.Find("Ankhu/PaintFilter");
            if (data == null || shader == null) return -1;
            string matPath = GeneratedDir + "/PaintFilter.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, matPath); }
            mat.shader = shader;
            mat.SetFloat("_Radius", 3f);
            mat.SetFloat("_Strength", 1f);
            EditorUtility.SetDirty(mat);

            var feature = data.rendererFeatures.OfType<FullScreenPassRendererFeature>().FirstOrDefault();
            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
                feature.name = "PaintFilter";
                AssetDatabase.AddObjectToAsset(feature, data);
                data.rendererFeatures.Add(feature);
            }
            feature.passMaterial = mat;
            feature.passIndex = 0;
            feature.fetchColorBuffer = true;
            feature.injectionPoint = FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingPostProcessing;
            feature.SetActive(true);
            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            typeof(ScriptableRendererData).GetMethod("ValidateRendererFeatures", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.Invoke(data, null);
            EditorUtility.SetDirty(data);

            var so = new SerializedObject(pipeline);
            SerializedProperty list = so.FindProperty("m_RendererDataList");
            for (int i = 0; i < list.arraySize; i++)
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == data) { AssetDatabase.SaveAssets(); return i; }
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = data;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);
            AssetDatabase.SaveAssets();
            return list.arraySize - 1;
        }

        private static void BuildWalk()
        {
            int paintRenderer = PaintFilterOn ? EnsurePaintRenderer() : -1;
            Debug.Log("[Opening] Walk paint renderer index " + paintRenderer);
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Moonlight: cool key from the upper right of most frames, low cool ambient, no fog (paintings carry the air).
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.30f, 0.36f, 0.52f);
            RenderSettings.ambientEquatorColor = new Color(0.20f, 0.21f, 0.27f);
            RenderSettings.ambientGroundColor = new Color(0.12f, 0.10f, 0.09f);
            RenderSettings.fog = false;
            RenderSettings.skybox = null;
            var moon = new GameObject("Moon").AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = new Color(0.70f, 0.78f, 1f);
            moon.intensity = 0.9f;
            moon.shadows = LightShadows.Soft;
            moon.transform.rotation = Quaternion.Euler(38f, -135f, 0f);
            var warm = new GameObject("TempleGlow").AddComponent<Light>();
            warm.type = LightType.Point;
            warm.color = new Color(1f, 0.62f, 0.32f);
            warm.range = 14f;
            warm.intensity = 2.2f;
            warm.transform.position = new Vector3(0f, 3f, -5f);
            // Warm torch fill from the camera side; ShotLook turns it and the moon on every cut.
            var torchFill = new GameObject("TorchFill").AddComponent<Light>();
            torchFill.type = LightType.Directional;
            torchFill.color = new Color(1f, 0.66f, 0.40f);
            torchFill.intensity = 0.95f;
            torchFill.shadows = LightShadows.None;
            // Amber rim from the braziers ahead of the group, so the figures pick up the plate's torch edge light.
            var torchRim = new GameObject("TorchRim").AddComponent<Light>();
            torchRim.type = LightType.Directional;
            torchRim.color = new Color(1f, 0.58f, 0.28f);
            torchRim.intensity = 1.1f;
            torchRim.shadows = LightShadows.None;

            var pathGo = new GameObject("Road");
            var path = pathGo.AddComponent<Ankhu.Opening.WalkPath>();
            path.SetPoints(PathPoints);

            var greybox = new GameObject("Greybox");
            BuildGreybox(greybox.transform);
            BuildCollision(path);

            // Characters.
            // The group starts a few metres down the road so the first frame of the walk already has them in it.
            float startS = 3.0f;
            Vector3 startPos = path.PointAt(startS);
            Quaternion startRot = Quaternion.LookRotation(path.TangentAt(startS), Vector3.up);

            GameObject ankhu = MakeCharacter("Ankhu", AnkhuPrefab, startPos, startRot, null, out Animator ankhuAnim);
            var cc = ankhu.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.3f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.stepOffset = 0.3f;
            cc.skinWidth = 0.03f;
            var player = ankhu.AddComponent<WalkPlayer>();

            var escortController = AssetDatabase.LoadAssetAtPath<AnimatorController>(AnpuEscortControllerPath);
            // Procession: Anpu leads a couple of metres down the road, the two attendants walk behind him staggered left
            // and right, and Ankhu follows. Each walker gets its own start phase and cadence so no two feet ever land together.
            const float anpuLat = -0.55f;
            var formation = new (string name, string prefab, RuntimeAnimatorController ctrl, float along, float lateral, float delay, float phase, float cadence, float shadow)[]
            {
                ("Anpu", AnpuPrefab, escortController, 2.6f, anpuLat, 0.4f, 0f, 1f, 0.62f),
                ("Attendant_L", GuardianPrefab, null, 2.6f - 1.2f, anpuLat - 0.7f, 0.6f, 0.17f, 0.96f, 0.55f),
                ("Attendant_R", GuardianPrefab, null, 2.6f - 1.45f, anpuLat + 0.7f, 0.85f, 0.43f, 1.04f, 0.55f),
            };
            var escorts = new List<(Escort e, Animator a, float delay, float phase, float cadence)>();
            var people = new List<(Transform t, float r)> { (ankhu.transform, 0.5f) };
            foreach (var f in formation)
            {
                Vector3 p = path.OffsetPoint(startS + f.along, f.lateral);
                GameObject go = MakeCharacter(f.name, f.prefab, p, startRot, f.ctrl, out Animator anim);
                if (go == null) continue;
                if (f.name == "Anpu") MeasureStaff(go.AddComponent<PropUpright>());
                var e = go.AddComponent<Escort>();
                escorts.Add((e, anim, f.delay, f.phase, f.cadence));
                people.Add((go.transform, f.shadow));
            }

            // Cameras and director.
            var director = new GameObject("CameraDirector").AddComponent<FixedCameraDirector>();
            var shots = new FixedCameraDirector.Shot[Cameras.Length];
            int painted = 0;
            for (int i = 0; i < Cameras.Length; i++)
            {
                CamSpec spec = Cameras[i];
                var camGo = new GameObject(spec.name);
                camGo.transform.SetParent(director.transform, false);
                camGo.transform.position = spec.position;
                camGo.transform.rotation = Quaternion.LookRotation(spec.lookAt - spec.position, Vector3.up);
                var cam = camGo.AddComponent<Camera>();
                cam.fieldOfView = spec.fov;
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = BackdropDistance + 40f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.035f, 0.045f, 0.08f);
                if (i > 0) cam.enabled = false;
                if (paintRenderer > 0) cam.GetUniversalAdditionalCameraData().SetRenderer(paintRenderer);

                Texture2D painting = AssetDatabase.LoadAssetAtPath<Texture2D>(WalkArtDir + "/" + spec.name + ".png");
                bool hasPainting = painting != null;
                if (hasPainting)
                {
                    painted++;
                    cam.cullingMask &= ~(1 << GreyboxLayer);
                    var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    quad.name = "Backdrop";
                    UnityEngine.Object.DestroyImmediate(quad.GetComponent<Collider>());
                    quad.transform.SetParent(camGo.transform, false);
                    var mr = quad.GetComponent<MeshRenderer>();
                    mr.sharedMaterial = BackdropMaterial(spec.name, painting);
                    mr.shadowCastingMode = ShadowCastingMode.Off;
                    mr.receiveShadows = false;
                    quad.AddComponent<BackdropFit>().Configure(cam, BackdropDistance, painting.width / (float)painting.height);
                    AddForegroundMatte(spec, cam, greybox.transform, painting.width / (float)painting.height);
                    // Wide shots made the cast tiny. Zoom after the backdrop and mattes are sized from the painted lens.
                    if (spec.zoom > 1f)
                        cam.fieldOfView = 2f * Mathf.Atan(Mathf.Tan(spec.fov * 0.5f * Mathf.Deg2Rad) / spec.zoom) * Mathf.Rad2Deg;
                }
                // A painted frame is locked: any pan would slide the painting off the invisible ground.
                shots[i] = new FixedCameraDirector.Shot { name = spec.name, camera = cam, follow = hasPainting ? 0f : 0.12f, aimHeight = 1.2f };
            }
            // Painted frames hide the greybox only through their camera masks; the greybox stays for unpainted ones.
            SetLayerRecursive(greybox, GreyboxLayer);
            director.Configure(shots, ankhu.transform);

            // The paintings carry the set's own shadows, so the hidden greybox must not cast a second set. The
            // figures' real moon shadows land on an invisible catcher plane at road height.
            if (painted == Cameras.Length)
                foreach (Renderer r in greybox.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = ShadowCastingMode.Off;
            var catcher = GameObject.CreatePrimitive(PrimitiveType.Quad);
            catcher.name = "ShadowCatcher";
            UnityEngine.Object.DestroyImmediate(catcher.GetComponent<Collider>());
            catcher.transform.position = new Vector3(20f, 0.004f, 30f);
            catcher.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            catcher.transform.localScale = new Vector3(160f, 160f, 1f);
            var catcherMr = catcher.GetComponent<MeshRenderer>();
            catcherMr.sharedMaterial = ShadowCatcherMaterial();
            catcherMr.shadowCastingMode = ShadowCastingMode.Off;
            catcherMr.receiveShadows = true;

            var look = new GameObject("ShotLook").AddComponent<ShotLook>();
            look.Configure(director, moon, torchFill, torchRim, Cameras.Select(SampleLook).ToArray(),
                new[] { ankhu.transform }.Concat(escorts.Select(e => e.e.transform)).ToArray());
            look.SetAttendantSkin(AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Ankhu/Opening/Art/Characters/Guardian_Opening_BaseColor.png"));
            look.SetAnpuSkin(AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Ankhu/Opening/Art/Characters/Anpu_Opening_BaseColor.png"));
            look.SetAnkhuSkin(AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Ankhu/Opening/Art/Characters/Ankhu_Opening_BaseColor.png"));
            look.SetPaintedShader(AssetDatabase.LoadAssetAtPath<Shader>("Assets/Ankhu/Opening/Runtime/PaintedCharacter.shader"));

            var listener = new GameObject("Listener");
            listener.AddComponent<AudioListener>();

            var seqGo = new GameObject("WalkSequence");
            var seq = seqGo.AddComponent<WalkSequence>();
            seq.Configure(AssetDatabase.LoadAssetAtPath<AudioClip>(WalkAmbiencePath), player);

            AudioClip[] steps = Directory.Exists(FootstepDir)
                ? Directory.GetFiles(FootstepDir, "*.wav").Select(p => AssetDatabase.LoadAssetAtPath<AudioClip>(p.Replace('\\', '/'))).Where(c => c != null).ToArray()
                : new AudioClip[0];
            player.Configure(director, ankhuAnim, steps);
            foreach (var e in escorts) e.e.Configure(path, player, e.a, e.delay, e.phase, e.cadence);
            // Anpu eases to a stop a metre short of each cut point, looks back, and walks on once Ankhu is within 3 m.
            // At the gate he stops at the road's left edge. The attendants fall in behind him, reacting a beat late.
            Escort anpuEscort = escorts.Select(e => e.e).FirstOrDefault(e => e.name == "Anpu");
            if (anpuEscort != null)
            {
                float[] pauses = Cameras.Skip(1).Select(c => c.startS - 1f).ToArray();
                // cam2 looks back up the road from a lens on it, so a stop a metre short of the next cut put Anpu right
                // in front of the lens, covering the shot. He stops further up the frame there instead.
                pauses[1] = Cameras[1].startS + 2.8f;
                anpuEscort.SetLeader(pauses, path.Length - 1.4f, -0.85f, anpuLat);
                player.SetLeader(anpuEscort, path);
                foreach (var e in escorts)
                {
                    if (e.e == anpuEscort) continue;
                    bool left = e.e.name.EndsWith("_L");
                    e.e.SetFollower(anpuEscort, left ? 1.2f : 1.45f, left ? -0.7f : 0.7f, left ? 0.25f : 0.4f, 1.9f, 1.3f, left ? 1 : 2);
                }
            }

            // Footfall dust: one shared particle system, a FootDust on each walker.
            var dustGo = new GameObject("FootDust");
            var dustPs = dustGo.AddComponent<ParticleSystem>();
            dustPs.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var dm = dustPs.main;
            dm.playOnAwake = false; dm.loop = false; dm.simulationSpace = ParticleSystemSimulationSpace.World;
            dm.maxParticles = 1200; dm.gravityModifier = 0f;
            var de = dustPs.emission; de.enabled = false;
            var dsh = dustPs.shape; dsh.enabled = false;
            var dsz = dustPs.sizeOverLifetime; dsz.enabled = true; dsz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 1.6f));
            var dcol = dustPs.colorOverLifetime; dcol.enabled = true;
            var dg = new Gradient();
            dg.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
            dcol.color = dg;
            var dvel = dustPs.limitVelocityOverLifetime; dvel.enabled = true; dvel.dampen = 0.15f; dvel.limit = 0.05f;
            string dustMatPath = GeneratedDir + "/foot_dust.mat";
            Shader dustSh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            var dustMat = AssetDatabase.LoadAssetAtPath<Material>(dustMatPath);
            if (dustMat == null) { dustMat = new Material(dustSh); AssetDatabase.CreateAsset(dustMat, dustMatPath); }
            dustMat.shader = dustSh;
            dustMat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Art/DustPuff.png"));
            dustMat.SetFloat("_Surface", 1f); dustMat.SetFloat("_Blend", 0f);
            dustMat.SetOverrideTag("RenderType", "Transparent");
            dustMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            dustMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            dustMat.SetInt("_ZWrite", 0);
            dustMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            dustMat.renderQueue = 3000;
            EditorUtility.SetDirty(dustMat);
            var dustR = dustGo.GetComponent<ParticleSystemRenderer>();
            dustR.sharedMaterial = dustMat; dustR.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
            dustR.shadowCastingMode = ShadowCastingMode.Off; dustR.receiveShadows = false;
            ankhu.AddComponent<FootDust>().Configure(dustPs, ankhuAnim, 0.72f);
            foreach (var e in escorts) e.e.gameObject.AddComponent<FootDust>().Configure(dustPs, e.a, 0.72f);

            // Blob shadows (the painted ground cannot receive real shadows).
            Material blob = BlobMaterial();
            foreach (var person in people)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                q.name = person.t.name + "_Shadow";
                UnityEngine.Object.DestroyImmediate(q.GetComponent<Collider>());
                var mr = q.GetComponent<MeshRenderer>();
                mr.sharedMaterial = blob;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
                // Contact darkening right under the feet only. The moon shadow from the catcher does the rest, so a wide
                // blob read as a second, detached shadow.
                q.AddComponent<BlobShadow>().Configure(person.t, 0f, person.r * 0.48f, 0.8f);
            }

            // Progress: cuts and dialogue.
            float[] thresholds = Cameras.Select(c => c.startS).ToArray();
            var progress = new GameObject("WalkProgress").AddComponent<WalkProgress>();
            progress.Configure(path, player, director, seq, thresholds, BuildBeats());

            EditorSceneManager.SaveScene(scene, WalkScenePath);
            Debug.Log(string.Format("[Opening] Walk: path {0:0.0} m, {1} cameras ({2} painted), {3} escorts, {4} footsteps",
                path.Length, Cameras.Length, painted, escorts.Count, steps.Length));
        }

        private static WalkProgress.Beat[] BuildBeats()
        {
            Dictionary<string, string> text = ReadLines("tools/voice/walk/walk_ankhu_lines.json")
                .Concat(ReadLines("tools/voice/walk/walk_anpu_lines.json")).ToDictionary(k => k.Key, k => k.Value);

            WalkProgress.Line L(string key, bool ankhu)
            {
                string dir = ankhu ? WalkAnkhuClips : WalkAnpuClips;
                return new WalkProgress.Line
                {
                    ankhu = ankhu,
                    clip = AssetDatabase.LoadAssetAtPath<AudioClip>(dir + "/" + key + "__0.wav"),
                    text = text.TryGetValue(key, out string t) ? t : "",
                };
            }

            // Each exchange starts a little after its shot opens, so the cut lands first and the words follow.
            return new[]
            {
                new WalkProgress.Beat { name = "cam2", start = Cameras[1].startS + 1.5f, lines = new[] { L("dialogue.ankhu.walk.cam2.a", true), L("dialogue.anpu.walk.cam2.b", false) } },
                new WalkProgress.Beat { name = "cam3", start = Cameras[2].startS + 3.0f, lines = new[] { L("dialogue.ankhu.walk.cam3.a", true), L("dialogue.anpu.walk.cam3.b", false) } },
                new WalkProgress.Beat { name = "cam4", start = Cameras[3].startS + 1.5f, lines = new[] { L("dialogue.ankhu.walk.cam4.a", true), L("dialogue.anpu.walk.cam4.b", false) } },
                new WalkProgress.Beat { name = "cam5", start = Cameras[4].startS + 0.5f, lines = new[] { L("dialogue.ankhu.walk.cam5.a", true), L("dialogue.anpu.walk.cam5.b", false) } },
                new WalkProgress.Beat { name = "cam6", start = Cameras[5].startS + 3.0f, lines = new[] { L("dialogue.anpu.walk.cam6.a", false) }, endsScene = true },
            };
        }

        private static IEnumerable<KeyValuePair<string, string>> ReadLines(string file)
        {
            if (!File.Exists(file)) yield break;
            LineFile lf = JsonUtility.FromJson<LineFile>(File.ReadAllText(file));
            if (lf?.lines == null) yield break;
            foreach (LineEntry e in lf.lines) yield return new KeyValuePair<string, string>(e.key, e.text);
        }

        private static GameObject MakeCharacter(string name, string prefabPath, Vector3 pos, Quaternion rot, RuntimeAnimatorController controller, out Animator animator)
        {
            animator = null;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogWarning("[Opening] Missing prefab " + prefabPath);
                return null;
            }
            var root = new GameObject(name);
            root.transform.SetPositionAndRotation(pos, rot);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            // The Borrowed Dawn player scripts drive the animator from the 2D controller. The walk drives it directly.
            // Reverse order and two passes, in case one script requires another.
            for (int pass = 0; pass < 2; pass++)
            {
                foreach (MonoBehaviour mb in model.GetComponentsInChildren<MonoBehaviour>(true).Reverse())
                {
                    if (mb != null && mb.GetType().Namespace != null && mb.GetType().Namespace.StartsWith("Ankhu.Player", StringComparison.Ordinal))
                        UnityEngine.Object.DestroyImmediate(mb);
                }
            }
            animator = model.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                if (animator.isHuman && animator.GetComponent<FootFloorClamp>() == null) animator.gameObject.AddComponent<FootFloorClamp>();
                if (controller != null) animator.runtimeAnimatorController = controller;
            }
            return root;
        }

        // ---- Greybox: the geometry the paintings are made from. Plain shapes, honest scale. ----

        private static void BuildGreybox(Transform parent)
        {
            Material sand = Lit("gb_sand", new Color(0.55f, 0.50f, 0.42f));
            Material road = Lit("gb_road", new Color(0.66f, 0.60f, 0.50f));
            Material stone = Lit("gb_stone", new Color(0.62f, 0.58f, 0.52f));
            Material dark = Lit("gb_dark", new Color(0.20f, 0.20f, 0.24f));
            Material water = Lit("gb_water", new Color(0.18f, 0.26f, 0.34f));

            Box(parent, "Ground", new Vector3(20f, -0.06f, 30f), new Vector3(400f, 0.1f, 400f), 0f, sand);

            // Road: one slab per segment, a little longer than the segment so corners close.
            for (int i = 0; i + 1 < PathPoints.Length; i++)
            {
                Vector3 a = PathPoints[i], b = PathPoints[i + 1];
                Vector3 d = b - a;
                float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                Box(parent, "Road_" + i, (a + b) * 0.5f + Vector3.down * 0.02f, new Vector3(RoadHalfWidth * 2f, 0.04f, d.magnitude + 1.6f), yaw, road);
            }

            // The temple they leave: pylon gate across the road at z = -5 and a colonnade behind it.
            var temple = new GameObject("Temple").transform;
            temple.SetParent(parent, false);
            Box(temple, "Pylon_L", new Vector3(-5.2f, 4.5f, -5.5f), new Vector3(5f, 9f, 2.4f), 0f, stone);
            Box(temple, "Pylon_R", new Vector3(5.2f, 4.5f, -5.5f), new Vector3(5f, 9f, 2.4f), 0f, stone);
            Box(temple, "Lintel", new Vector3(0f, 6.6f, -5.5f), new Vector3(5.6f, 1.2f, 2.2f), 0f, stone);
            for (int k = 0; k < 4; k++)
            {
                Cylinder(temple, "Column_L" + k, new Vector3(-3.4f, 0f, -9f - k * 3.2f), 0.6f, 7f, stone);
                Cylinder(temple, "Column_R" + k, new Vector3(3.4f, 0f, -9f - k * 3.2f), 0.6f, 7f, stone);
            }
            Box(temple, "Hall", new Vector3(0f, 4f, -16f), new Vector3(12f, 8f, 16f), 0f, dark);

            // Road markers: low stone posts both sides every 6 m, and a pair of jackal-statue blocks at key frames.
            var markers = new GameObject("Markers").transform;
            markers.SetParent(parent, false);
            var pathTmp = new GameObject("tmp").AddComponent<Ankhu.Opening.WalkPath>();
            pathTmp.SetPoints(PathPoints);
            for (float s = 4f; s < pathTmp.Length - 2f; s += 6f)
            {
                Box(markers, "Post_L", pathTmp.OffsetPoint(s, -(RoadHalfWidth + 0.6f)) + Vector3.up * 0.4f, new Vector3(0.35f, 0.8f, 0.35f), 0f, stone);
                Box(markers, "Post_R", pathTmp.OffsetPoint(s, RoadHalfWidth + 0.6f) + Vector3.up * 0.4f, new Vector3(0.35f, 0.8f, 0.35f), 0f, stone);
            }
            foreach (float s in new[] { 24f, 50f })
            {
                Vector3 t = pathTmp.TangentAt(s);
                float yaw = Mathf.Atan2(t.x, t.z) * Mathf.Rad2Deg;
                Box(markers, "Statue_L", pathTmp.OffsetPoint(s, -(RoadHalfWidth + 1.6f)) + Vector3.up * 1.6f, new Vector3(1f, 3.2f, 1.8f), yaw, dark);
                Box(markers, "Statue_R", pathTmp.OffsetPoint(s, RoadHalfWidth + 1.6f) + Vector3.up * 1.6f, new Vector3(1f, 3.2f, 1.8f), yaw, dark);
            }

            // Desert: deterministic low dunes and rocks off the road.
            var rng = new System.Random(25);
            var desert = new GameObject("Desert").transform;
            desert.SetParent(parent, false);
            for (int k = 0; k < 70; k++)
            {
                float s = (float)rng.NextDouble() * pathTmp.Length;
                float side = rng.NextDouble() < 0.5 ? -1f : 1f;
                float off = side * (RoadHalfWidth + 3f + (float)rng.NextDouble() * 30f);
                Vector3 p = pathTmp.OffsetPoint(s, off);
                float w = 2f + (float)rng.NextDouble() * 8f;
                var dune = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                dune.name = "Dune";
                dune.transform.SetParent(desert, false);
                dune.transform.position = p + Vector3.down * 0.2f;
                dune.transform.localScale = new Vector3(w, 0.6f + (float)rng.NextDouble() * 1.6f, w * (0.6f + (float)rng.NextDouble()));
                dune.GetComponent<MeshRenderer>().sharedMaterial = k % 5 == 0 ? stone : sand;
                UnityEngine.Object.DestroyImmediate(dune.GetComponent<Collider>());
            }
            UnityEngine.Object.DestroyImmediate(pathTmp.gameObject);

            // Vista: a river of stars across the far plain and the great gate of the Duat beyond it.
            var vista = new GameObject("Vista").transform;
            vista.SetParent(parent, false);
            Box(vista, "River", new Vector3(55f, -0.03f, 68f), new Vector3(120f, 0.02f, 7f), 20f, water);
            Box(vista, "Gate_L", new Vector3(56f, 12f, 92f), new Vector3(12f, 24f, 5f), 20f, stone);
            Box(vista, "Gate_R", new Vector3(76f, 12f, 99f), new Vector3(12f, 24f, 5f), 20f, stone);
            Box(vista, "Gate_Lintel", new Vector3(66f, 22f, 95.5f), new Vector3(10f, 3f, 4.6f), 20f, stone);
            for (int k = 0; k < 9; k++)
            {
                var m = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                m.name = "Range";
                m.transform.SetParent(vista, false);
                m.transform.position = new Vector3(-60f + k * 30f, -4f, 170f + (k % 3) * 18f);
                m.transform.localScale = new Vector3(60f, 22f + (k % 4) * 8f, 30f);
                m.GetComponent<MeshRenderer>().sharedMaterial = dark;
                UnityEngine.Object.DestroyImmediate(m.GetComponent<Collider>());
            }
        }

        private static void BuildCollision(Ankhu.Opening.WalkPath path)
        {
            var col = new GameObject("Collision").transform;
            var floor = new GameObject("Floor");
            floor.transform.SetParent(col, false);
            floor.transform.position = new Vector3(20f, -0.5f, 30f);
            floor.AddComponent<BoxCollider>().size = new Vector3(400f, 1f, 400f);

            for (int i = 0; i + 1 < PathPoints.Length; i++)
            {
                Vector3 a = PathPoints[i], b = PathPoints[i + 1];
                Vector3 d = b - a;
                Vector3 n = Vector3.Cross(Vector3.up, d.normalized);
                Quaternion r = Quaternion.LookRotation(d.normalized, Vector3.up);
                foreach (float side in new[] { -1f, 1f })
                {
                    var w = new GameObject("Edge_" + i + (side < 0 ? "_L" : "_R"));
                    w.transform.SetParent(col, false);
                    w.transform.SetPositionAndRotation((a + b) * 0.5f + n * side * (RoadHalfWidth + 0.55f) + Vector3.up, r);
                    w.AddComponent<BoxCollider>().size = new Vector3(0.5f, 2f, d.magnitude + 1.2f);
                }
            }
            // End caps.
            CapAt(col, "Cap_Start", PathPoints[0], PathPoints[1] - PathPoints[0], -0.3f);
            CapAt(col, "Cap_End", PathPoints[PathPoints.Length - 1], PathPoints[PathPoints.Length - 1] - PathPoints[PathPoints.Length - 2], 0.3f);
        }

        private static void CapAt(Transform parent, string name, Vector3 at, Vector3 dir, float push)
        {
            var c = new GameObject(name);
            c.transform.SetParent(parent, false);
            c.transform.SetPositionAndRotation(at + dir.normalized * push + Vector3.up, Quaternion.LookRotation(dir.normalized, Vector3.up));
            c.AddComponent<BoxCollider>().size = new Vector3(RoadHalfWidth * 2f + 1.6f, 2f, 0.5f);
        }

        private static GameObject Box(Transform parent, string name, Vector3 pos, Vector3 size, float yaw, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            go.transform.localScale = size;
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        private static void Cylinder(Transform parent, string name, Vector3 basePos, float radius, float height, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = basePos + Vector3.up * height * 0.5f;
            go.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform t in go.transform) SetLayerRecursive(t.gameObject, layer);
        }

        // ---- Materials ----

        private static Shader LitShader => Shader.Find("Universal Render Pipeline/Lit");
        private static Shader UnlitShader => Shader.Find("Universal Render Pipeline/Unlit");

        private static Material Lit(string name, Color c)
        {
            string path = GeneratedDir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(LitShader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", 0.15f);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>
        /// Resident Evil style foreground matte. A painting has no depth, so a character walking behind a painted
        /// statue would draw over it. Art/Walk/Foreground/&lt;cam&gt;_fg.png is the painting with alpha only on the
        /// statue. It is shown on a quad framed exactly like the backdrop but at the depth of the greybox statue
        /// that lines up with it, alpha clipped so it writes depth: characters behind it are hidden, in front draw over.
        /// </summary>
        // The was-sceptre is modelled at a tilt, so its long axis is measured here from the baked mesh in the rest pose
        // (lowest to highest vertex, the forked foot to the Set head) and stored in the socket's own space.
        private static void MeasureStaff(PropUpright pu)
        {
            Transform socket = pu.FindSocket();
            SkinnedMeshRenderer smr = pu.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(r => r.name == pu.RendererName);
            Vector3[] v = null;
            if (smr != null)
            {
                var baked = new Mesh();
                smr.BakeMesh(baked, true);
                v = baked.vertices.Select(p => smr.transform.TransformPoint(p)).ToArray();
                UnityEngine.Object.DestroyImmediate(baked);
            }
            else
            {
                // AccuRig rig: the staff is a plain mesh under the socket, named after the renderer.
                Transform staff = pu.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == pu.RendererName);
                MeshFilter[] mfs = staff != null ? staff.GetComponentsInChildren<MeshFilter>(true) : new MeshFilter[0];
                v = mfs.Where(m => m.sharedMesh != null).SelectMany(m => m.sharedMesh.vertices.Select(p => m.transform.TransformPoint(p))).ToArray();
            }
            if (socket == null || v == null || v.Length == 0) { Debug.LogWarning("[Opening] PropUpright: socket or staff renderer missing"); return; }
            if (v.Length < 2) return;
            Vector3 lo = v.OrderBy(p => p.y).First(), hi = v.OrderBy(p => p.y).Last();
            Vector3 axis = socket.InverseTransformDirection((hi - lo).normalized);
            pu.SetAxis(axis);
            pu.SetFoot(socket.InverseTransformPoint(lo));
            Debug.Log("[Opening] PropUpright: staff " + (hi - lo).magnitude.ToString("0.00") + " m, rest tilt " + Vector3.Angle(hi - lo, Vector3.up).ToString("0.0") + " deg, socket axis " + axis.ToString("F2"));
        }

        // Resident Evil style mattes: cut-outs of painted foreground objects, drawn as camera-facing cards at the
        // object's depth so 3D figures that walk behind the object are hidden by it. "_fg" is a statue, placed at the
        // greybox statue's depth. "_fg_groundN" is any object standing on the road, placed where its base meets y = 0.
        private static void AddForegroundMatte(CamSpec spec, Camera cam, Transform greybox, float imageAspect)
        {
            string dir = WalkArtDir + "/Foreground/" + spec.name;
            AddForegroundMatte(spec, cam, greybox, imageAspect, dir + "_fg.png", false);
            for (int k = 1; k <= 6; k++) AddForegroundMatte(spec, cam, greybox, imageAspect, dir + "_fg_ground" + k + ".png", true);
        }

        private static void AddForegroundMatte(CamSpec spec, Camera cam, Transform greybox, float imageAspect, string path, bool onGround)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null) return;
            // Matte bounds in image uv, read from the source file (the imported texture is compressed and not readable).
            var src = new Texture2D(2, 2);
            src.LoadImage(File.ReadAllBytes(path));
            float u0 = 1f, u1 = 0f, v0 = 1f, v1 = 0f;
            Color32[] c = src.GetPixels32();
            for (int y = 0; y < src.height; y += 2)
            for (int x = 0; x < src.width; x += 2)
            {
                if (c[y * src.width + x].a < 128) continue;
                float u = x / (float)src.width, v = y / (float)src.height;
                u0 = Mathf.Min(u0, u); u1 = Mathf.Max(u1, u); v0 = Mathf.Min(v0, v); v1 = Mathf.Max(v1, v);
            }
            UnityEngine.Object.DestroyImmediate(src);
            float tanH = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float best = -1f;
            if (onGround)
            {
                // The painting covers the 16:9 frame's width, so its height spans 16:9 / imageAspect of the view.
                float xt = ((u0 + u1) * 0.5f - 0.5f) * 2f * tanH * GameAspect;
                float yt = (v0 - 0.5f) * 2f * tanH * GameAspect / imageAspect;
                Vector3 dirW = cam.transform.TransformDirection(new Vector3(xt, yt, 1f));
                if (dirW.y < -1e-4f) best = -cam.transform.position.y / dirW.y - 0.05f;
            }
            else
            foreach (Transform t in greybox.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.StartsWith("Statue")) continue;
                Vector3 l = cam.transform.InverseTransformPoint(t.position);
                if (l.z <= 0.5f) continue;
                float u = 0.5f + l.x / (l.z * 2f * tanH * imageAspect), v = 0.5f + l.y / (l.z * 2f * tanH);
                const float margin = 0.08f; // the painted statue and the greybox block are only roughly aligned
                if (u < u0 - margin || u > u1 + margin || v < v0 - margin || v > v1 + margin) continue;
                float d = l.z - 0.6f; // front face of the block, roughly
                if (best < 0f || d < best) best = d;
            }
            if (best < 0f) { Debug.LogWarning("[Opening] " + spec.name + ": foreground matte has no statue behind it, skipped"); return; }

            string matPath = GeneratedDir + "/foreground_" + Path.GetFileNameWithoutExtension(path).Replace("_fg", "") + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (m == null) { m = new Material(UnlitShader); AssetDatabase.CreateAsset(m, matPath); }
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_AlphaClip", 1f);
            m.SetFloat("_Cutoff", 0.5f);
            m.EnableKeyword("_ALPHATEST_ON");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            EditorUtility.SetDirty(m);

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Backdrop_FG";
            UnityEngine.Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.transform.SetParent(cam.transform, false);
            var mr = quad.GetComponent<MeshRenderer>();
            mr.sharedMaterial = m;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            quad.AddComponent<BackdropFit>().Configure(cam, best, imageAspect);
            Debug.Log("[Opening] " + Path.GetFileName(path) + ": foreground matte at " + best.ToString("0.0") + " m");
        }

        private static Material BackdropMaterial(string name, Texture2D tex)
        {
            string path = GeneratedDir + "/backdrop_" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(UnlitShader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(m);
            return m;
        }

        private static Material ShadowCatcherMaterial()
        {
            string path = GeneratedDir + "/shadow_catcher.mat";
            Shader sh = Shader.Find("Ankhu/ShadowCatcher");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
            m.shader = sh;
            m.SetColor("_ShadowColor", new Color(0.05f, 0.07f, 0.15f, 0.5f));
            EditorUtility.SetDirty(m);
            return m;
        }

        // Ambient for the figures in each shot, from the painting itself: the sky colour from the top of the frame,
        // the horizon from the middle band and the ground bounce from the bottom third, all dimmed to fill levels.
        private static ShotLook.Look SampleLook(CamSpec spec)
        {
            var look = new ShotLook.Look
            {
                sky = new Color(0.30f, 0.36f, 0.52f), equator = new Color(0.20f, 0.21f, 0.27f),
                ground = new Color(0.12f, 0.10f, 0.09f), fill = new Color(1f, 0.66f, 0.40f),
            };
            string file = WalkArtDir + "/" + spec.name + ".png";
            if (!File.Exists(file)) return look;
            var src = new Texture2D(2, 2);
            src.LoadImage(File.ReadAllBytes(file));
            Color Band(float v0, float v1)
            {
                Color sum = Color.black; int n = 0;
                Color32[] px = src.GetPixels32();
                for (int y = (int)(v0 * src.height); y < (int)(v1 * src.height); y += 4)
                for (int x = 0; x < src.width; x += 4) { sum += px[y * src.width + x]; n++; }
                return n > 0 ? sum / n : Color.gray;
            }
            Color top = Band(0.70f, 1f), mid = Band(0.35f, 0.60f), bottom = Band(0f, 0.33f);
            UnityEngine.Object.DestroyImmediate(src);
            look.sky = (top.linear * 1.6f + new Color(0.06f, 0.07f, 0.10f));
            look.equator = mid.linear * 1.1f;
            look.ground = bottom.linear * 0.8f;
            // The fill takes the hue of the lit ground (torchlight on stone), at a fixed brightness.
            float max = Mathf.Max(bottom.r, Mathf.Max(bottom.g, bottom.b));
            look.fill = max > 0.01f ? Color.Lerp(new Color(1f, 0.66f, 0.40f), bottom / max, 0.5f) : look.fill;
            Debug.Log("[Opening] " + spec.name + " look: sky " + look.sky + " eq " + look.equator + " ground " + look.ground + " fill " + look.fill);
            return look;
        }

        private static Material BlobMaterial()
        {
            string texPath = GeneratedDir + "/blob_shadow.png";
            if (!File.Exists(texPath))
            {
                const int n = 128;
                var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
                for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Pow(Mathf.Clamp01(1f - r), 1.6f);
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
                t.Apply();
                File.WriteAllBytes(texPath, t.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(t);
                AssetDatabase.ImportAsset(texPath);
                var ti = (TextureImporter)AssetImporter.GetAtPath(texPath);
                ti.alphaIsTransparency = true;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.SaveAndReimport();
            }
            string path = GeneratedDir + "/blob_shadow.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(UnlitShader);
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
            m.SetColor("_BaseColor", new Color(0f, 0f, 0f, 0.55f));
            // URP Unlit, transparent, alpha blended.
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetOverrideTag("RenderType", "Transparent");
            EditorUtility.SetDirty(m);
            return m;
        }

        // ------------------------------------------------------------------------------------------------
        // Greybox capture: one PNG per camera, no characters, for the paint-over.
        // ------------------------------------------------------------------------------------------------

        // In-game check: each camera as the player sees it (16:9, painted backdrop, native masks), with the group
        // moved to the middle of that camera's stretch of path. Positions are restored and nothing is saved.
        public static void CaptureWalkIngameCli() { RunCli(() => { BuildScenes(); CaptureWalkIngame(); }); }

        public static void CaptureWalkIngame()
        {
            EditorSceneManager.OpenScene(WalkScenePath, OpenSceneMode.Single);
            string dir = "art_gen/walk/ingame";
            Directory.CreateDirectory(dir);
            WalkPath path = UnityEngine.Object.FindFirstObjectByType<WalkPath>();
            var group = new (string n, float along, float lateral)[] { ("Ankhu", 0f, 0f), ("Anpu", 0.35f, -1.0f), ("Attendant_L", -2.0f, -1.4f), ("Attendant_R", -2.0f, 0.6f) };
            var saved = new List<(Transform t, Vector3 p, Quaternion r)>();
            foreach (var g in group) { GameObject go = GameObject.Find(g.n); if (go != null) saved.Add((go.transform, go.transform.position, go.transform.rotation)); }
            const int w = 1920, h = 1080;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var img = new Texture2D(w, h, TextureFormat.RGB24, false);
            try
            {
                // Each camera at the start, middle and end of its stretch (occlusion against painted props changes along it).
                for (int k = 0; k < Cameras.Length * 3; k++)
                {
                    int i = k / 3, phase = k % 3;
                    CamSpec spec = Cameras[i];
                    float end = i + 1 < Cameras.Length ? Cameras[i + 1].startS : spec.startS + 12f;
                    float s = Mathf.Lerp(spec.startS + 1.5f, end - 1f, phase * 0.5f);
                    string suffix = phase == 0 ? "_a" : phase == 1 ? "" : "_c";
                    if (path != null)
                        foreach (var g in group)
                        {
                            GameObject go = GameObject.Find(g.n);
                            if (go == null) continue;
                            go.transform.position = path.OffsetPoint(s + g.along, g.lateral);
                            go.transform.rotation = Quaternion.LookRotation(path.TangentAt(s + g.along), Vector3.up);
                        }
                    // Pose the group with their walk clip (otherwise the editor shows the bind T-pose).
                    if (!AnimationMode.InAnimationMode()) AnimationMode.StartAnimationMode();
                    AnimationMode.BeginSampling();
                    foreach (var g in group)
                    {
                        GameObject go = GameObject.Find(g.n);
                        Animator an = go != null ? go.GetComponentInChildren<Animator>() : null;
                        if (an == null || an.runtimeAnimatorController == null) continue;
                        AnimationClip clip = null;
                        foreach (AnimationClip c in an.runtimeAnimatorController.animationClips)
                            if (clip == null || c.name.ToLowerInvariant().Contains("walk")) { clip = c; if (c.name.ToLowerInvariant().Contains("walk")) break; }
                        if (clip != null) AnimationMode.SampleAnimationClip(an.gameObject, clip, (k * 0.37f + g.along) % Mathf.Max(clip.length, 0.01f));
                    }
                    AnimationMode.EndSampling();
                    foreach (PropUpright pu in UnityEngine.Object.FindObjectsByType<PropUpright>(FindObjectsSortMode.None)) { pu.Resolve(); pu.Apply(); }
                    // Editor renders reuse cached skin matrices, so after AnimationMode sampling the body can lag the bones its props follow.
                    foreach (SkinnedMeshRenderer smr in UnityEngine.Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None)) smr.forceMatrixRecalculationPerRender = true;
                    GameObject camGo = GameObject.Find(spec.name);
                    if (camGo == null) continue;
                    Camera cam = camGo.GetComponent<Camera>();
                    // As the director does in play: only this camera's backdrop is shown.
                    foreach (CamSpec o in Cameras) { Transform oc = GameObject.Find(o.name)?.transform; if (oc != null) foreach (Transform ob in oc) if (ob.name.StartsWith("Backdrop")) ob.gameObject.SetActive(o.name == spec.name); }
                    bool enabled = cam.enabled;
                    cam.targetTexture = rt;
                    cam.aspect = w / (float)h;
                    foreach (BackdropFit bf in camGo.GetComponentsInChildren<BackdropFit>(true)) bf.SendMessage("LateUpdate", SendMessageOptions.DontRequireReceiver);
                    foreach (BlobShadow b in UnityEngine.Object.FindObjectsByType<BlobShadow>(FindObjectsSortMode.None)) b.SendMessage("LateUpdate", SendMessageOptions.DontRequireReceiver);
                    cam.Render();
                    cam.Render();
                    RenderTexture.active = rt;
                    img.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                    img.Apply();
                    RenderTexture.active = null;
                    cam.targetTexture = null;
                    cam.ResetAspect();
                    cam.enabled = enabled;
                    string file = Path.Combine(dir, spec.name + suffix + ".png");
                    File.WriteAllBytes(file, img.EncodeToPNG());
                    Debug.Log("[Opening] In-game capture " + Path.GetFullPath(file) + " at s=" + s.ToString("0.0"));
                }
            }
            finally
            {
                if (AnimationMode.InAnimationMode()) AnimationMode.StopAnimationMode();
                foreach (var sv in saved) { sv.t.position = sv.p; sv.t.rotation = sv.r; }
                foreach (CamSpec o in Cameras) { Transform oc = GameObject.Find(o.name)?.transform; if (oc != null) foreach (Transform ob in oc) if (ob.name.StartsWith("Backdrop")) ob.gameObject.SetActive(true); }
                rt.Release();
            }
        }

        public static List<string> CaptureWalkGreybox()
        {
            EditorSceneManager.OpenScene(WalkScenePath, OpenSceneMode.Single);
            Directory.CreateDirectory(GreyboxCaptureDir);
            var hidden = new List<GameObject>();
            foreach (string n in new[] { "Ankhu", "Anpu", "Attendant_L", "Attendant_R" })
            {
                GameObject g = GameObject.Find(n);
                if (g != null) { g.SetActive(false); hidden.Add(g); }
            }
            foreach (BlobShadow b in UnityEngine.Object.FindObjectsByType<BlobShadow>(FindObjectsSortMode.None)) { b.gameObject.SetActive(false); hidden.Add(b.gameObject); }

            var written = new List<string>();
            var rt = new RenderTexture(CaptureWidth, CaptureHeight, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var img = new Texture2D(CaptureWidth, CaptureHeight, TextureFormat.RGB24, false);
            try
            {
                foreach (CamSpec spec in Cameras)
                {
                    GameObject camGo = GameObject.Find(spec.name);
                    if (camGo == null) continue;
                    Camera cam = camGo.GetComponent<Camera>();
                    Transform backdrop = camGo.transform.Find("Backdrop");
                    bool hadBackdrop = backdrop != null && backdrop.gameObject.activeSelf;
                    if (backdrop != null) backdrop.gameObject.SetActive(false);
                    int mask = cam.cullingMask;
                    bool enabled = cam.enabled;
                    float fov = cam.fieldOfView;
                    float halfH = Mathf.Atan(Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad) * GameAspect);
                    cam.fieldOfView = 2f * Mathf.Atan(Mathf.Tan(halfH) / (CaptureWidth / (float)CaptureHeight)) * Mathf.Rad2Deg;
                    cam.cullingMask = ~0;
                    cam.targetTexture = rt;
                    cam.aspect = CaptureWidth / (float)CaptureHeight;
                    cam.Render();
                    cam.Render();
                    RenderTexture.active = rt;
                    img.ReadPixels(new Rect(0, 0, CaptureWidth, CaptureHeight), 0, 0);
                    img.Apply();
                    RenderTexture.active = null;
                    cam.targetTexture = null;
                    cam.ResetAspect();
                    cam.fieldOfView = fov;
                    cam.cullingMask = mask;
                    cam.enabled = enabled;
                    if (backdrop != null) backdrop.gameObject.SetActive(hadBackdrop);
                    string file = Path.Combine(GreyboxCaptureDir, spec.name + ".png");
                    File.WriteAllBytes(file, img.EncodeToPNG());
                    written.Add(file);
                    Debug.Log("[Opening] Captured " + Path.GetFullPath(file));
                }
            }
            finally
            {
                foreach (GameObject g in hidden) g.SetActive(true);
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
                UnityEngine.Object.DestroyImmediate(img);
            }
            return written;
        }

        // ------------------------------------------------------------------------------------------------
        // Build settings and player
        // ------------------------------------------------------------------------------------------------

        private static void SetBuildSettings()
        {
            var opening = new[] { TitlePath, CinematicPath, WalkScenePath };
            var list = opening.Select(p => new EditorBuildSettingsScene(p, true)).ToList();
            // The story thread's realms follow the walk directly, sorted by name (agreed with that thread).
            const string realmDir = "Assets/Ankhu/Realms/Generated/Scenes";
            string[] realms = Directory.Exists(realmDir)
                ? Directory.GetFiles(realmDir, "Realm_*.unity").Select(p => p.Replace('\\', '/')).OrderBy(p => Path.GetFileName(p), StringComparer.Ordinal).ToArray()
                : new string[0];
            list.AddRange(realms.Select(p => new EditorBuildSettingsScene(p, true)));
            foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
            {
                if (opening.Contains(s.path) || realms.Contains(s.path)) continue;
                list.Add(s);
            }
            EditorBuildSettings.scenes = list.ToArray();
        }

        public static void BuildPlayer()
        {
            Directory.CreateDirectory(BuildDir);
            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled && File.Exists(s.path)).Select(s => s.path).ToArray();
            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = Path.Combine(BuildDir, ExeName),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Player build " + report.summary.result + ", " + report.summary.totalErrors + " errors.");
            Debug.Log(string.Format("[Opening] Player built: {0} ({1:0.0} MB, {2} scenes)", Path.GetFullPath(options.locationPathName), report.summary.totalSize / 1048576.0, scenes.Length));
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
