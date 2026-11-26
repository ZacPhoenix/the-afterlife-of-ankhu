using UnityEngine;

namespace Ankhu.Opening
{
    /// <summary>
    /// Soft contact shadow under a character standing on a painted backdrop (the walk ground is invisible, so real
    /// shadows have nothing to land on). Follows the target's feet on the ground plane and fades when it moves.
    /// </summary>
    public sealed class BlobShadow : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private float groundY;
        [SerializeField] private float radius = 0.55f;
        [SerializeField] private float strength = 0.55f;

        private MeshRenderer quad;
        private MaterialPropertyBlock block;
        private static readonly int ColorId = Shader.PropertyToID("_BaseColor");

        public void Configure(Transform t, float ground, float r, float s)
        {
            target = t;
            groundY = ground;
            radius = r;
            strength = s;
        }

        private void Awake()
        {
            quad = GetComponent<MeshRenderer>();
            block = new MaterialPropertyBlock();
        }

        private void LateUpdate()
        {
            if (target == null) return;
            Vector3 p = target.position;
            transform.position = new Vector3(p.x, groundY + 0.015f, p.z);
            transform.rotation = Quaternion.Euler(90f, target.eulerAngles.y, 0f);
            transform.localScale = new Vector3(radius * 2f, radius * 2.4f, 1f);
            if (quad != null)
            {
                quad.GetPropertyBlock(block);
                block.SetColor(ColorId, new Color(0f, 0f, 0f, strength));
                quad.SetPropertyBlock(block);
            }
        }
    }
}
