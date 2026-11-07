using UnityEngine;

namespace NineDays
{
    /// <summary>
    /// 方向性重力场：让区域内的子弹沿固定方向加速。
    /// 加重场 direction = (0,-1)，反转场 direction = (0,+1)；
    /// 与 RadialGravityField（径向）互补，视觉方向必须和这里的 direction 一致。
    /// </summary>
    public sealed class DirectionalGravityField : ProjectileField
    {
        [Header("重力")]
        [Tooltip("加速度方向：加重场填 (0, -1)，反转场填 (0, 1)")]
        [SerializeField] private Vector2 direction = Vector2.down;

        [Tooltip("每秒增加的加速度（像素/秒²）")]
        [SerializeField] private float acceleration = 30f;

        [Tooltip("单个场能提供的加速度上限，避免多个场叠加时数值爆炸")]
        [SerializeField] private float maxAcceleration = 120f;

        public override void Accumulate(Projectile projectile, ref ProjectileInfluence influence, float deltaTime)
        {
            influence.Acceleration += ComputeAcceleration(direction, acceleration, maxAcceleration);
        }

        /// <summary>纯函数：便于 EditMode 测试。</summary>
        public static Vector2 ComputeAcceleration(Vector2 direction, float acceleration, float maxAcceleration)
        {
            if (acceleration <= 0f) return Vector2.zero;

            Vector2 dir = direction.sqrMagnitude < 0.0001f ? Vector2.down : direction.normalized;
            return dir * Mathf.Min(acceleration, Mathf.Max(0f, maxAcceleration));
        }
    }
}
