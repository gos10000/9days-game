using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace NineDays.Tests
{
    /// <summary>战斗轮会话：轮循环、先手、额外行动、血量胜负、事件次数。</summary>
    public class BattleSessionTests
    {
        private class FakeCombatant : IBattleParticipant
        {
            public Team Team { get; }
            public float CurrentHealth { get; set; }
            public bool IsDefeated => CurrentHealth <= 0f;

            public FakeCombatant(Team team, float health)
            {
                Team = team;
                CurrentHealth = health;
            }
        }

        private class FakeRunner : ITurnActionRunner
        {
            public readonly List<Team> Begun = new List<Team>();
            public int Aborts;
            public bool AutoResolve = true;
            public float DamagePerTurn;
            public FakeCombatant DamageTarget;
            public System.Action<Team> OnBegin;

            private System.Action<TurnResult> callback;
            private Team actor;

            public void BeginTurn(Team actor, System.Action<TurnResult> onResolved)
            {
                this.actor = actor;
                Begun.Add(actor);
                callback = onResolved;

                OnBegin?.Invoke(actor);

                if (AutoResolve) Resolve();
            }

            public void Resolve()
            {
                System.Action<TurnResult> cb = callback;
                callback = null;

                float damage = 0f;
                if (DamageTarget != null && DamageTarget.Team != actor && DamagePerTurn > 0f)
                {
                    damage = Mathf.Min(DamagePerTurn, DamageTarget.CurrentHealth);
                    DamageTarget.CurrentHealth -= damage;
                }

                cb?.Invoke(new TurnResult(actor, true, damage > 0f, damage,
                                          DamageTarget != null && DamageTarget.IsDefeated));
            }

            public void Abort() => Aborts++;
        }

        private static BattleConfig NewConfig(int rounds, int seed = 7, int maxTurns = 6)
        {
            BattleConfig config = ScriptableObject.CreateInstance<BattleConfig>();
            config.roundsToPlay = rounds;
            config.randomSeed = seed;
            config.maxTurnsPerRound = maxTurns;
            config.roundResolveDelay = 0f;
            config.turnResolveTimeout = 0f;
            return config;
        }

        private static void NewSession(int rounds, float hp1, float hp2, int seed,
                                       out BattleSession session, out FakeRunner runner,
                                       out FakeCombatant p1, out FakeCombatant p2)
        {
            p1 = new FakeCombatant(Team.Player1, hp1);
            p2 = new FakeCombatant(Team.Player2, hp2);
            runner = new FakeRunner();
            session = new BattleSession(NewConfig(rounds, seed),
                                        new List<IBattleParticipant> { p1, p2 }, runner);
        }

        [Test]
        public void NineRounds_NoDamage_TieIsNone()
        {
            NewSession(9, 100f, 100f, 7, out BattleSession session, out FakeRunner runner,
                       out FakeCombatant _, out FakeCombatant _);

            BattleResult? result = null;
            int lastRound = 0;
            session.RoundStarted += (round, first) => lastRound = round;
            session.BattleEnded += r => result = r;

            session.Start();

            Assert.IsTrue(result.HasValue);
            Assert.AreEqual(Team.None, result.Value.Winner);
            Assert.AreEqual(9, result.Value.RoundsPlayed);
            Assert.AreEqual(9, lastRound);
            Assert.AreEqual(18, runner.Begun.Count);
        }

        [Test]
        public void Defeat_EndsBattleImmediately()
        {
            NewSession(9, 10f, 15f, 7, out BattleSession session, out FakeRunner runner,
                       out FakeCombatant _, out FakeCombatant p2);

            runner.DamagePerTurn = 10f;
            runner.DamageTarget = p2;

            BattleResult? result = null;
            session.BattleEnded += r => result = r;

            session.Start();

            Assert.IsTrue(result.HasValue);
            Assert.AreEqual(Team.Player1, result.Value.Winner);
            Assert.AreEqual(2, result.Value.RoundsPlayed);
            Assert.IsTrue(p2.IsDefeated);
        }

        [Test]
        public void BattleEnded_FiresOnlyOnce()
        {
            NewSession(9, 10f, 15f, 7, out BattleSession session, out FakeRunner runner,
                       out FakeCombatant _, out FakeCombatant p2);

            runner.DamagePerTurn = 10f;
            runner.DamageTarget = p2;

            int ended = 0;
            session.BattleEnded += _ => ended++;

            session.Start();
            session.RequestExtraTurn(Team.Player1);
            session.ContinueAfterRound();
            session.Abort();

            Assert.AreEqual(1, ended);
        }

        [Test]
        public void RequestExtraTurn_Immediate_InsertsNext()
        {
            NewSession(1, 10f, 100f, 7, out BattleSession session, out FakeRunner runner,
                       out FakeCombatant _, out FakeCombatant _);

            bool granted = false;
            runner.OnBegin = actor =>
            {
                if (granted || actor != Team.Player1) return;
                granted = true;
                session.RequestExtraTurn(Team.Player1, true);
            };

            session.Start();

            Assert.GreaterOrEqual(runner.Begun.Count, 2);
            Assert.AreEqual(Team.Player1, runner.Begun[0]);
            Assert.AreEqual(Team.Player1, runner.Begun[1]);
        }

        [Test]
        public void RequestExtraTurn_Append_GoesLast()
        {
            NewSession(1, 10f, 100f, 7, out BattleSession session, out FakeRunner runner,
                       out FakeCombatant _, out FakeCombatant _);

            bool granted = false;
            runner.OnBegin = actor =>
            {
                if (granted || actor != Team.Player1) return;
                granted = true;
                session.RequestExtraTurn(Team.Player1);
            };

            session.Start();

            Assert.GreaterOrEqual(runner.Begun.Count, 3);
            Assert.AreEqual(Team.Player1, runner.Begun[0]);
            Assert.AreEqual(Team.Player2, runner.Begun[1]);
            Assert.AreEqual(Team.Player1, runner.Begun[2]);
        }

        [Test]
        public void ExtraTurns_AreCappedByMaxTurnsPerRound()
        {
            var p1 = new FakeCombatant(Team.Player1, 10f);
            var p2 = new FakeCombatant(Team.Player2, 100f);
            var runner = new FakeRunner();
            var session = new BattleSession(NewConfig(1, 7, 3),
                                            new List<IBattleParticipant> { p1, p2 }, runner);

            int grantedCount = 0;
            bool requested = false;
            session.ExtraTurnGranted += (team, immediate) => grantedCount++;
            runner.OnBegin = actor =>
            {
                if (requested) return;
                requested = true;
                session.RequestExtraTurn(Team.Player1, false, 5);
            };

            session.Start();

            Assert.LessOrEqual(grantedCount, 1);
        }

        [Test]
        public void AutoAdvanceOff_StopsAfterRound_UntilContinue()
        {
            NewSession(2, 100f, 100f, 7, out BattleSession session, out FakeRunner runner,
                       out FakeCombatant _, out FakeCombatant _);

            session.AutoAdvanceRounds = false;
            session.Start();

            Assert.AreEqual(1, session.Round);
            Assert.IsTrue(session.IsAwaitingRoundAdvance);
            Assert.AreEqual(2, runner.Begun.Count);
            Assert.IsTrue(session.IsRunning);

            session.ContinueAfterRound();

            Assert.AreEqual(2, session.Round);
            Assert.IsFalse(session.IsRunning);
        }

        [Test]
        public void AutoAdvanceTurnsOff_WaitsAfterTurn_UntilContinue()
        {
            NewSession(1, 100f, 100f, 7, out BattleSession session, out FakeRunner runner,
                       out FakeCombatant _, out FakeCombatant _);

            session.AutoAdvanceTurns = false;
            session.Start();

            Assert.AreEqual(1, runner.Begun.Count);
            Assert.IsTrue(session.IsAwaitingTurnAdvance);
            Assert.IsTrue(session.IsRunning);

            session.ContinueAfterTurn();

            Assert.AreEqual(2, runner.Begun.Count);
            Assert.IsFalse(session.IsAwaitingTurnAdvance);
        }

        [Test]
        public void AutoAdvanceTurnsOff_LastTurnEndsRoundWithoutWaiting()
        {
            NewSession(1, 100f, 100f, 7, out BattleSession session, out FakeRunner runner,
                       out FakeCombatant _, out FakeCombatant _);

            session.AutoAdvanceTurns = false;
            session.Start();
            session.ContinueAfterTurn();   // 第二个行动

            Assert.IsFalse(session.IsAwaitingTurnAdvance);
            Assert.IsFalse(session.IsRunning);   // 1 轮结束 → 战斗结束（平局）
        }

        [Test]
        public void Abort_StopsSession()
        {
            NewSession(9, 100f, 100f, 7, out BattleSession session, out FakeRunner runner,
                       out FakeCombatant _, out FakeCombatant _);

            runner.AutoResolve = false;
            session.Start();
            session.Abort();

            Assert.IsFalse(session.IsRunning);
            Assert.AreEqual(1, runner.Aborts);
        }
    }
}
