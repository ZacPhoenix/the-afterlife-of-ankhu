using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ankhu.Realms
{
    /// <summary>
    /// A world object the words of power can land on. Add with AddComponent, then <see cref="Configure"/>.
    /// <see cref="Ignite"/> plays a golden flare: a blooming point light, an emissive gold wash and a fresnel
    /// rim shell on every child mesh with a band of light sweeping up through it, a light shaft, a ground ring,
    /// rising motes and a few small glyphs orbiting. <see cref="SetHint"/> toggles a gentle idle shimmer that
    /// says "this is magical". Everything is restored and destroyed when idle or disabled.
    /// HDR intensities assume URP Bloom with a threshold near 0.9.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GoldMagic : MonoBehaviour, ISpellTarget
    {
        private static readonly string[] OrbitSigns = { "S34", "N5", "R8", "D4", "S29", "Aa1" };
        private static readonly int RimId = Shader.PropertyToID("_Rim");
        private static readonly int FillId = Shader.PropertyToID("_Fill");
        private static readonly int BandId = Shader.PropertyToID("_Band");
        private static readonly int SweepYId = Shader.PropertyToID("_SweepY");
        private static readonly int SweepWidthId = Shader.PropertyToID("_SweepWidth");
        private const string EmissionKeyword = "_EMISSION";

        [SerializeField] private string targetId;
        [SerializeField] private Vector3 localAnchor = new Vector3(0f, 1f, 0f);

        [Tooltip("Overall brightness of the effect.")]
        public float Intensity = 1f;
        [Tooltip("Hue of the magic. Deep Egyptian gold by default.")]
        public Color Tint = MagicPalette.Gold;
        [Tooltip("Drive _EmissionColor on Lit materials through a MaterialPropertyBlock (instancing a material copy with _EMISSION on when needed).")]
        public bool UseEmission = true;
        [Tooltip("Draw an additive fresnel gold shell over each mesh (shader Ankhu/Realms/GoldRim).")]
        public bool UseRimShell = true;
        [Tooltip("Spawn a blooming point light on Ignite.")]
        public bool UseLight = true;
        [Tooltip("Spawn the vertical light shaft on Ignite.")]
        public bool UseShaft = true;

        private sealed class Slot
        {
            public Renderer Renderer;
            public Material[] Original;
            public Material[] Swapped;
            public List<Material> Instances;
            public bool HadBlock;
            public MaterialPropertyBlock Saved;
            public bool Emissive;
            public Color OriginalEmission;
            public GameObject Shell;
            public Renderer ShellRenderer;
        }

        private sealed class Fx
        {
            public FxQuad Quad;
            public float Start, Life;
            public int Kind;   // 0 ground ring, 1 shaft, 2 orbit glyph
            public float A, B, C;
        }

        private readonly List<Slot> slots = new List<Slot>();
        private readonly List<Fx> fx = new List<Fx>();
        private readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        private readonly MaterialPropertyBlock rimBlock = new MaterialPropertyBlock();
        private Transform fxRoot;
        private Light glowLight;
        private bool active;
        private bool hint;
        private float hintLevel;
        private float igniteStart = float.NegativeInfinity;
        private float igniteStrength;
        private float nextHintMote;
        private Bounds bounds;

        /// <summary>Raised when <see cref="Ignite"/> is called, with the strength. For sfx.</summary>
        public static event System.Action<GoldMagic, float> Ignited;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Ignited = null;

        public void Configure(string id, Vector3 anchor)
        {
            targetId = id;
            localAnchor = anchor;
        }

        public string TargetId => string.IsNullOrEmpty(targetId) ? name : targetId;

        public Vector3 GlyphAnchor => transform.TransformPoint(localAnchor);

        public Vector3 LocalAnchor => localAnchor;

        public bool Hinting => hint;

        /// <summary>True while any effect (ignite or hint) is still visible.</summary>
        public bool Glowing => active;

        private static float Now => Time.unscaledTime;
        private const float IgniteLength = 3.2f;

        // ------------------------------------------------------------------ API

        public void Ignite(float strength)
        {
            strength = Mathf.Clamp(strength, 0f, 2f);
            if (strength <= 0f || !isActiveAndEnabled) return;
            Activate();
            RecomputeBounds();
            igniteStart = Now;
            igniteStrength = strength;
            SpawnIgniteFx(strength);
            try { Ignited?.Invoke(this, strength); } catch (System.Exception e) { Debug.LogException(e, this); }
        }

        /// <summary>Gentle pulsing gold to hint an interactable object. Fades in and out smoothly.</summary>
        public void SetHint(bool on)
        {
            hint = on;
            if (on && isActiveAndEnabled) Activate();
        }

        // ------------------------------------------------------------------ lifecycle

        private void OnDisable()
        {
            Deactivate();
            hintLevel = 0f;
        }

        private void OnDestroy()
        {
            Deactivate();
            if (fxRoot != null) Destroy(fxRoot.gameObject);
        }

        private void Activate()
        {
            if (active) return;
            active = true;
            CollectSlots();
        }

        private void CollectSlots()
        {
            RestoreSlots();
            Renderer[] all = GetComponentsInChildren<Renderer>(false);
            Material rim = UseRimShell ? MagicMaterials.Rim() : null;
            bool any = false;
            foreach (Renderer r in all)
            {
                if (r == null || !(r is MeshRenderer || r is SkinnedMeshRenderer)) continue;
                if (fxRoot != null && r.transform.IsChildOf(fxRoot)) continue;
                if (IsOurs(r)) continue;
                var slot = new Slot { Renderer = r, Original = r.sharedMaterials };
                SetupEmission(slot);
                if (rim != null) SetupShell(slot, rim);
                slots.Add(slot);
                bounds = any ? Encapsulate(bounds, r.bounds) : r.bounds;
                any = true;
            }
            if (!any) bounds = new Bounds(transform.position + Vector3.up * 0.5f, Vector3.one);
        }

        private void RecomputeBounds()
        {
            bool any = false;
            foreach (Slot s in slots)
            {
                if (s.Renderer == null) continue;
                bounds = any ? Encapsulate(bounds, s.Renderer.bounds) : s.Renderer.bounds;
                any = true;
            }
            if (!any) bounds = new Bounds(transform.position + Vector3.up * 0.5f, Vector3.one);
        }

        private static Bounds Encapsulate(Bounds a, Bounds b)
        {
            a.Encapsulate(b);
            return a;
        }

        private static bool IsOurs(Renderer r)
        {
            Material m = r.sharedMaterial;
            if (m == null) return false;
            string n = m.shader != null ? m.shader.name : string.Empty;
            return n == MagicMaterials.RimShaderName || n == MagicMaterials.AdditiveShaderName;
        }

        private void SetupEmission(Slot slot)
        {
            if (!UseEmission) return;
            Material[] mats = slot.Original;
            Material[] swapped = null;
            for (int i = 0; i < mats.Length; i++)
            {
                Material m = mats[i];
                if (m == null || !m.HasProperty(MaterialEmission)) continue;
                slot.Emissive = true;
                slot.OriginalEmission = m.GetColor(MaterialEmission);
                if (!m.IsKeywordEnabled(EmissionKeyword))
                {
                    swapped ??= (Material[])mats.Clone();
                    var inst = new Material(m) { name = m.name + " (Gold Magic)", hideFlags = HideFlags.DontSave };
                    inst.EnableKeyword(EmissionKeyword);
                    inst.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    swapped[i] = inst;
                    (slot.Instances ??= new List<Material>()).Add(inst);
                }
            }
            if (!slot.Emissive) return;
            slot.HadBlock = slot.Renderer.HasPropertyBlock();
            if (slot.HadBlock)
            {
                slot.Saved = new MaterialPropertyBlock();
                slot.Renderer.GetPropertyBlock(slot.Saved);
            }
            if (swapped != null)
            {
                slot.Swapped = swapped;
                slot.Renderer.sharedMaterials = swapped;
            }
        }

        private static readonly int MaterialEmission = MagicMaterials.EmissionColorId;

        private void SetupShell(Slot slot, Material rim)
        {
            Renderer r = slot.Renderer;
            int subMeshes = 1;
            GameObject go = null;
            if (r is MeshRenderer)
            {
                var mf = r.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) return;
                subMeshes = Mathf.Max(1, mf.sharedMesh.subMeshCount);
                go = new GameObject("Gold Rim");
                go.transform.SetParent(r.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                slot.ShellRenderer = go.AddComponent<MeshRenderer>();
            }
            else if (r is SkinnedMeshRenderer smr)
            {
                if (smr.sharedMesh == null) return;
                subMeshes = Mathf.Max(1, smr.sharedMesh.subMeshCount);
                go = new GameObject("Gold Rim");
                go.transform.SetParent(r.transform, false);
                var shell = go.AddComponent<SkinnedMeshRenderer>();
                shell.sharedMesh = smr.sharedMesh;
                shell.bones = smr.bones;
                shell.rootBone = smr.rootBone;
                shell.localBounds = smr.localBounds;
                shell.updateWhenOffscreen = smr.updateWhenOffscreen;
                slot.ShellRenderer = shell;
            }
            if (go == null) return;
            go.layer = r.gameObject.layer;
            var mats = new Material[subMeshes];
            for (int i = 0; i < subMeshes; i++) mats[i] = rim;
            slot.ShellRenderer.sharedMaterials = mats;
            slot.ShellRenderer.shadowCastingMode = ShadowCastingMode.Off;
            slot.ShellRenderer.receiveShadows = false;
            slot.ShellRenderer.lightProbeUsage = LightProbeUsage.Off;
            slot.ShellRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            slot.Shell = go;
            slot.ShellRenderer.enabled = false;
        }

        private void RestoreSlots()
        {
            foreach (Slot s in slots)
            {
                Renderer r = s.Renderer;
                if (r != null)
                {
                    if (s.Swapped != null)
                    {
                        Material[] now = r.sharedMaterials;
                        bool changed = false;
                        for (int i = 0; i < now.Length && i < s.Swapped.Length && i < s.Original.Length; i++)
                        {
                            if (now[i] == s.Swapped[i] && s.Swapped[i] != s.Original[i])
                            {
                                now[i] = s.Original[i];
                                changed = true;
                            }
                        }
                        if (changed) r.sharedMaterials = now;
                    }
                    if (s.Emissive)
                    {
                        if (s.HadBlock && s.Saved != null) r.SetPropertyBlock(s.Saved);
                        else r.SetPropertyBlock(null);
                    }
                }
                if (s.Instances != null)
                {
                    foreach (Material m in s.Instances) if (m != null) Destroy(m);
                }
                if (s.Shell != null) Destroy(s.Shell);
            }
            slots.Clear();
        }

        private void Deactivate()
        {
            RestoreSlots();
            foreach (Fx f in fx) f.Quad?.Destroy();
            fx.Clear();
            if (glowLight != null) glowLight.enabled = false;
            active = false;
            igniteStart = float.NegativeInfinity;
        }

        // ------------------------------------------------------------------ per frame

        private void Update()
        {
            if (!active) return;
            float now = Now;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            hintLevel = Mathf.MoveTowards(hintLevel, hint ? 1f : 0f, dt / 0.6f);

            float t = now - igniteStart;
            bool igniting = t >= 0f && t < IgniteLength;
            float env = 0f;
            if (igniting)
            {
                env = t < 0.14f ? Ease.OutCubic(t / 0.14f) : Mathf.Exp(-(t - 0.14f) / 0.75f);
                env *= 1f - Ease.InQuad(Mathf.InverseLerp(IgniteLength - 0.6f, IgniteLength, t));
            }
            float pulse = 0.5f + 0.5f * Mathf.Sin(now * Mathf.PI * 2f / 2.4f);
            float k = Intensity;

            float emission = Mathf.Max(hintLevel * (0.10f + 0.14f * pulse), env * 2.4f * igniteStrength) * k;
            float rim = (hintLevel * (0.30f + 0.30f * pulse) + env * 2.0f * igniteStrength) * k;
            float fill = (hintLevel * 0.03f + env * 0.25f * igniteStrength) * k;
            float band = 0f, sweepY = -10000f;
            float h = Mathf.Max(0.2f, bounds.size.y);
            if (igniting && t < 1.6f)
            {
                band = 3.2f * igniteStrength * (1f - Ease.InQuad(t / 1.6f)) * k;
                sweepY = bounds.min.y - 0.2f + (h + 0.6f) * Ease.OutCubic(t / 1.3f);
            }
            else if (hintLevel > 0f)
            {
                float period = 4.2f;
                float hp = (now % period) / period;
                band = 0.9f * hintLevel * Ease.Bell(hp) * k;
                sweepY = bounds.min.y - 0.2f + (h + 0.4f) * hp;
            }

            bool rebuild = false;
            foreach (Slot s in slots)
            {
                if (s.Renderer == null) { rebuild = true; continue; }
                if (s.Emissive)
                {
                    s.Renderer.GetPropertyBlock(block);
                    Color e = MagicPalette.Hdr(Tint, emission);
                    block.SetColor(MaterialEmission, new Color(e.r + s.OriginalEmission.r, e.g + s.OriginalEmission.g, e.b + s.OriginalEmission.b, 1f));
                    s.Renderer.SetPropertyBlock(block);
                }
                if (s.ShellRenderer != null)
                {
                    bool on = rim > 0.002f || band > 0.002f;
                    if (s.ShellRenderer.enabled != on) s.ShellRenderer.enabled = on && s.Renderer.enabled;
                    if (on)
                    {
                        rimBlock.SetColor(MagicMaterials.ColorId, MagicPalette.Hdr(Tint, 1.6f));
                        rimBlock.SetFloat(RimId, rim);
                        rimBlock.SetFloat(FillId, fill);
                        rimBlock.SetFloat(BandId, band);
                        rimBlock.SetFloat(SweepYId, sweepY);
                        rimBlock.SetFloat(SweepWidthId, Mathf.Max(0.12f, h * 0.12f));
                        s.ShellRenderer.SetPropertyBlock(rimBlock);
                    }
                }
            }

            UpdateLight(env, k);
            UpdateFx(now);

            if (hintLevel > 0.05f && now >= nextHintMote)
            {
                nextHintMote = now + Random.Range(0.22f, 0.4f) / Mathf.Max(0.3f, hintLevel);
                float r = Mathf.Max(0.2f, Mathf.Max(bounds.extents.x, bounds.extents.z));
                Vector2 c = Random.insideUnitCircle.normalized * r * Random.Range(0.6f, 1.05f);
                Vector3 p = new Vector3(bounds.center.x + c.x, bounds.min.y + Random.Range(0f, h * 0.6f), bounds.center.z + c.y);
                Color col = Tint;
                col.a = 0.7f * hintLevel;
                MagicFx.Mote(p, Vector3.up * Random.Range(0.25f, 0.55f), Random.Range(0.035f, 0.07f) * ScaleRef(), Random.Range(1.4f, 2.2f), col);
            }

            if (!hint && hintLevel <= 0f && !igniting && fx.Count == 0)
            {
                Deactivate();
            }
            else if (rebuild)
            {
                CollectSlots();
            }
        }

        private float ScaleRef() => Mathf.Clamp(bounds.extents.magnitude, 0.4f, 4f);

        private void UpdateLight(float env, float k)
        {
            if (!UseLight) return;
            if (env <= 0.001f)
            {
                if (glowLight != null && glowLight.enabled) glowLight.enabled = false;
                return;
            }
            if (glowLight == null)
            {
                var go = new GameObject("Gold Light");
                go.transform.SetParent(FxRoot(), false);
                glowLight = go.AddComponent<Light>();
                glowLight.type = LightType.Point;
                glowLight.shadows = LightShadows.None;
                glowLight.renderMode = LightRenderMode.ForcePixel;
            }
            glowLight.enabled = true;
            glowLight.color = Tint;
            Vector3 center = bounds.center;
            glowLight.transform.position = Vector3.Lerp(center, GlyphAnchor, 0.5f);
            glowLight.range = Mathf.Clamp(ScaleRef() * 5f, 3f, 16f);
            glowLight.intensity = env * 7f * igniteStrength * k;
        }

        // ------------------------------------------------------------------ ignite fx

        private Transform FxRoot()
        {
            if (fxRoot == null)
            {
                // A scene root (not a child) so the object's own scale never distorts the effects.
                var go = new GameObject("Gold Magic FX (" + name + ")");
                fxRoot = go.transform;
            }
            return fxRoot;
        }

        private void SpawnIgniteFx(float strength)
        {
            float s = ScaleRef();
            Vector3 anchor = GlyphAnchor;
            Vector3 baseCenter = new Vector3(bounds.center.x, bounds.min.y + 0.02f, bounds.center.z);
            float radius = Mathf.Max(0.3f, Mathf.Max(bounds.extents.x, bounds.extents.z));

            MagicFx.FlareAt(anchor, 2.4f * s * strength, 0.7f, MagicPalette.GoldPale);
            MagicFx.Burst(anchor, 0.8f * s, strength);

            // Ring of rising motes swirling up from the base.
            int n = Mathf.RoundToInt(40 * strength);
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f + Random.Range(-0.08f, 0.08f);
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 p = baseCenter + dir * radius * Random.Range(0.85f, 1.1f);
                Vector3 tangent = Vector3.Cross(Vector3.up, dir);
                Vector3 v = Vector3.up * Random.Range(0.9f, 2.0f) * Mathf.Sqrt(s) + tangent * Random.Range(0.3f, 0.7f) - dir * 0.15f;
                Color c = Color.Lerp(Tint, MagicPalette.WhiteHot, Random.value * 0.5f);
                MagicFx.Mote(p, v, Random.Range(0.05f, 0.1f) * s, Random.Range(1.3f, 2.4f), c);
            }
            for (int i = 0; i < 6; i++)
            {
                Vector3 p = baseCenter + Random.insideUnitSphere * radius * 0.7f;
                p.y = baseCenter.y + Random.Range(0f, 0.4f);
                Color c = Tint;
                c.a = 0.35f;
                MagicFx.HazeAt(p, Vector3.up * 0.2f, radius * 1.4f, Random.Range(1.6f, 2.4f), c);
            }

            // Ground ring.
            var ring = new FxQuad("Gold Ring", GlyphAtlas.Ring, FxRoot());
            ring.Transform.position = baseCenter;
            ring.Transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            fx.Add(new Fx { Quad = ring, Start = Now, Life = 1.1f, Kind = 0, A = radius * 3.2f, B = strength });

            // Light shaft.
            if (UseShaft)
            {
                var shaft = new FxQuad("Gold Shaft", GlyphAtlas.Shaft, FxRoot());
                float height = Mathf.Max(3f, bounds.size.y * 2.6f);
                fx.Add(new Fx { Quad = shaft, Start = Now, Life = 1.9f, Kind = 1, A = height, B = radius * 1.7f, C = strength });
            }

            // A few glyphs orbiting, rising and fading.
            int glyphs = 4;
            float phase = Random.value * Mathf.PI * 2f;
            for (int i = 0; i < glyphs; i++)
            {
                string code = OrbitSigns[(i + Random.Range(0, OrbitSigns.Length)) % OrbitSigns.Length];
                Texture tex = GlyphAtlas.GetTexture(code);
                if (tex == null) continue;
                var q = new FxQuad("Orbit " + code, tex, FxRoot());
                q.Transform.localScale = Vector3.zero;
                fx.Add(new Fx { Quad = q, Start = Now + i * 0.08f, Life = 2.3f, Kind = 2, A = phase + i * Mathf.PI * 2f / glyphs, B = radius * 1.15f, C = GlyphAtlas.Aspect(code) });
            }
        }

        private void UpdateFx(float now)
        {
            Camera cam = FxQuad.ResolveCamera(null);
            float s = ScaleRef();
            for (int i = fx.Count - 1; i >= 0; i--)
            {
                Fx f = fx[i];
                float t = (now - f.Start) / f.Life;
                if (f.Quad == null || !f.Quad.Alive || t >= 1f)
                {
                    f.Quad?.Destroy();
                    fx.RemoveAt(i);
                    continue;
                }
                if (t < 0f) { f.Quad.SetColor(Tint, 0f, 0f); continue; }
                switch (f.Kind)
                {
                    case 0:
                    {
                        float size = Mathf.Lerp(0.2f, f.A, Ease.OutQuint(t));
                        f.Quad.Transform.localScale = new Vector3(size, size, 1f);
                        f.Quad.SetColor(Tint, 3.2f * f.B * Intensity, 1f - Ease.InQuad(t));
                        break;
                    }
                    case 1:
                    {
                        float a = Ease.OutCubic(t * 5f) * (1f - Ease.InQuad(Mathf.InverseLerp(0.3f, 1f, t)));
                        float w = f.B * Mathf.Lerp(0.6f, 1.1f, Ease.OutCubic(t));
                        Vector3 basePos = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                        f.Quad.Transform.position = basePos + Vector3.up * f.A * 0.5f;
                        f.Quad.Transform.localScale = new Vector3(w, f.A, 1f);
                        f.Quad.BillboardUpright(cam);
                        f.Quad.SetColor(Color.Lerp(Tint, MagicPalette.WhiteHot, 0.3f), 2.4f * f.C * Intensity, a * 0.85f);
                        break;
                    }
                    default:
                    {
                        float ang = f.A + t * 2.6f;
                        float y = Mathf.Lerp(bounds.min.y + bounds.size.y * 0.25f, bounds.max.y + 0.4f * s, Ease.OutCubic(t));
                        float r = f.B * Mathf.Lerp(0.8f, 1.15f, t);
                        Vector3 p = new Vector3(bounds.center.x + Mathf.Cos(ang) * r, y, bounds.center.z + Mathf.Sin(ang) * r);
                        f.Quad.Transform.position = p;
                        float size = 0.32f * s * Mathf.Lerp(0.6f, 1f, Ease.OutBack(Mathf.Clamp01(t * 3f)));
                        f.Quad.Transform.localScale = new Vector3(size * f.C, size, 1f);
                        f.Quad.Billboard(cam, Mathf.Sin(t * 5f + f.A) * 8f);
                        float a = Ease.OutCubic(t * 4f) * (1f - Ease.InQuad(Mathf.InverseLerp(0.55f, 1f, t)));
                        f.Quad.SetColor(MagicPalette.GoldPale, 2.6f * Intensity, a);
                        if (Random.value < 0.35f)
                        {
                            Color c = Tint;
                            c.a = a;
                            MagicFx.Mote(p, Vector3.up * 0.15f, size * 0.12f, Random.Range(0.4f, 0.7f), c);
                        }
                        break;
                    }
                }
            }
        }
    }
}
