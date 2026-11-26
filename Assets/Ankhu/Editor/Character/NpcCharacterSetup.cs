using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Ankhu.EditorTools.Character
{
    /// <summary>
    /// Import settings and setup for the non-player Meshy characters (Anpu, the obsidian Guardian).
    /// Each lives in Assets/Characters/&lt;Name&gt;/ with Model/, Textures/ and Materials/ subfolders,
    /// mirroring the Ankhu layout. Models import as Humanoid with an avatar from the model.
    /// Setup builds URP Lit materials (the glTF metallic-roughness map is repacked to URP
    /// metallic-smoothness), maps the optional Jaw bone into the avatar, and writes
    /// Assets/Characters/&lt;Name&gt;/&lt;Name&gt;.prefab with an Animator on the Ankhu controller so the
    /// Mixamo idle plays. Props (Anpu's was-sceptre and ankh) ship inside the model as rigid meshes
    /// skinned to Prop.R and Prop.L bones under the hands, so they follow any clip. Hide one by
    /// disabling its renderer (Prop_WasSceptre, Prop_Ankh). Idempotent.
    ///
    /// Batch: Unity -batchmode -quit -projectPath . -executeMethod Ankhu.EditorTools.Character.NpcCharacterSetup.SetupFromCommandLine -logFile npc_setup.log
    /// </summary>
    public sealed class NpcCharacterImport : AssetPostprocessor
    {
        public static readonly string[] Characters = { "Anpu", "Guardian" };

        public static string Root(string name) { return "Assets/Characters/" + name; }

        private static string Owner(string path, string sub)
        {
            foreach (string c in Characters)
            {
                if (AnkhuCharacterImport.InFolder(path, Root(c) + "/" + sub))
                {
                    return c;
                }
            }
            return null;
        }

        private void OnPreprocessModel()
        {
            if (Owner(assetPath, "Animations") != null)
            {
                var clipImporter = (ModelImporter)assetImporter;
                clipImporter.animationType = ModelImporterAnimationType.Human;
                clipImporter.importAnimation = true;
                clipImporter.materialImportMode = ModelImporterMaterialImportMode.None;
                clipImporter.importCameras = false;
                clipImporter.importLights = false;
                clipImporter.useFileScale = true;
                clipImporter.globalScale = 1f;
                clipImporter.bakeAxisConversion = true;
                return;
            }
            if (Owner(assetPath, "Model") == null)
            {
                return;
            }
            var importer = (ModelImporter)assetImporter;
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = false;
            importer.importBlendShapes = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            importer.isReadable = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;
        }

        private void OnPreprocessTexture()
        {
            if (Owner(assetPath, "Textures") == null)
            {
                return;
            }
            var importer = (TextureImporter)assetImporter;
            string n = Path.GetFileNameWithoutExtension(assetPath);
            importer.mipmapEnabled = true;
            importer.maxTextureSize = 4096;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.textureType = n.EndsWith("_Normal") ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = n.EndsWith("_BaseColor");
            importer.isReadable = n.EndsWith("_MetallicRoughness");
        }
    }

    public static class NpcCharacterSetup
    {
        private const float DefaultSmoothness = 0.45f;

        [MenuItem("Ankhu/Character/Set Up NPC Models (Anpu, Guardian)")]
        public static void SetupFromMenu()
        {
            foreach (string c in NpcCharacterImport.Characters)
            {
                Setup(c);
            }
        }

        public static void SetupFromCommandLine()
        {
            try
            {
                SetupFromMenu();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        public static void Setup(string name)
        {
            if (AccuRigCharacterSetup.Replaces(name))
            {
                Debug.LogWarning("[NpcSetup] " + name + ".prefab now holds the AccuRig rig. Use Ankhu > Character > Set Up AccuRig Rerigs instead. Skipped.");
                return;
            }
            string root = NpcCharacterImport.Root(name);
            string modelPath = Directory.GetFiles(root + "/Model", "*.fbx").Select(p => p.Replace('\\', '/')).FirstOrDefault();
            if (modelPath == null)
            {
                Debug.LogWarning("[NpcSetup] No FBX under " + root + "/Model, skipped.");
                return;
            }
            AssetDatabase.ImportAsset(modelPath, ImportAssetOptions.ForceUpdate);
            var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
            MapJaw(importer, modelPath);

            string stem = Path.GetFileNameWithoutExtension(modelPath);
            foreach (KeyValuePair<AssetImporter.SourceAssetIdentifier, Object> kv in importer.GetExternalObjectMap().ToList())
            {
                importer.RemoveRemap(kv.Key);
            }
            importer.SaveAndReimport();
            // One URP material per source material. Textures are <stem>_<material>_<Role>.png when the
            // model has several materials (Anpu: Body, WasSceptre, Ankh), else <stem>_<Role>.png.
            foreach (Material m in AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Material>().ToList())
            {
                string prefix = File.Exists(root + "/Textures/" + stem + "_" + m.name + "_BaseColor.png") ? stem + "_" + m.name : stem;
                string label = prefix == stem ? name : name + "_" + m.name;
                Material mat = BuildMaterial(label, root, prefix);
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), m.name), mat);
                Debug.Log(string.Format("[NpcSetup] {0}: material {1} -> M_{2} (textures {3}_*)", name, m.name, label, prefix));
            }
            importer.SaveAndReimport();

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Avatar>().FirstOrDefault();
            foreach (string socket in new[] { "Prop.R", "Prop.L" })
            {
                Transform t = model.GetComponentsInChildren<Transform>(true).FirstOrDefault(b => b.name == socket);
                if (t != null)
                {
                    Debug.Log(string.Format("[NpcSetup] {0}: socket {1} at {2}", name, socket, t.position.ToString("F3")));
                }
            }
            Debug.Log(string.Format("[NpcSetup] {0}: avatar {1}, valid {2}, human {3}, bones mapped {4}, jaw {5}",
                name, avatar != null ? avatar.name : "none", avatar != null && avatar.isValid, avatar != null && avatar.isHuman,
                importer.humanDescription.human.Length,
                importer.humanDescription.human.Any(h => h.humanName == "Jaw") ? "mapped" : "none"));
            AnimationClip hold = ConfigureHoldClip(root, avatar);
            RuntimeAnimatorController controller = hold != null
                ? (RuntimeAnimatorController)BuildHoldController(name, root, hold)
                : AssetDatabase.LoadAssetAtPath<AnimatorController>(AnkhuCharacterSetup.ControllerPath);
            BuildPrefab(name, root, model, avatar, controller);
            if (hold != null)
            {
                CheckHold(name, root, hold);
            }
            AssetDatabase.SaveAssets();
        }

        private static void MapJaw(ModelImporter importer, string modelPath)
        {
            Transform[] bones = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath).GetComponentsInChildren<Transform>(true);
            Transform jaw = bones.FirstOrDefault(b => b.name == "Jaw");
            HumanDescription hd = importer.humanDescription;
            var human = hd.human.Where(h => h.humanName != "Jaw" && h.boneName != "Jaw").ToList();
            if (jaw == null)
            {
                return;
            }
            var hb = new HumanBone { boneName = "Jaw", humanName = "Jaw" };
            hb.limit.useDefaultValues = true;
            human.Add(hb);
            hd.human = human.ToArray();
            importer.humanDescription = hd;
            importer.SaveAndReimport();
        }

        private static Material BuildMaterial(string name, string root, string stem)
        {
            string tex = root + "/Textures/";
            Texture2D baseColor = AssetDatabase.LoadAssetAtPath<Texture2D>(tex + stem + "_BaseColor.png");
            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(tex + stem + "_Normal.png");
            Texture2D ms = RepackMetallicSmoothness(tex, stem);

            Directory.CreateDirectory(root + "/Materials");
            string matPath = root + "/Materials/M_" + name + ".mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, matPath);
            }
            mat.SetTexture("_BaseMap", baseColor);
            mat.SetColor("_BaseColor", Color.white);
            if (normal != null)
            {
                mat.SetTexture("_BumpMap", normal);
                mat.SetFloat("_BumpScale", 1f);
                mat.EnableKeyword("_NORMALMAP");
            }
            if (ms != null)
            {
                mat.SetTexture("_MetallicGlossMap", ms);
                mat.SetFloat("_Smoothness", 1f);
                mat.SetFloat("_Metallic", 1f);
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            else
            {
                mat.SetFloat("_Smoothness", DefaultSmoothness);
                mat.SetFloat("_Metallic", 0f);
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>glTF packs roughness in G and metallic in B. URP wants metallic in R and smoothness in A.</summary>
        private static Texture2D RepackMetallicSmoothness(string tex, string stem)
        {
            string src = tex + stem + "_MetallicRoughness.png";
            string dst = tex + stem + "_MetallicSmoothness.png";
            Texture2D mr = AssetDatabase.LoadAssetAtPath<Texture2D>(src);
            if (mr == null)
            {
                return null;
            }
            if (!File.Exists(dst))
            {
                var raw = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
                raw.LoadImage(File.ReadAllBytes(src));
                Color32[] px = raw.GetPixels32();
                for (int i = 0; i < px.Length; i++)
                {
                    byte metal = px[i].b;
                    byte smooth = (byte)(255 - px[i].g);
                    px[i] = new Color32(metal, metal, metal, smooth);
                }
                var outTex = new Texture2D(raw.width, raw.height, TextureFormat.RGBA32, false, true);
                outTex.SetPixels32(px);
                File.WriteAllBytes(dst, outTex.EncodeToPNG());
                Object.DestroyImmediate(raw);
                Object.DestroyImmediate(outTex);
                AssetDatabase.ImportAsset(dst, ImportAssetOptions.ForceUpdate);
                var ti = (TextureImporter)AssetImporter.GetAtPath(dst);
                ti.sRGBTexture = false;
                ti.alphaSource = TextureImporterAlphaSource.FromInput;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(dst);
        }

        private static void BuildPrefab(string name, string root, GameObject model, Avatar avatar, RuntimeAnimatorController controller)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            try
            {
                instance.name = name;
                Animator animator = instance.GetComponent<Animator>();
                if (animator == null)
                {
                    animator = instance.AddComponent<Animator>();
                }
                animator.avatar = avatar;
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
                foreach (SkinnedMeshRenderer smr in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                    smr.receiveShadows = true;
                }
                instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                Vector2 span = AnkhuCharacterSetup.BakedHeightSpan(instance);
                Debug.Log(string.Format("[NpcSetup] {0}: bind-pose soles y {1:0.000}, top y {2:0.000}, height {3:0.000} m",
                    name, span.x, span.y, span.y - span.x));
                string path = root + "/" + name + ".prefab";
                PrefabUtility.SaveAsPrefabAsset(instance, path, out bool ok);
                if (!ok)
                {
                    throw new InvalidOperationException("Could not save " + path);
                }
                Debug.Log("[NpcSetup] Prefab written to " + path);
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }
            // ------------------------------------------------------------------ hold pose (Anpu)

        private const string IdleClipPath = "Assets/Characters/Ankhu/Animations/01_Idle_Breathing.fbx";

        /// <summary>
        /// Imports Animations/*HoldStaff*.fbx against this character's avatar as one looping clip with the
        /// root locked. Returns null when the character has no hold clip.
        /// </summary>
        private static AnimationClip ConfigureHoldClip(string root, Avatar avatar)
        {
            string folder = root + "/Animations";
            if (!Directory.Exists(folder))
            {
                return null;
            }
            string path = Directory.GetFiles(folder, "*HoldStaff*.fbx").Select(p => p.Replace('\\', '/')).FirstOrDefault();
            if (path == null)
            {
                return null;
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
            importer.sourceAvatar = avatar;
            TakeInfo take = importer.importedTakeInfos.FirstOrDefault();
            var clip = new ModelImporterClipAnimation
            {
                name = "HoldStaff",
                takeName = take.name,
                firstFrame = take.startTime * take.sampleRate,
                lastFrame = take.stopTime * take.sampleRate,
                loopTime = true,
                lockRootRotation = true,
                lockRootHeightY = true,
                lockRootPositionXZ = true,
                keepOriginalOrientation = true,
                keepOriginalPositionY = true,
                keepOriginalPositionXZ = true,
            };
            importer.clipAnimations = new[] { clip };
            importer.SaveAndReimport();
            AnimationClip result = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => c.name == "HoldStaff");
            if (result == null)
            {
                // The avatar copy needs the clip file's hierarchy to match the model's. If it does not,
                // fall back to an avatar built from the clip's own skeleton (same bones, so it retargets).
                Debug.LogWarning("[NpcSetup] Avatar copy failed for " + path + ", retrying with an avatar from the clip file.");
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.sourceAvatar = null;
                importer.SaveAndReimport();
                result = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => c.name == "HoldStaff");
            }
            Debug.Log(string.Format("[NpcSetup] Hold clip {0}: {1}", path, result != null ? "imported, humanoid " + result.humanMotion : "MISSING"));
            return result;
        }

        /// <summary>Idle on the base layer, the hold pose on an override layer masked to the right arm and fingers.</summary>
        private static AnimatorController BuildHoldController(string name, string root, AnimationClip hold)
        {
            string maskPath = root + "/" + name + "_RightArm.mask";
            AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(maskPath);
            if (mask == null)
            {
                mask = new AvatarMask();
                AssetDatabase.CreateAsset(mask, maskPath);
            }
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            {
                var part = (AvatarMaskBodyPart)i;
                mask.SetHumanoidBodyPartActive(part, part == AvatarMaskBodyPart.RightArm || part == AvatarMaskBodyPart.RightFingers);
            }
            EditorUtility.SetDirty(mask);

            string path = root + "/" + name + ".controller";
            AssetDatabase.DeleteAsset(path);
            AnimatorController ac = AnimatorController.CreateAnimatorControllerAtPath(path);
            AnimationClip idle = AssetDatabase.LoadAllAssetsAtPath(IdleClipPath).OfType<AnimationClip>().FirstOrDefault(c => !c.name.StartsWith("__preview"));
            AnimatorStateMachine sm = ac.layers[0].stateMachine;
            AnimatorState idleState = sm.AddState("Idle");
            idleState.motion = idle != null ? idle : hold;
            sm.defaultState = idleState;

            ac.AddLayer("HoldStaff");
            AnimatorControllerLayer[] layers = ac.layers;
            layers[1].defaultWeight = 1f;
            layers[1].avatarMask = mask;
            layers[1].blendingMode = AnimatorLayerBlendingMode.Override;
            ac.layers = layers;
            AnimatorState holdState = ac.layers[1].stateMachine.AddState("Hold");
            holdState.motion = hold;
            ac.layers[1].stateMachine.defaultState = holdState;
            EditorUtility.SetDirty(ac);
            Debug.Log(string.Format("[NpcSetup] {0}: controller {1} (idle {2}, hold layer on the right arm)", name, path, idle != null ? idle.name : "missing, hold used"));
            return ac;
        }

        /// <summary>
        /// Poses the prefab with the hold clip and measures the sceptre: tilt from vertical and the height
        /// of its foot. Also renders a front and side PNG next to the art sources when a GPU is available.
        /// </summary>
        private static void CheckHold(string name, string root, AnimationClip hold)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(root + "/" + name + ".prefab");
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            try
            {
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                AnimationMode.StartAnimationMode();
                AnkhuCharacterSetup.SamplePose(go, hold, 0f);
                foreach (SkinnedMeshRenderer smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (!smr.name.StartsWith("Prop_"))
                    {
                        continue;
                    }
                    var baked = new Mesh();
                    smr.BakeMesh(baked, true);
                    Vector3[] v = baked.vertices.Select(p => smr.transform.TransformPoint(p)).ToArray();
                    Object.DestroyImmediate(baked);
                    Vector3 lo = v.OrderBy(p => p.y).First();
                    Vector3 hi = v.OrderBy(p => p.y).Last();
                    float tilt = Vector3.Angle(hi - lo, Vector3.up);
                    Debug.Log(string.Format("[NpcSetup] {0}: {1} in hold pose, bottom {2}, top {3}, tilt {4:0.0} deg",
                        name, smr.name, lo.ToString("F3"), hi.ToString("F3"), tilt));
                }
                RenderCheck(go, name);
            }
            finally
            {
                AnimationMode.StopAnimationMode();
                Object.DestroyImmediate(go);
            }
        }

        private static void RenderCheck(GameObject go, string name)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                Debug.Log("[NpcSetup] No graphics device, render check skipped.");
                return;
            }
            var camGo = new GameObject("NpcCheckCamera");
            var lightGo = new GameObject("NpcCheckLight");
            var rt = new RenderTexture(1200, 900, 24, RenderTextureFormat.ARGB32);
            try
            {
                Light light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.3f;
                lightGo.transform.rotation = Quaternion.Euler(35f, 150f, 0f);
                Camera cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = 1.25f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.55f, 0.55f, 0.55f);
                cam.targetTexture = rt;
                var sheet = new Texture2D(1200, 900, TextureFormat.RGB24, false);
                var shots = new[]
                {
                    new { pos = new Vector3(0f, 1.15f, 5f), rot = Quaternion.Euler(0f, 180f, 0f), x = 0 },
                    new { pos = new Vector3(5f, 1.15f, 0f), rot = Quaternion.Euler(0f, 270f, 0f), x = 600 },
                };
                var half = new RenderTexture(600, 900, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = half;
                foreach (var s in shots)
                {
                    camGo.transform.SetPositionAndRotation(s.pos, s.rot);
                    cam.Render();
                    RenderTexture.active = half;
                    sheet.ReadPixels(new Rect(0, 0, 600, 900), s.x, 0);
                }
                sheet.Apply();
                RenderTexture.active = null;
                string outPath = Path.GetFullPath("art_gen/anpu/3d/unity_" + name.ToLowerInvariant() + "_hold_check.png");
                File.WriteAllBytes(outPath, sheet.EncodeToPNG());
                Object.DestroyImmediate(sheet);
                half.Release();
                Debug.Log("[NpcSetup] Render check written to " + outPath);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[NpcSetup] Render check failed: " + e.Message);
            }
            finally
            {
                RenderTexture.active = null;
                rt.Release();
                Object.DestroyImmediate(camGo);
                Object.DestroyImmediate(lightGo);
            }
        }
    }
}
