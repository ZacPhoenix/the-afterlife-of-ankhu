using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Ankhu.Tests.RoomGuides
{
    /// <summary>
    /// Zac 2026-11-26: every room guide in D:\TheAfterlifeofAnkhu\docs\rooms carries e2e blocks (canonical solution,
    /// alternate orders, failures). This plays each block through the real rules and the director's beat logic, with
    /// no scene, and checks every expected state, beat, film, flag and exit. Report:
    /// D:\TheAfterlifeofAnkhu\art_gen\realms\audit\room_guides_sim.md. The play-mode twin is RoomGuidePlayTests.
    /// </summary>
    public sealed class RoomGuideSimTests
    {
        private static readonly List<ScriptResult> results = new List<ScriptResult>();

        public static IEnumerable<TestCaseData> Blocks()
        {
            foreach (GuideScript g in GuideParser.LoadAll())
                yield return new TestCaseData(g).SetName("Guide " + g.Id.Replace("/", " ").Replace(".", "_"));
        }

        [Test]
        public void GuidesExist()
        {
            Assert.IsNotEmpty(GuideParser.LoadAll(), "No e2e blocks found under " + GuideParser.GuideFolder());
        }

        [TestCaseSource(nameof(Blocks))]
        public void PlaysAsTheGuideSays(GuideScript g)
        {
            ScriptResult r = g.Engine == "bd" ? BdRunner.Run(g) : SimRunner.Run(g);
            results.Add(r);
            if (r.Skipped) Assert.Ignore(r.Summary);
            Assert.IsTrue(r.Pass, r.Explain());
        }

        [OneTimeTearDown]
        public void WriteReport()
        {
            if (results.Count > 0) GuideReport.Write("room_guides_sim.md", "Room guides, rules simulation", results.OrderBy(r => r.Script.Id));
        }
    }
}
