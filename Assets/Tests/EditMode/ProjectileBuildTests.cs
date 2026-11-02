using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace NineDays.Tests
{
    /// <summary>发射构建：配置 → 特性 → 修改器，以及命中决策。</summary>
    public class ProjectileBuildTests
    {
        private ProjectileConfig config;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<ProjectileConfig>();
            config.mass = 0.5f;
            config.radius = 0.25f;
            config.initialSpeed = 15f;
            config.lifeTime = 5f;
            config.ignoreOwnerTime = 0.15f;
            config.damage = 10f;
            config.destroyOnHit = true;
            config.gravityScale = 1f;
            config.traits = new List<ProjectileTrait>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(config);
        }

        /// <summary>测试用特性：给伤害加固定值。</summary>
        public sealed class FlatDamageTrait : ProjectileTrait
        {
            public float bonus = 3f;
            public override void ApplyStats(ProjectileStats stats) => stats.damage += bonus;
        }

        /// <summary>测试用修改器：给伤害加固定值。</summary>
        public sealed class FlatDamageModifier : ProjectileModifier
        {
            public float bonus = 5f;
            public override void Modify(ProjectileStats stats, ProjectileBuildContext context) => stats.damage += bonus;
        }

        /// <summary>测试用修改器：追加一个特性。</summary>
        public sealed class AddTraitModifier : ProjectileModifier
        {
            public ProjectileTrait trait;
            public override void Modify(ProjectileStats stats, ProjectileBuildContext context) => context.Traits.Add(trait);
        }

        [Test]
        public void From_CopiesConfigValues()
        {
            ProjectileStats stats = ProjectileStats.From(config);

            Assert.AreEqual(0.5f, stats.mass, 0.001f);
            Assert.AreEqual(15f, stats.speed, 0.001f);
            Assert.AreEqual(1f, stats.gravityScale, 0.001f);
            Assert.IsTrue(stats.destroyOnHit);
        }

        [Test]
        public void Validate_ClampsNegativeValues()
        {
            var stats = new ProjectileStats
            {
                mass = -1f,
                radius = 0f,
                speed = -5f,
                lifeTime = 0f,
                damage = -3f,
                gravityScale = -2f,
                linearDamping = -1f,
                pierceCount = -4
            };

            stats.Validate();

            Assert.Greater(stats.mass, 0f);
            Assert.Greater(stats.radius, 0f);
            Assert.Greater(stats.lifeTime, 0f);
            Assert.AreEqual(0f, stats.speed, 0.001f);
            Assert.AreEqual(0f, stats.damage, 0.001f);
            Assert.AreEqual(0f, stats.gravityScale, 0.001f);
            Assert.AreEqual(0, stats.pierceCount);
        }

        [Test]
        public void Build_AppliesSpeedOverride()
        {
            ProjectileLoadout loadout = ProjectileBuild.Build(config, null, null, 22f);
            Assert.AreEqual(22f, loadout.Stats.speed, 0.001f);
        }

        [Test]
        public void Build_AppliesTraitStats()
        {
            config.traits.Add(ScriptableObject.CreateInstance<FlatDamageTrait>());

            ProjectileLoadout loadout = ProjectileBuild.Build(config, null, null);

            Assert.AreEqual(13f, loadout.Stats.damage, 0.001f);
            Assert.AreEqual(1, loadout.Traits.Count);
        }

        [Test]
        public void Build_AppliesModifierAfterTrait()
        {
            config.traits.Add(ScriptableObject.CreateInstance<FlatDamageTrait>());
            var modifiers = new List<ProjectileModifier> { ScriptableObject.CreateInstance<FlatDamageModifier>() };

            ProjectileLoadout loadout = ProjectileBuild.Build(config, modifiers, null);

            Assert.AreEqual(18f, loadout.Stats.damage, 0.001f);
        }

        [Test]
        public void Build_TraitAddedByModifier_StillContributes()
        {
            var trait = ScriptableObject.CreateInstance<FlatDamageTrait>();
            var modifier = ScriptableObject.CreateInstance<AddTraitModifier>();
            modifier.trait = trait;
            var modifiers = new List<ProjectileModifier> { modifier };

            ProjectileLoadout loadout = ProjectileBuild.Build(config, modifiers, null);

            Assert.AreEqual(1, loadout.Traits.Count);
            Assert.AreEqual(13f, loadout.Stats.damage, 0.001f);
        }

        [Test]
        public void Build_PierceTrait_SetsRemaining()
        {
            config.traits.Add(ScriptableObject.CreateInstance<PierceTrait>());

            ProjectileLoadout loadout = ProjectileBuild.Build(config, null, null);

            Assert.AreEqual(1, loadout.Stats.pierceCount);
            Assert.AreEqual(1, loadout.Stats.pierceRemaining);
        }

        [Test]
        public void HitResolver_NoVotes_UsesDestroyOnHit()
        {
            var hit = new ProjectileHit(null, null, Vector2.zero, Vector2.up, 0f, 0f);

            Assert.AreEqual(HitDisposition.Consume, ProjectileHitResolver.Resolve(true, null, null, hit));
            Assert.AreEqual(HitDisposition.Pass, ProjectileHitResolver.Resolve(false, null, null, hit));
        }

        [Test]
        public void HitResolver_ConsumeWinsOverPass()
        {
            Assert.AreEqual(HitDisposition.Consume, ProjectileHitResolver.Combine(HitDisposition.Pass, HitDisposition.Consume));
            Assert.AreEqual(HitDisposition.Pass, ProjectileHitResolver.Combine(HitDisposition.Ignore, HitDisposition.Pass));
            Assert.AreEqual(HitDisposition.Ignore, ProjectileHitResolver.Combine(HitDisposition.Ignore, HitDisposition.Ignore));
        }
    }
}
