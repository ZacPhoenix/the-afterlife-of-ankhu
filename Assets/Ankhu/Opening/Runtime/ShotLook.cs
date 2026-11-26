using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ankhu.Opening
{
    /// <summary>
    /// Lights the 3D figures to match each painted shot. On every cut the moon is turned to come from the painting's
    /// sky (ahead and to the right, so the figures are rim lit and cast their shadows toward the lens), a warm torch
    /// fill comes from the camera side, and the ambient is set from colours sampled from that painting in the editor.
    /// It also takes the plastic shine off the characters and darkens the props to aged bronze.
    /// </summary>
    public sealed class ShotLook : MonoBehaviour
    {
        [Serializable]
        public struct Look
        {
            public Color sky;
            public Color equator;
            public Color ground;
            public Color fill;
        }

        [SerializeField] private FixedCameraDirector director;
        [SerializeField] private Light moon;
        [SerializeField] private Light fill;
        [SerializeField] private Light rim;
        [SerializeField] private Look[] looks = Array.Empty<Look>();
        [SerializeField] private Transform[] characters = Array.Empty<Transform>();
        // Higher moon, shorter shadows that stay attached to the feet like the painted props' shadows.
        [SerializeField] private float moonPitch = 52f;
        [SerializeField] private float moonYawFromView = 205f;
        [SerializeField] private float skinSmoothness = 0.30f;
        [SerializeField] private float escortLift = 1.35f;
        [SerializeField] private float glossMapScale = 0.38f;
        [SerializeField] private Texture2D attendantSkin;
        // Anpu's body texture with the bright gold trim repainted as aged dark bronze, to match the props.
        [SerializeField] private Texture2D anpuSkin;
        [SerializeField] private float anpuLift = 1.7f;
        // Ankhu's body texture with the skin deepened to a warm brown. Under the blue night grade and the torches the
        // original read as pink next to the attendants. The linen is untouched.
        [SerializeField] private Texture2D ankhuSkin;
        // Moonlit skin: the paintings grade every figure toward blue, so the 3D skin loses some orange to sit in them.
        [SerializeField] private Color nightGrade = new Color(0.84f, 0.88f, 1.0f, 1f);
        [SerializeField] private Color bronze = new Color(0.50f, 0.43f, 0.31f, 1f);
        // Painted character shading (soft banded light, flat ambient, moonlit edge) in place of URP Lit, so the cast
        // reads as painted into the plates. Props stay on Lit as metal. Null keeps Lit everywhere.
        [SerializeField] private Shader paintedShader;
        [SerializeField] private float rimTintScale = 1.6f;

        private static readonly int Smooth = Shader.PropertyToID("_Smoothness");
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int Metallic = Shader.PropertyToID("_Metallic");

        public void Configure(FixedCameraDirector d, Light key, Light warm, Light edge, Look[] perShot, Transform[] people)
        {
            director = d;
            moon = key;
            fill = warm;
            rim = edge;
            looks = perShot;
            characters = people;
        }

        // The Guardian model's skin is near-black veined marble. The opening uses a copy with the skin repainted
        // as warm brown, so the attendants read as people in the painted world. Other scenes keep the original.
        public void SetAttendantSkin(Texture2D skin) => attendantSkin = skin;
        public void SetAnpuSkin(Texture2D skin) => anpuSkin = skin;
        public void SetAnkhuSkin(Texture2D skin) => ankhuSkin = skin;
        public void SetPaintedShader(Shader s) => paintedShader = s;

        private void OnEnable() { if (director != null) director.Cut += OnCut; }
        private void OnDisable() { if (director != null) director.Cut -= OnCut; }

        private void Start()
        {
            Dematerialise();
            if (director != null && director.Active >= 0) OnCut(-1, director.Active);
        }

        private void OnCut(int previous, int now)
        {
            Camera cam = director.ActiveCamera;
            if (cam == null) return;
            float yaw = cam.transform.eulerAngles.y;
            if (moon != null) moon.transform.rotation = Quaternion.Euler(moonPitch, yaw + moonYawFromView, 0f);
            if (fill != null) fill.transform.rotation = Quaternion.Euler(22f, yaw - 20f, 0f);
            // From ahead and to the left, opposite the moon, low like a brazier, so both edges of each figure catch light.
            if (rim != null) rim.transform.rotation = Quaternion.Euler(12f, yaw + 150f, 0f);
            if (now < 0 || now >= looks.Length) return;
            Look l = looks[now];
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = l.sky;
            RenderSettings.ambientEquatorColor = l.equator;
            RenderSettings.ambientGroundColor = l.ground;
            if (fill != null) fill.color = l.fill;
            Shader.SetGlobalColor("_AnkhuRimTint", l.sky * rimTintScale);
        }

        // Instanced copies on the painted shader, so other scenes keep their Lit materials. Cut-out and transparent
        // materials (hair cards, cloth edges) stay on Lit because the painted pass has no alpha.
        private void Repaint(Renderer r)
        {
            Material[] mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                Material m = mats[i];
                if (m == null || m.renderQueue > 2450 || m.IsKeywordEnabled("_ALPHATEST_ON")) continue;
                var painted = new Material(paintedShader) { name = m.name + " (painted)" };
                if (m.HasProperty("_BaseMap")) painted.SetTexture("_BaseMap", m.GetTexture("_BaseMap"));
                if (m.HasProperty(BaseColor)) painted.SetColor(BaseColor, m.GetColor(BaseColor));
                mats[i] = painted;
            }
            r.sharedMaterials = mats;
        }

        // Property blocks, so the shared character materials used by other scenes are untouched.
        private void Dematerialise()
        {
            var block = new MaterialPropertyBlock();
            for (int ci = 0; ci < characters.Length; ci++)
            {
                Transform root = characters[ci];
                if (root == null) continue;
                foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
                {
                    Material m = r.sharedMaterial;
                    if (m == null || !m.HasProperty(Smooth)) continue;
                    r.GetPropertyBlock(block);
                    bool glossMapped = m.HasProperty("_MetallicGlossMap") && m.GetTexture("_MetallicGlossMap") != null;
                    bool prop = r.name.StartsWith("Prop_");
                    if (prop)
                    {
                        Color c = m.HasProperty(BaseColor) ? m.GetColor(BaseColor) : Color.white;
                        block.SetColor(BaseColor, new Color(c.r * bronze.r, c.g * bronze.g, c.b * bronze.b, c.a));
                        block.SetFloat(Smooth, glossMapped ? 0.45f : 0.32f);
                        if (m.HasProperty(Metallic) && !glossMapped) block.SetFloat(Metallic, 0.55f);
                    }
                    else
                    {
                        block.SetFloat(Smooth, glossMapped ? glossMapScale : Mathf.Min(m.GetFloat(Smooth), skinSmoothness));
                        // Anpu and the attendants are dark skinned and mostly backlit, so they went to pure silhouette.
                        // A small albedo lift keeps the skin dark but lets the torch fill model the form.
                        if (ci >= 2 && attendantSkin != null && m.HasProperty("_BaseMap"))
                            block.SetTexture("_BaseMap", attendantSkin);
                        if (ci == 1 && anpuSkin != null && m.HasProperty("_BaseMap"))
                            block.SetTexture("_BaseMap", anpuSkin);
                        if (ci == 0 && ankhuSkin != null && m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null)
                            block.SetTexture("_BaseMap", ankhuSkin);
                        if (m.HasProperty(BaseColor))
                        {
                            Color c = m.GetColor(BaseColor);
                            float lift = ci == 1 ? anpuLift : (ci >= 2 && attendantSkin == null ? escortLift : 1f);
                            block.SetColor(BaseColor, new Color(Mathf.Min(1f, c.r * lift * nightGrade.r), Mathf.Min(1f, c.g * lift * nightGrade.g), Mathf.Min(1f, c.b * lift * nightGrade.b), c.a));
                        }
                    }
                    r.SetPropertyBlock(block);
                    if (!prop && paintedShader != null) Repaint(r);
                }
            }
        }
    }
}
