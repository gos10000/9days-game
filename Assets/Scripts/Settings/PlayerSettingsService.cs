using System;
using System.Collections.Generic;
using UnityEngine;

namespace NineDays
{
    /// <summary>
    /// 玩家设置门面：读写当前值、应用 Applier、保存和重置。
    /// 业务通过 Update/Apply 修改，不能直接持有 Current 的引用。
    /// </summary>
    public static class PlayerSettingsService
    {
        private static readonly List<ISettingsApplier> appliers = new List<ISettingsApplier>();

        private static PlayerSettingsDefaults defaults;
        private static ISettingsStore store;
        private static PlayerSettingsData current;
        private static bool initialized;

        public static bool IsInitialized => initialized;

        /// <summary>返回当前设置副本；未初始化时抛出明确异常。</summary>
        public static PlayerSettingsData Current
        {
            get
            {
                EnsureInitialized();
                return current.Clone();
            }
        }

        /// <summary>设置应用完成后触发；设置页或其他观察者再读取 Current。</summary>
        public static event Action Changed;

        /// <summary>用默认值资产、存档和 Applier 初始化；重复调用会先关闭旧状态。</summary>
        public static void Initialize(PlayerSettingsDefaults defaultsAsset, ISettingsStore settingsStore = null,
                                      IEnumerable<ISettingsApplier> settingsAppliers = null)
        {
            if (defaultsAsset == null)
            {
                Debug.LogError("[Settings] 缺少 PlayerSettingsDefaults，无法初始化");
                return;
            }

            if (initialized) Shutdown();

            defaults = defaultsAsset;
            store = settingsStore ?? new JsonSettingsStore();
            appliers.Clear();

            if (settingsAppliers != null)
            {
                foreach (ISettingsApplier applier in settingsAppliers)
                    if (applier != null) appliers.Add(applier);
            }

            SortAppliers();

            current = LoadAndNormalize();
            initialized = true;
            ApplyAppliers();
        }

        /// <summary>替换整份设置：校验、应用、保存（可选）并广播变化。</summary>
        public static void Apply(PlayerSettingsData next, bool save = true)
        {
            EnsureInitialized();

            PlayerSettingsData data = next != null ? next.Clone() : defaults.CreateDefault();
            if (!PlayerSettingsValidator.Normalize(data, out string error))
            {
                Debug.LogError(error);
                return;
            }

            current = data;
            ApplyAppliers();
            if (save) Save();
            Changed?.Invoke();
        }

        /// <summary>修改当前设置的副本，再走 Apply 的完整流程。</summary>
        public static void Update(Action<PlayerSettingsData> mutate, bool save = true)
        {
            EnsureInitialized();
            if (mutate == null) return;

            PlayerSettingsData data = current.Clone();
            mutate(data);
            Apply(data, save);
        }

        /// <summary>恢复默认值并应用、保存。</summary>
        public static void ResetToDefaults(bool save = true)
        {
            EnsureInitialized();
            Apply(defaults.CreateDefault(), save);
        }

        /// <summary>重新从存档读取，适合“放弃未保存修改”或外部文件变更后的刷新。</summary>
        public static void Reload()
        {
            EnsureInitialized();
            current = LoadAndNormalize();
            ApplyAppliers();
            Changed?.Invoke();
        }

        /// <summary>把当前设置写入存档；保存失败只记录日志，不影响内存中的值。</summary>
        public static void Save()
        {
            if (!initialized || store == null || current == null) return;

            try
            {
                store.Save(current);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Settings] 保存失败：{e.Message}");
            }
        }

        /// <summary>关闭 Service 并清理静态状态；Bootstrap 销毁和测试清理时调用。</summary>
        public static void Shutdown()
        {
            initialized = false;
            current = null;
            defaults = null;
            store = null;
            appliers.Clear();
            Changed = null;
        }

        /// <summary>测试专用别名，语义更明确。</summary>
        public static void ResetForTests() => Shutdown();

        private static PlayerSettingsData LoadAndNormalize()
        {
            PlayerSettingsData loaded = store.Load(defaults.CreateDefault());
            if (loaded == null) loaded = defaults.CreateDefault();

            if (!PlayerSettingsValidator.Normalize(loaded, out string error))
            {
                Debug.LogError(error);
                loaded = defaults.CreateDefault();
                PlayerSettingsValidator.Normalize(loaded, out _);
            }

            return loaded;
        }

        private static void ApplyAppliers()
        {
            for (int i = 0; i < appliers.Count; i++)
            {
                ISettingsApplier applier = appliers[i];
                if (applier == null) continue;

                try
                {
                    applier.Apply(current);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Settings] Applier 应用失败：{applier.Section}\n{e}");
                }
            }
        }

        private static void SortAppliers()
        {
            appliers.Sort((a, b) => a.Order.CompareTo(b.Order));
        }

        private static void EnsureInitialized()
        {
            if (!initialized || current == null)
                throw new InvalidOperationException("[Settings] PlayerSettingsService 尚未初始化，请先放置 PlayerSettingsBootstrap");
        }
    }
}
