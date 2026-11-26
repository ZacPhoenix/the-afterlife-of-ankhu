using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Ankhu.EditorTools
{
    /// <summary>
    /// Keeps the Borrowed Dawn art dressing in every player build. The greybox builder (and the EditMode
    /// test GreyboxSceneWiringTests, which runs it) rewrites the Borrowed Dawn scenes as plain greybox.
    /// Before any player build that includes one of those scenes, this re-runs BorrowedDawnDresser when a
    /// scene has lost its "Dressing" root, so a build can never ship the undressed room. It restores the
    /// Build Settings list and the open scenes afterwards, so other build scripts see what they set.
    /// </summary>
    public sealed class BorrowedDawnDressOnBuild : IPreprocessBuildWithReport
    {
        public int callbackOrder => -100;

        public void OnPreprocessBuild(BuildReport report)
        {
            string[] built = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            string[] dressable = BorrowedDawnGreyboxBuilder.Sequence.Select(s => s.ScenePath).ToArray();
            string[] wanted = dressable.Where(p => built.Contains(p)).ToArray();
            if (wanted.Length == 0)
            {
                return;
            }
            string[] undressed = wanted.Where(p => !IsDressed(p)).ToArray();
            if (undressed.Length == 0)
            {
                Debug.Log("[DressOnBuild] Borrowed Dawn scenes already dressed.");
                return;
            }

            Debug.Log("[DressOnBuild] Undressed before build: " + string.Join(", ", undressed.Select(Path.GetFileNameWithoutExtension)) + ". Re-dressing.");
            EditorBuildSettingsScene[] savedScenes = EditorBuildSettings.scenes;
            SceneSetup[] savedSetup = Application.isBatchMode ? null : EditorSceneManager.GetSceneManagerSetup();
            try
            {
                BorrowedDawnDresser.BuildAll();
            }
            finally
            {
                EditorBuildSettings.scenes = savedScenes;
                if (savedSetup != null && savedSetup.Length > 0)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(savedSetup);
                }
            }

            string[] still = wanted.Where(p => !IsDressed(p)).ToArray();
            if (still.Length > 0)
            {
                throw new BuildFailedException("Borrowed Dawn dressing failed for " + string.Join(", ", still));
            }
            Debug.Log("[DressOnBuild] Borrowed Dawn re-dressed for the build.");
        }

        /// <summary>The dresser adds a "Room/Dressing" object. Scenes are text YAML, so a name check is enough.</summary>
        public static bool IsDressed(string scenePath)
        {
            if (!File.Exists(scenePath))
            {
                return false;
            }
            return File.ReadAllText(scenePath).Contains("m_Name: Dressing\n") || File.ReadAllText(scenePath).Contains("m_Name: Dressing\r\n");
        }
    }
}
