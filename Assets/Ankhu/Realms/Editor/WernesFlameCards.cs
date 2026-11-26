using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ankhu.Realms.EditorTools
{
    public static partial class RealmSceneBuilder
    {
        // Flame height in metres over a brazier bowl and under a hanging lamp, matched to the First Gate braziers.
        private const float BrazierFlameH = 0.95f, HangFlameH = 0.38f;

        /// <summary>
        /// The backdrops paint every brazier and hanging lamp cold, so each fire is a card on top (Zac's rule: complete
        /// backgrounds, objects authored separately). One fin_flame flipbook with a warm fx_glow halo goes over each
        /// RealmKit.Brazier bowl and under each hanging lamp. A group that already has a Flame card is left alone.
        /// Called by ApplyPaint and ApplyRosetauPaint before the set moves to the paint layer, so a rebuild keeps them.
        /// The cards carry CardFacing, so the paint pass treats them as live.
        /// </summary>
        private static int FlameCards()
        {
            Texture2D flameTex = SpriteTex("fin_flame", out float fa);
            Material flame = FlipMat("flip_room_flame", flameTex, new Color(1f, 0.92f, 0.8f, 1f), 1.5f, 0.08f, 3f, 0.1f, true);
            if (flame == null) { Debug.LogWarning("[Realm] fin_flame missing, no flame cards"); return 0; }
            flame.SetFloat("_Cols", 4f); flame.SetFloat("_Rows", 4f); flame.SetFloat("_FrameCount", 16f);
            flame.SetFloat("_Fps", 12f); flame.SetFloat("_Loop", 1f);
            Material halo = FlipMat("fx_room_flamehalo", FxTex("fx_glow"), new Color(1f, 0.6f, 0.3f, 0.5f), 1.2f, 0.2f, 2.2f, 0.25f, true);

            int n = 0;
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (t.Find("FlameCard") != null) continue;
                Transform bowl = t.Find("Bowl");
                if (bowl != null && t.Find("Embers") != null && t.Find("Stand") != null)
                {
                    // The rim of the bowl sphere (local 1.18, half height 0.16) is where the painted flame sits.
                    AddFlame(t, bowl.position + Vector3.up * 0.14f, BrazierFlameH, flame, halo, fa);
                    n++;
                    continue;
                }
                Transform lampFlame = t.Find("Flame");
                if (t.name == "Hang" && t.Find("Cord") != null && lampFlame != null && t.parent != null)
                {
                    // Parent to the still lamp root, not the swaying Hang: the painted lamp does not move.
                    AddFlame(t.parent, lampFlame.position - Vector3.up * 0.05f, HangFlameH, flame, halo, fa);
                    n++;
                }
            }
            HideBlockedFlames();
            Debug.Log("[Realm] flame cards: " + n);
            return n;
        }

        /// <summary>Layer for cards that one camera must not draw. Only the cameras that see them keep it in their mask.</summary>
        public const int CamHiddenLayer = 28;

        // A painted card has nothing in front of it, so a brazier behind a wall or a closed door would draw straight
        // through the painting. Each entry is a scene, the camera that cannot see the fire, and the lamp group.
        // Checked by projecting the lamp into the camera against the saved set (2026-11-26):
        // Wernes_C_House cam_house: HouseLampWest stands behind the west wall, GatePost behind the east door leaf.
        private static readonly (string scene, string cam, string lamp)[] BlockedFlames =
        {
            ("Wernes_C_House", "cam_house", "HouseLampWest"),
            ("Wernes_C_House", "cam_house", "GatePost"),
        };

        private static void HideBlockedFlames()
        {
            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            Camera[] cams = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var b in BlockedFlames)
            {
                if (b.scene != scene) continue;
                foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (t.name != b.lamp) continue;
                    Transform card = t.Find("FlameCard");
                    if (card == null) continue;
                    foreach (Transform c in card.GetComponentsInChildren<Transform>(true)) c.gameObject.layer = CamHiddenLayer;
                    foreach (Camera cam in cams)
                        if (cam.name == b.cam) cam.cullingMask &= ~(1 << CamHiddenLayer);
                    Debug.Log("[Realm] flame card " + b.lamp + " hidden from " + b.cam);
                }
            }
        }

        /// <summary>
        /// In place, for saved scenes that other work has edited since their build: opens each scene, removes only the
        /// objects named "FlameCard" (this helper's own cards), adds them fresh over every brazier bowl and hanging lamp,
        /// and saves. Nothing else in the scene is touched. Returns the card count per scene path.
        /// </summary>
        public static System.Collections.Generic.Dictionary<string, int> FlameCardsInPlace(string[] scenePaths)
        {
            var counts = new System.Collections.Generic.Dictionary<string, int>();
            foreach (string path in scenePaths)
            {
                if (!System.IO.File.Exists(path)) { Debug.LogWarning("[Realm] flame cards: missing " + path); continue; }
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                int removed = 0;
                foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    if (t != null && t.name == "FlameCard") { Object.DestroyImmediate(t.gameObject); removed++; }
                int n = FlameCards();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[Realm] flame cards in place: " + System.IO.Path.GetFileNameWithoutExtension(path) + " removed " + removed + ", added " + n);
                counts[path] = n;
            }
            return counts;
        }

        private static void AddFlame(Transform parent, Vector3 worldBase, float h, Material flame, Material halo, float fa)
        {
            Transform c = NewCard("FlameCard", parent, Vector3.zero, Quaternion.identity);
            c.position = worldBase;
            if (halo != null) Quad(c, halo, new Vector3(0f, h * 0.35f, 0.02f), new Vector2(h * 2.6f, h * 2.6f));
            Quad(c, flame, new Vector3(0f, h * 0.42f, 0f), new Vector2(h * fa, h));
        }
    }
}
