using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ankhu.Opening
{
    /// <summary>
    /// Zac 2026-11-24: the whole game in chronological play order, so it can be tested end to end.
    /// One list every exit agrees with. docs/PLAY_ORDER.md describes each link and what plays inside it,
    /// and Ankhu > Play Order > Check (PlayOrderCheck.CheckCli) opens every scene and proves each exit
    /// leads to the next one here.
    /// </summary>
    public static class PlayOrder
    {
        public const string BorrowedDawnPainted = "BorrowedDawn_Painted";

        /// <summary>Stands in the chain for Borrowed Dawn. It resolves to the painted scene when that is built.</summary>
        public const string BorrowedDawnSlot = "@BorrowedDawn";

        public static readonly string[] Chain =
        {
            OpeningSettings.TitleScene,          // Opening_Title
            OpeningSettings.CinematicScene,      // Opening_Cinematic
            OpeningSettings.WalkScene,           // Opening_WalkDuat
            OpeningSettings.FirstGateScene,      // Realm_01_Arit1, Anpu gives the empty codex
            OpeningSettings.FirstTeachScene,     // Teach1_Cistern, then film flick_f1_river
            "Teach2_HeldStep",                   // then film flick_f2_hands
            "Teach3_LingeringShadow",            // then flick_f3_shadow and Anpu's farewell
            BorrowedDawnSlot,                    // alone, then film mem0_dawn_name and Anpu's exit lines
            TerraceScene,                        // WK5, the reunion with Anpu above Wernes
            HubScene,                            // the Memory House, Anpu waits here between rooms
            "Wernes_A_Field",                    // each Wernes room returns to the hub, the hub sends
            "Wernes_B_Shabti",                   // Ankhu on to the first room not yet done
            "Wernes_C_House",
            "Wernes_D_Bricks",                   // once all four are done the hub opens on the finale
            FinaleScene,                         // Ra's night barque passes Wernes, then Rosetau
            "Rosetau_A_Laugh",                   // Rosetau, the passage under Abdju: the god's laugh
            "Rosetau_B_Mourners",                // the two mourners at the bier
            "Rosetau_C_Mother",                  // the mother's room
            "Rosetau_D_TwoWays",                 // the Book of Two Ways: the father's watch over the fire
            "Rosetau_E_Finale",                  // the shield brought home, then the title (end of built content)
        };

        /// <summary>The last Wernes room in the chain. The hub's all-done exit continues from here.</summary>
        public const string LastWernesRoom = "Wernes_D_Bricks";

        public const string TerraceScene = "Wernes_Terrace";
        public const string HubScene = "Wernes_Hub";

        /// <summary>Zac 2026-11-25: the first realm's finale, Ra's night barque over Wernes. Plays after the hub's all-done exit.</summary>
        public const string FinaleScene = "Wernes_Finale";

        /// <summary>The Borrowed Dawn scene to play: painted when it is in Build Settings, else the greybox.</summary>
        public static string BorrowedDawnScene()
        {
            return InBuild(BorrowedDawnPainted) ? BorrowedDawnPainted : OpeningSettings.GreyboxScene;
        }

        /// <summary>
        /// Zac 2026-11-25: a room with a painted twin (Name_Painted, built by BorrowedDawnPainted) plays the twin
        /// when it is in Build Settings. Otherwise the scene itself.
        /// </summary>
        public static string PaintedOf(string scene)
        {
            if (string.IsNullOrEmpty(scene) || scene.EndsWith(PaintedSuffix)) return scene;
            return InBuild(scene + PaintedSuffix) ? scene + PaintedSuffix : scene;
        }

        public const string PaintedSuffix = "_Painted";

        /// <summary>A chain entry as it plays: the Borrowed Dawn slot resolved, painted twins preferred.</summary>
        public static string Play(string entry) => entry == BorrowedDawnSlot ? BorrowedDawnScene() : PaintedOf(entry);

        /// <summary>The chain with the Borrowed Dawn slot resolved.</summary>
        public static string[] Resolved()
        {
            var r = new string[Chain.Length];
            for (int i = 0; i < Chain.Length; i++) r[i] = Play(Chain[i]);
            return r;
        }

        public static int IndexOf(string scene)
        {
            if (string.IsNullOrEmpty(scene)) return -1;
            if (scene == BorrowedDawnPainted || scene == OpeningSettings.GreyboxScene) return Array.IndexOf(Chain, BorrowedDawnSlot);
            int i = Array.IndexOf(Chain, scene);
            if (i < 0 && scene.EndsWith(PaintedSuffix)) i = Array.IndexOf(Chain, scene.Substring(0, scene.Length - PaintedSuffix.Length));
            return i;
        }

        public static bool Contains(string scene) => IndexOf(scene) >= 0;

        /// <summary>The next scene in play order after this one, or null when it is the last or not in the chain.</summary>
        public static string After(string scene)
        {
            int i = IndexOf(scene);
            if (i < 0 || i + 1 >= Chain.Length) return null;
            return Play(Chain[i + 1]);
        }

        /// <summary>
        /// Where a chain scene's exit goes. Wernes rooms return to the hub when it is built. The hub sends Ankhu to
        /// the first room not yet done (WernesProgress), and on to Rosetau once all are done. Everything else goes
        /// to the next scene in the chain that is built, or the title after the last.
        /// </summary>
        public static string ExitOf(string scene)
        {
            if (WernesProgress.IsRoom(scene) && InBuild(HubScene)) return HubScene;
            if (scene == HubScene)
            {
                string room = WernesProgress.NextRoom();
                while (room != null && !InBuild(room)) room = WernesProgress.Rooms.SkipWhile(r => r != room).Skip(1).FirstOrDefault();
                return room ?? NextBuiltAfter(IndexOf(LastWernesRoom));
            }
            return NextBuiltAfter(IndexOf(scene));
        }

        /// <summary>The first chain scene after index i that is in Build Settings, or the title.</summary>
        private static string NextBuiltAfter(int i)
        {
            if (i < 0) return OpeningSettings.TitleScene;
            for (int j = i + 1; j < Chain.Length; j++)
            {
                string n = Play(Chain[j]);
                if (InBuild(n)) return n;
            }
            return OpeningSettings.TitleScene;
        }

        /// <summary>
        /// What the exit of a chain scene should be with no Wernes rooms done, which is what PlayOrderCheck expects.
        /// Rooms go to the hub, the hub to Wernes A. Otherwise the next chain scene.
        /// </summary>
        public static string ExpectedExit(string scene)
        {
            if (WernesProgress.IsRoom(scene)) return HubScene;
            if (scene == HubScene) return WernesProgress.Rooms[0];
            return After(scene) ?? OpeningSettings.TitleScene;
        }

        /// <summary>True when the scene is enabled in Build Settings. Walks the build list, which also answers in the editor.</summary>
        public static bool InBuild(string scene)
        {
            if (string.IsNullOrEmpty(scene)) return false;
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                if (System.IO.Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(i)) == scene) return true;
            }
            return false;
        }

        public static string ActiveScene() => SceneManager.GetActiveScene().name;
    }
}
