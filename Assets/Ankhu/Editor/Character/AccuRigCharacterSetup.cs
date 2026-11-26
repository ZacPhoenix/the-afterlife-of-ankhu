using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Ankhu.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using L = Ankhu.Layout.BorrowedDawnLayout;
using Object = UnityEngine.Object;

namespace Ankhu.EditorTools.Character
{
    /// <summary>
    /// Import settings for the AccuRig rerigs (Reallusion CC_Base skeleton with five-finger hands).
    /// Models live in Assets/Characters/AccuRig/&lt;Name&gt;/Model, Anpu's staff and ankh in
    /// Assets/Characters/AccuRig/Anpu/Props. The old Mixamo-named rigs stay where they were, so
    /// nothing that uses them changes until the prefabs are swapped on purpose.
    /// </summary>
    public sealed class AccuRigCharacterImport : AssetPostprocessor
    {
        public const string Root = "Assets/Characters/AccuRig";

        private void OnPreprocessModel()
        {
            string p = assetPath.Replace('\\', '/');
            if (!p.StartsWith(Root + "/", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            var importer = (ModelImporter)assetImporter;
            importer.importCameras = false;
            importer.importLights = false;
            importer.useFileScale = true;
            importer.globalScale = 1f;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.isReadable = p.Contains("/Props/");
            if (p.Contains("/Model/"))
            {
                importer.animationType = ModelImporterAnimationType.Human;
                importer.importAnimation = false;
                importer.importBlendShapes = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            }
            else if (p.Contains("/Props/"))
            {
                importer.animationType = ModelImporterAnimationType.None;
                importer.importAnimation = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            }
        }

        private void OnPreprocessTexture()
        {
            string p = assetPath.Replace('\\', '/');
            if (!p.StartsWith(Root + "/", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            var importer = (TextureImporter)assetImporter;
            string n = Path.GetFileNameWithoutExtension(p).ToLowerInvariant();
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            bool normal = n.Contains("normal");
            bool data = n.Contains("metal") || n.Contains("rough") || n.Contains("image_1");
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normal && !data;
        }
    }

    /// <summary>
    /// Builds Humanoid prefabs from the AccuRig rerigs: explicit CC_Base to Humanoid bone map (fingers,
    /// jaw and eyes included), an enforced T-pose in the avatar skeleton (the meshes were rigged in a
    /// 45 degree A-pose), URP Lit materials, and Assets/Characters/AccuRig/&lt;Name&gt;/&lt;Name&gt;_AccuRig.prefab.
    /// Anpu also gets a hold clip (the old staff arm pose plus curled fingers on both hands), his own
    /// controller, and the was-sceptre and ankh parented to his hand bones at grip sockets measured from
    /// the curled fingers. Then renders walk strips (old rig against new) and hand close-ups to
    /// ArtSource/AccuRig/checks and logs foot and grip measurements. Touches no scene.
    ///
    /// Batch: Unity -batchmode -quit -projectPath . -executeMethod Ankhu.EditorTools.Character.AccuRigCharacterSetup.SetupFromCommandLine -logFile accurig_setup.log
    /// </summary>
    public static class AccuRigCharacterSetup
    {
        public static readonly string[] Characters = { "Ankhu", "Anpu", "Guardian" };
        private const string AnimFolder = "Assets/Characters/Ankhu/Animations";
        private const string IdleClipPath = AnimFolder + "/01_Idle_Breathing.fbx";
        private const string WalkClipPath = AnimFolder + "/03_Walk_InPlace.fbx";
        private const string HoldClipPath = "Assets/Characters/Anpu/Animations/Anpu_HoldStaff.fbx";
        private const string ChecksFolder = "ArtSource/AccuRig/checks";

        // Finger curl for the grips. Humanoid "Stretched" muscles: 1 is open, -1 is a closed fist.
        private static readonly float[] StaffCurl = { -0.35f, -0.6f, -0.5f };
        private static readonly float[] AnkhCurl = { -0.55f, -0.8f, -0.65f };
        private static readonly float[] ThumbStaff = { 0.1f, -0.3f, -0.4f };
        private static readonly float[] ThumbAnkh = { 0.0f, -0.45f, -0.55f };

        private static readonly string[,] BoneMap =
        {
            { "Hips", "CC_Base_Hip" }, { "Spine", "CC_Base_Waist" }, { "Chest", "CC_Base_Spine01" }, { "UpperChest", "CC_Base_Spine02" },
            { "Neck", "CC_Base_NeckTwist01" }, { "Head", "CC_Base_Head" }, { "Jaw", "CC_Base_JawRoot" },
            { "LeftEye", "CC_Base_L_Eye" }, { "RightEye", "CC_Base_R_Eye" },
            { "LeftUpperLeg", "CC_Base_L_Thigh" }, { "LeftLowerLeg", "CC_Base_L_Calf" }, { "LeftFoot", "CC_Base_L_Foot" }, { "LeftToes", "CC_Base_L_ToeBase" },
            { "RightUpperLeg", "CC_Base_R_Thigh" }, { "RightLowerLeg", "CC_Base_R_Calf" }, { "RightFoot", "CC_Base_R_Foot" }, { "RightToes", "CC_Base_R_ToeBase" },
            { "LeftShoulder", "CC_Base_L_Clavicle" }, { "LeftUpperArm", "CC_Base_L_Upperarm" }, { "LeftLowerArm", "CC_Base_L_Forearm" }, { "LeftHand", "CC_Base_L_Hand" },
            { "RightShoulder", "CC_Base_R_Clavicle" }, { "RightUpperArm", "CC_Base_R_Upperarm" }, { "RightLowerArm", "CC_Base_R_Forearm" }, { "RightHand", "CC_Base_R_Hand" },
        };

        private static readonly string[] Fingers = { "Thumb", "Index", "Middle", "Ring", "Little" };
        private static string CcFinger(string f) { return f == "Middle" ? "Mid" : f == "Little" ? "Pinky" : f; }
        private static readonly string[] Phalanx = { "Proximal", "Intermediate", "Distal" };

        private static readonly StringBuilder Report = new StringBuilder();

        private static void Log(string msg)
        {
            Debug.Log("[AccuRig] " + msg);
            Report.AppendLine(msg);
        }

        [MenuItem("Ankhu/Character/Set Up AccuRig Rerigs (Ankhu, Anpu, Guardian)")]
        public static void SetupFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("Leave Play mode first.");
                return;
            }
            SetupAll();
        }

        public static void SetupFromCommandLine()
        {
            try
            {
                SetupAll();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        public static void SetupAll() { SetupAll(true); }

        public static void SetupAll(bool compareOld)
        {
            Report.Clear();
            Directory.CreateDirectory(ChecksFolder);
            var prefabs = new Dictionary<string, GameObject>();
            foreach (string c in Characters)
            {
                GameObject p = Setup(c);
                if (p != null)
                {
                    prefabs[c] = p;
                }
            }
            AssetDatabase.SaveAssets();

            // Checks: walk strips old against new, hand close-ups, foot contact numbers.
            AnimatorController ankhuController = AssetDatabase.LoadAssetAtPath<AnimatorController>(AnkhuCharacterSetup.ControllerPath);
            var walkRows = new List<(string label, GameObject prefab, RuntimeAnimatorController ctrl)>();
            foreach (string c in Characters)
            {
                GameObject old = compareOld ? AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Characters/" + c + "/" + c + ".prefab") : null;
                if (old != null)
                {
                    walkRows.Add((c + " old", old, ankhuController));
                }
                if (prefabs.TryGetValue(c, out GameObject np))
                {
                    walkRows.Add((c + " AccuRig", np, np.GetComponent<Animator>().runtimeAnimatorController));
                }
            }
            foreach (var row in walkRows)
            {
                WalkCheck(row.label, row.prefab, row.ctrl);
            }
            if (prefabs.TryGetValue("Anpu", out GameObject anpu))
            {
                HandCheck(anpu);
            }
            File.WriteAllText(Path.Combine(ChecksFolder, "report.txt"), Report.ToString());
            Log("Report written to " + Path.GetFullPath(Path.Combine(ChecksFolder, "report.txt")));
        }

        // ------------------------------------------------------------------ per character

        private static GameObject Setup(string name)
        {
            string root = AccuRigCharacterImport.Root + "/" + name;
            string modelPath = root + "/Model/" + name + "_AccuRig.fbx";
            if (!File.Exists(modelPath))
            {
                Log(name + ": no model at " + modelPath + ", skipped.");
                return null;
            }
            AssetDatabase.ImportAsset(modelPath, ImportAssetOptions.ForceUpdate);
            ConfigureAvatar(name, modelPath);
            BuildMaterials(name, root, modelPath);

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Avatar>().FirstOrDefault();
            if (avatar == null || !avatar.isValid || !avatar.isHuman)
            {
                throw new InvalidOperationException(name + ": avatar is not a valid humanoid.");
            }

            RuntimeAnimatorController controller;
            if (name == "Anpu")
            {
                controller = BuildAnpuController(root, model);
            }
            else
            {
                controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(AnkhuCharacterSetup.ControllerPath);
            }
            return BuildPrefab(name, root, model, avatar, controller);
        }

        private static void ConfigureAvatar(string name, string modelPath)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            var names = new HashSet<string>(model.GetComponentsInChildren<Transform>(true).Select(t => t.name));

            var human = new List<HumanBone>();
            var missing = new List<string>();
            void Add(string h, string b)
            {
                if (!names.Contains(b))
                {
                    missing.Add(b);
                    return;
                }
                var hb = new HumanBone { humanName = h, boneName = b };
                hb.limit.useDefaultValues = true;
                human.Add(hb);
            }
            for (int i = 0; i < BoneMap.GetLength(0); i++)
            {
                Add(BoneMap[i, 0], BoneMap[i, 1]);
            }
            foreach (string side in new[] { "Left", "Right" })
            {
                string s = side == "Left" ? "L" : "R";
                foreach (string f in Fingers)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        Add(side + " " + f + " " + Phalanx[k], "CC_Base_" + s + "_" + CcFinger(f) + (k + 1));
                    }
                }
            }
            if (missing.Count > 0)
            {
                Log(name + ": bones not in the model (left unmapped): " + string.Join(", ", missing));
            }

            HumanDescription hd = importer.humanDescription;
            hd.human = human.ToArray();
            hd.skeleton = TPoseSkeleton(model);
            hd.upperArmTwist = 0.5f;
            hd.lowerArmTwist = 0.5f;
            hd.upperLegTwist = 0.5f;
            hd.lowerLegTwist = 0.5f;
            hd.feetSpacing = 0f;
            hd.hasTranslationDoF = false;
            importer.humanDescription = hd;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.SaveAndReimport();

            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Avatar>().FirstOrDefault();
            Log(string.Format("{0}: avatar valid {1}, human {2}, {3} bones mapped (fingers {4}).", name,
                avatar != null && avatar.isValid, avatar != null && avatar.isHuman, human.Count,
                human.Count(h => Fingers.Any(f => h.humanName.Contains(f)))));
        }

        /// <summary>
        /// The skeleton from the file's bind pose with arms straightened out sideways and legs straight down,
        /// which is the T-pose Humanoid expects. Without it every arm clip plays about 45 degrees too low.
        /// </summary>
        private static SkeletonBone[] TPoseSkeleton(GameObject model)
        {
            GameObject go = Object.Instantiate(model);
            go.name = model.name;
            try
            {
                Transform[] all = go.GetComponentsInChildren<Transform>(true);
                Transform T(string n) { return all.FirstOrDefault(t => t.name == n); }
                Vector3 across = T("CC_Base_R_Thigh").position - T("CC_Base_L_Thigh").position;
                across.y = 0f;
                across.Normalize();

                void Align(string bone, string child, Vector3 dir)
                {
                    Transform b = T(bone), c = T(child);
                    if (b == null || c == null)
                    {
                        return;
                    }
                    Vector3 cur = c.position - b.position;
                    b.rotation = Quaternion.FromToRotation(cur, dir) * b.rotation;
                }
                foreach (string s in new[] { "L", "R" })
                {
                    Vector3 outDir = s == "R" ? across : -across;
                    // Sanity: the hand must already be on that side.
                    if (Vector3.Dot(T("CC_Base_" + s + "_Hand").position - T("CC_Base_" + s + "_Upperarm").position, outDir) < 0f)
                    {
                        outDir = -outDir;
                    }
                    Align("CC_Base_" + s + "_Upperarm", "CC_Base_" + s + "_Forearm", outDir);
                    Align("CC_Base_" + s + "_Forearm", "CC_Base_" + s + "_Hand", outDir);
                    Align("CC_Base_" + s + "_Hand", "CC_Base_" + s + "_Mid1", outDir);
                    Align("CC_Base_" + s + "_Thigh", "CC_Base_" + s + "_Calf", Vector3.down);
                    Align("CC_Base_" + s + "_Calf", "CC_Base_" + s + "_Foot", Vector3.down);
                }
                return all.Select(t => new SkeletonBone
                {
                    name = t.name,
                    position = t.localPosition,
                    rotation = t.localRotation,
                    scale = t.localScale,
                }).ToArray();
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // ------------------------------------------------------------------ materials

        private static void BuildMaterials(string name, string root, string modelPath)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
            string texFolder = root + "/Textures";
            string matFolder = root + "/Materials";
            EnsureFolder(texFolder);
            EnsureFolder(matFolder);
            if (!AssetDatabase.FindAssets("t:Texture2D", new[] { texFolder }).Any())
            {
                importer.ExtractTextures(texFolder);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                FixExtensions(texFolder);
            }
            var textures = AssetDatabase.FindAssets("t:Texture2D", new[] { texFolder })
                .Select(g => AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(g))).Where(t => t != null).ToList();
            Log(name + ": textures " + string.Join(", ", textures.Select(t => t.name + " " + t.width)));

            foreach (KeyValuePair<AssetImporter.SourceAssetIdentifier, Object> kv in importer.GetExternalObjectMap().ToList())
            {
                importer.RemoveRemap(kv.Key);
            }
            importer.SaveAndReimport();
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            foreach (Material src in AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Material>().ToList())
            {
                string key = src.name.Replace("_Pbr", "");
                Texture2D baseMap = textures.FirstOrDefault(t => t.name.StartsWith(src.name) && !t.name.ToLowerInvariant().Contains("normal"))
                                    ?? textures.FirstOrDefault(t => t.name.StartsWith(key) && !t.name.ToLowerInvariant().Contains("normal"));
                Texture2D normal = textures.FirstOrDefault(t => t.name.StartsWith(src.name) && t.name.ToLowerInvariant().Contains("normal"));
                string matPath = matFolder + "/M_" + name + "_" + Sanitize(src.name) + ".mat";
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null)
                {
                    mat = new Material(lit);
                    AssetDatabase.CreateAsset(mat, matPath);
                }
                mat.shader = lit;
                mat.SetTexture("_BaseMap", baseMap);
                Color col = baseMap != null ? Color.white : (src.HasProperty("_Color") ? src.GetColor("_Color") : Color.grey);
                mat.SetColor("_BaseColor", col);
                mat.SetFloat("_Metallic", 0f);
                mat.SetFloat("_Smoothness", name == "Anpu" ? 0.45f : 0.35f);
                if (normal != null)
                {
                    mat.SetTexture("_BumpMap", normal);
                    mat.EnableKeyword("_NORMALMAP");
                }
                else
                {
                    mat.SetTexture("_BumpMap", null);
                    mat.DisableKeyword("_NORMALMAP");
                }
                EditorUtility.SetDirty(mat);
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), src.name), mat);
                Log(string.Format("{0}: material {1} -> {2} (base {3}, normal {4}, colour {5})", name, src.name, matPath,
                    baseMap != null ? baseMap.name : "none", normal != null ? normal.name : "none", col));
            }
            importer.SaveAndReimport();
        }

        /// <summary>Embedded images sometimes extract without an extension. Name them from the file header.</summary>
        private static void FixExtensions(string folder)
        {
            bool moved = false;
            foreach (string file in Directory.GetFiles(folder))
            {
                if (file.EndsWith(".meta") || Path.HasExtension(file))
                {
                    continue;
                }
                byte[] head = new byte[4];
                using (FileStream fs = File.OpenRead(file))
                {
                    fs.Read(head, 0, 4);
                }
                string ext = head[0] == 0x89 && head[1] == 0x50 ? ".png" : head[0] == 0xFF && head[1] == 0xD8 ? ".jpg" : null;
                if (ext == null)
                {
                    continue;
                }
                File.Move(file, file + ext);
                if (File.Exists(file + ".meta"))
                {
                    File.Delete(file + ".meta");
                }
                moved = true;
            }
            if (moved)
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
        }

        // ------------------------------------------------------------------ Anpu: hold, grip, props

        private static AnimatorController BuildAnpuController(string root, GameObject model)
        {
            AnimationClip idle = LoadClip(IdleClipPath);
            AnimationClip walk = LoadClip(WalkClipPath);
            AnimationClip hold = LoadClip(HoldClipPath);
            AnimationClip grip = BuildHoldGripClip(root, null);

            float implied = AnkhuCharacterSetup.MeasureGroundSpeed(model, walk);
            float walkScale = implied > 0.05f ? Mathf.Clamp(L.WalkSpeed / implied, 0.6f, 2.4f) : 1f;
            Log(string.Format("Anpu: walk clip implies {0:0.00} m/s on the new rig, plays at x{1:0.00} for {2:0.00} m/s.", implied, walkScale, L.WalkSpeed));

            string maskPath = root + "/Anpu_AccuRig_Hold.mask";
            AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(maskPath);
            if (mask == null)
            {
                mask = new AvatarMask();
                AssetDatabase.CreateAsset(mask, maskPath);
            }
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            {
                var part = (AvatarMaskBodyPart)i;
                mask.SetHumanoidBodyPartActive(part, part == AvatarMaskBodyPart.RightArm || part == AvatarMaskBodyPart.RightFingers || part == AvatarMaskBodyPart.LeftFingers);
            }
            EditorUtility.SetDirty(mask);

            string path = root + "/Anpu_AccuRig.controller";
            AssetDatabase.DeleteAsset(path);
            AnimatorController ac = AnimatorController.CreateAnimatorControllerAtPath(path);
            ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
            AnimatorStateMachine sm = ac.layers[0].stateMachine;
            AnimatorState loco = ac.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
            loco.iKOnFeet = true;
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            tree.AddChild(idle, 0f);
            tree.AddChild(walk, L.WalkSpeed);
            ChildMotion[] ch = tree.children;
            ch[1].timeScale = walkScale;
            tree.children = ch;
            sm.defaultState = loco;

            ac.AddLayer("HoldStaff"); // Same layer name as the old Anpu.controller, so the opening's escort controller copies it.
            AnimatorControllerLayer[] layers = ac.layers;
            layers[1].defaultWeight = 1f;
            layers[1].avatarMask = mask;
            layers[1].blendingMode = AnimatorLayerBlendingMode.Override;
            ac.layers = layers;
            AnimatorState h = ac.layers[1].stateMachine.AddState("HoldGrip");
            h.motion = grip;
            ac.layers[1].stateMachine.defaultState = h;
            EditorUtility.SetDirty(ac);
            Log("Anpu: controller " + path + " (idle/walk blend on Speed, HoldGrip layer on right arm and both hands' fingers)");
            return ac;
        }

        /// <summary>The old staff arm pose with both hands' fingers curled. Right hand wraps the staff, left the ankh loop.</summary>
        private static AnimationClip BuildHoldGripClip(string root, AnimationClip hold)
        {
            var clip = new AnimationClip { name = "Anpu_HoldGrip", frameRate = 30f };
            if (hold != null)
            {
                foreach (EditorCurveBinding b in AnimationUtility.GetCurveBindings(hold))
                {
                    if (b.propertyName.Contains("Hand.") && (b.propertyName.Contains("Stretched") || b.propertyName.Contains("Spread")))
                    {
                        continue;
                    }
                    AnimationUtility.SetEditorCurve(clip, b, AnimationUtility.GetEditorCurve(hold, b));
                }
            }
            else
            {
                Log("Anpu: hold clip starts with fingers only, the arm pose is solved later.");
            }
            void Muscle(string prop, float v)
            {
                var b = EditorCurveBinding.FloatCurve("", typeof(Animator), prop);
                AnimationUtility.SetEditorCurve(clip, b, AnimationCurve.Constant(0f, 1f, v));
            }
            foreach (string side in new[] { "Right", "Left" })
            {
                float[] curl = side == "Right" ? StaffCurl : AnkhCurl;
                float[] thumb = side == "Right" ? ThumbStaff : ThumbAnkh;
                foreach (string f in Fingers)
                {
                    float[] c = f == "Thumb" ? thumb : curl;
                    for (int k = 0; k < 3; k++)
                    {
                        Muscle(side + "Hand." + f + "." + (k + 1) + " Stretched", c[k]);
                    }
                    Muscle(side + "Hand." + f + ".Spread", 0f);
                }
            }
            AnimationClipSettings s = AnimationUtility.GetAnimationClipSettings(clip);
            s.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, s);
            string path = root + "/Anpu_HoldGrip.anim";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        private class PropSpec
        {
            public string File;
            public string Hand;          // CC_Base_R_Hand or CC_Base_L_Hand
            public string Side;          // R or L
            public bool DownIsShorterEnd; // staff: the grip sits nearer the foot. Ankh: the loop is at the origin, the shaft hangs.
            public bool Vertical;        // hangs or stands plumb in the grip pose
            public bool Floor = true;    // slide along the long axis so the low end rests on the floor
        }

        private static readonly PropSpec[] Props =
        {
            new PropSpec { File = "Anpu_Prop_WasSceptre", Hand = "CC_Base_R_Hand", Side = "R", DownIsShorterEnd = false, Vertical = true },
            new PropSpec { File = "Anpu_Prop_Ankh", Hand = "CC_Base_L_Hand", Side = "L", DownIsShorterEnd = false, Vertical = true, Floor = false },
        };

        // ---- grip geometry helpers

        private static readonly string[] RightArmMuscles =
        {
            "Right Shoulder Down-Up", "Right Shoulder Front-Back", "Right Arm Down-Up", "Right Arm Front-Back",
            "Right Arm Twist In-Out", "Right Forearm Stretch", "Right Forearm Twist In-Out", "Right Hand Down-Up", "Right Hand In-Out",
        };
        private static readonly float[] CurlPattern = { 0.6f, 1f, 0.8f };

        private static int MuscleIndex(string name)
        {
            int i = Array.IndexOf(HumanTrait.MuscleName, name);
            if (i < 0)
            {
                throw new InvalidOperationException("No humanoid muscle named " + name);
            }
            return i;
        }

        private static Vector3 Circum(Vector3 a, Vector3 b, Vector3 c, out float r, out Vector3 normal)
        {
            Vector3 ab = b - a, ac = c - a;
            Vector3 n = Vector3.Cross(ab, ac);
            float d = 2f * n.sqrMagnitude;
            normal = n.normalized;
            if (d < 1e-12f)
            {
                r = 0f;
                return (a + b + c) / 3f;
            }
            Vector3 o = a + (Vector3.Cross(n, ab) * ac.sqrMagnitude + Vector3.Cross(ac, n) * ab.sqrMagnitude) / d;
            r = (o - a).magnitude;
            return o;
        }

        /// <summary>
        /// The hole a curled hand makes: per finger (index, middle, ring) the circle through the knuckle, the middle
        /// joint and the fingertip. Returns the mean centre and radius, and the hole's axis pointing from index to little finger.
        /// </summary>
        private static Vector3 FistHole(Transform[] bones, string side, out float radius, out Vector3 axis)
        {
            Transform B(string n) { return bones.First(t => t.name == n); }
            Vector3 sum = Vector3.zero, nsum = Vector3.zero;
            float rsum = 0f;
            foreach (string f in new[] { "Index", "Mid", "Ring" })
            {
                Vector3 j1 = B("CC_Base_" + side + "_" + f + "1").position;
                Vector3 j2 = B("CC_Base_" + side + "_" + f + "2").position;
                Vector3 j3 = B("CC_Base_" + side + "_" + f + "3").position;
                Vector3 tip = j3 + (j3 - j2) * 0.9f;
                sum += Circum(j1, j2, tip, out float r, out Vector3 n);
                rsum += r;
                nsum += n;
            }
            Vector3 pinkyward = B("CC_Base_" + side + "_Pinky1").position - B("CC_Base_" + side + "_Index1").position;
            axis = nsum.normalized;
            if (Vector3.Dot(axis, pinkyward) < 0f)
            {
                axis = -axis;
            }
            radius = rsum / 3f;
            return sum / 3f;
        }

        private static void SetCurl(float[] muscles, string side, float k, float[] thumb)
        {
            string s = side == "R" ? "Right" : "Left";
            foreach (string f in Fingers)
            {
                for (int j = 0; j < 3; j++)
                {
                    float v = f == "Thumb" ? thumb[j] : Mathf.Clamp(-k * CurlPattern[j], -1f, 1f);
                    muscles[MuscleIndex(s + " " + f + " " + (j + 1) + " Stretched")] = v;
                }
                muscles[MuscleIndex(s + " " + f + " Spread")] = 0f;
            }
        }

        private static Dictionary<Transform, (Vector3, Quaternion)> SnapshotBones(GameObject go)
        {
            return go.GetComponentsInChildren<Transform>(true).ToDictionary(t => t, t => (t.localPosition, t.localRotation));
        }

        private static void RestoreBones(Dictionary<Transform, (Vector3, Quaternion)> snap)
        {
            foreach (var kv in snap)
            {
                if (kv.Key != null)
                {
                    kv.Key.localPosition = kv.Value.Item1;
                    kv.Key.localRotation = kv.Value.Item2;
                }
            }
        }

        private static float PropRadiusAtOrigin(string root, string file)
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(root + "/Props/" + file + ".fbx");
            MeshFilter mf = asset.GetComponentInChildren<MeshFilter>();
            Vector3[] v = mf.sharedMesh.vertices.Select(p => asset.transform.InverseTransformPoint(mf.transform.TransformPoint(p))).ToArray();
            Vector3 ext = v.Aggregate(Vector3.negativeInfinity, Vector3.Max) - v.Aggregate(Vector3.positiveInfinity, Vector3.Min);
            int ax = ext.x >= ext.y && ext.x >= ext.z ? 0 : ext.y >= ext.z ? 1 : 2;
            int a1 = (ax + 1) % 3, a2 = (ax + 2) % 3;
            var near = v.Where(p => Mathf.Abs(p[ax]) < 0.03f).ToArray();
            if (near.Length < 3)
            {
                return 0.02f;
            }
            float w = Mathf.Min(near.Max(p => p[a1]) - near.Min(p => p[a1]), near.Max(p => p[a2]) - near.Min(p => p[a2]));
            return w * 0.5f;
        }

        /// <summary>
        /// Builds Anpu's holding pose on the new hands. Fingers: curl each hand until the hole it makes fits its prop
        /// (staff shaft, ankh loop) plus finger thickness. Right arm: coordinate descent on the nine arm and wrist
        /// muscles so the fist sits beside and ahead of the hip with its hole plumb, the way he carries the
        /// was-sceptre upright. Writes the result into Anpu_HoldGrip.anim. Left arm keeps the walk's swing.
        /// </summary>
        private static HumanPose solvedPose;

        private static void SolveHoldPose(GameObject instance, string root)
        {
            Animator animator = instance.GetComponent<Animator>();
            Transform[] bones = instance.GetComponentsInChildren<Transform>(true);
            var snap = SnapshotBones(instance);
            var handler = new HumanPoseHandler(animator.avatar, instance.transform);
            var pose = new HumanPose();
            AnimationClip idle = LoadClip(IdleClipPath);
            AnimationMode.StartAnimationMode();
            AnkhuCharacterSetup.SamplePose(instance, idle, 0f);
            handler.GetHumanPose(ref pose);
            AnimationMode.StopAnimationMode();
            float[] m0 = (float[])pose.muscles.Clone();
            try
            {
                const float FingerHalfThickness = 0.011f;
                float staffR = PropRadiusAtOrigin(root, "Anpu_Prop_WasSceptre");
                float ankhR = PropRadiusAtOrigin(root, "Anpu_Prop_Ankh");
                var curl = new Dictionary<string, float>();
                foreach (var (side, propR, thumb) in new[] { ("R", staffR, ThumbStaff), ("L", ankhR, ThumbAnkh) })
                {
                    float target = propR + FingerHalfThickness;
                    float bestK = 0.6f, bestErr = float.PositiveInfinity, bestR = 0f;
                    for (float k = 0.2f; k <= 1.6f; k += 0.02f)
                    {
                        SetCurl(pose.muscles, side, k, thumb);
                        handler.SetHumanPose(ref pose);
                        FistHole(bones, side, out float r, out _);
                        float err = Mathf.Abs(r - target);
                        if (err < bestErr)
                        {
                            bestErr = err;
                            bestK = k;
                            bestR = r;
                        }
                    }
                    SetCurl(pose.muscles, side, bestK, thumb);
                    curl[side] = bestK;
                    Log(string.Format("Anpu: {0} hand curl x{1:0.00}, fist hole radius {2:0.000} m for a {3:0.000} m prop radius (target {4:0.000}).", side, bestK, bestR, propR, target));
                }

                Transform shoulderL = bones.First(t => t.name == "CC_Base_L_Upperarm");
                Transform shoulderR = bones.First(t => t.name == "CC_Base_R_Upperarm");
                Vector3 right = shoulderR.position - shoulderL.position;
                right.y = 0f;
                right.Normalize();
                Vector3 fwd = Vector3.Cross(right, Vector3.up).normalized;
                if (Vector3.Dot(fwd, Vector3.forward) < 0f)
                {
                    fwd = -fwd;
                }
                float height = AnkhuCharacterSetup.BakedHeightSpan(instance).y;
                Vector3 target3 = new Vector3(shoulderR.position.x, 0f, shoulderR.position.z) + right * 0.06f + fwd * 0.30f + Vector3.up * (0.56f * height);
                int[] idx = RightArmMuscles.Select(MuscleIndex).ToArray();
                float Cost()
                {
                    handler.SetHumanPose(ref pose);
                    Vector3 c = FistHole(bones, "R", out _, out Vector3 axis);
                    float pos = (c - target3).magnitude / 0.05f;
                    float ang = Vector3.Angle(axis, Vector3.down) / 10f;
                    float reg = 0f;
                    foreach (int i in idx)
                    {
                        reg += (pose.muscles[i] - m0[i]) * (pose.muscles[i] - m0[i]);
                    }
                    return pos * pos + ang * ang + 0.05f * reg;
                }
                float best = Cost();
                float start = best;
                foreach (float step in new[] { 0.2f, 0.1f, 0.05f, 0.02f, 0.01f })
                {
                    for (int pass = 0; pass < 40; pass++)
                    {
                        bool improved = false;
                        foreach (int i in idx)
                        {
                            foreach (float s in new[] { step, -step })
                            {
                                float old = pose.muscles[i];
                                pose.muscles[i] = Mathf.Clamp(old + s, -1f, 1f);
                                float c = Cost();
                                if (c < best - 1e-5f)
                                {
                                    best = c;
                                    improved = true;
                                }
                                else
                                {
                                    pose.muscles[i] = old;
                                }
                            }
                        }
                        if (!improved)
                        {
                            break;
                        }
                    }
                }
                handler.SetHumanPose(ref pose);
                solvedPose = pose;
                solvedPose.muscles = (float[])pose.muscles.Clone();
                Vector3 hole = FistHole(bones, "R", out _, out Vector3 holeAxis);
                Log(string.Format("Anpu: right arm hold pose solved, cost {0:0.00} -> {1:0.00}. Fist at {2} (target {3}), off by {4:0.000} m, hole {5:0.0} deg from plumb.",
                    start, best, hole.ToString("F3"), target3.ToString("F3"), (hole - target3).magnitude, Vector3.Angle(holeAxis, Vector3.down)));

                // Write the clip: right arm and both hands' fingers.
                string clipPath = root + "/Anpu_HoldGrip.anim";
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                foreach (EditorCurveBinding b in AnimationUtility.GetCurveBindings(clip))
                {
                    AnimationUtility.SetEditorCurve(clip, b, null);
                }
                void Put(string prop, float v)
                {
                    AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), prop), AnimationCurve.Constant(0f, 1f, v));
                }
                foreach (string mname in RightArmMuscles)
                {
                    Put(mname, pose.muscles[MuscleIndex(mname)]);
                }
                foreach (string side in new[] { "Right", "Left" })
                {
                    foreach (string f in Fingers)
                    {
                        for (int j = 1; j <= 3; j++)
                        {
                            Put(side + "Hand." + f + "." + j + " Stretched", pose.muscles[MuscleIndex(side + " " + f + " " + j + " Stretched")]);
                        }
                        Put(side + "Hand." + f + ".Spread", pose.muscles[MuscleIndex(side + " " + f + " Spread")]);
                    }
                }
                EditorUtility.SetDirty(clip);
                AssetDatabase.SaveAssets();
            }
            finally
            {
                handler.Dispose();
                RestoreBones(snap);
            }
        }

        /// <summary>
        /// Puts each prop in its hand in the controller's idle pose (hold layer on): through the centre of the fist
        /// hole, staff plumb with its foot on the floor and its head facing forward, ankh hanging plumb from its loop
        /// with the crossbar along the fist. Parents each to a socket under the hand bone so it follows every clip.
        /// </summary>
        private static void AttachProps(GameObject instance, string root)
        {
            Animator animator = instance.GetComponent<Animator>();
            Transform[] bones = instance.GetComponentsInChildren<Transform>(true);
            var snap = SnapshotBones(instance);
            var handler = new HumanPoseHandler(animator.avatar, instance.transform);
            try
            {
                // The solved idle-with-hold pose: idle body and left arm, solved right arm, both hands curled.
                HumanPose held = solvedPose;
                handler.SetHumanPose(ref held);
                foreach (PropSpec spec in Props)
                {
                    GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(root + "/Props/" + spec.File + ".fbx");
                    Transform hand = bones.FirstOrDefault(t => t.name == spec.Hand);
                    Vector3 centre = FistHole(bones, spec.Side, out float r, out Vector3 holeAxis);
                    Log(string.Format("Anpu idle: {0} fist hole centre {1}, radius {2:0.000}, axis {3} ({4:0} deg from plumb)",
                        spec.File, centre.ToString("F3"), r, holeAxis.ToString("F2"), Vector3.Angle(holeAxis, Vector3.down)));

                    GameObject prop = (GameObject)PrefabUtility.InstantiatePrefab(asset);
                    prop.name = spec.File.Replace("Anpu_", "");
                    MeshFilter mf = prop.GetComponentInChildren<MeshFilter>();
                    Vector3[] verts = mf.sharedMesh.vertices.Select(v => prop.transform.InverseTransformPoint(mf.transform.TransformPoint(v))).ToArray();
                    Vector3 min = verts.Aggregate(Vector3.positiveInfinity, Vector3.Min);
                    Vector3 max = verts.Aggregate(Vector3.negativeInfinity, Vector3.Max);
                    Vector3 ext = max - min;
                    int ax = ext.x >= ext.y && ext.x >= ext.z ? 0 : ext.y >= ext.z ? 1 : 2;
                    Vector3 axis = Vector3.zero;
                    axis[ax] = 1f;
                    bool positiveShorter = max[ax] < -min[ax];
                    Vector3 downLocal = (spec.DownIsShorterEnd == positiveShorter) ? axis : -axis;
                    Vector3 faceLocal = FacingAxis(verts, ax, downLocal, spec.Floor);

                    Vector3 down = Vector3.down;
                    Vector3 fwd = spec.Floor ? Vector3.forward : Vector3.ProjectOnPlane(holeAxis, Vector3.up);
                    if (fwd.sqrMagnitude < 1e-4f)
                    {
                        fwd = Vector3.forward;
                    }
                    fwd.Normalize();
                    Quaternion rot = Quaternion.LookRotation(fwd, -down) * Quaternion.Inverse(Quaternion.LookRotation(Vector3.ProjectOnPlane(faceLocal, downLocal).normalized, -downLocal));
                    Vector3 pos = centre;
                    if (spec.Floor)
                    {
                        float footBelowOrigin = (spec.DownIsShorterEnd == positiveShorter) ? max[ax] : -min[ax];
                        pos = new Vector3(centre.x, footBelowOrigin, centre.z);
                        Log(string.Format("Anpu: staff slides {0:0.000} m along the shaft so its foot meets the floor (fist at {1:0.000} m).", pos.y - centre.y, centre.y));
                    }
                    var socket = new GameObject("Socket_" + prop.name).transform;
                    socket.SetParent(hand, false);
                    // Socket pivot at the fist, so anything that turns the socket (PropUpright) turns the prop about the grip.
                    socket.SetPositionAndRotation(centre, rot);
                    prop.transform.SetParent(socket, false);
                    prop.transform.localRotation = Quaternion.identity;
                    prop.transform.localScale = Vector3.one;
                    prop.transform.position = pos;
                    Log(string.Format("Anpu: {0} socket local pos {1} rot {2} under {3}", prop.name, socket.localPosition.ToString("F3"), socket.localEulerAngles.ToString("F1"), hand.name));
                }
            }
            finally
            {
                handler.Dispose();
                // Sockets keep their local offsets. Put the bones back to bind pose before the prefab is saved.
                RestoreBones(snap.Where(kv => kv.Key != null && !kv.Key.name.StartsWith("Socket_")).ToDictionary(kv => kv.Key, kv => kv.Value));
            }
        }


        private static Vector3 FacingAxis(Vector3[] verts, int ax, Vector3 downLocal, bool staffHead)
        {
            float lo = verts.Min(v => v[ax]);
            float hi = verts.Max(v => v[ax]);
            int a1 = (ax + 1) % 3, a2 = (ax + 2) % 3;
            Vector3 Dir(int i) { Vector3 d = Vector3.zero; d[i] = 1f; return d; }
            if (staffHead)
            {
                // Head end is the up end. Its mean offset from the shaft line points where the head faces.
                float upSign = Vector3.Dot(downLocal, Dir(ax)) > 0 ? -1f : 1f;
                float len = hi - lo;
                var head = verts.Where(v => upSign > 0 ? v[ax] > hi - 0.08f * len : v[ax] < lo + 0.08f * len).ToArray();
                var shaft = verts.Where(v => v[ax] > lo + 0.4f * len && v[ax] < lo + 0.6f * len).ToArray();
                Vector3 hc = head.Aggregate(Vector3.zero, (s, v) => s + v) / Mathf.Max(1, head.Length);
                Vector3 sc = shaft.Aggregate(Vector3.zero, (s, v) => s + v) / Mathf.Max(1, shaft.Length);
                Vector3 off = hc - sc;
                off[ax] = 0f;
                return off.sqrMagnitude > 1e-6f ? off.normalized : Dir(a1);
            }
            // Ankh: the crossbar is the widest slice. Its long direction faces forward.
            float best = -1f;
            int bestAxis = a1;
            int slices = 12;
            for (int i = 0; i < slices; i++)
            {
                float s0 = lo + (hi - lo) * i / slices, s1 = lo + (hi - lo) * (i + 1) / slices;
                var sl = verts.Where(v => v[ax] >= s0 && v[ax] < s1).ToArray();
                if (sl.Length < 3)
                {
                    continue;
                }
                foreach (int a in new[] { a1, a2 })
                {
                    float w = sl.Max(v => v[a]) - sl.Min(v => v[a]);
                    if (w > best)
                    {
                        best = w;
                        bestAxis = a;
                    }
                }
            }
            return Dir(bestAxis);
        }

        private static void BuildPropMaterials(string root)
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            foreach (PropSpec spec in Props)
            {
                string propPath = root + "/Props/" + spec.File + ".fbx";
                var importer = AssetImporter.GetAtPath(propPath) as ModelImporter;
                if (importer == null)
                {
                    continue;
                }
                string dir = root + "/Props/";
                string[] pngs = Directory.GetFiles(dir, spec.File + "_*.png").Select(p => p.Replace('\\', '/')).ToArray();
                Texture2D baseMap = pngs.Where(p => p.Contains("_Image_0")).Select(AssetDatabase.LoadAssetAtPath<Texture2D>).FirstOrDefault();
                Texture2D normal = pngs.Where(p => p.ToLowerInvariant().Contains("_normal")).Select(AssetDatabase.LoadAssetAtPath<Texture2D>).FirstOrDefault();
                string matPath = dir + "M_" + spec.File + ".mat";
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null)
                {
                    mat = new Material(lit);
                    AssetDatabase.CreateAsset(mat, matPath);
                }
                mat.SetTexture("_BaseMap", baseMap);
                mat.SetColor("_BaseColor", Color.white);
                mat.SetFloat("_Metallic", spec.Side == "L" ? 0.6f : 0f);
                mat.SetFloat("_Smoothness", spec.Side == "L" ? 0.55f : 0.35f);
                if (normal != null)
                {
                    mat.SetTexture("_BumpMap", normal);
                    mat.EnableKeyword("_NORMALMAP");
                }
                EditorUtility.SetDirty(mat);
                foreach (Material src in AssetDatabase.LoadAllAssetsAtPath(propPath).OfType<Material>())
                {
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), src.name), mat);
                }
                importer.SaveAndReimport();
                Log(string.Format("Anpu: prop {0} material base {1}, normal {2}", spec.File, baseMap != null ? baseMap.name : "none", normal != null ? normal.name : "none"));
            }
        }

        // ------------------------------------------------------------------ prefab

        private static GameObject BuildPrefab(string name, string root, GameObject model, Avatar avatar, RuntimeAnimatorController controller)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            try
            {
                instance.name = name;
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                Animator animator = instance.GetComponent<Animator>() ?? instance.AddComponent<Animator>();
                animator.avatar = avatar;
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                foreach (SkinnedMeshRenderer smr in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    smr.receiveShadows = true;
                    smr.updateWhenOffscreen = false;
                }
                Vector2 span = AnkhuCharacterSetup.BakedHeightSpan(instance);
                foreach (SkinnedMeshRenderer smr in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    FitBounds(smr);
                }
                if (name == "Ankhu")
                {
                    if (instance.GetComponent<AnkhuAnimatorDriver>() == null)
                    {
                        instance.AddComponent<AnkhuAnimatorDriver>();
                    }
                    AnkhuModelMetrics metrics = instance.GetComponent<AnkhuModelMetrics>() ?? instance.AddComponent<AnkhuModelMetrics>();
                    metrics.Set(span.y - span.x, span.x);
                }
                if (name == "Anpu")
                {
                    BuildPropMaterials(root);
                    SolveHoldPose(instance, root);
                    AttachProps(instance, root);
                }
                Log(string.Format("{0}: bind-pose soles y {1:0.000}, crown y {2:0.000}, height {3:0.000} m", name, span.x, span.y, span.y - span.x));
                string path = root + "/" + name + "_AccuRig.prefab";
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(instance, path, out bool ok);
                if (!ok)
                {
                    throw new InvalidOperationException("Could not save " + path);
                }
                Log(name + ": prefab " + path);
                return saved;
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        /// <summary>Skinned bounds fitted to the bind-pose mesh in root-bone space, with 0.3 m of slack for swinging limbs and props.</summary>
        private static void FitBounds(SkinnedMeshRenderer smr)
        {
            var baked = new Mesh();
            try
            {
                smr.BakeMesh(baked, true);
                Transform t = smr.transform;
                Transform rootBone = smr.rootBone != null ? smr.rootBone : t;
                Vector3 lo = Vector3.positiveInfinity, hi = Vector3.negativeInfinity;
                foreach (Vector3 v in baked.vertices)
                {
                    Vector3 l = rootBone.InverseTransformPoint(t.position + t.rotation * v);
                    lo = Vector3.Min(lo, l);
                    hi = Vector3.Max(hi, l);
                }
                var b = new Bounds();
                b.SetMinMax(lo - Vector3.one * 0.3f, hi + Vector3.one * 0.3f);
                smr.localBounds = b;
            }
            finally
            {
                Object.DestroyImmediate(baked);
            }
        }

        // ------------------------------------------------------------------ checks

        /// <summary>
        /// Plays the controller through a manual PlayableGraph at walking speed, renders 8 side-on frames
        /// over one stride, and logs the lowest vertex per frame (floor contact) and how far the planted foot slides.
        /// </summary>
        private static void WalkCheck(string label, GameObject prefab, RuntimeAnimatorController ctrl)
        {
            if (prefab == null || ctrl == null)
            {
                Log(label + ": walk check skipped (no prefab or controller).");
                return;
            }
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Animator animator = go.GetComponent<Animator>();
            animator.runtimeAnimatorController = ctrl;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            PlayableGraph graph = default;
            try
            {
                AnimatorControllerPlayable acp = AnimationPlayableUtilities.PlayAnimatorController(animator, ctrl, out graph);
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                acp.SetFloat("Speed", L.WalkSpeed);
                const float dt = 1f / 30f;
                for (int i = 0; i < 60; i++)
                {
                    graph.Evaluate(dt);
                }
                Transform lf = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                Transform rf = animator.GetBoneTransform(HumanBodyBones.RightFoot);
                Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                float lowMin = float.PositiveInfinity, lowMax = float.NegativeInfinity, hipMin = float.PositiveInfinity, hipMax = float.NegativeInfinity;
                float slide = 0f;
                int slideN = 0;
                Vector3 pl = lf.position, pr = rf.position;
                const int N = 36;
                var frames = new List<Texture2D>();
                for (int i = 0; i < N; i++)
                {
                    graph.Evaluate(dt);
                    Vector2 span = AnkhuCharacterSetup.BakedHeightSpan(go);
                    lowMin = Mathf.Min(lowMin, span.x);
                    lowMax = Mathf.Max(lowMax, span.x);
                    hipMin = Mathf.Min(hipMin, hips.position.y);
                    hipMax = Mathf.Max(hipMax, hips.position.y);
                    foreach (var (now, prev) in new[] { (lf.position, pl), (rf.position, pr) })
                    {
                        if (now.y < Mathf.Min(lf.position.y, rf.position.y) + 0.02f)
                        {
                            Vector3 d = now - prev;
                            d.y = 0f;
                            slide += d.magnitude / dt;
                            slideN++;
                        }
                    }
                    pl = lf.position;
                    pr = rf.position;
                    if (i % 4 == 0 && frames.Count < 9)
                    {
                        Texture2D f = RenderFrame(go, new Vector3(5f, 1.1f, 0f), Quaternion.Euler(0f, 270f, 0f), 1.25f, 360, 540);
                        if (f != null)
                        {
                            frames.Add(f);
                        }
                    }
                }
                Log(string.Format("{0}: walk at {1:0.00} m/s. Lowest vertex per frame ranges {2:0.000} to {3:0.000} m (0 is floor contact). Hips bob {4:0.000} m. Planted foot slides {5:0.00} m/s against the body (ideal {1:0.00}).",
                    label, L.WalkSpeed, lowMin, lowMax, hipMax - hipMin, slideN > 0 ? slide / slideN : 0f));
                SaveStrip(frames, Path.Combine(ChecksFolder, "walk_" + label.Replace(" ", "_") + ".png"));
            }
            catch (Exception e)
            {
                Log(label + ": walk check failed: " + e.Message);
            }
            finally
            {
                if (graph.IsValid())
                {
                    graph.Destroy();
                }
                Object.DestroyImmediate(go);
            }
        }

        private static void HandCheck(GameObject prefab)
        {
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            Animator animator = go.GetComponent<Animator>();
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            PlayableGraph graph = default;
            try
            {
                AnimatorControllerPlayable acp = AnimationPlayableUtilities.PlayAnimatorController(animator, animator.runtimeAnimatorController, out graph);
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                acp.SetFloat("Speed", 0f);
                for (int i = 0; i < 30; i++)
                {
                    graph.Evaluate(1f / 30f);
                }
                var frames = new List<Texture2D>();
                frames.Add(RenderFrame(go, new Vector3(0f, 1.15f, 5f), Quaternion.Euler(0f, 180f, 0f), 1.25f, 400, 540));
                frames.Add(RenderFrame(go, new Vector3(5f, 1.15f, 0f), Quaternion.Euler(0f, 270f, 0f), 1.25f, 400, 540));
                foreach (HumanBodyBones hb in new[] { HumanBodyBones.RightHand, HumanBodyBones.LeftHand })
                {
                    Vector3 h = animator.GetBoneTransform(hb).position;
                    frames.Add(RenderFrame(go, h + new Vector3(0f, 0f, 2f), Quaternion.Euler(0f, 180f, 0f), 0.16f, 400, 540));
                    frames.Add(RenderFrame(go, h + new Vector3(2f, 0f, 0f), Quaternion.Euler(0f, 270f, 0f), 0.16f, 400, 540));
                    frames.Add(RenderFrame(go, h + new Vector3(-2f, 0f, 0f), Quaternion.Euler(0f, 90f, 0f), 0.16f, 400, 540));
                }
                SaveStrip(frames.Where(f => f != null).ToList(), Path.Combine(ChecksFolder, "anpu_grip.png"));
                foreach (Transform s in go.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Socket_")))
                {
                    Renderer r = s.GetComponentInChildren<Renderer>();
                    Log(string.Format("Anpu idle: {0} under {1}, prop bounds min {2} max {3}", s.name, s.parent.name,
                        r != null ? r.bounds.min.ToString("F3") : "-", r != null ? r.bounds.max.ToString("F3") : "-"));
                }
            }
            catch (Exception e)
            {
                Log("Anpu hand check failed: " + e.Message);
            }
            finally
            {
                if (graph.IsValid())
                {
                    graph.Destroy();
                }
                Object.DestroyImmediate(go);
            }
        }

        private static Texture2D RenderFrame(GameObject subject, Vector3 camPos, Quaternion camRot, float orthoSize, int w, int h)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                return null;
            }
            var camGo = new GameObject("AccuRigCheckCamera");
            var lightGo = new GameObject("AccuRigCheckLight");
            var fillGo = new GameObject("AccuRigCheckFill");
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            try
            {
                Light key = lightGo.AddComponent<Light>();
                key.type = LightType.Directional;
                key.intensity = 1.4f;
                lightGo.transform.rotation = Quaternion.Euler(35f, 230f, 0f);
                Light fill = fillGo.AddComponent<Light>();
                fill.type = LightType.Directional;
                fill.intensity = 0.6f;
                fillGo.transform.rotation = Quaternion.Euler(20f, 60f, 0f);
                Camera cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = orthoSize;
                cam.nearClipPlane = 0.05f;
                cam.farClipPlane = 20f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.62f, 0.6f, 0.56f);
                cam.targetTexture = rt;
                camGo.transform.SetPositionAndRotation(camPos, camRot);
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                return tex;
            }
            finally
            {
                RenderTexture.active = null;
                rt.Release();
                Object.DestroyImmediate(camGo);
                Object.DestroyImmediate(lightGo);
                Object.DestroyImmediate(fillGo);
            }
        }

        private static void SaveStrip(List<Texture2D> frames, string path)
        {
            if (frames.Count == 0)
            {
                Log("No frames rendered for " + path + " (no graphics device?).");
                return;
            }
            int w = frames.Sum(f => f.width), h = frames.Max(f => f.height);
            var strip = new Texture2D(w, h, TextureFormat.RGB24, false);
            int x = 0;
            foreach (Texture2D f in frames)
            {
                strip.SetPixels(x, 0, f.width, f.height, f.GetPixels());
                x += f.width;
                Object.DestroyImmediate(f);
            }
            strip.Apply();
            File.WriteAllBytes(path, strip.EncodeToPNG());
            Object.DestroyImmediate(strip);
            Log("Wrote " + Path.GetFullPath(path));
        }

        // ------------------------------------------------------------------ swap into the game

        /// <summary>True once the AccuRig prefab exists, so the old Mixamo-rig setup tools leave the swapped prefabs alone.</summary>
        public static bool Replaces(string character)
        {
            return File.Exists(AccuRigCharacterImport.Root + "/" + character + "/" + character + "_AccuRig.prefab");
        }

        private const string BackupFolder = "ArtSource/AccuRig/backup_before_swap";

        private static readonly string[] SceneFiles =
        {
            "Assets/Ankhu/Generated/Scenes/Opening/Opening_WalkDuat.unity",
            "Assets/Ankhu/Generated/Scenes/Opening/Opening_Title.unity",
            "Assets/Ankhu/Generated/Scenes/Opening/Opening_Cinematic.unity",
            "Assets/Ankhu/Realms/Generated/Scenes/Realm_01_Arit1.unity",
            "Assets/Ankhu/Realms/Generated/Scenes/Realm_02_Wernes_Field.unity",
            "Assets/Ankhu/Realms/Generated/Scenes/Realm_03_Wernes_House.unity",
            "Assets/Ankhu/Realms/Generated/Scenes/Realm_04_Wernes_Row.unity",
            "Assets/Ankhu/Opening/Generated/Anpu_Escort.controller",
        };

        /// <summary>
        /// Batch: rebuild the AccuRig prefabs, back up the old prefabs, Anpu.controller and the generated scenes to
        /// ArtSource/AccuRig/backup_before_swap (first run only), then copy each AccuRig prefab over the old prefab file
        /// (and Anpu_AccuRig.controller over Anpu.controller) keeping the old .meta, so every GUID reference in the
        /// project now resolves to the new rig. Re-runs the Opening and Realm scene builders, which unpack the
        /// prefabs into their scenes, then lists every humanoid in every scene with its avatar.
        /// </summary>
        public static void SwapIntoGameCli()
        {
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    throw new InvalidOperationException("Unity is in Play mode.");
                }
                SetupAll(false);
                SwapPrefabs();
                RunBuilder("Ankhu.Opening.EditorTools.OpeningSceneBuilder", "BuildScenes");
                RunBuilder("Ankhu.Realms.EditorTools.RealmSceneBuilder", "BuildAll");
                VerifyScenes();
                File.WriteAllText(Path.Combine(ChecksFolder, "swap_report.txt"), Report.ToString());
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                File.WriteAllText(Path.Combine(ChecksFolder, "swap_report.txt"), Report + "\nFAILED: " + e);
                EditorApplication.Exit(1);
            }
        }

        private static void Backup(string assetPath)
        {
            if (!File.Exists(assetPath))
            {
                return;
            }
            string dst = Path.Combine(BackupFolder, assetPath);
            if (File.Exists(dst))
            {
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(dst));
            File.Copy(assetPath, dst);
            if (File.Exists(assetPath + ".meta"))
            {
                File.Copy(assetPath + ".meta", dst + ".meta");
            }
        }

        private static void SwapPrefabs()
        {
            var pairs = new List<(string from, string to)>();
            foreach (string c in Characters)
            {
                pairs.Add((AccuRigCharacterImport.Root + "/" + c + "/" + c + "_AccuRig.prefab", "Assets/Characters/" + c + "/" + c + ".prefab"));
            }
            pairs.Add((AccuRigCharacterImport.Root + "/Anpu/Anpu_AccuRig.controller", "Assets/Characters/Anpu/Anpu.controller"));
            foreach (var (_, to) in pairs)
            {
                Backup(to);
            }
            foreach (string s in SceneFiles)
            {
                Backup(s);
            }
            Log("Backed up old prefabs, controller and generated scenes to " + Path.GetFullPath(BackupFolder));
            foreach (var (from, to) in pairs)
            {
                if (!File.Exists(from))
                {
                    Log("Swap skipped, missing " + from);
                    continue;
                }
                File.Copy(from, to, true);
                AssetDatabase.ImportAsset(to, ImportAssetOptions.ForceUpdate);
                Object a = AssetDatabase.LoadMainAssetAtPath(to);
                Animator anim = (a as GameObject) != null ? ((GameObject)a).GetComponent<Animator>() : null;
                Log(string.Format("Swapped {0} <- {1} (guid kept {2}){3}", to, from, AssetDatabase.AssetPathToGUID(to),
                    anim != null ? ", avatar " + (anim.avatar != null ? anim.avatar.name : "none") : ""));
            }
            AssetDatabase.SaveAssets();
        }

        private static void RunBuilder(string typeName, string method)
        {
            Type t = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(typeName)).FirstOrDefault(x => x != null);
            if (t == null)
            {
                throw new InvalidOperationException("Builder type not found: " + typeName);
            }
            Log("Running " + typeName + "." + method);
            t.GetMethod(method, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static).Invoke(null, null);
            Log("Finished " + typeName + "." + method);
        }

        private static void VerifyScenes()
        {
            var scenes = new[] { "Assets/Ankhu/Generated/Scenes", "Assets/Ankhu/Realms/Generated/Scenes" }
                .Where(Directory.Exists)
                .SelectMany(d => Directory.GetFiles(d, "*.unity", SearchOption.AllDirectories))
                .Select(p => p.Replace('\\', '/')).OrderBy(p => p).ToList();
            foreach (string path in scenes.Where(File.Exists))
            {
                UnityEngine.SceneManagement.Scene scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path, UnityEditor.SceneManagement.OpenSceneMode.Single);
                var lines = new List<string>();
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (Animator a in root.GetComponentsInChildren<Animator>(true))
                    {
                        if (a.avatar != null && a.avatar.isHuman)
                        {
                            lines.Add(a.transform.root.name + "/" + a.name + ": " + a.avatar.name + (a.runtimeAnimatorController != null ? " + " + a.runtimeAnimatorController.name : ""));
                        }
                    }
                    foreach (AnkhuVisual v in root.GetComponentsInChildren<AnkhuVisual>(true))
                    {
                        var so = new SerializedObject(v);
                        Object prefab = so.FindProperty("modelPrefab").objectReferenceValue;
                        Animator pa = prefab is GameObject g ? g.GetComponent<Animator>() : null;
                        lines.Add("AnkhuVisual on " + v.name + ": prefab " + AssetDatabase.GetAssetPath(prefab) + ", avatar " + (pa != null && pa.avatar != null ? pa.avatar.name : "none"));
                    }
                }
                Log(Path.GetFileNameWithoutExtension(path) + ": " + (lines.Count == 0 ? "no humanoids" : string.Join(" | ", lines)));
            }
        }

        // ------------------------------------------------------------------ helpers

        private static AnimationClip LoadClip(string path)
        {
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        }

        private static string Sanitize(string n)
        {
            var sb = new StringBuilder();
            foreach (char c in n)
            {
                sb.Append(char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_');
            }
            return sb.ToString();
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
