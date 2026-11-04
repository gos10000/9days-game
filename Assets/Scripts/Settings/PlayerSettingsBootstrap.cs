using System.Collections.Generic;
using UnityEngine;

namespace NineDays
{
    /// <summary>
    /// 玩家设置启动入口：加载存档、收集 Applier 并初始化 Service。
    /// 放在启动场景；没有设置页面时也可以独立工作。
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public sealed class PlayerSettingsBootstrap : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private PlayerSettingsDefaults defaults;

        [Header("生命周期")]
        [Tooltip("跨场景保留设置服务")]
        [SerializeField] private bool dontDestroyOnLoad = true;

        [Header("Applier")]
        [Tooltip("从 Bootstrap 子节点自动收集 SettingsApplier")]
        [SerializeField] private bool autoCollectAppliers = true;

        [SerializeField] private List<SettingsApplier> appliers = new List<SettingsApplier>();

        public static PlayerSettingsBootstrap Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (dontDestroyOnLoad) DontDestroyOnLoad(gameObject);

            if (defaults == null)
            {
                Debug.LogError("[Settings] PlayerSettingsBootstrap 缺少 PlayerSettingsDefaults");
                return;
            }

            if (autoCollectAppliers && appliers.Count == 0)
            {
                appliers.AddRange(GetComponentsInChildren<SettingsApplier>(true));
            }

            var settingsAppliers = new List<ISettingsApplier>();
            for (int i = 0; i < appliers.Count; i++)
                if (appliers[i] != null) settingsAppliers.Add(appliers[i]);

            PlayerSettingsService.Initialize(defaults, new JsonSettingsStore(), settingsAppliers);
        }

        private void OnApplicationQuit()
        {
            if (PlayerSettingsService.IsInitialized) PlayerSettingsService.Save();
        }

        private void OnDestroy()
        {
            if (Instance != this) return;

            Instance = null;
            PlayerSettingsService.Shutdown();
        }
    }
}
