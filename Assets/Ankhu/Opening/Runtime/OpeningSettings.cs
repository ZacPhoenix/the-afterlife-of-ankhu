using UnityEngine;

namespace Ankhu.Opening
{
    /// <summary>
    /// Player settings for the opening slice, kept in PlayerPrefs. Master volume drives AudioListener.volume,
    /// music volume is read by the title and the cinematic. The save key is set once the player reaches the walk,
    /// so Continue can load it directly.
    /// </summary>
    public static class OpeningSettings
    {
        private const string MasterKey = "ankhu.opening.master";
        private const string MusicKey = "ankhu.opening.music";
        private const string SubtitlesKey = "ankhu.opening.subtitles";
        private const string ReducedMotionKey = "ankhu.opening.reducedMotion";
        private const string SaveKey = "ankhu.opening.reachedWalk";

        public const string TitleScene = "Opening_Title";
        public const string CinematicScene = "Opening_Cinematic";
        public const string WalkScene = "Opening_WalkDuat";
        public const string GreyboxScene = "BorrowedDawn_Greybox";
        public const string FirstTeachScene = "Teach1_Cistern";
        public const string FirstGateScene = "Realm_01_Arit1";

        /// <summary>
        /// The scene the walk hands over to: the first enabled build scene after the walk that is not one of the
        /// older prototype rooms (Assets/Ankhu/Generated/Scenes). The realms and puzzle rooms that follow the walk
        /// are added to Build Settings after it. With none there, the walk returns to the title.
        /// </summary>
        public static string SceneAfterWalk()
        {
            // Zac 2026-11-24: the walk ends at the First Gate (Arit 1, where Anpu gives the empty codex). Its
            // exit leads down into Anpu's teaching rooms (Teach 1 to 3), then Borrowed Dawn alone, then Wernes.
            // Falls back to the teaching rooms, then Borrowed Dawn, if the gate is not in Build Settings.
            if (UnityEngine.Application.CanStreamedLevelBeLoaded(FirstGateScene)) return PlayOrder.PaintedOf(FirstGateScene);
            if (UnityEngine.Application.CanStreamedLevelBeLoaded(FirstTeachScene)) return PlayOrder.PaintedOf(FirstTeachScene);
            if (UnityEngine.Application.CanStreamedLevelBeLoaded(GreyboxScene)) return GreyboxScene;
            int walk = -1;
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings; i++)
            {
                string path = UnityEngine.SceneManagement.SceneUtility.GetScenePathByBuildIndex(i);
                string name = System.IO.Path.GetFileNameWithoutExtension(path);
                if (walk < 0) { if (name == WalkScene) walk = i; continue; }
                if (path.StartsWith("Assets/Ankhu/Generated/Scenes/")) continue;
                return name;
            }
            return TitleScene;
        }

        public static float Master
        {
            get => PlayerPrefs.GetFloat(MasterKey, 1f);
            set { PlayerPrefs.SetFloat(MasterKey, Mathf.Clamp01(value)); Apply(); }
        }

        public static float Music
        {
            get => PlayerPrefs.GetFloat(MusicKey, 0.85f);
            set => PlayerPrefs.SetFloat(MusicKey, Mathf.Clamp01(value));
        }

        public static bool Subtitles
        {
            get => PlayerPrefs.GetInt(SubtitlesKey, 1) == 1;
            set => PlayerPrefs.SetInt(SubtitlesKey, value ? 1 : 0);
        }

        public static bool ReducedMotion
        {
            get => PlayerPrefs.GetInt(ReducedMotionKey, 0) == 1;
            set => PlayerPrefs.SetInt(ReducedMotionKey, value ? 1 : 0);
        }

        public static bool HasSave => PlayerPrefs.GetInt(SaveKey, 0) == 1;

        public static void MarkReachedWalk()
        {
            PlayerPrefs.SetInt(SaveKey, 1);
            PlayerPrefs.Save();
        }

        public static void Apply()
        {
            AudioListener.volume = Master;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ApplyOnStart()
        {
            Apply();
        }
    }
}
