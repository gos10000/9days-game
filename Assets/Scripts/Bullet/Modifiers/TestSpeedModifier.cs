using UnityEngine;

namespace NineDays
{
    /// <summary>
    /// 测试用修改器：把发射速度乘一个倍率，并在 Console 打印结果。
    /// 用来验证「免疫道具」的子弹（neutralize gravity ammo）是否真的跳过了修改器。
    /// </summary>
    [CreateAssetMenu(menuName = "9Days/子弹/修改器/测试加速", fileName = "TestSpeedModifier")]
    public sealed class TestSpeedModifier : ProjectileModifier
    {
        [Tooltip("速度倍率；2 = 翻倍")]
        [SerializeField, Min(0f)] private float multiplier = 2f;

        public override void Modify(ProjectileStats stats, ProjectileBuildContext context)
        {
            if (stats == null) return;

            stats.speed *= Mathf.Max(0f, multiplier);
            Debug.Log($"[TestSpeedModifier] 速度 → {stats.speed:0.##}（子弹 {context?.Config?.name ?? "?"}）");
        }
    }
}
