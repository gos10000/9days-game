using UnityEngine;

namespace NineDays
{
    /// <summary>
    /// 场上还在生效的「时长型效果」（牵引区等）。
    /// 回合结算会等它们结束，避免下一位玩家在特效还在演的时候就开始抽卡。
    /// </summary>
    public static class ActiveEffects
    {
        private static int count;

        public static bool Any => count > 0;
        public static int Count => count;

        public static void Register() => count++;

        public static void Unregister()
        {
            if (count > 0) count--;
        }

        /// <summary>战斗重开/场景切换时兜底清零，避免计数卡住导致永远不结算。</summary>
        public static void Clear() => count = 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad() => count = 0;
    }
}
