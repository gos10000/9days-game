using UnityEngine;

namespace NineDays
{
    /// <summary>一帧的原始输入快照；面向玩法，不暴露 Input System 设备类型。</summary>
    public readonly struct InputSnapshot
    {
        public static readonly InputSnapshot Empty = new InputSnapshot(
            false, Vector2.zero, false, false, false, false, false);

        public bool HasPointer { get; }
        public Vector2 PointerScreen { get; }

        public bool PrimaryPressed { get; }
        public bool PrimaryHeld { get; }
        public bool PrimaryReleased { get; }

        public bool SecondaryPressed { get; }
        public bool CancelPressed { get; }

        public InputSnapshot(bool hasPointer, Vector2 pointerScreen,
                             bool primaryPressed, bool primaryHeld, bool primaryReleased,
                             bool secondaryPressed, bool cancelPressed)
        {
            HasPointer = hasPointer;
            PointerScreen = pointerScreen;
            PrimaryPressed = primaryPressed;
            PrimaryHeld = primaryHeld;
            PrimaryReleased = primaryReleased;
            SecondaryPressed = secondaryPressed;
            CancelPressed = cancelPressed;
        }
    }

    /// <summary>输入源接口；以后接入 Input Actions / 手柄 / 重绑定时只替换实现。</summary>
    public interface IInputSource
    {
        InputSnapshot Read();
        bool WasPressed(GameInputAction action);
    }

    /// <summary>语义化输入动作；业务代码不直接依赖物理按键枚举。</summary>
    public enum GameInputAction
    {
        TestShot,
        ForcePlayer1,
        ForcePlayer2,
        ExtraTurn,
        Restart,
        ToggleImpactLog,
        ToggleAutoTest,
        ToggleSlowMotion,
    }
}
