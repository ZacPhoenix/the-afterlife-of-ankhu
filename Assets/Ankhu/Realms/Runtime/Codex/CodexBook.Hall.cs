using System;
using System.Collections;
using System.Collections.Generic;
using Ankhu.Realms.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Ankhu.Realms
{
    /// <summary>One sheet read aloud in the Hall of Two Maats.</summary>
    public struct CodexHallLine
    {
        public string EntryId;
        /// <summary>The revision whose line was chosen, or null for a sheet that was never rewritten.</summary>
        public string RevisionId;
        /// <summary>True when he read the true (rewritten) line, false when he read the old one.</summary>
        public bool ReadTrueLine;
    }

    /// <summary>
    /// The ending (spec section 7, picks 8 A+D). Before the Assessors he unrolls the roll and reads it aloud. The
    /// player chooses which sheets to read. On a rewritten sheet he chooses which line to say, the old one or the
    /// true one. Then the last sheet is written: his name with every epithet, ending "true of voice", and the roll
    /// closes on a complete cartouche.
    ///
    /// From a scene: <c>yield return CodexBook.ReadInHall(line => ...)</c> then <c>yield return CodexBook.WriteLastLine()</c>.
    /// The director does both for Step.Cue("codex:hall"). Results also collect in <see cref="HallResults"/>.
    /// </summary>
    public sealed partial class CodexBook
    {
        private const string ContinueText = "Close the roll to go on";
        private const string HallPromptKeys = "E  Read this sheet aloud        ENTER  I have read enough";
        private const string HallPromptPad = "A  Read this sheet aloud        START  I have read enough";
        private const string ChoiceKeys = "Q  Say the old line        E  Say the true line";
        private const string ChoicePad = "LB  Say the old line        RB  Say the true line";

        /// <summary>Every sheet read in the Hall this session, in order.</summary>
        public static readonly List<CodexHallLine> HallResults = new List<CodexHallLine>();

        /// <summary>Raised for each sheet read aloud.</summary>
        public static event Action<CodexHallLine> HallLineRead;

        private Text continueLabel;
        private bool hallMode;
        private bool hallReading;
        private bool hallDone;
        private int hallChoice;   // 0 none, 1 old, 2 true
        private bool hallChoosing;
        private readonly HashSet<string> hallRead = new HashSet<string>();

        /// <summary>
        /// Open the roll at its first sheet and let the player read sheets aloud until they say they have read enough.
        /// </summary>
        public static IEnumerator ReadInHall(Action<CodexHallLine> onRead)
        {
            CodexBook b = Ensure();
            HallResults.Clear();
            b.hallRead.Clear();
            b.hallDone = false;
            b.hallMode = true;
            b.filter = "All";
            if (b.state == State.Closed) b.RequestOpen(null);
            while (b.state != State.Open) yield return null;
            b.scrollTarget = 0f;
            while (b.scribing) yield return null;
            Action<CodexHallLine> relay = l => onRead?.Invoke(l);
            HallLineRead += relay;
            try
            {
                while (!b.hallDone) yield return null;
            }
            finally
            {
                HallLineRead -= relay;
                b.hallMode = false;
                b.continueLabel.text = ContinueText;
                b.continueGroup.alpha = 0f;
            }
        }

        /// <summary>
        /// The last sheet: the hand writes his name with every epithet, ending "true of voice", then the roll closes
        /// on its own with the cartouche complete.
        /// </summary>
        public static IEnumerator WriteLastLine()
        {
            CodexBook b = Ensure();
            if (b.state == State.Closed) { b.RequestOpen(null); }
            while (b.state != State.Open) yield return null;
            b.Queue("entry:acct.true_name");
            CodexStore.Write("acct.true_name", true);
            b.Queue("ep:ep.true");
            CodexStore.AddEpithet("ep.true");
            // Update rebuilds and starts the pen once the store has changed.
            float guard = Time.unscaledTime + 60f;
            while ((b.scribeQueue.Count > 0 || b.scribing) && Time.unscaledTime < guard) yield return null;
            for (float t = 0f; t < 0.8f; t += Time.unscaledDeltaTime) yield return null;
            yield return b.SealName();
            for (float t = 0f; t < 1.6f; t += Time.unscaledDeltaTime) yield return null;
            b.RequestClose();
            while (b.state != State.Closed) yield return null;
        }

        /// <summary>
        /// The name is complete. Gold light swells through the wrap twice, like a breath, and the cartouche lifts a
        /// little and settles. Nothing is said: the page has already said it.
        /// </summary>
        private IEnumerator SealName()
        {
            if (wrapContent == null) yield break;
            RectTransform cart = wrapContent.Find("Cartouche") as RectTransform;
            Image glow = CodexArt.Solid("SealLight", wrapContent, new Color(1f, 0.84f, 0.5f, 0f));
            CodexPages.UiStretch(glow.rectTransform);
            glow.raycastTarget = false;
            glow.transform.SetAsFirstSibling();
            Vector3 baseScale = cart != null ? cart.localScale : Vector3.one;
            const float dur = 3.2f;
            for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.Clamp01(t / dur);
                // two breaths, the second softer
                float breath = Mathf.Sin(k * Mathf.PI * 2f) * Mathf.Sin(k * Mathf.PI * 2f) * (k < 0.5f ? 1f : 0.6f);
                glow.color = new Color(1f, 0.84f, 0.5f, 0.32f * breath);
                if (cart != null) cart.localScale = baseScale * (1f + 0.05f * breath);
                yield return null;
            }
            if (cart != null) cart.localScale = baseScale;
            if (glow != null) Destroy(glow.gameObject);
        }

        // ================================================================== input and reading

        private void HallInput(Keyboard kb, Gamepad gp, Mouse mouse)
        {
            if (state != State.Open || scribing) return;
            // Read the click before Navigate starts a drag from it.
            bool clicked = mouse != null && mouse.leftButton.wasPressedThisFrame && Hit(viewport, mouse.position.ReadValue());
            if (!hallReading) Navigate(kb, gp, mouse, false);

            if (hallChoosing)
            {
                bool old = (kb != null && (kb.qKey.wasPressedThisFrame || kb.digit1Key.wasPressedThisFrame)) || (gp != null && gp.leftShoulder.wasPressedThisFrame);
                bool tru = (kb != null && (kb.eKey.wasPressedThisFrame || kb.digit2Key.wasPressedThisFrame)) || (gp != null && gp.rightShoulder.wasPressedThisFrame);
                if (old) hallChoice = 1;
                else if (tru) hallChoice = 2;
                return;
            }
            if (hallReading) return;

            bool read = (kb != null && (kb.eKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)) || (gp != null && gp.buttonSouth.wasPressedThisFrame)
                        || clicked;
            bool done = (kb != null && kb.enterKey.wasPressedThisFrame) || (gp != null && gp.startButton.wasPressedThisFrame);
            if (done) { hallDone = true; return; }
            if (read)
            {
                int i = CentreSheet();
                if (i >= 0) StartCoroutine(ReadSheet(i));
            }
        }

        private void UpdateHallPrompt()
        {
            if (!hallMode || continueLabel == null) return;
            string text = hallChoosing ? (GamepadMode ? ChoicePad : ChoiceKeys) : (GamepadMode ? HallPromptPad : HallPromptKeys);
            if (continueLabel.text != text) continueLabel.text = text;
            float target = state == State.Open && !scribing && (!hallReading || hallChoosing) ? 0.85f : 0f;
            continueGroup.alpha = Mathf.MoveTowards(continueGroup.alpha, target, Time.unscaledDeltaTime * 3f);
        }

        /// <summary>The entry sheet nearest the middle of the view.</summary>
        private int CentreSheet()
        {
            float mid = scroll + ViewW * 0.5f;
            int best = -1;
            float bestD = float.MaxValue;
            for (int i = 0; i < sheets.Count; i++)
            {
                if (sheets[i].Entry == null) continue;
                float d = Mathf.Abs(sheetX[i] + sheets[i].Width * 0.5f - mid);
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        private IEnumerator ReadSheet(int i)
        {
            hallReading = true;
            CodexSheetView v = sheets[i];
            scrollTarget = Mathf.Clamp(sheetX[i] + v.Width * 0.5f - ViewW * 0.5f, 0f, MaxScroll);

            // Gold light passes over the sheet while his voice reads it.
            Image wash = CodexArt.Solid("ReadLight", v.Root, new Color(1f, 0.82f, 0.45f, 0f));
            CodexPages.UiStretch(wash.rectTransform);
            wash.raycastTarget = false;
            AudioClip clip = RealmVoice.Find("codex." + v.Entry.Id);
            if (clip != null) { voice.clip = clip; voice.Play(); }
            float dur = clip != null ? clip.length : 2.2f;
            for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
            {
                wash.color = new Color(1f, 0.82f, 0.45f, 0.26f * Mathf.Sin(Mathf.Clamp01(t / dur) * Mathf.PI));
                yield return null;
            }

            // A rewritten sheet: which line does he say aloud?
            CodexSheetView.RevisionView rv = v.Revisions.Count > 0 ? v.Revisions[v.Revisions.Count - 1] : null;
            var line = new CodexHallLine { EntryId = v.Entry.Id, RevisionId = rv?.Revision.Id, ReadTrueLine = true };
            if (rv != null)
            {
                hallChoice = 0;
                hallChoosing = true;
                Color oldC = rv.Old != null ? rv.Old.color : CodexArt.InkSoft;
                Color newC = rv.New != null ? rv.New.color : CodexArt.Ink;
                float pulse = 0f;
                while (hallChoice == 0 && hallMode)
                {
                    pulse += Time.unscaledDeltaTime;
                    float k = 0.5f + 0.5f * Mathf.Sin(pulse * 3f);
                    if (rv.Old != null) rv.Old.color = Color.Lerp(oldC, CodexRollSheets.Rubric, 0.3f * k);
                    if (rv.New != null) rv.New.color = Color.Lerp(newC, CodexArt.GoldInk, 0.35f * k);
                    yield return null;
                }
                hallChoosing = false;
                line.ReadTrueLine = hallChoice != 1;
                Text said = line.ReadTrueLine ? rv.New : rv.Old;
                if (rv.Old != null) rv.Old.color = oldC;
                if (rv.New != null) rv.New.color = newC;
                AudioClip lineClip = RealmVoice.Find("codex." + rv.Revision.Id + (line.ReadTrueLine ? ".true" : ".old"));
                if (lineClip != null) { voice.clip = lineClip; voice.Play(); }
                float ld = lineClip != null ? lineClip.length : 1.8f;
                if (said != null)
                {
                    Color c0 = said.color;
                    for (float t = 0f; t < ld; t += Time.unscaledDeltaTime)
                    {
                        said.color = Color.Lerp(c0, line.ReadTrueLine ? CodexArt.GoldInk : CodexRollSheets.Rubric, Mathf.Sin(Mathf.Clamp01(t / ld) * Mathf.PI));
                        yield return null;
                    }
                    said.color = c0;
                }
            }
            if (wash != null) Destroy(wash.gameObject);

            // A small gold dot in the margin: this sheet has been said aloud.
            if (hallRead.Add(v.Entry.Id))
            {
                RawImage mark = CodexArt.Raw("ReadMark", v.Root, CodexArt.Disc, CodexArt.GoldInk);
                RectTransform r = mark.rectTransform;
                r.anchorMin = r.anchorMax = new Vector2(1f, 0f);
                r.pivot = new Vector2(0.5f, 0.5f);
                r.sizeDelta = new Vector2(14f, 14f);
                r.anchoredPosition = new Vector2(-30f, 36f);
            }
            HallResults.Add(line);
            try { HallLineRead?.Invoke(line); }
            catch (Exception e) { Debug.LogException(e, this); }
            hallReading = false;
        }
    }
}
