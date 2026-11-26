using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Ankhu.Tests.RoomGuides
{
    /// <summary>
    /// Zac 2026-11-26: the room guides played in the real scenes. Each e2e block of each guide in
    /// D:\TheAfterlifeofAnkhu\docs\rooms loads its scene and plays through the real director, speech bubble, beats,
    /// codex roll, memory films and exit. Report: D:\TheAfterlifeofAnkhu\art_gen\realms\audit\room_guides_play.md.
    /// Batch: Unity.exe -batchmode -projectPath D:\TheAfterlifeofAnkhu -runTests -testPlatform PlayMode
    ///        -testFilter Ankhu.Tests.RoomGuides -testResults D:\TheAfterlifeofAnkhu\art_gen\realms\audit\room_guides_play.xml
    /// </summary>
    public sealed class RoomGuidePlayTests
    {
        private static readonly List<ScriptResult> results = new List<ScriptResult>();

        public static IEnumerable<TestCaseData> Blocks()
        {
            foreach (GuideScript g in GuideParser.LoadAll())
                yield return new TestCaseData(g).SetName("Play " + g.Id.Replace("/", " ").Replace(".", "_")).Returns(null);
        }

        [UnityTest, Timeout(900000)]
        [TestCaseSource(nameof(Blocks))]
        public IEnumerator PlaysInTheScene(GuideScript g)
        {
            ScriptResult r;
            if (g.Engine == "bd")
            {
                r = new ScriptResult { Script = g };
                yield return BdRunner.Play(g, r);
            }
            else
            {
                var p = new PlayRunner(g);
                yield return p.Run();
                r = p.Result;
            }
            results.Add(r);
            GuideReport.Write("room_guides_play.md", "Room guides, played in the scenes", results.OrderBy(x => x.Script.Id));
            if (r.Skipped) Assert.Ignore(r.Summary);
            Assert.IsTrue(r.Pass, r.Explain());
        }
    }
}
