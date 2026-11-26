using UnityEngine;

namespace Ankhu.Painted
{
    /// <summary>
    /// Seats the 3D Ankhu in a painted room. Only the painted scene adds it. Two parts:
    /// a warm tint on his base colour through a property block, so the shared materials stay untouched
    /// and his white linen reads as the painting's cream instead of clipping to white, and a soft
    /// camera-facing contact shadow at the floor under his feet that fades as he leaves the ground.
    /// Visual only, never touches movement or rules.
    /// </summary>
    public sealed class PaintedActorGrade : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");

        [SerializeField] private Color tint = new Color(0.9f, 0.8f, 0.68f, 1f);
        [SerializeField] private float smoothness = 0.12f;
        [SerializeField] private float shadowWidth = 0.95f;
        [SerializeField] private float shadowHeight = 0.24f;
        [SerializeField] private float shadowStrength = 0.55f;
        [SerializeField] private float shadowFadeHeight = 1.6f;
        [SerializeField] private float shadowDepth = 0.08f;

        private Renderer[] renderers;
        private MaterialPropertyBlock block;
        private Transform shadow;
        private Material shadowMat;
        private Collider2D own;
        private readonly RaycastHit2D[] hits = new RaycastHit2D[8];

        public void Configure(Color baseTint, float strength)
        {
            tint = baseTint;
            shadowStrength = strength;
        }

        private void Start()
        {
            block = new MaterialPropertyBlock();
            own = GetComponent<Collider2D>();
            BuildShadow();
        }

        private void BuildShadow()
        {
            Shader s = Shader.Find("Ankhu/PaintedAnim");
            if (s == null) return;
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "contact_shadow" };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - r);
                    a = a * a * (3f - 2f * a);
                    px[y * n + x] = new Color32(0, 0, 0, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            shadowMat = new Material(s) { name = "P_ContactShadow" };
            shadowMat.SetTexture("_Mask", tex);
            shadowMat.SetFloat("_Gain", 0f);
            shadowMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            shadowMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            shadowMat.renderQueue = 3050; // after the painted layers (queue 3000), or the opaque floor paint covers it
            alphaTex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { name = "contact_shadow_alpha" };
            shadowMat.SetTexture("_BaseMap", alphaTex);
            shadowMat.SetFloat("_UseSheetAlpha", 1f);
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "Paint_contact_shadow";
            Object.Destroy(go.GetComponent<Collider>());
            go.GetComponent<Renderer>().sharedMaterial = shadowMat;
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            shadow = go.transform;
            shadow.localScale = new Vector3(shadowWidth, shadowHeight, 1f);
        }

        private void LateUpdate()
        {
            // The model can be rebuilt at runtime, so pick up new renderers now and then.
            if (renderers == null || Time.frameCount % 30 == 0)
            {
                renderers = GetComponentsInChildren<Renderer>(true);
            }
            foreach (Renderer r in renderers)
            {
                if (r == null || r is LineRenderer || r is ParticleSystemRenderer) continue;
                Material[] mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    Material m = mats[i];
                    if (m == null || !m.HasProperty(BaseColorId)) continue;
                    // Multiply each material's own colour so a dark skin material stays dark,
                    // and flatten the specular that clips the linen to white under the warm lights.
                    r.GetPropertyBlock(block, i);
                    block.SetColor(BaseColorId, m.GetColor(BaseColorId) * tint);
                    if (m.HasProperty(SmoothnessId)) block.SetFloat(SmoothnessId, smoothness);
                    r.SetPropertyBlock(block, i);
                }
            }

            if (shadow == null) return;
            Vector2 feet = transform.position;
            float floorY = feet.y;
            bool found = false;
            int count = Physics2D.RaycastNonAlloc(feet + Vector2.up * 0.05f, Vector2.down, hits, shadowFadeHeight + 0.1f);
            float best = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                Collider2D c = hits[i].collider;
                if (c == null || c.isTrigger || c == own) continue;
                if (hits[i].distance < best) { best = hits[i].distance; floorY = hits[i].point.y; found = true; }
            }
            float lift = found ? Mathf.Max(0f, feet.y - floorY) : shadowFadeHeight;
            float k = 1f - Mathf.Clamp01(lift / shadowFadeHeight);
            shadow.gameObject.SetActive(k > 0.01f);
            shadow.position = new Vector3(feet.x, floorY - shadowHeight * 0.1f, transform.position.z + shadowDepth);
            float w = Mathf.Lerp(0.6f, 1f, k);
            shadow.localScale = new Vector3(shadowWidth * w, shadowHeight * w, 1f);
            // PaintedAnim alpha is mask.a times the sheet alpha (with _UseSheetAlpha on), so a 1x1 sheet
            // whose alpha is the strength scales the whole shadow.
            byte a = (byte)(Mathf.Clamp01(shadowStrength * k) * 255f);
            if (a != lastA)
            {
                alphaTex.SetPixel(0, 0, new Color32(0, 0, 0, a));
                alphaTex.Apply();
                lastA = a;
            }
        }

        private Texture2D alphaTex;
        private int lastA = -1;
    }
}
