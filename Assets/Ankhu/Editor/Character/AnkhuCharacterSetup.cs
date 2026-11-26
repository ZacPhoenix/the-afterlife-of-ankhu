using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Ankhu.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using L = Ankhu.Layout.BorrowedDawnLayout;
using Object = UnityEngine.Object;

namespace Ankhu.EditorTools.Character
{
    /// <summary>
    /// Turns the imported Ankhu model and Mixamo clips into the playable character:
    /// URP materials from the embedded textures, per-clip import settings, an upper-body avatar mask,
    /// Assets/Characters/Ankhu/Ankhu.controller, and the Assets/Characters/Ankhu/Ankhu.prefab variant
    /// of the model with the controller and <see cref="AnkhuAnimatorDriver"/>. Then it points the
    /// AnkhuVisual of every generated room scene at the prefab. Idempotent.
    ///
    /// Batch: Unity -batchmode -quit -projectPath . -executeMethod Ankhu.EditorTools.Character.AnkhuCharacterSetup.SetupFromCommandLine -logFile setup.log
    /// </summary>
    public static class AnkhuCharacterSetup
    {
        public const string ControllerPath = AnkhuCharacterImport.Root + "/Ankhu.controller";
        public const string PrefabPath = AnkhuCharacterImport.Root + "/Ankhu.prefab";
        public const string MaskPath = AnkhuCharacterImport.Root + "/Ankhu_UpperBody.mask";
        public const string MaterialPrefix = "M_Ankhu_";

        public const string IdleClip = "01_Idle_Breathing";
        public const string WalkClip = "03_Walk_InPlace";
        public const string JogClip = "04_Jog_InPlace";
        public const string ClimbClip = "06_Ladder_Climb_InPlace";
        public const string PickUpLowClip = "08_PickUp_OneHand_Low";
        public const string ReachClip = "09_Reach_Take_EyeLevel";
        public const string SpeakClip = "13_Speak_Gesture";

        /// <summary>Clips that cycle. Every other clip plays once.</summary>
        public static readonly string[] LoopingClips = { IdleClip, WalkClip, JogClip, ClimbClip, "15_Kneel_Idle" };

        /// <summary>Smoothness for every Ankhu material until the packed roughness map is converted.</summary>
        public const float Smoothness = 0.35f;

        /// <summary>Playback clamp for the stride-matched walk, so a bad measurement never looks absurd.</summary>
        private const float MinWalkScale = 0.6f;
        private const float MaxWalkScale = 2.4f;

        [MenuItem("Ankhu/Character/Set Up Ankhu Player Model")]
        public static void SetupFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Leave Play mode before setting up the Ankhu character.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }
            string current = SceneManager.GetActiveScene().path;
            Setup();
            ApplyToRoomScenes();
            if (!string.IsNullOrEmpty(current))
            {
                EditorSceneManager.OpenScene(current, OpenSceneMode.Single);
            }
        }

        public static void SetupFromCommandLine()
        {
            try
            {
                Setup();
                ApplyToRoomScenes();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        public static void Setup()
        {
            if (AccuRigCharacterSetup.Replaces("Ankhu"))
            {
                Debug.LogWarning("[AnkhuSetup] Ankhu.prefab now holds the AccuRig rig. Use Ankhu > Character > Set Up AccuRig Rerigs instead. Skipped.");
                return;
            }
            ConfigureModel();
            Dictionary<string, AnimationClip> clips = ConfigureClips();
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(AnkhuCharacterImport.ModelPath);
            if (model == null)
            {
                throw new InvalidOperationException("Model not found at " + AnkhuCharacterImport.ModelPath);
            }
            ReportModel(model);

            float walkImplied = MeasureGroundSpeed(model, clips[WalkClip]);
            float walkScale = walkImplied > 0.05f ? Mathf.Clamp(L.WalkSpeed / walkImplied, MinWalkScale, MaxWalkScale) : 1f;
            Debug.Log(string.Format("[AnkhuSetup] Walk clip implies {0:0.00} m/s on Ankhu. Game walk speed is {1:0.00} m/s, so the walk plays at x{2:0.00}.",
                walkImplied, L.WalkSpeed, walkScale));
            float jogImplied = MeasureGroundSpeed(model, clips[JogClip]);
            Debug.Log(string.Format("[AnkhuSetup] Jog clip implies {0:0.00} m/s (not used, the controller has no run).", jogImplied));

            AvatarMask mask = BuildUpperBodyMask();
            AnimatorController controller = BuildController(clips, mask, walkScale);
            BuildPrefab(model, controller);
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------ model and materials

        public static TextureRole RoleOf(string textureName)
        {
            string n = textureName.ToLowerInvariant();
            if (n.Contains("normal") || n.Contains("nrm") || n.EndsWith("_n") || n.Contains("_n_") || n.Contains("bump"))
            {
                return TextureRole.Normal;
            }
            if (n.Contains("rough") || n.Contains("metal") || n.Contains("orm") || n.Contains("_mr") || n.Contains("packed") || n.Contains("spec"))
            {
                return TextureRole.Packed;
            }
            return TextureRole.BaseColor;
        }

        private static void ConfigureModel()
        {
            AssetDatabase.ImportAsset(AnkhuCharacterImport.ModelPath, ImportAssetOptions.ForceUpdate);
            var importer = (ModelImporter)AssetImporter.GetAtPath(AnkhuCharacterImport.ModelPath);
            if (importer == null)
            {
                throw new InvalidOperationException("No model importer at " + AnkhuCharacterImport.ModelPath);
            }

            EnsureFolder(AnkhuCharacterImport.TextureFolder);
            EnsureFolder(AnkhuCharacterImport.MaterialFolder);
            if (!AssetDatabase.FindAssets("t:Texture2D", new[] { AnkhuCharacterImport.TextureFolder }).Any())
            {
                bool ok = importer.ExtractTextures(AnkhuCharacterImport.TextureFolder);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                Debug.Log("[AnkhuSetup] Extracted embedded textures: " + ok);
            }
            NameExtractedTextures();

            var textures = new List<Texture2D>();
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { AnkhuCharacterImport.TextureFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if (tex != null)
                {
                    textures.Add(tex);
                    Debug.Log(string.Format("[AnkhuSetup] Texture {0} {1}x{2} role {3}", path, tex.width, tex.height, RoleOf(tex.name)));
                }
            }

            // Source material names are the keys for remapping. Read them from the renderers, undoing any
            // earlier remap through the prefix.
            var sourceNames = new HashSet<string>();
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(AnkhuCharacterImport.ModelPath);
            foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true))
            {
                foreach (Material m in r.sharedMaterials)
                {
                    if (m != null)
                    {
                        sourceNames.Add(m.name.StartsWith(MaterialPrefix) ? m.name.Substring(MaterialPrefix.Length) : m.name);
                    }
                }
            }
            if (sourceNames.Count == 0)
            {
                throw new InvalidOperationException("The Ankhu model has no materials on its renderers.");
            }

            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null)
            {
                throw new InvalidOperationException("URP Lit shader not found.");
            }
            foreach (string source in sourceNames)
            {
                string path = AnkhuCharacterImport.MaterialFolder + "/" + MaterialPrefix + Sanitize(source) + ".mat";
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                {
                    mat = new Material(lit);
                    AssetDatabase.CreateAsset(mat, path);
                }
                mat.shader = lit;
                mat.name = MaterialPrefix + Sanitize(source);
                Texture2D baseMap = Pick(textures, TextureRole.BaseColor, source);
                Texture2D normal = Pick(textures, TextureRole.Normal, source);
                mat.SetColor("_BaseColor", Color.white);
                mat.SetTexture("_BaseMap", baseMap);
                mat.SetTexture("_MainTex", baseMap);
                mat.SetFloat("_WorkflowMode", 1f);
                mat.SetFloat("_Metallic", 0f);
                mat.SetFloat("_Smoothness", Smoothness);
                mat.SetFloat("_SmoothnessTextureChannel", 0f);
                if (normal != null)
                {
                    mat.SetTexture("_BumpMap", normal);
                    mat.SetFloat("_BumpScale", 1f);
                    mat.EnableKeyword("_NORMALMAP");
                }
                else
                {
                    mat.DisableKeyword("_NORMALMAP");
                }
                EditorUtility.SetDirty(mat);
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), source), mat);
                Debug.Log(string.Format("[AnkhuSetup] Material {0} <- base {1}, normal {2}, smoothness {3}, metallic 0",
                    path, baseMap != null ? baseMap.name : "none", normal != null ? normal.name : "none", Smoothness));
            }
            AssetDatabase.SaveAssets();
            importer.SaveAndReimport();
        }

        /// <summary>
        /// The fbx embeds its images without file extensions (Image_0, Image_2), which Unity imports as
        /// plain files. Give each one its real extension from the file header and a name that says its
        /// role, judged from the pixels: a tangent-space normal map averages close to (0.5, 0.5, 1).
        /// </summary>
        private static void NameExtractedTextures()
        {
            string folder = Path.GetFullPath(AnkhuCharacterImport.TextureFolder);
            bool moved = false;
            foreach (string file in Directory.GetFiles(folder))
            {
                if (file.EndsWith(".meta", StringComparison.OrdinalIgnoreCase) || Path.HasExtension(file))
                {
                    continue;
                }
                byte[] bytes = File.ReadAllBytes(file);
                string ext = bytes.Length > 4 && bytes[0] == 0x89 && bytes[1] == 0x50 ? ".png"
                    : bytes.Length > 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 ? ".jpg"
                    : bytes.Length > 2 && bytes[0] == 0x42 && bytes[1] == 0x4D ? ".bmp"
                    : bytes.Length > 4 && bytes[0] == 0x00 && bytes[2] == 0x02 ? ".tga"
                    : null;
                if (ext == null)
                {
                    Debug.LogWarning("[AnkhuSetup] Unknown image format, left as is: " + file);
                    continue;
                }
                Color average = AverageColour(bytes);
                bool normal = average.b > 0.7f && Mathf.Abs(average.r - 0.5f) < 0.15f && Mathf.Abs(average.g - 0.5f) < 0.15f;
                string role = normal ? "Normal" : "BaseColor";
                string target = Path.Combine(folder, "T_Ankhu_" + role + "_" + Path.GetFileName(file) + ext);
                File.Move(file, target);
                if (File.Exists(file + ".meta"))
                {
                    File.Delete(file + ".meta");
                }
                Debug.Log(string.Format("[AnkhuSetup] {0} is {1} (average colour {2}), renamed to {3}",
                    Path.GetFileName(file), ext, average, Path.GetFileName(target)));
                moved = true;
            }
            if (moved)
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
        }

        private static Color AverageColour(byte[] bytes)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            try
            {
                if (!tex.LoadImage(bytes))
                {
                    return Color.black;
                }
                Color32[] px = tex.GetPixels32();
                double r = 0, g = 0, b = 0;
                int n = 0;
                for (int i = 0; i < px.Length; i += 97)
                {
                    r += px[i].r; g += px[i].g; b += px[i].b; n++;
                }
                return new Color((float)(r / n / 255.0), (float)(g / n / 255.0), (float)(b / n / 255.0));
            }
            finally
            {
                Object.DestroyImmediate(tex);
            }
        }

        private static Texture2D Pick(List<Texture2D> textures, TextureRole role, string materialName)
        {
            List<Texture2D> matches = textures.Where(t => RoleOf(t.name) == role).ToList();
            if (matches.Count == 0)
            {
                return null;
            }
            Texture2D named = matches.FirstOrDefault(t => t.name.IndexOf(materialName, StringComparison.OrdinalIgnoreCase) >= 0);
            return named != null ? named : matches.OrderByDescending(t => t.width * t.height).First();
        }

        private static string Sanitize(string name)
        {
            var sb = new StringBuilder();
            foreach (char c in name)
            {
                sb.Append(char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_');
            }
            return sb.ToString();
        }

        private static void ReportModel(GameObject model)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(AnkhuCharacterImport.ModelPath);
            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(AnkhuCharacterImport.ModelPath).OfType<Avatar>().FirstOrDefault();
            Debug.Log(string.Format("[AnkhuSetup] Model avatar: {0}, human {1}, valid {2}. Import scale {3}, file scale {4}.",
                avatar != null ? avatar.name : "none", avatar != null && avatar.isHuman, avatar != null && avatar.isValid,
                importer.globalScale, importer.fileScale));

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                Bounds b = RendererBounds(instance);
                Debug.Log(string.Format("[AnkhuSetup] Model bind-pose bounds: size {0}, min {1}, max {2}", b.size.ToString("F3"), b.min.ToString("F3"), b.max.ToString("F3")));
                Animator animator = instance.GetComponent<Animator>();
                if (animator != null && animator.isHuman)
                {
                    Transform leftLeg = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                    Transform rightLeg = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
                    Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                    Transform leftToes = animator.GetBoneTransform(HumanBodyBones.LeftToes);
                    // Facing +z, the character's left is at -x in Unity's left-handed frame.
                    Vector3 across = rightLeg.position - leftLeg.position;
                    Vector3 forward = Vector3.Cross(across, Vector3.up).normalized;
                    Debug.Log(string.Format("[AnkhuSetup] Facing from hips (left to right leg x up): {0}. Toes minus foot: {1}.",
                        (-forward).ToString("F2"), leftToes != null && leftFoot != null ? (leftToes.position - leftFoot.position).ToString("F3") : "no toes bone"));
                }
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        // ------------------------------------------------------------------ clips

        private static Dictionary<string, AnimationClip> ConfigureClips()
        {
            var result = new Dictionary<string, AnimationClip>();
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { AnkhuCharacterImport.AnimationFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                string name = Path.GetFileNameWithoutExtension(path);
                if (importer.animationType != ModelImporterAnimationType.Human)
                {
                    importer.SaveAndReimport();
                }
                ModelImporterClipAnimation[] defaults = importer.defaultClipAnimations;
                if (defaults == null || defaults.Length == 0)
                {
                    Debug.LogWarning("[AnkhuSetup] " + path + " has no takes.");
                    continue;
                }
                ModelImporterClipAnimation clip = defaults[0];
                clip.name = name;
                clip.loopTime = Array.IndexOf(LoopingClips, name) >= 0;
                clip.loopPose = false;
                clip.lockRootRotation = true;
                clip.keepOriginalOrientation = true;
                clip.lockRootHeightY = true;
                clip.keepOriginalPositionY = true;
                clip.heightFromFeet = false;
                clip.lockRootPositionXZ = true;
                clip.keepOriginalPositionXZ = true;
                importer.clipAnimations = new[] { clip };
                importer.SaveAndReimport();

                AnimationClip imported = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                    .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
                Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
                if (imported == null)
                {
                    Debug.LogWarning("[AnkhuSetup] No clip imported from " + path);
                    continue;
                }
                result[name] = imported;
                Debug.Log(string.Format("[AnkhuSetup] Clip {0}: {1:0.00} s at {2} fps, loop {3}, humanMotion {4}, avatar valid {5}",
                    name, imported.length, imported.frameRate, clip.loopTime, imported.humanMotion, avatar != null && avatar.isValid && avatar.isHuman));
            }
            foreach (string needed in new[] { IdleClip, WalkClip, JogClip, ClimbClip, PickUpLowClip, ReachClip, SpeakClip })
            {
                if (!result.ContainsKey(needed))
                {
                    throw new InvalidOperationException("Clip missing: " + needed);
                }
            }
            return result;
        }

        /// <summary>
        /// Ground speed an in-place cycle implies on Ankhu's body: while a foot is planted (within 2 cm of
        /// its lowest point) it slides backward under the hips at the speed the body would travel.
        /// Averages that horizontal slide over every planted frame of both feet.
        /// </summary>
        public static float MeasureGroundSpeed(GameObject model, AnimationClip clip)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            bool startedMode = !AnimationMode.InAnimationMode();
            try
            {
                Animator animator = instance.GetComponent<Animator>();
                animator.applyRootMotion = false;
                if (startedMode)
                {
                    AnimationMode.StartAnimationMode();
                }

                Transform[] feet = { animator.GetBoneTransform(HumanBodyBones.LeftFoot), animator.GetBoneTransform(HumanBodyBones.RightFoot) };
                const int Samples = 240;
                float dt = clip.length / Samples;
                var positions = new Vector3[2, Samples + 1];
                for (int i = 0; i <= Samples; i++)
                {
                    SamplePose(instance, clip, i * dt);
                    for (int f = 0; f < 2; f++)
                    {
                        positions[f, i] = feet[f].position;
                    }
                }
                float range = 0f;
                for (int i = 0; i <= Samples; i++)
                {
                    range = Mathf.Max(range, Vector3.Distance(positions[0, i], positions[0, 0]));
                }
                Debug.Log(string.Format("[AnkhuSetup] {0}: left foot travels up to {1:0.000} m from its first-frame position.", clip.name, range));

                double total = 0;
                int count = 0;
                for (int f = 0; f < 2; f++)
                {
                    float minY = float.PositiveInfinity;
                    for (int i = 0; i <= Samples; i++)
                    {
                        minY = Mathf.Min(minY, positions[f, i].y);
                    }
                    for (int i = 1; i <= Samples; i++)
                    {
                        if (positions[f, i].y < minY + 0.02f && positions[f, i - 1].y < minY + 0.02f)
                        {
                            Vector3 d = positions[f, i] - positions[f, i - 1];
                            d.y = 0f;
                            total += d.magnitude / dt;
                            count++;
                        }
                    }
                }
                return count > 0 ? (float)(total / count) : 0f;
            }
            finally
            {
                if (startedMode)
                {
                    AnimationMode.StopAnimationMode();
                }
                Object.DestroyImmediate(instance);
            }
        }

        /// <summary>Poses a humanoid instance at a clip time in the editor. Needs AnimationMode on.</summary>
        public static void SamplePose(GameObject go, AnimationClip clip, float time)
        {
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(go, clip, time);
            AnimationMode.EndSampling();
        }

        // ------------------------------------------------------------------ controller

        private static AvatarMask BuildUpperBodyMask()
        {
            AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(MaskPath);
            if (mask == null)
            {
                mask = new AvatarMask();
                AssetDatabase.CreateAsset(mask, MaskPath);
            }
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            {
                var part = (AvatarMaskBodyPart)i;
                bool upper = part == AvatarMaskBodyPart.Body || part == AvatarMaskBodyPart.Head ||
                             part == AvatarMaskBodyPart.LeftArm || part == AvatarMaskBodyPart.RightArm ||
                             part == AvatarMaskBodyPart.LeftFingers || part == AvatarMaskBodyPart.RightFingers;
                mask.SetHumanoidBodyPartActive(part, upper);
            }
            EditorUtility.SetDirty(mask);
            return mask;
        }

        private static AnimatorController BuildController(Dictionary<string, AnimationClip> clips, AvatarMask mask, float walkScale)
        {
            AssetDatabase.DeleteAsset(ControllerPath);
            AnimatorController ac = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ac.AddParameter("Climbing", AnimatorControllerParameterType.Bool);
            ac.AddParameter(new AnimatorControllerParameter { name = "Grounded", type = AnimatorControllerParameterType.Bool, defaultBool = true });
            ac.AddParameter("Interact", AnimatorControllerParameterType.Trigger);
            ac.AddParameter("InteractLow", AnimatorControllerParameterType.Trigger);
            ac.AddParameter("Speak", AnimatorControllerParameterType.Trigger);

            // Base layer: locomotion, ladder, one-shot interactions.
            AnimatorStateMachine sm = ac.layers[0].stateMachine;
            AnimatorState loco = ac.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
            loco.iKOnFeet = true; // Pins the feet to the clip's own foot goals. Without it the retarget rolls the left foot over at heel strike.
            { var ls = ac.layers; ls[0].iKPass = true; ac.layers = ls; } // Lets the opening's FootFloorClamp keep the IK feet out of the floor.
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            tree.AddChild(clips[IdleClip], 0f);
            tree.AddChild(clips[WalkClip], L.WalkSpeed);
            ChildMotion[] children = tree.children;
            children[1].timeScale = walkScale;
            tree.children = children;
            sm.defaultState = loco;

            AnimatorState climb = sm.AddState("Climb", new Vector3(300f, 200f, 0f));
            climb.motion = clips[ClimbClip];
            // Speed is |vertical velocity| while climbing, so the cycle freezes when Ankhu holds still.
            climb.speedParameterActive = true;
            climb.speedParameter = "Speed";
            climb.speed = 1f / L.ClimbSpeed;
            Transition(loco, climb, 0.15f).AddCondition(AnimatorConditionMode.If, 0f, "Climbing");
            Transition(climb, loco, 0.15f).AddCondition(AnimatorConditionMode.IfNot, 0f, "Climbing");

            AddOneShot(sm, loco, "Reach", clips[ReachClip], "Interact", new Vector3(300f, -80f, 0f));
            AddOneShot(sm, loco, "PickUpLow", clips[PickUpLowClip], "InteractLow", new Vector3(300f, 60f, 0f));

            // Upper-body layer for talking while the legs keep walking or idling.
            ac.AddLayer("Gesture");
            AnimatorControllerLayer[] layers = ac.layers;
            layers[1].defaultWeight = 1f;
            layers[1].avatarMask = mask;
            layers[1].blendingMode = AnimatorLayerBlendingMode.Override;
            ac.layers = layers;
            AnimatorStateMachine gesture = ac.layers[1].stateMachine;
            AnimatorState empty = gesture.AddState("Empty", new Vector3(300f, 0f, 0f));
            gesture.defaultState = empty;
            AnimatorState speak = gesture.AddState("Speak", new Vector3(300f, 120f, 0f));
            speak.motion = clips[SpeakClip];
            Transition(empty, speak, 0.15f).AddCondition(AnimatorConditionMode.If, 0f, "Speak");
            AnimatorStateTransition back = Transition(speak, empty, 0.3f);
            back.hasExitTime = true;
            back.exitTime = 0.9f;
            Transition(speak, empty, 0.15f).AddCondition(AnimatorConditionMode.If, 0f, "Climbing");

            EditorUtility.SetDirty(ac);
            Debug.Log("[AnkhuSetup] Controller written to " + ControllerPath);
            return ac;
        }

        private static void AddOneShot(AnimatorStateMachine sm, AnimatorState loco, string name, AnimationClip clip, string trigger, Vector3 position)
        {
            AnimatorState state = sm.AddState(name, position);
            state.motion = clip;
            Transition(loco, state, 0.1f).AddCondition(AnimatorConditionMode.If, 0f, trigger);
            AnimatorStateTransition done = Transition(state, loco, 0.25f);
            done.hasExitTime = true;
            done.exitTime = 0.85f;
            // Walking away or grabbing a ladder cuts the gesture short.
            Transition(state, loco, 0.2f).AddCondition(AnimatorConditionMode.Greater, 0.5f, "Speed");
            Transition(state, loco, 0.15f).AddCondition(AnimatorConditionMode.If, 0f, "Climbing");
        }

        private static AnimatorStateTransition Transition(AnimatorState from, AnimatorState to, float duration)
        {
            AnimatorStateTransition t = from.AddTransition(to);
            t.hasExitTime = false;
            t.hasFixedDuration = true;
            t.duration = duration;
            return t;
        }

        // ------------------------------------------------------------------ prefab

        private static void BuildPrefab(GameObject model, AnimatorController controller)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            try
            {
                instance.name = "Ankhu";
                Animator animator = instance.GetComponent<Animator>();
                if (animator == null)
                {
                    animator = instance.AddComponent<Animator>();
                }
                animator.avatar = AssetDatabase.LoadAllAssetsAtPath(AnkhuCharacterImport.ModelPath).OfType<Avatar>().FirstOrDefault();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                if (instance.GetComponent<AnkhuAnimatorDriver>() == null)
                {
                    instance.AddComponent<AnkhuAnimatorDriver>();
                }
                Bounds before = RendererBounds(instance);
                foreach (SkinnedMeshRenderer smr in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    smr.receiveShadows = true;
                    smr.updateWhenOffscreen = false;
                    TightenBounds(smr);
                }
                Bounds after = RendererBounds(instance);
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                instance.transform.localScale = Vector3.one;
                Vector2 span = BakedHeightSpan(instance);
                AnkhuModelMetrics metrics = instance.GetComponent<AnkhuModelMetrics>();
                if (metrics == null)
                {
                    metrics = instance.AddComponent<AnkhuModelMetrics>();
                }
                metrics.Set(span.y - span.x, span.x);
                Debug.Log(string.Format("[AnkhuSetup] Bind-pose mesh: soles at y {0:0.000}, crown at y {1:0.000}, standing height {2:0.000} m. Written to AnkhuModelMetrics.",
                    span.x, span.y, span.y - span.x));
                Debug.Log(string.Format("[AnkhuSetup] Renderer bounds imported: min {0} max {1}. Fitted to the bind-pose mesh: min {2} max {3}, height {4:0.000} m.",
                    before.min.ToString("F3"), before.max.ToString("F3"), after.min.ToString("F3"), after.max.ToString("F3"), after.size.y));
                PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath, out bool success);
                if (!success)
                {
                    throw new InvalidOperationException("Could not save " + PrefabPath);
                }
                Debug.Log("[AnkhuSetup] Prefab written to " + PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        /// <summary>
        /// The imported skinned bounds are looser than the mesh, and AnkhuVisual sizes and grounds the
        /// model from renderer bounds. Fit them to the baked bind-pose mesh so height and feet are
        /// exact, with 0.25 m of horizontal slack so swinging arms do not get culled.
        /// </summary>
        private static void TightenBounds(SkinnedMeshRenderer smr)
        {
            var baked = new Mesh();
            try
            {
                smr.BakeMesh(baked, true);
                Vector3[] vertices = baked.vertices;
                if (vertices.Length == 0)
                {
                    return;
                }
                Transform t = smr.transform;
                Transform root = smr.rootBone != null ? smr.rootBone : t;
                // Fit in the root bone's own space. Fitting in world space and converting the box
                // corners inflated the height whenever the root bone is rotated, which made
                // AnkhuVisual shrink the model and lift it off the floor.
                Vector3 lmin = Vector3.positiveInfinity;
                Vector3 lmax = Vector3.negativeInfinity;
                foreach (Vector3 v in vertices)
                {
                    Vector3 l = root.InverseTransformPoint(t.position + t.rotation * v);
                    lmin = Vector3.Min(lmin, l);
                    lmax = Vector3.Max(lmax, l);
                }
                // Horizontal slack only on the root axes that point sideways in the world.
                Vector3 up = root.InverseTransformDirection(Vector3.up);
                var pad = new Vector3(
                    Mathf.Abs(up.x) < 0.5f ? 0.25f : 0f,
                    Mathf.Abs(up.y) < 0.5f ? 0.25f : 0f,
                    Mathf.Abs(up.z) < 0.5f ? 0.25f : 0f);
                var local = new Bounds();
                local.SetMinMax(lmin - pad, lmax + pad);
                smr.localBounds = local;
                Debug.Log(string.Format("[AnkhuSetup] {0}: root bone {1} rotation {2}, world up in root space {3}",
                    smr.name, root.name, root.rotation.eulerAngles.ToString("F1"), up.ToString("F3")));
            }
            finally
            {
                Object.DestroyImmediate(baked);
            }
        }

        /// <summary>Lowest and highest world y over every baked skinned mesh in its current pose.</summary>
        public static Vector2 BakedHeightSpan(GameObject go)
        {
            float lo = float.PositiveInfinity;
            float hi = float.NegativeInfinity;
            foreach (SkinnedMeshRenderer smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var baked = new Mesh();
                try
                {
                    smr.BakeMesh(baked, true);
                    Transform t = smr.transform;
                    foreach (Vector3 v in baked.vertices)
                    {
                        float y = (t.position + t.rotation * v).y;
                        lo = Mathf.Min(lo, y);
                        hi = Mathf.Max(hi, y);
                    }
                }
                finally
                {
                    Object.DestroyImmediate(baked);
                }
            }
            if (float.IsInfinity(lo))
            {
                throw new InvalidOperationException("The Ankhu model has no skinned mesh to measure.");
            }
            return new Vector2(lo, hi);
        }

        private static float? modelYaw;

        /// <summary>
        /// Yaw that turns the model to face +z, as AnkhuVisual expects. Measured from the bind pose:
        /// the left toes point the way the model faces.
        /// </summary>
        public static float ModelYaw()
        {
            if (modelYaw.HasValue)
            {
                return modelYaw.Value;
            }
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(AnkhuCharacterImport.ModelPath);
            if (model == null)
            {
                return 0f;
            }
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            // Measure in the animated idle pose, not the bind pose. Humanoid retargeting turns the
            // body to face the avatar's +z, while this model's bind pose faces -z, so the bind pose
            // gives the wrong answer for how Ankhu looks in play.
            bool startedAnimationMode = !AnimationMode.InAnimationMode();
            if (startedAnimationMode)
            {
                AnimationMode.StartAnimationMode();
            }
            try
            {
                AnimationClip idle = AssetDatabase.LoadAllAssetsAtPath(AnkhuCharacterImport.AnimationFolder + "/" + IdleClip + ".fbx")
                    .OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
                if (idle != null)
                {
                    SamplePose(instance, idle, 0f);
                }
                Animator animator = instance.GetComponent<Animator>();
                Transform foot = animator != null ? animator.GetBoneTransform(HumanBodyBones.LeftFoot) : null;
                Transform toes = animator != null ? animator.GetBoneTransform(HumanBodyBones.LeftToes) : null;
                float yaw = 0f;
                if (foot != null && toes != null)
                {
                    Vector3 d = toes.position - foot.position;
                    yaw = Mathf.Round(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg / 90f) * -90f;
                    yaw = Mathf.Repeat(yaw, 360f);
                }
                modelYaw = yaw;
                Debug.Log(string.Format("[AnkhuSetup] Idle-pose toes point {0}, so baseYaw is {1}.", foot != null && toes != null ? (toes.position - foot.position).ToString("F3") : "unknown", yaw));
                return yaw;
            }
            finally
            {
                if (startedAnimationMode)
                {
                    AnimationMode.StopAnimationMode();
                }
                Object.DestroyImmediate(instance);
            }
        }

        // ------------------------------------------------------------------ scenes

        /// <summary>
        /// Points the AnkhuVisual of every generated room scene at the prefab and saves only that change.
        /// Collider, controller and the rest of the scene are untouched.
        /// </summary>
        public static void ApplyToRoomScenes()
        {
            foreach (BorrowedDawnGreyboxBuilder.RoomSceneSpec spec in BorrowedDawnGreyboxBuilder.Sequence)
            {
                if (!File.Exists(spec.ScenePath))
                {
                    continue;
                }
                Scene scene = EditorSceneManager.OpenScene(spec.ScenePath, OpenSceneMode.Single);
                int count = 0;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (PlayerController2D player in root.GetComponentsInChildren<PlayerController2D>(true))
                    {
                        if (AssignModel(player.gameObject))
                        {
                            count++;
                        }
                    }
                }
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[AnkhuSetup] " + spec.SceneName + ": Ankhu model assigned on " + count + " player(s).");
            }
        }

        /// <summary>Sets the Ankhu prefab on the player's AnkhuVisual. Returns false when the prefab does not exist yet.</summary>
        public static bool AssignModel(GameObject player)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            AnkhuVisual visual = player != null ? player.GetComponentInChildren<AnkhuVisual>(true) : null;
            if (prefab == null || visual == null)
            {
                return false;
            }
            var so = new SerializedObject(visual);
            so.FindProperty("modelPrefab").objectReferenceValue = prefab;
            so.FindProperty("modelHeight").floatValue = L.AnkhuHeight;
            so.FindProperty("baseYaw").floatValue = ModelYaw();
            so.ApplyModifiedPropertiesWithoutUndo();
            visual.Rebuild();
            return true;
        }

        // ------------------------------------------------------------------ helpers

        public static Bounds RendererBounds(GameObject go)
        {
            Bounds b = default;
            bool found = false;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (!found)
                {
                    b = r.bounds;
                    found = true;
                }
                else
                {
                    b.Encapsulate(r.bounds);
                }
            }
            return b;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
