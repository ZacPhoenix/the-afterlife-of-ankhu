using System;
using System.Collections;
using UnityEngine;

namespace Ankhu.Opening
{
    /// <summary>
    /// Progress-driven cuts and dialogue. The player's distance along the road decides the shot: walking past
    /// a shot's threshold cuts forward, walking back past it (less a little hysteresis) cuts back. Thresholds are
    /// placed by the scene builder where the player walks off the edge of the previous frame.
    /// Dialogue beats fire once, the first time the player passes their start.
    /// </summary>
    public sealed class WalkProgress : MonoBehaviour
    {
        [Serializable]
        public struct Line
        {
            public bool ankhu;
            public AudioClip clip;
            [TextArea] public string text;
        }

        [Serializable]
        public struct Beat
        {
            public string name;
            public float start;
            public Line[] lines;
            public bool endsScene;
        }

        [SerializeField] private WalkPath path;
        [SerializeField] private WalkPlayer player;
        [SerializeField] private FixedCameraDirector director;
        [SerializeField] private WalkSequence sequence;
        [SerializeField] private float[] thresholds = Array.Empty<float>();
        [SerializeField] private float hysteresis = 0.8f;
        [SerializeField] private Beat[] beats = Array.Empty<Beat>();
        [SerializeField] private float pauseBetween = 0.5f;

        private bool[] fired;

        public float Progress { get; private set; }

        public void Configure(WalkPath p, WalkPlayer pl, FixedCameraDirector d, WalkSequence seq, float[] cutAt, Beat[] dialogue)
        {
            path = p;
            player = pl;
            director = d;
            sequence = seq;
            thresholds = cutAt;
            beats = dialogue;
        }

        private void Start()
        {
            fired = new bool[beats.Length];
        }

        private void Update()
        {
            Progress = path.Project(player.transform.position, out _);
            int shot = director.Active < 0 ? 0 : director.Active;
            while (shot + 1 < thresholds.Length && Progress >= thresholds[shot + 1]) shot++;
            while (shot > 0 && Progress < thresholds[shot] - hysteresis) shot--;
            director.CutTo(shot);

            for (int i = 0; i < beats.Length; i++)
            {
                if (fired[i] || Progress < beats[i].start) continue;
                fired[i] = true;
                StartCoroutine(Play(beats[i]));
            }
        }

        private IEnumerator Play(Beat beat)
        {
            yield return sequence.WaitForVoiceFree();
            sequence.BeginDialogue();
            foreach (Line l in beat.lines)
            {
                float len = sequence.Say(l.clip, l.ankhu);
                // No canned gesture: the same open-armed pose on every Ankhu line read as a loop. The walk cycle carries him.
                sequence.Subtitles.Show(l.text, Mathf.Max(1.6f, len + 0.4f));
                yield return new WaitForSeconds(Mathf.Max(1.4f, len) + pauseBetween);
            }
            sequence.EndDialogue();
            if (beat.endsScene) sequence.EndScene();
        }
    }
}
