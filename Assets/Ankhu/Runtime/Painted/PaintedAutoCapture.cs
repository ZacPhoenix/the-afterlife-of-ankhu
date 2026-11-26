using System.Collections;
using System.IO;
using Ankhu.Core;
using Ankhu.Integration;
using UnityEngine;
using L = Ankhu.Layout.BorrowedDawnLayout;

namespace Ankhu.Painted
{
    /// <summary>
    /// Review autopilot for the painted Borrowed Dawn test, off unless the player is started with
    /// -ankhuPaintedCapture &lt;folder&gt;. Muted, renders to an offscreen texture only. It saves the
    /// FrameCapture orthographic framings (for an A/B against the hill-climb captures), three in-game
    /// perspective framings, then a 30 fps camera pan while the rules engine moves the room, and quits.
    /// Optional -ankhuPaintedTag &lt;name&gt; prefixes the stills.
    /// </summary>
    public sealed class PaintedAutoCapture : MonoBehaviour
    {
        private const int W = 1920, H = 1080;
        private static string dir;
        private static string tag = "painted";
        private static bool stillsOnly;
        private static bool states;
        private RenderTexture rt;
        private Texture2D read;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            foreach (string a in args)
            {
                if (a == "-ankhuPaintedStillsOnly") stillsOnly = true;
                if (a == "-ankhuPaintedStates") states = true;
            }
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-ankhuPaintedCapture") dir = args[i + 1];
                if (args[i] == "-ankhuPaintedTag") tag = args[i + 1];
            }
            if (string.IsNullOrEmpty(dir)) return;
            dir = Path.GetFullPath(dir);
            Directory.CreateDirectory(Path.Combine(dir, "frames"));
            Application.runInBackground = true;
            AudioListener.volume = 0f;
            var go = new GameObject("PaintedAutoCapture");
            DontDestroyOnLoad(go);
            go.AddComponent<PaintedAutoCapture>();
        }

        private IEnumerator Start()
        {
            rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            read = new Texture2D(W, H, TextureFormat.RGB24, false);
            yield return new WaitForSecondsRealtime(2f);
            Camera cam = Camera.main;
            if (cam == null) { Debug.LogError("[PaintedCapture] no main camera"); Application.Quit(1); yield break; }
            foreach (MonoBehaviour b in cam.GetComponents<MonoBehaviour>()) b.enabled = false;
            float fov = cam.orthographic ? 20f : cam.fieldOfView;
            float planeDist = L.CameraOrthoSize / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);

            // Stills, same framings as Ankhu.EditorTools.FrameCapture.
            float half = L.CameraOrthoSize * W / (float)H;
            float west = L.RoomMinX + half, east = L.RoomMaxX - half, centre = (west + east) * 0.5f;
            Ortho(cam, L.CameraOrthoSize);
            Snap(cam, tag + "_west", new Vector3(west, L.CameraFixedY, -30f));
            Snap(cam, tag + "_centre", new Vector3(centre, L.CameraFixedY, -30f));
            Snap(cam, tag + "_east", new Vector3(east, L.CameraFixedY, -30f));
            float wholeHalfHeight = (L.RoomMaxX - L.RoomMinX) * 0.5f * H / W;
            Ortho(cam, Mathf.Max(wholeHalfHeight, (L.CeilingY - L.BasinBottomY) * 0.5f + 1f));
            Snap(cam, tag + "_whole_room", new Vector3((L.RoomMinX + L.RoomMaxX) * 0.5f, (L.BasinBottomY + L.CeilingY) * 0.5f + 0.5f, -30f));
            Persp(cam, fov);
            Snap(cam, tag + "_persp_west", new Vector3(west, L.CameraFixedY, -planeDist));
            Snap(cam, tag + "_persp_centre", new Vector3(centre, L.CameraFixedY, -planeDist));
            Snap(cam, tag + "_persp_east", new Vector3(east, L.CameraFixedY, -planeDist));

            // Match framing for the hill-climb scorer: the painted export rect (x -1.5..36, y -4.5..12.5),
            // full width, centred, so the target master crop lines up pixel for pixel.
            Ortho(cam, 37.5f * 0.5f * H / W);
            Snap(cam, tag + "_match", new Vector3(-1.5f + 37.5f * 0.5f, -4.5f + 17f * 0.5f, -30f));
            if (states)
            {
                // Walk the intended solution and snap the gameplay views at each checkpoint, so every
                // state the room can show gets judged, not just the rest pose.
                Ortho(cam, L.CameraOrthoSize);
                var steps = new (string kind, string zone, string what, string snap)[]
                {
                    ("say", ZoneIds.Floor, "flow west", "s1_flowwest"),
                    ("walk", ZoneIds.Lift, null, null),
                    ("say", ZoneIds.Lift, "flow east", "s2_lifthigh"),
                    ("walk", ZoneIds.Gallery, null, null),
                    ("use", ZoneIds.Gallery, InteractionIds.ReadInscription, null),
                    ("walk", ZoneIds.Lift, null, null),
                    ("say", ZoneIds.Lift, "bind elevator", null),
                    ("say", ZoneIds.Lift, "flow west", "s3_elevatorbound"),
                    ("use", ZoneIds.Lift, InteractionIds.TakeMirror, null),
                    ("walk", ZoneIds.Gallery, null, null),
                    ("use", ZoneIds.Gallery, InteractionIds.LowerLadder, "s4_ladder"),
                    ("walk", ZoneIds.Floor, null, null),
                    ("use", ZoneIds.Floor, InteractionIds.InstallMirror, "s5_mirror"),
                    ("say", ZoneIds.Floor, "bind shadow", null),
                    ("say", ZoneIds.Floor, "wake bearer", "s6_bearer"),
                    ("say", ZoneIds.Floor, "flow east", null),
                    ("say", ZoneIds.Floor, "release elevator", "s7_released"),
                    ("walk", ZoneIds.Gallery, null, null),
                    ("walk", ZoneIds.Landing, null, null),
                    ("say", ZoneIds.Landing, "recall bell", null),
                    ("use", ZoneIds.Landing, InteractionIds.StrikeBell, null),
                    ("say", ZoneIds.Landing, "echo bell", "s8_bell"),
                    ("say", ZoneIds.Landing, "bind bridge", "s9_bridge"),
                    ("walk", ZoneIds.Bridge, null, null),
                    ("walk", ZoneIds.Gate, null, null),
                    ("say", ZoneIds.Gate, "He who returns the morning.", "s10_gate"),
                };
                foreach (var st in steps)
                {
                    if (st.kind == "walk") Walk(st.zone);
                    else if (st.kind == "say") Say(st.what, st.zone);
                    else Act(st.what, st.zone);
                    if (st.snap == null) continue;
                    yield return new WaitForSeconds(5f);
                    Ortho(cam, L.CameraOrthoSize);
                    Snap(cam, tag + "_" + st.snap + "_west", new Vector3(west, L.CameraFixedY, -30f));
                    Snap(cam, tag + "_" + st.snap + "_centre", new Vector3(centre, L.CameraFixedY, -30f));
                    Snap(cam, tag + "_" + st.snap + "_east", new Vector3(east, L.CameraFixedY, -30f));
                    Ortho(cam, 37.5f * 0.5f * H / W);
                    Snap(cam, tag + "_" + st.snap + "_match", new Vector3(-1.5f + 37.5f * 0.5f, -4.5f + 17f * 0.5f, -30f));
                }
                Debug.Log("[PaintedCapture] states done in " + dir);
                Application.Quit(0);
                yield break;
            }
            if (stillsOnly)
            {
                Debug.Log("[PaintedCapture] stills done in " + dir);
                Application.Quit(0);
                yield break;
            }

            // Motion: a perspective pan west to east while the room changes state.
            Time.captureFramerate = 30;
            const int frames = 480;
            for (int f = 0; f < frames; f++)
            {
                if (f == 15) Say("flow west", ZoneIds.Floor);
                if (f == 150) Say("flow east", ZoneIds.Floor);
                if (f == 270) Say("bind bridge", ZoneIds.Landing);
                if (f == 330) Act(InteractionIds.StrikeBell, ZoneIds.Landing);
                float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((f - 30f) / (frames - 90f)));
                cam.transform.position = new Vector3(Mathf.Lerp(west, east, u), L.CameraFixedY, -planeDist);
                yield return new WaitForEndOfFrame();
                Save(cam, Path.Combine(dir, "frames", "f" + f.ToString("0000") + ".jpg"), true);
            }
            Time.captureFramerate = 0;
            Debug.Log("[PaintedCapture] done in " + dir);
            Application.Quit(0);
        }

        private static void Say(string text, string zone)
        {
            var host = RoomRulesHost.Instance;
            if (host == null || host.Adapter == null) { Debug.LogWarning("[PaintedCapture] no rules host for '" + text + "'"); return; }
            CommandResult r = host.Adapter.Speak(text, Ctx(zone));
            Debug.Log("[PaintedCapture] speak '" + text + "' -> " + r.Outcome + " " + r.Message);
        }

        private static void Walk(string zone)
        {
            var host = RoomRulesHost.Instance;
            if (host == null || host.Adapter == null) return;
            // SyncLocation lives on the room's concrete adapter (Ankhu.Bindings), which this assembly cannot reference.
            var m = host.Adapter.GetType().GetMethod("SyncLocation");
            object r = m != null ? m.Invoke(host.Adapter, new object[] { zone }) : null;
            Debug.Log("[PaintedCapture] walk " + zone + (r == null ? " ok" : " refused " + ((CommandResult)r).Message));
        }

        private static void Act(string id, string zone)
        {
            var host = RoomRulesHost.Instance;
            if (host == null || host.Adapter == null) return;
            CommandResult r = host.Adapter.Interact(id, Ctx(zone));
            Debug.Log("[PaintedCapture] interact '" + id + "' -> " + r.Outcome + " " + r.Message);
        }

        private static PlayerContext Ctx(string zone) => new PlayerContext(zone, zone == ZoneIds.Lift, zone == ZoneIds.Bridge);

        private static void Ortho(Camera cam, float size) { cam.orthographic = true; cam.orthographicSize = size; cam.farClipPlane = 200f; }
        private static void Persp(Camera cam, float fov) { cam.orthographic = false; cam.fieldOfView = fov; cam.farClipPlane = 200f; }

        private void Snap(Camera cam, string name, Vector3 pos)
        {
            cam.transform.SetPositionAndRotation(pos, Quaternion.identity);
            cam.targetTexture = rt;
            cam.Render();
            Save(cam, Path.Combine(dir, name + ".png"), false);
        }

        private void Save(Camera cam, string path, bool jpg)
        {
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            read.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            read.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(path, jpg ? read.EncodeToJPG(92) : read.EncodeToPNG());
        }
    }
}
