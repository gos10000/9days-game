using System;
using System.Collections.Generic;

namespace NineDays
{
    /// <summary>战斗中的一方（角色或测试替身）。</summary>
    public interface IBattleParticipant
    {
        Team Team { get; }
        float CurrentHealth { get; }
        bool IsDefeated { get; }
    }

    /// <summary>一次行动的驱动方：真实实现是 TurnController，测试用 Fake。</summary>
    public interface ITurnActionRunner
    {
        void BeginTurn(Team actor, Action<TurnResult> onResolved);
        void Abort();
    }

    /// <summary>
    /// 战斗轮会话（纯逻辑，不依赖 MonoBehaviour）：
    /// 开局抽一次先手（血少优先 / 同血随机）→ 之后每轮沿用同一顺序，不再重算
    /// → 行动队列 → 额外行动 → 轮结束 → 血量胜负。
    /// </summary>
    public class BattleSession
    {
        private readonly BattleConfig config;
        private readonly IReadOnlyList<IBattleParticipant> participants;
        private readonly ITurnActionRunner runner;
        private readonly Random rng;

        private TurnQueue queue;
        private int round;
        private bool running;
        private Team battleFirstActor = Team.None;
        private bool awaitingRoundAdvance;
        private bool awaitingTurnAdvance;
        private Action<TurnResult> pendingCallback;

        public event Action<int, Team> RoundStarted;
        public event Action<int> RoundEnded;
        public event Action<TurnResult> TurnResolved;
        public event Action<Team, bool> ExtraTurnGranted;
        public event Action<BattleResult> BattleEnded;

        /// <summary>true = 轮结束立即开下一轮（测试用）；false = 等 ContinueAfterRound。</summary>
        public bool AutoAdvanceRounds { get; set; } = true;

        /// <summary>true = 行动结算后立刻开下一动（测试用）；false = 等 ContinueAfterTurn。</summary>
        public bool AutoAdvanceTurns { get; set; } = true;

        public int Round => round;
        public Team CurrentActor { get; private set; } = Team.None;
        public bool IsRunning => running;
        public bool IsAwaitingRoundAdvance => awaitingRoundAdvance;
        public bool IsAwaitingTurnAdvance => awaitingTurnAdvance;

        public BattleSession(BattleConfig config, IReadOnlyList<IBattleParticipant> participants,
                             ITurnActionRunner runner, Random rng = null)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (participants == null || participants.Count < 2)
                throw new ArgumentException("至少需要两名参战者", nameof(participants));
            if (runner == null) throw new ArgumentNullException(nameof(runner));

            this.config = config;
            this.participants = participants;
            this.runner = runner;
            this.rng = rng ?? (config.randomSeed != 0 ? new Random(config.randomSeed) : new Random());
        }

        /// <summary>开始战斗：进入第 1 轮。</summary>
        public void Start()
        {
            if (running) return;
            running = true;
            round = 0;
            battleFirstActor = Team.None;
            BeginRound();
        }

        /// <summary>表现层停顿结束后继续下一轮（AutoAdvanceRounds = false 时使用）。</summary>
        public void ContinueAfterRound()
        {
            if (!awaitingRoundAdvance) return;
            awaitingRoundAdvance = false;
            if (!running) return;
            BeginRound();
        }

        /// <summary>行动后停顿结束，继续下一个行动者（AutoAdvanceTurns = false 时使用）。</summary>
        public void ContinueAfterTurn()
        {
            if (!awaitingTurnAdvance) return;
            awaitingTurnAdvance = false;
            if (!running) return;
            NextTurn();
        }

        /// <summary>追加额外行动：immediate = 插到下一个，否则排到队尾。</summary>
        public void RequestExtraTurn(Team team, bool immediate = false, int count = 1)
        {
            if (!running || awaitingRoundAdvance || queue == null) return;

            for (int i = 0; i < count; i++)
            {
                bool added = immediate ? queue.TryInsertNext(team) : queue.TryAppend(team);
                if (!added) break;
                ExtraTurnGranted?.Invoke(team, immediate);
            }
        }

        /// <summary>中止战斗（切场景 / 退出）。</summary>
        public void Abort()
        {
            if (!running) return;
            running = false;
            awaitingRoundAdvance = false;
            awaitingTurnAdvance = false;
            runner.Abort();
        }

        private void BeginRound()
        {
            round++;
            IBattleParticipant a = participants[0];
            IBattleParticipant b = participants[1];

            // 先手只在开局抽一次；之后每一轮都沿用同一顺序，不再按血量重算
            if (battleFirstActor == Team.None)
                battleFirstActor = BattleRules.DecideFirstActor(a.Team, a.CurrentHealth, b.Team, b.CurrentHealth, rng);

            Team first = battleFirstActor;

            queue = new TurnQueue(config.maxTurnsPerRound);
            queue.Reset(new[] { first, OpponentOf(first) });
            awaitingRoundAdvance = false;
            awaitingTurnAdvance = false;

            RoundStarted?.Invoke(round, first);
            NextTurn();
        }

        private void NextTurn()
        {
            if (!running) return;

            if (!queue.TryDequeue(out Team actor))
            {
                EndRound();
                return;
            }

            CurrentActor = actor;
            pendingCallback = OnTurnResolved;
            runner.BeginTurn(actor, pendingCallback);
        }

        private void OnTurnResolved(TurnResult result)
        {
            if (!running) return;
            pendingCallback = null;

            int defeatedCount = 0;
            Team defeatedTeam = Team.None;
            for (int i = 0; i < participants.Count; i++)
            {
                if (!participants[i].IsDefeated) continue;
                defeatedCount++;
                defeatedTeam = participants[i].Team;
            }

            bool battleOver = defeatedCount >= 1;
            if (battleOver) awaitingTurnAdvance = false;
            else if (!AutoAdvanceTurns && !queue.IsEmpty) awaitingTurnAdvance = true;

            TurnResolved?.Invoke(result);

            if (battleOver)
            {
                EndBattle(defeatedCount == 1 ? OpponentOf(defeatedTeam) : Team.None);
                return;
            }

            if (awaitingTurnAdvance) return;
            NextTurn();
        }

        private void EndRound()
        {
            RoundEnded?.Invoke(round);

            if (round >= config.roundsToPlay)
            {
                EndBattle(BattleRules.DecideWinner(
                    participants[0].Team, participants[0].CurrentHealth,
                    participants[1].Team, participants[1].CurrentHealth));
                return;
            }

            if (!AutoAdvanceRounds)
            {
                awaitingRoundAdvance = true;
                return;
            }

            BeginRound();
        }

        private void EndBattle(Team winner)
        {
            if (!running) return;

            running = false;
            awaitingRoundAdvance = false;
            awaitingTurnAdvance = false;

            var result = new BattleResult(winner, round,
                                         participants[0].CurrentHealth, participants[1].CurrentHealth);
            BattleEnded?.Invoke(result);
        }

        private static Team OpponentOf(Team team)
        {
            if (team == Team.Player1) return Team.Player2;
            if (team == Team.Player2) return Team.Player1;
            return Team.None;
        }
    }
}
