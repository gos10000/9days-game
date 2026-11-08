using System;
using System.Collections.Generic;
using UnityEngine;

namespace NineDays
{
    /// <summary>
    /// 子弹抽取表：一列图标、一列子弹预制体，填什么就能抽到什么。
    /// 图标留空时会自动取 prefab 上的 SpriteRenderer，不至于出现空按钮。
    /// </summary>
    [CreateAssetMenu(menuName = "9Days/配置/子弹抽取表", fileName = "AmmoTable")]
    public class AmmoTable : ScriptableObject
    {
        [Serializable]
        public class Row
        {
            [Tooltip("选择页显示的图标；留空时自动取 prefab 上的精灵")]
            public Sprite icon;

            [Tooltip("子弹预制体（挂 Projectile 的那个）")]
            public GameObject prefab;

            [Tooltip("显示名；留空时用 prefab 名字")]
            public string displayName;

            [Tooltip("选择页的介绍文字")]
            [TextArea(2, 4)]
            public string description;

            public string ResolvedName
            {
                get
                {
                    if (!string.IsNullOrWhiteSpace(displayName)) return displayName;
                    return prefab != null ? prefab.name : "Unnamed";
                }
            }

            public Sprite ResolvedIcon
            {
                get
                {
                    if (icon != null) return icon;
                    if (prefab == null) return null;
                    SpriteRenderer renderer = prefab.GetComponentInChildren<SpriteRenderer>(true);
                    return renderer != null ? renderer.sprite : null;
                }
            }

            public Projectile ResolvedProjectile =>
                prefab != null ? prefab.GetComponent<Projectile>() : null;

            public bool IsValid => ResolvedProjectile != null;
        }

        [SerializeField] private List<Row> rows = new List<Row>();

        public IReadOnlyList<Row> Rows => rows;

        /// <summary>只挑能用的行（预制体上有 Projectile），避免抽到空项。</summary>
        public List<Row> ValidRows()
        {
            var result = new List<Row>();
            if (rows == null) return result;

            for (int i = 0; i < rows.Count; i++)
            {
                Row row = rows[i];
                if (row == null) continue;
                if (!row.IsValid)
                {
                    if (row.prefab != null)
                        Debug.LogWarning($"[AmmoTable] {row.prefab.name} 上没有 Projectile 组件，已跳过", this);
                    continue;
                }

                result.Add(row);
            }

            return result;
        }
    }
}
