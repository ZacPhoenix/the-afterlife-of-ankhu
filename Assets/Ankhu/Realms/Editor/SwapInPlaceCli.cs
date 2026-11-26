using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Ankhu.Opening;
using Ankhu.Painted;
using Object = UnityEngine.Object;

namespace Ankhu.Realms.EditorTools
{
    /// <summary>
    /// The cut-out repaint swap (Zac 2026-11-26) in one Unity launch, in place, no builder rebuilds:
    /// 1. imports: every png the swap copied gets clamp, no NPOT scaling and a Max Size that covers its source.
    /// 2. card variants: a quad that shows its image stretched past 1% gets the repainted variant "&lt;tex&gt;_a&lt;aspect&gt;.png"
    ///    from the same folder whose aspect matches the quad, through a cloned material "&lt;mat&gt;_a&lt;aspect&gt;.mat".
    /// 3. Wernes_Finale: ApepShadow/Coils becomes four section cards, each at its own aspect.
    /// 4. BorrowedDawn_Painted: the cut-out Paint_ layers go, the separately authored pillars and bell post come in.
    ///    Teach1_Cistern_Painted: the cut-out Inscription and ShadowSeal layers go.
    /// 5. flame cards in place in the 12 Wernes and Rosetau scenes, NoStretchCheck fix then check.
    /// 6. 1920x1080 captures of every touched scene, one per camera.
    /// -swapLog &lt;abs path to swap_copy.log&gt; -capOut &lt;abs dir&gt;. Logs "[Swap] CheckCli exit N".
    /// </summary>
    public static partial class RealmSceneBuilder
    {
        private const string BdArt = "Assets/Ankhu/Painted/Art";
        private static readonly string[] BdRemove =
        {
            "Paint_g_Room__Dressing__Hero_Bell", "Paint_g_Room__Props__SunGate", "Paint_gate_dawn",
            "Paint_fill_Room__Movers__LightScreen__Panel", "Paint_g_Room__Props__Inscription", "Paint_g_Room__Props__ShadowSeal",
        };
        private static readonly string[] T1Remove = { "Paint_g_Room__Props__Inscription", "Paint_g_Room__Props__ShadowSeal" };

        public static void SwapInPlaceCli()
        {
            var log = new StringBuilder();
            void L(string s) { log.AppendLine(s); Debug.Log("[Swap] " + s); }
            string outDir = null;
            try
            {
                string[] a = System.Environment.GetCommandLineArgs();
                string swapLog = SwArg(a, "-swapLog") ?? throw new ArgumentException("-swapLog required");
                outDir = SwArg(a, "-capOut") ?? throw new ArgumentException("-capOut required");
                Directory.CreateDirectory(outDir);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

                // 1. imports
                var copied = new List<string>();
                foreach (string line in File.ReadAllLines(swapLog))
                {
                    string t = line.Trim();
                    if (!(t.StartsWith("new ") || t.StartsWith("over "))) continue;
                    string p = t.Substring(t.IndexOf(' ')).Trim();
                    p = p.Substring(0, p.IndexOf("  <-", StringComparison.Ordinal)).Trim().Replace('\\', '/');
                    if (p.EndsWith(".png")) copied.Add(p);
                }
                int fixedImp = 0;
                foreach (string p in copied.Distinct())
                {
                    var imp = AssetImporter.GetAtPath(p) as TextureImporter;
                    if (imp == null) { L("no importer " + p); continue; }
                    imp.GetSourceTextureWidthAndHeight(out int w, out int h);
                    int need = 32; while (need < Mathf.Max(w, h) && need < 16384) need *= 2;
                    bool normal = imp.textureType == TextureImporterType.NormalMap;
                    bool ch = false;
                    if (imp.maxTextureSize < need) { imp.maxTextureSize = need; ch = true; }
                    if (imp.npotScale != TextureImporterNPOTScale.None) { imp.npotScale = TextureImporterNPOTScale.None; ch = true; }
                    if (!normal && !p.Contains("_cam_") && imp.wrapMode != TextureWrapMode.Clamp && !p.Contains("water")) { imp.wrapMode = TextureWrapMode.Clamp; ch = true; }
                    if (ch) { imp.SaveAndReimport(); fixedImp++; }
                }
                L("imports: " + copied.Distinct().Count() + " copied pngs, " + fixedImp + " importers changed");

                // 2. card variants in every scene NoStretchCheck walks
                var scenes = (typeof(NoStretchCheck).GetMethod("Scenes", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                    .Invoke(null, null) as IEnumerable<string>).ToList();
                var cloneCache = new Dictionary<string, Material>();
                var usedVariants = new HashSet<string>();
                var touched = new List<string>();
                foreach (string sp in scenes)
                {
                    var scene = EditorSceneManager.OpenScene(sp, OpenSceneMode.Single);
                    int swapped = 0;
                    foreach (MeshRenderer mr in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    {
                        var mf = mr.GetComponent<MeshFilter>();
                        if (mf == null || mf.sharedMesh == null || mf.sharedMesh.name != "Quad") continue;
                        if (mr.GetComponent<BackdropFit>() != null) continue;
                        Material m = mr.sharedMaterial;
                        if (!NoStretchCheck.ShownAspect(m, out Texture tex, out float content, out string what, out Vector2 tile)) continue;
                        if (what == "mask") continue;
                        if (m.HasProperty("_Cols") && (m.GetFloat("_Cols") > 1f || m.GetFloat("_Rows") > 1f)) continue;
                        Vector3 ls = mr.transform.lossyScale;
                        if (Mathf.Abs(ls.y) < 1e-5f) continue;
                        float qa = Mathf.Abs(ls.x / ls.y) * Mathf.Abs(tile.y / tile.x);
                        if (Mathf.Abs(qa / content - 1f) <= 0.01f) continue;
                        string texPath = AssetDatabase.GetAssetPath(tex);
                        if (string.IsNullOrEmpty(texPath)) continue;
                        string dir = Path.GetDirectoryName(texPath).Replace('\\', '/');
                        string baseName = tex.name;
                        int ai = baseName.LastIndexOf("_a", StringComparison.Ordinal);
                        if (ai > 0 && float.TryParse(baseName.Substring(ai + 2), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _))
                            baseName = baseName.Substring(0, ai);
                        string best = null; float bestErr = 0.05f;
                        foreach (string f in Directory.GetFiles(dir, baseName + "_a*.png"))
                        {
                            string fp = f.Replace('\\', '/');
                            var vt = AssetDatabase.LoadAssetAtPath<Texture2D>(fp);
                            if (vt == null) continue;
                            float err = Mathf.Abs(qa / NoStretchCheck.SourceAspect(vt) - 1f);
                            if (err < bestErr) { bestErr = err; best = fp; }
                        }
                        if (best == null) { L(scene.name + ": no variant for " + tex.name + " at " + qa.ToString("0.###") + " on " + SwPath(mr.transform)); continue; }
                        string suffix = Path.GetFileNameWithoutExtension(best).Substring(baseName.Length);
                        string key = AssetDatabase.GetAssetPath(m) + "|" + m.name + "|" + best;
                        if (!cloneCache.TryGetValue(key, out Material clone))
                        {
                            string mp = AssetDatabase.GetAssetPath(m);
                            string mdir = string.IsNullOrEmpty(mp) || !mp.EndsWith(".mat") ? "Assets/Ankhu/Generated/VariantMaterials" : Path.GetDirectoryName(mp).Replace('\\', '/');
                            if (!AssetDatabase.IsValidFolder(mdir)) { Directory.CreateDirectory(mdir); AssetDatabase.Refresh(); }
                            string mname = m.name;
                            int mi = mname.LastIndexOf("_a", StringComparison.Ordinal);
                            if (mi > 0 && float.TryParse(mname.Substring(mi + 2), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _))
                                mname = mname.Substring(0, mi);
                            string cp = mdir + "/" + mname + suffix + ".mat";
                            clone = AssetDatabase.LoadAssetAtPath<Material>(cp);
                            if (clone == null) { clone = new Material(m); AssetDatabase.CreateAsset(clone, cp); }
                            else clone.CopyPropertiesFromMaterial(m);
                            string prop = m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") == tex ? "_BaseMap" : "_MainTex";
                            clone.SetTexture(prop, AssetDatabase.LoadAssetAtPath<Texture2D>(best));
                            clone.renderQueue = m.renderQueue;
                            EditorUtility.SetDirty(clone);
                            cloneCache[key] = clone;
                        }
                        mr.sharedMaterial = clone;
                        usedVariants.Add(best);
                        swapped++;
                    }
                    if (swapped > 0)
                    {
                        EditorSceneManager.SaveScene(scene);
                        touched.Add(sp);
                        L(scene.name + ": " + swapped + " cards rewired to variants");
                    }
                }
                AssetDatabase.SaveAssets();
                foreach (string p in copied.Distinct().Where(p => IsVariantName(p)))
                    if (!usedVariants.Contains(p)) L("variant copy not used by any card: " + p);

                // 3. Apep sections
                string fin = RealmKit.SceneDir + "/Wernes_Finale.unity";
                {
                    var scene = EditorSceneManager.OpenScene(fin, OpenSceneMode.Single);
                    var apep = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(t => t.name == "ApepShadow");
                    Transform coils = apep != null ? apep.Find("Coils") : null;
                    Transform cq = coils != null ? coils.Find("CardQuad") : null;
                    if (cq == null) L("Wernes_Finale: ApepShadow/Coils/CardQuad not found, Apep left as is");
                    else
                    {
                        float aw = cq.localScale.x;
                        bool wasActive = coils.gameObject.activeSelf;
                        Object.DestroyImmediate(coils.gameObject);
                        var parts = new (string part, float cx, float cy, float w, float h)[]
                        {
                            ("tail", -0.35223f, -0.00119f, 0.29553f, 0.11317f),
                            ("body1", -0.11983f, 0.00131f, 0.27770f, 0.10651f),
                            ("body2", 0.10604f, -0.00202f, 0.28245f, 0.10818f),
                            ("head", 0.34653f, 0f, 0.30694f, 0.11769f),
                        };
                        int si = 0;
                        foreach (var p in parts)
                        {
                            Texture2D pt = SpriteTex("fin_apep_" + p.part, out _);
                            Material pm = FlipMat("flip_fin_apep_" + p.part, pt, new Color(0.22f, 0.2f, 0.36f, 0.92f), 1f, 0.06f, 0.25f, 0f, false, 0.004f);
                            if (pm != null) pm.renderQueue = (int)RenderQueue.Transparent - 20;
                            Transform card = FinLocalCard(apep, "Coils_" + p.part, pm, new Vector3(p.cx * aw, p.cy * aw, 0f), new Vector2(p.w * aw, p.h * aw));
                            card.SetSiblingIndex(si++);
                            card.gameObject.SetActive(wasActive);
                            var r = card.GetComponentInChildren<MeshRenderer>(true);
                            if (r != null)
                            {
                                var fade = r.gameObject.AddComponent<PaintedDrift>();
                                fade.from = fade.to = fade.transform.localPosition; fade.seconds = 8f; fade.fade = true; fade.fadeFrom = 0f; fade.fadeTo = 1f;
                            }
                        }
                        EditorSceneManager.SaveScene(scene);
                        if (!touched.Contains(fin)) touched.Add(fin);
                        L("Wernes_Finale: Apep coils now 4 section cards, band width " + aw.ToString("0.##"));
                    }
                }

                // 4. Borrowed Dawn and Teach1
                string bd = "Assets/Ankhu/Generated/Scenes/BorrowedDawn_Painted.unity";
                {
                    var scene = EditorSceneManager.OpenScene(bd, OpenSceneMode.Single);
                    var all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                    foreach (string n in BdRemove)
                    {
                        var hits = all.Where(t => t != null && t.name == n).ToList();
                        foreach (var t in hits) Object.DestroyImmediate(t.gameObject);
                        L("BorrowedDawn: removed " + n + " x" + hits.Count);
                    }
                    Transform root = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(t => t.parent == null && t.name == "Painted");
                    if (root == null) L("BorrowedDawn: Painted root not found, new layers not placed");
                    else
                    {
                        Shader lit = Shader.Find("Ankhu/PaintedLit");
                        Mesh quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
                        var rows = new (string id, float xmin, float ymin, float w, float h, float z)[]
                        {
                            ("SunGate_pillars", 29.9662f, 4.9338f, 4.4667f, 4.9333f, 0.205f),
                            ("Hero_Bell_post_w", 21.9551f, 4.9338f, 1.0444f, 3.8889f, 0.215f),
                        };
                        foreach (var r in rows)
                        {
                            foreach (Transform old in root.Cast<Transform>().Where(t => t.name == "Paint_" + r.id).ToList()) Object.DestroyImmediate(old.gameObject);
                            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(BdArt + "/L_" + r.id + ".png");
                            var nrm = AssetDatabase.LoadAssetAtPath<Texture2D>(BdArt + "/L_" + r.id + "_n.png");
                            if (tex == null) { L("BorrowedDawn: missing L_" + r.id + ".png"); continue; }
                            string mp = BdArt + "/Materials/P_" + r.id + ".mat";
                            var mat = AssetDatabase.LoadAssetAtPath<Material>(mp);
                            if (mat == null) { mat = new Material(lit); AssetDatabase.CreateAsset(mat, mp); }
                            mat.shader = lit;
                            mat.SetTexture("_BaseMap", tex);
                            if (nrm != null) mat.SetTexture("_NormalMap", nrm);
                            mat.SetFloat("_LightGain", 0.12f);
                            mat.SetFloat("_ClipMinY", -100000f);
                            EditorUtility.SetDirty(mat);
                            var go = new GameObject("Paint_" + r.id);
                            go.AddComponent<MeshFilter>().sharedMesh = quad;
                            var mr = go.AddComponent<MeshRenderer>();
                            mr.sharedMaterial = mat;
                            mr.shadowCastingMode = ShadowCastingMode.Off;
                            mr.receiveShadows = false;
                            go.transform.SetParent(root, false);
                            go.transform.position = new Vector3(r.xmin + r.w * 0.5f, r.ymin + r.h * 0.5f, r.z);
                            go.transform.rotation = Quaternion.identity;
                            Vector3 pls = root.lossyScale;
                            go.transform.localScale = new Vector3(r.w / pls.x, r.h / pls.y, 1f / pls.z);
                            L("BorrowedDawn: placed Paint_" + r.id + " (texture " + NoStretchCheck.SourceAspect(tex).ToString("0.####") + ":1, quad " + (r.w / r.h).ToString("0.####") + ":1)");
                        }
                    }
                    AssetDatabase.SaveAssets();
                    EditorSceneManager.SaveScene(scene);
                    if (!touched.Contains(bd)) touched.Add(bd);
                }
                string t1 = "Assets/Ankhu/Generated/Scenes/Teach1_Cistern_Painted.unity";
                {
                    var scene = EditorSceneManager.OpenScene(t1, OpenSceneMode.Single);
                    var all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                    foreach (string n in T1Remove)
                    {
                        var hits = all.Where(t => t != null && t.name == n).ToList();
                        foreach (var t in hits) Object.DestroyImmediate(t.gameObject);
                        L("Teach1: removed " + n + " x" + hits.Count);
                    }
                    EditorSceneManager.SaveScene(scene);
                    if (!touched.Contains(t1)) touched.Add(t1);
                }

                // 5. flame cards, fix, check
                var twelve = RepaintBatchCli.Scenes.Select(s => RealmKit.SceneDir + "/" + s + ".unity").ToArray();
                foreach (var kv in FlameCardsInPlace(twelve)) L("flame cards " + Path.GetFileNameWithoutExtension(kv.Key) + " " + kv.Value);
                foreach (string s in twelve) if (!touched.Contains(s)) touched.Add(s);
                int code;
                try
                {
                    int fixBad = NoStretchCheck.Run(true, false, out _);
                    L("FixCli exit " + (fixBad == 0 ? 0 : 1));
                    int bad = NoStretchCheck.Run(false, false, out string report);
                    code = bad == 0 ? 0 : 1;
                    L("CheckCli exit " + code + ", failing rows " + bad);
                }
                catch (Exception e) { Debug.LogException(e); code = 99; L("CheckCli exit 99 " + e.Message); }

                // 6. captures
                foreach (string sp in touched)
                {
                    if (!File.Exists(sp)) continue;
                    string s = Path.GetFileNameWithoutExtension(sp);
                    EditorSceneManager.OpenScene(sp, OpenSceneMode.Single);
                    foreach (var f in Object.FindObjectsByType<Ankhu.CameraRig.CameraFollow2D>(FindObjectsSortMode.None))
                    {
                        var c = f.GetComponent<Camera>();
                        if (c != null) c.aspect = 16f / 9f;
                        f.SnapToTarget();
                    }
                    foreach (Camera cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                        RealmCapture.RenderTo(cam, Path.Combine(outDir, s + "_" + cam.name + ".png"), 1920, 1080);
                    L("captured " + s);
                }
                File.WriteAllText(Path.Combine(outDir, "swap_in_place.log"), log.ToString());
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                log.AppendLine("ERROR " + e);
                if (outDir != null) try { File.WriteAllText(Path.Combine(outDir, "swap_in_place.log"), log.ToString()); } catch { }
                EditorApplication.Exit(1);
            }
        }

        private static bool IsVariantName(string p) => System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileNameWithoutExtension(p), @"_a\d+(\.\d+)?$");

        private static string SwPath(Transform t)
        {
            string p = t.name;
            while (t.parent != null) { t = t.parent; p = t.name + "/" + p; }
            return p;
        }

        private static string SwArg(string[] a, string key)
        {
            int i = Array.IndexOf(a, key);
            return i >= 0 && i + 1 < a.Length ? a[i + 1] : null;
        }
    }
}
