using UnityEngine;

namespace NineDays
{
    /// <summary>
    /// 牵引区域：命中点生成一块持续存在的区域，把范围内角色吸向中心，
    /// 期间周期性造成伤害，结束时再触发一次普通爆炸（finalBurst）。
    /// 区域是独立 GameObject，子弹回收后仍然存活到持续时间结束。
    /// </summary>
    [CreateAssetMenu(menuName = "9Days/子弹/爆发效果/牵引区域", fileName = "TractionEffect")]
    public sealed class TractionEffect : BurstEffect
    {
        [Header("牵引")]
        [Tooltip("牵引范围（世界单位）；应大于强化爆炸的 3")]
        [Min(0f)] public float radius = 4f;

        [Tooltip("区域持续时间（秒）")]
        [Min(0.01f)] public float duration = 2f;

        [Tooltip("每秒朝中心施加的加速度")]
        [Min(0f)] public float pullAcceleration = 25f;

        public LayerMask targetMask = ~0;

        [Header("周期伤害")]
        [Tooltip("每隔多久结算一次")]
        [Min(0.01f)] public float tickInterval = 0.5f;

        [Tooltip("每次结算的伤害")]
        [Min(0f)] public float damagePerTick = 2.5f;

        [Header("结束时")]
        [Tooltip("持续时间结束后触发的爆发定义；留空则只结束不爆炸")]
        public BurstDefinition finalBurst;

        [Header("过滤")]
        public bool hitAllies;
        public bool hitOwner;

        [Header("表现")]
        [Tooltip("收缩圆环的颜色")]
        public Color ringColor = new Color(0.35f, 0.95f, 0.85f, 1f);

        [Tooltip("同时存在的圆环数量；越多越密")]
        [Min(1)] public int ringCount = 4;

        [Tooltip("单个圆环从外圈收缩到中心的时长")]
        [Min(0.05f)] public float ringLifetime = 0.9f;

        [Tooltip("圆环线宽（世界单位）")]
        [Min(0f)] public float ringWidth = 0.07f;

        public override void Execute(in BurstContext context)
        {
            TractionZone.Create(context.Point, this, context.Owner, context.Team);
        }

        public override void Validate()
        {
            radius = Mathf.Max(0f, radius);
            duration = Mathf.Max(0.01f, duration);
            pullAcceleration = Mathf.Max(0f, pullAcceleration);
            tickInterval = Mathf.Max(0.01f, tickInterval);
            damagePerTick = Mathf.Max(0f, damagePerTick);
        }

        private void OnValidate() => Validate();
    }
}
