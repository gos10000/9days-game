using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace NineDays.Tests
{
    /// <summary>发射构建中的爆发列表与定义校验。</summary>
    public class BurstBuildTests
    {
        private sealed class DummyEffect : BurstEffect
        {
            public override void Execute(in BurstContext context) { }
        }

        private sealed class AddBurstModifier : ProjectileModifier
        {
            public BurstDefinition burst;
            public override void Modify(ProjectileStats stats, ProjectileBuildContext context) => context.Bursts.Add(burst);
        }

        private ProjectileConfig config;
        private BurstDefinition definition;
        private DummyEffect effect;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<ProjectileConfig>();
            config.bursts = new List<BurstDefinition>();
            config.traits = new List<ProjectileTrait>();
            effect = ScriptableObject.CreateInstance<DummyEffect>();
            definition = ScriptableObject.CreateInstance<BurstDefinition>();
            definition.effects = new List<BurstEffect> { effect };
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(config);
            Object.DestroyImmediate(definition);
            Object.DestroyImmediate(effect);
        }

        [Test]
        public void Build_CopiesConfigBursts()
        {
            config.bursts.Add(definition);

            ProjectileLoadout loadout = ProjectileBuild.Build(config, null, null);

            Assert.AreEqual(1, loadout.Bursts.Count);
            Assert.AreSame(definition, loadout.Bursts[0]);
        }

        [Test]
        public void Build_ModifierCanAddBurst()
        {
            var modifier = ScriptableObject.CreateInstance<AddBurstModifier>();
            modifier.burst = definition;
            var modifiers = new List<ProjectileModifier> { modifier };

            ProjectileLoadout loadout = ProjectileBuild.Build(config, modifiers, null);

            Assert.AreEqual(1, loadout.Bursts.Count);
            Object.DestroyImmediate(modifier);
        }

        [Test]
        public void Validate_DestroyOnTriggerClampsToOne()
        {
            definition.maxTriggers = 4;
            definition.destroyOnTrigger = true;

            definition.Validate();

            Assert.AreEqual(1, definition.maxTriggers);
        }
    }
}
