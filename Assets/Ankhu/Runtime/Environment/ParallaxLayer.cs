using UnityEngine;

namespace Ankhu.Environment
{
    /// <summary>
    /// Side-on parallax for one plate. factor 1 = moves with the world (play layer),
    /// below 1 = further away, above 1 = foreground. Tileable plates wrap with a SpriteRenderer
    /// in Tiled draw mode, so they cover any level width.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class ParallaxLayer : MonoBehaviour
    {
        [SerializeField] private float factor = 1f;
        [SerializeField] private bool tileable = true;
        [SerializeField] private Transform cameraTransform;

        private Vector3 origin;
        private float camStartX;
        private float tileWidth;
        private SpriteRenderer sr;

        public void Configure(float parallaxFactor, bool isTileable)
        {
            factor = parallaxFactor;
            tileable = isTileable;
        }

        private void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
            origin = transform.position;
            camStartX = cameraTransform != null ? cameraTransform.position.x : 0f;
            tileWidth = sr.sprite != null ? sr.sprite.bounds.size.x : 0f;
            if (tileable && sr.sprite != null)
            {
                sr.drawMode = SpriteDrawMode.Tiled;
                sr.tileMode = SpriteTileMode.Continuous;
                sr.size = new Vector2(tileWidth * 3f, sr.sprite.bounds.size.y);
            }
        }

        private void LateUpdate()
        {
            if (cameraTransform == null) return;
            float camDx = cameraTransform.position.x - camStartX;
            // Layer moves by (1 - factor) of the camera motion so it appears to scroll at `factor`.
            float x = origin.x + camDx * (1f - factor);
            if (tileable && tileWidth > 0f)
            {
                // Bottom-left pivot: the 3-tile strip spans x .. x + 3w. Keep its centre near the camera.
                float rel = cameraTransform.position.x - (x + 1.5f * tileWidth);
                x += Mathf.Floor(rel / tileWidth + 0.5f) * tileWidth;
            }
            transform.position = new Vector3(x, origin.y, origin.z);
        }
    }
}
