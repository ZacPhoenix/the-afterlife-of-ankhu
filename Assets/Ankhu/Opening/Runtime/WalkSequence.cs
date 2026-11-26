using System.Collections;
using UnityEngine;

namespace Ankhu.Opening
{
    /// <summary>
    /// Runs the Walk_Duat scene: opens the letterbox when arriving from the cinematic (the DUAT hit), owns the voice
    /// sources and subtitles, ducks the ambience under speech, sets the save key, and at the end fades slowly back
    /// to the title. Escape or Start offers a return to the title.
    /// </summary>
    public sealed class WalkSequence : MonoBehaviour
    {
        [SerializeField] private AudioClip ambience;
        [SerializeField] private float ambienceVolume = 0.45f;
        [SerializeField] private WalkPlayer player;

        private AudioSource ambienceSource;
        private AudioSource ankhuVoice;
        private AudioSource anpuVoice;
        private float duck = 1f;
        private bool speaking;
        private bool ending;
        private float pauseHeld;

        public SubtitleView Subtitles { get; private set; }

        public void Configure(AudioClip amb, WalkPlayer p)
        {
            ambience = amb;
            player = p;
        }

        private void Awake()
        {
            Subtitles = SubtitleView.Create();
            ankhuVoice = MakeSource(1f);
            // Zac 2026-11-25: Anpu's own child source with a gentle low-end roll-off.
            var anpuGo = new GameObject("AnpuVoice");
            anpuGo.transform.SetParent(transform, false);
            anpuVoice = anpuGo.AddComponent<AudioSource>();
            anpuVoice.playOnAwake = false;
            anpuVoice.spatialBlend = 0f;
            VoiceTone.AddAnpu(anpuGo);
            if (ambience != null)
            {
                ambienceSource = MakeSource(0f);
                ambienceSource.clip = ambience;
                ambienceSource.loop = true;
                ambienceSource.Play();
            }
        }

        private AudioSource MakeSource(float volume)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            src.volume = volume;
            return src;
        }

        private IEnumerator Start()
        {
            OpeningSettings.MarkReachedWalk();
            if (WalkHandoff.FromCinematic)
            {
                Subtitles.SetBars(1f, 0f);
                if (player != null) player.InputEnabled = false;
                yield return new WaitForSeconds(0.7f);
                Subtitles.SetBars(0f, 2.2f);
                yield return new WaitForSeconds(1.0f);
                if (player != null) player.InputEnabled = true;
            }
            else
            {
                Subtitles.SetBars(0f, 0f);
            }
            WalkHandoff.FromCinematic = false;
            StartCoroutine(MoveHint());
        }

        // Zac 2026-11-25: the first walkable scene teaches the keys on the shared papyrus note, and the note
        // fades once Ankhu first moves under the player's hand.
        private IEnumerator MoveHint()
        {
            PapyrusNote note = PapyrusNote.Show("Use [W A S D] to walk.");
            float shown = Time.unscaledTime;
            while (true)
            {
                bool moved = player != null && player.InputEnabled && OpeningInput.Move().sqrMagnitude > 0.04f;
                if (moved || (player == null && Time.unscaledTime - shown > 6f) || ending) break;
                yield return null;
            }
            yield return new WaitForSeconds(0.8f);
            if (note != null) note.Dismiss();
        }

        public IEnumerator WaitForVoiceFree()
        {
            while (speaking) yield return null;
        }

        public void BeginDialogue() { speaking = true; }
        public void EndDialogue() { speaking = false; }

        public float Say(AudioClip clip, bool ankhu)
        {
            if (clip == null) return 0f;
            AudioSource src = ankhu ? ankhuVoice : anpuVoice;
            src.clip = clip;
            src.Play();
            return clip.length;
        }

        public void EndScene()
        {
            if (ending) return;
            ending = true;
            StartCoroutine(Ending());
        }

        private IEnumerator Ending()
        {
            StartCoroutine(PushIn(9f, 1.1f));
            yield return new WaitForSeconds(2.0f);
            Subtitles.SetBars(1f, 2f);
            yield return new WaitForSeconds(1.6f);
            Subtitles.Hide();
            Subtitles.ShowCard("The First Gate", "The Afterlife of Ankhu");
            yield return new WaitForSeconds(4.2f);
            if (player != null) player.InputEnabled = false;
            SceneFader.FadeTo(OpeningSettings.SceneAfterWalk(), 2.5f, 0.5f);
        }

        // A slow push-in on the gate shot under the closing card, so the last image keeps moving instead of
        // freezing on a tableau. The painting is sized for its painted lens, so the zoom crops into it in place.
        private IEnumerator PushIn(float seconds, float factor)
        {
            var director = FindAnyObjectByType<FixedCameraDirector>();
            Camera cam = director != null ? director.ActiveCamera : null;
            if (cam == null) yield break;
            float from = cam.fieldOfView;
            float to = Mathf.Rad2Deg * 2f * Mathf.Atan(Mathf.Tan(from * 0.5f * Mathf.Deg2Rad) / factor);
            for (float t = 0f; t < seconds && cam != null; t += Time.deltaTime)
            {
                cam.fieldOfView = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / seconds));
                yield return null;
            }
        }

        private void Update()
        {
            bool anyVoice = ankhuVoice.isPlaying || anpuVoice.isPlaying;
            duck = Mathf.MoveTowards(duck, anyVoice ? 0.55f : 1f, Time.deltaTime * (anyVoice ? 3f : 0.8f));
            if (ambienceSource != null)
            {
                float fadeIn = Mathf.Clamp01(Time.timeSinceLevelLoad / 3f);
                ambienceSource.volume = ambienceVolume * OpeningSettings.Music * duck * fadeIn;
            }

            if (!ending && OpeningInput.PausePressed())
            {
                if (pauseHeld > 0f && Time.unscaledTime - pauseHeld < 2.5f)
                {
                    ending = true;
                    SceneFader.FadeTo(OpeningSettings.TitleScene, 1.5f, 0.8f);
                }
                else
                {
                    pauseHeld = Time.unscaledTime;
                    Subtitles.Show("Press again to return to the title", 2.5f);
                }
            }
        }
    }
}
