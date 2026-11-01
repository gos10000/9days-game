using UnityEngine;

namespace NineDays
{
    /// <summary>
    /// 子弹数值配置（配置管理模块）。不同子弹复用同一个 Projectile 脚本，差在配置。
    /// </summary>
    [CreateAssetMenu(menuName = "9Days/配置/子弹配置", fileName = "ProjectileConfig")]
    public class ProjectileConfig : ScriptableObject
    {
        [Header("物理")]
        [Tooltip("质量")]
        public float mass = 0.5f;

        [Tooltip("圆形碰撞体半径")]
        public float radius = 0.25f;

        [Header("发射")]
        [Tooltip("HUD / 测试发射使用的初速")]
        public float initialSpeed = 15f;

        [Tooltip("存活时间，超时自动销毁")]
        public float lifeTime = 5f;

        [Tooltip("出膛后忽略发射者碰撞的时间")]
        public float ignoreOwnerTime = 0.15f;

        [Header("命中")]
        [Tooltip("伤害值（本轮只透传，不扣血）")]
        public float damage = 10f;

        [Tooltip("命中后是否销毁")]
        public bool destroyOnHit = true;

        [Tooltip("是否打自己人")]
        public bool friendlyFire = false;
    }
}
