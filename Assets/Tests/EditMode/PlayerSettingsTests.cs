using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace NineDays.Tests
{
    /// <summary>玩家设置框架：数据、校验、JSON 存档、Service 与 Applier 顺序。</summary>
    public class PlayerSettingsTests
    {
        private const string DefaultAssetPath = "Assets/Data/Settings/PlayerSettingsDefaults.asset";

        private sealed class MemoryStore : ISettingsStore
        {
            public PlayerSettingsData Value;
            public int Saves;

            public PlayerSettingsData Load(PlayerSettingsData defaults)
            {
                return Value != null ? Value.Clone() : defaults.Clone();
            }

            public void Save(PlayerSettingsData data)
            {
                Value = data.Clone();
                Saves++;
            }
        }

        private sealed class RecordingApplier : ISettingsApplier
        {
            private readonly List<int> orderLog;

            public string Section { get; }
            public int Order { get; }
            public PlayerSettingsData Last { get; private set; }

            public RecordingApplier(string section, int order, List<int> orderLog)
            {
                Section = section;
                Order = order;
                this.orderLog = orderLog;
            }

            public void Apply(PlayerSettingsData data)
            {
                Last = data.Clone();
                orderLog?.Add(Order);
            }
        }

        private PlayerSettingsDefaults defaults;
        private string tempDirectory;
        private string tempFile;

        [SetUp]
        public void SetUp()
        {
            PlayerSettingsService.Shutdown();

            defaults = ScriptableObject.CreateInstance<PlayerSettingsDefaults>();
            defaults.SetValues(NewDefaultData());

            tempDirectory = Path.Combine(Path.GetTempPath(), "NineDaysSettingsTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDirectory);
            tempFile = Path.Combine(tempDirectory, "settings.json");
        }

        [TearDown]
        public void TearDown()
        {
            PlayerSettingsService.Shutdown();
            if (defaults != null) UnityEngine.Object.DestroyImmediate(defaults);

            if (Directory.Exists(tempDirectory))
            {
                try
                {
                    Directory.Delete(tempDirectory, true);
                }
                catch (IOException)
                {
                    // 临时目录清理失败不影响测试结论
                }
            }
        }

        [Test]
        public void DefaultAsset_IsValid()
        {
            var asset = AssetDatabase.LoadAssetAtPath<PlayerSettingsDefaults>(DefaultAssetPath);
            Assert.IsNotNull(asset, "默认设置资产不存在，请先运行 Build/All 或 Build/Prefabs");
            Assert.IsTrue(PlayerSettingsValidator.Normalize(asset.CreateDefault(), out string error), error);
        }

        [Test]
        public void DataClone_IsDeepCopy()
        {
            var source = NewDefaultData();
            source.audio.masterVolume = 0.25f;
            source.display.fullscreenMode = FullScreenMode.Windowed;

            var copy = source.Clone();
            copy.audio.masterVolume = 0.9f;
            copy.display.fullscreenMode = FullScreenMode.ExclusiveFullScreen;

            Assert.AreEqual(0.25f, source.audio.masterVolume, 0.001f);
            Assert.AreEqual(FullScreenMode.Windowed, source.display.fullscreenMode);
            Assert.AreNotSame(source.audio, copy.audio);
            Assert.AreNotSame(source.display, copy.display);
        }

        [Test]
        public void Validator_ClampsInvalidValues()
        {
            var data = NewDefaultData();
            data.audio.masterVolume = -1f;
            data.audio.musicVolume = 2f;
            data.audio.sfxVolume = 0.5f;
            data.display.fullscreenMode = (FullScreenMode)99;
            data.display.resolutionWidth = 100;
            data.display.resolutionHeight = 0;
            data.display.frameRateLimit = 1;
            data.display.qualityLevel = 999;

            Assert.IsTrue(PlayerSettingsValidator.Normalize(data, out string error), error);
            Assert.AreEqual(0f, data.audio.masterVolume, 0.001f);
            Assert.AreEqual(1f, data.audio.musicVolume, 0.001f);
            Assert.AreEqual(0, data.display.resolutionWidth);
            Assert.AreEqual(0, data.display.resolutionHeight);
            Assert.AreEqual(FullScreenMode.FullScreenWindow, data.display.fullscreenMode);
            Assert.AreEqual(30, data.display.frameRateLimit);

            if (QualitySettings.names.Length > 0)
                Assert.AreEqual(QualitySettings.names.Length - 1, data.display.qualityLevel);
        }

        [Test]
        public void JsonStore_MissingFile_UsesDefaults()
        {
            var store = new JsonSettingsStore(tempFile);
            PlayerSettingsData loaded = store.Load(defaults.CreateDefault());

            Assert.AreEqual(0.8f, loaded.audio.musicVolume, 0.001f);
            Assert.IsFalse(File.Exists(tempFile));
        }

        [Test]
        public void JsonStore_SaveAndLoad_RoundTrips()
        {
            var store = new JsonSettingsStore(tempFile);
            var data = defaults.CreateDefault();
            data.audio.musicVolume = 0.25f;
            data.display.vSync = false;

            store.Save(data);
            Assert.IsTrue(File.Exists(tempFile));

            PlayerSettingsData loaded = store.Load(defaults.CreateDefault());
            Assert.AreEqual(0.25f, loaded.audio.musicVolume, 0.001f);
            Assert.IsFalse(loaded.display.vSync);
            Assert.AreEqual(PlayerSettingsData.CurrentVersion, loaded.version);
        }

        [Test]
        public void JsonStore_CorruptFile_BacksUpAndUsesDefaults()
        {
            File.WriteAllText(tempFile, "{ not valid json");
            var store = new JsonSettingsStore(tempFile);

            PlayerSettingsData loaded = store.Load(defaults.CreateDefault());

            Assert.AreEqual(0.8f, loaded.audio.musicVolume, 0.001f);
            Assert.IsTrue(File.Exists(tempFile + ".corrupt"));
        }

        [Test]
        public void Service_InitializesAppliesAndExposesCopy()
        {
            var store = new MemoryStore { Value = defaults.CreateDefault() };
            var applier = new RecordingApplier(SettingsSections.Audio, 0, null);

            PlayerSettingsService.Initialize(defaults, store, new[] { applier });

            Assert.IsTrue(PlayerSettingsService.IsInitialized);
            Assert.IsNotNull(applier.Last);
            Assert.AreEqual(1f, PlayerSettingsService.Current.audio.masterVolume, 0.001f);

            PlayerSettingsData copy = PlayerSettingsService.Current;
            copy.audio.masterVolume = 0.1f;
            Assert.AreEqual(1f, PlayerSettingsService.Current.audio.masterVolume, 0.001f);
        }

        [Test]
        public void Service_UpdateAppliesSavesAndRaisesChanged()
        {
            var store = new MemoryStore { Value = defaults.CreateDefault() };
            var applier = new RecordingApplier(SettingsSections.Audio, 0, null);
            PlayerSettingsService.Initialize(defaults, store, new[] { applier });

            int changed = 0;
            PlayerSettingsService.Changed += () => changed++;
            PlayerSettingsService.Update(settings => settings.audio.sfxVolume = 0.2f);

            Assert.AreEqual(0.2f, PlayerSettingsService.Current.audio.sfxVolume, 0.001f);
            Assert.AreEqual(0.2f, applier.Last.audio.sfxVolume, 0.001f);
            Assert.AreEqual(1, store.Saves);
            Assert.AreEqual(1, changed);
        }

        [Test]
        public void Service_ResetToDefaults_RestoresDefaults()
        {
            var store = new MemoryStore { Value = defaults.CreateDefault() };
            PlayerSettingsService.Initialize(defaults, store, Array.Empty<ISettingsApplier>());

            PlayerSettingsService.Update(settings => settings.audio.masterVolume = 0.1f);
            PlayerSettingsService.ResetToDefaults();

            Assert.AreEqual(1f, PlayerSettingsService.Current.audio.masterVolume, 0.001f);
        }

        [Test]
        public void Service_AppliesAppliersInOrder()
        {
            var order = new List<int>();
            var first = new RecordingApplier(SettingsSections.Audio, 20, order);
            var second = new RecordingApplier(SettingsSections.Display, 10, order);
            var store = new MemoryStore { Value = defaults.CreateDefault() };

            PlayerSettingsService.Initialize(defaults, store, new ISettingsApplier[] { first, second });

            CollectionAssert.AreEqual(new[] { 10, 20 }, order);
        }

        [Test]
        public void Service_Uninitialized_Throws()
        {
            PlayerSettingsService.Shutdown();
            Assert.Throws<InvalidOperationException>(() =>
            {
                PlayerSettingsData ignored = PlayerSettingsService.Current;
            });
        }

        private static PlayerSettingsData NewDefaultData()
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
