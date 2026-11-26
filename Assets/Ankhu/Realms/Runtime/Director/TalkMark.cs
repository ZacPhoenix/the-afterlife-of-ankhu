using System.Collections.Generic;
using Ankhu.Realms.Core;
using UnityEngine;

namespace Ankhu.Realms
{
    /// <summary>
    /// Zac 2026-11-26: when one of the statues or guardians speaks, painted sound rings ripple out beside its head
    /// so the player can tell which figure is talking. Anpu and Ankhu have none (they are the cast). The builder puts
    /// a few ring quads (painted fx_soundring) under this component, and the director turns a speaker's marks on for
    /// the length of each line it says.
    /// </summary>
    public sealed class TalkMark : MonoBehaviour
    {
        public Speaker speaker = Speaker.Other;
        [Tooltip("Ring size at its widest, in metres.")]
        public float ringSize = 0.9f;
        [Tooltip("Seconds for one ring to grow and fade.")]
        public float period = 1.2f;

        private static readonly List<TalkMark> Live = new List<TalkMark>();
        private static readonly int TintId = Shader.PropertyToID("_Tint");

        private Renderer[] rings;
        private Color[] baseTint;
        private MaterialPropertyBlock block;
        private float level, target;

        /// <summary>Turns every mark of this speaker on or off. A speaker with no mark is ignored.</summary>
        public static void Set(Speaker who, bool on)
        {
            foreach (TalkMark m in Live)
                if (m.speaker == who) m.target = on ? 1f : 0f;
        }

        public static void ClearAll()
        {
            foreach (TalkMark m in Live) m.target = 0f;
        }

        private void Awake()
        {
            rings = GetComponentsInChildren<Renderer>(true);
            baseTint = new Color[rings.Length];
            for (int i = 0; i < rings.Length; i++)
            {
                Material m = rings[i].sharedMaterial;
                baseTint[i] = m != null && m.HasProperty(TintId) ? m.GetColor(TintId) : Color.white;
            }
            block = new MaterialPropertyBlock();
            Apply(0f);
        }

        private void OnEnable() { if (!Live.Contains(this)) Live.Add(this); }

        private void OnDisable()
        {
            Live.Remove(this);
            level = target = 0f;
            Apply(0f);
        }

        private void Update()
        {
            level = Mathf.MoveTowards(level, target, Time.deltaTime * (target > level ? 5f : 2f));
            Apply(Time.time);
        }

        private void Apply(float t)
        {
            if (rings == null) return;
            for (int i = 0; i < rings.Length; i++)
            {
                Renderer r = rings[i];
                if (r == null) continue;
                bool on = level > 0.001f;
                r.enabled = on;
                if (!on) continue;
                // Each ring starts small at the head, grows and fades, one after another.
                float p = Mathf.Repeat(t / Mathf.Max(0.1f, period) + i / (float)rings.Length, 1f);
                float s = ringSize * Mathf.Lerp(0.3f, 1f, p);
                r.transform.localScale = new Vector3(s, s, 1f);
                Color c = baseTint[i];
                c.a *= level * Mathf.SmoothStep(0f, 1f, p / 0.15f) * (1f - p);
                r.GetPropertyBlock(block);
                block.SetColor(TintId, c);
                r.SetPropertyBlock(block);
            }
        }
    }
}
