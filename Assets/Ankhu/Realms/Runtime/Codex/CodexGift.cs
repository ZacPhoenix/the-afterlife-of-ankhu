using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Ankhu.Realms
{
    /// <summary>
    /// The gift at the First Gate. Gold gathers above Anpu's open palm, the closed roll forms out of the light,
    /// hangs for a breath, then drifts across to Ankhu and settles into his hands, where it stays while Anpu
    /// speaks. When the roll first opens on screen, the object in his hands gives way to it: the on-screen roll
    /// flies out from exactly where the held one was, so the world object and the page read as one thing.
    ///
    /// The model is Resources/RealmCodex/RollModel when it exists (Zac's 3D roll: long axis on local X, pivot at
    /// its centre, about 0.34 m long). Until then a procedural roll stands in: papyrus cylinder, ebony spindle
    /// caps, cream linen cord with a green heart scarab (roll E).
    ///
    /// Run it from a scene beat: <c>yield return CodexGift.Play(anpuTransform, anpuAnimator, ankhuTransform, ankhuAnimator)</c>.
    /// The director does this for the cue "anpu_offers_codex".
    /// </summary>
    public sealed class CodexGift : MonoBehaviour
    {
        public const string ModelPath = "RealmCodex/RollModel";
        public const string SoundPath = "RealmCodex/sfx_gift";

        /// <summary>Scale on the model: a touch larger than life so it reads in a fixed-camera shot.</summary>
        public const float ModelScale = 1.3f;

        private static CodexGift held;

        private Transform receiver;
        private Animator receiverAnim;
        private Transform model;
        private Renderer[] renderers;
        private Light glow;
        private bool vanishing;
        private MaterialPropertyBlock block;
        private Camera giftCam;
        private Transform giver;
        private Vector3 camLook;
        private Vector3 camFrom, camTo;
        private float camT;

        /// <summary>How long the two-shot holds after the roll is in his hands, if the roll is not opened first.</summary>
        public const float HoldShotSeconds = 14f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { held = null; handoffTime = -100f; }

        /// <summary>True while the gift is playing or the roll is still in his hands (CodexFamiliar waits for this).</summary>
        internal static bool Busy => held != null;

        private static Vector3 handoffPoint;
        private static float handoffTime = -100f;

        /// <summary>Where the held roll left his hands, for the floating roll to rise from (once, within 20 s).</summary>
        internal static bool ConsumeHandoff(out Vector3 point)
        {
            point = handoffPoint;
            bool fresh = Time.unscaledTime - handoffTime < 20f;
            handoffTime = -100f;
            return fresh;
        }

        /// <summary>Zac's model if it exists (Resources/RealmCodex/RollModel), else the procedural roll E stand-in. No colliders.</summary>
        internal static Transform CreateRollModel(Transform parent)
        {
            Transform m;
            GameObject prefab = Resources.Load<GameObject>(ModelPath);
            if (prefab != null)
            {
                m = Instantiate(prefab, parent, false).transform;
                m.localPosition = Vector3.zero;
            }
            else
            {
                m = BuildProcedural(parent);
            }
            foreach (Collider c in m.GetComponentsInChildren<Collider>()) Destroy(c);
            foreach (Renderer r in m.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return m;
        }

        /// <summary>The whole gift, about six seconds. Ends with the roll held in Ankhu's hands.</summary>
        public static IEnumerator Play(Transform giver, Animator giverAnim, Transform receiver, Animator receiverAnim)
        {
            if (giver == null || receiver == null) yield break;
            if (held != null) Destroy(held.gameObject);

            var go = new GameObject("CodexGift");
            CodexGift g = go.AddComponent<CodexGift>();
            held = g;
            g.receiver = receiver;
            g.receiverAnim = receiverAnim;
            yield return g.Run(giver, giverAnim);
        }

        // ================================================================== sequence

        private IEnumerator Run(Transform giver, Animator giverAnim)
        {
            block = new MaterialPropertyBlock();
            this.giver = giver;
            // Zac 2026-11-26: the side-on two-shot cut to the old 3D set. Painted rooms keep their painted camera.
            if (!PaintedCamera()) MakeTwoShot(giver, receiver);
            AudioClip sfx = Resources.Load<AudioClip>(SoundPath);
            if (sfx != null) AudioSource.PlayClipAtPoint(sfx, giver.position, 0.9f);

            Vector3 palm = PalmPoint(giver, giverAnim);
            transform.position = palm;

            // A warm light that the gold lives in.
            glow = new GameObject("GiftLight").AddComponent<Light>();
            glow.transform.SetParent(transform, false);
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.78f, 0.42f);
            glow.range = 3.2f;
            glow.intensity = 0f;
            glow.shadows = LightShadows.None;

            // 1. Gathering: motes drawn in from the air on a slowly closing spiral.
            const float gather = 1.7f;
            for (float t = 0f; t < gather; t += Time.deltaTime)
            {
                float k = t / gather;
                palm = PalmPoint(giver, giverAnim);
                transform.position = palm;
                glow.intensity = Mathf.Lerp(0f, 1.6f, Ease.InQuad(k));
                int n = Mathf.RoundToInt(Mathf.Lerp(2f, 6f, k));
                for (int i = 0; i < n; i++)
                {
                    float a = Random.value * Mathf.PI * 2f;
                    float r = Mathf.Lerp(0.85f, 0.35f, k) * Random.Range(0.8f, 1.2f);
                    Vector3 offs = new Vector3(Mathf.Cos(a) * r, Random.Range(-0.25f, 0.45f) * (1f - k), Mathf.Sin(a) * r);
                    Vector3 tangent = Vector3.Cross(Vector3.up, offs).normalized * 0.55f;
                    Vector3 vel = -offs * 1.15f + tangent;
                    Color c = Color.Lerp(MagicPalette.Gold, MagicPalette.WhiteHot, Random.value * 0.5f);
                    MagicFx.Mote(palm + offs, vel, Random.Range(0.025f, 0.055f), Random.Range(0.55f, 0.9f), c);
                }
                if (Random.value < 0.08f) MagicFx.HazeAt(palm, Random.insideUnitSphere * 0.05f, 0.5f, 1.4f, new Color(1f, 0.8f, 0.45f, 0.18f));
                yield return null;
            }

            // 2. The roll forms out of the light.
            BuildModel();
            MagicFx.FlareAt(palm, 1.1f, 0.6f, MagicPalette.WhiteHot);
            MagicFx.RingAt(palm, 0.9f, 0.7f, MagicPalette.Gold);
            const float form = 0.9f;
            for (float t = 0f; t < form; t += Time.deltaTime)
            {
                float k = t / form;
                palm = PalmPoint(giver, giverAnim);
                transform.position = palm + Vector3.up * 0.04f * Mathf.Sin(k * Mathf.PI);
                model.localScale = Vector3.one * ModelScale * Mathf.Max(0.001f, Ease.OutBack(k, 1.4f));
                model.localRotation = Quaternion.Euler(0f, Mathf.Lerp(-80f, 0f, Ease.OutCubic(k)), Mathf.Lerp(25f, 0f, k));
                SetEmission(Mathf.Lerp(3.5f, 0.9f, Ease.OutCubic(k)));
                glow.intensity = Mathf.Lerp(2.6f, 1.3f, k);
                yield return null;
            }
            model.localScale = Vector3.one * ModelScale;

            // 3. A breath of stillness above his palm.
            const float hang = 0.75f;
            for (float t = 0f; t < hang; t += Time.deltaTime)
            {
                transform.position = PalmPoint(giver, giverAnim) + Vector3.up * (0.04f + 0.015f * Mathf.Sin(t * 4f));
                model.localRotation = Quaternion.Euler(0f, Mathf.Sin(t * 1.7f) * 6f, 0f);
                if (Random.value < 0.4f) MagicFx.Mote(transform.position + Random.insideUnitSphere * 0.15f, Vector3.up * 0.2f, 0.03f, 0.8f, MagicPalette.GoldPale);
                yield return null;
            }

            // 4. It drifts across to him on a soft arc, turning to lie the way his hands will hold it.
            Vector3 from = transform.position;
            Quaternion rot0 = transform.rotation;
            const float drift = 1.9f;
            for (float t = 0f; t < drift; t += Time.deltaTime)
            {
                float k = Ease.InOutSine(t / drift);
                Vector3 to = HandsPoint();
                Vector3 mid = Vector3.Lerp(from, to, 0.5f) + Vector3.up * (0.35f + 0.08f * Vector3.Distance(from, to));
                Vector3 p = Bezier(from, mid, to, k);
                transform.position = p + Vector3.up * 0.02f * Mathf.Sin(t * 5f);
                transform.rotation = Quaternion.Slerp(rot0, HoldRotation(), Ease.InOutCubic(t / drift));
                model.localRotation = Quaternion.Euler(Mathf.Sin(t * 2.4f) * 10f * (1f - k), 0f, 0f);
                SetEmission(Mathf.Lerp(0.9f, 0.55f, k));
                if (Random.value < 0.75f)
                    MagicFx.Mote(p + Random.insideUnitSphere * 0.06f, Random.insideUnitSphere * 0.08f + Vector3.up * 0.05f, Random.Range(0.02f, 0.04f), Random.Range(0.5f, 1f), Color.Lerp(MagicPalette.Gold, MagicPalette.GoldPale, Random.value));
                yield return null;
            }

            // 5. It settles into his hands, and the light goes into him.
            Vector3 hands = HandsPoint();
            MagicFx.RingAt(hands, 0.55f, 0.6f, MagicPalette.GoldPale);
            const float settle = 0.9f;
            Vector3 s0 = transform.position;
            for (float t = 0f; t < settle; t += Time.deltaTime)
            {
                float k = t / settle;
                transform.position = Vector3.Lerp(s0, HandsPoint(), Ease.OutCubic(k));
                transform.rotation = HoldRotation();
                SetEmission(Mathf.Lerp(0.55f, 0.08f, k));
                glow.intensity = Mathf.Lerp(1.3f, 0f, k);
                if (Random.value < 0.5f)
                {
                    Vector3 up = Vector3.up * Random.Range(0.25f, 0.7f);
                    MagicFx.Mote(transform.position + Random.insideUnitSphere * 0.12f, up, 0.025f, 0.9f, MagicPalette.GoldPale);
                }
                yield return null;
            }
            SetEmission(0.06f);
            Destroy(glow.gameObject);
            glow = null;

            CodexBook.Ensure();
            CodexBook.OpenChanged += OnRollOpened;
            StartCoroutine(ReleaseShotLater());
        }

        // ================================================================== the two-shot

        /// <summary>
        /// A side-on shot that holds both of them, from the side the scene camera is already on (so the cut does not
        /// cross the line). It drifts in slowly through the gift and is released under the opening roll.
        /// </summary>
        /// <summary>True when the scene camera carries a painted backdrop, which a free camera would leave behind.</summary>
        private static bool PaintedCamera()
        {
            Camera scene = FxQuad.ResolveCamera(Camera.main);
            if (scene == null) return false;
            foreach (Transform c in scene.transform) if (c.name == "Painting" || c.GetComponent<Ankhu.Opening.BackdropFit>() != null) return true;
            return scene.GetComponentInChildren<Ankhu.Opening.BackdropFit>(true) != null;
        }

        private void MakeTwoShot(Transform a, Transform b)
        {
            Camera scene = FxQuad.ResolveCamera(Camera.main);
            Vector3 pa = a.position, pb = b.position;
            Vector3 axis = pb - pa;
            axis.y = 0f;
            float span = Mathf.Max(0.8f, axis.magnitude);
            axis = axis.sqrMagnitude > 0.001f ? axis.normalized : Vector3.right;
            Vector3 side = Vector3.Cross(Vector3.up, axis);
            Vector3 mid = (pa + pb) * 0.5f;
            if (scene != null && Vector3.Dot(side, scene.transform.position - mid) < 0f) side = -side;
            float dist = Mathf.Max(2.7f, span * 1.45f);
            camLook = mid + Vector3.up * 1.35f;
            camFrom = mid + side * dist + Vector3.up * 1.55f - axis * 0.25f;
            camTo = mid + side * (dist - 0.45f) + Vector3.up * 1.5f - axis * 0.1f;

            var go = new GameObject("CodexGiftCamera");
            giftCam = go.AddComponent<Camera>();
            if (scene != null) giftCam.CopyFrom(scene);
            giftCam.fieldOfView = 36f;
            giftCam.nearClipPlane = 0.05f;
            giftCam.depth = (scene != null ? scene.depth : 0f) + 10f;
            giftCam.transform.position = camFrom;
            giftCam.transform.rotation = Quaternion.LookRotation(camLook - camFrom, Vector3.up);
            UniversalAdditionalCameraData extra = go.AddComponent<UniversalAdditionalCameraData>();
            UniversalAdditionalCameraData sceneExtra = scene != null ? scene.GetComponent<UniversalAdditionalCameraData>() : null;
            extra.renderPostProcessing = sceneExtra == null || sceneExtra.renderPostProcessing;
            AudioListener l = go.GetComponent<AudioListener>();
            if (l != null) Destroy(l);
        }

        private void Update()
        {
            if (giftCam == null) return;
            camT += Time.deltaTime;
            float k = Ease.InOutSine(Mathf.Clamp01(camT / 9f));
            Vector3 p = Vector3.Lerp(camFrom, camTo, k);
            giftCam.transform.position = p;
            // Favour the roll a little as it travels, so the eye follows it.
            Vector3 look = model != null ? Vector3.Lerp(camLook, transform.position, 0.35f) : camLook;
            giftCam.transform.rotation = Quaternion.Slerp(giftCam.transform.rotation, Quaternion.LookRotation(look - p, Vector3.up), 1f - Mathf.Exp(-3f * Time.deltaTime));
        }

        private IEnumerator ReleaseShotLater()
        {
            for (float t = 0f; t < HoldShotSeconds && giftCam != null; t += Time.deltaTime) yield return null;
            ReleaseShot();
            // Zac 2026-11-25: then the roll lifts out of his hands and floats beside him (CodexFamiliar takes over).
            for (float t = 0f; t < 1.2f; t += Time.deltaTime) yield return null;
            if (!vanishing) StartCoroutine(LiftAway());
        }

        private IEnumerator LiftAway()
        {
            vanishing = true;
            CodexBook.OpenChanged -= OnRollOpened;
            Vector3 p0 = transform.position;
            const float dur = 0.9f;
            for (float t = 0f; t < dur; t += Time.deltaTime)
            {
                float k = Ease.InOutSine(t / dur);
                transform.position = p0 + Vector3.up * 0.5f * k;
                SetEmission(Mathf.Lerp(0.06f, 1.2f, k));
                if (Random.value < 0.5f) MagicFx.Mote(transform.position + Random.insideUnitSphere * 0.12f, Vector3.up * 0.3f, 0.02f, 0.9f, new Color(0.46f, 1f, 0.66f));
                yield return null;
            }
            handoffPoint = transform.position;
            handoffTime = Time.unscaledTime;
            Destroy(gameObject);
        }

        private void ReleaseShot()
        {
            if (giftCam != null) Destroy(giftCam.gameObject);
            giftCam = null;
        }

        // ================================================================== held

        private void LateUpdate()
        {
            if (vanishing || receiver == null || model == null || glow != null) return;
            // Held: follow his hands as he breathes and shifts.
            transform.position = Vector3.Lerp(transform.position, HandsPoint(), 1f - Mathf.Exp(-12f * Time.deltaTime));
            transform.rotation = Quaternion.Slerp(transform.rotation, HoldRotation(), 1f - Mathf.Exp(-8f * Time.deltaTime));
            // The UI roll will fly out from here.
            Camera cam = giftCam != null ? giftCam : FxQuad.ResolveCamera(Camera.main);
            if (cam != null)
            {
                Vector3 sp = cam.WorldToScreenPoint(transform.position);
                if (sp.z > 0f) CodexBook.SetNextOpenOrigin(new Vector2(sp.x, sp.y));
            }
        }

        private void OnRollOpened(bool open)
        {
            if (!open || vanishing) return;
            CodexBook.OpenChanged -= OnRollOpened;
            StartCoroutine(Vanish());
        }

        private IEnumerator Vanish()
        {
            vanishing = true;
            handoffPoint = transform.position;
            handoffTime = Time.unscaledTime;
            MagicFx.FlareAt(transform.position, 0.5f, 0.35f, MagicPalette.GoldPale);

            const float dur = 0.22f;
            Vector3 s = model.localScale;
            for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
            {
                model.localScale = Vector3.Lerp(s, s * 0.6f, t / dur);
                SetEmission(Mathf.Lerp(0.06f, 2.5f, t / dur));
                yield return null;
            }
            model.gameObject.SetActive(false);
            // Hold the two-shot until the roll is fully open over it, so the cut back to the scene camera is unseen.
            for (float t = 0f; t < 2.5f && !CodexBook.PagesVisible; t += Time.unscaledDeltaTime) yield return null;
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            ReleaseShot();
            CodexBook.OpenChanged -= OnRollOpened;
            if (held == this) held = null;
        }

        // ================================================================== geometry

        private static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float t)
        {
            float u = 1f - t;
            return u * u * a + 2f * u * t * b + t * t * c;
        }

        private static Vector3 PalmPoint(Transform giver, Animator anim)
        {
            if (anim != null && anim.isHuman)
            {
                Transform hand = anim.GetBoneTransform(HumanBodyBones.RightHand);
                if (hand != null) return hand.position + Vector3.up * 0.22f + giver.forward * 0.06f;
            }
            return giver.position + Vector3.up * 1.45f + giver.forward * 0.5f + giver.right * 0.18f;
        }

        private Vector3 HandsPoint()
        {
            if (receiverAnim != null && receiverAnim.isHuman)
            {
                Transform l = receiverAnim.GetBoneTransform(HumanBodyBones.LeftHand);
                Transform r = receiverAnim.GetBoneTransform(HumanBodyBones.RightHand);
                if (l != null && r != null)
                {
                    Vector3 mid = (l.position + r.position) * 0.5f;
                    // Keep it in front of the body even when the arms hang at his sides.
                    Vector3 chest = receiver.position + Vector3.up * 1.05f + receiver.forward * 0.32f;
                    return Vector3.Distance(l.position, r.position) < 0.45f ? mid + Vector3.up * 0.05f : chest;
                }
            }
            return receiver.position + Vector3.up * 1.05f + receiver.forward * 0.32f;
        }

        private Quaternion HoldRotation() =>
            receiver != null ? Quaternion.LookRotation(receiver.forward, Vector3.up) : transform.rotation;

        // ================================================================== the object

        private void BuildModel()
        {
            model = CreateRollModel(transform);
            renderers = model.GetComponentsInChildren<Renderer>();
            model.localScale = Vector3.one * 0.001f;
        }

        private static Transform BuildProcedural(Transform parent)
        {
            var root = new GameObject("RollProcedural").transform;
            root.SetParent(parent, false);

            Material papyrus = LitMaterial(new Color(0.86f, 0.74f, 0.52f), 0.25f);
            Material ebony = LitMaterial(new Color(0.07f, 0.05f, 0.04f), 0.6f);
            Material gold = LitMaterial(new Color(0.85f, 0.62f, 0.22f), 0.85f, 0.9f);
            // Zac's pick (roll E): cream linen cord, green and gold bands, a green heart scarab on the cord.
            Material linen = LitMaterial(new Color(0.86f, 0.80f, 0.66f), 0.2f);
            Material green = LitMaterial(new Color(0.10f, 0.42f, 0.26f), 0.75f, 0.2f);

            // Papyrus body: long axis on X.
            Piece(root, PrimitiveType.Cylinder, papyrus, Vector3.zero, new Vector3(0f, 0f, 90f), new Vector3(0.095f, 0.15f, 0.095f));
            // The outer sheet's edge, a thin lip along the body.
            Piece(root, PrimitiveType.Cube, papyrus, new Vector3(0f, 0.02f, 0.046f), new Vector3(0f, 0f, 0f), new Vector3(0.30f, 0.012f, 0.004f));
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * 0.165f;
                Piece(root, PrimitiveType.Cylinder, ebony, new Vector3(x, 0f, 0f), new Vector3(0f, 0f, 90f), new Vector3(0.11f, 0.018f, 0.11f));
                Piece(root, PrimitiveType.Cylinder, gold, new Vector3(x + side * 0.02f, 0f, 0f), new Vector3(0f, 0f, 90f), new Vector3(0.06f, 0.006f, 0.06f));
                Piece(root, PrimitiveType.Sphere, gold, new Vector3(x + side * 0.034f, 0f, 0f), Vector3.zero, new Vector3(0.032f, 0.036f, 0.036f));
                // Banding just inside each cap: green between two gold rings.
                float bx = side * 0.135f;
                Piece(root, PrimitiveType.Cylinder, gold, new Vector3(bx - side * 0.012f, 0f, 0f), new Vector3(0f, 0f, 90f), new Vector3(0.098f, 0.003f, 0.098f));
                Piece(root, PrimitiveType.Cylinder, green, new Vector3(bx, 0f, 0f), new Vector3(0f, 0f, 90f), new Vector3(0.097f, 0.009f, 0.097f));
                Piece(root, PrimitiveType.Cylinder, gold, new Vector3(bx + side * 0.012f, 0f, 0f), new Vector3(0f, 0f, 90f), new Vector3(0.098f, 0.003f, 0.098f));
            }
            // Cord, two turns, and the heart scarab in a gold setting.
            for (int i = 0; i < 2; i++)
                Piece(root, PrimitiveType.Cylinder, linen, new Vector3(-0.012f + i * 0.02f, 0f, 0f), new Vector3(0f, 0f, 90f), new Vector3(0.1f, 0.004f, 0.1f));
            Piece(root, PrimitiveType.Sphere, gold, new Vector3(0.004f, -0.012f, 0.05f), Vector3.zero, new Vector3(0.034f, 0.038f, 0.012f));
            Piece(root, PrimitiveType.Sphere, green, new Vector3(0.004f, -0.012f, 0.055f), Vector3.zero, new Vector3(0.026f, 0.03f, 0.012f));
            return root;
        }

        private static void Piece(Transform parent, PrimitiveType type, Material m, Vector3 pos, Vector3 euler, Vector3 scale)
        {
            GameObject g = GameObject.CreatePrimitive(type);
            g.name = type.ToString();
            Collider c = g.GetComponent<Collider>();
            if (c != null) Destroy(c);
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localEulerAngles = euler;
            g.transform.localScale = scale;
            g.GetComponent<Renderer>().sharedMaterial = m;
        }

        private static Material LitMaterial(Color c, float smooth, float metal = 0f)
        {
            Shader s = Shader.Find("Universal Render Pipeline/Lit");
            if (s == null) s = Shader.Find("Standard");
            var m = new Material(s) { name = "Gift " + c, hideFlags = HideFlags.DontSave };
            m.SetColor("_BaseColor", c);
            m.SetColor("_Color", c);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Glossiness", smooth);
            m.SetFloat("_Metallic", metal);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            return m;
        }

        private void SetEmission(float intensity)
        {
            if (renderers == null) return;
            Color e = MagicPalette.Hdr(new Color(1f, 0.76f, 0.38f), intensity);
            foreach (Renderer r in renderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetColor(MagicMaterials.EmissionColorId, e);
                r.SetPropertyBlock(block);
            }
        }
    }
}
