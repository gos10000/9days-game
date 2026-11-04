using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace NineDays
{
    /// <summary>玩家设置的 JSON 存档；默认写到 persistentDataPath/NineDays/player-settings.json。</summary>
    public sealed class JsonSettingsStore : ISettingsStore
    {
        public const string DefaultFileName = "player-settings.json";

        public string FilePath { get; }

        public JsonSettingsStore(string filePath = null)
        {
            FilePath = string.IsNullOrWhiteSpace(filePath)
                ? Path.Combine(Application.persistentDataPath, "NineDays", DefaultFileName)
                : filePath;
        }

        /// <summary>读取设置；文件不存在、为空或损坏时返回默认值副本。</summary>
        public PlayerSettingsData Load(PlayerSettingsData defaults)
        {
            PlayerSettingsData fallback = defaults != null ? defaults.Clone() : new PlayerSettingsData();
            if (string.IsNullOrEmpty(FilePath) || !File.Exists(FilePath)) return fallback;

            try
            {
                string json = File.ReadAllText(FilePath, Encoding.UTF8);
                PlayerSettingsData data = JsonUtility.FromJson<PlayerSettingsData>(json);
                if (data == null) throw new InvalidDataException("反序列化结果为空");
                return PlayerSettingsMigration.Upgrade(data);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Settings] 读取失败，使用默认值：{FilePath}\n{e.Message}");
                BackupCorruptFile();
                return fallback;
            }
        }

        /// <summary>写入设置；先写临时文件，再尽量原子替换目标文件。</summary>
        public void Save(PlayerSettingsData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));

            string directory = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            string json = JsonUtility.ToJson(data, true);
            string tempPath = FilePath + ".tmp";
            File.WriteAllText(tempPath, json, Encoding.UTF8);

            try
            {
                if (File.Exists(FilePath))
                    File.Replace(tempPath, FilePath, null);
                else
                    File.Move(tempPath, FilePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Settings] 原子替换失败，改用覆盖写入：{FilePath}\n{e.Message}");
                File.Copy(tempPath, FilePath, true);
                File.Delete(tempPath);
            }
        }

        /// <summary>损坏文件保留一份 .corrupt 备份，避免直接覆盖玩家原文件。</summary>
        private void BackupCorruptFile()
        {
            try
            {
                if (!File.Exists(FilePath)) return;
                string backupPath = FilePath + ".corrupt";
                if (File.Exists(backupPath)) File.Delete(backupPath);
                File.Copy(FilePath, backupPath, true);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Settings] 无法备份损坏的设置文件：{FilePath}\n{e.Message}");
            }
        }
    }
}
