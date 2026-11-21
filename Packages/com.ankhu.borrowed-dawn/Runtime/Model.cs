using System;

namespace Ankhu.BorrowedDawn
{
    /// <summary>Which basin currently holds the water. Only one basin can fill.</summary>
    public enum WaterSide
    {
        East = 0,
        West = 1,
    }

    /// <summary>Abstract positions Ankhu can occupy. A walking port maps trigger volumes onto these.</summary>
    public enum Location
    {
        Floor = 0,
        Lift = 1,
        Gallery = 2,
        Landing = 3,
        Gate = 4,
        /// <summary>On the pendulum bridge between landing and gate. Only reachable under <see cref="RuleProfile.Greybox"/>.</summary>
        Bridge = 5,
    }

    /// <summary>
    /// Which rule set applies. Prototype is a faithful port of borrowed-dawn.html. Greybox is the walking-layout
    /// variant chosen for the Unity slice: ladder hatch, mirror taken from the lift, open upper walkway,
    /// a physical bridge zone, and a load-bearing elevator binding.
    /// </summary>
    public enum RuleProfile
    {
        Prototype = 0,
        Greybox = 1,
    }

    /// <summary>Where the sun-mirror currently is.</summary>
    public enum MirrorPlace
    {
        Raft = 0,
        Carried = 1,
        Socket = 2,
    }

    /// <summary>Objects that accept a binding.</summary>
    public enum BindTarget
    {
        Elevator = 0,
        Shadow = 1,
        Bridge = 2,
    }

    /// <summary>Set of active bindings. Stored as flags so equality and hashing are order independent.</summary>
    [Flags]
    public enum BindingSet
    {
        None = 0,
        Elevator = 1 << BindTarget.Elevator,
        Shadow = 1 << BindTarget.Shadow,
        Bridge = 1 << BindTarget.Bridge,
    }

    public static class BindingSetExtensions
    {
        public static BindingSet ToFlag(this BindTarget target) => (BindingSet)(1 << (int)target);

        public static bool Has(this BindingSet set, BindTarget target) => (set & target.ToFlag()) != 0;

        public static int Count(this BindingSet set)
        {
            int n = 0;
            int v = (int)set;
            while (v != 0)
            {
                n += v & 1;
                v >>= 1;
            }
            return n;
        }
    }
}
