using System;
using UnityEditor;
using UnityEngine;

namespace NineDays.EditorTools
{
    /// <summary>菜单入口与 batchmode 入口。</summary>
    internal static class BuildPipeline
    {
        [MenuItem("9Days/Build/Prefabs (角色 + 皮肤 + 子弹)")]
        public static void BuildPrefabsMenu()
        {
            PrefabFactory.BuildAll();
            AmmoFactory.EnsureDefaults();
            SettingsFactory.EnsureDefaultSettings();
            ArenaTerrainFactory.EnsureDefaultConfig();
            BuildCheck.ValidateAll();
            Debug.Log("[Build] 预制体完成");
        }

        [MenuItem("9Days/Build/Test Arena")]
        public static void BuildArenaMenu()
        {
            UIFactory.BuildAll();
            SettingsFactory.EnsureDefaultSettings();
            ArenaBuilder.Build();
            Debug.Log("[Build] 测试场景完成");
        }

        [MenuItem("9Days/Build/All (Setup + Prefabs + Arena)")]
        public static void BuildAllMenu()
        {
            ProjectSetup.Apply();
            PrefabFactory.BuildAll();
            AmmoFactory.EnsureDefaults();
            SettingsFactory.EnsureDefaultSettings();
            ArenaTerrainFactory.EnsureDefaultConfig();
            ArenaBuilder.Build();
            BuildCheck.ValidateAll();
            Debug.Log("[Build] 全部完成");
        }

        /// <summary>
        /// batchmode 入口（只重建 prefab + 配置 + 结构校验，不动测试场景）：
        /// Unity.exe -batchmode -quit -projectPath &lt;项目&gt; -executeMethod NineDays.EditorTools.BuildPipeline.BuildPrefabsBatch -logFile &lt;日志&gt;
        /// </summary>
        public static void BuildPrefabsBatch()
        {
            try
            {
                ProjectSetup.Apply();
                PrefabFactory.BuildAll();
                AmmoFactory.EnsureDefaults();
                SettingsFactory.EnsureDefaultSettings();
                ArenaTerrainFactory.EnsureDefaultConfig();
                BuildCheck.ValidateAll();

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log("BUILD_OK");
            }
            catch (Exception e)
            {
                Debug.LogError("BUILD_FAILED\n" + e);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// batchmode 入口：
        /// Unity.exe -batchmode -quit -projectPath &lt;项目&gt; -executeMethod NineDays.EditorTools.BuildPipeline.BuildAllBatch -logFile &lt;日志&gt;
        /// </summary>
        public static void BuildAllBatch()
        {
            try
            {
                ProjectSetup.Apply();
                PrefabFactory.BuildAll();
                AmmoFactory.EnsureDefaults();
                SettingsFactory.EnsureDefaultSettings();
                ArenaTerrainFactory.EnsureDefaultConfig();
                ArenaBuilder.Build();
                BuildCheck.ValidateAll();

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                Debug.Log("BUILD_OK");
            }
            catch (Exception e)
            {
                Debug.LogError("BUILD_FAILED\n" + e);
                EditorApplication.Exit(1);
            }
        }
    }
}
