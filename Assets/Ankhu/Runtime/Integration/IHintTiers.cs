namespace Ankhu.Integration
{
    /// <summary>
    /// Optional companion to <see cref="IRoomRulesAdapter"/> for the three sealed hint steps (Zac 2026-11-25,
    /// hint pick B). Index 0 is a nudge, 1 a direction (which thing must change) and 2 the answer (what to do
    /// next). Adapters that cannot solve their room return null and the HUD falls back to the plain hint.
    /// </summary>
    public interface IHintTiers
    {
        /// <summary>Three strings, nudge then direction then answer, or null. Must not change state.</summary>
        string[] HintTiers(PlayerContext context);
    }
}
