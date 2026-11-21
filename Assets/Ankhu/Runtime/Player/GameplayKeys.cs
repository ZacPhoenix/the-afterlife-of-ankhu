using System.Collections.Generic;
using Ankhu.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ankhu.Player
{
    /// <summary>
    /// The only place gameplay code reads the keyboard. Every read returns idle while
    /// <see cref="InputGate.GameplayInputAllowed"/> is false. When a text entry session ends,
    /// any gameplay key still held at that moment stays suppressed until it is released, so keys
    /// held or typed while the speech bubble was open never leak into movement or hotkeys.
    /// </summary>
    public static class GameplayKeys
    {
        private static readonly Key[] Tracked =
        {
            Key.A, Key.D, Key.LeftArrow, Key.RightArrow,
            Key.W, Key.S, Key.UpArrow, Key.DownArrow,
            Key.E, Key.Q, Key.U, Key.H, Key.R, Key.F2
        };

        private static readonly List<Key> suppressed = new List<Key>();
        private static int seenClosedFrame = -1;
        private static int refreshedFrame = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            suppressed.Clear();
            seenClosedFrame = -1;
            refreshedFrame = -1;
        }

        /// <summary>The keyboard when gameplay may read it, otherwise null.</summary>
        private static Keyboard GameplayKeyboard()
        {
            if (!InputGate.GameplayInputAllowed)
            {
                return null;
            }
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return null;
            }
            Refresh(keyboard);
            return keyboard;
        }

        private static void Refresh(Keyboard keyboard)
        {
            if (InputGate.TextEntryClosedFrame != seenClosedFrame)
            {
                seenClosedFrame = InputGate.TextEntryClosedFrame;
                suppressed.Clear();
                for (int i = 0; i < Tracked.Length; i++)
                {
                    if (keyboard[Tracked[i]].isPressed)
                    {
                        suppressed.Add(Tracked[i]);
                    }
                }
                refreshedFrame = -1;
            }
            int frame = Time.frameCount;
            if (frame == refreshedFrame)
            {
                return;
            }
            refreshedFrame = frame;
            for (int i = suppressed.Count - 1; i >= 0; i--)
            {
                if (!keyboard[suppressed[i]].isPressed)
                {
                    suppressed.RemoveAt(i);
                }
            }
        }

        /// <summary>True while the key is down and gameplay may read it.</summary>
        public static bool Held(Key key)
        {
            Keyboard keyboard = GameplayKeyboard();
            return keyboard != null && !suppressed.Contains(key) && keyboard[key].isPressed;
        }

        /// <summary>True on the frame the key went down, when gameplay may read it. Use from Update only.</summary>
        public static bool Pressed(Key key)
        {
            Keyboard keyboard = GameplayKeyboard();
            return keyboard != null && !suppressed.Contains(key) && keyboard[key].wasPressedThisFrame;
        }

        /// <summary>-1 west, +1 east, 0 idle. A/D or Left/Right.</summary>
        public static float Horizontal()
        {
            float value = 0f;
            if (Held(Key.D) || Held(Key.RightArrow))
            {
                value += 1f;
            }
            if (Held(Key.A) || Held(Key.LeftArrow))
            {
                value -= 1f;
            }
            return value;
        }

        /// <summary>-1 down, +1 up, 0 idle. W/S or Up/Down.</summary>
        public static float Vertical()
        {
            float value = 0f;
            if (Held(Key.W) || Held(Key.UpArrow))
            {
                value += 1f;
            }
            if (Held(Key.S) || Held(Key.DownArrow))
            {
                value -= 1f;
            }
            return value;
        }
    }
}
