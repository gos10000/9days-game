namespace NineDays
{
    /// <summary>玩家设置存档接口；Service 不依赖具体存储实现。</summary>
    public interface ISettingsStore
    {
        /// <summary>读取设置；无文件或读取失败时返回 defaults 的副本。</summary>
        PlayerSettingsData Load(PlayerSettingsData defaults);

        /// <summary>保存设置；实现负责路径、格式和错误处理。</summary>
        void Save(PlayerSettingsData data);
    }
}
