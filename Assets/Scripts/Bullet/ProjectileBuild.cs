using System.Collections.Generic;
using UnityEngine;

namespace NineDays
{
    /// <summary>发射构建上下文：修改器可以读发射者 / 阵营，也可以追加特性。</summary>
    public sealed class ProjectileBuildContext
    {
        public Character Shooter { get; }
        public Team Team { get; }
        public ProjectileConfig Config { get; }
        public List<ProjectileTrait> Traits { get; }
        public List<BurstDefinition> Bursts { get; }

        public ProjectileBuildContext(Character shooter, ProjectileConfig config, List<ProjectileTrait> traits,
                                      List<BurstDefinition> bursts = null)
        {
            Shooter = shooter;
            Team = shooter != null ? shooter.Team : NineDays.Team.None;
            Config = config;
            Traits = traits ?? new List<ProjectileTrait>();
            Bursts = bursts ?? new List<BurstDefinition>();
        }
    }

    /// <summary>一发子弹的最终构建结果（运行时数值 + 特性 + 爆发）。</summary>
    public sealed class ProjectileLoadout
    {
        public ProjectileStats Stats { get; }
        public IReadOnlyList<ProjectileTrait> Traits { get; }
        public IReadOnlyList<BurstDefinition> Bursts { get; }

        public ProjectileLoadout(ProjectileStats stats, IReadOnlyList<ProjectileTrait> traits,
                                 IReadOnlyList<BurstDefinition> bursts = null)
        {
            Stats = stats;
            Traits = traits;
            Bursts = bursts;
        }
    }

    /// <summary>
    /// 纯函数构建：配置资产 → 特性默认值 → 修改器。
    /// 顺序固定，EditMode 可直接测；不修改任何 ScriptableObject 资产。
    /// </summary>
    public static class ProjectileBuild
    {
        public static ProjectileLoadout Build(ProjectileConfig config,
                                              IReadOnlyList<ProjectileModifier> modifiers,
                                              Character shooter,
                                              float speedOverride = -1f)
        {
            var stats = ProjectileStats.From(config);
            if (speedOverride > 0f) stats.speed = speedOverride;

            var traits = new List<ProjectileTrait>();
            if (config != null && config.traits != null)
            {
                for (int i = 0; i < config.traits.Count; i++)
                    if (config.traits[i] != null) traits.Add(config.traits[i]);
            }

            var bursts = new List<BurstDefinition>();
            if (config != null && config.bursts != null)
            {
                for (int i = 0; i < config.bursts.Count; i++)
                    if (config.bursts[i] != null) bursts.Add(config.bursts[i]);
            }

            var context = new ProjectileBuildContext(shooter, config, traits, bursts);

            int baseTraitCount = traits.Count;
            for (int i = 0; i < baseTraitCount; i++) traits[i].ApplyStats(stats);

            // 免疫道具的子弹在建档时就跳过修改器，连特性追加也不给
            bool ignoreModifiers = config != null && config.ignoreModifiers;

            if (!ignoreModifiers && modifiers != null)
            {
                for (int i = 0; i < modifiers.Count; i++)
                    if (modifiers[i] != null) modifiers[i].Modify(stats, context);
            }

            // 修改器追加的特性在修改器之后补算数值，保证“道具加特性”也能生效
            for (int i = baseTraitCount; i < traits.Count; i++)
                if (traits[i] != null) traits[i].ApplyStats(stats);

            stats.Validate();
            stats.pierceCount = Mathf.Max(0, stats.pierceCount);
            stats.pierceRemaining = stats.pierceCount;

            return new ProjectileLoadout(stats, traits, bursts);
        }
    }
}
