using System;
using UnityEngine;

namespace Ankhu.Realms.EditorTools
{
    /// <summary>
    /// One batch session for a backdrop swap: the NoStretchCheck fix pass and check (as FixCli), then
    /// RealmCapture.CaptureSceneCli for -capScene. Unity runs only one -executeMethod, so this chains them.
    /// The FixCli result (0 pass, 1 fail, 99 error) is logged as "[FixThenCapture] FixCli exit N".
    /// </summary>
    public static class FixThenCaptureCli
    {
        public static void Run()
        {
            int code;
            bool strict = Array.IndexOf(System.Environment.GetCommandLineArgs(), "-strictRes") >= 0;
            try
            {
                NoStretchCheck.Run(true, strict, out _);
                int bad = NoStretchCheck.Run(false, strict, out string report);
                Debug.Log(report);
                code = bad == 0 ? 0 : 1;
            }
            catch (Exception e) { Debug.LogException(e); code = 99; }
            Debug.Log("[FixThenCapture] FixCli exit " + code);
            RealmCapture.CaptureSceneCli();
        }
    }
}
