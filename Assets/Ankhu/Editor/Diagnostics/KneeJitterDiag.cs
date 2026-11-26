using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ankhu.EditorTools.Diagnostics
{
    /// <summary>
    /// Zac 2026-11-26: the guardians' and Ankhu's knees popped in the opening, even standing still. Opens the opening
    /// scenes, steps every humanoid animator at 60 fps, standing and walking, and measures how much each knee's bend
    /// jerks from one frame to the next (the second difference of the knee angle, in degrees). Three variants: the
    /// floor clamp as it was (switching IK on and off), as it is now (always on), and no clamp at all.
    ///   Unity -batchmode -projectPath . -executeMethod Ankhu.EditorTools.Diagnostics.KneeJitterDiag.RunCli -logFile knee.log
    /// Writes art_gen/realms/audit/knee_jitter.md.
    /// </summary>
    public static class KneeJitterDiag
    {
        private static readonly string[] Scenes =
        {
            "Assets/Ankhu/Generated/Scenes/Opening/Opening_Cinematic.unity",
            "Assets/Ankhu/Generated/Scenes/Opening/Opening_WalkDuat.unity",
        };

        public static void RunCli()
        {
            int code = 0;
            try { Run(); }
            catch (System.Exception e) { Debug.LogException(e); code = 1; }
            EditorApplication.Exit(code);
        }

        public static void Run()
        {
            var clampType = System.Type.GetType("Ankhu.Opening.FootFloorClamp, Ankhu.Opening");
            var legacy = clampType?.GetField("Legacy");
            var sb = new StringBuilder("# Knee jitter check\n\n");
            sb.Append("Max frame-to-frame jerk of each knee's bend (degrees, second difference at 60 fps) over 10 s, and how many frames jerk more than 1 degree. Old = clamp switching IK on and off, new = clamp always on, none = no clamp.\n\n");
            sb.Append("| scene | figure | speed | old max | old pops | new max | new pops | none max | none pops |\n|---|---|---|---|---|---|---|---|---|\n");
            foreach (string scene in Scenes)
            {
                if (!File.Exists(scene)) continue;
                EditorSceneManager.OpenScene(scene, OpenSceneMode.Single);
                var animators = Object.FindObjectsByType<Animator>(FindObjectsSortMode.None).Where(a => a.isHuman && a.runtimeAnimatorController != null).ToList();
                foreach (Animator a in animators)
                foreach (float speed in new[] { 0f, 1.35f })
                {
                    var row = new StringBuilder("| " + Path.GetFileNameWithoutExtension(scene) + " | " + a.name + " | " + speed + " |");
                    foreach (int variant in new[] { 0, 1, 2 })
                    {
                        Behaviour clamp = clampType != null ? a.GetComponent(clampType) as Behaviour : null;
                        bool had = clamp != null && clamp.enabled;
                        if (legacy != null) legacy.SetValue(null, variant == 0);
                        if (clamp != null) clamp.enabled = variant != 2;
                        Measure(a, speed, out float max, out int pops);
                        if (clamp != null) clamp.enabled = had;
                        row.Append(" " + max.ToString("F2") + " | " + pops + " |");
                    }
                    sb.Append(row).Append("\n");
                }
            }
            if (legacy != null) legacy.SetValue(null, false);
            Directory.CreateDirectory("art_gen/realms/audit");
            File.WriteAllText("art_gen/realms/audit/knee_jitter.md", sb.ToString());
            Debug.Log("[KneeJitter]\n" + sb);
        }

        private static void Measure(Animator a, float speed, out float max, out int pops)
        {
            a.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            a.Rebind();
            if (a.parameters.Any(p => p.name == "Speed")) a.SetFloat("Speed", speed);
            a.Update(0f);
            for (int i = 0; i < 60; i++) a.Update(1f / 60f);
            Transform[] up = { a.GetBoneTransform(HumanBodyBones.LeftUpperLeg), a.GetBoneTransform(HumanBodyBones.RightUpperLeg) };
            Transform[] lo = { a.GetBoneTransform(HumanBodyBones.LeftLowerLeg), a.GetBoneTransform(HumanBodyBones.RightLowerLeg) };
            Transform[] ft = { a.GetBoneTransform(HumanBodyBones.LeftFoot), a.GetBoneTransform(HumanBodyBones.RightFoot) };
            max = 0f; pops = 0;
            var prev = new float[2, 2];
            for (int f = 0; f < 600; f++)
            {
                a.Update(1f / 60f);
                for (int l = 0; l < 2; l++)
                {
                    if (up[l] == null || lo[l] == null || ft[l] == null) continue;
                    float k = Vector3.Angle(lo[l].position - up[l].position, ft[l].position - lo[l].position);
                    if (f >= 2)
                    {
                        float jerk = Mathf.Abs(k - 2f * prev[l, 1] + prev[l, 0]);
                        max = Mathf.Max(max, jerk);
                        if (jerk > 1f) pops++;
                    }
                    prev[l, 0] = prev[l, 1];
                    prev[l, 1] = k;
                }
            }
        }
    }
}
