using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D.Animation;

namespace NineDays
{
    /// <summary>
    /// 角色数值配置（配置管理模块）。代码与 prefab 里不写魔法数字，统一放这里。
    /// </summary>
    [CreateAssetMenu(menuName = "9Days/配置/角色配置", fileName = "CharacterConfig")]
    public class CharacterConfig : ScriptableObject
    {
        /// <summary>
        /// 一套皮肤 = 一个只含表现层的 prefab + 可选控制器 / 图集。
        /// visualPrefab 里禁止挂 Rigidbody2D / Collider2D，偏移只用 visualOffset。
        /// </summary>
        [Serializable]
        public class Skin
        {
            [Tooltip("皮肤 id，便于查找")]
            public string skinId = "Default";

            [Tooltip("只含表现层的 prefab（SortingGroup + SpriteRenderer / 骨骼 + Animator）")]
            public GameObject visualPrefab;

            [Tooltip("可空：留空则使用 visualPrefab 自带的 AnimatorController")]
            public RuntimeAnimatorController controller;

            [Tooltip("可空：骨骼皮肤用它替换图集（Sprite Library）")]
            public SpriteLibraryAsset spriteLibrary;

            [Tooltip("脚底对齐偏移，换美术时不用改逻辑 prefab")]
            public Vector2 visualOffset = Vector2.zero;

            [Tooltip("排序序号，第 i 个渲染器为 sortingOrder + i")]
            public int sortingOrder = 10;

            [Tooltip("选人界面图标，可空")]
            public Sprite icon;
        }

        [Header("物理")]
        [Tooltip("质量")]
        public float mass = 1f;

        [Tooltip("胶囊碰撞体尺寸（根节点 CapsuleCollider2D）")]
        public Vector2 colliderSize = new Vector2(1f, 2f);

        [Tooltip("禁止物理体旋转：摔倒 / 翻滚只做在 Visual 上")]
        public bool freezeRotation = true;

        [Header("受击")]
        [Tooltip("小于该冲量只闪白、不击飞（N·s）")]
        public float hitThreshold = 0.5f;

        [Tooltip("击退冲量放大系数")]
        public float knockbackScale = 1f;

        [Tooltip("最小击退冲量，保证一碰就动")]
        public float minKnockback = 2f;

        [Tooltip("最大击退冲量，防止连续受击叠加爆飞")]
        public float maxKnockback = 20f;

        [Tooltip("击退后禁止操作的时间")]
        public float controlLockTime = 0.35f;

        [Header("撞击障碍")]
        [Tooltip("撞击冲量达到该值才上报 StrongImpact（N·s）")]
        public float impactThreshold = 1f;

        [Tooltip("哪些层算障碍物")]
        public LayerMask obstacleMask = 1;

        [Tooltip("同一碰撞体的上报冷却")]
        public float impactCooldown = 0.05f;

        [Header("起身")]
        [Tooltip("落地判定：竖直速度小于该值视为落地")]
        public float landedSpeed = 1f;

        [Tooltip("停稳判定速度")]
        public float settleSpeed = 0.15f;

        [Tooltip("停稳判定持续时间")]
        public float settleTime = 0.3f;

        [Tooltip("倒地停留时间")]
        public float getUpDelay = 0.5f;

        [Tooltip("起身动作时长")]
        public float getUpDuration = 0.4f;

        [Header("接地检测")]
        [Tooltip("GroundCheck 处的检测半径")]
        public float groundCheckRadius = 0.12f;

        [Header("血量")]
        [Tooltip("最大生命值（战斗轮血量制）")]
        public float maxHealth = 100f;

        [Header("出膛点避障")]
        [Tooltip("出膛点被障碍占住时，沿发射方向尝试平移的次数")]
        public int spawnNudgeAttempts = 8;

        [Tooltip("每次平移的步长（世界单位）")]
        public float spawnNudgeStep = 0.3f;

        [Header("皮肤")]
        public List<Skin> skins = new List<Skin>();

        [Tooltip("默认皮肤下标")]
        public int defaultSkinIndex;

        public Skin GetSkin(int index)
        {
            if (skins == null || skins.Count == 0) return null;
            return skins[Mathf.Clamp(index, 0, skins.Count - 1)];
        }
    }
}
