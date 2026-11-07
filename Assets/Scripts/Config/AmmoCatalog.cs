using System;
using System.Collections.Generic;
using UnityEngine;

namespace NineDays
{
    /// <summary>全部可选子弹的目录；AmmoDraw 从这里抽取。</summary>
    [CreateAssetMenu(menuName = "9Days/配置/弹药目录", fileName = "AmmoCatalog")]
    public class AmmoCatalog : ScriptableObject
    {
        [SerializeField] private List<AmmoDefinition> ammo = new List<AmmoDefinition>();

        public IReadOnlyList<AmmoDefinition> Ammo => ammo;
        public int Count => ammo != null ? ammo.Count : 0;

        public AmmoDefinition Get(int index)
        {
            if (ammo == null || index < 0 || index >= ammo.Count) return null;
            return ammo[index];
        }

        /// <summary>编辑器生成与测试注入：整体替换列表。</summary>
        public void SetAmmo(IEnumerable<AmmoDefinition> definitions)
        {
            ammo = definitions != null ? new List<AmmoDefinition>(definitions) : new List<AmmoDefinition>();
        }

        /// <summary>生成器补默认项：保留已有条目，只补缺失项。</summary>
        public void EnsureAmmo(IEnumerable<AmmoDefinition> definitions)
        {
            if (ammo == null) ammo = new List<AmmoDefinition>();
            if (definitions == null) return;

            foreach (AmmoDefinition definition in definitions)
                if (definition != null && !ammo.Contains(definition))
                    ammo.Add(definition);
        }

        /// <summary>编辑器/生成器校验：移除空项与没有 prefab 的项。</summary>
        public void Validate()
        {
            if (ammo == null)
            {
                ammo = new List<AmmoDefinition>();
                return;
            }

            for (int i = ammo.Count - 1; i >= 0; i--)
            {
                if (ammo[i] == null || ammo[i].prefab == null)
                    ammo.RemoveAt(i);
            }
        }

        private void OnValidate()
        {
            // 运行时只读；这里只清理编辑器里的空引用，不修改 prefab。
            if (ammo == null) ammo = new List<AmmoDefinition>();
        }
    }
}
