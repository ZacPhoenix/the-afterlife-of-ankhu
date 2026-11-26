using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ankhu.EditorTools.Environment
{
    /// <summary>
    /// Swaps flat 3D Glow* lamp meshes in already built painted scenes for additive fx_glow cards, in place, without
    /// rebuilding the scene from its greybox (so later dressing is kept). Same treatment BorrowedDawnPainted gives new builds.
    ///   -executeMethod Ankhu.EditorTools.Environment.PaintedGlowLamps.Cli -glowScenes "Assets/.../A.unity;Assets/.../B.unity"
    /// </summary>
    public static class PaintedGlowLamps
    {
        const string GlowTex = "Assets/Ankhu/Painted/Art/fx_glow.png";
        const string GlowMat = "Assets/Ankhu/Painted/Art/Materials/P_Glow.mat";

        public static void Cli()
        {
            try
            {
                string[] a = System.Environment.GetCommandLineArgs();
                int i = Array.IndexOf(a, "-glowScenes");
                if (i < 0 || i + 1 >= a.Length) throw new ArgumentException("-glowScenes <a;b;c> required");
                Material gm = Mat();
                foreach (string path in a[i + 1].Split(';'))
                {
                    if (!File.Exists(path)) { Debug.LogWarning("[Glow] missing " + path); continue; }
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    int n = Swap(gm);
                    int h = HideShadowFigure();
                    int f = RehomeDressing();
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    Debug.Log("[Glow] " + Path.GetFileNameWithoutExtension(path) + " lamp glows swapped: " + n + ", shadow figure parts hidden: " + h + ", dressing quads rehomed: " + f);
                }
                AssetDatabase.SaveAssets();
                EditorApplication.Exit(0);
            }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        static Material Mat()
        {
            AssetDatabase.ImportAsset(GlowTex);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(GlowTex);
            if (tex == null) throw new FileNotFoundException(GlowTex);
            Shader add = Shader.Find("Ankhu/Realms/MagicAdditive");
            if (add == null) throw new Exception("Ankhu/Realms/MagicAdditive not found");
            var gm = AssetDatabase.LoadAssetAtPath<Material>(GlowMat);
            if (gm == null) { gm = new Material(add); AssetDatabase.CreateAsset(gm, GlowMat); }
            gm.shader = add;
            gm.SetTexture("_MainTex", tex);
            gm.SetColor("_Color", new Color(1f, 0.85f, 0.55f, 0.9f));
            EditorUtility.SetDirty(gm);
            return gm;
        }

        static int Swap(Material gm)
        {
            int n = 0;
            foreach (MeshRenderer mr in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Material m0 = mr.sharedMaterial;
                if (m0 == null || m0 == gm || !m0.name.StartsWith("Glow")) continue;
                if (m0.HasProperty("_BaseMap") && m0.GetTexture("_BaseMap") != null) continue;
                var mf = mr.GetComponent<MeshFilter>(); if (mf == null) continue;
                Vector3 s = mr.transform.lossyScale;
                float d = Mathf.Max(0.25f, Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y))) * 2.2f;
                mf.sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
                mr.transform.rotation = Quaternion.identity;
                Vector3 ps = mr.transform.parent != null ? mr.transform.parent.lossyScale : Vector3.one;
                mr.transform.localScale = new Vector3(d / Safe(ps.x), d / Safe(ps.y), 1f);
                mr.sharedMaterial = Tinted(gm, m0);
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                n++;
            }
            return n;
        }

        /// <summary>One additive glow material per source colour, so violet knots stay violet and gold suns stay gold.</summary>
        static Material Tinted(Material gm, Material src)
        {
            Color c = src.HasProperty("_BaseColor") ? src.GetColor("_BaseColor") : src.HasProperty("_Color") ? src.GetColor("_Color") : gm.color;
            c.a = 0.9f;
            string p = "Assets/Ankhu/Painted/Art/Materials/P_" + src.name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(p);
            if (m == null) { m = new Material(gm); AssetDatabase.CreateAsset(m, p); }
            m.shader = gm.shader;
            m.SetTexture("_MainTex", gm.GetTexture("_MainTex"));
            m.SetColor("_Color", c);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>The painted seal carries the figure, so the greybox silhouette cubes stop rendering (as in BorrowedDawn and the cistern).</summary>
        static int HideShadowFigure()
        {
            int n = 0;
            foreach (Transform t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.name != "ShadowFigure") continue;
                foreach (Renderer r in t.GetComponentsInChildren<Renderer>(true)) if (r.enabled) { r.enabled = false; n++; }
            }
            return n;
        }

        /// <summary>
        /// Light dressing quads (floor shade, shaft motes, wall bounce) are painted fx, but their textures sat in
        /// Generated/Dressed. Copy each into the painted Art folder as fx_&lt;name&gt; and point a painted copy of the material at it.
        /// </summary>
        static int RehomeDressing()
        {
            int n = 0;
            foreach (MeshRenderer mr in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Material m0 = mr.sharedMaterial;
                if (m0 == null || m0.name.StartsWith("fx_")) continue;
                string prop = m0.HasProperty("_BaseMap") && m0.GetTexture("_BaseMap") != null ? "_BaseMap" : m0.HasProperty("_MainTex") && m0.GetTexture("_MainTex") != null ? "_MainTex" : null;
                if (prop == null) continue;
                Texture tex = m0.GetTexture(prop);
                string tp = AssetDatabase.GetAssetPath(tex);
                if (!tp.StartsWith("Assets/Ankhu/Generated/Dressed/")) continue;
                string baseName = tex.name.StartsWith("T_") ? tex.name.Substring(2) : tex.name;
                string dst = "Assets/Ankhu/Painted/Art/fx_" + baseName.ToLowerInvariant() + Path.GetExtension(tp);
                if (!File.Exists(dst)) { AssetDatabase.CopyAsset(tp, dst); AssetDatabase.ImportAsset(dst); }
                var nt = AssetDatabase.LoadAssetAtPath<Texture2D>(dst);
                string mp = "Assets/Ankhu/Painted/Art/Materials/fx_" + m0.name + ".mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(mp);
                if (m == null) { m = new Material(m0); AssetDatabase.CreateAsset(m, mp); }
                m.CopyPropertiesFromMaterial(m0);
                m.SetTexture(prop, nt);
                EditorUtility.SetDirty(m);
                mr.sharedMaterial = m;
                n++;
            }
            return n;
        }

        static float Safe(float v) => Mathf.Abs(v) < 1e-4f ? 1e-4f : v;
    }
}
