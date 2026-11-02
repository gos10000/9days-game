using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace NineDays.Tests
{
    /// <summary>先手判定与轮数用尽后的胜负判定。</summary>
    public class BattleRulesTests
    {
        [Test]
        public void LowerHealth_ActsFirst()
        {
            var rng = new Random(1);

            Assert.AreEqual(Team.Player2,
                BattleRules.DecideFirstActor(Team.Player1, 80f, Team.Player2, 50f, rng));

            Assert.AreEqual(Team.Player1,
                BattleRules.DecideFirstActor(Team.Player1, 50f, Team.Player2, 80f, rng));
        }

        [Test]
        public void EqualHealth_IsDeterministicWithSameSeed()
        {
            Team a = BattleRules.DecideFirstActor(Team.Player1, 100f, Team.Player2, 100f, new Random(12345));
            Team b = BattleRules.DecideFirstActor(Team.Player1, 100f, Team.Player2, 100f, new Random(12345));

            Assert.AreEqual(a, b);
        }

        [Test]
        public void EqualHealth_CanProduceBothSides()
        {
            var seen = new HashSet<Team>();
            for (int seed = 0; seed < 32; seed++)
                seen.Add(BattleRules.DecideFirstActor(Team.Player1, 100f, Team.Player2, 100f, new Random(seed)));

            Assert.IsTrue(seen.Contains(Team.Player1));
            Assert.IsTrue(seen.Contains(Team.Player2));
        }

        [Test]
        public void NullRandom_FallsBackToTeamA()
        {
            Assert.AreEqual(Team.Player1,
                BattleRules.DecideFirstActor(Team.Player1, 30f, Team.Player2, 30f, null));
        }

        [Test]
        public void Winner_ByRemainingHealth_TieIsNone()
        {
            Assert.AreEqual(Team.Player1, BattleRules.DecideWinner(Team.Player1, 40f, Team.Player2, 20f));
            Assert.AreEqual(Team.Player2, BattleRules.DecideWinner(Team.Player1, 10f, Team.Player2, 20f));
            Assert.AreEqual(Team.None, BattleRules.DecideWinner(Team.Player1, 30f, Team.Player2, 30f));
        }
    }
}
