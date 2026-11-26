using Ankhu.Integration;

namespace Ankhu.Voice
{
    /// <summary>
    /// Maps a spoken utterance and the rules' answer to what Ankhu should say aloud. Each room's binding
    /// supplies one (Borrowed Dawn: Ankhu.Bindings.BorrowedDawnSpellVoice). Return null for silence.
    /// </summary>
    public interface ISpellVoiceResolver
    {
        SpokenSpell Resolve(string utterance, CommandResult result);
    }
}
