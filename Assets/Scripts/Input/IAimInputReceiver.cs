using UnityEngine;

namespace NineDays
{
    /// <summary>瞄准输入的接收端；把设备输入和具体角色表现解耦，便于以后接 AI 或网络。</summary>
    public interface IAimInputReceiver
    {
        bool IsAiming { get; }

        void BeginAim(Vector2 pointerWorld);
        void UpdateAim(Vector2 pointerWorld);
        void ReleaseAim(Vector2 pointerWorld);
        void CancelAim();
    }
}
