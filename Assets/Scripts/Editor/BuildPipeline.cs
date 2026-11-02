using System;
using UnityEditor;
using UnityEngine;

namespace NineDays.EditorTools
{
    /// <summary>菜单入口与 batchmode 入口（niexa 分支只保留 UI 生成）。</summary>
    internal static class BuildPipeline
    {
        [MenuItem("9Days/Build/UI (Prefabs + UIScene)")]
        public static void BuildUIMenu()
        {
            UIBuilder.Build();
            UIBuilder.BuildScene();
            UIBuilder.Validate();
            Debug.Log("[Build] UI 完成");
        }

        /// <summary>
        /// batchmode 入口（UI 预制体 + UIScene）：
        /// Unity.exe -batchmode -quit -projectPath &lt;项目&gt; -executeMethod NineDays.EditorTools.BuildPipeline.BuildUIBatch -logFile &lt;日志&gt;
        /// </summary>
        public static void BuildUIBatch()
        {
            try
            {
                UIBuilder.Build();
                UIBuilder.BuildScene();
                UIBuilder.Validate();

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
