using UnityEngine;

namespace Ankhu.Realms
{
    /// <summary>A slow bob for things afloat (the ferry).</summary>
    public sealed class Bob : MonoBehaviour
    {
        public float amplitude = 0.04f;
        public float roll = 1.5f;
        private Vector3 basePos;
        private Quaternion baseRot;

        private void Awake()
        {
            basePos = transform.localPosition;
            baseRot = transform.localRotation;
        }

        private void Update()
        {
            float t = Time.time;
            transform.localPosition = basePos + new Vector3(0f, Mathf.Sin(t * 0.9f) * amplitude, 0f);
            transform.localRotation = baseRot * Quaternion.Euler(0f, 0f, Mathf.Sin(t * 0.7f) * roll);
        }
    }
}
