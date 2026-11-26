using Ankhu.BorrowedDawn;
using Ankhu.Voice;
using CommandResult = Ankhu.Integration.CommandResult;

namespace Ankhu.Bindings
{
    /// <summary>
    /// Borrowed Dawn's voice resolver: re-parses the accepted utterance with the room's deterministic
    /// <see cref="SpellParser"/> and maps the parsed <see cref="Command"/> to the phrase Ankhu says.
    /// Aliases collapse to the canonical form ("grab mirror" is voiced as "take the mirror", "unbind lift"
    /// as "release the elevator"). Movement is never voiced. Engine-free.
    /// Phrase ids must match tools/voice/voice_lines.json.
    /// </summary>
    public sealed class BorrowedDawnSpellVoice : ISpellVoiceResolver
    {
        public SpokenSpell Resolve(string utterance, CommandResult result)
        {
            if (result == null || utterance == null)
            {
                return null;
            }
            ParseResult parse = SpellParser.Parse(utterance);
            return parse.Success ? ForCommand(parse.Command) : null;
        }

        public static SpokenSpell ForCommand(Command command)
        {
            if (command == null)
            {
                return null;
            }
            switch (command.Kind)
            {
                case CommandKind.Flow:
                    string dir = command.Direction == WaterSide.West ? "west" : "east";
                    return Word("flow_" + dir, "flow", dir);
                case CommandKind.Bind:
                    return Word("bind_" + Target(command.Target), "bind", "the", Target(command.Target));
                case CommandKind.Release:
                    return Word("release_" + Target(command.Target), "release", "the", Target(command.Target));
                case CommandKind.WakeBearer: return Word("wake_bearer", "wake", "bearer");
                case CommandKind.SleepBearer: return Word("sleep_bearer", "sleep", "bearer");
                case CommandKind.RecallBell: return Word("recall_bell", "recall", "the", "bell");
                case CommandKind.EchoBell: return Word("echo_bell", "echo", "the", "bell");
                case CommandKind.SpeakName:
                    return new SpokenSpell(SpellVoiceCategory.Name, new SpokenClause("name_gate", null));
                case CommandKind.TakeMirror: return Deed("take_mirror", "take", "the", "mirror");
                case CommandKind.InstallMirror: return Deed("install_mirror", "install", "the", "mirror");
                case CommandKind.ReadInscription: return Deed("read_inscription", "read", "the", "inscription");
                case CommandKind.StrikeBell: return Deed("strike_bell", "strike", "the", "bell");
                case CommandKind.CrossBridge: return Deed("cross_bridge", "cross", "the", "bridge");
                case CommandKind.LowerLadder: return Deed("lower_ladder", "lower", "the", "ladder");
                default:
                    return null;
            }
        }

        private static string Target(BindTarget target)
        {
            switch (target)
            {
                case BindTarget.Shadow: return "shadow";
                case BindTarget.Bridge: return "bridge";
                default: return "elevator";
            }
        }

        private static SpokenSpell Word(string phraseId, params string[] words) =>
            new SpokenSpell(SpellVoiceCategory.WordOfPower, new SpokenClause(phraseId, words));

        private static SpokenSpell Deed(string phraseId, params string[] words) =>
            new SpokenSpell(SpellVoiceCategory.Deed, new SpokenClause(phraseId, words));
    }
}
