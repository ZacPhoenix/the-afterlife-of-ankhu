using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace Ankhu.Opening
{
    /// <summary>
    /// Review autopilot, off unless the player is started with -ankhuCapture &lt;folder&gt;. It plays the opening as a
    /// player would (waits on the title, picks Begin Journey, watches the film, walks the path with the stick held
    /// along the road) and saves screenshots at fixed times, then quits. Used to judge the real build, not editor renders.
    /// </summary>
    public sealed class OpeningAutoCapture : MonoBehaviour
    {
        private static string dir;
        private static string framesDir;
        private static bool titleOnly;
        private int shot;
        private int frame;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-ankhuCapture") dir = args[i + 1];
                if (args[i] == "-ankhuFrames") framesDir = args[i + 1];
                if (args[i] == "-ankhuTitleLook") titleOnly = args[i + 1] != "0";
            }
            if (string.IsNullOrEmpty(dir)) return;
            // Absolute paths: ScreenCapture resolves relative ones against the Data folder, not the working folder.
            dir = Path.GetFullPath(dir);
            if (!string.IsNullOrEmpty(framesDir)) framesDir = Path.GetFullPath(framesDir);
            Directory.CreateDirectory(dir);
            // The capture window is small and parked off-screen without focus, so it must keep running unfocused.
            Application.runInBackground = true;
            // Captures are silent: they run unseen next to whatever Zac is doing.
            AudioListener.volume = 0f;
            var go = new GameObject("OpeningAutoCapture");
            DontDestroyOnLoad(go);
            go.AddComponent<OpeningAutoCapture>();
        }

        private IEnumerator Start()
        {
            float filmEvery = 6f, walkEvery = 2.5f;
            yield return Until(OpeningSettings.TitleScene, 10f);
            float t0 = Time.unscaledTime;
            foreach (float at in new[] { 1.0f, 2.4f, 4.5f, 7.0f })
            {
                yield return new WaitForSecondsRealtime(Mathf.Max(0f, t0 + at - Time.unscaledTime));
                Snap("title");
            }
            if (titleOnly)
            {
                // Parallax check: hold the pointer at the far left, centre and far right of the screen, let the
                // layers settle, and snap each. Then quit without starting the journey.
                foreach ((string tag, float x) in new[] { ("left", -1f), ("centre", 0f), ("right", 1f) })
                {
                    OpeningInput.LookOverride = new Vector2(x, 0f);
                    yield return new WaitForSecondsRealtime(3f);
                    Snap("title_look_" + tag);
                }
                OpeningInput.LookOverride = null;
                Debug.Log("[AutoCapture] done, " + shot + " shots in " + dir);
                Application.Quit();
                yield break;
            }
            GameObject sel = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (sel != null) ExecuteEvents.Execute(sel, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
            yield return new WaitForSecondsRealtime(0.9f);
            Snap("title_out");

            yield return Until(OpeningSettings.CinematicScene, 30f);
            while (SceneManager.GetActiveScene().name == OpeningSettings.CinematicScene)
            {
                yield return new WaitForSecondsRealtime(filmEvery);
                if (SceneManager.GetActiveScene().name == OpeningSettings.CinematicScene) Snap("film");
            }

            yield return Until(OpeningSettings.WalkScene, 400f);
            yield return new WaitForSecondsRealtime(1.5f);
            Snap("walk");
            WalkPath path = FindAnyObjectByType<WalkPath>();
            WalkPlayer player = FindAnyObjectByType<WalkPlayer>();
            FixedCameraDirector director = FindAnyObjectByType<FixedCameraDirector>();
            if (path != null && player != null)
            {
                OpeningInput.AutoMove = () =>
                {
                    if (player == null || path == null) return Vector2.zero;
                    float s = path.Project(player.transform.position, out float lat);
                    Vector3 want = path.TangentAt(s + 2f) - Vector3.Cross(Vector3.up, path.TangentAt(s)) * Mathf.Clamp(lat * 0.4f, -0.6f, 0.6f);
                    want.y = 0f;
                    // Steer in the basis the player reads the stick in. It stays latched to the previous camera
                    // after a cut, as a held stick would in the game.
                    Quaternion basis = player.InputBasis;
                    Vector3 local = Quaternion.Inverse(basis) * want.normalized;
                    return new Vector2(local.x, local.z);
                };
            }
            // Pacing review: a snap when Anpu settles at a screen edge and one shortly after he walks on, a CSV of every
            // walker's walk-cycle phase (to show the attendants never step in time), and with -ankhuFrames every frame as
            // a JPG for the review film. The walk loop runs on game time so a slow frame dump does not cut it short.
            Escort anpu = null;
            Escort[] walkers = FindObjectsByType<Escort>(FindObjectsSortMode.None);
            foreach (Escort e in walkers) if (e.IsLeader) anpu = e;
            StreamWriter csv = new StreamWriter(Path.Combine(dir, "walk_phases.csv"));
            csv.Write("t,cam,player_s,player_speed");
            foreach (Escort e in walkers) csv.Write("," + e.name + "_s," + e.name + "_speed," + e.name + "_phase");
            csv.WriteLine(",anpu_paused");
            if (framesDir != null) { Directory.CreateDirectory(framesDir); Time.captureFramerate = 15; }
            float start = Time.time;
            float nextSnap = start + walkEvery, nextRow = start, exitAt = -1f;
            int lastCam = director != null ? director.Active : -1;
            bool wasPaused = false;
            while (SceneManager.GetActiveScene().name == OpeningSettings.WalkScene && Time.time - start < 150f)
            {
                yield return new WaitForEndOfFrame();
                if (framesDir != null)
                {
                    Texture2D tex = ScreenCapture.CaptureScreenshotAsTexture(SuperSize);
                    File.WriteAllBytes(Path.Combine(framesDir, (frame++).ToString("00000") + ".jpg"), tex.EncodeToJPG(88));
                    Destroy(tex);
                }
                if (SceneManager.GetActiveScene().name != OpeningSettings.WalkScene) break;
                int cam = director != null ? director.Active + 1 : 0;
                if (director != null && director.Active != lastCam) { lastCam = director.Active; Snap("walk_cut" + lastCam); }
                if (anpu != null)
                {
                    if (anpu.Paused && !wasPaused) Snap("cam" + cam + "_pause");
                    if (!anpu.Paused && wasPaused) exitAt = Time.time + 1.2f;
                    wasPaused = anpu.Paused;
                }
                if (exitAt > 0f && Time.time >= exitAt) { exitAt = -1f; Snap("cam" + cam + "_exit"); }
                if (Time.time >= nextRow && path != null && player != null)
                {
                    nextRow += 0.5f;
                    csv.Write(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0:0.00},{1},{2:0.00},{3:0.00}",
                        Time.time - start, cam, path.Project(player.transform.position, out _), player.CurrentSpeed));
                    foreach (Escort e in walkers)
                    {
                        float ph = e.Animator != null ? e.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime % 1f : -1f;
                        csv.Write(string.Format(System.Globalization.CultureInfo.InvariantCulture, ",{0:0.00},{1:0.00},{2:0.000}", e.S, e.Speed, ph));
                    }
                    csv.WriteLine("," + (anpu != null && anpu.Paused ? 1 : 0));
                }
                if (Time.time >= nextSnap) { nextSnap += walkEvery; Snap("walk"); }
            }
            csv.Close();
            Time.captureFramerate = 0;
            OpeningInput.AutoMove = null;
            // The last frame is the title the walk hands back to, once its logo has finished fading in.
            if (SceneManager.GetActiveScene().name == OpeningSettings.TitleScene)
            {
                yield return new WaitForSecondsRealtime(5f);
                Snap("title_return");
            }
            Debug.Log("[AutoCapture] done, " + shot + " shots in " + dir);
            Application.Quit();
        }

        // The settings menu re-applies the master volume, so hold the capture silent every frame.
        private void LateUpdate() => AudioListener.volume = 0f;

        private static IEnumerator Until(string scene, float timeout)
        {
            float end = Time.unscaledTime + timeout;
            while (SceneManager.GetActiveScene().name != scene && Time.unscaledTime < end) yield return null;
        }

        private void Snap(string tag)
        {
            string file = Path.Combine(dir, (shot++).ToString("000") + "_" + tag + ".png");
            ScreenCapture.CaptureScreenshot(file, SuperSize);
        }

        /// <summary>Renders captures at about 1080 lines whatever the window size, so a small window still gives full-size shots.</summary>
        private static int SuperSize => Mathf.Max(1, Mathf.RoundToInt(1080f / Mathf.Max(1, Screen.height)));
    }
}
