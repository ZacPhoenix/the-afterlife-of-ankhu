using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Ankhu.Opening;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ankhu.EditorTools.Character
{
    /// <summary>
    /// Zac 2026-11-25: Anpu's was-sceptre stood upside down, the forked foot up and the animal head on the floor.
    /// The AccuRig setup guessed the foot from which end sits nearer the mesh origin, and this mesh is the other way.
    /// This tool decides by shape instead: the head end is the one whose centre sits off the shaft line (the
    /// Set head leans out), the forked foot is centred on it. Wherever the head is the low end, it turns the staff
    /// 180 degrees about the grip, faces the head forward, slides it so the foot sits where the old low end sat,
    /// and re-measures PropUpright. Idempotent: an already upright staff is left alone.
    /// Runs over both Anpu prefabs and every scene under Assets that holds Socket_Prop_WasSceptre.
    ///   Unity -batchmode -quit -projectPath D:\TheAfterlifeofAnkhu -executeMethod Ankhu.EditorTools.Character.AnpuStaffFlip.FlipCli -logFile staff.log
    /// </summary>
    public static class AnpuStaffFlip
    {
        public const string SocketName = "Socket_Prop_WasSceptre";
        public const string PropName = "Prop_WasSceptre";
        private static readonly string[] Prefabs =
        {
            "Assets/Characters/Anpu/Anpu.prefab",
            "Assets/Characters/AccuRig/Anpu/Anpu_AccuRig.prefab",
        };

        private static readonly StringBuilder Report = new StringBuilder();

        public static void FlipCli()
        {
            int code = 1;
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Unity is in Play mode.");
                Report.Clear();
                foreach (string p in Prefabs)
                {
                    if (!File.Exists(p)) continue;
                    GameObject root = PrefabUtility.LoadPrefabContents(p);
                    try
                    {
                        if (FixAll(root, p)) PrefabUtility.SaveAsPrefabAsset(root, p);
                    }
                    finally { PrefabUtility.UnloadPrefabContents(root); }
                }
                foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!File.ReadAllText(path).Contains(SocketName)) continue;
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    bool changed = false;
                    foreach (GameObject g in scene.GetRootGameObjects()) changed |= FixAll(g, path);
                    if (changed) EditorSceneManager.SaveScene(scene);
                }
                AssetDatabase.SaveAssets();
                string outDir = "ArtSource/AccuRig/checks";
                Directory.CreateDirectory(outDir);
                File.WriteAllText(Path.Combine(outDir, "staff_flip.txt"), Report.ToString());
                Debug.Log("[StaffFlip]\n" + Report);
                RenderCheck();
                code = 0;
            }
            catch (System.Exception e) { Debug.LogException(e); }
            EditorApplication.Exit(code);
        }

        private static bool FixAll(GameObject root, string where)
        {
            bool changed = false;
            foreach (Transform socket in root.GetComponentsInChildren<Transform>(true).Where(t => t.name == SocketName).ToArray())
            {
                Transform prop = socket.Cast<Transform>().FirstOrDefault(t => t.name == PropName);
                if (prop == null) { Report.AppendLine(where + ": socket without " + PropName); continue; }
                if (!Fix(socket, prop, where)) continue;
                changed = true;
                PropUpright pu = root.GetComponentInChildren<PropUpright>(true);
                if (pu != null) Remeasure(pu, where);
            }
            return changed;
        }

        private static Vector3[] SocketVerts(Transform socket, Transform prop)
        {
            return prop.GetComponentsInChildren<MeshFilter>(true).Where(m => m.sharedMesh != null)
                .SelectMany(m => m.sharedMesh.vertices.Select(v => socket.InverseTransformPoint(m.transform.TransformPoint(v)))).ToArray();
        }

        private static Vector3 Axis(int i) { Vector3 d = Vector3.zero; d[i] = 1f; return d; }

        /// <summary>
        /// Centroid offsets from the shaft line of the up end and the down end (top and bottom 8 percent along up).
        /// </summary>
        private static void Ends(Vector3[] v, Vector3 up, out Vector3 upEnd, out Vector3 downEnd, out float lo, out float hi, out Vector3 line)
        {
            lo = v.Min(p => Vector3.Dot(p, up)); hi = v.Max(p => Vector3.Dot(p, up));
            float len = hi - lo, l = lo, h = hi;
            Vector3 C(IEnumerable<Vector3> s) { var a = s.ToArray(); return a.Length == 0 ? Vector3.zero : a.Aggregate(Vector3.zero, (x, y) => x + y) / a.Length; }
            Vector3 Off(Vector3 c) => Vector3.ProjectOnPlane(c, up);
            line = Off(C(v.Where(p => { float d = Vector3.Dot(p, up); return d > l + 0.4f * len && d < l + 0.6f * len; })));
            upEnd = Off(C(v.Where(p => Vector3.Dot(p, up) > h - 0.08f * len))) - line;
            downEnd = Off(C(v.Where(p => Vector3.Dot(p, up) < l + 0.08f * len))) - line;
        }

        private static bool Fix(Transform socket, Transform prop, string where)
        {
            // The setup built the socket so that, in the idle hold, the socket-space direction -downLocal is world up,
            // where downLocal came from the mesh in the prop's own space with the old DownIsShorterEnd = true rule.
            // That direction is fixed in socket space, so recomputing it here keeps the tool idempotent.
            MeshFilter mf = prop.GetComponentInChildren<MeshFilter>(true);
            if (mf == null || mf.sharedMesh == null) return false;
            Vector3[] pv = mf.sharedMesh.vertices.Select(x => prop.InverseTransformPoint(mf.transform.TransformPoint(x))).ToArray();
            Vector3 min = pv.Aggregate(Vector3.positiveInfinity, Vector3.Min), max = pv.Aggregate(Vector3.negativeInfinity, Vector3.Max);
            Vector3 ext = max - min;
            int ax = ext.x >= ext.y && ext.x >= ext.z ? 0 : ext.y >= ext.z ? 1 : 2;
            bool positiveShorter = max[ax] < -min[ax];
            Vector3 downOld = positiveShorter ? Axis(ax) : -Axis(ax);
            Vector3 up = -downOld;
            // The old facing: the up end's offset off the shaft (the setup took that as the head) went to world forward.
            Ends(pv, up, out Vector3 oldFace, out _, out _, out _, out _);
            Vector3 forward = oldFace.sqrMagnitude > 1e-8f ? oldFace.normalized : Axis((ax + 1) % 3);
            if (Quaternion.Angle(prop.localRotation, Quaternion.identity) > 0.5f && Quaternion.Angle(prop.localRotation, Quaternion.identity) < 179.5f)
                Report.AppendLine(where + ": note, prop local rotation is " + prop.localEulerAngles.ToString("F1") + ".");

            Vector3[] v = SocketVerts(socket, prop);
            Ends(v, up, out Vector3 top, out Vector3 bottom, out float lo, out float hi, out Vector3 line);
            Report.AppendLine(string.Format("{0}: up in socket space {1}. Before, top end off the shaft {2:0.000} m, bottom end {3:0.000} m, ends {4:0.000} to {5:0.000}.",
                where, up.ToString("F2"), top.magnitude, bottom.magnitude, lo, hi));
            if (top.magnitude >= bottom.magnitude)
            {
                Report.AppendLine(where + ": head already up, left alone.");
                return false;
            }
            // Turn 180 degrees about an axis across the shaft, through its middle, so the ends swap and the extent stays put.
            Vector3 across = Vector3.Cross(up, forward).normalized;
            Vector3 pivot = line + up * ((lo + hi) * 0.5f);
            prop.RotateAround(socket.TransformPoint(pivot), socket.TransformDirection(across), 180f);
            // Turn about the shaft so the head leans forward.
            v = SocketVerts(socket, prop);
            Ends(v, up, out top, out _, out _, out _, out line);
            float yaw = Vector3.SignedAngle(top, forward, up);
            prop.RotateAround(socket.TransformPoint(line), socket.TransformDirection(up), yaw);
            // Slide along the shaft so the foot sits where the old low end sat (the floor in the hold pose).
            v = SocketVerts(socket, prop);
            float lo2 = v.Min(p => Vector3.Dot(p, up));
            prop.position += socket.TransformVector(up * (lo - lo2));
            v = SocketVerts(socket, prop);
            Ends(v, up, out top, out bottom, out float lo3, out float hi3, out _);
            Report.AppendLine(string.Format("{0}: flipped. Head off the shaft {1:0.000} m, {2:0} deg from forward, foot {3:0.000} m, ends {4:0.000} to {5:0.000}.",
                where, top.magnitude, Vector3.Angle(top, forward), bottom.magnitude, lo3, hi3));
            EditorUtility.SetDirty(prop);
            return true;
        }

        // Re-measure PropUpright in the socket's space: foot to head along the shaft, and the foot point.
        private static void Remeasure(PropUpright pu, string where)
        {
            Transform socket = pu.FindSocket();
            Transform staff = socket == null ? null : socket.Cast<Transform>().FirstOrDefault(t => t.name == pu.RendererName);
            if (socket == null || staff == null) return;
            Vector3[] v = SocketVerts(socket, staff);
            if (v.Length < 2) return;
            Vector3 axis = pu.LocalAxis.sqrMagnitude > 0.5f ? pu.LocalAxis.normalized : Vector3.up;
            Ends(v, axis, out _, out _, out float lo, out _, out Vector3 line);
            Vector3 foot = line + axis * lo;
            Report.AppendLine(string.Format("{0}: PropUpright axis kept {1}, foot {2} -> {3}.", where, axis.ToString("F3"), pu.LocalFoot.ToString("F3"), foot.ToString("F3")));
            pu.SetFoot(foot);
            EditorUtility.SetDirty(pu);
        }

        /// <summary>Renders the in-game Anpu prefab idle and walking side-on through the AccuRig setup's own checks.</summary>
        private static void RenderCheck()
        {
            var t = typeof(AccuRigCharacterSetup);
            var walk = t.GetMethod("WalkCheck", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var hand = t.GetMethod("HandCheck", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            GameObject anpu = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs[0]);
            var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Ankhu/Opening/Generated/Anpu_Escort.controller")
                       ?? anpu.GetComponent<Animator>().runtimeAnimatorController;
            walk?.Invoke(null, new object[] { "Anpu staff after", anpu, ctrl });
            hand?.Invoke(null, new object[] { anpu });
        }
    }
}
