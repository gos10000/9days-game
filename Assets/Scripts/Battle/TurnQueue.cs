using System;
using System.Collections.Generic;

namespace NineDays
{
    /// <summary>
    /// 一轮内的行动队列：默认每队各一次，支持追加到队尾 / 插到下一个，
    /// 总行动数受 maxTurnsPerRound 限制（含已经行动过的次数）。
    /// </summary>
    public class TurnQueue
    {
        private readonly List<Team> pending = new List<Team>();
        private readonly int maxTurnsPerRound;
        private int consumed;

        public TurnQueue(int maxTurnsPerRound = 6)
        {
            this.maxTurnsPerRound = Math.Max(2, maxTurnsPerRound);
        }

        public int PendingCount => pending.Count;
        public int ConsumedCount => consumed;
        public int TotalTurns => consumed + pending.Count;
        public int MaxTurns => maxTurnsPerRound;
        public bool IsEmpty => pending.Count == 0;
        public Team Next => pending.Count > 0 ? pending[0] : Team.None;

        /// <summary>用本轮初始行动者重置队列（同时清空已消耗计数）。</summary>
        public void Reset(IEnumerable<Team> actors)
        {
            pending.Clear();
            consumed = 0;
            if (actors == null) return;

            foreach (Team actor in actors)
            {
                if (pending.Count >= maxTurnsPerRound) break;
                pending.Add(actor);
            }
        }

        public bool TryDequeue(out Team actor)
        {
            if (pending.Count == 0)
            {
                actor = Team.None;
                return false;
            }

            actor = pending[0];
            pending.RemoveAt(0);
            consumed++;
            return true;
        }

        /// <summary>追加到队尾（本轮正常顺序打完之后再打）。</summary>
        public bool TryAppend(Team team) => TryAdd(team, false);

        /// <summary>插到下一个行动（立即生效）。</summary>
        public bool TryInsertNext(Team team) => TryAdd(team, true);

        private bool TryAdd(Team team, bool insertNext)
        {
            if (team == Team.None) return false;
            if (TotalTurns >= maxTurnsPerRound) return false;

            if (insertNext) pending.Insert(0, team);
            else pending.Add(team);
            return true;
        }
    }
}
