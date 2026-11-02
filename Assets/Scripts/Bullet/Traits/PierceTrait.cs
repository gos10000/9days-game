using UnityEngine;

namespace NineDays
{
    /// <summary>穿透：命中后继续飞，直到用完额外穿透次数。</summary>
    [CreateAssetMenu(menuName = "9Days/子弹/特性/穿透", fileName = "PierceTrait")]
    public sealed class PierceTrait : ProjectileTrait
    {
        [Tooltip("额外可穿透的目标数量")]
        [SerializeField, Min(0)] private int extraPierce = 1;

        public override void ApplyStats(ProjectileStats stats)
        {
            if (stats == null) return;
            stats.pierceCount += extraPierce;
        }

        public override HitDisposition? OnHit(Projectile projectile, in ProjectileHit hit)
        {
            if (projectile == null || projectile.Stats == null) return HitDisposition.Consume;

            if (projectile.Stats.pierceRemaining <= 0) return HitDisposition.Consume;

            projectile.Stats.pierceRemaining--;
            return HitDisposition.Pass;
        }
    }
}
