using UnityEngine;
using UnityEngine.InputSystem;

namespace NoSleep
{
    public static class GameInput
    {
        public static Vector2 Move
        {
            get
            {
                var k = Keyboard.current; Vector2 value = Vector2.zero;
                if (k != null)
                {
                    if (k.aKey.isPressed || k.leftArrowKey.isPressed) value.x--;
                    if (k.dKey.isPressed || k.rightArrowKey.isPressed) value.x++;
                    if (k.sKey.isPressed || k.downArrowKey.isPressed) value.y--;
                    if (k.wKey.isPressed || k.upArrowKey.isPressed) value.y++;
                }
                if (Gamepad.current != null && Gamepad.current.leftStick.ReadValue().sqrMagnitude > value.sqrMagnitude) value = Gamepad.current.leftStick.ReadValue();
                return Vector2.ClampMagnitude(value, 1);
            }
        }
        public static bool Run => (Keyboard.current != null && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed)) || (Gamepad.current != null && Gamepad.current.leftStickButton.isPressed);
        public static bool Confirm => (Keyboard.current != null && (Keyboard.current.eKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame)) || (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);
        public static bool Status => (Keyboard.current != null && (Keyboard.current.tabKey.wasPressedThisFrame || Keyboard.current.iKey.wasPressedThisFrame)) || (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame);
        public static bool Cancel => (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) || (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);
        public static bool QuickSave => Keyboard.current != null && Keyboard.current.f5Key.wasPressedThisFrame;
        public static bool QuickLoad => Keyboard.current != null && Keyboard.current.f9Key.wasPressedThisFrame;
        public static bool Journal => Keyboard.current != null && Keyboard.current.jKey.wasPressedThisFrame;
        public static int ChoiceDelta
        {
            get
            {
                var k = Keyboard.current; var g = Gamepad.current;
                if ((k != null && (k.upArrowKey.wasPressedThisFrame || k.wKey.wasPressedThisFrame)) || (g != null && g.dpad.up.wasPressedThisFrame)) return -1;
                if ((k != null && (k.downArrowKey.wasPressedThisFrame || k.sKey.wasPressedThisFrame)) || (g != null && g.dpad.down.wasPressedThisFrame)) return 1;
                return 0;
            }
        }
    }
}
