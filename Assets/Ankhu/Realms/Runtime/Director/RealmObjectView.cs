using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Ankhu.Realms
{
    /// <summary>
    /// The visible side of one rules object. Each state has a pose: parts to move, objects to show or hide.
    /// The director calls <see cref="SetState"/> when the rules change it. The first call snaps.
    /// </summary>
    public sealed class RealmObjectView : MonoBehaviour
    {
        [Serializable]
        public sealed class PartPose
        {
            public Transform part;
            public Vector3 localPosition;
            public Vector3 localEuler;
            public Vector3 localScale = Vector3.one;
        }

        [Serializable]
        public sealed class StatePose
        {
            public string state;
            public List<PartPose> parts = new List<PartPose>();
            public List<GameObject> show = new List<GameObject>();
            public List<GameObject> hide = new List<GameObject>();
            /// <summary>Light intensities to blend to (light, intensity).</summary>
            public List<Light> lights = new List<Light>();
            public List<float> lightIntensities = new List<float>();
            public float seconds = 1.6f;
        }

        public string objectId = "";
        public List<StatePose> poses = new List<StatePose>();

        private string current;
        private Coroutine running;

        public string Current => current;
        public event Action<string, string> StateShown;

        public void SetState(string state, bool snap)
        {
            if (state == current) return;
            string prev = current;
            current = state;
            StatePose pose = poses.Find(p => p.state == state);
            if (pose == null) return;
            if (running != null) StopCoroutine(running);
            if (snap || !isActiveAndEnabled) Apply(pose, 1f, null);
            else running = StartCoroutine(Animate(pose));
            StateShown?.Invoke(prev, state);
        }

        private IEnumerator Animate(StatePose pose)
        {
            var from = new List<(Vector3 p, Quaternion r, Vector3 s)>();
            foreach (PartPose pp in pose.parts)
                from.Add(pp.part != null ? (pp.part.localPosition, pp.part.localRotation, pp.part.localScale) : default);
            var lightFrom = new List<float>();
            foreach (Light l in pose.lights) lightFrom.Add(l != null ? l.intensity : 0f);
            foreach (GameObject g in pose.show) if (g != null) g.SetActive(true);
            float t = 0f;
            float dur = Mathf.Max(0.05f, pose.seconds);
            while (t < 1f)
            {
                t = Mathf.Min(1f, t + Time.deltaTime / dur);
                float e = t * t * (3f - 2f * t);
                for (int i = 0; i < pose.parts.Count; i++)
                {
                    PartPose pp = pose.parts[i];
                    if (pp.part == null) continue;
                    pp.part.localPosition = Vector3.Lerp(from[i].p, pp.localPosition, e);
                    pp.part.localRotation = Quaternion.Slerp(from[i].r, Quaternion.Euler(pp.localEuler), e);
                    pp.part.localScale = Vector3.Lerp(from[i].s, pp.localScale, e);
                }
                for (int i = 0; i < pose.lights.Count; i++)
                    if (pose.lights[i] != null && i < pose.lightIntensities.Count)
                        pose.lights[i].intensity = Mathf.Lerp(lightFrom[i], pose.lightIntensities[i], e);
                yield return null;
            }
            foreach (GameObject g in pose.hide) if (g != null) g.SetActive(false);
            running = null;
        }

        private static void Apply(StatePose pose, float e, object _)
        {
            foreach (PartPose pp in pose.parts)
            {
                if (pp.part == null) continue;
                pp.part.localPosition = pp.localPosition;
                pp.part.localRotation = Quaternion.Euler(pp.localEuler);
                pp.part.localScale = pp.localScale;
            }
            for (int i = 0; i < pose.lights.Count; i++)
                if (pose.lights[i] != null && i < pose.lightIntensities.Count) pose.lights[i].intensity = pose.lightIntensities[i];
            foreach (GameObject g in pose.show) if (g != null) g.SetActive(true);
            foreach (GameObject g in pose.hide) if (g != null) g.SetActive(false);
        }
    }
}
