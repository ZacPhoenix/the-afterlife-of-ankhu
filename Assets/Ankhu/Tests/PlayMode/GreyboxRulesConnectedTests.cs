using System.Collections;
using Ankhu.Bindings;
using Ankhu.Core;
using Ankhu.Integration;
using Ankhu.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Ankhu.Tests
{
    /// <summary>
    /// Loads each generated scene in play mode and checks that the real rules are connected to the
    /// right room, that Ankhu stands where the rules say he starts, and that Anpu speaks in the
    /// teaching rooms. Requires the scenes in Build Settings, which the greybox builder does.
    /// </summary>
    public sealed class GreyboxRulesConnectedTests
    {
        private static IEnumerator Load(string sceneName)
        {
            Assert.IsTrue(Application.CanStreamedLevelBeLoaded(sceneName),
                sceneName + " is not in Build Settings. Run Ankhu > Build Borrowed Dawn Greybox first.");
            yield return SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            // Let physics settle so the zone tracker has reported Ankhu's starting zone.
            for (int i = 0; i < 20; i++)
            {
                yield return new WaitForFixedUpdate();
            }
        }

        private static BorrowedDawnRulesAdapter RequireAdapter()
        {
            RoomRulesHost host = RoomRulesHost.Instance;
            Assert.IsNotNull(host, "No RoomRulesHost is active");
            Assert.IsNotNull(host.Adapter, "RoomRulesHost has no adapter");
            Assert.IsInstanceOf<BorrowedDawnRulesAdapter>(host.Adapter, "The scene is running without the rules library");
            return (BorrowedDawnRulesAdapter)host.Adapter;
        }

        [UnityTest]
        public IEnumerator GreyboxSceneRunsOnTheRulesLibrary()
        {
            yield return Load("BorrowedDawn_Greybox");
            BorrowedDawnRulesAdapter adapter = RequireAdapter();
            Assert.AreEqual(BorrowedDawnRooms.BorrowedDawnId, adapter.Room.Id);

            CommandResult result = adapter.Speak("flow west", new PlayerContext(ZoneIds.Floor, false, false));
            Assert.AreEqual(CommandOutcome.Success, result.Outcome, result.Message);
            Assert.AreEqual(WaterSide.West, adapter.Current.Water);
        }

        [UnityTest]
        public IEnumerator TeachingScenesPlayTheirRoomsWithAnkhuAtTheStart(
            [Values("teach-1-cistern", "teach-2-held-step", "teach-3-lingering-shadow")] string roomId)
        {
            yield return Load(BorrowedDawnRooms.SceneNameFor(roomId));
            BorrowedDawnRulesAdapter adapter = RequireAdapter();
            Assert.AreEqual(roomId, adapter.Room.Id);
            Assert.IsFalse(adapter.IsSolved, "A teaching room must not start solved");

            PlayerZoneTracker tracker = Object.FindAnyObjectByType<PlayerZoneTracker>();
            Assert.IsNotNull(tracker);
            Assert.AreEqual(adapter.RulesZoneId, tracker.ZoneId, "Ankhu spawned outside the zone the rules start him in");

            TeachingRoomDirector director = Object.FindAnyObjectByType<TeachingRoomDirector>();
            Assert.IsNotNull(director, "Anpu's director is missing");
            Assert.IsNotEmpty(director.CurrentLine, "Anpu shows nothing on entry");
        }
    }
}
