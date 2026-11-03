using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace NineDays.Tests
{
    /// <summary>默认地形资产必须能被运行时读取并搭建。</summary>
    public class DefaultArenaConfigTests
    {
        [Test]
        public void DefaultConfig_HasReadableMasksAndBuilds()
        {
            var config = AssetDatabase.LoadAssetAtPath<ArenaConfig>(EditorAssetsConstants.ArenaConfigPath);
            Assert.IsNotNull(config, "默认地形配置不存在，请先跑 BuildAll");
            Assert.Greater(config.layers.Count, 0);

            for (int i = 0; i < config.layers.Count; i++)
            {
                TerrainLayerData layer = config.layers[i];
                Assert.IsNotNull(layer.initialMask, layer.layerId);
                Assert.IsTrue(layer.initialMask.isReadable, layer.layerId + " 遮罩必须可读");
                Assert.DoesNotThrow(() => layer.initialMask.GetPixels32());
            }

            var go = new GameObject("Arena_Default_Test");
            var arena = go.AddComponent<ArenaController>();
            arena.Configure(config, null, null);
            arena.BuildForBattle();

            Assert.Greater(arena.Layers.Count, 0);
            Assert.IsNotNull(arena.Physics);
            Assert.Greater(arena.Physics.ContourCount, 0);

            Object.DestroyImmediate(go);
        }

        /// <summary>测试不能引用 EditorTools 的 internal 常量，这里只存路径副本。</summary>
        private static class EditorAssetsConstants
        {
            internal const string ArenaConfigPath = "Assets/Data/Arena/ArenaConfig_Default.asset";
        }
    }
}
