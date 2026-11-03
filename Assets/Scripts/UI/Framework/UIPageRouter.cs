using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NineDays
{
    /// <summary>页面路由：Screen 栈 + Popup 栈，负责实例化、缓存、返回和过渡。</summary>
    public sealed class UIPageRouter : MonoBehaviour
    {
        private readonly Dictionary<UIPageLayer, RectTransform> layers =
            new Dictionary<UIPageLayer, RectTransform>();

        private readonly List<UIPage> screenStack = new List<UIPage>();
        private readonly List<UIPage> popupStack = new List<UIPage>();
        private readonly Dictionary<string, UIPage> pageCache = new Dictionary<string, UIPage>();
        private readonly Dictionary<UIPage, UIPageConfig> pageConfigs = new Dictionary<UIPage, UIPageConfig>();
        private readonly Dictionary<UIPage, UIPageArgs> pageArgs = new Dictionary<UIPage, UIPageArgs>();
        private readonly Dictionary<UIPage, GameObject> modalBlockers = new Dictionary<UIPage, GameObject>();

        private UIPageCatalog catalog;

        public bool IsTransitioning { get; private set; }
        public UIPage CurrentScreen => screenStack.Count > 0 ? screenStack[screenStack.Count - 1] : null;
        public UIPage CurrentPopup => popupStack.Count > 0 ? popupStack[popupStack.Count - 1] : null;
        public bool CanGoBack
        {
            get
            {
                if (popupStack.Count > 0)
                {
                    UIPageConfig popupConfig = GetConfig(CurrentPopup);
                    return popupConfig == null || popupConfig.CloseOnBack;
                }

                if (screenStack.Count > 1)
                {
                    UIPageConfig screenConfig = GetConfig(CurrentScreen);
                    return screenConfig == null || screenConfig.CloseOnBack;
                }

                return false;
            }
        }
        public int ScreenCount => screenStack.Count;
        public int PopupCount => popupStack.Count;

        /// <summary>UIRoot 初始化时调用；已有页面会被清空。</summary>
        internal void Configure(UIPageCatalog pageCatalog, Dictionary<UIPageLayer, RectTransform> layerRoots)
        {
            if (pageCache.Count > 0) CloseAll();

            catalog = pageCatalog;
            layers.Clear();

            if (layerRoots != null)
            {
                foreach (KeyValuePair<UIPageLayer, RectTransform> pair in layerRoots)
                    layers[pair.Key] = pair.Value;
            }

            if (catalog != null) catalog.Validate();
        }

        /// <summary>打开主页面；当前主页面会被隐藏并压入返回栈。</summary>
        public UIPageHandle ShowScreen(string pageId, UIPageArgs args = null)
        {
            if (!CanStartTransition()) return null;

            UIPageConfig config = FindConfig(pageId);
            if (CurrentScreen != null && CurrentScreen.PageId == pageId)
            {
                pageArgs[CurrentScreen] = args;
                CurrentScreen.Show(args);
                CurrentScreen.SetAlpha(1f);
                CurrentScreen.SetInteractable(true);
                return new UIPageHandle(this, CurrentScreen);
            }

            UIPage page = GetOrCreate(config);
            if (screenStack.Contains(page)) screenStack.Remove(page);
            if (CurrentScreen != null) HideImmediate(CurrentScreen);

            screenStack.Add(page);
            pageArgs[page] = args;
            BeginShow(page, config, args);
            return new UIPageHandle(this, page);
        }

        /// <summary>打开弹窗；不隐藏当前主页面。</summary>
        public UIPageHandle PushPopup(string pageId, UIPageArgs args = null)
        {
            if (!CanStartTransition()) return null;

            UIPageConfig config = FindConfig(pageId);
            UIPage page = GetOrCreate(config);

            if (popupStack.Contains(page))
            {
                pageArgs[page] = args;
                page.Show(args);
                page.SetAlpha(1f);
                page.SetInteractable(true);
                return new UIPageHandle(this, page);
            }

            if (config.Modal) CreateModalBlocker(page);
            popupStack.Add(page);
            pageArgs[page] = args;
            BeginShow(page, config, args);
            return new UIPageHandle(this, page);
        }

        /// <summary>返回：优先关弹窗，其次回退主页面。</summary>
        public bool Back()
        {
            if (IsTransitioning || !CanGoBack) return false;

            if (popupStack.Count > 0)
            {
                Close(CurrentPopup);
                return true;
            }

            Close(CurrentScreen);
            return true;
        }

        /// <summary>关闭指定页面；主页面关闭后自动恢复上一层。</summary>
        public void Close(UIPage page)
        {
            if (page == null || IsTransitioning) return;

            int popupIndex = popupStack.IndexOf(page);
            if (popupIndex >= 0)
            {
                popupStack.RemoveAt(popupIndex);
                RemoveModalBlocker(page);
                BeginHide(page);
                return;
            }

            int screenIndex = screenStack.IndexOf(page);
            if (screenIndex < 0) return;

            bool wasTop = screenIndex == screenStack.Count - 1;
            screenStack.RemoveAt(screenIndex);
            BeginHide(page);

            if (wasTop && screenStack.Count > 0) ShowExisting(CurrentScreen);
        }

        /// <summary>用新页面替换当前主页面。</summary>
        public UIPageHandle ReplaceScreen(string pageId, UIPageArgs args = null)
        {
            if (!CanStartTransition()) return null;

            UIPageConfig config = FindConfig(pageId);
            if (CurrentScreen != null && CurrentScreen.PageId == pageId)
            {
                pageArgs[CurrentScreen] = args;
                CurrentScreen.Show(args);
                CurrentScreen.SetAlpha(1f);
                CurrentScreen.SetInteractable(true);
                return new UIPageHandle(this, CurrentScreen);
            }

            UIPage page = GetOrCreate(config);

            if (screenStack.Contains(page)) screenStack.Remove(page);

            if (CurrentScreen != null)
            {
                UIPage previous = CurrentScreen;
                screenStack.Remove(previous);
                HideImmediate(previous);
            }

            screenStack.Add(page);
            pageArgs[page] = args;
            BeginShow(page, config, args);
            return new UIPageHandle(this, page);
        }

        /// <summary>关闭所有页面并清空缓存；通常用于切换场景。</summary>
        public void CloseAll()
        {
            StopAllCoroutines();
            IsTransitioning = false;

            var pages = new List<UIPage>(pageCache.Values);
            for (int i = 0; i < pages.Count; i++)
            {
                UIPage page = pages[i];
                if (page == null) continue;
                RemoveModalBlocker(page);
                page.Dispose();
                if (page != null) Destroy(page.gameObject);
            }

            screenStack.Clear();
            popupStack.Clear();
            pageCache.Clear();
            pageConfigs.Clear();
            pageArgs.Clear();
            modalBlockers.Clear();
        }

        private bool CanStartTransition()
        {
            if (catalog == null)
            {
                Debug.LogError("[UI] UIRoot 没有配置 UIPageCatalog");
                return false;
            }

            if (IsTransitioning)
            {
                Debug.LogWarning("[UI] 页面切换尚未完成，忽略本次请求");
                return false;
            }

            return true;
        }

        private UIPageConfig FindConfig(string pageId)
        {
            return catalog.Find(pageId);
        }

        private UIPage GetOrCreate(UIPageConfig config)
        {
            if (pageCache.TryGetValue(config.PageId, out UIPage cached) && cached != null)
                return cached;

            RectTransform layer = GetLayer(config.Layer);
            GameObject instance = Instantiate(config.Prefab, layer, false);
            instance.name = config.PageId;

            UIPage page = instance.GetComponent<UIPage>();
            if (page == null)
            {
                Destroy(instance);
                throw new System.InvalidOperationException($"[UI] 页面 {config.PageId} 的 prefab 缺少 UIPage");
            }

            page.Bind(config, this);
            page.Create();

            pageConfigs[page] = config;
            pageCache[config.PageId] = page;
            return page;
        }

        private RectTransform GetLayer(UIPageLayer layer)
        {
            if (layers.TryGetValue(layer, out RectTransform root) && root != null) return root;
            throw new System.InvalidOperationException($"[UI] 缺少 UI 层: {layer}");
        }

        private void BeginShow(UIPage page, UIPageConfig config, UIPageArgs args)
        {
            page.SetAlpha(0f);
            page.SetInteractable(false);
            page.Show(args);

            if (config.Transition != null)
            {
                StartCoroutine(ShowRoutine(page, config.Transition));
            }
            else
            {
                page.SetAlpha(1f);
                page.SetInteractable(true);
            }
        }

        private void BeginHide(UIPage page)
        {
            UIPageConfig config = GetConfig(page);
            if (config != null && config.Transition != null)
            {
                StartCoroutine(HideRoutine(page, config.Transition));
            }
            else
            {
                HideAndCleanup(page);
            }
        }

        private IEnumerator ShowRoutine(UIPage page, UIPageTransition transition)
        {
            IsTransitioning = true;
            yield return transition.Enter(page);
            page.SetAlpha(1f);
            page.SetInteractable(true);
            IsTransitioning = false;
        }

        private IEnumerator HideRoutine(UIPage page, UIPageTransition transition)
        {
            IsTransitioning = true;
            yield return transition.Exit(page);
            HideAndCleanup(page);
            IsTransitioning = false;
        }

        private void HideImmediate(UIPage page)
        {
            HideAndCleanup(page);
        }

        private void HideAndCleanup(UIPage page)
        {
            if (page == null) return;

            UIPageConfig config = GetConfig(page);
            page.Hide();

            if (config != null && config.CachePolicy == UIPageCachePolicy.DestroyOnHide)
            {
                pageCache.Remove(page.PageId);
                pageConfigs.Remove(page);
                pageArgs.Remove(page);
                Destroy(page.gameObject);
            }
        }

        private void ShowExisting(UIPage page)
        {
            if (page == null) return;

            pageArgs.TryGetValue(page, out UIPageArgs args);
            page.Show(args);
            page.SetAlpha(1f);
            page.SetInteractable(true);
        }

        private UIPageConfig GetConfig(UIPage page)
        {
            return pageConfigs.TryGetValue(page, out UIPageConfig config) ? config : null;
        }

        private void CreateModalBlocker(UIPage page)
        {
            var go = new GameObject("ModalBlocker", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            go.transform.SetParent(page.transform.parent, false);
            go.layer = page.gameObject.layer;

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = go.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.45f);
            image.raycastTarget = true;

            var group = go.GetComponent<CanvasGroup>();
            group.blocksRaycasts = true;
            group.interactable = true;

            go.transform.SetSiblingIndex(page.transform.GetSiblingIndex());
            modalBlockers[page] = go;
        }

        private void RemoveModalBlocker(UIPage page)
        {
            if (modalBlockers.TryGetValue(page, out GameObject blocker))
            {
                modalBlockers.Remove(page);
                if (blocker != null) Destroy(blocker);
            }
        }
    }
}

