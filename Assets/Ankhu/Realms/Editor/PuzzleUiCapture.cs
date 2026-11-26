using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Ankhu.Opening;
using Ankhu.Player;
using Ankhu.Realms.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Ankhu.Realms.EditorTools
{
    /// <summary>
    /// Zac 2026-11-25 puzzle UI picks: renders the realm HUD pieces (papyrus prompt slip and glint, cartouche and
    /// ledger, seal hint scroll, codex roll with its new-entry badge, the typing bubble with live gloss and word
    /// tray, the WASD note) over a realm capture, in edit mode, so the layout can be checked without Play mode.
    /// Animations do not run here, so every group is forced fully shown. Output: art_gen/realms/audit/ui_capture.png.
    /// CLI: -executeMethod Ankhu.Realms.EditorTools.PuzzleUiCapture.CaptureCli
    /// </summary>
    public static class PuzzleUiCapture
    {
        public const string OutPath = "art_gen/realms/audit/ui_capture.png";
        private const string Backdrop = "art_gen/realms/captures/Realm_01_Arit1_cam_court_start.png";

        public static void CaptureCli()
        {
            try { Capture(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        [UnityEditor.MenuItem("Ankhu/Realms/Capture Puzzle UI")]
        public static void Capture()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            const int W = 1920, H = 1080;
            var camGo = new GameObject("UiCam");
            Camera cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.1f, 0.08f, 0.06f, 1f);
            cam.tag = "MainCamera";
            cam.transform.position = new Vector3(0f, 1.5f, -8f);

            var backCanvas = PapyrusKit.Canvas("Back", -100);
            if (File.Exists(Backdrop))
            {
                var tex = new Texture2D(2, 2);
                tex.LoadImage(File.ReadAllBytes(Backdrop));
                var raw = new GameObject("Backdrop", typeof(RectTransform)).AddComponent<RawImage>();
                raw.transform.SetParent(backCanvas.transform, false);
                raw.texture = tex;
                PapyrusKit.Stretch(raw.rectTransform);
            }

            StateBoard.ForgetKeyScroll();
            RealmHud hud = RealmHud.Create();
            SceneScript script = RealmContent.ByScene("Realm_01_Arit1") ?? RealmContent.ByScene("Wernes_A_Field");
            var room = new RealmRoom(script.Room);
            hud.SetRoomState(room);
            string[] tiers = RealmHintSolver.Tiers(room);
            hud.ShowHintTiers(tiers);
            HintScroll scroll = UnityEngine.Object.FindAnyObjectByType<HintScroll>();
            if (scroll != null) scroll.BreakNext();
            hud.ShowPrompt("E", "Read the stela", new Vector3(3f, 1.5f, 0f));
            hud.ShowGlint(new Vector3(-3f, 1.2f, 0f));

            Canvas hudCanvas = hud.GetComponentInChildren<Canvas>();
            CodexHud codex = CodexHud.Build((RectTransform)hudCanvas.transform);
            Invoke(codex, "OnWritten", new CodexEntry { Id = "word.open", Kind = CodexKind.Word, Title = "wn, to open" });

            PapyrusNote.Show("Use [W A S D] to walk.");

            RealmSpeechBubble bubble = RealmSpeechBubble.Create(null);
            bubble.BeginTyping();
            const string typed = "open the gate";
            bubble.SetText(typed, false);
            var live = new List<RealmSpeechBubble.LiveWord>();
            int at = 0;
            foreach (string w in typed.Split(' '))
            {
                if (Lexicon.TryGlossToken(w, out WordGloss g) || w == "gate")
                {
                    if (g == null) Lexicon.TryGlossToken("door", out g);
                    if (g != null)
                        live.Add(new RealmSpeechBubble.LiveWord { From = at, To = at + w.Length, Signs = g.Signs, Translit = g.Transliteration, Target = w == "gate" });
                }
                at += w.Length + 1;
            }
            bubble.SetLiveGloss(live);
            bubble.SetTray(new[] { "open", "close", "rise", "Ra", "Anpu" }, 2);

            // Run a few update passes so layout code that lives in Update settles, then force everything shown.
            for (int i = 0; i < 4; i++)
                foreach (MonoBehaviour mb in UnityEngine.Object.FindObjectsByType<MonoBehaviour>())
                {
                    if (mb == null || mb is HintScroll) continue;
                    Invoke(mb, "Update");
                    Invoke(mb, "LateUpdate");
                }
            // The ledger only writes its lines while L is held, so write them here for the shot.
            StateBoard board = UnityEngine.Object.FindAnyObjectByType<StateBoard>();
            Invoke(board, "RebuildLedger");
            Debug.Log("PuzzleUiCapture room " + script.Room.Id + " goals " + script.Room.Goal.Length);
            foreach (CanvasGroup g in UnityEngine.Object.FindObjectsByType<CanvasGroup>(FindObjectsInactive.Include))
            {
                g.alpha = 1f;
                g.gameObject.SetActive(true);
            }
            foreach (Canvas c in UnityEngine.Object.FindObjectsByType<Canvas>())
            {
                if (!c.isRootCanvas) continue;
                c.renderMode = RenderMode.ScreenSpaceCamera;
                c.worldCamera = cam;
                c.planeDistance = 1f + Mathf.Clamp(1000 - c.sortingOrder, 0, 2000) * 0.001f;
            }
            Canvas.ForceUpdateCanvases();

            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.aspect = (float)W / H;
            cam.Render();
            cam.Render();
            RenderTexture.active = rt;
            var shot = new Texture2D(W, H, TextureFormat.RGB24, false);
            shot.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            shot.Apply();
            RenderTexture.active = null;
            Directory.CreateDirectory(Path.GetDirectoryName(OutPath));
            File.WriteAllBytes(OutPath, shot.EncodeToPNG());
            Debug.Log("PuzzleUiCapture wrote " + Path.GetFullPath(OutPath));
            // Building the HUD marked the first-room key scroll as seen. Clear it so the player still gets it.
            StateBoard.ForgetKeyScroll();
        }

        private static void Invoke(object target, string method, object arg = null)
        {
            if (target == null) return;
            MethodInfo m = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (m == null) return;
            try { m.Invoke(target, m.GetParameters().Length == 1 ? new[] { arg } : null); }
            catch (Exception e) { Debug.LogWarning("PuzzleUiCapture: " + target.GetType().Name + "." + method + " threw " + (e.InnerException ?? e).Message); }
        }
    }
}
