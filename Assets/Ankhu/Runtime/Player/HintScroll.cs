using UnityEngine;
using UnityEngine.UI;

namespace Ankhu.Player
{
    /// <summary>
    /// Zac 2026-11-25, hint pick B: a papyrus scroll titled HINT with three wax seals, a nudge, a direction and
    /// the answer. Each H press breaks the next seal, so the player chooses how much to learn. The seals close
    /// again when the room's state changes (new tiers), and the scroll rolls away after a while untouched.
    /// Built at runtime under any canvas so teaching rooms and realms share it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HintScroll : MonoBehaviour
    {
        private const float ShowSeconds = 14f;
        private const float Width = 620f;
        private static readonly string[] StepNames = { "NUDGE", "DIRECTION", "ANSWER" };

        private RectTransform root;
        private CanvasGroup group;
        private readonly RectTransform[] seals = new RectTransform[3];
        private readonly RectTransform[] halves = new RectTransform[6];
        private readonly Text[] bodies = new Text[3];
        private readonly Text[] names = new Text[3];
        private readonly float[] breakT = new float[3];
        private string[] tiers = new string[3];
        private int broken;
        private float shownFor = -1f;
        private float showT;

        public int Broken => broken;
        public bool Showing => shownFor >= 0f;

        public static HintScroll Install(RectTransform canvas)
        {
            HintScroll s = canvas.GetComponent<HintScroll>();
            if (s == null) s = canvas.gameObject.AddComponent<HintScroll>();
            s.Build(canvas);
            return s;
        }

        private void Build(RectTransform canvas)
        {
            if (root != null) return;
            root = PapyrusKit.Rect("Hint Scroll", canvas);
            PapyrusKit.Place(root, new Vector2(1f, 1f), new Vector2(-24f, -150f), new Vector2(Width, 420f));
            RectTransform sheet = PapyrusKit.Sheet("Sheet", root, 32f);
            PapyrusKit.Stretch(sheet);
            Text title = PapyrusKit.Label("Title", root, "HINT", PapyrusKit.Caps, 32, PapyrusKit.Rubric, TextAnchor.UpperCenter);
            PapyrusKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(Width - 60f, 40f));
            Image rule = PapyrusKit.Image("Rule", root, null, new Color(PapyrusKit.Rubric.r, PapyrusKit.Rubric.g, PapyrusKit.Rubric.b, 0.7f));
            PapyrusKit.Place(rule.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(Width - 140f, 2f));

            for (int i = 0; i < 3; i++)
            {
                RectTransform step = PapyrusKit.Rect("Step " + (i + 1), root);
                PapyrusKit.Place(step, new Vector2(0f, 1f), new Vector2(0f, -86f - i * 104f), new Vector2(Width, 100f));
                RectTransform seal = PapyrusKit.Seal(step, 46f);
                seal.anchorMin = seal.anchorMax = new Vector2(0f, 0.5f);
                seal.anchoredPosition = new Vector2(64f, 4f);
                Text mark = PapyrusKit.Label("Num", seal, (i + 1).ToString(), PapyrusKit.Caps, 20, new Color(1f, 0.86f, 0.70f, 0.9f), TextAnchor.MiddleCenter);
                PapyrusKit.Stretch(mark.rectTransform);
                seals[i] = seal;
                for (int h = 0; h < 2; h++)
                {
                    RectTransform half = PapyrusKit.Rect("Broken " + h, step);
                    half.anchorMin = half.anchorMax = new Vector2(0f, 0.5f);
                    half.pivot = new Vector2(0.5f, 0.5f);
                    half.anchoredPosition = new Vector2(64f, 4f);
                    half.sizeDelta = new Vector2(46f, 46f);
                    var mask = half.gameObject.AddComponent<RectMask2D>();
                    mask.padding = h == 0 ? new Vector4(0f, 0f, 24f, 0f) : new Vector4(24f, 0f, 0f, 0f);
                    RectTransform inner = PapyrusKit.Seal(half, 46f);
                    inner.anchoredPosition = Vector2.zero;
                    half.gameObject.SetActive(false);
                    halves[i * 2 + h] = half;
                }
                Text name = PapyrusKit.Label("Name", step, StepNames[i], PapyrusKit.Caps, 18, PapyrusKit.Rubric, TextAnchor.UpperLeft);
                PapyrusKit.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(112f, -8f), new Vector2(Width - 150f, 24f));
                names[i] = name;
                Text body = PapyrusKit.Label("Body", step, "", PapyrusKit.Serif, 25, PapyrusKit.Ink, TextAnchor.UpperLeft);
                body.horizontalOverflow = HorizontalWrapMode.Wrap;
                body.lineSpacing = 0.95f;
                PapyrusKit.Place(body.rectTransform, new Vector2(0f, 1f), new Vector2(112f, -32f), new Vector2(Width - 150f, 66f));
                bodies[i] = body;
            }
            group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            root.gameObject.SetActive(false);
            Refresh();
        }

        /// <summary>
        /// Sets the three tiers. When they differ from the shown ones (the room changed) every seal closes again.
        /// A single string (no tiers available) fills the nudge only and seals the rest with a quiet note.
        /// </summary>
        public void SetTiers(string[] next)
        {
            if (next == null || next.Length == 0) return;
            var t = new string[3];
            for (int i = 0; i < 3; i++) t[i] = i < next.Length ? next[i] : null;
            bool same = true;
            for (int i = 0; i < 3; i++) same &= t[i] == tiers[i];
            if (same) return;
            tiers = t;
            broken = 0;
            for (int i = 0; i < 3; i++) breakT[i] = 0f;
            Refresh();
        }

        /// <summary>The H press: opens the scroll and breaks the next seal, or just shows it again when all are open.</summary>
        public void BreakNext()
        {
            if (root == null) return;
            int available = 0;
            for (int i = 0; i < 3; i++) if (!string.IsNullOrEmpty(tiers[i])) available = i + 1;
            if (Showing && broken < available)
            {
                breakT[broken] = 0.0001f;
                broken++;
            }
            else if (!Showing && broken == 0 && available > 0)
            {
                breakT[0] = 0.0001f;
                broken = 1;
            }
            shownFor = 0f;
            root.gameObject.SetActive(true);
            Refresh();
        }

        public void Hide()
        {
            shownFor = -1f;
        }

        private void Refresh()
        {
            for (int i = 0; i < 3; i++)
            {
                bool open = i < broken;
                bool exists = !string.IsNullOrEmpty(tiers[i]);
                seals[i].gameObject.SetActive(!open);
                halves[i * 2].gameObject.SetActive(open);
                halves[i * 2 + 1].gameObject.SetActive(open);
                if (open)
                {
                    bodies[i].text = tiers[i];
                    bodies[i].color = PapyrusKit.Ink;
                    bodies[i].fontStyle = i == 2 ? FontStyle.Bold : FontStyle.Normal;
                }
                else
                {
                    bodies[i].text = !exists ? "" : (i == broken ? "Press H to break this seal." : "Sealed.");
                    bodies[i].color = PapyrusKit.InkFaint;
                    bodies[i].fontStyle = FontStyle.Italic;
                }
                names[i].color = exists ? PapyrusKit.Rubric : PapyrusKit.InkFaint;
            }
        }

        private void Update()
        {
            if (root == null) return;
            float dt = Time.unscaledDeltaTime;
            bool want = shownFor >= 0f;
            if (want)
            {
                shownFor += dt;
                if (shownFor > ShowSeconds) shownFor = -1f;
            }
            showT = Mathf.MoveTowards(showT, want ? 1f : 0f, dt / (want ? 0.22f : 0.45f));
            group.alpha = showT;
            float e = 1f - (1f - showT) * (1f - showT);
            root.localScale = new Vector3(1f, Mathf.Lerp(0.55f, 1f, e), 1f);
            if (showT <= 0f && root.gameObject.activeSelf) root.gameObject.SetActive(false);

            for (int i = 0; i < 3; i++)
            {
                if (breakT[i] <= 0f || breakT[i] >= 1f) continue;
                breakT[i] = Mathf.Min(1f, breakT[i] + dt / 0.35f);
                float k = 1f - (1f - breakT[i]) * (1f - breakT[i]);
                RectTransform l = halves[i * 2], r = halves[i * 2 + 1];
                l.anchoredPosition = new Vector2(64f - 9f * k, 4f - 5f * k);
                r.anchoredPosition = new Vector2(64f + 9f * k, 4f - 3f * k);
                l.localRotation = Quaternion.Euler(0f, 0f, 16f * k);
                r.localRotation = Quaternion.Euler(0f, 0f, -12f * k);
            }
        }
    }
}
