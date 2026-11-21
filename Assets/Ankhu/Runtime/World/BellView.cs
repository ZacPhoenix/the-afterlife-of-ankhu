using Ankhu.Integration;
using UnityEngine;

namespace Ankhu.World
{
    /// <summary>
    /// The bronze bell. Cracked and dull until <see cref="RoomSnapshot.BellRepaired"/>.
    /// Sways once when <see cref="RoomSnapshot.BellStruck"/> turns on and rings continuously
    /// (sway plus expanding sound rings) while <see cref="RoomSnapshot.BellEchoing"/>.
    /// The root is the hanging pivot.
    /// </summary>
    public sealed class BellView : SnapshotListener
    {
        [SerializeField] private Transform swing;
        [SerializeField] private Renderer[] bellBody = new Renderer[0];
        [SerializeField] private GameObject crack;
        [Tooltip("Unrotated holders, each with a disc renderer child. Holders are scaled in x and y.")]
        [SerializeField] private Transform[] soundRings = new Transform[0];
        [SerializeField] private float ringSwayDegrees = 6f;
        [SerializeField] private float strikeSwayDegrees = 12f;
        [SerializeField] private float strikeDecaySeconds = 1.6f;

        private bool echoing;
        private float strikeTime = -100f;
        private Renderer[] ringRenderers = new Renderer[0];

        public void Configure(Transform swingRoot, Renderer[] body, GameObject crackVisual, Transform[] rings)
        {
            swing = swingRoot;
            bellBody = body ?? new Renderer[0];
            crack = crackVisual;
            soundRings = rings ?? new Transform[0];
        }

        private void Awake()
        {
            ringRenderers = new Renderer[soundRings.Length];
            for (int i = 0; i < soundRings.Length; i++)
            {
                ringRenderers[i] = soundRings[i] != null ? soundRings[i].GetComponentInChildren<Renderer>(true) : null;
            }
        }

        protected override void Apply(RoomSnapshot previous, RoomSnapshot next, bool instant)
        {
            Color body = next.BellRepaired ? GreyboxPalette.Gold : GreyboxPalette.BronzeDull;
            for (int i = 0; i < bellBody.Length; i++)
            {
                GreyboxRender.Tint(bellBody[i], body);
            }
            GreyboxRender.SetActive(crack, !next.BellRepaired);

            if (!instant && previous != null && !previous.BellStruck && next.BellStruck)
            {
                strikeTime = Time.time;
            }
            echoing = next.BellEchoing;
            for (int i = 0; i < ringRenderers.Length; i++)
            {
                GreyboxRender.SetVisible(ringRenderers[i], echoing);
            }
        }

        private void Update()
        {
            float angle = 0f;
            float sinceStrike = Time.time - strikeTime;
            if (sinceStrike < strikeDecaySeconds)
            {
                float decay = 1f - sinceStrike / strikeDecaySeconds;
                angle += strikeSwayDegrees * decay * Mathf.Sin(sinceStrike * 14f);
            }
            if (echoing)
            {
                angle += ringSwayDegrees * Mathf.Sin(Time.time * 9f);
                for (int i = 0; i < soundRings.Length; i++)
                {
                    if (soundRings[i] == null)
                    {
                        continue;
                    }
                    float phase = Mathf.Repeat(Time.time * 0.8f + i / (float)Mathf.Max(1, soundRings.Length), 1f);
                    float scale = Mathf.Lerp(1f, 2.6f, phase);
                    Vector3 s = soundRings[i].localScale;
                    soundRings[i].localScale = new Vector3(scale, scale, s.z);
                    GreyboxRender.Tint(ringRenderers[i], GreyboxPalette.WithAlpha(GreyboxPalette.Gold, 0.5f * (1f - phase)));
                }
            }
            if (swing != null)
            {
                swing.localRotation = Quaternion.Euler(0f, 0f, angle);
            }
        }
    }
}
