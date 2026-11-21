using System;
using Ankhu.Realms.Core;
using UnityEngine;

namespace Ankhu.Realms
{
    /// <summary>
    /// The one codex of the whole game. Anpu gives it once, and everything Ankhu learns is written into it.
    /// Persisted in PlayerPrefs under <see cref="PrefsKey"/>: loaded on first access, saved on every change.
    /// Writing a new entry raises <see cref="Written"/>, which the toast and the book listen to.
    /// </summary>
    public static class CodexStore
    {
        public const string PrefsKey = "ankhu.realms.codex";

        private static CodexJournal journal;
        private static bool silent;

        /// <summary>A new entry was written (not raised for <see cref="EnsureEntries"/>).</summary>
        public static event Action<CodexEntry> Written;

        /// <summary>Anything changed: a write, a read mark, a reset. The HUD badge and the book refresh on it.</summary>
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            journal = null;
            silent = false;
            Written = null;
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
                }
                return journal;
            }
        }

        public static int UnreadCount => Journal.UnreadCount;

        public static bool Has(string id) => Journal.Has(id);

        /// <summary>Write an entry by id. Returns false if the id is unknown or already written.</summary>
        public static bool Write(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            if (CodexCatalog.Get(id) == null)
            {
                Debug.LogWarning("[Ankhu.Realms] Unknown codex entry '" + id + "'.");
                return false;
            }
            return Journal.Write(id);
        }

        /// <summary>
        /// Development helper for a scene loaded directly: writes any missing ids quietly (no toast, marked read),
        /// so the codex holds what the player would know by this point.
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
                    if (CodexCatalog.Get(id) == null || Journal.Has(id)) continue;
                    Journal.Write(id);
                    Journal.MarkRead(id);
                    any = true;
                }
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
            if (!Journal.IsUnread(id)) return;
            Journal.MarkRead(id);
            Save();
        }

        /// <summary>Mark several entries read with a single save.</summary>
        public static void MarkRead(System.Collections.Generic.IEnumerable<string> ids)
        {
            bool any = false;
            foreach (string id in ids)
            {
                if (!Journal.IsUnread(id)) continue;
                Journal.MarkRead(id);
                any = true;
            }
            if (any) Save();
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
            if (Application.isPlaying) CodexBook.Ensure();
            Written?.Invoke(entry);
        }
    }
}
