using Ankhu.Bindings;
using Ankhu.Core;
using Ankhu.Integration;
using Ankhu.Realms;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ankhu.Spellcast
{
    /// <summary>
    /// Sounds for the Borrowed Dawn style rooms (the three teaching rooms and Borrowed Dawn), Zac 2026-11-26: they
    /// were silent. Each change of the room's state plays its sound when the world shows it, because it listens to
    /// <see cref="RoomRulesHost.SnapshotChanged"/>, which the painted spell bridge holds until the glyphs strike.
    /// Each room also gets a quiet looping bed: dripping water in the cistern, a soft breath of wind elsewhere.
    /// Clips are earshot takes in Assets/Ankhu/Audio/Sfx/Resources/Sfx/state/ and Sfx/bed/.
    /// </summary>
    public sealed class RoomStateSfx : MonoBehaviour
    {
        private const float BedVolume = 0.35f;
        private const float BedFade = 2.5f;

        private static RoomStateSfx instance;
        private RoomRulesHost host;
        private AudioSource bed;
        private float bedTarget;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { instance = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (instance != null) return;
            var go = new GameObject("RoomStateSfx");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<RoomStateSfx>();
        }

        private void Awake()
        {
            bed = gameObject.AddComponent<AudioSource>();
            bed.playOnAwake = false;
            bed.loop = true;
            bed.spatialBlend = 0f;
            bed.volume = 0f;
        }

        private void Update()
        {
            RoomRulesHost now = RoomRulesHost.Instance;
            if (now != host)
            {
                if (host != null) host.SnapshotChanged -= OnChanged;
                host = now;
                if (host != null) host.SnapshotChanged += OnChanged;
                StartBed();
            }
            bed.volume = Mathf.MoveTowards(bed.volume, bedTarget, Time.unscaledDeltaTime * BedVolume / BedFade);
            if (bed.volume <= 0f && bedTarget <= 0f && bed.isPlaying) bed.Stop();
        }

        private void OnDestroy()
        {
            if (host != null) host.SnapshotChanged -= OnChanged;
        }

        private void StartBed()
        {
            bedTarget = 0f;
            if (host == null || !(host.Adapter is BorrowedDawnRulesAdapter)) return;
            var installer = FindAnyObjectByType<BorrowedDawnRulesInstaller>();
            string id = installer != null ? installer.RoomId ?? string.Empty : string.Empty;
            bool cistern = id.ToLowerInvariant().Contains("cistern") || SceneManager.GetActiveScene().name.Contains("Cistern");
            AudioClip[] clips = Resources.LoadAll<AudioClip>(cistern ? "Sfx/bed/cistern" : "Sfx/bed/tomb");
            if (clips.Length == 0) return;
            if (bed.clip != clips[0] || !bed.isPlaying)
            {
                bed.clip = clips[0];
                bed.volume = 0f;
                bed.Play();
            }
            bedTarget = BedVolume;
        }

        private static void OnChanged(RoomSnapshot a, RoomSnapshot b)
        {
            if (a == null || b == null) return;
            if (a.Water != b.Water) S("water");
            if (a.RaftHigh != b.RaftHigh) S("raft", 0.6f);
            if (a.Mirror != b.Mirror) S(b.Mirror == MirrorLocation.Socket ? "mirror_set" : "mirror_take");
            if (a.ElevatorBound != b.ElevatorBound || a.ShadowBound != b.ShadowBound) S("bind", b.BindingsUsed > a.BindingsUsed ? 0.7f : 0.45f);
            if (a.BridgeBound != b.BridgeBound) S("bridge", 0.6f);
            if (a.ElevatorHigh != b.ElevatorHigh) S("stone_slide");
            if (a.BearerAwake != b.BearerAwake) S("stir");
            if (!a.BellRepaired && b.BellRepaired) S("bell_fix");
            if (!a.BellStruck && b.BellStruck) S("bell_strike", 0.8f);
            if (a.LadderLowered != b.LadderLowered) S("ladder");
            if (!a.GateOpen && b.GateOpen) S("gate_open");
            if (a.LightScreenRaised != b.LightScreenRaised) S("screen_raise", 0.6f);
            if (!a.SunReachesGate && b.SunReachesGate) S("sun_gate", 0.7f);
        }

        private static void S(string name, float volume = 0.7f) => SfxBank.Play("state/" + name, volume, null, 0.15f, 0.03f);
    }
}
