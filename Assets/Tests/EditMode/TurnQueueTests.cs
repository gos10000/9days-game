using System.Collections.Generic;
using NUnit.Framework;

namespace NineDays.Tests
{
    /// <summary>一轮行动队列：默认 2 动、追加/插队、上限、重置。</summary>
    public class TurnQueueTests
    {
        private static TurnQueue NewQueue(int maxTurns = 6)
        {
            var queue = new TurnQueue(maxTurns);
            queue.Reset(new List<Team> { Team.Player1, Team.Player2 });
            return queue;
        }

        [Test]
        public void DefaultRound_HasTwoTurns_ThenEmpty()
        {
            TurnQueue queue = NewQueue();

            Assert.IsTrue(queue.TryDequeue(out Team first));
            Assert.AreEqual(Team.Player1, first);

            Assert.IsTrue(queue.TryDequeue(out Team second));
            Assert.AreEqual(Team.Player2, second);

            Assert.IsFalse(queue.TryDequeue(out Team none));
            Assert.AreEqual(Team.None, none);
        }

        [Test]
        public void Append_GoesToQueueEnd()
        {
            TurnQueue queue = NewQueue();
            queue.TryDequeue(out _);                 // P1 已行动
            Assert.IsTrue(queue.TryAppend(Team.Player1));

            queue.TryDequeue(out Team second);
            Assert.AreEqual(Team.Player2, second);

            queue.TryDequeue(out Team extra);
            Assert.AreEqual(Team.Player1, extra);
        }

        [Test]
        public void InsertNext_TakesImmediateEffect()
        {
            TurnQueue queue = NewQueue();
            Assert.IsTrue(queue.TryInsertNext(Team.Player2));

            queue.TryDequeue(out Team first);
            Assert.AreEqual(Team.Player2, first);
        }

        [Test]
        public void ExtraTurns_RespectMaxTurnsPerRound()
        {
            TurnQueue queue = NewQueue(3);           // 本轮最多 3 动
            Assert.IsTrue(queue.TryAppend(Team.Player1));
            Assert.IsFalse(queue.TryAppend(Team.Player2));
            Assert.AreEqual(3, queue.TotalTurns);
        }

        [Test]
        public void Reset_ClearsConsumedCount()
        {
            TurnQueue queue = NewQueue(3);
            queue.TryDequeue(out _);
            queue.TryAppend(Team.Player2);
            Assert.IsFalse(queue.TryAppend(Team.Player1));

            queue.Reset(new List<Team> { Team.Player1, Team.Player2 });
            Assert.AreEqual(2, queue.PendingCount);
            Assert.AreEqual(0, queue.ConsumedCount);
            Assert.IsTrue(queue.TryAppend(Team.Player1));
        }

        [Test]
        public void NoneIsRejected()
        {
            TurnQueue queue = NewQueue();
            Assert.IsFalse(queue.TryAppend(Team.None));
            Assert.IsFalse(queue.TryInsertNext(Team.None));
            Assert.AreEqual(2, queue.PendingCount);
        }
    }
}
