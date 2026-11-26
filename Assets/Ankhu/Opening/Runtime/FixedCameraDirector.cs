using System;
using UnityEngine;

namespace Ankhu.Opening
{
    /// <summary>
    /// Fixed-camera director in the survival-horror tradition: one authored camera per shot, a hard cut when the
    /// player crosses into the next shot's trigger volume. Each shot can pan slightly to keep the player framed
    /// (a held tripod with a light operator, never a chase cam). Only the active shot's camera renders.
    /// </summary>
    public sealed class FixedCameraDirector : MonoBehaviour
    {
        [Serializable]
        public sealed class Shot
        {
            public string name;
            public Camera camera;
            [Tooltip("0 = locked off. 1 = turns fully to the player. Small values feel like a tripod pan.")]
            [Range(0f, 1f)] public float follow = 0.15f;
            [Tooltip("Height above the player's feet that the pan aims at.")]
            public float aimHeight = 1.2f;
        }

        [SerializeField] private Shot[] shots = Array.Empty<Shot>();
        [SerializeField] private Transform player;

        private Quaternion[] baseRotations;
        private int active = -1;

        public event Action<int, int> Cut;

        public int Active => active;
        public Camera ActiveCamera => active >= 0 ? shots[active].camera : null;
        public int Count => shots.Length;

        public void Configure(Shot[] newShots, Transform target)
        {
            shots = newShots;
            player = target;
        }

        private void Awake()
        {
            baseRotations = new Quaternion[shots.Length];
            for (int i = 0; i < shots.Length; i++)
            {
                baseRotations[i] = shots[i].camera.transform.rotation;
                shots[i].camera.enabled = false;
                ShowBackdrop(i, false);
                var listener = shots[i].camera.GetComponent<AudioListener>();
                if (listener != null) Destroy(listener);
            }
        }

        private void Start()
        {
            if (active < 0) CutTo(0);
        }

        public void CutTo(int index)
        {
            if (index == active || index < 0 || index >= shots.Length) return;
            int previous = active;
            if (active >= 0) { shots[active].camera.enabled = false; ShowBackdrop(active, false); }
            active = index;
            Shot s = shots[active];
            s.camera.enabled = true;
            ShowBackdrop(active, true);
            s.camera.transform.rotation = AimRotation(active, 1f);
            Cut?.Invoke(previous, active);
        }

        // Every painted backdrop is a quad in the world 300 m out from its own camera, so the active camera would
        // otherwise see the other cameras' paintings. Only the active shot's backdrop is shown.
        private void ShowBackdrop(int i, bool on)
        {
            // The painting (Backdrop or the Wernes "Painting" quad) and any foreground mattes (Backdrop_FG) belong to this camera only.
            foreach (Transform b in shots[i].camera.transform)
                if (b.name.StartsWith("Backdrop") || b.name == "Painting") b.gameObject.SetActive(on);
        }

        private Quaternion AimRotation(int i, float blend)
        {
            Shot s = shots[i];
            Quaternion baseRot = baseRotations[i];
            if (player == null || s.follow <= 0f || OpeningSettings.ReducedMotion) return baseRot;
            Vector3 to = player.position + Vector3.up * s.aimHeight - s.camera.transform.position;
            if (to.sqrMagnitude < 0.01f) return baseRot;
            Quaternion look = Quaternion.LookRotation(to.normalized, Vector3.up);
            return Quaternion.Slerp(baseRot, look, s.follow * blend);
        }

        private void LateUpdate()
        {
            if (active < 0) return;
            Transform cam = shots[active].camera.transform;
            Quaternion want = AimRotation(active, 1f);
            cam.rotation = Quaternion.Slerp(cam.rotation, want, 1f - Mathf.Exp(-Time.deltaTime * 2.2f));
        }
    }
}
