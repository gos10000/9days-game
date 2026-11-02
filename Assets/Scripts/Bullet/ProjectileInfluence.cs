using UnityEngine;

namespace NineDays
{
    /// <summary>每个物理帧累加的飞行影响；采用质量无关的加速度语义。</summary>
    public struct ProjectileInfluence
    {
        public Vector2 Acceleration;
        public float VelocityScale;
        public Vector2? VelocityOverride;

        /// <summary>空影响：不改速度。</summary>
        public static ProjectileInfluence None => new ProjectileInfluence { VelocityScale = 1f };

        public void Reset()
        {
            Acceleration = Vector2.zero;
            VelocityScale = 1f;
            VelocityOverride = null;
        }

        public void Add(in ProjectileInfluence other)
        {
            Acceleration += other.Acceleration;
            VelocityScale *= other.VelocityScale;
            if (other.VelocityOverride.HasValue) VelocityOverride = other.VelocityOverride;
        }
    }

    /// <summary>纯 C# 积分：EditMode 可直接测。</summary>
    public static class ProjectileInfluenceSolver
    {
        public static Vector2 Integrate(Vector2 velocity, in ProjectileInfluence influence, float deltaTime)
        {
            if (influence.VelocityOverride.HasValue) return influence.VelocityOverride.Value;

            Vector2 result = velocity + influence.Acceleration * deltaTime;
            return result * Mathf.Max(0f, influence.VelocityScale);
        }
    }
}
