using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Ankhu.Realms.Core;
using Ankhu.Realms.Core.Content;
using UnityEditor;
using UnityEngine;

namespace Ankhu.Realms.EditorTools
{
    /// <summary>
    /// Zac 2026-11-24: every spell word and every target the player can type must have its hieroglyphs, and every
    /// sign must ship as a texture. This check walks every word any room accepts and fails on a gap.
    ///
    /// Wernes painted and Realm rooms: each spoken verb (not a deed), each object a spoken rule acts on and each
    /// spoken name needs a gloss with signs. Borrowed Dawn and the teaching rooms (SpellParser, read by reflection
    /// because the editor assembly does not reference that package): each spell verb, its object words, the bind
    /// targets and the directions must gloss through Lexicon.TryGlossToken. Every lexicon entry counts too.
    /// For every sign of every gloss, Resources/RealmGlyphs must hold both code.png and code_glow.png and the
    /// GlyphTable must know it. Deeds (hands, not words) and movement carry no glyphs and are skipped.
    ///
    /// Report: art_gen/realms/audit/glyph_coverage_check.md. CheckCli exits 1 when anything is missing.
    /// </summary>
    public static class GlyphCoverageCheck
    {
        private const string ReportPath = "art_gen/realms/audit/glyph_coverage_check.md";
        private const string GlyphDir = "Assets/Ankhu/Realms/Art/Glyphs/Resources/RealmGlyphs";

        /// <summary>BD and teaching canonicals that are deeds or movement, so they fly no glyphs.</summary>
        private static readonly HashSet<string> BdUnspoken = new HashSet<string> { "take", "install", "board", "enter", "descend", "cross", "lower", "go to" };
        private static readonly HashSet<string> Glue = new HashSet<string> { "the", "a", "an", "in", "into", "on", "onto", "to" };

        private sealed class Entry
        {
            public string Where;
            public string Word;
            public string Signs;
            public string Problem;
        }

        [MenuItem("Ankhu/Glyphs/Check Glyph Coverage")]
        public static void CheckFromMenu()
        {
            int bad = Run(out string report);
            Debug.Log(report);
            EditorUtility.DisplayDialog("Glyph coverage", bad == 0 ? "Every word has its signs." : bad + " gaps. See " + ReportPath, "OK");
        }

        public static void CheckCli()
        {
            int bad = 0;
            try { bad = Run(out string report); Debug.Log(report); }
            catch (Exception e) { Debug.LogException(e); bad = 99; }
            EditorApplication.Exit(bad == 0 ? 0 : 1);
        }

        public static int Run(out string report)
        {
            var rows = new List<Entry>();
            string root = Directory.GetParent(Application.dataPath).FullName;
            string glyphDir = Path.Combine(root, GlyphDir);

            void Add(string where, string word, WordGloss g, string missingWhy)
            {
                var e = new Entry { Where = where, Word = word, Signs = g == null ? "" : string.Join(" ", g.Signs) };
                if (g == null || g.Signs.Count == 0) e.Problem = missingWhy;
                else
                {
                    var gaps = new List<string>();
                    foreach (string raw in g.Signs)
                    {
                        string code = GlyphAtlas.Normalize(raw);
                        var miss = new List<string>();
                        if (!File.Exists(Path.Combine(glyphDir, code + ".png"))) miss.Add("png");
                        if (!File.Exists(Path.Combine(glyphDir, code + "_glow.png"))) miss.Add("glow png");
                        if (!GlyphAtlas.Has(code)) miss.Add("GlyphTable entry");
                        if (miss.Count > 0) gaps.Add(code + " (no " + string.Join(", ", miss) + ")");
                    }
                    if (gaps.Count > 0) e.Problem = "sign asset missing: " + string.Join("; ", gaps);
                }
                rows.Add(e);
            }

            // Every lexicon entry, whichever room speaks it.
            foreach (string v in Lexicon.AllVerbs) Add("Lexicon verb", v, Lexicon.Gloss(v), "no signs");
            foreach (string n in Lexicon.KnownNames) Add("Lexicon name", n, Lexicon.NameGloss(n), "no signs");
            foreach (string t in Lexicon.AllNounAndDirectionTokens)
            {
                Lexicon.TryGlossToken(t, out WordGloss g);
                Add("Lexicon noun or direction", t, g, "no signs");
            }

            // Wernes painted rooms and the Realm rooms.
            var scripts = WernesPaintedScripts.All().Concat(WernesScripts.All()).Concat(RosetauScripts.All()).Where(s => s?.Room != null).ToList();
            foreach (SceneScript s in scripts)
            {
                RoomDef d = s.Room;
                string where = s.SceneName;
                var spoken = new HashSet<string>(d.Verbs.Concat(d.Rules.Select(r => r.Verb)).Where(v => v != "auto" && v != "name" && !d.Deeds.Contains(v)));
                foreach (string v in spoken.OrderBy(x => x))
                    Add(where, v, Lexicon.AllVerbs.Contains(v) ? Lexicon.Gloss(v) : null, "spoken verb with no lexicon gloss");

                var targets = d.Rules.Where(r => r.Verb != "auto" && r.Verb != "name" && !d.Deeds.Contains(r.Verb) && !string.IsNullOrEmpty(r.Target))
                                     .Select(r => r.Target).Distinct();
                foreach (string id in targets.OrderBy(x => x))
                {
                    RoomObject o = d.Find(id);
                    if (o == null) continue;
                    string word = o.Name + (o.Aliases.Length > 0 ? " (" + string.Join(", ", o.Aliases) + ")" : "");
                    Add(where, word, o.Gloss, "spell target with no gloss");
                }

                foreach (SpellRule r in d.Rules.Where(r => r.Verb == "name" && r.NameValue != null))
                {
                    WordGloss g = r.NameGloss ?? (Lexicon.KnownNames.Contains(r.NameValue) ? Lexicon.NameGloss(r.NameValue) : null);
                    Add(where, "name " + r.NameValue, g, "spoken name with no gloss (it would fall back to the generic ren)");
                }
            }

            // Borrowed Dawn and the teaching rooms.
            Type parser = FindType("Ankhu.BorrowedDawn.SpellParser");
            if (parser == null)
            {
                rows.Add(new Entry { Where = "BorrowedDawn", Word = "SpellParser", Problem = "type Ankhu.BorrowedDawn.SpellParser not found" });
            }
            else
            {
                var bdTokens = new SortedDictionary<string, string>();
                void Need(string token, string why) { if (!Glue.Contains(token) && !bdTokens.ContainsKey(token)) bdTokens[token] = why; }

                var rules = (IEnumerable)parser.GetField("Rules", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
                foreach (object rule in rules)
                {
                    Type rt = rule.GetType();
                    string canonical = (string)rt.GetField("Canonical").GetValue(rule);
                    if (BdUnspoken.Contains(canonical)) continue;
                    foreach (string t in (string[])rt.GetField("Phrase").GetValue(rule)) Need(t, "spell verb (" + canonical + ")");
                    if (rt.GetField("FixedObjects").GetValue(rule) is IDictionary objs)
                        foreach (object key in objs.Keys)
                            foreach (string t in (string[])key) Need(t, "object of " + canonical);
                }
                foreach (string t in ((IEnumerable)parser.GetField("DirectionAliases").GetValue(null)).Cast<object>().Select(KeyOf)) Need(t, "direction for flow");
                foreach (string t in ((IEnumerable)parser.GetField("BindTargetAliases").GetValue(null)).Cast<object>().Select(KeyOf)) Need(t, "bind or release target");

                foreach (var kv in bdTokens)
                {
                    Lexicon.TryGlossToken(kv.Key, out WordGloss g);
                    Add("BorrowedDawn and Teach (" + kv.Value + ")", kv.Key, g, "typed word fades, no Egyptian form");
                }
            }

            var bad = rows.Where(r => r.Problem != null).ToList();
            var sb = new StringBuilder();
            sb.AppendLine("# Glyph coverage check");
            sb.AppendLine();
            sb.AppendLine("Run " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + ". " + (bad.Count == 0 ? "PASS, " + rows.Count + " words, every one has its signs." : "FAIL, " + bad.Count + " gaps in " + rows.Count + " words."));
            sb.AppendLine();
            if (bad.Count > 0)
            {
                sb.AppendLine("## Gaps");
                sb.AppendLine();
                sb.AppendLine("| Where | Word | Signs | Problem |");
                sb.AppendLine("|---|---|---|---|");
                foreach (Entry e in bad) sb.AppendLine("| " + e.Where + " | " + e.Word + " | " + e.Signs + " | " + e.Problem + " |");
                sb.AppendLine();
            }
            sb.AppendLine("## Every word checked");
            sb.AppendLine();
            sb.AppendLine("| Where | Word | Signs | Status |");
            sb.AppendLine("|---|---|---|---|");
            foreach (Entry e in rows) sb.AppendLine("| " + e.Where + " | " + e.Word + " | " + e.Signs + " | " + (e.Problem ?? "ok") + " |");
            report = sb.ToString();
            string full = Path.Combine(root, ReportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllText(full, report);
            return bad.Count;
        }

        private static string KeyOf(object kv) => (string)kv.GetType().GetProperty("Key").GetValue(kv);

        private static Type FindType(string fullName)
        {
            foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = a.GetType(fullName, false);
                if (t != null) return t;
            }
            return null;
        }
    }
}
