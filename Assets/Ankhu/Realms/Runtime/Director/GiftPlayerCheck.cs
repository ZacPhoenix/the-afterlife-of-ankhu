using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;

namespace Ankhu.Realms
{
    /// <summary>
    /// Built-player check that the First Gate gift (CodexGift) plays and that the ambient components
    /// (FlameFlicker, WaterMotion, Bob, WorkerLoop, Sway) survive into a build. Off unless the player is started
    /// with -ankhuGiftCheck &lt;folder&gt;. Muted. Lets the scene's Start beat run, watches the CodexGift object,
    /// renders the active camera into three PNGs while it moves, writes gift_check.txt with PASS or FAIL, and quits.
    /// </summary>
    public sealed class GiftPlayerCheck : MonoBehaviour
    {
        private static string dir;
        private readonly StringBuilder log = new StringBuilder();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-ankhuGiftCheck") dir = args[i + 1];
            }
            if (string.IsNullOrEmpty(dir) || FindAnyObjectByType<GiftPlayerCheck>() != null) return;
            dir = Path.GetFullPath(dir);
            Directory.CreateDirectory(dir);
            Application.runInBackground = true;
            AudioListener.volume = 0f;
            var go = new GameObject("GiftPlayerCheck");
            DontDestroyOnLoad(go);
            go.AddComponent<GiftPlayerCheck>();
        }

        private void Log(string line)
        {
            line = $"[{Time.realtimeSinceStartup:F2}s] {line}";
            log.AppendLine(line);
            Debug.Log("[GiftCheck] " + line);
        }

        private IEnumerator Start()
        {
            float guard = Time.realtimeSinceStartup + 20f;
            while (RealmSceneDirector.Current == null && Time.realtimeSinceStartup < guard) yield return null;
            RealmSceneDirector director = RealmSceneDirector.Current;
            if (director == null) { Finish(false, "no RealmSceneDirector in the scene"); yield break; }
            int flames = FindObjectsByType<FlameFlicker>(FindObjectsSortMode.None).Length;
            int water = FindObjectsByType<WaterMotion>(FindObjectsSortMode.None).Length;
            int bob = FindObjectsByType<Bob>(FindObjectsSortMode.None).Length;
            int workers = FindObjectsByType<WorkerLoop>(FindObjectsSortMode.None).Length;
            int sway = FindObjectsByType<Sway>(FindObjectsSortMode.None).Length;
            Log($"scene {director.sceneName}: FlameFlicker {flames}, WaterMotion {water}, Bob {bob}, WorkerLoop {workers}, Sway {sway}");
            // A first look at the painted set as the player sees it, before any gift.
            float settle = Time.realtimeSinceStartup + 1.5f;
            while (Time.realtimeSinceStartup < settle) yield return null;
            Shoot(director, "scene_start.png");

            CodexGift gift = null;
            guard = Time.realtimeSinceStartup + 120f;
            while (gift == null && Time.realtimeSinceStartup < guard)
            {
                gift = FindAnyObjectByType<CodexGift>();
                yield return null;
            }
            if (gift == null) { Finish(false, "no CodexGift appeared within 120s of the scene start"); yield break; }
            float t0 = Time.realtimeSinceStartup;
            Vector3 start = gift.transform.position;
            Log("CodexGift appeared at " + start);
            float travelled = 0f;
            Vector3 last = start;
            int shots = 0;
            float[] at = { 1.2f, 3.2f, 6.5f };
            int visibleFrames = 0, frames = 0;
            while (gift != null && Time.realtimeSinceStartup - t0 < 9f)
            {
                Vector3 p = gift.transform.position;
                travelled += Vector3.Distance(p, last);
                last = p;
                frames++;
                foreach (Renderer r in gift.GetComponentsInChildren<Renderer>())
                {
                    if (r.enabled && r.gameObject.activeInHierarchy) { visibleFrames++; break; }
                }
                if (shots < at.Length && Time.realtimeSinceStartup - t0 >= at[shots])
                {
                    Shoot(director, "gift_" + shots + ".png");
                    shots++;
                }
                yield return null;
            }
            Log($"gift travelled {travelled:F2} m, from {start} to {last}, renderer visible in {visibleFrames} of {frames} frames, still held: {gift != null}");
            bool ambientOk = flames + water + bob + workers + sway > 0;
            bool pass = travelled > 0.3f && visibleFrames > frames / 2 && ambientOk;
            Finish(pass, pass ? "gift formed, moved and stayed visible, ambient components present" : "gift or ambient components missing");
        }

        private void Shoot(RealmSceneDirector director, string name)
        {
            Camera cam = director.cameras != null ? director.cameras.ActiveCamera : null;
            if (cam == null) cam = Camera.main;
            if (cam == null) { Log("no camera for " + name); return; }
            var rt = RenderTexture.GetTemporary(1280, 720, 24);
            RenderTexture prevTarget = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = prevTarget;
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            File.WriteAllBytes(Path.Combine(dir, name), tex.EncodeToPNG());
            Destroy(tex);
            Log("saved " + name + " from " + cam.name);
        }

        private void Finish(bool pass, string why)
        {
            Log((pass ? "PASS: " : "FAIL: ") + why);
            File.WriteAllText(Path.Combine(dir, "gift_check.txt"), log.ToString());
            Application.Quit(pass ? 0 : 2);
        }
    }
}
