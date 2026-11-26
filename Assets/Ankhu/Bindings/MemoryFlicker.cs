using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Ankhu.Bindings
{
    /// <summary>
    /// Zac 2026-11-24: Memory 0, the cutscene when Ankhu solves Borrowed Dawn, so he feels the room
    /// unlock something buried in him. The teaching-room flickers belong to MemoryFlashback (another
    /// thread), so only Borrowed Dawn is listed here. Fold this into MemoryFlashback once that lands. A full-screen painted still (Resources/RealmMemories/&lt;image&gt;)
    /// fades in with a slow push, Ankhu's inner lines play as captions, and it fades back out. If the
    /// painting is not made yet, a warm dusk wash stands in so the beat still plays.
    /// Memory 0 follows the Wernes spec v2: the dawn Hori was born.
    /// </summary>
    public sealed class MemoryFlicker : MonoBehaviour
    {
        public sealed class Memory
        {
            public string Image;
            public string[] Lines;
            public string Closing;
        }

        public const float FadeSeconds = 1.2f;
        public const float SecondsPerLine = 4.5f;

        private static readonly Color CaptionColor = new Color(1f, 0.9f, 0.68f);
        private static readonly Color ClosingColor = new Color(0.78f, 0.66f, 1f);

        /// <summary>The memory a room leaves behind when solved, or null.</summary>
        public static Memory ForRoom(string roomId)
        {
            switch (roomId)
            {
                case BorrowedDawnRooms.BorrowedDawnId:
                    return new Memory
                    {
                        Image = "memory0_dawn",
                        Lines = new[]
                        {
                            "First light through a doorway. Neferet on the mat, tired, smiling at me.",
                            "I lift him into the sun so Ra will know his face. I say his name.",
                            "Hori.",
                        },
                        Closing = "I had a son.",
                    };
                default:
                    return null;
            }
        }

        /// <summary>The authored memory film for a solved room (StreamingAssets/Memories/&lt;id&gt;.mp4), or null.</summary>
        public static string FilmForRoom(string roomId)
        {
            if (roomId == BorrowedDawnRooms.BorrowedDawnId) return "mem0_dawn_name";
            if (roomId == global::Ankhu.BorrowedDawn.TeachingRooms.Cistern.Id) return "flick_f1_river";
            if (roomId == global::Ankhu.BorrowedDawn.TeachingRooms.HeldStep.Id) return "flick_f2_hands";
            if (roomId == global::Ankhu.BorrowedDawn.TeachingRooms.LingeringShadow.Id) return "flick_f3_shadow";
            return null;
        }

        /// <summary>
        /// Plays the room's authored memory film through Ankhu.Memories.MemoryFlashback (found by reflection, so this
        /// assembly needs no reference to it). Falls back to the painted still (ForRoom) when the film or the player is
        /// missing. 120 s guard so a broken clip never strands the player.
        /// </summary>
        public static IEnumerator PlayForRoom(string roomId)
        {
            string film = FilmForRoom(roomId);
            System.Reflection.MethodInfo play = null;
            if (film != null && System.IO.File.Exists(System.IO.Path.Combine(Application.streamingAssetsPath, "Memories", film + ".mp4")))
            {
                foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    var type = asm.GetType("Ankhu.Memories.MemoryFlashback");
                    if (type == null) continue;
                    play = type.GetMethod("Play", new[] { typeof(string), typeof(System.Action) });
                    break;
                }
            }
            if (play == null)
            {
                yield return Play(ForRoom(roomId));
                yield break;
            }
            bool done = false;
            play.Invoke(null, new object[] { film, (System.Action)(() => done = true) });
            float guard = 0f;
            while (!done && guard < 120f)
            {
                guard += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        /// <summary>Plays a memory on a new overlay and destroys it after. Yield on the result to wait.</summary>
        public static IEnumerator Play(Memory memory)
        {
            if (memory == null)
            {
                yield break;
            }
            var go = new GameObject("Memory " + memory.Image);
            var flicker = go.AddComponent<MemoryFlicker>();
            yield return flicker.Run(memory);
            Destroy(go);
        }

        private CanvasGroup group;
        private RawImage picture;
        private Text caption;

        private IEnumerator Run(Memory memory)
        {
            Build(memory.Image);
            yield return Fade(0f, 1f, FadeSeconds);
            foreach (string line in memory.Lines)
            {
                yield return Caption(line, CaptionColor, 1f);
            }
            if (!string.IsNullOrEmpty(memory.Closing))
            {
                yield return Caption(memory.Closing, ClosingColor, 0.8f);
            }
            caption.text = string.Empty;
            yield return Fade(1f, 0f, FadeSeconds);
        }

        private IEnumerator Caption(string line, Color color, float scale)
        {
            caption.text = line;
            caption.color = color;
            float hold = Mathf.Max(2.5f, SecondsPerLine * scale + line.Length * 0.02f);
            float t = 0f;
            while (t < hold)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private IEnumerator Fade(float from, float to, float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / seconds));
                yield return null;
            }
            group.alpha = to;
        }

        private void Update()
        {
            // A slow push in and drift, so the still breathes like a remembered moment.
            if (picture != null)
            {
                float k = Time.unscaledTime;
                float s = 1.04f + 0.03f * Mathf.Sin(k * 0.11f);
                picture.rectTransform.localScale = new Vector3(s, s, 1f);
                picture.rectTransform.anchoredPosition = new Vector2(Mathf.Sin(k * 0.07f) * 14f, Mathf.Cos(k * 0.05f) * 8f);
            }
        }

        private void Build(string image)
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 400;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = true;

            var black = new GameObject("Black", typeof(RectTransform)).AddComponent<Image>();
            black.transform.SetParent(transform, false);
            Stretch(black.rectTransform);
            black.color = Color.black;

            picture = new GameObject("Picture", typeof(RectTransform)).AddComponent<RawImage>();
            picture.transform.SetParent(transform, false);
            Stretch(picture.rectTransform);
            Texture2D tex = Resources.Load<Texture2D>("RealmMemories/" + image);
            if (tex != null)
            {
                picture.texture = tex;
                picture.color = new Color(1f, 0.96f, 0.9f);
                // No-stretch rule: cover the screen at the still's own aspect, never fill it at the window's.
                var fit = picture.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fit.aspectRatio = tex.width / (float)Mathf.Max(1, tex.height);
            }
            else
            {
                picture.texture = DuskWash();
                picture.color = Color.white;
            }

            var vignette = new GameObject("Caption Band", typeof(RectTransform)).AddComponent<Image>();
            vignette.transform.SetParent(transform, false);
            var vr = vignette.rectTransform;
            vr.anchorMin = new Vector2(0f, 0f);
            vr.anchorMax = new Vector2(1f, 0.26f);
            vr.offsetMin = vr.offsetMax = Vector2.zero;
            vignette.color = new Color(0f, 0f, 0f, 0.55f);

            caption = new GameObject("Caption", typeof(RectTransform)).AddComponent<Text>();
            caption.transform.SetParent(vignette.transform, false);
            var cr = caption.rectTransform;
            cr.anchorMin = Vector2.zero;
            cr.anchorMax = Vector2.one;
            cr.offsetMin = new Vector2(160f, 20f);
            cr.offsetMax = new Vector2(-160f, -20f);
            caption.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            caption.fontSize = 34;
            caption.fontStyle = FontStyle.Italic;
            caption.alignment = TextAnchor.MiddleCenter;
            caption.horizontalOverflow = HorizontalWrapMode.Wrap;
            caption.text = string.Empty;
        }

        private static void Stretch(RectTransform r)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = r.offsetMax = Vector2.zero;
        }

        /// <summary>Stand-in until the painting exists: a soft amber dawn fading to night blue.</summary>
        private static Texture2D DuskWash()
        {
            const int w = 4, h = 64;
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < h; y++)
            {
                float k = y / (h - 1f);
                Color c = Color.Lerp(new Color(0.85f, 0.55f, 0.28f), new Color(0.08f, 0.09f, 0.2f), Mathf.Pow(k, 0.8f));
                for (int x = 0; x < w; x++) t.SetPixel(x, y, c);
            }
            t.Apply();
            return t;
        }
    }
}
