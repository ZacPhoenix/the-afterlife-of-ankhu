using System.Collections;
using System.IO;
using UnityEngine;

namespace Ankhu.Bindings
{
    /// <summary>
    /// Built-player check for Anpu's farewell door (pick 9). Off unless started with -ankhuDoorCheck &lt;folder&gt;.
    /// Opens the door overlay over whatever scene loads, closes it, saves screenshots and quits. Muted.
    /// </summary>
    public sealed class FarewellDoorCheck : MonoBehaviour
    {
        private static string dir;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == "-ankhuDoorCheck") dir = args[i + 1];
            if (string.IsNullOrEmpty(dir)) return;
            dir = Path.GetFullPath(dir);
            Directory.CreateDirectory(dir);
            Application.runInBackground = true;
            AudioListener.volume = 0f;
            var go = new GameObject("FarewellDoorCheck");
            DontDestroyOnLoad(go);
            go.AddComponent<FarewellDoorCheck>();
        }

        private IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(dir, name), tex.EncodeToPNG());
            Destroy(tex);
        }

        private IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(3f);
            var door = FarewellDoor.Open();
            if (door == null) { File.WriteAllText(Path.Combine(dir, "door_check.txt"), "FAIL: art missing"); Application.Quit(2); yield break; }
            yield return new WaitForSecondsRealtime(2f);
            yield return Shot("door_open.png");
            StartCoroutine(door.Close(4f));
            yield return new WaitForSecondsRealtime(2.6f);
            yield return Shot("door_mid.png");
            while (!door.Closed) yield return null;
            yield return new WaitForSecondsRealtime(0.15f);
            yield return Shot("door_thud.png");
            yield return new WaitForSecondsRealtime(1.8f);
            yield return Shot("door_closed.png");
            File.WriteAllText(Path.Combine(dir, "door_check.txt"), "PASS: opened, closed, 4 shots");
            Application.Quit(0);
        }
    }
}
