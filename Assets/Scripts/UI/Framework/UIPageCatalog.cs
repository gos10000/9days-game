using System;
using System.Collections.Generic;
using UnityEngine;

namespace NineDays
{
    /// <summary>页面注册表；按 pageId 查配置，重复 id 会被拒绝。</summary>
    [CreateAssetMenu(menuName = "9Days/UI/Page Catalog", fileName = "UIPageCatalog")]
    public sealed class UIPageCatalog : ScriptableObject
    {
        [SerializeField] private List<UIPageConfig> pages = new List<UIPageConfig>();

        private readonly Dictionary<string, UIPageConfig> lookup = new Dictionary<string, UIPageConfig>();

        public IReadOnlyList<UIPageConfig> Pages => pages;
        public int Count => pages != null ? pages.Count : 0;

        /// <summary>编辑器生成与测试用：整体替换页面列表。</summary>
        public void SetPages(IEnumerable<UIPageConfig> configs)
        {
            pages = configs != null ? new List<UIPageConfig>(configs) : new List<UIPageConfig>();
            Rebuild();
        }

        /// <summary>建立 id 到配置的索引；重复 id 或空配置会抛异常。</summary>
        public void Rebuild()
        {
            lookup.Clear();
            if (pages == null) return;

            for (int i = 0; i < pages.Count; i++)
            {
                UIPageConfig config = pages[i];
                if (config == null)
                    throw new InvalidOperationException($"[UI] UIPageCatalog 第 {i} 项为空");

                if (lookup.ContainsKey(config.PageId))
                    throw new InvalidOperationException($"[UI] 重复的 pageId: {config.PageId}");

                lookup.Add(config.PageId, config);
            }
        }

        public bool TryGet(string pageId, out UIPageConfig config)
        {
            if (lookup.Count == 0) Rebuild();
            return lookup.TryGetValue(pageId, out config);
        }

        public UIPageConfig Find(string pageId)
        {
            if (string.IsNullOrWhiteSpace(pageId))
                throw new ArgumentException("[UI] pageId 不能为空", nameof(pageId));

            if (lookup.Count == 0) Rebuild();

            if (!lookup.TryGetValue(pageId, out UIPageConfig config))
                throw new InvalidOperationException($"[UI] 找不到页面: {pageId}");

            return config;
        }

        /// <summary>在所有配置上执行结构校验；BuildCheck 会调用。</summary>
        public void Validate()
        {
            Rebuild();
            if (pages == null) return;

            for (int i = 0; i < pages.Count; i++)
                pages[i].Validate();
        }

        private void OnEnable()
        {
            try
            {
                Rebuild();
            }
            catch (InvalidOperationException e)
            {
                Debug.LogError(e.Message);
            }
        }
    }
}
