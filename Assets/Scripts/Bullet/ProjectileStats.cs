using UnityEngine;

namespace NineDays
{
    /// <summary>
    /// 每发子弹的运行时数值副本：配置资产只读，特性与道具都只改这里。
    /// </summary>
    public sealed class ProjectileStats
    {
        public float mass = 0.5f;
        public float radius = 0.25f;
        public float speed = 15f;
        public float lifeTime = 5f;
        public float ignoreOwnerTime = 0.15f;
        public float damage = 10f;
        public bool destroyOnHit = true;
        public bool friendlyFire;

        /// <summary>免疫场上所有道具场（ProjectileField）的作用。</summary>
        public bool ignoreFields;

        public float gravityScale = 1f;
        public float linearDamping;
        public int pierceCount;
        public int pierceRemaining;

        /// <summary>从配置资产拷贝一份运行时数值；配置为空时返回默认值。</summary>
        public static ProjectileStats From(ProjectileConfig config)
        {
            var stats = new ProjectileStats();
            if (config == null) return stats;

            stats.mass = config.mass;
            stats.radius = config.radius;
            stats.speed = config.initialSpeed;
            stats.lifeTime = config.lifeTime;
            stats.ignoreOwnerTime = config.ignoreOwnerTime;
            stats.damage = config.damage;
            stats.destroyOnHit = config.destroyOnHit;
            stats.friendlyFire = config.friendlyFire;
            stats.ignoreFields = config.ignoreFields;
            stats.gravityScale = config.gravityScale;
            stats.linearDamping = config.linearDamping;
            return stats;
        }

        /// <summary>把数值钳制到合法范围（寿命不能为 0，半径不能为负）。</summary>
        public void Validate()
        {
            mass = Mathf.Max(0.01f, mass);
            radius = Mathf.Max(0.01f, radius);
            speed = Mathf.Max(0f, speed);
            lifeTime = Mathf.Max(0.01f, lifeTime);
            ignoreOwnerTime = Mathf.Max(0f, ignoreOwnerTime);
            damage = Mathf.Max(0f, damage);
            // 允许负值：反重力弹药（AntigravityTrait）用它把重力方向翻转为向上
            if (float.IsNaN(gravityScale)) gravityScale = 0f;
            linearDamping = Mathf.Max(0f, linearDamping);
            pierceCount = Mathf.Max(0, pierceCount);
            pierceRemaining = Mathf.Clamp(pierceRemaining, 0, pierceCount);
        }

        public ProjectileStats Clone() => (ProjectileStats)MemberwiseClone();
    }
}
