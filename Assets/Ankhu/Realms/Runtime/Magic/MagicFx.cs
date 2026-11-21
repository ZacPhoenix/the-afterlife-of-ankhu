using System.Collections.Generic;
using UnityEngine;

namespace Ankhu.Realms
{
    /// <summary>
    /// Shared world space particle emitters for the words of power (motes, sparks, rings, flares, haze).
    /// A handful of long lived ParticleSystems under one hidden root, fed with ParticleSystem.Emit so
    /// effects can spawn thousands of particles without creating objects. Unscaled time throughout.
    /// Particle colours carry the hue, the material carries the HDR intensity (so URP Bloom picks them up).
    /// </summary>
    public static class MagicFx
    {
        private sealed class Channel
        {
            public ParticleSystem System;
        }

        private static GameObject root;
        private static Channel motes, sparks, rings, flares, haze;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            root = null;
            motes = sparks = rings = flares = haze = null;
        }

        /// <summary>Intensity multiplier of the particle materials. Bloom threshold is about 0.9, so above 1 glows.</summary>
        public const float ParticleHdr = 3.2f;

        private static bool Ensure()
        {
            if (root != null && motes != null && motes.System != null) return true;
            if (root != null) Object.Destroy(root);
            root = new GameObject("Ankhu Magic FX");
            Object.DontDestroyOnLoad(root);

            motes = Make("Motes", GlyphAtlas.Mote, ParticleHdr, 4000, ps =>
            {
                ParticleSystem.MainModule main = ps.main;
                main.gravityModifier = -0.04f;
                ParticleSystem.NoiseModule noise = ps.noise;
                noise.enabled = true;
                noise.strength = 0.35f;
                noise.frequency = 0.9f;
                noise.scrollSpeed = 0.4f;
                noise.damping = true;
                noise.quality = ParticleSystemNoiseQuality.Medium;
                ParticleSystem.LimitVelocityOverLifetimeModule drag = ps.limitVelocityOverLifetime;
                drag.enabled = true;
                drag.limit = 50f;
                drag.drag = 1.6f;
                drag.multiplyDragByParticleSize = false;
                drag.multiplyDragByParticleVelocity = false;
                SetSize(ps, new AnimationCurve(new Keyframe(0f, 0.3f), new Keyframe(0.12f, 1f), new Keyframe(1f, 0f)));
                SetFade(ps, 0.08f, 0.55f);
            });
            sparks = Make("Sparks", GlyphAtlas.Spark, ParticleHdr * 1.3f, 1500, ps =>
            {
                ParticleSystem.LimitVelocityOverLifetimeModule drag = ps.limitVelocityOverLifetime;
                drag.enabled = true;
                drag.limit = 50f;
                drag.drag = 3.5f;
                drag.multiplyDragByParticleSize = false;
                drag.multiplyDragByParticleVelocity = false;
                ParticleSystem.RotationOverLifetimeModule rot = ps.rotationOverLifetime;
                rot.enabled = true;
                rot.z = new ParticleSystem.MinMaxCurve(-1.5f, 1.5f);
                SetSize(ps, new AnimationCurve(new Keyframe(0f, 0.2f), new Keyframe(0.1f, 1.1f), new Keyframe(0.3f, 0.9f), new Keyframe(1f, 0f)));
                SetFade(ps, 0.02f, 0.4f);
            });
            rings = Make("Rings", GlyphAtlas.Ring, ParticleHdr, 300, ps =>
            {
                SetSize(ps, new AnimationCurve(new Keyframe(0f, 0.1f, 0f, 4f), new Keyframe(1f, 1f, 0f, 0f)));
                SetFade(ps, 0.02f, 0.75f);
            });
            flares = Make("Flares", GlyphAtlas.Flare, ParticleHdr * 1.5f, 300, ps =>
            {
                SetSize(ps, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(0.15f, 1f), new Keyframe(1f, 0.3f)));
                SetFade(ps, 0.03f, 0.8f);
            });
            haze = Make("Haze", GlyphAtlas.Smoke, 1.3f, 600, ps =>
            {
                ParticleSystem.RotationOverLifetimeModule rot = ps.rotationOverLifetime;
                rot.enabled = true;
                rot.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
                SetSize(ps, new AnimationCurve(new Keyframe(0f, 0.5f), new Keyframe(1f, 1.4f)));
                SetFade(ps, 0.25f, 0.5f);
            });
            return true;
        }

        private static Channel Make(string name, Texture tex, float hdr, int max, System.Action<ParticleSystem> configure)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = ps.main;
            main.duration = 1f;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.useUnscaledTime = true;
            main.maxParticles = max;
            main.startSpeed = 0f;
            main.startLifetime = 1f;
            main.startSize = 0.1f;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = false;
            configure(ps);

            var r = go.GetComponent<ParticleSystemRenderer>();
            Material mat = new Material(MagicMaterials.Additive(tex)) { name = "Magic FX " + name, hideFlags = HideFlags.DontSave };
            MagicMaterials.SetColor(mat, MagicPalette.Hdr(Color.white, hdr));
            r.sharedMaterial = mat;
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.alignment = ParticleSystemRenderSpace.View;
            r.minParticleSize = 0f;
            r.maxParticleSize = 3f;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sortingFudge = 0f;
            ps.Play();
            return new Channel { System = ps };
        }

        private static void SetSize(ParticleSystem ps, AnimationCurve curve)
        {
            ParticleSystem.SizeOverLifetimeModule s = ps.sizeOverLifetime;
            s.enabled = true;
            s.size = new ParticleSystem.MinMaxCurve(1f, curve);
        }

        private static void SetFade(ParticleSystem ps, float fadeIn, float fadeOutStart)
        {
            ParticleSystem.ColorOverLifetimeModule c = ps.colorOverLifetime;
            c.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, fadeIn), new GradientAlphaKey(1f, fadeOutStart), new GradientAlphaKey(0f, 1f) });
            c.color = new ParticleSystem.MinMaxGradient(g);
        }

        private static void Emit(Channel ch, Vector3 pos, Vector3 vel, float size, float life, Color color, float rotationDeg = 0f)
        {
            if (!Ensure() || ch == null || ch.System == null) return;
            var p = new ParticleSystem.EmitParams
            {
                position = pos,
                velocity = vel,
                startSize = size,
                startLifetime = life,
                startColor = color,
                rotation = rotationDeg,
                applyShapeToPosition = false,
            };
            ch.System.Emit(p, 1);
        }

        // ------------------------------------------------------------------ public emitters

        public static void Mote(Vector3 pos, Vector3 vel, float size, float life, Color color)
        {
            Ensure();
            Emit(motes, pos, vel, size, life, color, Random.Range(0f, 360f));
        }

        public static void SparkAt(Vector3 pos, Vector3 vel, float size, float life, Color color)
        {
            Ensure();
            Emit(sparks, pos, vel, size, life, color, Random.Range(0f, 360f));
        }

        public static void RingAt(Vector3 pos, float size, float life, Color color)
        {
            Ensure();
            Emit(rings, pos, Vector3.zero, size, life, color);
        }

        public static void FlareAt(Vector3 pos, float size, float life, Color color)
        {
            Ensure();
            Emit(flares, pos, Vector3.zero, size, life, color);
        }

        public static void HazeAt(Vector3 pos, Vector3 vel, float size, float life, Color color)
        {
            Ensure();
            Emit(haze, pos, vel, size, life, color, Random.Range(0f, 360f));
        }

        /// <summary>Spherical burst of gold motes and sparks with a ring and a flare: a sign striking home.</summary>
        public static void Burst(Vector3 pos, float scale, float strength = 1f)
        {
            Ensure();
            int n = Mathf.RoundToInt(14 * strength);
            for (int i = 0; i < n; i++)
            {
                Vector3 dir = Random.onUnitSphere;
                dir.y = Mathf.Abs(dir.y) * 0.7f + 0.15f;
                Color c = Color.Lerp(MagicPalette.Gold, MagicPalette.WhiteHot, Random.value * 0.6f);
                Mote(pos, dir * Random.Range(0.8f, 2.4f) * scale, Random.Range(0.05f, 0.11f) * scale, Random.Range(0.6f, 1.3f), c);
            }
            for (int i = 0; i < Mathf.RoundToInt(5 * strength); i++)
            {
                SparkAt(pos, Random.onUnitSphere * Random.Range(1.5f, 3.5f) * scale, Random.Range(0.12f, 0.22f) * scale, Random.Range(0.25f, 0.45f), MagicPalette.WhiteHot);
            }
            RingAt(pos, 1.3f * scale, 0.55f, MagicPalette.Gold);
            FlareAt(pos, 1.4f * scale, 0.45f, MagicPalette.GoldPale);
        }
    }

    /// <summary>
    /// One additive camera facing (or freely oriented) quad in the world, coloured through a property block.
    /// Owner destroys it with <see cref="Destroy"/>.
    /// </summary>
    public sealed class FxQuad
    {
        private readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        public readonly GameObject GameObject;
        public readonly Transform Transform;
        public readonly MeshRenderer Renderer;

        public FxQuad(string name, Texture texture, Transform parent = null)
        {
            GameObject = new GameObject(name);
            Transform = GameObject.transform;
            if (parent != null) Transform.SetParent(parent, false);
            GameObject.AddComponent<MeshFilter>().sharedMesh = MagicMaterials.Quad;
            Renderer = GameObject.AddComponent<MeshRenderer>();
            Renderer.sharedMaterial = MagicMaterials.Additive(texture);
            Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Renderer.receiveShadows = false;
            Renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            Renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            Renderer.allowOcclusionWhenDynamic = false;
        }

        public bool Alive => GameObject != null;

        /// <summary>Sets the HDR colour: <paramref name="color"/> hue, times intensity, alpha as given.</summary>
        public void SetColor(Color color, float intensity, float alpha)
        {
            if (Renderer == null) return;
            MagicMaterials.SetColor(block, MagicPalette.Hdr(color, intensity, Mathf.Clamp01(alpha)));
            Renderer.SetPropertyBlock(block);
            bool visible = alpha > 0.002f && intensity > 0.0001f;
            if (Renderer.enabled != visible) Renderer.enabled = visible;
        }

        /// <summary>Faces the camera: same rotation as the camera, rolled by <paramref name="rollDeg"/>.</summary>
        public void Billboard(Camera cam, float rollDeg)
        {
            if (cam == null || Transform == null) return;
            Transform.rotation = cam.transform.rotation * Quaternion.Euler(0f, 0f, rollDeg);
        }

        /// <summary>Faces the camera turning only about world up (for light shafts).</summary>
        public void BillboardUpright(Camera cam)
        {
            if (cam == null || Transform == null) return;
            Vector3 f = Transform.position - cam.transform.position;
            f.y = 0f;
            if (f.sqrMagnitude < 1e-6f) f = cam.transform.forward;
            f.y = 0f;
            if (f.sqrMagnitude < 1e-6f) f = Vector3.forward;
            Transform.rotation = Quaternion.LookRotation(f.normalized, Vector3.up);
        }

        public void Destroy()
        {
            if (GameObject != null) Object.Destroy(GameObject);
        }

        /// <summary>The camera to orient effects toward: the given one, else Camera.main.</summary>
        public static Camera ResolveCamera(Camera preferred)
        {
            if (preferred != null && preferred.isActiveAndEnabled) return preferred;
            Camera main = Camera.main;
            if (main != null) return main;
            return preferred;
        }

        /// <summary>Destroys every quad in the list and clears it.</summary>
        public static void DestroyAll(List<FxQuad> quads)
        {
            if (quads == null) return;
            foreach (FxQuad q in quads) q?.Destroy();
            quads.Clear();
        }
    }
}
