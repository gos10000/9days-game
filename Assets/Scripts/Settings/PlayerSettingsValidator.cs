using System;
using UnityEngine;

namespace NineDays
{
    /// <summary>把玩家设置钳制到安全范围；不读取也不写入文件。</summary>
    public static class PlayerSettingsValidator
    {
        private const int MinResolution = 320;
        private const int MaxResolution = 16384;
        private const int MinFrameRate = 30;
        private const int MaxFrameRate = 240;

        /// <summary>规范化数据并初始化缺失的分组。</summary>
        public static bool Normalize(PlayerSettingsData data, out string error)
        {
            if (data == null)
            {
                error = "[Settings] 设置数据为空";
                return false;
            }

            if (data.audio == null) data.audio = new AudioSettingsData();
            if (data.display == null) data.display = new DisplaySettingsData();
            if (data.version <= 0) data.version = PlayerSettingsData.CurrentVersion;

            data.audio.masterVolume = Mathf.Clamp01(data.audio.masterVolume);
            data.audio.musicVolume = Mathf.Clamp01(data.audio.musicVolume);
            data.audio.sfxVolume = Mathf.Clamp01(data.audio.sfxVolume);

            DisplaySettingsData display = data.display;
            if (!Enum.IsDefined(typeof(FullScreenMode), display.fullscreenMode))
                display.fullscreenMode = FullScreenMode.FullScreenWindow;

            if (display.resolutionWidth <= 0 || display.resolutionHeight <= 0)
            {
                display.resolutionWidth = 0;
                display.resolutionHeight = 0;
            }
            else
            {
                display.resolutionWidth = Mathf.Clamp(display.resolutionWidth, MinResolution, MaxResolution);
                display.resolutionHeight = Mathf.Clamp(display.resolutionHeight, MinResolution, MaxResolution);
            }

            if (display.frameRateLimit < 0)
                display.frameRateLimit = 0;
            else if (display.frameRateLimit > 0)
                display.frameRateLimit = Mathf.Clamp(display.frameRateLimit, MinFrameRate, MaxFrameRate);

            int qualityCount = QualitySettings.names != null ? QualitySettings.names.Length : 0;
            if (qualityCount <= 0)
                display.qualityLevel = -1;
            else if (display.qualityLevel < -1)
                display.qualityLevel = -1;
            else if (display.qualityLevel >= qualityCount)
                display.qualityLevel = qualityCount - 1;

            error = null;
            return true;
        }
    }
}
