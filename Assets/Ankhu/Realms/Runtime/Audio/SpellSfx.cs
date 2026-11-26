using UnityEngine;

namespace Ankhu.Realms
{
    /// <summary>
    /// The sounds of a spell, in every room that uses the painted bubble (realm rooms, the teaching rooms and Borrowed
    /// Dawn all go through <see cref="SpellPresenter"/>). Zac 2026-11-26.
    ///   bubble opens: a soft breath of air (spell/open)
    ///   each letter typed: a pen on papyrus, very quiet (spell/type)
    ///   each glyph leaves the bubble: a thin swish (spell/launch)
    ///   each glyph strikes its target: a high harp note, the take Zac picked for the roll's glyphs (spell/arrive)
    ///   a refused spell: the words crumble like dry stone (spell/fail)
    ///   an accepted spell: a singing bowl a moment after the last glyph lands (spell/success)
    /// Clips are earshot takes in Assets/Ankhu/Audio/Sfx/Resources/Sfx/spell/.
    /// </summary>
    public sealed class SpellSfx : MonoBehaviour
    {
        private const float SuccessAfterLastStrike = 0.35f;
        private const float SuccessFallback = 4f; // an accepted spell with no glyph flight still gets its bowl

        private static SpellSfx instance;
        private bool successPending;
        private float successAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { instance = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (instance != null) return;
            var go = new GameObject("SpellSfx");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<SpellSfx>();
        }

        private void OnEnable()
        {
            SpellPresenter.OpenChanged += OnOpen;
            SpellPresenter.CharTyped += OnChar;
            SpellPresenter.GlyphLaunched += OnLaunch;
            SpellPresenter.GlyphArrived += OnArrive;
            SpellPresenter.OutcomeStarted += OnOutcome;
        }

        private void OnDisable()
        {
            SpellPresenter.OpenChanged -= OnOpen;
            SpellPresenter.CharTyped -= OnChar;
            SpellPresenter.GlyphLaunched -= OnLaunch;
            SpellPresenter.GlyphArrived -= OnArrive;
            SpellPresenter.OutcomeStarted -= OnOutcome;
        }

        private void Update()
        {
            if (successPending && Time.unscaledTime >= successAt)
            {
                successPending = false;
                SfxBank.Play("spell/success", 0.5f, null, 0.5f, 0f);
            }
        }

        private static void OnOpen(bool open)
        {
            if (open) SfxBank.Play("spell/open", 0.5f, null, 0.3f);
        }

        private static void OnChar(char c)
        {
            if (!char.IsWhiteSpace(c)) SfxBank.Play("spell/type", 0.35f, null, 0.045f, 0.08f);
        }

        private static void OnLaunch(string code, Vector3 at) => SfxBank.Play("spell/launch", 0.35f, null, 0.06f, 0.08f);

        private void OnArrive(string code, Vector3 at)
        {
            SfxBank.Play("spell/arrive", 0.4f, at, 0.07f, 0f);
            if (successPending) successAt = Time.unscaledTime + SuccessAfterLastStrike;
        }

        private void OnOutcome(bool success)
        {
            if (!success)
            {
                successPending = false;
                SfxBank.Play("spell/fail", 0.55f, null, 0.3f);
                return;
            }
            successPending = true;
            successAt = Time.unscaledTime + SuccessFallback;
        }
    }
}
