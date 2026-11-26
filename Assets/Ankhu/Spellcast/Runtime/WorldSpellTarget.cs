using System.Collections.Generic;
using Ankhu.Realms;
using UnityEngine;

namespace Ankhu.Spellcast
{
    /// <summary>
    /// Makes any room object a landing place for the words of power without changing the object. The glyphs aim at
    /// the upper middle of its renderers (painted or greybox, shown or hidden), and on arrival it answers with a
    /// golden strike: a burst of motes and sparks, a ring, a flare that blooms and fades, a short warm light and,
    /// when the object carries <see cref="GoldMagic"/>, that component's full flare as well.
    /// </summary>
    public sealed class WorldSpellTarget : ISpellTarget
    {
        private readonly Transform root;
        private readonly List<Renderer> renderers = new List<Renderer>();
        private Vector3 lastAnchor;
        private float size = 1f;

        public string TargetId { get; }

        public WorldSpellTarget(string id, Transform target)
        {
            TargetId = id ?? string.Empty;
            root = target;
            if (root != null)
            {
                root.GetComponentsInChildren(true, renderers);
                // Leave out effects and UI that may hang under the object.
                renderers.RemoveAll(r => r == null || r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer);
                lastAnchor = root.position;
            }
            Measure();
        }

        public Vector3 GlyphAnchor
        {
            get
            {
                Measure();
                return lastAnchor;
            }
        }

        /// <summary>The object's rough size in metres (largest bounds axis), used to scale the strike.</summary>
        public float Size => size;

        private void Measure()
        {
            if (root == null)
            {
                return;
            }
            bool any = false;
            Bounds b = default;
            // Prefer what is on screen. Fall back to hidden renderers (a greybox mesh under a painted layer).
            for (int pass = 0; pass < 2 && !any; pass++)
            {
                foreach (Renderer r in renderers)
                {
                    if (r == null) continue;
                    bool shown = r.enabled && r.gameObject.activeInHierarchy;
                    if (pass == 0 && !shown) continue;
                    Bounds rb = shown ? r.bounds : TransformBounds(r.transform, r.localBounds);
                    if (rb.size.sqrMagnitude < 1e-6f) continue;
                    if (!any) { b = rb; any = true; }
                    else b.Encapsulate(rb);
                }
            }
            if (!any)
            {
                lastAnchor = root.position + Vector3.up * 0.5f;
                size = 1f;
                return;
            }
            size = Mathf.Clamp(Mathf.Max(b.size.x, b.size.y), 0.4f, 6f);
            // Upper middle: glyphs read as striking the object, not its base.
            lastAnchor = b.center + Vector3.up * (b.extents.y * 0.25f);
        }

        private static Bounds TransformBounds(Transform t, Bounds local)
        {
            Vector3 c = t.TransformPoint(local.center);
            Vector3 e = local.extents;
            Vector3 x = t.TransformVector(new Vector3(e.x, 0f, 0f));
            Vector3 y = t.TransformVector(new Vector3(0f, e.y, 0f));
            Vector3 z = t.TransformVector(new Vector3(0f, 0f, e.z));
            Vector3 ext = new Vector3(
                Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x),
                Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y),
                Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z));
            return new Bounds(c, ext * 2f);
        }

        public void Ignite(float strength)
        {
            Vector3 at = GlyphAnchor;
            float scale = Mathf.Lerp(0.6f, 1.6f, Mathf.InverseLerp(0.4f, 4f, size));
            MagicFx.Burst(at, scale, Mathf.Max(0.2f, strength));
            // A second, wider ring a beat later reads as the power spreading across the object.
            MagicFx.RingAt(at, 2.4f * scale, 0.9f, new Color(MagicPalette.Gold.r, MagicPalette.Gold.g, MagicPalette.Gold.b, 0.6f));
            MagicFx.FlareAt(at, 2.6f * scale, 0.7f, MagicPalette.GoldPale);
            for (int i = 0; i < 10; i++)
            {
                Vector3 p = at + Vector3.Scale(Random.insideUnitSphere, new Vector3(size * 0.5f, size * 0.4f, 0.2f));
                MagicFx.Mote(p, Vector3.up * Random.Range(0.3f, 0.9f), Random.Range(0.04f, 0.09f) * scale, Random.Range(1.0f, 1.8f), MagicPalette.Gold);
            }
            SpellStrikeLight.Flash(at, scale, strength);
            if (root != null)
            {
                GoldMagic gold = root.GetComponentInParent<GoldMagic>();
                if (gold != null) gold.Ignite(strength);
            }
        }
    }

    /// <summary>A warm point light that blooms on a strike and fades out, then removes itself.</summary>
    public sealed class SpellStrikeLight : MonoBehaviour
    {
        private const float Rise = 0.08f;
        private const float Fall = 0.9f;
        private Light lightSource;
        private float start;
        private float peak;

        public static void Flash(Vector3 position, float scale, float strength)
        {
            var go = new GameObject("Spell Strike Light");
            go.transform.position = position + Vector3.back * 0.6f;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.78f, 0.38f);
            l.range = 3.5f * scale;
            l.intensity = 0f;
            l.shadows = LightShadows.None;
            var s = go.AddComponent<SpellStrikeLight>();
            s.lightSource = l;
            s.start = Time.unscaledTime;
            s.peak = 6f * Mathf.Clamp(strength, 0.2f, 1.5f);
        }

        private void Update()
        {
            float t = Time.unscaledTime - start;
            float k = t < Rise ? t / Rise : 1f - Mathf.Clamp01((t - Rise) / Fall);
            lightSource.intensity = peak * k * k;
            if (t >= Rise + Fall)
            {
                Destroy(gameObject);
            }
        }
    }
}
