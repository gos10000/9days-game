using UnityEngine;

namespace NineDays
{
    /// <summary>
    /// 反重力：这颗子弹受到的重力方向朝上，其余数值沿用基础配置（Normal 弹药）。
    /// 通过把 gravityScale 取负值实现，物理由 Rigidbody2D 自己积分，不额外施力。
    /// </summary>
    [CreateAssetMenu(menuName = "9Days/子弹/特性/反重力", fileName = "AntigravityTrait")]
    public sealed class AntigravityTrait : ProjectileTrait
    {
        [Tooltip("向上的重力倍率；最终 gravityScale 取这个值的负数。0.5 = 一半重力往上")]
        [SerializeField, Min(0f)] private float upwardScale = 0.5f;

        public override void ApplyStats(ProjectileStats stats)
        {
            if (stats == null) return;

            // 用绝对值而不是 -stats.gravityScale：
            // 基础配置若是 0（直线弹），取负仍然是 0，反重力会失效
            stats.gravityScale = -Mathf.Max(0f, upwardScale);
        }
    }
}
