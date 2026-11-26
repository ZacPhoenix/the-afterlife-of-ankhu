using UnityEngine;
using UnityEngine.UI;

namespace Ankhu.Opening
{
    /// <summary>
    /// Bottom-centre subtitles with a soft fade, plus the 2.39 letterbox bars. Bars can be set instantly or
    /// animated open (the DUAT handoff from the cinematic into the walk). Respects the subtitle setting.
    /// </summary>
    public sealed class SubtitleView : MonoBehaviour
    {
        private Text line;
        private Text shadow;
        // A chapter card in the bottom letterbox bar, so the walk ends on a held beat instead of dropping to the menu.
        private Text card;
        private Text cardSub;
        private float cardAlpha;
        private float cardTarget;
        private Image top;
        private Image bottom;
        private float alpha;
        private float targetAlpha;
        private float hideAt = -1f;
        private float bars;
        private float barsTarget;
        private float barsSpeed = 1f;

        public static SubtitleView Create(int order = 200)
        {
            Canvas c = UiKit.Canvas("Subtitles", order);
            var view = c.gameObject.AddComponent<SubtitleView>();
            view.Build(c.transform);
            return view;
        }

        private void Build(Transform root)
        {
            top = UiKit.Solid("BarTop", root, Color.black);
            bottom = UiKit.Solid("BarBottom", root, Color.black);
            foreach (Image bar in new[] { top, bottom })
            {
                RectTransform r = bar.rectTransform;
                bool isTop = bar == top;
                r.anchorMin = new Vector2(0f, isTop ? 1f : 0f);
                r.anchorMax = new Vector2(1f, isTop ? 1f : 0f);
                r.pivot = new Vector2(0.5f, isTop ? 1f : 0f);
                r.anchoredPosition = Vector2.zero;
                r.sizeDelta = new Vector2(0f, 0f);
            }
            shadow = UiKit.Label("SubtitleShadow", root, "", UiKit.Serif, 40, new Color(0f, 0f, 0f, 0.85f), TextAnchor.LowerCenter);
            UiKit.Place(shadow.rectTransform, new Vector2(0.5f, 0f), new Vector2(2f, 52f), new Vector2(1500f, 140f));
            line = UiKit.Label("Subtitle", root, "", UiKit.Serif, 40, UiKit.Parchment, TextAnchor.LowerCenter);
            UiKit.Place(line.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 54f), new Vector2(1500f, 140f));
            // A thin dark edge so lines hold up over the bright torchlit paving.
            var edge = line.gameObject.AddComponent<Outline>();
            edge.effectColor = new Color(0f, 0f, 0f, 0.7f);
            edge.effectDistance = new Vector2(1.2f, -1.2f);
            card = UiKit.Label("Card", root, "", UiKit.Serif, 46, UiKit.Parchment, TextAnchor.MiddleCenter);
            UiKit.Place(card.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 64f), new Vector2(1500f, 70f));
            cardSub = UiKit.Label("CardSub", root, "", UiKit.Serif, 24, UiKit.Parchment, TextAnchor.MiddleCenter);
            UiKit.Place(cardSub.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(1500f, 40f));
            Apply();
        }

        /// <summary>Fades a chapter card into the bottom bar. Shown whatever the subtitle setting, like a title.</summary>
        public void ShowCard(string title, string sub)
        {
            card.text = title;
            cardSub.text = sub;
            cardTarget = 1f;
        }

        /// <summary>Shows a line for the given seconds. Speaker names are left out, as in film subtitles.</summary>
        public void Show(string text, float seconds)
        {
            if (!OpeningSettings.Subtitles) return;
            line.text = text;
            shadow.text = text;
            targetAlpha = 1f;
            hideAt = Time.unscaledTime + seconds;
        }

        public void Hide()
        {
            targetAlpha = 0f;
            hideAt = -1f;
        }

        /// <summary>Letterbox amount 0 (none) to 1 (full 2.39 bars inside 16:9).</summary>
        public void SetBars(float amount, float seconds)
        {
            barsTarget = Mathf.Clamp01(amount);
            if (seconds <= 0f)
            {
                bars = barsTarget;
                barsSpeed = 1f;
            }
            else
            {
                barsSpeed = Mathf.Abs(barsTarget - bars) / seconds;
            }
        }

        private void Update()
        {
            if (hideAt > 0f && Time.unscaledTime >= hideAt) Hide();
            alpha = Mathf.MoveTowards(alpha, targetAlpha, Time.unscaledDeltaTime * 4f);
            cardAlpha = Mathf.MoveTowards(cardAlpha, cardTarget, Time.unscaledDeltaTime * 0.8f);
            bars = Mathf.MoveTowards(bars, barsTarget, Time.unscaledDeltaTime * Mathf.Max(0.0001f, barsSpeed));
            Apply();
        }

        private void Apply()
        {
            if (line == null) return;
            line.color = new Color(UiKit.Parchment.r, UiKit.Parchment.g, UiKit.Parchment.b, alpha);
            shadow.color = new Color(0f, 0f, 0f, 0.85f * alpha);
            card.color = new Color(UiKit.Parchment.r, UiKit.Parchment.g, UiKit.Parchment.b, cardAlpha);
            cardSub.color = new Color(UiKit.Parchment.r, UiKit.Parchment.g, UiKit.Parchment.b, 0.7f * cardAlpha);
            // 1080 tall reference: a 2.39 image is 1920 / 2.39 = 803 tall, so each bar is 138.
            float h = 138f * Mathf.SmoothStep(0f, 1f, bars);
            top.rectTransform.sizeDelta = new Vector2(0f, h);
            bottom.rectTransform.sizeDelta = new Vector2(0f, h);
            float lift = h > 1f ? h - 20f : 0f;
            line.rectTransform.anchoredPosition = new Vector2(0f, 54f + Mathf.Max(0f, lift - 40f));
            shadow.rectTransform.anchoredPosition = new Vector2(2f, 52f + Mathf.Max(0f, lift - 40f));
        }
    }
}
