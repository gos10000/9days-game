using UnityEditor;
using UnityEngine;

namespace NineDays.EditorTools
{
    /// <summary>生成玩家设置默认值资产；已有资产不覆盖。</summary>
    internal static class SettingsFactory
    {
        internal static PlayerSettingsDefaults EnsureDefaultSettings()
        {
            var existing = AssetDatabase.LoadAssetAtPath<PlayerSettingsDefaults>(
                EditorAssets.PlayerSettingsDefaultsPath);
            if (existing != null) return existing;

            EditorAssets.EnsureFolderOfFile(EditorAssets.PlayerSettingsDefaultsPath);

            var asset = ScriptableObject.CreateInstance<PlayerSettingsDefaults>();
            asset.SetValues(BuildDefaultValues());
            AssetDatabase.CreateAsset(asset, EditorAssets.PlayerSettingsDefaultsPath);
            AssetDatabase.SaveAssets();

            Debug.Log($"[Settings] 默认设置资产: {EditorAssets.PlayerSettingsDefaultsPath}");
            return asset;
        }

        /// <summary>默认值只在新资产创建时写入，后续手调不会被生成器覆盖。</summary>
        private static PlayerSettingsData BuildDefaultValues()
        {
            return new PlayerSettingsData
            {
                version = PlayerSettingsData.CurrentVersion,
                audio = new AudioSettingsData
                {
                    masterVolume = 1f,
                    musicVolume = 0.8f,
                    sfxVolume = 0.8f,
                    mute = false
                },
                display = new DisplaySettingsData
                {
                    fullscreenMode = FullScreenMode.FullScreenWindow,
                    resolutionWidth = 0,
                    resolutionHeight = 0,
                    vSync = true,
                    frameRateLimit = 0,
                    qualityLevel = -1
                }
            };
        }
    }
}
