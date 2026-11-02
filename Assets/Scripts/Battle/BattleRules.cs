using System;

namespace NineDays
{
    /// <summary>
    /// 战斗规则（纯逻辑，不依赖 MonoBehaviour，便于 EditMode 测试）。
    /// 先手：血量低者先；相同由注入的随机源决定。
    /// 胜负：轮数用尽时比剩余血量，相同为平局（Team.None）。
    /// </summary>
    public static class BattleRules
    {
        /// <summary>血量低者先手；相同则用随机源决定（rng 为空时默认 teamA）。</summary>
        public static Team DecideFirstActor(Team teamA, float hpA, Team teamB, float hpB, Random rng)
        {
            if (hpA < hpB) return teamA;
            if (hpB < hpA) return teamB;
            if (rng == null) return teamA;
            return rng.Next(2) == 0 ? teamA : teamB;
        }

        /// <summary>轮数用尽时按剩余血量判胜负；相同返回 None（平局）。</summary>
        public static Team DecideWinner(Team teamA, float hpA, Team teamB, float hpB)
        {
            if (hpA > hpB) return teamA;
            if (hpB > hpA) return teamB;
            return Team.None;
        }
    }
}
