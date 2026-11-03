using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace NineDays.Tests
{
    /// <summary>爆发运行时：计时、冷却、次数、OnImpact 过滤。</summary>
    public class BurstRuntimeTests
    {
        private sealed class RecordingEffect : BurstEffect
        {
            public int Count;
            public BurstContext Last;

            public override void Execute(in BurstContext context)
            {
                Count++;
                Last = context;
            }
        }

        private BurstDefinition definition;
        private RecordingEffect effect;

        [SetUp]
        public void SetUp()
        {
            effect = ScriptableObject.CreateInstance<RecordingEffect>();
            definition = ScriptableObject.CreateInstance<BurstDefinition>();
            definition.effects = new List<BurstEffect> { effect };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(definition);
            Object.DestroyImmediate(effect);
        }

        private static ProjectileHit Hit(float speed, Character target = null)
        {
            return new ProjectileHit(null, target, Vector2.zero, Vector2.up, 0f, speed);
        }

        [Test]
        public void Timer_TriggersAtDelayAndCooldown()
        {
            definition.trigger = BurstTriggerType.OnTimer;
            definition.delay = 0.2f;
            definition.cooldown = 0.5f;
            definition.maxTriggers = 3;

            var runtime = new BurstRuntime(definition);
            runtime.Tick(0.1f, null);
            Assert.AreEqual(0, effect.Count);

            runtime.Tick(0.15f, null);
            Assert.AreEqual(1, effect.Count);
            Assert.AreEqual(1, runtime.TriggerCount);

            runtime.Tick(0.4f, null);
            Assert.AreEqual(1, effect.Count, "冷却未到不应再次触发");

            runtime.Tick(0.2f, null);
            Assert.AreEqual(2, effect.Count);

            runtime.Tick(0.5f, null);
            Assert.AreEqual(3, effect.Count);

            runtime.Tick(1f, null);
            Assert.AreEqual(3, effect.Count, "达到 maxTriggers 后停止");
        }

        [Test]
        public void Impact_CooldownBlocksThenAllows()
        {
            definition.trigger = BurstTriggerType.OnImpact;
            definition.cooldown = 0.5f;
            definition.maxTriggers = 2;

            var runtime = new BurstRuntime(definition);
            Assert.IsTrue(runtime.TryImpact(null, Hit(10f), false));
            Assert.IsFalse(runtime.TryImpact(null, Hit(10f), false));

            runtime.Tick(0.5f, null);
            Assert.IsTrue(runtime.TryImpact(null, Hit(10f), false));
            Assert.AreEqual(2, effect.Count);
            Assert.IsTrue(runtime.TriggerCount >= 2);
        }

        [Test]
        public void Impact_FilterAndMinSpeed()
        {
            definition.trigger = BurstTriggerType.OnImpact;
            definition.impactFilter = BurstImpactFilter.Terrain;
            definition.minImpactSpeed = 5f;

            var runtime = new BurstRuntime(definition);
            Assert.IsFalse(runtime.TryImpact(null, Hit(10f), false), "非地形不应触发");
            Assert.IsTrue(runtime.TryImpact(null, Hit(10f), true), "地形应触发");

            var runtime2 = new BurstRuntime(definition);
            Assert.IsFalse(runtime2.TryImpact(null, Hit(2f), true), "低速不应触发");
        }

        [Test]
        public void LaunchAndExpire_OnlyOnce()
        {
            definition.trigger = BurstTriggerType.OnExpire;
            definition.maxTriggers = 5;
            var runtime = new BurstRuntime(definition);

            Assert.IsTrue(runtime.TryTrigger(null, BurstTriggerType.OnExpire, Vector2.zero,
                                             Vector2.zero, default, false));
            Assert.IsFalse(runtime.TryTrigger(null, BurstTriggerType.OnExpire, Vector2.zero,
                                              Vector2.zero, default, false));
            Assert.AreEqual(1, effect.Count);
        }

        [Test]
        public void ResetForLaunch_ClearsCountAndCooldown()
        {
            definition.trigger = BurstTriggerType.OnTimer;
            definition.delay = 0f;
            definition.cooldown = 0.5f;
            definition.maxTriggers = 3;

            var runtime = new BurstRuntime(definition);
            runtime.Tick(0.01f, null);
            Assert.AreEqual(1, runtime.TriggerCount);

            runtime.ResetForLaunch();
            Assert.AreEqual(0, runtime.TriggerCount);
            Assert.AreEqual(0f, runtime.CooldownRemaining);
        }

        [Test]
        public void Trigger_RaisesAnimationInterfaceEvent()
        {
            definition.trigger = BurstTriggerType.OnLaunch;
            int triggered = 0;
            BurstContext received = default;

            void OnBurst(Projectile p, BurstDefinition d, BurstContext c)
            {
                triggered++;
                received = c;
            }

            GameEvents.BurstTriggered += OnBurst;
            try
            {
                var runtime = new BurstRuntime(definition);
                Assert.IsTrue(runtime.TryTrigger(null, BurstTriggerType.OnLaunch,
                                                  Vector2.one, Vector2.up, default, false));
            }
            finally
            {
                GameEvents.BurstTriggered -= OnBurst;
            }

            Assert.AreEqual(1, triggered);
            Assert.AreEqual(1, received.TriggerCount);
            Assert.AreEqual(Vector2.one, received.Point);
        }
    }
}
