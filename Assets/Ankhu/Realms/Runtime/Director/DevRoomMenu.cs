using Ankhu.Core;
using Ankhu.Opening;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ankhu.Realms
{
    /// <summary>
    /// Zac 2026-11-26: "a dev menu to skip ahead to future rooms when I press F3". F3 opens a list of every room in
    /// play order (PlayOrder.Resolved, only the ones in the build). Click one, or move with W/S or the arrows and
    /// press Enter, to fade straight there. Esc or F3 closes it. The game is paused while it is open, and F3 is
    /// ignored while the speech bubble is taking typed text. Jumping past the First Gate hands Ankhu the codex, so
    /// later rooms open as they would in a real run.
    /// </summary>
    public sealed class DevRoomMenu : MonoBehaviour
    {
        private static DevRoomMenu instance;
        private bool open;
        private int cursor;
        private float savedScale = 1f;
        private Vector2 scroll;
        private string[] rooms = new string[0];

        public static bool IsOpen => instance != null && instance.open;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            if (instance != null) return;
            var go = new GameObject("DevRoomMenu");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<DevRoomMenu>();
        }

        private void Update()
        {
            Keyboard k = Keyboard.current;
            if (k == null) return;
            if (!open)
            {
                if (k.f3Key.wasPressedThisFrame && !InputGate.TextEntryActive && !SceneFader.Busy) Open();
                return;
            }
            if (k.f3Key.wasPressedThisFrame || k.escapeKey.wasPressedThisFrame) { Close(); return; }
            if (k.downArrowKey.wasPressedThisFrame || k.sKey.wasPressedThisFrame) cursor = Mathf.Min(rooms.Length - 1, cursor + 1);
            if (k.upArrowKey.wasPressedThisFrame || k.wKey.wasPressedThisFrame) cursor = Mathf.Max(0, cursor - 1);
            if (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame) Go(cursor);
        }

        private void Open()
        {
            var list = new System.Collections.Generic.List<string>();
            foreach (string s in PlayOrder.Resolved())
                if (PlayOrder.InBuild(s) && !list.Contains(s)) list.Add(s);
            rooms = list.ToArray();
            string here = PlayOrder.ActiveScene();
            cursor = Mathf.Max(0, System.Array.IndexOf(rooms, here));
            savedScale = Time.timeScale;
            Time.timeScale = 0f;
            open = true;
        }

        private void Close()
        {
            if (!open) return;
            open = false;
            Time.timeScale = savedScale <= 0f ? 1f : savedScale;
        }

        private void Go(int i)
        {
            if (i < 0 || i >= rooms.Length) return;
            string scene = rooms[i];
            int gate = PlayOrder.IndexOf(OpeningSettings.FirstGateScene);
            if (PlayOrder.IndexOf(scene) > gate && !CodexStore.Has("lore.codex"))
            {
                CodexStore.Write("lore.codex");
                CodexStore.Write("lore.gate_one");
            }
            Close();
            SceneFader.FadeTo(scene, 0.4f, 0.6f);
        }

        private void OnGUI()
        {
            if (!open) return;
            float w = 460f, h = Mathf.Min(Screen.height - 80f, 90f + rooms.Length * 30f);
            var box = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            GUI.color = new Color(0f, 0f, 0f, 0.85f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUILayout.BeginArea(box, GUI.skin.box);
            GUILayout.Label("<b>Dev: jump to a room</b>   (Enter go, Esc close)", new GUIStyle(GUI.skin.label) { richText = true, fontSize = 16 });
            scroll = GUILayout.BeginScrollView(scroll);
            string here = PlayOrder.ActiveScene();
            for (int i = 0; i < rooms.Length; i++)
            {
                string label = (i + 1).ToString("00") + "   " + rooms[i] + (rooms[i] == here ? "   (here)" : "");
                var style = new GUIStyle(GUI.skin.button) { alignment = TextAnchor.MiddleLeft, fontSize = 15 };
                if (i == cursor) style.normal.textColor = style.hover.textColor = new Color(1f, 0.85f, 0.45f);
                if (GUILayout.Button(label, style, GUILayout.Height(26f))) Go(i);
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
