using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Ankhu.Memories
{
    /// <summary>
    /// Plays one authored memory flashback (painted film rendered in the opening cinematic's style) full screen over
    /// whatever scene is running, with its baked music, voice and ambience, and the subtitles from its cue file.
    /// Usage from any room: MemoryFlashback.Play("memA_river_sunset", () => { /* back in the room */ });
    /// Clips live in Resources/Memories/<id>.mp4 is NOT used (video in Resources is slow): they sit in
    /// StreamingAssets/Memories/<id>.mp4 so the VideoPlayer streams them by URL. Cues: Resources/Memories/<id>_cues.json.
    /// </summary>
    public class MemoryFlashback : MonoBehaviour
    {
        [Serializable] public class Cue { public float t0; public float t1; public string speaker; public string text; }
        [Serializable] class CueFile { public Cue[] cues; }

        public static bool IsPlaying { get; private set; }
        public static event Action<string> Started;
        public static event Action<string> Finished;

        /// <summary>Hold this long (any key or the submit button) to skip. Set to 0 to make a memory unskippable.</summary>
        public static float HoldToSkipSeconds = 1.2f;

        VideoPlayer _vp; RawImage _img; Text _sub; Image _skipBar; CanvasGroup _group;
        RenderTexture _rt; Cue[] _cues = new Cue[0]; string _id; Action _onDone; float _hold; bool _ending;

        public static MemoryFlashback Play(string id, Action onDone = null)
        {
            var go = new GameObject("MemoryFlashback_" + id);
            DontDestroyOnLoad(go);
            var m = go.AddComponent<MemoryFlashback>();
            m.Begin(id, onDone);
            return m;
        }

        void Begin(string id, Action onDone)
        {
            _id = id; _onDone = onDone; IsPlaying = true;
            BuildUi();
            var cueAsset = Resources.Load<TextAsset>("Memories/" + id + "_cues");
            if (cueAsset != null) _cues = JsonUtility.FromJson<CueFile>(cueAsset.text).cues ?? new Cue[0];

            _rt = new RenderTexture(1920, 1080, 0) { name = "MemoryFlashbackRT" };
            _img.texture = _rt;
            _vp = gameObject.AddComponent<VideoPlayer>();
            _vp.playOnAwake = false;
            _vp.source = VideoSource.Url;
            _vp.url = System.IO.Path.Combine(Application.streamingAssetsPath, "Memories", id + ".mp4");
            _vp.renderMode = VideoRenderMode.RenderTexture;
            _vp.targetTexture = _rt;
            _vp.audioOutputMode = VideoAudioOutputMode.Direct;
            _vp.isLooping = false;
            _vp.skipOnDrop = true;
            _vp.prepareCompleted += p => { _group.alpha = 1f; p.Play(); Started?.Invoke(_id); };
            _vp.loopPointReached += p => End();
            _vp.errorReceived += (p, msg) => { Debug.LogWarning($"[MemoryFlashback] {_id}: {msg}"); End(); };
            _group.alpha = 0f;
            _vp.Prepare();
        }

        void BuildUi()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1f;
            _group = gameObject.AddComponent<CanvasGroup>();

            var bg = NewChild("Black", out RectTransform bgRt).AddComponent<Image>();
            bg.color = Color.black; Stretch(bgRt);

            // The film is 16:9 with the 2.39 letterbox baked in. Fit it inside the screen, keeping aspect.
            var vid = NewChild("Film", out RectTransform vRt);
            _img = vid.AddComponent<RawImage>();
            var fit = vid.AddComponent<AspectRatioFitter>();
            fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent; fit.aspectRatio = 16f / 9f;
            Stretch(vRt);

            var subGo = NewChild("Subtitle", out RectTransform sRt);
            _sub = subGo.AddComponent<Text>();
            _sub.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _sub.fontSize = 34; _sub.alignment = TextAnchor.MiddleCenter; _sub.color = new Color(0.96f, 0.91f, 0.80f);
            _sub.fontStyle = FontStyle.Italic;
            var sh = subGo.AddComponent<Shadow>(); sh.effectColor = new Color(0, 0, 0, 0.8f); sh.effectDistance = new Vector2(2, -2);
            sRt.anchorMin = new Vector2(0.1f, 0f); sRt.anchorMax = new Vector2(0.9f, 0f);
            sRt.pivot = new Vector2(0.5f, 0f); sRt.anchoredPosition = new Vector2(0, 40); sRt.sizeDelta = new Vector2(0, 90);

            var barGo = NewChild("SkipBar", out RectTransform bRt);
            _skipBar = barGo.AddComponent<Image>();
            _skipBar.color = new Color(0.95f, 0.78f, 0.35f, 0.8f);
            bRt.anchorMin = new Vector2(0.94f, 0.03f); bRt.anchorMax = new Vector2(0.94f, 0.03f);
            bRt.pivot = new Vector2(1f, 0f); bRt.sizeDelta = new Vector2(0, 4);
        }

        GameObject NewChild(string name, out RectTransform rt)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            rt = (RectTransform)go.transform; return go;
        }

        static void Stretch(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; }

        void Update()
        {
            if (_vp == null || _ending) return;
            float t = (float)_vp.time;
            string line = "";
            foreach (var c in _cues) if (t >= c.t0 && t < c.t1) { line = c.text; break; }
            _sub.text = line;

            if (HoldToSkipSeconds > 0f && _vp.isPlaying)
            {
                bool held = Input.anyKey; // legacy input is enabled in this project's player settings (Both)
                _hold = held ? _hold + Time.unscaledDeltaTime : 0f;
                ((RectTransform)_skipBar.transform).sizeDelta = new Vector2(220f * Mathf.Clamp01(_hold / HoldToSkipSeconds), 4);
                if (_hold >= HoldToSkipSeconds) End();
            }
        }

        public void Skip() => End();

        void End()
        {
            if (_ending) return;
            _ending = true;
            StartCoroutine(FadeAndFinish());
        }

        IEnumerator FadeAndFinish()
        {
            for (float k = 1f; k > 0f; k -= Time.unscaledDeltaTime / 0.5f)
            {
                _group.alpha = k;
                if (_vp != null) _vp.SetDirectAudioVolume(0, k);
                yield return null;
            }
            if (_vp != null) _vp.Stop();
            if (_rt != null) _rt.Release();
            IsPlaying = false;
            Finished?.Invoke(_id);
            var cb = _onDone; Destroy(gameObject);
            cb?.Invoke();
        }
    }
}
