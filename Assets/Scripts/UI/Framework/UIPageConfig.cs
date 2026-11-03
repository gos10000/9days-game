using System;
using UnityEngine;

namespace NineDays
{
    /// <summary>单个页面的配置：预制体、层、缓存策略与过渡。</summary>
    [CreateAssetMenu(menuName = "9Days/UI/Page Config", fileName = "UIPageConfig")]
    public sealed class UIPageConfig : ScriptableObject
    {
        [SerializeField] private string pageId = "Page";
        [SerializeField] private GameObject prefab;
        [SerializeField] private UIPageLayer layer = UIPageLayer.Screen;
        [SerializeField] private UIPageCachePolicy cachePolicy = UIPageCachePolicy.KeepAlive;
        [SerializeField] private bool closeOnBack = true;
        [SerializeField] private bool modal = true;
        [SerializeField] private UIPageTransition transition;

        public string PageId => pageId;
        public GameObject Prefab => prefab;
        public UIPageLayer Layer => layer;
        public UIPageCachePolicy CachePolicy => cachePolicy;
        public bool CloseOnBack => closeOnBack;
        public bool Modal => modal;
        public UIPageTransition Transition => transition;

        /// <summary>编辑器生成与测试用初始化入口。</summary>
        public void Configure(string id, GameObject pagePrefab, UIPageLayer pageLayer,
                              UIPageCachePolicy policy = UIPageCachePolicy.KeepAlive,
                              bool canCloseOnBack = true, UIPageTransition pageTransition = null,
                              bool isModal = true)
        {
            pageId = id;
            prefab = pagePrefab;
            layer = pageLayer;
            cachePolicy = policy;
            closeOnBack = canCloseOnBack;
            transition = pageTransition;
            modal = isModal;
        }

        /// <summary>校验 id、prefab 结构；失败直接抛异常，方便 BuildCheck 拦截。</summary>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(pageId))
                throw new InvalidOperationException("[UI] UIPageConfig.pageId 不能为空");

            if (prefab == null)
                throw new InvalidOperationException($"[UI] 页面 {pageId} 缺少 prefab");

            if (prefab.GetComponent<RectTransform>() == null)
                throw new InvalidOperationException($"[UI] 页面 {pageId} 的 prefab 根节点必须是 RectTransform");

            if (prefab.GetComponent<UIPage>() == null)
                throw new InvalidOperationException($"[UI] 页面 {pageId} 的 prefab 缺少 UIPage 组件");

            if (prefab.GetComponent<CanvasGroup>() == null)
                throw new InvalidOperationException($"[UI] 页面 {pageId} 的 prefab 缺少 CanvasGroup");
        }

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(pageId)) pageId = "Page";
        }
    }
}
