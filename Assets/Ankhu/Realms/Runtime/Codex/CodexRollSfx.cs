using UnityEngine;

namespace Ankhu.Realms
{
    /// <summary>
    /// Zac 2026-11-25 picked the roll's sounds (earshot takes, proposal art_gen/sfx/roll_proposal): no float bed, a
    /// harp chord on the gift (CodexGift plays Resources/RealmCodex/sfx_gift itself), a papyrus slide when the roll
    /// opens, a book closing when it closes and a page turn as the view crosses a sheet.
    ///
    /// Zac 2026-11-26: the glyphs the floating roll sheds while it trails Ankhu make no sound. The harp notes play only
    /// when a spell's glyphs strike their target (see <see cref="SpellSfx"/>).
    ///
    /// Only listens to the static events of <see cref="CodexBook"/>, so the codex scripts do not change. Those events
    /// are cleared at SubsystemRegistration, so this subscribes after scene load.
    /// </summary>
    public sealed class CodexRollSfx : MonoBehaviour
    {
        private const float PageAfterOpen = 0.7f; // the unroll fires a page event, the slide already covers it

        private static CodexRollSfx instance;
        private float openedAt = -10f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { instance = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (instance != null) return;
            var go = new GameObject("CodexRollSfx");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<CodexRollSfx>();
        }

        private void OnEnable()
        {
            CodexBook.OpenChanged += OnOpen;
            CodexBook.PageTurned += OnPage;
        }

        private void OnDisable()
        {
            CodexBook.OpenChanged -= OnOpen;
            CodexBook.PageTurned -= OnPage;
        }

        private void OnOpen(bool open)
        {
            if (open) openedAt = Time.unscaledTime;
            SfxBank.Play(open ? "roll/open" : "roll/close", open ? 0.8f : 0.85f);
        }

        private void OnPage(int first)
        {
            if (Time.unscaledTime - openedAt < PageAfterOpen) return;
            SfxBank.Play("roll/page", 0.55f, null, 0.22f);
        }
    }
}
