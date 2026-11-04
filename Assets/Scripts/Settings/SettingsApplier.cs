using UnityEngine;

namespace NineDays
{
    /// <summary>内置设置分组 id；新增分组可继续添加常量，不需要修改 Service。</summary>
    public static class SettingsSections
    {
        public const string Audio = "audio";
        public const string Display = "display";
    }

    /// <summary>把设置数据应用到具体系统；实现可以是 MonoBehaviour，也可以是测试替身。</summary>
    public interface ISettingsApplier
    {
        string Section { get; }
        int Order { get; }
        void Apply(PlayerSettingsData data);
    }

    /// <summary>场景内的设置应用器基类；Bootstrap 可以自动收集它。</summary>
    public abstract class SettingsApplier : MonoBehaviour, ISettingsApplier
    {
        public abstract string Section { get; }
        public virtual int Order => 0;
        public abstract void Apply(PlayerSettingsData data);
    }
}
