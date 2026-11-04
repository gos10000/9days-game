using UnityEngine;

namespace NineDays
{
    /// <summary>设置存档版本迁移入口；新增设置项导致结构变化时在这里逐版本补齐。</summary>
    public static class PlayerSettingsMigration
    {
        /// <summary>把旧版本数据升级到当前版本；当前只有版本 1。</summary>
        public static PlayerSettingsData Upgrade(PlayerSettingsData data)
        {
            if (data == null) return new PlayerSettingsData();

            if (data.version <= 0)
                data.version = PlayerSettingsData.CurrentVersion;

            if (data.version > PlayerSettingsData.CurrentVersion)
                Debug.LogWarning($"[Settings] 存档版本 {data.version} 高于当前版本 {PlayerSettingsData.CurrentVersion}");

            // 后续版本在这里按 data.version 补齐新字段，例如：
            // if (data.version < 2) { ...; data.version = 2; }

            return data;
        }
    }
}
