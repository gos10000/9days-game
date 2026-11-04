using System;
using UnityEngine;

namespace NineDays
{
    /// <summary>音频设置；值范围为 0~1。</summary>
    [Serializable]
    public sealed class AudioSettingsData
    {
        [Range(0f, 1f)] public float masterVolume = 1f;
        [Range(0f, 1f)] public float musicVolume = 0.8f;
        [Range(0f, 1f)] public float sfxVolume = 0.8f;
        public bool mute;

        /// <summary>创建一份独立副本，避免 UI 或存档修改原对象。</summary>
        public AudioSettingsData Clone()
        {
            return new AudioSettingsData
            {
                masterVolume = masterVolume,
                musicVolume = musicVolume,
                sfxVolume = sfxVolume,
                mute = mute
            };
        }
    }

    /// <summary>显示设置；分辨率为 0 表示沿用当前设备分辨率。</summary>
    [Serializable]
    public sealed class DisplaySettingsData
    {
        public FullScreenMode fullscreenMode = FullScreenMode.FullScreenWindow;

        [Min(0)] public int resolutionWidth;
        [Min(0)] public int resolutionHeight;

        public bool vSync = true;

        [Tooltip("0 = 不限制；大于 0 时限制帧率")]
        [Min(0)] public int frameRateLimit;

        [Tooltip("-1 = 沿用当前画质")]
        [Min(-1)] public int qualityLevel = -1;

        /// <summary>创建一份独立副本。</summary>
        public DisplaySettingsData Clone()
        {
            return new DisplaySettingsData
            {
                fullscreenMode = fullscreenMode,
                resolutionWidth = resolutionWidth,
                resolutionHeight = resolutionHeight,
                vSync = vSync,
                frameRateLimit = frameRateLimit,
                qualityLevel = qualityLevel
            };
        }
    }

    /// <summary>
    /// 玩家设置数据。只保存玩家可修改的值；默认值放在 PlayerSettingsDefaults。
    /// </summary>
    [Serializable]
    public sealed class PlayerSettingsData
    {
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public AudioSettingsData audio = new AudioSettingsData();
        public DisplaySettingsData display = new DisplaySettingsData();

        /// <summary>深拷贝，避免 Service 与 UI 共享同一个对象。</summary>
        public PlayerSettingsData Clone()
        {
            return new PlayerSettingsData
            {
                version = version,
                audio = audio != null ? audio.Clone() : new AudioSettingsData(),
                display = display != null ? display.Clone() : new DisplaySettingsData()
            };
        }
    }
}
