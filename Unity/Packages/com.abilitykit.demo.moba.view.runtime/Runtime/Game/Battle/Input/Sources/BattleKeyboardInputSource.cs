using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace AbilityKit.Game.Flow
{
    internal static class BattleKeyboardInputSource
    {
        public static void ReadMove(out float dx, out float dz)
        {
            dx = 0f;
            dz = 0f;

#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.aKey.isPressed) dx -= 1f;
                if (kb.dKey.isPressed) dx += 1f;
                if (kb.wKey.isPressed) dz += 1f;
                if (kb.sKey.isPressed) dz -= 1f;
                return;
            }
#endif

            if (Input.GetKey(KeyCode.A)) dx -= 1f;
            if (Input.GetKey(KeyCode.D)) dx += 1f;
            if (Input.GetKey(KeyCode.W)) dz += 1f;
            if (Input.GetKey(KeyCode.S)) dz -= 1f;
        }

        public static void ReadSecondaryMove(out float dx, out float dz)
        {
            dx = 0f;
            dz = 0f;

#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.leftArrowKey.isPressed) dx -= 1f;
                if (kb.rightArrowKey.isPressed) dx += 1f;
                if (kb.upArrowKey.isPressed) dz += 1f;
                if (kb.downArrowKey.isPressed) dz -= 1f;
                return;
            }
#endif

            if (Input.GetKey(KeyCode.LeftArrow)) dx -= 1f;
            if (Input.GetKey(KeyCode.RightArrow)) dx += 1f;
            if (Input.GetKey(KeyCode.UpArrow)) dz += 1f;
            if (Input.GetKey(KeyCode.DownArrow)) dz -= 1f;
        }

        public static bool TryReadSkillSlotDown(out int slot)
        {
            slot = 0;

#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.jKey.wasPressedThisFrame) { slot = 1; return true; }
                if (kb.kKey.wasPressedThisFrame) { slot = 2; return true; }
                if (kb.lKey.wasPressedThisFrame) { slot = 3; return true; }
                return false;
            }
#endif

            if (Input.GetKeyDown(KeyCode.J)) { slot = 1; return true; }
            if (Input.GetKeyDown(KeyCode.K)) { slot = 2; return true; }
            if (Input.GetKeyDown(KeyCode.L)) { slot = 3; return true; }
            return false;
        }
    }
}
