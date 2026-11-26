using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ankhu.Realms
{
    /// <summary>
    /// Built-player check of the painted speech bubble and the glyph strike. Off unless the player is started with
    /// -ankhuSpellCheck &lt;folder&gt; (and optionally -ankhuSpellText "bind the beam"). Muted. It waits for the room's
    /// SpellPresenter to accept input, types the words into the bubble one letter at a time, submits them, and saves
    /// camera frames while typing, at glyph launch, mid flight, at the strike and just after. The overlay canvases are
    /// drawn into the camera for each frame, so the bubble shows even with a hidden window. Writes spell_check.txt with
    /// PASS (glyphs launched and struck) or FAIL, then quits.
    /// </summary>
    public sealed class SpellPlayerCheck : MonoBehaviour
    {
        private static string dir;
        private static string text = "bind the beam";
        private readonly StringBuilder log = new StringBuilder();
        private int launched, arrived;
        private float firstLaunch = -1f, firstArrive = -1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-ankhuSpellCheck") dir = args[i + 1];
                if (args[i] == "-ankhuSpellText") text = args[i + 1];
            }
            if (string.IsNullOrEmpty(dir) || FindAnyObjectByType<SpellPlayerCheck>() != null) return;
            dir = Path.GetFullPath(dir);
            Directory.CreateDirectory(dir);
            Application.runInBackground = true;
            AudioListener.volume = 0f;
            var go = new GameObject("SpellPlayerCheck");
            DontDestroyOnLoad(go);
            go.AddComponent<SpellPlayerCheck>();
        }

        private void Log(string line)
        {
            line = $"[{Time.realtimeSinceStartup:F2}s] {line}";
            log.AppendLine(line);
            Debug.Log("[SpellCheck] " + line);
        }

        private void OnEnable()
        {
            SpellPresenter.GlyphLaunched += OnLaunched;
            SpellPresenter.GlyphArrived += OnArrived;
        }

        private void OnDisable()
        {
            SpellPresenter.GlyphLaunched -= OnLaunched;
            SpellPresenter.GlyphArrived -= OnArrived;
        }

        private void OnLaunched(string code, Vector3 at)
        {
            launched++;
            if (firstLaunch < 0f) firstLaunch = Time.realtimeSinceStartup;
            Log($"glyph launched {code} at {at}");
        }

        private void OnArrived(string code, Vector3 at)
        {
            arrived++;
            if (firstArrive < 0f) firstArrive = Time.realtimeSinceStartup;
            Log($"glyph arrived {code} at {at}");
        }

        private IEnumerator Start()
        {
            float guard = Time.realtimeSinceStartup + 30f;
            SpellPresenter p = null;
            while (p == null && Time.realtimeSinceStartup < guard) { p = FindAnyObjectByType<SpellPresenter>(); yield return null; }
            if (p == null) { Finish(false, "no SpellPresenter in the scene"); yield break; }
            Log("scene " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name + ", skin " + (p.Bubble != null && p.Bubble.Skin != null ? p.Bubble.Skin.Name : "?"));

            guard = Time.realtimeSinceStartup + 60f;
            while (!(p.InputAllowed && !p.Busy) && Time.realtimeSinceStartup < guard) yield return null;
            Log("input allowed: " + p.InputAllowed);
            yield return new WaitForSecondsRealtime(1.5f);
            Shoot("0_room.png");

            Keyboard kb = Keyboard.current;
            if (kb == null) { Finish(false, "no keyboard device in the player"); yield break; }
            const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic;
            MethodInfo open = typeof(SpellPresenter).GetMethod("Open", F);
            MethodInfo type = typeof(SpellPresenter).GetMethod("HandleTextInput", F);
            MethodInfo submit = typeof(SpellPresenter).GetMethod("Submit", F);
            if (open == null || type == null || submit == null) { Finish(false, "SpellPresenter typing methods not found"); yield break; }

            open.Invoke(p, new object[] { kb });
            yield return new WaitForSecondsRealtime(0.4f);
            foreach (char c in text)
            {
                type.Invoke(p, new object[] { c });
                yield return new WaitForSecondsRealtime(0.08f);
            }
            yield return new WaitForSecondsRealtime(0.6f);
            Shoot("1_typed.png");
            Log("submitting \"" + text + "\"");
            submit.Invoke(p, null);
            // The signs assemble in the bubble and hold there before they fly. Catch them at rest.
            float shot = Time.realtimeSinceStartup + 1.3f;
            while (launched == 0 && Time.realtimeSinceStartup < shot) yield return null;
            if (launched == 0) Shoot("1b_signs.png");

            guard = Time.realtimeSinceStartup + 20f;
            while (launched == 0 && Time.realtimeSinceStartup < guard) yield return null;
            if (launched == 0) { yield return new WaitForSecondsRealtime(1f); Shoot("2_no_launch.png"); Finish(false, "no glyph left the bubble within 20s"); yield break; }
            Shoot("2_launch.png");
            int n = 0;
            guard = Time.realtimeSinceStartup + 15f;
            float nextShot = Time.realtimeSinceStartup + 0.25f;
            while (arrived == 0 && Time.realtimeSinceStartup < guard)
            {
                if (Time.realtimeSinceStartup >= nextShot && n < 8)
                {
                    Shoot($"3_flight_{n++}.png");
                    nextShot = Time.realtimeSinceStartup + 0.25f;
                }
                yield return null;
            }
            if (arrived == 0) { Finish(false, "glyphs launched but never struck"); yield break; }
            Shoot("4_strike.png");
            yield return new WaitForSecondsRealtime(0.45f);
            Shoot("5_after.png");
            yield return new WaitForSecondsRealtime(2.0f);
            Shoot("6_settled.png");
            Log($"launched {launched}, arrived {arrived}, flight {firstArrive - firstLaunch:F2}s");
            Finish(true, "glyphs flew from the bubble and struck the target");
        }

        /// <summary>Renders the main camera with every overlay canvas drawn into it, at 1280x720.</summary>
        private void Shoot(string name)
        {
            Camera cam = Camera.main;
            if (cam == null || !cam.isActiveAndEnabled)
            {
                cam = null;
                foreach (Camera c in FindObjectsByType<Camera>(FindObjectsSortMode.None))
                {
                    if (c.isActiveAndEnabled && c.targetTexture == null && (cam == null || c.depth > cam.depth)) cam = c;
                }
            }
            if (cam == null) { Log("no main camera for " + name); return; }
            var switched = new List<Canvas>();
            foreach (Canvas c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (!c.isRootCanvas || c.renderMode != RenderMode.ScreenSpaceOverlay || !c.isActiveAndEnabled) continue;
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = cam;
                c.planeDistance = cam.nearClipPlane + 0.05f;
                switched.Add(c);
            }
            Canvas.ForceUpdateCanvases();
            var rt = RenderTexture.GetTemporary(Screen.width, Screen.height, 24);
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
            foreach (Canvas c in switched) c.renderMode = RenderMode.ScreenSpaceOverlay;
            File.WriteAllBytes(Path.Combine(dir, name), tex.EncodeToPNG());
            Destroy(tex);
            Log("saved " + name);
        }

        private void Finish(bool pass, string why)
        {
            Log((pass ? "PASS: " : "FAIL: ") + why);
            File.WriteAllText(Path.Combine(dir, "spell_check.txt"), log.ToString());
            Application.Quit(pass ? 0 : 2);
        }
    }
}
