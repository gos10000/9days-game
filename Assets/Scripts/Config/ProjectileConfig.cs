using System.Collections.Generic;
using UnityEngine;

namespace NineDays
{
    /// <summary>
    /// 子弹数值配置（配置管理模块）。不同子弹复用同一个 Projectile 脚本，差异在数值与特性列表。
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

        [Header("运动")]
        [Tooltip("重力倍率：1 = 抛物弹，0 = 直线弹，负数 = 反重力（重力方向朝上）")]
        public float gravityScale = 1f;

        [Tooltip("空气阻力（0 = 无阻力）")]
        public float linearDamping = 0f;

        [Header("命中")]
        [Tooltip("伤害值")]
        public float damage = 10f;

        [Tooltip("命中后是否销毁（没有特性投票时生效）")]
        public bool destroyOnHit = true;

        [Tooltip("是否打自己人")]
        public bool friendlyFire = false;

        [Header("特性")]
        [Tooltip("可组合的子弹特性，按列表顺序生效")]
        public List<ProjectileTrait> traits = new List<ProjectileTrait>();

        [Header("道具")]
        [Tooltip("勾选后忽略发射者的所有道具 / 装备 / Buff 修改器（ProjectileModifier）")]
        public bool ignoreModifiers;

        [Tooltip("勾选后免疫场上所有道具场（黑洞 / 白洞 / 重力场 / 反重力场，即 ProjectileField）的作用")]
        public bool ignoreFields;

        [Header("表现")]
        [Tooltip("撞击特效：子弹撞到任何东西时生成，所有子弹都该配")]
        public GameObject impactViewPrefab;

        [Header("爆发")]
        [Tooltip("子弹携带的爆发定义；按列表顺序触发")]
        public List<BurstDefinition> bursts = new List<BurstDefinition>();

        /// <summary>把配置钳制到合法范围，并保证列表不为空。</summary>
        public void Validate()
        {
            mass = Mathf.Max(0.01f, mass);
            radius = Mathf.Max(0.01f, radius);
            initialSpeed = Mathf.Max(0f, initialSpeed);
            lifeTime = Mathf.Max(0.01f, lifeTime);
            ignoreOwnerTime = Mathf.Max(0f, ignoreOwnerTime);
            damage = Mathf.Max(0f, damage);
            // 允许负值：反重力弹药（AntigravityTrait）用它把重力方向翻转为向上
            if (float.IsNaN(gravityScale)) gravityScale = 0f;
            linearDamping = Mathf.Max(0f, linearDamping);

            if (traits == null) traits = new List<ProjectileTrait>();
            if (bursts == null) bursts = new List<BurstDefinition>();
        }

        private void OnValidate() => Validate();
    }
}
