using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Ankhu.Player;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Ankhu.EditorTools.Environment
{
    /// <summary>
    /// 2.5D painted test of Borrowed Dawn. Everything but Ankhu becomes authored paint on camera-facing
    /// layers, while the rules engine, colliders and views stay exactly as in the greybox scene.
    ///
    /// 1. ExportSourcesCli renders the dressed greybox room side-on (never saving it) into
    ///    art_gen/borrowed_dawn/painted/src: the full composite, the static plate, a front-layer mask, and
    ///    per moving group a rest mask plus an isolated colour render and masks.
    /// 2. Python and gpt-image paint those and write art_gen/borrowed_dawn/painted/out/painted.json.
    /// 3. BuildPaintedCli opens the greybox scene, hides the plate and group renderers, adds the paint
    ///    quads (group sprites parented to the transform that moves them) and saves a new scene,
    ///    Assets/Ankhu/Generated/Scenes/BorrowedDawn_Painted.unity. The greybox scene file is never written.
    /// 4. BuildPlayerCli builds that one scene to Builds/BorrowedDawn_Painted.
    /// </summary>
    public static class BorrowedDawnPainted
    {
        public const float Ppm = 90f;
        public const float FrontZ = -0.25f;
        public const float FarZ = 2.0f;

        // ---------- which room ----------
        // Any greybox room can be painted. The CLI takes -paintRoom <name> (folder under art_gen) and
        // -paintGreybox <scene path>. With neither it is Borrowed Dawn exactly as before.
        // art_gen/<room>/painted/room.json may set "art" (texture folder) and "plateRect" [x, y, w, h].
        // The painted scene is the greybox name with _Greybox swapped for _Painted (or _Painted added),
        // and it can never be the greybox file itself.
        // Zac 2026-11-25 (cistern layout B): "camera" [halfHeight, y, minX, maxX] pins the follow camera so the whole
        // room sits on one fixed screen, and "seals" [x0, y0, x1, y1, ...] closes the room off with solid walls. The
        // export renders each seal as greybox wall so the painter paints it, the build keeps only its collider.
        [Serializable] private class RoomCfg { public string art; public float[] plateRect; public float[] camera; public float[] seals; }

        private static string roomName;
        private static string greyboxPath;
        private static RoomCfg roomCfg;

        public static string RoomName { get { Resolve(); return roomName; } }
        public static string GreyboxPath { get { Resolve(); return greyboxPath; } }
        public static string PaintedScenePath
        {
            get
            {
                string dir = Path.GetDirectoryName(GreyboxPath).Replace("\\", "/");
                string name = Path.GetFileNameWithoutExtension(GreyboxPath);
                name = name.EndsWith("_Greybox") ? name.Substring(0, name.Length - 8) + "_Painted" : name + "_Painted";
                string p = dir + "/" + name + ".unity";
                if (p == GreyboxPath) throw new InvalidOperationException("Painted scene path equals the greybox " + p);
                return p;
            }
        }
        public static string ArtFolder
        {
            get
            {
                Resolve();
                if (!string.IsNullOrEmpty(roomCfg.art)) return roomCfg.art.TrimEnd('/');
                return roomName == "borrowed_dawn" ? "Assets/Ankhu/Painted/Art" : "Assets/Ankhu/Painted/Art/" + roomName;
            }
        }
        public static Rect PlateRect
        {
            get
            {
                Resolve();
                float[] r = roomCfg.plateRect;
                return r != null && r.Length == 4 ? new Rect(r[0], r[1], r[2], r[3]) : new Rect(-1.5f, -4.5f, 37.5f, 17f);
            }
        }

        private static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        private static string PaintDir => Path.Combine(Root, "art_gen/" + RoomName + "/painted");
        private static string SrcDir => Path.Combine(PaintDir, "src");
        private static string OutDir => Path.Combine(PaintDir, "out");

        private static void Resolve()
        {
            if (roomName != null) return;
            string[] args = System.Environment.GetCommandLineArgs();
            string a(string key) { int i = Array.IndexOf(args, key); return i >= 0 && i + 1 < args.Length ? args[i + 1] : null; }
            roomName = a("-paintRoom") ?? "borrowed_dawn";
            greyboxPath = (a("-paintGreybox") ?? BorrowedDawnGreyboxBuilder.ScenePath).Replace("\\", "/");
            string cfg = Path.Combine(PaintDir, "room.json");
            roomCfg = File.Exists(cfg) ? JsonUtility.FromJson<RoomCfg>(File.ReadAllText(cfg)) : new RoomCfg();
            Debug.Log("[Painted] room " + roomName + ", greybox " + greyboxPath + ", art " + ArtFolder);
        }

        private static readonly string[] KeepNames = { "Binding", "Indicator", "Thread", "Glow", "Beam", "Water", "Light", "Flame", "Crack", "Dawn", "Trigger", "Socket", "Ring", "Lamp", "Halo", "Shaft", "Dust", "Mote" };
        private static readonly string[] SelfMovers = { "RaftView", "BearerView", "ElevatorPlatform", "PendulumBridge" };
        private static readonly string[] SkipRoots = { "Debug", "Zones" };

        // ---------- classification ----------

        private sealed class Room
        {
            public readonly List<Renderer> Plate = new List<Renderer>();
            public readonly List<Renderer> Water = new List<Renderer>();
            public readonly List<Renderer> Kept = new List<Renderer>();
            public readonly List<Transform> Groups = new List<Transform>();
            public readonly Dictionary<Transform, List<Renderer>> GroupRenderers = new Dictionary<Transform, List<Renderer>>();
            public readonly Dictionary<Transform, List<Renderer>> GroupDisabled = new Dictionary<Transform, List<Renderer>>();
        }

        private static bool UnderSkip(Transform t)
        {
            for (Transform p = t; p != null; p = p.parent)
            {
                if (p.parent == null && SkipRoots.Contains(p.name)) return true;
                if (p.name == "Zones" || p.name == "Debug") return true;
                if (p.GetComponent<PlayerController2D>() != null) return true;
                if (p.GetComponent<Camera>() != null) return true;
            }
            return false;
        }

        private static bool IsWater(Renderer r)
        {
            Material m = r.sharedMaterial;
            return m != null && m.name.Contains("Water");
        }

        private static bool Keep3D(Renderer r)
        {
            if (r is LineRenderer || r is TrailRenderer || r is ParticleSystemRenderer || r is SpriteRenderer) return true;
            if (r is SkinnedMeshRenderer) return true;
            string path = r.transform.name + "/" + (r.transform.parent != null ? r.transform.parent.name : "");
            string named = path.Replace("LightScreen", "").Replace("BellBeam", "");
            foreach (string k in KeepNames)
            {
                if (named.Contains(k)) return true;
            }
            Material m = r.sharedMaterial;
            if (m == null) return true;
            string mn = m.name;
            if (mn.Contains("Glow") || mn.Contains("Violet") || mn.Contains("Cyan") || mn.Contains("Beam") || mn.Contains("Flame") || mn.Contains("Ember") || mn.Contains("Additive")) return true;
            // Transparent effects (dust, light cards) stay live.
            return m.renderQueue >= 2900;
        }

        private static Room Classify()
        {
            var room = new Room();
            var groups = new HashSet<Transform>();
            foreach (MonoBehaviour mb in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (mb == null || UnderSkip(mb.transform)) continue;
                Type type = mb.GetType();
                if (type.Namespace == null || !type.Namespace.StartsWith("Ankhu.World")) continue;
                if (SelfMovers.Contains(type.Name)) groups.Add(mb.transform);
                var so = new SerializedObject(mb);
                SerializedProperty p = so.GetIterator();
                while (p.Next(true))
                {
                    if (p.propertyType != SerializedPropertyType.ObjectReference || p.objectReferenceValue == null) continue;
                    Transform t = p.objectReferenceValue is Transform tr ? tr : p.objectReferenceValue is GameObject go ? go.transform : null;
                    if (t != null && !UnderSkip(t) && t.gameObject.scene == mb.gameObject.scene) groups.Add(t);
                }
            }
            // The bell hero is a static Meshy mesh standing in for the frame and the swinging bell: it gets its
            // own group so Python can split the swinging body (cut by the Swing mask) from the frame.
            foreach (Transform t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.name == "Hero_Bell") groups.Add(t);
            }

            foreach (Renderer r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (UnderSkip(r.transform)) continue;
                if (IsWater(r)) { room.Water.Add(r); continue; }
                if (Keep3D(r)) { room.Kept.Add(r); continue; }
                Transform g = null;
                for (Transform p = r.transform; p != null; p = p.parent)
                {
                    if (groups.Contains(p)) { g = p; break; }
                }
                if (g == null)
                {
                    if (r.enabled && r.gameObject.activeInHierarchy) room.Plate.Add(r);
                    continue;
                }
                if (!room.GroupRenderers.ContainsKey(g))
                {
                    room.GroupRenderers[g] = new List<Renderer>();
                    room.GroupDisabled[g] = new List<Renderer>();
                }
                (r.enabled ? room.GroupRenderers[g] : room.GroupDisabled[g]).Add(r);
            }
            room.Groups.AddRange(groups.Where(g => room.GroupRenderers.ContainsKey(g)).OrderBy(PathOf));
            return room;
        }

        public static string PathOf(Transform t)
        {
            var parts = new List<string>();
            for (Transform p = t; p != null; p = p.parent) parts.Add(p.name);
            parts.Reverse();
            return string.Join("/", parts);
        }

        public static string IndexPathOf(Transform t)
        {
            var parts = new List<string>();
            for (Transform p = t; p != null; p = p.parent) parts.Add(p.GetSiblingIndex().ToString());
            parts.Reverse();
            return string.Join("/", parts);
        }

        private static Transform FromIndexPath(Scene scene, string indexPath)
        {
            string[] parts = indexPath.Split('/');
            GameObject[] roots = scene.GetRootGameObjects();
            int i0 = int.Parse(parts[0]);
            Transform t = roots.FirstOrDefault(r => r.transform.GetSiblingIndex() == i0)?.transform;
            for (int i = 1; i < parts.Length && t != null; i++)
            {
                int k = int.Parse(parts[i]);
                t = k < t.childCount ? t.GetChild(k) : null;
            }
            return t;
        }

        private static string Id(Transform t) => PathOf(t).Replace("/", "__").Replace(" ", "_");

        // ---------- export ----------

        public static void ExportSourcesCli()
        {
            try
            {
                ExportSources();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        [MenuItem("Ankhu/Painted/Export Sources")]
        public static void ExportSources()
        {
            EditorSceneManager.OpenScene(GreyboxPath, OpenSceneMode.Single);
            Directory.CreateDirectory(SrcDir);
            AddSeals(false);
            Room room = Classify();
            AutoRect();
            Camera cam = Camera.main;
            var white = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            white.SetColor("_BaseColor", Color.white);

            var all = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var state = all.ToDictionary(r => r, r => r.enabled);

            var groupRest = new List<Renderer>();
            foreach (Transform g in room.Groups) groupRest.AddRange(room.GroupRenderers[g].Where(r => r.gameObject.activeInHierarchy));
            var front = room.Plate.Where(r => r.bounds.center.z < FrontZ).ToList();

            Render(cam, all, room.Plate.Concat(groupRest), null, "full.png", false);
            Render(cam, all, room.Plate, null, "plate.png", false);
            Render(cam, all, front, white, "front_mask.png", true);
            Render(cam, all, room.Water, white, "water_mask.png", true);
            Render(cam, all, room.Plate.Where(r => r.bounds.center.z >= FarZ), null, "far.png", false);
            Render(cam, all, room.Plate.Where(r => r.bounds.center.z < FarZ), white, "mid_mask.png", true);

            var sb = new StringBuilder();
            sb.Append("{\n  \"ppm\": ").Append(Ppm).Append(",\n  \"rect\": [").Append(F(PlateRect.xMin)).Append(", ").Append(F(PlateRect.yMin))
              .Append(", ").Append(F(PlateRect.width)).Append(", ").Append(F(PlateRect.height)).Append("],\n");
            sb.Append("  \"plateCount\": ").Append(room.Plate.Count).Append(", \"frontCount\": ").Append(front.Count)
              .Append(", \"waterCount\": ").Append(room.Water.Count).Append(", \"keptCount\": ").Append(room.Kept.Count).Append(",\n");
            sb.Append("  \"water\": [").Append(string.Join(", ", room.Water.Select(r => Q(PathOf(r.transform))))).Append("],\n");
            sb.Append("  \"kept\": [").Append(string.Join(", ", room.Kept.Select(r => Q(PathOf(r.transform))))).Append("],\n");
            sb.Append("  \"groups\": [\n");
            var entries = new List<string>();
            for (int i = 0; i < room.Groups.Count; i++)
            {
                Transform g = room.Groups[i];
                string id = Id(g);
                List<Renderer> own = room.GroupRenderers[g];
                List<Renderer> off = room.GroupDisabled[g];
                if (own.Count + off.Count == 0) continue;
                bool activeAtRest = g.gameObject.activeInHierarchy;
                Render(cam, all, own.Where(r => r.gameObject.activeInHierarchy), white, "g_" + id + "_rest.png", true);
                var reactivated = Activate(g);
                Render(cam, all, own, null, "g_" + id + "_iso.png", false, new Color(0.5f, 0.5f, 0.5f, 1f));
                Render(cam, all, own, white, "g_" + id + "_isomask.png", true);
                Render(cam, all, own.Concat(off), white, "g_" + id + "_force.png", true);
                foreach (GameObject go in reactivated) go.SetActive(false);
                var both = own.Concat(off).ToList();
                Bounds b = both[0].bounds;
                foreach (Renderer r in both) b.Encapsulate(r.bounds);
                var eb = new StringBuilder();
                eb.Append("    {\"id\": ").Append(Q(id)).Append(", \"path\": ").Append(Q(PathOf(g))).Append(", \"index\": ").Append(Q(IndexPathOf(g)))
                  .Append(", \"activeAtRest\": ").Append(activeAtRest ? "true" : "false")
                  .Append(", \"z\": ").Append(F(b.center.z)).Append(", \"zmin\": ").Append(F(b.min.z))
                  .Append(", \"renderers\": [").Append(string.Join(", ", own.Select(r => Q(r.name + "|" + (r.sharedMaterial != null ? r.sharedMaterial.name : "")))))
                  .Append("], \"disabled\": ").Append(off.Count).Append("}");
                entries.Add(eb.ToString());
            }
            sb.Append(string.Join(",\n", entries)).Append("\n");
            sb.Append("  ],\n  \"plate\": [\n");
            for (int i = 0; i < room.Plate.Count; i++)
            {
                Renderer r = room.Plate[i];
                sb.Append("    [").Append(Q(PathOf(r.transform))).Append(", ").Append(F(r.bounds.center.z)).Append("]").Append(i < room.Plate.Count - 1 ? ",\n" : "\n");
            }
            sb.Append("  ]\n}\n");
            File.WriteAllText(Path.Combine(SrcDir, "manifest.json"), sb.ToString());
            foreach (var kv in state) if (kv.Key != null) kv.Key.enabled = kv.Value;
            Debug.Log("[Painted] exported " + room.Groups.Count + " groups, " + room.Plate.Count + " plate renderers to " + SrcDir);
        }

        // Seals are copies of the greybox west wall, so they classify as plate and render as wall in the export.
        // In the painted build they keep only their collider (the painting shows the wall).
        private static void AddSeals(bool colliderOnly)
        {
            Resolve();
            float[] s = roomCfg.seals;
            if (s == null || s.Length < 4) return;
            GameObject wall = GameObject.Find("WestWall");
            if (wall == null) throw new InvalidOperationException("No WestWall to copy for the room seals");
            for (int i = 0; i + 3 < s.Length; i += 4)
            {
                GameObject g = UnityEngine.Object.Instantiate(wall, wall.transform.parent);
                g.name = "ChamberSeal_" + (i / 4);
                Vector3 p = wall.transform.position, ls = wall.transform.localScale;
                g.transform.position = new Vector3((s[i] + s[i + 2]) * 0.5f, (s[i + 1] + s[i + 3]) * 0.5f, p.z);
                g.transform.localScale = new Vector3(s[i + 2] - s[i], s[i + 3] - s[i + 1], ls.z);
                if (colliderOnly)
                {
                    UnityEngine.Object.DestroyImmediate(g.GetComponent<MeshRenderer>());
                    UnityEngine.Object.DestroyImmediate(g.GetComponent<MeshFilter>());
                }
                Debug.Log("[Painted] seal " + g.name + " x " + s[i] + ".." + s[i + 2] + " y " + s[i + 1] + ".." + s[i + 3]);
            }
        }

        private static void FixCamera()
        {
            Resolve();
            float[] c = roomCfg.camera;
            if (c == null || c.Length != 4) return;
            var follow = UnityEngine.Object.FindFirstObjectByType<Ankhu.CameraRig.CameraFollow2D>(FindObjectsInactive.Include);
            if (follow == null) { Debug.LogWarning("[Painted] no CameraFollow2D to pin"); return; }
            follow.SetFraming(c[0], c[1], c[2], c[3]);
            follow.SnapToTarget();
            EditorUtility.SetDirty(follow);
            Debug.Log("[Painted] camera pinned: half height " + c[0] + ", y " + c[1] + ", x " + c[2] + ".." + c[3]);
        }

        // A room with no plateRect in room.json gets one from its solid gameplay colliders plus a margin,
        // so the export frames the whole playable space. Logged so it can be pinned in room.json later.
        private static void AutoRect()
        {
            Resolve();
            if (roomCfg.plateRect != null && roomCfg.plateRect.Length == 4) return;
            if (roomName == "borrowed_dawn") return;
            Bounds? b = null;
            foreach (Collider2D c in UnityEngine.Object.FindObjectsByType<Collider2D>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (c.isTrigger || c.bounds.size.x > 200f || c.bounds.size.y > 200f) continue;
                if (b == null) b = c.bounds; else { Bounds t = b.Value; t.Encapsulate(c.bounds); b = t; }
            }
            if (b == null) throw new InvalidOperationException("No solid colliders to frame " + GreyboxPath);
            const float mx = 2f, myBelow = 1.5f, myAbove = 4f;
            float x = Mathf.Floor(b.Value.min.x - mx), y = Mathf.Floor(b.Value.min.y - myBelow);
            float w = Mathf.Ceil(b.Value.max.x + mx) - x, h = Mathf.Ceil(b.Value.max.y + myAbove) - y;
            roomCfg.plateRect = new[] { x, y, w, h };
            Debug.Log("[Painted] auto plate rect " + F(x) + ", " + F(y) + ", " + F(w) + ", " + F(h));
        }

        private static List<GameObject> Activate(Transform t)
        {
            var list = new List<GameObject>();
            for (Transform p = t; p != null; p = p.parent)
            {
                if (!p.gameObject.activeSelf) { p.gameObject.SetActive(true); list.Add(p.gameObject); }
            }
            return list;
        }

        private static string F(float v) => v.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
        private static string Q(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

        private static void Render(Camera cam, Renderer[] all, IEnumerable<Renderer> visible, Material overrideMat, string file, bool mask, Color? bg = null)
        {
            var show = new HashSet<Renderer>(visible);
            var swapped = new Dictionary<Renderer, Material[]>();
            foreach (Renderer r in all)
            {
                if (r == null) continue;
                r.enabled = show.Contains(r);
                if (overrideMat != null && r.enabled)
                {
                    swapped[r] = r.sharedMaterials;
                    r.sharedMaterials = Enumerable.Repeat(overrideMat, r.sharedMaterials.Length).ToArray();
                }
            }
            int w = Mathf.RoundToInt(PlateRect.width * Ppm), h = Mathf.RoundToInt(PlateRect.height * Ppm);
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = mask ? 1 : 4 };
            var img = new Texture2D(w, h, TextureFormat.RGB24, false);
            Vector3 pos = cam.transform.position;
            Quaternion rot = cam.transform.rotation;
            bool ortho = cam.orthographic;
            float size = cam.orthographicSize, far = cam.farClipPlane;
            CameraClearFlags clear = cam.clearFlags;
            Color bgc = cam.backgroundColor;
            UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
            bool post = data != null && data.renderPostProcessing;
            RenderTexture prevActive = RenderTexture.active;
            try
            {
                cam.transform.SetPositionAndRotation(new Vector3(PlateRect.center.x, PlateRect.center.y, -30f), Quaternion.identity);
                cam.orthographic = true;
                cam.orthographicSize = PlateRect.height * 0.5f;
                cam.farClipPlane = 200f;
                if (mask || bg.HasValue)
                {
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = mask ? Color.black : bg.Value;
                }
                if (mask && data != null) data.renderPostProcessing = false;
                cam.targetTexture = rt;
                cam.Render();
                cam.Render();
                RenderTexture.active = rt;
                img.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                img.Apply();
                File.WriteAllBytes(Path.Combine(SrcDir, file), img.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = prevActive;
                cam.targetTexture = null;
                cam.transform.SetPositionAndRotation(pos, rot);
                cam.orthographic = ortho;
                cam.orthographicSize = size;
                cam.farClipPlane = far;
                cam.clearFlags = clear;
                cam.backgroundColor = bgc;
                if (data != null) data.renderPostProcessing = post;
                foreach (var kv in swapped) kv.Key.sharedMaterials = kv.Value;
                rt.Release();
                UnityEngine.Object.DestroyImmediate(rt);
                UnityEngine.Object.DestroyImmediate(img);
            }
        }

        // ---------- build ----------

        [Serializable] private class Layer { public string id; public string png; public string normal; public float xmin; public float ymin; public float w; public float h; public float z; public string index; public string path;
            // Ambient motion layers (Ankhu/PaintedAnim): a scrolling colour sheet under a static mask.
            public string mask; public int anim; public float tileX, tileY, scrollX, scrollY, tile2X, tile2Y, scroll2X, scroll2Y, mix2, gain, pulse, pulseSpeed, flicker; public int additive; }
        [Serializable] private class PaintedSpec { public Layer[] layers; public string water; public int waterFrames; public float waterMetres; public string[] hide; public float lightGain; public Nudge[] nudge; public string waterSurface; public float waterTint; public float waterBodyAlpha; public float waterDepthDark; public float waterScroll; }
        [Serializable] private class Nudge { public string path; public float dx; public float dy; public float dz; public float scale = 1f; }

        public static void BuildPaintedCli()
        {
            try
            {
                BuildPainted();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }

        [MenuItem("Ankhu/Painted/Build Painted Scene")]
        public static void BuildPainted()
        {
            var spec = JsonUtility.FromJson<PaintedSpec>(File.ReadAllText(Path.Combine(OutDir, "painted.json")));
            AssetDatabase.Refresh();
            Scene scene = EditorSceneManager.OpenScene(GreyboxPath, OpenSceneMode.Single);
            AddSeals(true);
            FixCamera();
            // Zac 2026-11-26: the plates are painted from a flat side-on export, so they only line up in the
            // orthographic view (what F2 used to switch to). Painted rooms start in it, F2 still flips to perspective.
            var follow = UnityEngine.Object.FindFirstObjectByType<Ankhu.CameraRig.CameraFollow2D>(FindObjectsInactive.Include);
            if (follow != null) { follow.SetProjection(Ankhu.CameraRig.CameraFollow2D.Projection.Orthographic); EditorUtility.SetDirty(follow); }
            Room room = Classify();
            foreach (Renderer r in room.Plate) r.enabled = false;
            foreach (var kv in room.GroupRenderers) foreach (Renderer r in kv.Value) r.enabled = false;
            // The master's light is painted in, so the dresser's volumetric beam would double it.
            GameObject shafts = GameObject.Find("LightShafts");
            if (shafts != null) foreach (Renderer r in shafts.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            // Static dressing that Keep3D kept only because its name holds a keep word (Cornice_Beam, ShaftWindow_Glow).
            // The master already paints these, so the live copies would double them.
            string[] paintedOver = { "Cornice_", "ShaftWindow_", "ShaftPool", "ShaftRail", "ShaftLintel", "LightWellBar", "LightWellLip_" };
            foreach (Renderer r in room.Kept)
            {
                foreach (string p in paintedOver) if (r.name.StartsWith(p)) { r.enabled = false; break; }
            }

            // Greybox props the master never painted get authored sprites instead (painted.json "hide").
            int hidden = 0;
            if (spec.hide != null)
            {
                foreach (string hp in spec.hide)
                {
                    GameObject h = GameObject.Find(hp);
                    if (h == null) { Debug.LogWarning("[Painted] hide path not found: " + hp); continue; }
                    foreach (Renderer r in h.GetComponentsInChildren<Renderer>(true)) { r.enabled = false; hidden++; }
                }
            }
            // Live indicators the painting frames (selector lamps, gate lamp) are moved onto their painted bezels.
            if (spec.nudge != null)
            {
                foreach (Nudge n in spec.nudge)
                {
                    GameObject g = GameObject.Find(n.path);
                    if (g == null) { Debug.LogWarning("[Painted] nudge path not found: " + n.path); continue; }
                    g.transform.position += new Vector3(n.dx, n.dy, n.dz);
                    if (n.scale > 0f) g.transform.localScale *= n.scale;
                }
            }
            DumpKept(room);

            string matDir = ArtFolder + "/Materials";
            Directory.CreateDirectory(Path.Combine(Root, matDir));
            // Live beams: soft additive light instead of the greybox strip.
            Shader beamShader = Shader.Find("Ankhu/PaintedBeam");
            if (beamShader != null)
            {
                string bp = matDir + "/P_Beam.mat";
                var bm = AssetDatabase.LoadAssetAtPath<Material>(bp);
                if (bm == null) { bm = new Material(beamShader); AssetDatabase.CreateAsset(bm, bp); }
                bm.shader = beamShader;
                // r05 read as a hard white bar: keep the shaft a soft warm haze like the painted light.
                bm.SetFloat("_Gain", 0.5f);
                bm.SetFloat("_WarmMix", 0.85f);
                bm.SetFloat("_Soft", 3.0f);
                bm.SetFloat("_EndFade", 0.2f);
                EditorUtility.SetDirty(bm);
                foreach (LineRenderer lr in UnityEngine.Object.FindObjectsByType<LineRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (lr.sharedMaterial != null && lr.sharedMaterial.name.Contains("Beam")) lr.sharedMaterial = bm;
                }
                // The mirror beam's 6 cm core read as a hard white wire in r12 to r14. Paint it as a soft
                // ray: a wider, dimmer core inside a broad glow.
                string cp = matDir + "/P_BeamCore.mat";
                var cm = AssetDatabase.LoadAssetAtPath<Material>(cp);
                if (cm == null) { cm = new Material(beamShader); AssetDatabase.CreateAsset(cm, cp); }
                cm.shader = beamShader;
                cm.SetFloat("_Gain", 0.35f);
                cm.SetFloat("_WarmMix", 0.7f);
                cm.SetFloat("_Soft", 1.6f);
                cm.SetFloat("_EndFade", 0.08f);
                EditorUtility.SetDirty(cm);
                foreach (LineRenderer lr in UnityEngine.Object.FindObjectsByType<LineRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (lr.name.EndsWith("Core")) { lr.sharedMaterial = cm; lr.widthMultiplier = 2.2f; }
                    else if (lr.name.EndsWith("Glow")) { lr.sharedMaterial = bm; lr.widthMultiplier = 2.4f; }
                }
            }
            // Bell sound rings: the greybox draws them as solid gold discs, which read as a pale moon behind
            // the painted bell. Swap each disc for a flat quad with a soft additive ring (BellView still
            // toggles and scales the same renderer).
            Shader add = Shader.Find("Ankhu/Realms/MagicAdditive");
            Texture2D ringTex = ImportTexture(ArtFolder + "/fx_soundring.png", false);
            if (add != null && ringTex != null)
            {
                string rp = matDir + "/P_SoundRing.mat";
                var rm = AssetDatabase.LoadAssetAtPath<Material>(rp);
                if (rm == null) { rm = new Material(add); AssetDatabase.CreateAsset(rm, rp); }
                rm.shader = add;
                rm.SetTexture("_MainTex", ringTex);
                rm.SetColor("_Color", new Color(1.0f, 0.78f, 0.45f, 0.55f));
                EditorUtility.SetDirty(rm);
                int rings = 0;
                foreach (Transform t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (!t.name.StartsWith("SoundRing_")) continue;
                    foreach (MeshRenderer mr in t.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        mr.GetComponent<MeshFilter>().sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
                        mr.transform.localRotation = Quaternion.identity;
                        mr.transform.localScale = new Vector3(1.6f, 1.6f, 1f);
                        mr.sharedMaterial = rm;
                        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        rings++;
                    }
                }
                Debug.Log("[Painted] sound rings swapped: " + rings);
            }
            // Binding threads: the greybox draws them as hard flat bars. Swap each to a soft additive
            // strand on a quad. BindingIndicator still tints and pulses _Color through a property block.
            Texture2D threadTex = ImportTexture(ArtFolder + "/fx_thread.png", false);
            if (add != null && threadTex != null)
            {
                string tp = matDir + "/P_Thread.mat";
                var tm = AssetDatabase.LoadAssetAtPath<Material>(tp);
                if (tm == null) { tm = new Material(add); AssetDatabase.CreateAsset(tm, tp); }
                tm.shader = add;
                tm.SetTexture("_MainTex", threadTex);
                tm.SetColor("_Color", new Color(0.8f, 0.6f, 1f, 0.9f));
                EditorUtility.SetDirty(tm);
                int threads = 0;
                foreach (Transform t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (!t.name.StartsWith("Binding_")) continue;
                    foreach (MeshRenderer mr in t.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        if (mr.name != "Thread" && mr.name != "Band") continue;
                        var mf = mr.GetComponent<MeshFilter>();
                        if (mf == null) continue;
                        Vector3 s = mr.transform.localScale;
                        bool vertical = Mathf.Abs(s.y) > Mathf.Abs(s.x);
                        float len = vertical ? s.y : s.x;
                        float thick = Mathf.Max(0.08f, (vertical ? s.x : s.y)) * 4f;
                        mf.sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
                        mr.transform.localRotation = vertical ? Quaternion.Euler(0f, 0f, 90f) : Quaternion.identity;
                        mr.transform.localScale = new Vector3(len, thick, 1f);
                        mr.sharedMaterial = tm;
                        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        threads++;
                    }
                }
                Debug.Log("[Painted] binding threads swapped: " + threads);
            }
            // Live lamps and glow knots (selector lamps, gate lamps, sun disk): the greybox draws them as flat
            // unlit cylinders. Swap each to a soft additive glow on a quad. GreyboxRender.Tint still writes _Color
            // through a property block, so lamp state reads the same. Only when the room ships fx_glow.png.
            Texture2D glowTex = ImportTexture(ArtFolder + "/fx_glow.png", false);
            if (add != null && glowTex != null)
            {
                string gp = matDir + "/P_Glow.mat";
                var gm = AssetDatabase.LoadAssetAtPath<Material>(gp);
                if (gm == null) { gm = new Material(add); AssetDatabase.CreateAsset(gm, gp); }
                gm.shader = add;
                gm.SetTexture("_MainTex", glowTex);
                gm.SetColor("_Color", new Color(1f, 0.85f, 0.55f, 0.9f));
                EditorUtility.SetDirty(gm);
                int glows = 0;
                foreach (MeshRenderer mr in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    Material m0 = mr.sharedMaterial;
                    if (m0 == null || !m0.name.StartsWith("Glow") || m0.GetTexture("_BaseMap") != null) continue;
                    var mf = mr.GetComponent<MeshFilter>();
                    if (mf == null) continue;
                    Vector3 s = mr.transform.lossyScale;
                    float d = Mathf.Max(0.25f, Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y))) * 2.2f;
                    mf.sharedMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
                    mr.transform.rotation = Quaternion.identity;
                    Vector3 ps = mr.transform.parent != null ? mr.transform.parent.lossyScale : Vector3.one;
                    mr.transform.localScale = new Vector3(d / Safe(ps.x), d / Safe(ps.y), 1f);
                    mr.sharedMaterial = gm;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    glows++;
                }
                Debug.Log("[Painted] lamp glows swapped: " + glows);
            }
            Shader lit = Shader.Find("Ankhu/PaintedLit");
            if (lit == null || !lit.isSupported) throw new InvalidOperationException("Ankhu/PaintedLit shader missing or unsupported");
            Mesh quad = Resources.GetBuiltinResource<Mesh>("Quad.fbx");
            var paintedRoot = new GameObject("Painted").transform;

            int placed = 0;
            Shader animShader = Shader.Find("Ankhu/PaintedAnim");
            int animated = 0;
            foreach (Layer layer in spec.layers)
            {
                if (layer.anim != 0)
                {
                    if (PlaceAnimLayer(scene, layer, animShader, quad, matDir, paintedRoot)) animated++;
                    continue;
                }
                Texture2D tex = ImportTexture(ArtFolder + "/" + layer.png, false);
                Texture2D nrm = string.IsNullOrEmpty(layer.normal) ? null : ImportTexture(ArtFolder + "/" + layer.normal, true);
                if (tex == null) { Debug.LogWarning("[Painted] missing " + layer.png); continue; }
                string matPath = matDir + "/P_" + layer.id + ".mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null) { mat = new Material(lit); AssetDatabase.CreateAsset(mat, matPath); }
                mat.shader = lit;
                mat.SetTexture("_BaseMap", tex);
                if (nrm != null) mat.SetTexture("_NormalMap", nrm);
                // The master already has its light painted in, so live lamps only add a hint of flicker.
                if (spec.lightGain > 0) mat.SetFloat("_LightGain", spec.lightGain);
                EditorUtility.SetDirty(mat);

                Transform parent = paintedRoot;
                if (!string.IsNullOrEmpty(layer.index))
                {
                    parent = FromIndexPath(scene, layer.index);
                    if (parent == null || PathOf(parent) != layer.path)
                    {
                        Debug.LogWarning("[Painted] parent mismatch for " + layer.id + ", got " + (parent != null ? PathOf(parent) : "null"));
                        parent = GameObject.Find(layer.path)?.transform ?? paintedRoot;
                    }
                }
                var go = new GameObject("Paint_" + layer.id);
                go.AddComponent<MeshFilter>().sharedMesh = quad;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
                go.transform.SetParent(parent, false);
                go.transform.position = new Vector3(layer.xmin + layer.w * 0.5f, layer.ymin + layer.h * 0.5f, layer.z);
                go.transform.rotation = Quaternion.identity;
                Vector3 ls = parent.lossyScale;
                go.transform.localScale = new Vector3(layer.w / Safe(ls.x), layer.h / Safe(ls.y), 1f / Safe(ls.z));
                // Lift gates: the painted post sinks into its slab instead of floating up over the wall.
                bool gate = layer.path != null && layer.path.Contains("/LiftGate_") && layer.path.EndsWith("/Bars");
                mat.SetFloat("_ClipMinY", gate ? layer.ymin + 0.04f : -100000f);
                if (gate)
                {
                    go.AddComponent<Ankhu.Painted.PaintedGateSink>().Configure(parent, go.transform.position);
                    Debug.Log("[Painted] gate sink on " + layer.path + " clip " + (layer.ymin + 0.04f).ToString("0.00"));
                }
                placed++;
            }
            Debug.Log("[Painted] ambient motion layers: " + animated);

            // Sun gate: painted leaves slide apart behind the painted frame when the greybox leaves narrow.
            GameObject sunGate = GameObject.Find("Room/Props/SunGate");
            if (sunGate != null)
            {
                Transform doorW = sunGate.transform.Find("Paint_gate_door_w");
                Transform doorE = sunGate.transform.Find("Paint_gate_door_e");
                Transform dawnT = sunGate.transform.Find("Paint_gate_dawn");
                Transform leafW = sunGate.transform.Find("Leaf_West");
                if (doorW != null && doorE != null && leafW != null)
                {
                    var pg = sunGate.GetComponent<Ankhu.Painted.PaintedSunGate>() ?? sunGate.AddComponent<Ankhu.Painted.PaintedSunGate>();
                    Renderer dawnR = dawnT != null ? dawnT.GetComponent<Renderer>() : null;
                    pg.Configure(leafW, doorW, doorE, dawnR, Ankhu.Layout.BorrowedDawnLayout.GateWidth * 0.5f, 73f / 90f);
                    if (dawnR != null) dawnR.enabled = false;
                    Debug.Log("[Painted] sun gate leaves wired");
                }
                else
                {
                    Debug.LogWarning("[Painted] sun gate paint layers not found");
                }
            }

            // Seat Ankhu in the paint: warm grade on his materials and a soft contact shadow (painted scene only).
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerController2D>(FindObjectsInactive.Include);
            if (player != null)
            {
                var grade = player.GetComponent<Ankhu.Painted.PaintedActorGrade>() ?? player.gameObject.AddComponent<Ankhu.Painted.PaintedActorGrade>();
                grade.Configure(new Color(0.84f, 0.76f, 0.66f, 1f), 0.7f);
                Debug.Log("[Painted] Ankhu grade and contact shadow wired");
            }

            if (!string.IsNullOrEmpty(spec.water))
            {
                Texture2D sheet = ImportTexture(ArtFolder + "/" + spec.water, false, true);
                Shader ws = Shader.Find("Ankhu/PaintedWater");
                if (sheet != null && ws != null)
                {
                    string wp = matDir + "/P_Water.mat";
                    var wm = AssetDatabase.LoadAssetAtPath<Material>(wp);
                    if (wm == null) { wm = new Material(ws); AssetDatabase.CreateAsset(wm, wp); }
                    wm.shader = ws;
                    wm.SetTexture("_Flipbook", sheet);
                    wm.SetFloat("_Frames", spec.waterFrames);
                    wm.SetFloat("_MetresPerTile", spec.waterMetres);
                    wm.SetColor("_Tint", new Color(1f, 1f, 1f, spec.waterTint > 0f ? spec.waterTint : 0.55f)); // let the painted basin read through
                    wm.SetFloat("_BodyAlpha", spec.waterBodyAlpha > 0f ? spec.waterBodyAlpha : 0.8f);
                    wm.SetFloat("_DepthDark", spec.waterDepthDark > 0f ? spec.waterDepthDark : 0.25f);
            wm.SetFloat("_ScrollY", spec.waterScroll);
                    Texture2D strip = string.IsNullOrEmpty(spec.waterSurface) ? null : ImportTexture(ArtFolder + "/" + spec.waterSurface, false, true);
                    wm.SetTexture("_SurfaceTex", strip);
                    wm.SetFloat("_SurfaceOn", strip != null ? 1f : 0f);
                    EditorUtility.SetDirty(wm);
                    foreach (Renderer r in room.Water) r.sharedMaterials = Enumerable.Repeat(wm, r.sharedMaterials.Length).ToArray();
                }
            }
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveScene(scene, PaintedScenePath, false)) throw new IOException("Could not save " + PaintedScenePath);
            Debug.Log("[Painted] built " + PaintedScenePath + " with " + placed + " paint layers, hid " + room.Plate.Count + " plate renderers and " + hidden + " prop renderers");
        }

        // Every renderer still drawn live after the build, with its world bounds, so the judge can name artifacts.
        private static void DumpKept(Room room)
        {
            var sb = new System.Text.StringBuilder("[\n");
            bool first = true;
            foreach (Renderer r in room.Kept.Concat(room.Water))
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                Bounds b = r.bounds;
                string mat = r.sharedMaterial != null ? r.sharedMaterial.name : "";
                sb.Append(first ? "" : ",\n").Append("{\"path\":\"").Append(PathOf(r.transform)).Append("\",\"type\":\"").Append(r.GetType().Name)
                  .Append("\",\"mat\":\"").Append(mat).Append("\",\"min\":[").Append(b.min.x.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)).Append(",")
                  .Append(b.min.y.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)).Append("],\"max\":[")
                  .Append(b.max.x.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)).Append(",")
                  .Append(b.max.y.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)).Append("]}");
                first = false;
            }
            File.WriteAllText(Path.Combine(OutDir, "kept_dump.json"), sb.Append("\n]\n").ToString());
        }

        private static float Safe(float v) => Mathf.Abs(v) < 1e-4f ? 1e-4f : v;

        private static bool PlaceAnimLayer(Scene scene, Layer layer, Shader shader, Mesh quad, string matDir, Transform paintedRoot)
        {
            if (shader == null) { Debug.LogWarning("[Painted] Ankhu/PaintedAnim missing, skipping " + layer.id); return false; }
            Texture2D sheet = ImportTexture(ArtFolder + "/" + layer.png, false, true);
            Texture2D mask = ImportTexture(ArtFolder + "/" + layer.mask, false);
            if (sheet == null || mask == null) { Debug.LogWarning("[Painted] missing anim art for " + layer.id); return false; }
            string matPath = matDir + "/A_" + layer.id + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, matPath); }
            mat.shader = shader;
            mat.SetTexture("_BaseMap", sheet);
            mat.SetTexture("_Mask", mask);
            mat.SetVector("_Tile", new Vector4(Or1(layer.tileX), Or1(layer.tileY), 0, 0));
            mat.SetVector("_Scroll", new Vector4(layer.scrollX, layer.scrollY, 0, 0));
            mat.SetVector("_Tile2", new Vector4(Or1(layer.tile2X), Or1(layer.tile2Y), 0, 0));
            mat.SetVector("_Scroll2", new Vector4(layer.scroll2X, layer.scroll2Y, 0, 0));
            mat.SetFloat("_Mix2", layer.mix2);
            mat.SetFloat("_Gain", Or1(layer.gain));
            mat.SetFloat("_PulseAmp", layer.pulse);
            mat.SetFloat("_PulseSpeed", Or1(layer.pulseSpeed));
            mat.SetFloat("_Flicker", layer.flicker);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", layer.additive != 0 ? (float)UnityEngine.Rendering.BlendMode.One : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            EditorUtility.SetDirty(mat);

            Transform parent = paintedRoot;
            if (!string.IsNullOrEmpty(layer.path))
            {
                parent = (string.IsNullOrEmpty(layer.index) ? null : FromIndexPath(scene, layer.index));
                if (parent == null || PathOf(parent) != layer.path) parent = GameObject.Find(layer.path)?.transform ?? paintedRoot;
            }
            var go = new GameObject("Anim_" + layer.id);
            go.AddComponent<MeshFilter>().sharedMesh = quad;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(layer.xmin + layer.w * 0.5f, layer.ymin + layer.h * 0.5f, layer.z);
            go.transform.rotation = Quaternion.identity;
            Vector3 ls = parent.lossyScale;
            go.transform.localScale = new Vector3(layer.w / Safe(ls.x), layer.h / Safe(ls.y), 1f / Safe(ls.z));
            return true;
        }

        private static float Or1(float v) => v == 0f ? 1f : v;

        private static Texture2D ImportTexture(string assetPath, bool normal, bool repeat = false)
        {
            if (!File.Exists(Path.Combine(Root, assetPath))) return null;
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            var ti = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (ti != null)
            {
                ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                ti.alphaIsTransparency = !normal;
                ti.sRGBTexture = !normal;
                ti.mipmapEnabled = true;
                // Zoomed-out views were reading the next mip down and losing the brushwork: blend mips and lean sharp.
                ti.filterMode = FilterMode.Trilinear;
                ti.mipMapBias = -0.5f;
                ti.anisoLevel = 4;
                ti.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
                ti.maxTextureSize = 8192;
                ti.textureCompression = TextureImporterCompression.CompressedHQ;
                ti.npotScale = TextureImporterNPOTScale.None;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
        }

        // ---------- player ----------

        public static void BuildPlayerCli()
        {
            try
            {
                string sceneName = Path.GetFileNameWithoutExtension(PaintedScenePath);
                string outDir = Path.Combine(Root, "Builds/" + sceneName);
                Directory.CreateDirectory(outDir);
                BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { PaintedScenePath },
                    locationPathName = Path.Combine(outDir, sceneName + ".exe"),
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None,
                });
                Debug.Log("[Painted] player build " + report.summary.result + " " + report.summary.totalSize + " bytes, " + report.summary.totalErrors + " errors");
                EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorApplication.Exit(1);
            }
        }
    }
}
