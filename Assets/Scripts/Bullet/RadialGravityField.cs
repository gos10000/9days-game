using UnityEngine;

namespace NineDays
{
    /// <summary>点状引力场：把区域内的子弹拉向中心；负强度则推离。</summary>
    public sealed class RadialGravityField : ProjectileField
    {
        [Header("引力")]
        [Tooltip("正数吸引、负数排斥")]
        [SerializeField] private float strength = 30f;

        [Tooltip("作用半径（本地单位，实际半径会乘上物体缩放）")]
        [SerializeField] private float radius = 4f;

        [Tooltip("单颗子弹每帧受到的加速度上限")]
        [SerializeField] private float maxAcceleration = 60f;

        [Tooltip("距离小于该值时按该值计算衰减，避免中心处数值爆炸")]
        [SerializeField] private float minDistance = 0.5f;

        public override void Accumulate(Projectile projectile, ref ProjectileInfluence influence, float deltaTime)
        {
            // 作用半径要跟着物体缩放走，否则「看起来这么大、实际只吸中心一小圈」：
            // 预制体的碰撞体是用本地尺寸 × Transform 缩放得到的，这里必须一致。
            float scale = Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y));
            float worldRadius = radius * Mathf.Max(0.0001f, scale);

            Vector2 acceleration = ComputeAcceleration(
                projectile.transform.position, transform.position,
                strength, worldRadius, maxAcceleration, minDistance);

            influence.Acceleration += acceleration;
        }

        /// <summary>纯函数：便于 EditMode 测试与编辑器预览。</summary>
        public static Vector2 ComputeAcceleration(Vector2 position, Vector2 center, float strength,
                                                  float radius, float maxAcceleration, float minDistance)
        {
            if (radius <= 0f || Mathf.Approximately(strength, 0f)) return Vector2.zero;

            Vector2 toCenter = center - position;
            float distance = toCenter.magnitude;
            if (distance > radius || distance < 0.0001f) return Vector2.zero;

            float safeDistance = Mathf.Max(distance, Mathf.Max(0.01f, minDistance));
            float falloff = Mathf.Clamp01(1f - safeDistance / radius);
            Vector2 acceleration = toCenter / distance * (strength * falloff);

            return Vector2.ClampMagnitude(acceleration, Mathf.Max(0f, maxAcceleration));
        }
    }
}
