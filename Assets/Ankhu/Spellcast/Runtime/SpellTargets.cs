using Ankhu.BorrowedDawn;
using Ankhu.Realms;
using Ankhu.World;
using UnityEngine;

namespace Ankhu.Spellcast
{
    /// <summary>
    /// Where a spoken command lands in a Borrowed Dawn style room (Borrowed Dawn itself and the teaching rooms, greybox
    /// or painted). Each command kind maps to the room view that shows its result. Movement has no target: walking is
    /// not a word of power.
    /// </summary>
    public static class SpellTargets
    {
        /// <summary>True for commands that move Ankhu rather than speak to the room.</summary>
        public static bool IsMovement(Command command)
        {
            return command != null && (command.Kind == CommandKind.MoveTo || command.Kind == CommandKind.CrossBridge);
        }

        /// <summary>The object the command acts on, or null when the room has no view for it.</summary>
        public static ISpellTarget For(Command command)
        {
            if (command == null || IsMovement(command)) return null;
            switch (command.Kind)
            {
                case CommandKind.Flow:
                    return Wrap("water", Find<WaterSelectorView>() ?? (Component)Find<WaterColumnView>());
                case CommandKind.Bind:
                case CommandKind.Release:
                    switch (command.Target)
                    {
                        case BindTarget.Elevator: return Wrap("elevator", Find<ElevatorPlatform>());
                        case BindTarget.Shadow: return Wrap("shadow", Find<ShadowSealView>());
                        case BindTarget.Bridge: return Wrap("bridge", Find<PendulumBridge>());
                        default: return null;
                    }
                case CommandKind.WakeBearer:
                case CommandKind.SleepBearer:
                    return Wrap("bearer", Find<BearerView>());
                case CommandKind.RecallBell:
                case CommandKind.StrikeBell:
                case CommandKind.EchoBell:
                    return Wrap("bell", Find<BellView>());
                case CommandKind.TakeMirror:
                    return Wrap("mirror", Find<RaftView>());
                case CommandKind.InstallMirror:
                    return Wrap("socket", Find<SocketView>());
                case CommandKind.ReadInscription:
                    return Wrap("inscription", Find<InscriptionView>());
                case CommandKind.SpeakName:
                    return Wrap("gate", Find<GateView>());
                case CommandKind.LowerLadder:
                    return Wrap("ladder", Find<LadderHatch>());
                default:
                    return null;
            }
        }

        private static T Find<T>() where T : Component
        {
            return Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);
        }

        private static ISpellTarget Wrap(string id, Component view)
        {
            return view != null ? new WorldSpellTarget(id, view.transform) : null;
        }
    }
}
