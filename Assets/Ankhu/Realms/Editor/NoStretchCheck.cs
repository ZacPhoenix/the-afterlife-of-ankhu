using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Ankhu.Opening;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Ankhu.Realms.EditorTools
{
    /// <summary>
    /// NO-STRETCH RULE (Zac, 2026-11-26): stretching images is banned. Every image is authored at the aspect it is
    /// shown at, and never resampled to a different aspect by the importer, a quad, a sprite or a UI rect.
    ///
    /// The bug that started it: 1536x1024 paintings imported with Non Power of 2 = ToNearest became 2048x1024, and the
    /// backdrop code read that size, so every Wernes and Rosetau backdrop showed 1.33x too wide (oval moons).
    ///
    /// What fails:
    ///   1. A texture under Assets/Ankhu or Assets/Art with a non power of two size, not a sprite, and nPOTScale not None.
    ///   2. A BackdropFit whose imageAspect differs from its painting's source aspect.
    ///   3. A textured quad whose shown aspect (world scale, tiling, flipbook grid) differs from its image's aspect.
    ///   4. A SpriteRenderer with non-uniform world scale.
    ///   5. A scene UI Image (simple, no preserve aspect) or RawImage whose rect aspect differs from its image.
    ///   6. A film (StreamingAssets/Memories, Opening video) that is not 16:9, the aspect every player fits it to.
    ///   7. A BackdropFit quad whose saved localScale x/y differs from its imageAspect (backdrop-scale). BackdropFit
    ///      only resizes in LateUpdate, so a fixed imageAspect left the saved quad at the old shape until Play.
    /// Reported but not failed (resolution debt, pass -strictRes to fail on it): a backdrop or film shown larger than
    /// it was authored at 1920x1080, and a texture the importer shrinks below its source size.
    ///
    /// Exempt on purpose: the farewell door leaves squash on x at runtime to fake their swing on the hinge, and the
    /// soft light masks in LightMasks below (each measured, with the number in its comment).
    ///
    /// Fix pass: sets Non Power of 2 to None, sets each backdrop's imageAspect from its painting and refits the quad,
    /// and snaps a card shown within 5% of its image's aspect (a hand-typed size) to the exact aspect, keeping its
    /// height and position. The builders snap the same way through SnapSmallError, so a rebuild matches.
    ///
    /// Menu: Ankhu > Painted > Check No Stretch / Fix No Stretch.
    /// Batch: -executeMethod Ankhu.Realms.EditorTools.NoStretchCheck.CheckCli (exit 1 on any violation)
    ///        -executeMethod Ankhu.Realms.EditorTools.NoStretchCheck.FixCli  (fixes imports and backdrop aspects, then checks)
    /// Report: art_gen/realms/audit/no_stretch_check.md
    /// </summary>
    public static class NoStretchCheck
    {
        public const float Tolerance = 0.01f;
        /// <summary>A card shown within this of its image's aspect is a size typing slip, fixed by keeping height and setting width.</summary>
        public const float SmallError = 0.05f;

        /// <summary>
        /// Soft light masks with no drawn detail: a smooth falloff looks the same at any aspect, so stretching it is not
        /// stretching a picture. Only a texture measured under 0.002 mean absolute Laplacian (on 0..1 alpha and on
        /// luma times alpha, at source resolution) may be listed here, with the number. Everything else stays strict.
        /// </summary>
        private static readonly Dictionary<string, string> LightMasks = new Dictionary<string, string>
        {
            // fx_floorshade5 (Assets/Ankhu/Painted/Art, 4x256): a 1D falloff, constant across x, so any width shows
            // the same thing. Measured 2026-11-26: mean |Laplacian| alpha 0.00173, luma*alpha under 0.002.
            { "fx_floorshade5", "soft light falloff, no drawn detail (mean |Laplacian| 0.00173)" },
        };
        private const string ReportPath = "art_gen/realms/audit/no_stretch_check.md";
        private static readonly string[] Roots = { "Assets/Ankhu", "Assets/Art" };
        private static readonly string[] ExtraScenes = { "Opening_Title", "Opening_Cinematic", "Opening_WalkDuat" };
        private static readonly string[] FilmFiles =
        {
            "Assets/Ankhu/Opening/Video/Opening.mp4",
        };
        private const string FilmDir = "Assets/StreamingAssets/Memories";

        private sealed class Finding
        {
            public string Kind, Where, Detail;
            public bool Fail;
        }

        // ------------------------------------------------------------------ shared helpers

        public static bool IsPow2(int n) => n > 0 && (n & (n - 1)) == 0;

        /// <summary>The image's size on disk, not the imported (possibly resized) size.</summary>
        public static bool SourceSize(Texture t, out int w, out int h)
        {
            w = h = 0;
            if (t == null) return false;
            var imp = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(t)) as TextureImporter;
            if (imp != null) { imp.GetSourceTextureWidthAndHeight(out w, out h); if (w > 0 && h > 0) return true; }
            w = t.width; h = t.height;
            return w > 0 && h > 0;
        }

        /// <summary>The aspect the image was authored at. Use this, never tex.width / tex.height, to size anything.</summary>
        public static float SourceAspect(Texture t) => SourceSize(t, out int w, out int h) ? w / (float)h : 1f;

        private static Texture MainTex(Material m, out string prop)
        {
            prop = null;
            if (m == null) return null;
            foreach (string p in new[] { "_BaseMap", "_MainTex" })
                if (m.HasProperty(p) && m.GetTexture(p) != null) { prop = p; return m.GetTexture(p); }
            return null;
        }

        /// <summary>
        /// The image a quad's material shows and its aspect: the _Mask if it has one, else the main texture's flipbook
        /// frame. tile is the material's tiling (_Tile or the texture scale). False when there is no real image.
        /// </summary>
        public static bool ShownAspect(Material m, out Texture tex, out float content, out string what, out Vector2 tile)
        {
            tex = null; content = 0f; what = null; tile = Vector2.one;
            if (m == null) return false;
            Texture mask = m.HasProperty("_Mask") ? m.GetTexture("_Mask") : null;
            if (mask != null && SourceSize(mask, out int mw, out int mh) && mw > 8 && mh > 8)
            {
                tex = mask; content = mw / (float)mh; what = "mask";
                return true;
            }
            tex = MainTex(m, out string prop);
            if (tex == null || !SourceSize(tex, out int w, out int h) || (w <= 8 && h <= 8)) return false;
            content = w / (float)h;
            what = prop;
            if (m.HasProperty("_Cols") && m.HasProperty("_Rows"))
            {
                float cols = Mathf.Max(1f, m.GetFloat("_Cols")), rows = Mathf.Max(1f, m.GetFloat("_Rows"));
                content *= rows / cols;
                what = "flipbook frame";
            }
            if (m.HasProperty("_Tile")) { Vector4 t = m.GetVector("_Tile"); tile = new Vector2(t.x, t.y); }
            else tile = m.GetTextureScale(prop);
            if (Mathf.Abs(tile.x) < 1e-5f || Mathf.Abs(tile.y) < 1e-5f) tile = Vector2.one;
            return true;
        }

        /// <summary>The quad width over height that shows this material's image at its own aspect, or 0 if it has none.</summary>
        public static float QuadAspect(Material m) =>
            ShownAspect(m, out _, out float c, out _, out Vector2 tile) ? c * Mathf.Abs(tile.x / tile.y) : 0f;

        /// <summary>
        /// For builders: a hand-typed card size within 5% of the image's aspect is a typing slip. Keep the height, set
        /// the width from the image. Larger differences are left alone for the check to report.
        /// </summary>
        public static Vector2 SnapSmallError(Material m, Vector2 size)
        {
            float a = QuadAspect(m);
            if (a <= 0f || Mathf.Abs(size.y) < 1e-6f) return size;
            float k = Mathf.Abs(size.x / size.y) / a;
            if (Mathf.Abs(k - 1f) > 1e-4f && Mathf.Abs(k - 1f) <= SmallError) size.x = Mathf.Sign(size.x) * Mathf.Abs(size.y) * a;
            return size;
        }

        private static readonly MethodInfo FitMethod = typeof(BackdropFit).GetMethod("Fit", BindingFlags.NonPublic | BindingFlags.Instance);

        /// <summary>Runs BackdropFit's own sizing now, at 16:9, as LateUpdate would in the game. For saving and capture.</summary>
        public static void FitBackdrop(BackdropFit bf)
        {
            if (bf == null || FitMethod == null) return;
            var cam = new SerializedObject(bf).FindProperty("cam").objectReferenceValue as Camera;
            bool setAspect = cam != null && cam.targetTexture == null;
            if (setAspect) cam.aspect = 16f / 9f;
            FitMethod.Invoke(bf, null);
            if (setAspect) cam.ResetAspect();
        }

        private static bool Off(float shown, float authored) => authored > 0f && Mathf.Abs(shown / authored - 1f) > Tolerance;

        private static string Path(Transform t)
        {
            var s = new StringBuilder(t.name);
            for (Transform p = t.parent; p != null; p = p.parent) s.Insert(0, p.name + "/");
            return s.ToString();
        }

        // ------------------------------------------------------------------ entry points

        [UnityEditor.MenuItem("Ankhu/Painted/Check No Stretch")]
        public static void CheckFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string open = SceneManager.GetActiveScene().path;
            int bad = Run(false, false, out _);
            if (!string.IsNullOrEmpty(open)) EditorSceneManager.OpenScene(open);
            EditorUtility.DisplayDialog("No stretch", bad == 0 ? "Nothing is stretched." : bad + " stretched images. See " + ReportPath, "OK");
        }

        [UnityEditor.MenuItem("Ankhu/Painted/Fix No Stretch")]
        public static void FixFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string open = SceneManager.GetActiveScene().path;
            int bad = Run(true, false, out _);
            if (!string.IsNullOrEmpty(open)) EditorSceneManager.OpenScene(open);
            EditorUtility.DisplayDialog("No stretch", bad == 0 ? "Fixed. Nothing is stretched." : bad + " left that need new art. See " + ReportPath, "OK");
        }

        public static void CheckCli() => Cli(false);
        public static void FixCli() => Cli(true);

        private static void Cli(bool fix)
        {
            int bad;
            bool strict = System.Environment.GetCommandLineArgs().Contains("-strictRes");
            try
            {
                if (fix) Run(true, strict, out _);
                bad = Run(false, strict, out string report);
                Debug.Log(report);
            }
            catch (Exception e) { Debug.LogException(e); bad = 99; }
            EditorApplication.Exit(bad == 0 ? 0 : 1);
        }

        /// <summary>Returns the number of failing findings. With fix, repairs what needs no new art first.</summary>
        public static int Run(bool fix, bool strictRes, out string report)
        {
            var found = new List<Finding>();
            CheckImports(found, fix);
            foreach (string scene in Scenes()) CheckScene(scene, found, fix);
            CheckFilms(found);
            if (strictRes) foreach (Finding f in found.Where(f => f.Kind.StartsWith("res"))) f.Fail = true;

            int bad = found.Count(f => f.Fail);
            var s = new StringBuilder();
            s.AppendLine("# No-stretch check");
            s.AppendLine();
            s.AppendLine("Run " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + (fix ? " (fix pass)" : "") + ". Tolerance " + (Tolerance * 100f).ToString("0.#") + "%.");
            s.AppendLine();
            s.AppendLine(bad == 0 ? "**PASS.** Nothing is stretched." : "**FAIL.** " + bad + " stretched images.");
            s.AppendLine();
            s.AppendLine("| Result | Kind | Where | Detail |");
            s.AppendLine("|---|---|---|---|");
            foreach (Finding f in found.OrderByDescending(f => f.Fail).ThenBy(f => f.Kind).ThenBy(f => f.Where))
                s.AppendLine("| " + (f.Fail ? "FAIL" : "note") + " | " + f.Kind + " | " + f.Where.Replace("|", "/") + " | " + f.Detail.Replace("|", "/") + " |");
            report = s.ToString();
            string full = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", ReportPath));
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full));
            File.WriteAllText(full, report);
            Debug.Log("[NoStretch] " + (bad == 0 ? "PASS" : "FAIL " + bad) + ", report " + full);
            return bad;
        }

        // ------------------------------------------------------------------ 1. imports

        private static void CheckImports(List<Finding> found, bool fix)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", Roots))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                if (imp == null) continue;
                imp.GetSourceTextureWidthAndHeight(out int w, out int h);
                if (w <= 0 || h <= 0) continue;
                bool npot = !IsPow2(w) || !IsPow2(h);
                if (npot && imp.textureType != TextureImporterType.Sprite && imp.textureShape == TextureImporterShape.Texture2D
                    && imp.npotScale != TextureImporterNPOTScale.None)
                {
                    if (fix)
                    {
                        imp.npotScale = TextureImporterNPOTScale.None;
                        imp.SaveAndReimport();
                        continue;
                    }
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    string shown = tex != null ? tex.width + "x" + tex.height : "power of two";
                    found.Add(new Finding { Kind = "import", Where = path, Fail = true,
                        Detail = "authored " + w + "x" + h + ", imported as " + shown + " (Non Power of 2 = " + imp.npotScale + "). Set it to None." });
                }
                if (Mathf.Max(w, h) > imp.maxTextureSize)
                    found.Add(new Finding { Kind = "res-import", Where = path,
                        Detail = "authored " + w + "x" + h + " but Max Size " + imp.maxTextureSize + " shrinks it on import" });
            }
            if (fix) AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------ 2 to 5. scenes

        private static IEnumerable<string> Scenes()
        {
            var names = new List<string>(ExtraScenes);
            foreach (string c in PlayOrder.Resolved())
            {
                if (!names.Contains(c)) names.Add(c);
                string painted = c.EndsWith(PlayOrder.PaintedSuffix) ? c : c + PlayOrder.PaintedSuffix;
                if (!names.Contains(painted) && FindScenePath(painted) != null) names.Add(painted);
            }
            foreach (string n in names)
            {
                string p = FindScenePath(n);
                if (p != null) yield return p;
            }
        }

        private static string FindScenePath(string name)
        {
            foreach (string guid in AssetDatabase.FindAssets(name + " t:Scene"))
            {
                string p = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(p) == name) return p;
            }
            return null;
        }

        private static void CheckScene(string scenePath, List<Finding> found, bool fix)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            string sn = scene.name;
            bool dirty = false;
            var roots = scene.GetRootGameObjects();

            // 2. painted backdrops
            var backdrops = new HashSet<Transform>();
            foreach (BackdropFit bf in roots.SelectMany(r => r.GetComponentsInChildren<BackdropFit>(true)))
            {
                backdrops.Add(bf.transform);
                Texture tex = MainTex(bf.GetComponent<Renderer>()?.sharedMaterial, out _);
                if (tex == null) continue;
                var so = new SerializedObject(bf);
                SerializedProperty ia = so.FindProperty("imageAspect");
                float src = SourceAspect(tex);
                if (Off(ia.floatValue, src))
                {
                    if (fix)
                    {
                        ia.floatValue = src;
                        so.ApplyModifiedPropertiesWithoutUndo();
                        dirty = true;
                    }
                    else
                        found.Add(new Finding { Kind = "backdrop", Where = sn + ": " + Path(bf.transform), Fail = true,
                            Detail = tex.name + " is " + src.ToString("0.###") + ":1 but shown at " + ia.floatValue.ToString("0.###") + ":1 (" + (ia.floatValue / src).ToString("0.###") + "x wide)" });
                }
                if (fix)
                {
                    Vector3 before = bf.transform.localScale;
                    FitBackdrop(bf);
                    if (bf.transform.localScale != before) dirty = true;
                }
                else
                {
                    Vector3 sc = bf.transform.localScale;
                    if (Mathf.Abs(sc.y) > 1e-5f && Off(Mathf.Abs(sc.x / sc.y), ia.floatValue))
                        found.Add(new Finding { Kind = "backdrop-scale", Where = sn + ": " + Path(bf.transform), Fail = true,
                            Detail = tex.name + " imageAspect " + ia.floatValue.ToString("0.###") + ":1 but the saved quad is " + (sc.x / sc.y).ToString("0.###") + ":1 (" + (sc.x / sc.y / ia.floatValue).ToString("0.###") + "x wide). Run the fix pass." });
                }
                BackdropResolution(sn, bf, so, tex, found);
            }
            var cams = roots.SelectMany(r => r.GetComponentsInChildren<Camera>(true)).ToList();

            // 3. textured quads
            foreach (MeshRenderer mr in roots.SelectMany(r => r.GetComponentsInChildren<MeshRenderer>(true)))
            {
                if (backdrops.Contains(mr.transform)) continue;
                var mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null || mf.sharedMesh.name != "Quad") continue;
                Material m = mr.sharedMaterial;
                if (!ShownAspect(m, out Texture tex, out float content, out string what, out Vector2 tile)) continue;
                Vector3 ls = mr.transform.lossyScale;
                if (Mathf.Abs(ls.y) < 1e-5f) continue;
                float quadAspect = Mathf.Abs(ls.x / ls.y) * Mathf.Abs(tile.y / tile.x);
                if (!Off(quadAspect, content)) continue;
                float k = quadAspect / content;
                if (LightMasks.TryGetValue(tex.name, out string why))
                {
                    found.Add(new Finding { Kind = "light-mask", Where = sn + ": " + Path(mr.transform),
                        Detail = tex.name + " shown " + k.ToString("0.###") + "x wide, allowed: " + why });
                    continue;
                }
                if (fix && Mathf.Abs(k - 1f) <= SmallError)
                {
                    Vector3 sc = mr.transform.localScale;
                    sc.x /= k;
                    mr.transform.localScale = sc;
                    dirty = true;
                    continue;
                }
                found.Add(new Finding { Kind = "card", Where = sn + ": " + Path(mr.transform), Fail = true,
                    Detail = tex.name + " (" + what + ") is " + content.ToString("0.###") + ":1 but the quad shows it at " + quadAspect.ToString("0.###") + ":1 (" + k.ToString("0.###") + "x wide)."
                        + PaintSize(mr, ls, tile, cams) });
            }

            // 4. sprites
            foreach (SpriteRenderer sr in roots.SelectMany(r => r.GetComponentsInChildren<SpriteRenderer>(true)))
            {
                if (sr.sprite == null || sr.drawMode != SpriteDrawMode.Simple) continue;
                Vector3 ls = sr.transform.lossyScale;
                if (Mathf.Abs(ls.y) < 1e-5f) continue;
                float k = Mathf.Abs(ls.x / ls.y);
                if (Off(k, 1f))
                    found.Add(new Finding { Kind = "sprite", Where = sn + ": " + Path(sr.transform), Fail = true,
                        Detail = sr.sprite.name + " scaled " + k.ToString("0.###") + "x wide by a non-uniform scale" });
            }

            // 5. scene UI
            foreach (Graphic g in roots.SelectMany(r => r.GetComponentsInChildren<Graphic>(true)))
            {
                float content;
                string name;
                if (g is RawImage ri && ri.texture != null)
                {
                    if (!SourceSize(ri.texture, out int w, out int h) || (w <= 8 && h <= 8)) continue;
                    Rect uv = ri.uvRect;
                    if (uv.height == 0f) continue;
                    content = w / (float)h * Mathf.Abs(uv.width / uv.height);
                    name = ri.texture.name;
                }
                else if (g is Image im && im.sprite != null && im.type == Image.Type.Simple && !im.preserveAspect)
                {
                    Rect r = im.sprite.rect;
                    if (r.width <= 8 && r.height <= 8) continue;
                    content = r.width / r.height;
                    name = im.sprite.name;
                }
                else continue;
                Rect rect = g.rectTransform.rect;
                Vector3 ls = g.rectTransform.lossyScale;
                if (rect.width < 1f || rect.height < 1f || Mathf.Abs(ls.y) < 1e-6f) continue;
                float shown = rect.width / rect.height * Mathf.Abs(ls.x / ls.y);
                if (Off(shown, content))
                    found.Add(new Finding { Kind = "ui", Where = sn + ": " + Path(g.transform), Fail = true,
                        Detail = name + " is " + content.ToString("0.###") + ":1 but its rect is " + shown.ToString("0.###") + ":1" });
            }

            if (dirty) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
        }

        /// <summary>
        /// The pixel size to paint a card's image at so it is neither stretched nor upscaled: its shown world size (one
        /// tile of it) times the pixels per metre at 1080p from the camera that sees it closest.
        /// </summary>
        private static string PaintSize(MeshRenderer mr, Vector3 ls, Vector2 tile, List<Camera> cams)
        {
            float w = Mathf.Abs(ls.x / tile.x), h = Mathf.Abs(ls.y / tile.y);
            Vector3 c = mr.transform.position;
            float best = 0f; string camName = null;
            foreach (Camera cam in cams)
            {
                Vector3 lp = cam.transform.InverseTransformPoint(c);
                float half;
                if (cam.orthographic) half = cam.orthographicSize;
                else
                {
                    if (lp.z < 0.1f) continue;
                    half = lp.z * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
                }
                if (Mathf.Abs(lp.y) > half * 1.1f || Mathf.Abs(lp.x) > half * 16f / 9f * 1.1f) continue;
                float ppm = 1080f / (2f * half);
                if (ppm > best) { best = ppm; camName = cam.name; }
            }
            string size = " Shown " + w.ToString("0.###") + " x " + h.ToString("0.###") + " m";
            if (best <= 0f) return size + ", in no camera's view at load.";
            return size + ", " + best.ToString("0") + " px/m at 1080p from " + camName + ": paint at "
                + Mathf.Max(1, Mathf.RoundToInt(w * best)) + "x" + Mathf.Max(1, Mathf.RoundToInt(h * best)) + " px.";
        }

        /// <summary>How many screen pixels the backdrop covers at 1920x1080, against the pixels it was painted with.</summary>
        private static void BackdropResolution(string sn, BackdropFit bf, SerializedObject so, Texture tex, List<Finding> found)
        {
            var cam = so.FindProperty("cam").objectReferenceValue as Camera;
            float d = so.FindProperty("distance").floatValue;
            float painted = so.FindProperty("paintedFov").floatValue;
            if (cam == null || d <= 0f || !SourceSize(tex, out int sw, out int sh)) return;
            float aspect = sw / (float)sh;
            float h = 2f * d * Mathf.Tan((painted > 0f ? painted : cam.fieldOfView) * 0.5f * Mathf.Deg2Rad);
            float w = h * aspect;
            float viewW = h * 16f / 9f;
            if (viewW > w) { float k = viewW / w; w *= k; h *= k; }
            float screenH = 2f * d * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float px = w * 1080f / screenH;
            float up = px / sw;
            if (up > 1.05f)
                found.Add(new Finding { Kind = "res-backdrop", Where = sn + ": " + Path(bf.transform),
                    Detail = tex.name + " authored " + sw + "x" + sh + ", shown " + Mathf.RoundToInt(px) + "x" + Mathf.RoundToInt(px / aspect) + " at 1080p (" + up.ToString("0.##") + "x upscale)" });
        }

        // ------------------------------------------------------------------ 6. films

        private static void CheckFilms(List<Finding> found)
        {
            var files = new List<string>(FilmFiles);
            string dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", FilmDir));
            if (Directory.Exists(dir))
                files.AddRange(Directory.GetFiles(dir, "*.mp4").Select(f => FilmDir + "/" + System.IO.Path.GetFileName(f)));
            foreach (string rel in files)
            {
                string full = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", rel));
                if (!File.Exists(full)) continue;
                if (!Mp4Size(full, out int w, out int h))
                {
                    found.Add(new Finding { Kind = "film", Where = rel, Fail = true, Detail = "could not read the picture size" });
                    continue;
                }
                float a = w / (float)h;
                if (Off(a, 16f / 9f))
                    found.Add(new Finding { Kind = "film", Where = rel, Fail = true,
                        Detail = w + "x" + h + " is " + a.ToString("0.###") + ":1, but the players fit every film to 16:9" });
                if (w < 1920)
                    found.Add(new Finding { Kind = "res-film", Where = rel,
                        Detail = w + "x" + h + " shown at 1920x1080 (" + (1920f / w).ToString("0.##") + "x upscale)" });
            }
        }

        /// <summary>Reads the first video track's display size from the mp4 tkhd box.</summary>
        public static bool Mp4Size(string file, out int w, out int h)
        {
            w = h = 0;
            byte[] b = File.ReadAllBytes(file);
            return Walk(b, 0, b.Length, ref w, ref h);
        }

        private static uint U32(byte[] b, int i) => (uint)(b[i] << 24 | b[i + 1] << 16 | b[i + 2] << 8 | b[i + 3]);

        private static bool Walk(byte[] b, int start, int end, ref int w, ref int h)
        {
            int i = start;
            while (i + 8 <= end)
            {
                long size = U32(b, i);
                string type = Encoding.ASCII.GetString(b, i + 4, 4);
                int header = 8;
                if (size == 1 && i + 16 <= end) { size = (long)((ulong)U32(b, i + 8) << 32 | U32(b, i + 12)); header = 16; }
                else if (size == 0) size = end - i;
                if (size < header || i + size > end) return false;
                if (type == "moov" || type == "trak")
                {
                    if (Walk(b, i + header, (int)(i + size), ref w, ref h)) return true;
                }
                else if (type == "tkhd")
                {
                    int at = (int)(i + size) - 8;
                    int tw = (int)(U32(b, at) >> 16), th = (int)(U32(b, at + 4) >> 16);
                    if (tw > 0 && th > 0) { w = tw; h = th; return true; }
                }
                i += (int)size;
            }
            return false;
        }
    }

    /// <summary>
    /// Keeps the no-stretch rule on every new import: a non power of two image under Assets/Ankhu or Assets/Art is
    /// never resized to a power of two (which changes its aspect and resamples it). Sprites ignore the setting anyway.
    /// </summary>
    public sealed class NoStretchImportRule : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!(assetPath.StartsWith("Assets/Ankhu/") || assetPath.StartsWith("Assets/Art/"))) return;
            var imp = (TextureImporter)assetImporter;
            if (imp.textureShape != TextureImporterShape.Texture2D || imp.textureType == TextureImporterType.Sprite) return;
            if (imp.npotScale != TextureImporterNPOTScale.None) imp.npotScale = TextureImporterNPOTScale.None;
        }
    }
}
