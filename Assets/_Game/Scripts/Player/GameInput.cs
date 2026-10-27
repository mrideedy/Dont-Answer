using UnityEngine;
using UnityEngine.InputSystem;

namespace DontAnswer
{
    /// <summary>Thin wrapper over the Input System devices so every script reads input the same way.</summary>
    public static class GameInput
    {
        static Keyboard K => Keyboard.current;
        static Mouse M => Mouse.current;

        public static Vector2 Move
        {
            get
            {
                var k = K;
                if (k == null) return Vector2.zero;
                float x = 0f, y = 0f;
                if (k.aKey.isPressed || k.leftArrowKey.isPressed) x -= 1f;
                if (k.dKey.isPressed || k.rightArrowKey.isPressed) x += 1f;
                if (k.sKey.isPressed || k.downArrowKey.isPressed) y -= 1f;
                if (k.wKey.isPressed || k.upArrowKey.isPressed) y += 1f;
                var v = new Vector2(x, y);
                return v.sqrMagnitude > 1f ? v.normalized : v;
            }
        }

        public static Vector2 Look => M != null ? M.delta.ReadValue() : Vector2.zero;

        public static bool Interact =>
            (K != null && K.eKey.wasPressedThisFrame) || (M != null && M.leftButton.wasPressedThisFrame);

        /// <summary>Leave a view (peephole, note): E, Space, Esc or right mouse.</summary>
        public static bool Back =>
            (K != null && (K.eKey.wasPressedThisFrame || K.spaceKey.wasPressedThisFrame || K.escapeKey.wasPressedThisFrame || K.sKey.wasPressedThisFrame)) ||
            (M != null && M.rightButton.wasPressedThisFrame);

        public static bool InteractHeld =>
            (K != null && K.eKey.isPressed) || (M != null && M.leftButton.isPressed);

        public static bool Flashlight => K != null && K.fKey.wasPressedThisFrame;

        public static bool Pause => K != null && K.escapeKey.wasPressedThisFrame;
        public static bool Phone => K != null && (K.tabKey.wasPressedThisFrame || K.qKey.wasPressedThisFrame);
        public static bool Rules => K != null && (K.nKey.wasPressedThisFrame || K.rKey.wasPressedThisFrame);

        /// <summary>Speed up / skip dialogue.</summary>
        public static bool Skip =>
            (K != null && (K.spaceKey.wasPressedThisFrame || K.enterKey.wasPressedThisFrame)) ||
            (M != null && M.leftButton.wasPressedThisFrame);

        public static int ChoiceNumber
        {
            get
            {
                var k = K;
                if (k == null) return -1;
                if (k.digit1Key.wasPressedThisFrame || k.numpad1Key.wasPressedThisFrame) return 0;
                if (k.digit2Key.wasPressedThisFrame || k.numpad2Key.wasPressedThisFrame) return 1;
                if (k.digit3Key.wasPressedThisFrame || k.numpad3Key.wasPressedThisFrame) return 2;
                if (k.digit4Key.wasPressedThisFrame || k.numpad4Key.wasPressedThisFrame) return 3;
                if (k.digit5Key.wasPressedThisFrame || k.numpad5Key.wasPressedThisFrame) return 4;
                if (k.digit6Key.wasPressedThisFrame || k.numpad6Key.wasPressedThisFrame) return 5;
                return -1;
            }
        }
    }
}
