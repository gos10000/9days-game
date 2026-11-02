using UnityEngine;

namespace NineDays
{
    /// <summary>战斗配置（战斗模块的轮次 / 先手 / 行动上限 / 节奏）。</summary>
    [CreateAssetMenu(menuName = "9Days/配置/战斗配置", fileName = "BattleConfig")]
    public class BattleConfig : ScriptableObject
    {
        [Header("轮次")]
        [Tooltip("打满多少轮结束（按剩余血量判胜负）")]
        public int roundsToPlay = 9;

        [Header("先手")]
        [Tooltip("0 = 每次真随机；非 0 = 固定种子，便于复现/验收")]
        public int randomSeed;

        [Header("行动")]
        [Tooltip("一轮最多行动次数（含额外行动），防止无限追加")]
        public int maxTurnsPerRound = 6;

        [Header("节奏")]
        [Tooltip("每次行动结算后到下一个行动者之间的停顿（秒）")]
        public float turnResolveDelay = 0.6f;

        [Tooltip("轮结束后的停顿，给表现层（UI / 镜头）留时间")]
        public float roundResolveDelay = 1f;

        [Tooltip("单次行动结算的最长等待时间（子弹飞出后等停稳）")]
        public float turnResolveTimeout = 3f;
    }
}
