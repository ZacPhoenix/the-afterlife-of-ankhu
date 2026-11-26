using System;
using System.Collections.Generic;
using Ankhu.Realms.Core;
using UnityEngine;

namespace Ankhu.Realms
{
    /// <summary>
    /// The one roll of the whole game. Anpu gives it once, and everything Ankhu learns is written on it.
    /// Persisted in PlayerPrefs under <see cref="PrefsKey"/>: loaded on first access, saved on every change.
    /// Writing raises <see cref="Written"/> (toasts listen). Revisions, epithets, questions and notes raise their own events.
    /// </summary>
    public static class CodexStore
    {
        public const string PrefsKey = "ankhu.realms.codex";

        private static CodexJournal journal;
        private static bool silent;
        private static readonly HashSet<string> quiet = new HashSet<string>();

        /// <summary>A new entry was written (not raised for <see cref="EnsureEntries"/> or for entries the roll presents itself).</summary>
        public static event Action<CodexEntry> Written;

        /// <summary>Any new writing on the roll, including entries the roll presents itself. The roll listens to this.</summary>
        public static event Action<CodexEntry> WrittenAny;
        public static event Action<CodexRevision> Revised;
        public static event Action<CodexEpithet> EpithetAdded;
        public static event Action<CodexQuestion> Asked;
        public static event Action<CodexQuestion> Answered;
        public static event Action<CodexNoteRecord> Noted;

        /// <summary>Anything changed: a write, a read mark, a reset. The HUD badge and the roll refresh on it.</summary>
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            journal = null;
            silent = false;
            quiet.Clear();
            Written = null;
            WrittenAny = null;
            Revised = null;
            EpithetAdded = null;
            Asked = null;
            Answered = null;
            Noted = null;
            Changed = null;
        }

        /// <summary>The journal. Loaded from PlayerPrefs the first time it is touched.</summary>
        public static CodexJournal Journal
        {
            get
            {
                if (journal == null)
                {
                    journal = new CodexJournal();
                    try
                    {
                        string payload = PlayerPrefs.GetString(PrefsKey, string.Empty);
                        if (!string.IsNullOrEmpty(payload)) journal.Load(payload);
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning("[Ankhu.Realms] Could not load the codex: " + e.Message);
                    }
                    journal.Written += OnJournalWritten;
                    journal.Revised += r => { Save(); if (!silent) { Ensure(); Revised?.Invoke(r); } };
                    journal.EpithetAdded += ep => { Save(); if (!silent) { Ensure(); EpithetAdded?.Invoke(ep); } };
                    journal.Asked += q => { Save(); if (!silent) Asked?.Invoke(q); };
                    journal.Answered += q => { Save(); if (!silent) Answered?.Invoke(q); };
                    journal.Noted += n => { Save(); if (!silent) Noted?.Invoke(n); };
                }
                return journal;
            }
        }

        private static void Ensure()
        {
            if (Application.isPlaying) CodexBook.Ensure();
        }

        public static int UnreadCount => Journal.UnreadCount;

        public static bool Has(string id) => Journal.Has(id);

        /// <summary>Write an entry by id. Returns false if the id is unknown or already written.</summary>
        public static bool Write(string id) => Write(id, false);

        /// <summary>
        /// Write an entry. With <paramref name="presented"/> the small toast is skipped, because the roll opens
        /// itself and his hand writes it on screen (<see cref="CodexBook.Present"/>).
        /// </summary>
        public static bool Write(string id, bool presented)
        {
            if (string.IsNullOrEmpty(id)) return false;
            if (CodexCatalog.Get(id) == null)
            {
                Debug.LogWarning("[Ankhu.Realms] Unknown codex entry '" + id + "'.");
                return false;
            }
            if (presented) quiet.Add(id);
            try { return Journal.Write(id); }
            finally { quiet.Remove(id); }
        }

        public static bool Revise(string revisionId) => Journal.Revise(revisionId);
        public static bool AddEpithet(string epithetId) => Journal.AddEpithet(epithetId);
        public static bool Ask(string questionId) => Journal.Ask(questionId);
        public static bool Answer(string questionId) => Journal.Answer(questionId);
        public static void Note(CodexHand hand, string scene, string text) => Journal.Note(hand, scene, text);

        /// <summary>
        /// Development helper for a scene loaded directly: writes any missing ids quietly (no toast, marked read),
        /// so the roll holds what the player would know by this point. Accepts entry ids and "ep." epithet ids.
        /// </summary>
        public static void EnsureEntries(params string[] ids)
        {
            if (ids == null) return;
            bool any = false;
            silent = true;
            try
            {
                foreach (string id in ids)
                {
                    if (string.IsNullOrEmpty(id)) continue;
                    if (id.StartsWith("ep.", StringComparison.Ordinal))
                    {
                        if (Journal.AddEpithet(id)) { Journal.MarkRead(id); any = true; }
                        continue;
                    }
                    if (id.StartsWith("rev.", StringComparison.Ordinal))
                    {
                        if (Journal.Revise(id)) { Journal.MarkRead(id); any = true; }
                        continue;
                    }
                    if (CodexCatalog.Get(id) == null || Journal.Has(id)) continue;
                    Journal.Write(id);
                    Journal.MarkRead(id);
                    any = true;
                }
                // Every roll starts with his name on the wrap.
                if (Journal.Order.Count > 0 && Journal.AddEpithet("ep.ren")) { Journal.MarkRead("ep.ren"); any = true; }
            }
            finally
            {
                silent = false;
            }
            if (any) Save();
        }

        public static bool IsUnread(string id) => Journal.IsUnread(id);

        public static void MarkRead(string id)
        {
            Journal.MarkRead(id);
            Save();
        }

        /// <summary>Mark several entries read with a single save.</summary>
        public static void MarkRead(IEnumerable<string> ids)
        {
            foreach (string id in ids) Journal.MarkRead(id);
            Save();
        }

        /// <summary>New Game: forget everything written and clear the saved codex.</summary>
        public static void Reset()
        {
            Journal.Clear();
            PlayerPrefs.DeleteKey(PrefsKey);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        public static void Save()
        {
            try
            {
                PlayerPrefs.SetString(PrefsKey, Journal.Save());
                PlayerPrefs.Save();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Ankhu.Realms] Could not save the codex: " + e.Message);
            }
            Changed?.Invoke();
        }

        private static void OnJournalWritten(CodexEntry entry)
        {
            Save();
            if (silent) return;
            Ensure();
            WrittenAny?.Invoke(entry);
            if (!quiet.Contains(entry.Id)) Written?.Invoke(entry);
        }
    }
}
