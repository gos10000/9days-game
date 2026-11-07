using UnityEngine;

namespace NineDays
{
    /// <summary>
    /// 伤害提升：在基础配置的伤害上乘一个倍率。
    /// 用乘法而不是写死数值，配置里的 damage 改动时倍率关系仍然成立。
    /// </summary>
    [CreateAssetMenu(menuName = "9Days/子弹/特性/伤害提升", fileName = "DamageBoostTrait")]
    public sealed class DamageBoostTrait : ProjectileTrait
    {
        [Tooltip("伤害倍率；2 = 基础伤害的两倍")]
        [SerializeField, Min(0f)] private float damageMultiplier = 2f;

        public override void ApplyStats(ProjectileStats stats)
        {
            if (stats == null) return;
            stats.damage *= Mathf.Max(0f, damageMultiplier);
        }
    }
}
