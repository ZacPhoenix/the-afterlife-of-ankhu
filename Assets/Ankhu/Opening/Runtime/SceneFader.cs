using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Ankhu.Opening
{
    /// <summary>
    /// One black overlay that survives scene loads. FadeTo fades out, loads the scene and fades back in.
    /// Created on first use, so scenes do not need to contain it.
    /// </summary>
    public sealed class SceneFader : MonoBehaviour
    {
        private static SceneFader instance;
        private Image black;
        private bool busy;

        public static bool Busy => instance != null && instance.busy;

        private static SceneFader Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("SceneFader");
                    DontDestroyOnLoad(go);
                    instance = go.AddComponent<SceneFader>();
                    Canvas c = UiKit.Canvas("FaderCanvas", 1000, go.transform);
                    instance.black = UiKit.Solid("Black", c.transform, new Color(0f, 0f, 0f, 0f));
                    UiKit.Stretch(instance.black.rectTransform);
                }
                return instance;
            }
        }

        /// <summary>Sets the overlay to fully black at once (for scenes that start dark and fade in).</summary>
        public static void SetBlack(float alpha)
        {
            Instance.black.color = new Color(0f, 0f, 0f, alpha);
        }

        public static void FadeIn(float seconds)
        {
            SceneFader f = Instance;
            f.StartCoroutine(f.Fade(f.black.color.a, 0f, seconds));
        }

        public static void FadeTo(string scene, float outSeconds = 1f, float inSeconds = 1f)
        {
            SceneFader f = Instance;
            if (f.busy) return;
            f.StartCoroutine(f.Run(scene, outSeconds, inSeconds));
        }

        private IEnumerator Run(string scene, float outSeconds, float inSeconds)
        {
            busy = true;
            yield return Fade(black.color.a, 1f, outSeconds);
            AsyncOperation op = SceneManager.LoadSceneAsync(scene);
            while (op != null && !op.isDone) yield return null;
            yield return null;
            busy = false;
            yield return Fade(1f, 0f, inSeconds);
        }

        private IEnumerator Fade(float from, float to, float seconds)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / Mathf.Max(0.0001f, seconds)));
                black.color = new Color(0f, 0f, 0f, Mathf.Lerp(from, to, k));
                yield return null;
            }
            black.color = new Color(0f, 0f, 0f, to);
        }
    }
}
