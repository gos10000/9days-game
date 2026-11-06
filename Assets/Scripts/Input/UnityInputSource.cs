using UnityEngine;
using UnityEngine.InputSystem;

namespace NineDays
{
    /// <summary>鼠标 / 键盘输入源；整个战斗输入模块只在这里直接访问 Input System。</summary>
    public sealed class UnityInputSource : IInputSource
    {
        /// <summary>读取鼠标位置、左键、右键和 Esc，生成当帧快照。</summary>
        public InputSnapshot Read()
        {
            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;

            bool hasPointer = mouse != null;
            Vector2 pointer = hasPointer ? mouse.position.ReadValue() : Vector2.zero;

            return new InputSnapshot(
                hasPointer,
                pointer,
                hasPointer && mouse.leftButton.wasPressedThisFrame,
                hasPointer && mouse.leftButton.isPressed,
                hasPointer && mouse.leftButton.wasReleasedThisFrame,
                hasPointer && mouse.rightButton.wasPressedThisFrame,
                keyboard != null && keyboard.escapeKey.wasPressedThisFrame);
        }

        /// <summary>把调试热键映射成语义动作，HUD 不再直接使用 Key 枚举。</summary>
        public bool WasPressed(GameInputAction action)
        {
            switch (action)
            {
                case GameInputAction.TestShot:
                    return KeyPressed(Key.Space);
                case GameInputAction.ForcePlayer1:
                    return KeyPressed(Key.Digit1);
                case GameInputAction.ForcePlayer2:
                    return KeyPressed(Key.Digit2);
                case GameInputAction.ExtraTurn:
                    return KeyPressed(Key.E);
                case GameInputAction.Restart:
                    return KeyPressed(Key.R);
                case GameInputAction.ToggleImpactLog:
                    return KeyPressed(Key.F1);
                case GameInputAction.ToggleAutoTest:
                    return KeyPressed(Key.F2) || KeyPressed(Key.T);
                case GameInputAction.ToggleSlowMotion:
                    return KeyPressed(Key.F3) || KeyPressed(Key.G);
                default:
                    return false;
            }
        }

        private static bool KeyPressed(Key key)
        {
            return Keyboard.current != null && Keyboard.current[key].wasPressedThisFrame;
        }
    }
}
