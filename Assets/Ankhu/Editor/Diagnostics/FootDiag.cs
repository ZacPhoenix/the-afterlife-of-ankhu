using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Ankhu.EditorTools.Diagnostics
{
    /// <summary>
    /// Temporary diagnosis for the bent left foot in the walk. Samples the walk clip on Ankhu and on the
    /// clip's own source skeleton, logs foot angles per frame and renders side and left-foot close-up frames.
    /// Run: -executeMethod Ankhu.EditorTools.Diagnostics.FootDiag.RunCli (outDir via -footOut).
    /// </summary>
    public static class FootDiag
    {
        const string AnkhuPrefab = "Assets/Characters/Ankhu/Model/Ankhu_mixamorig_v3.fbx";
        const string WalkFbx = "Assets/Characters/Ankhu/Animations/03_Walk_InPlace.fbx";
        const int Frames = 12;

        public static void RunCli()
        {
            string outDir = "D:/TheAfterlifeofAnkhu/docs/captures/foot_fix/before";
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-footOut") outDir = args[i + 1];
            Directory.CreateDirectory(outDir);

            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(WalkFbx).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview"));
            var report = new StringBuilder();
            report.AppendLine("clip " + clip.name + " length " + clip.length + " humanMotion " + clip.humanMotion);

            AnimationMode.StartAnimationMode();
            try
            {
                var ankhu = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(AnkhuPrefab));
                ankhu.transform.position = Vector3.zero;
                Animator aa = ankhu.GetComponentInChildren<Animator>();
                if (aa.runtimeAnimatorController != null) aa.runtimeAnimatorController = null;

                var src = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(WalkFbx));
                src.transform.position = new Vector3(0f, 0f, 3f);
                Animator sa = src.GetComponentInChildren<Animator>();
                report.AppendLine("source has renderer: " + (src.GetComponentInChildren<Renderer>() != null) + ", avatar human: " + (sa != null && sa.avatar != null && sa.avatar.isHuman));

                // Bind pose (no clip applied).
                CaptureBind(aa); report.AppendLine("BIND ankhu " + FootLine(aa));
                if (sa != null && sa.isHuman) { CaptureBind(sa); report.AppendLine("BIND source " + FootLine(sa)); }

                var camGo = new GameObject("diagCam");
                Camera cam = camGo.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.55f, 0.55f, 0.58f);
                cam.fieldOfView = 30f;
                var lightGo = new GameObject("diagLight");
                Light light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.6f;
                lightGo.transform.rotation = Quaternion.Euler(40f, -60f, 0f);
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.5f, 0.5f, 0.5f);
                var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
                floor.transform.localScale = new Vector3(2f, 1f, 2f);

                var rt = new RenderTexture(960, 960, 24);
                cam.targetTexture = rt;
                var tex = new Texture2D(960, 960, TextureFormat.RGB24, false);

                for (int f = 0; f < Frames; f++)
                {
                    float t = clip.length * f / Frames;
                    // AnimationMode reverts the previous sample at BeginSampling, so measure each figure right after its own sample.
                    if (sa != null && sa.isHuman)
                    {
                        Sample(sa.gameObject, clip, t);
                        report.AppendLine(string.Format("f{0:00} t{1:0.000} source {2}", f, t, FootLine(sa)));
                    }
                    Sample(aa.gameObject, clip, t);
                    report.AppendLine(string.Format("f{0:00} t{1:0.000} ankhu {2}", f, t, FootLine(aa)));

                    src.SetActive(false);
                    Vector3 hipL = aa.GetBoneTransform(HumanBodyBones.LeftUpperLeg).position, hipR = aa.GetBoneTransform(HumanBodyBones.RightUpperLeg).position;
                    Vector3 leftDir = Vector3.ProjectOnPlane(hipL - hipR, Vector3.up).normalized;
                    Vector3 fwd = Vector3.Cross(leftDir, Vector3.up).normalized * -1f;
                    // Full side view from Ankhu's left.
                    Shoot(cam, rt, tex, leftDir * 4.2f + Vector3.up * 1.0f, new Vector3(0f, 0.9f, 0f), 30f, Path.Combine(outDir, string.Format("side_{0:00}.png", f)));
                    Vector3 fp = aa.GetBoneTransform(HumanBodyBones.LeftFoot).position;
                    Shoot(cam, rt, tex, fp + leftDir * 1.1f + Vector3.up * 0.12f, fp + Vector3.down * 0.04f + fwd * 0.06f, 30f, Path.Combine(outDir, string.Format("lfoot_side_{0:00}.png", f)));
                    Shoot(cam, rt, tex, fp + fwd * 1.1f + leftDir * 0.15f + Vector3.up * 0.2f, fp + Vector3.down * 0.04f, 30f, Path.Combine(outDir, string.Format("lfoot_front_{0:00}.png", f)));
                    src.SetActive(true);
                }
                Object.DestroyImmediate(rt);
            }
            finally
            {
                AnimationMode.StopAnimationMode();
            }
            File.WriteAllText(Path.Combine(outDir, "foot_angles.txt"), report.ToString());
            Debug.Log("[FootDiag] done " + outDir);
        }

        /// <summary>Real Animator evaluation of the walk on Ankhu, Foot IK off then on, into before/after dirs.</summary>
        public static void RunAnimCli()
        {
            string root = "D:/TheAfterlifeofAnkhu/docs/captures/foot_fix";
            AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(WalkFbx).OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview"));
            foreach (bool ik in new[] { false, true })
            {
                string outDir = Path.Combine(root, ik ? "after" : "before");
                Directory.CreateDirectory(outDir);
                string ctrlPath = "Assets/_footdiag_tmp_" + (ik ? "ik" : "noik") + ".controller";
                var ctrl = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
                var st = ctrl.layers[0].stateMachine.AddState("Walk");
                st.motion = clip;
                st.iKOnFeet = ik;
                AssetDatabase.SaveAssets();

                var report = new StringBuilder();
                report.AppendLine("Animator evaluation, iKOnFeet=" + ik + ", clip " + clip.name + " len " + clip.length);
                var ankhu = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(AnkhuPrefab));
                Animator aa = ankhu.GetComponentInChildren<Animator>();
                aa.runtimeAnimatorController = ctrl;
                aa.applyRootMotion = false;
                aa.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                CaptureBind(aa);
                aa.Rebind();
                aa.Update(0f);

                var camGo = new GameObject("diagCam");
                Camera cam = camGo.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.55f, 0.55f, 0.58f);
                var lightGo = new GameObject("diagLight");
                Light light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.6f;
                lightGo.transform.rotation = Quaternion.Euler(40f, -60f, 0f);
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.5f, 0.5f, 0.5f);
                var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
                floor.transform.localScale = new Vector3(2f, 1f, 2f);
                var rt = new RenderTexture(960, 960, 24);
                cam.targetTexture = rt;
                var tex = new Texture2D(960, 960, TextureFormat.RGB24, false);

                float dt = clip.length / Frames;
                for (int f = 0; f < Frames; f++)
                {
                    if (f > 0) aa.Update(dt);
                    report.AppendLine(string.Format("f{0:00} t{1:0.000} ankhu {2}", f, dt * f, FootLine(aa)));
                    Vector3 hipL = aa.GetBoneTransform(HumanBodyBones.LeftUpperLeg).position, hipR = aa.GetBoneTransform(HumanBodyBones.RightUpperLeg).position;
                    Vector3 leftDir = Vector3.ProjectOnPlane(hipL - hipR, Vector3.up).normalized;
                    Vector3 fwd = Vector3.Cross(leftDir, Vector3.up).normalized * -1f;
                    Shoot(cam, rt, tex, leftDir * 4.2f + Vector3.up * 1.0f, new Vector3(0f, 0.9f, 0f), 30f, Path.Combine(outDir, string.Format("anim_side_{0:00}.png", f)));
                    Vector3 fp = aa.GetBoneTransform(HumanBodyBones.LeftFoot).position;
                    Shoot(cam, rt, tex, fp + leftDir * 1.1f + Vector3.up * 0.12f, fp + Vector3.down * 0.04f + fwd * 0.06f, 30f, Path.Combine(outDir, string.Format("anim_lfoot_side_{0:00}.png", f)));
                    Shoot(cam, rt, tex, fp + fwd * 1.1f + leftDir * 0.15f + Vector3.up * 0.2f, fp + Vector3.down * 0.04f, 30f, Path.Combine(outDir, string.Format("anim_lfoot_front_{0:00}.png", f)));
                }
                File.WriteAllText(Path.Combine(outDir, "anim_foot_angles.txt"), report.ToString());
                Object.DestroyImmediate(ankhu); Object.DestroyImmediate(camGo); Object.DestroyImmediate(lightGo); Object.DestroyImmediate(floor); Object.DestroyImmediate(rt);
                AssetDatabase.DeleteAsset(ctrlPath);
            }
            Debug.Log("[FootDiag] anim done");
        }

        /// <summary>Evaluates the shipped controllers (Ankhu, Anpu escort) at walk speed and logs foot angles.</summary>
        public static void RunRealCli()
        {
            string root = "D:/TheAfterlifeofAnkhu/docs/captures/foot_fix";
            var clampType = System.Type.GetType("Ankhu.Opening.FootFloorClamp, Ankhu.Opening");
            var report = new StringBuilder();
            report.AppendLine("clamp type found: " + (clampType != null));
            var sets = new[] {
                (AnkhuPrefab, "Assets/Characters/Ankhu/Ankhu.controller", true),
                ("Assets/Characters/Anpu/Anpu.prefab", "Assets/Ankhu/Opening/Generated/Anpu_Escort.controller", false) };
            var camGo = new GameObject("diagCam");
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.55f, 0.55f, 0.58f);
            var lightGo = new GameObject("diagLight");
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.6f;
            lightGo.transform.rotation = Quaternion.Euler(40f, -60f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.5f, 0.5f, 0.5f);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.localScale = new Vector3(2f, 1f, 2f);
            var rt = new RenderTexture(960, 960, 24);
            cam.targetTexture = rt;
            var tex = new Texture2D(960, 960, TextureFormat.RGB24, false);
            foreach (bool clamp in new[] { false, true })
            foreach (var (model, ctrlPath, render) in sets)
            {
                var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ctrlPath);
                var ac = ctrl as UnityEditor.Animations.AnimatorController;
                var loco = ac.layers[0].stateMachine.states.Select(s => s.state).First(s => s.name == "Locomotion");
                report.AppendLine(ctrlPath + " Locomotion iKOnFeet=" + loco.iKOnFeet + " ikPass=" + ac.layers[0].iKPass + " clamp=" + clamp);
                var go = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(model));
                foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(mb);
                Animator a = go.GetComponentInChildren<Animator>();
                if (clamp && clampType != null) a.gameObject.AddComponent(clampType);
                a.runtimeAnimatorController = ctrl;
                a.applyRootMotion = false;
                a.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                CaptureBind(a);
                a.Rebind();
                a.SetFloat("Speed", 1.4f);
                a.Update(0.5f);
                for (int f = 0; f < Frames; f++)
                {
                    a.Update(1.2333f / Frames);
                    report.AppendLine(string.Format("f{0:00} {1} {2}", f, FootLine(a), SoleMin(go, a)));
                    if (!(render && clamp)) continue;
                    string outDir = root + "/after";
                    Vector3 hipL = a.GetBoneTransform(HumanBodyBones.LeftUpperLeg).position, hipR = a.GetBoneTransform(HumanBodyBones.RightUpperLeg).position;
                    Vector3 leftDir = Vector3.ProjectOnPlane(hipL - hipR, Vector3.up).normalized;
                    Vector3 fwd = Vector3.Cross(leftDir, Vector3.up).normalized * -1f;
                    Shoot(cam, rt, tex, leftDir * 4.2f + Vector3.up * 1.0f, new Vector3(0f, 0.9f, 0f), 30f, Path.Combine(outDir, string.Format("final_side_{0:00}.png", f)));
                    Vector3 fp = a.GetBoneTransform(HumanBodyBones.LeftFoot).position;
                    Shoot(cam, rt, tex, fp + leftDir * 1.1f + Vector3.up * 0.12f, fp + Vector3.down * 0.04f + fwd * 0.06f, 30f, Path.Combine(outDir, string.Format("final_lfoot_side_{0:00}.png", f)));
                    Shoot(cam, rt, tex, fp + fwd * 1.1f + leftDir * 0.15f + Vector3.up * 0.2f, fp + Vector3.down * 0.04f, 30f, Path.Combine(outDir, string.Format("final_lfoot_front_{0:00}.png", f)));
                }
                Object.DestroyImmediate(go);
            }
            File.WriteAllText(root + "/after/real_controllers.txt", report.ToString());
            Debug.Log("[FootDiag] real done");
        }

        /// <summary>
        /// Zac 2026-11-25: one of the in-game player's feet does not go flat in the walk. Evaluates the shipped player
        /// (Characters/Ankhu/Ankhu.prefab with Ankhu.controller) at walk speed in four variants (Foot IK on or off,
        /// floor clamp on or off), logs per-frame foot pitch, roll and sole height, and renders side-on frames of
        /// each foot from its own side. Out dir via -footOut.
        /// </summary>
        public static void RunPlayerCli()
        {
            string root = "D:/TheAfterlifeofAnkhu/docs/captures/foot_player";
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-footOut") root = args[i + 1];
            Directory.CreateDirectory(root);
            var clampType = System.Type.GetType("Ankhu.Opening.FootFloorClamp, Ankhu.Opening");
            var report = new StringBuilder();
            var camGo = new GameObject("diagCam");
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.55f, 0.55f, 0.58f);
            cam.nearClipPlane = 0.02f;
            var lightGo = new GameObject("diagLight");
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.6f;
            lightGo.transform.rotation = Quaternion.Euler(40f, -60f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.5f, 0.5f, 0.5f);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.transform.localScale = new Vector3(2f, 1f, 2f);
            var rt = new RenderTexture(720, 720, 24);
            cam.targetTexture = rt;
            var tex = new Texture2D(720, 720, TextureFormat.RGB24, false);
            var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Characters/Ankhu/Ankhu.controller");
            var ac = ctrl as UnityEditor.Animations.AnimatorController;
            var loco = ac.layers[0].stateMachine.states.Select(s => s.state).First(s => s.name == "Locomotion");
            bool ikWas = loco.iKOnFeet;
            report.AppendLine("Locomotion iKOnFeet=" + ikWas + " ikPass=" + ac.layers[0].iKPass + " clamp type " + (clampType != null));
            string only = null;
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-footVariant") only = args[i + 1];
            try
            {
                foreach (bool ik in new[] { true, false })
                foreach (bool clamp in new[] { false, true })
                {
                    string tag = (ik ? "ikOn" : "ikOff") + (clamp ? "_clamp" : "");
                    if (only != null && only != tag) continue;
                    loco.iKOnFeet = ik;
                    report.AppendLine("== " + tag);
                    var go = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Characters/Ankhu/Ankhu.prefab"));
                    go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                    foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(mb);
                    Animator a = go.GetComponentInChildren<Animator>();
                    if (clamp && clampType != null) a.gameObject.AddComponent(clampType);
                    a.runtimeAnimatorController = ctrl;
                    a.applyRootMotion = false;
                    a.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    soleLocal.Clear();
                    CaptureBind(a);
                    a.Rebind();
                    a.SetFloat("Speed", 1.4f);
                    a.Update(0.5f);
                    string dir = Path.Combine(root, tag);
                    Directory.CreateDirectory(dir);
                    for (int f = 0; f < 16; f++)
                    {
                        a.Update(1.0f / 16f / 1.0f);
                        report.AppendLine(string.Format("f{0:00} {1} {2}", f, FootLine(a), SoleMin(go, a)));
                        Vector3 hipL = a.GetBoneTransform(HumanBodyBones.LeftUpperLeg).position, hipR = a.GetBoneTransform(HumanBodyBones.RightUpperLeg).position;
                        Vector3 leftDir = Vector3.ProjectOnPlane(hipL - hipR, Vector3.up).normalized;
                        Vector3 fwd = -Vector3.Cross(leftDir, Vector3.up).normalized;
                        Vector3 lp = a.GetBoneTransform(HumanBodyBones.LeftFoot).position, rp = a.GetBoneTransform(HumanBodyBones.RightFoot).position;
                        Shoot(cam, rt, tex, lp + leftDir * 1.2f + Vector3.up * 0.05f, new Vector3(lp.x, 0.08f, lp.z) + fwd * 0.05f, 22f, Path.Combine(dir, string.Format("L_{0:00}.png", f)));
                        Shoot(cam, rt, tex, rp - leftDir * 1.2f + Vector3.up * 0.05f, new Vector3(rp.x, 0.08f, rp.z) + fwd * 0.05f, 22f, Path.Combine(dir, string.Format("R_{0:00}.png", f)));
                    }
                    Object.DestroyImmediate(go);
                }
            }
            finally { loco.iKOnFeet = ikWas; }
            File.WriteAllText(Path.Combine(root, "player_feet.txt"), report.ToString());
            Debug.Log("[FootDiag] player done");
        }

        public static void RunGoalsCli()
        {
            var sb = new StringBuilder();
            foreach (var path in new[] { WalkFbx })
            foreach (var clip in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview")))
            {
                sb.AppendLine(path + " " + clip.name);
                foreach (var b in AnimationUtility.GetCurveBindings(clip))
                {
                    if (!(b.propertyName.StartsWith("LeftFoot") || b.propertyName.StartsWith("RightFoot") || b.propertyName.StartsWith("RootT"))) continue;
                    var c = AnimationUtility.GetEditorCurve(clip, b);
                    float mn = 9, mx = -9; var vals = new StringBuilder();
                    for (int i = 0; i < 12; i++) { float v = c.Evaluate(clip.length * i / 12f); mn = Mathf.Min(mn, v); mx = Mathf.Max(mx, v); vals.Append(v.ToString("0.000")).Append(' '); }
                    sb.AppendLine(string.Format("  {0,-14} min {1:0.000} max {2:0.000} | {3}", b.propertyName, mn, mx, vals));
                }
            }
            File.WriteAllText("D:/TheAfterlifeofAnkhu/docs/captures/foot_fix/clip_goals.txt", sb.ToString());
        }

        /// <summary>Lowest baked skin vertex near each foot, in metres above the floor.</summary>
        static string SoleMin(GameObject go, Animator a)
        {
            Vector3 lf = a.GetBoneTransform(HumanBodyBones.LeftFoot).position, rf = a.GetBoneTransform(HumanBodyBones.RightFoot).position;
            float lmin = 9f, rmin = 9f;
            var mesh = new Mesh();
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                smr.BakeMesh(mesh, true);
                var m = smr.transform.localToWorldMatrix;
                foreach (var v0 in mesh.vertices)
                {
                    Vector3 v = m.MultiplyPoint3x4(v0);
                    if ((v - lf).sqrMagnitude < 0.06f) lmin = Mathf.Min(lmin, v.y);
                    if ((v - rf).sqrMagnitude < 0.06f) rmin = Mathf.Min(rmin, v.y);
                }
            }
            Object.DestroyImmediate(mesh);
            return string.Format("soleL{0:0.000} soleR{1:0.000}", lmin, rmin);
        }

        static readonly System.Collections.Generic.Dictionary<Transform, Vector3> soleLocal = new System.Collections.Generic.Dictionary<Transform, Vector3>();
        static void CaptureBind(Animator a)
        {
            foreach (var b in new[] { HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot })
            {
                Transform t = a.GetBoneTransform(b);
                soleLocal[t] = Quaternion.Inverse(t.rotation) * Vector3.up;
            }
        }

        static void Sample(GameObject go, AnimationClip clip, float t)
        {
            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(go, clip, t);
            AnimationMode.EndSampling();
        }

        static void Shoot(Camera cam, RenderTexture rt, Texture2D tex, Vector3 pos, Vector3 look, float fov, string path)
        {
            cam.transform.position = pos;
            cam.transform.LookAt(look);
            cam.fieldOfView = fov;
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(path, tex.EncodeToPNG());
        }

        /// <summary>Per foot: toe-out yaw (positive = toes outward), pitch of foot-to-toe, ankle flex angle
        /// (shin vs foot), and roll (how far the toe-to-heel lateral axis tilts from level).</summary>
        static string FootLine(Animator a)
        {
            Transform root = a.transform;
            string L = One(a, root, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, HumanBodyBones.LeftToes, -1f);
            string R = One(a, root, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, HumanBodyBones.RightToes, 1f);
            Quaternion lq = a.GetBoneTransform(HumanBodyBones.LeftFoot).localRotation;
            Quaternion rq = a.GetBoneTransform(HumanBodyBones.RightFoot).localRotation;
            Quaternion lt = a.GetBoneTransform(HumanBodyBones.LeftToes).localRotation;
            Quaternion rt = a.GetBoneTransform(HumanBodyBones.RightToes).localRotation;
            return "L[" + L + "] R[" + R + "] Lloc" + lq.eulerAngles.ToString("F0") + " Rloc" + rq.eulerAngles.ToString("F0") + " LtoeLoc" + lt.eulerAngles.ToString("F0") + " RtoeLoc" + rt.eulerAngles.ToString("F0");
        }

        static string One(Animator a, Transform root, HumanBodyBones shinB, HumanBodyBones footB, HumanBodyBones toeB, float side)
        {
            Transform shin = a.GetBoneTransform(shinB), foot = a.GetBoneTransform(footB), toe = a.GetBoneTransform(toeB);
            if (shin == null || foot == null || toe == null) return "missing";
            Vector3 f2t = toe.position - foot.position;
            Vector3 flat = Vector3.ProjectOnPlane(f2t, Vector3.up);
            float yaw = Vector3.SignedAngle(root.forward, flat, Vector3.up) * side;
            float pitch = Mathf.Asin(Mathf.Clamp(f2t.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
            Vector3 shinDir = foot.position - shin.position;
            float flex = Vector3.Angle(shinDir, f2t);
            // Roll: foot lateral axis = cross(shin, foot-to-toe), tilt of that from horizontal.
            Vector3 sole = foot.rotation * (soleLocal.TryGetValue(foot, out Vector3 sl) ? sl : Vector3.up);
            Vector3 right = a.transform.right;
            float roll = Mathf.Asin(Mathf.Clamp(Vector3.Dot(sole.normalized, right), -1f, 1f)) * Mathf.Rad2Deg * side;
            float toeBend = Vector3.Angle(f2t, toe.childCount > 0 ? toe.GetChild(0).position - toe.position : f2t);
            return string.Format("yaw{0:0} pitch{1:0} flex{2:0} roll{3:0} toeBend{4:0} footY{5:0.000}", yaw, pitch, flex, roll, toeBend, foot.position.y);
        }
    }
}
