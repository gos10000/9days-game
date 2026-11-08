using System;
using System.Collections.Generic;
using UnityEngine;

namespace NineDays
{
    /// <summary>
    /// 道具抽取表：一列图标、一列道具预制体。
    /// 占地尺寸不在这里填，运行时从 prefab 的根碰撞体推导。
    /// </summary>
    [CreateAssetMenu(menuName = "9Days/配置/道具抽取表", fileName = "PropTable")]
    public class PropTable : ScriptableObject
    {
        [Serializable]
        public class Row
        {
            [Tooltip("选择页显示的图标；留空时自动取 prefab 上的精灵")]
            public Sprite icon;

            [Tooltip("道具预制体（挂 ProjectileField 的那个）")]
            public GameObject prefab;

            [Tooltip("显示名；留空时用 prefab 名字")]
            public string displayName;

            [Tooltip("选择页的介绍文字")]
            [TextArea(2, 4)]
            public string description;

            [Tooltip("抽到一次可放置几个")]
            [Min(1)]
            public int count = 1;

            public string ResolvedName
            {
                get
                {
                    if (!string.IsNullOrWhiteSpace(displayName)) return displayName;
                    return prefab != null ? prefab.name : "Unnamed";
                }
            }

            public bool IsValid => prefab != null;

            public PropSpec ToSpec() =>
                new PropSpec(prefab, icon, ResolvedName, count);
        }

        [SerializeField] private List<Row> rows = new List<Row>();

        public IReadOnlyList<Row> Rows => rows;

        public List<Row> ValidRows()
        {
            var result = new List<Row>();
            if (rows == null) return result;

            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i] == null || !rows[i].IsValid) continue;
                result.Add(rows[i]);
            }

            return result;
        }
    }
}
