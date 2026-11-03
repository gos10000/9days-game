using System.Collections.Generic;
using UnityEngine;

namespace NineDays
{
    /// <summary>
    /// 一次爆发的配置：触发条件 + 冷却/次数 + 效果列表。
    /// 资产可共享；每次触发状态在 BurstRuntime 里，不写回 SO。
    /// </summary>
    [CreateAssetMenu(menuName = "9Days/子弹/爆发定义", fileName = "BurstDefinition")]
    public class BurstDefinition : ScriptableObject
    {
        [Header("触发")]
        public string burstId = "Burst";

        public BurstTriggerType trigger = BurstTriggerType.OnImpact;

        [Tooltip("首次触发延迟（秒）；仅 OnTimer 使用")]
        [Min(0f)] public float delay;

        [Tooltip("两次成功触发之间的最小间隔（秒）")]
        [Min(0f)] public float cooldown = 0.5f;

        [Tooltip("最大触发次数；0 = 无限。OnLaunch/OnExpire 实际只触发一次")]
        [Min(0)] public int maxTriggers = 1;

        [Tooltip("OnImpact 允许响应的目标类型")]
        public BurstImpactFilter impactFilter = BurstImpactFilter.Any;

        [Tooltip("OnImpact 的最小碰撞速度；0 = 不限制")]
        [Min(0f)] public float minImpactSpeed;

        [Tooltip("效果执行完后是否回收子弹")]
        public bool destroyOnTrigger;

        [Header("表现（以后接）")]
        [Tooltip("可选的表现 prefab；当前逻辑不使用，动画层以后订阅 BurstTriggered 后自行实例化")]
        public GameObject viewPrefab;

        [Header("效果")]
        [Tooltip("按列表顺序执行")]
        public List<BurstEffect> effects = new List<BurstEffect>();

        /// <summary>是否还有触发次数。</summary>
        public bool HasTriggersLeft(int triggerCount)
        {
            return maxTriggers <= 0 || triggerCount < maxTriggers;
        }

        /// <summary>碰撞目标是否通过 OnImpact 过滤。</summary>
        public bool MatchesImpact(in ProjectileHit hit, bool hitTerrain)
        {
            if (impactFilter == BurstImpactFilter.None) return false;
            if (hit.Speed < minImpactSpeed) return false;

            BurstImpactFilter actual = BurstImpactFilter.Other;
            if (hit.Character != null) actual = BurstImpactFilter.Character;
            else if (hitTerrain) actual = BurstImpactFilter.Terrain;

            return (impactFilter & actual) != 0;
        }

        /// <summary>把配置夹到可用范围；不修改效果资产。</summary>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(burstId)) burstId = "Burst";
            delay = Mathf.Max(0f, delay);
            cooldown = Mathf.Max(0f, cooldown);
            maxTriggers = Mathf.Max(0, maxTriggers);
            minImpactSpeed = Mathf.Max(0f, minImpactSpeed);

            if (destroyOnTrigger) maxTriggers = 1;
            if (effects == null) effects = new List<BurstEffect>();
        }

        private void OnValidate() => Validate();
    }
}
