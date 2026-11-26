using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ankhu.Realms.EditorTools
{
    /// <summary>
    /// Builds Assets/Resources/RealmCodex/RollModel.prefab from Zac's exported scroll
    /// (Assets/Ankhu/Realms/Art/Codex/Models/Roll/RollModel.fbx plus its PNG textures). The prefab's root is the pivot at
    /// the roll's centre with its long axis on local X, about 0.34 m long. CodexGift and CodexFamiliar load it by name.
    /// Materials are URP Lit, made from the textures by name, with emission enabled so the roll can glow.
    ///   Unity -batchmode -quit -projectPath . -executeMethod Ankhu.Realms.EditorTools.RollModelSetup.BuildCli
    /// Then, with graphics: -executeMethod Ankhu.Realms.EditorTools.RollModelSetup.RenderCheckCli renders a check image.
    /// </summary>
    public static class RollModelSetup
    {
        public const string Folder = "Assets/Ankhu/Realms/Art/Codex/Models/Roll";
        public const string Fbx = Folder + "/RollModel.fbx";
        public const string PrefabPath = "Assets/Resources/RealmCodex/RollModel.prefab";
        public const string MaterialPath = Folder + "/RollModel_Lit.mat";
        public const float Length = 0.34f;

        [MenuItem("Ankhu/Codex/Rebuild roll model prefab")]
        public static void Build()
        {
            AssetDatabase.Refresh();
            var importer = AssetImporter.GetAtPath(Fbx) as ModelImporter;
            if (importer == null) throw new FileNotFoundException("No model at " + Fbx);
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.addCollider = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.SaveAndReimport();

            Material mat = MakeMaterial();
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
            var root = new GameObject("RollModel");
            var inst = (GameObject)Object.Instantiate(source);
            inst.name = "Mesh";
            inst.transform.SetParent(root.transform, false);
            foreach (Renderer r in inst.GetComponentsInChildren<Renderer>())
            {
                r.sharedMaterials = Enumerable.Repeat(mat, Mathf.Max(1, r.sharedMaterials.Length)).ToArray();
                r.shadowCastingMode = ShadowCastingMode.Off;
            }
            foreach (Collider c in inst.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);

            // Normalise in Unity space too: centre the pivot, long axis on X, 0.34 m.
            // Turn it half round about Y (keeps the writing upright) so the written face (band, cartouche, scarab) looks down -Z, toward a camera
            // that looks along +Z.
            inst.transform.localRotation = Quaternion.Euler(0f, 180f, 0f) * inst.transform.localRotation;
            Bounds b = WorldBounds(inst);
            Vector3 s = b.size;
            if (s.y > s.x && s.y >= s.z) inst.transform.localRotation = Quaternion.Euler(0f, 0f, 90f) * inst.transform.localRotation;
            else if (s.z > s.x && s.z >= s.y) inst.transform.localRotation = Quaternion.Euler(0f, 90f, 0f) * inst.transform.localRotation;
            b = WorldBounds(inst);
            float k = Length / Mathf.Max(1e-5f, b.size.x);
            inst.transform.localScale *= k;
            b = WorldBounds(inst);
            inst.transform.localPosition -= b.center;
            b = WorldBounds(inst);

            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            Debug.Log($"[RollModel] prefab {PrefabPath}, size {b.size.x:0.000} x {b.size.y:0.000} x {b.size.z:0.000} m, centre {b.center}");
        }

        public static void BuildCli()
        {
            try { Build(); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        private static Material MakeMaterial()
        {
            string[] pngs = Directory.GetFiles(Folder, "*.png").Select(p => p.Replace('\\', '/')).ToArray();
            Texture2D Find(params string[] keys)
            {
                foreach (string key in keys)
                {
                    string p = pngs.FirstOrDefault(x => Path.GetFileNameWithoutExtension(x).ToLowerInvariant().Contains(key));
                    if (p != null) return AssetDatabase.LoadAssetAtPath<Texture2D>(p);
                }
                return null;
            }
            // Normal and data maps must not be sRGB.
            foreach (string p in pngs)
            {
                string n = Path.GetFileNameWithoutExtension(p).ToLowerInvariant();
                var ti = AssetImporter.GetAtPath(p) as TextureImporter;
                if (ti == null) continue;
                bool normal = n.Contains("normal");
                bool data = n.Contains("metal") || n.Contains("rough") || n.Contains("occlusion") || n.Contains("_ao");
                bool big = n.Contains("basecolor") || normal;
                ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                ti.sRGBTexture = !(normal || data);
                ti.maxTextureSize = big ? 4096 : 2048;
                ti.mipmapEnabled = true;
                ti.anisoLevel = 4;
                ti.SaveAndReimport();
            }
            Texture2D baseMap = Find("basecolor", "albedo", "diffuse", "color", "texture_0", "texture");
            if (baseMap == null && pngs.Length > 0) baseMap = AssetDatabase.LoadAssetAtPath<Texture2D>(pngs[0]);
            Texture2D normalMap = Find("normal");
            Texture2D metal = Find("metalsmooth", "metal");

            Material m = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (m == null)
            {
                m = new Material(lit);
                AssetDatabase.CreateAsset(m, MaterialPath);
            }
            m.shader = lit;
            m.SetTexture("_BaseMap", baseMap);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Smoothness", 0.42f);
            if (normalMap != null) { m.SetTexture("_BumpMap", normalMap); m.EnableKeyword("_NORMALMAP"); }
            if (metal != null)
            {
                // Roll_MetalSmooth: R metallic, A smoothness (converted from Meshy's glTF G roughness, B metallic).
                m.SetTexture("_MetallicGlossMap", metal);
                m.EnableKeyword("_METALLICSPECGLOSSMAP");
                m.SetFloat("_Smoothness", 1f);
                m.SetFloat("_SmoothnessTextureChannel", 0f);
            }
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", Color.black);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(m);
            Debug.Log($"[RollModel] material base={(baseMap ? baseMap.name : "none")} normal={(normalMap ? normalMap.name : "none")} metal={(metal ? metal.name : "none")}");
            return m;
        }

        private static Bounds WorldBounds(GameObject go)
        {
            Renderer[] rs = go.GetComponentsInChildren<Renderer>();
            Bounds b = rs.Length > 0 ? rs[0].bounds : new Bounds(go.transform.position, Vector3.zero);
            foreach (Renderer r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        /// <summary>Renders the prefab in URP to _staging/codex_roll/model/RollModel_check_unity.png (needs graphics).</summary>
        public static void RenderCheckCli()
        {
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                var go = (GameObject)Object.Instantiate(prefab);
                go.transform.rotation = Quaternion.Euler(-12f, -22f, 6f);
                var camGo = new GameObject("CheckCam");
                Camera cam = camGo.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.04f, 0.04f, 0.05f);
                cam.fieldOfView = 28f;
                cam.nearClipPlane = 0.01f;
                cam.transform.position = new Vector3(0f, 0.1f, -0.75f);
                cam.transform.LookAt(Vector3.zero);
                var key = new GameObject("Key").AddComponent<Light>();
                key.type = LightType.Directional; key.intensity = 1.0f; key.color = new Color(1f, 0.88f, 0.7f);
                key.transform.rotation = Quaternion.Euler(35f, -40f, 0f);
                var fill = new GameObject("Fill").AddComponent<Light>();
                fill.type = LightType.Directional; fill.intensity = 0.45f; fill.color = new Color(0.6f, 0.75f, 1f);
                fill.transform.rotation = Quaternion.Euler(15f, 140f, 0f);
                var rt = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                string outPath = "_staging/codex_roll/model/RollModel_check_unity.png";
                Directory.CreateDirectory(Path.GetDirectoryName(outPath));
                File.WriteAllBytes(outPath, tex.EncodeToPNG());
                Debug.Log("[RollModel] check render " + outPath);
                EditorApplication.Exit(0);
            }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
    }
}
