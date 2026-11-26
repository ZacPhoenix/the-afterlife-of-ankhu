using System;
using UnityEngine;
using UnityEngine.Video;

namespace Ankhu.Opening
{
    /// <summary>
    /// Plays the painted Spell 25 opening. The picture is an offline render (art_gen/cinematic, the layer renderer)
    /// played by a VideoPlayer slaved to the audio DSP clock: the music is scheduled with PlayScheduled and the
    /// video's external reference time is set from AudioSettings.dspTime every frame, so picture and music cannot
    /// drift. Voice lines are scheduled on the same clock. Hold any key or button for one second to skip.
    /// At the handoff time the scene loads the walk, which opens the letterbox on the DUAT hit.
    /// </summary>
    public sealed class OpeningCinematic : MonoBehaviour
    {
        [Serializable]
        public struct Cue
        {
            public float time;
            public AudioClip voice;
            [TextArea] public string subtitle;
            public float hold;
        }

        [SerializeField] private VideoClip video;
        [SerializeField] private AudioClip music;
        [SerializeField] private float musicStart;
        [SerializeField] private float handoff = 129.67f;
        [SerializeField] private float musicFadeAfterHandoff = 2.5f;
        [SerializeField] private Cue[] cues = Array.Empty<Cue>();
        [SerializeField] private float skipHoldSeconds = 1f;

        private VideoPlayer player;
        private RenderTexture screenTexture;
        private AudioSource musicSource;
        private AudioSource voiceSource;
        private AudioSource anpuSource;
        private SubtitleView subtitles;
        private UnityEngine.UI.Image skipRing;
        private UnityEngine.UI.Text skipLabel;
        private double dspStart = -1;
        private int nextCue;
        private float held;
        private bool leaving;

        /// <summary>Seconds since the music started, on the DSP clock. Negative before it starts.</summary>
        public double Now => dspStart < 0 ? -1 : AudioSettings.dspTime - dspStart;

        private void Start()
        {
            Camera cam = Camera.main;
            if (cam == null) cam = new GameObject("CinematicCamera").AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;

            player = gameObject.AddComponent<VideoPlayer>();
            player.playOnAwake = false;
            player.source = VideoSource.VideoClip;
            player.clip = video;
            // Render to a texture shown on an overlay canvas under the subtitles. CameraFarPlane does not draw
            // under URP on D3D12 in the player (verified: every film frame came back black in the built game).
            int vw = video != null ? (int)video.width : 1920, vh = video != null ? (int)video.height : 1080;
            screenTexture = new RenderTexture(vw, vh, 0, RenderTextureFormat.ARGB32) { name = "OpeningFilm" };
            screenTexture.Create();
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = screenTexture;
            GL.Clear(true, true, Color.black);
            RenderTexture.active = prev;
            player.renderMode = VideoRenderMode.RenderTexture;
            player.targetTexture = screenTexture;
            player.aspectRatio = VideoAspectRatio.FitInside;
            Canvas screen = UiKit.Canvas("FilmScreen", -100);
            UnityEngine.UI.Image back = UiKit.Solid("Black", screen.transform, Color.black);
            UiKit.Stretch(back.rectTransform);
            UnityEngine.UI.RawImage pic = UiKit.Raw("Film", screen.transform, screenTexture);
            UiKit.Stretch(pic.rectTransform);
            var fit = pic.gameObject.AddComponent<UnityEngine.UI.AspectRatioFitter>();
            fit.aspectMode = UnityEngine.UI.AspectRatioFitter.AspectMode.FitInParent;
            fit.aspectRatio = (float)vw / vh;
            player.audioOutputMode = VideoAudioOutputMode.None;
            player.timeReference = VideoTimeReference.ExternalTime;
            player.skipOnDrop = true;
            player.waitForFirstFrame = true;
            if (video != null) player.Prepare();

            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.clip = music;
            musicSource.volume = OpeningSettings.Music;
            musicSource.spatialBlend = 0f;
            voiceSource = gameObject.AddComponent<AudioSource>();
            voiceSource.playOnAwake = false;
            voiceSource.spatialBlend = 0f;
            // Zac 2026-11-25: Anpu's lines go through his own source with a gentle low-end roll-off.
            var anpuGo = new GameObject("AnpuVoice");
            anpuGo.transform.SetParent(transform, false);
            anpuSource = anpuGo.AddComponent<AudioSource>();
            anpuSource.playOnAwake = false;
            anpuSource.spatialBlend = 0f;
            VoiceTone.AddAnpu(anpuGo);

            subtitles = SubtitleView.Create();
            subtitles.SetBars(1f, 0f);
            BuildSkip();
            SceneFader.SetBlack(1f);
        }

        private void BuildSkip()
        {
            Canvas c = UiKit.Canvas("Skip", 210);
            skipRing = UiKit.Solid("SkipFill", c.transform, new Color(UiKit.Gold.r, UiKit.Gold.g, UiKit.Gold.b, 0.85f));
            UiKit.Place(skipRing.rectTransform, new Vector2(1f, 0f), new Vector2(-60f, 40f), new Vector2(180f, 2f));
            skipRing.rectTransform.pivot = new Vector2(0f, 0f);
            skipLabel = UiKit.Label("SkipLabel", c.transform, "Hold to skip", UiKit.Serif, 26, new Color(1f, 1f, 1f, 0f), TextAnchor.LowerRight);
            UiKit.Place(skipLabel.rectTransform, new Vector2(1f, 0f), new Vector2(-60f, 48f), new Vector2(400f, 40f));
        }

        private void Update()
        {
            if (dspStart < 0)
            {
                if (player.isPrepared || video == null)
                {
                    dspStart = AudioSettings.dspTime + 0.25;
                    if (music != null)
                    {
                        musicSource.time = musicStart;
                        musicSource.PlayScheduled(dspStart);
                    }
                    if (video != null) player.Play();
                    SceneFader.FadeIn(0.6f);
                }
                return;
            }

            if (leaving) return;
            double t = Now;
            if (video != null) player.externalReferenceTime = Math.Max(0.0, t);

            while (nextCue < cues.Length && cues[nextCue].time <= t + 0.2)
            {
                Cue cue = cues[nextCue++];
                double at = dspStart + cue.time;
                if (cue.voice != null)
                {
                    AudioSource vs = cue.voice.name.Contains(".anpu.") ? anpuSource : voiceSource;
                    vs.clip = cue.voice;
                    vs.PlayScheduled(Math.Max(AudioSettings.dspTime, at));
                }
                float hold = cue.hold > 0f ? cue.hold : (cue.voice != null ? cue.voice.length + 0.6f : 3f);
                if (!string.IsNullOrEmpty(cue.subtitle)) StartCoroutine(ShowAt(cue.subtitle, (float)(cue.time - t), hold));
            }

            UpdateSkip();
            if (!leaving && t >= handoff) Leave(false);
        }

        private System.Collections.IEnumerator ShowAt(string text, float delay, float hold)
        {
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            subtitles.Show(text, hold);
        }

        private void UpdateSkip()
        {
            if (leaving) return;
            held = OpeningInput.AnyHeld() ? held + Time.unscaledDeltaTime : Mathf.Max(0f, held - Time.unscaledDeltaTime * 2f);
            float k = Mathf.Clamp01(held / skipHoldSeconds);
            skipRing.rectTransform.sizeDelta = new Vector2(180f * k, 2f);
            skipRing.rectTransform.anchoredPosition = new Vector2(-240f, 40f);
            skipLabel.color = new Color(1f, 1f, 1f, Mathf.Clamp01(held * 3f) * 0.8f);
            if (k >= 1f) Leave(true);
        }

        private void Leave(bool skipped)
        {
            leaving = true;
            WalkHandoff.FromCinematic = true;
            if (skipped)
            {
                StartCoroutine(FadeMusic(0.8f));
                SceneFader.FadeTo(OpeningSettings.WalkScene, 0.8f, 1.2f);
            }
            else
            {
                // The music keeps ringing through the cut: the fader carries this object over the load.
                transform.SetParent(null);
                DontDestroyOnLoad(gameObject);
                player.Stop();
                subtitles.Hide();
                StartCoroutine(FadeMusic(musicFadeAfterHandoff));
                SceneFader.FadeTo(OpeningSettings.WalkScene, 0.05f, 0.6f);
            }
        }

        private System.Collections.IEnumerator FadeMusic(float seconds)
        {
            float v0 = musicSource.volume;
            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                musicSource.volume = Mathf.Lerp(v0, 0f, t / seconds);
                yield return null;
            }
            musicSource.Stop();
            if (gameObject.scene.name == "DontDestroyOnLoad") Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (screenTexture != null) screenTexture.Release();
        }
    }

    /// <summary>Carries one flag across the scene load: the walk opens the letterbox only when it follows the film.</summary>
    public static class WalkHandoff
    {
        public static bool FromCinematic;
    }
}
