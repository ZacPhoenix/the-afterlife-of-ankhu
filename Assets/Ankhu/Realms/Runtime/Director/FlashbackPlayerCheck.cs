using System.Collections;
using System.IO;
using System.Text;
using Ankhu.Realms.Core;
using UnityEngine;
using UnityEngine.Video;

namespace Ankhu.Realms
{
    /// <summary>
    /// Built-player check that a memory film really plays. Off unless the player is started with
    /// -ankhuFlashbackCheck &lt;folder&gt;. Muted. In a realm scene it fires the Solved beat (which runs
    /// Step.Cue("flashback:...")), watches the VideoPlayer the flashback creates, saves two decoded frames
    /// from its render texture, writes flashback_check.txt with PASS or FAIL, and quits.
    /// </summary>
    public sealed class FlashbackPlayerCheck : MonoBehaviour
    {
        private static string dir;
        private readonly StringBuilder log = new StringBuilder();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-ankhuFlashbackCheck") dir = args[i + 1];
            }
            if (string.IsNullOrEmpty(dir) || FindAnyObjectByType<FlashbackPlayerCheck>() != null) return;
            dir = Path.GetFullPath(dir);
            Directory.CreateDirectory(dir);
            Application.runInBackground = true;
            AudioListener.volume = 0f;
            var go = new GameObject("FlashbackPlayerCheck");
            DontDestroyOnLoad(go);
            go.AddComponent<FlashbackPlayerCheck>();
        }

        private void Log(string line)
        {
            line = $"[{Time.realtimeSinceStartup:F2}s] {line}";
            log.AppendLine(line);
            Debug.Log("[FlashbackCheck] " + line);
        }

        private IEnumerator Start()
        {
            float guard = Time.realtimeSinceStartup + 20f;
            while (RealmSceneDirector.Current == null && Time.realtimeSinceStartup < guard) yield return null;
            RealmSceneDirector director = RealmSceneDirector.Current;
            if (director == null) { Finish(false, "no RealmSceneDirector in the scene"); yield break; }
            Log("scene " + director.sceneName + ", waiting 4s for the start beats");
            yield return new WaitForSecondsRealtime(4f);
            Log("firing Solved");
            director.FireForCheck(TriggerKind.Solved);
            StartCoroutine(ShootBookPage());

            VideoPlayer vp = null;
            guard = Time.realtimeSinceStartup + 60f;
            while (vp == null && Time.realtimeSinceStartup < guard)
            {
                vp = FindAnyObjectByType<VideoPlayer>();
                yield return null;
            }
            if (vp == null) { Finish(false, "no VideoPlayer appeared within 60s of the Solved beat"); yield break; }
            Log("VideoPlayer appeared on " + vp.gameObject.name + ", url " + vp.url);

            long maxFrame = -1;
            double maxTime = 0;
            bool started = false, savedEarly = false, savedMid = false;
            float startedAt = 0f;
            float brightEarly = -1f, brightMid = -1f;
            guard = Time.realtimeSinceStartup + 150f;
            while (vp != null && Time.realtimeSinceStartup < guard)
            {
                for (ushort t = 0; t < vp.audioTrackCount; t++) vp.SetDirectAudioMute(t, true);
                if (vp.isPlaying && !started)
                {
                    started = true;
                    startedAt = Time.realtimeSinceStartup;
                    Log($"playing: {vp.width}x{vp.height}, {vp.frameCount} frames at {vp.frameRate:F2} fps, length {vp.length:F2}s");
                }
                if (vp.frame > maxFrame) maxFrame = vp.frame;
                if (vp.time > maxTime) maxTime = vp.time;
                if (started && !savedEarly && vp.frame > 24) { savedEarly = true; brightEarly = Save(vp, "frame_early.png"); }
                if (started && !savedMid && vp.frameCount > 0 && vp.frame > (long)vp.frameCount / 2) { savedMid = true; brightMid = Save(vp, "frame_mid.png"); }
                yield return null;
            }
            bool ended = vp == null;
            Log($"ended by itself: {ended}, max frame {maxFrame}, max time {maxTime:F2}s, wall {(started ? Time.realtimeSinceStartup - startedAt : 0f):F2}s");
            Log($"mean brightness early {brightEarly:F3}, mid {brightMid:F3}");
            bool pass = started && maxFrame > 30 && Mathf.Max(brightEarly, brightMid) > 0.02f;
            Finish(pass, pass ? "video decoded and advanced" : "video did not play through");
        }

        /// <summary>Screenshots of the Book painting the memory (pick 7): mid paint, end of paint, and mid grow.</summary>
        private IEnumerator ShootBookPage()
        {
            float guard = Time.realtimeSinceStartup + 30f;
            CodexMemoryPage page = null;
            while (page == null && Time.realtimeSinceStartup < guard) { page = FindAnyObjectByType<CodexMemoryPage>(); yield return null; }
            if (page == null) { Log("no Codex memory page appeared"); yield break; }
            float t0 = Time.realtimeSinceStartup;
            Log("Codex memory page opened");
            float[] at = { 2.2f, 4.3f, 5.6f };
            string[] names = { "book_paint.png", "book_painted.png", "book_grow.png" };
            for (int i = 0; i < at.Length; i++)
            {
                while (Time.realtimeSinceStartup - t0 < at[i]) yield return null;
                yield return new WaitForEndOfFrame();
                var tex = ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(Path.Combine(dir, names[i]), tex.EncodeToPNG());
                Destroy(tex);
                Log($"saved {names[i]} at {Time.realtimeSinceStartup - t0:F2}s into the page");
            }
            while (page != null) yield return null;
            Log($"Codex memory page gone {Time.realtimeSinceStartup - t0:F2}s after it opened");
        }

        private float Save(VideoPlayer vp, string name)
        {
            var rt = vp.targetTexture;
            if (rt == null) { Log("no target texture for " + name); return -1f; }
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            Color32[] px = tex.GetPixels32();
            double sum = 0;
            for (int i = 0; i < px.Length; i += 97) sum += (px[i].r + px[i].g + px[i].b) / 765.0;
            float mean = (float)(sum / Mathf.Max(1, px.Length / 97));
            File.WriteAllBytes(Path.Combine(dir, name), tex.EncodeToPNG());
            Destroy(tex);
            Log($"saved {name} at frame {vp.frame}, mean brightness {mean:F3}");
            return mean;
        }

        private void Finish(bool pass, string why)
        {
            Log((pass ? "PASS: " : "FAIL: ") + why);
            File.WriteAllText(Path.Combine(dir, "flashback_check.txt"), log.ToString());
            Application.Quit(pass ? 0 : 2);
        }
    }
}
