using UnityEngine;
using UnityEngine.InputSystem;

namespace Ankhu.Opening
{
    /// <summary>Device polling shared by the opening scenes (Input System: keyboard, mouse and gamepad).</summary>
    public static class OpeningInput
    {
        /// <summary>Set only by the review autopilot (OpeningAutoCapture). Overrides the stick when not null.</summary>
        public static System.Func<Vector2> AutoMove;

        public static Vector2 Move()
        {
            if (AutoMove != null) return Vector2.ClampMagnitude(AutoMove(), 1f);
            Vector2 v = Vector2.zero;
            Keyboard k = Keyboard.current;
            if (k != null)
            {
                if (k.aKey.isPressed || k.leftArrowKey.isPressed) v.x -= 1f;
                if (k.dKey.isPressed || k.rightArrowKey.isPressed) v.x += 1f;
                if (k.sKey.isPressed || k.downArrowKey.isPressed) v.y -= 1f;
                if (k.wKey.isPressed || k.upArrowKey.isPressed) v.y += 1f;
            }
            Gamepad g = Gamepad.current;
            if (g != null)
            {
                Vector2 s = g.leftStick.ReadValue();
                if (s.sqrMagnitude > v.sqrMagnitude) v = s;
                Vector2 d = g.dpad.ReadValue();
                if (d.sqrMagnitude > v.sqrMagnitude) v = d;
            }
            return Vector2.ClampMagnitude(v, 1f);
        }

        /// <summary>True while any key, mouse button or face button is held. Used by hold-to-skip.</summary>
        public static bool AnyHeld()
        {
            Keyboard k = Keyboard.current;
            if (k != null && k.anyKey.isPressed) return true;
            Mouse m = Mouse.current;
            if (m != null && (m.leftButton.isPressed || m.rightButton.isPressed)) return true;
            Gamepad g = Gamepad.current;
            if (g != null && (g.buttonSouth.isPressed || g.buttonEast.isPressed || g.buttonNorth.isPressed || g.buttonWest.isPressed || g.startButton.isPressed)) return true;
            return false;
        }

        /// <summary>Pointer or right stick position in -1..1, for parallax.</summary>
        /// <summary>Review autopilot only: a fixed pointer position for parallax captures.</summary>
        public static Vector2? LookOverride;

        public static Vector2 Look()
        {
            if (LookOverride.HasValue) return LookOverride.Value;
            Gamepad g = Gamepad.current;
            if (g != null)
            {
                Vector2 s = g.rightStick.ReadValue();
                if (s.sqrMagnitude > 0.01f) return s;
            }
            Mouse m = Mouse.current;
            if (m != null && Screen.width > 0 && Screen.height > 0)
            {
                Vector2 p = m.position.ReadValue();
                return new Vector2(Mathf.Clamp(p.x / Screen.width * 2f - 1f, -1f, 1f), Mathf.Clamp(p.y / Screen.height * 2f - 1f, -1f, 1f));
            }
            return Vector2.zero;
        }

        public static bool BackPressed()
        {
            Keyboard k = Keyboard.current;
            if (k != null && (k.escapeKey.wasPressedThisFrame || k.backspaceKey.wasPressedThisFrame)) return true;
            Gamepad g = Gamepad.current;
            return g != null && g.buttonEast.wasPressedThisFrame;
        }

        public static bool PausePressed()
        {
            Keyboard k = Keyboard.current;
            if (k != null && k.escapeKey.wasPressedThisFrame) return true;
            Gamepad g = Gamepad.current;
            return g != null && g.startButton.wasPressedThisFrame;
        }
    }
}
