using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Ankhu.Opening
{
    /// <summary>
    /// The title screen, built in code from painted layers (back to front) of the night temple title art.
    /// Each layer shifts with the pointer or right stick by its depth (parallax). The glow layers flicker like
    /// brazier fire. Dust drifts in the portal light. Menu: Begin Journey, Continue (when a save exists),
    /// Settings, Quit. F9 opens Borrowed Dawn (the greybox) for the team. Keyboard, mouse and gamepad all navigate.
    /// </summary>
    public sealed class TitleMenu : MonoBehaviour
    {
        [Serializable]
        public struct Layer
        {
            public Texture2D texture;
            [Tooltip("0 = far (moves least), 1 = near.")] public float depth;
            [Tooltip("Flicker amount for fire glow layers, 0 for none.")] public float flicker;
            public bool additive;
        }

        [SerializeField] private Layer[] layers = Array.Empty<Layer>();
        [SerializeField] private Texture2D logo;
        [SerializeField] private Texture2D logoPool;
        [SerializeField] private Texture2D wingedSun;
        [SerializeField] private Texture2D ankhDivider;
        // The logo art is bright gold. The canon is aged dark bronze, so it is toned down here.
        [SerializeField] private Color logoTint = new Color(0.86f, 0.79f, 0.68f, 1f);
        [SerializeField] private AudioClip ambient;
        [SerializeField] private float parallaxPixels = 26f;
        [SerializeField] private Rect dustArea = new Rect(0.38f, 0.18f, 0.24f, 0.62f);

        private readonly List<RawImage> layerImages = new List<RawImage>();
        private readonly List<RectTransform> dust = new List<RectTransform>();
        private readonly List<Vector3> dustState = new List<Vector3>();
        private RectTransform root;
        private RectTransform menuPanel;
        private RectTransform settingsPanel;
        private RawImage logoImage;
        private RawImage poolImage;
        private RawImage sunImage;
        private RawImage dividerImage;
        private CanvasGroup menuGroup;
        private Vector2 look;
        private AudioSource ambientSource;
        private float startTime;
        private MenuItem firstItem;
        private MenuItem firstSetting;
        private Material additiveMat;

        private void Start()
        {
            startTime = Time.unscaledTime;
            EnsureEventSystem();
            Canvas c = UiKit.Canvas("Title", 0);
            root = (RectTransform)c.transform;
            BuildLayers();
            BuildDust();
            BuildLogo();
            BuildMenu();
            BuildSettings();
            ShowSettings(false);

            if (ambient != null)
            {
                ambientSource = gameObject.AddComponent<AudioSource>();
                ambientSource.clip = ambient;
                ambientSource.loop = true;
                ambientSource.volume = 0f;
                ambientSource.Play();
            }
            SceneFader.SetBlack(1f);
            SceneFader.FadeIn(2.2f);
        }

        private static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            go.GetComponent<EventSystem>().sendNavigationEvents = true;
        }

        private void BuildLayers()
        {
            RectTransform holder = UiKit.Rect("Layers", root);
            UiKit.Stretch(holder);
            var aspect = holder.gameObject.AddComponent<AspectRatioFitter>();
            aspect.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            aspect.aspectRatio = layers.Length > 0 && layers[0].texture != null ? (float)layers[0].texture.width / layers[0].texture.height : 16f / 9f;
            foreach (Layer l in layers)
            {
                RawImage img = UiKit.Raw(l.texture != null ? l.texture.name : "Layer", holder, l.texture);
                UiKit.Stretch(img.rectTransform, -parallaxPixels * 1.6f);
                if (l.additive)
                {
                    if (additiveMat == null)
                    {
                        Shader s = Shader.Find("UI/Additive") ?? Shader.Find("Legacy Shaders/Particles/Additive");
                        if (s != null) additiveMat = new Material(s);
                    }
                    if (additiveMat != null) img.material = additiveMat;
                }
                layerImages.Add(img);
            }
            // Bottom shade so the menu reads over the floor, not over the lit doorway.
            RawImage vignette = UiKit.Raw("MenuShade", root, UiKit.TitleShade());
            UiKit.Stretch(vignette.rectTransform);
            vignette.raycastTarget = false;
        }

        private void BuildDust()
        {
            Texture2D dot = UiKit.SoftDot(32, 2.2f);
            var rng = new System.Random(25);
            for (int i = 0; i < 70; i++)
            {
                RawImage d = UiKit.Raw("Mote", root, dot);
                float size = 2f + (float)rng.NextDouble() * 5f;
                d.rectTransform.anchorMin = d.rectTransform.anchorMax = Vector2.zero;
                d.rectTransform.sizeDelta = new Vector2(size, size);
                d.color = new Color(1f, 0.86f, 0.62f, 0f);
                dust.Add(d.rectTransform);
                dustState.Add(new Vector3((float)rng.NextDouble(), (float)rng.NextDouble(), (float)rng.NextDouble() * 100f));
            }
        }

        private void BuildLogo()
        {
            if (logo == null) return;
            // Winged sun, title words and ankh divider are one group: centred on the logo axis, evenly spaced, one fade.
            RectTransform group = UiKit.Rect("LogoGroup", root);
            UiKit.Stretch(group);
            float w = 760f;
            float h = w * logo.height / Mathf.Max(1f, logo.width);
            const float logoY = -78f;
            // Where the lettering sits inside Logo.png (alpha bounds of the 2206x713 art).
            float wordsW = w * (2098f - 71f) / 2206f;
            float wordsTop = -logoY + h * 117f / 713f;
            float wordsBottom = -logoY + h * 624f / 713f;
            const float gap = 20f;

            float sunTop = wordsTop, divBottom = wordsBottom;
            float sw = 0f, sh = 0f, dw = 0f, dh = 0f;
            if (wingedSun != null)
            {
                sw = wordsW * 0.6f;
                sh = sw * wingedSun.height / Mathf.Max(1f, wingedSun.width);
                sunTop = wordsTop - gap - sh;
            }
            if (ankhDivider != null)
            {
                // About the word width. Its bar lands just under the painted moon, so it never crosses it.
                dw = wordsW;
                dh = dw * ankhDivider.height / Mathf.Max(1f, ankhDivider.width);
                divBottom = wordsBottom + gap + dh;
            }

            // A soft dark pool behind the whole group so it reads off the lit lintel and the night sky.
            if (logoPool != null)
            {
                poolImage = UiKit.Raw("LogoPool", group, logoPool);
                float ph = (divBottom - sunTop) * 1.35f;
                UiKit.Place(poolImage.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -((sunTop + divBottom) * 0.5f - ph * 0.5f)), new Vector2(w * 1.6f, ph));
                poolImage.color = new Color(1f, 1f, 1f, 0f);
            }
            if (wingedSun != null)
            {
                sunImage = UiKit.Raw("WingedSun", group, wingedSun);
                UiKit.Place(sunImage.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -sunTop), new Vector2(sw, sh));
                sunImage.color = new Color(1f, 1f, 1f, 0f);
            }
            logoImage = UiKit.Raw("Logo", group, logo);
            UiKit.Place(logoImage.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, logoY), new Vector2(w, h));
            logoImage.color = new Color(1f, 1f, 1f, 0f);
            if (ankhDivider != null)
            {
                dividerImage = UiKit.Raw("AnkhDivider", group, ankhDivider);
                UiKit.Place(dividerImage.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -(wordsBottom + gap)), new Vector2(dw, dh));
                dividerImage.color = new Color(1f, 1f, 1f, 0f);
            }
        }

        private void BuildMenu()
        {
            menuPanel = UiKit.Rect("Menu", root);
            // Lower left, over the dark floor and pillar base, clear of the doorway and the figure in it.
            UiKit.Place(menuPanel, new Vector2(0f, 0f), new Vector2(56f, 48f), new Vector2(660f, 300f));
            menuGroup = menuPanel.gameObject.AddComponent<CanvasGroup>();
            menuGroup.alpha = 0f;
            var items = new List<MenuItem>();
            items.Add(MenuItem.Create(menuPanel, "Begin Journey", BeginJourney));
            if (OpeningSettings.HasSave) items.Add(MenuItem.Create(menuPanel, "Continue", Continue));
            items.Add(MenuItem.Create(menuPanel, "Settings", () => ShowSettings(true)));
            items.Add(MenuItem.Create(menuPanel, "Quit", Quit));
            MenuItem.Stack(items, OpeningSettings.HasSave ? 68f : 74f);
            firstItem = items[0];
        }

        private void BuildSettings()
        {
            settingsPanel = UiKit.Rect("Settings", root);
            UiKit.Place(settingsPanel, new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(760f, 360f));
            var items = new List<MenuItem>
            {
                MenuItem.Create(settingsPanel, "", null),
                MenuItem.Create(settingsPanel, "", null),
                MenuItem.Create(settingsPanel, "", null),
                MenuItem.Create(settingsPanel, "", null),
                MenuItem.Create(settingsPanel, "Back", () => ShowSettings(false)),
            };
            items[0].Bind(() => "Master Volume   " + Mathf.RoundToInt(OpeningSettings.Master * 100f), d => OpeningSettings.Master = Step(OpeningSettings.Master, d));
            items[1].Bind(() => "Music Volume   " + Mathf.RoundToInt(OpeningSettings.Music * 100f), d => { OpeningSettings.Music = Step(OpeningSettings.Music, d); if (ambientSource != null) ambientSource.volume = 0.5f * OpeningSettings.Music; });
            items[2].Bind(() => "Subtitles   " + (OpeningSettings.Subtitles ? "On" : "Off"), d => OpeningSettings.Subtitles = !OpeningSettings.Subtitles);
            items[3].Bind(() => "Reduced Motion   " + (OpeningSettings.ReducedMotion ? "On" : "Off"), d => OpeningSettings.ReducedMotion = !OpeningSettings.ReducedMotion);
            MenuItem.Stack(items, 58f);
            firstSetting = items[0];
        }

        private static float Step(float v, int dir)
        {
            float n = Mathf.Round(v * 10f) / 10f + 0.1f * (dir == 0 ? 1 : dir);
            if (n > 1.001f) n = 0f;
            if (n < -0.001f) n = 1f;
            return Mathf.Clamp01(n);
        }

        private void ShowSettings(bool on)
        {
            settingsPanel.gameObject.SetActive(on);
            menuPanel.gameObject.SetActive(!on);
            PlayerPrefs.Save();
            MenuItem target = on ? firstSetting : firstItem;
            if (EventSystem.current != null && target != null) EventSystem.current.SetSelectedGameObject(target.gameObject);
        }

        private void Update()
        {
            // The Borrowed Dawn greybox stays reachable for the team, off the player-facing menu: F9 on the title.
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.f9Key.wasPressedThisFrame) SceneFader.FadeTo(OpeningSettings.GreyboxScene, 1f, 0.8f);
            float t = Time.unscaledTime - startTime;
            bool calm = OpeningSettings.ReducedMotion;
            Vector2 want = calm ? Vector2.zero : OpeningInput.Look();
            look = Vector2.Lerp(look, want, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 2.5f));
            Vector2 drift = calm ? Vector2.zero : new Vector2(Mathf.Sin(t * 0.11f), Mathf.Sin(t * 0.07f + 1f)) * 0.25f;

            for (int i = 0; i < layerImages.Count; i++)
            {
                Layer l = layers[i];
                Vector2 off = -(look + drift) * parallaxPixels * l.depth;
                layerImages[i].rectTransform.anchoredPosition = off;
                if (l.flicker > 0f)
                {
                    float n = Mathf.PerlinNoise(t * 3.1f, i * 7.3f) * 0.65f + Mathf.PerlinNoise(t * 9.7f, i * 3.1f) * 0.35f;
                    float a = Mathf.Clamp01(1f - l.flicker + l.flicker * 2f * n);
                    layerImages[i].color = new Color(1f, 1f, 1f, a);
                }
            }

            float reveal = Mathf.Clamp01((t - 1.2f) / 2.5f);
            float ra = Mathf.SmoothStep(0f, 1f, reveal);
            if (logoImage != null) logoImage.color = new Color(logoTint.r, logoTint.g, logoTint.b, ra);
            if (poolImage != null) poolImage.color = new Color(1f, 1f, 1f, ra);
            if (sunImage != null) sunImage.color = new Color(logoTint.r, logoTint.g, logoTint.b, ra);
            if (dividerImage != null) dividerImage.color = new Color(logoTint.r, logoTint.g, logoTint.b, ra);
            menuGroup.alpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 2.6f) / 1.5f));
            if (ambientSource != null) ambientSource.volume = Mathf.Min(0.5f * OpeningSettings.Music, t * 0.15f);
            if (t > 2.6f && EventSystem.current != null && EventSystem.current.currentSelectedGameObject == null && firstItem != null && menuPanel.gameObject.activeSelf)
            {
                EventSystem.current.SetSelectedGameObject(firstItem.gameObject);
            }
            if (settingsPanel.gameObject.activeSelf && OpeningInput.BackPressed()) ShowSettings(false);

            UpdateDust(t, calm);
        }

        private void UpdateDust(float t, bool calm)
        {
            Vector2 size = root.rect.size;
            for (int i = 0; i < dust.Count; i++)
            {
                Vector3 s = dustState[i];
                float speed = calm ? 0.004f : 0.012f;
                float y = Mathf.Repeat(s.y + t * speed * (0.5f + s.x), 1f);
                float x = s.x + Mathf.Sin(t * 0.3f + s.z) * 0.04f;
                float px = (dustArea.x + x * dustArea.width) * size.x - look.x * parallaxPixels * 0.8f;
                float py = (dustArea.y + y * dustArea.height) * size.y - look.y * parallaxPixels * 0.8f;
                dust[i].anchoredPosition = new Vector2(px, py);
                float edge = Mathf.Sin(y * Mathf.PI);
                float tw = 0.5f + 0.5f * Mathf.Sin(t * (1.3f + s.x) + s.z);
                var img = dust[i].GetComponent<RawImage>();
                img.color = new Color(1f, 0.86f, 0.62f, 0.55f * edge * tw * Mathf.Clamp01(t / 3f));
            }
        }

        private void BeginJourney()
        {
            if (SceneFader.Busy) return;
            StartCoroutine(FadeAmbient(1.6f));
            SceneFader.FadeTo(OpeningSettings.CinematicScene, 1.8f, 0f);
        }

        private void Continue()
        {
            if (SceneFader.Busy) return;
            StartCoroutine(FadeAmbient(1.2f));
            WalkHandoff.FromCinematic = false;
            SceneFader.FadeTo(OpeningSettings.WalkScene, 1.2f, 1.2f);
        }

        private System.Collections.IEnumerator FadeAmbient(float seconds)
        {
            if (ambientSource == null) yield break;
            float v0 = ambientSource.volume;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                ambientSource.volume = Mathf.Lerp(v0, 0f, t / seconds);
                yield return null;
            }
            ambientSource.volume = 0f;
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }

    /// <summary>One text menu entry: gold when selected, with a dark band, a gold diamond and a rule under the word. Left and right step values.</summary>
    public sealed class MenuItem : Selectable, ISubmitHandler, IPointerClickHandler, IMoveHandler
    {
        private Text label;
        private Image rule;
        private Image band;
        private Image marker;
        private const float TextInset = 50f;
        private Action onSubmit;
        private Func<string> text;
        private Action<int> onStep;
        private float glow;
        private bool selected;

        public static MenuItem Create(Transform parent, string caption, Action submit)
        {
            RectTransform r = UiKit.Rect("Item_" + caption, parent);
            var hit = r.gameObject.AddComponent<Image>();
            hit.color = new Color(0f, 0f, 0f, 0f);
            var item = r.gameObject.AddComponent<MenuItem>();
            item.targetGraphic = hit;
            item.transition = Transition.None;
            // Selected state: a soft dark band behind the row, a gold diamond at the left and a rule the width of the word.
            item.band = UiKit.Solid("Band", r, new Color(0f, 0f, 0f, 0f));
            UiKit.Stretch(item.band.rectTransform);
            item.marker = UiKit.Solid("Marker", r, UiKit.Gold);
            UiKit.Place(item.marker.rectTransform, new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(11f, 11f));
            item.marker.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            item.marker.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            item.label = UiKit.Label("Label", r, caption.ToUpperInvariant(), UiKit.Caps, 44, UiKit.GoldDim, TextAnchor.MiddleLeft);
            UiKit.Stretch(item.label.rectTransform);
            item.label.rectTransform.offsetMin = new Vector2(TextInset, 0f);
            var outline = item.label.gameObject.AddComponent<Shadow>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(2f, -2.5f);
            var rim = item.label.gameObject.AddComponent<Outline>();
            rim.effectColor = new Color(0f, 0f, 0f, 0.55f);
            rim.effectDistance = new Vector2(1f, -1f);
            item.rule = UiKit.Solid("Rule", r, UiKit.Gold);
            UiKit.Place(item.rule.rectTransform, new Vector2(0f, 0f), new Vector2(TextInset, 6f), new Vector2(0f, 2f));
            item.rule.rectTransform.pivot = new Vector2(0f, 0f);
            item.onSubmit = submit;
            return item;
        }

        public void Bind(Func<string> textFn, Action<int> step)
        {
            text = textFn;
            onStep = step;
            onSubmit = () => step(0);
            Refresh();
        }

        public static void Stack(List<MenuItem> items, float spacing)
        {
            for (int i = 0; i < items.Count; i++)
            {
                RectTransform r = (RectTransform)items[i].transform;
                UiKit.Place(r, new Vector2(0.5f, 1f), new Vector2(0f, -i * spacing), new Vector2(620f, spacing - 6f));
                Navigation n = new Navigation { mode = Navigation.Mode.Explicit };
                n.selectOnUp = items[(i - 1 + items.Count) % items.Count];
                n.selectOnDown = items[(i + 1) % items.Count];
                items[i].navigation = n;
            }
        }

        private void Refresh()
        {
            if (text != null && label != null) label.text = text().ToUpperInvariant();
        }

        public override void OnSelect(BaseEventData eventData) { base.OnSelect(eventData); selected = true; }
        public override void OnDeselect(BaseEventData eventData) { base.OnDeselect(eventData); selected = false; }
        public override void OnPointerEnter(PointerEventData eventData) { base.OnPointerEnter(eventData); Select(); }

        public void OnSubmit(BaseEventData eventData) { Fire(); }
        public void OnPointerClick(PointerEventData eventData) { Fire(); }

        public override void OnMove(AxisEventData eventData)
        {
            if (onStep != null && (eventData.moveDir == MoveDirection.Left || eventData.moveDir == MoveDirection.Right))
            {
                onStep(eventData.moveDir == MoveDirection.Left ? -1 : 1);
                Refresh();
                return;
            }
            base.OnMove(eventData);
        }

        private void Fire()
        {
            if (!IsInteractable() || SceneFader.Busy) return;
            onSubmit?.Invoke();
            Refresh();
        }

        private void Update()
        {
            glow = Mathf.MoveTowards(glow, selected ? 1f : 0f, Time.unscaledDeltaTime * 5f);
            if (label != null) label.color = Color.Lerp(UiKit.GoldDim, UiKit.Gold, glow);
            float k = Mathf.SmoothStep(0f, 1f, glow);
            if (rule != null && label != null) rule.rectTransform.sizeDelta = new Vector2(label.preferredWidth * k, 2f);
            if (band != null) band.color = new Color(0f, 0f, 0f, 0.42f * k);
            if (marker != null) marker.color = new Color(UiKit.Gold.r, UiKit.Gold.g, UiKit.Gold.b, k);
        }
    }
}
