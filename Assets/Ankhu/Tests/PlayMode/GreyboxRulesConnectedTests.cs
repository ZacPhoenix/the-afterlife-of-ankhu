using System.Collections;
using Ankhu.Bindings;
using Ankhu.Core;
using Ankhu.Integration;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Ankhu.Tests
{
    /// <summary>
    /// Loads the generated greybox in play mode and checks that the real rules are connected and
    /// answer a spell. Fails if the scene runs on the traversal test adapter or on no adapter.
    /// Requires the scene in Build Settings, which the greybox builder does.
    /// </summary>
    public sealed class GreyboxRulesConnectedTests
    {
        private const string SceneName = "BorrowedDawn_Greybox";

        [UnityTest]
        public IEnumerator GreyboxSceneRunsOnTheRulesLibrary()
        {
            Assert.IsTrue(Application.CanStreamedLevelBeLoaded(SceneName),
                SceneName + " is not in Build Settings. Run Ankhu > Build Borrowed Dawn Greybox first.");

            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return null;
            yield return null;

            RoomRulesHost host = RoomRulesHost.Instance;
            Assert.IsNotNull(host, "No RoomRulesHost is active in the greybox");
            Assert.IsNotNull(host.Adapter, "RoomRulesHost has no adapter");
            Assert.IsInstanceOf<BorrowedDawnRulesAdapter>(host.Adapter, "The greybox is running without the rules library");
            Assert.IsNotNull(host.Snapshot);

            CommandResult result = host.Adapter.Speak("flow west", new PlayerContext(ZoneIds.Floor, false, false));
            Assert.AreEqual(CommandOutcome.Success, result.Outcome, result.Message);
            Assert.AreEqual(WaterSide.West, host.Snapshot.Water);
        }
    }
}
