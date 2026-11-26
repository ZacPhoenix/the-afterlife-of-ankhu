using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ankhu.ArtChecks
{
    /// <summary>
    /// Build check for the Borrowed Dawn dressing, off unless the player is started with -bdCapture &lt;folder&gt;.
    /// It loads BorrowedDawn_Greybox straight away, mutes audio, saves two screenshots of the room as it ships,
    /// then quits. Used to prove a player build carries the art pass, not just the editor scene.
    /// </summary>
    public sealed class BorrowedDawnPlayerCapture : MonoBehaviour
    {
        private const string Scene = "BorrowedDawn_Greybox";
        private static string dir;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "-bdCapture") dir = args[i + 1];
            }
            if (string.IsNullOrEmpty(dir)) return;
            dir = Path.GetFullPath(dir);
            Directory.CreateDirectory(dir);
            Application.runInBackground = true;
            AudioListener.volume = 0f;
            var go = new GameObject("BorrowedDawnPlayerCapture");
            DontDestroyOnLoad(go);
            go.AddComponent<BorrowedDawnPlayerCapture>();
        }

        private IEnumerator Start()
        {
            yield return null;
            SceneManager.LoadScene(Scene, LoadSceneMode.Single);
            float end = Time.unscaledTime + 30f;
            while (SceneManager.GetActiveScene().name != Scene && Time.unscaledTime < end) yield return null;
            yield return new WaitForSecondsRealtime(3f);
            Snap("bd_start_a");
            yield return new WaitForSecondsRealtime(2f);
            Snap("bd_start_b");
            yield return new WaitForSecondsRealtime(1f);
            Debug.Log("[BDCapture] done in " + dir + " scene " + SceneManager.GetActiveScene().name);
            Application.Quit();
        }

        private void LateUpdate() => AudioListener.volume = 0f;

        private static void Snap(string tag)
        {
            int size = Mathf.Max(1, Mathf.RoundToInt(1080f / Mathf.Max(1, Screen.height)));
            ScreenCapture.CaptureScreenshot(Path.Combine(dir, tag + ".png"), size);
        }
    }
}
