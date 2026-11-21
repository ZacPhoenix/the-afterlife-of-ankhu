using System.Collections.Generic;
using Ankhu.Layout;
using UnityEngine;

namespace Ankhu.Player
{
    /// <summary>
    /// Presentation of Ankhu on the "Visual" child of the player. Gameplay never reads it.
    ///
    /// With no <see cref="ModelPrefab"/> it builds a greybox 3D placeholder from primitives: a bronze
    /// capsule body, a white apron cylinder with a white belt band, a bronze sphere head with a nose
    /// that shows facing. With a prefab it instantiates the model, measures its renderer bounds and
    /// scales it uniformly to <see cref="ModelHeight"/> with the feet at the origin.
    ///
    /// The built content is marked DontSave and rebuilt on enable, so a saved scene never stores
    /// runtime meshes, materials or model instances. Models are expected to be authored facing +z.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class AnkhuVisual : MonoBehaviour
    {
        private const string ContentName = "Ankhu Model";
        private const float TurnDegreesPerSecond = 900f;

        private static readonly int SpeedId = Animator.StringToHash("Speed");
        private static readonly int ClimbingId = Animator.StringToHash("Climbing");
        private static readonly int GroundedId = Animator.StringToHash("Grounded");

        private static readonly Dictionary<Color, Material> materialCache = new Dictionary<Color, Material>();

        [SerializeField] private GameObject modelPrefab;
        [SerializeField, Min(0.1f)] private float modelHeight = BorrowedDawnLayout.AnkhuHeight;
        [Tooltip("Extra yaw in degrees added to every facing, for models not authored facing +z.")]
        [SerializeField] private float baseYaw;

        private GameObject content;
        private Animator animator;
        private bool hasSpeed;
        private bool hasClimbing;
        private bool hasGrounded;
        private float targetYaw = 90f;
        private float currentYaw = 90f;

        public GameObject ModelPrefab => modelPrefab;
        public float ModelHeight => modelHeight;
        public float BaseYaw => baseYaw;
        public Animator Animator => animator;
        public GameObject Content => content;

        public void Configure(GameObject prefab, float height, float yawOffset)
        {
            modelPrefab = prefab;
            modelHeight = Mathf.Max(0.1f, height);
            baseYaw = yawOffset;
            if (isActiveAndEnabled)
            {
                Rebuild();
            }
        }

        private void OnEnable()
        {
            Rebuild();
        }

        private void OnDisable()
        {
            DestroyContent();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                return;
            }
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null && isActiveAndEnabled)
                {
                    Rebuild();
                }
            };
        }
#endif

        /// <summary>Faces east (+1) or west (-1). While climbing Ankhu faces the wall (away from the camera).</summary>
        public void SetFacing(int direction, bool climbing, bool instant)
        {
            float side = direction < 0 ? -90f : 90f;
            targetYaw = baseYaw + (climbing ? 0f : side);
            if (instant || !Application.isPlaying)
            {
                currentYaw = targetYaw;
            }
            else
            {
                currentYaw = Mathf.MoveTowardsAngle(currentYaw, targetYaw, TurnDegreesPerSecond * Time.deltaTime);
            }
            transform.localRotation = Quaternion.Euler(0f, currentYaw, 0f);
        }

        /// <summary>Passes motion to the model's Animator. Parameters the controller lacks are skipped.</summary>
        public void SetMotion(float speed, bool climbing, bool grounded)
        {
            if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null)
            {
                return;
            }
            if (hasSpeed)
            {
                animator.SetFloat(SpeedId, speed);
            }
            if (hasClimbing)
            {
                animator.SetBool(ClimbingId, climbing);
            }
            if (hasGrounded)
            {
                animator.SetBool(GroundedId, grounded);
            }
        }

        /// <summary>Destroys and rebuilds the model or placeholder.</summary>
        public void Rebuild()
        {
            DestroyContent();
            content = modelPrefab != null ? BuildFromPrefab() : BuildPlaceholder();
            MarkDontSave(content.transform);
            CacheAnimator();
            transform.localRotation = Quaternion.Euler(0f, currentYaw, 0f);
        }

        private void DestroyContent()
        {
            if (content != null)
            {
                DestroyImmediate(content);
            }
            content = null;
            animator = null;
            hasSpeed = false;
            hasClimbing = false;
            hasGrounded = false;
            // Remove strays left by an interrupted rebuild.
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Transform child = transform.GetChild(i);
                if (child.name == ContentName && (child.gameObject.hideFlags & HideFlags.DontSave) != 0)
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }

        private void CacheAnimator()
        {
            animator = content.GetComponentInChildren<Animator>(true);
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                return;
            }
            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                AnimatorControllerParameter p = parameters[i];
                if (p.nameHash == SpeedId && p.type == AnimatorControllerParameterType.Float)
                {
                    hasSpeed = true;
                }
                else if (p.nameHash == ClimbingId && p.type == AnimatorControllerParameterType.Bool)
                {
                    hasClimbing = true;
                }
                else if (p.nameHash == GroundedId && p.type == AnimatorControllerParameterType.Bool)
                {
                    hasGrounded = true;
                }
            }
        }

        private GameObject BuildFromPrefab()
        {
            GameObject instance = Instantiate(modelPrefab, transform, false);
            instance.name = ContentName;
            Transform t = instance.transform;
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;

            // Physics for the model is never wanted. Gameplay uses the 2D capsule on the root.
            foreach (Collider c in instance.GetComponentsInChildren<Collider>(true))
            {
                DestroyImmediate(c);
            }

            if (!TryGetRendererBounds(instance, out Bounds bounds) || bounds.size.y < 1e-4f)
            {
                Debug.LogWarning("AnkhuVisual: the model prefab has no renderer bounds, so it was not scaled.", this);
                return instance;
            }

            // The Visual root only yaws, so the world height of the instance is its unscaled height
            // times the parent scale. Scale it so the world height equals modelHeight times that scale.
            float parentScaleY = Mathf.Approximately(transform.lossyScale.y, 0f) ? 1f : Mathf.Abs(transform.lossyScale.y);
            float factor = modelHeight * parentScaleY / bounds.size.y;
            t.localScale = Vector3.one * factor;

            TryGetRendererBounds(instance, out bounds);
            Vector3 root = transform.position;
            Vector3 shift = new Vector3(root.x - bounds.center.x, root.y - bounds.min.y, root.z - bounds.center.z);
            t.position += shift;
            return instance;
        }

        private static bool TryGetRendererBounds(GameObject go, out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer)
                {
                    continue;
                }
                if (!found)
                {
                    bounds = r.bounds;
                    found = true;
                }
                else
                {
                    bounds.Encapsulate(r.bounds);
                }
            }
            return found;
        }

        private GameObject BuildPlaceholder()
        {
            var root = new GameObject(ContentName);
            root.transform.SetParent(transform, false);
            float s = modelHeight / BorrowedDawnLayout.AnkhuHeight;
            root.transform.localScale = new Vector3(s, s, s);

            Color bronze = new Color(0.70f, 0.44f, 0.23f);
            Color bronzeLight = new Color(0.76f, 0.50f, 0.28f);
            Color bronzeDark = new Color(0.55f, 0.33f, 0.17f);
            Color linen = new Color(0.94f, 0.93f, 0.88f);
            Color white = new Color(1f, 1f, 1f);
            Color ink = new Color(0.08f, 0.06f, 0.05f);

            // Primitive sizes: capsule and cylinder are 2 units tall with radius 0.5, sphere and cube are 1 unit.
            Part(root, PrimitiveType.Capsule, "Body", new Vector3(0f, 0.80f, 0f), new Vector3(0.36f, 0.70f, 0.28f), bronze);
            Part(root, PrimitiveType.Capsule, "Arm L", new Vector3(-0.25f, 1.10f, 0f), new Vector3(0.10f, 0.30f, 0.10f), bronzeDark);
            Part(root, PrimitiveType.Capsule, "Arm R", new Vector3(0.25f, 1.10f, 0f), new Vector3(0.10f, 0.30f, 0.10f), bronzeDark);
            Part(root, PrimitiveType.Cylinder, "Apron", new Vector3(0f, 0.76f, 0f), new Vector3(0.46f, 0.21f, 0.36f), linen);
            Part(root, PrimitiveType.Cube, "Apron Front", new Vector3(0f, 0.70f, 0.17f), new Vector3(0.16f, 0.46f, 0.04f), linen);
            Part(root, PrimitiveType.Cylinder, "Belt", new Vector3(0f, 0.99f, 0f), new Vector3(0.48f, 0.035f, 0.38f), white);
            Part(root, PrimitiveType.Cube, "Foot L", new Vector3(-0.09f, 0.04f, 0.05f), new Vector3(0.12f, 0.08f, 0.24f), bronzeDark);
            Part(root, PrimitiveType.Cube, "Foot R", new Vector3(0.09f, 0.04f, 0.05f), new Vector3(0.12f, 0.08f, 0.24f), bronzeDark);
            Part(root, PrimitiveType.Sphere, "Head", new Vector3(0f, 1.64f, 0.01f), new Vector3(0.30f, 0.32f, 0.30f), bronzeLight);
            Part(root, PrimitiveType.Sphere, "Nose", new Vector3(0f, 1.63f, 0.16f), new Vector3(0.06f, 0.07f, 0.07f), bronzeLight);
            Part(root, PrimitiveType.Sphere, "Eye L", new Vector3(-0.06f, 1.69f, 0.13f), new Vector3(0.035f, 0.035f, 0.035f), ink);
            Part(root, PrimitiveType.Sphere, "Eye R", new Vector3(0.06f, 1.69f, 0.13f), new Vector3(0.035f, 0.035f, 0.035f), ink);
            return root;
        }

        private static void Part(GameObject root, PrimitiveType type, string name, Vector3 position, Vector3 scale, Color color)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            Collider collider = part.GetComponent<Collider>();
            if (collider != null)
            {
                DestroyImmediate(collider);
            }
            part.transform.SetParent(root.transform, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            var renderer = part.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = MaterialFor(color);
        }

        private static Material MaterialFor(Color color)
        {
            if (materialCache.TryGetValue(color, out Material cached) && cached != null)
            {
                return cached;
            }
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }
            var material = new Material(shader)
            {
                name = "Ankhu Placeholder",
                hideFlags = HideFlags.HideAndDontSave
            };
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }
            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", 0.25f);
            }
            if (material.HasProperty("_Glossiness"))
            {
                material.SetFloat("_Glossiness", 0.25f);
            }
            materialCache[color] = material;
            return material;
        }

        private static void MarkDontSave(Transform t)
        {
            t.gameObject.hideFlags = HideFlags.DontSave;
            for (int i = 0; i < t.childCount; i++)
            {
                MarkDontSave(t.GetChild(i));
            }
        }
    }
}
