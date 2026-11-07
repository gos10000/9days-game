using UnityEngine;

namespace NineDays
{
    /// <summary>
    /// 一种可选子弹的数据体：prefab、名称、介绍、图标。
    /// 数值仍由 prefab 上的 ProjectileConfig 决定。
    /// </summary>
    [CreateAssetMenu(menuName = "9Days/配置/弹药定义", fileName = "AmmoDefinition")]
    public class AmmoDefinition : ScriptableObject
    {
        [Header("子弹")]
        [Tooltip("子弹预制体；数值由 prefab 上的 ProjectileConfig 决定")]
        public Projectile prefab;

        [Header("显示")]
        [Tooltip("选弹页显示名；为空时回退 prefab 名称")]
        public string displayName;

        [Tooltip("选弹页介绍")]
        [TextArea(2, 4)]
        public string description;

        [Tooltip("选弹页图标，可空")]
        public Sprite icon;

        public string ResolvedName => string.IsNullOrWhiteSpace(displayName)
            ? (prefab != null ? prefab.name : "Unnamed")
            : displayName;

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(displayName) && prefab != null)
                displayName = prefab.name;
        }
    }
}
