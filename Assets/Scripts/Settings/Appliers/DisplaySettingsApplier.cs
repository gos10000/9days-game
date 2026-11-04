using UnityEngine;

namespace NineDays
{
    /// <summary>把显示设置应用到 Unity 的 Screen / QualitySettings；示例性 Applier。</summary>
    public sealed class DisplaySettingsApplier : SettingsApplier
    {
        [Tooltip("编辑器 Play Mode 下是否真的修改分辨率；默认关闭，避免影响调试窗口")]
        [SerializeField] private bool applyInEditor = false;

        public override string Section => SettingsSections.Display;
        public override int Order => 10;

        public override void Apply(PlayerSettingsData data)
        {
            if (data == null || data.display == null) return;
            if (Application.isEditor && !applyInEditor) return;

            DisplaySettingsData display = data.display;
            if (display.resolutionWidth > 0 && display.resolutionHeight > 0)
            {
                Screen.SetResolution(display.resolutionWidth, display.resolutionHeight, display.fullscreenMode);
            }
            else
            {
                Screen.fullScreenMode = display.fullscreenMode;
            }

            QualitySettings.vSyncCount = display.vSync ? 1 : 0;
            Application.targetFrameRate = display.frameRateLimit;

            if (display.qualityLevel >= 0 && QualitySettings.names.Length > 0)
            {
                int level = Mathf.Clamp(display.qualityLevel, 0, QualitySettings.names.Length - 1);
                QualitySettings.SetQualityLevel(level, true);
            }
        }
    }
}
