using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Ankhu.Realms.Core.Content;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ankhu.Realms.EditorTools
{
    /// <summary>
    /// Enforces docs/PAINTED_ROOM_SPEC.md. Opens every painted puzzle room and lists each renderer a player camera can
    /// see (pose-shown pieces included), sorted into what the spec allows and what it forbids. A room passes only with
    /// zero violations. Run: -batchmode -quit -executeMethod Ankhu.Realms.EditorTools.PaintedRoomAudit.AuditWernesCli
    /// Report: art_gen/realms/audit/painted_audit.md
    /// </summary>
    public static class PaintedRoomAudit
    {
        public enum Verdict { Painting, Card, PaintedFx, Cast, Catcher, Violation }

        public struct Row { public string scene, path, mesh, material, shader, why; public Verdict verdict; public bool poseOnly; }

        [MenuItem("Ankhu/Realms/Audit Painted Rooms")]
        public static void AuditMenu() { var rows = AuditWernes(); Debug.Log(Write(rows)); }

        public static void AuditWernesCli()
        {
            try { var rows = AuditWernes(); Debug.Log("[Audit] report " + Write(rows)); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        /// <summary>
        /// Audits one scene by asset path: -executeMethod Ankhu.Realms.EditorTools.PaintedRoomAudit.AuditSceneCli
        /// -auditScene Assets/.../X.unity. Writes art_gen/realms/audit/painted_audit_X.md and exits 1 on any violation.
        /// </summary>
        public static void AuditSceneCli()
        {
            try
            {
                string[] a = System.Environment.GetCommandLineArgs();
                int i = Array.IndexOf(a, "-auditScene");
                if (i < 0 || i + 1 >= a.Length) throw new ArgumentException("-auditScene <path> required");
                string path = a[i + 1];
                string name = Path.GetFileNameWithoutExtension(path);
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var rows = AuditOpenScene(name);
                // Write() owns painted_audit.md/.json (the Wernes report), so keep those and save this one beside them.
                string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../art_gen/realms/audit"));
                string[] shared = { Path.Combine(dir, "painted_audit.md"), Path.Combine(dir, "painted_audit.json") };
                var kept = shared.Select(p => File.Exists(p) ? File.ReadAllBytes(p) : null).ToArray();
                string md = Write(rows);
                string outPath = Path.Combine(dir, "painted_audit_" + name + ".md");
                File.Copy(md, outPath, true);
                for (int k = 0; k < shared.Length; k++)
                    if (kept[k] != null) File.WriteAllBytes(shared[k], kept[k]); else File.Delete(shared[k]);
                int bad = rows.Count(r => r.verdict == Verdict.Violation);
                Debug.Log("[Audit] " + name + " " + bad + " violations of " + rows.Count + " renderers, report " + outPath);
                EditorApplication.Exit(bad == 0 ? 0 : 1);
            }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        public static List<Row> AuditWernes()
        {
            var rows = new List<Row>();
            foreach (var script in WernesPaintedScripts.All())
            {
                string path = RealmKit.SceneDir + "/" + script.SceneName + ".unity";
                if (!File.Exists(path)) continue;
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                rows.AddRange(AuditOpenScene(script.SceneName));
            }
            return rows;
        }

        /// <summary>Classifies every renderer in the open scene that is not on the hidden paint layer.</summary>
        public static List<Row> AuditOpenScene(string scene)
        {
            var rows = new List<Row>();
            foreach (Renderer r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!r.enabled || r.gameObject.layer == RealmSceneBuilder.PaintLayer) continue;
                var row = new Row
                {
                    scene = scene,
                    path = PathOf(r.transform),
                    mesh = r is MeshRenderer ? (r.GetComponent<MeshFilter>()?.sharedMesh?.name ?? "-") : r.GetType().Name,
                    material = r.sharedMaterial != null ? r.sharedMaterial.name : "-",
                    shader = r.sharedMaterial != null && r.sharedMaterial.shader != null ? r.sharedMaterial.shader.name : "-",
                    poseOnly = !r.gameObject.activeInHierarchy,
                };
                row.verdict = Classify(r, out row.why);
                rows.Add(row);
            }
            return rows;
        }

        private static Verdict Classify(Renderer r, out string why)
        {
            Transform t = r.transform;
            if (t.name == "Painting" && t.parent != null && t.parent.GetComponent<Camera>() != null) { why = "backdrop painting"; return Verdict.Painting; }
            if (t.name == "ShadowCatcher") { why = "invisible shadow catcher"; return Verdict.Catcher; }
            for (Transform a = t; a != null; a = a.parent)
                if (a.GetComponent<Animator>() || a.GetComponent<CharacterController>() || a.GetComponent<RealmFollower>())
                { why = "cast (Ankhu, Anpu, followers)"; return Verdict.Cast; }

            Material m = r.sharedMaterial;
            Texture tex = m == null ? null : (m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : null) ?? (m.HasProperty("_MainTex") ? m.GetTexture("_MainTex") : null);
            string sh = m != null && m.shader != null ? m.shader.name : "";
            bool painted = m != null && (sh.StartsWith("Ankhu/Painted") || m.name.StartsWith("card_") || m.name.StartsWith("fx_") || m.name.StartsWith("flip_"));

            if (m == null) { why = "no material"; return Verdict.Violation; }
            if (tex == null && !painted) { why = "untextured flat colour (" + sh + ")"; return Verdict.Violation; }
            if (sh.Contains("/Lit") || sh.Contains("Simple Lit")) { why = "lit 3D surface shader"; return Verdict.Violation; }
            if (!painted && tex != null && !tex.name.StartsWith("fx_") && !AssetDatabase.GetAssetPath(tex).Contains("/Art/")) { why = "texture not from an Art folder: " + tex.name; return Verdict.Violation; }

            bool isCard = false;
            for (Transform a = t; a != null; a = a.parent) if (a.GetComponent<CardFacing>()) { isCard = true; break; }
            if (isCard || m.name.StartsWith("card_")) { why = "painted card"; return Verdict.Card; }
            why = "painted fx or plane"; return Verdict.PaintedFx;
        }

        private static string PathOf(Transform t)
        {
            var parts = new List<string>();
            for (Transform a = t; a != null; a = a.parent) parts.Add(a.name);
            parts.Reverse();
            return string.Join("/", parts);
        }

        private static string Write(List<Row> rows)
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../art_gen/realms/audit"));
            Directory.CreateDirectory(dir);
            var sb = new StringBuilder();
            sb.AppendLine("# Painted room audit");
            sb.AppendLine();
            sb.AppendLine("Generated " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + " by PaintedRoomAudit against docs/PAINTED_ROOM_SPEC.md.");
            sb.AppendLine();
            sb.AppendLine("| Scene | Painting | Card | Painted FX | Cast | Violations | Result |");
            sb.AppendLine("|---|---|---|---|---|---|---|");
            foreach (var g in rows.GroupBy(x => x.scene))
            {
                int v = g.Count(x => x.verdict == Verdict.Violation);
                sb.AppendLine("| " + g.Key + " | " + g.Count(x => x.verdict == Verdict.Painting) + " | " + g.Count(x => x.verdict == Verdict.Card)
                    + " | " + g.Count(x => x.verdict == Verdict.PaintedFx) + " | " + g.Count(x => x.verdict == Verdict.Cast) + " | " + v + " | " + (v == 0 ? "PASS" : "FAIL") + " |");
            }
            foreach (var g in rows.GroupBy(x => x.scene))
            {
                var bad = g.Where(x => x.verdict == Verdict.Violation).ToList();
                if (bad.Count == 0) continue;
                sb.AppendLine();
                sb.AppendLine("## " + g.Key + " violations (" + bad.Count + ")");
                sb.AppendLine();
                foreach (var x in bad.OrderBy(x => x.path))
                    sb.AppendLine("- `" + x.path + "` mesh " + x.mesh + ", material " + x.material + ": " + x.why + (x.poseOnly ? " (shown by a pose)" : ""));
            }
            string md = Path.Combine(dir, "painted_audit.md");
            File.WriteAllText(md, sb.ToString());
            File.WriteAllText(Path.Combine(dir, "painted_audit.json"), JsonUtility.ToJson(new Wrap { rows = rows.Select(x => x.scene + "|" + x.verdict + "|" + x.path + "|" + x.mesh + "|" + x.material + "|" + x.why).ToArray() }, true));
            return md;
        }

        [Serializable] private class Wrap { public string[] rows; }
    }
}
