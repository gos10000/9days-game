using UnityEngine;

namespace NineDays
{
    /// <summary>
    /// 破坏力 → 范围的可调配置。默认线性；需要曲线时勾选 useCurve。
    /// 场景模块只负责使用最终 Radius，不关心调用方是子弹、爆发还是道具。
    /// </summary>
    [CreateAssetMenu(menuName = "9Days/场景/破坏范围配置", fileName = "TerrainBreakProfile")]
    public class TerrainBreakProfile : ScriptableObject
    {
        [Header("范围")]
        [Tooltip("基础范围（世界单位）")]
        [Min(0f)] public float baseRadius = 0.5f;

        [Tooltip("每点破坏力增加的范围")]
        public float radiusPerPower = 0.25f;

        [Tooltip("最小范围")]
        [Min(0f)] public float minRadius = 0.25f;

        [Tooltip("最大范围，防止一次抹掉整个场地")]
        [Min(0f)] public float maxRadius = 3f;

        [Header("曲线（可选）")]
        [Tooltip("横轴 = 破坏力，纵轴 = 范围；关闭时用线性公式")]
        public bool useCurve;

        public AnimationCurve radiusByPower = AnimationCurve.Linear(0f, 0.5f, 10f, 3f);

        /// <summary>根据破坏力算出最终范围，并夹在 min/max 之间。</summary>
        public float EvaluateRadius(float breakPower)
        {
            float power = Mathf.Max(0f, breakPower);
            float radius = useCurve && radiusByPower != null
                ? radiusByPower.Evaluate(power)
                : baseRadius + power * radiusPerPower;

            return Mathf.Clamp(radius, minRadius, maxRadius);
        }

        /// <summary>用当前 Profile 构造一个圆形破坏请求。</summary>
        public TerrainBreakRequest CreateRequest(float breakPower, Vector2 point, Vector2 normal,
                                                 GameObject source, TerrainBreakKind kind)
        {
            float radius = EvaluateRadius(breakPower);
            return new TerrainBreakRequest(breakPower, radius, TerrainBreakShape.Circle,
                                           new Vector2(radius * 2f, radius * 2f),
                                           point, normal, source, kind);
        }

        private void OnValidate()
        {
            baseRadius = Mathf.Max(0f, baseRadius);
            minRadius = Mathf.Max(0f, minRadius);
            maxRadius = Mathf.Max(minRadius, maxRadius);
        }
    }
}
