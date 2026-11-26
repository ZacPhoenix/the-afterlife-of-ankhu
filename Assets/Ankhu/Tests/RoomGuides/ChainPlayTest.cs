using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Ankhu.Tests.RoomGuides
{
    /// <summary>
    /// Zac 2026-11-26: the whole built game, First Gate to the shield brought home, played in one go. Each room's
    /// canonical guide block runs in the scene the previous room's exit loaded, with the codex and Wernes progress
    /// carried over, so a broken exit, a missing hand-over or a room that only works from a clean start shows here.
    /// Report: D:\TheAfterlifeofAnkhu\art_gen\realms\audit\room_guides_chain.md.
    /// </summary>
    public sealed class ChainPlayTest
    {
        /// <summary>(guide file prefix, block) in play order. The hub returns between every Wernes room.</summary>
        public static readonly (string guide, string block)[] Chain =
        {
            ("04_", "canonical"), ("05_", "canonical"), ("06_", "canonical"), ("07_", "canonical"), ("08_", "canonical"),
            ("09_", "canonical"), ("10_", "canonical"),
            ("11_", "canonical"), ("10_", "after-a"),
            ("12_", "canonical"), ("10_", "after-b"),
            ("13_", "canonical"), ("10_", "after-c"),
            ("14_", "canonical"), ("10_", "after-d"),
            ("16_", "canonical"),
            ("17_", "canonical"), ("18_", "canonical"), ("19_", "canonical"), ("20_", "canonical"), ("21_", "canonical"),
        };

        [UnityTest, Timeout(5400000)]
        public IEnumerator WholeGameInPlayOrder()
        {
            List<GuideScript> all = GuideParser.LoadAll();
            var results = new List<ScriptResult>();
            bool first = true;
            foreach (var (guide, block) in Chain)
            {
                GuideScript g = all.FirstOrDefault(x => System.IO.Path.GetFileName(x.File).StartsWith(guide) && x.Name == block);
                Assert.IsNotNull(g, $"no e2e block {block} in guide {guide}*");
                ScriptResult r;
                if (g.Engine == "bd")
                {
                    r = new ScriptResult { Script = g };
                    yield return BdRunner.Play(g, r, !first);
                }
                else
                {
                    var p = new PlayRunner(g, !first);
                    yield return p.Run();
                    r = p.Result;
                }
                r.Mode = "chain";
                results.Add(r);
                GuideReport.Write("room_guides_chain.md", "Room guides, whole game in play order", results);
                if (!r.Pass) Assert.Fail(r.Explain());
                first = false;
            }
        }
    }
}
