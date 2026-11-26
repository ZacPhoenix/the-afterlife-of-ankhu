using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Ankhu.Opening
{
    /// <summary>
    /// Which Wernes memory rooms Ankhu has finished, kept in PlayerPrefs so the Memory House hub knows what to
    /// light and what Anpu says when Ankhu comes back. The terrace reunion starts the realm fresh (Reset), each
    /// room marks itself done when its exit runs, and the hub reads the flags at Start.
    ///
    /// Flags handed to the hub's director (prefixed "progress."): done.A to done.D for each finished room,
    /// back.X for the room just finished, count.N for how many are done, and all once every room is done.
    /// </summary>
    public static class WernesProgress
    {
        public const string PrefsKey = "ankhu.wernes.done";
        public const string LastKey = "ankhu.wernes.last";

        /// <summary>The hub's rooms in the order the hub sends Ankhu into them, with their letters.</summary>
        public static readonly string[] Rooms = { "Wernes_A_Field", "Wernes_B_Shabti", "Wernes_C_House", "Wernes_D_Bricks" };

        private static HashSet<string> simulated;

        public static string LetterOf(string scene)
        {
            int i = System.Array.IndexOf(Rooms, scene);
            return i < 0 ? null : ((char)('A' + i)).ToString();
        }

        public static bool IsRoom(string scene) => System.Array.IndexOf(Rooms, scene) >= 0;

        private static HashSet<string> Load()
        {
            if (simulated != null) return simulated;
            string raw = PlayerPrefs.GetString(PrefsKey, string.Empty);
            return new HashSet<string>(raw.Split(',').Where(s => s.Length > 0));
        }

        public static bool IsDone(string scene) => Load().Contains(LetterOf(scene) ?? "?");

        public static int DoneCount => Load().Count(l => Rooms.Any(r => LetterOf(r) == l));

        public static string LastDone => simulated != null ? null : PlayerPrefs.GetString(LastKey, string.Empty);

        public static void MarkDone(string scene)
        {
            string l = LetterOf(scene);
            if (l == null || simulated != null) return;
            HashSet<string> d = Load();
            d.Add(l);
            PlayerPrefs.SetString(PrefsKey, string.Join(",", d.OrderBy(x => x)));
            PlayerPrefs.SetString(LastKey, l);
            PlayerPrefs.Save();
        }

        public static void Reset()
        {
            if (simulated != null) return;
            PlayerPrefs.DeleteKey(PrefsKey);
            PlayerPrefs.DeleteKey(LastKey);
            PlayerPrefs.Save();
        }

        /// <summary>The first room not yet done, in hub order, or null when all are done.</summary>
        public static string NextRoom()
        {
            HashSet<string> d = Load();
            return Rooms.FirstOrDefault(r => !d.Contains(LetterOf(r)));
        }

        /// <summary>Flags for the hub director, without the "progress." prefix.</summary>
        public static IEnumerable<string> Flags()
        {
            HashSet<string> d = Load();
            foreach (string l in d.OrderBy(x => x)) yield return "done." + l;
            string last = LastDone;
            if (!string.IsNullOrEmpty(last)) yield return "back." + last;
            yield return "count." + d.Count;
            if (Rooms.All(r => d.Contains(LetterOf(r)))) yield return "all";
        }

        /// <summary>Editor checks only: pretend these letters are done without touching PlayerPrefs. Null ends it.</summary>
        public static void Simulate(IEnumerable<string> letters)
        {
            simulated = letters == null ? null : new HashSet<string>(letters);
        }
    }
}
