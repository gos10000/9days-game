using System;
using UnityEditor;
using UnityEngine;

namespace NineDays.EditorTools
{
    /// <summary>
    /// 项目设置：Layer 与 Sorting Layer。
    /// 必须在 Unity 里执行（直接改 ProjectSettings 会被 Unity 回写覆盖）。
    /// 菜单：9Days/Setup/Layers & Sorting Layers
    /// </summary>
    internal static class ProjectSetup
    {
        internal static readonly string[] RequiredLayers = { "Character", "Projectile", "Obstacle" };
        internal static readonly string[] RequiredSortingLayers = { "Character", "Projectile", "FX" };

        [MenuItem("9Days/Setup/Layers & Sorting Layers")]
        public static void Apply()
        {
            UnityEngine.Object[] settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (settings == null || settings.Length == 0)
                throw new InvalidOperationException("找不到 ProjectSettings/TagManager.asset");

            var so = new SerializedObject(settings[0]);
            SerializedProperty layers = so.FindProperty("layers");
            SerializedProperty sortingLayers = so.FindProperty("m_SortingLayers");

            foreach (string layerName in RequiredLayers)
            {
                if (HasLayer(layers, layerName)) continue;

                int slot = FindFreeLayerSlot(layers);
                if (slot < 0) throw new InvalidOperationException("没有空闲的用户 Layer 槽位");

                layers.GetArrayElementAtIndex(slot).stringValue = layerName;
                Debug.Log($"[Setup] 新增 Layer: {layerName} (slot {slot})");
            }

            foreach (string sortingName in RequiredSortingLayers)
            {
                if (HasSortingLayer(sortingLayers, sortingName)) continue;

                int index = sortingLayers.arraySize;
                sortingLayers.arraySize = index + 1;

                SerializedProperty entry = sortingLayers.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("name").stringValue = sortingName;
                entry.FindPropertyRelative("uniqueID").intValue = StableId(sortingName);
                entry.FindPropertyRelative("locked").intValue = 0;

                Debug.Log($"[Setup] 新增 Sorting Layer: {sortingName}");
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log("[Setup] Layer / Sorting Layer 配置完成");
        }

        private static bool HasLayer(SerializedProperty layers, string name)
        {
            for (int i = 0; i < layers.arraySize; i++)
                if (layers.GetArrayElementAtIndex(i).stringValue == name) return true;
            return false;
        }

        private static int FindFreeLayerSlot(SerializedProperty layers)
        {
            for (int i = 8; i < layers.arraySize; i++)   // 0~7 是 Unity 内置层
                if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue)) return i;
            return -1;
        }

        private static bool HasSortingLayer(SerializedProperty sortingLayers, string name)
        {
            for (int i = 0; i < sortingLayers.arraySize; i++)
            {
                SerializedProperty entry = sortingLayers.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("name").stringValue == name) return true;
            }
            return false;
        }

        /// <summary>给 Sorting Layer 生成一个稳定的唯一 id（只需唯一，不必和 Unity 内部算法一致）。</summary>
        private static int StableId(string text)
        {
            unchecked
            {
                int hash = (int)2166136261;
                for (int i = 0; i < text.Length; i++)
                {
                    hash ^= text[i];
                    hash *= 16777619;
                }
                return hash == 0 ? 1 : Mathf.Abs(hash);
            }
        }
    }
}
